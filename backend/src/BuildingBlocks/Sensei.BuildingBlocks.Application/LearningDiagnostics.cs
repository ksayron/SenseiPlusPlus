using System.Diagnostics.Metrics;

namespace Sensei.BuildingBlocks.Application;

/// <summary>Bounded labels only: no answers, notes, owner IDs or content bodies.</summary>
public static class LearningDiagnostics
{
    public static readonly Meter Meter = new("Sensei.Learning", "1.0");
    public static readonly Counter<long> Replays = Meter.CreateCounter<long>("learning.operation.replays");
    public static readonly Counter<long> Conflicts = Meter.CreateCounter<long>("learning.operation.conflicts");
    public static readonly Counter<long> ProjectionFailures = Meter.CreateCounter<long>("learning.projection.failures");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("learning.request.duration", "ms");
}
