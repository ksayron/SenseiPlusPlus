using System.Text;

namespace Sensei.LocalAgent.Core;

public sealed class AgentService(IAgentStore store, IGitContext git, TimeProvider? time = null)
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly Dictionary<(Guid Repository, SourceKind Kind), string> previousWorking = [];

    public RepositoryRegistration[] Repositories() => store.WithLock(store.Repositories);
    public EvidenceCandidate[] Inbox() => store.WithLock(store.Candidates);
    public EvidenceCandidate Candidate(Guid id) => store.WithLock(() => store.Candidate(id));
    public bool WatcherRunning => store.WatcherRunning;
    public IDisposable AcquireWatcher() => store.WatcherLock();

    public async Task<RepositoryRegistration> Register(string root, string[] paths, WorkSetting setting, bool retainSource, CancellationToken ct)
    {
        paths = SourcePolicy.Selection(paths);
        root = await git.ResolveRoot(root, ct);
        var head = await git.Head(root, ct);
        var registration = new RepositoryRegistration(Guid.NewGuid(), root, paths, setting, retainSource, head, clock.GetUtcNow());
        return store.WithLock(() =>
        {
            var all = store.Repositories();
            if (all.Any(r => r.Root.Equals(root, StringComparison.OrdinalIgnoreCase))) throw new AgentException("repository_already_registered");
            store.SaveRepositories(all.Append(registration).ToArray());
            return registration;
        });
    }

    public void Configure(Guid id, string[]? selection, bool? retainSource, WorkSetting? setting, bool resetBaseline = false, string? newHead = null)
    {
        store.WithLock(() =>
        {
            var all = store.Repositories();
            var r = all.SingleOrDefault(r => r.Id == id) ?? throw new AgentException("repository_not_found");
            var changed = r with { Selection = selection is null ? r.Selection : SourcePolicy.Selection(selection),
                RetainSource = retainSource ?? r.RetainSource, Setting = setting ?? r.Setting,
                CommitCheckpoint = resetBaseline ? newHead : r.CommitCheckpoint };
            store.SaveRepositories(all.Select(x => x.Id == id ? changed : x).ToArray());
            return 0;
        });
    }

    public void Remove(Guid id) => store.WithLock(() =>
    {
        var all = store.Repositories();
        if (!all.Any(r => r.Id == id)) throw new AgentException("repository_not_found");
        store.SaveRepositories(all.Where(r => r.Id != id).ToArray());
        return 0;
    });

    public async Task<int> Poll(CancellationToken ct, Action<Guid, string>? report = null)
    {
        PurgeExpired();
        var count = 0;
        foreach (var initial in Repositories())
        {
            try
            {
                var r = initial;
                foreach (var commit in await git.NewCommits(r, ct))
                {
                    var captured = await git.Capture(r, SourceKind.Commit, commit, ct);
                    count += SaveCapture(r, captured, commit);
                    r = r with { CommitCheckpoint = commit };
                }
                // Never advance to a freshly read HEAD: another commit could have
                // arrived during reconciliation and must remain for the next poll.
                foreach (var kind in new[] { SourceKind.Staged, SourceKind.Unstaged })
                {
                    var observation = await git.Capture(r, kind, null, ct);
                    var fingerprint = observation.Manifest.Fingerprint;
                    var key = (r.Id, kind);
                    if (previousWorking.TryGetValue(key, out var previous) && previous == fingerprint && observation.HasChanges)
                    {
                        var verified = await git.Capture(r, kind, null, ct);
                        if (verified.Manifest.Fingerprint != fingerprint || verified.SourceText != observation.SourceText)
                        { previousWorking.Remove(key); throw new AgentException("source_changed_during_capture"); }
                        count += SaveCapture(r, verified);
                    }
                    previousWorking[key] = fingerprint;
                }
                report?.Invoke(r.Id, "poll_complete");
            }
            catch (AgentException ex)
            {
                previousWorking.Remove((initial.Id, SourceKind.Staged));
                previousWorking.Remove((initial.Id, SourceKind.Unstaged));
                report?.Invoke(initial.Id, ex.Code);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { report?.Invoke(initial.Id, "repository_unavailable"); }
        }
        return count;
    }

    public async Task<int> Capture(Guid repository, SourceKind kind, string? commit, CancellationToken ct)
    {
        var r = Repositories().SingleOrDefault(r => r.Id == repository) ?? throw new AgentException("repository_not_found");
        var observation = await git.Capture(r, kind, commit, ct);
        if (kind != SourceKind.Commit)
        {
            var verified = await git.Capture(r, kind, null, ct);
            if (observation.Manifest.Fingerprint != verified.Manifest.Fingerprint || observation.SourceText != verified.SourceText)
                throw new AgentException("source_changed_during_capture");
        }
        return SaveCapture(r, observation);
    }

    private int SaveCapture(RepositoryRegistration expected, CaptureResult capture, string? checkpoint = null) => store.WithLock(() =>
    {
        var all = store.Repositories();
        var current = all.SingleOrDefault(r => r.Id == expected.Id) ?? throw new AgentException("registration_changed");
        if (AgentJson.Digest(current) != AgentJson.Digest(expected)) throw new AgentException("registration_changed");
        var count = 0;
        if (capture.HasChanges)
        {
            var fingerprint = capture.Manifest.Fingerprint;
            var identity = SourcePolicy.Hash(Encoding.UTF8.GetBytes(expected.Id.ToString("N") + ":" + fingerprint));
            var id = new Guid(Convert.FromHexString(identity[..32]));
            if (!store.IsDeleted(id) && !store.Candidates().Any(c => c.Id == id))
            {
                store.SaveCandidate(new(id, id, current.Id, current.Setting, clock.GetUtcNow(), capture.Manifest,
                    fingerprint, current.RetainSource ? capture.SourceText : null, ContributionNotes.Empty, 1, CandidateStatus.Pending));
                count = 1;
            }
        }
        if (checkpoint is not null)
            store.SaveRepositories(all.Select(r => r.Id == current.Id ? r with { CommitCheckpoint = checkpoint } : r).ToArray());
        return count;
    });

    public EvidenceCandidate Edit(Guid id, int expectedRevision, ContributionNotes notes) => Change(id, expectedRevision, c => c with { Notes = Validate(notes) });
    public EvidenceCandidate Dismiss(Guid id, int expectedRevision) => Change(id, expectedRevision, c => c with { Status = CandidateStatus.Dismissed });
    public EvidenceCandidate PurgeSource(Guid id, int expectedRevision) => Change(id, expectedRevision, c => c with { SourceText = null });
    public void Delete(Guid id, int expectedRevision) => store.WithLock(() =>
    {
        RequireRevision(store.Candidate(id), expectedRevision);
        store.DeleteCandidate(id);
        return 0;
    });
    public int PurgeExpired() => store.WithLock(() =>
    {
        var count = 0;
        foreach (var c in store.Candidates().Where(c => c.SourceText is not null && c.CapturedAt.AddDays(30) <= clock.GetUtcNow()))
        {
            store.SaveCandidate(c with { SourceText = null, Revision = c.Revision + 1 });
            count++;
        }
        return count;
    });

    private EvidenceCandidate Change(Guid id, int revision, Func<EvidenceCandidate, EvidenceCandidate> change) => store.WithLock(() =>
    {
        var c = store.Candidate(id);
        RequireRevision(c, revision);
        var updated = change(c) with { Revision = c.Revision + 1 };
        store.SaveCandidate(updated);
        return updated;
    });

    public async Task<Freshness> FreshnessOf(EvidenceCandidate c, CancellationToken ct)
    {
        if (c.Manifest.Kind == SourceKind.Commit) return Freshness.Historical;
        var r = Repositories().SingleOrDefault(r => r.Id == c.RepositoryId);
        if (r is null) return Freshness.Unavailable;
        if (!r.Selection.SequenceEqual(c.Manifest.Selection)) return Freshness.Changed;
        try
        {
            // Retention settings do not affect fingerprint verification.
            var fresh = await git.Capture(r with { RetainSource = false }, c.Manifest.Kind, null, ct);
            if (fresh.Manifest.Fingerprint != c.Fingerprint) return Freshness.Changed;
            if (!fresh.Manifest.Complete) return Freshness.Incomplete;
            var verified = await git.Capture(r with { RetainSource = false }, c.Manifest.Kind, null, ct);
            return fresh.Manifest.Fingerprint == verified.Manifest.Fingerprint ? Freshness.Current : Freshness.Changed;
        }
        catch (Exception ex) when (ex is AgentException or IOException or UnauthorizedAccessException) { return Freshness.Unavailable; }
    }

    public async Task<ExportPreview> Preview(Guid id, CancellationToken ct)
    {
        var c = Candidate(id);
        if (c.Status != CandidateStatus.Pending) throw new AgentException("candidate_dismissed");
        if (await FreshnessOf(c, ct) is Freshness.Changed or Freshness.Unavailable or Freshness.Incomplete) throw new AgentException("working_context_requires_recapture");
        var n = Validate(c.Notes);
        if (string.IsNullOrWhiteSpace(n.Role) || string.IsNullOrWhiteSpace(n.Actions) || string.IsNullOrWhiteSpace(n.Assistance)) throw new AgentException("role_actions_assistance_required");
        var draft = new SanitizedEventDraftV1(c.EventId, 1, c.Setting, DateOnly.FromDateTime(c.CapturedAt.UtcDateTime),
            n.Role, n.Actions, n.Alternatives, n.Outcome, n.Uncertainty, n.Assistance, n.ConceptIds, "client_reported");
        if (SecretGuard.Contains(Encoding.UTF8.GetString(AgentJson.Bytes(draft)))) throw new AgentException("export_recognized_credential");
        return new(draft, c.Revision, AgentJson.Digest(new { c.Id, c.Revision, c.Fingerprint, Draft = draft }));
    }

    public async Task<byte[]> ConfirmExport(Guid id, string confirmation, CancellationToken ct)
    {
        var preview = await Preview(id, ct);
        if (!preview.Confirmation.Equals(confirmation, StringComparison.Ordinal)) throw new AgentException("export_revision_changed");
        return store.WithLock(() =>
        {
            RequireRevision(store.Candidate(id), preview.Revision);
            return AgentJson.Bytes(preview.Draft);
        });
    }

    public EvidenceCandidate CandidateWithoutLock(Guid id) => store.Candidate(id);

    private static void RequireRevision(EvidenceCandidate c, int revision)
    { if (c.Revision != revision) throw new AgentException("candidate_revision_changed"); }
    private static ContributionNotes Validate(ContributionNotes n)
    {
        var text = new[] { n.Role, n.Actions, n.Alternatives, n.Outcome, n.Uncertainty, n.Assistance, n.Context };
        if (n.Title is null || n.Title.Length > 240 || text.Any(s => s is null || s.Length > 4096) || n.ConceptIds is null || n.ConceptIds.Length > 100 || n.ConceptIds.Contains(Guid.Empty)) throw new AgentException("invalid_contribution_notes");
        return n with { ConceptIds = n.ConceptIds.Distinct().Order().ToArray() };
    }
}
