using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Infrastructure;

public static class EvidenceInfrastructure
{
    public static IServiceCollection AddEvidenceModule(this IServiceCollection services) => services
        .AddScoped<IEvidenceObservationRepository, EfEvidenceObservationRepository>()
        .AddScoped<IEvidenceObservationService, EvidenceObservationService>();
}

internal sealed class EfEvidenceObservationRepository(SenseiDbContext dbContext)
    : IEvidenceObservationRepository
{
    public async Task AddAsync(EvidenceObservation observation, CancellationToken cancellationToken) =>
        await dbContext.Set<EvidenceObservation>().AddAsync(observation, cancellationToken);

    public Task<EvidenceObservation?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<EvidenceObservation>()
            .SingleOrDefaultAsync(observation => observation.OwnerId == ownerId && observation.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<EvidenceObservation>> ListAsync(
        Guid ownerId,
        Guid? conceptId,
        bool includeInactive,
        CancellationToken cancellationToken) =>
        await dbContext.Set<EvidenceObservation>()
            .AsNoTracking()
            .Where(observation => observation.OwnerId == ownerId)
            .Where(observation => conceptId == null || observation.ConceptId == conceptId)
            .Where(observation => includeInactive || observation.Status == EvidenceStatus.Active)
            .OrderByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Id)
            .ToArrayAsync(cancellationToken);
}

internal sealed class EvidenceObservationConfiguration : IEntityTypeConfiguration<EvidenceObservation>
{
    public void Configure(EntityTypeBuilder<EvidenceObservation> builder)
    {
        builder.ToTable("observations", "evidence", table =>
        {
            table.HasCheckConstraint("ck_observations_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_observations_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_observations_version_positive", "version >= 1");
        });
        builder.HasKey(observation => observation.Id).HasName("pk_observations");
        builder.Property(observation => observation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(observation => observation.OwnerId).HasColumnName("owner_id");
        builder.Property(observation => observation.ConceptId).HasColumnName("concept_id");
        builder.Property(observation => observation.Aspect).HasColumnName("aspect").HasMaxLength(240).IsRequired();
        builder.Property(observation => observation.SignalKind).HasColumnName("signal_kind").HasConversion<string>().HasMaxLength(64);
        builder.Property(observation => observation.SourceKind).HasColumnName("source_kind").HasConversion<string>().HasMaxLength(64);
        builder.Property(observation => observation.SourceId).HasColumnName("source_id");
        builder.Property(observation => observation.Assistance).HasColumnName("assistance").HasConversion<string>().HasMaxLength(64);
        builder.Property(observation => observation.Conditions).HasColumnName("conditions").IsRequired();
        builder.Property(observation => observation.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(observation => observation.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone");
        builder.Property(observation => observation.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(observation => new { observation.OwnerId, observation.Id }).IsUnique().HasDatabaseName("ux_observations_owner_id");
        builder.HasIndex(observation => new { observation.OwnerId, observation.ObservedAt, observation.Id }).HasDatabaseName("ix_observations_owner_observed_id");
        builder.HasIndex(observation => new { observation.OwnerId, observation.ConceptId }).HasDatabaseName("ix_observations_owner_concept");
    }
}
