using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Application.Models;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Infrastructure;

/// <summary>All locks and leases are committed before the worker calls the gateway.</summary>
public sealed class PostgresAssessmentQueue(SenseiDbContext db, ITransactionRunner tx, TimeProvider clock) : IAssessmentQueue
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(90);
    public async Task<AssessmentJob?> LockJobAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A row lock requires a transaction.");
        return (await db.Set<AssessmentJob>().FromSqlInterpolated($"SELECT * FROM learning.assessment_jobs WHERE owner_id = {owner} AND id = {id} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault();
    }
    public Task<AssessmentLease?> ClaimAsync(CancellationToken ct) => tx.RunAsync<AssessmentLease?>(async () =>
    {
        await tx.LockAsync("assessment:dispatch", ct);
        var now = clock.GetUtcNow();
        var expired = await db.Set<AssessmentJob>().FromSqlInterpolated($"SELECT * FROM learning.assessment_jobs WHERE state = 'Running' AND lease_until <= {now} FOR UPDATE SKIP LOCKED").ToArrayAsync(ct);
        foreach (var job in expired)
        {
            job.Fence++; job.LeaseUntil = null; job.UpdatedAt = now; job.Version++; job.FailureCode = "LeaseExpired";
            if (job.InvocationCount == 0) { job.State = AssessmentState.Retry; job.NotBefore = now; }
            else if (job.TransientRetries < 1 && job.InvocationCount < 3)
            { job.TransientRetries++; job.State = AssessmentState.Retry; job.NotBefore = now.AddSeconds(30); }
            else job.State = AssessmentState.Failed;
        }
        await db.CommitAsync(ct); // Make recovered states visible to the following SQL in this short transaction.
        if (await db.Set<AssessmentJob>().AnyAsync(x => x.State == AssessmentState.Running && x.LeaseUntil > now, ct)) return null;
        var candidate = (await db.Set<AssessmentJob>().FromSqlInterpolated($"""
            SELECT j.* FROM learning.assessment_jobs j
            WHERE j.state IN ('Queued','Retry') AND j.not_before <= {now}
            ORDER BY COALESCE((SELECT max(i.started_at) FROM learning.assessment_invocations i WHERE i.owner_id = j.owner_id), '-infinity'::timestamptz), j.created_at, j.id
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct)).SingleOrDefault();
        if (candidate is null) return null;
        candidate.State = AssessmentState.Running; candidate.Fence++; candidate.LeaseUntil = now + LeaseDuration;
        candidate.UpdatedAt = now; candidate.Version++;
        return new(candidate, candidate.Fence, candidate.Generation);
    }, ct);

    public Task<Guid?> ReserveInvocationAsync(AssessmentLease lease, CancellationToken ct) => tx.RunAsync<Guid?>(async () =>
    {
        await tx.LockAsync("assessment:quota", ct);
        var job = await LockJobAsync(lease.Job.OwnerId, lease.Job.Id, ct);
        var now = clock.GetUtcNow();
        if (job is null || !job.Owns(lease.Fence, lease.Generation, now)) return null;
        if (job.InvocationCount >= 3) { Terminal(job, AssessmentState.Failed, "InvocationLimit", now); return null; }
        // Account-wide conservative reservations include timeouts and unknown outcomes after crashes.
        // This local budget cannot see requests from other apps sharing a future provider key.
        var day = now.AddHours(-24); var minute = now.AddMinutes(-1);
        var recent = await db.Set<AssessmentInvocation>().Where(x => x.StartedAt > day).Select(x => x.StartedAt).ToArrayAsync(ct);
        if (recent.Length >= 40 || recent.Count(x => x > minute) >= 20)
        {
            job.State = AssessmentState.Retry; job.LeaseUntil = null; job.FailureCode = "LocalQuotaPaused";
            job.NotBefore = recent.Length >= 40 ? recent.Order().First().AddHours(24).AddSeconds(1) : recent.Where(x => x > minute).Order().First().AddMinutes(1).AddSeconds(1);
            job.UpdatedAt = now; job.Version++; return null;
        }
        var invocation = new AssessmentInvocation { OwnerId = job.OwnerId, JobId = job.Id, Number = ++job.InvocationCount,
            Fence = lease.Fence, Generation = lease.Generation, RequestDigest = job.RequestDigest, StartedAt = now };
        db.Add(invocation); job.FailureCode = null; job.UpdatedAt = now; job.Version++;
        return invocation.Id;
    }, ct);

    public Task<bool> HeartbeatAsync(AssessmentLease lease, CancellationToken ct) => tx.RunAsync(async () =>
    {
        var job = await LockJobAsync(lease.Job.OwnerId, lease.Job.Id, ct); var now = clock.GetUtcNow();
        if (job is null || !job.Owns(lease.Fence, lease.Generation, now)) return false;
        job.LeaseUntil = now + LeaseDuration; return true;
    }, ct);

    public Task ObsoleteAsync(AssessmentLease lease, string code, CancellationToken ct) => tx.RunAsync(async () =>
    {
        var job = await LockJobAsync(lease.Job.OwnerId, lease.Job.Id, ct); var now = clock.GetUtcNow();
        if (job is not null && job.Owns(lease.Fence, lease.Generation, now)) Terminal(job, AssessmentState.Obsolete, code, now);
        return true;
    }, ct);

    public Task CompleteAsync(AssessmentLease lease, Guid invocationId, ModelResult result, CancellationToken ct, bool routeCurrent = true) => tx.RunAsync(async () =>
    {
        var job = await LockJobAsync(lease.Job.OwnerId, lease.Job.Id, ct); var now = clock.GetUtcNow();
        if (job is null) return false;
        var invocation = await db.Set<AssessmentInvocation>().SingleAsync(x => x.OwnerId == job.OwnerId && x.JobId == job.Id && x.Id == invocationId, ct);
        if (invocation.FinishedAt is not null) return false; // Duplicate deliveries do not rewrite results or audit records.
        invocation.FinishedAt = now;
        if (!job.Owns(lease.Fence, lease.Generation, now) || invocation.Fence != lease.Fence || invocation.Generation != lease.Generation)
        { invocation.FailureCode = "LateDiscarded"; return false; }
        var route = ContentJson.Read<ModelRoute>(job.RouteJson);
        var input = ContentJson.Read<AssessmentInput>(job.InputJson);
        var identity = result.Audit.Origin.ToString() == job.Origin && result.Audit.RouteRevision == route.Revision &&
            result.Audit.RouteDigest == job.RouteDigest && result.Audit.RequestDigest == job.RequestDigest &&
            result.Audit.OutputContractVersion == job.ContractVersion &&
            (result.Audit.Origin == ModelOrigin.Fixture || result.Audit.ActualProvider == route.ProviderName &&
                (result.Audit.ActualModel == route.ModelId || route.ModelId.EndsWith(":free", StringComparison.Ordinal) && result.Audit.ActualModel == route.ModelId[..^5]));
        // Persist only known identities and bounded safe metadata, never a raw provider body or exception.
        invocation.AuditJson = ContentJson.Write(new { route.Revision, routeDigest = job.RouteDigest, requestDigest = job.RequestDigest,
            origin = job.Origin, actualModel = identity ? result.Audit.ActualModel : null, actualProvider = identity ? result.Audit.ActualProvider : null,
            result.Audit.Usage, elapsedMilliseconds = Math.Clamp(result.Audit.Elapsed.TotalMilliseconds, 0, 3600000),
            finishReason = result.Audit.FinishReason is "stop" or "length" or "content_filter" or "tool_calls" or "error" ? result.Audit.FinishReason : null });
        if (!routeCurrent) { invocation.FailureCode = "InputOrRouteChanged"; Terminal(job, AssessmentState.Obsolete, "InputOrRouteChanged", now); return false; }
        if (!await SourceCurrent(job, input, ct)) { invocation.FailureCode = "SourceUnavailable"; Terminal(job, AssessmentState.Obsolete, "SourceUnavailable", now); return false; }
        var failure = result.Failure;
        if (result.Succeeded && (!identity || result.Output is not { } output || !new AssessmentContract(input).Accepts(output)))
            failure = identity ? ModelFailure.InvalidOutput : ModelFailure.IdentityMismatch;
        if (!result.Succeeded && failure is null) failure = ModelFailure.InvalidOutput;
        if (result.Audit.Usage.Cost is { } cost && cost != 0) failure = ModelFailure.NonzeroPrice;
        if (failure is { } error)
        {
            invocation.FailureCode = error.ToString(); job.FailureCode = error.ToString(); job.LeaseUntil = null; job.UpdatedAt = now; job.Version++;
            if (error is ModelFailure.RateLimited or ModelFailure.QuotaExhausted)
            {
                job.State = AssessmentState.Retry;
                var minimum = TimeSpan.FromMinutes(error == ModelFailure.QuotaExhausted ? 60 : 1);
                var delay = result.RetryAfter > minimum ? result.RetryAfter.Value : minimum;
                job.NotBefore = delay < DateTimeOffset.MaxValue - now ? now + delay : DateTimeOffset.MaxValue;
            }
            else if (error is ModelFailure.Timeout or ModelFailure.ProviderUnavailable or ModelFailure.MetadataUnavailable && job.TransientRetries < 1 && job.InvocationCount < 3)
            { job.TransientRetries++; job.State = AssessmentState.Retry; job.NotBefore = now.AddSeconds(30); }
            else if (error is ModelFailure.InvalidOutput or ModelFailure.Malformed or ModelFailure.Truncated && job.Repairs < 1 && job.InvocationCount < 3)
            { job.Repairs++; job.State = AssessmentState.Retry; job.NotBefore = now.AddSeconds(5); }
            else job.State = AssessmentState.Failed;
            if (job.InvocationCount >= 3) job.State = AssessmentState.Failed;
            return false;
        }
        var revision = await db.Set<AssessmentFeedbackRevision>().Where(x => x.OwnerId == job.OwnerId && x.AttemptId == job.AttemptId).MaxAsync(x => (int?)x.Revision, ct) ?? 0;
        db.Add(new AssessmentFeedbackRevision { OwnerId = job.OwnerId, AttemptId = job.AttemptId, JobId = job.Id, InvocationId = invocation.Id,
            ExerciseVersionId = job.ExerciseVersionId, Revision = revision + 1, Origin = job.Origin, InputDigest = job.InputDigest,
            RouteDigest = job.RouteDigest, TemplateVersion = job.TemplateVersion, ContractVersion = job.ContractVersion,
            OutputJson = result.Output!.Value.GetRawText(), AuditJson = invocation.AuditJson, CreatedAt = now });
        Terminal(job, AssessmentState.Succeeded, null, now);
        // No knowledge observation is staged: the library has no reviewed AI rubric yet.
        return true;
    }, ct);

    private async Task<bool> SourceCurrent(AssessmentJob job, AssessmentInput input, CancellationToken ct)
    {
        var attempt = await db.Set<LearningAttempt>().SingleOrDefaultAsync(x => x.OwnerId == job.OwnerId && x.Id == job.AttemptId, ct);
        var exercise = (await db.Set<ExerciseVersion>().FromSqlInterpolated($"SELECT * FROM learning.exercise_versions WHERE id = {job.ExerciseVersionId} FOR SHARE").ToArrayAsync(ct)).SingleOrDefault();
        if (attempt is null || exercise is not { Available: true, SchemaVersion: 1 }) return false;
        if (exercise.MaterialVersionId is { } material && !(await db.Set<MaterialVersion>().FromSqlInterpolated($"SELECT * FROM learning.material_versions WHERE id = {material} FOR SHARE").ToArrayAsync(ct)).Any(x => x.Available)) return false;
        var result = await db.Set<ExerciseResult>().SingleOrDefaultAsync(x => x.OwnerId == job.OwnerId && x.AttemptId == job.AttemptId, ct);
        if (result is null) return false;
        var current = new AssessmentInput(attempt.Id, exercise.Id, exercise.PromptJson, exercise.InteractionJson, exercise.EvaluationJson,
            attempt.AnswerJson, attempt.Note, result.SelfReview, attempt.Assisted, attempt.AnswerAware);
        return job.InputDigest == AssessmentTemplate.Digest(ContentJson.Write(current)) && job.InputDigest == AssessmentTemplate.Digest(ContentJson.Write(input));
    }
    private static void Terminal(AssessmentJob job, AssessmentState state, string? code, DateTimeOffset now)
    { job.State = state; job.FailureCode = code; job.LeaseUntil = null; job.UpdatedAt = now; job.Version++; }
}
