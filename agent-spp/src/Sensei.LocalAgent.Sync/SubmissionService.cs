using System.Net;
using Sensei.Client.Contracts;
using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Sync;

public sealed class SubmissionService(AgentService agent, SyncStore store, IAgentTransport transport)
{
    public Connection? Profile() => store.Locked(store.ReadConnection);
    public Submission[] Outbox() => store.Locked(store.ReadOutbox);
    public async Task Login(string server, bool insecure, CancellationToken ct)
    {
        using var held = store.SyncLock();
        var connection = await transport.Login(server, insecure, ct);
        store.Locked(() => { store.Connection(connection); return 0; });
    }
    public async Task<bool> Logout(CancellationToken ct)
    {
        using var held = store.SyncLock(); var connection = Profile();
        store.Locked(() => { store.Connection(null); return 0; });
        if (connection is null) return true;
        try { await transport.Logout(connection, ct); return true; }
        catch (Exception ex) when (ex is HttpRequestException or TransportException or OperationCanceledException) { return false; }
    }
    public static ProfessionalEventImportV1 Payload(EvidenceCandidate c) => new(c.EventId, 1, c.Setting.ToString(), DateOnly.FromDateTime(c.CapturedAt.UtcDateTime),
        c.Notes.Title, c.Notes.Context, c.Notes.Role, c.Notes.Actions, c.Notes.Alternatives, c.Notes.Outcome, c.Notes.Uncertainty, c.Notes.Assistance, c.Notes.ConceptIds.Order().ToArray(), "client_reported");
    public async Task<SubmissionPreview> Preview(Guid id, CancellationToken ct)
    {
        var connection = Profile() ?? throw new AgentException("sign_in_required");
        var c = agent.Candidate(id); var payload = Payload(c); ImportContract.Validate(payload);
        if (c.Status != CandidateStatus.Pending) throw new AgentException("candidate_dismissed");
        if (!c.Manifest.Complete) throw new AgentException("candidate_incomplete");
        var freshness = await agent.FreshnessOf(c, ct);
        if (freshness is not (Freshness.Current or Freshness.Historical)) throw new AgentException("working_context_requires_recapture");
        var confirmation = AgentJson.Digest(new { c.Id, c.Revision, c.Fingerprint, connection.Issuer, connection.OwnerId, Payload = payload });
        return new(payload, connection.Issuer, connection.OwnerId, connection.DisplayName, freshness, confirmation);
    }
    public async Task Confirm(Guid id, string token, CancellationToken ct)
    {
        var preview = await Preview(id, ct);
        if (preview.Confirmation != token) throw new AgentException("submission_revision_changed");
        store.Locked(() =>
        {
            var connection = store.ReadConnection() ?? throw new AgentException("sign_in_required");
            var c = agent.CandidateWithoutLock(id);
            var current = AgentJson.Digest(new { c.Id, c.Revision, c.Fingerprint, connection.Issuer, connection.OwnerId, Payload = Payload(c) });
            if (current != token) throw new AgentException("submission_revision_changed");
            var items = store.ReadOutbox();
            var existing = items.SingleOrDefault(x => x.Issuer == connection.Issuer && x.OwnerId == connection.OwnerId && x.Payload.EventId == c.EventId);
            if (existing?.State is DeliveryState.Imported or DeliveryState.DeliveryUnknown || existing?.MayHaveBeenDelivered == true) throw new AgentException("submission_immutable_or_delivery_unknown");
            var replacement = new Submission(id, c.Fingerprint, connection.Issuer, connection.OwnerId, preview.Payload,
                ImportContract.Digest(preview.Payload), DeliveryState.Pending, DateTimeOffset.UtcNow);
            store.SaveOutbox(items.Where(x => x != existing).Append(replacement).ToArray()); return 0;
        });
    }
    public void Cancel(Guid eventId) => store.Locked(() =>
    {
        var connection = store.ReadConnection() ?? throw new AgentException("sign_in_required");
        var items = store.ReadOutbox(); var item = items.SingleOrDefault(x => x.Issuer == connection.Issuer && x.OwnerId == connection.OwnerId && x.Payload.EventId == eventId) ?? throw new AgentException("submission_not_found");
        if (item.State == DeliveryState.Imported) throw new AgentException("already_imported_use_web_lifecycle");
        store.SaveOutbox(items.Select(x => x == item ? x with { State = DeliveryState.Cancelled, MayHaveBeenDelivered = x.MayHaveBeenDelivered || x.State == DeliveryState.DeliveryUnknown } : x).ToArray()); return 0;
    });
    public void CancelCandidate(Guid id) => store.Locked(() =>
    {
        store.SaveOutbox(store.ReadOutbox().Select(x => x.CandidateId == id && x.State != DeliveryState.Imported ? x with { State = DeliveryState.Cancelled, MayHaveBeenDelivered = x.MayHaveBeenDelivered || x.State == DeliveryState.DeliveryUnknown } : x).ToArray()); return 0;
    });
    private async Task<Connection> Active(CancellationToken ct)
    {
        var current = Profile() ?? throw new AgentException("sign_in_required");
        if (current.AccessExpiresAt > DateTimeOffset.UtcNow) return current;
        var updated = await transport.Refresh(current, ct);
        if (updated.Issuer != current.Issuer || updated.OwnerId != current.OwnerId) throw new AgentException("account_changed");
        store.Locked(() => { if (store.ReadConnection() != current) throw new AgentException("account_changed"); store.Connection(updated); return 0; });
        return updated;
    }
    public async Task<AgentConcept[]> Concepts(CancellationToken ct)
    { using var held = store.SyncLock(); return await transport.Concepts(await Active(ct), ct); }
    public async Task Sync(CancellationToken ct, Action<Guid, string> progress)
    {
        using var held = store.SyncLock(); var connection = await Active(ct);
        var outbox = Outbox();
        foreach (var other in outbox.Where(x => (x.Issuer != connection.Issuer || x.OwnerId != connection.OwnerId) && x.State is DeliveryState.Pending or DeliveryState.DeliveryUnknown))
            progress(other.Payload.EventId, "original_destination_sign_in_required");
        foreach (var snapshot in outbox.Where(x => x.Issuer == connection.Issuer && x.OwnerId == connection.OwnerId &&
            (x.State is DeliveryState.Pending or DeliveryState.DeliveryUnknown or DeliveryState.Cancelled || x.MayHaveBeenDelivered && x.State is DeliveryState.NeedsReview or DeliveryState.Rejected)))
        {
            ct.ThrowIfCancellationRequested(); var item = snapshot; var posting = false;
            try
            {
                // Receipt lookup happens before source checks: an accepted historical submission remains accepted.
                var receipt = await transport.Receipt(connection, item.Payload.EventId, ct);
                if (receipt is not null)
                {
                    if (receipt.EventId != item.Payload.EventId || receipt.PayloadDigest != item.PayloadDigest) throw new TransportException("receipt_payload_conflict", HttpStatusCode.Conflict);
                    progress(item.Payload.EventId, AcceptReceipt(item, receipt) ? "imported" : "queue_changed_sync_again"); continue;
                }
                if (item.State is DeliveryState.Cancelled or DeliveryState.NeedsReview or DeliveryState.Rejected) continue;
                EvidenceCandidate candidate;
                try { candidate = agent.Candidate(item.CandidateId); }
                catch (AgentException ex) when (ex.Code == "candidate_not_found") { Update(item, item with { State = DeliveryState.Cancelled }); continue; }
                var freshness = await agent.FreshnessOf(candidate, ct);
                if (candidate.Status != CandidateStatus.Pending || !candidate.Manifest.Complete || candidate.Fingerprint != item.Fingerprint ||
                    ImportContract.Digest(Payload(candidate)) != item.PayloadDigest || freshness is not (Freshness.Current or Freshness.Historical))
                { Update(item, item with { State = DeliveryState.NeedsReview, Error = "working_context_or_notes_changed" }); progress(item.Payload.EventId, "needs_review"); continue; }
                var sending = item with { State = DeliveryState.DeliveryUnknown, Error = null, MayHaveBeenDelivered = true };
                if (!Update(item, sending, requireReviewedCandidate: true)) { progress(item.Payload.EventId, "needs_review_or_cancelled"); continue; } item = sending; posting = true;
                receipt = await transport.Import(connection, item.Payload, ct);
                if (receipt.EventId != item.Payload.EventId || receipt.PayloadDigest != item.PayloadDigest) throw new TransportException("invalid_receipt");
                progress(item.Payload.EventId, AcceptReceipt(item, receipt) ? "imported" : "queue_changed_sync_again");
            }
            catch (TransportException ex)
            {
                var state = ex.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.RequestEntityTooLarge ? DeliveryState.Rejected : item.State;
                if (ex.Status == HttpStatusCode.TooManyRequests) state = DeliveryState.Pending;
                var definitivelyRejected = posting && ex.Status is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.TooManyRequests;
                Update(item, item with { State = state, Error = ex.Code, MayHaveBeenDelivered = !definitivelyRejected && (item.MayHaveBeenDelivered || ex.Status == HttpStatusCode.Conflict) }); progress(item.Payload.EventId, ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "sign_in_required" : "delivery_deferred");
                if (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) break;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            { progress(item.Payload.EventId, "delivery_deferred"); if (ct.IsCancellationRequested) throw; break; }
        }
    }
    private bool AcceptReceipt(Submission expected, ImportReceipt receipt) => store.Locked(() =>
    {
        var items = store.ReadOutbox(); var index = Array.FindIndex(items, x => x.Issuer == expected.Issuer && x.OwnerId == expected.OwnerId &&
            x.Payload.EventId == expected.Payload.EventId && x.PayloadDigest == expected.PayloadDigest && x.ConfirmedAt == expected.ConfirmedAt);
        if (index < 0) return false;
        // Cancellation stops new delivery; it cannot undo an already accepted server transaction.
        items[index] = items[index] with { State = DeliveryState.Imported, Receipt = receipt, Error = null, MayHaveBeenDelivered = true };
        store.SaveOutbox(items); return true;
    });
    private bool Update(Submission expected, Submission value, bool requireReviewedCandidate = false) => store.Locked(() =>
    {
        var items = store.ReadOutbox(); var index = Array.FindIndex(items, x => x.Issuer == expected.Issuer && x.OwnerId == expected.OwnerId &&
            x.Payload.EventId == expected.Payload.EventId && x.State == expected.State && x.PayloadDigest == expected.PayloadDigest && x.ConfirmedAt == expected.ConfirmedAt);
        if (index < 0) return false;
        if (requireReviewedCandidate)
        {
            var profile = store.ReadConnection();
            EvidenceCandidate candidate;
            try { candidate = agent.CandidateWithoutLock(expected.CandidateId); }
            catch (AgentException ex) when (ex.Code == "candidate_not_found")
            { items[index] = expected with { State = DeliveryState.Cancelled }; store.SaveOutbox(items); return false; }
            if (profile?.Issuer != expected.Issuer || profile.OwnerId != expected.OwnerId || candidate.Status != CandidateStatus.Pending || !candidate.Manifest.Complete ||
                candidate.Fingerprint != expected.Fingerprint || ImportContract.Digest(Payload(candidate)) != expected.PayloadDigest)
            {
                items[index] = expected with { State = DeliveryState.NeedsReview, Error = "working_context_or_notes_changed" };
                store.SaveOutbox(items); return false;
            }
        }
        items[index] = value; store.SaveOutbox(items); return true;
    });
}
