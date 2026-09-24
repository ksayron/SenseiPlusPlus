using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Api;

public sealed record CreateWorkEpisodeRequest(string Title, WorkSetting Setting, DateOnly EventDate, string Role, string Summary);
public sealed record UpdateWorkEpisodeRequest(string Title, WorkSetting Setting, DateOnly EventDate, string Role, string Summary);
public sealed record WorkEpisodeResource(
    Guid Id, Guid OwnerId, string Title, WorkSetting Setting, DateOnly EventDate, string Role, string Summary,
    bool IsArchived, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string VersionToken);

public static class WorkReflectionEndpoints
{
    private const string EndpointId = "work.episodes";

    public static IEndpointRouteBuilder MapWorkReflectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/work/episodes").WithTags("Work Reflection");

        group.MapPost("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId, CreateWorkEpisodeRequest request,
            HttpContext context, IWorkEpisodeService service, CancellationToken cancellationToken) =>
        {
            var episode = await service.CreateAsync(ownerId, new CreateWorkEpisodeCommand(
                request.Title, request.Setting, request.EventDate, request.Role, request.Summary), cancellationToken);
            return HttpContract.CreatedVersioned(context.Response, $"/api/v1/work/episodes/{episode.Id}", Map(episode), episode.Version);
        })
        .WithName("CreateWorkEpisode")
        .Produces<WorkEpisodeResource>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId, bool? includeArchived, int? limit, string? cursor,
            IWorkEpisodeService service, CancellationToken cancellationToken) =>
        {
            var episodes = (await service.ListAsync(ownerId, includeArchived ?? false, cancellationToken)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(episodes, limit, cursor, $"{EndpointId}:{ownerId}:{includeArchived ?? false}",
                episode => episode.EventDate.ToString("yyyy-MM-dd"), episode => episode.Id));
        })
        .WithName("ListWorkEpisodes")
        .Produces<PageEnvelope<WorkEpisodeResource>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId, HttpContext context,
            IWorkEpisodeService service, CancellationToken cancellationToken) =>
        {
            var episode = await service.GetAsync(ownerId, id, cancellationToken);
            return episode is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(episode), episode.Version);
        })
        .WithName("GetWorkEpisode")
        .Produces<WorkEpisodeResource>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            [FromHeader(Name = "If-Match")] string? ifMatch, UpdateWorkEpisodeRequest request,
            HttpContext context, IWorkEpisodeService service, CancellationToken cancellationToken) =>
        {
            var episode = await service.UpdateAsync(ownerId, id, new UpdateWorkEpisodeCommand(
                request.Title, request.Setting, request.EventDate, request.Role, request.Summary,
                HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return episode is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(episode), episode.Version);
        })
        .WithName("UpdateWorkEpisode")
        .Produces<WorkEpisodeResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IWorkEpisodeService service, CancellationToken cancellationToken) =>
            await service.ArchiveAsync(ownerId, id, HttpContract.RequireVersion(ifMatch), cancellationToken)
                ? Results.NoContent()
                : HttpContract.NotFound(context))
        .WithName("ArchiveWorkEpisode")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static WorkEpisodeResource Map(WorkEpisodeResponse episode) => new(
        episode.Id, episode.OwnerId, episode.Title, episode.Setting, episode.EventDate, episode.Role, episode.Summary,
        episode.IsArchived, episode.CreatedAt, episode.UpdatedAt, HttpContract.VersionToken(episode.Version));
}
