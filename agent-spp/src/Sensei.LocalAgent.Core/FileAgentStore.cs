using System.Security.Cryptography;
using System.Text.Json;

namespace Sensei.LocalAgent.Core;

public sealed class FileAgentStore : IAgentStore
{
    private readonly string root;
    private readonly IContentProtector protector;
    public FileAgentStore(string root, IContentProtector protector)
    {
        this.root = Path.GetFullPath(root);
        this.protector = protector;
        Directory.CreateDirectory(this.root);
        Directory.CreateDirectory(Path.Combine(this.root, "candidates"));
        if (!SourcePolicy.SafeFile(this.root, "candidates")) throw new AgentException("storage_reparse_point");
    }

    public T WithLock<T>(Func<T> action)
    {
        using var held = Acquire("state.lock", "state_busy");
        foreach (var directory in new[] { root, Full("candidates") })
            foreach (var path in Directory.EnumerateFiles(directory, "*.tmp"))
            {
                var parts = Path.GetFileName(path).Split('.');
                if (parts.Length == 4 && parts[1] == "protected" && Guid.TryParseExact(parts[2], "N", out _) &&
                    (parts[0] is "repositories" or "deleted" or "connection" or "outbox" || Guid.TryParseExact(parts[0], "N", out _)))
                    File.Delete(Full(Path.GetRelativePath(root, path)));
            }
        return action();
    }
    public RepositoryRegistration[] Repositories() => Read<RepositoryRegistration[]>("repositories.protected") ?? [];
    public void SaveRepositories(RepositoryRegistration[] repositories) => Write("repositories.protected", repositories);
    public EvidenceCandidate[] Candidates()
    {
        var deleted = Read<Guid[]>("deleted.protected") ?? [];
        foreach (var id in deleted) File.Delete(Full(CandidatePath(id)));
        return Directory.EnumerateFiles(Path.Combine(root, "candidates"), "*.protected")
            .Select(p => Read<EvidenceCandidate>(Path.GetRelativePath(root, p)) ?? throw new AgentException("state_unreadable")).ToArray();
    }
    public bool IsDeleted(Guid id) => (Read<Guid[]>("deleted.protected") ?? []).Contains(id);
    public EvidenceCandidate Candidate(Guid id) => !IsDeleted(id) ? Read<EvidenceCandidate>(CandidatePath(id)) ?? throw new AgentException("candidate_not_found") : throw new AgentException("candidate_not_found");
    public void SaveCandidate(EvidenceCandidate candidate)
    {
        if (!IsDeleted(candidate.Id)) Write(CandidatePath(candidate.Id), candidate);
    }
    public void DeleteCandidate(Guid id)
    {
        _ = Candidate(id);
        var deleted = Read<Guid[]>("deleted.protected") ?? [];
        // Durable tombstone first: a crash or later observation cannot recreate the source.
        Write("deleted.protected", deleted.Append(id).Distinct().ToArray());
        File.Delete(Full(CandidatePath(id)));
    }
    public IDisposable WatcherLock() => Acquire("watcher.lock", "watcher_already_running");
    public bool WatcherRunning
    {
        get
        {
            try { using var held = Acquire("watcher.lock", "watcher_already_running"); return false; }
            catch (AgentException ex) when (ex.Code == "watcher_already_running") { return true; }
        }
    }

    private FileStream Acquire(string name, string code)
    {
        try { return new FileStream(Full(name), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new AgentException(code); }
    }
    private static string CandidatePath(Guid id) => Path.Combine("candidates", id.ToString("N") + ".protected");
    private string Full(string relative)
    {
        if (!SourcePolicy.SafeFile(root, relative.Replace('\\', '/'))) throw new AgentException("storage_reparse_point");
        return Path.Combine(root, relative);
    }
    public T? ReadDocument<T>(string relative) => Read<T>(relative);
    public void WriteDocument<T>(string relative, T value) => Write(relative, value);
    private T? Read<T>(string relative)
    {
        var path = Full(relative);
        if (!File.Exists(path)) return default;
        byte[]? plain = null;
        try
        {
            plain = protector.Unprotect(File.ReadAllBytes(path));
            if (typeof(T) == typeof(EvidenceCandidate))
            {
                using var document = JsonDocument.Parse(plain);
                var version = document.RootElement.TryGetProperty("recordVersion", out var member) ? member.GetInt32() : 1;
                if (version is not (1 or 2)) throw new AgentException("state_version_unsupported");
            }
            return JsonSerializer.Deserialize<T>(plain, AgentJson.Options) ?? throw new AgentException("state_unreadable");
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or JsonException or NotSupportedException)
        { throw new AgentException("state_unreadable"); }
        finally { if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
    private void Write<T>(string relative, T value)
    {
        var target = Full(relative);
        // Never silently replace unreadable existing records.
        if (File.Exists(target)) _ = Read<T>(relative);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var plain = AgentJson.Bytes(value);
        try
        {
            var encrypted = protector.Protect(plain);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
