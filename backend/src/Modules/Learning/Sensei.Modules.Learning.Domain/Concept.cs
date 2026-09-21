namespace Sensei.Modules.Learning.Domain;

public enum ConceptDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

public sealed class Concept
{
    private Concept() { }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Locale { get; private set; } = string.Empty;
    public ConceptDifficulty Difficulty { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int Version { get; private set; }

    public static Concept Create(
        string key,
        string name,
        string description,
        string locale,
        ConceptDifficulty difficulty,
        DateTimeOffset now)
    {
        Validate(key, name, description, locale);
        return new Concept
        {
            Id = Guid.NewGuid(),
            Key = NormalizeKey(key),
            Name = name.Trim(),
            Description = description.Trim(),
            Locale = locale.Trim(),
            Difficulty = difficulty,
            IsActive = true,
            CreatedAt = now,
            Version = 1
        };
    }

    public void Update(
        string name,
        string description,
        string locale,
        ConceptDifficulty difficulty,
        int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Validate(Key, name, description, locale);
        Name = name.Trim();
        Description = description.Trim();
        Locale = locale.Trim();
        Difficulty = difficulty;
        Version++;
    }

    public void Deactivate(int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsActive = false;
        Version++;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected concept version {expectedVersion}, but current version is {Version}.");
        }
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant().Replace(' ', '-');

    private static void Validate(string key, string name, string description, string locale)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Concept key and name are required.");
        }

        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException("Concept description and locale are required.");
        }
    }
}
