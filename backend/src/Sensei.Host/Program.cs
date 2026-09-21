using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Host.Persistence;
using Sensei.Modules.Evidence.Api;
using Sensei.Modules.Evidence.Infrastructure;
using Sensei.Modules.Experience.Api;
using Sensei.Modules.Experience.Infrastructure;
using Sensei.Modules.Identity.Api;
using Sensei.Modules.Identity.Infrastructure;
using Sensei.Modules.Learning.Api;
using Sensei.Modules.Learning.Infrastructure;
using Sensei.Modules.WorkReflection.Api;
using Sensei.Modules.WorkReflection.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSenseiPersistence(
    builder.Configuration,
    typeof(IdentityInfrastructure).Assembly,
    typeof(LearningInfrastructure).Assembly,
    typeof(WorkReflectionInfrastructure).Assembly,
    typeof(EvidenceInfrastructure).Assembly,
    typeof(ExperienceInfrastructure).Assembly,
    typeof(Program).Assembly);

builder.Services
    .AddIdentityModule()
    .AddLearningModule()
    .AddWorkReflectionModule()
    .AddEvidenceModule()
    .AddExperienceModule();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SenseiDbContext>("postgresql", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        ArgumentException => (StatusCodes.Status400BadRequest, "Validation failed"),
        DuplicateResourceException => (StatusCodes.Status409Conflict, "Resource already exists"),
        ResourceInUseException => (StatusCodes.Status409Conflict, "Resource is in use"),
        ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
    };

    await Results.Problem(
        statusCode: status,
        title: title,
        detail: status == StatusCodes.Status500InternalServerError ? null : exception?.Message)
        .ExecuteAsync(context);
}));

app.MapGet("/", () => Results.Ok(new
{
    name = "Sensei++ API",
    version = "v1",
    persistence = "postgresql"
}));
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString().ToLowerInvariant())
        }));
    }
});

app.MapIdentityEndpoints();
app.MapLearningEndpoints();
app.MapWorkReflectionEndpoints();
app.MapEvidenceEndpoints();
app.MapExperienceEndpoints();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:SeedDevelopmentOwner"))
{
    await DevelopmentDataSeeder.SeedAsync(app.Services);
}

app.Run();

public partial class Program;
