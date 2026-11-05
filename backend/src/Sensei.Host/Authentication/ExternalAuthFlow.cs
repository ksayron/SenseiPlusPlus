using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sensei.Host.Authentication;

public sealed class ExternalAuthFlow
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string BindingHash { get; set; } = "";
    public Guid? OwnerId { get; set; }
    public string? SecurityStamp { get; set; }
    public string ReturnPath { get; set; } = "/";
    public string TimeZone { get; set; } = "UTC";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ChallengedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

internal sealed class ExternalAuthFlowMapping : IEntityTypeConfiguration<ExternalAuthFlow>
{
    public void Configure(EntityTypeBuilder<ExternalAuthFlow> b)
    {
        b.ToTable("external_auth_flows", "identity");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Provider).HasMaxLength(128);
        b.Property(x => x.Purpose).HasMaxLength(32);
        b.Property(x => x.BindingHash).HasMaxLength(64);
        b.Property(x => x.SecurityStamp).HasMaxLength(256);
        b.Property(x => x.ReturnPath).HasMaxLength(2048);
        b.Property(x => x.TimeZone).HasMaxLength(100);
        b.HasIndex(x => x.ExpiresAt);
    }
}
