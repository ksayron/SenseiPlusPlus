using System.Security.Cryptography;
using System.Text;
using Sensei.BuildingBlocks.Application;
using Sensei.Modules.Learning.Domain;
namespace Sensei.Modules.Learning.Application;

/// <summary>Small explicitly labeled fixtures, consumed through the same validated runtime contracts.</summary>
public sealed class LearningFixtures(ILearningStore store, ITransactionRunner tx, TimeProvider clock)
{
    private static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("sensei-fixture-v1:" + key)).AsSpan(0, 16));
    public async Task<bool> Seed(CancellationToken ct = default)
    {
        var added = await SeedCore(ct);
        return await tx.RunAsync(() => SeedAlternatives(ct), ct) || added;
    }
    private Task<bool> SeedCore(CancellationToken ct) => tx.RunAsync(async () =>
    {
        await tx.LockAsync("learning:fixture-v1", ct);
        if ((await store.Read<RoadmapVersion>(x => x.Id == Id("csharp-roadmap"), ct)).Length > 0) return false;
        var definitions = new[]{
            ("csharp-fixture","C# fundamentals","Small demonstration library; not a complete course."),
            ("value-reference-fixture","Value and reference semantics","Compare assignment of values and object references."),
            ("oop-fixture","Object-oriented design","Interfaces, inheritance and composition."),
            ("delegates-fixture","Delegates and events","Subscription, invocation and encapsulation."),
            ("orbital-fixture","Orbital workshop","Synthetic subject for engine-independence testing."),
            ("orbital-signals-fixture","Beacon signals","Rules of a fictional beacon system."),
            ("reading-fixture","Reading without exercises","A readable topic with no practice library."),
            ("practice-fixture","Practice without material","Exercises can stand alone.")};
        var concepts = new Dictionary<string, Concept>();
        foreach (var (key, name, description) in definitions)
        {
            var concept = (await store.Read<Concept>(x => x.Key == key, ct)).SingleOrDefault();
            if (concept is null) { concept = Concept.Create(key, name, description, "en", ConceptDifficulty.Intermediate, clock.GetUtcNow()); store.Add(concept); }
            concepts[key] = concept;
        }
        foreach (var child in new[] { "value-reference-fixture", "oop-fixture", "delegates-fixture" }) store.Add(new ConceptRelation { SourceId = concepts[child].Id, TargetId = concepts["csharp-fixture"].Id, Kind = RelationKind.PartOf });
        store.Add(new ConceptRelation { SourceId = concepts["orbital-signals-fixture"].Id, TargetId = concepts["orbital-fixture"].Id, Kind = RelationKind.PartOf });
        store.Add(new ConceptRelation { SourceId = concepts["value-reference-fixture"].Id, TargetId = concepts["oop-fixture"].Id, Kind = RelationKind.Prerequisite });
        var materials = new Dictionary<string, Guid>();
        foreach (var (key, name, description) in definitions.Where(x => x.Item1 != "practice-fixture"))
        {
            var blocks = new List<ContentBlock> { new("heading", name + " · fixture"), new("paragraph", description), new("list", Items: ["Read the prompt carefully.", "Notes are saved, not graded."]), new("link", "Reference: C# language documentation", Url: "https://learn.microsoft.com/en-us/dotnet/csharp/") };
            if (key == "value-reference-fixture") { blocks.Insert(2, new("paragraph", "Assigning a struct copies its value. Assigning a class variable copies the reference; both variables can refer to the same object. A struct can itself contain references.")); blocks.Add(new("code", "var first = new Counter { Value = 1 };\nvar second = first;\nsecond.Value = 2;", Language: "csharp")); blocks.Add(new("image", Url: "https://example.invalid/sensei-fixture-unavailable.svg", Alt: "Missing-asset fixture: two variables referring to one object.")); }
            if (key == "orbital-signals-fixture") { blocks[3] = new("paragraph", "In this fictional system, amber means wait; blue means transmit. Before transmission: power, align, send."); }
            ContentValidation.Blocks(blocks); var material = new MaterialVersion { Id = Id(key + "-material"), ConceptId = concepts[key].Id, BlocksJson = ContentJson.Write(blocks) }; store.Add(material); materials[key] = material.Id;
        }
        var choices = new[] { new Choice("a", "The copied value changes independently"), new Choice("b", "Both variables refer to the same object") };
        var data = new List<(string Key, string Topic, ExerciseType Type, string Prompt, Interaction Interaction, Answer[] Answers, string Explanation, string Reference)> {
            ("struct-assignment","value-reference-fixture",ExerciseType.Prediction,"A mutable struct is copied into another variable. The second variable's integer field is changed. What happens?",new(choices),[new(["a"])],"The struct's value was copied. Changing the copy's integer field does not change the original.","The copied value changes independently."),
            ("class-assignment","value-reference-fixture",ExerciseType.Prediction,"A class instance is assigned to another variable. What does assignment copy?",new(choices),[new(["b"])],"Class assignment copies the reference, not the object.","Both variables refer to the same object."),
            ("struct-facts","value-reference-fixture",ExerciseType.MultipleChoice,"Select every true statement.",new([new("a","A struct is a value type"),new("b","A struct can contain reference fields"),new("c","All structs are immutable")],Multiple:true),[new(["a","b"])],"Structs are value types and may contain references. They are not inherently immutable.","A and B."),
            ("boxing-order","value-reference-fixture",ExerciseType.Ordering,"Arrange the operations: create an integer, box it, then unbox it.",new([new("a","int n = 7;"),new("b","object boxed = n;"),new("c","int copy = (int)boxed;")]),[new(["a","b","c"])],"Each operation needs the value created by the preceding operation.","Create → box → unbox."),
            ("type-matching","value-reference-fixture",ExerciseType.Matching,"Match each category to its declaration.",new([new("a","Value type"),new("b","Reference type")],[new("struct","struct Point"),new("class","class Counter")]),[new([],new(){{"a","struct"},{"b","class"}})],"Struct and class declarations establish value and reference types respectively.","Value → struct; Reference → class."),
            ("independent-values","value-reference-fixture",ExerciseType.BugSpotting,"int first = 3; int second = first; second = 4; // first remains 3\nSelect an incorrect claim, or No issue.",new([new("a","first remains 3"),new("b","second becomes 4")],Multiple:true),[new([],NoIssue:true)],"Both claims are correct because int assignment copies the value.","No issue."),
            ("recall-values","value-reference-fixture",ExerciseType.Flashcard,"What is copied when you assign a class variable?",new([]),[],"This is a self-review, not an objective assessment.","The reference is copied; the object is not cloned."),
            ("oop-interface","oop-fixture",ExerciseType.MultipleChoice,"What does an interface primarily describe?",new([new("a","A contract implementers satisfy"),new("b","A unique object instance")]),[new(["a"])],"An interface expresses a contract for its implementers.","A contract implementers satisfy."),
            ("delegate-recall","delegates-fixture",ExerciseType.Flashcard,"Why expose an event instead of a public delegate field?",new([]),[],"Compare your recall with the reference.","An event restricts outside callers to subscribing and unsubscribing; invocation remains controlled by the declaring type."),
            ("signal-prediction","orbital-signals-fixture",ExerciseType.Prediction,"The beacon is amber. Which action follows the workshop rules?",new([new("a","Wait"),new("b","Transmit")]),[new(["a"])],"The fictional workshop defines amber as wait.","Wait."),
            ("signal-order","orbital-signals-fixture",ExerciseType.Ordering,"Order a transmission procedure.",new([new("a","Power"),new("b","Align"),new("c","Send")]),[new(["a","b","c"])],"The workshop procedure is power, align, then send.","Power → align → send."),
            ("standalone","practice-fixture",ExerciseType.Prediction,"Choose the even integer.",new([new("a","2"),new("b","3")]),[new(["a"])],"2 is divisible by 2.","2.")
        };
        foreach (var d in data)
        {
            var family = new ExerciseFamily { Id = Id(d.Key + "-family"), Key = d.Key }; store.Add(family);
            var exercise = new ExerciseVersion { Id = Id(d.Key + "-v1"), ExerciseId = Id(d.Key), FamilyId = family.Id, PrimaryConceptId = concepts[d.Topic].Id, MaterialVersionId = materials.GetValueOrDefault(d.Topic) is var m && m != Guid.Empty ? m : null, Type = d.Type, PromptJson = ContentJson.Write(new[] { new ContentBlock("paragraph", d.Prompt) }), InteractionJson = ContentJson.Write(d.Interaction), EvaluationJson = ContentJson.Write(new EvaluationDefinition(d.Answers, d.Explanation, d.Reference)), HintsJson = ContentJson.Write(new[] { "Identify the rule being applied before choosing an answer." }) };
            ContentValidation.Exercise(exercise); store.Add(exercise); store.Add(new ExerciseConcept { ExerciseVersionId = exercise.Id, ConceptId = concepts[d.Topic].Id });
            if (d.Key == "struct-assignment")
            {
                var replacement = new ExerciseVersion { Id = Id(d.Key + "-v2"), ExerciseId = exercise.ExerciseId, Revision = 2, FamilyId = family.Id, PrimaryConceptId = exercise.PrimaryConceptId, MaterialVersionId = exercise.MaterialVersionId, Type = exercise.Type, PromptJson = exercise.PromptJson, InteractionJson = exercise.InteractionJson, EvaluationJson = exercise.EvaluationJson, HintsJson = exercise.HintsJson };
                exercise.Available = false; ContentValidation.Exercise(replacement); store.Add(replacement); store.Add(new ExerciseConcept { ExerciseVersionId = replacement.Id, ConceptId = exercise.PrimaryConceptId });
                var variant = new ExerciseVersion { Id = Id(d.Key + "-variant"), ExerciseId = Id(d.Key + "-variant-logical"), FamilyId = family.Id, PrimaryConceptId = exercise.PrimaryConceptId, MaterialVersionId = exercise.MaterialVersionId, Type = exercise.Type, PromptJson = ContentJson.Write(new[] { new ContentBlock("paragraph", "A Point struct with integer fields is copied. The copy is changed. Which rule applies?") }), InteractionJson = exercise.InteractionJson, EvaluationJson = exercise.EvaluationJson, HintsJson = exercise.HintsJson };
                ContentValidation.Exercise(variant); store.Add(variant); store.Add(new ExerciseConcept { ExerciseVersionId = variant.Id, ConceptId = exercise.PrimaryConceptId });
            }
        }
        foreach (var subject in new[] { "csharp", "orbital" })
        {
            var roadmap = new RoadmapVersion { Id = Id(subject + "-roadmap"), Title = subject == "csharp" ? "C# fundamentals · fixture" : "Orbital workshop · fixture", Description = "Demonstration content. Evidence and traversal are separate." }; store.Add(roadmap);
            var keys = subject == "csharp" ? new[] { "value-reference-fixture", "oop-fixture", "delegates-fixture" } : new[] { "orbital-signals-fixture" };
            for (var i = 0; i < keys.Length; i++) store.Add(new RoadmapStage { Id = Id(keys[i] + "-stage"), RoadmapVersionId = roadmap.Id, ConceptId = concepts[keys[i]].Id, Title = concepts[keys[i]].Name, Position = i });
        }
        return true;
    }, ct);

    private async Task<bool> SeedAlternatives(CancellationToken ct)
    {
        await tx.LockAsync("learning:fixture-v1", ct);
        if ((await store.Read<RoadmapVersion>(x => x.Id == Id("review-roadmap"), ct)).Length > 0) return false;
        var concepts = await store.Read<Concept>(_ => true, ct);
        var orbital = concepts.SingleOrDefault(x => x.Key == "orbital-signals-fixture");
        if (orbital is null) return false;
        var value = concepts.Single(x => x.Key == "value-reference-fixture");
        var alternatives = new[] {
            (Key: "parallel-preparation", Type: ExerciseType.Ordering, Prompt: "Power and align can happen in either order. Send only after both are complete.",
                Interaction: new Interaction([new("a", "Power"), new("b", "Align"), new("c", "Send")]),
                Answers: new[] { new Answer(["a", "b", "c"]), new Answer(["b", "a", "c"]) }),
            (Key: "equivalent-beacons", Type: ExerciseType.Matching, Prompt: "Assign each transmitter to a different compatible beacon. Both beacons are compatible with both transmitters.",
                Interaction: new Interaction([new("a", "Transmitter A"), new("b", "Transmitter B")], [new("x", "Beacon X"), new("y", "Beacon Y")]),
                Answers: new[] { new Answer([], new() { ["a"] = "x", ["b"] = "y" }), new Answer([], new() { ["a"] = "y", ["b"] = "x" }) })
        };
        foreach (var definition in alternatives)
        {
            var family = new ExerciseFamily { Id = Id(definition.Key + "-family"), Key = definition.Key };
            var exercise = new ExerciseVersion
            {
                Id = Id(definition.Key),
                ExerciseId = Id(definition.Key + "-logical"),
                FamilyId = family.Id,
                PrimaryConceptId = orbital.Id,
                Type = definition.Type,
                PromptJson = ContentJson.Write(new[] { new ContentBlock("paragraph", definition.Prompt) }),
                InteractionJson = ContentJson.Write(definition.Interaction),
                EvaluationJson = ContentJson.Write(new EvaluationDefinition(definition.Answers, "Both authored alternatives satisfy the stated constraints.", "Either of the two valid alternatives is accepted."))
            };
            ContentValidation.Exercise(exercise);
            store.Add(family); store.Add(exercise); store.Add(new ExerciseConcept { ExerciseVersionId = exercise.Id, ConceptId = orbital.Id });
        }
        var roadmap = new RoadmapVersion { Id = Id("review-roadmap"), Title = "C# review · fixture", Description = "Another route using the same value-semantics evidence." };
        store.Add(roadmap);
        store.Add(new RoadmapStage { Id = Id("review-stage"), RoadmapVersionId = roadmap.Id, ConceptId = value.Id, Title = value.Name });
        return true;
    }
}
