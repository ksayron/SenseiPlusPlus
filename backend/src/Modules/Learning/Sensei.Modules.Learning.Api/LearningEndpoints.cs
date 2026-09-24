using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Api;

public sealed record CreateConceptRequest(string Key, string Name, string Description, string Locale, ConceptDifficulty Difficulty);
public sealed record UpdateConceptRequest(string Name, string Description, string Locale, ConceptDifficulty Difficulty);
public sealed record ConceptResource(
    Guid Id, string Key, string Name, string Description, string Locale, ConceptDifficulty Difficulty,
    bool IsActive, DateTimeOffset CreatedAt, string VersionToken);

public static class LearningEndpoints
{
    private const string EndpointId = "learning.concepts";

    public static IEndpointRouteBuilder MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/learning/concepts").WithTags("Learning");

        group.MapPost("/", async (CreateConceptRequest request, HttpContext context, IConceptService service, CancellationToken cancellationToken) =>
        {
            var concept = await service.CreateAsync(new CreateConceptCommand(
                request.Key, request.Name, request.Description, request.Locale, request.Difficulty), cancellationToken);
            return HttpContract.CreatedVersioned(context.Response, $"/api/v1/learning/concepts/{concept.Id}", Map(concept), concept.Version);
        })
        .WithName("CreateConcept")
        .Produces<ConceptResource>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
            bool? includeInactive, int? limit, string? cursor,
            IConceptService service, CancellationToken cancellationToken) =>
        {
            var concepts = (await service.ListAsync(includeInactive ?? false, cancellationToken)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(concepts, limit, cursor, $"{EndpointId}:{includeInactive ?? false}",
                concept => concept.Key, concept => concept.Id));
        })
        .WithName("ListConcepts")
        .Produces<PageEnvelope<ConceptResource>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, IConceptService service, CancellationToken cancellationToken) =>
        {
            var concept = await service.GetAsync(id, cancellationToken);
            return concept is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(concept), concept.Version);
        })
        .WithName("GetConcept")
        .Produces<ConceptResource>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, UpdateConceptRequest request,
            HttpContext context, IConceptService service, CancellationToken cancellationToken) =>
        {
            var concept = await service.UpdateAsync(id, new UpdateConceptCommand(
                request.Name, request.Description, request.Locale, request.Difficulty,
                HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return concept is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(concept), concept.Version);
        })
        .WithName("UpdateConcept")
        .Produces<ConceptResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", async (
            Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IConceptService service, CancellationToken cancellationToken) =>
            await service.DeactivateAsync(id, HttpContract.RequireVersion(ifMatch), cancellationToken)
                ? Results.NoContent()
                : HttpContract.NotFound(context))
        .WithName("DeactivateConcept")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static ConceptResource Map(ConceptResponse concept) => new(
        concept.Id, concept.Key, concept.Name, concept.Description, concept.Locale, concept.Difficulty,
        concept.IsActive, concept.CreatedAt, HttpContract.VersionToken(concept.Version));
}
