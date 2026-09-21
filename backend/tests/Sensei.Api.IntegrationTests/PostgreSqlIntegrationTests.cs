using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Initial_migration_is_applied()
    {
        using var factory = fixture.CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var migrations = await scope.ServiceProvider.GetRequiredService<SenseiDbContext>()
            .Database.GetAppliedMigrationsAsync();

        Assert.Contains(migrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Data_survives_application_host_restart()
    {
        var email = $"restart-{Guid.NewGuid():N}@sensei.test";
        Guid userId;

        using (var firstHost = fixture.CreateFactory())
        {
            using var client = firstHost.CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/identity/users", new
            {
                email,
                displayName = "Restart Test",
                uiLocale = "en",
                answerLanguage = "en",
                timeZone = "UTC"
            });
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            userId = document.RootElement.GetProperty("id").GetGuid();
        }

        using var restartedHost = fixture.CreateFactory();
        using var restartedClient = restartedHost.CreateClient();
        var persisted = await restartedClient.GetAsync($"/api/v1/identity/users/{userId}");

        Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
    }

    [Fact]
    public async Task Database_enforces_unique_normalized_email()
    {
        var email = $"unique-{Guid.NewGuid():N}@sensei.test";
        await AddUserAsync(email);

        using var factory = fixture.CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        dbContext.Add(UserAccount.Create(email.ToUpperInvariant(), "Duplicate", "en", "en", "UTC", DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DuplicateResourceException>(() => dbContext.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Owner_scoped_endpoint_does_not_leak_another_owners_episode()
    {
        var owner = await AddUserAsync($"owner-{Guid.NewGuid():N}@sensei.test");
        var otherOwner = await AddUserAsync($"other-{Guid.NewGuid():N}@sensei.test");
        var episode = WorkEpisode.Create(owner.Id, "Private work", Sensei.Modules.WorkReflection.Domain.WorkSetting.Employment,
            DateOnly.FromDateTime(DateTime.UtcNow), "Developer", "Owner-only summary", DateTimeOffset.UtcNow);

        using (var factory = fixture.CreateFactory())
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            dbContext.Add(episode);
            await dbContext.CommitAsync(CancellationToken.None);
        }

        using var host = fixture.CreateFactory();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Owner-Id", otherOwner.Id.ToString());
        var items = await client.GetFromJsonAsync<JsonElement>("/api/v1/work/episodes");

        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task User_deletion_is_restricted_when_owned_records_exist()
    {
        var owner = await AddUserAsync($"restricted-{Guid.NewGuid():N}@sensei.test");

        using (var factory = fixture.CreateFactory())
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            dbContext.Add(WorkEpisode.Create(owner.Id, "Owned", Sensei.Modules.WorkReflection.Domain.WorkSetting.Coursework,
                DateOnly.FromDateTime(DateTime.UtcNow), "Student", "Cannot orphan", DateTimeOffset.UtcNow));
            await dbContext.CommitAsync(CancellationToken.None);
        }

        using var deleteFactory = fixture.CreateFactory();
        await using var deleteScope = deleteFactory.Services.CreateAsyncScope();
        var deleteContext = deleteScope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        deleteContext.Remove(await deleteContext.Set<UserAccount>().SingleAsync(user => user.Id == owner.Id));

        await Assert.ThrowsAsync<ResourceInUseException>(() => deleteContext.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Experience_revisions_and_concepts_are_reconstructed()
    {
        var owner = await AddUserAsync($"experience-{Guid.NewGuid():N}@sensei.test");
        var firstConcept = Concept.Create($"concept-{Guid.NewGuid():N}", "First", "First", "en", ConceptDifficulty.Beginner, DateTimeOffset.UtcNow);
        var secondConcept = Concept.Create($"concept-{Guid.NewGuid():N}", "Second", "Second", "en", ConceptDifficulty.Advanced, DateTimeOffset.UtcNow);
        var entry = ExperienceEntry.Create(owner.Id, Content("First revision", firstConcept.Id), DateTimeOffset.UtcNow);
        entry.Revise(Content("Second revision", secondConcept.Id), 1, DateTimeOffset.UtcNow.AddMinutes(1));

        using (var factory = fixture.CreateFactory())
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
            dbContext.AddRange(firstConcept, secondConcept, entry);
            await dbContext.CommitAsync(CancellationToken.None);
        }

        using var readFactory = fixture.CreateFactory();
        await using var readScope = readFactory.Services.CreateAsyncScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var reconstructed = await readContext.Set<ExperienceEntry>()
            .AsNoTracking()
            .Include(item => item.Revisions)
            .ThenInclude(revision => revision.Concepts)
            .SingleAsync(item => item.Id == entry.Id);

        Assert.Equal(2, reconstructed.Revisions.Count);
        Assert.Equal(secondConcept.Id, reconstructed.CurrentRevision.ConceptIds.Single());
    }

    [Fact]
    public async Task Two_contexts_detect_an_optimistic_concurrency_conflict()
    {
        var saved = await AddUserAsync($"concurrency-{Guid.NewGuid():N}@sensei.test");
        using var firstFactory = fixture.CreateFactory();
        using var secondFactory = fixture.CreateFactory();
        await using var firstScope = firstFactory.Services.CreateAsyncScope();
        await using var secondScope = secondFactory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var firstCopy = await first.Set<UserAccount>().SingleAsync(user => user.Id == saved.Id);
        var secondCopy = await second.Set<UserAccount>().SingleAsync(user => user.Id == saved.Id);

        firstCopy.Update("First writer", "en", "en", "UTC", 1);
        secondCopy.Update("Second writer", "en", "en", "UTC", 1);
        await first.CommitAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.CommitAsync(CancellationToken.None));
    }

    private async Task<UserAccount> AddUserAsync(string email)
    {
        var user = UserAccount.Create(email, "Test User", "en", "en", "UTC", DateTimeOffset.UtcNow);
        using var factory = fixture.CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        dbContext.Add(user);
        await dbContext.CommitAsync(CancellationToken.None);
        return user;
    }

    private static ExperienceRevisionContent Content(string title, Guid conceptId) => new(
        title, Sensei.Modules.Experience.Domain.ExperienceSetting.Employment, "Context", "Role", "Actions", "Alternatives", "Outcome",
        ImpactState.Measured, [conceptId]);
}
