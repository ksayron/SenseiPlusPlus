using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Evidence.Infrastructure;
using Sensei.Modules.Experience.Infrastructure;
using Sensei.Modules.Identity.Infrastructure;
using Sensei.Modules.Learning.Infrastructure;
using Sensei.Modules.WorkReflection.Infrastructure;

namespace Sensei.Host.Persistence;

public sealed class SenseiDbContextFactory : IDesignTimeDbContextFactory<SenseiDbContext>
{
    public SenseiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sensei")
            ?? "Host=localhost;Port=5432;Database=sensei;Username=sensei;Password=sensei_local_dev";
        var options = new DbContextOptionsBuilder<SenseiDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("Sensei.Host"))
            .Options;
        var assemblies = new SenseiModelAssemblies([
            typeof(IdentityInfrastructure).Assembly,
            typeof(LearningInfrastructure).Assembly,
            typeof(WorkReflectionInfrastructure).Assembly,
            typeof(EvidenceInfrastructure).Assembly,
            typeof(ExperienceInfrastructure).Assembly,
            typeof(SenseiDbContextFactory).Assembly
        ]);
        return new SenseiDbContext(options, assemblies);
    }
}
