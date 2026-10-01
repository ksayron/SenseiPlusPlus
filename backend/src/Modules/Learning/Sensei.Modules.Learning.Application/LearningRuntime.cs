using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Application;

public interface ILearningStore
{
    Task<T[]> Read<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
}
public sealed record KnowledgeSnapshot(Guid ConceptId, double? Estimate, int FamilyCount, int AssistedCount, int SelfReviewCount, int QualifyingFamilies, int QualifyingSessions, double? Correctness, Guid[] ObservationIds, DateTimeOffset? NextPositiveReviewAt, Guid[] RecentFamilies);
public sealed record ResultEvidence(Guid OwnerId, Guid ResultId, Guid AttemptId, Guid FamilyId, Guid SessionId, Guid[] ConceptIds, int? Score, string? SelfReview, bool Assisted, bool AnswerAware, DateTimeOffset ReceivedAt);
public interface ILearningKnowledge
{
    Task Stage(ResultEvidence result, CancellationToken ct);
    Task<KnowledgeSnapshot[]> Read(Guid owner, Guid? concept, CancellationToken ct);
}
public sealed record SessionSetup(int Count = 5, Guid? ConceptId = null, Guid? RoadmapStageId = null, Guid? EnrollmentId = null, ExerciseType[]? Types = null, string Locale = "en", string FeedbackPolicy = "Immediate", string ReturnContext = "/learn");
public sealed record Preview(int RequestedCount, int ActualCount, int EstimatedSeconds, bool EnglishFallback, string? UnavailableReason);
public sealed record Draft(Answer? Answer, string Note = "");
public sealed record ItemCommand(Guid OperationId, Answer? Answer = null, string Note = "", string? Rating = null, Guid? PreviousAttemptId = null, string? Assistance = null);
public sealed record Feedback(Guid AttemptId, Guid ResultId, int? Score, string? SelfReview, Answer? Answer, string Note, bool Assisted, bool AnswerAware, Guid? PreviousAttemptId, string Explanation, string ReferenceAnswer, DateTimeOffset ReceivedAt);
public sealed record ItemPresentation(Guid Id, Guid ExerciseVersionId, Guid ConceptId, int Position, ExerciseType Type, string Locale, ContentBlock[] Prompt, Interaction Interaction, Draft Draft, ItemOutcome? Outcome, bool HintUsed, bool ReferenceUsed, bool Revealed, int HintCount, Guid? MaterialVersionId, Feedback[] Feedback);
public sealed record SessionView(LearningSession Session, ItemPresentation[] Items);
public sealed record CommandOutcome(Guid SessionId, Guid OutcomeId, int ReceiptVersion, bool Replayed);
public sealed record AssistanceView(string[] Hints, Guid? Reference, string? Answer);
public sealed record TopicView(Concept Concept, ConceptRelation[] Relations, Guid[] MaterialVersions, int AvailableFamilies);

