using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sensei.BuildingBlocks.Application;

namespace Sensei.BuildingBlocks.Persistence;

public sealed record SenseiModelAssemblies(IReadOnlyCollection<Assembly> Assemblies);

public sealed class SenseiDbContext(
    DbContextOptions<SenseiDbContext> options,
    SenseiModelAssemblies modelAssemblies) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var assembly in modelAssemblies.Assemblies.Distinct())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The resource changed after it was loaded. Reload it and retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateResourceException("A resource with the same unique value already exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new ResourceInUseException("The operation would break an existing resource relationship.");
        }
    }
}

public static class PersistenceRegistration
{
    public static IServiceCollection AddSenseiPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] modelAssemblies)
    {
        var connectionString = configuration.GetConnectionString("Sensei")
            ?? throw new InvalidOperationException("Connection string 'Sensei' is required.");

        services.AddSingleton(new SenseiModelAssemblies(modelAssemblies));
        services.AddDbContext<SenseiDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly("Sensei.Host")));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<SenseiDbContext>());
        return services;
    }
}
