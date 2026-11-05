using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Infrastructure;

internal sealed class AssessmentJobMapping : IEntityTypeConfiguration<AssessmentJob>
{
    public void Configure(EntityTypeBuilder<AssessmentJob> b)
    {
        RuntimeMapping.Configure(b, "assessment_jobs"); b.Ignore(x => x.Terminal);
        b.HasAlternateKey(x => new { x.OwnerId, x.Id, x.AttemptId, x.ExerciseVersionId });
        b.HasAlternateKey(x => new { x.OwnerId, x.Id });
        b.HasIndex(x => new { x.OwnerId, x.OperationId }).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.AttemptId }).IsUnique().HasFilter("state IN ('Queued', 'Running', 'Retry')");
        b.HasIndex(x => new { x.State, x.NotBefore, x.CreatedAt });
        b.HasOne<LearningAttempt>().WithMany().HasForeignKey(x => new { x.OwnerId, x.AttemptId, x.ExerciseVersionId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id, x.ExerciseVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("assessment_jobs", "learning", t =>
        {
            t.HasCheckConstraint("ck_assessment_bounds", "invocation_count BETWEEN 0 AND 3 AND transient_retries BETWEEN 0 AND 1 AND repairs BETWEEN 0 AND 1 AND version >= 1 AND generation >= 0 AND fence >= 0");
            t.HasCheckConstraint("ck_assessment_state", "state IN ('Queued','Running','Retry','Succeeded','Failed','Cancelled','Obsolete') AND ((state = 'Running') = (lease_until IS NOT NULL))");
            t.HasCheckConstraint("ck_assessment_origin", "origin IN ('Provider','Fixture')");
        });
    }
}
internal sealed class AssessmentInvocationMapping : IEntityTypeConfiguration<AssessmentInvocation>
{
    public void Configure(EntityTypeBuilder<AssessmentInvocation> b)
    {
        RuntimeMapping.Configure(b, "assessment_invocations");
        b.HasAlternateKey(x => new { x.OwnerId, x.JobId, x.Id });
        b.HasIndex(x => new { x.JobId, x.Number }).IsUnique();
        b.HasIndex(x => x.StartedAt);
        b.HasOne<AssessmentJob>().WithMany().HasForeignKey(x => new { x.OwnerId, x.JobId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssessmentFeedbackMapping : IEntityTypeConfiguration<AssessmentFeedbackRevision>
{
    public void Configure(EntityTypeBuilder<AssessmentFeedbackRevision> b)
    {
        RuntimeMapping.Configure(b, "assessment_feedback_revisions");
        b.HasIndex(x => x.JobId).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.AttemptId, x.Revision }).IsUnique();
        b.HasOne<AssessmentJob>().WithMany().HasForeignKey(x => new { x.OwnerId, x.JobId, x.AttemptId, x.ExerciseVersionId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id, x.AttemptId, x.ExerciseVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssessmentInvocation>().WithMany().HasForeignKey(x => new { x.OwnerId, x.JobId, x.InvocationId })
            .HasPrincipalKey(x => new { x.OwnerId, x.JobId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
