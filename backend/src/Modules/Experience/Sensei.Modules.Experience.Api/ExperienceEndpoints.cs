using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.Modules.Experience.Application;

namespace Sensei.Modules.Experience.Api;

public static class ExperienceEndpoints
{
    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/experience/entries").WithTags("Experience and Presentation");

        group.MapPost("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            CreateExperienceEntryCommand command,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.CreateAsync(ownerId, command, cancellationToken);
            return Results.Created($"/api/v1/experience/entries/{entry.Id}", entry);
        });

        group.MapGet("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            bool? includeArchived,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(ownerId, includeArchived ?? false, cancellationToken)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.GetAsync(ownerId, id, cancellationToken);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            ReviseExperienceEntryCommand command,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.ReviseAsync(ownerId, id, command, cancellationToken);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });

        group.MapPost("/{id:guid}/revisions/{revisionNumber:int}/approval", async (
            Guid id,
            int revisionNumber,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            ApproveExperienceRevisionCommand command,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.ApproveAsync(ownerId, id, revisionNumber, command, cancellationToken);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            int expectedVersion,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IExperienceEntryService service,
            CancellationToken cancellationToken) =>
            await service.ArchiveAsync(ownerId, id, expectedVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());

        return endpoints;
    }
}
