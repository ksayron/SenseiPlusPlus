using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using Sensei.Client.Contracts;
using Sensei.LocalAgent.Core;

namespace Sensei.LocalAgent.Sync;

public interface IAgentTransport
{
    Task<Connection> Login(string server, bool allowInsecure, CancellationToken ct);
    Task<Connection> Refresh(Connection connection, CancellationToken ct);
    Task Logout(Connection connection, CancellationToken ct);
    Task<ImportReceipt?> Receipt(Connection connection, Guid eventId, CancellationToken ct);
    Task<ImportReceipt> Import(Connection connection, ProfessionalEventImportV1 payload, CancellationToken ct);
    Task<AgentConcept[]> Concepts(Connection connection, CancellationToken ct);
}
public sealed class TransportException(string code, HttpStatusCode? status = null) : Exception(code)
{
    public string Code { get; } = code;
    public HttpStatusCode? Status { get; } = status;
}
public sealed class AgentConnection : IAgentTransport
{
    private readonly Func<Uri, CancellationToken, Task>? browserLauncher;
    private readonly TimeSpan authenticationTimeout = TimeSpan.FromMinutes(5);
    public AgentConnection() { }
    internal AgentConnection(Func<Uri, CancellationToken, Task> browserLauncher, TimeSpan? authenticationTimeout = null)
    { this.browserLauncher = browserLauncher; this.authenticationTimeout = authenticationTimeout ?? TimeSpan.FromMinutes(5); }
    public static Uri Server(string value, bool allowInsecure)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/" ||
            uri.Scheme != "https" && !(allowInsecure && uri.Scheme == "http" && uri.IsLoopback)) throw new AgentException("https_server_required");
        return uri;
    }
    private IHost Host(Connection? connection, Uri issuer)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenIddict().AddClient(o =>
        {
            o.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().DisableTokenStorage().AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            // Authorization-code and rotating refresh requests must never be retried invisibly.
            o.UseSystemNetHttp().SetHttpResiliencePipeline(_ => { });
            o.UseSystemIntegration();
            if (browserLauncher is not null)
            {
                var descriptor = OpenIddict.Client.SystemIntegration.OpenIddictClientSystemIntegrationHandlers.Authentication.LaunchSystemBrowser.Descriptor;
                o.RemoveEventHandler(descriptor);
                o.AddEventHandler<OpenIddictClientEvents.ApplyAuthorizationRequestContext>(handler => handler.SetOrder(descriptor.Order).UseInlineHandler(async context =>
                {
                    using var query = new FormUrlEncodedContent(context.Request.GetParameters().Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString() ?? "")));
                    await browserLauncher(new Uri(context.AuthorizationEndpoint! + "?" + await query.ReadAsStringAsync(context.CancellationToken)), context.CancellationToken);
                    context.HandleRequest();
                }));
            }
            o.AddRegistration(new OpenIddictClientRegistration
            {
                RegistrationId = "sensei", Issuer = issuer, ClientId = "sensei-local-agent", RedirectUri = new Uri("http://localhost/"),
                Scopes = { "openid", "profile", "offline_access", "professional-events:write", "professional-events:read", "concepts:read" }
            });
        });
        return builder.Build();
    }
    public async Task<Connection> Login(string server, bool allowInsecure, CancellationToken ct)
    {
        var issuer = Server(server, allowInsecure);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(authenticationTimeout);
        using var host = Host(null, issuer); await host.StartAsync(timeout.Token);
        try
        {
            var client = host.Services.GetRequiredService<OpenIddictClientService>();
            var challenge = await client.ChallengeInteractivelyAsync(new() { RegistrationId = "sensei", CancellationToken = timeout.Token });
            var result = await client.AuthenticateInteractivelyAsync(new() { Nonce = challenge.Nonce, CancellationToken = timeout.Token });
            var provisional = new Connection(issuer.AbsoluteUri, Guid.Empty, "", result.BackchannelAccessToken!, result.RefreshToken!, DateTimeOffset.UtcNow.AddMinutes(4), allowInsecure);
            var profile = await Get<AgentProfile>(provisional, "api/v1/agent/profile", timeout.Token);
            return provisional with { OwnerId = profile!.OwnerId, DisplayName = profile.DisplayName };
        }
        finally { await host.StopAsync(CancellationToken.None); }
    }
    public async Task<Connection> Refresh(Connection connection, CancellationToken ct)
    {
        using var host = Host(connection, Server(connection.Issuer, connection.AllowInsecureLoopback)); await host.StartAsync(ct);
        try
        {
            var result = await host.Services.GetRequiredService<OpenIddictClientService>().AuthenticateWithRefreshTokenAsync(new()
            { RegistrationId = "sensei", RefreshToken = connection.RefreshToken, CancellationToken = ct });
            var updated = connection with { AccessToken = result.AccessToken!, RefreshToken = result.RefreshToken ?? connection.RefreshToken, AccessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(4) };
            var profile = await Get<AgentProfile>(updated, "api/v1/agent/profile", ct);
            if (profile!.OwnerId != connection.OwnerId) throw new AgentException("account_changed");
            return updated;
        }
        finally { await host.StopAsync(CancellationToken.None); }
    }
    public async Task Logout(Connection connection, CancellationToken ct)
    {
        using var client = Http(connection);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = "sensei-local-agent", ["token"] = connection.RefreshToken, ["token_type_hint"] = "refresh_token" });
        using var response = await client.PostAsync("connect/revoke", body, ct);
        if (!response.IsSuccessStatusCode) throw new TransportException("remote_revocation_failed", response.StatusCode);
    }
    public Task<ImportReceipt?> Receipt(Connection c, Guid id, CancellationToken ct) => Get<ImportReceipt>(c, $"api/v1/experience/imports/{id}", ct, missing: true);
    public async Task<AgentConcept[]> Concepts(Connection c, CancellationToken ct) => await Get<AgentConcept[]>(c, "api/v1/agent/concepts", ct) ?? [];
    public async Task<ImportReceipt> Import(Connection c, ProfessionalEventImportV1 payload, CancellationToken ct)
    {
        using var client = Http(c);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/experience/imports") { Content = new ByteArrayContent(ImportContract.Bytes(payload)) };
        request.Content.Headers.ContentType = new("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        return await Read<ImportReceipt>(response, ct) ?? throw new TransportException("empty_receipt");
    }
    private static HttpClient Http(Connection c) => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    { BaseAddress = Server(c.Issuer, c.AllowInsecureLoopback), Timeout = TimeSpan.FromSeconds(30), DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken) } };
    private static async Task<T?> Get<T>(Connection c, string path, CancellationToken ct, bool missing = false)
    {
        using var client = Http(c); using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (missing && response.StatusCode == HttpStatusCode.NotFound) return default;
        return await Read<T>(response, ct);
    }
    private static async Task<T?> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) throw new TransportException("server_rejected_request", response.StatusCode);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var buffer = new MemoryStream(); var bytes = new byte[8192];
        while (true) { var count = await stream.ReadAsync(bytes, deadline.Token); if (count == 0) break; if (buffer.Length + count > 1024 * 1024) throw new TransportException("response_too_large"); buffer.Write(bytes, 0, count); }
        try { return JsonSerializer.Deserialize<T>(buffer.ToArray(), ImportContract.Json); }
        catch (JsonException) { throw new TransportException("invalid_server_response"); }
    }
}
