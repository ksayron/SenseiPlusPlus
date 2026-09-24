using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Sensei.BuildingBlocks.Api;

public sealed record PageEnvelope<T>(IReadOnlyCollection<T> Items, string? NextCursor);

public sealed class ApiContractException(
    string code,
    int statusCode,
    string title,
    string detail) : Exception(detail)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public static class HttpContract
{
    private const int CursorVersion = 1;
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static string VersionToken(int version) => Base64UrlEncode($"v1:{version.ToString(CultureInfo.InvariantCulture)}");

    public static int RequireVersion(HttpRequest request) => RequireVersion(request.Headers.IfMatch.ToString());

    public static int RequireVersion(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new ApiContractException(
                "precondition_required",
                StatusCodes.Status428PreconditionRequired,
                "Precondition required",
                "A strong If-Match header containing the resource version token is required.");
        }

        if (header == "*" || header.Contains(',') ||
            header.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ||
            header.Length < 3 || header[0] != '"' || header[^1] != '"')
        {
            throw InvalidVersionToken();
        }

        var token = header[1..^1];
        try
        {
            var decoded = Base64UrlDecode(token);
            if (!decoded.StartsWith("v1:", StringComparison.Ordinal) ||
                !int.TryParse(decoded.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
                version < 1 || VersionToken(version) != token)
            {
                throw InvalidVersionToken();
            }

            return version;
        }
        catch (FormatException)
        {
            throw InvalidVersionToken();
        }
    }

    public static IResult OkVersioned<T>(HttpResponse response, T value, int version)
    {
        response.Headers.ETag = $"\"{VersionToken(version)}\"";
        return Results.Ok(value);
    }

    public static IResult CreatedVersioned<T>(HttpResponse response, string location, T value, int version)
    {
        response.Headers.ETag = $"\"{VersionToken(version)}\"";
        return Results.Created(location, value);
    }

    public static PageEnvelope<T> Page<T>(
        IReadOnlyCollection<T> orderedItems,
        int? requestedLimit,
        string? cursor,
        string endpoint,
        Func<T, string> stableSortValue,
        Func<T, Guid> id)
    {
        var limit = requestedLimit ?? DefaultLimit;
        if (limit is < 1 or > MaximumLimit)
        {
            throw new ApiContractException(
                "validation_failed",
                StatusCodes.Status400BadRequest,
                "Validation failed",
                $"limit must be between 1 and {MaximumLimit}.");
        }

        var items = orderedItems.ToArray();
        var start = 0;
        if (cursor is not null)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                throw InvalidCursor();
            }
            var state = DecodeCursor(cursor);
            if (state.Version != CursorVersion || !string.Equals(state.Endpoint, endpoint, StringComparison.Ordinal))
            {
                throw InvalidCursor();
            }

            var index = Array.FindIndex(items, item =>
                id(item) == state.Id && string.Equals(stableSortValue(item), state.SortValue, StringComparison.Ordinal));
            if (index < 0)
            {
                throw InvalidCursor();
            }

            start = index + 1;
        }

        var pageItems = items.Skip(start).Take(limit).ToArray();
        var nextCursor = start + pageItems.Length < items.Length && pageItems.Length > 0
            ? EncodeCursor(new CursorState(
                CursorVersion,
                endpoint,
                stableSortValue(pageItems[^1]),
                id(pageItems[^1])))
            : null;

        return new PageEnvelope<T>(pageItems, nextCursor);
    }

    public static IResult Problem(HttpContext context, string code, int status, string title, string? detail = null) =>
        Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });

    public static IResult NotFound(HttpContext context) => Problem(
        context,
        "resource_not_found",
        StatusCodes.Status404NotFound,
        "Resource not found",
        "The requested resource does not exist or is outside the current owner scope.");

    private static ApiContractException InvalidVersionToken() => new(
        "validation_failed",
        StatusCodes.Status400BadRequest,
        "Validation failed",
        "If-Match must contain exactly one strong ETag emitted by this API.");

    private static ApiContractException InvalidCursor() => new(
        "invalid_cursor",
        StatusCodes.Status400BadRequest,
        "Invalid cursor",
        "The cursor is malformed, expired, or belongs to another endpoint.");

    private static string EncodeCursor(CursorState cursor) =>
        Base64UrlEncode(JsonSerializer.Serialize(cursor, JsonSerializerOptions.Web));

    private static CursorState DecodeCursor(string cursor)
    {
        try
        {
            return JsonSerializer.Deserialize<CursorState>(Base64UrlDecode(cursor), JsonSerializerOptions.Web)
                ?? throw InvalidCursor();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw InvalidCursor();
        }
    }

    private static string Base64UrlEncode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException() };
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private sealed record CursorState(int Version, string Endpoint, string SortValue, Guid Id);
}
