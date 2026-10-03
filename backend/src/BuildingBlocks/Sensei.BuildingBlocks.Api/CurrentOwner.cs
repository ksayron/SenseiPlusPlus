using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Application;

namespace Sensei.BuildingBlocks.Api;

public sealed class HttpCurrentOwner(IHttpContextAccessor accessor) : ICurrentOwner
{
    public Guid Id => Require(accessor.HttpContext?.User);

    public static Guid Require(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true &&
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
            ? id : throw new ApiContractException("authentication_required", 401, "Sign in required", "Sign in to access your workspace.");
}

public static class OwnerContext
{
    public static Guid OwnerId(this HttpContext context) => context.RequestServices.GetRequiredService<ICurrentOwner>().Id;
}
