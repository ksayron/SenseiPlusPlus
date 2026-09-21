using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Host.Persistence;

internal sealed class WorkEpisodeOwnerConfiguration : IEntityTypeConfiguration<WorkEpisode>
{
    public void Configure(EntityTypeBuilder<WorkEpisode> builder) => builder
        .HasOne<UserAccount>()
        .WithMany()
        .HasForeignKey(episode => episode.OwnerId)
        .OnDelete(DeleteBehavior.Restrict)
        .HasConstraintName("fk_work_episodes_owner");
}

internal sealed class EvidenceRelationsConfiguration : IEntityTypeConfiguration<EvidenceObservation>
{
    public void Configure(EntityTypeBuilder<EvidenceObservation> builder)
    {
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(observation => observation.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observations_owner");
        builder.HasOne<Concept>()
            .WithMany()
            .HasForeignKey(observation => observation.ConceptId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observations_concept");
    }
}

internal sealed class ExperienceOwnerConfiguration : IEntityTypeConfiguration<ExperienceEntry>
{
    public void Configure(EntityTypeBuilder<ExperienceEntry> builder) => builder
        .HasOne<UserAccount>()
        .WithMany()
        .HasForeignKey(entry => entry.OwnerId)
        .OnDelete(DeleteBehavior.Restrict)
        .HasConstraintName("fk_entries_owner");
}

internal sealed class ExperienceConceptConfiguration : IEntityTypeConfiguration<ExperienceRevisionConcept>
{
    public void Configure(EntityTypeBuilder<ExperienceRevisionConcept> builder) => builder
        .HasOne<Concept>()
        .WithMany()
        .HasForeignKey(link => link.ConceptId)
        .OnDelete(DeleteBehavior.Restrict)
        .HasConstraintName("fk_entry_revision_concepts_concept");
}
