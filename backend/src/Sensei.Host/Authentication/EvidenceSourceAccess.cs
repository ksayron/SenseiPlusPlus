using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Host.Authentication;

public sealed class EvidenceSourceAccess(SenseiDbContext db) : IEvidenceSourceAccess
{
    public async Task RequireAsync(Guid ownerId, EvidenceSourceKind kind, Guid sourceId, CancellationToken ct)
    {
        if (kind == EvidenceSourceKind.ApprovedExperienceRevision)
        {
            if (!await db.Set<ExperienceEntry>().AnyAsync(x => x.Id == sourceId && x.OwnerId == ownerId && x.Revisions.Any(r => r.ApprovalState == RevisionApprovalState.Approved), ct))
                throw new ResourceNotFoundException();
        }
        else if (kind == EvidenceSourceKind.Attempt)
        {
            // Preserve legacy, untrusted manual source labels. An actual attempt from another owner is never linkable.
            if (await db.Set<LearningAttempt>().AnyAsync(x => x.Id == sourceId && x.OwnerId != ownerId, ct))
                throw new ResourceNotFoundException();
        }
        else if (kind == EvidenceSourceKind.FeedbackRevision)
            throw new ResourceNotFoundException(); // AI feedback sources are not implemented.
    }
}
