using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Client.Contracts;
using Sensei.Modules.Identity.Infrastructure;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Domain;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sensei.Host.Authentication;

public sealed class AgentBearerEndpoint;
public sealed class CliGrant
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string AuthorizationId { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
public sealed class CliGrantMapping : IEntityTypeConfiguration<CliGrant>
{
    public void Configure(EntityTypeBuilder<CliGrant> b)
    {
        b.ToTable("cli_grants", "identity"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.OwnerId, x.Id }).IsUnique();
        b.HasOne<LoginAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.SecurityStamp).HasMaxLength(256);
    }
}
public sealed record CliConsentRequest(bool Approve);
public static class AgentOAuth
{
    public const string ClientId = "sensei-local-agent";
    public const string WriteScope = "professional-events:write", ReadScope = "professional-events:read", ConceptScope = "concepts:read";
    public const string WritePolicy = "AgentImport", ReadPolicy = "AgentReceipt", ConceptPolicy = "AgentConcepts";
    public const string GrantClaim = "sensei:cli-grant";
    private sealed record Pending(string Query, DateTimeOffset ExpiresAt, Guid? Owner = null, bool? Decision = null);
    private static readonly ConcurrentDictionary<string, Pending> PendingRequests = new();
    private static readonly SemaphoreSlim SeedLock = new(1);
    public static readonly string[] AllowedScopes = [Scopes.OpenId, Scopes.Profile, Scopes.OfflineAccess, WriteScope, ReadScope, ConceptScope];

