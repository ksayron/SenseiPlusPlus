using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Application;
using Sensei.Modules.Identity.Domain;

namespace Sensei.Modules.Identity.Infrastructure;

public static class IdentityInfrastructure
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services) => services
        .AddScoped<IUserRepository, EfUserRepository>()
        .AddScoped<IUserService, UserService>();
}

internal sealed class EfUserRepository(SenseiDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken) =>
        await dbContext.Set<UserAccount>().AddAsync(user, cancellationToken);

    public Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<UserAccount>().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<UserAccount>()
            .AsNoTracking()
            .OrderBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .ToArrayAsync(cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Set<UserAccount>()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<UserAccount>().SingleAsync(user => user.Id == id, cancellationToken);
        dbContext.Remove(user);
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("users", "identity", table =>
        {
            table.HasCheckConstraint("ck_users_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_users_version_positive", "version >= 1");
        });
        builder.HasKey(user => user.Id).HasName("pk_users");
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(user => user.UiLocale).HasColumnName("ui_locale").HasMaxLength(32).IsRequired();
        builder.Property(user => user.AnswerLanguage).HasColumnName("answer_language").HasMaxLength(32).IsRequired();
        builder.Property(user => user.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(user => user.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("ux_users_email");
        builder.HasIndex(user => new { user.CreatedAt, user.Id }).HasDatabaseName("ix_users_created_at_id");
    }
}
