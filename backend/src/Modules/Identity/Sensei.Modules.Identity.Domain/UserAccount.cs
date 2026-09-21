namespace Sensei.Modules.Identity.Domain;

public sealed class UserAccount
{
    private UserAccount() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string UiLocale { get; private set; } = string.Empty;
    public string AnswerLanguage { get; private set; } = string.Empty;
    public string TimeZone { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public int Version { get; private set; }

    public static UserAccount Create(
        string email,
        string displayName,
        string uiLocale,
        string answerLanguage,
        string timeZone,
        DateTimeOffset now)
    {
        Validate(email, displayName, uiLocale, answerLanguage, timeZone);

        return new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            UiLocale = uiLocale.Trim(),
            AnswerLanguage = answerLanguage.Trim(),
            TimeZone = timeZone.Trim(),
            CreatedAt = now,
            Version = 1
        };
    }

    public void Update(
        string displayName,
        string uiLocale,
        string answerLanguage,
        string timeZone,
        int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Validate(Email, displayName, uiLocale, answerLanguage, timeZone);

        DisplayName = displayName.Trim();
        UiLocale = uiLocale.Trim();
        AnswerLanguage = answerLanguage.Trim();
        TimeZone = timeZone.Trim();
        Version++;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected user version {expectedVersion}, but current version is {Version}.");
        }
    }

    private static void Validate(
        string email,
        string displayName,
        string uiLocale,
        string answerLanguage,
        string timeZone)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(uiLocale) || string.IsNullOrWhiteSpace(answerLanguage))
        {
            throw new ArgumentException("UI locale and answer language are required.");
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("Time zone is required.");
        }
    }
}
