using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace Sensei.Client.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfessionalEventImportV1(Guid EventId, [property: JsonNumberHandling(JsonNumberHandling.Strict), Range(1, 1)] int SchemaVersion,
    [property: RegularExpression("^(Employment|Coursework|PersonalProject|Other)$")] string Setting,
    DateOnly EventDate, [property: StringLength(240, MinimumLength = 1)] string Title,
    [property: StringLength(4096, MinimumLength = 1)] string Context,
    [property: StringLength(4096, MinimumLength = 1)] string Role,
    [property: StringLength(4096, MinimumLength = 1)] string Actions,
    [property: StringLength(4096)] string Alternatives, [property: StringLength(4096)] string Outcome,
    [property: StringLength(4096)] string Uncertainty, [property: StringLength(4096, MinimumLength = 1)] string Assistance,
    [property: MaxLength(100)] Guid[] ConceptIds, [property: RegularExpression("^client_reported$")] string ClaimProvenance);
public sealed record ImportReceipt(Guid EventId, Guid EntryId, int RevisionNumber, Guid[] ObservationIds,
    string PayloadDigest, DateTimeOffset ReceivedAt, string ExperiencePath);
public sealed record AgentProfile(Guid OwnerId, string DisplayName);
public sealed record AgentConcept(Guid Id, string Name);

public static class ImportContract
{
    public const int MaximumBytes = 64 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        RespectRequiredConstructorParameters = true
    };
    public static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    public static string Digest(ProfessionalEventImportV1 value) => Convert.ToHexStringLower(SHA256.HashData(Bytes(value with { ConceptIds = value.ConceptIds.Order().ToArray() })));
    public static ProfessionalEventImportV1 Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new ArgumentException("import_too_large");
        var reader = new Utf8JsonReader(bytes);
        var stack = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new(StringComparer.OrdinalIgnoreCase));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !stack.Peek().Add(reader.GetString()!))
                throw new ArgumentException("duplicate_import_property");
        }
        var value = JsonSerializer.Deserialize<ProfessionalEventImportV1>(bytes, Json) ?? throw new ArgumentException("invalid_import");
        Validate(value);
        return value;
    }
    public static void Validate(ProfessionalEventImportV1 p)
    {
        if (p.EventId == Guid.Empty || p.SchemaVersion != 1 || p.ClaimProvenance != "client_reported" ||
            p.Setting is not ("Employment" or "Coursework" or "PersonalProject" or "Other") || p.EventDate == default)
            throw new ArgumentException("invalid_import_identity");
        var text = new[] { p.Context, p.Role, p.Actions, p.Alternatives, p.Outcome, p.Uncertainty, p.Assistance };
        if (p.Title is null || p.Title.Length > 240 || string.IsNullOrWhiteSpace(p.Title) ||
            text.Any(x => x is null || x.Length > 4096) || string.IsNullOrWhiteSpace(p.Context) ||
            string.IsNullOrWhiteSpace(p.Role) || string.IsNullOrWhiteSpace(p.Actions) || string.IsNullOrWhiteSpace(p.Assistance) ||
            p.ConceptIds is null || p.ConceptIds.Length > 100 || p.ConceptIds.Contains(Guid.Empty) || p.ConceptIds.Distinct().Count() != p.ConceptIds.Length)
            throw new ArgumentException("invalid_import_contribution");
        var bytes = Bytes(p);
        if (bytes.Length > MaximumBytes) throw new ArgumentException("import_too_large");
        if (UnsafeProse(string.Join("\n", text.Prepend(p.Title)))) throw new ArgumentException("import_sensitive_content");
    }
    public static bool UnsafeProse(string text) => Regex.IsMatch(text,
        @"(?im)(-----BEGIN (?:\w+ )?PRIVATE KEY-----|\b(?:gh[pousr]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{16,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9_-]{8,})\b|(?:password|api[_-]?key|secret|token|access[_-]?token|client[_-]?secret)\s*[:=]\s*\S+|^diff --git |^@@ .+ @@|[A-Z]:[\\/]|(?:https?://(?:[^\s]+\.git\b|(?:www\.)?github\.com/[^\s/]+/[^\s]+)|git@[^\s]+)|\b(?:SELECT\s+.+\s+FROM|INSERT\s+INTO|DELETE\s+FROM)\b)",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
}
