using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Identity.Infrastructure;
using Npgsql;

namespace Sensei.Host.Authentication;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string UiLocale = "en", string AnswerLanguage = "en", string TimeZone = "UTC");
public sealed record LoginRequest(string Email, string Password);
public sealed record ReauthenticateRequest(string Password);
public sealed record AccountSession(Guid Id, string Email, string DisplayName, string UiLocale, string AnswerLanguage, string TimeZone);
public sealed record CsrfToken(string Token);

public static class AuthenticationEndpoints
{
    public static void MapSenseiAuthentication(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        g.MapGet("/csrf", (HttpContext c, IAntiforgery csrf) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new CsrfToken(csrf.GetAndStoreTokens(c).RequestToken!));
        }).WithName("GetAntiforgeryToken");
        g.MapPost("/register", async (RegisterRequest body, HttpContext c, UserManager<LoginAccount> users, SignInManager<LoginAccount> signIn, SenseiDbContext db) =>
        {
            if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
                return HttpContract.Problem(c, "registration_disabled", 403, "Local registration only");
            ValidateCredentials(body.Email, body.Password);
            var profile = UserAccount.Create(body.Email, body.DisplayName, body.UiLocale, body.AnswerLanguage, body.TimeZone, DateTimeOffset.UtcNow);
            // Never attach credentials to an existing, caller-selected profile.
            if (await db.Set<UserAccount>().AnyAsync(x => x.Email == profile.Email, c.RequestAborted))
                return HttpContract.Problem(c, "registration_failed", 409, "Account cannot be registered", "Use a different email or the documented local recovery procedure.");
            await using var tx = await db.Database.BeginTransactionAsync(c.RequestAborted);
            db.Add(profile);
            var account = new LoginAccount { Id = profile.Id, UserName = profile.Email, Email = profile.Email, LockoutEnabled = true };
            IdentityResult result;
            try { result = await users.CreateAsync(account, body.Password); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return HttpContract.Problem(c, "registration_failed", 409, "Account cannot be registered", "Use a different email or the documented local recovery procedure.");
            }
            if (!result.Succeeded) return IdentityFailure(c, result);
            await tx.CommitAsync(c.RequestAborted);
            await SignIn(signIn, account);
            c.Response.Headers.CacheControl = "no-store";
            return Results.Created("/api/v1/auth/me", Map(profile));
        }).RequireRateLimiting("LocalAuthentication").WithName("RegisterAccount").Produces<AccountSession>(201).ProducesProblem(400).ProducesProblem(409);
        g.MapPost("/login", async (LoginRequest body, HttpContext c, UserManager<LoginAccount> users, SignInManager<LoginAccount> signIn, SenseiDbContext db) =>
        {
            ValidateCredentials(body.Email, body.Password);
            var account = await users.FindByEmailAsync(body.Email.Trim());
            var result = account is null ? SignInResult.Failed : await signIn.CheckPasswordSignInAsync(account, body.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return HttpContract.Problem(c, "login_failed", 401, "Sign-in failed", "Check your email and password or try again later.");
            await SignIn(signIn, account!);
            c.Response.Headers.CacheControl = "no-store";
            return Results.Ok(Map(await db.Set<UserAccount>().SingleAsync(x => x.Id == account!.Id, c.RequestAborted)));
        }).RequireRateLimiting("LocalAuthentication").WithName("SignInAccount").Produces<AccountSession>().ProducesProblem(401);
        g.MapGet("/me", async (HttpContext c, SenseiDbContext db) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            var id = c.OwnerId();
            var profile = await db.Set<UserAccount>().SingleOrDefaultAsync(x => x.Id == id, c.RequestAborted);
            return profile is null ? HttpContract.NotFound(c) : Results.Ok(Map(profile));
        }).RequireAuthorization().WithName("GetCurrentAccount").Produces<AccountSession>().ProducesProblem(401);
        g.MapPost("/logout", async (HttpContext c, SignInManager<LoginAccount> signIn) =>
        {
            await c.SignOutAsync(IdentityConstants.ApplicationScheme);
            c.Response.Headers.CacheControl = "no-store";
            return Results.NoContent();
        }).RequireAuthorization().WithName("SignOutAccount");
        g.MapPost("/reauthenticate", async (ReauthenticateRequest body, HttpContext c, UserManager<LoginAccount> users, SignInManager<LoginAccount> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(body.Password) || body.Password.Length > 256) throw new ArgumentException("A password is required (maximum 256 characters).");
            var account = await users.FindByIdAsync(c.OwnerId().ToString());
            if (account is null || !(await signIn.CheckPasswordSignInAsync(account, body.Password, lockoutOnFailure: true)).Succeeded)
                return HttpContract.Problem(c, "reauthentication_failed", 401, "Password confirmation failed");
            await SignIn(signIn, account);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("LocalAuthentication").WithName("ReauthenticateAccount");
        g.MapGet("/reauthentication", () => Results.NoContent()).RequireAuthorization(AuthenticationSetup.RecentPolicy).WithName("CheckRecentAuthentication");
    }

    private static Task SignIn(SignInManager<LoginAccount> signIn, LoginAccount account) =>
        signIn.SignInWithClaimsAsync(account, isPersistent: false,
            [new Claim(AuthenticationSetup.AuthTimeClaim, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))]);
    private static AccountSession Map(UserAccount p) => new(p.Id, p.Email, p.DisplayName, p.UiLocale, p.AnswerLanguage, p.TimeZone);
    private static IResult IdentityFailure(HttpContext c, IdentityResult result) =>
        HttpContract.Problem(c, "validation_failed", 400, "Account cannot be registered", string.Join(" ", result.Errors.Select(x => x.Description)));
    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email.Trim())
            throw new ArgumentException("A valid email of at most 256 characters is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length > 256) throw new ArgumentException("A password is required (maximum 256 characters).");
    }
}
