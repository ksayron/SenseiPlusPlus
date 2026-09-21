namespace Sensei.BuildingBlocks.Application;

public sealed class ConcurrencyConflictException(string message) : Exception(message);

public sealed class DuplicateResourceException(string message) : Exception(message);
