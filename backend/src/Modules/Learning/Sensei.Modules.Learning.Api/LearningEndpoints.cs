using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sensei.Modules.Learning.Application;

namespace Sensei.Modules.Learning.Api;

public static class LearningEndpoints
{
    public static IEndpointRouteBuilder MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/learning/concepts").WithTags("Learning");

        group.MapPost("/", async (CreateConceptCommand command, IConceptService service, CancellationToken cancellationToken) =>
        {
            var concept = await service.CreateAsync(command, cancellationToken);
            return Results.Created($"/api/v1/learning/concepts/{concept.Id}", concept);
        });

        group.MapGet("/", async (
            bool? includeInactive,
            IConceptService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IConceptService service, CancellationToken cancellationToken) =>
        {
            var concept = await service.GetAsync(id, cancellationToken);
            return concept is null ? Results.NotFound() : Results.Ok(concept);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateConceptCommand command,
            IConceptService service,
            CancellationToken cancellationToken) =>
        {
            var concept = await service.UpdateAsync(id, command, cancellationToken);
            return concept is null ? Results.NotFound() : Results.Ok(concept);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            int expectedVersion,
            IConceptService service,
            CancellationToken cancellationToken) =>
            await service.DeactivateAsync(id, expectedVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());

        return endpoints;
    }
}
