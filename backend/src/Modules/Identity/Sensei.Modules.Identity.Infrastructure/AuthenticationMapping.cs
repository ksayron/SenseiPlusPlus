using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensei.Modules.Identity.Domain;

namespace Sensei.Modules.Identity.Infrastructure;

// The credential identity shares the existing profile's stable ID. Existing profiles receive no password.
public sealed class LoginAccount : IdentityUser<Guid>;

internal sealed class LoginAccountMapping : IEntityTypeConfiguration<LoginAccount>
{
    public void Configure(EntityTypeBuilder<LoginAccount> b)
    {
        b.ToTable("login_accounts", "identity");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.UserName).HasMaxLength(256);
        b.Property(x => x.NormalizedUserName).HasMaxLength(256);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.NormalizedEmail).HasMaxLength(256);
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.HasIndex(x => x.NormalizedUserName).IsUnique();
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
        b.HasOne<UserAccount>().WithOne().HasForeignKey<LoginAccount>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoginClaimMapping : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b)
    {
        b.ToTable("login_claims", "identity"); b.HasKey(x => x.Id);
        b.HasOne<LoginAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoginProviderMapping : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b)
    {
        b.ToTable("login_providers", "identity"); b.HasKey(x => new { x.LoginProvider, x.ProviderKey });
        b.Property(x => x.LoginProvider).HasMaxLength(128); b.Property(x => x.ProviderKey).HasMaxLength(128);
        b.HasIndex(x => new { x.UserId, x.LoginProvider }).IsUnique();
        b.HasOne<LoginAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoginTokenMapping : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b)
    {
        b.ToTable("login_tokens", "identity"); b.HasKey(x => new { x.UserId, x.LoginProvider, x.Name });
        b.Property(x => x.LoginProvider).HasMaxLength(128); b.Property(x => x.Name).HasMaxLength(128);
        b.HasOne<LoginAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
