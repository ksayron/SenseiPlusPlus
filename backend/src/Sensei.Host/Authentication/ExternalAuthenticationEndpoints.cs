using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Identity.Infrastructure;

namespace Sensei.Host.Authentication;

public sealed record StartExternalAuthentication(string Purpose = "signin", string ReturnPath = "/", string TimeZone = "UTC");
public sealed record ExternalProviderView(string Provider, string DisplayName);
public sealed record ConnectedProfileView(string Provider, string DisplayName, string? Username, string? ProfileUrl, DateTimeOffset ConnectedAt, DateTimeOffset RefreshedAt, bool Enabled);
public sealed record AuthenticationMethods(bool HasPassword, IReadOnlyList<ConnectedProfileView> Profiles);
public sealed record ExternalAuthenticationRedirect(string Redirect);

public static class ExternalAuthenticationEndpoints
{
    public static bool IsRecent(ClaimsPrincipal user) => long.TryParse(user.FindFirstValue(AuthenticationSetup.AuthTimeClaim), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var time) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - time is >= 0 and <= 300;

    // Encoded query values are normal SPA state; encoded paths and dot segments can change the navigation target.
    public static bool SafeReturnPath(string? path)
    {
        if (path is not { Length: > 0 and <= 2048 } || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Any(char.IsControl)) return false;
        var pathname = path.Split(['?', '#'], 2)[0];
        return !pathname.Contains('%') && !pathname.Split('/').Any(segment => segment is "." or "..") &&
            !pathname.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && !pathname.StartsWith("/signin-", StringComparison.OrdinalIgnoreCase) &&
            !pathname.StartsWith("/auth/result", StringComparison.OrdinalIgnoreCase) && !pathname.StartsWith("/sign-out", StringComparison.OrdinalIgnoreCase);
    }

