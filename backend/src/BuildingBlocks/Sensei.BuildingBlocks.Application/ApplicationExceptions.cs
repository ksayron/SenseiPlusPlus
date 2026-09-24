namespace Sensei.BuildingBlocks.Application;

public sealed class ConcurrencyConflictException(string message) : Exception(message);

public sealed class DuplicateResourceException(string message) : Exception(message);

public sealed class ResourceInUseException(string message) : Exception(message);

public static class VersionPrecondition
{
    public static void RequireCurrent(int actualVersion, int expectedVersion)
    {
        if (actualVersion != expectedVersion)
        {
            throw new ConcurrencyConflictException("The resource has changed since the supplied ETag was issued.");
        }
    }
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}
