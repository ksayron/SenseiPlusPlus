using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sensei.BuildingBlocks.Api;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestAssessment(Guid OperationId, string DisclosureDigest);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelAssessment(Guid OperationId);
public sealed record AssessmentFeedbackResource(Guid Id, int Revision, string Scope, string Origin, Guid ExerciseVersionId,
    string InputDigest, string RouteDigest, string TemplateVersion, string ContractVersion, JsonElement Output, JsonElement Provenance, DateTimeOffset CreatedAt);
public sealed record AssessmentJobResource(Guid Id, Guid AttemptId, AssessmentState State, string VersionToken, string? FailureCode,
    int InvocationCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? NotBefore, bool Replayed, AssessmentFeedbackResource? Feedback);

public static class AssessmentEndpoints
{
    private static AssessmentJobResource Map(AssessmentJobView view)
    {
        var j = view.Job; var f = view.Feedback;
        return new(j.Id, j.AttemptId, j.State, HttpContract.VersionToken(j.Version), j.FailureCode, j.InvocationCount,
            j.CreatedAt, j.UpdatedAt, j.State == AssessmentState.Retry ? j.NotBefore : null, view.Replayed,
            f is null ? null : new(f.Id, f.Revision, f.Scope, f.Origin, f.ExerciseVersionId, f.InputDigest, f.RouteDigest,
                f.TemplateVersion, f.ContractVersion, JsonSerializer.Deserialize<JsonElement>(f.OutputJson), JsonSerializer.Deserialize<JsonElement>(f.AuditJson), f.CreatedAt));
    }
    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/v1/learning").WithTags("Optional assessment").RequireAuthorization();
        g.MapPost("/attempts/{attempt:guid}/assessment-preview", async (Guid attempt, HttpContext c, AssessmentService s, CancellationToken ct) =>
            Results.Ok(await s.PreviewAsync(c.OwnerId(), attempt, ct))).WithName("PreviewAttemptAssessment").Produces<AssessmentPreview>();
        g.MapPost("/attempts/{attempt:guid}/assessments", async (Guid attempt, RequestAssessment body, HttpContext c, AssessmentService s, CancellationToken ct) =>
        {
            try
            {
                var v = await s.RequestAsync(c.OwnerId(), attempt, new(body.OperationId, body.DisclosureDigest), ct);
                c.Response.Headers.ETag = $"\"{HttpContract.VersionToken(v.Job.Version)}\"";
                return Results.Accepted($"/api/v1/learning/assessments/{v.Job.Id}", Map(v));
            }
            catch (AssessmentUnavailableException error) { return HttpContract.Problem(c, "assessment_unavailable", 503, "Optional assessment unavailable", error.Message); }
        }).WithName("RequestAttemptAssessment").Produces<AssessmentJobResource>(202).ProducesProblem(503);
        g.MapGet("/attempts/{attempt:guid}/assessments", async (Guid attempt, int? limit, string? cursor, HttpContext c, AssessmentService s, CancellationToken ct) =>
        {
            var values = (await s.ListAsync(c.OwnerId(), attempt, ct)).Select(Map).ToArray();
            return Results.Ok(HttpContract.Page(values, limit, cursor, $"assessments:{c.OwnerId()}:{attempt}", x => x.CreatedAt.ToString("O"), x => x.Id));
        }).WithName("ListAttemptAssessments").Produces<PageEnvelope<AssessmentJobResource>>();
        g.MapGet("/assessments/{id:guid}", async (Guid id, HttpContext c, AssessmentService s, CancellationToken ct) =>
        {
            var v = await s.GetAsync(c.OwnerId(), id, ct); return HttpContract.OkVersioned(c.Response, Map(v), v.Job.Version);
        }).WithName("GetAttemptAssessment").Produces<AssessmentJobResource>();
        g.MapPost("/assessments/{id:guid}/cancel", async (Guid id, CancelAssessment body, HttpContext c, AssessmentService s, CancellationToken ct) =>
        {
            var v = await s.CancelAsync(c.OwnerId(), id, HttpContract.RequireVersion(c.Request), body.OperationId, ct);
            return HttpContract.OkVersioned(c.Response, Map(v), v.Job.Version);
        }).WithName("CancelAttemptAssessment").Produces<AssessmentJobResource>();
        return endpoints;
    }
}
