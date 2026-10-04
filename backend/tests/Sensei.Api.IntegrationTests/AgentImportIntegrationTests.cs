using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Client.Contracts;
using Sensei.Host.Authentication;
using Sensei.Host.Integrations;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Experience.Domain;
using Microsoft.AspNetCore.Identity;
using Sensei.Modules.Identity.Infrastructure;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AgentImportIntegrationTests(PostgreSqlFixture fixture)
{
    private static HttpClient Client(SenseiWebApplicationFactory f, bool cookies = true) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = cookies });
    private static async Task<Guid> Register(HttpClient browser)
    {
        async Task Csrf() { var token = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); browser.DefaultRequestHeaders.Remove("X-CSRF-Token"); browser.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString()); }
        await Csrf(); var response = await browser.PostAsJsonAsync("/api/v1/auth/register", new { email = $"agent-{Guid.NewGuid():N}@example.test", password = "Correct-Horse-Password1!", displayName = "Agent tester" }); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await Csrf(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Authorize(HttpClient browser, HttpClient cli, string? scope = null, bool wrongVerifier = false)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var uri = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?> { ["client_id"] = AgentOAuth.ClientId, ["redirect_uri"] = "http://localhost:51111/", ["response_type"] = "code", ["scope"] = scope ?? string.Join(' ', AgentOAuth.AllowedScopes), ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = "fixture-state", ["nonce"] = "fixture-nonce" });
        var initial = await browser.GetAsync(uri); Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
        var ticket = QueryHelpers.ParseQuery(initial.Headers.Location!.Query)["request"].ToString();
        var csrf = browser.DefaultRequestHeaders.GetValues("X-CSRF-Token").Single(); browser.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync($"/api/v1/auth/cli-consent/{ticket}", new { approve = true })).StatusCode);
        browser.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        var consent = await browser.PostAsJsonAsync($"/api/v1/auth/cli-consent/{ticket}", new { approve = true }); Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        var grant = await browser.GetAsync((await consent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirect").GetString()); Assert.Equal(HttpStatusCode.Redirect, grant.StatusCode);
        var parameters = QueryHelpers.ParseQuery(grant.Headers.Location!.Query); Assert.Equal("fixture-state", parameters["state"]);
        var token = await cli.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["client_id"] = AgentOAuth.ClientId, ["redirect_uri"] = "http://localhost:51111/", ["code"] = parameters["code"].ToString(), ["code_verifier"] = wrongVerifier ? WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)) : verifier }));
        Assert.Equal(wrongVerifier ? HttpStatusCode.BadRequest : HttpStatusCode.OK, token.StatusCode);
        return await token.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static ProfessionalEventImportV1 Payload(Guid eventId, params Guid[] concepts) => new(eventId, 1, "PersonalProject", new(2026, 10, 3), "Investigated retry behavior", "A service needed reliable retries", "Developer", "Investigated the failure", "Considered retry alternatives", "Outcome remains unknown", "Not measured", "Peer review", concepts, "client_reported");

    [Fact]
    public async Task OAuth_import_replay_is_atomic_owner_bound_and_unscored()
    {
        using var f = new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false); using var browser = Client(f); using var cli = Client(f, false); var owner = await Register(browser);
        var conceptResponse = await browser.PostAsJsonAsync("/api/v1/learning/concepts", new { key = $"import-{Guid.NewGuid():N}", name = "Retry reasoning", description = "Fixture", locale = "en", difficulty = "Beginner" }); Assert.Equal(HttpStatusCode.Created, conceptResponse.StatusCode);
        var concept = (await conceptResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var tokens = await Authorize(browser, cli); cli.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await cli.GetAsync("/api/v1/agent/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cli.GetAsync("/api/v1/experience/entries")).StatusCode);
        var payload = Payload(Guid.NewGuid(), concept);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => cli.PostAsJsonAsync("/api/v1/experience/imports", payload)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.All(responses, x => Assert.True(x.IsSuccessStatusCode));
        var receipts = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<ImportReceipt>(ImportContract.Json))); Assert.Single(receipts.Select(x => x!.EntryId).Distinct());
        var receipt = receipts[0]!;
        Assert.Equal(HttpStatusCode.Conflict, (await cli.PostAsJsonAsync("/api/v1/experience/imports", payload with { Actions = "Different payload" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cli.PostAsJsonAsync("/api/v1/experience/imports", Payload(Guid.NewGuid(), Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cli.PostAsync("/api/v1/experience/imports", new StringContent("{\"repositoryUrl\":\"private\"}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cli.PostAsJsonAsync("/api/v1/experience/imports", payload with { EventId = Guid.NewGuid(), Actions = "password=private-token" })).StatusCode);
        using var b = Client(f); await Register(b); using var other = Client(f, false); var otherTokens = await Authorize(b, other); other.DefaultRequestHeaders.Authorization = new("Bearer", otherTokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/experience/imports/{payload.EventId}")).StatusCode);
        var entry = await browser.GetAsync($"/api/v1/experience/entries/{receipt.EntryId}"); var body = await entry.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Draft", body.GetProperty("revisions")[0].GetProperty("approvalState").GetString());
        using var approve = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/experience/entries/{receipt.EntryId}/revisions/1/approval"); approve.Headers.IfMatch.ParseAdd(entry.Headers.ETag!.ToString()); Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(approve)).StatusCode);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        Assert.Equal(1, await db.Set<ProfessionalImport>().CountAsync(x => x.OwnerId == owner && x.EventId == payload.EventId));
        Assert.Equal(0, await db.Set<KnowledgeContribution>().CountAsync(x => x.OwnerId == owner));
        Assert.Equal(0, await db.Set<KnowledgeState>().CountAsync(x => x.OwnerId == owner));
        var observation = await db.Set<EvidenceObservation>().SingleAsync(x => x.OwnerId == owner && x.SourceId == payload.EventId); Assert.Equal(EvidenceSignalKind.ProfessionalExposure, observation.SignalKind); Assert.Equal(AssistanceLevel.Unspecified, observation.Assistance);
        Assert.Equal(0, await db.Set<SelfReviewAllowance>().CountAsync(x => x.OwnerId == owner));
        var inspected = await browser.GetAsync($"/api/v1/evidence/observations/{observation.Id}");
        using var changeStatus = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/evidence/observations/{observation.Id}/status") { Content = JsonContent.Create(new { status = "Withdrawn" }) };
        changeStatus.Headers.IfMatch.ParseAdd(inspected.Headers.ETag!.ToString()); Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(changeStatus)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Set<KnowledgeContribution>().CountAsync(x => x.OwnerId == owner));
        Assert.Equal(0, await db.Set<SelfReviewAllowance>().CountAsync(x => x.OwnerId == owner));
        Assert.All(await db.Set<KnowledgeState>().Where(x => x.OwnerId == owner).ToArrayAsync(), x => Assert.Null(x.Estimate));
        Assert.Equal(1, await db.Set<ExperienceEntry>().CountAsync(x => x.OwnerId == owner)); // Rejected imports left no partial drafts.
        Assert.Equal(receipt.EntryId, (await (await cli.PostAsJsonAsync("/api/v1/experience/imports", payload)).Content.ReadFromJsonAsync<ImportReceipt>(ImportContract.Json))!.EntryId);
    }
    [Fact]
    public async Task PKCE_consent_csrf_scope_rotation_and_revocation_are_enforced()
    {
        using var f = new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false); using var browser = Client(f); using var cli = Client(f, false); await Register(browser);
        await Authorize(browser, cli, wrongVerifier: true);
        var tokens = await Authorize(browser, cli, scope: "openid profile offline_access professional-events:read"); cli.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await cli.PostAsJsonAsync("/api/v1/experience/imports", Payload(Guid.NewGuid()))).StatusCode);
        var refresh = tokens.GetProperty("refresh_token").GetString()!;
        var exchange = await cli.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"] = "refresh_token", ["client_id"] = AgentOAuth.ClientId, ["refresh_token"] = refresh })); Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var updated = await exchange.Content.ReadFromJsonAsync<JsonElement>(); Assert.NotEqual(refresh, updated.GetProperty("refresh_token").GetString());
        var revoked = await cli.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string,string> { ["client_id"] = AgentOAuth.ClientId, ["token"] = updated.GetProperty("refresh_token").GetString()! })); Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        cli.DefaultRequestHeaders.Authorization = new("Bearer", updated.GetProperty("access_token").GetString()); Assert.Equal(HttpStatusCode.Unauthorized, (await cli.GetAsync("/api/v1/agent/profile")).StatusCode);
        browser.DefaultRequestHeaders.Remove("X-CSRF-Token"); browser.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
    }
    [Fact]
    public async Task Redirects_plain_PKCE_password_recovery_and_absolute_grant_expiry_are_rejected()
    {
        using var f = new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false); using var browser = Client(f); using var cli = Client(f, false); var owner = await Register(browser);
        var query = new Dictionary<string, string?> { ["client_id"] = AgentOAuth.ClientId, ["redirect_uri"] = "https://unregistered.example/callback", ["response_type"] = "code", ["scope"] = "openid professional-events:read", ["code_challenge"] = new string('x', 43), ["code_challenge_method"] = "S256" };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", query))).StatusCode);
        query["redirect_uri"] = "http://localhost:51111/"; query["code_challenge_method"] = "plain";
        var plain = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", query));
        Assert.True(plain.StatusCode == HttpStatusCode.BadRequest || plain.Headers.Location?.Query.Contains("error=") == true);
        var tokens = await Authorize(browser, cli); cli.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        Assert.InRange(tokens.GetProperty("expires_in").GetInt32(), 290, 300);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>(); var account = (await users.FindByIdAsync(owner.ToString()))!;
            var recovery = await users.GeneratePasswordResetTokenAsync(account); Assert.True((await users.ResetPasswordAsync(account, recovery, "Recovered-Horse-Password2!")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await cli.GetAsync("/api/v1/agent/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cli.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = AgentOAuth.ClientId, ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()! }))).StatusCode);
        // The stamp check protects CLI grants independently of the browser cookie's validation interval.
        using var freshBrowser = Client(f); await Register(freshBrowser); var expiring = await Authorize(freshBrowser, cli);
        cli.DefaultRequestHeaders.Authorization = new("Bearer", expiring.GetProperty("access_token").GetString());
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var grant = await db.Set<CliGrant>().OrderByDescending(x => x.CreatedAt).FirstAsync(); grant.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await cli.GetAsync("/api/v1/agent/profile")).StatusCode);
    }
}
