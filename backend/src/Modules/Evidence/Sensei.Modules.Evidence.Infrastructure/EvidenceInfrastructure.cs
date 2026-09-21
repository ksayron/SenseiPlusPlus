using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Infrastructure;

public static class EvidenceInfrastructure
{
    public static IServiceCollection AddEvidenceModule(this IServiceCollection services) => services
        .AddSingleton<IEvidenceObservationRepository, InMemoryEvidenceObservationRepository>()
        .AddScoped<IEvidenceObservationService, EvidenceObservationService>();
}

internal sealed class InMemoryEvidenceObservationRepository : IEvidenceObservationRepository
{
    private readonly ConcurrentDictionary<(Guid OwnerId, Guid Id), EvidenceObservation> _observations = new();

    public Task AddAsync(EvidenceObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _observations[(observation.OwnerId, observation.Id)] = observation;
        return Task.CompletedTask;
    }

    public Task<EvidenceObservation?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _observations.TryGetValue((ownerId, id), out var observation);
        return Task.FromResult(observation);
    }

    public Task<IReadOnlyCollection<EvidenceObservation>> ListAsync(
        Guid ownerId,
        Guid? conceptId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<EvidenceObservation>>(_observations.Values
            .Where(observation => observation.OwnerId == ownerId)
            .Where(observation => conceptId is null || observation.ConceptId == conceptId)
            .Where(observation => includeInactive || observation.Status == EvidenceStatus.Active)
            .OrderByDescending(observation => observation.ObservedAt)
            .ToArray());
    }
}
