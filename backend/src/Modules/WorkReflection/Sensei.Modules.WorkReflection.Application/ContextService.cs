using System.Text.Json;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Application.Models;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Application;

public sealed record DisclosureRecipient(string Kind, string Name, string PolicyUrl, DateTimeOffset? PolicyCheckedAt);
public sealed record DisclosureManifest(int SchemaVersion, string Mode, ContextDataScope DataScope, string SourceFingerprint,
    ContextSource SelectedContent, DisclosureRecipient[] Recipients, ModelRoute? Route, string Notice);
public sealed record ContextPreview(string SourceFingerprint, string FingerprintVersion, DisclosureManifest Manifest, string PreviewToken);
public sealed record SaveContextCommand(Guid OperationId, ContextSource Source, ContextDataScope DataScope, string? RouteRevision, string PreviewToken);
public sealed record ReviseInterpretationCommand(Guid OperationId, Guid? SupersedesId, InterpretationText Text);
public sealed record ConfirmContextCommand(Guid OperationId, Guid InterpretationId, string SourceFingerprint);
public sealed record ContextMutation<T>(T Item, int EpisodeVersion, Guid? CurrentSnapshotId, Guid? AcceptedInterpretationId);

public interface IContextDisclosurePolicy
{
    DisclosureManifest Preview(ContextSource source, ContextDataScope scope, string? routeRevision);
}

/// <summary>No route is installed by default. Optional routes are supplied by trusted host code, never API input.</summary>
public sealed class ContextDisclosurePolicy(ModelRoute? publicRoute = null, TimeProvider? time = null) : IContextDisclosurePolicy
{
    public DisclosureManifest Preview(ContextSource source, ContextDataScope scope, string? routeRevision)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentException("Select a known data classification.");
        var normalized = ContextFingerprint.Normalize(source);
        var fingerprint = ContextFingerprint.Compute(normalized);
        if (routeRevision is null)
            return new(1, "LocalOnly", scope, fingerprint, normalized, [], null,
                "Saved on the local Sensei++ server. No external recipients and no provider call. A later external request requires a new disclosure review.");
        var now = (time ?? TimeProvider.System).GetUtcNow();
        if (publicRoute is null || routeRevision != publicRoute.Revision || scope != ContextDataScope.PublicSynthetic ||
            !publicRoute.ModelId.EndsWith(":free", StringComparison.Ordinal) || publicRoute.PrivacyPolicy is null ||
            publicRoute.PrivacyPolicy.Scheme != "https" || publicRoute.PrivacyReviewedAt > now || now - publicRoute.PrivacyReviewedAt > TimeSpan.FromDays(30))
            throw new InvalidOperationException("No eligible public-example route is available for this disclosure preview. Keep the context local.");
        return new(1, "PublicProviderPreview", scope, fingerprint, normalized,
            [new("Router", "OpenRouter", "https://openrouter.ai/privacy", null),
             new("UpstreamProvider", publicRoute.ProviderName, publicRoute.PrivacyPolicy.AbsoluteUri, publicRoute.PrivacyReviewedAt)], publicRoute,
            "Preview only: these selected fields would cross both listed boundaries. This snapshot does not authorize inference. A job must preview its complete prompt, recheck current price/privacy/capability, and obtain explicit acceptance before sending.");
    }
}

public interface IContextRepository
{
    Task<ContextSnapshot?> Snapshot(Guid owner, Guid episode, Guid id, CancellationToken ct);
    Task<ContextSnapshot?> SnapshotOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct);
    Task<IReadOnlyCollection<ContextSnapshot>> Snapshots(Guid owner, Guid episode, CancellationToken ct);
    Task<InterpretationRevision?> Interpretation(Guid owner, Guid episode, Guid snapshot, Guid id, CancellationToken ct);
    Task<InterpretationRevision?> InterpretationOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct);
    Task<IReadOnlyCollection<InterpretationRevision>> Interpretations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct);
    Task<ContextConfirmation?> ConfirmationOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct);
    Task<IReadOnlyCollection<ContextConfirmation>> Confirmations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct);
    void Add(ContextSnapshot snapshot);
    void Add(InterpretationRevision revision);
    void Add(ContextConfirmation confirmation);
}

