using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.WorkReflection.Application;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.Modules.WorkReflection.Infrastructure;

internal sealed class EfContextRepository(SenseiDbContext db) : IContextRepository
{
    public Task<ContextSnapshot?> Snapshot(Guid owner, Guid episode, Guid id, CancellationToken ct) =>
        db.Set<ContextSnapshot>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EpisodeId == episode && x.Id == id, ct);
    public Task<ContextSnapshot?> SnapshotOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct) =>
        db.Set<ContextSnapshot>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EpisodeId == episode && x.OperationId == operation, ct);
    public async Task<IReadOnlyCollection<ContextSnapshot>> Snapshots(Guid owner, Guid episode, CancellationToken ct) =>
        await db.Set<ContextSnapshot>().AsNoTracking().Where(x => x.OwnerId == owner && x.EpisodeId == episode).OrderByDescending(x => x.Number).ToArrayAsync(ct);
    public Task<InterpretationRevision?> Interpretation(Guid owner, Guid episode, Guid snapshot, Guid id, CancellationToken ct) =>
        db.Set<InterpretationRevision>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EpisodeId == episode && x.SnapshotId == snapshot && x.Id == id, ct);
    public Task<InterpretationRevision?> InterpretationOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct) =>
        db.Set<InterpretationRevision>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EpisodeId == episode && x.OperationId == operation, ct);
    public async Task<IReadOnlyCollection<InterpretationRevision>> Interpretations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct) =>
        await db.Set<InterpretationRevision>().AsNoTracking().Where(x => x.OwnerId == owner && x.EpisodeId == episode && x.SnapshotId == snapshot)
            .OrderByDescending(x => x.Number).ToArrayAsync(ct);
    public Task<ContextConfirmation?> ConfirmationOperation(Guid owner, Guid episode, Guid operation, CancellationToken ct) =>
        db.Set<ContextConfirmation>().SingleOrDefaultAsync(x => x.OwnerId == owner && x.EpisodeId == episode && x.OperationId == operation, ct);
    public async Task<IReadOnlyCollection<ContextConfirmation>> Confirmations(Guid owner, Guid episode, Guid snapshot, CancellationToken ct) =>
        await db.Set<ContextConfirmation>().AsNoTracking().Where(x => x.OwnerId == owner && x.EpisodeId == episode && x.SnapshotId == snapshot)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToArrayAsync(ct);
    public void Add(ContextSnapshot snapshot) => db.Add(snapshot);
    public void Add(InterpretationRevision revision) => db.Add(revision);
    public void Add(ContextConfirmation confirmation) => db.Add(confirmation);
}

internal sealed class ContextSnapshotConfiguration : IEntityTypeConfiguration<ContextSnapshot>
{
    public void Configure(EntityTypeBuilder<ContextSnapshot> b)
    {
        b.ToTable("context_snapshots", "work_reflection", t => {
            t.HasCheckConstraint("ck_context_number", "number > 0");
            t.HasCheckConstraint("ck_context_scope", "data_scope IN ('Restricted','PublicSynthetic')");
            t.HasCheckConstraint("ck_context_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OwnerId, x.EpisodeId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.EpisodeId).HasColumnName("episode_id");
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.RequestDigest).HasColumnName("request_digest").HasMaxLength(64);
        b.Property(x => x.SourceJson).HasColumnName("source_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.SourceFingerprint).HasColumnName("source_fingerprint").HasMaxLength(64);
        b.Property(x => x.FingerprintVersion).HasColumnName("fingerprint_version").HasMaxLength(64);
        b.Property(x => x.DataScope).HasColumnName("data_scope").HasConversion<string>();
        b.Property(x => x.DisclosureJson).HasColumnName("disclosure_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.DisclosureDigest).HasColumnName("disclosure_digest").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.OwnerId, x.EpisodeId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.EpisodeId, x.OperationId }).IsUnique();
        b.HasOne<WorkEpisode>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EpisodeId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InterpretationRevisionConfiguration : IEntityTypeConfiguration<InterpretationRevision>
{
    public void Configure(EntityTypeBuilder<InterpretationRevision> b)
    {
        b.ToTable("interpretation_revisions", "work_reflection", t => {
            t.HasCheckConstraint("ck_interpretation_number", "number > 0");
            t.HasCheckConstraint("ck_interpretation_origin", "origin IN ('User','Fixture','Provider') AND (origin = 'User' OR provenance_json IS NOT NULL)");
            t.HasCheckConstraint("ck_interpretation_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.EpisodeId).HasColumnName("episode_id");
        b.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.SupersedesId).HasColumnName("supersedes_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.RequestDigest).HasColumnName("request_digest").HasMaxLength(64);
        b.Property(x => x.Origin).HasColumnName("origin").HasConversion<string>();
        b.Property(x => x.TextJson).HasColumnName("text_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ProvenanceJson).HasColumnName("provenance_json").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.EpisodeId, x.OperationId }).IsUnique();
        b.HasOne<ContextSnapshot>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId })
            .HasPrincipalKey(x => new { x.OwnerId, x.EpisodeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InterpretationRevision>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.SupersedesId })
            .HasPrincipalKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContextConfirmationConfiguration : IEntityTypeConfiguration<ContextConfirmation>
{
    public void Configure(EntityTypeBuilder<ContextConfirmation> b)
    {
        b.ToTable("context_confirmations", "work_reflection", t =>
            t.HasCheckConstraint("ck_confirmation_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.EpisodeId).HasColumnName("episode_id");
        b.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
        b.Property(x => x.InterpretationId).HasColumnName("interpretation_id");
        b.Property(x => x.SourceFingerprint).HasColumnName("source_fingerprint").HasMaxLength(64);
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.RequestDigest).HasColumnName("request_digest").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.OwnerId, x.EpisodeId, x.OperationId }).IsUnique();
        b.HasOne<InterpretationRevision>().WithMany().HasForeignKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.InterpretationId })
            .HasPrincipalKey(x => new { x.OwnerId, x.EpisodeId, x.SnapshotId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