public sealed partial class LearningRuntime(ILearningStore store, ITransactionRunner tx, ILearningKnowledge knowledge, TimeProvider clock)
{
    public async Task<T> Required<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class =>
        (await store.Read(predicate, ct)).SingleOrDefault() ?? throw new ResourceNotFoundException();
    public Task<T[]> List<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => store.Read(predicate, ct);
    public Task<KnowledgeSnapshot[]> Knowledge(Guid owner, Guid? concept, CancellationToken ct) => knowledge.Read(owner, concept, ct);
    public async Task<TopicView[]> Topics(string? search, Guid? parent, CancellationToken ct)
    {
        var concepts = await store.Read<Concept>(x => x.IsActive, ct);
        var relations = await store.Read<ConceptRelation>(_ => true, ct);
        var materials = await store.Read<MaterialVersion>(x => x.Available, ct);
        var exercises = await store.Read<ExerciseVersion>(x => x.Available, ct);
        return concepts.Where(x => string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(x => parent == null || relations.Any(r => r.Kind == RelationKind.PartOf && r.SourceId == x.Id && r.TargetId == parent))
            .OrderBy(x => x.Key).ThenBy(x => x.Id).Select(x => new TopicView(x, relations.Where(r => r.SourceId == x.Id || r.TargetId == x.Id).ToArray(), materials.Where(m => m.ConceptId == x.Id).Select(m => m.Id).ToArray(), exercises.Where(e => e.PrimaryConceptId == x.Id).Select(e => e.FamilyId).Distinct().Count())).ToArray();
    }
    public Task<ConceptRelation> AddRelation(Guid source, Guid target, RelationKind kind, CancellationToken ct) => tx.RunAsync(async () =>
    {
        await tx.LockAsync("learning:graph", ct);
        await Required<Concept>(x => x.Id == source, ct); await Required<Concept>(x => x.Id == target, ct);
        var relation = ConceptRelation.Create(source, target, kind, await store.Read<ConceptRelation>(_ => true, ct)); store.Add(relation); return relation;
    }, ct);
    public async Task<Preview> Preview(Guid owner, SessionSetup setup, CancellationToken ct)
    {
        var selected = await Select(owner, setup, ct);
        return new(setup.Count, selected.Length, selected.Sum(x => x.EstimatedSeconds), selected.Any(x => x.Locale != setup.Locale), selected.Length == 0 ? "No exercises are available for these filters." : null);
    }
    private async Task<HashSet<Guid>> Scope(Guid root, CancellationToken ct)
    {
        await Required<Concept>(x => x.Id == root && x.IsActive, ct);
        var edges = await store.Read<ConceptRelation>(x => x.Kind == RelationKind.PartOf, ct);
        var scope = new HashSet<Guid> { root }; bool changed;
        do { changed = false; foreach (var edge in edges) if (scope.Contains(edge.TargetId)) changed |= scope.Add(edge.SourceId); } while (changed);
        return scope;
    }
    private async Task<ExerciseVersion[]> Select(Guid owner, SessionSetup setup, CancellationToken ct)
    {
        if (setup.Count is not (3 or 5 or 10) || setup.FeedbackPolicy != "Immediate" || string.IsNullOrWhiteSpace(setup.Locale) || (setup.Types?.Any(t => !Enum.IsDefined(t)) ?? false)) throw new ArgumentException("Use 3, 5 or 10 items and Immediate feedback with supported exercise types.");
        Guid? root = setup.ConceptId;
        if (setup.RoadmapStageId is { } stageId) { var stage = await Required<RoadmapStage>(x => x.Id == stageId, ct); if (root is not null && root != stage.ConceptId) throw new ArgumentException("Conflicting scopes."); root = stage.ConceptId; }
        if (setup.EnrollmentId is { } enrollmentId)
        {
            var enrollment = await Required<RoadmapEnrollment>(x => x.Id == enrollmentId && x.OwnerId == owner, ct);
            if (setup.RoadmapStageId is null || !(await store.Read<RoadmapStage>(x => x.Id == setup.RoadmapStageId && x.RoadmapVersionId == enrollment.RoadmapVersionId, ct)).Any()) throw new ArgumentException("Stage does not belong to enrollment.");
        }
        var scope = root is { } id ? await Scope(id, ct) : null;
        var activeConcepts = (await store.Read<Concept>(x => x.IsActive, ct)).Select(x => x.Id).ToHashSet();
        var candidates = (await store.Read<ExerciseVersion>(x => x.Available && x.SchemaVersion == 1, ct))
            .Where(x => activeConcepts.Contains(x.PrimaryConceptId) && (scope is null || scope.Contains(x.PrimaryConceptId)) && (setup.Types is null || setup.Types.Length == 0 || setup.Types.Contains(x.Type)) && (x.Locale == setup.Locale || x.Locale == "en"))
            .GroupBy(x => x.ExerciseId).Select(g => g.OrderByDescending(x => x.Revision).ThenBy(x => x.Id).First()).ToArray();
        foreach (var exercise in candidates) ContentValidation.Exercise(exercise);
        var summaries = await knowledge.Read(owner, null, ct);
        var goals = await store.Read<LearningGoal>(x => x.OwnerId == owner && !x.Archived, ct);
        var goalIds = goals.Select(x => x.Id).ToArray();
        var targetIds = (await store.Read<GoalConcept>(x => goalIds.Contains(x.GoalId), ct)).Select(x => x.ConceptId).ToHashSet();
        var roadmapIds = goals.Where(x => x.RoadmapVersionId != null).Select(x => x.RoadmapVersionId!.Value).ToArray();
        foreach (var stage in await store.Read<RoadmapStage>(x => roadmapIds.Contains(x.RoadmapVersionId), ct)) targetIds.Add(stage.ConceptId);
        var enrollments = await store.Read<RoadmapEnrollment>(x => x.OwnerId == owner && !x.Paused, ct);
        var currentStages = enrollments.Where(x => x.CurrentStageId != null).Select(x => x.CurrentStageId!.Value).ToArray();
        var currentConcepts = (await store.Read<RoadmapStage>(x => currentStages.Contains(x.Id), ct)).Select(x => x.ConceptId).ToHashSet();
        var containment = await store.Read<ConceptRelation>(x => x.Kind == RelationKind.PartOf, ct);
        void IncludeDescendants(HashSet<Guid> targets)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var edge in containment)
                    if (targets.Contains(edge.TargetId)) changed |= targets.Add(edge.SourceId);
            } while (changed);
        }
        IncludeDescendants(targetIds);
        IncludeDescendants(currentConcepts);
        var attempts = await store.Read<LearningAttempt>(x => x.OwnerId == owner, ct);
        var attemptedVersions = attempts.Select(x => x.ExerciseVersionId).ToArray();
        var seen = (await store.Read<ExerciseVersion>(x => attemptedVersions.Contains(x.Id), ct)).Select(x => x.FamilyId).ToHashSet();
        double Rank(ExerciseVersion e) { var s = summaries.FirstOrDefault(x => x.ConceptId == e.PrimaryConceptId); return SelectionPolicy.Rank(targetIds.Contains(e.PrimaryConceptId), s?.Estimate, !seen.Contains(e.FamilyId), currentConcepts.Contains(e.PrimaryConceptId), summaries.Any(x => x.RecentFamilies.Contains(e.FamilyId))); }
        var prerequisites = await store.Read<ConceptRelation>(x => x.Kind == RelationKind.Prerequisite, ct);
        var candidateConcepts = candidates.Select(x => x.PrimaryConceptId).ToHashSet();
        var advisoryFirst = prerequisites.Where(x => candidateConcepts.Contains(x.TargetId)).Select(x => x.SourceId).ToHashSet();
        var pool = candidates.OrderByDescending(Rank).ThenBy(x => x.Locale == setup.Locale ? 0 : 1).ThenByDescending(x => advisoryFirst.Contains(x.PrimaryConceptId)).ThenBy(x => x.Id).GroupBy(x => x.FamilyId).Select(g => g.First()).ToList();
        return SelectionPolicy.Diverse(pool, setup.Count);
    }
    public Task<CommandOutcome> Start(Guid owner, Guid operation, SessionSetup setup, CancellationToken ct) => Operation(owner, operation, "start", Guid.Empty, setup, async () =>
    {
        await tx.LockAsync($"learning:owner:{owner}", ct);
        if ((await store.Read<LearningSession>(x => x.OwnerId == owner && (x.State == SessionState.Active || x.State == SessionState.Paused), ct)).Length > 0) throw new InvalidOperationException("Resume or end the existing session first.");
        var selected = await Select(owner, setup, ct); if (selected.Length == 0) throw new InvalidOperationException("No exercises are available for these filters.");
        if (string.IsNullOrWhiteSpace(setup.ReturnContext) || !(setup.ReturnContext == "/learn" || setup.ReturnContext.StartsWith("/learn/", StringComparison.Ordinal) || setup.ReturnContext.StartsWith("/learn?", StringComparison.Ordinal)) || setup.ReturnContext.Contains('\\') || setup.ReturnContext.Any(char.IsControl)) throw new ArgumentException("Invalid return context.");
        var session = new LearningSession { OwnerId = owner, ConceptId = setup.ConceptId, RoadmapStageId = setup.RoadmapStageId, EnrollmentId = setup.EnrollmentId, RequestedCount = setup.Count, ActualCount = selected.Length, ReturnContext = setup.ReturnContext, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() }; store.Add(session);
        for (var index = 0; index < selected.Length; index++) store.Add(new SessionItem { OwnerId = owner, SessionId = session.Id, ExerciseVersionId = selected[index].Id, Position = index });
        if (setup.EnrollmentId is { } enrollmentId && setup.RoadmapStageId is { } stageId) await Traverse(owner, enrollmentId, stageId, "Practiced", ct);
        Event(owner, operation, "Started", session.Id);
        return new CommandOutcome(session.Id, session.Id, session.Version, false);
    }, ct);
    private Task<CommandOutcome> Operation<T>(Guid owner, Guid operation, string kind, Guid target, T payload, Func<Task<CommandOutcome>> action, CancellationToken ct) => tx.RunAsync(async () =>
    {
        if (owner == Guid.Empty || operation == Guid.Empty) throw new ArgumentException("Owner and operation IDs are required.");
        await tx.LockAsync($"learning:operation:{owner}:{operation}", ct);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(ContentJson.Write(payload)))));
        var previous = (await store.Read<OperationReceipt>(x => x.OwnerId == owner && x.OperationId == operation, ct)).SingleOrDefault();
        if (previous is not null)
        {
            if (previous.Kind != kind || previous.TargetId != target || previous.Digest != digest) throw new InvalidOperationException("Operation ID was already used for another command.");
            LearningDiagnostics.Replays.Add(1);
            return new CommandOutcome(kind == "start" ? previous.OutcomeId : target, previous.OutcomeId, previous.ResponseVersion, true);
        }
        var outcome = await action();
        store.Add(new OperationReceipt { OwnerId = owner, OperationId = operation, Kind = kind, TargetId = target, Digest = digest, OutcomeId = outcome.OutcomeId, ResponseVersion = outcome.ReceiptVersion });
        return outcome;
    }, ct);
    private static string Canonical(string json)
    {
        using var document = JsonDocument.Parse(json);
        object? Value(JsonElement e) => e.ValueKind switch { JsonValueKind.Object => e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToDictionary(x => x.Name, x => Value(x.Value)), JsonValueKind.Array => e.EnumerateArray().Select(Value).ToArray(), _ => e.Clone() };
        return ContentJson.Write(Value(document.RootElement));
    }
    private void Event(Guid owner, Guid operation, string kind, Guid? session = null, SessionItem? item = null, Guid? attempt = null, Guid? material = null) => store.Add(new LearningEvent { OwnerId = owner, OperationId = operation, Kind = kind, SessionId = session, ItemId = item?.Id, ExerciseVersionId = item?.ExerciseVersionId, AttemptId = attempt, MaterialVersionId = material, ReceivedAt = clock.GetUtcNow() });
    private async Task<LearningSession> Mutable(Guid owner, Guid id, int version, CancellationToken ct)
    {
        await tx.LockAsync($"learning:session:{id}", ct);
        var session = await Required<LearningSession>(x => x.OwnerId == owner && x.Id == id, ct); VersionPrecondition.RequireCurrent(session.Version, version); return session;
    }
    public Task<CommandOutcome> Lifecycle(Guid owner, Guid id, int version, Guid operation, string action, CancellationToken ct) => Operation(owner, operation, action, id, new { action }, async () =>
    {
        var session = await Mutable(owner, id, version, ct);
        switch (action)
        {
            case "pause" when session.State == SessionState.Active: session.State = SessionState.Paused; break;
            case "resume" when session.State == SessionState.Paused: session.State = SessionState.Active; break;
            case "end" when session.State is SessionState.Active or SessionState.Paused: session.State = SessionState.EndedEarly; break;
            case "advance" when session.State is SessionState.Active or SessionState.Completed:
                var item = await Required<SessionItem>(x => x.SessionId == id && x.Position == session.Position, ct);
                if (item.Outcome is null) throw new InvalidOperationException("Finish or skip this item first.");
                session.Position = Math.Min(session.Position + 1, session.ActualCount); break;
            default: throw new InvalidOperationException("This transition is unavailable.");
        }
        session.Version++; session.UpdatedAt = clock.GetUtcNow(); Event(owner, operation, action, id);
        return new CommandOutcome(id, id, session.Version, false);
    }, ct);
    public Task<CommandOutcome> SaveDraft(Guid owner, Guid sessionId, Guid itemId, int version, Guid operation, Draft draft, CancellationToken ct) => Operation(owner, operation, "draft", sessionId, new { itemId, draft }, async () =>
    {
        var session = await Mutable(owner, sessionId, version, ct); var item = await Current(owner, session, itemId, ct);
        if (item.Outcome != null) throw new InvalidOperationException("Submitted items cannot be edited.");
        if (draft.Note is null || draft.Note.Length > 10000) throw new ArgumentException("Note must be text with at most 10000 characters.");
        item.DraftJson = ContentJson.Write(draft.Answer); item.Note = draft.Note; session.Version++; session.UpdatedAt = clock.GetUtcNow();
        return new CommandOutcome(sessionId, itemId, session.Version, false);
    }, ct);
    private async Task<SessionItem> Current(Guid owner, LearningSession session, Guid itemId, CancellationToken ct)
    {
        var item = await Required<SessionItem>(x => x.OwnerId == owner && x.SessionId == session.Id && x.Id == itemId, ct);
        if (session.State != SessionState.Active || item.Position != session.Position) throw new InvalidOperationException("This is not the active item."); return item;
    }
    public Task<CommandOutcome> ItemAction(Guid owner, Guid sessionId, Guid itemId, int version, string action, ItemCommand command, CancellationToken ct) => Operation(owner, command.OperationId, action, sessionId, new { itemId, command }, async () =>
    {
        var session = await Mutable(owner, sessionId, version, ct);
        var item = await Required<SessionItem>(x => x.OwnerId == owner && x.SessionId == sessionId && x.Id == itemId, ct);
        var retry = command.PreviousAttemptId is not null;
        if (!retry) await Current(owner, session, itemId, ct);
        else if (action != "attempts" || item.Outcome != ItemOutcome.Answered) throw new InvalidOperationException("Only answered objective items can be retried.");
        var exercise = await Required<ExerciseVersion>(x => x.Id == item.ExerciseVersionId, ct);
        if (command.Note is null || command.Note.Length > 10000) throw new ArgumentException("Note must be text with at most 10000 characters.");
        if (item.Outcome != null && !retry) throw new InvalidOperationException("This item is already terminal.");
        Guid outcome = itemId;
        if (action == "assistance")
        {
            switch (command.Assistance)
            {
                case "Hint": if (ContentJson.Read<string[]>(exercise.HintsJson).Length == 0) throw new ArgumentException("No hint available."); item.HintUsed = true; break;
                case "Reference": if (exercise.MaterialVersionId is null) throw new ArgumentException("No reference available."); item.ReferenceUsed = true; break;
                case "Reveal" when exercise.Type == ExerciseType.Flashcard: item.Revealed = true; break;
                default: throw new ArgumentException("Unsupported assistance.");
            }
        }
        else if (action == "skip") item.Outcome = ItemOutcome.Skipped;
        else
        {
            int? score = null; string? rating = null;
            if (action == "self-reviews")
            {
                if (exercise.Type != ExerciseType.Flashcard || !item.Revealed || command.Rating is not ("Again" or "Partly" or "Recalled")) throw new ArgumentException("Reveal the flashcard and select a rating."); rating = command.Rating;
            }
            else if (action == "attempts") score = Evaluator.Evaluate(exercise, command.Answer ?? throw new ArgumentException("Answer is required."));
            else throw new ArgumentException("Unsupported item action.");
            if (retry) await Required<LearningAttempt>(x => x.OwnerId == owner && x.ItemId == itemId && x.Id == command.PreviousAttemptId, ct);
            var attempt = new LearningAttempt { OwnerId = owner, SessionId = sessionId, ItemId = itemId, ExerciseVersionId = exercise.Id, PreviousAttemptId = command.PreviousAttemptId, AnswerJson = ContentJson.Write(command.Answer), Note = command.Note, Assisted = item.HintUsed || item.ReferenceUsed, AnswerAware = retry, ReceivedAt = clock.GetUtcNow() };
            var result = new ExerciseResult { OwnerId = owner, AttemptId = attempt.Id, Score = score, SelfReview = rating, EvaluatorVersion = exercise.EvaluatorVersion };
            store.Add(attempt); store.Add(result);
            var concepts = (await store.Read<ExerciseConcept>(x => x.ExerciseVersionId == exercise.Id, ct)).Select(x => x.ConceptId).ToArray();
            await knowledge.Stage(new(owner, result.Id, attempt.Id, exercise.FamilyId, sessionId, concepts, score, rating, attempt.Assisted, retry, attempt.ReceivedAt), ct);
            item.Outcome = score is null ? ItemOutcome.SelfReviewed : ItemOutcome.Answered;
            item.Note = command.Note; outcome = attempt.Id;
            Event(owner, command.OperationId, action, sessionId, item, attempt.Id);
        }
        if (action is "skip" or "assistance") Event(owner, command.OperationId, command.Assistance ?? action, sessionId, item);
        var items = await store.Read<SessionItem>(x => x.SessionId == sessionId, ct);
        if (session.State == SessionState.Active && items.All(x => x.Outcome != null)) session.State = SessionState.Completed;
        session.Version++; session.UpdatedAt = clock.GetUtcNow();
        return new CommandOutcome(sessionId, outcome, session.Version, false);
    }, ct);
    public async Task<SessionView> Session(Guid owner, Guid id, CancellationToken ct)
    {
        var session = await Required<LearningSession>(x => x.OwnerId == owner && x.Id == id, ct);
        var items = await store.Read<SessionItem>(x => x.SessionId == id && x.OwnerId == owner, ct);
        var views = new List<ItemPresentation>();
        foreach (var item in items.OrderBy(x => x.Position))
        {
            var exercise = await Required<ExerciseVersion>(x => x.Id == item.ExerciseVersionId, ct);
            var attempts = await store.Read<LearningAttempt>(x => x.OwnerId == owner && x.ItemId == item.Id, ct);
            var feedback = new List<Feedback>(); foreach (var attempt in attempts.OrderBy(x => x.ReceivedAt).ThenBy(x => x.Id)) feedback.Add(await Attempt(owner, attempt.Id, ct));
            views.Add(new(item.Id, exercise.Id, exercise.PrimaryConceptId, item.Position, exercise.Type, exercise.Locale, ContentJson.Read<ContentBlock[]>(exercise.PromptJson), ContentJson.Read<Interaction>(exercise.InteractionJson), new(item.DraftJson == "null" ? null : ContentJson.Read<Answer>(item.DraftJson), item.Note), item.Outcome, item.HintUsed, item.ReferenceUsed, item.Revealed, ContentJson.Read<string[]>(exercise.HintsJson).Length, exercise.MaterialVersionId, feedback.ToArray()));
        }
        return new(session, views.ToArray());
    }
    public async Task<Feedback> Attempt(Guid owner, Guid id, CancellationToken ct)
    {
        var attempt = await Required<LearningAttempt>(x => x.OwnerId == owner && x.Id == id, ct);
        var result = await Required<ExerciseResult>(x => x.OwnerId == owner && x.AttemptId == id, ct);
        var exercise = await Required<ExerciseVersion>(x => x.Id == attempt.ExerciseVersionId, ct);
        var evaluation = ContentJson.Read<EvaluationDefinition>(exercise.EvaluationJson);
        return new(attempt.Id, result.Id, result.Score, result.SelfReview, attempt.AnswerJson == "null" ? null : ContentJson.Read<Answer>(attempt.AnswerJson), attempt.Note, attempt.Assisted, attempt.AnswerAware, attempt.PreviousAttemptId, evaluation.Explanation, evaluation.ReferenceAnswer, attempt.ReceivedAt);
    }
    public async Task<AssistanceView> Assistance(Guid owner, Guid sessionId, Guid itemId, CancellationToken ct)
    {
        var item = await Required<SessionItem>(x => x.OwnerId == owner && x.SessionId == sessionId && x.Id == itemId, ct);
        var exercise = await Required<ExerciseVersion>(x => x.Id == item.ExerciseVersionId, ct);
        return new(item.HintUsed ? ContentJson.Read<string[]>(exercise.HintsJson) : [], item.ReferenceUsed ? exercise.MaterialVersionId : null, item.Revealed ? ContentJson.Read<EvaluationDefinition>(exercise.EvaluationJson).ReferenceAnswer : null);
    }
}
