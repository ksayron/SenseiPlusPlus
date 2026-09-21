using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Application;

public sealed record CreateEvidenceObservationCommand(
    Guid ConceptId,
    string Aspect,
    EvidenceSignalKind SignalKind,
    EvidenceSourceKind SourceKind,
    Guid SourceId,
    AssistanceLevel Assistance,
    string Conditions);

public sealed record UpdateEvidenceStatusCommand(EvidenceStatus Status, int ExpectedVersion);

public sealed record EvidenceObservationResponse(
    Guid Id,
    Guid OwnerId,
    Guid ConceptId,
    string Aspect,
    EvidenceSignalKind SignalKind,
    EvidenceSourceKind SourceKind,
    Guid SourceId,
    AssistanceLevel Assistance,
    string Conditions,
    EvidenceStatus Status,
    DateTimeOffset ObservedAt,
    int Version);

public interface IEvidenceObservationRepository
{
    Task AddAsync(EvidenceObservation observation, CancellationToken cancellationToken);
    Task<EvidenceObservation?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EvidenceObservation>> ListAsync(
        Guid ownerId,
        Guid? conceptId,
        bool includeInactive,
        CancellationToken cancellationToken);
}

public interface IEvidenceObservationService
{
    Task<EvidenceObservationResponse> CreateAsync(
        Guid ownerId,
        CreateEvidenceObservationCommand command,
        CancellationToken cancellationToken);
    Task<EvidenceObservationResponse?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EvidenceObservationResponse>> ListAsync(
        Guid ownerId,
        Guid? conceptId,
        bool includeInactive,
        CancellationToken cancellationToken);
    Task<EvidenceObservationResponse?> ChangeStatusAsync(
        Guid ownerId,
        Guid id,
        UpdateEvidenceStatusCommand command,
        CancellationToken cancellationToken);
    Task<bool> WithdrawAsync(Guid ownerId, Guid id, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class EvidenceObservationService(IEvidenceObservationRepository repository)
    : IEvidenceObservationService
{
    public async Task<EvidenceObservationResponse> CreateAsync(
        Guid ownerId,
        CreateEvidenceObservationCommand command,
        CancellationToken cancellationToken)
    {
        var observation = EvidenceObservation.Create(
            ownerId,
            command.ConceptId,
            command.Aspect,
            command.SignalKind,
            command.SourceKind,
            command.SourceId,
            command.Assistance,
            command.Conditions,
            DateTimeOffset.UtcNow);
        await repository.AddAsync(observation, cancellationToken);
        return Map(observation);
    }

    public async Task<EvidenceObservationResponse?> GetAsync(
        Guid ownerId,
        Guid id,
        CancellationToken cancellationToken) =>
        MapOrNull(await repository.GetAsync(ownerId, id, cancellationToken));

    public async Task<IReadOnlyCollection<EvidenceObservationResponse>> ListAsync(
        Guid ownerId,
        Guid? conceptId,
        bool includeInactive,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(ownerId, conceptId, includeInactive, cancellationToken)).Select(Map).ToArray();

    public async Task<EvidenceObservationResponse?> ChangeStatusAsync(
        Guid ownerId,
        Guid id,
        UpdateEvidenceStatusCommand command,
        CancellationToken cancellationToken)
    {
        var observation = await repository.GetAsync(ownerId, id, cancellationToken);
        if (observation is null)
        {
            return null;
        }

        ExecuteVersioned(() => observation.ChangeStatus(command.Status, command.ExpectedVersion));
        return Map(observation);
    }

    public async Task<bool> WithdrawAsync(
        Guid ownerId,
        Guid id,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var observation = await repository.GetAsync(ownerId, id, cancellationToken);
        if (observation is null)
        {
            return false;
        }

        ExecuteVersioned(() => observation.ChangeStatus(EvidenceStatus.Withdrawn, expectedVersion));
        return true;
    }

    private static void ExecuteVersioned(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            throw new ConcurrencyConflictException(exception.Message);
        }
    }

    private static EvidenceObservationResponse? MapOrNull(EvidenceObservation? observation) =>
        observation is null ? null : Map(observation);

    private static EvidenceObservationResponse Map(EvidenceObservation observation) => new(
        observation.Id,
        observation.OwnerId,
        observation.ConceptId,
        observation.Aspect,
        observation.SignalKind,
        observation.SourceKind,
        observation.SourceId,
        observation.Assistance,
        observation.Conditions,
        observation.Status,
        observation.ObservedAt,
        observation.Version);
}
