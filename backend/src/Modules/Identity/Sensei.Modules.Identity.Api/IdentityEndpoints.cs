using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.Identity.Application;

namespace Sensei.Modules.Identity.Api;

public sealed record CreateUserRequest(string Email, string DisplayName, string UiLocale, string AnswerLanguage, string TimeZone);
public sealed record UpdateUserRequest(string DisplayName, string UiLocale, string AnswerLanguage, string TimeZone);
public sealed record UserResource(
    Guid Id, string Email, string DisplayName, string UiLocale, string AnswerLanguage, string TimeZone,
    DateTimeOffset CreatedAt, string VersionToken);

public static class IdentityEndpoints
{
    private const string EndpointId = "identity.users";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/users").WithTags("Identity");

        group.MapPost("/", async (CreateUserRequest request, HttpContext context, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.CreateAsync(new CreateUserCommand(
                request.Email, request.DisplayName, request.UiLocale, request.AnswerLanguage, request.TimeZone), cancellationToken);
            return HttpContract.CreatedVersioned(context.Response, $"/api/v1/identity/users/{user.Id}", Map(user), user.Version);
        })
        .WithName("CreateUser")
        .Produces<UserResource>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (int? limit, string? cursor, IUserService service, CancellationToken cancellationToken) =>
        {
            var users = (await service.ListAsync(cancellationToken)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(users, limit, cursor, EndpointId,
                user => user.CreatedAt.ToString("O"), user => user.Id));
        })
        .WithName("ListUsers")
        .Produces<PageEnvelope<UserResource>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.GetAsync(id, cancellationToken);
            return user is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(user), user.Version);
        })
        .WithName("GetUser")
        .Produces<UserResource>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, UpdateUserRequest request,
            HttpContext context, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.UpdateAsync(id, new UpdateUserCommand(
                request.DisplayName, request.UiLocale, request.AnswerLanguage, request.TimeZone,
                HttpContract.RequireVersion(ifMatch)), cancellationToken);
            return user is null ? HttpContract.NotFound(context) : HttpContract.OkVersioned(context.Response, Map(user), user.Version);
        })
        .WithName("UpdateUser")
        .Produces<UserResource>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", async (
            Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, HttpContext context,
            IUserService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, HttpContract.RequireVersion(ifMatch), cancellationToken)
                ? Results.NoContent()
                : HttpContract.NotFound(context))
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static UserResource Map(UserResponse user) => new(
        user.Id, user.Email, user.DisplayName, user.UiLocale, user.AnswerLanguage, user.TimeZone,
        user.CreatedAt, HttpContract.VersionToken(user.Version));
}
