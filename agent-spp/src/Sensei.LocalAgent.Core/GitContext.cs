using System.Text;

namespace Sensei.LocalAgent.Core;

public sealed class GitContext(GitProcess? process = null) : IGitContext
{
    private readonly GitProcess git = process ?? new();
    public const int MaximumFiles = 100;
    public const int MaximumTextBytes = 2 * 1024 * 1024;

    public async Task<string> ResolveRoot(string path, CancellationToken ct)
    {
        var root = Path.GetFullPath(path);
        var output = await Required(root, ["rev-parse", "--show-toplevel"], ct);
        root = Path.GetFullPath(Text(output).TrimEnd('\r', '\n'));
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new AgentException("repository_reparse_point");
        return root;
    }

    public async Task<string?> Head(string root, CancellationToken ct)
    {
        var r = await git.Run(root, ["rev-parse", "--verify", "HEAD"], ct);
        return r.ExitCode == 0 ? ObjectId(Text(r.Bytes).Trim()) : null;
    }

    public async Task<string[]> NewCommits(RepositoryRegistration repository, CancellationToken ct)
    {
        var head = await Head(repository.Root, ct);
        if (head is null || head == repository.CommitCheckpoint) return [];
        var args = new List<string> { "rev-list", "--reverse", "--topo-order", "--max-count=100", head };
        if (repository.CommitCheckpoint is { } previous)
        {
            var exists = await git.Run(repository.Root, ["cat-file", "-e", ObjectId(previous)], ct);
            if (exists.ExitCode != 0) throw new AgentException("commit_checkpoint_unavailable");
            args.Add("^" + ObjectId(previous));
        }
        // Limit each reconciliation batch; the checkpoint is advanced commit by commit.
        // Reverse a bounded newest-first list would skip older commits, so require the full
        // remaining range to fit before advancing a checkpoint.
        args[3] = "--max-count=101";
        var commits = Text(await Required(repository.Root, args, ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => ObjectId(s.Trim())).ToArray();
        if (commits.Length > 100) throw new AgentException("commit_backlog_exceeds_100_recapture_baseline");
        return commits;
    }

    public async Task<CaptureResult> Capture(RepositoryRegistration repository, SourceKind kind, string? commit, CancellationToken ct)
    {
        var selection = SourcePolicy.Selection(repository.Selection);
        var root = repository.Root;
        var head = await Head(root, ct);
        var diff = new List<string> { "diff", "--no-ext-diff", "--no-textconv", "--no-color", "--find-renames=50%" };
        string baseIdentity;
        bool merge = false;
        if (kind == SourceKind.Commit)
        {
            commit = ObjectId(commit ?? throw new AgentException("commit_required"));
            var parents = Text(await Required(root, ["rev-list", "--parents", "-n", "1", commit], ct)).Trim().Split(' ');
            merge = parents.Length > 2;
            baseIdentity = parents.Length > 1 ? ObjectId(parents[1]) : await EmptyTree(root, ct);
            diff.AddRange([baseIdentity, commit]);
        }
        else if (kind == SourceKind.Staged)
        {
            baseIdentity = head ?? await EmptyTree(root, ct);
            diff.AddRange(["--cached", baseIdentity]);
        }
        else
        {
            var index = await Required(root, new[] { "ls-files", "--stage", "-z", "--" }.Concat(selection), ct);
            baseIdentity = "index-sha256:" + SourcePolicy.Hash(index) + ":head:" + (head ?? "unborn");
        }

        var rawArgs = diff.Concat(["--raw", "--no-abbrev", "-z", "--"]).Concat(selection);
        var files = ParseRaw(await Required(root, rawArgs, ct));
        if (kind == SourceKind.Unstaged)
        {
            var untracked = await Required(root, new[] { "ls-files", "--others", "--exclude-standard", "-z", "--" }.Concat(selection), ct);
            files.AddRange(Nul(untracked).Select(p => new ManifestFile(p, null, "?", "000000", "000000", "", "", "untracked")));
        }
        files = files.Where(f => SourcePolicy.Selected(f.Path, selection) || f.OldPath is { } old && SourcePolicy.Selected(old, selection)).ToList();
        var paths = files.SelectMany(f => f.OldPath is null ? new[] { f.Path } : new[] { f.Path, f.OldPath }).Distinct(StringComparer.Ordinal).ToArray();
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        if (paths.Length != 0)
        {
            var r = await git.Run(root, ["check-ignore", "--no-index", "-z", "--stdin"], ct, Encoding.UTF8.GetBytes(string.Join('\0', paths) + "\0"));
            if (r.ExitCode is not (0 or 1)) throw new AgentException("ignore_rules_unavailable");
            ignored.UnionWith(Nul(r.Bytes));
        }
        files = files.Where(f => !ignored.Contains(f.Path) && (f.OldPath is null || !ignored.Contains(f.OldPath)) &&
            !SourcePolicy.CredentialPath(f.Path) && (f.OldPath is null || !SourcePolicy.CredentialPath(f.OldPath))).ToList();

        var limitations = new List<string>();
        if (files.Count > MaximumFiles) limitations.Add("file_limit_exceeded");
        var result = new List<ManifestFile>();
        var source = new StringBuilder();
        var retainedBytes = 0;
        foreach (var original in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var f = original;
            string? patch = null;
            if (result.Count >= MaximumFiles) f = f with { UnreviewedReason = "file_limit_exceeded" };
            else if (f.OldMode == "160000" || f.NewMode == "160000") f = f with { UnreviewedReason = "submodule" };
            else if (f.OldMode == "120000" || f.NewMode == "120000" || kind != SourceKind.Commit &&
                (!SourcePolicy.SafeFile(root, f.Path) || f.OldPath is { } op && !SourcePolicy.SafeFile(root, op))) f = f with { UnreviewedReason = "symlink_or_reparse_point" };
            else if (f.Status.StartsWith('U')) f = f with { UnreviewedReason = "unmerged" };
            if (f.UnreviewedReason is null)
            {
                try
                {
                    var before = await Blob(root, f.OldIdentity, ct);
                    byte[] after;
                    if (kind == SourceKind.Unstaged && f.NewMode != "000000")
                    {
                        var path = Path.Combine(root, f.Path.Replace('/', Path.DirectorySeparatorChar));
                        after = await WorkingBytes(root, f.Path, ct);
                        f = f with { NewIdentity = "sha256:" + SourcePolicy.Hash(after) };
                    }
                    else after = await Blob(root, f.NewIdentity, ct);
                    if (before.Contains((byte)0) || after.Contains((byte)0)) throw new AgentException("binary");
                    _ = SourcePolicy.StrictUtf8.GetString(before);
                    _ = SourcePolicy.StrictUtf8.GetString(after);
                    var pathspec = f.OldPath is null ? new[] { f.Path } : new[] { f.Path, f.OldPath };
                    var bytes = await Required(root, diff.Concat(["--patch", "--",]).Concat(pathspec), ct);
                    patch = SourcePolicy.StrictUtf8.GetString(bytes);
                    if (kind == SourceKind.Unstaged && f.NewMode != "000000")
                    {
                        if (!SourcePolicy.SafeFile(root, f.Path)) throw new AgentException("source_changed_during_capture");
                        var current = await WorkingBytes(root, f.Path, ct);
                        if (SourcePolicy.Hash(current) != SourcePolicy.Hash(after)) throw new AgentException("source_changed_during_capture");
                    }
                    if (SecretGuard.Contains(patch)) throw new AgentException("recognized_credential");
                    if (retainedBytes + bytes.Length > MaximumTextBytes) throw new AgentException("text_limit_exceeded");
                    retainedBytes += bytes.Length;
                }
                catch (DecoderFallbackException) { f = f with { UnreviewedReason = "unsupported_encoding" }; }
                catch (IOException) { throw new AgentException("source_changed_during_capture"); }
                catch (AgentException ex) when (ex.Code is "oversized" or "binary" or "recognized_credential" or "text_limit_exceeded" or "git_output_limit")
                { f = f with { UnreviewedReason = ex.Code }; patch = null; }
            }
            result.Add(f);
            if (f.UnreviewedReason is null && repository.RetainSource) source.Append(patch);
        }
        var complete = limitations.Count == 0 && result.All(f => f.UnreviewedReason is null);
        return new(new(1, kind, baseIdentity, kind == SourceKind.Commit ? commit : null, selection, result.ToArray(), merge, complete, limitations.ToArray()),
            repository.RetainSource && source.Length != 0 ? source.ToString() : null);
    }

    private static async Task<byte[]> WorkingBytes(string root, string path, CancellationToken ct)
    {
        if (!SourcePolicy.SafeFile(root, path)) throw new AgentException("source_changed_during_capture");
        await using var stream = new FileStream(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)), FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, FileOptions.Asynchronous);
        if (!SourcePolicy.SafeFile(root, path)) throw new AgentException("source_changed_during_capture");
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (data.Length + count > MaximumTextBytes) throw new AgentException("oversized");
            data.Write(buffer, 0, count);
        }
        return data.ToArray();
    }

    private async Task<byte[]> Blob(string root, string identity, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(identity) || identity.All(c => c == '0')) return [];
        identity = ObjectId(identity);
        var size = Text(await Required(root, ["cat-file", "-s", identity], ct)).Trim();
        if (!long.TryParse(size, out var count) || count > MaximumTextBytes) throw new AgentException("oversized");
        return await Required(root, ["cat-file", "blob", identity], ct);
    }

    private async Task<string> EmptyTree(string root, CancellationToken ct) => ObjectId(Text(await Required(root, ["hash-object", "-t", "tree", "--stdin"], ct)).Trim());
    private async Task<byte[]> Required(string root, IEnumerable<string> args, CancellationToken ct)
    {
        var output = await git.Run(root, args, ct);
        if (output.ExitCode != 0) throw new AgentException("git_read_failed");
        return output.Bytes;
    }
    private static string Text(byte[] bytes) => SourcePolicy.StrictUtf8.GetString(bytes);
    private static string[] Nul(byte[] bytes) => Text(bytes).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    private static string ObjectId(string value) => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : throw new AgentException("invalid_git_identity");

    private static List<ManifestFile> ParseRaw(byte[] bytes)
    {
        var tokens = Nul(bytes);
        var files = new List<ManifestFile>();
        for (var i = 0; i < tokens.Length; i++)
        {
            var header = tokens[i].TrimStart(':').Split(' ');
            if (header.Length != 5 || ++i >= tokens.Length) throw new AgentException("unsupported_git_raw_format");
            var path = tokens[i];
            string? old = null;
            if (header[4][0] is 'R' or 'C')
            {
                old = path;
                if (++i >= tokens.Length) throw new AgentException("unsupported_git_raw_format");
                path = tokens[i];
            }
            files.Add(new(path, old, header[4], header[0], header[1], header[2], header[3], null));
        }
        return files;
    }
}
