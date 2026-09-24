using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sensei.BuildingBlocks.Api;
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

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
    {
        StatusCodes.Status400BadRequest => "validation_failed",
        StatusCodes.Status404NotFound => "resource_not_found",
        StatusCodes.Status409Conflict => "resource_conflict",
        StatusCodes.Status412PreconditionFailed => "precondition_failed",
        StatusCodes.Status428PreconditionRequired => "precondition_required",
        _ => "unexpected_error"
    });
    if (context.ProblemDetails.Status >= 500)
    {
        context.ProblemDetails.Detail = null;
    }
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties["code"] = new OpenApiSchema { Type = "string", Description = "Stable machine-readable error code." };
            schema.Properties["traceId"] = new OpenApiSchema { Type = "string", Description = "Request trace identifier for diagnostics." };
        }
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Sensei++ API";
    document.Info.Version = "v1";
    document.Info.Description = "Versioned API. Owner-scoped operations require X-Owner-Id during development; " +
        "this header selects an owner and is not authentication. Production authentication is not implemented yet. " +
        "Resources return an opaque versionToken and an ETag header. Send that ETag as a single strong If-Match " +
        "header on updates, approvals, and DELETE actions. Missing If-Match returns 428, malformed tokens return 400, " +
        "and stale tokens return 412. Creates do not require If-Match and return the initial ETag. " +
        "Lists return { items, nextCursor }; limit defaults to 25 and accepts 1 to 100. Pass nextCursor as cursor " +
        "with the same owner and filters. Errors use application/problem+json with code and traceId.";

    foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations.Values))
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            parameter.Description = parameter.Name switch
            {
                "X-Owner-Id" => "Required development owner selector (UUID). This is not authentication.",
                "If-Match" => "Strong ETag of the resource's current version, including double quotes. Required for this mutation.",
                "cursor" => "Opaque nextCursor from the preceding page. Keep owner and filters unchanged.",
                "limit" => "Page size from 1 to 100; defaults to 25.",
                _ => parameter.Description
            };
            if (parameter.Name == "If-Match") parameter.Required = true;
        }

        if (operation.OperationId is "CreateUser" or "GetUser" or "UpdateUser" or
            "CreateConcept" or "GetConcept" or "UpdateConcept" or
            "CreateWorkEpisode" or "GetWorkEpisode" or "UpdateWorkEpisode" or
            "CreateEvidenceObservation" or "GetEvidenceObservation" or "ChangeEvidenceStatus" or
            "CreateExperienceEntry" or "GetExperienceEntry" or "ReviseExperienceEntry" or "ApproveExperienceRevision")
        {
            foreach (var response in operation.Responses.Where(pair => pair.Key is "200" or "201"))
            {
                response.Value.Headers ??= new Dictionary<string, OpenApiHeader>();
                response.Value.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Strong ETag to send in If-Match for a later mutation.",
                    Schema = new OpenApiSchema { Type = "string" }
                };
            }
        }
    }

    return Task.CompletedTask;
    });
});
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
    var (code, status, title) = exception switch
    {
        ApiContractException api => (api.Code, api.StatusCode, api.Title),
        ArgumentException => ("validation_failed", StatusCodes.Status400BadRequest, "Validation failed"),
        DuplicateResourceException => ("resource_conflict", StatusCodes.Status409Conflict, "Resource conflict"),
        ResourceInUseException => ("resource_in_use", StatusCodes.Status409Conflict, "Resource is in use"),
        ConcurrencyConflictException => ("precondition_failed", StatusCodes.Status412PreconditionFailed, "Precondition failed"),
        InvalidOperationException => ("resource_conflict", StatusCodes.Status409Conflict, "Resource conflict"),
        _ => ("unexpected_error", StatusCodes.Status500InternalServerError, "Unexpected server error")
    };

    await HttpContract.Problem(
        context,
        code,
        status,
        title,
        status == StatusCodes.Status500InternalServerError ? null : exception?.Message)
        .ExecuteAsync(context);
}));

app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (context.Response.StatusCode == StatusCodes.Status404NotFound)
    {
        await HttpContract.NotFound(context).ExecuteAsync(context);
    }
});

app.MapGet("/", () => Results.Ok(new
{
    name = "Sensei++ API",
    version = "v1",
    persistence = "postgresql"
}));
app.MapGet("/health", async (HealthCheckService healthChecks) =>
{
    var report = await healthChecks.CheckHealthAsync();
    var response = new HealthResponse(
        report.Status.ToString().ToLowerInvariant(),
        report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString().ToLowerInvariant()));
    return Results.Json(response, statusCode: report.Status == HealthStatus.Healthy ? 200 : 503);
})
.WithName("GetHealth")
.Produces<HealthResponse>(StatusCodes.Status200OK)
.Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
app.MapOpenApi("/openapi/{documentName}.json");

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

public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);
