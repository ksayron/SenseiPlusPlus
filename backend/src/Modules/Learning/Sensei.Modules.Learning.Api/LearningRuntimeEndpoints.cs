using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Api;

public sealed record StartSessionRequest(Guid OperationId, SessionSetup Setup);
public sealed record OperationRequest(Guid OperationId);
public sealed record DraftRequest(Guid OperationId, Answer? Answer, string Note);
public sealed record RelationRequest(Guid SourceId, Guid TargetId, RelationKind Kind);
public sealed record MaterialViewRequest(Guid OperationId, Guid MaterialVersionId);
public sealed record ActivityReceipt(Guid OperationId, Guid MaterialVersionId, bool Replayed);
public sealed record EnrollmentRequest(Guid OperationId, Guid RoadmapVersionId);
public sealed record EnrollmentUpdate(Guid OperationId, bool Paused, Guid? StageId, string? Traversal);
public sealed record SessionResource(Guid Id, SessionState State, int Position, int RequestedCount, int ActualCount, string ReturnContext, DateTimeOffset CreatedAt, string VersionToken, ItemPresentation[] Items);
public sealed record GoalResource(Guid Id, string Intention, Guid[] ConceptIds, Guid? RoadmapVersionId, bool Archived, string VersionToken);
public sealed record EnrollmentResource(Guid Id, Guid RoadmapVersionId, bool Paused, Guid? CurrentStageId, string VersionToken);
public sealed record KnowledgeResource(Guid ConceptId, int FamilyCount, int AssistedCount, int SelfReviewCount, int QualifyingFamilies, int QualifyingSessions, double? Correctness, Guid[] ObservationIds, DateTimeOffset? NextPositiveReviewAt);
public sealed record StageResource(RoadmapStage Stage, string Traversal, string EvidenceStatus, KnowledgeResource? Evidence, int AvailableFamilies);
public sealed record RoadmapResource(RoadmapVersion Roadmap, EnrollmentResource? Enrollment, StageResource[] Stages);
public sealed record MutationResource(Guid OutcomeId, string ReceiptVersionToken, bool Replayed, SessionResource Session);
public sealed record MaterialResource(Guid Id, Guid ConceptId, string Locale, int SchemaVersion, ContentBlock[] Blocks);
public sealed record TopicResource(ConceptResource Concept, ConceptRelation[] Relations, Guid[] MaterialVersions, int AvailableFamilies);

