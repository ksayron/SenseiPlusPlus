using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
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
using Sensei.Modules.Learning.Application;
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
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddOperationTransformer((operation, context, _) =>
    {
        var path = context.Description.RelativePath ?? "";
        var learning = path.StartsWith("api/v1/learning/", StringComparison.Ordinal) && !path.StartsWith("api/v1/learning/concepts", StringComparison.Ordinal);
        var knowledge = path.StartsWith("api/v1/evidence/knowledge", StringComparison.Ordinal);
        if (!learning && !knowledge) return Task.CompletedTask;
        operation.Responses ??= new OpenApiResponses();
        var publicContent = path.StartsWith("api/v1/learning/topics", StringComparison.Ordinal) || path.StartsWith("api/v1/learning/materials/", StringComparison.Ordinal) || path == "api/v1/learning/roadmaps" || path == "api/v1/learning/concept-relations";
        operation.Parameters ??= new List<IOpenApiParameter>();
        if (!publicContent) operation.Parameters.Add(new OpenApiParameter { Name = "X-Owner-Id", In = ParameterLocation.Header, Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" } });
        var mutation = context.Description.HttpMethod is "PUT" or "DELETE" || context.Description.HttpMethod == "POST" && path.Contains("/sessions/", StringComparison.Ordinal);
        if (mutation) operation.Parameters.Add(new OpenApiParameter { Name = "If-Match", In = ParameterLocation.Header, Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
        foreach (var status in mutation ? new[] { "400", "404", "409", "412", "428" } : new[] { "400", "404", "409" })
            operation.Responses.TryAdd(status, new OpenApiResponse { Description = "Problem Details with code and traceId", Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference("ProblemDetails", context.Document) } } });
        if (mutation || context.Description.HttpMethod == "POST" && (path.EndsWith("/sessions") || path.EndsWith("/goals") || path.EndsWith("/roadmap-enrollments")) || operation.OperationId == "GetLearningSession")
            foreach (var response in operation.Responses.Where(x => x.Key is "200" or "201").Select(x => x.Value).OfType<OpenApiResponse>())
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers["ETag"] = new OpenApiHeader { Description = "Current strong resource ETag; receiptVersionToken may describe an earlier committed response.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
        return Task.CompletedTask;
    });
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable machine-readable error code." };
            schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Request trace identifier for diagnostics." };
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

    foreach (var operation in document.Paths.Values.SelectMany(path => (path.Operations ?? []).Values))
    {
        foreach (var parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>())
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
            foreach (var response in (operation.Responses ?? []).Where(pair => pair.Key is "200" or "201").Select(pair => pair.Value).OfType<OpenApiResponse>())
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Strong ETag to send in If-Match for a later mutation.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
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

builder.Services.AddScoped<ILearningKnowledge, LearningKnowledgeAdapter>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    await next(context);
    if (context.Request.Path.StartsWithSegments("/api/v1/learning"))
    {
        LearningDiagnostics.RequestDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        if (context.Response.StatusCode is 409 or 412) LearningDiagnostics.Conflicts.Add(1);
        app.Logger.LogInformation("Learning request {Method} {Path} returned {Status} in {ElapsedMs}ms; trace {TraceId}",
            context.Request.Method, context.Request.Path, context.Response.StatusCode,
            System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
    }
});

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (code, status, title) = exception switch
    {
        ApiContractException api => (api.Code, api.StatusCode, api.Title),
        ArgumentException => ("validation_failed", StatusCodes.Status400BadRequest, "Validation failed"),
        ResourceNotFoundException => ("resource_not_found", StatusCodes.Status404NotFound, "Resource not found"),
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
app.MapLearningRuntime();
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
