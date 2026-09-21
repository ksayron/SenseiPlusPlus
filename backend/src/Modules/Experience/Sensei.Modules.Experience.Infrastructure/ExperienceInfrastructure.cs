using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sensei.Modules.Experience.Application;
using Sensei.Modules.Experience.Domain;

namespace Sensei.Modules.Experience.Infrastructure;

public static class ExperienceInfrastructure
{
    public static IServiceCollection AddExperienceModule(this IServiceCollection services) => services
        .AddSingleton<IExperienceEntryRepository, InMemoryExperienceEntryRepository>()
        .AddScoped<IExperienceEntryService, ExperienceEntryService>();
}

internal sealed class InMemoryExperienceEntryRepository : IExperienceEntryRepository
{
    private readonly ConcurrentDictionary<(Guid OwnerId, Guid Id), ExperienceEntry> _entries = new();

    public Task AddAsync(ExperienceEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries[(entry.OwnerId, entry.Id)] = entry;
        return Task.CompletedTask;
    }

    public Task<ExperienceEntry?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue((ownerId, id), out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyCollection<ExperienceEntry>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<ExperienceEntry>>(_entries.Values
            .Where(entry => entry.OwnerId == ownerId && (includeArchived || !entry.IsArchived))
            .OrderByDescending(entry => entry.CreatedAt)
            .ToArray());
    }
}
