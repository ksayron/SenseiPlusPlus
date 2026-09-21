using System.Text.Json.Serialization;
using Sensei.BuildingBlocks.Application;
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

builder.Services
    .AddIdentityModule()
    .AddLearningModule()
    .AddWorkReflectionModule()
    .AddEvidenceModule()
    .AddExperienceModule();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        ArgumentException => (StatusCodes.Status400BadRequest, "Validation failed"),
        DuplicateResourceException => (StatusCodes.Status409Conflict, "Resource already exists"),
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
    persistence = "in-memory"
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapIdentityEndpoints();
app.MapLearningEndpoints();
app.MapWorkReflectionEndpoints();
app.MapEvidenceEndpoints();
app.MapExperienceEndpoints();

app.Run();

public partial class Program;
