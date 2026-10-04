using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Tests;

public sealed class CaptureTests
{
    [Fact]
    public async Task Unborn_repositories_capture_staged_files_and_the_first_root_commit()
    {
        using var f = new AgentFixture();
        var empty = Path.Combine(f.DirectoryRoot, "unborn");
        Directory.CreateDirectory(empty);
        AgentFixture.RunAt(empty, "init", "-b", "main");
        AgentFixture.RunAt(empty, "config", "user.name", "Fixture");
        AgentFixture.RunAt(empty, "config", "user.email", "fixture@example.invalid");
        File.WriteAllText(Path.Combine(empty, "first.txt"), "first source\n");
        var r = await f.Service.Register(empty, ["first.txt"], WorkSetting.Coursework, true, default);
        Assert.Null(r.CommitCheckpoint);
        AgentFixture.RunAt(empty, "add", "first.txt");
        await f.Service.Capture(r.Id, SourceKind.Staged, null, default);
        Assert.Contains("first source", Assert.Single(f.Service.Inbox()).SourceText);
        AgentFixture.RunAt(empty, "commit", "-m", "first commit");
        await f.Service.Poll(default);
        var root = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Commit);
        Assert.True(root.Manifest.Complete);
        Assert.Contains("first source", root.SourceText);
    }

    [Fact]
    public async Task Git_subprocess_output_is_bounded_and_cancellation_is_honored()
    {
        using var f = new AgentFixture();
        var bounded = new GitProcess(TimeSpan.FromSeconds(5), outputLimit: 32);
        Assert.Equal("git_output_limit", (await Assert.ThrowsAsync<AgentException>(() => bounded.Run(f.RepositoryRoot, ["show", "--format=raw", "--no-patch", "HEAD"], default))).Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitProcess().Run(f.RepositoryRoot, ["rev-parse", "HEAD"], cancelled.Token));
    }

    [Fact]
    public async Task Registration_does_not_backfill_and_requires_explicit_safe_selection()
    {
        using var f = new AgentFixture();
        foreach (var selection in new[] { Array.Empty<string>(), new[] { "." }, new[] { "../other" }, new[] { ".git" }, new[] { "C:/elsewhere" } })
            await Assert.ThrowsAsync<AgentException>(() => f.Service.Register(f.RepositoryRoot, selection, WorkSetting.Coursework, false, default));
        var r = await f.Register();
        Assert.Equal(f.Run("rev-parse", "HEAD").Trim(), r.CommitCheckpoint);
        Assert.Equal(0, await f.Service.Poll(default));
        Assert.Empty(f.Service.Inbox());
    }

    [Fact]
    public async Task Staged_unstaged_and_committed_sources_are_distinct_and_deduplicated()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "staged version\n"); f.Run("add", "src/a.txt");
        f.Write("src/a.txt", "unstaged version\n");
        Assert.Equal(0, await f.Service.Poll(default));
        Assert.Equal(2, await f.Service.Poll(default));
        Assert.Equal(0, await f.Service.Poll(default));
        var staged = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Staged);
        var unstaged = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Unstaged);
        Assert.Contains("staged version", staged.SourceText);
        Assert.Contains("unstaged version", unstaged.SourceText);
        Assert.NotEqual(staged.Fingerprint, unstaged.Fingerprint);
        var commit = f.Commit();
        Assert.True(await f.Service.Poll(default) >= 1);
        var historical = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Commit);
        Assert.Equal(commit, historical.Manifest.CommitIdentity);
        Assert.Equal(r.CommitCheckpoint, historical.Manifest.BaseIdentity);
        Assert.Equal(Freshness.Historical, await f.Service.FreshnessOf(historical, default));
    }

    [Fact]
    public async Task Only_selected_paths_are_captured_and_metadata_mode_stores_no_source()
    {
        using var f = new AgentFixture();
        f.Write("other/b.txt", "other before\n"); f.Commit();
        var r = await f.Register();
        f.Write("src/a.txt", "selected source\n"); f.Write("other/b.txt", "private other source\n");
        Assert.Equal(1, await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default));
        var c = Assert.Single(f.Service.Inbox());
        Assert.Null(c.SourceText);
        Assert.Equal("src/a.txt", Assert.Single(c.Manifest.Files).Path);
    }

    [Fact]
    public async Task Renames_deletions_and_unusual_names_have_exact_paths_and_modes()
    {
        using var f = new AgentFixture();
        f.Write("src/юникод [x];$value.txt", "unusual\n"); f.Commit();
        var r = await f.Register();
        f.Run("mv", "src/a.txt", "src/renamed space.txt");
        File.Delete(Path.Combine(f.RepositoryRoot, "src", "юникод [x];$value.txt")); f.Run("add", "-u");
        await f.Service.Capture(r.Id, SourceKind.Staged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.Contains(c.Manifest.Files, x => x.Path == "src/renamed space.txt" && x.OldPath == "src/a.txt" && x.Status.StartsWith('R'));
        Assert.Contains(c.Manifest.Files, x => x.Path == "src/юникод [x];$value.txt" && x.NewMode == "000000");
    }

    [Fact]
    public async Task Credential_paths_and_ignored_files_are_excluded_and_unsupported_files_are_listed()
    {
        using var f = new AgentFixture();
        f.Write("src/.env", "before secret\n"); f.Bytes("src/binary.bin", [1, 0, 2]);
        f.Write("src/ignored.txt", "before ignored\n"); f.Commit();
        f.Write(".gitignore", "src/ignored.txt\n");
        var r = await f.Register(source: true);
        f.Write("src/.env", "after secret\n"); f.Write("src/ignored.txt", "after ignored\n");
        f.Bytes("src/binary.bin", [3, 0, 4]); f.Write("src/new.txt", "untracked source\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.DoesNotContain(c.Manifest.Files, x => x.Path.EndsWith(".env") || x.Path.EndsWith("ignored.txt"));
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "binary");
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "untracked");
        Assert.False(c.Manifest.Complete); Assert.Null(c.SourceText);
    }

    [Fact]
    public async Task Recognized_credentials_and_oversized_source_are_never_retained()
    {
        using var f = new AgentFixture();
        f.Write("src/large.txt", "small\n"); f.Commit();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", "token=github_pat_123456789012345678901234567890\n");
        f.Write("src/large.txt", new string('x', GitContext.MaximumTextBytes + 1));
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "recognized_credential");
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "oversized");
        Assert.Null(c.SourceText);
    }

    [Fact]
    public async Task File_limit_is_visible_instead_of_silently_truncating_a_complete_capture()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        for (var i = 0; i < 101; i++) f.Write($"src/new-{i}.txt", "untracked\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.False(c.Manifest.Complete);
        Assert.Contains("file_limit_exceeded", c.Manifest.Limitations);
        Assert.Equal(101, c.Manifest.Files.Length);
    }

    [Fact]
    public async Task Combined_patch_budget_marks_partial_source_incomplete()
    {
        using var f = new AgentFixture();
        f.Write("src/a.txt", new string('a', 600000) + "\n");
        f.Write("src/b.txt", new string('a', 600000) + "\n"); f.Commit();
        var r = await f.Register(source: true);
        f.Write("src/a.txt", new string('b', 600000) + "\n");
        f.Write("src/b.txt", new string('b', 600000) + "\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.False(c.Manifest.Complete);
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "text_limit_exceeded");
        Assert.NotNull(c.SourceText);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(c.SourceText) <= GitContext.MaximumTextBytes);
    }

    [Fact]
    public async Task A_real_rebase_creates_new_historical_identities_and_invalidates_working_base()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        f.Run("checkout", "-b", "topic"); f.Write("src/a.txt", "topic change\n"); var original = f.Commit();
        await f.Service.Poll(default);
        f.Write("src/a.txt", "pending working change\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var working = Assert.Single(f.Service.Inbox(), c => c.Manifest.Kind == SourceKind.Unstaged);
        f.Run("stash", "push", "-m", "fixture pending");
        f.Run("checkout", "main"); f.Write("outside.txt", "new base\n"); var newBase = f.Commit();
        f.Run("checkout", "topic"); f.Run("rebase", "main"); var rewritten = f.Run("rev-parse", "HEAD").Trim();
        f.Run("stash", "pop");
        Assert.NotEqual(original, rewritten);
        Assert.Equal(Freshness.Changed, await f.Service.FreshnessOf(working, default));
        await f.Service.Poll(default);
        var history = f.Service.Inbox().Where(c => c.Manifest.Kind == SourceKind.Commit).ToArray();
        Assert.Equal(2, history.Length);
        Assert.Contains(history, c => c.Manifest.CommitIdentity == rewritten && c.Manifest.BaseIdentity == newBase);
    }

    [Fact]
    public async Task Merge_uses_first_parent_and_marks_historical_source()
    {
        using var f = new AgentFixture();
        var r = await f.Register();
        f.Run("checkout", "-b", "topic"); f.Write("src/topic.txt", "topic\n"); f.Commit();
        f.Run("checkout", "main"); f.Write("src/main.txt", "main\n"); var parent = f.Commit();
        f.Run("merge", "--no-ff", "topic", "-m", "fixture merge");
        var merge = f.Run("rev-parse", "HEAD").Trim();
        await f.Service.Capture(r.Id, SourceKind.Commit, merge, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.True(c.Manifest.IsMerge); Assert.Equal(parent, c.Manifest.BaseIdentity);
        Assert.Equal("src/topic.txt", Assert.Single(c.Manifest.Files).Path);
    }

    [Fact]
    public async Task Branch_changes_and_rewritten_commits_are_reconciled_without_duplicate_candidates()
    {
        using var f = new AgentFixture();
        await f.Register();
        f.Write("src/a.txt", "first change\n"); f.Commit(); await f.Service.Poll(default);
        f.Write("src/a.txt", "amended change\n"); f.Run("add", "."); f.Run("commit", "--amend", "-m", "rewrite");
        await f.Service.Poll(default);
        Assert.Equal(2, f.Service.Inbox().Count(c => c.Manifest.Kind == SourceKind.Commit));
        f.Run("checkout", "HEAD~1"); await f.Service.Poll(default);
        f.Run("checkout", "main"); await f.Service.Poll(default);
        Assert.Equal(2, f.Service.Inbox().Count(c => c.Manifest.Kind == SourceKind.Commit));
    }

    [Fact]
    public async Task Linked_worktrees_are_resolved_without_assuming_git_is_a_directory()
    {
        using var f = new AgentFixture();
        var linked = Path.Combine(f.DirectoryRoot, "linked");
        f.Run("worktree", "add", "-b", "linked", linked);
        var r = await f.Service.Register(linked, ["src"], WorkSetting.Coursework, false, default);
        File.WriteAllText(Path.Combine(linked, "src", "a.txt"), "linked change\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        Assert.Single(f.Service.Inbox());
    }

    [Fact]
    public async Task Git_helpers_and_filters_are_disabled_and_environment_cannot_redirect_the_repository()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        // If any helper runs, it fails the read. Filter.required tests fail-closed
        // override as well as disabling the executable filter itself.
        f.Run("config", "diff.external", "nonexistent-sensei-helper");
        f.Run("config", "diff.fixture.textconv", "nonexistent-sensei-helper");
        f.Run("config", "filter.fixture.clean", "nonexistent-sensei-helper");
        f.Run("config", "filter.fixture.process", "nonexistent-sensei-helper");
        f.Run("config", "filter.fixture.required", "true");
        f.Write(".gitattributes", "src/a.txt diff=fixture filter=fixture\n");
        f.Write("src/a.txt", "safe source change\n");
        await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        Assert.Contains("safe source change", Assert.Single(f.Service.Inbox()).SourceText);
    }

    [Fact]
    public async Task Submodule_and_symlink_modes_are_unreviewed_without_dereferencing_source()
    {
        using var f = new AgentFixture();
        var r = await f.Register(source: true);
        var head = f.Run("rev-parse", "HEAD").Trim();
        f.Run("update-index", "--add", "--cacheinfo", $"160000,{head},src/module");
        var linkTarget = f.Run("hash-object", "src/a.txt").Trim();
        f.Run("update-index", "--add", "--cacheinfo", $"120000,{linkTarget},src/link");
        await f.Service.Capture(r.Id, SourceKind.Staged, null, default);
        var c = Assert.Single(f.Service.Inbox());
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "submodule");
        Assert.Contains(c.Manifest.Files, x => x.UnreviewedReason == "symlink_or_reparse_point");
        Assert.Null(c.SourceText);
    }
}
