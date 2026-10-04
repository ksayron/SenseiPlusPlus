using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Infrastructure;
using System.Threading.RateLimiting;

namespace Sensei.Host.Authentication;

public static class AuthenticationSetup
{
    public const string TestScheme = "TestOwner";
    public const string RecentPolicy = "RecentAuthentication";
    public const string AuthTimeClaim = "sensei:auth-time";

    public static bool TestOwnersEnabled(IHostEnvironment env, IConfiguration config) =>
        env.IsEnvironment("Testing") && config.GetValue<bool>("Authentication:EnableTestOwnerHeader");

    public static IServiceCollection AddSenseiAuthentication(this IServiceCollection services, IHostEnvironment env, IConfiguration config)
    {
        if (config.GetValue<bool>("Authentication:EnableTestOwnerHeader") && !env.IsEnvironment("Testing"))
            throw new InvalidOperationException("The test owner header is only allowed in the Testing environment.");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentOwner, HttpCurrentOwner>();
        services.AddIdentityCore<LoginAccount>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddSignInManager().AddDefaultTokenProviders().AddUserStore<UserOnlyStore<LoginAccount, SenseiDbContext, Guid>>();
        var auth = services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            o.DefaultChallengeScheme = o.DefaultAuthenticateScheme;
            o.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        });
        auth.AddCookie(IdentityConstants.ApplicationScheme, o =>
        {
            o.Cookie.Name = "sensei.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = env.IsDevelopment() || env.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = false;
            o.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            o.Events.OnRedirectToLogin = c => HttpContract.Problem(c.HttpContext, "authentication_required", 401, "Sign in required").ExecuteAsync(c.HttpContext);
            o.Events.OnRedirectToAccessDenied = c => HttpContract.Problem(c.HttpContext, "access_denied", 403, "Access denied").ExecuteAsync(c.HttpContext);
        });
        auth.AddCookie(IdentityConstants.ExternalScheme);
        auth.AddCookie(IdentityConstants.TwoFactorUserIdScheme);
        auth.AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.Zero;
            o.OnRefreshingPrincipal = context =>
            {
                var time = context.CurrentPrincipal?.FindFirst(AuthTimeClaim);
                if (time is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity) identity.AddClaim(time);
                return Task.CompletedTask;
            };
        });
        services.AddAuthorization(o =>
        {
            o.AddPolicy(RecentPolicy, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
                long.TryParse(c.User.FindFirstValue(AuthTimeClaim), NumberStyles.Integer, CultureInfo.InvariantCulture, out var time) &&
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - time is >= 0 and <= 300));
        });
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-Token";
            o.Cookie.Name = "sensei.csrf";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = env.IsDevelopment() || env.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("LocalAuthentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = env.IsEnvironment("Testing") ? 2000 : env.IsDevelopment() ? 200 : 20,
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
            o.OnRejected = (context, _) => new ValueTask(HttpContract.Problem(context.HttpContext, "rate_limit_exceeded", 429, "Too many sign-in requests", "Wait a minute before trying again.").ExecuteAsync(context.HttpContext));
        });
        return services;
    }

    public static async Task ValidateCsrf(HttpContext c, RequestDelegate next)
    {
        if (c.Request.Path.StartsWithSegments("/api")) c.Response.Headers.CacheControl = "no-store";
        if (c.User.Identity?.IsAuthenticated == true && c.Request.Headers.TryGetValue("X-Expected-Owner", out var expected) &&
            expected.ToString() != c.OwnerId().ToString())
        {
            await HttpContract.Problem(c, "account_changed", 409, "Account changed", "Reload this workspace before sending more work.").ExecuteAsync(c);
            return;
        }
        if (c.GetEndpoint()?.Metadata.GetMetadata<AgentBearerEndpoint>() is null && c.Request.Path.StartsWithSegments("/api") && c.Request.Method is not ("GET" or "HEAD" or "OPTIONS") &&
            !(c.User.Identity?.AuthenticationType == TestScheme && TestOwnersEnabled(c.RequestServices.GetRequiredService<IHostEnvironment>(), c.RequestServices.GetRequiredService<IConfiguration>())))
        {
            try { await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); }
            catch (AntiforgeryValidationException)
            {
                await HttpContract.Problem(c, "antiforgery_failed", 400, "Refresh your security token", "Reload the page and retry this action.").ExecuteAsync(c);
                return;
            }
        }
        await next(c);
    }
}
