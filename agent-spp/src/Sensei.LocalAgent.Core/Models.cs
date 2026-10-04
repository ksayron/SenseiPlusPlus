using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sensei.LocalAgent.Core;

public enum WorkSetting { Employment, Coursework, PersonalProject, Other }
public enum SourceKind { Staged, Unstaged, Commit }
public enum CandidateStatus { Pending, Dismissed }
public enum Freshness { Current, Changed, Unavailable, Incomplete, Historical }

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };
    public static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static string Digest<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Bytes(value)));
}

public sealed record RepositoryRegistration(Guid Id, string Root, string[] Selection, WorkSetting Setting,
    bool RetainSource, string? CommitCheckpoint, DateTimeOffset RegisteredAt);

public sealed record ManifestFile(string Path, string? OldPath, string Status, string OldMode, string NewMode,
    string OldIdentity, string NewIdentity, string? UnreviewedReason);

public sealed record SourceManifest(int SchemaVersion, SourceKind Kind, string BaseIdentity, string? CommitIdentity,
    string[] Selection, ManifestFile[] Files, bool IsMerge, bool Complete, string[] Limitations)
{
    public string Fingerprint => AgentJson.Digest(new
    {
        SchemaVersion, Kind, BaseIdentity, CommitIdentity,
        Selection = Selection.Order(StringComparer.Ordinal).ToArray(),
        Files = Files.OrderBy(f => f.Path, StringComparer.Ordinal).ThenBy(f => f.OldPath, StringComparer.Ordinal).ToArray(),
        IsMerge, Complete, Limitations
    });
}

public sealed record CaptureResult(SourceManifest Manifest, string? SourceText)
{
    public bool HasChanges => Manifest.Files.Length != 0 || Manifest.Limitations.Length != 0;
}

public sealed record ContributionNotes(string Role, string Actions, string Alternatives, string Outcome,
    string Uncertainty, string Assistance, Guid[] ConceptIds, string Title = "", string Context = "")
{
    public static ContributionNotes Empty => new("", "", "", "", "", "", []);
}

public sealed record EvidenceCandidate(Guid Id, Guid EventId, Guid RepositoryId, WorkSetting Setting,
    DateTimeOffset CapturedAt, SourceManifest Manifest, string Fingerprint, string? SourceText,
    ContributionNotes Notes, int Revision, CandidateStatus Status, int RecordVersion = 2);

// Deliberately distinct from the future authenticated ProfessionalEventImportV1 contract.
public sealed record SanitizedEventDraftV1(Guid EventId, int SchemaVersion, WorkSetting Setting,
    DateOnly EventDate, string Role, string Actions, string Alternatives, string Outcome,
    string Uncertainty, string Assistance, Guid[] ConceptIds, string ClaimProvenance);

public sealed record ExportPreview(SanitizedEventDraftV1 Draft, int Revision, string Confirmation);

public sealed class AgentException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public interface IContentProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

public interface IAgentStore
{
    T WithLock<T>(Func<T> action);
    RepositoryRegistration[] Repositories();
    void SaveRepositories(RepositoryRegistration[] repositories);
    EvidenceCandidate[] Candidates();
    EvidenceCandidate Candidate(Guid id);
    void SaveCandidate(EvidenceCandidate candidate);
    void DeleteCandidate(Guid id);
    bool IsDeleted(Guid id);
    IDisposable WatcherLock();
    bool WatcherRunning { get; }
}

public interface IGitContext
{
    Task<string> ResolveRoot(string path, CancellationToken ct);
    Task<string?> Head(string root, CancellationToken ct);
    Task<string[]> NewCommits(RepositoryRegistration repository, CancellationToken ct);
    Task<CaptureResult> Capture(RepositoryRegistration repository, SourceKind kind, string? commit, CancellationToken ct);
}

public static class SourcePolicy
{
    public static string[] Selection(IEnumerable<string> paths)
    {
        var result = paths.Select(p => p.Replace('\\', '/').TrimEnd('/')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (result.Length == 0 || result.Any(p => string.IsNullOrWhiteSpace(p) || p == "." ||
            p.StartsWith('/') || p.Contains(':') || p.Contains('\0') || p.Split('/').Any(s => s is "." or ".." or "") || p.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || p.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            throw new AgentException("explicit_relative_selection_required");
        return result;
    }

    public static bool Selected(string path, string[] selection) => selection.Any(p =>
        path.Equals(p, StringComparison.Ordinal) || path.StartsWith(p + "/", StringComparison.Ordinal));

    public static bool CredentialPath(string path)
    {
        var name = path.Split('/').Last().ToLowerInvariant();
        return name == ".env" || name.StartsWith(".env.", StringComparison.Ordinal) ||
            name is "id_rsa" or "id_ed25519" or "credentials" or "credentials.json" or "secrets.json" ||
            name.EndsWith(".pem", StringComparison.Ordinal) || name.EndsWith(".key", StringComparison.Ordinal) ||
            name.EndsWith(".pfx", StringComparison.Ordinal) || name.EndsWith(".p12", StringComparison.Ordinal) ||
            name.EndsWith(".keystore", StringComparison.Ordinal);
    }

    public static bool SafeFile(string root, string path)
    {
        if (path.Split('/').Any(s => s is ".." or "." or "") || Path.IsPathRooted(path)) return false;
        var current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        foreach (var part in path.Split('/'))
        {
            current = Path.Combine(current, part);
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        }
        return true;
    }

    public static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    public static readonly UTF8Encoding StrictUtf8 = new(false, true);
}
