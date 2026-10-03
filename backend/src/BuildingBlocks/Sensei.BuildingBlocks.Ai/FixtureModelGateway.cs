using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.BuildingBlocks.Ai;

/// <summary>Exact-input synthetic fixtures only. This is never an automatic provider fallback.</summary>
public sealed class FixtureModelGateway(IReadOnlyDictionary<string, ModelResult> fixtures) : IModelGateway
{
    public Task<ModelResult> CompleteAsync(ModelRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var audit = new ModelAudit("fixture-v1", null, null, null, new(null, null, null), TimeSpan.Zero, ModelOrigin.Fixture);
        if (request.DataScope != ModelDataScope.PublicSynthetic)
            return Task.FromResult(new ModelResult(null, ModelFailure.Configuration, audit));
        if (!fixtures.TryGetValue(request.Digest, out var fixture))
            return Task.FromResult(new ModelResult(null, ModelFailure.FixtureMissing, audit));
        var result = fixture with { Audit = fixture.Audit with
            { Origin = ModelOrigin.Fixture, RequestDigest = request.Digest, OutputContractVersion = request.Contract.Version } };
        if (result.Failure is null && (result.Output is null || !OutputValidation.Accepts(request.Contract, result.Output.Value)))
            result = result with { Output = null, Failure = ModelFailure.InvalidOutput };
        return Task.FromResult(result);
    }
}
