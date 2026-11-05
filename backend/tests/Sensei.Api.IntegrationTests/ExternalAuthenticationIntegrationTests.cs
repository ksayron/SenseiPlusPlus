using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Host.Authentication;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Identity.Infrastructure;

namespace Sensei.Api.IntegrationTests;

internal sealed class BrowserCookieHandler(CookieContainer jar) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Cookie");
        var cookies = jar.GetCookieHeader(request.RequestUri!);
        if (!string.IsNullOrEmpty(cookies)) request.Headers.TryAddWithoutValidation("Cookie", cookies);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values) jar.SetCookies(request.RequestUri!, value);
        return response;
    }
}

[Collection(PostgreSqlCollection.Name)]
public sealed class ExternalAuthenticationIntegrationTests(PostgreSqlFixture fixture)
{
    private const string Password = "Correct-Horse-Password1!";
    private static readonly ConditionalWeakTable<HttpClient, CookieContainer> BrowserCookies = new();
    private static HttpClient Client(SenseiWebApplicationFactory factory)
    {
        var jar = new CookieContainer();
        var client = new HttpClient(new BrowserCookieHandler(jar) { InnerHandler = factory.Server.CreateHandler() }) { BaseAddress = new Uri("https://localhost") };
        BrowserCookies.Add(client, jar);
        return client;
    }
    private static ProviderIdentity Identity(string provider, bool verified = true) => new(provider == "GitHub" ? Random.Shared.NextInt64(10000000, long.MaxValue).ToString() : Guid.NewGuid().ToString("N"), $"oauth-{Guid.NewGuid():N}@example.test", Verified: verified);
    private static async Task Csrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString());
    }
    private static async Task<string> Start(HttpClient client, string provider, string purpose = "signin", string returnPath = "/connections")
    {
        await Csrf(client);
        var start = await client.PostAsJsonAsync($"/api/v1/auth/external/{provider}/start", new { purpose, returnPath });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        return (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirect").GetString()!;
    }
    private static async Task<string> Callback(ExternalProviderFactory factory, HttpClient client, string provider, ProviderIdentity identity, string purpose = "signin", string returnPath = "/connections")
    {
        var challenge = await client.GetAsync(await Start(client, provider, purpose, returnPath));
        Assert.True(challenge.StatusCode == HttpStatusCode.Redirect, "Challenge failed: " + await challenge.Content.ReadAsStringAsync() + "; schemes=" + string.Join(',', (await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync()).Select(x => x.Name)));
        var authorization = challenge.Headers.Location!;
        var query = QueryHelpers.ParseQuery(authorization.Query);
        Assert.Contains("email", query["scope"].ToString());
        Assert.DoesNotContain("repo", query["scope"].ToString());
        var code = factory.Provider.Authorize(authorization, identity);
        var callback = "/signin-" + provider.ToLowerInvariant() + "?code=" + code + "&state=" + Uri.EscapeDataString(query["state"].ToString());
        var response = await client.GetAsync(callback);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.Location!.OriginalString == "/api/v1/auth/external/complete", "Callback failed: " + factory.Provider.LastFailure?.GetBaseException().Message);
        return callback;
    }
    private static async Task<HttpResponseMessage> Flow(ExternalProviderFactory factory, HttpClient client, string provider, ProviderIdentity identity, string purpose = "signin")
    {
        await Callback(factory, client, provider, identity, purpose);
        var complete = await client.GetAsync("/api/v1/auth/external/complete");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        return complete;
    }
    private static async Task<JsonElement> Register(HttpClient client, string email)
    {
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Local name" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task Logout(HttpClient client) { await Csrf(client); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode); }
    private static void Success(HttpResponseMessage response) => Assert.StartsWith("/auth/result?returnPath=", response.Headers.Location!.OriginalString);
    private static void Error(HttpResponseMessage response, string code) => Assert.Equal("/auth/result?error=" + code, response.Headers.Location!.OriginalString);

    [Theory]
    [InlineData("/learn?search=%D1%82%D0%B5%D1%81%D1%82", true)]
    [InlineData("/learn?search=100%25", true)]
    [InlineData("/agent/authorize?request=example-ticket", true)]
    [InlineData("/learn/../sign-out", false)]
    [InlineData("/learn/../api/v1/auth/logout", false)]
    [InlineData("/./signin-google", false)]
    [InlineData("/%2fevil.example", false)]
    public void Return_path_preserves_encoded_queries_without_allowing_path_tricks(string path, bool allowed) =>
        Assert.Equal(allowed, ExternalAuthenticationEndpoints.SafeReturnPath(path));

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task Provider_completion_preserves_an_encoded_learning_search(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        const string returnPath = "/learn?search=%D1%82%D0%B5%D1%81%D1%82";
        await Callback(factory, client, provider, Identity(provider), returnPath: returnPath);
        var complete = await client.GetAsync("/api/v1/auth/external/complete");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        var query = QueryHelpers.ParseQuery(new Uri("https://localhost" + complete.Headers.Location!.OriginalString).Query);
        Assert.Equal(returnPath, query["returnPath"].ToString());
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task Verified_provider_creates_passwordless_account_and_reuses_stable_identity_without_overwriting_preferences(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var identity = Identity(provider);
        Success(await Flow(factory, client, provider, identity));
        var account = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me"); var owner = account.GetProperty("id").GetGuid();
        Assert.Equal(identity.Email, account.GetProperty("email").GetString());
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods")).GetProperty("hasPassword").GetBoolean());
        var response = await client.PostAsJsonAsync("/api/v1/work/episodes", new { title = "Owned before another login", setting = "PersonalProject", eventDate = "2026-10-04", role = "Developer", summary = "Stable provider ID" });
        // Refresh CSRF after the principal changed.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/work/episodes", new { title = "Owned before another login", setting = "PersonalProject", eventDate = "2026-10-04", role = "Developer", summary = "Stable provider ID" })).StatusCode);
        await Logout(client);
        var changed = identity with { Name = "Renamed profile", Email = $"changed-{Guid.NewGuid():N}@example.test", Username = "renamed-profile", Verified = false };
        Success(await Flow(factory, client, provider, changed));
        var again = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal(owner, again.GetProperty("id").GetGuid()); Assert.Equal(identity.Email, again.GetProperty("email").GetString());
        Assert.Equal(identity.Name, again.GetProperty("displayName").GetString());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/v1/work/episodes")).GetProperty("items").EnumerateArray());
        var methods = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        var profile = Assert.Single(methods.GetProperty("profiles").EnumerateArray());
        Assert.Equal("Renamed profile", profile.GetProperty("displayName").GetString());
        if (provider == "GitHub") Assert.Equal("https://github.com/renamed-profile", profile.GetProperty("profileUrl").GetString());
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        Assert.Empty(await db.Set<IdentityUserToken<Guid>>().Where(x => x.UserId == owner).ToArrayAsync());
        Assert.Empty(await db.Set<Sensei.Modules.Evidence.Domain.EvidenceObservation>().Where(x => x.OwnerId == owner).ToArrayAsync());
        Assert.NotNull(factory.Provider.LastVerifier);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task Missing_verified_email_cannot_create_account_and_email_collision_requires_explicit_linking(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var identity = Identity(provider, false);
        Error(await Flow(factory, client, provider, identity), "verified_email_required");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var local = await Register(client, identity.Email); var owner = local.GetProperty("id").GetGuid();
        await Logout(client);
        Error(await Flow(factory, client, provider, identity with { Verified = true }), "email_conflict");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await Csrf(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = identity.Email, password = Password })).StatusCode);
        Success(await Flow(factory, client, provider, identity, "link"));
        await Logout(client);
        Success(await Flow(factory, client, provider, identity));
        Assert.Equal(owner, (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task Linking_and_provider_confirmation_cannot_transfer_or_switch_accounts(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var a = Client(factory); using var b = Client(factory);
        var identity = Identity(provider); Success(await Flow(factory, a, provider, identity));
        var other = await Register(b, $"other-{Guid.NewGuid():N}@example.test");
        Error(await Flow(factory, b, provider, identity, "link"), "provider_identity_conflict");
        Assert.Equal(other.GetProperty("id").GetGuid(), (await b.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
        Error(await Flow(factory, a, provider, Identity(provider), "reauthenticate"), "provider_identity_mismatch");
        Success(await Flow(factory, a, provider, identity, "reauthenticate"));
        Assert.Equal(HttpStatusCode.NoContent, (await a.GetAsync("/api/v1/auth/reauthentication")).StatusCode);
        Success(await Flow(factory, a, provider, identity with { Name = "Explicit refresh" }, "refresh"));
    }

    [Fact]
    public async Task Disconnect_requires_recent_authentication_keeps_last_method_and_serializes_concurrent_removals()
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var google = Identity("Google"); var github = Identity("GitHub");
        Success(await Flow(factory, client, "Google", google)); await Csrf(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/external/Google/disconnect", new { })).StatusCode);
        Success(await Flow(factory, client, "GitHub", github, "link")); await Csrf(client);
        var removals = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/auth/external/Google/disconnect", new { }), client.PostAsJsonAsync("/api/v1/auth/external/GitHub/disconnect", new { }));
        Assert.Single(removals, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(removals, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Unauthorized);
        // A racing old-stamp request may clear the shared browser cookie. Inspect persistence, then sign in afresh.
        string provider;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var owner = (await db.Set<UserAccount>().SingleAsync(x => x.Email == google.Email)).Id;
            provider = Assert.Single(await db.Set<ConnectedProfile>().Where(x => x.OwnerId == owner).ToArrayAsync()).Provider;
            Assert.Single(await db.Set<IdentityUserLogin<Guid>>().Where(x => x.UserId == owner).ToArrayAsync());
        }
        using var fresh = Client(factory);
        Success(await Flow(factory, fresh, provider, provider == "Google" ? google : github));
        await ExpireConfirmation(factory, fresh);
        await Csrf(fresh);
        Assert.Equal(HttpStatusCode.Forbidden, (await fresh.PostAsJsonAsync($"/api/v1/auth/external/{provider}/disconnect", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fresh.PostAsJsonAsync($"/api/v1/auth/external/{(provider == "Google" ? "GitHub" : "Google")}/start", new { purpose = "link", returnPath = "/connections" })).StatusCode);
        Success(await Flow(factory, fresh, provider, provider == "Google" ? google : github, "reauthenticate"));
        Assert.Equal(HttpStatusCode.NoContent, (await fresh.GetAsync("/api/v1/auth/reauthentication")).StatusCode);
    }

    private static async Task ExpireConfirmation(ExternalProviderFactory factory, HttpClient client)
    {
        // Reprotect a real cookie without modifying the user's database identity or introducing a test auth scheme.
        var response = await client.GetAsync("/api/v1/auth/me"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<JsonElement>();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>();
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<LoginAccount>>().CreateAsync((await users.FindByIdAsync(account.GetProperty("id").GetString()!))!);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AuthenticationSetup.AuthTimeClaim, DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString()));
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, IdentityConstants.ApplicationScheme));
        BrowserCookies.GetValue(client, _ => throw new InvalidOperationException("Browser cookies required")).SetCookies(client.BaseAddress!, "sensei.session=" + cookie + "; Path=/; Secure; HttpOnly");
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task State_tampering_missing_correlation_cancellation_outage_and_callback_replay_fail_safely(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var challenge = await client.GetAsync(await Start(client, provider)); var url = challenge.Headers.Location!;
        var query = QueryHelpers.ParseQuery(url.Query); var code = factory.Provider.Authorize(url, Identity(provider));
        Error(await client.GetAsync($"/signin-{provider.ToLowerInvariant()}?code={code}&state=invalid-state"), "external_failed");
        using var stranger = Client(factory);
        Error(await stranger.GetAsync($"/signin-{provider.ToLowerInvariant()}?code={code}&state={Uri.EscapeDataString(query["state"].ToString())}"), "external_failed");
        var callback = await Callback(factory, client, provider, Identity(provider));
        Success(await client.GetAsync("/api/v1/auth/external/complete"));
        Error(await client.GetAsync(callback), "external_failed");
        Error(await client.GetAsync("/api/v1/auth/external/complete"), "external_expired");
        await Logout(client);
        challenge = await client.GetAsync(await Start(client, provider)); query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var cancelled = await client.GetAsync($"/signin-{provider.ToLowerInvariant()}?error=access_denied&state={Uri.EscapeDataString(query["state"].ToString())}");
        Error(cancelled, "external_cancelled");
        challenge = await client.GetAsync(await Start(client, provider)); url = challenge.Headers.Location!; query = QueryHelpers.ParseQuery(url.Query);
        code = factory.Provider.Authorize(url, Identity(provider)); factory.Provider.Outage = true;
        Error(await client.GetAsync($"/signin-{provider.ToLowerInvariant()}?code={code}&state={Uri.EscapeDataString(query["state"].ToString())}"), "external_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("signature")]
    public async Task Google_rejects_invalid_identity_tokens(string invalid)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var challenge = await client.GetAsync(await Start(client, "Google")); var url = challenge.Headers.Location!;
        var query = QueryHelpers.ParseQuery(url.Query); var code = factory.Provider.Authorize(url, Identity("Google"));
        factory.Provider.WrongNonce = invalid == "nonce"; factory.Provider.WrongAudience = invalid == "audience";
        factory.Provider.WrongIssuer = invalid == "issuer"; factory.Provider.WrongSignature = invalid == "signature";
        Error(await client.GetAsync($"/signin-google?code={code}&state={Uri.EscapeDataString(query["state"].ToString())}"), "external_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GitHub")]
    public async Task Pkce_rejects_a_code_from_another_authorization_attempt(string provider)
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var first = await client.GetAsync(await Start(client, provider));
        var code = factory.Provider.Authorize(first.Headers.Location!, Identity(provider));
        var second = await client.GetAsync(await Start(client, provider));
        var state = QueryHelpers.ParseQuery(second.Headers.Location!.Query)["state"].ToString();
        Error(await client.GetAsync($"/signin-{provider.ToLowerInvariant()}?code={code}&state={Uri.EscapeDataString(state)}"), "external_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Expired_completion_and_recovered_account_cannot_finish_a_pending_connection()
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        await Callback(factory, client, "Google", Identity("Google"));
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<ExternalAuthFlow>().Where(x => x.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Error(await client.GetAsync("/api/v1/auth/external/complete"), "external_expired");
        var local = await Register(client, $"recover-flow-{Guid.NewGuid():N}@example.test"); var owner = local.GetProperty("id").GetGuid();
        await Callback(factory, client, "Google", Identity("Google"), "link");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var profile = await db.Set<UserAccount>().SingleAsync(x => x.Id == owner);
            Assert.True((await LocalAccountRecovery.RecoverAsync(scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>(), profile, "Recovered-Password2!")).Succeeded);
        }
        Error(await client.GetAsync("/api/v1/auth/external/complete"), "account_changed");
        await using var verify = factory.Services.CreateAsyncScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<IdentityUserLogin<Guid>>().Where(x => x.UserId == owner).ToArrayAsync());
    }

    [Fact]
    public async Task Password_account_can_disconnect_provider_and_keep_its_owned_workspace()
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var account = await Register(client, $"disconnect-{Guid.NewGuid():N}@example.test"); var owner = account.GetProperty("id").GetGuid();
        var identity = Identity("GitHub"); Success(await Flow(factory, client, "GitHub", identity, "link"));
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/external/GitHub/start", new { purpose = "link", returnPath = "/connections" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/work/episodes", new { title = "Keep after disconnect", setting = "PersonalProject", eventDate = "2026-10-04", role = "Developer", summary = "Preserve owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/external/GitHub/disconnect", new { })).StatusCode);
        var methods = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.True(methods.GetProperty("hasPassword").GetBoolean()); Assert.Empty(methods.GetProperty("profiles").EnumerateArray());
        Assert.Equal(owner, (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/v1/work/episodes")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Disabled_connected_provider_is_not_an_alternative_for_the_last_enabled_method()
    {
        var google = Identity("Google"); var github = Identity("GitHub");
        using (var first = new ExternalProviderFactory(fixture.ConnectionString))
        using (var client = Client(first))
        {
            Success(await Flow(first, client, "GitHub", github));
            Success(await Flow(first, client, "Google", google, "link"));
        }
        using var restarted = new ExternalProviderFactory(fixture.ConnectionString, google: false); using var browser = Client(restarted);
        Success(await Flow(restarted, browser, "GitHub", github));
        var methods = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.False(Assert.Single(methods.GetProperty("profiles").EnumerateArray(), x => x.GetProperty("provider").GetString() == "Google").GetProperty("enabled").GetBoolean());
        await Csrf(browser);
        var removal = await browser.PostAsJsonAsync("/api/v1/auth/external/GitHub/disconnect", new { });
        Assert.Equal(HttpStatusCode.Conflict, removal.StatusCode);
        Assert.Equal("last_signin_method", (await removal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Csrf_open_redirect_expiry_browser_binding_and_account_switch_are_rejected()
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/external/Google/start", new { })).StatusCode);
        await Csrf(client);
        foreach (var path in new[] { "https://evil.example", "//evil.example", "/\\evil.example", "/%2fevil.example", "/api/v1/auth/logout", "/auth/result", "/sign-out" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/external/Google/start", new { returnPath = path })).StatusCode);
        var redirect = await Start(client, "Google");
        using var stranger = Client(factory); Error(await stranger.GetAsync(redirect), "external_expired");
        var flowId = QueryHelpers.ParseQuery(new Uri("https://localhost" + redirect).Query)["flow"].ToString();
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<ExternalAuthFlow>().Where(x => x.Id == flowId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Error(await client.GetAsync(redirect), "external_expired");
        await Callback(factory, client, "Google", Identity("Google"));
        var local = await Register(client, $"switched-{Guid.NewGuid():N}@example.test");
        Error(await client.GetAsync("/api/v1/auth/external/complete"), "account_changed");
        Assert.Equal(local.GetProperty("id").GetGuid(), (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Passwordless_provider_confirmation_can_revoke_an_owned_agent_grant()
    {
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var client = Client(factory);
        var identity = Identity("GitHub"); Success(await Flow(factory, client, "GitHub", identity));
        var owner = (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid();
        var grant = new CliGrant { Id = Guid.NewGuid(), OwnerId = owner, AuthorizationId = "test-" + Guid.NewGuid().ToString("N"), SecurityStamp = "test", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        await using (var scope = factory.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>(); db.Add(grant); await db.SaveChangesAsync(); }
        await ExpireConfirmation(factory, client); await Csrf(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/auth/connections/{grant.Id}/revoke", new { })).StatusCode);
        Success(await Flow(factory, client, "GitHub", identity, "reauthenticate"));
        client.DefaultRequestHeaders.Remove("Cookie"); await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/auth/connections/{grant.Id}/revoke", new { })).StatusCode);
    }

    [Fact]
    public async Task Disabled_providers_missing_credentials_origin_and_locked_out_accounts_fail_closed()
    {
        using (var disabled = new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false))
        using (var client = Client(disabled))
        {
            Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/providers")).EnumerateArray());
            await Csrf(client);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/external/GitHub/start", new { })).StatusCode);
            disabled.Services.GetRequiredService<IConfiguration>()["Authentication:External:GitHub:Enabled"] = "true";
            Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/providers")).EnumerateArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/external/GitHub/start", new { })).StatusCode);
        }
        using var factory = new ExternalProviderFactory(fixture.ConnectionString); using var browser = Client(factory);
        var identity = Identity("GitHub"); Success(await Flow(factory, browser, "GitHub", identity));
        var owner = (await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid();
        await Logout(browser);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>();
            Assert.True((await users.SetLockoutEndDateAsync((await users.FindByIdAsync(owner.ToString()))!, DateTimeOffset.UtcNow.AddMinutes(15))).Succeeded);
        }
        Error(await Flow(factory, browser, "GitHub", identity), "external_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/auth/me")).StatusCode);
        using var wrongOrigin = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://wrong.example"), AllowAutoRedirect = false });
        await Csrf(wrongOrigin);
        Assert.Equal(HttpStatusCode.BadRequest, (await wrongOrigin.PostAsJsonAsync("/api/v1/auth/external/GitHub/start", new { })).StatusCode);
        var services = new ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:PublicOrigin"] = "https://localhost/", ["Authentication:External:Google:Enabled"] = "true"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => { ExternalProviders.AddSenseiExternalProviders(services.AddAuthentication(), factory.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>(), config); });
    }
}