    public static void MapExternalAuthentication(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        g.MapGet("/providers", (HttpContext c) => Results.Ok(ExternalProviders.Names.Where(x => ExternalProviders.Enabled(c, x))
            .Select(x => new ExternalProviderView(x, x)).ToArray())).WithName("GetExternalProviders").Produces<ExternalProviderView[]>();
        g.MapGet("/methods", async (HttpContext c, UserManager<LoginAccount> users, SenseiDbContext db) =>
        {
            var owner = c.OwnerId(); var account = await users.FindByIdAsync(owner.ToString());
            if (account is null) return Results.Unauthorized();
            var profiles = await db.Set<ConnectedProfile>().AsNoTracking().Where(x => x.OwnerId == owner).OrderBy(x => x.Provider).ToArrayAsync(c.RequestAborted);
            return Results.Ok(new AuthenticationMethods(await users.HasPasswordAsync(account), profiles.Select(x => new ConnectedProfileView(
                x.Provider, x.DisplayName, x.Username, x.ProfileUrl, x.ConnectedAt, x.RefreshedAt, ExternalProviders.Enabled(c, x.Provider))).ToArray()));
        }).RequireAuthorization().WithName("GetAuthenticationMethods").Produces<AuthenticationMethods>();

        g.MapPost("/external/{provider}/start", async (string provider, StartExternalAuthentication body, HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users) =>
        {
            var name = ExternalProviders.Canonical(provider);
            if (name is null || !ExternalProviders.Enabled(c, name)) return Problem(c, "provider_unavailable", 400);
            if (!ExternalProviders.OriginMatches(c)) return Problem(c, "external_origin_mismatch", 400);
            if (body.Purpose is not ("signin" or "link" or "refresh" or "reauthenticate") || !SafeReturnPath(body.ReturnPath)) return Problem(c, "validation_failed", 400);
            var signedIn = c.User.Identity?.IsAuthenticated == true;
            if (body.Purpose == "signin" && signedIn) return Problem(c, "account_changed", 409);
            LoginAccount? account = null;
            if (body.Purpose != "signin")
            {
                if (!signedIn) return Problem(c, "authentication_required", 401);
                account = await users.FindByIdAsync(c.OwnerId().ToString());
                if (account is null) return Problem(c, "authentication_required", 401);
                if (body.Purpose == "link" && !IsRecent(c.User)) return Problem(c, "recent_authentication_required", 403);
                var logins = await users.GetLoginsAsync(account);
                if (body.Purpose is "refresh" or "reauthenticate" && !logins.Any(x => x.LoginProvider == name)) return Problem(c, "provider_not_linked", 409);
                if (body.Purpose == "link" && logins.Any(x => x.LoginProvider == name)) return Problem(c, "provider_already_linked", 409);
            }
            string zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(body.TimeZone).Id; }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return Problem(c, "validation_failed", 400); }
            var now = DateTimeOffset.UtcNow;
            // Expired transactions contain no provider tokens and are pruned on subsequent starts.
            await db.Set<ExternalAuthFlow>().Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(c.RequestAborted);
            var binding = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var flow = new ExternalAuthFlow { Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Provider = name, Purpose = body.Purpose,
                OwnerId = account?.Id, SecurityStamp = account?.SecurityStamp, ReturnPath = body.ReturnPath, TimeZone = zone,
                BindingHash = Hash(binding), ExpiresAt = now.AddMinutes(5) };
            db.Add(flow); await db.SaveChangesAsync(c.RequestAborted);
            c.Response.Cookies.Append(CookieName(flow.Id), binding, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax,
                Secure = c.Request.IsHttps || !app.Environment.IsDevelopment(), Path = "/", MaxAge = TimeSpan.FromMinutes(5), IsEssential = true });
            return Results.Ok(new ExternalAuthenticationRedirect("/api/v1/auth/external/challenge?flow=" + flow.Id));
        }).RequireRateLimiting("LocalAuthentication").WithName("StartExternalAuthentication")
            .WithDescription("Start anonymous sign-in, or authenticated linking/profile refresh/provider confirmation. Linking requires recent authentication. Returns a local challenge URL.")
            .Produces<ExternalAuthenticationRedirect>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(429);

        g.MapGet("/external/challenge", async (string flow, HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users) =>
        {
            var pending = await db.Set<ExternalAuthFlow>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow, c.RequestAborted);
            if (pending is null || !ValidBinding(c, pending) || pending.ExpiresAt <= DateTimeOffset.UtcNow || pending.ConsumedAt is not null ||
                pending.ChallengedAt is not null || !ExternalProviders.Enabled(c, pending.Provider) || !ExternalProviders.OriginMatches(c)) return Result("external_expired");
            if (!await SameAccount(c, pending, users)) return Result("account_changed");
            var count = await db.Set<ExternalAuthFlow>().Where(x => x.Id == flow && x.ChallengedAt == null && x.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ChallengedAt, DateTimeOffset.UtcNow), c.RequestAborted);
            if (count != 1) return Result("external_expired");
            await c.SignOutAsync(IdentityConstants.ExternalScheme);
            var properties = new AuthenticationProperties { RedirectUri = "/api/v1/auth/external/complete", ExpiresUtc = pending.ExpiresAt };
            properties.Items[ExternalProviders.FlowKey] = pending.Id;
            return Results.Challenge(properties, [pending.Provider]);
        }).RequireRateLimiting("LocalAuthentication").WithName("ChallengeExternalAuthentication").Produces(302).ProducesProblem(429);

        g.MapGet("/external/complete", Complete).RequireRateLimiting("LocalAuthentication").WithName("CompleteExternalAuthentication").Produces(302).ProducesProblem(429);
        g.MapPost("/external/{provider}/disconnect", async (string provider, HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users) =>
        {
            var name = ExternalProviders.Canonical(provider);
            if (name is null) return Problem(c, "provider_unavailable", 400);
            var owner = c.OwnerId();
            await using var tx = await db.Database.BeginTransactionAsync(c.RequestAborted);
            await Lock(db, "external-owner/" + owner, c.RequestAborted);
            var account = await users.FindByIdAsync(owner.ToString());
            if (account is null) return Results.Unauthorized();
            var logins = await users.GetLoginsAsync(account);
            var login = logins.SingleOrDefault(x => x.LoginProvider == name);
            if (login is null) return Problem(c, "provider_not_linked", 409);
            if (!await users.HasPasswordAsync(account) && !logins.Any(x => x.LoginProvider != name && ExternalProviders.Enabled(c, x.LoginProvider)))
                return Problem(c, "last_signin_method", 409);
            if (!(await users.RemoveLoginAsync(account, name, login.ProviderKey)).Succeeded) return Problem(c, "external_failed", 409);
            await tx.CommitAsync(c.RequestAborted);
            // Identity changes the security stamp on removal; keep this confirmed session usable.
            await AuthenticationEndpoints.SignIn(c.RequestServices.GetRequiredService<SignInManager<LoginAccount>>(), account);
            return Results.NoContent();
        }).RequireAuthorization(AuthenticationSetup.RecentPolicy).RequireRateLimiting("LocalAuthentication").WithName("DisconnectExternalProvider")
            .Produces(204).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(429);
    }

    private static async Task<IResult> Complete(HttpContext c, SenseiDbContext db, UserManager<LoginAccount> users, SignInManager<LoginAccount> signIn, IHostEnvironment env)
    {
        var external = await c.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await c.SignOutAsync(IdentityConstants.ExternalScheme);
        if (!external.Succeeded || external.Principal is null || external.Properties is null || !external.Properties.Items.TryGetValue(ExternalProviders.FlowKey, out var flowId) || flowId is null) return Result("external_expired");
        var flow = await db.Set<ExternalAuthFlow>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == flowId, c.RequestAborted);
        if (flow is null || !ValidBinding(c, flow)) return Result("external_expired");
        c.Response.Cookies.Delete(CookieName(flow.Id), new CookieOptions { Path = "/" });
        var consumed = await db.Set<ExternalAuthFlow>().Where(x => x.Id == flowId && x.ConsumedAt == null && x.ChallengedAt != null && x.ExpiresAt > DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, DateTimeOffset.UtcNow), c.RequestAborted);
        if (consumed != 1) return Result("external_expired");
        if (!ExternalProviders.Enabled(c, flow.Provider) || !ExternalProviders.OriginMatches(c)) return Result("provider_unavailable");
        if (!await SameAccount(c, flow, users)) return Result("account_changed");
        var principal = external.Principal!;
        var key = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(key) || key.Length > 128 || external.Ticket?.AuthenticationScheme != IdentityConstants.ExternalScheme) return Result("external_failed");
        if (!external.Properties.Items.TryGetValue(ExternalProviders.ProviderKey, out var actualProvider) || actualProvider != flow.Provider) return Result("external_failed");
        await using var tx = await db.Database.BeginTransactionAsync(c.RequestAborted);
        await Lock(db, "external-identity/" + flow.Provider + "/" + key, c.RequestAborted);
        LoginAccount? account;
        var bound = await users.FindByLoginAsync(flow.Provider, key);
        if (flow.Purpose == "signin")
        {
            account = bound;
            if (account is null)
            {
                if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) return Result("registration_disabled");
                var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
                if (principal.FindFirstValue(ExternalProviders.VerifiedEmail) != "true" || email is null || email.Length > 256 ||
                    !MailAddress.TryCreate(email, out var address) || address.Address != email) return Result("verified_email_required");
                if (await db.Set<UserAccount>().AnyAsync(x => x.Email == email, c.RequestAborted) || await users.FindByEmailAsync(email) is not null) return Result("email_conflict");
                var profile = UserAccount.Create(email, DisplayName(principal), "en", "en", flow.TimeZone, DateTimeOffset.UtcNow);
                db.Add(profile);
                account = new LoginAccount { Id = profile.Id, Email = profile.Email, UserName = profile.Email, EmailConfirmed = true, LockoutEnabled = true };
                try
                {
                    if (!(await users.CreateAsync(account)).Succeeded || !(await users.AddLoginAsync(account, new UserLoginInfo(flow.Provider, key, flow.Provider))).Succeeded)
                        return Result("external_failed");
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return Result("email_conflict"); }
            }
            await Lock(db, "external-owner/" + account.Id, c.RequestAborted);
        }
        else
        {
            await Lock(db, "external-owner/" + flow.OwnerId, c.RequestAborted);
            account = await users.FindByIdAsync(flow.OwnerId!.Value.ToString());
            if (account is null || account.SecurityStamp != flow.SecurityStamp || !await SameAccount(c, flow, users)) return Result("account_changed");
            if (flow.Purpose == "link")
            {
                if (!IsRecent(c.User)) return Result("recent_authentication_required");
                if (bound is not null && bound.Id != account.Id) return Result("provider_identity_conflict");
                var existing = (await users.GetLoginsAsync(account)).SingleOrDefault(x => x.LoginProvider == flow.Provider);
                if (existing is not null && existing.ProviderKey != key) return Result("provider_already_linked");
                if (existing is null && !(await users.AddLoginAsync(account, new UserLoginInfo(flow.Provider, key, flow.Provider))).Succeeded) return Result("external_failed");
            }
            else if (bound?.Id != account.Id) return Result("provider_identity_mismatch");
        }
        if (!await signIn.CanSignInAsync(account) || await users.IsLockedOutAsync(account)) return Result("external_failed");
        // Recheck the login after the owner lock: a concurrent disconnect must not be undone by sign-in.
        if (!(await users.GetLoginsAsync(account)).Any(x => x.LoginProvider == flow.Provider && x.ProviderKey == key)) return Result("provider_not_linked");
        var metadata = await db.Set<ConnectedProfile>().SingleOrDefaultAsync(x => x.Provider == flow.Provider && x.ProviderKey == key, c.RequestAborted);
        if (metadata is null) { metadata = new ConnectedProfile { Provider = flow.Provider, ProviderKey = key, OwnerId = account.Id, ConnectedAt = DateTimeOffset.UtcNow }; db.Add(metadata); }
        metadata.DisplayName = DisplayName(principal);
        metadata.Username = flow.Provider == "GitHub" ? principal.FindFirstValue(ExternalProviders.Username) : null;
        metadata.ProfileUrl = metadata.Username is { } username ? "https://github.com/" + Uri.EscapeDataString(username) : null;
        metadata.RefreshedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(c.RequestAborted);
        await tx.CommitAsync(c.RequestAborted);
        if (flow.Purpose is "signin" or "reauthenticate") await AuthenticationEndpoints.SignIn(signIn, account);
        return Results.Redirect("/auth/result?returnPath=" + Uri.EscapeDataString(flow.ReturnPath));
    }

    private static string DisplayName(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(ClaimTypes.Name)?.Trim();
        return string.IsNullOrEmpty(name) ? "Connected account" : name[..Math.Min(name.Length, 200)];
    }
    private static async Task<bool> SameAccount(HttpContext c, ExternalAuthFlow flow, UserManager<LoginAccount> users)
    {
        if (flow.OwnerId is null) return c.User.Identity?.IsAuthenticated != true;
        if (c.User.Identity?.IsAuthenticated != true || c.OwnerId() != flow.OwnerId) return false;
        var account = await users.FindByIdAsync(flow.OwnerId.Value.ToString());
        return account is not null && account.SecurityStamp == flow.SecurityStamp;
    }
    private static string CookieName(string id) => "sensei.flow." + id;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool ValidBinding(HttpContext c, ExternalAuthFlow flow) => c.Request.Cookies.TryGetValue(CookieName(flow.Id), out var binding) && binding.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(binding)), Encoding.UTF8.GetBytes(flow.BindingHash));
    private static Task Lock(SenseiDbContext db, string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    private static IResult Result(string code) => Results.Redirect("/auth/result?error=" + code);
    private static IResult Problem(HttpContext c, string code, int status) => HttpContract.Problem(c, code, status, "Account connection could not be completed", code switch
    {
        "last_signin_method" => "Connect another enabled provider or keep a password before disconnecting this sign-in method.",
        "recent_authentication_required" => "Confirm your account before connecting a provider.",
        "account_changed" => "Your account changed. Reload before retrying.",
        "provider_already_linked" => "This provider is already connected. Refresh or disconnect it first.",
        "provider_not_linked" => "This provider is not connected to your account.",
        "external_origin_mismatch" => "Open the app at its configured external authentication origin.",
        _ => "Refresh the page and retry with an available provider."
    });
}
