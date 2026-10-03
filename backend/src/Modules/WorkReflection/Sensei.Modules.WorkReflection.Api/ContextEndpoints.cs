using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Application;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewContextRequest(ContextSource Source, ContextDataScope DataScope = ContextDataScope.Restricted, string? RouteRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveContextRequest(Guid OperationId, ContextSource Source, string PreviewToken,
    ContextDataScope DataScope = ContextDataScope.Restricted, string? RouteRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviseInterpretationRequest(Guid OperationId, InterpretationText Text, Guid? SupersedesId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmContextRequest(Guid OperationId, Guid InterpretationId, string SourceFingerprint);

public sealed record ContextWorkspaceResource(Guid EpisodeId, Guid? CurrentSnapshotId, Guid? AcceptedInterpretationId, bool IsArchived, string VersionToken);
public sealed record SnapshotResource(Guid Id, Guid EpisodeId, int Number, ContextSource Source, string SourceFingerprint,
    string FingerprintVersion, ContextDataScope DataScope, DisclosureManifest Disclosure, string DisclosureDigest, DateTimeOffset CreatedAt);
public sealed record InterpretationResource(Guid Id, Guid SnapshotId, int Number, Guid? SupersedesId, InterpretationOrigin Origin,
    InterpretationText Text, string? ProvenanceJson, DateTimeOffset CreatedAt);
public sealed record ConfirmationResource(Guid Id, Guid SnapshotId, Guid InterpretationId, string SourceFingerprint, DateTimeOffset CreatedAt);
public sealed record ContextMutationResource<T>(T Item, Guid? CurrentSnapshotId, Guid? AcceptedInterpretationId, string VersionToken);

public static class ContextEndpoints
{
    public static IEndpointRouteBuilder MapContextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/work/episodes/{episode:guid}/context").WithTags("Work Context").RequireAuthorization();
        group.MapGet("/", async (Guid episode, ICurrentOwner owner, ContextService service, HttpContext http, CancellationToken ct) =>
        {
            var work = await service.Workspace(owner.Id, episode, ct);
            return HttpContract.OkVersioned(http.Response, new ContextWorkspaceResource(work.Id, work.CurrentSnapshotId,
                work.AcceptedInterpretationId, work.IsArchived, HttpContract.VersionToken(work.Version)), work.Version);
        }).WithName("GetContextWorkspace").Produces<ContextWorkspaceResource>().ProducesProblem(404);

        group.MapPost("/preview", async (Guid episode, ICurrentOwner owner, PreviewContextRequest request, ContextService service, CancellationToken ct) =>
            Results.Ok(await service.Preview(owner.Id, episode, request.Source, request.DataScope, request.RouteRevision, ct)))
            .WithName("PreviewContextDisclosure").Produces<ContextPreview>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapGet("/snapshots", async (Guid episode, ICurrentOwner owner, int? limit, string? cursor, ContextService service, CancellationToken ct) =>
        {
            var items = (await service.Snapshots(owner.Id, episode, ct)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(items, limit, cursor, $"work.snapshots:{owner.Id}:{episode}",
                x => x.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), x => x.Id));
        }).WithName("ListContextSnapshots").Produces<PageEnvelope<SnapshotResource>>().ProducesProblem(400).ProducesProblem(404);

        group.MapGet("/snapshots/{snapshot:guid}", async (Guid episode, Guid snapshot, ICurrentOwner owner, ContextService service, CancellationToken ct) =>
            Results.Ok(Map(await service.Snapshot(owner.Id, episode, snapshot, ct))))
            .WithName("GetContextSnapshot").Produces<SnapshotResource>().ProducesProblem(404);

        group.MapPost("/snapshots", async (Guid episode, ICurrentOwner owner, [FromHeader(Name = "If-Match")] string? ifMatch,
            SaveContextRequest request, ContextService service, HttpContext http, CancellationToken ct) =>
        {
            var result = await service.Save(owner.Id, episode, HttpContract.RequireVersion(ifMatch),
                new(request.OperationId, request.Source, request.DataScope, request.RouteRevision, request.PreviewToken), ct);
            return HttpContract.CreatedVersioned(http.Response, $"/api/v1/work/episodes/{episode}/context/snapshots/{result.Item.Id}",
                Mutation(result, Map(result.Item)), result.EpisodeVersion);
        }).WithName("SaveContextSnapshot").Produces<ContextMutationResource<SnapshotResource>>(201)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);

        group.MapGet("/snapshots/{snapshot:guid}/interpretations", async (Guid episode, Guid snapshot, ICurrentOwner owner, int? limit,
            string? cursor, ContextService service, CancellationToken ct) =>
        {
            var items = (await service.Interpretations(owner.Id, episode, snapshot, ct)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(items, limit, cursor, $"work.interpretations:{owner.Id}:{episode}:{snapshot}",
                x => x.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), x => x.Id));
        }).WithName("ListContextInterpretations").Produces<PageEnvelope<InterpretationResource>>().ProducesProblem(400).ProducesProblem(404);

        group.MapPost("/snapshots/{snapshot:guid}/interpretations", async (Guid episode, Guid snapshot, ICurrentOwner owner,
            [FromHeader(Name = "If-Match")] string? ifMatch, ReviseInterpretationRequest request, ContextService service, HttpContext http, CancellationToken ct) =>
        {
            var result = await service.Revise(owner.Id, episode, snapshot, HttpContract.RequireVersion(ifMatch),
                new(request.OperationId, request.SupersedesId, request.Text), ct);
            return HttpContract.OkVersioned(http.Response, Mutation(result, Map(result.Item)), result.EpisodeVersion);
        }).WithName("ReviseContextInterpretation").Produces<ContextMutationResource<InterpretationResource>>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);

        group.MapGet("/snapshots/{snapshot:guid}/confirmations", async (Guid episode, Guid snapshot, ICurrentOwner owner, int? limit,
            string? cursor, ContextService service, CancellationToken ct) =>
        {
            var items = (await service.Confirmations(owner.Id, episode, snapshot, ct)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(items, limit, cursor, $"work.confirmations:{owner.Id}:{episode}:{snapshot}",
                x => x.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture), x => x.Id));
        }).WithName("ListContextConfirmations").Produces<PageEnvelope<ConfirmationResource>>().ProducesProblem(400).ProducesProblem(404);

        group.MapPost("/snapshots/{snapshot:guid}/confirmation", async (Guid episode, Guid snapshot, ICurrentOwner owner,
            [FromHeader(Name = "If-Match")] string? ifMatch, ConfirmContextRequest request, ContextService service, HttpContext http, CancellationToken ct) =>
        {
            var result = await service.Confirm(owner.Id, episode, snapshot, HttpContract.RequireVersion(ifMatch),
                new(request.OperationId, request.InterpretationId, request.SourceFingerprint), ct);
            return HttpContract.OkVersioned(http.Response, Mutation(result, Map(result.Item)), result.EpisodeVersion);
        }).WithName("ConfirmContextInterpretation").Produces<ContextMutationResource<ConfirmationResource>>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);
        return endpoints;
    }

    private static SnapshotResource Map(ContextSnapshot x) => new(x.Id, x.EpisodeId, x.Number, x.Source(), x.SourceFingerprint,
        x.FingerprintVersion, x.DataScope, JsonSerializer.Deserialize<DisclosureManifest>(x.DisclosureJson)!, x.DisclosureDigest, x.CreatedAt);
    private static InterpretationResource Map(InterpretationRevision x) => new(x.Id, x.SnapshotId, x.Number, x.SupersedesId,
        x.Origin, x.Text(), x.ProvenanceJson, x.CreatedAt);
    private static ConfirmationResource Map(ContextConfirmation x) => new(x.Id, x.SnapshotId, x.InterpretationId, x.SourceFingerprint, x.CreatedAt);
    private static ContextMutationResource<TOut> Mutation<TIn, TOut>(ContextMutation<TIn> result, TOut item) =>
        new(item, result.CurrentSnapshotId, result.AcceptedInterpretationId, HttpContract.VersionToken(result.EpisodeVersion));
}
