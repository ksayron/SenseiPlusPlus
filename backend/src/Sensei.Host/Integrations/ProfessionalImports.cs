using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.BuildingBlocks.Api;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Client.Contracts;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Identity.Domain;
using Sensei.Host.Authentication;

namespace Sensei.Host.Integrations;

public sealed class ProfessionalImport
{
    public Guid OwnerId { get; set; }
    public Guid EventId { get; set; }
    public Guid EntryId { get; set; }
    public string PayloadDigest { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
}
public sealed class ProfessionalImportMapping : IEntityTypeConfiguration<ProfessionalImport>
{
    public void Configure(EntityTypeBuilder<ProfessionalImport> b)
    {
        b.ToTable("professional_imports", "experience");
        b.HasKey(x => new { x.OwnerId, x.EventId });
        b.HasIndex(x => new { x.OwnerId, x.EntryId }).IsUnique();
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExperienceEntry>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EntryId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PayloadDigest).HasMaxLength(64);
    }
}
public static class ProfessionalImports
{
    public static void MapProfessionalImports(this WebApplication app)
    {
        app.MapPost("/api/v1/experience/imports", async (HttpContext c, SenseiDbContext db, TransactionRunner tx) =>
        {
            var owner = c.OwnerId();
            ProfessionalEventImportV1 payload;
            try
            {
                if (!c.Request.HasJsonContentType()) return HttpContract.Problem(c, "validation_failed", 400, "JSON required");
                using var buffer = new MemoryStream();
                var bytes = new byte[8192];
                while (true)
                {
                    var count = await c.Request.Body.ReadAsync(bytes, c.RequestAborted);
                    if (count == 0) break;
                    if (buffer.Length + count > ImportContract.MaximumBytes) return HttpContract.Problem(c, "request_too_large", 413, "Import exceeds 64 KiB");
                    buffer.Write(bytes, 0, count);
                }
                payload = ImportContract.Parse(buffer.ToArray());
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            { return HttpContract.Problem(c, "validation_failed", 400, "Invalid sanitized import"); }
            var digest = ImportContract.Digest(payload);
            var created = false;
            var receipt = await tx.RunAsync(async () =>
            {
                await tx.LockAsync($"professional-import:{owner}:{payload.EventId}", c.RequestAborted);
                var previous = await db.Set<ProfessionalImport>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EventId == payload.EventId, c.RequestAborted);
                if (previous is not null)
                {
                    if (previous.PayloadDigest != digest) throw new ApiContractException("event_payload_conflict", 409, "Event already imported", "This event UUID is bound to a different reviewed payload.");
                    return JsonSerializer.Deserialize<ImportReceipt>(previous.ReceiptJson, ImportContract.Json)!;
                }
                var ids = payload.ConceptIds;
                if (await db.Set<Concept>().CountAsync(x => ids.Contains(x.Id) && x.IsActive, c.RequestAborted) != ids.Length)
                    throw new ArgumentException("Concept selection is unavailable.");
                var now = DateTimeOffset.UtcNow;
                var entry = ExperienceEntry.Create(owner, new(payload.Title, Enum.Parse<ExperienceSetting>(payload.Setting),
                    payload.Context, payload.Role, payload.Actions, payload.Alternatives, payload.Outcome, ImpactState.Unknown,
                    ids, payload.Uncertainty, payload.Assistance), now);
                db.Add(entry);
                var observations = ids.Select(id => EvidenceObservation.Create(owner, id, payload.Title,
                    EvidenceSignalKind.ProfessionalExposure, EvidenceSourceKind.ProfessionalEventImport, payload.EventId,
                    AssistanceLevel.Unspecified, "Client reported; not assessed. " + payload.Uncertainty, now)).ToArray();
                db.AddRange(observations);
                var result = new ImportReceipt(payload.EventId, entry.Id, 1, observations.Select(x => x.Id).ToArray(), digest, now, $"/experience?entry={entry.Id}");
                db.Add(new ProfessionalImport { OwnerId = owner, EventId = payload.EventId, EntryId = entry.Id,
                    PayloadDigest = digest, PayloadJson = JsonSerializer.Serialize(payload, ImportContract.Json),
                    ReceiptJson = JsonSerializer.Serialize(result, ImportContract.Json) });
                created = true;
                return result;
            }, c.RequestAborted);
            return Results.Json(receipt, ImportContract.Json, statusCode: created ? 201 : 200);
        }).WithName("ImportProfessionalEvent").Accepts<ProfessionalEventImportV1>("application/json").Produces<ImportReceipt>(201).Produces<ImportReceipt>()
            .ProducesProblem(400).ProducesProblem(409).ProducesProblem(413).RequireAuthorization(AgentOAuth.WritePolicy).WithMetadata(new AgentBearerEndpoint());

        app.MapGet("/api/v1/experience/imports/{eventId:guid}", async (Guid eventId, HttpContext c, SenseiDbContext db) =>
        {
            var owner = c.OwnerId();
            var item = await db.Set<ProfessionalImport>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EventId == eventId, c.RequestAborted);
            return item is null ? HttpContract.NotFound(c) : Results.Text(item.ReceiptJson, "application/json");
        }).WithName("GetProfessionalImportReceipt").Produces<ImportReceipt>().RequireAuthorization(AgentOAuth.ReadPolicy).WithMetadata(new AgentBearerEndpoint());

        app.MapGet("/api/v1/experience/entries/{entryId:guid}/import", async (Guid entryId, HttpContext c, SenseiDbContext db) =>
        {
            var owner = c.OwnerId();
            var item = await db.Set<ProfessionalImport>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EntryId == entryId, c.RequestAborted);
            return item is null ? HttpContract.NotFound(c) : Results.Text(item.PayloadJson, "application/json");
        }).WithName("InspectExperienceImport").Produces<ProfessionalEventImportV1>().RequireAuthorization();
    }
}
