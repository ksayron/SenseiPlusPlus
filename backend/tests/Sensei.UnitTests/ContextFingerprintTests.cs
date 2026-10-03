using Sensei.Modules.WorkReflection.Domain;

namespace Sensei.UnitTests;

public sealed class ContextFingerprintTests
{
    private static ContextSource Source => new(ContextSourceKind.ManualWeb,
        [new("snippet", "v1", "// keep\nvar x = 1;  \n")], "Explain value copying", "No external calls");

    [Fact]
    public void Canonical_envelope_matches_independently_encoded_big_endian_utf8_fixture()
    {
        // Independently encoded using Node Buffer.writeInt32BE and node:crypto SHA-256.
        Assert.Equal("f24294d9a7d95e3b4b9c0883c61ee1829386a831be1041671c4dce236aeb57da", ContextFingerprint.Compute(Source));
        var crlf = Source with { Excerpts = [Source.Excerpts[0] with { Text = Source.Excerpts[0].Text.Replace("\n", "\r\n") }] };
        Assert.Equal(ContextFingerprint.Compute(Source), ContextFingerprint.Compute(crlf));
        var cr = Source with { Excerpts = [Source.Excerpts[0] with { Text = Source.Excerpts[0].Text.Replace('\n', '\r') }] };
        Assert.Equal(ContextFingerprint.Compute(Source), ContextFingerprint.Compute(cr));
    }

    [Fact]
    public void Every_source_field_order_comment_and_whitespace_remains_significant()
    {
        var source = Source;
        ContextSource[] changed = [
            source with { Intent = source.Intent + " " }, source with { Constraints = "Calls permitted" },
            source with { Excerpts = [source.Excerpts[0] with { Label = "another" }] },
            source with { Excerpts = [source.Excerpts[0] with { VersionLabel = "v2" }] },
            source with { Excerpts = [source.Excerpts[0] with { Text = "var x = 1;  \n" }] },
            source with { Excerpts = [source.Excerpts[0] with { Text = source.Excerpts[0].Text.Trim() }] }
        ];
        Assert.All(changed, value => Assert.NotEqual(ContextFingerprint.Compute(source), ContextFingerprint.Compute(value)));
        var ordered = source with { Excerpts = [source.Excerpts[0], new("second", "v2", "😀 Ж")] };
        Assert.NotEqual(ContextFingerprint.Compute(ordered), ContextFingerprint.Compute(ordered with { Excerpts = ordered.Excerpts.Reverse().ToArray() }));
        // Length prefixes distinguish boundaries that naïve concatenation would collapse.
        Assert.NotEqual(ContextFingerprint.Compute(source with { Intent = "ab", Constraints = "c" }),
            ContextFingerprint.Compute(source with { Intent = "a", Constraints = "bc" }));
    }

    [Fact]
    public void Snapshot_copies_inputs_and_interpretation_never_modifies_the_source()
    {
        var source = Source;
        var snapshot = ContextSnapshot.Create(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "digest", source,
            ContextDataScope.Restricted, "{}", DateTimeOffset.UtcNow);
        var original = snapshot.SourceFingerprint;
        source.Excerpts[0] = new("mutated", "", "different source");
        var revision = InterpretationRevision.Create(snapshot.OwnerId, snapshot.EpisodeId, snapshot.Id, 1, null,
            Guid.NewGuid(), "digest", new("Alternative intent", "Different constraints", "Unknown outcome", ""),
            InterpretationOrigin.User, null, DateTimeOffset.UtcNow);
        Assert.Equal(original, snapshot.SourceFingerprint);
        Assert.Equal("snippet", snapshot.Source().Excerpts[0].Label);
        Assert.Equal("Alternative intent", revision.Text().Intent);
        var providerRevision = InterpretationRevision.Create(snapshot.OwnerId, snapshot.EpisodeId, snapshot.Id, 2,
            revision.Id, Guid.NewGuid(), "digest", new("Provider interpretation", "Different interpretation constraint", "Unknown", ""),
            InterpretationOrigin.Provider, "{\"origin\":\"Provider\",\"routeRevision\":\"synthetic-test\"}", DateTimeOffset.UtcNow);
        Assert.Equal(InterpretationOrigin.Provider, providerRevision.Origin);
        Assert.Equal(original, snapshot.SourceFingerprint);
        Assert.Equal("No external calls", snapshot.Source().Constraints);
        Assert.Throws<ArgumentException>(() => InterpretationRevision.Create(snapshot.OwnerId, snapshot.EpisodeId, snapshot.Id, 2,
            revision.Id, Guid.NewGuid(), "digest", revision.Text(), InterpretationOrigin.Provider, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Malformed_unicode_and_byte_oversize_context_are_rejected_without_truncation()
    {
        Assert.Throws<ArgumentException>(() => ContextFingerprint.Compute(Source with { Intent = "\ud800" }));
        Assert.Throws<ArgumentException>(() => ContextFingerprint.Compute(Source with { Excerpts = [] }));
        Assert.Throws<ArgumentException>(() => ContextFingerprint.Compute(Source with { Excerpts = [new("large", "", new string('Ж', 16000))] }));
        Assert.Throws<ArgumentException>(() => ContextFingerprint.Compute(Source with { Kind = (ContextSourceKind)99 }));
    }
}
