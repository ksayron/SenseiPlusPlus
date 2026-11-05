using System.Text.Json;
using System.Text.Json.Serialization;
using Sensei.Ai.Pilot;
using Sensei.BuildingBlocks.Ai;
using Sensei.BuildingBlocks.Application.Models;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
};
if (args.Length == 0)
{
    Console.WriteLine("Usage: --fixtures OUTPUT | --live ROUTE_FILE OUTPUT | --review-template REPORT OUTPUT | --review REPORT REVIEWS OUTPUT");
    return 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    object report;
    string output;
    if (args is ["--fixtures", var fixtureOutput])
    {
        report = await PilotRunner.Fixtures(cancellation.Token); output = fixtureOutput;
    }
    else if (args is ["--live", var routeFile, var liveOutput])
    {
        var route = JsonSerializer.Deserialize<ModelRoute>(await File.ReadAllTextAsync(routeFile, cancellation.Token), json)
            ?? throw new ArgumentException("A route snapshot is required.");
        using var gateway = new OpenRouterModelGateway(route, () => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
        report = await PilotRunner.Live(gateway, route, cancellation.Token); output = liveOutput;
    }
    else if (args is ["--review-template", var templateReportFile, var templateOutput])
    {
        var previous = JsonSerializer.Deserialize<PilotReport>(await File.ReadAllTextAsync(templateReportFile, cancellation.Token), json)
            ?? throw new ArgumentException("A pilot report is required.");
        report = new
        {
            reviewer = "", reviewedAt = DateTimeOffset.UtcNow,
            cases = previous.Cases.Select(item => new
            {
                caseId = item.CaseId, acceptable = (bool?)null, unsafeApproval = (bool?)null,
                inventedClaim = (bool?)null, notes = ""
            }).ToArray()
        };
        output = templateOutput;
    }
    else if (args is ["--review", var reportFile, var reviewsFile, var reviewedOutput])
    {
        var previous = JsonSerializer.Deserialize<PilotReport>(await File.ReadAllTextAsync(reportFile, cancellation.Token), json)
            ?? throw new ArgumentException("A pilot report is required.");
        var review = JsonSerializer.Deserialize<PilotReview>(await File.ReadAllTextAsync(reviewsFile, cancellation.Token), json)
            ?? throw new ArgumentException("A review record is required.");
        report = PilotRunner.Review(previous, review); output = reviewedOutput;
    }
    else throw new ArgumentException("Unknown command. Use --fixtures, --live, --review-template or --review.");
    // Preserve the completed public cases, including a cancellation result, before returning.
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, json), CancellationToken.None);
    Console.WriteLine($"Pilot record saved: {Path.GetFullPath(output)}");
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Pilot cancelled."); return 2; }
catch (Exception error) when (error is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Pilot input or output is invalid. Check the command, route/review schema, paths and file permissions.");
    return 2;
}
