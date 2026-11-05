using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sensei.Host.Authentication;

public sealed record ExternalProviderRegistration(Uri? Origin, IReadOnlyCollection<string> Providers);

public static class ExternalProviders
{
    public const string VerifiedEmail = "sensei:provider-email-verified";
    public const string Username = "sensei:provider-username";
    public const string FlowKey = "sensei:external-flow";
    public const string ProviderKey = "sensei:external-provider";
    public static readonly string[] Names = ["Google", "GitHub"];

    public static string? Canonical(string provider) => Names.FirstOrDefault(x => x.Equals(provider, StringComparison.OrdinalIgnoreCase));
    private static bool Configured(IConfiguration config, string provider) => config.GetValue<bool>($"Authentication:External:{provider}:Enabled");
    public static bool Enabled(HttpContext c, string provider) => c.RequestServices.GetRequiredService<ExternalProviderRegistration>().Providers.Contains(provider);

    public static Uri? PublicOrigin(IConfiguration config) => Uri.TryCreate(config["Authentication:External:PublicOrigin"], UriKind.Absolute, out var origin) ? origin : null;
    public static bool OriginMatches(HttpContext c) => c.RequestServices.GetRequiredService<ExternalProviderRegistration>().Origin is { } origin &&
        string.Equals(origin.Scheme, c.Request.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(origin.Authority, c.Request.Host.Value, StringComparison.OrdinalIgnoreCase);

    public static void AddSenseiExternalProviders(this AuthenticationBuilder auth, IHostEnvironment env, IConfiguration config)
    {
        var enabled = Names.Where(x => Configured(config, x)).ToArray();
        // Match the handlers registered at startup; hot reload must not advertise nonexistent sign-in methods.
        auth.Services.Replace(ServiceDescriptor.Singleton(new ExternalProviderRegistration(PublicOrigin(config), enabled)));
        if (enabled.Length == 0) return;
        var origin = PublicOrigin(config);
        var insecure = env.IsDevelopment() && config.GetValue<bool>("Authentication:External:AllowInsecureLoopback") && origin?.IsLoopback == true;
        if (origin is null || !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            origin.Scheme != "https" && !(insecure && origin.Scheme == "http"))
            throw new InvalidOperationException("External authentication requires an HTTPS public origin, or explicitly enabled Development loopback HTTP.");
        foreach (var provider in enabled)
            if (string.IsNullOrWhiteSpace(config[$"Authentication:External:{provider}:ClientId"]) || string.IsNullOrWhiteSpace(config[$"Authentication:External:{provider}:ClientSecret"]))
                throw new InvalidOperationException($"Enabled external provider {provider} requires client credentials.");

        if (enabled.Contains("Google")) auth.AddOpenIdConnect("Google", "Google", o =>
        {
            o.SignInScheme = IdentityConstants.ExternalScheme;
            o.Authority = "https://accounts.google.com";
            o.ClientId = config["Authentication:External:Google:ClientId"];
            o.ClientSecret = config["Authentication:External:Google:ClientSecret"];
            o.CallbackPath = "/signin-google";
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.ResponseMode = OpenIdConnectResponseMode.Query;
            o.UsePkce = true;
            o.MapInboundClaims = false;
            o.SaveTokens = false;
            o.Scope.Clear();
            foreach (var scope in new[] { "openid", "email", "profile" }) o.Scope.Add(scope);
            o.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
            o.BackchannelTimeout = TimeSpan.FromSeconds(20);
            o.CorrelationCookie.SameSite = SameSiteMode.Lax;
            o.NonceCookie.SameSite = SameSiteMode.Lax;
            o.CorrelationCookie.SecurePolicy = insecure ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.NonceCookie.SecurePolicy = o.CorrelationCookie.SecurePolicy;
            o.Events = new OpenIdConnectEvents
            {
                OnTicketReceived = c => { c.Properties!.Items[ProviderKey] = "Google"; return Task.CompletedTask; },
                OnRedirectToIdentityProvider = c =>
                {
                    c.ProtocolMessage.Prompt = "select_account";
                    return Task.CompletedTask;
                },
                OnTokenValidated = c =>
                {
                    var identity = (ClaimsIdentity)c.Principal!.Identity!;
                    var subject = c.Principal.FindFirstValue("sub");
                    if (string.IsNullOrEmpty(subject) || subject.Length > 128) { c.Fail("Invalid provider identity."); return Task.CompletedTask; }
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
                    identity.AddClaim(new Claim(ClaimTypes.Name, c.Principal.FindFirstValue("name") ?? "Google account"));
                    var email = c.Principal.FindFirstValue("email");
                    if (!string.IsNullOrEmpty(email)) identity.AddClaim(new Claim(ClaimTypes.Email, email));
                    identity.AddClaim(new Claim(VerifiedEmail, string.Equals(c.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false"));
                    return Task.CompletedTask;
                },
                OnRemoteFailure = Failure,
                OnAccessDenied = c => { c.HandleResponse(); c.Response.Redirect("/auth/result?error=external_cancelled"); return Task.CompletedTask; }
            };
        });

        if (enabled.Contains("GitHub")) auth.AddOAuth("GitHub", "GitHub", o =>
        {
            o.SignInScheme = IdentityConstants.ExternalScheme;
            o.ClientId = config["Authentication:External:GitHub:ClientId"]!;
            o.ClientSecret = config["Authentication:External:GitHub:ClientSecret"]!;
            o.CallbackPath = "/signin-github";
            o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            o.TokenEndpoint = "https://github.com/login/oauth/access_token";
            o.UserInformationEndpoint = "https://api.github.com/user";
            o.Scope.Add("read:user"); o.Scope.Add("user:email");
            o.UsePkce = true;
            o.SaveTokens = false;
            o.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
            o.CorrelationCookie.SameSite = SameSiteMode.Lax;
            o.CorrelationCookie.SecurePolicy = insecure ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.BackchannelTimeout = TimeSpan.FromSeconds(20);
            o.Events = new OAuthEvents
            {
                OnTicketReceived = c => { c.Properties!.Items[ProviderKey] = "GitHub"; return Task.CompletedTask; },
                OnRedirectToAuthorizationEndpoint = c => { c.Response.Redirect(c.RedirectUri + "&prompt=select_account"); return Task.CompletedTask; },
                OnCreatingTicket = async c =>
                {
                    using var user = await GitHubJson(c, "https://api.github.com/user");
                    if (!user.RootElement.TryGetProperty("id", out var id) || !id.TryGetInt64(out var number) || number <= 0)
                        throw new AuthenticationFailureException("Invalid provider identity.");
                    var login = user.RootElement.GetProperty("login").GetString();
                    if (string.IsNullOrEmpty(login) || login.Length > 100) throw new AuthenticationFailureException("Invalid provider profile.");
                    c.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    c.Identity.AddClaim(new Claim(Username, login));
                    c.Identity.AddClaim(new Claim(ClaimTypes.Name, user.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : login));
                    using var emails = await GitHubJson(c, "https://api.github.com/user/emails");
                    var primary = emails.RootElement.EnumerateArray().FirstOrDefault(x => x.GetProperty("primary").GetBoolean() && x.GetProperty("verified").GetBoolean());
                    if (primary.ValueKind == JsonValueKind.Object)
                    {
                        c.Identity.AddClaim(new Claim(ClaimTypes.Email, primary.GetProperty("email").GetString()!));
                        c.Identity.AddClaim(new Claim(VerifiedEmail, "true"));
                    }
                },
                OnRemoteFailure = Failure,
                OnAccessDenied = c => { c.HandleResponse(); c.Response.Redirect("/auth/result?error=external_cancelled"); return Task.CompletedTask; }
            };
        });
    }

    private static Task Failure(RemoteFailureContext c)
    {
        c.HandleResponse();
        c.Response.Redirect("/auth/result?error=external_failed");
        return Task.CompletedTask;
    }

    private static async Task<JsonDocument> GitHubJson(OAuthCreatingTicketContext c, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("SenseiPlusPlus/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await c.Backchannel.SendAsync(request, c.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(c.HttpContext.RequestAborted));
    }
}
