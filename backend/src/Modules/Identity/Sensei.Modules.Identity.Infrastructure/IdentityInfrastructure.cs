using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sensei.Modules.Identity.Application;
using Sensei.Modules.Identity.Domain;

namespace Sensei.Modules.Identity.Infrastructure;

public static class IdentityInfrastructure
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services) => services
        .AddSingleton<IUserRepository, InMemoryUserRepository>()
        .AddScoped<IUserService, UserService>();
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, UserAccount> _users = new();

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<UserAccount>>(
            _users.Values.OrderBy(user => user.CreatedAt).ToArray());
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_users.Values.Any(
            user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