    public static void AddAgentOAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var issuer = new Uri(config["AgentOAuth:Issuer"] ?? (env.IsEnvironment("Testing") ? "https://localhost/" : "https://localhost:7082/"));
        var insecure = config.GetValue<bool>("AgentOAuth:AllowInsecureLoopback") && env.IsDevelopment() && issuer.IsLoopback && issuer.Scheme == "http";
        if (issuer.Scheme != "https" && !insecure) throw new InvalidOperationException("OAuth requires HTTPS or explicit Development loopback HTTP.");
        var webOrigin = new Uri(config["AgentOAuth:WebOrigin"] ?? issuer.AbsoluteUri);
        if (webOrigin.UserInfo.Length != 0 || webOrigin.AbsolutePath != "/" || webOrigin.Query.Length != 0 || webOrigin.Fragment.Length != 0 ||
            webOrigin.Scheme != "https" && !(insecure && webOrigin.Scheme == "http" && webOrigin.IsLoopback))
            throw new InvalidOperationException("OAuth web origin must be HTTPS or explicitly enabled Development loopback HTTP.");
        services.AddDbContext<SenseiDbContext>(o => o.UseOpenIddict());
        services.AddScoped<TransactionRunner>();
        services.AddOpenIddict().AddCore(o => o.UseEntityFrameworkCore().UseDbContext<SenseiDbContext>())
            .AddServer(o =>
            {
                o.SetIssuer(issuer).SetAuthorizationEndpointUris("/connect/authorize").SetTokenEndpointUris("/connect/token").SetRevocationEndpointUris("/connect/revoke");
                o.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();
                o.RegisterScopes(AllowedScopes).SetAccessTokenLifetime(TimeSpan.FromMinutes(5)).SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2)).SetRefreshTokenLifetime(TimeSpan.FromDays(30)).SetRefreshTokenReuseLeeway(TimeSpan.Zero);
                // The OpenAPI tooling starts a mock host; it must not require deployment secrets.
                var generatingDocument = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
                if (env.IsEnvironment("Testing") || generatingDocument) o.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                else if (env.IsDevelopment()) o.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
                else
                {
                    var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                        config["AgentOAuth:CertificatePath"] ?? throw new InvalidOperationException("OAuth certificate required."), config["AgentOAuth:CertificatePassword"]);
                    o.AddEncryptionCertificate(certificate).AddSigningCertificate(certificate);
                }
                o.AddEventHandler<OpenIddictServerEvents.HandleRevocationRequestContext>(b => b.UseScopedHandler<RevokeCliGrant>()
                    .SetOrder(OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor.Order - 500));
                var integration = o.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
                if (insecure) integration.DisableTransportSecurityRequirement();
            }).AddValidation(o => { o.UseLocalServer(); o.UseAspNetCore(); o.AddAudiences("sensei-agent"); o.EnableTokenEntryValidation(); o.EnableAuthorizationEntryValidation(); o.Configure(options => options.TokenValidationParameters.ClockSkew = TimeSpan.Zero); });
        services.AddAuthorization(o =>
        {
            foreach (var (policy, scope) in new[] { (WritePolicy, WriteScope), (ReadPolicy, ReadScope), (ConceptPolicy, ConceptScope) })
                o.AddPolicy(policy, p => p.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser().RequireAssertion(c => c.User.HasScope(scope)));
        });
    }

    public static async Task SeedClient(IServiceProvider provider)
    {
        await SeedLock.WaitAsync();
        try
        {
            var manager = provider.GetRequiredService<IOpenIddictApplicationManager>();
            if (await manager.FindByClientIdAsync(ClientId) is not null) return;
            var descriptor = new OpenIddictApplicationDescriptor { ClientId = ClientId, DisplayName = "Sensei++ local evidence agent",
                ClientType = ClientTypes.Public, ApplicationType = ApplicationTypes.Native, ConsentType = ConsentTypes.Explicit };
            descriptor.RedirectUris.Add(new Uri("http://localhost/"));
            descriptor.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code]);
            foreach (var scope in AllowedScopes.Where(x => x is not (Scopes.OpenId or Scopes.OfflineAccess))) descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
            await manager.CreateAsync(descriptor);
        }
        finally { SeedLock.Release(); }
    }

    public static async Task<bool> ValidGrant(ClaimsPrincipal principal, SenseiDbContext db, UserManager<LoginAccount> users, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.GetClaim(GrantClaim), out var id)) return false;
        var grant = await db.Set<CliGrant>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (grant is null || grant.Revoked || grant.ExpiresAt <= DateTimeOffset.UtcNow || principal.GetClaim(Claims.Subject) != grant.OwnerId.ToString()) return false;
        var user = await users.FindByIdAsync(grant.OwnerId.ToString());
        return user is not null && grant.SecurityStamp == await users.GetSecurityStampAsync(user);
    }

    public static void MapAgentOAuth(this WebApplication app)
    {
        app.MapGet("/connect/authorize", async (HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users, IOpenIddictAuthorizationManager authorizations, IOpenIddictApplicationManager applications) =>
        {
            var request = c.GetOpenIddictServerRequest()!;
            if (request.ClientId != ClientId || request.CodeChallengeMethod != CodeChallengeMethods.Sha256 || request.GetScopes().Any(x => !AllowedScopes.Contains(x)))
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?> { [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(c.Request.QueryString.Value).Where(x => x.Key != "consent_ticket");
            var canonical = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("", query);
            if (!c.Request.Query.TryGetValue("consent_ticket", out var ticket))
            {
                foreach (var pair in PendingRequests.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow)) PendingRequests.TryRemove(pair.Key, out _);
                if (PendingRequests.Count >= 1000) return Results.StatusCode(429);
                var key = Guid.NewGuid().ToString("N");
                PendingRequests[key] = new(canonical, DateTimeOffset.UtcNow.AddMinutes(5));
                var origin = app.Configuration["AgentOAuth:WebOrigin"] ?? app.Configuration["AgentOAuth:Issuer"] ?? (app.Environment.IsEnvironment("Testing") ? "https://localhost" : "https://localhost:7082");
                return Results.Redirect(origin.TrimEnd('/') + "/agent/authorize?request=" + key);
            }
            if (!PendingRequests.TryRemove(ticket.ToString(), out var pending) || pending.ExpiresAt <= DateTimeOffset.UtcNow || pending.Query != canonical ||
                pending.Owner != c.OwnerId() || pending.Decision is null) return Results.BadRequest();
            if (pending.Decision != true) return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?> { [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            var user = await users.FindByIdAsync(c.OwnerId().ToString());
            if (user is null) return Results.Unauthorized();
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, user.Id.ToString()).SetClaim(ClaimTypes.NameIdentifier, user.Id.ToString());
            identity.SetClaim(Claims.Name, (await db.Set<UserAccount>().SingleAsync(x => x.Id == user.Id, c.RequestAborted)).DisplayName);
            var principal = new ClaimsPrincipal(identity); principal.SetScopes(request.GetScopes()); principal.SetResources("sensei-agent");
            var authorization = await authorizations.CreateAsync(principal, user.Id.ToString(), (await applications.GetIdAsync((await applications.FindByClientIdAsync(ClientId))!))!, AuthorizationTypes.Permanent, principal.GetScopes());
            var grant = new CliGrant { Id = Guid.NewGuid(), OwnerId = user.Id, SecurityStamp = await users.GetSecurityStampAsync(user),
                AuthorizationId = (await authorizations.GetIdAsync(authorization))!, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) };
            db.Add(grant); await db.SaveChangesAsync(c.RequestAborted);
            principal.SetAuthorizationId(grant.AuthorizationId); principal.SetClaim(GrantClaim, grant.Id.ToString());
            principal.SetDestinations(claim => claim.Type is Claims.Subject or Claims.Name or ClaimTypes.NameIdentifier or GrantClaim ? [Destinations.AccessToken, Destinations.IdentityToken] : []);
            return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }).ExcludeFromDescription();

        app.MapPost("/connect/token", async (HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users) =>
        {
            var principal = (await c.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal!;
            if (!await ValidGrant(principal, db, users, c.RequestAborted)) return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?> { [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            var grant = await db.Set<CliGrant>().SingleAsync(x => x.Id.ToString() == principal.GetClaim(GrantClaim), c.RequestAborted);
            principal.SetRefreshTokenLifetime(grant.ExpiresAt - DateTimeOffset.UtcNow);
            return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }).ExcludeFromDescription();

        app.MapGet("/api/v1/auth/cli-consent/{ticket}", (string ticket, HttpContext c) => PendingRequests.TryGetValue(ticket, out var pending) && pending.ExpiresAt > DateTimeOffset.UtcNow
            ? Results.Ok(new { clientName = "Sensei++ local evidence agent", scopes = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(pending.Query)["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries), account = c.User.FindFirstValue(ClaimTypes.NameIdentifier) }) : Results.NotFound()).RequireAuthorization();
        app.MapPost("/api/v1/auth/cli-consent/{ticket}", (string ticket, CliConsentRequest body, HttpContext c) =>
        {
            if (!PendingRequests.TryGetValue(ticket, out var pending) || pending.ExpiresAt <= DateTimeOffset.UtcNow || pending.Decision is not null) return Results.NotFound();
            if (!PendingRequests.TryUpdate(ticket, pending with { Owner = c.OwnerId(), Decision = body.Approve }, pending)) return Results.Conflict();
            return Results.Ok(new { redirect = "/connect/authorize" + pending.Query + "&consent_ticket=" + ticket });
        }).RequireAuthorization();
        app.MapGet("/api/v1/auth/connections", async (HttpContext c, SenseiDbContext db) => Results.Ok(await db.Set<CliGrant>().Where(x => x.OwnerId == c.OwnerId())
            .Select(x => new { x.Id, x.CreatedAt, x.ExpiresAt, x.Revoked }).ToArrayAsync(c.RequestAborted))).RequireAuthorization();
        app.MapPost("/api/v1/auth/connections/{id:guid}/revoke", async (Guid id, HttpContext c, SenseiDbContext db, IOpenIddictAuthorizationManager manager) =>
        {
            var owner = c.OwnerId(); var grant = await db.Set<CliGrant>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, c.RequestAborted);
            if (grant is null) return Results.NotFound();
            grant.Revoked = true; await db.SaveChangesAsync(c.RequestAborted);
            var authorization = await manager.FindByIdAsync(grant.AuthorizationId); if (authorization is not null) await manager.TryRevokeAsync(authorization);
            return Results.NoContent();
        }).RequireAuthorization(AuthenticationSetup.RecentPolicy);
        app.MapGet("/api/v1/agent/profile", (HttpContext c) => Results.Ok(new AgentProfile(c.OwnerId(), c.User.GetClaim(Claims.Name) ?? "Account")))
            .RequireAuthorization(ReadPolicy).WithMetadata(new AgentBearerEndpoint());
        app.MapGet("/api/v1/agent/concepts", async (SenseiDbContext db, CancellationToken ct) => Results.Ok(await db.Set<Concept>().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new AgentConcept(x.Id, x.Name)).Take(1000).ToArrayAsync(ct)))
            .RequireAuthorization(ConceptPolicy).WithMetadata(new AgentBearerEndpoint());
    }
}

public sealed class RevokeCliGrant(SenseiDbContext db, IOpenIddictAuthorizationManager authorizations)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleRevocationRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleRevocationRequestContext context)
    {
        var authorizationId = context.GenericTokenPrincipal?.GetAuthorizationId();
        if (authorizationId is null) return;
        var grant = await db.Set<CliGrant>().SingleOrDefaultAsync(x => x.AuthorizationId == authorizationId, context.CancellationToken);
        if (grant is not null) { grant.Revoked = true; await db.SaveChangesAsync(context.CancellationToken); }
        var authorization = await authorizations.FindByIdAsync(authorizationId, context.CancellationToken);
        if (authorization is not null) await authorizations.TryRevokeAsync(authorization, context.CancellationToken);
    }
}
