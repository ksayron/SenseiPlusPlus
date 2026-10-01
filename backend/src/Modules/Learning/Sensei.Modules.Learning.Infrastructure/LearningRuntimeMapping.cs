using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;
namespace Sensei.Modules.Learning.Infrastructure;

public sealed class LearningStore(SenseiDbContext db) : ILearningStore
{
    public Task<T[]> Read<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => db.Set<T>().Where(predicate).ToArrayAsync(ct);
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void Remove<T>(T entity) where T : class => db.Remove(entity);
}
internal static class RuntimeMapping
{
    public static void Configure<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table, "learning");
        b.HasKey("Id");
        b.Property<Guid>("Id").ValueGeneratedNever();
        foreach (var property in typeof(T).GetProperties())
        {
            var p = b.Property(property.Name).HasColumnName(Regex.Replace(property.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsEnum) p.HasConversion<string>();
            if (property.Name.EndsWith("Json")) p.HasColumnType("jsonb");
            if (property.Name == "Version") p.IsConcurrencyToken();
        }
    }
}
internal sealed class ConceptRelationMapping : IEntityTypeConfiguration<ConceptRelation> { public void Configure(EntityTypeBuilder<ConceptRelation> b) { RuntimeMapping.Configure(b, "concept_relations"); b.HasIndex(x => new { x.SourceId, x.TargetId, x.Kind }).IsUnique(); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class MaterialVersionMapping : IEntityTypeConfiguration<MaterialVersion> { public void Configure(EntityTypeBuilder<MaterialVersion> b) { RuntimeMapping.Configure(b, "material_versions"); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ExerciseFamilyMapping : IEntityTypeConfiguration<ExerciseFamily> { public void Configure(EntityTypeBuilder<ExerciseFamily> b) { RuntimeMapping.Configure(b, "exercise_families"); b.HasIndex(x => x.Key).IsUnique(); } }
internal sealed class ExerciseVersionMapping : IEntityTypeConfiguration<ExerciseVersion> { public void Configure(EntityTypeBuilder<ExerciseVersion> b) { RuntimeMapping.Configure(b, "exercise_versions"); b.HasIndex(x => new { x.ExerciseId, x.Revision }).IsUnique(); b.HasOne<ExerciseFamily>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.PrimaryConceptId).OnDelete(DeleteBehavior.Restrict); b.HasOne<MaterialVersion>().WithMany().HasForeignKey(x => x.MaterialVersionId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ExerciseConceptMapping : IEntityTypeConfiguration<ExerciseConcept> { public void Configure(EntityTypeBuilder<ExerciseConcept> b) { RuntimeMapping.Configure(b, "exercise_concepts"); b.HasIndex(x => new { x.ExerciseVersionId, x.ConceptId }).IsUnique(); b.HasOne<ExerciseVersion>().WithMany().HasForeignKey(x => x.ExerciseVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningSessionMapping : IEntityTypeConfiguration<LearningSession> { public void Configure(EntityTypeBuilder<LearningSession> b) { RuntimeMapping.Configure(b, "sessions"); b.HasAlternateKey(x => new { x.OwnerId, x.Id }); b.HasIndex(x => x.OwnerId).IsUnique().HasFilter("state IN ('Active', 'Paused')"); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); b.HasOne<RoadmapStage>().WithMany().HasForeignKey(x => x.RoadmapStageId).OnDelete(DeleteBehavior.Restrict); b.HasOne<RoadmapEnrollment>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EnrollmentId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class SessionItemMapping : IEntityTypeConfiguration<SessionItem> { public void Configure(EntityTypeBuilder<SessionItem> b) { RuntimeMapping.Configure(b, "session_items"); b.HasAlternateKey(x => new { x.OwnerId, x.SessionId, x.Id, x.ExerciseVersionId }); b.HasIndex(x => new { x.SessionId, x.Position }).IsUnique(); b.HasOne<LearningSession>().WithMany().HasForeignKey(x => new { x.OwnerId, x.SessionId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); b.HasOne<ExerciseVersion>().WithMany().HasForeignKey(x => x.ExerciseVersionId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningAttemptMapping : IEntityTypeConfiguration<LearningAttempt> { public void Configure(EntityTypeBuilder<LearningAttempt> b) { RuntimeMapping.Configure(b, "attempts"); b.HasAlternateKey(x => new { x.OwnerId, x.Id }); b.HasOne<SessionItem>().WithMany().HasForeignKey(x => new { x.OwnerId, x.SessionId, x.ItemId, x.ExerciseVersionId }).HasPrincipalKey(x => new { x.OwnerId, x.SessionId, x.Id, x.ExerciseVersionId }).OnDelete(DeleteBehavior.Restrict); b.HasOne<LearningAttempt>().WithMany().HasForeignKey(x => new { x.OwnerId, x.PreviousAttemptId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ExerciseResultMapping : IEntityTypeConfiguration<ExerciseResult> { public void Configure(EntityTypeBuilder<ExerciseResult> b) { RuntimeMapping.Configure(b, "results"); b.HasAlternateKey(x => new { x.OwnerId, x.Id }); b.HasIndex(x => x.AttemptId).IsUnique(); b.HasOne<LearningAttempt>().WithMany().HasForeignKey(x => new { x.OwnerId, x.AttemptId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningEventMapping : IEntityTypeConfiguration<LearningEvent> { public void Configure(EntityTypeBuilder<LearningEvent> b) { RuntimeMapping.Configure(b, "events"); b.HasIndex(x => new { x.OwnerId, x.ReceivedAt, x.Id }); b.HasOne<LearningSession>().WithMany().HasForeignKey(x => new { x.OwnerId, x.SessionId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); b.HasOne<LearningAttempt>().WithMany().HasForeignKey(x => new { x.OwnerId, x.AttemptId }).HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict); b.HasOne<ExerciseVersion>().WithMany().HasForeignKey(x => x.ExerciseVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<MaterialVersion>().WithMany().HasForeignKey(x => x.MaterialVersionId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class OperationReceiptMapping : IEntityTypeConfiguration<OperationReceipt> { public void Configure(EntityTypeBuilder<OperationReceipt> b) { RuntimeMapping.Configure(b, "operation_receipts"); b.HasIndex(x => new { x.OwnerId, x.OperationId }).IsUnique(); } }
internal sealed class LearningGoalMapping : IEntityTypeConfiguration<LearningGoal> { public void Configure(EntityTypeBuilder<LearningGoal> b) { RuntimeMapping.Configure(b, "goals"); b.HasOne<RoadmapVersion>().WithMany().HasForeignKey(x => x.RoadmapVersionId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class GoalConceptMapping : IEntityTypeConfiguration<GoalConcept> { public void Configure(EntityTypeBuilder<GoalConcept> b) { RuntimeMapping.Configure(b, "goal_concepts"); b.HasIndex(x => new { x.GoalId, x.ConceptId }).IsUnique(); b.HasOne<LearningGoal>().WithMany().HasForeignKey(x => x.GoalId); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class RoadmapVersionMapping : IEntityTypeConfiguration<RoadmapVersion> { public void Configure(EntityTypeBuilder<RoadmapVersion> b) { RuntimeMapping.Configure(b, "roadmap_versions"); } }
internal sealed class RoadmapStageMapping : IEntityTypeConfiguration<RoadmapStage> { public void Configure(EntityTypeBuilder<RoadmapStage> b) { RuntimeMapping.Configure(b, "roadmap_stages"); b.HasIndex(x => new { x.RoadmapVersionId, x.Position }).IsUnique(); b.HasOne<RoadmapVersion>().WithMany().HasForeignKey(x => x.RoadmapVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class RoadmapEnrollmentMapping : IEntityTypeConfiguration<RoadmapEnrollment> { public void Configure(EntityTypeBuilder<RoadmapEnrollment> b) { RuntimeMapping.Configure(b, "roadmap_enrollments"); b.HasAlternateKey(x => new { x.OwnerId, x.Id }); b.HasIndex(x => new { x.OwnerId, x.RoadmapVersionId }).IsUnique(); b.HasOne<RoadmapVersion>().WithMany().HasForeignKey(x => x.RoadmapVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<RoadmapStage>().WithMany().HasForeignKey(x => x.CurrentStageId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class StageTraversalMapping : IEntityTypeConfiguration<StageTraversal> { public void Configure(EntityTypeBuilder<StageTraversal> b) { RuntimeMapping.Configure(b, "stage_traversals"); b.HasIndex(x => new { x.EnrollmentId, x.StageId }).IsUnique(); b.HasOne<RoadmapEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict); b.HasOne<RoadmapStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict); } }
