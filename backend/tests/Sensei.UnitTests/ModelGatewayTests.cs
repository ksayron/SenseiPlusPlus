using System.Net;
using System.Text.Json;
using Sensei.BuildingBlocks.Ai;
using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.UnitTests;

public sealed class ModelGatewayTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static readonly ModelRoute Route = new("test-route-v1", "example/model:free", "example/endpoint", "Example",
        ModelOutputMode.StrictJsonSchema, new("https://example.test/privacy"), DateTimeOffset.UtcNow);
    private static readonly ModelRequest Request = new("test", "Return a valid result.", "Public synthetic example", new Contract(), ModelDataScope.PublicSynthetic);
    private sealed class Contract : IModelOutputContract
    {
        public string Name => "test_result";
        public string Version => "v1";
        public JsonElement Schema => Json("""{"type":"object","properties":{"ok":{"type":"boolean"}},"required":["ok"],"additionalProperties":false}""");
        public bool Accepts(JsonElement output) => output.EnumerateObject().Count() == 1 && output.GetProperty("ok").ValueKind == JsonValueKind.True;
    }
    private static string Metadata(string prices = "{\"prompt\":\"0\",\"completion\":\"0\",\"request\":\"0\"}", string parameters = "[\"max_tokens\",\"response_format\",\"structured_outputs\"]") =>
        "{\"data\":{\"id\":\"example/model:free\",\"endpoints\":[{\"tag\":\"example/endpoint\",\"provider_name\":\"Example\",\"model_id\":\"example/model:free\",\"status\":0,\"context_length\":32000,\"max_completion_tokens\":2000,\"supported_parameters\":" + parameters + ",\"pricing\":" + prices + "}]}}";
    private static string Completion(string content = "{\"ok\":true}", string finish = "stop", string provider = "Example", string model = "example/model:free") =>
        JsonSerializer.Serialize(new { model, provider, choices = new[] { new { finish_reason = finish, message = new { content } } }, usage = new { prompt_tokens = 20, completion_tokens = 10, cost = 0 } });
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return respond(request, ct); }
    }
    private static HttpResponseMessage Response(string content, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(content) };

    [Fact]
    public async Task Rate_limit_returns_retry_after_without_retrying_or_exposing_the_body()
    {
        var handler = new Handler((message, _) =>
        {
            var response = message.Method == HttpMethod.Get ? Response(Metadata()) : Response("private provider diagnostic", HttpStatusCode.TooManyRequests);
            if (message.Method == HttpMethod.Post) response.Headers.RetryAfter = new(TimeSpan.FromMinutes(7));
            return Task.FromResult(response);
        });
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        var result = await gateway.CompleteAsync(Request);
        Assert.Equal(ModelFailure.RateLimited, result.Failure); Assert.Equal(TimeSpan.FromMinutes(7), result.RetryAfter);
        Assert.Null(result.Output); Assert.Equal(2, handler.Calls); Assert.DoesNotContain("private provider", result.ToString());
    }

    [Theory]
    [InlineData("{}", ModelFailure.UnknownPrice)]
    [InlineData("{\"prompt\":\"0\",\"completion\":\"0\"}", ModelFailure.UnknownPrice)]
    [InlineData("{\"prompt\":\"0.01\",\"completion\":\"0\",\"request\":\"0\"}", ModelFailure.NonzeroPrice)]
    [InlineData("{\"prompt\":\"1e-100\",\"completion\":\"0\",\"request\":\"0\"}", ModelFailure.NonzeroPrice)]
    [InlineData("{\"prompt\":\"0\",\"completion\":\"0\",\"request\":\"0\",\"input_cache_read\":\"0.2\"}", ModelFailure.NonzeroPrice)]
    [InlineData("{\"prompt\":\"0\",\"completion\":null,\"request\":\"0\"}", ModelFailure.UnknownPrice)]
    [InlineData("{\"prompt\":\"0\",\"prompt\":\"0\",\"completion\":\"0\",\"request\":\"0\"}", ModelFailure.UnknownPrice)]
    public async Task Unproven_or_nonzero_price_never_sends_a_prompt(string prices, ModelFailure failure)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(Metadata(prices))));
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        Assert.Equal(failure, (await gateway.CompleteAsync(Request)).Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Pinning_zero_price_no_fallback_and_schema_are_enforced_on_the_wire()
    {
        var handler = new Handler(async (message, ct) =>
        {
            Assert.Equal("openrouter.ai", message.RequestUri!.Host);
            if (message.Method == HttpMethod.Get) { Assert.Null(message.Headers.Authorization); return Response(Metadata()); }
            Assert.Equal("test-secret", message.Headers.Authorization!.Parameter);
            var payload = Json(await message.Content!.ReadAsStringAsync(ct));
            Assert.Equal(Route.ModelId, payload.GetProperty("model").GetString());
            var provider = payload.GetProperty("provider");
            Assert.False(provider.GetProperty("allow_fallbacks").GetBoolean());
            Assert.Equal(Route.EndpointTag, provider.GetProperty("only")[0].GetString());
            Assert.Equal(0, provider.GetProperty("max_price").GetProperty("request").GetInt32());
            Assert.True(provider.GetProperty("zdr").GetBoolean());
            Assert.Equal("deny", provider.GetProperty("data_collection").GetString());
            Assert.False(payload.TryGetProperty("tools", out _)); Assert.False(payload.TryGetProperty("plugins", out _));
            Assert.Equal("json_schema", payload.GetProperty("response_format").GetProperty("type").GetString());
            return Response(Completion());
        });
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        var result = await gateway.CompleteAsync(Request);
        Assert.True(result.Succeeded); Assert.Equal(ModelOrigin.Provider, result.Audit.Origin);
        Assert.Equal("Example", result.Audit.ActualProvider); Assert.Equal(20, result.Audit.Usage.InputTokens);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("not JSON", "stop", ModelFailure.Malformed)]
    [InlineData("{\"ok\":true}", "length", ModelFailure.Truncated)]
    [InlineData("{\"ok\":true}", "content_filter", ModelFailure.Refused)]
    [InlineData("", "stop", ModelFailure.Refused)]
    [InlineData("{\"ok\":false}", "stop", ModelFailure.InvalidOutput)]
    [InlineData("{\"ok\":true,\"ok\":true}", "stop", ModelFailure.InvalidOutput)]
    [InlineData("```json\n{\"ok\":true}\n```", "stop", ModelFailure.Malformed)]
    public async Task Bad_outputs_fail_safely_without_retry_or_repair(string content, string finish, ModelFailure failure)
    {
        var handler = new Handler((message, _) => Task.FromResult(Response(message.Method == HttpMethod.Get ? Metadata() : Completion(content, finish))));
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        var result = await gateway.CompleteAsync(Request);
        Assert.Equal(failure, result.Failure); Assert.Null(result.Output); Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("Other", "example/model:free")]
    [InlineData("Example", "other/model:free")]
    public async Task Unapproved_actual_identity_is_rejected(string provider, string model)
    {
        var handler = new Handler((message, _) => Task.FromResult(Response(message.Method == HttpMethod.Get ? Metadata() : Completion(provider: provider, model: model))));
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        Assert.Equal(ModelFailure.IdentityMismatch, (await gateway.CompleteAsync(Request)).Failure);
    }

    [Fact]
    public async Task Missing_schema_capability_blocks_strict_mode_but_validated_json_uses_no_unsupported_flag()
    {
        var handler = new Handler(async (message, ct) =>
        {
            if (message.Method == HttpMethod.Get) return Response(Metadata(parameters: "[\"max_tokens\"]"));
            var payload = Json(await message.Content!.ReadAsStringAsync(ct));
            Assert.False(payload.TryGetProperty("response_format", out _));
            return Response(Completion());
        });
        using (var strict = new OpenRouterModelGateway(Route, () => "test-secret", handler))
            Assert.Equal(ModelFailure.UnsupportedCapability, (await strict.CompleteAsync(Request)).Failure);
        var plainHandler = new Handler(async (message, ct) =>
        {
            if (message.Method == HttpMethod.Get) return Response(Metadata(parameters: "[\"max_tokens\"]"));
            Assert.False(Json(await message.Content!.ReadAsStringAsync(ct)).TryGetProperty("response_format", out _));
            return Response(Completion());
        });
        using var plain = new OpenRouterModelGateway(Route with { OutputMode = ModelOutputMode.ValidatedJson }, () => "test-secret", plainHandler);
        Assert.True((await plain.CompleteAsync(Request)).Succeeded);
    }

    [Theory]
    [InlineData(429, ModelFailure.RateLimited)]
    [InlineData(402, ModelFailure.QuotaExhausted)]
    [InlineData(503, ModelFailure.ProviderUnavailable)]
    [InlineData(302, ModelFailure.ProviderUnavailable)]
    public async Task Provider_failures_never_trigger_fallback(int status, ModelFailure failure)
    {
        var handler = new Handler((message, _) => Task.FromResult(message.Method == HttpMethod.Get ? Response(Metadata()) : Response("secret provider body", (HttpStatusCode)status)));
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        var result = await gateway.CompleteAsync(Request); Assert.Equal(failure, result.Failure);
        Assert.DoesNotContain("secret provider body", result.ToString()); Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Unsafe_configuration_missing_credentials_and_private_data_never_reach_http()
    {
        foreach (var route in new[] { Route with { ModelId = "openrouter/free" }, Route with { ModelId = "example/paid" }, Route with { PrivacyReviewedAt = DateTimeOffset.UtcNow.AddDays(-31) } })
        {
            var handler = new Handler((_, _) => throw new Exception("Must not call HTTP"));
            using var gateway = new OpenRouterModelGateway(route, () => "test-secret", handler);
            Assert.Equal(ModelFailure.Configuration, (await gateway.CompleteAsync(Request)).Failure); Assert.Equal(0, handler.Calls);
        }
        var blocked = new Handler((_, _) => throw new Exception("Must not call HTTP"));
        using var missing = new OpenRouterModelGateway(Route, () => null, blocked);
        Assert.Equal(ModelFailure.Configuration, (await missing.CompleteAsync(Request)).Failure);
        Assert.Equal(ModelFailure.Configuration, (await missing.CompleteAsync(Request with { DataScope = ModelDataScope.Restricted })).Failure);
        Assert.Equal(ModelFailure.Configuration, (await missing.CompleteAsync(Request with { Input = new string('x', 9000) })).Failure);
        Assert.Equal(0, blocked.Calls);
    }

    [Fact]
    public async Task Cancellation_and_timeout_preserve_safe_categories()
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); throw new Exception(); });
        using var gateway = new OpenRouterModelGateway(Route with { TimeoutSeconds = 1 }, () => "test-secret", handler);
        Assert.Equal(ModelFailure.Timeout, (await gateway.CompleteAsync(Request)).Failure);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(ModelFailure.Cancelled, (await gateway.CompleteAsync(Request, cancellation.Token)).Failure);
    }

    [Fact]
    public async Task Fixtures_are_exact_input_and_never_impersonate_live_assessments()
    {
        var audit = new ModelAudit("fixture-v1", "fixture", null, "stop", new(null, null, null), TimeSpan.Zero, ModelOrigin.Provider);
        var gateway = new FixtureModelGateway(new Dictionary<string, ModelResult> { [Request.Digest] = new(Json("{\"ok\":true}"), null, audit) });
        Assert.Equal(ModelOrigin.Fixture, (await gateway.CompleteAsync(Request)).Audit.Origin);
        Assert.Equal(ModelFailure.FixtureMissing, (await gateway.CompleteAsync(Request with { Input = "different input" })).Failure);
        Assert.Equal(ModelFailure.Configuration, (await gateway.CompleteAsync(Request with { DataScope = ModelDataScope.Restricted })).Failure);
    }

    [Fact]
    public async Task Price_changes_are_revalidated_for_each_new_invocation()
    {
        var metadataReads = 0; var completions = 0;
        var handler = new Handler((message, _) =>
        {
            if (message.Method == HttpMethod.Get)
            {
                metadataReads++;
                return Task.FromResult(Response(Metadata(metadataReads == 1 ? "{\"prompt\":\"0\",\"completion\":\"0\",\"request\":\"0\"}" :
                    "{\"prompt\":\"0\",\"completion\":\"0.01\",\"request\":\"0\"}")));
            }
            completions++; return Task.FromResult(Response(Completion()));
        });
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        Assert.True((await gateway.CompleteAsync(Request)).Succeeded);
        Assert.Equal(ModelFailure.NonzeroPrice, (await gateway.CompleteAsync(Request)).Failure);
        Assert.Equal(1, completions); Assert.Equal(2, metadataReads);
    }

    [Fact]
    public async Task Concurrent_invocations_respect_the_shared_limit()
    {
        var active = 0; var maximum = 0;
        var handler = new Handler(async (message, ct) =>
        {
            var current = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref maximum); } while (current > observed && Interlocked.CompareExchange(ref maximum, current, observed) != observed);
            try { await Task.Delay(30, ct); return Response(message.Method == HttpMethod.Get ? Metadata() : Completion()); }
            finally { Interlocked.Decrement(ref active); }
        });
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler, maxConcurrency: 2);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => gateway.CompleteAsync(Request)));
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(2, maximum);
    }

    [Fact]
    public async Task Nonzero_reported_cost_and_oversized_response_never_produce_feedback()
    {
        foreach (var completion in new[] { Completion().Replace("\"cost\":0", "\"cost\":0.001"), new string('x', 256 * 1024 + 1) })
        {
            var handler = new Handler((message, _) => Task.FromResult(Response(message.Method == HttpMethod.Get ? Metadata() : completion)));
            using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
            var result = await gateway.CompleteAsync(Request);
            Assert.Equal(completion.StartsWith('{') ? ModelFailure.NonzeroPrice : ModelFailure.Malformed, result.Failure);
            Assert.Null(result.Output); Assert.Equal(2, handler.Calls);
        }
    }

    [Fact]
    public async Task Unclassified_inputs_default_to_restricted_and_do_not_make_requests()
    {
        var handler = new Handler((_, _) => throw new Exception("Unclassified input must remain local."));
        using var gateway = new OpenRouterModelGateway(Route, () => "test-secret", handler);
        var unclassified = new ModelRequest("test", "Instructions", "Learner input", new Contract());
        Assert.Equal(ModelDataScope.Restricted, unclassified.DataScope);
        Assert.Equal(ModelFailure.Configuration, (await gateway.CompleteAsync(unclassified)).Failure);
        Assert.Equal(0, handler.Calls);
    }
}
