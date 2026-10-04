using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Windows;

namespace Sensei.LocalAgent.Tests;

public sealed class WorkflowTests
{
    private static ContributionNotes Notes => new("Developer", "Investigated duplicate delivery", "Compared retry strategies", "Outcome not measured", "Production impact unknown", "peer review", []);

    [Fact]
    public async Task Restart_preserves_candidates_and_deleted_or_dismissed_sources_do_not_reappear()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "saved source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        var restarted = f.Restart();
        Assert.Equal(c.Fingerprint, Assert.Single(restarted.Inbox()).Fingerprint);
        restarted.Dismiss(c.Id, c.Revision);
        Assert.Equal(0, await restarted.Capture(r.Id, SourceKind.Unstaged, null, default));
        Assert.Equal(CandidateStatus.Dismissed, Assert.Single(restarted.Inbox()).Status);
        restarted.Delete(c.Id, c.Revision + 1);
        Assert.Empty(restarted.Inbox());
        Assert.Equal(0, await f.Restart().Capture(r.Id, SourceKind.Unstaged, null, default));
        Assert.Empty(f.Restart().Inbox());
    }

    [Fact]
    public async Task Source_expiry_is_exact_and_preserves_notes_manifest_and_identity()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "retained source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        c = f.Service.Edit(c.Id, c.Revision, Notes);
        f.Clock.Now = c.CapturedAt.AddDays(30).AddTicks(-1);
        Assert.Equal(0, f.Service.PurgeExpired());
        f.Clock.Now = c.CapturedAt.AddDays(30);
        Assert.Equal(1, f.Service.PurgeExpired());
        var purged = f.Service.Candidate(c.Id);
        Assert.Null(purged.SourceText); Assert.Equal(c.Fingerprint, purged.Fingerprint);
        Assert.Equal(AgentJson.Digest(c.Notes), AgentJson.Digest(purged.Notes)); Assert.Equal(c.EventId, purged.EventId);
        Assert.Equal(c.Revision + 1, purged.Revision);
        Assert.Equal(0, f.Service.PurgeExpired());
    }

    [Fact]
    public async Task Manual_source_purge_and_repository_removal_have_separate_effects()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "retained source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        f.Service.PurgeSource(c.Id, c.Revision);
        f.Service.Remove(r.Id);
        Assert.Empty(f.Service.Repositories());
        Assert.Null(Assert.Single(f.Service.Inbox()).SourceText);
        Assert.Equal(Freshness.Unavailable, await f.Service.FreshnessOf(f.Service.Candidate(c.Id), default));
    }

    [Fact]
    public async Task Export_is_a_strict_sanitized_draft_and_exact_preview_revision_is_required()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "PRIVATE IMPLEMENTATION marker\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        c = f.Service.Edit(c.Id, c.Revision, Notes);
        var preview = await f.Service.Preview(c.Id, default);
        var bytes = await f.Service.ConfirmExport(c.Id, preview.Confirmation, default);
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal("client_reported", json.RootElement.GetProperty("claimProvenance").GetString());
        Assert.Equal(c.EventId, json.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(new[] { "eventId", "schemaVersion", "setting", "eventDate", "role", "actions", "alternatives", "outcome", "uncertainty", "assistance", "conceptIds", "claimProvenance" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        var text = Encoding.UTF8.GetString(bytes);
        foreach (var disallowed in new[] { "PRIVATE IMPLEMENTATION", "src/a.txt", r.Root, c.Fingerprint, c.Manifest.BaseIdentity }) Assert.DoesNotContain(disallowed, text);
        f.Service.Edit(c.Id, c.Revision, Notes with { Outcome = "Changed reviewed claim" });
        var failure = await Assert.ThrowsAsync<AgentException>(() => f.Service.ConfirmExport(c.Id, preview.Confirmation, default));
        Assert.Equal("export_revision_changed", failure.Code);
    }

    [Fact]
    public async Task Export_blocks_changed_or_unavailable_working_context_and_credentials_in_notes()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        f.Write("src/a.txt", "captured source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        c = f.Service.Edit(c.Id, c.Revision, Notes);
        var preview = await f.Service.Preview(c.Id, default);
        f.Write("src/a.txt", "later source\n");
        Assert.Equal(Freshness.Changed, await f.Service.FreshnessOf(c, default));
        Assert.Equal("working_context_requires_recapture", (await Assert.ThrowsAsync<AgentException>(() => f.Service.ConfirmExport(c.Id, preview.Confirmation, default))).Code);
        f.Write("src/a.txt", "captured source\n");
        c = f.Service.Edit(c.Id, c.Revision, Notes with { Actions = "github_pat_123456789012345678901234567890" });
        Assert.Equal("export_recognized_credential", (await Assert.ThrowsAsync<AgentException>(() => f.Service.Preview(c.Id, default))).Code);
        f.Service.Remove(r.Id);
        Assert.Equal("working_context_requires_recapture", (await Assert.ThrowsAsync<AgentException>(() => f.Service.Preview(c.Id, default))).Code);
    }

    [Fact]
    public async Task Rebase_and_staging_transitions_invalidate_working_currency_but_not_historical_candidates()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        f.Write("src/a.txt", "working source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var working = Assert.Single(f.Service.Inbox());
        f.Run("add", "src/a.txt");
        Assert.Equal(Freshness.Changed, await f.Service.FreshnessOf(working, default));
        var commit = f.Commit();
        await f.Service.Capture(r.Id, SourceKind.Commit, commit, default);
        var historical = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Commit);
        f.Run("commit", "--amend", "-m", "rewritten identity");
        Assert.Equal(Freshness.Historical, await f.Service.FreshnessOf(historical, default));
        Assert.Equal(commit, historical.Manifest.CommitIdentity);
    }

    [Fact]
    public async Task Protected_files_contain_no_plaintext_source_paths_or_user_notes()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "private_source_test_marker\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        f.Service.Edit(c.Id, c.Revision, Notes with { Actions = "private_notes_test_marker" });
        foreach (var path in Directory.EnumerateFiles(f.StateRoot, "*", SearchOption.AllDirectories))
        {
            var stored = Encoding.UTF8.GetString(File.ReadAllBytes(path));
            Assert.DoesNotContain("private_source_test_marker", stored);
            Assert.DoesNotContain("private_notes_test_marker", stored);
            Assert.DoesNotContain("src/a.txt", stored);
            Assert.DoesNotContain(f.RepositoryRoot, stored);
        }
        Assert.Equal("private_notes_test_marker", f.Restart().Candidate(c.Id).Notes.Actions);
    }

    [Fact]
    public void Dpapi_roundtrip_and_tamper_rejection_are_verified_on_windows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var protection = new WindowsContentProtector();
        var original = Encoding.UTF8.GetBytes("sensitive selected source");
        var encrypted = protection.Protect(original);
        Assert.NotEqual(original, encrypted);
        Assert.Equal(original, protection.Unprotect(encrypted));
        encrypted[^1] ^= 0x55;
        Assert.Throws<CryptographicException>(() => protection.Unprotect(encrypted));
    }

    [Fact]
    public async Task Unreadable_records_are_reported_and_never_overwritten_and_orphan_temps_are_ignored()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        f.Write("src/a.txt", "source\n"); await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        File.WriteAllBytes(Path.Combine(f.StateRoot, "candidates", "interrupted.tmp"), [1, 2, 3]);
        Assert.Single(f.Restart().Inbox());
        var path = Path.Combine(f.StateRoot, "candidates", c.Id.ToString("N") + ".protected");
        byte[] invalid = [1, 2, 3]; File.WriteAllBytes(path, invalid);
        Assert.Equal("state_unreadable", Assert.Throws<AgentException>(() => f.Service.Edit(c.Id, c.Revision, Notes)).Code);
        Assert.Equal(invalid, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task State_and_watcher_locks_prevent_concurrent_mutations_and_stale_notes_fail()
    {
        using var f = new AgentFixture();
        using (f.Service.AcquireWatcher())
        {
            Assert.True(f.Service.WatcherRunning);
            Assert.Equal("watcher_already_running", Assert.Throws<AgentException>(() => f.Restart().AcquireWatcher()).Code);
        }
        Assert.False(f.Service.WatcherRunning);
        using (new FileStream(Path.Combine(f.StateRoot, "state.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal("state_busy", Assert.Throws<AgentException>(() => f.Service.Inbox()).Code);
        var r = await f.Register(); f.Write("src/a.txt", "source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox()); f.Service.Edit(c.Id, c.Revision, Notes);
        Assert.Equal("candidate_revision_changed", Assert.Throws<AgentException>(() => f.Restart().Edit(c.Id, c.Revision, Notes)).Code);
    }

    [Fact]
    public async Task A_failed_candidate_write_does_not_advance_the_commit_checkpoint()
    {
        using var f = new AgentFixture();
        var r = await f.Register(); f.Write("src/a.txt", "new commit\n"); var head = f.Commit();
        var injected = new FailingStore(f.Store);
        var service = new AgentService(injected, f.Git, f.Clock);
        var errors = new List<string>();
        Assert.Equal(0, await service.Poll(default, (_, code) => errors.Add(code)));
        Assert.Contains("repository_unavailable", errors);
        Assert.Equal(r.CommitCheckpoint, Assert.Single(f.Service.Repositories()).CommitCheckpoint);
        Assert.Empty(f.Service.Inbox());
        await f.Restart().Poll(default);
        Assert.Equal(head, Assert.Single(f.Service.Repositories()).CommitCheckpoint);
        Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Commit);
    }

    [Fact]
    public async Task Incomplete_working_manifests_cannot_assert_currency_or_be_exported()
    {
        using var f = new AgentFixture();
        var r = await f.Register(); f.Write("src/untracked.txt", "not reviewed\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        c = f.Service.Edit(c.Id, c.Revision, Notes);
        Assert.Equal(Freshness.Incomplete, await f.Service.FreshnessOf(c, default));
        Assert.Equal("working_context_requires_recapture", (await Assert.ThrowsAsync<AgentException>(() => f.Service.Preview(c.Id, default))).Code);
    }

    [Fact]
    public async Task Orphan_protected_temporary_files_are_removed_after_restart()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true); f.Write("src/a.txt", "retained source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        var durable = Path.Combine(f.StateRoot, "candidates", c.Id.ToString("N") + ".protected");
        var orphan = durable + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.Copy(durable, orphan);
        Assert.Single(f.Restart().Inbox());
        Assert.False(File.Exists(orphan)); Assert.True(File.Exists(durable));
    }

    [Fact]
    public async Task A_checkpoint_failure_after_candidate_save_replays_without_duplication()
    {
        using var f = new AgentFixture();
        var r = await f.Register(); f.Write("src/a.txt", "new commit\n"); var head = f.Commit();
        var failing = new FailingCheckpointStore(f.Store);
        await new AgentService(failing, f.Git, f.Clock).Poll(default);
        Assert.Equal(r.CommitCheckpoint, Assert.Single(f.Service.Repositories()).CommitCheckpoint);
        var saved = Assert.Single(f.Service.Inbox());
        Assert.Equal(0, await f.Restart().Poll(default));
        Assert.Equal(head, Assert.Single(f.Service.Repositories()).CommitCheckpoint);
        Assert.Equal(saved.Id, Assert.Single(f.Service.Inbox()).Id);
    }

    [Fact]
    public async Task Unstable_working_observations_are_not_saved()
    {
        using var f = new AgentFixture();
        var r = await f.Register(); f.Write("src/a.txt", "source\n");
        var real = await f.Git.Capture(r, SourceKind.Unstaged, null, default);
        var changing = new ChangingGit(f.Git, real);
        var service = new AgentService(f.Store, changing, f.Clock);
        Assert.Equal("source_changed_during_capture", (await Assert.ThrowsAsync<AgentException>(() => service.Capture(r.Id, SourceKind.Unstaged, null, default))).Code);
        Assert.Empty(f.Service.Inbox());
    }

    private sealed class FailingStore(IAgentStore inner) : IAgentStore
    {
        public T WithLock<T>(Func<T> a) => inner.WithLock(a);
        public RepositoryRegistration[] Repositories() => inner.Repositories();
        public void SaveRepositories(RepositoryRegistration[] r) => inner.SaveRepositories(r);
        public EvidenceCandidate[] Candidates() => inner.Candidates();
        public EvidenceCandidate Candidate(Guid id) => inner.Candidate(id);
        public void SaveCandidate(EvidenceCandidate c) => throw new IOException("Injected write failure");
        public void DeleteCandidate(Guid id) => inner.DeleteCandidate(id);
        public bool IsDeleted(Guid id) => inner.IsDeleted(id);
        public IDisposable WatcherLock() => inner.WatcherLock();
        public bool WatcherRunning => inner.WatcherRunning;
    }
    private sealed class ChangingGit(IGitContext real, CaptureResult observation) : IGitContext
    {
        private int calls;
        public Task<string> ResolveRoot(string p, CancellationToken ct) => real.ResolveRoot(p, ct);
        public Task<string?> Head(string root, CancellationToken ct) => real.Head(root, ct);
        public Task<string[]> NewCommits(RepositoryRegistration r, CancellationToken ct) => real.NewCommits(r, ct);
        public Task<CaptureResult> Capture(RepositoryRegistration r, SourceKind kind, string? c, CancellationToken ct)
            => Task.FromResult(observation with { Manifest = observation.Manifest with { BaseIdentity = (++calls).ToString() } });
    }
    private sealed class FailingCheckpointStore(IAgentStore inner) : IAgentStore
    {
        public T WithLock<T>(Func<T> a) => inner.WithLock(a);
        public RepositoryRegistration[] Repositories() => inner.Repositories();
        public void SaveRepositories(RepositoryRegistration[] r) => throw new IOException("Injected checkpoint failure");
        public EvidenceCandidate[] Candidates() => inner.Candidates();
        public EvidenceCandidate Candidate(Guid id) => inner.Candidate(id);
        public void SaveCandidate(EvidenceCandidate c) => inner.SaveCandidate(c);
        public void DeleteCandidate(Guid id) => inner.DeleteCandidate(id);
        public bool IsDeleted(Guid id) => inner.IsDeleted(id);
        public IDisposable WatcherLock() => inner.WatcherLock();
        public bool WatcherRunning => inner.WatcherRunning;
    }
}
