using System.Text;
using System.Text.Json;
using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Cli;

public sealed class CliApp(AgentService service, TextReader input, TextWriter output, string stateRoot, Sensei.LocalAgent.Sync.SubmissionService? synchronization = null)
{
    public const string Help = """
        Sensei++ local evidence agent (Windows storage; reviewed manual synchronization; no AI calls)
        repo add --root DIR --path PATH [--path PATH] --setting SETTING --source metadata|diffs
        repo list
        repo configure ID [--path PATH] [--setting SETTING] [--source metadata|diffs] [--reset-baseline]
        repo remove ID
        watch                         Poll every 5 seconds until Ctrl+C
        status
        capture REPO_ID --kind Staged|Unstaged|Commit [--commit OBJECT_ID]
        inbox list
        inbox show ID [--source]       Source text is shown only with --source
        inbox edit ID --revision N [--notes JSON_FILE]
        inbox dismiss ID --revision N
        inbox delete ID --revision N
        source purge ID --revision N
        source purge-expired
        export ID [--preview] [--confirm TOKEN --output JSON_FILE]
        auth login --server URL [--allow-insecure-loopback]
        auth status | auth logout
        concepts
        submit ID --preview | submit ID --confirm TOKEN
        outbox list | outbox show EVENT_ID | outbox cancel EVENT_ID
        sync                          Send only explicitly confirmed drafts
        Global: --state-dir DIR (outside repositories; still protected with Windows DPAPI)
        Settings: Employment, Coursework, PersonalProject, Other.
        Export is a user-reviewed client-reported draft, not a trusted assessment.
        """;

