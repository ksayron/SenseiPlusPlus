using Sensei.LocalAgent.Core;
using Sensei.Client.Contracts;

namespace Sensei.LocalAgent.Sync;

public sealed record Connection(string Issuer, Guid OwnerId, string DisplayName, string AccessToken,
    string RefreshToken, DateTimeOffset AccessExpiresAt, bool AllowInsecureLoopback);
public enum DeliveryState { Pending, DeliveryUnknown, NeedsReview, Rejected, Imported, Cancelled }
public sealed record Submission(Guid CandidateId, string Fingerprint, string Issuer, Guid OwnerId,
    ProfessionalEventImportV1 Payload, string PayloadDigest, DeliveryState State, DateTimeOffset ConfirmedAt,
    ImportReceipt? Receipt = null, string? Error = null, bool MayHaveBeenDelivered = false);
public sealed record SubmissionPreview(ProfessionalEventImportV1 Payload, string Server, Guid OwnerId,
    string Account, Freshness Freshness, string Confirmation, string Destination = "Experience journal draft · Client reported · Not assessed");

public sealed class SyncStore(FileAgentStore store, string root)
{
    public T Locked<T>(Func<T> action) => store.WithLock(action);
    public void Connection(Connection? connection) => store.WriteDocument("connection.protected", new ConnectionEnvelope(connection));
    public Connection? ReadConnection() => store.ReadDocument<ConnectionEnvelope>("connection.protected")?.Connection;
    public Submission[] ReadOutbox() => store.ReadDocument<Submission[]>("outbox.protected") ?? [];
    public void SaveOutbox(Submission[] items) => store.WriteDocument("outbox.protected", items);
    public IDisposable SyncLock()
    {
        if (!SourcePolicy.SafeFile(root, "sync.lock")) throw new AgentException("storage_reparse_point");
        try { return new FileStream(Path.Combine(root, "sync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new AgentException("sync_already_running"); }
    }
    private sealed record ConnectionEnvelope(Connection? Connection);
}
