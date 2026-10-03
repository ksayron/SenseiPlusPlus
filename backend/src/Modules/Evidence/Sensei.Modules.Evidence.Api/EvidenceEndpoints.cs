using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;

namespace Sensei.Modules.Evidence.Api;

public sealed record CreateEvidenceObservationRequest(
    Guid ConceptId, string Aspect, EvidenceSignalKind SignalKind, EvidenceSourceKind SourceKind,
    Guid SourceId, AssistanceLevel Assistance, string Conditions);
public sealed record UpdateEvidenceStatusRequest(EvidenceStatus Status);
public sealed record EvidenceObservationResource(
    Guid Id, Guid OwnerId, Guid ConceptId, string Aspect, EvidenceSignalKind SignalKind,
    EvidenceSourceKind SourceKind, Guid SourceId, AssistanceLevel Assistance, string Conditions,
    EvidenceStatus Status, DateTimeOffset ObservedAt, string VersionToken);

public static class EvidenceEndpoints
{
    private const string EndpointId = "evidence.observations";

    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/evidence/observations").WithTags("Evidence and Profile").RequireAuthorization();

        group.MapPost("/", async (
            ICurrentOwner currentOwner, CreateEvidenceObservationRequest request,
            HttpContext context, IEvidenceObservationService service, CancellationToken cancellationToken) =>
        {
            var observation = await service.CreateAsync(currentOwner.Id, new CreateEvidenceObservationCommand(
                request.ConceptId, request.Aspect, request.SignalKind, request.SourceKind,
                request.SourceId, request.Assistance, request.Conditions), cancellationToken);
            return HttpContract.CreatedVersioned(context.Response, $"/api/v1/evidence/observations/{observation.Id}", Map(observation), observation.Version);
        })
        .WithName("CreateEvidenceObservation")
        .Produces<EvidenceObservationResource>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
            ICurrentOwner currentOwner, Guid? conceptId, bool? includeInactive,
            int? limit, string? cursor, IEvidenceObservationService service, CancellationToken cancellationToken) =>
        {
            var observations = (await service.ListAsync(currentOwner.Id, conceptId, includeInactive ?? false, cancellationToken))
                .Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(observations, limit, cursor,
                $"{EndpointId}:{currentOwner.Id}:{conceptId}:{includeInactive ?? false}",
                observation => observation.ObservedAt.ToString("O"), observation => observation.Id));
        })
        .WithName("ListEvidenceObservations")
        .Produces<PageEnvelope<EvidenceObservationResource>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            Guid id, ICurrentOwner currentOwner, HttpContext context,
            IEvidenceObservationService service, CancellationToken cancellationToken) =>
        {
            var observation = await service.GetAsync(currentOwner.Id, id, cancellationToken);
            return observation is null
                ? HttpContract.NotFound(context)
                : HttpContract.OkVersioned(context.Response, Map(observation), observation.Version);
        })
        .WithName("GetEvidenceObservation")
        .Produces<EvidenceObservationResource>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/status", async (
            Guid id, ICurrentOwner currentOwner,
            [FromHeader(Name = "If-Match")] string? ifMatch, UpdateEvidenceStatusRequest request,
            HttpContext context, IEvidenceObservationService service, CancellationToken cancellationToken) =>
        {
            var observation = await service.ChangeStatusAsync(currentOwner.Id, id,
                new UpdateEvidenceStatusCommand(request.Status, HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return observation is null
                ? HttpContract.NotFound(context)
                : HttpContract.OkVersioned(context.Response, Map(observation), observation.Version);
        })
        .WithName("ChangeEvidenceStatus")
        .Produces<EvidenceObservationResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", async (
            Guid id, ICurrentOwner currentOwner,
            [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IEvidenceObservationService service, CancellationToken cancellationToken) =>
            await service.WithdrawAsync(currentOwner.Id, id, HttpContract.RequireVersion(ifMatch), cancellationToken)
                ? Results.NoContent()
                : HttpContract.NotFound(context))
        .WithName("WithdrawEvidenceObservation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static EvidenceObservationResource Map(EvidenceObservationResponse observation) => new(
        observation.Id, observation.OwnerId, observation.ConceptId, observation.Aspect, observation.SignalKind,
        observation.SourceKind, observation.SourceId, observation.Assistance, observation.Conditions,
        observation.Status, observation.ObservedAt, HttpContract.VersionToken(observation.Version));
}
