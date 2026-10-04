using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Windows;
using Sensei.LocalAgent.Sync;
using Sensei.Client.Contracts;
using Sensei.LocalAgent.Cli;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class NativeAgentJourneyTests(PostgreSqlFixture fixture)
{
    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> previous = [];
        public EnvironmentScope(Dictionary<string, string> values)
        {
            foreach (var (key, value) in values) { previous[key] = Environment.GetEnvironmentVariable(key); Environment.SetEnvironmentVariable(key, value); }
        }
        public void Dispose() { foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value); }
    }
    private sealed class RealFactory(string connection, string issuer) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Sensei"] = connection, ["Authentication:EnableTestOwnerHeader"] = "false", ["AgentOAuth:Issuer"] = issuer, ["AgentOAuth:WebOrigin"] = issuer, ["AgentOAuth:AllowInsecureLoopback"] = "true", ["Database:SeedDevelopmentOwner"] = "false" }));
        }
    }
    [Fact]
    public async Task Windows_native_client_uses_dynamic_callback_and_recovers_queued_import_after_server_restart()
    {
        if (!OperatingSystem.IsWindows()) return;
        const int port = 5098; var issuer = $"http://localhost:{port}/";
        // Minimal hosting reads OAuth configuration before WebApplicationFactory's late configuration callback.
        using var environment = new EnvironmentScope(new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development",
            ["AgentOAuth__Issuer"] = issuer, ["AgentOAuth__WebOrigin"] = issuer, ["AgentOAuth__AllowInsecureLoopback"] = "true",
            ["ConnectionStrings__Sensei"] = fixture.ConnectionString, ["Database__SeedDevelopmentOwner"] = "false"
        });
        var f = new RealFactory(fixture.ConnectionString, issuer); f.UseKestrel(port);
        using var hostStarter = f.CreateClient();
        // Kestrel's underlying handler follows redirects independently of the testing wrapper.
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true }) { BaseAddress = new Uri(issuer) };
        var discoveryResponse = await browser.GetAsync("/.well-known/openid-configuration");
        Assert.True(discoveryResponse.IsSuccessStatusCode, await discoveryResponse.Content.ReadAsStringAsync());
        var discovery = await discoveryResponse.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(issuer, discovery.GetProperty("issuer").GetString());
        var csrf = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); browser.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.GetProperty("token").GetString());
        var registration = await browser.PostAsJsonAsync("/api/v1/auth/register", new { email = $"native-{Guid.NewGuid():N}@example.test", password = "Correct-Horse-Password1!", displayName = "Native tester" }); Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<JsonElement>(); var owner = account.GetProperty("id").GetGuid();
        csrf = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); browser.DefaultRequestHeaders.Remove("X-CSRF-Token"); browser.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.GetProperty("token").GetString());
        Task? browserTask = null;
        var transport = new AgentConnection((uri, ct) =>
        {
            browserTask = Task.Run(async () =>
            {
                var initial = await browser.GetAsync(uri.PathAndQuery, ct);
                Assert.True(initial.StatusCode == HttpStatusCode.Redirect, $"Authorization endpoint {uri.GetLeftPart(UriPartial.Path)} returned {initial.StatusCode}");
                var ticket = QueryHelpers.ParseQuery(initial.Headers.Location!.Query)["request"].ToString();
                var consent = await browser.PostAsJsonAsync($"/api/v1/auth/cli-consent/{ticket}", new { approve = true }, ct); Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
                var grant = await browser.GetAsync((await consent.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("redirect").GetString(), ct); Assert.Equal(HttpStatusCode.Redirect, grant.StatusCode);
                Assert.True(grant.Headers.Location!.IsLoopback); Assert.NotEqual(0, grant.Headers.Location.Port);
                using var callback = new HttpClient(); var delivered = await callback.GetAsync(grant.Headers.Location, ct); Assert.True(delivered.IsSuccessStatusCode);
            }, ct);
            return browserTask;
        });
        var root = Path.Combine(Path.GetTempPath(), "sensei-native-agent-journey", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var stateRoot = Path.Combine(root, "state"); var state = new FileAgentStore(stateRoot, new WindowsContentProtector()); var local = new AgentService(state, new GitContext()); var syncStore = new SyncStore(state, stateRoot);
            var service = new SubmissionService(local, syncStore, transport); var output = new StringWriter(); var cli = new CliApp(local, new StringReader(""), output, stateRoot, service);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { Assert.Equal(0, await cli.Run(["auth", "login", "--server", issuer, "--allow-insecure-loopback"], timeout.Token)); }
            catch { if (browserTask is not null) await browserTask; throw; }
            Assert.Equal(owner, service.Profile()!.OwnerId); await browserTask!;
            var repository = Path.Combine(root, "repository"); Directory.CreateDirectory(repository); var git = new GitProcess();
            foreach (var arguments in new string[][] { ["init", "-b", "main"], ["config", "user.name", "Native fixture"], ["config", "user.email", "fixture@example.invalid"] }) Assert.Equal(0, (await git.Run(repository, arguments, default)).ExitCode);
            Directory.CreateDirectory(Path.Combine(repository, "src")); var file = Path.Combine(repository, "src", "change.txt"); await File.WriteAllTextAsync(file, "before\n");
            Assert.Equal(0, (await git.Run(repository, ["add", "src"], default)).ExitCode); Assert.Equal(0, (await git.Run(repository, ["commit", "-m", "fixture"], default)).ExitCode);
            await cli.Run(["repo", "add", "--root", repository, "--path", "src", "--setting", "PersonalProject", "--source", "diffs"], default);
            await File.WriteAllTextAsync(file, "private-working-marker\n"); await local.Poll(default); await local.Poll(default);
            var candidate = Assert.Single(local.Inbox());
            local.Edit(candidate.Id, candidate.Revision, new("Developer", "Reviewed retry behavior", "Alternative considered", "Unknown", "Not measured", "Peer review", [], "Reviewed retry behavior", "A service needed reliable retries"));
            await cli.Run(["inbox", "show", candidate.Id.ToString()], default);
            var preview = await service.Preview(candidate.Id, default); await cli.Run(["submit", candidate.Id.ToString(), "--confirm", preview.Confirmation], default);
            foreach (var path in Directory.GetFiles(stateRoot, "*.protected", SearchOption.AllDirectories)) Assert.DoesNotContain("private-working-marker", System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)));
            // A real second account cannot reroute the first account's confirmed queue item.
            var secondRegistration = await browser.PostAsJsonAsync("/api/v1/auth/register", new { email = $"second-native-{Guid.NewGuid():N}@example.test", password = "Correct-Horse-Password1!", displayName = "Second native tester" });
            Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode); var secondOwner = (await secondRegistration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            async Task RefreshCsrf()
            { var value = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); browser.DefaultRequestHeaders.Remove("X-CSRF-Token"); browser.DefaultRequestHeaders.Add("X-CSRF-Token", value.GetProperty("token").GetString()); }
            await RefreshCsrf();
            using (var loginTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60))) await cli.Run(["auth", "login", "--server", issuer, "--allow-insecure-loopback"], loginTimeout.Token);
            await browserTask!; Assert.Equal(secondOwner, service.Profile()!.OwnerId); await cli.Run(["sync"], default);
            Assert.Equal(DeliveryState.Pending, Assert.Single(service.Outbox()).State);
            Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/v1/auth/login", new { email = account.GetProperty("email").GetString(), password = "Correct-Horse-Password1!" })).StatusCode);
            await RefreshCsrf();
            using (var loginTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60))) await cli.Run(["auth", "login", "--server", issuer, "--allow-insecure-loopback"], loginTimeout.Token);
            await browserTask!; Assert.Equal(owner, service.Profile()!.OwnerId);
            await f.DisposeAsync(); await cli.Run(["sync"], default); Assert.Equal(DeliveryState.Pending, Assert.Single(service.Outbox()).State);
            await using var restarted = new RealFactory(fixture.ConnectionString, issuer); restarted.UseKestrel(port); using var restartedClient = restarted.CreateClient();
            await cli.Run(["sync"], default); var receipt = Assert.Single(service.Outbox()).Receipt!; Assert.NotNull(receipt);
            await cli.Run(["sync"], default);
            // Reauthenticate the browser account after the server restart and inspect its draft.
            var token = await restartedClient.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); restartedClient.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString());
            var email = account.GetProperty("email").GetString();
            Assert.Equal(HttpStatusCode.OK, (await restartedClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Correct-Horse-Password1!" })).StatusCode);
            var entry = await restartedClient.GetFromJsonAsync<JsonElement>($"/api/v1/experience/entries/{receipt.EntryId}"); Assert.Equal("Draft", entry.GetProperty("revisions")[0].GetProperty("approvalState").GetString());
            var imported = await restartedClient.GetFromJsonAsync<ProfessionalEventImportV1>($"/api/v1/experience/entries/{receipt.EntryId}/import"); Assert.Equal(candidate.EventId, imported!.EventId);
        }
        finally
        {
            await f.DisposeAsync();
            var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sensei-native-agent-journey")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(expected, Path.GetFullPath(root));
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }
    [Theory]
    [InlineData("state")]
    [InlineData("nonce")]
    [InlineData("cancel")]
    [InlineData("timeout")]
    public async Task Windows_client_rejects_invalid_correlation_and_cleans_up_cancelled_callbacks(string mode)
    {
        if (!OperatingSystem.IsWindows()) return;
        const int port = 5099; var issuer = $"http://localhost:{port}/";
        using var environment = new EnvironmentScope(new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development",
            ["AgentOAuth__Issuer"] = issuer, ["AgentOAuth__WebOrigin"] = issuer, ["AgentOAuth__AllowInsecureLoopback"] = "true",
            ["ConnectionStrings__Sensei"] = fixture.ConnectionString, ["Database__SeedDevelopmentOwner"] = "false"
        });
        await using var factory = new RealFactory(fixture.ConnectionString, issuer); factory.UseKestrel(port); using var starter = factory.CreateClient();
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(issuer) };
        async Task Csrf()
        { var value = await browser.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); browser.DefaultRequestHeaders.Remove("X-CSRF-Token"); browser.DefaultRequestHeaders.Add("X-CSRF-Token", value.GetProperty("token").GetString()); }
        await Csrf(); Assert.Equal(HttpStatusCode.Created, (await browser.PostAsJsonAsync("/api/v1/auth/register", new { email = $"correlation-{Guid.NewGuid():N}@example.test", password = "Correct-Horse-Password1!", displayName = "Correlation tester" })).StatusCode); await Csrf();
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Task? browserFlow = null; var callbackDelivered = false;
        var transport = new AgentConnection((uri, ct) =>
        {
            if (mode == "cancel") { cancel.Cancel(); return Task.CompletedTask; }
            if (mode == "timeout") return Task.CompletedTask;
            browserFlow = Deliver(); return Task.CompletedTask;
            async Task Deliver()
            {
            if (mode == "nonce")
            {
                var request = QueryHelpers.ParseQuery(uri.Query).ToDictionary(x => x.Key, x => (string?)x.Value.ToString()); request["nonce"] = "invalid-nonce-fixture";
                uri = new Uri(QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), request));
            }
            var initial = await browser.GetAsync(uri, ct); Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
            var ticket = QueryHelpers.ParseQuery(initial.Headers.Location!.Query)["request"].ToString();
            var consent = await browser.PostAsJsonAsync($"/api/v1/auth/cli-consent/{ticket}", new { approve = true }, ct); Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
            var grant = await browser.GetAsync((await consent.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("redirect").GetString(), ct); Assert.Equal(HttpStatusCode.Redirect, grant.StatusCode);
            var callbackUri = grant.Headers.Location!;
            if (mode == "state")
            {
                var response = QueryHelpers.ParseQuery(callbackUri.Query).ToDictionary(x => x.Key, x => (string?)x.Value.ToString()); response["state"] += "invalid";
                callbackUri = new Uri(QueryHelpers.AddQueryString(callbackUri.GetLeftPart(UriPartial.Path), response));
            }
            using var callback = new HttpClient(); using var result = await callback.GetAsync(callbackUri, ct); callbackDelivered = true;
            // The library's completion page is HTTP 200 even for rejected authentication.
            // Assert the caller's authentication outcome instead of the page's transport status.
            }
        }, mode == "timeout" ? TimeSpan.FromSeconds(2) : mode == "state" ? TimeSpan.FromSeconds(10) : null);
        if (mode == "nonce") await Assert.ThrowsAsync<OpenIddict.Abstractions.OpenIddictExceptions.ProtocolException>(() => transport.Login(issuer, true, cancel.Token));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.Login(issuer, true, cancel.Token));
        if (browserFlow is not null) { await browserFlow; Assert.True(callbackDelivered); }
        // Login only returns credentials after correlated authentication and the bearer profile check.
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
