using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sensei.Modules.Identity.Infrastructure;

// Authentication proves control of this provider identity; it does not assess authored work.
public sealed class ConnectedProfile
{
    public string Provider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public Guid OwnerId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Username { get; set; }
    public string? ProfileUrl { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset RefreshedAt { get; set; }
}

internal sealed class ConnectedProfileMapping : IEntityTypeConfiguration<ConnectedProfile>
{
    public void Configure(EntityTypeBuilder<ConnectedProfile> b)
    {
        b.ToTable("connected_profiles", "identity");
        b.HasKey(x => new { x.Provider, x.ProviderKey });
        b.Property(x => x.Provider).HasMaxLength(128);
        b.Property(x => x.ProviderKey).HasMaxLength(128);
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.Username).HasMaxLength(100);
        b.Property(x => x.ProfileUrl).HasMaxLength(512);
        b.HasIndex(x => new { x.OwnerId, x.Provider }).IsUnique();
        b.HasOne<IdentityUserLogin<Guid>>().WithOne()
            .HasForeignKey<ConnectedProfile>(x => new { x.Provider, x.ProviderKey }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<LoginAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
