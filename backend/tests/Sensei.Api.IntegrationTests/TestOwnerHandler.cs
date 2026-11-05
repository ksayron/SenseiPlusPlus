using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Sensei.Api.IntegrationTests;

// This authentication handler exists only in the test assembly.
public sealed class TestOwnerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IHostEnvironment env, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!env.IsEnvironment("Testing") || !config.GetValue<bool>("Authentication:EnableTestOwnerHeader"))
            return Task.FromResult(AuthenticateResult.Fail("Test authentication is disabled."));
        var raw = Request.Headers["X-Owner-Id"].ToString();
        var id = string.IsNullOrEmpty(raw) ? Guid.Parse("61c9ed1a-2333-46a2-9d48-3a840228e61b") : Guid.TryParse(raw, out var parsed) ? parsed : Guid.Empty;
        if (id == Guid.Empty) return Task.FromResult(AuthenticateResult.Fail("Invalid test owner."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "TestOwner");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "TestOwner")));
    }
}
