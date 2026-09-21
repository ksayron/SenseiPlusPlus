using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Infrastructure;

public static class WorkReflectionInfrastructure
{
    public static IServiceCollection AddWorkReflectionModule(this IServiceCollection services) => services
        .AddScoped<IWorkEpisodeRepository, EfWorkEpisodeRepository>()
        .AddScoped<IWorkEpisodeService, WorkEpisodeService>();
}

internal sealed class EfWorkEpisodeRepository(SenseiDbContext dbContext) : IWorkEpisodeRepository
{
    public async Task AddAsync(WorkEpisode episode, CancellationToken cancellationToken) =>
        await dbContext.Set<WorkEpisode>().AddAsync(episode, cancellationToken);

    public Task<WorkEpisode?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<WorkEpisode>()
            .SingleOrDefaultAsync(episode => episode.OwnerId == ownerId && episode.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<WorkEpisode>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        await dbContext.Set<WorkEpisode>()
            .AsNoTracking()
            .Where(episode => episode.OwnerId == ownerId && (includeArchived || !episode.IsArchived))
            .OrderByDescending(episode => episode.EventDate)
            .ThenByDescending(episode => episode.Id)
            .ToArrayAsync(cancellationToken);
}

internal sealed class WorkEpisodeConfiguration : IEntityTypeConfiguration<WorkEpisode>
{
    public void Configure(EntityTypeBuilder<WorkEpisode> builder)
    {
        builder.ToTable("work_episodes", "work_reflection", table =>
        {
            table.HasCheckConstraint("ck_work_episodes_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_work_episodes_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_work_episodes_version_positive", "version >= 1");
            table.HasCheckConstraint("ck_work_episodes_setting", "setting IN ('Employment', 'Coursework', 'PersonalProject', 'Other')");
        });
        builder.HasKey(episode => episode.Id).HasName("pk_work_episodes");
        builder.Property(episode => episode.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(episode => episode.OwnerId).HasColumnName("owner_id");
        builder.Property(episode => episode.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
        builder.Property(episode => episode.Setting).HasColumnName("setting").HasConversion<string>().HasMaxLength(32);
        builder.Property(episode => episode.EventDate).HasColumnName("event_date").HasColumnType("date");
        builder.Property(episode => episode.Role).HasColumnName("role").HasMaxLength(240).IsRequired();
        builder.Property(episode => episode.Summary).HasColumnName("summary").IsRequired();
        builder.Property(episode => episode.IsArchived).HasColumnName("is_archived");
        builder.Property(episode => episode.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(episode => episode.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(episode => episode.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(episode => new { episode.OwnerId, episode.Id }).IsUnique().HasDatabaseName("ux_work_episodes_owner_id");
        builder.HasIndex(episode => new { episode.OwnerId, episode.EventDate, episode.Id }).HasDatabaseName("ix_work_episodes_owner_event_id");
    }
}
