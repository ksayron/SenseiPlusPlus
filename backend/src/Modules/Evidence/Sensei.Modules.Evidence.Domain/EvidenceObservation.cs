namespace Sensei.Modules.Evidence.Domain;

public enum EvidenceSignalKind
{
    SelfDeclaration,
    ProfessionalExposure,
    Explanation,
    Scenario,
    Recall,
    ClientReportedAssessment
}

public enum EvidenceSourceKind
{
    Declaration,
    Attempt,
    FeedbackRevision,
    ApprovedExperienceRevision
}

public enum AssistanceLevel
{
    None,
    HintUsed,
    ReferenceReviewed,
    SelfReviewed
}

public enum EvidenceStatus
{
    Active,
    Disputed,
    Superseded,
    Withdrawn
}

public sealed class EvidenceObservation
{
    private EvidenceObservation() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ConceptId { get; private set; }
    public string Aspect { get; private set; } = string.Empty;
    public EvidenceSignalKind SignalKind { get; private set; }
    public EvidenceSourceKind SourceKind { get; private set; }
    public Guid SourceId { get; private set; }
    public AssistanceLevel Assistance { get; private set; }
    public string Conditions { get; private set; } = string.Empty;
    public EvidenceStatus Status { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
    public int Version { get; private set; }

    public static EvidenceObservation Create(
        Guid ownerId,
        Guid conceptId,
        string aspect,
        EvidenceSignalKind signalKind,
        EvidenceSourceKind sourceKind,
        Guid sourceId,
        AssistanceLevel assistance,
        string conditions,
        DateTimeOffset now)
    {
        if (ownerId == Guid.Empty || conceptId == Guid.Empty || sourceId == Guid.Empty)
        {
            throw new ArgumentException("Owner, concept, and source IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(aspect) || string.IsNullOrWhiteSpace(conditions))
        {
            throw new ArgumentException("Evidence aspect and conditions are required.");
        }

        return new EvidenceObservation
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ConceptId = conceptId,
            Aspect = aspect.Trim(),
            SignalKind = signalKind,
            SourceKind = sourceKind,
            SourceId = sourceId,
            Assistance = assistance,
            Conditions = conditions.Trim(),
            Status = EvidenceStatus.Active,
            ObservedAt = now,
            Version = 1
        };
    }

    public void ChangeStatus(EvidenceStatus status, int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected evidence version {expectedVersion}, but current version is {Version}.");
        }

        if (Status is EvidenceStatus.Superseded or EvidenceStatus.Withdrawn)
        {
            throw new InvalidOperationException("Terminal evidence cannot be changed.");
        }

        if (status == Status)
        {
            return;
        }

        Status = status;
        Version++;
    }
}
