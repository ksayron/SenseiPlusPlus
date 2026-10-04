using System.Text.Json;

namespace Sensei.Host.Security;

/// <summary>
/// An allowlisted diagnostic projection. Never calls an arbitrary formatter, enumerates
/// logging state/scopes, or serializes an exception: each can contain private input or keys.
/// Changing log levels cannot enable payload logging through this provider.
/// </summary>
public sealed class PrivateConsoleLoggerProvider(TextWriter writer) : ILoggerProvider
{
    private readonly TextWriter _writer = TextWriter.Synchronized(writer);

    public ILogger CreateLogger(string categoryName) => new PrivateLogger(_writer, Area(categoryName));
    public void Dispose() { } // The host owns Console.Out (or the test's writer).

    private static string Area(string category) => category switch
    {
        "Sensei.Host.Security.RequestDiagnostics" => "request",
        "Sensei.Modules.Learning.Infrastructure.AssessmentWorker" => "assessment_worker",
        _ when category.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) => "persistence",
        _ when category.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) => "http",
        _ => "runtime"
    };

    internal sealed record RequestCompleted(string Area, string Method, int Status, long DurationMs, Guid Trace);

    private sealed class PrivateLogger(TextWriter writer, string area) : ILogger
    {
        public bool IsEnabled(LogLevel level) => level != LogLevel.None;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            // RequestCompleted is constructed only from fixed enums/numbers and a fresh UUID.
            // Event names, categories, messages, structured state and exception types are excluded.
            writer.WriteLine(JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow,
                level = level.ToString(),
                source = area,
                eventId = eventId.Id,
                hasException = exception is not null,
                request = state as RequestCompleted
            }));
        }
    }
}
