namespace Sensei.Modules.Evidence.Domain;

public sealed class KnowledgeContribution
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ObservationId { get; init; }
    public Guid OwnerId { get; init; }
    public Guid ConceptId { get; init; }
    public Guid ResultId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid SessionId { get; init; }
    public int? Score { get; init; }
    public bool Assisted { get; init; }
    public bool AnswerAware { get; init; }
    public string? SelfReview { get; init; }
    public bool Admitted { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public string PolicyVersion { get; init; } = KnowledgePolicy.Version;
}
public sealed class SelfReviewAllowance
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid ConceptId { get; init; }
    public DateTimeOffset LastAdmittedAt { get; set; }
}
public sealed class KnowledgeState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid ConceptId { get; init; }
    public double? Estimate { get; set; }
    public string PolicyVersion { get; set; } = KnowledgePolicy.Version;
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class EvidenceStatusRevision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ObservationId { get; init; }
    public EvidenceStatus Status { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}
public static class KnowledgePolicy
{
    public const string Version = "knowledge-v1";
    public const int Window = 20;
    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(72);
    public static bool CanAdmit(DateTimeOffset now, DateTimeOffset? last) => last is null || now >= last.Value + Cooldown;
    public static KnowledgeContribution[] Objective(IEnumerable<KnowledgeContribution> source, bool unassistedOnly = false) => source
        .Where(x => x.Score is not null && !x.AnswerAware && (!unassistedOnly || !x.Assisted))
        .OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id)
        .GroupBy(x => x.FamilyId).Select(g => g.First()).Take(Window).ToArray();
    public static double? Estimate(IEnumerable<KnowledgeContribution> source)
    {
        var all = source.ToArray(); var objective = Objective(all);
        var weight = objective.Sum(x => x.Assisted ? .5 : 1);
        if (weight == 0) return null;
        var numerator = objective.Sum(x => x.Score!.Value * (x.Assisted ? .5 : 1));
        var value = numerator / weight;
        return all.Any(x => x.Admitted) ? Math.Min(Math.Min((numerator + .1) / (weight + .1), value + .05), 1) : value;
    }
}
