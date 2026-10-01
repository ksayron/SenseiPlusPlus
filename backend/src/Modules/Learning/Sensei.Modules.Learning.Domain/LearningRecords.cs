namespace Sensei.Modules.Learning.Domain;

public enum SessionState { Active, Paused, Completed, EndedEarly }
public enum ItemOutcome { Answered, SelfReviewed, Skipped }
public sealed class LearningSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid? ConceptId { get; init; }
    public Guid? RoadmapStageId { get; init; }
    public Guid? EnrollmentId { get; init; }
    public int RequestedCount { get; init; }
    public int ActualCount { get; init; }
    public string FeedbackPolicy { get; init; } = "Immediate";
    public string SelectionPolicy { get; init; } = "selection-v1";
    public SessionState State { get; set; } = SessionState.Active;
    public int Position { get; set; }
    public int Version { get; set; } = 1;
    public string ReturnContext { get; init; } = "/learn";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class SessionItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ExerciseVersionId { get; init; }
    public int Position { get; init; }
    public string DraftJson { get; set; } = "null";
    public string Note { get; set; } = "";
    public ItemOutcome? Outcome { get; set; }
    public bool HintUsed { get; set; }
    public bool ReferenceUsed { get; set; }
    public bool Revealed { get; set; }
}
public sealed class LearningAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ItemId { get; init; }
    public Guid ExerciseVersionId { get; init; }
    public Guid? PreviousAttemptId { get; init; }
    public string AnswerJson { get; init; } = "null";
    public string Note { get; init; } = "";
    public bool Assisted { get; init; }
    public bool AnswerAware { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}
public sealed class ExerciseResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid AttemptId { get; init; }
    public string EvaluatorVersion { get; init; } = "binary-v1";
    public int? Score { get; init; }
    public string? SelfReview { get; init; }
}
public sealed class LearningEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid OperationId { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string Kind { get; init; } = "";
    public Guid? SessionId { get; init; }
    public Guid? ItemId { get; init; }
    public Guid? AttemptId { get; init; }
    public Guid? ExerciseVersionId { get; init; }
    public Guid? MaterialVersionId { get; init; }
    public string Origin { get; init; } = "web";
    public DateTimeOffset? ClientRecordedAt { get; init; }
    public string? DeviceId { get; init; }
    public long? DeviceSequence { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}
public sealed class OperationReceipt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid OperationId { get; init; }
    public string Kind { get; init; } = "";
    public Guid TargetId { get; init; }
    public string Digest { get; init; } = "";
    public Guid OutcomeId { get; init; }
    public int ResponseVersion { get; init; }
}
public sealed class LearningGoal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public string Intention { get; set; } = "";
    public Guid? RoadmapVersionId { get; set; }
    public bool Archived { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class GoalConcept
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GoalId { get; init; }
    public Guid ConceptId { get; init; }
}
public sealed class RoadmapVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Locale { get; init; } = "en";
    public bool Available { get; set; } = true;
}
public sealed class RoadmapStage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RoadmapVersionId { get; init; }
    public Guid ConceptId { get; init; }
    public string Title { get; init; } = "";
    public int Position { get; init; }
    public int MinimumFamilies { get; init; } = 3;
    public int MinimumSessions { get; init; } = 2;
    public double MinimumCorrectness { get; init; } = .8;
}
public sealed class RoadmapEnrollment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid RoadmapVersionId { get; init; }
    public bool Paused { get; set; }
    public Guid? CurrentStageId { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class StageTraversal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EnrollmentId { get; init; }
    public Guid StageId { get; init; }
    public string State { get; set; } = "Visited";
}
