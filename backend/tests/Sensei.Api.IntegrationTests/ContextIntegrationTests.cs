using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Sensei.BuildingBlocks.Application.Models;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ContextIntegrationTests(PostgreSqlFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static ContextSource Source => new(ContextSourceKind.ManualWeb, [new("selected code", "draft-v1", "// preserve\nvar value = 1;  \n")],
        "Explain the selected decision", "No entire repository was supplied");
    private static string Root(Guid episode) => $"/api/v1/work/episodes/{episode}/context";
    private static string Token(JsonElement response) => $"\"{response.GetProperty("versionToken").GetString()}\"";
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) };
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request);
    }
    private static async Task<(Guid Id, string Etag)> Episode(HttpClient client)
    {
        // Test-selector clients still need a real profile for the existing episode-owner FK.
        if ((await client.GetAsync("/api/v1/auth/me")).StatusCode == HttpStatusCode.NotFound)
        {
            var profile = await client.PostAsJsonAsync("/api/v1/identity/users", new { email = $"context-owner-{Guid.NewGuid():N}@example.test",
                displayName = "Context test", uiLocale = "en", answerLanguage = "en", timeZone = "UTC" });
            profile.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Add("X-Owner-Id", (await profile.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString());
        }
        var created = await client.PostAsJsonAsync("/api/v1/work/episodes", new { title = "Snapshot investigation", setting = "PersonalProject",
            eventDate = "2026-10-03", role = "Developer", summary = "Mutable journal metadata" });
        created.EnsureSuccessStatusCode();
        return ((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), created.Headers.ETag!.ToString());
    }
    private static async Task<SaveContextRequestBody> SaveBody(HttpClient client, Guid episode, ContextSource? source = null)
    {
        var selected = source ?? Source;
        var response = await Post(client, Root(episode) + "/preview", new { source = selected }, null);
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LocalOnly", preview.GetProperty("manifest").GetProperty("mode").GetString());
        Assert.Empty(preview.GetProperty("manifest").GetProperty("recipients").EnumerateArray());
        Assert.Equal("Restricted", preview.GetProperty("manifest").GetProperty("dataScope").GetString());
        return new(Guid.NewGuid(), selected, preview.GetProperty("previewToken").GetString()!);
    }
    private sealed record SaveContextRequestBody(Guid OperationId, ContextSource Source, string PreviewToken);

    [Fact]
    public async Task New_source_corrections_and_exact_confirmation_preserve_history_across_restart()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode);
        var saved = await Post(client, root + "/snapshots", await SaveBody(client, episode), etag);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var first = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var snapshot = first.GetProperty("item").GetProperty("id").GetGuid();
        var fingerprint = first.GetProperty("item").GetProperty("sourceFingerprint").GetString();
        Assert.Equal(ContextFingerprint.Compute(Source), fingerprint);
        var text = new InterpretationText("Proposed interpretation", "Only selected excerpt", "Outcome unknown", "");
        var revised = await Post(client, root + $"/snapshots/{snapshot}/interpretations", new { operationId = Guid.NewGuid(), text }, Token(first));
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        var revision = await revised.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User", revision.GetProperty("item").GetProperty("origin").GetString());
        Assert.Equal(JsonValueKind.Null, revision.GetProperty("acceptedInterpretationId").ValueKind);
        var interpretation = revision.GetProperty("item").GetProperty("id").GetGuid();
        var confirmed = await Post(client, root + $"/snapshots/{snapshot}/confirmation", new { operationId = Guid.NewGuid(), interpretationId = interpretation,
            sourceFingerprint = fingerprint }, Token(revision));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmation = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(interpretation, confirmation.GetProperty("acceptedInterpretationId").GetGuid());
        var correction = await Post(client, root + $"/snapshots/{snapshot}/interpretations", new { operationId = Guid.NewGuid(), supersedesId = interpretation,
            text = text with { Intent = "Corrected interpretation", Correction = "Original intent was misunderstood" } }, Token(confirmation));
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        var corrected = await correction.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, corrected.GetProperty("acceptedInterpretationId").ValueKind);
        Assert.Equal(fingerprint, (await client.GetFromJsonAsync<JsonElement>(root + $"/snapshots/{snapshot}")).GetProperty("sourceFingerprint").GetString());
        var second = await Post(client, root + "/snapshots", await SaveBody(client, episode, Source with { Constraints = "Changed source constraint" }), Token(corrected));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, secondBody.GetProperty("item").GetProperty("number").GetInt32());
        Assert.NotEqual(fingerprint, secondBody.GetProperty("item").GetProperty("sourceFingerprint").GetString());
        var historicalConfirm = await Post(client, root + $"/snapshots/{snapshot}/confirmation", new { operationId = Guid.NewGuid(), interpretationId = interpretation,
            sourceFingerprint = fingerprint }, Token(secondBody));
        Assert.Equal(HttpStatusCode.Conflict, historicalConfirm.StatusCode);
        using var restarted = fixture.CreateFactory(); using var anotherClient = restarted.CreateClient();
        anotherClient.DefaultRequestHeaders.Add("X-Owner-Id", client.DefaultRequestHeaders.GetValues("X-Owner-Id"));
        Assert.Equal(2, (await anotherClient.GetFromJsonAsync<JsonElement>(root + "/snapshots")).GetProperty("items").GetArrayLength());
        Assert.Equal(2, (await anotherClient.GetFromJsonAsync<JsonElement>(root + $"/snapshots/{snapshot}/interpretations")).GetProperty("items").GetArrayLength());
        Assert.Single((await anotherClient.GetFromJsonAsync<JsonElement>(root + $"/snapshots/{snapshot}/confirmations")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Snapshot_receipts_replay_before_stale_preconditions_and_reject_changed_bodies()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode); var body = await SaveBody(client, episode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Post(client, root + "/snapshots", body, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, root + "/snapshots", body, "W/" + etag)).StatusCode);
        var saved = await Post(client, root + "/snapshots", body, etag); var first = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var replay = await Post(client, root + "/snapshots", body, etag);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(first.GetProperty("item").GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("item").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, root + "/snapshots", body with { Source = Source with { Intent = "Changed" } }, etag)).StatusCode);
        var other = body with { OperationId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Post(client, root + "/snapshots", other, etag)).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>(root + "/snapshots")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_context_saves_commit_one_snapshot_and_mismatched_preview_is_rejected()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode); var body = await SaveBody(client, episode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, root + "/snapshots", body with { Source = Source with { Intent = "Not previewed" } }, etag)).StatusCode);
        var attempts = await Task.WhenAll(Post(client, root + "/snapshots", body, etag),
            Post(client, root + "/snapshots", body with { OperationId = Guid.NewGuid() }, etag));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.PreconditionFailed);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>(root + "/snapshots")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Interpretation_and_confirmation_replays_do_not_restore_an_outdated_selection()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode);
        var saved = await Post(client, root + "/snapshots", await SaveBody(client, episode), etag);
        var source = await saved.Content.ReadFromJsonAsync<JsonElement>(); var snapshot = source.GetProperty("item").GetProperty("id").GetGuid();
        var reviseBody = new { operationId = Guid.NewGuid(), text = new InterpretationText("My interpretation", "", "Unknown outcome", "") };
        var revision = await (await Post(client, root + $"/snapshots/{snapshot}/interpretations", reviseBody, Token(source))).Content.ReadFromJsonAsync<JsonElement>();
        var revisionId = revision.GetProperty("item").GetProperty("id").GetGuid();
        var repeatedRevision = await Post(client, root + $"/snapshots/{snapshot}/interpretations", reviseBody, Token(source));
        Assert.Equal(HttpStatusCode.OK, repeatedRevision.StatusCode);
        Assert.Equal(revisionId, (await repeatedRevision.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("item").GetProperty("id").GetGuid());
        var confirmationBody = new { operationId = Guid.NewGuid(), interpretationId = revisionId,
            sourceFingerprint = source.GetProperty("item").GetProperty("sourceFingerprint").GetString() };
        var confirmed = await (await Post(client, root + $"/snapshots/{snapshot}/confirmation", confirmationBody, Token(revision))).Content.ReadFromJsonAsync<JsonElement>();
        var next = await (await Post(client, root + "/snapshots", await SaveBody(client, episode, Source with { Intent = "Changed source intent" }), Token(confirmed))).Content.ReadFromJsonAsync<JsonElement>();
        var repeatedConfirmation = await Post(client, root + $"/snapshots/{snapshot}/confirmation", confirmationBody, Token(revision));
        Assert.Equal(HttpStatusCode.OK, repeatedConfirmation.StatusCode);
        var replay = await repeatedConfirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(confirmed.GetProperty("item").GetProperty("id").GetGuid(), replay.GetProperty("item").GetProperty("id").GetGuid());
        Assert.Equal(next.GetProperty("currentSnapshotId").GetGuid(), replay.GetProperty("currentSnapshotId").GetGuid());
        Assert.Equal(JsonValueKind.Null, replay.GetProperty("acceptedInterpretationId").ValueKind);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>(root + $"/snapshots/{snapshot}/confirmations")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_source_change_and_confirmation_cannot_approve_new_context_with_old_interpretation()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode);
        var source = await (await Post(client, root + "/snapshots", await SaveBody(client, episode), etag)).Content.ReadFromJsonAsync<JsonElement>();
        var snapshot = source.GetProperty("item").GetProperty("id").GetGuid();
        var revision = await (await Post(client, root + $"/snapshots/{snapshot}/interpretations", new { operationId = Guid.NewGuid(),
            text = new InterpretationText("Review old context", "", "", "") }, Token(source))).Content.ReadFromJsonAsync<JsonElement>();
        var revisionId = revision.GetProperty("item").GetProperty("id").GetGuid();
        var change = await SaveBody(client, episode, Source with { Constraints = "Competing new constraint" });
        var results = await Task.WhenAll(Post(client, root + "/snapshots", change, Token(revision)),
            Post(client, root + $"/snapshots/{snapshot}/confirmation", new { operationId = Guid.NewGuid(), interpretationId = revisionId,
                sourceFingerprint = source.GetProperty("item").GetProperty("sourceFingerprint").GetString() }, Token(revision)));
        Assert.Single(results, x => x.IsSuccessStatusCode);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.PreconditionFailed);
        var head = await client.GetFromJsonAsync<JsonElement>(root);
        if (head.GetProperty("currentSnapshotId").GetGuid() == snapshot)
            Assert.Equal(revisionId, head.GetProperty("acceptedInterpretationId").GetGuid());
        else Assert.Equal(JsonValueKind.Null, head.GetProperty("acceptedInterpretationId").ValueKind);
    }

    [Fact]
    public async Task Provider_preview_lists_both_boundaries_but_restricted_or_unconfigured_routes_fail()
    {
        var route = new ModelRoute("test-disclosure-v1", "example/model:free", "example/endpoint", "ExampleProvider",
            ModelOutputMode.ValidatedJson, new("https://example.test/privacy"), DateTimeOffset.UtcNow);
        using var factory = fixture.CreateFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
            services.RemoveAll<IContextDisclosurePolicy>(); services.AddSingleton<IContextDisclosurePolicy>(new ContextDisclosurePolicy(route));
        }));
        using var client = configured.CreateClient(); var (episode, etag) = await Episode(client); var root = Root(episode);
        var request = new { source = Source, dataScope = "PublicSynthetic", routeRevision = route.Revision };
        var response = await Post(client, root + "/preview", request, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var recipients = preview.GetProperty("manifest").GetProperty("recipients");
        Assert.Equal("OpenRouter", recipients[0].GetProperty("name").GetString());
        Assert.Equal("ExampleProvider", recipients[1].GetProperty("name").GetString());
        Assert.Equal(route.Digest, preview.GetProperty("manifest").GetProperty("route").GetProperty("digest").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, root + "/preview", new { source = Source, routeRevision = route.Revision }, null)).StatusCode);
        using var unconfigured = factory.CreateClient();
        unconfigured.DefaultRequestHeaders.Add("X-Owner-Id", client.DefaultRequestHeaders.GetValues("X-Owner-Id"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(unconfigured, root + "/preview", request, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(unconfigured, root + "/snapshots", new { operationId = Guid.NewGuid(), source = Source,
            dataScope = "PublicSynthetic", routeRevision = route.Revision, previewToken = preview.GetProperty("previewToken").GetString() }, etag)).StatusCode);
    }

    [Fact]
    public async Task PostgreSql_blocks_updates_to_all_immutable_context_records_and_cross_owner_links()
    {
        using var factory = fixture.CreateFactory(); using var client = factory.CreateClient();
        var (episode, etag) = await Episode(client); var root = Root(episode);
        var saved = await Post(client, root + "/snapshots", await SaveBody(client, episode), etag);
        var source = await saved.Content.ReadFromJsonAsync<JsonElement>(); var snapshot = source.GetProperty("item").GetProperty("id").GetGuid();
        var revised = await Post(client, root + $"/snapshots/{snapshot}/interpretations", new { operationId = Guid.NewGuid(),
            text = new InterpretationText("Test", "", "", "") }, Token(source));
        var revision = await revised.Content.ReadFromJsonAsync<JsonElement>(); var interpretation = revision.GetProperty("item").GetProperty("id").GetGuid();
        var confirmed = await Post(client, root + $"/snapshots/{snapshot}/confirmation", new { operationId = Guid.NewGuid(), interpretationId = interpretation,
            sourceFingerprint = source.GetProperty("item").GetProperty("sourceFingerprint").GetString() }, Token(revision));
        var confirmation = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_reflection.context_snapshots SET number = 99 WHERE id = {snapshot}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_reflection.interpretation_revisions SET number = 99 WHERE id = {interpretation}"));
        var confirmationId = confirmation.GetProperty("item").GetProperty("id").GetGuid();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_reflection.context_confirmations SET source_fingerprint = 'changed' WHERE id = {confirmationId}"));
        var existing = await db.Set<ContextSnapshot>().SingleAsync(x => x.Id == snapshot);
        db.Add(ContextSnapshot.Create(Guid.NewGuid(), episode, 2, Guid.NewGuid(), "test", Source, ContextDataScope.Restricted, "{}", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<Sensei.BuildingBlocks.Application.ResourceInUseException>(() => db.CommitAsync(CancellationToken.None));
        Assert.Equal(1, existing.Number);
    }

    [Fact]
    public async Task Real_cookie_accounts_cannot_read_preview_confirm_or_link_another_owners_source()
    {
        using var factory = new SenseiWebApplicationFactory(fixture.ConnectionString, testOwners: false);
        using var a = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var b = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        async Task Register(HttpClient client)
        {
            async Task Csrf()
            {
                var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
                client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString());
            }
            await Csrf();
            (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = $"context-{Guid.NewGuid():N}@example.test",
                password = "Context-Test-Password1!", displayName = "Context owner" })).EnsureSuccessStatusCode();
            await Csrf();
        }
        await Register(a); await Register(b); var (episode, etag) = await Episode(a); var root = Root(episode);
        var saved = await Post(a, root + "/snapshots", await SaveBody(a, episode), etag); var source = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var snapshot = source.GetProperty("item").GetProperty("id").GetGuid();
        foreach (var path in new[] { root, root + "/snapshots", root + $"/snapshots/{snapshot}", root + $"/snapshots/{snapshot}/interpretations", root + $"/snapshots/{snapshot}/confirmations" })
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(b, root + "/preview", new { source = Source }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(b, root + $"/snapshots/{snapshot}/confirmation", new { operationId = Guid.NewGuid(),
            interpretationId = Guid.NewGuid(), sourceFingerprint = "unknown" }, Token(source))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(a, root + $"/snapshots/{snapshot}/interpretations", new { operationId = Guid.NewGuid(), origin = "Provider",
            text = new InterpretationText("Forged origin", "", "", "") }, Token(source))).StatusCode);
        a.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(a, root + "/preview", new { source = Source }, null)).StatusCode);
    }
}
