using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Identity.Domain;

namespace Sensei.Host.Persistence;

internal sealed class LearningKnowledgeAdapter(KnowledgeService knowledge) : ILearningKnowledge
{
    public async Task Stage(ResultEvidence r, CancellationToken ct)
    {
        try { await knowledge.StageAsync(new(r.OwnerId, r.ResultId, r.AttemptId, r.FamilyId, r.SessionId, r.ConceptIds, r.Score, r.SelfReview, r.Assisted, r.AnswerAware, r.ReceivedAt), ct); }
        catch { Sensei.BuildingBlocks.Application.LearningDiagnostics.ProjectionFailures.Add(1); throw; }
    }
    public async Task<KnowledgeSnapshot[]> Read(Guid owner, Guid? concept, CancellationToken ct) => (await knowledge.Summaries(owner, concept, ct)).Select(x => new KnowledgeSnapshot(x.ConceptId, x.InternalEstimate, x.FamilyCount, x.AssistedCount, x.SelfReviewCount, x.QualifyingFamilies, x.QualifyingSessions, x.Correctness, x.ObservationIds, x.NextPositiveReviewAt, x.RecentFamilies)).ToArray();
}
internal sealed class ContributionRelations : IEntityTypeConfiguration<KnowledgeContribution>
{
    public void Configure(EntityTypeBuilder<KnowledgeContribution> b)
    {
        b.HasOne<ExerciseResult>().WithMany().HasForeignKey(x => new { x.OwnerId, x.ResultId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LearningAttempt>().WithMany().HasForeignKey(x => new { x.OwnerId, x.AttemptId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExerciseFamily>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LearningSession>().WithMany().HasForeignKey(x => new { x.OwnerId, x.SessionId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
