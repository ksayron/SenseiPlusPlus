using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Evidence.Application;
using Sensei.Modules.Evidence.Domain;
namespace Sensei.Modules.Evidence.Infrastructure;

public sealed class KnowledgeStore(SenseiDbContext db) : IKnowledgeStore
{
    public Task<T[]> Read<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => db.Set<T>().Where(predicate).ToArrayAsync(ct);
    public void Add<T>(T entity) where T : class => db.Add(entity);
}
internal static class KnowledgeMapping
{
    public static void Map<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table, "evidence"); b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever();
        foreach (var p in typeof(T).GetProperties())
        {
            var property = b.Property(p.Name).HasColumnName(Regex.Replace(p.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
            if (p.PropertyType.IsEnum) property.HasConversion<string>();
        }
    }
}
internal sealed class KnowledgeContributionMapping : IEntityTypeConfiguration<KnowledgeContribution> { public void Configure(EntityTypeBuilder<KnowledgeContribution> b) { KnowledgeMapping.Map(b, "knowledge_contributions"); b.HasIndex(x => new { x.ResultId, x.ConceptId }).IsUnique(); b.HasOne<EvidenceObservation>().WithMany().HasForeignKey(x => new { x.OwnerId, x.ConceptId, x.ObservationId }).HasPrincipalKey(x => new { x.OwnerId, x.ConceptId, x.Id }).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class SelfReviewAllowanceMapping : IEntityTypeConfiguration<SelfReviewAllowance> { public void Configure(EntityTypeBuilder<SelfReviewAllowance> b) { KnowledgeMapping.Map(b, "self_review_allowances"); b.HasIndex(x => new { x.OwnerId, x.ConceptId }).IsUnique(); } }
internal sealed class KnowledgeStateMapping : IEntityTypeConfiguration<KnowledgeState> { public void Configure(EntityTypeBuilder<KnowledgeState> b) { KnowledgeMapping.Map(b, "knowledge_states"); b.HasIndex(x => new { x.OwnerId, x.ConceptId }).IsUnique(); } }
internal sealed class EvidenceStatusRevisionMapping : IEntityTypeConfiguration<EvidenceStatusRevision> { public void Configure(EntityTypeBuilder<EvidenceStatusRevision> b) { KnowledgeMapping.Map(b, "status_revisions"); b.HasOne<EvidenceObservation>().WithMany().HasForeignKey(x => x.ObservationId).OnDelete(DeleteBehavior.Restrict); } }
