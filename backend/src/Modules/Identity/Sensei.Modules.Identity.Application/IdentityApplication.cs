using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Identity.Domain;

namespace Sensei.Modules.Identity.Application;

public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    string UiLocale,
    string AnswerLanguage,
    string TimeZone);

public sealed record UpdateUserCommand(
    string DisplayName,
    string UiLocale,
    string AnswerLanguage,
    string TimeZone,
    int ExpectedVersion);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string UiLocale,
    string AnswerLanguage,
    string TimeZone,
    DateTimeOffset CreatedAt,
    int Version);

public interface IUserRepository
{
    Task AddAsync(UserAccount user, CancellationToken cancellationToken);
    Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IUserService
{
    Task<UserResponse> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken);
    Task<UserResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<UserResponse?> UpdateAsync(Guid id, UpdateUserCommand command, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class UserService(IUserRepository repository, IUnitOfWork unitOfWork) : IUserService
{
    public async Task<UserResponse> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        if (await repository.EmailExistsAsync(command.Email.Trim(), cancellationToken))
        {
            throw new DuplicateResourceException("A user with this email already exists.");
        }

        var user = UserAccount.Create(
            command.Email,
            command.DisplayName,
            command.UiLocale,
            command.AnswerLanguage,
            command.TimeZone,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MapOrNull(await repository.GetAsync(id, cancellationToken));

    public async Task<IReadOnlyCollection<UserResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        VersionPrecondition.RequireCurrent(user.Version, command.ExpectedVersion);
        user.Update(
            command.DisplayName,
            command.UiLocale,
            command.AnswerLanguage,
            command.TimeZone,
            command.ExpectedVersion);

        await unitOfWork.CommitAsync(cancellationToken);
        return Map(user);
    }

    public async Task<bool> DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken)
    {
        var user = await repository.GetAsync(id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        VersionPrecondition.RequireCurrent(user.Version, expectedVersion);

        await repository.DeleteAsync(id, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return true;
    }

    private static UserResponse? MapOrNull(UserAccount? user) => user is null ? null : Map(user);

    private static UserResponse Map(UserAccount user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.UiLocale,
        user.AnswerLanguage,
        user.TimeZone,
        user.CreatedAt,
        user.Version);
}
