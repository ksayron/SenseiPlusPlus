using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Learning.Domain;
namespace Sensei.Modules.Learning.Application;

public sealed record GoalInput(string Intention, Guid[] ConceptIds, Guid? RoadmapVersionId = null);
public sealed record GoalView(LearningGoal Goal, Guid[] ConceptIds);
public sealed record StageView(RoadmapStage Stage, string Traversal, string EvidenceStatus, KnowledgeSnapshot? Evidence, int AvailableFamilies);
public sealed record RoadmapView(RoadmapVersion Roadmap, RoadmapEnrollment? Enrollment, StageView[] Stages);
public sealed partial class LearningRuntime
{
    public async Task<GoalView[]> Goals(Guid owner, CancellationToken ct)
    {
        var goals = await store.Read<LearningGoal>(x => x.OwnerId == owner, ct); var ids = goals.Select(x => x.Id).ToArray();
        var links = await store.Read<GoalConcept>(x => ids.Contains(x.GoalId), ct);
        return goals.OrderBy(x => x.Id).Select(x => new GoalView(x, links.Where(l => l.GoalId == x.Id).Select(l => l.ConceptId).ToArray())).ToArray();
    }
    public Task<LearningGoal> SaveGoal(Guid owner, Guid? id, int? version, GoalInput input, CancellationToken ct) => tx.RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(input.Intention) || input.Intention.Length > 500 || input.ConceptIds is null || (input.ConceptIds.Length == 0) == (input.RoadmapVersionId is null)) throw new ArgumentException("Choose concepts or a roadmap and supply an intention.");
        foreach (var concept in input.ConceptIds.Distinct()) await Required<Concept>(x => x.Id == concept && x.IsActive, ct);
        if (input.RoadmapVersionId is { } roadmap) await Required<RoadmapVersion>(x => x.Id == roadmap && x.Available, ct);
        LearningGoal goal;
        if (id is { } existing)
        {
            goal = await Required<LearningGoal>(x => x.OwnerId == owner && x.Id == existing, ct); VersionPrecondition.RequireCurrent(goal.Version, version!.Value); if (goal.Archived) throw new InvalidOperationException("Goal is archived."); goal.Version++;
            foreach (var link in await store.Read<GoalConcept>(x => x.GoalId == existing, ct)) store.Remove(link);
        }
        else { goal = new LearningGoal { OwnerId = owner }; store.Add(goal); }
        goal.Intention = input.Intention.Trim(); goal.RoadmapVersionId = input.RoadmapVersionId;
        foreach (var concept in input.ConceptIds.Distinct()) store.Add(new GoalConcept { GoalId = goal.Id, ConceptId = concept }); return goal;
    }, ct);
    public Task<LearningGoal> ArchiveGoal(Guid owner, Guid id, int version, CancellationToken ct) => tx.RunAsync(async () =>
    {
        var goal = await Required<LearningGoal>(x => x.OwnerId == owner && x.Id == id, ct); VersionPrecondition.RequireCurrent(goal.Version, version); goal.Archived = true; goal.Version++; return goal;
    }, ct);
    public Task<CommandOutcome> Enroll(Guid owner, Guid operation, Guid roadmap, CancellationToken ct) => Operation(owner, operation, "enroll", roadmap, new { roadmap }, async () =>
    {
        await tx.LockAsync($"learning:enrollment:{owner}:{roadmap}", ct);
        await Required<RoadmapVersion>(x => x.Id == roadmap && x.Available, ct);
        var enrollment = (await store.Read<RoadmapEnrollment>(x => x.OwnerId == owner && x.RoadmapVersionId == roadmap, ct)).SingleOrDefault();
        if (enrollment is null)
        {
            var firstStage = (await store.Read<RoadmapStage>(x => x.RoadmapVersionId == roadmap, ct)).OrderBy(x => x.Position).FirstOrDefault();
            enrollment = new RoadmapEnrollment { OwnerId = owner, RoadmapVersionId = roadmap, CurrentStageId = firstStage?.Id };
            store.Add(enrollment);
        }
        return new CommandOutcome(Guid.Empty, enrollment.Id, enrollment.Version, false);
    }, ct);
    public Task<CommandOutcome> UpdateEnrollment(Guid owner, Guid id, int version, Guid operation, bool paused, Guid? stage, string? traversal, CancellationToken ct) => Operation(owner, operation, "enrollment", id, new { paused, stage, traversal }, async () =>
    {
        await tx.LockAsync($"learning:enrollment-id:{id}", ct);
        var enrollment = await Required<RoadmapEnrollment>(x => x.OwnerId == owner && x.Id == id, ct); VersionPrecondition.RequireCurrent(enrollment.Version, version);
        enrollment.Paused = paused;
        if (stage is { } stageId) await Traverse(owner, id, stageId, traversal ?? "Visited", ct);
        else enrollment.Version++;
        return new CommandOutcome(Guid.Empty, id, enrollment.Version, false);
    }, ct);
    private async Task Traverse(Guid owner, Guid enrollmentId, Guid stageId, string state, CancellationToken ct)
    {
        if (state is not ("Visited" or "Skipped" or "Practiced")) throw new ArgumentException("Invalid traversal action.");
        await tx.LockAsync($"learning:enrollment-id:{enrollmentId}", ct);
        var enrollment = await Required<RoadmapEnrollment>(x => x.OwnerId == owner && x.Id == enrollmentId, ct);
        await Required<RoadmapStage>(x => x.Id == stageId && x.RoadmapVersionId == enrollment.RoadmapVersionId, ct);
        var traversal = (await store.Read<StageTraversal>(x => x.EnrollmentId == enrollmentId && x.StageId == stageId, ct)).SingleOrDefault();
        if (traversal is null) { traversal = new StageTraversal { EnrollmentId = enrollmentId, StageId = stageId }; store.Add(traversal); }
        if (state != "Visited" || traversal.State != "Practiced") traversal.State = state;
        enrollment.CurrentStageId = stageId; enrollment.Version++;
    }
    public async Task<RoadmapView> Roadmap(Guid owner, Guid id, CancellationToken ct)
    {
        var roadmap = await Required<RoadmapVersion>(x => x.Id == id, ct);
        var enrollment = (await store.Read<RoadmapEnrollment>(x => x.OwnerId == owner && x.RoadmapVersionId == id, ct)).SingleOrDefault();
        var traversal = enrollment is null ? [] : await store.Read<StageTraversal>(x => x.EnrollmentId == enrollment.Id, ct);
        var knowledgeByConcept = await knowledge.Read(owner, null, ct);
        var stages = await store.Read<RoadmapStage>(x => x.RoadmapVersionId == id, ct);
        var exercises = await store.Read<ExerciseVersion>(x => x.Available && x.Type != ExerciseType.Flashcard, ct);
        var links = await store.Read<ExerciseConcept>(_ => true, ct);
        var views = stages.OrderBy(x => x.Position).Select(stage =>
        {
            var evidence = knowledgeByConcept.FirstOrDefault(x => x.ConceptId == stage.ConceptId);
            var versions = links.Where(x => x.ConceptId == stage.ConceptId).Select(x => x.ExerciseVersionId).ToHashSet();
            var available = exercises.Where(x => versions.Contains(x.Id)).Select(x => x.FamilyId).Distinct().Count();
            var status = evidence is not null && evidence.QualifyingFamilies >= stage.MinimumFamilies && evidence.QualifyingSessions >= stage.MinimumSessions && evidence.Correctness >= stage.MinimumCorrectness ? "Evidence requirement met" : available < stage.MinimumFamilies ? "Insufficient exercise coverage" : evidence is null || evidence.QualifyingFamilies == 0 ? "No objective evidence" : "More evidence needed";
            return new StageView(stage, traversal.FirstOrDefault(x => x.StageId == stage.Id)?.State ?? "Not visited", status, evidence, available);
        }).ToArray();
        return new(roadmap, enrollment, views);
    }
    public Task<CommandOutcome> MaterialView(Guid owner, Guid operation, Guid material, CancellationToken ct) => Operation(owner, operation, "material-view", material, new { material }, async () =>
    {
        await Required<MaterialVersion>(x => x.Id == material, ct); Event(owner, operation, "MaterialViewed", material: material); return new CommandOutcome(Guid.Empty, material, 1, false);
    }, ct);
}
