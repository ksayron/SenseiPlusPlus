using System.Text.Json;
using System.Text.Json.Serialization;
using Sensei.Ai.Pilot;
using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.UnitTests;

public sealed class PilotTests
{
    private static ModelRoute Route => new("pilot-test-v1", "example/model:free", "example/endpoint", "Example",
        ModelOutputMode.ValidatedJson, new("https://example.test/privacy"), DateTimeOffset.UtcNow);
    private sealed class ControlledGateway(ModelRoute route, ModelFailure? failure = null) : IModelGateway
    {
        public int Calls { get; private set; }
        public Task<ModelResult> CompleteAsync(ModelRequest request, CancellationToken ct = default)
        {
            Calls++;
            var example = PilotCases.InitialTwelve().Single(x => x.Request.Digest == request.Digest);
            return Task.FromResult(new ModelResult(failure is null ? example.FixtureOutput : null, failure,
                new(route.Revision, route.ModelId, route.ProviderName, "stop", new(10, 20, 0), TimeSpan.FromMilliseconds(10),
                    ModelOrigin.Provider, route.Digest, request.Digest, request.Contract.Version)));
        }
    }
    private static PilotReview Reviews(PilotReport report, bool acceptable = true) => new("Test reviewer", DateTimeOffset.UtcNow,
        report.Cases.Select(x => new CaseReview(x.CaseId, acceptable, false, false, "Inspected technical claim and source reference.")).ToArray());

    private sealed class CancelBetweenCasesGateway(IModelGateway inner, CancellationTokenSource cancellation) : IModelGateway
    {
        public async Task<ModelResult> CompleteAsync(ModelRequest request, CancellationToken ct = default)
        {
            var result = await inner.CompleteAsync(request, ct);
            cancellation.Cancel();
            return result;
        }
    }

    [Fact]
    public async Task Cancellation_between_cases_preserves_completed_public_evidence()
    {
        var route = Route;
        using var cancellation = new CancellationTokenSource();
        var gateway = new CancelBetweenCasesGateway(new ControlledGateway(route), cancellation);
        var report = await PilotRunner.Live(gateway, route, cancellation.Token);
        Assert.Single(report.Cases);
        Assert.True(report.Cases[0].Result.Succeeded);
        Assert.False(report.HumanReviewed);
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, Reviews(report)));
    }

    [Fact]
    public async Task Twenty_four_exact_fixtures_preserve_categories_assistance_and_holdout()
    {
        var cases = PilotCases.All();
        Assert.Equal(24, cases.Length); Assert.Equal(24, cases.Select(x => x.Id).Distinct().Count());
        Assert.All(cases.GroupBy(x => x.Category), group => Assert.Equal(4, group.Count()));
        Assert.Equal(12, PilotCases.InitialTwelve().Length);
        var report = await PilotRunner.Fixtures();
        Assert.False(report.HumanReviewed); Assert.Equal("fixture", report.Mode);
        Assert.All(report.Cases, item =>
        {
            Assert.True(item.Result.Succeeded); Assert.Equal(ModelOrigin.Fixture, item.Result.Audit.Origin);
            Assert.Equal(item.ExpectedVerdict, item.Result.Output!.Value.GetProperty("verdict").GetString());
            Assert.Equal(item.ExpectedAssistance, item.Result.Output.Value.GetProperty("assisted").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(item.Result.Audit.RequestDigest));
        });
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, Reviews(report)));
    }

    [Fact]
    public async Task Human_review_requires_the_complete_route_snapshot_and_explicit_quality_notes()
    {
        var route = Route; var report = await PilotRunner.Live(new ControlledGateway(route), route);
        Assert.Equal(12, report.Cases.Length); Assert.False(report.HumanReviewed);
        var review = Reviews(report);
        Assert.True(PilotRunner.Review(report, review).EligibleForSelection);
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, review with { Reviewer = "" }));
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, review with { Cases = review.Cases[..11] }));
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report with { Route = route with { ProviderName = "Changed" } }, review));
        var changed = review.Cases.ToArray(); changed[0] = changed[0] with { Notes = "" };
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, review with { Cases = changed }));
    }

    [Fact]
    public async Task Ten_of_twelve_and_zero_unsafe_promotions_are_required_for_selection_eligibility()
    {
        var route = Route; var report = await PilotRunner.Live(new ControlledGateway(route), route);
        var review = Reviews(report);
        var entries = review.Cases.ToArray();
        for (var i = 0; i < 3; i++) entries[i] = entries[i] with { Acceptable = false };
        Assert.False(PilotRunner.Review(report, review with { Cases = entries }).EligibleForSelection);
        entries[2] = entries[2] with { Acceptable = true };
        Assert.True(PilotRunner.Review(report, review with { Cases = entries }).EligibleForSelection);
        entries[2] = entries[2] with { UnsafeApproval = true };
        Assert.False(PilotRunner.Review(report, review with { Cases = entries }).EligibleForSelection);
        entries[2] = entries[2] with { UnsafeApproval = false, InventedClaim = true };
        Assert.False(PilotRunner.Review(report, review with { Cases = entries }).EligibleForSelection);
    }

    [Fact]
    public async Task Rate_failure_stops_the_pilot_and_structural_failures_cannot_pass_review()
    {
        var route = Route; var blocked = new ControlledGateway(route, ModelFailure.RateLimited);
        var report = await PilotRunner.Live(blocked, route);
        Assert.Single(report.Cases); Assert.Equal(1, blocked.Calls);
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(report, Reviews(report)));
        var malformed = await PilotRunner.Live(new ControlledGateway(route, ModelFailure.Malformed), route);
        Assert.Equal(12, malformed.Cases.Length);
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(malformed, Reviews(malformed)));
        Assert.False(PilotRunner.Review(malformed, Reviews(malformed, false)).EligibleForSelection);
    }

    [Theory]
    [InlineData("{\"verdict\":\"Supported\",\"reason\":\"Reason\",\"uncertainty\":\"None\",\"assisted\":false,\"referenceIds\":[\"invented\"]}")]
    [InlineData("{\"verdict\":\"Supported\",\"reason\":\"Reason\",\"uncertainty\":\"None\",\"assisted\":false,\"referenceIds\":[\"reference-int-copy\"],\"approved\":true}")]
    public void Invented_references_and_extra_authority_fields_are_rejected(string value)
    {
        using var output = JsonDocument.Parse(value);
        Assert.False(new PilotOutputContract("reference-int-copy").Accepts(output.RootElement));
    }

    [Fact]
    public async Task Serialized_reports_retain_route_binding_and_corrupted_success_output_cannot_be_accepted()
    {
        var route = Route; var report = await PilotRunner.Live(new ControlledGateway(route), route);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };
        var restored = JsonSerializer.Deserialize<PilotReport>(JsonSerializer.Serialize(report, options), options)!;
        Assert.True(PilotRunner.Review(restored, Reviews(restored)).EligibleForSelection);
        using var invalid = JsonDocument.Parse("[]");
        var rows = restored.Cases.ToArray();
        rows[0] = rows[0] with { Result = rows[0].Result with { Output = invalid.RootElement.Clone() } };
        var corrupted = restored with { Cases = rows };
        Assert.Throws<ArgumentException>(() => PilotRunner.Review(corrupted, Reviews(corrupted)));
    }
}
