using System.Linq.Expressions;
using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Application;

public interface IKnowledgeStore
{
    Task<T[]> Read<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    void Add<T>(T entity) where T : class;
}
public sealed record TrustedLearningResult(Guid OwnerId, Guid ResultId, Guid AttemptId, Guid FamilyId, Guid SessionId, Guid[] ConceptIds, int? Score, string? SelfReview, bool Assisted, bool AnswerAware, DateTimeOffset ReceivedAt);
public sealed record KnowledgeSummary(Guid ConceptId, double? InternalEstimate, int FamilyCount, int AssistedCount, int SelfReviewCount, int QualifyingFamilies, int QualifyingSessions, double? Correctness, Guid[] ObservationIds, DateTimeOffset? NextPositiveReviewAt, Guid[] RecentFamilies);
public sealed class KnowledgeService(IKnowledgeStore store, ITransactionRunner transactions, TimeProvider clock)
{
    public async Task StageAsync(TrustedLearningResult result, CancellationToken ct)
    {
        foreach (var concept in result.ConceptIds.Distinct().Order())
            await transactions.LockAsync($"knowledge:{result.OwnerId}:{concept}", ct);
        foreach (var concept in result.ConceptIds.Distinct().Order())
        {
            var allowance = (await store.Read<SelfReviewAllowance>(x => x.OwnerId == result.OwnerId && x.ConceptId == concept, ct)).SingleOrDefault();
            var admitted = result.SelfReview == "Recalled" && KnowledgePolicy.CanAdmit(result.ReceivedAt, allowance?.LastAdmittedAt);
            if (admitted)
            {
                if (allowance is null) store.Add(new SelfReviewAllowance { OwnerId = result.OwnerId, ConceptId = concept, LastAdmittedAt = result.ReceivedAt });
                else allowance.LastAdmittedAt = result.ReceivedAt;
            }
            var observation = EvidenceObservation.Create(result.OwnerId, concept, "Learning practice", result.Score is null ? EvidenceSignalKind.Recall : EvidenceSignalKind.Scenario, EvidenceSourceKind.Attempt, result.AttemptId,
                result.Score is null ? AssistanceLevel.SelfReviewed : result.Assisted ? AssistanceLevel.ReferenceReviewed : AssistanceLevel.None, "Deterministic learning result; knowledge-v1", result.ReceivedAt);
            store.Add(observation);
            var contribution = new KnowledgeContribution { ObservationId = observation.Id, OwnerId = result.OwnerId, ConceptId = concept, ResultId = result.ResultId, AttemptId = result.AttemptId, FamilyId = result.FamilyId, SessionId = result.SessionId, Score = result.Score, SelfReview = result.SelfReview, Assisted = result.Assisted, AnswerAware = result.AnswerAware, Admitted = admitted, ReceivedAt = result.ReceivedAt };
            store.Add(contribution);
            await RebuildAsync(result.OwnerId, concept, ct, contribution);
        }
    }
    public async Task RebuildAsync(Guid owner, Guid concept, CancellationToken ct, KnowledgeContribution? pending = null)
    {
        await transactions.LockAsync($"knowledge:{owner}:{concept}", ct);
        var active = await Active(owner, concept, ct);
        if (pending is not null) active = [.. active, pending];
        var state = (await store.Read<KnowledgeState>(x => x.OwnerId == owner && x.ConceptId == concept, ct)).SingleOrDefault();
        if (state is null) { state = new KnowledgeState { OwnerId = owner, ConceptId = concept }; store.Add(state); }
        state.Estimate = KnowledgePolicy.Estimate(await EstimateInputs(owner, concept, active, pending, ct)); state.UpdatedAt = clock.GetUtcNow();
    }
    public async Task<KnowledgeSummary[]> Summaries(Guid owner, Guid? concept, CancellationToken ct)
    {
        var contributions = await store.Read<KnowledgeContribution>(x => x.OwnerId == owner && (concept == null || x.ConceptId == concept), ct);
        var summaries = new List<KnowledgeSummary>();
        foreach (var id in contributions.Select(x => x.ConceptId).Distinct().Order())
        {
            var active = await Active(owner, id, ct);
            var objective = KnowledgePolicy.Objective(active); var qualifying = KnowledgePolicy.Objective(active, true);
            var allowance = (await store.Read<SelfReviewAllowance>(x => x.OwnerId == owner && x.ConceptId == id, ct)).SingleOrDefault();
            summaries.Add(new(id, KnowledgePolicy.Estimate(await EstimateInputs(owner, id, active, null, ct)), objective.Length, objective.Count(x => x.Assisted), active.Count(x => x.SelfReview != null), qualifying.Length, qualifying.Select(x => x.SessionId).Distinct().Count(), qualifying.Length == 0 ? null : qualifying.Average(x => (double)x.Score!.Value), qualifying.Select(x => x.ObservationId).ToArray(), allowance?.LastAdmittedAt + KnowledgePolicy.Cooldown, active.Where(x => x.ReceivedAt >= clock.GetUtcNow().AddHours(-24)).Select(x => x.FamilyId).Distinct().ToArray()));
        }
        return summaries.ToArray();
    }
    private async Task<KnowledgeContribution[]> Active(Guid owner, Guid concept, CancellationToken ct)
    {
        var observations = await store.Read<EvidenceObservation>(x => x.OwnerId == owner && x.ConceptId == concept, ct);
        var ids = observations.Where(x => x.Status == EvidenceStatus.Active).Select(x => x.Id).ToHashSet();
        return (await store.Read<KnowledgeContribution>(x => x.OwnerId == owner && x.ConceptId == concept, ct)).Where(x => ids.Contains(x.ObservationId)).ToArray();
    }
    private async Task<KnowledgeContribution[]> EstimateInputs(Guid owner, Guid concept, KnowledgeContribution[] active, KnowledgeContribution? pending, CancellationToken ct)
    {
        var admissions = await store.Read<KnowledgeContribution>(x => x.OwnerId == owner && x.ConceptId == concept && x.Admitted, ct);
        if (pending?.Admitted == true) admissions = [.. admissions, pending];
        // A withdrawn latest admission does not revive an older positive contribution.
        var latest = admissions.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id).FirstOrDefault()?.Id;
        return active.Where(x => !x.Admitted || x.Id == latest).ToArray();
    }
    public void RecordStatus(EvidenceObservation observation) => store.Add(new EvidenceStatusRevision { ObservationId = observation.Id, Status = observation.Status, ReceivedAt = clock.GetUtcNow() });
}
