using Sensei.Modules.Learning.Domain;
using Sensei.Modules.Evidence.Domain;
namespace Sensei.UnitTests;

public sealed class LearningPolicyTests
{
    [Fact]
    public void Selection_weights_unknowns_diversity_and_family_uniqueness_are_deterministic()
    {
        Assert.Equal(7, SelectionPolicy.Rank(true, 0, true, true, true));
        Assert.Equal(2, SelectionPolicy.Rank(false, null, true, false, false));
        Assert.Equal(-3, SelectionPolicy.Rank(false, 1, false, false, true));
        var concept = Guid.NewGuid(); var family = Guid.NewGuid();
        var first = new ExerciseVersion { PrimaryConceptId = concept, FamilyId = family, Type = ExerciseType.Prediction };
        var variant = new ExerciseVersion { PrimaryConceptId = concept, FamilyId = family, Type = ExerciseType.Prediction };
        var sameType = new ExerciseVersion { PrimaryConceptId = concept, FamilyId = Guid.NewGuid(), Type = ExerciseType.Prediction };
        var otherType = new ExerciseVersion { PrimaryConceptId = concept, FamilyId = Guid.NewGuid(), Type = ExerciseType.Ordering };
        var selected = SelectionPolicy.Diverse([first, variant, sameType, otherType], 10);
        Assert.Equal(new[] { first.Id, otherType.Id, sameType.Id }, selected.Select(x => x.Id));
        Assert.Equal(selected, SelectionPolicy.Diverse([first, variant, sameType, otherType], 10));
    }
    private static ExerciseVersion Exercise(ExerciseType type, Interaction interaction, params Answer[] accepted) => new() { Type = type, InteractionJson = ContentJson.Write(interaction), EvaluationJson = ContentJson.Write(new EvaluationDefinition(accepted, "Explanation", "Reference")) };
    [Fact]
    public void Graph_rejects_cycles_duplicates_and_self_edges_and_normalizes_related_pairs()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        foreach (var kind in new[] { RelationKind.PartOf, RelationKind.Prerequisite })
        {
            var edges = new[] { ConceptRelation.Create(a, b, kind, []), ConceptRelation.Create(b, c, kind, []) };
            Assert.Throws<ArgumentException>(() => ConceptRelation.Create(c, a, kind, edges));
            Assert.Throws<ArgumentException>(() => ConceptRelation.Create(a, b, kind, edges));
            Assert.Throws<ArgumentException>(() => ConceptRelation.Create(a, a, kind, []));
        }
        var related = ConceptRelation.Create(a, b, RelationKind.RelatedTo, []);
        Assert.Throws<ArgumentException>(() => ConceptRelation.Create(b, a, RelationKind.RelatedTo, [related]));
    }
    [Fact]
    public void Objective_formats_use_binary_exact_answers_and_authored_alternatives()
    {
        var interaction = new Interaction([new("a", "A"), new("b", "B"), new("c", "C")], Multiple: true);
        var multiple = Exercise(ExerciseType.MultipleChoice, interaction, new Answer(["a", "b"]));
        Assert.Equal(1, Evaluator.Evaluate(multiple, new(["b", "a"])));
        Assert.Equal(0, Evaluator.Evaluate(multiple, new(["a"])));
        Assert.Equal(0, Evaluator.Evaluate(multiple, new(["a", "b", "c"])));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(multiple, new(["a", "a"])));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(multiple, new(["unknown"])));
        var ordering = Exercise(ExerciseType.Ordering, interaction, new Answer(["a", "b", "c"]), new Answer(["b", "a", "c"]));
        Assert.Equal(1, Evaluator.Evaluate(ordering, new(["b", "a", "c"])));
        Assert.Equal(0, Evaluator.Evaluate(ordering, new(["c", "b", "a"])));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(ordering, new(["a", "b"])));
        var prediction = Exercise(ExerciseType.Prediction, interaction, new Answer(["a"]));
        Assert.Equal(0, Evaluator.Evaluate(prediction, new(["b"])));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(prediction, new(["a", "b"])));
        var bug = Exercise(ExerciseType.BugSpotting, interaction, new Answer([], NoIssue: true));
        Assert.Equal(1, Evaluator.Evaluate(bug, new([], NoIssue: true)));
        Assert.Equal(0, Evaluator.Evaluate(bug, new(["a"])));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(bug, new(["a"], NoIssue: true)));
    }
    [Fact]
    public void Matching_accepts_alternatives_and_rejects_incomplete_or_conflicting_mapping()
    {
        var exercise = Exercise(ExerciseType.Matching, new([new("a", "A"), new("b", "B")], [new("x", "X"), new("y", "Y")]),
            new Answer([], new() { { "a", "x" }, { "b", "y" } }), new Answer([], new() { { "a", "y" }, { "b", "x" } }));
        Assert.Equal(1, Evaluator.Evaluate(exercise, new([], new() { { "a", "y" }, { "b", "x" } })));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(exercise, new([], new() { { "a", "x" } })));
        Assert.Throws<ArgumentException>(() => Evaluator.Evaluate(exercise, new([], new() { { "a", "x" }, { "b", "x" } })));
    }
    private static KnowledgeContribution Contribution(int? score, bool assisted = false, bool admitted = false, Guid? family = null, bool retry = false, int minute = 0) => new() { FamilyId = family ?? Guid.NewGuid(), SessionId = Guid.NewGuid(), Score = score, Assisted = assisted, Admitted = admitted, AnswerAware = retry, SelfReview = score is null ? "Recalled" : null, ReceivedAt = DateTimeOffset.UnixEpoch.AddMinutes(minute) };
    [Fact]
    public void Projection_is_unknown_without_objective_evidence_and_self_review_uplift_is_capped()
    {
        Assert.Null(KnowledgePolicy.Estimate([Contribution(null, admitted: true)]));
        Assert.Equal(.05, KnowledgePolicy.Estimate([Contribution(0), Contribution(null, admitted: true)]));
        var objective = new[] { Contribution(1), Contribution(1), Contribution(1), Contribution(1), Contribution(0) };
        Assert.Equal(4.1 / 5.1, KnowledgePolicy.Estimate([.. objective, Contribution(null, admitted: true)]));
        Assert.Equal(4.1 / 5.1, KnowledgePolicy.Estimate([.. objective, Contribution(null, admitted: true), Contribution(null, admitted: true)]));
        Assert.Equal(1, KnowledgePolicy.Estimate([Contribution(1), Contribution(null, admitted: true)]));
    }
    [Fact]
    public void Projection_limits_distinct_families_and_excludes_answer_aware_retries()
    {
        var family = Guid.NewGuid(); var inputs = new[] { Contribution(0, family: family), Contribution(1, family: family, minute: 1), Contribution(0, retry: true, minute: 2) };
        Assert.Single(KnowledgePolicy.Objective(inputs)); Assert.Equal(1, KnowledgePolicy.Estimate(inputs));
        Assert.Equal(20, KnowledgePolicy.Objective(Enumerable.Range(0, 30).Select(i => Contribution(1, minute: i))).Length);
        Assert.Equal(1d / 3, KnowledgePolicy.Estimate([Contribution(1, assisted: true), Contribution(0)]));
        Assert.Single(KnowledgePolicy.Objective([Contribution(1, assisted: true), Contribution(0)], true));
    }
    [Fact]
    public void Cooldown_uses_exact_rolling_boundary()
    {
        var now = DateTimeOffset.UnixEpoch;
        Assert.True(KnowledgePolicy.CanAdmit(now, null));
        Assert.False(KnowledgePolicy.CanAdmit(now.AddHours(72).AddTicks(-1), now));
        Assert.True(KnowledgePolicy.CanAdmit(now.AddHours(72), now));
        Assert.True(KnowledgePolicy.CanAdmit(now.AddHours(73), now));
    }
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("http://example.org")]
    public void Unsafe_material_urls_are_rejected(string url) => Assert.Throws<ArgumentException>(() => ContentValidation.Blocks([new("link", Url: url)]));
    [Fact]
    public void Unsupported_content_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ContentValidation.Blocks([new("html", "<script>alert(1)</script>")]));
        Assert.Throws<ArgumentException>(() => ContentValidation.Blocks([new("image", Url: "https://example.org/image")]));
        Assert.Throws<ArgumentException>(() => ContentValidation.Exercise(new() { SchemaVersion = 99 }));
        Assert.Throws<ArgumentException>(() => ContentJson.Read<Interaction>("{broken"));
        Assert.Throws<ArgumentException>(() => ContentValidation.Exercise(new() { Type = ExerciseType.Flashcard, InteractionJson = "{\"choices\":[]}", EvaluationJson = "{\"accepted\":null,\"explanation\":\"Feedback\",\"referenceAnswer\":\"Reference\"}" }));
    }
}
