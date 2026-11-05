using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.BuildingBlocks.Ai;

public static partial class FreeOnlyPolicy
{
    [GeneratedRegex("^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.:-]+:free$")]
    private static partial Regex ModelIdPattern();
    [GeneratedRegex("^[a-zA-Z0-9_./-]+$")]
    private static partial Regex EndpointPattern();
    [GeneratedRegex("^[+-]?0+(?:\\.0+)?(?:[eE][+-]?[0-9]+)?$")]
    private static partial Regex ZeroPricePattern();

    public static ModelFailure? Validate(ModelRoute route, ModelRequest request, DateTimeOffset now)
    {
        if (route is null || request is null || request.Contract is null || string.IsNullOrEmpty(route.ModelId) ||
            string.IsNullOrEmpty(route.EndpointTag) || route.PrivacyPolicy is null || !route.PrivacyPolicy.IsAbsoluteUri)
            return ModelFailure.Configuration;
        if (string.IsNullOrWhiteSpace(route.Revision) || route.Revision.Length > 128 ||
            !ModelIdPattern().IsMatch(route.ModelId) || !EndpointPattern().IsMatch(route.EndpointTag) ||
            string.IsNullOrWhiteSpace(route.ProviderName) || route.ProviderName.Length > 128 ||
            !Enum.IsDefined(route.OutputMode) || route.PrivacyPolicy.Scheme != "https" || route.PrivacyPolicy.UserInfo.Length != 0 ||
            route.PrivacyReviewedAt > now || now - route.PrivacyReviewedAt > TimeSpan.FromDays(30) ||
            route.MaxInputBytes is < 256 or > 8000 || route.MaxOutputTokens is < 64 or > 1500 ||
            route.TimeoutSeconds is < 1 or > 60 || request.DataScope != ModelDataScope.PublicSynthetic ||
            string.IsNullOrWhiteSpace(request.Task) || string.IsNullOrWhiteSpace(request.SystemPrompt) ||
            string.IsNullOrWhiteSpace(request.Input) || string.IsNullOrWhiteSpace(request.Contract.Name) ||
            string.IsNullOrWhiteSpace(request.Contract.Version) || request.Contract.Schema.ValueKind != JsonValueKind.Object)
            return ModelFailure.Configuration;
        // Conservative byte upper bound, including prompt/schema overhead. Never silently truncate.
        var inputBytes = request.ConservativeInputBytes;
        return inputBytes > route.MaxInputBytes || SecretBoundary.ContainsRecognizedSecret(request.Input) ||
            SecretBoundary.ContainsRecognizedSecret(request.WireSystemPrompt) || SecretBoundary.ContainsSecret(request.Contract.Schema)
            ? ModelFailure.Configuration : null;
    }

    public static ModelFailure? Prices(JsonElement pricing)
    {
        if (pricing.ValueKind != JsonValueKind.Object || !pricing.TryGetProperty("prompt", out _) ||
            !pricing.TryGetProperty("completion", out _) || !pricing.TryGetProperty("request", out _))
            return ModelFailure.UnknownPrice;
        var properties = pricing.EnumerateObject().ToArray();
        if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
            return ModelFailure.UnknownPrice;
        foreach (var price in properties)
        {
            var raw = price.Value.ValueKind == JsonValueKind.String ? price.Value.GetString() :
                price.Value.ValueKind == JsonValueKind.Number ? price.Value.GetRawText() : null;
            if (raw is null || !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return ModelFailure.UnknownPrice;
            if (value != 0 || !ZeroPricePattern().IsMatch(raw)) return ModelFailure.NonzeroPrice;
        }
        return null;
    }

    public static ModelFailure? Endpoint(JsonElement metadata, ModelRoute route)
    {
        if (!metadata.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("id", out var id) || id.GetString() != route.ModelId ||
            !data.TryGetProperty("endpoints", out var endpoints) || endpoints.ValueKind != JsonValueKind.Array)
            return ModelFailure.MetadataUnavailable;
        var matches = endpoints.EnumerateArray().Where(e =>
            e.TryGetProperty("tag", out var tag) && tag.GetString() == route.EndpointTag).ToArray();
        if (matches.Length != 1) return ModelFailure.MetadataUnavailable;
        var endpoint = matches[0];
        if (!endpoint.TryGetProperty("provider_name", out var provider) || provider.GetString() != route.ProviderName ||
            !endpoint.TryGetProperty("model_id", out var model) || model.GetString() != route.ModelId ||
            !endpoint.TryGetProperty("status", out var status) || !status.TryGetInt32(out var statusCode) || statusCode != 0)
            return ModelFailure.MetadataUnavailable;
        var priceFailure = endpoint.TryGetProperty("pricing", out var prices) ? Prices(prices) : ModelFailure.UnknownPrice;
        if (priceFailure is not null) return priceFailure;
        if (!endpoint.TryGetProperty("context_length", out var context) || !context.TryGetInt32(out var contextLength) ||
            contextLength < route.MaxInputBytes + route.MaxOutputTokens ||
            !endpoint.TryGetProperty("max_completion_tokens", out var max) || !max.TryGetInt32(out var maxOutput) ||
            maxOutput < route.MaxOutputTokens ||
            !endpoint.TryGetProperty("supported_parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
            return ModelFailure.UnsupportedCapability;
        var supported = parameters.EnumerateArray().Select(x => x.GetString()).ToHashSet();
        if (!supported.Contains("max_tokens") || route.OutputMode == ModelOutputMode.StrictJsonSchema &&
            (!supported.Contains("response_format") || !supported.Contains("structured_outputs")))
            return ModelFailure.UnsupportedCapability;
        return null;
    }
}
