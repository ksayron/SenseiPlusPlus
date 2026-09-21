using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.Modules.Evidence.Application;

namespace Sensei.Modules.Evidence.Api;

public static class EvidenceEndpoints
{
    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/evidence/observations").WithTags("Evidence and Profile");

        group.MapPost("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            CreateEvidenceObservationCommand command,
            IEvidenceObservationService service,
            CancellationToken cancellationToken) =>
        {
            var observation = await service.CreateAsync(ownerId, command, cancellationToken);
            return Results.Created($"/api/v1/evidence/observations/{observation.Id}", observation);
        });

        group.MapGet("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            Guid? conceptId,
            bool? includeInactive,
            IEvidenceObservationService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(ownerId, conceptId, includeInactive ?? false, cancellationToken)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IEvidenceObservationService service,
            CancellationToken cancellationToken) =>
        {
            var observation = await service.GetAsync(ownerId, id, cancellationToken);
            return observation is null ? Results.NotFound() : Results.Ok(observation);
        });

        group.MapPut("/{id:guid}/status", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            UpdateEvidenceStatusCommand command,
            IEvidenceObservationService service,
            CancellationToken cancellationToken) =>
        {
            var observation = await service.ChangeStatusAsync(ownerId, id, command, cancellationToken);
            return observation is null ? Results.NotFound() : Results.Ok(observation);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            int expectedVersion,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IEvidenceObservationService service,
            CancellationToken cancellationToken) =>
            await service.WithdrawAsync(ownerId, id, expectedVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());

        return endpoints;
    }
}
