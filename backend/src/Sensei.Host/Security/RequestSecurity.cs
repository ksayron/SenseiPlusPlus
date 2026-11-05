using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Application;

namespace Sensei.Host.Security;

public static class RequestSecurity
{
    public const int MaximumBodyBytes = 256 * 1024;

    public static async Task HeadersAndDiagnostics(HttpContext context, RequestDelegate next)
    {
        // Do not use caller-controlled correlation headers as a diagnostic identifier.
        var trace = Guid.NewGuid();
        context.TraceIdentifier = trace.ToString("N");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            // The host currently serves JSON only. SPA hosting needs its own asset policy in #40.
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (context.Request.Path.StartsWithSegments("/api/v1/learning"))
            {
                LearningDiagnostics.RequestDuration.Record(elapsed);
                if (context.Response.StatusCode is 409 or 412) LearningDiagnostics.Conflicts.Add(1);
            }
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Sensei.Host.Security.RequestDiagnostics");
            var state = new PrivateConsoleLoggerProvider.RequestCompleted(
                RequestArea(context.Request.Path), SafeMethod(context.Request.Method), context.Response.StatusCode,
                (long)elapsed, trace);
            logger.Log(LogLevel.Information, new EventId(1000), state, null, static (_, _) => "Request completed");
        }
    }

    public static async Task BoundBody(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = MaximumBodyBytes;
        if (context.Request.ContentLength > MaximumBodyBytes) { await TooLarge(context); return; }
        if (context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == false)
        { await next(context); return; }

        // Also enforce chunked/unknown-length requests and in-process TestServer requests.
        // Buffer at most limit+1 bytes; never allocate from the caller's Content-Length.
        var original = context.Request.Body;
        await using var bounded = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await original.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaximumBodyBytes + 1 - bounded.Length)), context.RequestAborted);
            if (read == 0) break;
            await bounded.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
            if (bounded.Length > MaximumBodyBytes) { await TooLarge(context); return; }
        }
        bounded.Position = 0;
        context.Request.Body = bounded;
        try { await next(context); }
        finally { context.Request.Body = original; }
    }

    private static Task TooLarge(HttpContext context) =>
        HttpContract.Problem(context, "request_too_large", 413, "Request too large", "API request bodies are limited to 256 KiB.").ExecuteAsync(context);

    private static string SafeMethod(string method) => method switch
    {
        "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" => method,
        _ => "other"
    };
    private static string RequestArea(PathString path)
    {
        foreach (var area in new[] { "auth", "identity", "learning", "work", "evidence", "experience" })
            if (path.StartsWithSegments("/api/v1/" + area)) return area;
        return "other";
    }
}
