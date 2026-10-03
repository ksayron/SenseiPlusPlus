using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Application;

public sealed class AssessmentUnavailableException(string message) : Exception(message);

public sealed class AssessmentService(ILearningStore store, ITransactionRunner tx, IAssessmentRoutePolicy policy, IAssessmentQueue queue, TimeProvider clock)
{
    private async Task<T> Required<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class =>
        (await store.Read(predicate, ct)).SingleOrDefault() ?? throw new ResourceNotFoundException();

    public async Task<AssessmentInput> InputAsync(Guid owner, Guid attemptId, CancellationToken ct)
    {
        var attempt = await Required<LearningAttempt>(x => x.OwnerId == owner && x.Id == attemptId, ct);
        var exercise = await Required<ExerciseVersion>(x => x.Id == attempt.ExerciseVersionId, ct);
        if (!exercise.Available || exercise.SchemaVersion != 1) throw new InvalidOperationException("The pinned exercise is unavailable.");
        if (exercise.MaterialVersionId is { } material && !(await Required<MaterialVersion>(x => x.Id == material, ct)).Available)
            throw new InvalidOperationException("The pinned material is unavailable.");
        var result = await Required<ExerciseResult>(x => x.OwnerId == owner && x.AttemptId == attemptId, ct);
        return new(attempt.Id, exercise.Id, exercise.PromptJson, exercise.InteractionJson, exercise.EvaluationJson,
            attempt.AnswerJson, attempt.Note, result.SelfReview, attempt.Assisted, attempt.AnswerAware);
    }

    public async Task<AssessmentPreview> PreviewAsync(Guid owner, Guid attempt, CancellationToken ct)
    {
        var input = await InputAsync(owner, attempt, ct);
        // Policies read trusted local operator decisions, never make network calls or infer approval from learner text.
        var route = await policy.ResolveAsync(owner, attempt, ct);
        if (route is null) return new(false, "Optional AI feedback is unavailable while provider configuration and qualification are deferred. Your saved attempt and existing feedback are available.", null, null);
        var now = clock.GetUtcNow();
        bool Fresh(DateTimeOffset time) => time <= now && time >= now.AddDays(-30);
        if (!Fresh(route.RouterPrivacyReviewedAt) || !Fresh(route.Route.PrivacyReviewedAt) ||
            route.RouterPrivacyPolicy.Scheme != "https" || route.Route.PrivacyPolicy.Scheme != "https")
            return new(false, "The approved privacy review must be renewed before requesting feedback.", null, null);
        var request = AssessmentTemplate.Request(input);
        if (request.ConservativeInputBytes > Math.Min(8000, route.Route.MaxInputBytes))
            return new(false, "This saved attempt exceeds the assessment input limit. The answer is still saved.", null, null);
        var disclosure = new AssessmentDisclosure(request.Task, request.WireSystemPrompt, input, request.Contract.Schema.GetRawText(),
            AssessmentTemplate.Version, request.Contract.Version, route.Route.Revision, route.Route.Digest,
            route.Route.ModelId, route.Route.ProviderName, route.Origin.ToString(), "PublicSyntheticOnly",
            route.RouterPrivacyPolicy.AbsoluteUri, route.RouterPrivacyReviewedAt, route.Route.PrivacyPolicy.AbsoluteUri, route.Route.PrivacyReviewedAt);
        return new(true, "Exploratory feedback does not change knowledge scores. Review all disclosed input and both recipients before requesting it.",
            AssessmentTemplate.Digest(ContentJson.Write(disclosure)), disclosure);
    }

