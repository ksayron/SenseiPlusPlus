using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sensei.Host.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Sensei.Api.IntegrationTests;

internal sealed record ProviderIdentity(string Id, string Email, string Name = "Provider learner", string Username = "provider-learner", bool Verified = true);
internal sealed record ProviderCode(ProviderIdentity Identity, string Challenge, string? Nonce, string RedirectUri);

internal sealed class FakeExternalProviders : HttpMessageHandler
{
    private readonly RSA rsa = RSA.Create(2048);
    public RsaSecurityKey SigningKey { get; }
    private readonly ConcurrentDictionary<string, ProviderCode> codes = new();
    private readonly ConcurrentDictionary<string, ProviderIdentity> tokens = new();
    public bool Outage { get; set; }
    public bool WrongNonce { get; set; }
    public bool WrongAudience { get; set; }
    public bool WrongIssuer { get; set; }
    public bool WrongSignature { get; set; }
    public string? LastVerifier { get; private set; }
    public Exception? LastFailure { get; set; }

    public FakeExternalProviders() => SigningKey = new RsaSecurityKey(rsa) { KeyId = "fake-provider-key" };
    public string Authorize(Uri authorization, ProviderIdentity identity)
    {
        var query = QueryHelpers.ParseQuery(authorization.Query);
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.Equal("select_account", query["prompt"].ToString());
        var code = Guid.NewGuid().ToString("N");
        codes[code] = new ProviderCode(identity, query["code_challenge"].ToString(), query.TryGetValue("nonce", out var nonce) ? nonce.FirstOrDefault() : null, query["redirect_uri"].ToString());
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Outage) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        if (request.RequestUri!.AbsolutePath is "/login/oauth/access_token" or "/token")
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (!codes.TryRemove(form["code"].ToString(), out var issued)) return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
            LastVerifier = form["code_verifier"].ToString();
            Assert.NotEmpty(LastVerifier);
            var hash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(LastVerifier)));
            if (hash != issued.Challenge || form["redirect_uri"].ToString() != issued.RedirectUri) return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
            var token = "fake-" + Guid.NewGuid().ToString("N"); tokens[token] = issued.Identity;
            if (request.RequestUri.Host == "github.com") return Json(new { access_token = token, token_type = "bearer", scope = "read:user,user:email" });
            var payload = new JwtPayload(WrongIssuer ? "https://wrong.example" : "https://accounts.google.com", WrongAudience ? "wrong-client" : "fake-google-client", null,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow)
            {
                ["sub"] = issued.Identity.Id, ["email"] = issued.Identity.Email, ["email_verified"] = issued.Identity.Verified,
                ["name"] = issued.Identity.Name, ["nonce"] = WrongNonce ? "wrong-nonce" : issued.Nonce!
            };
            using var wrongRsa = WrongSignature ? RSA.Create(2048) : null;
            var key = WrongSignature ? new RsaSecurityKey(wrongRsa!) { KeyId = SigningKey.KeyId } : SigningKey;
            var jwt = new JwtSecurityToken(new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256)), payload);
            return Json(new { access_token = token, token_type = "Bearer", expires_in = 300, id_token = new JwtSecurityTokenHandler().WriteToken(jwt) });
        }
        if (!tokens.TryGetValue(request.Headers.Authorization?.Parameter ?? "", out var identity)) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        if (request.RequestUri.AbsolutePath == "/user") return Json(new { id = long.Parse(identity.Id), login = identity.Username, name = identity.Name });
        if (request.RequestUri.AbsolutePath == "/user/emails") return Json(new[] { new { email = identity.Email, primary = true, verified = identity.Verified } });
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
}

internal sealed class ExternalProviderFactory(string connectionString, bool google = true, bool github = true) : SenseiWebApplicationFactory(connectionString, testOwners: false)
{
    public FakeExternalProviders Provider { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var values = new Dictionary<string, string?>
        {
            ["Authentication:External:PublicOrigin"] = "https://localhost/",
            ["Authentication:External:Google:Enabled"] = google.ToString(),
            ["Authentication:External:Google:ClientId"] = "fake-google-client",
            ["Authentication:External:Google:ClientSecret"] = "fake-google-secret",
            ["Authentication:External:GitHub:Enabled"] = github.ToString(),
            ["Authentication:External:GitHub:ClientId"] = "fake-github-client",
            ["Authentication:External:GitHub:ClientSecret"] = "fake-github-secret"
        };
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));
        builder.ConfigureTestServices(services =>
        {
            // Minimal-host ConfigureAppConfiguration runs after Program's conditional provider registration.
            // Register the real handlers here with the same test config, then replace only their backchannels.
            services.AddAuthentication().AddSenseiExternalProviders(new ExternalTestEnvironment(), new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            services.PostConfigure<OAuthOptions>("GitHub", options => options.Backchannel = new HttpClient(Provider));
            services.PostConfigure<OpenIdConnectOptions>("Google", options =>
            {
                options.Backchannel = new HttpClient(Provider);
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://accounts.google.com", AuthorizationEndpoint = "https://accounts.google.com/authorize", TokenEndpoint = "https://accounts.google.com/token"
                };
                options.Configuration.SigningKeys.Add(Provider.SigningKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                var remoteFailure = options.Events.OnRemoteFailure;
                options.Events.OnRemoteFailure = c => { Provider.LastFailure = c.Failure; return remoteFailure(c); };
            });
        });
    }
}

internal sealed class ExternalTestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "Sensei.Host";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
