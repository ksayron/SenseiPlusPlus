using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Infrastructure;

public static class LearningInfrastructure
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services) => services
        .AddSingleton<IConceptRepository, InMemoryConceptRepository>()
        .AddScoped<IConceptService, ConceptService>();
}

internal sealed class InMemoryConceptRepository : IConceptRepository
{
    private readonly ConcurrentDictionary<Guid, Concept> _concepts = new();

    public Task AddAsync(Concept concept, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _concepts[concept.Id] = concept;
        return Task.CompletedTask;
    }

    public Task<Concept?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _concepts.TryGetValue(id, out var concept);
        return Task.FromResult(concept);
    }

    public Task<IReadOnlyCollection<Concept>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<Concept>>(_concepts.Values
            .Where(concept => includeInactive || concept.IsActive)
            .OrderBy(concept => concept.Key)
            .ToArray());
    }

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedKey = key.Trim().ToLowerInvariant().Replace(' ', '-');
        return Task.FromResult(_concepts.Values.Any(
            concept => string.Equals(concept.Key, normalizedKey, StringComparison.Ordinal)));
    }
}
