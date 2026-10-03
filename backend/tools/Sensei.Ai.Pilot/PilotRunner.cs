using System.Text.Json;
using Sensei.BuildingBlocks.Ai;
using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.Ai.Pilot;

public sealed record PilotCaseResult(string CaseId, string Category, string ExpectedVerdict, bool ExpectedAssistance,
    string ReferenceUrl, ModelResult Result);
public sealed record PilotReport(int SchemaVersion, string Mode, DateTimeOffset RecordedAt,
    ModelRoute? Route, PilotCaseResult[] Cases, bool HumanReviewed, string Decision);
public sealed record CaseReview(string CaseId, bool Acceptable, bool UnsafeApproval, bool InventedClaim, string Notes);
public sealed record PilotReview(string Reviewer, DateTimeOffset ReviewedAt, CaseReview[] Cases);
public sealed record ReviewedPilot(PilotReport Report, PilotReview Review, int AcceptableCount, bool EligibleForSelection);

public static class PilotRunner
{
    public static async Task<PilotReport> Fixtures(CancellationToken ct = default)
    {
        var cases = PilotCases.All();
        var audit = new ModelAudit("fixture-v1", "fixture", null, "stop", new(null, null, null), TimeSpan.Zero, ModelOrigin.Fixture);
        var gateway = new FixtureModelGateway(cases.ToDictionary(x => x.Request.Digest, x => new ModelResult(x.FixtureOutput, null, audit)));
        return await Run(gateway, cases, null, "fixture", ct);
    }
    public static Task<PilotReport> Live(IModelGateway gateway, ModelRoute route, CancellationToken ct = default) =>
        Run(gateway, PilotCases.InitialTwelve(), route, "live", ct);

    private static async Task<PilotReport> Run(IModelGateway gateway, PilotCase[] cases, ModelRoute? route, string mode, CancellationToken ct)
    {
        var results = new List<PilotCaseResult>();
        foreach (var item in cases)
        {
            // Cancellation between cases must still return the public evidence already recorded.
            if (ct.IsCancellationRequested) break;
            var result = await gateway.CompleteAsync(item.Request, ct);
            results.Add(new(item.Id, item.Category, item.ExpectedVerdict, item.Assisted, item.ReferenceUrl, result));
            // Do not consume more quota after a route/configuration/transport failure. Semantic failures remain reviewable.
            if (mode == "live" && result.Failure is ModelFailure.Configuration or ModelFailure.UnknownPrice or ModelFailure.NonzeroPrice or
                ModelFailure.MetadataUnavailable or ModelFailure.UnsupportedCapability or ModelFailure.RateLimited or
                ModelFailure.QuotaExhausted or ModelFailure.Timeout or ModelFailure.Cancelled or ModelFailure.ProviderUnavailable)
                break;
        }
        return new(1, mode, DateTimeOffset.UtcNow, route, results.ToArray(), false,
            mode == "fixture" ? "Synthetic replay verifies plumbing only; this is not model quality evidence." : "Pending human quality/privacy review; no route is selected automatically.");
    }

    public static ReviewedPilot Review(PilotReport report, PilotReview review)
    {
        if (report is null || review is null || report.Cases is null || review.Cases is null ||
            report.Cases.Any(x => x is null || x.Result is null || x.Result.Audit is null || x.Result.Audit.Usage is null) ||
            review.Cases.Any(x => x is null))
            throw new ArgumentException("A complete report and review record are required.");
        var expected = PilotCases.InitialTwelve().ToDictionary(x => x.Id);
        if (report.SchemaVersion != 1 || report.Mode != "live" || report.Route is null || report.Cases.Length != 12 ||
            report.Cases.Any(x => !expected.TryGetValue(x.CaseId, out var item) ||
                x.ExpectedVerdict != item.ExpectedVerdict || x.ExpectedAssistance != item.Assisted ||
                x.Result.Audit.Origin != ModelOrigin.Provider || x.Result.Audit.RouteDigest != report.Route.Digest ||
                x.Result.Audit.RequestDigest != item.Request.Digest || x.Result.Audit.OutputContractVersion != item.Request.Contract.Version) ||
            string.IsNullOrWhiteSpace(review.Reviewer) || review.Reviewer.Length > 200 ||
            review.ReviewedAt < report.RecordedAt || review.ReviewedAt > DateTimeOffset.UtcNow ||
            review.Cases.Length != 12 || review.Cases.Select(x => x.CaseId).Distinct().Count() != 12 ||
            !review.Cases.Select(x => x.CaseId).ToHashSet().SetEquals(report.Cases.Select(x => x.CaseId)))
            throw new ArgumentException("Review must identify the reviewer and all twelve cases from this live route snapshot.");
        foreach (var entry in review.Cases)
        {
            var result = report.Cases.Single(x => x.CaseId == entry.CaseId).Result;
            if (entry.Acceptable && (!result.Succeeded || !ValidOutput(expected[entry.CaseId], result) ||
                result.Audit.ActualProvider != report.Route.ProviderName ||
                result.Audit.ActualModel != report.Route.ModelId && result.Audit.ActualModel != report.Route.ModelId[..^5] ||
                result.Audit.Usage.Cost is not (null or 0)))
                throw new ArgumentException("A structural/provider failure cannot be accepted as technical feedback.");
            if (string.IsNullOrWhiteSpace(entry.Notes) || entry.Notes.Length > 4000) throw new ArgumentException("Every review needs bounded explanatory notes.");
        }
        var acceptable = review.Cases.Count(x => x.Acceptable);
        return new(report with { HumanReviewed = true, Decision = "Human review recorded; configuration and capability approval remain an operator decision." },
            review, acceptable, acceptable >= 10 && review.Cases.All(x => !x.UnsafeApproval && !x.InventedClaim));
    }

    private static bool ValidOutput(PilotCase item, ModelResult result)
    {
        try
        {
            return result.Output is { } output && output.ValueKind == JsonValueKind.Object &&
                item.Request.Contract.Accepts(output) && output.GetProperty("assisted").GetBoolean() == item.Assisted;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { return false; }
    }
}
