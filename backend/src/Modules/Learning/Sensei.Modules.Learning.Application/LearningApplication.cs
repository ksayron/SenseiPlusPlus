using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Application;

public sealed record CreateConceptCommand(
    string Key,
    string Name,
    string Description,
    string Locale,
    ConceptDifficulty Difficulty);

public sealed record UpdateConceptCommand(
    string Name,
    string Description,
    string Locale,
    ConceptDifficulty Difficulty,
    int ExpectedVersion);

public sealed record ConceptResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Locale,
    ConceptDifficulty Difficulty,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int Version);

public interface IConceptRepository
{
    Task AddAsync(Concept concept, CancellationToken cancellationToken);
    Task<Concept?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Concept>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);
}

public interface IConceptService
{
    Task<ConceptResponse> CreateAsync(CreateConceptCommand command, CancellationToken cancellationToken);
    Task<ConceptResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConceptResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<ConceptResponse?> UpdateAsync(Guid id, UpdateConceptCommand command, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class ConceptService(IConceptRepository repository, IUnitOfWork unitOfWork) : IConceptService
{
    public async Task<ConceptResponse> CreateAsync(CreateConceptCommand command, CancellationToken cancellationToken)
    {
        if (await repository.KeyExistsAsync(command.Key, cancellationToken))
        {
            throw new DuplicateResourceException("A concept with this key already exists.");
        }

        var concept = Concept.Create(
            command.Key,
            command.Name,
            command.Description,
            command.Locale,
            command.Difficulty,
            DateTimeOffset.UtcNow);
        await repository.AddAsync(concept, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(concept);
    }

    public async Task<ConceptResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MapOrNull(await repository.GetAsync(id, cancellationToken));

    public async Task<IReadOnlyCollection<ConceptResponse>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(includeInactive, cancellationToken)).Select(Map).ToArray();

    public async Task<ConceptResponse?> UpdateAsync(
        Guid id,
        UpdateConceptCommand command,
        CancellationToken cancellationToken)
    {
        var concept = await repository.GetAsync(id, cancellationToken);
        if (concept is null)
        {
            return null;
        }

        ExecuteVersioned(() => concept.Update(
            command.Name,
            command.Description,
            command.Locale,
            command.Difficulty,
            command.ExpectedVersion));
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(concept);
    }

    public async Task<bool> DeactivateAsync(Guid id, int expectedVersion, CancellationToken cancellationToken)
    {
        var concept = await repository.GetAsync(id, cancellationToken);
        if (concept is null)
        {
            return false;
        }

        ExecuteVersioned(() => concept.Deactivate(expectedVersion));
        await unitOfWork.CommitAsync(cancellationToken);
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

    private static ConceptResponse? MapOrNull(Concept? concept) => concept is null ? null : Map(concept);

    private static ConceptResponse Map(Concept concept) => new(
        concept.Id,
        concept.Key,
        concept.Name,
        concept.Description,
        concept.Locale,
        concept.Difficulty,
        concept.IsActive,
        concept.CreatedAt,
        concept.Version);
}
