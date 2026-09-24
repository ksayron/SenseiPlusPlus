using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.Experience.Application;
using Sensei.Modules.Experience.Domain;

namespace Sensei.Modules.Experience.Api;

public sealed record ExperienceRevisionRequest(
    string Title, ExperienceSetting Setting, string Context, string Role, string Actions,
    string Alternatives, string Outcome, ImpactState ImpactState, IReadOnlyCollection<Guid> ConceptIds);
public sealed record ExperienceRevisionResource(
    int Number, string Title, ExperienceSetting Setting, string Context, string Role, string Actions,
    string Alternatives, string Outcome, ImpactState ImpactState, IReadOnlyCollection<Guid> ConceptIds,
    RevisionApprovalState ApprovalState, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt);
public sealed record ExperienceEntryResource(
    Guid Id, Guid OwnerId, bool IsArchived, DateTimeOffset CreatedAt, string VersionToken,
    IReadOnlyCollection<ExperienceRevisionResource> Revisions);

public static class ExperienceEndpoints
{
    private const string EndpointId = "experience.entries";

    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/experience/entries").WithTags("Experience and Presentation");

        group.MapPost("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId, ExperienceRevisionRequest request,
            HttpContext context, IExperienceEntryService service, CancellationToken cancellationToken) =>
        {
            var entry = await service.CreateAsync(ownerId, new CreateExperienceEntryCommand(
                request.Title, request.Setting, request.Context, request.Role, request.Actions,
                request.Alternatives, request.Outcome, request.ImpactState, request.ConceptIds), cancellationToken);
            return HttpContract.CreatedVersioned(context.Response, $"/api/v1/experience/entries/{entry.Id}", Map(entry), entry.Version);
        })
        .WithName("CreateExperienceEntry")
        .Produces<ExperienceEntryResource>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
            [FromHeader(Name = "X-Owner-Id")] Guid ownerId, bool? includeArchived, int? limit, string? cursor,
            IExperienceEntryService service, CancellationToken cancellationToken) =>
        {
            var entries = (await service.ListAsync(ownerId, includeArchived ?? false, cancellationToken)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(entries, limit, cursor, $"{EndpointId}:{ownerId}:{includeArchived ?? false}",
                entry => entry.CreatedAt.ToString("O"), entry => entry.Id));
        })
        .WithName("ListExperienceEntries")
        .Produces<PageEnvelope<ExperienceEntryResource>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId, HttpContext context,
            IExperienceEntryService service, CancellationToken cancellationToken) =>
        {
            var entry = await service.GetAsync(ownerId, id, cancellationToken);
            return entry is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(entry), entry.Version);
        })
        .WithName("GetExperienceEntry")
        .Produces<ExperienceEntryResource>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            [FromHeader(Name = "If-Match")] string? ifMatch, ExperienceRevisionRequest request,
            HttpContext context, IExperienceEntryService service, CancellationToken cancellationToken) =>
        {
            var entry = await service.ReviseAsync(ownerId, id, new ReviseExperienceEntryCommand(
                request.Title, request.Setting, request.Context, request.Role, request.Actions,
                request.Alternatives, request.Outcome, request.ImpactState, request.ConceptIds,
                HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return entry is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(entry), entry.Version);
        })
        .WithName("ReviseExperienceEntry")
        .Produces<ExperienceEntryResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapPost("/{id:guid}/revisions/{revisionNumber:int}/approval", async (
            Guid id, int revisionNumber, [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IExperienceEntryService service, CancellationToken cancellationToken) =>
        {
            var entry = await service.ApproveAsync(ownerId, id, revisionNumber,
                new ApproveExperienceRevisionCommand(HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return entry is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(entry), entry.Version);
        })
        .WithName("ApproveExperienceRevision")
        .Produces<ExperienceEntryResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", async (
            Guid id, [FromHeader(Name = "X-Owner-Id")] Guid ownerId,
            [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IExperienceEntryService service, CancellationToken cancellationToken) =>
            await service.ArchiveAsync(ownerId, id, HttpContract.RequireVersion(ifMatch), cancellationToken)
                ? Results.NoContent()
                : HttpContract.NotFound(context))
        .WithName("ArchiveExperienceEntry")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static ExperienceEntryResource Map(ExperienceEntryResponse entry) => new(
        entry.Id, entry.OwnerId, entry.IsArchived, entry.CreatedAt, HttpContract.VersionToken(entry.Version),
        entry.Revisions.Select(revision => new ExperienceRevisionResource(
            revision.Number, revision.Title, revision.Setting, revision.Context, revision.Role, revision.Actions,
            revision.Alternatives, revision.Outcome, revision.ImpactState, revision.ConceptIds,
            revision.ApprovalState, revision.CreatedAt, revision.ApprovedAt)).ToArray());
}
