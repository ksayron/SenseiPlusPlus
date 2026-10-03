using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sensei.Modules.WorkReflection.Domain;

public enum ContextSourceKind { ManualWeb }
public enum ContextDataScope { Restricted, PublicSynthetic }
public sealed record SelectedExcerpt(string Label, string VersionLabel, string Text);
public sealed record ContextSource(ContextSourceKind Kind, SelectedExcerpt[] Excerpts, string Intent, string Constraints);

/// <summary>Versioned UTF-8 envelope. Only transport line endings are normalized.</summary>
public static class ContextFingerprint
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static ContextSource Normalize(ContextSource source)
    {
        if (source is null || source.Kind != ContextSourceKind.ManualWeb || source.Excerpts is null ||
            source.Excerpts.Length is < 1 or > 8 || source.Excerpts.Any(x => x is null))
            throw new ArgumentException("Select one to eight manually supplied excerpts.");
        var normalized = new ContextSource(source.Kind, source.Excerpts.Select(x => new SelectedExcerpt(
            NormalizeText(x.Label, 240, true), NormalizeText(x.VersionLabel, 240, false), NormalizeText(x.Text, 16000, true))).ToArray(),
            NormalizeText(source.Intent, 4000, true), NormalizeText(source.Constraints, 8000, false));
        if (Envelope(normalized).Length > 32000) throw new ArgumentException("Selected context exceeds 32,000 UTF-8 bytes.");
        return normalized;
    }

    public static string Compute(ContextSource source) => Hash(Envelope(Normalize(source)));
    public static string NormalizeText(string value, int max, bool required)
    {
        if (value is null || value.Length > max || required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Context fields are missing or exceed their limits.");
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        try { _ = Utf8.GetByteCount(normalized); }
        catch (EncoderFallbackException) { throw new ArgumentException("Context must contain valid Unicode text."); }
        return normalized;
    }
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string HashText(string text) => Hash(Utf8.GetBytes(text));

    private static byte[] Envelope(ContextSource source)
    {
        using var output = new MemoryStream();
        void Number(int number)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, number);
            output.Write(bytes);
        }
        void Field(string value)
        {
            var bytes = Utf8.GetBytes(value);
            Number(bytes.Length); output.Write(bytes);
        }
        Field("sensei.context-source.v1"); Field(source.Kind.ToString()); Number(source.Excerpts.Length);
        foreach (var excerpt in source.Excerpts) { Field(excerpt.Label); Field(excerpt.VersionLabel); Field(excerpt.Text); }
        Field(source.Intent); Field(source.Constraints);
        return output.ToArray();
    }
}

public sealed class ContextSnapshot
{
    private ContextSnapshot() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid EpisodeId { get; private set; }
    public int Number { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestDigest { get; private set; } = "";
    public string SourceJson { get; private set; } = "";
    public string SourceFingerprint { get; private set; } = "";
    public string FingerprintVersion { get; private set; } = "sensei.context-source.v1";
    public ContextDataScope DataScope { get; private set; }
    public string DisclosureJson { get; private set; } = "";
    public string DisclosureDigest { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public ContextSource Source() => JsonSerializer.Deserialize<ContextSource>(SourceJson)!;
    public static ContextSnapshot Create(Guid owner, Guid episode, int number, Guid operation, string requestDigest,
        ContextSource source, ContextDataScope scope, string disclosureJson, DateTimeOffset now)
    {
        if (owner == Guid.Empty || episode == Guid.Empty || operation == Guid.Empty || number < 1 || !Enum.IsDefined(scope))
            throw new ArgumentException("Snapshot identity and data classification are required.");
        var normalized = ContextFingerprint.Normalize(source);
        return new ContextSnapshot
        {
            Id = Guid.NewGuid(), OwnerId = owner, EpisodeId = episode, Number = number, OperationId = operation,
            RequestDigest = requestDigest, SourceJson = JsonSerializer.Serialize(normalized),
            SourceFingerprint = ContextFingerprint.Compute(normalized), DataScope = scope,
            DisclosureJson = disclosureJson, DisclosureDigest = ContextFingerprint.HashText(disclosureJson), CreatedAt = now
        };
    }
}

public enum InterpretationOrigin { User, Fixture, Provider }
public sealed record InterpretationText(string Intent, string Constraints, string MissingInformation, string Correction);
public sealed class InterpretationRevision
{
    private InterpretationRevision() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid EpisodeId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public int Number { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestDigest { get; private set; } = "";
    public InterpretationOrigin Origin { get; private set; }
    public string TextJson { get; private set; } = "";
    public string? ProvenanceJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public InterpretationText Text() => JsonSerializer.Deserialize<InterpretationText>(TextJson)!;

    public static InterpretationRevision Create(Guid owner, Guid episode, Guid snapshot, int number, Guid? supersedes,
        Guid operation, string digest, InterpretationText text, InterpretationOrigin origin, string? provenance, DateTimeOffset now)
    {
        if (owner == Guid.Empty || episode == Guid.Empty || snapshot == Guid.Empty || operation == Guid.Empty ||
            number < 1 || text is null || !Enum.IsDefined(origin) || origin != InterpretationOrigin.User && string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Interpretation identity, origin and provenance are required.");
        var normalized = ContextFingerprint.Normalize(new(ContextSourceKind.ManualWeb,
            [new("interpretation", "", "validation")], text.Intent, text.Constraints));
        var missing = ContextFingerprint.NormalizeText(text.MissingInformation, 8000, false);
        var correction = ContextFingerprint.NormalizeText(text.Correction, 4000, false);
        return new InterpretationRevision
        {
            Id = Guid.NewGuid(), OwnerId = owner, EpisodeId = episode, SnapshotId = snapshot, Number = number,
            SupersedesId = supersedes, OperationId = operation, RequestDigest = digest, Origin = origin,
            TextJson = JsonSerializer.Serialize(new InterpretationText(normalized.Intent, normalized.Constraints,
                missing, correction)),
            ProvenanceJson = provenance, CreatedAt = now
        };
    }
}

/// <summary>Append-only owner action confirming a specific interpretation of a specific source.</summary>
public sealed class ContextConfirmation
{
    private ContextConfirmation() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid EpisodeId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid InterpretationId { get; private set; }
    public string SourceFingerprint { get; private set; } = "";
    public Guid OperationId { get; private set; }
    public string RequestDigest { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public static ContextConfirmation Create(ContextSnapshot source, InterpretationRevision revision, Guid operation, string digest, DateTimeOffset now)
    {
        if (operation == Guid.Empty || source.OwnerId != revision.OwnerId || source.EpisodeId != revision.EpisodeId || source.Id != revision.SnapshotId)
            throw new ArgumentException("Confirmation must bind the owner's exact snapshot and interpretation.");
        return new ContextConfirmation { Id = Guid.NewGuid(), OwnerId = source.OwnerId, EpisodeId = source.EpisodeId,
            SnapshotId = source.Id, InterpretationId = revision.Id, SourceFingerprint = source.SourceFingerprint,
            OperationId = operation, RequestDigest = digest, CreatedAt = now };
    }
}
