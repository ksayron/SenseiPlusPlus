using Sensei.Host.Authentication;
using Sensei.Host.Security;
using Sensei.Host.Integrations;
using OpenIddict.Validation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
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
builder.Logging.AddProvider(new PrivateConsoleLoggerProvider(Console.Out));
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = RequestSecurity.MaximumBodyBytes;
});
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("OpenIddict", LogLevel.None);

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
        operation.Parameters ??= new List<IOpenApiParameter>();

        var mutation = context.Description.HttpMethod is "PUT" or "DELETE" || context.Description.HttpMethod == "POST" && (path.Contains("/sessions/", StringComparison.Ordinal) || path.EndsWith("/cancel", StringComparison.Ordinal));
        if (mutation) operation.Parameters.Add(new OpenApiParameter { Name = "If-Match", In = ParameterLocation.Header, Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
        foreach (var status in mutation ? new[] { "400", "404", "409", "412", "428" } : new[] { "400", "404", "409" })
            operation.Responses.TryAdd(status, new OpenApiResponse { Description = "Problem Details with code and traceId", Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference("ProblemDetails", context.Document) } } });
        if (mutation || context.Description.HttpMethod == "POST" && (path.EndsWith("/sessions") || path.EndsWith("/goals") || path.EndsWith("/roadmap-enrollments") || path.EndsWith("/assessments")) || operation.OperationId is "GetLearningSession" or "GetAttemptAssessment")
            foreach (var response in operation.Responses.Where(x => x.Key is "200" or "201" or "202").Select(x => x.Value).OfType<OpenApiResponse>())
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
    document.Info.Description = "Versioned API. Sign in using the HttpOnly sensei.session cookie. Owner IDs are derived from the authenticated principal. " +
        "Fetch /api/v1/auth/csrf and send X-CSRF-Token on every cookie-authenticated state-changing API request. Explicit agent routes accept scoped bearer credentials instead of cookies and are exempt from CSRF; an Authorization header does not exempt other routes. Local registration is disabled outside Development and Testing. " +
        "Resources return an opaque versionToken and an ETag header. Send that ETag as a single strong If-Match " +
        "header on updates, approvals, and DELETE actions. Missing If-Match returns 428, malformed tokens return 400, " +
        "and stale tokens return 412. Creates do not require If-Match and return the initial ETag. " +
        "Lists return { items, nextCursor }; limit defaults to 25 and accepts 1 to 100. Pass nextCursor as cursor " +
        "with the same owner and filters. Errors use application/problem+json with code and traceId.";

    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["SessionCookie"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "sensei.session" };
    document.Components.SecuritySchemes["AgentBearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", Description = "Scoped CLI access token; browser cookies are not accepted on agent routes." };
    foreach (var path in document.Paths)
    foreach (var entry in path.Value.Operations ?? [])
    {
        var op = entry.Value;
        var agentEndpoint = path.Key.StartsWith("/api/v1/agent/", StringComparison.Ordinal) || path.Key.StartsWith("/api/v1/experience/imports", StringComparison.Ordinal);
        var anonymous = path.Key is "/" or "/health" or "/api/v1/auth/csrf" or "/api/v1/auth/login" or "/api/v1/auth/register" or "/api/v1/auth/providers" or "/api/v1/auth/external/challenge" or "/api/v1/auth/external/complete" ||
            path.Key.StartsWith("/api/v1/auth/external/", StringComparison.Ordinal) && path.Key.EndsWith("/start", StringComparison.Ordinal);
        if (!anonymous)
        {
            op.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(agentEndpoint ? "AgentBearer" : "SessionCookie", document)] = [] }];
            op.Responses ??= new OpenApiResponses();
            op.Responses.TryAdd("401", new OpenApiResponse { Description = "Sign in required" });
        }
        if (!agentEndpoint && entry.Key != HttpMethod.Get && path.Key.StartsWith("/api/", StringComparison.Ordinal))
        {
            op.Parameters ??= new List<IOpenApiParameter>();
            op.Parameters.Add(new OpenApiParameter { Name = "X-CSRF-Token", In = ParameterLocation.Header, Required = true, Description = "Request token from /api/v1/auth/csrf for the current sign-in state.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
        }
    }
    foreach (var operation in document.Paths.Values.SelectMany(path => (path.Operations ?? []).Values))
    {
        foreach (var parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>())
        {
            parameter.Description = parameter.Name switch
            {
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
builder.Services.AddScoped<Sensei.Modules.Evidence.Application.IEvidenceSourceAccess, EvidenceSourceAccess>();

builder.Services.AddSenseiAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAgentOAuth(builder.Configuration, builder.Environment);

var app = builder.Build();

app.Use(RequestSecurity.HeadersAndDiagnostics);

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (code, status, title) = exception switch
    {
        BadHttpRequestException { StatusCode: 413 } => ("request_too_large", StatusCodes.Status413PayloadTooLarge, "Request too large"),
        BadHttpRequestException => ("validation_failed", StatusCodes.Status400BadRequest, "Validation failed"),
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
        SafeExceptionDetail(exception))
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

app.Use(async (c, next) =>
{
    if (c.Request.Path.StartsWithSegments("/connect")) await AgentOAuth.SeedClient(c.RequestServices);
    await next(c);
});
app.Use(async (c, next) =>
{
    if (c.Request.Path.StartsWithSegments("/api/v1/auth/external") || c.Request.Path is { Value: "/signin-google" or "/signin-github" })
    {
        c.Response.Headers.CacheControl = "no-store";
        if (ExternalProviders.Names.Any(x => ExternalProviders.Enabled(c, x)) && !ExternalProviders.OriginMatches(c))
        { await HttpContract.Problem(c, "external_origin_mismatch", 400, "Open the configured authentication origin").ExecuteAsync(c); return; }
    }
    await next(c);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (c, next) =>
{
    if (c.GetEndpoint()?.Metadata.GetMetadata<AgentBearerEndpoint>() is not null)
    {
        var auth = await c.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        if (!auth.Succeeded || !await AgentOAuth.ValidGrant(auth.Principal!, c.RequestServices.GetRequiredService<SenseiDbContext>(),
            c.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Sensei.Modules.Identity.Infrastructure.LoginAccount>>(), c.RequestAborted))
        { await HttpContract.Problem(c, "authentication_required", 401, "Sign in required").ExecuteAsync(c); return; }
        c.User = auth.Principal!;
    }
    await next(c);
});
app.Use(AuthenticationSetup.ValidateCsrf);
app.Use(RequestSecurity.BoundBody);
app.MapSenseiAuthentication();
app.MapExternalAuthentication();
app.MapAgentOAuth();
app.MapProfessionalImports();

app.MapIdentityEndpoints();
app.MapLearningEndpoints();
app.MapLearningRuntime();
app.MapAssessmentEndpoints();
app.MapWorkReflectionEndpoints();
app.MapEvidenceEndpoints();
app.MapExperienceEndpoints();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:SeedDevelopmentOwner"))
{
    await DevelopmentDataSeeder.SeedAsync(app.Services);
}

if (args.Contains("--recover-local-account", StringComparer.Ordinal))
{
    await LocalAccountRecovery.RunAsync(app);
    return;
}

app.Run();

static string? SafeExceptionDetail(Exception? error) => error switch
{
    // ApiContractException is an explicit, constant-message HTTP contract, never a raw parser/provider error.
    ApiContractException api => api.Message,
    BadHttpRequestException { StatusCode: 413 } => "API request bodies are limited to 256 KiB.",
    BadHttpRequestException => "The request does not match the API contract.",
    ArgumentException => "Check the supplied values against the API contract.",
    ResourceNotFoundException => "The requested resource does not exist or is outside the current owner scope.",
    DuplicateResourceException => "A resource with the same unique value already exists.",
    ResourceInUseException => "The operation would break an existing resource relationship.",
    ConcurrencyConflictException => "The resource changed after it was loaded. Reload it and retry.",
    InvalidOperationException => "The operation is not allowed in the resource's current state.",
    _ => null
};

public partial class Program;

public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);
