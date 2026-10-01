using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Identity.Domain;
namespace Sensei.Host.Persistence;

internal sealed class LearningSessionOwnerMapping : IEntityTypeConfiguration<LearningSession> { public void Configure(EntityTypeBuilder<LearningSession> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class SessionItemOwnerMapping : IEntityTypeConfiguration<SessionItem> { public void Configure(EntityTypeBuilder<SessionItem> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningAttemptOwnerMapping : IEntityTypeConfiguration<LearningAttempt> { public void Configure(EntityTypeBuilder<LearningAttempt> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ExerciseResultOwnerMapping : IEntityTypeConfiguration<ExerciseResult> { public void Configure(EntityTypeBuilder<ExerciseResult> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningEventOwnerMapping : IEntityTypeConfiguration<LearningEvent> { public void Configure(EntityTypeBuilder<LearningEvent> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class OperationReceiptOwnerMapping : IEntityTypeConfiguration<OperationReceipt> { public void Configure(EntityTypeBuilder<OperationReceipt> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LearningGoalOwnerMapping : IEntityTypeConfiguration<LearningGoal> { public void Configure(EntityTypeBuilder<LearningGoal> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class RoadmapEnrollmentOwnerMapping : IEntityTypeConfiguration<RoadmapEnrollment> { public void Configure(EntityTypeBuilder<RoadmapEnrollment> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class KnowledgeStateOwnerMapping : IEntityTypeConfiguration<KnowledgeState> { public void Configure(EntityTypeBuilder<KnowledgeState> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class SelfReviewAllowanceOwnerMapping : IEntityTypeConfiguration<SelfReviewAllowance> { public void Configure(EntityTypeBuilder<SelfReviewAllowance> b) { b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class AllowanceConceptMapping : IEntityTypeConfiguration<SelfReviewAllowance> { public void Configure(EntityTypeBuilder<SelfReviewAllowance> b) => b.HasOne<Concept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict); }