public static class LearningRuntimeEndpoints
{
    private static Guid Owner(HttpContext c) => Guid.TryParse(c.Request.Headers["X-Owner-Id"], out var id) && id != Guid.Empty ? id : throw new ArgumentException("X-Owner-Id is required.");
    private static SessionResource Map(SessionView v) => new(v.Session.Id, v.Session.State, v.Session.Position, v.Session.RequestedCount, v.Session.ActualCount, v.Session.ReturnContext, v.Session.CreatedAt, HttpContract.VersionToken(v.Session.Version), v.Items);
    private static GoalResource Map(GoalView v) => new(v.Goal.Id, v.Goal.Intention, v.ConceptIds, v.Goal.RoadmapVersionId, v.Goal.Archived, HttpContract.VersionToken(v.Goal.Version));
    private static EnrollmentResource Map(RoadmapEnrollment e) => new(e.Id, e.RoadmapVersionId, e.Paused, e.CurrentStageId, HttpContract.VersionToken(e.Version));
    private static TopicResource Map(TopicView t) => new(new(t.Concept.Id, t.Concept.Key, t.Concept.Name, t.Concept.Description, t.Concept.Locale, t.Concept.Difficulty, t.Concept.IsActive, t.Concept.CreatedAt, HttpContract.VersionToken(t.Concept.Version)), t.Relations, t.MaterialVersions, t.AvailableFamilies);
    public static KnowledgeResource Map(KnowledgeSnapshot k) => new(k.ConceptId, k.FamilyCount, k.AssistedCount, k.SelfReviewCount, k.QualifyingFamilies, k.QualifyingSessions, k.Correctness, k.ObservationIds, k.NextPositiveReviewAt);
    private static async Task<IResult> Outcome(HttpContext c, LearningRuntime r, CommandOutcome outcome, CancellationToken ct, bool created = false)
    {
        var view = await r.Session(Owner(c), outcome.SessionId, ct);
        var resource = new MutationResource(outcome.OutcomeId, HttpContract.VersionToken(outcome.ReceiptVersion), outcome.Replayed, Map(view));
        return created ? HttpContract.CreatedVersioned(c.Response, $"/api/v1/learning/sessions/{view.Session.Id}", resource, view.Session.Version) : HttpContract.OkVersioned(c.Response, resource, view.Session.Version);
    }
    private static PageEnvelope<T> Page<T>(T[] values, int? limit, string? cursor, string scope, Func<T, Guid> id) => HttpContract.Page(values.OrderBy(id).ToArray(), limit, cursor, scope, x => id(x).ToString(), id);
    public static IEndpointRouteBuilder MapLearningRuntime(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/v1/learning").WithTags("Learning runtime");
        g.MapGet("/topics", async (string? search, Guid? parent, int? limit, string? cursor, LearningRuntime r, CancellationToken ct) => Results.Ok(Page((await r.Topics(search, parent, ct)).Select(Map).ToArray(), limit, cursor, $"topics:{search}:{parent}", x => x.Concept.Id))).WithName("BrowseLearningTopics").Produces<PageEnvelope<TopicResource>>();
        g.MapGet("/topics/{id:guid}", async (Guid id, LearningRuntime r, CancellationToken ct) => Results.Ok(Map((await r.Topics(null, null, ct)).SingleOrDefault(x => x.Concept.Id == id) ?? throw new Sensei.BuildingBlocks.Application.ResourceNotFoundException()))).WithName("GetLearningTopic").Produces<TopicResource>();
        g.MapPost("/concept-relations", async (RelationRequest body, LearningRuntime r, CancellationToken ct) => Results.Ok(await r.AddRelation(body.SourceId, body.TargetId, body.Kind, ct))).WithName("CreateConceptRelation").Produces<ConceptRelation>();
        g.MapGet("/materials/{id:guid}", async (Guid id, LearningRuntime r, CancellationToken ct) =>
        {
            var m = await r.Required<MaterialVersion>(x => x.Id == id, ct); if (m.SchemaVersion != 1) throw new ArgumentException("Unsupported material schema."); var blocks = ContentJson.Read<ContentBlock[]>(m.BlocksJson); ContentValidation.Blocks(blocks); return Results.Ok(new MaterialResource(m.Id, m.ConceptId, m.Locale, m.SchemaVersion, blocks));
        }).WithName("GetLearningMaterial").Produces<MaterialResource>();
        g.MapPost("/material-views", async (MaterialViewRequest body, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var outcome = await r.MaterialView(Owner(c), body.OperationId, body.MaterialVersionId, ct);
            return Results.Ok(new ActivityReceipt(body.OperationId, body.MaterialVersionId, outcome.Replayed));
        }).WithName("RecordMaterialView").Produces<ActivityReceipt>();
        g.MapPost("/session-previews", async (SessionSetup body, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(await r.Preview(Owner(c), body, ct))).WithName("PreviewLearningSession").Produces<Preview>();
        g.MapPost("/sessions", async (StartSessionRequest body, HttpContext c, LearningRuntime r, CancellationToken ct) => await Outcome(c, r, await r.Start(Owner(c), body.OperationId, body.Setup, ct), ct, true)).WithName("StartLearningSession").Produces<MutationResource>(201);
        g.MapGet("/sessions", async (bool? resumable, int? limit, string? cursor, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var owner = Owner(c); var sessions = await r.List<LearningSession>(x => x.OwnerId == owner, ct);
            if (resumable == true) sessions = sessions.Where(x => x.State is SessionState.Active or SessionState.Paused).ToArray();
            var resources = sessions.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => Map(new SessionView(x, []))).ToArray(); return Results.Ok(HttpContract.Page(resources, limit, cursor, $"sessions:{owner}:{resumable}", x => x.CreatedAt.ToString("O"), x => x.Id));
        }).WithName("ListLearningSessions").Produces<PageEnvelope<SessionResource>>();
        g.MapGet("/sessions/{id:guid}", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var view = await r.Session(Owner(c), id, ct); return HttpContract.OkVersioned(c.Response, Map(view), view.Session.Version);
        }).WithName("GetLearningSession").Produces<SessionResource>();
        g.MapGet("/sessions/{id:guid}/summary", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(Map(await r.Session(Owner(c), id, ct)))).WithName("GetLearningSummary").Produces<SessionResource>();
        g.MapGet("/sessions/{id:guid}/items/{item:guid}", async (Guid id, Guid item, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var view = await r.Session(Owner(c), id, ct); return Results.Ok(view.Items.SingleOrDefault(x => x.Id == item) ?? throw new Sensei.BuildingBlocks.Application.ResourceNotFoundException());
        }).WithName("GetLearningItem").Produces<ItemPresentation>();
        foreach (var action in new[] { "pause", "resume", "end", "advance" })
        {
            var captured = action;
            g.MapPost($"/sessions/{{id:guid}}/{action}", async (Guid id, OperationRequest body, HttpContext c, LearningRuntime r, CancellationToken ct) => await Outcome(c, r, await r.Lifecycle(Owner(c), id, HttpContract.RequireVersion(c.Request), body.OperationId, captured, ct), ct)).WithName($"LearningSession_{action}").Produces<MutationResource>();
        }
        g.MapPut("/sessions/{id:guid}/items/{item:guid}/draft", async (Guid id, Guid item, DraftRequest body, HttpContext c, LearningRuntime r, CancellationToken ct) => await Outcome(c, r, await r.SaveDraft(Owner(c), id, item, HttpContract.RequireVersion(c.Request), body.OperationId, new(body.Answer, body.Note), ct), ct)).WithName("SaveLearningDraft").Produces<MutationResource>();
        foreach (var action in new[] { "attempts", "self-reviews", "skip", "assistance" })
        {
            var captured = action;
            g.MapPost($"/sessions/{{id:guid}}/items/{{item:guid}}/{action}", async (Guid id, Guid item, ItemCommand body, HttpContext c, LearningRuntime r, CancellationToken ct) => await Outcome(c, r, await r.ItemAction(Owner(c), id, item, HttpContract.RequireVersion(c.Request), captured, body, ct), ct)).WithName($"LearningItem_{action}").Produces<MutationResource>();
        }
        g.MapGet("/sessions/{id:guid}/items/{item:guid}/assistance", async (Guid id, Guid item, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(await r.Assistance(Owner(c), id, item, ct))).WithName("GetAcknowledgedAssistance").Produces<AssistanceView>();
        g.MapGet("/attempts/{id:guid}", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(await r.Attempt(Owner(c), id, ct))).WithName("GetLearningAttempt").Produces<Feedback>();
        g.MapGet("/activity", async (Guid? conceptId, string? kind, int? limit, string? cursor, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var owner = Owner(c); var events = await r.List<LearningEvent>(x => x.OwnerId == owner && (kind == null || x.Kind == kind), ct);
            if (conceptId is { } concept)
            {
                var versions = (await r.List<ExerciseConcept>(x => x.ConceptId == concept, ct)).Select(x => x.ExerciseVersionId).ToHashSet();
                var materials = (await r.List<MaterialVersion>(x => x.ConceptId == concept, ct)).Select(x => x.Id).ToHashSet();
                events = events.Where(x => (x.ExerciseVersionId is { } e && versions.Contains(e)) || (x.MaterialVersionId is { } m && materials.Contains(m))).ToArray();
            }
            return Results.Ok(HttpContract.Page(events.OrderByDescending(x => x.ReceivedAt).ThenBy(x => x.Id).ToArray(), limit, cursor, $"activity:{owner}:{conceptId}:{kind}", x => x.ReceivedAt.ToString("O"), x => x.Id));
        }).WithName("ListLearningActivity").Produces<PageEnvelope<LearningEvent>>();
        g.MapGet("/goals", async (int? limit, string? cursor, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(Page((await r.Goals(Owner(c), ct)).Select(Map).ToArray(), limit, cursor, $"goals:{Owner(c)}", x => x.Id))).WithName("ListLearningGoals").Produces<PageEnvelope<GoalResource>>();
        g.MapPost("/goals", async (GoalInput body, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var goal = await r.SaveGoal(Owner(c), null, null, body, ct); return HttpContract.CreatedVersioned(c.Response, $"/api/v1/learning/goals/{goal.Id}", Map(new GoalView(goal, body.ConceptIds)), goal.Version);
        }).WithName("CreateLearningGoal").Produces<GoalResource>(201);
        g.MapPut("/goals/{id:guid}", async (Guid id, GoalInput body, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var goal = await r.SaveGoal(Owner(c), id, HttpContract.RequireVersion(c.Request), body, ct); return HttpContract.OkVersioned(c.Response, Map(new GoalView(goal, body.ConceptIds)), goal.Version);
        }).WithName("UpdateLearningGoal").Produces<GoalResource>();
        g.MapDelete("/goals/{id:guid}", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) => { await r.ArchiveGoal(Owner(c), id, HttpContract.RequireVersion(c.Request), ct); return Results.NoContent(); }).WithName("ArchiveLearningGoal");
        g.MapGet("/roadmaps", async (int? limit, string? cursor, LearningRuntime r, CancellationToken ct) => Results.Ok(Page(await r.List<RoadmapVersion>(x => x.Available, ct), limit, cursor, "roadmaps", x => x.Id))).WithName("ListLearningRoadmaps").Produces<PageEnvelope<RoadmapVersion>>();
        g.MapGet("/roadmaps/{id:guid}", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            var view = await r.Roadmap(Owner(c), id, ct); return Results.Ok(new RoadmapResource(view.Roadmap, view.Enrollment is null ? null : Map(view.Enrollment), view.Stages.Select(s => new StageResource(s.Stage, s.Traversal, s.EvidenceStatus, s.Evidence is null ? null : Map(s.Evidence), s.AvailableFamilies)).ToArray()));
        }).WithName("GetLearningRoadmap").Produces<RoadmapResource>();
        g.MapGet("/roadmap-enrollments", async (int? limit, string? cursor, HttpContext c, LearningRuntime r, CancellationToken ct) => { var owner = Owner(c); return Results.Ok(Page((await r.List<RoadmapEnrollment>(x => x.OwnerId == owner, ct)).Select(Map).ToArray(), limit, cursor, $"enrollments:{owner}", x => x.Id)); }).WithName("ListRoadmapEnrollments").Produces<PageEnvelope<EnrollmentResource>>();
        g.MapPost("/roadmap-enrollments", async (EnrollmentRequest body, HttpContext c, LearningRuntime r, CancellationToken ct) => { var owner = Owner(c); var outcome = await r.Enroll(owner, body.OperationId, body.RoadmapVersionId, ct); var enrollment = await r.Required<RoadmapEnrollment>(x => x.OwnerId == owner && x.Id == outcome.OutcomeId, ct); return HttpContract.OkVersioned(c.Response, Map(enrollment), enrollment.Version); }).WithName("EnrollInRoadmap").Produces<EnrollmentResource>();
        g.MapPut("/roadmap-enrollments/{id:guid}", async (Guid id, EnrollmentUpdate body, HttpContext c, LearningRuntime r, CancellationToken ct) => { var owner = Owner(c); if (body.Traversal == "Practiced") throw new ArgumentException("Practice traversal is recorded by session creation."); await r.UpdateEnrollment(owner, id, HttpContract.RequireVersion(c.Request), body.OperationId, body.Paused, body.StageId, body.Traversal, ct); var enrollment = await r.Required<RoadmapEnrollment>(x => x.OwnerId == owner && x.Id == id, ct); return HttpContract.OkVersioned(c.Response, Map(enrollment), enrollment.Version); }).WithName("UpdateRoadmapEnrollment").Produces<EnrollmentResource>();
        endpoints.MapGet("/api/v1/evidence/knowledge", async (int? limit, string? cursor, HttpContext c, LearningRuntime r, CancellationToken ct) => Results.Ok(Page((await r.Knowledge(Owner(c), null, ct)).Select(Map).ToArray(), limit, cursor, $"knowledge:{Owner(c)}", x => x.ConceptId))).WithTags("Evidence and Profile").WithName("ListKnowledgeSummaries").Produces<PageEnvelope<KnowledgeResource>>();
        endpoints.MapGet("/api/v1/evidence/knowledge/{id:guid}", async (Guid id, HttpContext c, LearningRuntime r, CancellationToken ct) =>
        {
            await r.Required<Concept>(x => x.Id == id, ct); return Results.Ok((await r.Knowledge(Owner(c), id, ct)).Select(Map).SingleOrDefault() ?? new KnowledgeResource(id, 0, 0, 0, 0, 0, null, [], null));
        }).WithTags("Evidence and Profile").WithName("GetKnowledgeSummary").Produces<KnowledgeResource>();
        return endpoints;
    }
}
