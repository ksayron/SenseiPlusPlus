using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sensei.Host.Security;

namespace Sensei.Api.IntegrationTests;

public sealed class PrivateLoggingTests
{
    [Fact]
    public void Framework_messages_states_scopes_categories_and_nested_errors_are_never_formatted()
    {
        const string secret = "sk-or-v1-SyntheticNeverARealKey";
        using var sink = new StringWriter();
        using var provider = new PrivateConsoleLoggerProvider(sink);
        var logger = provider.CreateLogger("private category " + secret);
        using var scope = logger.BeginScope(new { authorization = secret, answer = secret });
        foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            logger.Log(level, new EventId(42, secret), new { headers = secret, body = secret },
                new InvalidOperationException(secret, new Exception(secret)), static (_, _) => throw new Exception("Formatter must not run"));
        Assert.DoesNotContain(secret, sink.ToString());
        var rows = sink.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, rows.Length);
        foreach (var row in rows)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(row);
            Assert.Equal("runtime", json.GetProperty("source").GetString());
            Assert.True(json.GetProperty("hasException").GetBoolean());
            Assert.Equal(42, json.GetProperty("eventId").GetInt32());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("request").ValueKind);
        }
    }
}

[Collection(PostgreSqlCollection.Name)]
public sealed class SecurityIntegrationTests(PostgreSqlFixture fixture)
{
    private const string PrivateText = "PRIVATE-answer-secret-203c1a99";
    private WebApplicationFactory<Program> Factory(StringWriter sink) => new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false)
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoggerProvider>();
            services.AddSingleton<ILoggerProvider>(new PrivateConsoleLoggerProvider(sink));
        }));
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private static async Task Csrf(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", json.GetProperty("token").GetString());
    }
    private static async Task Register(HttpClient client)
    {
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{PrivateText}-{Guid.NewGuid():N}@example.test", password = "Private-Password-1!", displayName = PrivateText
        })).StatusCode);
        await Csrf(client);
    }
    private static void Headers(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Private_body_query_path_headers_and_parser_errors_do_not_enter_console_or_error_details()
    {
        using var sink = new StringWriter(); using var factory = Factory(sink); using var client = Client(factory);
        Assert.Single(factory.Services.GetServices<ILoggerProvider>());
        Assert.IsType<PrivateConsoleLoggerProvider>(factory.Services.GetServices<ILoggerProvider>().Single());
        await Register(client);
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + PrivateText);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", PrivateText);
        var created = await client.PostAsJsonAsync("/api/v1/work/episodes", new
        { title = PrivateText, setting = "PersonalProject", eventDate = "2026-10-03", role = "Developer", summary = PrivateText });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Headers(created);
        // Private input may be returned to its authenticated author; the diagnostic projection may not contain it.
        Assert.Contains(PrivateText, await created.Content.ReadAsStringAsync());
        var list = await client.GetAsync("/api/v1/work/episodes?search=" + PrivateText); Headers(list);
        var badJson = await client.PostAsync("/api/v1/auth/register?private=" + PrivateText,
            new StringContent("{\"" + PrivateText + "\": [}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, badJson.StatusCode); Headers(badJson);
        Assert.DoesNotContain(PrivateText, await badJson.Content.ReadAsStringAsync());
        var badState = await client.PostAsJsonAsync("/api/v1/work/episodes", new
        { title = PrivateText, setting = PrivateText, eventDate = "2026-10-03", role = "Developer", summary = PrivateText });
        Assert.Equal(HttpStatusCode.BadRequest, badState.StatusCode);
        Assert.DoesNotContain(PrivateText, await badState.Content.ReadAsStringAsync());
        var missing = await client.GetAsync("/api/v1/learning/" + PrivateText + "?answer=" + PrivateText);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); Headers(missing);
        Assert.DoesNotContain(PrivateText, await missing.Content.ReadAsStringAsync());
        Assert.DoesNotContain(PrivateText, sink.ToString());
        Assert.DoesNotContain("Private-Password-1!", sink.ToString());
        Assert.DoesNotContain(fixture.ConnectionString, sink.ToString());
        var requests = sink.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).Where(row => row.GetProperty("request").ValueKind == JsonValueKind.Object).ToArray();
        Assert.Contains(requests, row => row.GetProperty("request").GetProperty("Area").GetString() == "work");
        Assert.Contains(requests, row => row.GetProperty("request").GetProperty("Status").GetInt32() == 400);
        Assert.All(requests, row => Assert.True(Guid.TryParse(row.GetProperty("request").GetProperty("Trace").GetString(), out _)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_and_unknown_length_bodies_are_bounded_before_json_binding(bool unknownLength)
    {
        using var sink = new StringWriter(); using var factory = Factory(sink); using var client = Client(factory);
        await Csrf(client);
        var bytes = Encoding.UTF8.GetBytes("{\"password\":\"" + new string('x', RequestSecurity.MaximumBodyBytes) + "\"}");
        using HttpContent content = unknownLength ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/json");
        var response = await client.PostAsync("/api/v1/auth/register", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode); Headers(response);
        Assert.Equal("request_too_large", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Header_policy_applies_to_unauthorized_and_antiforgery_rejections()
    {
        using var sink = new StringWriter(); using var factory = Factory(sink); using var client = Client(factory);
        var anonymous = await client.GetAsync("/api/v1/work/episodes");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Headers(anonymous);
        var rejected = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = PrivateText, password = PrivateText });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode); Headers(rejected);
        Assert.DoesNotContain(PrivateText, sink.ToString());
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
