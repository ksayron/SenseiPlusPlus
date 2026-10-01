namespace Sensei.BuildingBlocks.Application;

public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
    Task LockAsync(string key, CancellationToken cancellationToken);
}

public sealed class ResourceNotFoundException() : Exception("Resource not found.");
