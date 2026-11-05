using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Infrastructure;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Application;
using Sensei.Host.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Security.Claims;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthenticationIntegrationTests(PostgreSqlFixture fixture)
{
    private const string Password = "Correct-Horse-Password1!";
    private SenseiWebApplicationFactory Factory() => new(fixture.ConnectionString, testOwners: false);
    private static HttpClient Client(SenseiWebApplicationFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString());
    }
    private static async Task<JsonElement> Register(HttpClient client, string? email = null)
    {
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = email ?? $"auth-{Guid.NewGuid():N}@example.test", password = Password, displayName = "Private learner" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("sensei.session=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase) && x.Contains("secure", StringComparison.OrdinalIgnoreCase));
        await Csrf(client);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> Create(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Real_auth_requires_cookies_ignores_owner_selector_and_blocks_csrf_and_legacy_user_creation()
    {
        using var factory = Factory(); using var anonymous = Client(factory);
        anonymous.DefaultRequestHeaders.Add("X-Owner-Id", Guid.NewGuid().ToString());
        foreach (var path in new[] { "/api/v1/work/episodes", "/api/v1/evidence/observations", "/api/v1/experience/entries", "/api/v1/learning/sessions", "/api/v1/learning/concepts", "/api/v1/identity/users" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        var badCsrf = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { email = "csrf@example.test", password = Password, displayName = "CSRF" });
        Assert.Equal(HttpStatusCode.BadRequest, badCsrf.StatusCode);
        Assert.Equal("antiforgery_failed", (await badCsrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var client = Client(factory); var account = await Register(client);
        var owner = account.GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("X-Owner-Id", Guid.NewGuid().ToString());
        var episode = await Create(client, "/api/v1/work/episodes", new { title = "Private context", setting = "PersonalProject", eventDate = "2026-10-03", role = "Developer", summary = "Only this account" });
        Assert.Equal(owner, episode.GetProperty("ownerId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/identity/users", new { email = "unowned@example.test", displayName = "Other", uiLocale = "en", answerLanguage = "en", timeZone = "UTC" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.GetProperty("email").GetString(), password = Password })).StatusCode);
        Assert.Equal(owner, (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Two_real_accounts_cannot_read_mutate_approve_or_reference_each_others_records()
    {
        using var factory = Factory(); using var a = Client(factory); using var b = Client(factory);
        var accountA = await Register(a); var accountB = await Register(b);
        var concept = await Create(a, "/api/v1/learning/concepts", new { key = $"auth-{Guid.NewGuid():N}", name = "Isolation", description = "Security test", locale = "en", difficulty = "Beginner" });
        var conceptId = concept.GetProperty("id").GetGuid();
        var episode = await Create(a, "/api/v1/work/episodes", new { title = "Private searchable title", setting = "PersonalProject", eventDate = "2026-10-03", role = "Owner", summary = "Private supplied source" });
        var entry = await Create(a, "/api/v1/experience/entries", new { title = "Private story", setting = "PersonalProject", context = "Private context", role = "Developer", actions = "Investigated", alternatives = "", outcome = "", impactState = "Unknown", conceptIds = new[] { conceptId } });
        var observation = await Create(a, "/api/v1/evidence/observations", new { conceptId, aspect = "Exposure", signalKind = "ProfessionalExposure", sourceKind = "Declaration", sourceId = Guid.NewGuid(), assistance = "None", conditions = "Private" });
        foreach (var (path, resource) in new[] { ("work/episodes", episode), ("experience/entries", entry), ("evidence/observations", observation) })
        {
            var id = resource.GetProperty("id").GetGuid();
            b.DefaultRequestHeaders.Remove("X-Owner-Id"); b.DefaultRequestHeaders.Add("X-Owner-Id", accountA.GetProperty("id").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/{path}/{id}")).StatusCode);
            using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/{path}/{id}");
            delete.Headers.TryAddWithoutValidation("If-Match", $"\"{resource.GetProperty("versionToken").GetString()}\"");
            Assert.Equal(HttpStatusCode.NotFound, (await b.SendAsync(delete)).StatusCode);
            var list = await b.GetFromJsonAsync<JsonElement>($"/api/v1/{path}?search=Private");
            Assert.Empty(list.GetProperty("items").EnumerateArray());
        }
        using var approval = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/experience/entries/{entry.GetProperty("id").GetGuid()}/revisions/1/approval");
        approval.Headers.TryAddWithoutValidation("If-Match", $"\"{entry.GetProperty("versionToken").GetString()}\"");
        Assert.Equal(HttpStatusCode.NotFound, (await b.SendAsync(approval)).StatusCode);
        var foreignSource = await b.PostAsJsonAsync("/api/v1/evidence/observations", new { conceptId, aspect = "Forged", signalKind = "ProfessionalExposure", sourceKind = "ApprovedExperienceRevision", sourceId = entry.GetProperty("id").GetGuid(), assistance = "None", conditions = "Cross owner" });
        Assert.Equal(HttpStatusCode.NotFound, foreignSource.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/identity/users/{accountA.GetProperty("id").GetGuid()}")).StatusCode);
        var profiles = await b.GetFromJsonAsync<JsonElement>("/api/v1/identity/users");
        Assert.Equal(accountB.GetProperty("id").GetGuid(), profiles.GetProperty("items")[0].GetProperty("id").GetGuid());
        b.DefaultRequestHeaders.Add("X-Expected-Owner", accountA.GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await b.GetAsync("/api/v1/learning/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.PostAsJsonAsync("/api/v1/work/episodes", new { title = "Stale tab private input", setting = "PersonalProject", eventDate = "2026-10-03", role = "Owner", summary = "Must not be sent to the new account" })).StatusCode);
        b.DefaultRequestHeaders.Remove("X-Expected-Owner");
        // Future surfaces must remain unavailable, including when a caller spoofs the selector.
        foreach (var path in new[] { "/api/v1/ai/jobs", "/api/v1/identity/export" }) Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        Assert.NotNull(await db.Set<LoginAccount>().SingleAsync(x => x.Id == accountA.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Passwords_are_hashed_lockout_is_enforced_and_old_development_profiles_cannot_be_claimed()
    {
        using var factory = Factory(); using var client = Client(factory);
        var account = await Register(client); var email = account.GetProperty("email").GetString();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            var credential = await db.Set<LoginAccount>().SingleAsync(x => x.Id == account.GetProperty("id").GetGuid());
            Assert.NotNull(credential.PasswordHash); Assert.DoesNotContain(Password, credential.PasswordHash);
            db.Add(UserAccount.Create("legacy-" + email, "Legacy", "en", "en", "UTC", DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        }
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "legacy-" + email, password = Password, displayName = "Cannot claim" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/v1/auth/reauthentication")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/reauthenticate", new { password = Password })).StatusCode);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task Real_accounts_isolate_pinned_sessions_items_attempts_assistance_goals_and_activity()
    {
        using var factory = Factory(); using var a = Client(factory); using var b = Client(factory);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<LearningFixtures>().Seed();
        var account = await Register(a); await Register(b);
        var topics = await a.GetFromJsonAsync<JsonElement>("/api/v1/learning/topics?limit=100");
        var concept = topics.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("concept").GetProperty("key").GetString() == "value-reference-fixture").GetProperty("concept").GetProperty("id").GetGuid();
        var start = await Create(a, "/api/v1/learning/sessions", new { operationId = Guid.NewGuid(), setup = new { count = 3, conceptId = concept, types = new[] { "Prediction" } } });
        var session = start.GetProperty("session"); var id = session.GetProperty("id").GetGuid(); var item = session.GetProperty("items")[0].GetProperty("id").GetGuid();
        using var attemptRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/learning/sessions/{id}/items/{item}/attempts") { Content = JsonContent.Create(new { operationId = Guid.NewGuid(), answer = new { selected = new[] { "a" } } }) };
        attemptRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{session.GetProperty("versionToken").GetString()}\"");
        var submitted = await a.SendAsync(attemptRequest); Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var attemptId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcomeId").GetGuid();
        b.DefaultRequestHeaders.Add("X-Owner-Id", account.GetProperty("id").GetString());
        foreach (var path in new[] { $"sessions/{id}", $"sessions/{id}/summary", $"sessions/{id}/items/{item}", $"sessions/{id}/items/{item}/assistance", $"attempts/{attemptId}" })
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync("/api/v1/learning/" + path)).StatusCode);
        using var mutate = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/learning/sessions/{id}/pause") { Content = JsonContent.Create(new { operationId = Guid.NewGuid() }) };
        mutate.Headers.TryAddWithoutValidation("If-Match", $"\"{session.GetProperty("versionToken").GetString()}\"");
        Assert.Equal(HttpStatusCode.NotFound, (await b.SendAsync(mutate)).StatusCode);
        foreach (var path in new[] { "learning/sessions", "learning/activity", "learning/goals", "evidence/observations" })
            Assert.Empty((await b.GetFromJsonAsync<JsonElement>("/api/v1/" + path)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/v1/evidence/observations", new { conceptId = concept, aspect = "Forged source", signalKind = "Scenario", sourceKind = "Attempt", sourceId = attemptId, assistance = "None", conditions = "Cannot reference another owner attempt" })).StatusCode);
    }

    [Fact]
    public async Task Recent_authentication_rejects_missing_expired_or_future_password_confirmation()
    {
        using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var timestamp in new long?[] { null, now - 600, now + 600 })
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], IdentityConstants.ApplicationScheme);
            if (timestamp is not null) identity.AddClaim(new Claim(AuthenticationSetup.AuthTimeClaim, timestamp.Value.ToString(CultureInfo.InvariantCulture)));
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), null, AuthenticationSetup.RecentPolicy)).Succeeded);
        }
    }

    [Fact]
    public async Task Local_recovery_retains_owner_data_and_revokes_existing_cookies()
    {
        using var factory = Factory(); using var client = Client(factory);
        var account = await Register(client); var id = account.GetProperty("id").GetGuid();
        await Create(client, "/api/v1/work/episodes", new { title = "Retain after recovery", setting = "PersonalProject", eventDate = "2026-10-03", role = "Owner", summary = "Recovery keeps data" });
        const string newPassword = "New-Local-Password2!";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<SenseiDbContext>().Set<UserAccount>().SingleAsync(x => x.Id == id);
            Assert.True((await LocalAccountRecovery.RecoverAsync(scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>(), profile, newPassword)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.GetProperty("email").GetString(), password = newPassword })).StatusCode);
        Assert.Equal(id, (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/v1/work/episodes")).GetProperty("items").EnumerateArray());
    }
}
