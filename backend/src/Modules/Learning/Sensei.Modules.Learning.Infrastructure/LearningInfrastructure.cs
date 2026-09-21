using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Infrastructure;

public static class LearningInfrastructure
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services) => services
        .AddScoped<IConceptRepository, EfConceptRepository>()
        .AddScoped<IConceptService, ConceptService>();
}

internal sealed class EfConceptRepository(SenseiDbContext dbContext) : IConceptRepository
{
    public async Task AddAsync(Concept concept, CancellationToken cancellationToken) =>
        await dbContext.Set<Concept>().AddAsync(concept, cancellationToken);

    public Task<Concept?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Concept>().SingleOrDefaultAsync(concept => concept.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Concept>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken) =>
        await dbContext.Set<Concept>()
            .AsNoTracking()
            .Where(concept => includeInactive || concept.IsActive)
            .OrderBy(concept => concept.Key)
            .ThenBy(concept => concept.Id)
            .ToArrayAsync(cancellationToken);

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = key.Trim().ToLowerInvariant().Replace(' ', '-');
        return dbContext.Set<Concept>().AnyAsync(concept => concept.Key == normalizedKey, cancellationToken);
    }
}

internal sealed class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.ToTable("concepts", "learning", table =>
        {
            table.HasCheckConstraint("ck_concepts_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_concepts_version_positive", "version >= 1");
            table.HasCheckConstraint("ck_concepts_difficulty", "difficulty IN ('Beginner', 'Intermediate', 'Advanced')");
        });
        builder.HasKey(concept => concept.Id).HasName("pk_concepts");
        builder.Property(concept => concept.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(concept => concept.Key).HasColumnName("key").HasMaxLength(160).IsRequired();
        builder.Property(concept => concept.Name).HasColumnName("name").HasMaxLength(240).IsRequired();
        builder.Property(concept => concept.Description).HasColumnName("description").IsRequired();
        builder.Property(concept => concept.Locale).HasColumnName("locale").HasMaxLength(32).IsRequired();
        builder.Property(concept => concept.Difficulty).HasColumnName("difficulty").HasConversion<string>().HasMaxLength(32);
        builder.Property(concept => concept.IsActive).HasColumnName("is_active");
        builder.Property(concept => concept.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(concept => concept.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(concept => concept.Key).IsUnique().HasDatabaseName("ux_concepts_key");
        builder.HasIndex(concept => new { concept.Key, concept.Id }).HasDatabaseName("ix_concepts_key_id");
    }
}
