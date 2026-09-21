using Sensei.Modules.Evidence.Domain;
using Sensei.Modules.Experience.Domain;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Learning.Domain;
using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.UnitTests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void Validation_rejects_incomplete_aggregates()
    {
        Assert.Throws<ArgumentException>(() => UserAccount.Create("invalid", "Name", "en", "en", "UTC", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Concept.Create(" ", "Name", "Description", "en", ConceptDifficulty.Beginner, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => WorkEpisode.Create(Guid.Empty, "Title", WorkSetting.Employment, DateOnly.FromDateTime(DateTime.UtcNow), "Role", "Summary", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Mutations_increment_versions_and_reject_stale_versions()
    {
        var concept = Concept.Create("dotnet", ".NET", "Runtime", "en", ConceptDifficulty.Intermediate, DateTimeOffset.UtcNow);

        concept.Update("Modern .NET", "Runtime and SDK", "en", ConceptDifficulty.Advanced, 1);

        Assert.Equal(2, concept.Version);
        Assert.Throws<InvalidOperationException>(() => concept.Deactivate(1));
    }

    [Fact]
    public void Terminal_evidence_cannot_transition_again()
    {
        var evidence = EvidenceObservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "recall", EvidenceSignalKind.Recall,
            EvidenceSourceKind.Attempt, Guid.NewGuid(), AssistanceLevel.None, "closed book", DateTimeOffset.UtcNow);

        evidence.ChangeStatus(EvidenceStatus.Withdrawn, 1);

        Assert.Equal(2, evidence.Version);
        Assert.Throws<InvalidOperationException>(() => evidence.ChangeStatus(EvidenceStatus.Active, 2));
    }

    [Fact]
    public void Experience_revisions_are_immutable_snapshots()
    {
        var firstConcept = Guid.NewGuid();
        var entry = ExperienceEntry.Create(Guid.NewGuid(), Content("First", firstConcept), DateTimeOffset.UtcNow);

        entry.Revise(Content("Second", Guid.NewGuid()), 1, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(2, entry.Version);
        Assert.Equal(["First", "Second"], entry.Revisions.Select(revision => revision.Title));
        Assert.Equal([firstConcept], entry.Revisions.First().ConceptIds);
    }

    [Fact]
    public void Approval_requires_the_current_exact_revision()
    {
        var entry = ExperienceEntry.Create(Guid.NewGuid(), Content("First", Guid.NewGuid()), DateTimeOffset.UtcNow);
        entry.Revise(Content("Second", Guid.NewGuid()), 1, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => entry.Approve(1, 2, DateTimeOffset.UtcNow));

        entry.Approve(2, 2, DateTimeOffset.UtcNow);
        Assert.Equal(RevisionApprovalState.Approved, entry.CurrentRevision.ApprovalState);
        Assert.Equal(3, entry.Version);
    }

    private static ExperienceRevisionContent Content(string title, Guid conceptId) => new(
        title, ExperienceSetting.Employment, "Context", "Role", "Actions", "Alternatives", "Outcome",
        ImpactState.Qualitative, [conceptId]);
}
