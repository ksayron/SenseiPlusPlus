using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sensei.BuildingBlocks.Persistence;
using Testcontainers.PostgreSql;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Sensei.Api.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "postgresql";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("sensei_tests")
        .WithUsername("sensei")
        .WithPassword("sensei_tests")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public SenseiWebApplicationFactory CreateFactory() => new(ConnectionString);
}

public sealed class SenseiWebApplicationFactory(string connectionString, bool testOwners = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sensei"] = connectionString,
                ["Database:SeedDevelopmentOwner"] = "false",
                ["Authentication:EnableTestOwnerHeader"] = testOwners.ToString()
            }));
        if (testOwners) builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestSelector";
                options.DefaultChallengeScheme = "TestSelector";
            }).AddScheme<AuthenticationSchemeOptions, TestOwnerHandler>("TestOwner", _ => { })
              .AddPolicyScheme("TestSelector", null, options => options.ForwardDefaultSelector = context =>
                  context.Request.Cookies.ContainsKey("sensei.session") ? IdentityConstants.ApplicationScheme : "TestOwner"));
    }
}
