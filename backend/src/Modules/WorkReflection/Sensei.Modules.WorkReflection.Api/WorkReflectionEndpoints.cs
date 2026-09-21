using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.Modules.WorkReflection.Application;

namespace Sensei.Modules.WorkReflection.Api;

public static class WorkReflectionEndpoints
{
    public static IEndpointRouteBuilder MapWorkReflectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/work/episodes").WithTags("Work Reflection");

        group.MapPost("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            CreateWorkEpisodeCommand command,
            IWorkEpisodeService service,
            CancellationToken cancellationToken) =>
        {
            var episode = await service.CreateAsync(ownerId, command, cancellationToken);
            return Results.Created($"/api/v1/work/episodes/{episode.Id}", episode);
        });

        group.MapGet("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            bool? includeArchived,
            IWorkEpisodeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(ownerId, includeArchived ?? false, cancellationToken)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IWorkEpisodeService service,
            CancellationToken cancellationToken) =>
        {
            var episode = await service.GetAsync(ownerId, id, cancellationToken);
            return episode is null ? Results.NotFound() : Results.Ok(episode);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            UpdateWorkEpisodeCommand command,
            IWorkEpisodeService service,
            CancellationToken cancellationToken) =>
        {
            var episode = await service.UpdateAsync(ownerId, id, command, cancellationToken);
            return episode is null ? Results.NotFound() : Results.Ok(episode);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            int expectedVersion,
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            IWorkEpisodeService service,
            CancellationToken cancellationToken) =>
            await service.ArchiveAsync(ownerId, id, expectedVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());

        return endpoints;
    }
}