public sealed class ContextService(IWorkEpisodeRepository episodes, IContextRepository repository,
    ITransactionRunner transactions, IContextDisclosurePolicy policy, TimeProvider clock)
{
    public async Task<WorkEpisode> Workspace(Guid owner, Guid episode, CancellationToken ct) =>
        await episodes.GetAsync(owner, episode, ct) ?? throw new ResourceNotFoundException();

    public async Task<ContextPreview> Preview(Guid owner, Guid episode, ContextSource source, ContextDataScope scope, string? route, CancellationToken ct)
    {
        var work = await Workspace(owner, episode, ct);
        if (work.IsArchived) throw new InvalidOperationException("Archived episodes cannot receive context.");
        return MakePreview(source, scope, route);
    }

    private ContextPreview MakePreview(ContextSource source, ContextDataScope scope, string? route)
    {
        var manifest = policy.Preview(source, scope, route);
        var token = ContextFingerprint.HashText(JsonSerializer.Serialize(manifest));
        return new(manifest.SourceFingerprint, "sensei.context-source.v1", manifest, token);
    }

    public Task<ContextMutation<ContextSnapshot>> Save(Guid owner, Guid episode, int expectedVersion, SaveContextCommand command, CancellationToken ct) =>
        transactions.RunAsync(async () =>
        {
            await transactions.LockAsync($"work-context:{owner}:{episode}", ct);
            var work = await Workspace(owner, episode, ct);
            var source = ContextFingerprint.Normalize(command.Source);
            var digest = Digest(new { source, command.DataScope, command.RouteRevision, command.PreviewToken });
            var previous = await repository.SnapshotOperation(owner, episode, command.OperationId, ct);
            if (previous is not null) { Same(previous.RequestDigest, digest); return Mutation(work, previous); }
            VersionPrecondition.RequireCurrent(work.Version, expectedVersion);
            var preview = MakePreview(source, command.DataScope, command.RouteRevision);
            if (preview.PreviewToken != command.PreviewToken) throw new InvalidOperationException("The disclosure preview changed. Review it again before saving.");
            var snapshot = ContextSnapshot.Create(owner, episode, work.ContextNumber + 1, command.OperationId, digest,
                source, command.DataScope, JsonSerializer.Serialize(preview.Manifest), clock.GetUtcNow());
            work.SelectSnapshot(snapshot.Id, clock.GetUtcNow());
            repository.Add(snapshot);
            return Mutation(work, snapshot);
        }, ct);

    public async Task<ContextSnapshot> Snapshot(Guid owner, Guid episode, Guid id, CancellationToken ct) =>
        await repository.Snapshot(owner, episode, id, ct) ?? throw new ResourceNotFoundException();

    public async Task<IReadOnlyCollection<ContextSnapshot>> Snapshots(Guid owner, Guid episode, CancellationToken ct)
    {
        _ = await Workspace(owner, episode, ct);
        return await repository.Snapshots(owner, episode, ct);
    }
    public async Task<IReadOnlyCollection<InterpretationRevision>> Interpretations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct)
    {
        _ = await Snapshot(owner, episode, snapshot, ct);
        return await repository.Interpretations(owner, episode, snapshot, ct);
    }
    public async Task<IReadOnlyCollection<ContextConfirmation>> Confirmations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct)
    {
        _ = await Snapshot(owner, episode, snapshot, ct);
        return await repository.Confirmations(owner, episode, snapshot, ct);
    }

    public Task<ContextMutation<InterpretationRevision>> Revise(Guid owner, Guid episode, Guid snapshot, int expectedVersion,
        ReviseInterpretationCommand command, CancellationToken ct) => transactions.RunAsync(async () =>
        {
            await transactions.LockAsync($"work-context:{owner}:{episode}", ct);
            var work = await Workspace(owner, episode, ct);
            _ = await Snapshot(owner, episode, snapshot, ct);
            var digest = Digest(new { snapshot, command.SupersedesId, command.Text });
            var previous = await repository.InterpretationOperation(owner, episode, command.OperationId, ct);
            if (previous is not null) { Same(previous.RequestDigest, digest); return Mutation(work, previous); }
            VersionPrecondition.RequireCurrent(work.Version, expectedVersion);
            if (command.SupersedesId is { } prior && await repository.Interpretation(owner, episode, snapshot, prior, ct) is null)
                throw new ResourceNotFoundException();
            var revisions = await repository.Interpretations(owner, episode, snapshot, ct);
            var revision = InterpretationRevision.Create(owner, episode, snapshot, revisions.Count + 1,
                command.SupersedesId, command.OperationId, digest, command.Text, InterpretationOrigin.User, null, clock.GetUtcNow());
            // A correction does not confirm itself and leaves historical confirmations inspectable.
            work.ChangeInterpretation(snapshot, null, clock.GetUtcNow());
            repository.Add(revision);
            return Mutation(work, revision);
        }, ct);

    public Task<ContextMutation<ContextConfirmation>> Confirm(Guid owner, Guid episode, Guid snapshot, int expectedVersion,
        ConfirmContextCommand command, CancellationToken ct) => transactions.RunAsync(async () =>
        {
            await transactions.LockAsync($"work-context:{owner}:{episode}", ct);
            var work = await Workspace(owner, episode, ct);
            var source = await Snapshot(owner, episode, snapshot, ct);
            var digest = Digest(new { snapshot, command.InterpretationId, command.SourceFingerprint });
            var previous = await repository.ConfirmationOperation(owner, episode, command.OperationId, ct);
            if (previous is not null) { Same(previous.RequestDigest, digest); return Mutation(work, previous); }
            VersionPrecondition.RequireCurrent(work.Version, expectedVersion);
            var revision = await repository.Interpretation(owner, episode, snapshot, command.InterpretationId, ct)
                ?? throw new ResourceNotFoundException();
            if (source.SourceFingerprint != command.SourceFingerprint) throw new InvalidOperationException("The confirmed source fingerprint does not match.");
            work.ChangeInterpretation(snapshot, revision.Id, clock.GetUtcNow());
            var confirmation = ContextConfirmation.Create(source, revision, command.OperationId, digest, clock.GetUtcNow());
            repository.Add(confirmation);
            return Mutation(work, confirmation);
        }, ct);

    private static string Digest(object value) => ContextFingerprint.HashText(JsonSerializer.Serialize(value));
    private static void Same(string original, string proposed)
    { if (original != proposed) throw new InvalidOperationException("Operation ID was already used with different content."); }
    private static ContextMutation<T> Mutation<T>(WorkEpisode work, T item) =>
        new(item, work.Version, work.CurrentSnapshotId, work.AcceptedInterpretationId);
}
