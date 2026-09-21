namespace Sensei.Modules.Experience.Domain;

public enum ExperienceSetting
{
    Employment,
    Coursework,
    PersonalProject,
    Other
}

public enum ImpactState
{
    Unknown,
    Qualitative,
    Measured
}

public enum RevisionApprovalState
{
    Draft,
    Approved,
    Retracted
}

public sealed class ExperienceEntry
{
    private readonly List<ExperienceRevision> _revisions = [];

    private ExperienceEntry() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<ExperienceRevision> Revisions => _revisions.AsReadOnly();
    public ExperienceRevision CurrentRevision => _revisions[^1];

    public static ExperienceEntry Create(
        Guid ownerId,
        ExperienceRevisionContent content,
        DateTimeOffset now)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Owner ID is required.");
        }

        var entry = new ExperienceEntry
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CreatedAt = now,
            Version = 1
        };
        entry._revisions.Add(ExperienceRevision.Create(entry.Id, 1, content, now));
        return entry;
    }

    public void Revise(ExperienceRevisionContent content, int expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        _revisions.Add(ExperienceRevision.Create(Id, CurrentRevision.Number + 1, content, now));
        Version++;
    }

    public void Approve(int revisionNumber, int expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        if (CurrentRevision.Number != revisionNumber)
        {
            throw new InvalidOperationException("Only the current exact revision can be approved.");
        }

        CurrentRevision.Approve(now);
        Version++;
    }

    public void Archive(int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsArchived = true;
        Version++;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected entry version {expectedVersion}, but current version is {Version}.");
        }
    }
}

public sealed record ExperienceRevisionContent(
    string Title,
    ExperienceSetting Setting,
    string Context,
    string Role,
    string Actions,
    string Alternatives,
    string Outcome,
    ImpactState ImpactState,
    IReadOnlyCollection<Guid> ConceptIds)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Context))
        {
            throw new ArgumentException("Experience title and context are required.");
        }

        if (string.IsNullOrWhiteSpace(Role) || string.IsNullOrWhiteSpace(Actions))
        {
            throw new ArgumentException("Personal role and actions are required.");
        }

        if (ConceptIds is null || ConceptIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Concept IDs are required and cannot contain empty values.");
        }
    }
}

public sealed class ExperienceRevision
{
    private readonly List<ExperienceRevisionConcept> _concepts = [];

    private ExperienceRevision() { }

    public Guid EntryId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ExperienceSetting Setting { get; private set; }
    public string Context { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string Actions { get; private set; } = string.Empty;
    public string Alternatives { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public ImpactState ImpactState { get; private set; }
    public IReadOnlyCollection<Guid> ConceptIds => _concepts.Select(concept => concept.ConceptId).ToArray();
    public IReadOnlyCollection<ExperienceRevisionConcept> Concepts => _concepts.AsReadOnly();
    public RevisionApprovalState ApprovalState { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    internal static ExperienceRevision Create(
        Guid entryId,
        int number,
        ExperienceRevisionContent content,
        DateTimeOffset now)
    {
        content.Validate();
        var revision = new ExperienceRevision
        {
            EntryId = entryId,
            Number = number,
            Title = content.Title.Trim(),
            Setting = content.Setting,
            Context = content.Context.Trim(),
            Role = content.Role.Trim(),
            Actions = content.Actions.Trim(),
            Alternatives = (content.Alternatives ?? string.Empty).Trim(),
            Outcome = (content.Outcome ?? string.Empty).Trim(),
            ImpactState = content.ImpactState,
            ApprovalState = RevisionApprovalState.Draft,
            CreatedAt = now
        };

        revision._concepts.AddRange(content.ConceptIds
            .Distinct()
            .Select(conceptId => ExperienceRevisionConcept.Create(entryId, number, conceptId)));
        return revision;
    }

    internal void Approve(DateTimeOffset now)
    {
        if (ApprovalState != RevisionApprovalState.Draft)
        {
            throw new InvalidOperationException("Only a draft revision can be approved.");
        }

        ApprovalState = RevisionApprovalState.Approved;
        ApprovedAt = now;
    }
}

public sealed class ExperienceRevisionConcept
{
    private ExperienceRevisionConcept() { }

    public Guid EntryId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid ConceptId { get; private set; }

    internal static ExperienceRevisionConcept Create(Guid entryId, int revisionNumber, Guid conceptId) => new()
    {
        EntryId = entryId,
        RevisionNumber = revisionNumber,
        ConceptId = conceptId
    };
}
