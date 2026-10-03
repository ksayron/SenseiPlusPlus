using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Application;

/// <summary>Validates access to implemented source resources; it never turns a manual label into trusted evidence.</summary>
public interface IEvidenceSourceAccess
{
    Task RequireAsync(Guid ownerId, EvidenceSourceKind kind, Guid sourceId, CancellationToken cancellationToken);
}
