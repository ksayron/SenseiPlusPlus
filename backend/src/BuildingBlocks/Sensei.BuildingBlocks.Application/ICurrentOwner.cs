namespace Sensei.BuildingBlocks.Application;

/// <summary>The authenticated owner for the current operation. Never populated from request bodies.</summary>
public interface ICurrentOwner
{
    Guid Id { get; }
}
