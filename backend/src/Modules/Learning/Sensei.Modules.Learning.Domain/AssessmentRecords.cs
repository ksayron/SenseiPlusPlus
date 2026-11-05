namespace Sensei.Modules.Learning.Domain;

public enum AssessmentState { Queued, Running, Retry, Succeeded, Failed, Cancelled, Obsolete }

/// <summary>One optional assessment of an already committed, immutable attempt.</summary>
public sealed class AssessmentJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid ExerciseVersionId { get; init; }
    public Guid OperationId { get; init; }
    public string CommandDigest { get; init; } = "";
    public string InputJson { get; init; } = "{}";
    public string InputDigest { get; init; } = "";
    public string RequestDigest { get; init; } = "";
    public string RouteJson { get; init; } = "{}";
    public string RouteDigest { get; init; } = "";
    public string DisclosureDigest { get; init; } = "";
    public string Origin { get; init; } = "Provider";
    public string TemplateVersion { get; init; } = "attempt-exploratory-v1";
    public string ContractVersion { get; init; } = "assessment-v1";
    public AssessmentState State { get; set; } = AssessmentState.Queued;
    public int Version { get; set; } = 1;
    public int Generation { get; set; }
    public long Fence { get; set; }
    public int InvocationCount { get; set; }
    public int TransientRetries { get; set; }
    public int Repairs { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? FailureCode { get; set; }

    public bool Terminal => State is AssessmentState.Succeeded or AssessmentState.Failed or AssessmentState.Cancelled or AssessmentState.Obsolete;
    public bool Owns(long fence, int generation, DateTimeOffset now) => State == AssessmentState.Running && Fence == fence && Generation == generation && LeaseUntil > now;
    public void Cancel(DateTimeOffset now)
    {
        if (Terminal) throw new InvalidOperationException("This assessment is already terminal.");
        Generation++; Fence++; State = AssessmentState.Cancelled; LeaseUntil = null; UpdatedAt = now; Version++;
    }
}

/// <summary>A conservative invocation reservation is committed before any model call.</summary>
public sealed class AssessmentInvocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid JobId { get; init; }
    public int Number { get; init; }
    public long Fence { get; init; }
    public int Generation { get; init; }
    public string RequestDigest { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? FailureCode { get; set; }
    public string AuditJson { get; set; } = "{}";
}

/// <summary>Append-only model feedback. Exploratory feedback does not change knowledge scores.</summary>
public sealed class AssessmentFeedbackRevision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid JobId { get; init; }
    public Guid InvocationId { get; init; }
    public Guid ExerciseVersionId { get; init; }
    public int Revision { get; init; }
    public string Scope { get; init; } = "Exploratory";
    public string Origin { get; init; } = "Provider";
    public string InputDigest { get; init; } = "";
    public string RouteDigest { get; init; } = "";
    public string TemplateVersion { get; init; } = "";
    public string ContractVersion { get; init; } = "";
    public string OutputJson { get; init; } = "{}";
    public string AuditJson { get; init; } = "{}";
    public DateTimeOffset CreatedAt { get; init; }
}