    public Task<AssessmentJobView> RequestAsync(Guid owner, Guid attempt, AssessmentRequestCommand command, CancellationToken ct) => tx.RunAsync(async () =>
    {
        if (command.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.DisclosureDigest) || command.DisclosureDigest.Length != 64)
            throw new ArgumentException("An operation ID and exact disclosure digest are required.");
        await Required<LearningAttempt>(x => x.OwnerId == owner && x.Id == attempt, ct);
        await tx.LockAsync($"learning:operation:{owner}:{command.OperationId}", ct);
        var digest = AssessmentTemplate.Digest(ContentJson.Write(new { attempt, command.DisclosureDigest }));
        var receipt = (await store.Read<OperationReceipt>(x => x.OwnerId == owner && x.OperationId == command.OperationId, ct)).SingleOrDefault();
        if (receipt is not null)
        {
            if (receipt.Kind != "assessment-request" || receipt.TargetId != attempt || receipt.Digest != digest) throw new InvalidOperationException("Operation ID was already used for another command.");
            return (await GetAsync(owner, receipt.OutcomeId, ct)) with { Replayed = true };
        }
        await tx.LockAsync($"assessment:attempt:{owner}:{attempt}", ct);
        if ((await store.Read<AssessmentJob>(x => x.OwnerId == owner && x.AttemptId == attempt &&
                (x.State == AssessmentState.Queued || x.State == AssessmentState.Running || x.State == AssessmentState.Retry), ct)).Length != 0)
            throw new InvalidOperationException("This attempt already has an active assessment.");
        var preview = await PreviewAsync(owner, attempt, ct);
        if (!preview.Available) throw new AssessmentUnavailableException(preview.Reason);
        if (preview.Digest != command.DisclosureDigest) throw new InvalidOperationException("Assessment input or recipients changed. Review a fresh disclosure.");
        var disclosure = preview.Disclosure!;
        var route = await policy.ResolveAsync(owner, attempt, ct);
        if (route is null || route.Route.Digest != disclosure.RouteDigest || route.Origin.ToString() != disclosure.Origin ||
            route.RouterPrivacyPolicy.AbsoluteUri != disclosure.RouterPrivacyPolicy || route.RouterPrivacyReviewedAt != disclosure.RouterPrivacyReviewedAt)
            throw new InvalidOperationException("Assessment route changed. Review a fresh disclosure.");
        var now = clock.GetUtcNow();
        var job = new AssessmentJob { OwnerId = owner, AttemptId = attempt, ExerciseVersionId = disclosure.Input.ExerciseVersionId,
            OperationId = command.OperationId, CommandDigest = digest, InputJson = ContentJson.Write(disclosure.Input),
            InputDigest = AssessmentTemplate.Digest(ContentJson.Write(disclosure.Input)), RequestDigest = AssessmentTemplate.Request(disclosure.Input).Digest,
            RouteJson = ContentJson.Write(route.Route), RouteDigest = route.Route.Digest, DisclosureDigest = preview.Digest!, Origin = disclosure.Origin,
            CreatedAt = now, UpdatedAt = now, NotBefore = now };
        store.Add(job);
        store.Add(new OperationReceipt { OwnerId = owner, OperationId = command.OperationId, Kind = "assessment-request", TargetId = attempt,
            Digest = digest, OutcomeId = job.Id, ResponseVersion = job.Version });
        return new AssessmentJobView(job, null);
    }, ct);

    public async Task<AssessmentJobView> GetAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await Required<AssessmentJob>(x => x.OwnerId == owner && x.Id == id, ct);
        return new(job, (await store.Read<AssessmentFeedbackRevision>(x => x.OwnerId == owner && x.JobId == id, ct)).SingleOrDefault());
    }
    public async Task<AssessmentJobView[]> ListAsync(Guid owner, Guid attempt, CancellationToken ct)
    {
        await Required<LearningAttempt>(x => x.OwnerId == owner && x.Id == attempt, ct);
        var jobs = await store.Read<AssessmentJob>(x => x.OwnerId == owner && x.AttemptId == attempt, ct);
        var values = new List<AssessmentJobView>();
        foreach (var job in jobs.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)) values.Add(await GetAsync(owner, job.Id, ct));
        return values.ToArray();
    }
    public Task<AssessmentJobView> CancelAsync(Guid owner, Guid id, int version, Guid operation, CancellationToken ct) => tx.RunAsync(async () =>
    {
        if (operation == Guid.Empty) throw new ArgumentException("An operation ID is required.");
        await tx.LockAsync($"learning:operation:{owner}:{operation}", ct);
        var receipt = (await store.Read<OperationReceipt>(x => x.OwnerId == owner && x.OperationId == operation, ct)).SingleOrDefault();
        if (receipt is not null)
        {
            if (receipt.Kind != "assessment-cancel" || receipt.TargetId != id) throw new InvalidOperationException("Operation ID was already used for another command.");
            return (await GetAsync(owner, id, ct)) with { Replayed = true };
        }
        await queue.LockJobAsync(owner, id, ct);
        var view = await GetAsync(owner, id, ct);
        VersionPrecondition.RequireCurrent(view.Job.Version, version);
        view.Job.Cancel(clock.GetUtcNow());
        store.Add(new OperationReceipt { OwnerId = owner, OperationId = operation, Kind = "assessment-cancel", TargetId = id,
            OutcomeId = id, Digest = "cancel", ResponseVersion = view.Job.Version });
        return view;
    }, ct);
}
