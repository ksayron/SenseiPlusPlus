using System.Diagnostics;
using System.Text.Json;
using Sensei.LocalAgent.Cli;
using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task Cli_workflow_reviews_notes_and_exports_only_the_confirmed_draft()
    {
        using var f = new AgentFixture();
        var writer = new StringWriter();
        var app = new CliApp(f.Service, new StringReader(""), writer, f.StateRoot);
        await app.Run(["repo", "add", "--root", f.RepositoryRoot, "--path", "src", "--setting", "Coursework", "--source", "diffs"], default);
        var r = Assert.Single(f.Service.Repositories());
        f.Write("src/a.txt", "private_cli_source_marker\n");
        await app.Run(["capture", r.Id.ToString(), "--kind", "Unstaged"], default);
        var c = Assert.Single(f.Service.Inbox());
        writer.GetStringBuilder().Clear();
        await app.Run(["inbox", "show", c.Id.ToString()], default);
        Assert.DoesNotContain("private_cli_source_marker", writer.ToString());
        Assert.Contains("ReflectionPrompts", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        writer.GetStringBuilder().Clear();
        await app.Run(["inbox", "show", c.Id.ToString(), "--source"], default);
        Assert.Contains("private_cli_source_marker", writer.ToString());
        var notes = Path.Combine(f.DirectoryRoot, "notes.json");
        File.WriteAllBytes(notes, AgentJson.Bytes(new ContributionNotes("Student developer", "Compared recovery strategies", "Retry and stop", "Not measured", "Unknown scale", "none", [])));
        await app.Run(["inbox", "edit", c.Id.ToString(), "--revision", "1", "--notes", notes], default);
        writer.GetStringBuilder().Clear();
        var target = Path.Combine(f.DirectoryRoot, "draft.json");
        await app.Run(["export", c.Id.ToString(), "--preview", "--output", target], default);
        Assert.False(File.Exists(target));
        var confirmation = writer.ToString().Split('\n').Single(l => l.StartsWith("confirmation=", StringComparison.Ordinal))["confirmation=".Length..].Trim();
        await app.Run(["export", c.Id.ToString(), "--confirm", confirmation, "--output", target], default);
        var exported = JsonSerializer.Deserialize<SanitizedEventDraftV1>(File.ReadAllBytes(target), AgentJson.Options)!;
        Assert.Equal("client_reported", exported.ClaimProvenance); Assert.Equal(c.EventId, exported.EventId);
        Assert.DoesNotContain("private_cli_source_marker", File.ReadAllText(target));
        await Assert.ThrowsAsync<IOException>(() => app.Run(["export", c.Id.ToString(), "--confirm", confirmation, "--output", target], default));
        await app.Run(["source", "purge", c.Id.ToString(), "--revision", "2"], default);
        Assert.Null(f.Service.Candidate(c.Id).SourceText);
        await app.Run(["inbox", "dismiss", c.Id.ToString(), "--revision", "3"], default);
        await app.Run(["inbox", "delete", c.Id.ToString(), "--revision", "4"], default);
        await app.Run(["repo", "remove", r.Id.ToString()], default);
        Assert.Empty(f.Service.Inbox()); Assert.Empty(f.Service.Repositories());
    }

    [Fact]
    public async Task Watch_survives_transient_store_contention_and_releases_its_lock_on_cancellation()
    {
        using var f = new AgentFixture();
        var writer = new StringWriter();
        var app = new CliApp(f.Service, new StringReader(""), writer, f.StateRoot);
        using var held = new FileStream(Path.Combine(f.StateRoot, "state.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cancel = new CancellationTokenSource();
        var running = app.Run(["watch"], cancel.Token);
        Assert.Contains("retry_on_next_poll", writer.ToString());
        Assert.True(f.Service.WatcherRunning);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.False(f.Service.WatcherRunning);
    }

    [Fact]
    public async Task Cli_rejects_unknown_options_missing_consent_and_unrecognized_note_fields()
    {
        using var f = new AgentFixture();
        var app = new CliApp(f.Service, new StringReader(""), new StringWriter(), f.StateRoot);
        await Assert.ThrowsAsync<AgentException>(() => app.Run(["repo", "add", "--root", f.RepositoryRoot, "--path", "src", "--setting", "Coursework"], default));
        await Assert.ThrowsAsync<AgentException>(() => app.Run(["status", "--typo", "value"], default));
        await Assert.ThrowsAsync<AgentException>(() => app.Run(["status", "--typo", "one", "--typo", "two"], default));
        Assert.Empty(f.Service.Repositories());
        var r = await f.Register(); f.Write("src/a.txt", "source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        var notes = Path.Combine(f.DirectoryRoot, "invalid-notes.json");
        File.WriteAllText(notes, "{\"role\":\"role\",\"ownerId\":\"external\"}");
        Assert.Equal("invalid_notes_json", (await Assert.ThrowsAsync<AgentException>(() => app.Run(["inbox", "edit", c.Id.ToString(), "--revision", "1", "--notes", notes], default))).Code);
        Assert.Equal(1, f.Service.Candidate(c.Id).Revision);
    }

    [Fact]
    public async Task Installed_cli_watch_restart_and_export_work_on_windows_without_a_backend()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new AgentFixture();
        var agent = FindAgentRoot();
        var dll = Path.Combine(agent, "src", "Sensei.LocalAgent.Cli", "bin", "Release", "net10.0", "sensei-agent.dll");
        async Task<string> Cli(params string[] args)
        {
            using var p = Start(dll, f.StateRoot, args);
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await p.WaitForExitAsync(limit.Token); }
            finally { if (!p.HasExited) p.Kill(true); }
            Assert.Equal(0, p.ExitCode);
            var error = await stderr;
            Assert.True(string.IsNullOrEmpty(error), error);
            return await stdout;
        }
        var added = await Cli("repo", "add", "--root", f.RepositoryRoot, "--path", "src", "--setting", "PersonalProject", "--source", "diffs");
        var r = JsonSerializer.Deserialize<RepositoryRegistration>(added, AgentJson.Options)!;
        f.Write("src/a.txt", "actual_process_source_marker\n");
        using (var watcher = Start(dll, f.StateRoot, ["watch"]))
        {
            try
            {
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string? line;
                do { line = await watcher.StandardOutput.ReadLineAsync(wait.Token); }
                while (line is not null && !line.StartsWith("candidates_added=", StringComparison.Ordinal));
                Assert.NotNull(line);
                using var status = JsonDocument.Parse(await Cli("status"));
                Assert.True(status.RootElement.GetProperty("watcherRunning").GetBoolean());
            }
            finally { if (!watcher.HasExited) watcher.Kill(true); await watcher.WaitForExitAsync(); }
        }
        using var statusAfter = JsonDocument.Parse(await Cli("status"));
        Assert.False(statusAfter.RootElement.GetProperty("watcherRunning").GetBoolean());
        var candidates = f.Restart().Inbox();
        var c = Assert.Single(candidates);
        Assert.Equal(r.Id, c.RepositoryId);
        var notes = Path.Combine(f.DirectoryRoot, "cli-notes.json");
        File.WriteAllBytes(notes, AgentJson.Bytes(new ContributionNotes("Developer", "Reviewed recovery behavior", "Compared two approaches", "Unknown", "Not deployed", "none", [])));
        await Cli("inbox", "edit", c.Id.ToString(), "--revision", "1", "--notes", notes);
        var preview = await Cli("export", c.Id.ToString(), "--preview");
        var token = preview.Split('\n').Single(l => l.StartsWith("confirmation=", StringComparison.Ordinal))["confirmation=".Length..].Trim();
        var exported = Path.Combine(f.DirectoryRoot, "process-export.json");
        await Cli("export", c.Id.ToString(), "--confirm", token, "--output", exported);
        Assert.DoesNotContain("actual_process_source_marker", File.ReadAllText(exported));
        Assert.Equal("client_reported", JsonSerializer.Deserialize<SanitizedEventDraftV1>(File.ReadAllBytes(exported), AgentJson.Options)!.ClaimProvenance);
        Assert.Single(f.Restart().Inbox());
    }

    private static Process Start(string dll, string state, string[] args)
    {
        var p = new Process { StartInfo = new("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        // A parent terminal's Git environment must not redirect collection.
        p.StartInfo.Environment["GIT_DIR"] = Path.Combine(state, "not-a-repository");
        p.StartInfo.Environment["GIT_WORK_TREE"] = state;
        foreach (var arg in new[] { dll, "--state-dir", state }.Concat(args)) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        return p;
    }
    private static string FindAgentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentSpp.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
