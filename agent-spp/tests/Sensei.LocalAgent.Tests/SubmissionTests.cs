using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Sensei.Client.Contracts;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Sync;

namespace Sensei.LocalAgent.Tests;

public sealed class SubmissionTests
{
    private sealed class FakeTransport : IAgentTransport
    {
        public int Posts;
        public bool LoseResponse;
        public bool HideReceipts;
        public TaskCompletionSource? ReceiptGate;
        public TaskCompletionSource ReceiptStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? ImportGate;
        public TaskCompletionSource ImportStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly Dictionary<Guid, ImportReceipt> Receipts = [];
        public Task<Connection> Login(string s, bool i, CancellationToken ct) => throw new NotSupportedException();
        public Task<Connection> Refresh(Connection c, CancellationToken ct) => Task.FromResult(c with { AccessExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        public Task Logout(Connection c, CancellationToken ct) => Task.CompletedTask;
        public Task<AgentConcept[]> Concepts(Connection c, CancellationToken ct) => Task.FromResult(Array.Empty<AgentConcept>());
        public async Task<ImportReceipt?> Receipt(Connection c, Guid id, CancellationToken ct)
        { ReceiptStarted.TrySetResult(); if (ReceiptGate is not null) await ReceiptGate.Task.WaitAsync(ct); return HideReceipts ? null : Receipts.GetValueOrDefault(id); }
        public async Task<ImportReceipt> Import(Connection c, ProfessionalEventImportV1 p, CancellationToken ct)
        {
            Posts++; var receipt = new ImportReceipt(p.EventId, Guid.NewGuid(), 1, [], ImportContract.Digest(p), DateTimeOffset.UtcNow, "/experience"); Receipts[p.EventId] = receipt;
            ImportStarted.TrySetResult(); if (ImportGate is not null) await ImportGate.Task.WaitAsync(ct);
            if (LoseResponse) throw new HttpRequestException("fixture lost response");
            return receipt;
        }
    }
    [Fact]
    public async Task Cancellation_during_delivery_preserves_uncertainty_and_reconciles_the_accepted_receipt()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, _, transport) = Setup(f);
        await service.Confirm(c.Id, (await service.Preview(c.Id, default)).Confirmation, default);
        transport.ImportGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncing = service.Sync(default, (_, _) => { }); await transport.ImportStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        service.Cancel(c.EventId); Assert.True(Assert.Single(service.Outbox()).MayHaveBeenDelivered);
        var confirmation = (await service.Preview(c.Id, default)).Confirmation;
        await Assert.ThrowsAsync<AgentException>(() => service.Confirm(c.Id, confirmation, default));
        transport.ImportGate.SetResult(); await syncing;
        Assert.Equal(DeliveryState.Imported, Assert.Single(service.Outbox()).State); Assert.NotNull(Assert.Single(service.Outbox()).Receipt);
        await service.Sync(default, (_, _) => { }); Assert.Equal(1, transport.Posts);
    }
    [Fact]
    public async Task An_uncertain_item_reconciles_after_source_changes_even_if_an_earlier_receipt_lookup_was_missing()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, _, transport) = Setup(f);
        await service.Confirm(c.Id, (await service.Preview(c.Id, default)).Confirmation, default); transport.LoseResponse = true;
        await service.Sync(default, (_, _) => { }); Assert.Equal(DeliveryState.DeliveryUnknown, Assert.Single(service.Outbox()).State);
        transport.HideReceipts = true; f.Write("src/a.txt", "later changes\n"); await service.Sync(default, (_, _) => { });
        Assert.Equal(DeliveryState.NeedsReview, Assert.Single(service.Outbox()).State); Assert.True(Assert.Single(service.Outbox()).MayHaveBeenDelivered);
        transport.HideReceipts = false; await service.Sync(default, (_, _) => { });
        Assert.Equal(DeliveryState.Imported, Assert.Single(service.Outbox()).State); Assert.Equal(1, transport.Posts);
    }
    private static async Task<EvidenceCandidate> Candidate(AgentFixture f)
    {
        var r = await f.Register(source: true); f.Write("src/a.txt", "reviewed change\n"); await f.Service.Capture(r.Id, SourceKind.Unstaged, null, default);
        var c = Assert.Single(f.Service.Inbox()); f.Service.Edit(c.Id, c.Revision, new("Developer", "Investigated a failure", "Considered another approach", "Outcome unknown", "Not measured", "Peer review", [], "Investigated retry behavior", "A service needed reliable retries"));
        return f.Service.Candidate(c.Id);
    }
    private static (SubmissionService Service, SyncStore Store, FakeTransport Transport) Setup(AgentFixture f)
    {
        var store = new SyncStore(f.Store, f.StateRoot); store.Locked(() => { store.Connection(new("https://localhost/", Guid.NewGuid(), "Fixture", "private-access-token", "private-refresh-token", DateTimeOffset.UtcNow.AddHours(1), false)); return 0; });
        var transport = new FakeTransport(); return (new(f.Service, store, transport), store, transport);
    }
    [Fact]
    public async Task Confirmation_survives_restart_and_lost_response_without_duplicate_delivery()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, store, transport) = Setup(f);
        var preview = await service.Preview(c.Id, default); await service.Confirm(c.Id, preview.Confirmation, default);
        Assert.Equal(0, transport.Posts); transport.LoseResponse = true;
        await service.Sync(default, (_, _) => { }); Assert.Equal(DeliveryState.DeliveryUnknown, Assert.Single(service.Outbox()).State);
        var restarted = new SubmissionService(f.Restart(), new SyncStore(f.Store, f.StateRoot), transport);
        await restarted.Sync(default, (_, _) => { }); Assert.Equal(DeliveryState.Imported, Assert.Single(restarted.Outbox()).State); Assert.Equal(1, transport.Posts);
        foreach (var path in Directory.GetFiles(f.StateRoot, "*.protected"))
        { var bytes = Encoding.UTF8.GetString(File.ReadAllBytes(path)); Assert.DoesNotContain("private-refresh-token", bytes); Assert.DoesNotContain("Investigated a failure", bytes); }
    }
    [Fact]
    public async Task Changed_context_and_changed_notes_block_delivery_but_source_removal_does_not()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, _, transport) = Setup(f);
        await service.Confirm(c.Id, (await service.Preview(c.Id, default)).Confirmation, default);
        f.Service.PurgeSource(c.Id, c.Revision); await service.Sync(default, (_, _) => { }); Assert.Equal(1, transport.Posts);
        using var other = new AgentFixture(); var d = await Candidate(other); var (second, _, secondTransport) = Setup(other);
        await second.Confirm(d.Id, (await second.Preview(d.Id, default)).Confirmation, default); other.Write("src/a.txt", "different change\n");
        await second.Sync(default, (_, _) => { }); Assert.Equal(0, secondTransport.Posts); Assert.Equal(DeliveryState.NeedsReview, Assert.Single(second.Outbox()).State);
    }
    [Fact]
    public async Task Exact_confirmation_account_binding_and_deletion_are_enforced()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, store, transport) = Setup(f);
        var preview = await service.Preview(c.Id, default); f.Service.Edit(c.Id, c.Revision, c.Notes with { Actions = "New reviewed wording" });
        await Assert.ThrowsAsync<AgentException>(() => service.Confirm(c.Id, preview.Confirmation, default));
        await service.Confirm(c.Id, (await service.Preview(c.Id, default)).Confirmation, default);
        var previous = service.Profile()!; store.Locked(() => { store.Connection(previous with { OwnerId = Guid.NewGuid() }); return 0; });
        await service.Sync(default, (_, _) => { }); Assert.Equal(0, transport.Posts);
        store.Locked(() => { store.Connection(previous); return 0; }); var current = f.Service.Candidate(c.Id); f.Service.Delete(c.Id, current.Revision);
        await service.Sync(default, (_, _) => { }); Assert.Equal(DeliveryState.Cancelled, Assert.Single(service.Outbox()).State);
    }
    [Fact]
    public async Task Synchronization_serializes_without_locking_local_review_and_rejects_changed_notes()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, _, transport) = Setup(f);
        await service.Confirm(c.Id, (await service.Preview(c.Id, default)).Confirmation, default);
        transport.ReceiptGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivery = service.Sync(default, (_, _) => { }); await transport.ReceiptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<AgentException>(() => service.Sync(default, (_, _) => { }));
        f.Service.Edit(c.Id, c.Revision, c.Notes with { Outcome = "Newly reviewed wording" }); // State lock is free during HTTP.
        transport.ReceiptGate.SetResult(); await delivery;
        Assert.Equal(0, transport.Posts); Assert.Equal(DeliveryState.NeedsReview, Assert.Single(service.Outbox()).State);
    }
    [Fact]
    public async Task Legacy_notes_survive_versioned_read_and_unreadable_queue_is_not_replaced()
    {
        using var f = new AgentFixture(); var c = await Candidate(f); var (service, _, _) = Setup(f);
        var old = JsonNode.Parse(AgentJson.Bytes(c))!.AsObject(); old.Remove("recordVersion");
        old["notes"]!.AsObject().Remove("title"); old["notes"]!.AsObject().Remove("context");
        f.Store.WithLock(() => { f.Store.WriteDocument(Path.Combine("candidates", c.Id.ToString("N") + ".protected"), old); return 0; });
        var restored = f.Restart().Candidate(c.Id); Assert.Equal(c.Notes.Actions, restored.Notes.Actions); Assert.Equal("", restored.Notes.Title);
        await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(c.Id, default));
        old["recordVersion"] = 99;
        f.Store.WithLock(() => { f.Store.WriteDocument(Path.Combine("candidates", c.Id.ToString("N") + ".protected"), old); return 0; });
        Assert.Equal("state_version_unsupported", Assert.Throws<AgentException>(() => f.Service.Candidate(c.Id)).Code);
        var path = Path.Combine(f.StateRoot, "outbox.protected"); var corrupted = Encoding.UTF8.GetBytes("broken protected fixture"); File.WriteAllBytes(path, corrupted);
        Assert.Throws<AgentException>(() => service.Outbox()); Assert.Equal(corrupted, File.ReadAllBytes(path));
    }
    [Theory]
    [InlineData("{\"eventId\":\"x\",\"eventId\":\"y\"}")]
    [InlineData("{\"repositoryUrl\":\"https://example.test/repo\"}")]
    public void Strict_contract_rejects_duplicate_and_unknown_fields(string json) => Assert.ThrowsAny<Exception>(() => ImportContract.Parse(Encoding.UTF8.GetBytes(json)));
    [Theory]
    [InlineData("password=private-value")]
    [InlineData("diff --git a/file b/file")]
    [InlineData("C:\\private\\source.cs")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    public void Recognized_source_and_credentials_are_blocked(string prose) => Assert.True(ImportContract.UnsafeProse(prose));
}
