namespace Sensei.Modules.WorkReflection.Domain;

public enum WorkSetting
{
    Employment,
    Coursework,
    PersonalProject,
    Other
}

public sealed class WorkEpisode
{
    private WorkEpisode() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public WorkSetting Setting { get; private set; }
    public DateOnly EventDate { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public static WorkEpisode Create(
        Guid ownerId,
        string title,
        WorkSetting setting,
        DateOnly eventDate,
        string role,
        string summary,
        DateTimeOffset now)
    {
        Validate(ownerId, title, role, summary);
        return new WorkEpisode
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = title.Trim(),
            Setting = setting,
            EventDate = eventDate,
            Role = role.Trim(),
            Summary = summary.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public void Update(
        string title,
        WorkSetting setting,
        DateOnly eventDate,
        string role,
        string summary,
        int expectedVersion,
        DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        Validate(OwnerId, title, role, summary);
        Title = title.Trim();
        Setting = setting;
        EventDate = eventDate;
        Role = role.Trim();
        Summary = summary.Trim();
        UpdatedAt = now;
        Version++;
    }

    public void Archive(int expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        IsArchived = true;
        UpdatedAt = now;
        Version++;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected episode version {expectedVersion}, but current version is {Version}.");
        }
    }

    private static void Validate(Guid ownerId, string title, string role, string summary)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Owner ID is required.");
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Episode title and role are required.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Episode summary is required.");
        }
    }
}
