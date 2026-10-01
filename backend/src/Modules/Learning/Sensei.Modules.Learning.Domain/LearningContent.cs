using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sensei.Modules.Learning.Domain;

public enum RelationKind { PartOf, Prerequisite, RelatedTo }
public enum ExerciseType { Flashcard, Prediction, MultipleChoice, Ordering, Matching, BugSpotting }
public sealed record ContentBlock(string Kind, string Text = "", string? Language = null, string? Url = null, string? Alt = null, string[]? Items = null);
public sealed record Choice(string Id, string Text);
public sealed record Interaction(Choice[] Choices, Choice[]? Counterparts = null, bool Multiple = false, bool OneToOne = true);
public sealed record Answer(string[] Selected, Dictionary<string, string>? Pairs = null, bool NoIssue = false);
public sealed record EvaluationDefinition(Answer[] Accepted, string Explanation, string ReferenceAnswer);
public static class ContentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value)
    {
        try { return JsonSerializer.Deserialize<T>(value, Options) ?? throw new ArgumentException("Missing content."); }
        catch (JsonException error) { throw new ArgumentException("Invalid content JSON.", error); }
    }
}
public sealed class ConceptRelation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SourceId { get; init; }
    public Guid TargetId { get; init; }
    public RelationKind Kind { get; init; }
    public static ConceptRelation Create(Guid source, Guid target, RelationKind kind, IEnumerable<ConceptRelation> existing)
    {
        if (source == Guid.Empty || target == Guid.Empty || source == target || !Enum.IsDefined(kind)) throw new ArgumentException("Invalid concept relation.");
        if (kind == RelationKind.RelatedTo && source.CompareTo(target) > 0) (source, target) = (target, source);
        var edges = existing.Where(e => e.Kind == kind).ToArray();
        if (edges.Any(e => e.SourceId == source && e.TargetId == target)) throw new ArgumentException("Duplicate relation.");
        var visited = new HashSet<Guid>();
        bool Reaches(Guid current) => current == source || (visited.Add(current) && edges.Where(e => e.SourceId == current).Any(e => Reaches(e.TargetId)));
        if (kind != RelationKind.RelatedTo && Reaches(target)) throw new ArgumentException("Relation would create a cycle.");
        return new() { SourceId = source, TargetId = target, Kind = kind };
    }
}
public sealed class MaterialVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ConceptId { get; init; }
    public string Locale { get; init; } = "en";
    public int SchemaVersion { get; init; } = 1;
    public string BlocksJson { get; init; } = "[]";
    public bool Available { get; set; } = true;
}
public sealed class ExerciseFamily
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Key { get; init; } = "";
}
public sealed class ExerciseVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ExerciseId { get; init; } = Guid.NewGuid();
    public int Revision { get; init; } = 1;
    public Guid FamilyId { get; init; }
    public Guid PrimaryConceptId { get; init; }
    public Guid? MaterialVersionId { get; init; }
    public ExerciseType Type { get; init; }
    public ConceptDifficulty Difficulty { get; init; } = ConceptDifficulty.Intermediate;
    public int SchemaVersion { get; init; } = 1;
    public string Locale { get; init; } = "en";
    public string PromptJson { get; init; } = "[]";
    public string InteractionJson { get; init; } = "{}";
    public string EvaluationJson { get; init; } = "{}";
    public string HintsJson { get; init; } = "[]";
    public string EvaluatorVersion { get; init; } = "binary-v1";
    public int EstimatedSeconds { get; init; } = 60;
    public bool Available { get; set; } = true;
}
public sealed class ExerciseConcept
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ExerciseVersionId { get; init; }
    public Guid ConceptId { get; init; }
}
public static class ContentValidation
{
    public static bool SafeUrl(string? url) => url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    public static void Blocks(IEnumerable<ContentBlock> blocks)
    {
        foreach (var b in blocks)
        {
            if (b is null) throw new ArgumentException("Missing content block.");
            if (b.Kind is not ("heading" or "paragraph" or "list" or "code" or "image" or "link")) throw new ArgumentException("Unsupported content block.");
            if (b.Kind is "image" or "link" && !SafeUrl(b.Url)) throw new ArgumentException("Only HTTPS content URLs are supported.");
            if (b.Kind == "image" && string.IsNullOrWhiteSpace(b.Alt)) throw new ArgumentException("Image alternative text is required.");
        }
    }
    public static void Exercise(ExerciseVersion exercise)
    {
        if (exercise.SchemaVersion != 1 || exercise.EvaluatorVersion != "binary-v1" || !Enum.IsDefined(exercise.Type) || exercise.EstimatedSeconds <= 0) throw new ArgumentException("Unsupported exercise definition.");
        Blocks(ContentJson.Read<ContentBlock[]>(exercise.PromptJson));
        var interaction = ContentJson.Read<Interaction>(exercise.InteractionJson);
        if (interaction.Choices is null || interaction.Choices.Any(c => string.IsNullOrWhiteSpace(c.Id)) || interaction.Choices.Select(c => c.Id).Distinct().Count() != interaction.Choices.Length) throw new ArgumentException("Invalid choice IDs.");
        if (exercise.Type != ExerciseType.Flashcard && interaction.Choices.Length == 0) throw new ArgumentException("An objective exercise needs choices.");
        if (exercise.Type == ExerciseType.Matching && (interaction.Counterparts is null || interaction.Counterparts.Any(c => string.IsNullOrWhiteSpace(c.Id)) || interaction.Counterparts.Select(c => c.Id).Distinct().Count() != interaction.Counterparts.Length)) throw new ArgumentException("Invalid counterpart IDs.");
        var evaluation = ContentJson.Read<EvaluationDefinition>(exercise.EvaluationJson);
        if (string.IsNullOrWhiteSpace(evaluation.Explanation) || string.IsNullOrWhiteSpace(evaluation.ReferenceAnswer)) throw new ArgumentException("Feedback is required.");
        if (evaluation.Accepted is null || evaluation.Accepted.Any(x => x is null) || exercise.Type != ExerciseType.Flashcard && evaluation.Accepted.Length == 0) throw new ArgumentException("Accepted answers are required.");
        if (ContentJson.Read<string[]>(exercise.HintsJson).Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Hints must contain text.");
        foreach (var answer in evaluation.Accepted) Evaluator.Validate(exercise.Type, interaction, answer);
    }
}
public static class Evaluator
{
    public static void Validate(ExerciseType type, Interaction interaction, Answer answer)
    {
        if (answer.Selected is null || answer.Selected.Distinct().Count() != answer.Selected.Length || answer.Selected.Any(id => !interaction.Choices.Any(c => c.Id == id))) throw new ArgumentException("Unknown or duplicate answer ID.");
        if (type != ExerciseType.BugSpotting && answer.NoIssue) throw new ArgumentException("No issue is not valid for this format.");
        if (type != ExerciseType.Matching && answer.Pairs is { Count: > 0 }) throw new ArgumentException("Unexpected matching answer.");
        switch (type)
        {
            case ExerciseType.Flashcard: throw new ArgumentException("Flashcards use self-review after reveal.");
            case ExerciseType.Prediction:
            case ExerciseType.MultipleChoice when !interaction.Multiple:
                if (answer.Selected.Length != 1) throw new ArgumentException("Select one answer."); break;
            case ExerciseType.MultipleChoice:
                if (answer.Selected.Length == 0) throw new ArgumentException("Select at least one answer."); break;
            case ExerciseType.Ordering:
                if (answer.Selected.Length != interaction.Choices.Length) throw new ArgumentException("Supply every item exactly once."); break;
            case ExerciseType.Matching:
                if (answer.Selected.Length != 0 || answer.Pairs is null || answer.Pairs.Count != interaction.Choices.Length || interaction.Choices.Any(c => !answer.Pairs.ContainsKey(c.Id)) || answer.Pairs.Values.Any(id => !(interaction.Counterparts ?? []).Any(c => c.Id == id)) || (interaction.OneToOne && answer.Pairs.Values.Distinct().Count() != answer.Pairs.Count)) throw new ArgumentException("Supply a complete valid mapping."); break;
            case ExerciseType.BugSpotting:
                if (answer.NoIssue == (answer.Selected.Length > 0)) throw new ArgumentException("Choose findings or No issue."); break;
        }
    }
    public static int Evaluate(ExerciseVersion exercise, Answer answer)
    {
        Validate(exercise.Type, ContentJson.Read<Interaction>(exercise.InteractionJson), answer);
        return ContentJson.Read<EvaluationDefinition>(exercise.EvaluationJson).Accepted.Any(expected =>
            expected.NoIssue == answer.NoIssue && (exercise.Type == ExerciseType.Matching
                ? expected.Pairs!.OrderBy(p => p.Key).SequenceEqual(answer.Pairs!.OrderBy(p => p.Key))
                : exercise.Type == ExerciseType.Ordering ? expected.Selected.SequenceEqual(answer.Selected) : expected.Selected.ToHashSet().SetEquals(answer.Selected))) ? 1 : 0;
    }
}
