using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sensei.Modules.Identity.Application;

namespace Sensei.Modules.Identity.Api;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/users").WithTags("Identity");

        group.MapPost("/", async (CreateUserCommand command, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.CreateAsync(command, cancellationToken);
            return Results.Created($"/api/v1/identity/users/{user.Id}", user);
        });

        group.MapGet("/", async (IUserService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.GetAsync(id, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserCommand command,
            IUserService service,
            CancellationToken cancellationToken) =>
        {
            var user = await service.UpdateAsync(id, command, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IUserService service,
            CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return endpoints;
    }
}
