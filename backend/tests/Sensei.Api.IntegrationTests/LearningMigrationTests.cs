using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Api.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class LearningMigrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Learning_migration_preserves_existing_identities_evidence_and_experience()
    {
        // A separate disposable database allows the real pre-learning schema to be exercised.
        var database = "migration_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = database };
        using var factory = new SenseiWebApplicationFactory(connection.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260921172251_InitialCreate");
        var now = DateTimeOffset.UtcNow;
        var owner = UserAccount.Create("legacy@example.test", "Legacy owner", "en", "en", "UTC", now);
        var concept = Concept.Create("legacy-concept", "Legacy concept", "Retained identity", "en", ConceptDifficulty.Intermediate, now);
        var observation = EvidenceObservation.Create(owner.Id, concept.Id, "Legacy observation", EvidenceSignalKind.Scenario, EvidenceSourceKind.Attempt, Guid.NewGuid(), AssistanceLevel.None, "Manual source label", now);
        var experience = ExperienceEntry.Create(owner.Id, new("Legacy experience", ExperienceSetting.PersonalProject, "Existing context", "Developer", "Implemented feature", "Compared alternatives", "Delivered", ImpactState.Qualitative, [concept.Id]), now);
        db.AddRange(owner, concept, observation);
        await db.SaveChangesAsync();
        // Seed through the historical transport: the current model includes columns added after InitialCreate.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO experience.entries (id, owner_id, is_archived, created_at, version) VALUES ({experience.Id}, {owner.Id}, false, {now}, 1)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO experience.entry_revisions (entry_id, number, title, setting, context, role, actions, alternatives, outcome, impact_state, approval_state, created_at) VALUES ({experience.Id}, 1, 'Legacy experience', 'PersonalProject', 'Existing context', 'Developer', 'Implemented feature', 'Compared alternatives', 'Delivered', 'Qualitative', 'Draft', {now})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO experience.entry_revision_concepts (entry_id, revision_number, concept_id) VALUES ({experience.Id}, 1, {concept.Id})");
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal("Legacy concept", (await db.Set<Concept>().SingleAsync(x => x.Id == concept.Id)).Name);
        Assert.Equal("Legacy observation", (await db.Set<EvidenceObservation>().SingleAsync(x => x.Id == observation.Id)).Aspect);
        var preserved = await db.Set<ExperienceEntry>().Include(x => x.Revisions).SingleAsync(x => x.Id == experience.Id);
        Assert.Equal("Legacy experience", preserved.CurrentRevision.Title);
        Assert.Equal("", preserved.CurrentRevision.Assistance);
        Assert.Equal("", preserved.CurrentRevision.Uncertainty);
        Assert.Empty(await db.Set<KnowledgeContribution>().ToArrayAsync());
        Assert.Empty(await db.Set<KnowledgeState>().ToArrayAsync());
    }
}
