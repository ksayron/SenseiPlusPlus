using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Sensei.Modules.Evidence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Evidence.Domain;
using Sensei.BuildingBlocks.Application;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class LearningIntegrationTests(PostgreSqlFixture fixture)
{
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Clock_controlled_reviews_keep_original_admissions_after_withdrawal_and_restart()
    {
        var clock = new TestClock();
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = await Client(factory);
        var owner = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single());
        var concept = await Concept("value-reference-fixture");
        var objective = await Start(client, ExerciseType.MultipleChoice);
        await Body(await Command(client, $"sessions/{Id(objective)}/items/{Id(Item(objective))}/attempts", new ItemCommand(Guid.NewGuid(), new Answer(["c"])), Token(objective)));
        async Task Review(string rating)
        {
            var session = await Start(client, ExerciseType.Flashcard);
            var path = $"sessions/{Id(session)}/items/{Id(Item(session))}";
            session = (await Body(await Command(client, path + "/assistance", new ItemCommand(Guid.NewGuid(), Assistance: "Reveal"), Token(session)))).GetProperty("session");
            await Body(await Command(client, path + "/self-reviews", new ItemCommand(Guid.NewGuid(), Rating: rating), Token(session)));
        }
        await Review("Again"); await Review("Partly"); await Review("Recalled");
        clock.Now = clock.Now.AddHours(72).AddMilliseconds(-1);
        await Review("Recalled");
        clock.Now = clock.Now.AddMilliseconds(1);
        await Review("Recalled");
        Guid latestObservation;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var contributions = await db.Set<KnowledgeContribution>().Where(x => x.OwnerId == owner).ToArrayAsync();
            Assert.Equal(2, contributions.Count(x => x.Admitted));
            Assert.Equal(.05, (await db.Set<KnowledgeState>().SingleAsync(x => x.OwnerId == owner)).Estimate);
            latestObservation = contributions.Where(x => x.Admitted).OrderByDescending(x => x.ReceivedAt).First().ObservationId;
        }
        var observation = await Body(await client.GetAsync($"/api/v1/evidence/observations/{latestObservation}"));
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/evidence/observations/{latestObservation}/status") { Content = JsonContent.Create(new { status = "Withdrawn" }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Token(observation)}\"");
        await Body(await client.SendAsync(request));
        await Review("Recalled");
        using var restarted = fixture.CreateFactory();
        await using var verification = restarted.Services.CreateAsyncScope();
        await verification.ServiceProvider.GetRequiredService<ITransactionRunner>().RunAsync(async () =>
        {
            await verification.ServiceProvider.GetRequiredService<KnowledgeService>().RebuildAsync(owner, concept, default);
            return true;
        }, default);
        var context = verification.ServiceProvider.GetRequiredService<SenseiDbContext>();
        Assert.Equal(0, (await context.Set<KnowledgeState>().SingleAsync(x => x.OwnerId == owner)).Estimate);
        Assert.Equal(2, await context.Set<KnowledgeContribution>().CountAsync(x => x.OwnerId == owner && x.Admitted));
        Assert.Equal(clock.Now, (await context.Set<SelfReviewAllowance>().SingleAsync(x => x.OwnerId == owner)).LastAdmittedAt);
    }
    [Fact]
    public async Task Overlapping_reviews_create_one_first_allowance_per_concept_and_rebuild_preserves_admissions()
    {
        using var factory = fixture.CreateFactory();
        using var client = await Client(factory);
        var owner = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single());
        var concepts = new[] { await Concept("value-reference-fixture"), await Concept("oop-fixture") };
        var now = DateTimeOffset.UtcNow;
        var results = new List<TrustedLearningResult>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var exercise = await db.Set<ExerciseVersion>().FirstAsync(x => x.Type == ExerciseType.Flashcard);
            for (var i = 0; i < 2; i++)
            {
                var session = new LearningSession { OwnerId = owner, State = SessionState.Completed, RequestedCount = 3, ActualCount = 1, CreatedAt = now, UpdatedAt = now };
                var item = new SessionItem { OwnerId = owner, SessionId = session.Id, ExerciseVersionId = exercise.Id, Outcome = ItemOutcome.SelfReviewed };
                var attempt = new LearningAttempt { OwnerId = owner, SessionId = session.Id, ItemId = item.Id, ExerciseVersionId = exercise.Id, ReceivedAt = now };
                var result = new ExerciseResult { OwnerId = owner, AttemptId = attempt.Id, SelfReview = "Recalled" };
                db.AddRange(session, item, attempt, result);
                results.Add(new(owner, result.Id, attempt.Id, exercise.FamilyId, session.Id, i == 0 ? concepts : concepts.AsEnumerable().Reverse().ToArray(), null, "Recalled", false, false, now));
            }
            await db.SaveChangesAsync();
        }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        async Task Submit(TrustedLearningResult result)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var tx = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
            await tx.RunAsync(async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
                await ready.Task;
                await scope.ServiceProvider.GetRequiredService<KnowledgeService>().StageAsync(result, default);
                return true;
            }, default);
        }
        await Task.WhenAll(results.Select(Submit)).WaitAsync(TimeSpan.FromSeconds(30));
        using var restarted = fixture.CreateFactory();
        await using var verification = restarted.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var contributions = await context.Set<KnowledgeContribution>().Where(x => x.OwnerId == owner).ToArrayAsync();
        Assert.Equal(4, contributions.Length);
        foreach (var concept in concepts) Assert.Single(contributions, x => x.ConceptId == concept && x.Admitted);
        Assert.Equal(2, await context.Set<SelfReviewAllowance>().CountAsync(x => x.OwnerId == owner));
        await verification.ServiceProvider.GetRequiredService<ITransactionRunner>().RunAsync(async () =>
        {
            foreach (var concept in concepts)
                await verification.ServiceProvider.GetRequiredService<KnowledgeService>().RebuildAsync(owner, concept, default);
            return true;
        }, default);
        Assert.All(await context.Set<KnowledgeState>().Where(x => x.OwnerId == owner).ToArrayAsync(), x => Assert.Null(x.Estimate));
        Assert.Equal(2, await context.Set<KnowledgeContribution>().CountAsync(x => x.OwnerId == owner && x.Admitted));
    }

    [Fact]
    public async Task Active_session_keeps_disabled_content_pinned_and_new_preview_reports_shortage()
    {
        using var factory = fixture.CreateFactory();
        using var client = await Client(factory);
        var session = await Start(client, ExerciseType.Matching);
        var item = Item(session);
        var versionId = item.GetProperty("exerciseVersionId").GetGuid();
        var answer = await Correct(item);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var version = await db.Set<ExerciseVersion>().SingleAsync(x => x.Id == versionId);
        version.Available = false;
        await db.SaveChangesAsync();
        try
        {
            var preview = await Body(await client.PostAsJsonAsync("/api/v1/learning/session-previews", new SessionSetup(3, item.GetProperty("conceptId").GetGuid(), Types: [ExerciseType.Matching]), Json));
            Assert.Equal(0, preview.GetProperty("actualCount").GetInt32());
            var saved = await Body(await client.GetAsync($"/api/v1/learning/sessions/{Id(session)}"));
            Assert.Equal(versionId, Item(saved).GetProperty("exerciseVersionId").GetGuid());
            var accepted = await Body(await Command(client, $"sessions/{Id(session)}/items/{Id(item)}/attempts", new ItemCommand(Guid.NewGuid(), answer), Token(session)));
            Assert.Equal(1, Item(accepted.GetProperty("session")).GetProperty("feedback")[0].GetProperty("score").GetInt32());
        }
        finally
        {
            version.Available = true;
            await db.SaveChangesAsync();
        }
    }
    private static readonly JsonSerializerOptions Json = ContentJson.Options;
    private async Task<HttpClient> Client(WebApplicationFactory<Program> factory)
    {
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<LearningFixtures>().Seed();
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/identity/users", new { email = $"learning-{Guid.NewGuid():N}@example.test", displayName = "Learning test", uiLocale = "en", answerLanguage = "en", timeZone = "UTC" });
        created.EnsureSuccessStatusCode(); var owner = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("X-Owner-Id", owner.ToString()); return client;
    }
    private async Task<Guid> Concept(string key)
    {
        using var factory = fixture.CreateFactory(); await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<Concept>().Where(x => x.Key == key).Select(x => x.Id).SingleAsync();
    }
    private static async Task<JsonElement> Body(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static async Task<HttpResponseMessage> Command(HttpClient client, string path, object body, string? version, string method = "POST")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/learning/" + path) { Content = JsonContent.Create(body, options: Json) };
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\""); return await client.SendAsync(request);
    }
    private async Task<JsonElement> Start(HttpClient client, ExerciseType type = ExerciseType.Prediction, int count = 3)
    {
        var body = await Body(await client.PostAsJsonAsync("/api/v1/learning/sessions", new StartSessionRequestForTest(Guid.NewGuid(), new SessionSetup(count, await Concept("value-reference-fixture"), Types: [type])), Json)); return body.GetProperty("session");
    }
    private sealed record StartSessionRequestForTest(Guid OperationId, SessionSetup Setup);
    private static string Token(JsonElement session) => session.GetProperty("versionToken").GetString()!;
    private static Guid Id(JsonElement session) => session.GetProperty("id").GetGuid();
    private static JsonElement Item(JsonElement session) => session.GetProperty("items")[session.GetProperty("position").GetInt32()];
    private async Task<Answer> Correct(JsonElement item)
    {
        using var factory = fixture.CreateFactory(); await using var scope = factory.Services.CreateAsyncScope(); var id = item.GetProperty("exerciseVersionId").GetGuid();
        var version = await scope.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<ExerciseVersion>().SingleAsync(x => x.Id == id);
        return ContentJson.Read<EvaluationDefinition>(version.EvaluationJson).Accepted[0];
    }
    [Fact]
    public async Task All_objective_formats_validate_without_consuming_attempts_and_keep_notes_ungraded()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory);
        foreach (var type in Enum.GetValues<ExerciseType>().Where(x => x != ExerciseType.Flashcard))
        {
            var session = await Start(client, type); var item = Item(session); var path = $"sessions/{Id(session)}/items/{Id(item)}/attempts";
            var invalid = await Command(client, path, new ItemCommand(Guid.NewGuid(), new Answer(["unknown"])), Token(session)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var original = (await Body(await client.GetAsync($"/api/v1/learning/sessions/{Id(session)}"))); Assert.Equal(Token(session), Token(original));
            var accepted = await Body(await Command(client, path, new ItemCommand(Guid.NewGuid(), await Correct(item), "This note is not graded."), Token(session)));
            session = accepted.GetProperty("session"); var feedback = Item(session).GetProperty("feedback")[0]; Assert.Equal(1, feedback.GetProperty("score").GetInt32()); Assert.Equal("This note is not graded.", feedback.GetProperty("note").GetString());
            if (session.GetProperty("state").GetString() == "Active") await Body(await Command(client, $"sessions/{Id(session)}/end", new { operationId = Guid.NewGuid() }, Token(session)));
        }
    }
    [Fact]
    public async Task Draft_pause_resume_feedback_advance_and_receipt_replay_survive_restart()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var session = await Start(client); var item = Item(session);
        var saved = await Body(await Command(client, $"sessions/{Id(session)}/items/{Id(item)}/draft", new { operationId = Guid.NewGuid(), answer = new Answer(["a"]), note = "Preserve this draft" }, Token(session), "PUT")); session = saved.GetProperty("session");
        session = (await Body(await Command(client, $"sessions/{Id(session)}/pause", new { operationId = Guid.NewGuid() }, Token(session)))).GetProperty("session");
        using var restarted = fixture.CreateFactory(); using var otherClient = restarted.CreateClient(); otherClient.DefaultRequestHeaders.Add("X-Owner-Id", client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single());
        var resumed = await Body(await otherClient.GetAsync($"/api/v1/learning/sessions/{Id(session)}")); Assert.Equal("Preserve this draft", Item(resumed).GetProperty("draft").GetProperty("note").GetString());
        session = (await Body(await Command(otherClient, $"sessions/{Id(session)}/resume", new { operationId = Guid.NewGuid() }, Token(session)))).GetProperty("session");
        var operation = Guid.NewGuid(); var payload = new ItemCommand(operation, await Correct(item)); var submissionPath = $"sessions/{Id(session)}/items/{Id(item)}/attempts"; var stale = Token(session);
        var accepted = await Body(await Command(client, submissionPath, payload, stale)); session = accepted.GetProperty("session"); Assert.Equal(0, session.GetProperty("position").GetInt32());
        session = (await Body(await Command(client, $"sessions/{Id(session)}/advance", new { operationId = Guid.NewGuid() }, Token(session)))).GetProperty("session");
        var replay = await Body(await Command(client, submissionPath, payload, stale)); Assert.True(replay.GetProperty("replayed").GetBoolean()); Assert.Equal(accepted.GetProperty("outcomeId").GetGuid(), replay.GetProperty("outcomeId").GetGuid()); Assert.Equal(Token(session), Token(replay.GetProperty("session")));
        var conflict = await Command(client, submissionPath, payload with { Note = "different" }, stale); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
    [Fact]
    public async Task Initial_presentation_has_no_keys_and_owners_cannot_read_nested_resources()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); using var other = await Client(factory); var session = await Start(client); var item = Item(session);
        var json = session.GetRawText(); Assert.DoesNotContain("evaluationJson", json); Assert.DoesNotContain("referenceAnswer", json); Assert.DoesNotContain("accepted", json); Assert.DoesNotContain("explanation", json);
        foreach (var path in new[] { $"sessions/{Id(session)}", $"sessions/{Id(session)}/summary", $"sessions/{Id(session)}/items/{Id(item)}", $"sessions/{Id(session)}/items/{Id(item)}/assistance" }) Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/learning/" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Command(other, $"sessions/{Id(session)}/end", new { operationId = Guid.NewGuid() }, Token(session))).StatusCode);
    }
    [Fact]
    public async Task Concurrent_starts_and_duplicate_submissions_have_single_committed_effect()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var setup = new SessionSetup(3, await Concept("value-reference-fixture"), Types: [ExerciseType.Prediction]);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> StartRace() { await gate.Task; return await client.PostAsJsonAsync("/api/v1/learning/sessions", new StartSessionRequestForTest(Guid.NewGuid(), setup), Json); }
        var starts = new[] { StartRace(), StartRace() }; gate.SetResult(); var responses = await Task.WhenAll(starts); Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var session = (await Body(responses.Single(x => x.IsSuccessStatusCode))).GetProperty("session"); var item = Item(session); var answer = await Correct(item); var operation = Guid.NewGuid();
        var submitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<JsonElement> SubmitRace() { await submitGate.Task; return await Body(await Command(client, $"sessions/{Id(session)}/items/{Id(item)}/attempts", new ItemCommand(operation, answer), Token(session))); }
        var submissions = new[] { SubmitRace(), SubmitRace() }; submitGate.SetResult(); var results = await Task.WhenAll(submissions); Assert.Equal(results[0].GetProperty("outcomeId").GetGuid(), results[1].GetProperty("outcomeId").GetGuid());
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>(); var itemId = Id(item); Assert.Equal(1, await db.Set<LearningAttempt>().CountAsync(x => x.ItemId == itemId));
    }
    [Fact]
    public async Task Two_tab_drafts_reject_stale_writes_and_preconditions_are_distinct()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var session = await Start(client); var path = $"sessions/{Id(session)}/items/{Id(Item(session))}/draft";
        var payload = new { operationId = Guid.NewGuid(), answer = new Answer(["a"]), note = "acknowledged" };
        Assert.Equal((HttpStatusCode)428, (await Command(client, path, payload, null, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Command(client, path, payload, "bad-token", "PUT")).StatusCode);
        await Body(await Command(client, path, payload, Token(session), "PUT"));
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Command(client, path, new { operationId = Guid.NewGuid(), answer = new Answer(["b"]), note = "stale" }, Token(session), "PUT")).StatusCode);
        var saved = await Body(await client.GetAsync($"/api/v1/learning/sessions/{Id(session)}")); Assert.Equal("acknowledged", Item(saved).GetProperty("draft").GetProperty("note").GetString());
    }
    [Fact]
    public async Task Flashcard_reviews_preserve_activity_and_cooldown_without_objective_estimate()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory);
        for (var i = 0; i < 2; i++)
        {
            var session = await Start(client, ExerciseType.Flashcard); var item = Item(session); var path = $"sessions/{Id(session)}/items/{Id(item)}";
            Assert.Equal(HttpStatusCode.BadRequest, (await Command(client, path + "/self-reviews", new ItemCommand(Guid.NewGuid(), Rating: "Recalled"), Token(session))).StatusCode);
            session = (await Body(await Command(client, path + "/assistance", new ItemCommand(Guid.NewGuid(), Assistance: "Reveal"), Token(session)))).GetProperty("session");
            await Body(await Command(client, path + "/self-reviews", new ItemCommand(Guid.NewGuid(), Rating: "Recalled"), Token(session)));
        }
        var owner = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single()); await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var contributions = await db.Set<KnowledgeContribution>().Where(x => x.OwnerId == owner).ToArrayAsync(); Assert.Equal(2, contributions.Length); Assert.Single(contributions, x => x.Admitted); Assert.Null((await db.Set<KnowledgeState>().SingleAsync(x => x.OwnerId == owner)).Estimate);
    }
    [Fact]
    public async Task Deferred_mode_is_rejected_and_shortage_does_not_repeat_families()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var concept = await Concept("oop-fixture");
        var response = await client.PostAsJsonAsync("/api/v1/learning/sessions", new StartSessionRequestForTest(Guid.NewGuid(), new SessionSetup(3, concept, FeedbackPolicy: "EndOfSession")), Json); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var preview = await Body(await client.PostAsJsonAsync("/api/v1/learning/session-previews", new SessionSetup(10, concept), Json)); Assert.Equal(1, preview.GetProperty("actualCount").GetInt32());
        var session = (await Body(await client.PostAsJsonAsync("/api/v1/learning/sessions", new StartSessionRequestForTest(Guid.NewGuid(), new SessionSetup(10, concept)), Json))).GetProperty("session"); Assert.Equal(1, session.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Failure_after_evidence_staging_rolls_back_answer_projection_and_receipt()
    {
        using var normal = fixture.CreateFactory(); using var client = await Client(normal); var session = await Start(client); var item = Item(session); var operation = Guid.NewGuid();
        using var failing = fixture.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddScoped<ILearningKnowledge, FailingKnowledge>()));
        using var faultClient = failing.CreateClient(); faultClient.DefaultRequestHeaders.Add("X-Owner-Id", client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single());
        var payload = new ItemCommand(operation, await Correct(item)); var path = $"sessions/{Id(session)}/items/{Id(item)}/attempts";
        Assert.Equal(HttpStatusCode.InternalServerError, (await Command(faultClient, path, payload, Token(session))).StatusCode);
        await using (var scope = normal.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>(); var itemId = Id(item); var owner = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Owner-Id").Single());
            Assert.False(await db.Set<LearningAttempt>().AnyAsync(x => x.ItemId == itemId)); Assert.False(await db.Set<OperationReceipt>().AnyAsync(x => x.OperationId == operation)); Assert.False(await db.Set<KnowledgeContribution>().AnyAsync(x => x.OwnerId == owner)); Assert.False(await db.Set<KnowledgeState>().AnyAsync(x => x.OwnerId == owner));
        }
        var accepted = await Body(await Command(client, path, payload, Token(session))); Assert.False(accepted.GetProperty("replayed").GetBoolean());
    }
    private sealed class FailingKnowledge(KnowledgeService knowledge) : ILearningKnowledge
    {
        public async Task Stage(ResultEvidence r, CancellationToken ct) { await knowledge.StageAsync(new(r.OwnerId, r.ResultId, r.AttemptId, r.FamilyId, r.SessionId, r.ConceptIds, r.Score, r.SelfReview, r.Assisted, r.AnswerAware, r.ReceivedAt), ct); throw new Exception("Injected projection failure"); }
        public async Task<KnowledgeSnapshot[]> Read(Guid owner, Guid? concept, CancellationToken ct) => (await knowledge.Summaries(owner, concept, ct)).Select(x => new KnowledgeSnapshot(x.ConceptId, x.InternalEstimate, x.FamilyCount, x.AssistedCount, x.SelfReviewCount, x.QualifyingFamilies, x.QualifyingSessions, x.Correctness, x.ObservationIds, x.NextPositiveReviewAt, x.RecentFamilies)).ToArray();
    }
    [Fact]
    public async Task Concurrent_graph_additions_cannot_jointly_commit_a_cycle()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory);
        async Task<Guid> NewConcept() => (await Body(await client.PostAsJsonAsync("/api/v1/learning/concepts", new { key = Guid.NewGuid().ToString(), name = "Graph test", description = "Race fixture", locale = "en", difficulty = "Intermediate" }))).GetProperty("id").GetGuid();
        var a = await NewConcept(); var b = await NewConcept(); var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Add(Guid source, Guid target) { await gate.Task; return await client.PostAsJsonAsync("/api/v1/learning/concept-relations", new { sourceId = source, targetId = target, kind = "Prerequisite" }); }
        var calls = new[] { Add(a, b), Add(b, a) }; gate.SetResult(); var results = await Task.WhenAll(calls); Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task Free_practice_satisfies_shared_roadmap_evidence_without_fabricating_traversal()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var concept = await Concept("value-reference-fixture");
        // Six distinct objective families over two sessions; selection favors unseen families.
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var types = Enum.GetValues<ExerciseType>().Where(x => x != ExerciseType.Flashcard).ToArray();
            var session = (await Body(await client.PostAsJsonAsync("/api/v1/learning/sessions", new StartSessionRequestForTest(Guid.NewGuid(), new SessionSetup(3, concept, Types: types)), Json))).GetProperty("session");
            for (var index = 0; index < 3; index++)
            {
                var item = Item(session); session = (await Body(await Command(client, $"sessions/{Id(session)}/items/{Id(item)}/attempts", new ItemCommand(Guid.NewGuid(), await Correct(item)), Token(session)))).GetProperty("session");
                if (index < 2) session = (await Body(await Command(client, $"sessions/{Id(session)}/advance", new { operationId = Guid.NewGuid() }, Token(session)))).GetProperty("session");
            }
        }
        var roadmaps = (await Body(await client.GetAsync("/api/v1/learning/roadmaps?limit=100"))).GetProperty("items"); var roadmap = roadmaps.EnumerateArray().Single(x => x.GetProperty("title").GetString()!.StartsWith("C# fundamentals"));
        var view = await Body(await client.GetAsync($"/api/v1/learning/roadmaps/{Id(roadmap)}")); var stage = view.GetProperty("stages")[0];
        Assert.Equal("Evidence requirement met", stage.GetProperty("evidenceStatus").GetString()); Assert.Equal("Not visited", stage.GetProperty("traversal").GetString()); Assert.Equal(JsonValueKind.Null, view.GetProperty("enrollment").ValueKind);
        Assert.Equal("Insufficient exercise coverage", view.GetProperty("stages")[1].GetProperty("evidenceStatus").GetString());
        var evidence = await Body(await client.GetAsync($"/api/v1/evidence/knowledge/{concept}")); Assert.Equal(6, evidence.GetProperty("familyCount").GetInt32()); Assert.DoesNotContain("estimate", evidence.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manual_attempt_labels_are_untrusted_and_withdrawal_rebuilds_without_removing_attempts()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var session = await Start(client); var item = Item(session); var concept = item.GetProperty("conceptId").GetGuid();
        await Body(await client.PostAsJsonAsync("/api/v1/evidence/observations", new { conceptId = concept, aspect = "Untrusted label", signalKind = "Scenario", sourceKind = "Attempt", sourceId = Guid.NewGuid(), assistance = "None", conditions = "Manual claim" }));
        var unknown = await Body(await client.GetAsync($"/api/v1/evidence/knowledge/{concept}")); Assert.Equal(0, unknown.GetProperty("familyCount").GetInt32());
        var accepted = await Body(await Command(client, $"sessions/{Id(session)}/items/{Id(item)}/attempts", new ItemCommand(Guid.NewGuid(), await Correct(item)), Token(session))); var attemptId = accepted.GetProperty("outcomeId").GetGuid();
        var observations = (await Body(await client.GetAsync($"/api/v1/evidence/observations?conceptId={concept}&includeInactive=true&limit=100"))).GetProperty("items"); var observation = observations.EnumerateArray().Single(x => x.GetProperty("sourceId").GetGuid() == attemptId);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/evidence/observations/{Id(observation)}/status") { Content = JsonContent.Create(new { status = "Withdrawn" }) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{Token(observation)}\""); await Body(await client.SendAsync(request));
        var excluded = await Body(await client.GetAsync($"/api/v1/evidence/knowledge/{concept}")); Assert.Equal(0, excluded.GetProperty("familyCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/learning/attempts/{attemptId}")).StatusCode);
    }
    [Fact]
    public async Task Goal_and_enrollment_mutations_are_owner_scoped_and_version_guarded()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); using var other = await Client(factory); var concept = await Concept("value-reference-fixture");
        var input = new GoalInput("Work on assignments", [concept]); var goal = await Body(await client.PostAsJsonAsync("/api/v1/learning/goals", input));
        Assert.Equal(HttpStatusCode.NotFound, (await Command(other, $"goals/{Id(goal)}", input, Token(goal), "PUT")).StatusCode);
        var changed = await Body(await Command(client, $"goals/{Id(goal)}", input with { Intention = "Work on value semantics" }, Token(goal), "PUT"));
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Command(client, $"goals/{Id(goal)}", input, Token(goal), "PUT")).StatusCode);
        Assert.NotEqual(Token(goal), Token(changed));
        var roadmap = (await Body(await client.GetAsync("/api/v1/learning/roadmaps?limit=100"))).GetProperty("items")[0]; var enrollment = await Body(await client.PostAsJsonAsync("/api/v1/learning/roadmap-enrollments", new { operationId = Guid.NewGuid(), roadmapVersionId = Id(roadmap) }));
        Assert.Equal(HttpStatusCode.NotFound, (await Command(other, $"roadmap-enrollments/{Id(enrollment)}", new { operationId = Guid.NewGuid(), paused = true }, Token(enrollment), "PUT")).StatusCode);
    }
    [Fact]
    public async Task Assisted_answers_and_answer_aware_retries_preserve_original_and_do_not_meet_roadmap_requirement()
    {
        using var factory = fixture.CreateFactory(); using var client = await Client(factory); var session = await Start(client); var item = Item(session); var path = $"sessions/{Id(session)}/items/{Id(item)}";
        session = (await Body(await Command(client, path + "/assistance", new ItemCommand(Guid.NewGuid(), Assistance: "Hint"), Token(session)))).GetProperty("session");
        var submitted = await Body(await Command(client, path + "/attempts", new ItemCommand(Guid.NewGuid(), await Correct(item)), Token(session))); session = submitted.GetProperty("session");
        var retried = await Body(await Command(client, path + "/attempts", new ItemCommand(Guid.NewGuid(), await Correct(item), PreviousAttemptId: submitted.GetProperty("outcomeId").GetGuid()), Token(session)));
        var feedback = Item(retried.GetProperty("session")).GetProperty("feedback"); Assert.Equal(2, feedback.GetArrayLength()); Assert.False(feedback[0].GetProperty("answerAware").GetBoolean()); Assert.True(feedback[1].GetProperty("answerAware").GetBoolean()); Assert.True(feedback[0].GetProperty("assisted").GetBoolean());
        var evidence = await Body(await client.GetAsync($"/api/v1/evidence/knowledge/{item.GetProperty("conceptId").GetGuid()}")); Assert.Equal(1, evidence.GetProperty("familyCount").GetInt32()); Assert.Equal(0, evidence.GetProperty("qualifyingFamilies").GetInt32());
    }
}
