using Sensei.BuildingBlocks.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Application;

public sealed record CreateWorkEpisodeCommand(
    string Title,
    WorkSetting Setting,
    DateOnly EventDate,
    string Role,
    string Summary);

public sealed record UpdateWorkEpisodeCommand(
    string Title,
    WorkSetting Setting,
    DateOnly EventDate,
    string Role,
    string Summary,
    int ExpectedVersion);

public sealed record WorkEpisodeResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    WorkSetting Setting,
    DateOnly EventDate,
    string Role,
    string Summary,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public interface IWorkEpisodeRepository
{
    Task AddAsync(WorkEpisode episode, CancellationToken cancellationToken);
    Task<WorkEpisode?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkEpisode>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken);
}

public interface IWorkEpisodeService
{
    Task<WorkEpisodeResponse> CreateAsync(
        Guid ownerId,
        CreateWorkEpisodeCommand command,
        CancellationToken cancellationToken);
    Task<WorkEpisodeResponse?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkEpisodeResponse>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken);
    Task<WorkEpisodeResponse?> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateWorkEpisodeCommand command,
        CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(Guid ownerId, Guid id, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class WorkEpisodeService(IWorkEpisodeRepository repository) : IWorkEpisodeService
{
    public async Task<WorkEpisodeResponse> CreateAsync(
        Guid ownerId,
        CreateWorkEpisodeCommand command,
        CancellationToken cancellationToken)
    {
        var episode = WorkEpisode.Create(
            ownerId,
            command.Title,
            command.Setting,
            command.EventDate,
            command.Role,
            command.Summary,
            DateTimeOffset.UtcNow);
        await repository.AddAsync(episode, cancellationToken);
        return Map(episode);
    }

    public async Task<WorkEpisodeResponse?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        MapOrNull(await repository.GetAsync(ownerId, id, cancellationToken));

    public async Task<IReadOnlyCollection<WorkEpisodeResponse>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(ownerId, includeArchived, cancellationToken)).Select(Map).ToArray();

    public async Task<WorkEpisodeResponse?> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateWorkEpisodeCommand command,
        CancellationToken cancellationToken)
    {
        var episode = await repository.GetAsync(ownerId, id, cancellationToken);
        if (episode is null)
        {
            return null;
        }

        ExecuteVersioned(() => episode.Update(
            command.Title,
            command.Setting,
            command.EventDate,
            command.Role,
            command.Summary,
            command.ExpectedVersion,
            DateTimeOffset.UtcNow));
        return Map(episode);
    }

    public async Task<bool> ArchiveAsync(
        Guid ownerId,
        Guid id,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var episode = await repository.GetAsync(ownerId, id, cancellationToken);
        if (episode is null)
        {
            return false;
        }

        ExecuteVersioned(() => episode.Archive(expectedVersion, DateTimeOffset.UtcNow));
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

    private static WorkEpisodeResponse? MapOrNull(WorkEpisode? episode) => episode is null ? null : Map(episode);

    private static WorkEpisodeResponse Map(WorkEpisode episode) => new(
        episode.Id,
        episode.OwnerId,
        episode.Title,
        episode.Setting,
        episode.EventDate,
        episode.Role,
        episode.Summary,
        episode.IsArchived,
        episode.CreatedAt,
        episode.UpdatedAt,
        episode.Version);
}
