using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Application.Models;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Learning.Infrastructure;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AssessmentIntegrationTests(PostgreSqlFixture fixture)
{
    private sealed class Clock : TimeProvider
    {
        private static int sequence;
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(Interlocked.Increment(ref sequence) * 3);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Gateway : IModelGateway
    {
        public required ModelRoute Route { get; init; }
        public int Calls;
        public Func<ModelRequest, CancellationToken, Task<ModelResult>>? Handler;
        public Task<ModelResult> CompleteAsync(ModelRequest request, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); return Handler?.Invoke(request, cancellationToken) ?? Task.FromResult(Result(request)); }
        public ModelResult Result(ModelRequest request, ModelFailure? failure = null) => new(failure is null ? Output(request) : null, failure,
            new(Route.Revision, "fixture", "fixture", "stop", new(10, 10, 0), TimeSpan.FromMilliseconds(1), ModelOrigin.Fixture,
                Route.Digest, request.Digest, request.Contract.Version));
        private static JsonElement Output(ModelRequest request) => JsonSerializer.SerializeToElement(new AssessmentOutput("sufficient",
            [new("reasoning", "supported", "The supplied synthetic note supports the reference.", "Synthetic reasoning.", [ContentJson.Read<AssessmentInput>(request.Input).ExerciseVersionId.ToString()]),
             new("communication", "not_assessed", "No communication claim is made.", null, [])], [], ["Compare a fresh example."]), ContentJson.Options);
    }
    private sealed class Policy : IAssessmentRoutePolicy
    {
        public required AssessmentRouteDecision Decision { get; set; }
        public HashSet<Guid> SyntheticOwners { get; } = [];
        public bool Enabled = true;
        public Task<AssessmentRouteDecision?> ResolveAsync(Guid owner, Guid attempt, CancellationToken ct) =>
            Task.FromResult(Enabled && SyntheticOwners.Contains(owner) ? Decision : null);
    }
    private sealed class Harness : IAsyncDisposable
    {
        public Clock Clock { get; } = new();
        public Policy Policy { get; }
        public Gateway Gateway { get; }
        public WebApplicationFactory<Program> Factory { get; }
        private readonly PostgreSqlFixture fixture;
        public Harness(PostgreSqlFixture fixture)
        {
            this.fixture = fixture;
            var route = new ModelRoute("synthetic-test-v1", "fixture/model:free", "fixture", "fixture", ModelOutputMode.ValidatedJson,
                new Uri("https://example.test/provider-privacy"), Clock.Now);
            Gateway = new() { Route = route };
            Policy = new() { Decision = new(route, Gateway, ModelOrigin.Fixture, new Uri("https://example.test/router-privacy"), Clock.Now) };
            Factory = Restart();
        }
        public WebApplicationFactory<Program> Restart() => fixture.CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            foreach (var d in s.Where(x => x.ImplementationType?.Name == "AssessmentWorker").ToArray()) s.Remove(d);
            s.AddSingleton<TimeProvider>(Clock); s.AddSingleton<IAssessmentRoutePolicy>(Policy);
        }));
        public async Task<T> Scope<T>(Func<IServiceProvider, Task<T>> work, WebApplicationFactory<Program>? factory = null)
        { await using var scope = (factory ?? Factory).Services.CreateAsyncScope(); return await work(scope.ServiceProvider); }
        public Task<T> Service<T>(Func<AssessmentService, Task<T>> work) => Scope(p => work(p.GetRequiredService<AssessmentService>()));
        public Task<T> Queue<T>(Func<IAssessmentQueue, Task<T>> work, WebApplicationFactory<Program>? factory = null) => Scope(p => work(p.GetRequiredService<IAssessmentQueue>()), factory);
        public Task<T> Db<T>(Func<SenseiDbContext, Task<T>> work) => Scope(p => work(p.GetRequiredService<SenseiDbContext>()));
        public async Task<(Guid Owner, Guid Attempt, Guid Exercise)> Saved(Guid? ownerId = null, string note = "Synthetic reasoning.")
        {
            return await Scope(async p =>
            {
                await p.GetRequiredService<LearningFixtures>().Seed();
                var db = p.GetRequiredService<SenseiDbContext>();
                var owner = ownerId;
                if (owner is null)
                {
                    var account = UserAccount.Create($"synthetic-{Guid.NewGuid():N}@example.test", "Synthetic test profile", "en", "en", "UTC", Clock.Now);
                    db.Add(account); owner = account.Id;
                }
                Policy.SyntheticOwners.Add(owner.Value);
                var original = await db.Set<ExerciseVersion>().FirstAsync(x => x.Available && x.Type == ExerciseType.MultipleChoice);
                var exercise = new ExerciseVersion { FamilyId = original.FamilyId, PrimaryConceptId = original.PrimaryConceptId,
                    Type = original.Type, PromptJson = original.PromptJson, InteractionJson = original.InteractionJson, EvaluationJson = original.EvaluationJson };
                var session = new LearningSession { OwnerId = owner.Value, RequestedCount = 3, ActualCount = 1, State = SessionState.Completed, CreatedAt = Clock.Now, UpdatedAt = Clock.Now };
                var item = new SessionItem { OwnerId = owner.Value, SessionId = session.Id, ExerciseVersionId = exercise.Id, Outcome = ItemOutcome.Answered };
                var attempt = new LearningAttempt { OwnerId = owner.Value, SessionId = session.Id, ItemId = item.Id, ExerciseVersionId = exercise.Id,
                    AnswerJson = ContentJson.Write(ContentJson.Read<EvaluationDefinition>(exercise.EvaluationJson).Accepted[0]), Note = note, ReceivedAt = Clock.Now };
                db.AddRange(exercise, session, item, attempt, new ExerciseResult { OwnerId = owner.Value, AttemptId = attempt.Id, Score = 1 });
                await db.CommitAsync(default); return (owner.Value, attempt.Id, exercise.Id);
            });
        }
        public async Task<AssessmentJob> Request(Guid owner, Guid attempt, Guid? operation = null)
        {
            var preview = await Service(s => s.PreviewAsync(owner, attempt, default)); Assert.True(preview.Available, preview.Reason);
            return (await Service(s => s.RequestAsync(owner, attempt, new(operation ?? Guid.NewGuid(), preview.Digest!), default))).Job;
        }
        public ModelRequest RequestOf(AssessmentJob job) => AssessmentTemplate.Request(ContentJson.Read<AssessmentInput>(job.InputJson));
        public Task<bool> Process(WebApplicationFactory<Program>? factory = null) => new AssessmentProcessor((factory ?? Factory).Services.GetRequiredService<IServiceScopeFactory>(), Clock).ProcessOneAsync(default);
        public async ValueTask DisposeAsync()
        {
            var owners = Policy.SyntheticOwners.ToArray();
            await Db(async db => await db.Set<AssessmentJob>().Where(x => owners.Contains(x.OwnerId) && (x.State == AssessmentState.Running || x.State == AssessmentState.Queued || x.State == AssessmentState.Retry))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, AssessmentState.Cancelled).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.Generation, x => x.Generation + 1).SetProperty(x => x.Fence, x => x.Fence + 1)));
            Factory.Dispose();
        }
    }

    [Fact]
    public void Output_contract_requires_grounding_exact_criteria_and_visible_insufficient_context()
    {
        var input = new AssessmentInput(Guid.NewGuid(), Guid.NewGuid(), "[]", "{}", "{}", "null", "Synthetic reasoning.", null, false, false);
        var contract = new AssessmentContract(input);
        var reasoning = new CriterionFeedback("reasoning", "supported", "Bounded explanation", input.Note, [input.ExerciseVersionId.ToString()]);
        var communication = new CriterionFeedback("communication", "not_assessed", "No claim", null, []);
        var valid = new AssessmentOutput("sufficient", [reasoning, communication], [], []);
        JsonElement Json(AssessmentOutput value) => JsonSerializer.SerializeToElement(value, ContentJson.Options);
        Assert.True(contract.Accepts(Json(valid)));
        Assert.False(contract.Accepts(Json(valid with { Criteria = [reasoning with { AnswerQuote = "Invented answer" }, communication] })));
        Assert.False(contract.Accepts(Json(valid with { Criteria = [reasoning with { ReferenceIds = [Guid.NewGuid().ToString()] }, communication] })));
        Assert.False(contract.Accepts(Json(valid with { Criteria = [reasoning, reasoning] })));
        Assert.False(contract.Accepts(Json(valid with { ContextSufficiency = "insufficient" })));
        Assert.True(contract.Accepts(Json(valid with { ContextSufficiency = "insufficient", MissingAssumptions = ["The prompt lacks the needed condition."],
            Criteria = [reasoning with { Judgment = "insufficient_context", AnswerQuote = null, ReferenceIds = [] }, communication] })));
    }

    [Fact]
    public async Task Default_policy_keeps_saved_attempt_available_and_cannot_queue_or_call_a_provider()
    {
        await using var h = new Harness(fixture); var source = await h.Saved(); h.Policy.Enabled = false;
        using var client = h.Factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Owner-Id", source.Owner.ToString());
        var prefix = $"/api/v1/learning/attempts/{source.Attempt}";
        var preview = await client.PostAsJsonAsync(prefix + "/assessment-preview", new { });
        Assert.False((await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(prefix + "/assessments", new { operationId = Guid.NewGuid(), disclosureDigest = new string('a', 64) })).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>(prefix)).GetProperty("score").GetInt32());
        Assert.Empty(await h.Db(db => db.Set<AssessmentJob>().Where(x => x.OwnerId == source.Owner).ToArrayAsync())); Assert.Equal(0, h.Gateway.Calls);
    }

    [Fact]
    public async Task Full_wire_prompt_is_disclosed_and_oversize_envelope_is_rejected_before_queueing()
    {
        await using var h = new Harness(fixture); var small = await h.Saved();
        var preview = await h.Service(x => x.PreviewAsync(small.Owner, small.Attempt, default));
        Assert.Contains(ModelRequest.JsonOutputInstruction, preview.Disclosure!.SystemPrompt);
        Assert.EndsWith(preview.Disclosure.Schema, preview.Disclosure.SystemPrompt);
        var large = await h.Saved(note: new string('x', 8000));
        var rejected = await h.Service(x => x.PreviewAsync(large.Owner, large.Attempt, default));
        Assert.False(rejected.Available); Assert.Contains("input limit", rejected.Reason);
        await Assert.ThrowsAsync<AssessmentUnavailableException>(() => h.Service(x => x.RequestAsync(large.Owner, large.Attempt, new(Guid.NewGuid(), new string('a', 64)), default)));
        Assert.Equal(1, (await h.Db(db => db.Set<ExerciseResult>().SingleAsync(x => x.AttemptId == large.Attempt))).Score);
    }

    [Fact]
    public async Task Request_replays_before_route_change_and_duplicate_completion_creates_one_exploratory_revision()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var op = Guid.NewGuid(); var j = await h.Request(s.Owner, s.Attempt, op);
        h.Policy.Enabled = false;
        var replay = await h.Service(x => x.RequestAsync(s.Owner, s.Attempt, new(op, j.DisclosureDigest), default)); Assert.True(replay.Replayed); Assert.Equal(j.Id, replay.Job.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service(x => x.RequestAsync(s.Owner, s.Attempt, new(op, new string('b', 64)), default)));
        h.Policy.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Request(s.Owner, s.Attempt));
        var lease = (await h.Queue(x => x.ClaimAsync(default)))!; var invocation = (await h.Queue(x => x.ReserveInvocationAsync(lease, default)))!.Value;
        var result = h.Gateway.Result(h.RequestOf(j));
        for (var i = 0; i < 2; i++) await h.Queue(async x => { await x.CompleteAsync(lease, invocation, result, default); return true; });
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); Assert.Equal(AssessmentState.Succeeded, view.Job.State);
        Assert.Equal("Exploratory", view.Feedback!.Scope); Assert.Equal("Fixture", view.Feedback.Origin);
        Assert.Single(await h.Db(db => db.Set<AssessmentFeedbackRevision>().Where(x => x.JobId == j.Id).ToArrayAsync()));
        Assert.Equal(1, (await h.Db(db => db.Set<ExerciseResult>().SingleAsync(x => x.AttemptId == s.Attempt))).Score);
        Assert.Equal(0, await h.Db(db => db.Set<Sensei.Modules.Evidence.Domain.EvidenceObservation>().CountAsync(x => x.OwnerId == s.Owner)));
    }

    [Fact]
    public async Task Restart_recovers_expired_lease_and_fences_out_the_original_invocation()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        var old = (await h.Queue(x => x.ClaimAsync(default)))!; var oldInvocation = (await h.Queue(x => x.ReserveInvocationAsync(old, default)))!.Value;
        h.Clock.Now = h.Clock.Now.AddSeconds(91);
        using var restarted = h.Restart(); Assert.Null(await h.Queue(x => x.ClaimAsync(default), restarted));
        Assert.False(await h.Queue(x => x.HeartbeatAsync(old, default), restarted));
        h.Clock.Now = h.Clock.Now.AddSeconds(31); var current = (await h.Queue(x => x.ClaimAsync(default), restarted))!;
        Assert.True(current.Fence > old.Fence);
        var next = (await h.Queue(x => x.ReserveInvocationAsync(current, default), restarted))!.Value;
        var output = h.Gateway.Result(h.RequestOf(j));
        await h.Queue(async x => { await x.CompleteAsync(old, oldInvocation, output, default); return true; }, restarted);
        Assert.Null((await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Feedback);
        await h.Queue(async x => { await x.CompleteAsync(current, next, output, default); return true; }, restarted);
        Assert.Equal(AssessmentState.Succeeded, (await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Job.State);
        Assert.Equal("LateDiscarded", (await h.Db(db => db.Set<AssessmentInvocation>().SingleAsync(x => x.Id == oldInvocation))).FailureCode);
    }

    [Fact]
    public async Task Cancellation_commits_while_gateway_is_blocked_and_late_response_cannot_write_feedback()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Gateway.Handler = async (request, _) => { entered.SetResult(); await release.Task; return h.Gateway.Result(request); };
        var work = h.Process(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); var operation = Guid.NewGuid();
        try
        {
            var cancelled = await h.Service(x => x.CancelAsync(s.Owner, j.Id, view.Job.Version, operation, default)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AssessmentState.Cancelled, cancelled.Job.State);
            Assert.True((await h.Service(x => x.CancelAsync(s.Owner, j.Id, view.Job.Version, operation, default))).Replayed);
        }
        finally { release.SetResult(); }
        await work;
        Assert.Null((await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Feedback);
    }

    [Fact]
    public async Task Concurrent_claims_only_dispatch_one_account_wide_invocation_and_next_owner_is_fairly_selected()
    {
        await using var h = new Harness(fixture); var one = await h.Saved(); var two = await h.Saved();
        var j1 = await h.Request(one.Owner, one.Attempt); var j2 = await h.Request(two.Owner, two.Attempt);
        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => h.Queue(x => x.ClaimAsync(default))));
        var lease = Assert.Single(claims, x => x is not null)!;
        var invocation = (await h.Queue(x => x.ReserveInvocationAsync(lease, default)))!.Value;
        await h.Queue(async x => { await x.CompleteAsync(lease, invocation, h.Gateway.Result(h.RequestOf(lease.Job)), default); return true; });
        var another = await h.Saved(lease.Job.OwnerId); await h.Request(another.Owner, another.Attempt);
        var next = (await h.Queue(x => x.ClaimAsync(default)))!; Assert.NotEqual(lease.Job.OwnerId, next.Job.OwnerId);
        Assert.Contains(next.Job.Id, new[] { j1.Id, j2.Id });
    }

    [Fact]
    public async Task Source_withdrawal_during_invocation_is_obsolete_and_input_and_feedback_are_database_immutable()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        var lease = (await h.Queue(x => x.ClaimAsync(default)))!; var invocation = (await h.Queue(x => x.ReserveInvocationAsync(lease, default)))!.Value;
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => h.Db(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE learning.assessment_jobs SET input_digest = 'forged' WHERE id = {j.Id}")));
        await h.Db(db => db.Set<ExerciseVersion>().Where(x => x.Id == s.Exercise).ExecuteUpdateAsync(x => x.SetProperty(e => e.Available, false)));
        await h.Queue(async x => { await x.CompleteAsync(lease, invocation, h.Gateway.Result(h.RequestOf(j)), default); return true; });
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); Assert.Equal(AssessmentState.Obsolete, view.Job.State); Assert.Null(view.Feedback);
        var second = await h.Saved(); var successful = await h.Request(second.Owner, second.Attempt); await h.Process();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => h.Db(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE learning.assessment_feedback_revisions SET scope = 'Graded' WHERE job_id = {successful.Id}")));
    }

    [Fact]
    public async Task Changed_recipient_review_or_revoked_policy_prevents_any_invocation()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        h.Policy.Decision = h.Policy.Decision with { RouterPrivacyReviewedAt = h.Clock.Now.AddMinutes(-1) };
        Assert.True(await h.Process()); Assert.Equal(0, h.Gateway.Calls);
        Assert.Equal(AssessmentState.Obsolete, (await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Job.State);
        var other = await h.Saved(); var another = await h.Request(other.Owner, other.Attempt); h.Policy.Enabled = false;
        await h.Process(); Assert.Equal(AssessmentState.Obsolete, (await h.Service(x => x.GetAsync(other.Owner, another.Id, default))).Job.State);
    }

    [Fact]
    public async Task Recipient_change_after_gateway_response_preserves_audit_but_discards_feedback()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        h.Gateway.Handler = (r, _) => { h.Policy.Enabled = false; return Task.FromResult(h.Gateway.Result(r)); };
        await h.Process();
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); Assert.Equal(AssessmentState.Obsolete, view.Job.State); Assert.Null(view.Feedback);
        var invocation = await h.Db(db => db.Set<AssessmentInvocation>().SingleAsync(x => x.JobId == j.Id));
        Assert.NotNull(invocation.FinishedAt); Assert.Equal("InputOrRouteChanged", invocation.FailureCode); Assert.Contains("fixture", invocation.AuditJson);
    }

    [Fact]
    public async Task Transient_failure_and_one_bounded_regeneration_are_limited_to_three_invocations()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        var failures = new Queue<ModelFailure>([ModelFailure.Timeout, ModelFailure.InvalidOutput, ModelFailure.ProviderUnavailable]);
        h.Gateway.Handler = (r, _) => Task.FromResult(h.Gateway.Result(r, failures.Dequeue()));
        for (var i = 0; i < 3; i++) { Assert.True(await h.Process()); h.Clock.Now = h.Clock.Now.AddSeconds(31); }
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); Assert.Equal(AssessmentState.Failed, view.Job.State); Assert.Null(view.Feedback);
        Assert.Equal(3, view.Job.InvocationCount); Assert.Equal(1, view.Job.Repairs); Assert.Equal(1, view.Job.TransientRetries); Assert.False(await h.Process());
    }

    [Fact]
    public async Task Invalid_semantics_are_rejected_even_when_gateway_reports_success()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        h.Gateway.Handler = (r, _) => Task.FromResult(h.Gateway.Result(r) with { Output = JsonSerializer.SerializeToElement(new { fabricatedScore = 100 }) });
        await h.Process(); h.Clock.Now = h.Clock.Now.AddSeconds(6); await h.Process();
        var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default)); Assert.Equal(AssessmentState.Failed, view.Job.State); Assert.Equal(2, view.Job.InvocationCount); Assert.Null(view.Feedback);
    }

    [Fact]
    public async Task Nonzero_cost_is_a_permanent_failure_even_if_output_also_needs_repair()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        h.Gateway.Handler = (r, _) =>
        {
            var result = h.Gateway.Result(r);
            return Task.FromResult(result with { Output = JsonSerializer.SerializeToElement(new { invented = true }), Audit = result.Audit with { Usage = new(10, 10, 1) } });
        };
        await h.Process(); var view = await h.Service(x => x.GetAsync(s.Owner, j.Id, default));
        Assert.Equal(AssessmentState.Failed, view.Job.State); Assert.Equal("NonzeroPrice", view.Job.FailureCode);
        Assert.Equal(0, view.Job.Repairs); Assert.Equal(1, view.Job.InvocationCount); Assert.Null(view.Feedback);
    }

    [Fact]
    public async Task Retry_after_is_respected_and_quota_pause_does_not_consume_semantic_retry_allowances()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        h.Gateway.Handler = (r, _) => Task.FromResult(h.Gateway.Result(r, ModelFailure.RateLimited) with { RetryAfter = TimeSpan.FromMinutes(7) });
        await h.Process(); var retry = (await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Job;
        Assert.Equal(h.Clock.Now.AddMinutes(7), retry.NotBefore); Assert.Equal(0, retry.TransientRetries); Assert.Equal(0, retry.Repairs);
        h.Clock.Now = h.Clock.Now.AddMinutes(6); Assert.False(await h.Process());
    }

    [Fact]
    public async Task Local_budget_counts_unknown_crash_outcomes_and_pauses_without_creating_an_invocation()
    {
        await using var h = new Harness(fixture); var s = await h.Saved(); var j = await h.Request(s.Owner, s.Attempt);
        await h.Db(async db =>
        {
            // Simulate reservations belonging to completed jobs elsewhere in the same local account budget.
            for (var i = 0; i < 20; i++)
            {
                var terminal = new AssessmentJob { OwnerId = s.Owner, AttemptId = s.Attempt, ExerciseVersionId = s.Exercise, OperationId = Guid.NewGuid(), State = AssessmentState.Failed,
                    CreatedAt = h.Clock.Now, UpdatedAt = h.Clock.Now, NotBefore = h.Clock.Now };
                db.Add(terminal); db.Add(new AssessmentInvocation { OwnerId = s.Owner, JobId = terminal.Id, Number = 1, StartedAt = h.Clock.Now });
            }
            await db.CommitAsync(default); return true;
        });
        await h.Process(); var paused = (await h.Service(x => x.GetAsync(s.Owner, j.Id, default))).Job;
        Assert.Equal(AssessmentState.Retry, paused.State); Assert.Equal("LocalQuotaPaused", paused.FailureCode); Assert.Equal(0, paused.InvocationCount); Assert.Equal(0, h.Gateway.Calls);
    }

    [Fact]
    public async Task Real_cookie_owner_isolation_csrf_and_etag_apply_to_jobs_and_input_preview()
    {
        await using var h = new Harness(fixture);
        using var client = h.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.GetProperty("token").GetString());
        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = $"job-cookie-{Guid.NewGuid():N}@example.test", password = "Synthetic-Test-Password1!", displayName = "Synthetic profile" });
        var owner = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        var s = await h.Saved(owner); var job = await h.Request(s.Owner, s.Attempt);
        var root = $"/api/v1/learning/assessments/{job.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/learning/attempts/{s.Attempt}/assessment-preview", new { })).StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.GetProperty("token").GetString());
        using var other = h.Factory.CreateClient(); other.DefaultRequestHeaders.Add("X-Owner-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(root)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/learning/attempts/{s.Attempt}/assessment-preview", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await client.PostAsJsonAsync(root + "/cancel", new { operationId = Guid.NewGuid() })).StatusCode);
        var get = await client.GetAsync(root); Assert.NotNull(get.Headers.ETag);
        using var cancel = new HttpRequestMessage(HttpMethod.Post, root + "/cancel") { Content = JsonContent.Create(new { operationId = Guid.NewGuid() }) };
        cancel.Headers.IfMatch.Add(get.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(cancel)).StatusCode);
        var spoofed = await client.PostAsJsonAsync($"/api/v1/learning/attempts/{s.Attempt}/assessments", new { operationId = Guid.NewGuid(), disclosureDigest = job.DisclosureDigest, origin = "Provider" });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
    }
}