    public async Task<int> Run(string[] args, CancellationToken ct)
    {
        var command = args.FirstOrDefault() ?? throw new AgentException("command_required");
        if (command != "watch") service.PurgeExpired();
        if (command is "auth" or "submit" or "outbox" or "sync" or "concepts")
        {
            await new SyncCommands(synchronization ?? throw new AgentException("sync_not_configured"), output).Run(args, ct);
            return 0;
        }
        var parsed = new Arguments(args.Skip(1).ToArray());
        switch (command)
        {
            case "repo": await Repository(parsed, ct); break;
            case "watch":
                parsed.Require([], 0);
                using (service.AcquireWatcher())
                {
                    output.WriteLine("watch_started (5-second polling; Ctrl+C stops)");
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var count = await service.Poll(ct, (id, code) => { if (code != "poll_complete") output.WriteLine($"repository={id} status={code}"); });
                            if (count != 0) output.WriteLine($"candidates_added={count}");
                        }
                        catch (AgentException ex) when (ex.Code == "state_busy") { output.WriteLine("status=state_busy; retry_on_next_poll"); }
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    }
                }
            case "status":
                parsed.Require([], 0);
                Json(new { WatcherRunning = service.WatcherRunning, Repositories = service.Repositories().Length, Candidates = service.Inbox().Length });
                break;
            case "capture":
                parsed.Require(["kind", "commit"], 1);
                var kind = parsed.Enum<SourceKind>("kind");
                if (kind != SourceKind.Commit && parsed.Value("commit") is not null) throw new AgentException("commit_only_for_commit_capture");
                output.WriteLine($"candidates_added={await service.Capture(parsed.Id(0), kind, parsed.Value("commit"), ct)}");
                break;
            case "inbox": await Inbox(parsed, ct); break;
            case "source":
                if (parsed.Position(0) == "purge-expired")
                { parsed.Require([], 1); output.WriteLine($"sources_purged={service.PurgeExpired()}"); }
                else if (parsed.Position(0) == "purge")
                { parsed.Require(["revision"], 2); Json(service.PurgeSource(parsed.Id(1), parsed.Revision()).Revision); }
                else throw new AgentException("unknown_source_command");
                break;
            case "export": await Export(parsed, ct); break;
            default: throw new AgentException("unknown_command");
        }
        return 0;
    }

    private async Task Repository(Arguments a, CancellationToken ct)
    {
        switch (a.Position(0))
        {
            case "add":
                a.Require(["root", "path", "setting", "source"], 1);
                var repoRoot = a.Required("root");
                var root = Path.GetFullPath(repoRoot);
                var state = Path.GetFullPath(stateRoot);
                if (state.Equals(root, StringComparison.OrdinalIgnoreCase) || state.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new AgentException("state_directory_inside_repository");
                Json(await service.Register(repoRoot, a.All("path"), a.Enum<WorkSetting>("setting"), SourceRetention(a.Required("source")), ct));
                break;
            case "list": a.Require([], 1); Json(service.Repositories()); break;
            case "configure":
                a.Require(["path", "setting", "source", "reset-baseline"], 2);
                var r = service.Repositories().SingleOrDefault(r => r.Id == a.Id(1)) ?? throw new AgentException("repository_not_found");
                var paths = a.All("path");
                var reset = a.Flag("reset-baseline");
                var head = reset ? await new GitContext().Head(r.Root, ct) : null;
                service.Configure(r.Id, paths.Length == 0 ? null : paths,
                    a.Value("source") is { } s ? SourceRetention(s) : null,
                    a.Value("setting") is not null ? a.Enum<WorkSetting>("setting") : null, reset, head);
                output.WriteLine(reset ? "repository_configured; commit_baseline_reset; existing_candidates_retained" : "repository_configured");
                break;
            case "remove": a.Require([], 2); service.Remove(a.Id(1)); output.WriteLine("repository_removed; existing_candidates_retained"); break;
            default: throw new AgentException("unknown_repository_command");
        }
    }

    private async Task Inbox(Arguments a, CancellationToken ct)
    {
        switch (a.Position(0))
        {
            case "list":
                a.Require([], 1);
                Json(service.Inbox().OrderByDescending(c => c.CapturedAt).Select(c => new { c.Id, c.RepositoryId, c.Manifest.Kind, c.CapturedAt, c.Revision, c.Status, c.Manifest.Complete, SourceAvailable = c.SourceText is not null }));
                break;
            case "show":
                a.Require(["source"], 2);
                var c = service.Candidate(a.Id(1));
                Json(new { c.Id, c.EventId, c.RepositoryId, c.Setting, c.CapturedAt, c.Revision, c.Status, c.Manifest,
                    Freshness = await service.FreshnessOf(c, ct), SourceAvailable = c.SourceText is not null, c.Notes,
                    ReflectionPrompts = new[] { "What was your personal contribution?", "Which alternative did you reject, and why?", "What outcome did you observe, and what remains unknown?" } });
                if (a.Flag("source")) Json(new { SourceText = c.SourceText });
                break;
            case "edit":
                a.Require(["revision", "notes"], 2);
                ContributionNotes notes;
                if (a.Value("notes") is { } file)
                {
                    if (new FileInfo(file).Length > 32 * 1024) throw new AgentException("notes_file_too_large");
                    try { notes = JsonSerializer.Deserialize<ContributionNotes>(await File.ReadAllTextAsync(file, ct), AgentJson.Options) ?? throw new AgentException("invalid_notes_json"); }
                    catch (JsonException) { throw new AgentException("invalid_notes_json"); }
                }
                else
                {
                    var existing = service.Candidate(a.Id(1)).Notes;
                    output.WriteLine("Enter contribution notes. Empty input keeps that field; enter '-' to clear it.");
                    notes = new(Prompt("Role", existing.Role), Prompt("Actions", existing.Actions), Prompt("Alternatives", existing.Alternatives),
                        Prompt("Outcome", existing.Outcome), Prompt("Uncertainty", existing.Uncertainty), Prompt("Assistance (e.g. none, AI-assisted, peer review)", existing.Assistance), existing.ConceptIds, Prompt("Title", existing.Title), Prompt("Context", existing.Context));
                }
                output.WriteLine($"revision={service.Edit(a.Id(1), a.Revision(), notes).Revision}");
                break;
            case "dismiss": a.Require(["revision"], 2); output.WriteLine($"revision={service.Dismiss(a.Id(1), a.Revision()).Revision}"); break;
            case "delete": a.Require(["revision"], 2); service.Delete(a.Id(1), a.Revision()); synchronization?.CancelCandidate(a.Id(1)); output.WriteLine("candidate_deleted"); break;
            default: throw new AgentException("unknown_inbox_command");
        }
    }

    private async Task Export(Arguments a, CancellationToken ct)
    {
        a.Require(["preview", "confirm", "output"], 1);
        if (a.Flag("preview") && a.Value("confirm") is not null) throw new AgentException("preview_and_confirmation_are_separate");
        var preview = await service.Preview(a.Id(0), ct);
        Json(preview.Draft);
        var c = service.Candidate(a.Id(0));
        output.WriteLine($"review_revision={preview.Revision} complete={c.Manifest.Complete} provenance=client_reported");
        output.WriteLine("Review all prose for sensitive information. This export is a draft; it does not confirm an achievement.");
        output.WriteLine($"confirmation={preview.Confirmation}");
        if (a.Flag("preview")) return;
        var target = a.Required("output");
        var confirmation = a.Value("confirm");
        if (confirmation is null)
        {
            output.WriteLine("Type the exact confirmation token to export this revision:");
            confirmation = input.ReadLine();
        }
        if (confirmation != preview.Confirmation) throw new AgentException("export_confirmation_required");
        var bytes = await service.ConfirmExport(a.Id(0), confirmation, ct);
        var targetPath = Path.GetFullPath(target);
        var storage = Path.GetFullPath(stateRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (targetPath.StartsWith(storage, StringComparison.OrdinalIgnoreCase)) throw new AgentException("export_inside_state_directory");
        // CreateNew prevents a sanitized export from replacing unrelated user files.
        await using var stream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
        output.WriteLine("sanitized_draft_exported");
    }

    private string Prompt(string label, string existing)
    {
        output.Write(label + ": ");
        var value = input.ReadLine() ?? throw new AgentException("input_closed");
        return value == "-" ? "" : value.Length == 0 ? existing : value;
    }
    private void Json<T>(T value) => output.WriteLine(JsonSerializer.Serialize(value, AgentJson.Options));
    private static bool SourceRetention(string value) => value switch { "metadata" => false, "diffs" => true, _ => throw new AgentException("source_must_be_metadata_or_diffs") };

    private sealed class Arguments
    {
        private readonly List<string> positions = [];
        private readonly Dictionary<string, List<string>> options = [];
        private static readonly HashSet<string> Flags = ["source", "reset-baseline", "preview"];
        public Arguments(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal)) { positions.Add(arg); continue; }
                var key = arg[2..];
                // --source is a flag only for inbox show; otherwise it requires metadata/diffs.
                var isFlag = Flags.Contains(key) && (key != "source" || i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal));
                if (!options.TryGetValue(key, out var values)) options[key] = values = [];
                if (values.Count != 0 && key != "path") throw new AgentException("duplicate_option");
                if (isFlag) values.Add("true");
                else if (++i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)) values.Add(args[i]);
                else throw new AgentException("option_value_required");
            }
        }
        public void Require(string[] allowed, int count)
        { if (positions.Count != count || options.Keys.Any(k => !allowed.Contains(k))) throw new AgentException("invalid_command_arguments"); }
        public string Position(int index) => positions.ElementAtOrDefault(index) ?? throw new AgentException("command_argument_required");
        public Guid Id(int index) => Guid.TryParse(Position(index), out var id) && id != Guid.Empty ? id : throw new AgentException("invalid_identifier");
        public string? Value(string key) => options.GetValueOrDefault(key)?.SingleOrDefault();
        public string Required(string key) => Value(key) ?? throw new AgentException("required_option_" + key);
        public string[] All(string key) => options.GetValueOrDefault(key)?.ToArray() ?? [];
        public bool Flag(string key) => Value(key) is { } v ? v == "true" ? true : throw new AgentException("flag_has_value") : false;
        public int Revision() => int.TryParse(Required("revision"), out var value) && value > 0 ? value : throw new AgentException("invalid_revision");
        public T Enum<T>(string key) where T : struct, Enum => System.Enum.TryParse<T>(Required(key), true, out var value) && System.Enum.IsDefined(value) ? value : throw new AgentException("invalid_" + key);
    }
}
