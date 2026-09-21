using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Infrastructure;

public static class WorkReflectionInfrastructure
{
    public static IServiceCollection AddWorkReflectionModule(this IServiceCollection services) => services
        .AddSingleton<IWorkEpisodeRepository, InMemoryWorkEpisodeRepository>()
        .AddScoped<IWorkEpisodeService, WorkEpisodeService>();
}

internal sealed class InMemoryWorkEpisodeRepository : IWorkEpisodeRepository
{
    private readonly ConcurrentDictionary<(Guid OwnerId, Guid Id), WorkEpisode> _episodes = new();

    public Task AddAsync(WorkEpisode episode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _episodes[(episode.OwnerId, episode.Id)] = episode;
        return Task.CompletedTask;
    }

    public Task<WorkEpisode?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _episodes.TryGetValue((ownerId, id), out var episode);
        return Task.FromResult(episode);
    }

    public Task<IReadOnlyCollection<WorkEpisode>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<WorkEpisode>>(_episodes.Values
            .Where(episode => episode.OwnerId == ownerId && (includeArchived || !episode.IsArchived))
            .OrderByDescending(episode => episode.EventDate)
            .ToArray());
    }
}
