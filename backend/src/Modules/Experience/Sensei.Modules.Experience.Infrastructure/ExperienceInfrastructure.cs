using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Experience.Application;
using Sensei.Modules.Experience.Domain;

namespace Sensei.Modules.Experience.Infrastructure;

public static class ExperienceInfrastructure
{
    public static IServiceCollection AddExperienceModule(this IServiceCollection services) => services
        .AddScoped<IExperienceEntryRepository, EfExperienceEntryRepository>()
        .AddScoped<IExperienceEntryService, ExperienceEntryService>();
}

internal sealed class EfExperienceEntryRepository(SenseiDbContext dbContext) : IExperienceEntryRepository
{
    public async Task AddAsync(ExperienceEntry entry, CancellationToken cancellationToken) =>
        await dbContext.Set<ExperienceEntry>().AddAsync(entry, cancellationToken);

    public Task<ExperienceEntry?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<ExperienceEntry>()
            .Include(entry => entry.Revisions)
            .ThenInclude(revision => revision.Concepts)
            .SingleOrDefaultAsync(entry => entry.OwnerId == ownerId && entry.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<ExperienceEntry>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        await dbContext.Set<ExperienceEntry>()
            .AsNoTracking()
            .Include(entry => entry.Revisions)
            .ThenInclude(revision => revision.Concepts)
            .Where(entry => entry.OwnerId == ownerId && (includeArchived || !entry.IsArchived))
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
}

internal sealed class ExperienceEntryConfiguration : IEntityTypeConfiguration<ExperienceEntry>
{
    public void Configure(EntityTypeBuilder<ExperienceEntry> builder)
    {
        builder.ToTable("entries", "experience", table =>
        {
            table.HasCheckConstraint("ck_entries_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_entries_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_entries_version_positive", "version >= 1");
        });
        builder.HasKey(entry => entry.Id).HasName("pk_entries");
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.OwnerId).HasColumnName("owner_id");
        builder.Property(entry => entry.IsArchived).HasColumnName("is_archived");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(entry => entry.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Ignore(entry => entry.CurrentRevision);
        builder.HasMany(entry => entry.Revisions)
            .WithOne()
            .HasForeignKey(revision => revision.EntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entry => entry.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(entry => new { entry.OwnerId, entry.Id }).IsUnique().HasDatabaseName("ux_entries_owner_id");
        builder.HasIndex(entry => new { entry.OwnerId, entry.CreatedAt, entry.Id }).HasDatabaseName("ix_entries_owner_created_id");
    }
}

internal sealed class ExperienceRevisionConfiguration : IEntityTypeConfiguration<ExperienceRevision>
{
    public void Configure(EntityTypeBuilder<ExperienceRevision> builder)
    {
        builder.ToTable("entry_revisions", "experience", table =>
        {
            table.HasCheckConstraint("ck_entry_revisions_number_positive", "number >= 1");
            table.HasCheckConstraint("ck_entry_revisions_setting", "setting IN ('Employment', 'Coursework', 'PersonalProject', 'Other')");
            table.HasCheckConstraint("ck_entry_revisions_impact", "impact_state IN ('Unknown', 'Qualitative', 'Measured')");
            table.HasCheckConstraint("ck_entry_revisions_approval", "approval_state IN ('Draft', 'Approved', 'Retracted')");
        });
        builder.HasKey(revision => new { revision.EntryId, revision.Number }).HasName("pk_entry_revisions");
        builder.Property(revision => revision.EntryId).HasColumnName("entry_id").ValueGeneratedNever();
        builder.Property(revision => revision.Number).HasColumnName("number").ValueGeneratedNever();
        builder.Property(revision => revision.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
        builder.Property(revision => revision.Setting).HasColumnName("setting").HasConversion<string>().HasMaxLength(32);
        builder.Property(revision => revision.Context).HasColumnName("context").IsRequired();
        builder.Property(revision => revision.Role).HasColumnName("role").IsRequired();
        builder.Property(revision => revision.Actions).HasColumnName("actions").IsRequired();
        builder.Property(revision => revision.Alternatives).HasColumnName("alternatives").IsRequired();
        builder.Property(revision => revision.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(revision => revision.ImpactState).HasColumnName("impact_state").HasConversion<string>().HasMaxLength(32);
        builder.Property(revision => revision.ApprovalState).HasColumnName("approval_state").HasConversion<string>().HasMaxLength(32);
        builder.Property(revision => revision.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(revision => revision.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamp with time zone");
        builder.Ignore(revision => revision.ConceptIds);
        builder.HasMany(revision => revision.Concepts)
            .WithOne()
            .HasForeignKey(concept => new { concept.EntryId, concept.RevisionNumber })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(revision => revision.Concepts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ExperienceRevisionConceptConfiguration : IEntityTypeConfiguration<ExperienceRevisionConcept>
{
    public void Configure(EntityTypeBuilder<ExperienceRevisionConcept> builder)
    {
        builder.ToTable("entry_revision_concepts", "experience", table =>
            table.HasCheckConstraint("ck_entry_revision_concepts_id_not_empty", "concept_id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.HasKey(concept => new { concept.EntryId, concept.RevisionNumber, concept.ConceptId })
            .HasName("pk_entry_revision_concepts");
        builder.Property(concept => concept.EntryId).HasColumnName("entry_id").ValueGeneratedNever();
        builder.Property(concept => concept.RevisionNumber).HasColumnName("revision_number").ValueGeneratedNever();
        builder.Property(concept => concept.ConceptId).HasColumnName("concept_id").ValueGeneratedNever();
        builder.HasIndex(concept => concept.ConceptId).HasDatabaseName("ix_entry_revision_concepts_concept");
    }
}
