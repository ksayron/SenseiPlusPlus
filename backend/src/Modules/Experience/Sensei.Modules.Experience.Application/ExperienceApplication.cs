using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Experience.Domain;

namespace Sensei.Modules.Experience.Application;

public sealed record CreateExperienceEntryCommand(
    string Title,
    ExperienceSetting Setting,
    string Context,
    string Role,
    string Actions,
    string Alternatives,
    string Outcome,
    ImpactState ImpactState,
    IReadOnlyCollection<Guid> ConceptIds);

public sealed record ReviseExperienceEntryCommand(
    string Title,
    ExperienceSetting Setting,
    string Context,
    string Role,
    string Actions,
    string Alternatives,
    string Outcome,
    ImpactState ImpactState,
    IReadOnlyCollection<Guid> ConceptIds,
    int ExpectedVersion);

public sealed record ApproveExperienceRevisionCommand(int ExpectedVersion);

public sealed record ExperienceRevisionResponse(
    int Number,
    string Title,
    ExperienceSetting Setting,
    string Context,
    string Role,
    string Actions,
    string Alternatives,
    string Outcome,
    ImpactState ImpactState,
    IReadOnlyCollection<Guid> ConceptIds,
    RevisionApprovalState ApprovalState,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);

public sealed record ExperienceEntryResponse(
    Guid Id,
    Guid OwnerId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int Version,
    IReadOnlyCollection<ExperienceRevisionResponse> Revisions);

public interface IExperienceEntryRepository
{
    Task AddAsync(ExperienceEntry entry, CancellationToken cancellationToken);
    Task<ExperienceEntry?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExperienceEntry>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken);
}

public interface IExperienceEntryService
{
    Task<ExperienceEntryResponse> CreateAsync(
        Guid ownerId,
        CreateExperienceEntryCommand command,
        CancellationToken cancellationToken);
    Task<ExperienceEntryResponse?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExperienceEntryResponse>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken);
    Task<ExperienceEntryResponse?> ReviseAsync(
        Guid ownerId,
        Guid id,
        ReviseExperienceEntryCommand command,
        CancellationToken cancellationToken);
    Task<ExperienceEntryResponse?> ApproveAsync(
        Guid ownerId,
        Guid id,
        int revisionNumber,
        ApproveExperienceRevisionCommand command,
        CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(Guid ownerId, Guid id, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class ExperienceEntryService(IExperienceEntryRepository repository, IUnitOfWork unitOfWork) : IExperienceEntryService
{
    public async Task<ExperienceEntryResponse> CreateAsync(
        Guid ownerId,
        CreateExperienceEntryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = ExperienceEntry.Create(ownerId, ToContent(command), DateTimeOffset.UtcNow);
        await repository.AddAsync(entry, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<ExperienceEntryResponse?> GetAsync(
        Guid ownerId,
        Guid id,
        CancellationToken cancellationToken) =>
        MapOrNull(await repository.GetAsync(ownerId, id, cancellationToken));

    public async Task<IReadOnlyCollection<ExperienceEntryResponse>> ListAsync(
        Guid ownerId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(ownerId, includeArchived, cancellationToken)).Select(Map).ToArray();

    public async Task<ExperienceEntryResponse?> ReviseAsync(
        Guid ownerId,
        Guid id,
        ReviseExperienceEntryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = await repository.GetAsync(ownerId, id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        VersionPrecondition.RequireCurrent(entry.Version, command.ExpectedVersion);
        entry.Revise(ToContent(command), command.ExpectedVersion, DateTimeOffset.UtcNow);
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<ExperienceEntryResponse?> ApproveAsync(
        Guid ownerId,
        Guid id,
        int revisionNumber,
        ApproveExperienceRevisionCommand command,
        CancellationToken cancellationToken)
    {
        var entry = await repository.GetAsync(ownerId, id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        VersionPrecondition.RequireCurrent(entry.Version, command.ExpectedVersion);
        entry.Approve(revisionNumber, command.ExpectedVersion, DateTimeOffset.UtcNow);
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<bool> ArchiveAsync(
        Guid ownerId,
        Guid id,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var entry = await repository.GetAsync(ownerId, id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        VersionPrecondition.RequireCurrent(entry.Version, expectedVersion);
        entry.Archive(expectedVersion);
        await unitOfWork.CommitAsync(cancellationToken);
        return true;
    }

    private static ExperienceRevisionContent ToContent(CreateExperienceEntryCommand command) => new(
        command.Title,
        command.Setting,
        command.Context,
        command.Role,
        command.Actions,
        command.Alternatives,
        command.Outcome,
        command.ImpactState,
        command.ConceptIds);

    private static ExperienceRevisionContent ToContent(ReviseExperienceEntryCommand command) => new(
        command.Title,
        command.Setting,
        command.Context,
        command.Role,
        command.Actions,
        command.Alternatives,
        command.Outcome,
        command.ImpactState,
        command.ConceptIds);

    private static ExperienceEntryResponse? MapOrNull(ExperienceEntry? entry) => entry is null ? null : Map(entry);

    private static ExperienceEntryResponse Map(ExperienceEntry entry) => new(
        entry.Id,
        entry.OwnerId,
        entry.IsArchived,
        entry.CreatedAt,
        entry.Version,
        entry.Revisions.Select(revision => new ExperienceRevisionResponse(
            revision.Number,
            revision.Title,
            revision.Setting,
            revision.Context,
            revision.Role,
            revision.Actions,
            revision.Alternatives,
            revision.Outcome,
            revision.ImpactState,
            revision.ConceptIds,
            revision.ApprovalState,
            revision.CreatedAt,
            revision.ApprovedAt)).ToArray());
}
