using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensei.BuildingBlocks.Application;
using Sensei.BuildingBlocks.Application.Models;
using Sensei.Modules.Learning.Application;
using Sensei.Modules.Learning.Domain;

namespace Sensei.Modules.Learning.Infrastructure;

public sealed class AssessmentProcessor(IServiceScopeFactory scopes, TimeProvider clock)
{
    private async Task<T> Scoped<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope(); return await action(scope.ServiceProvider);
    }
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        var lease = await Scoped(p => p.GetRequiredService<IAssessmentQueue>().ClaimAsync(ct));
        if (lease is null) return false;
        var route = await CurrentRoute(lease, ct);
        if (route is null) { await Obsolete(lease, "InputOrRouteChanged", ct); return true; }
        var invocation = await Scoped(p => p.GetRequiredService<IAssessmentQueue>().ReserveInvocationAsync(lease, ct));
        if (invocation is null) return true;
        var input = ContentJson.Read<AssessmentInput>(lease.Job.InputJson);
        var request = AssessmentTemplate.Request(input);
        using var stopHeartbeat = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(route.Route.TimeoutSeconds, 1, 60)), clock);
        using var call = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var heartbeat = Heartbeats(lease, call, stopHeartbeat.Token);
        var leaseCertain = true;
        ModelResult result;
        try { result = await route.Gateway.CompleteAsync(request, call.Token).WaitAsync(call.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { result = Failure(ModelFailure.Timeout); }
        catch { result = Failure(ModelFailure.ProviderUnavailable); }
        finally
        {
            stopHeartbeat.Cancel();
            leaseCertain = await heartbeat;
        }
        ct.ThrowIfCancellationRequested(); // Shutdown leaves the reservation for bounded lease recovery.
        if (!leaseCertain) return true;
        var current = await CurrentRoute(lease, ct) is not null;
        await Scoped(async p => { await p.GetRequiredService<IAssessmentQueue>().CompleteAsync(lease, invocation.Value, result, ct, current); return true; });
        return true;

        ModelResult Failure(ModelFailure failure) => new(null, failure, new(route.Route.Revision, null, null, null,
            new(null, null, null), TimeSpan.Zero, route.Origin, route.Route.Digest, request.Digest, request.Contract.Version));
    }
    private async Task<AssessmentRouteDecision?> CurrentRoute(AssessmentLease lease, CancellationToken ct) => await Scoped(async p =>
    {
        try
        {
            var service = p.GetRequiredService<AssessmentService>();
            var preview = await service.PreviewAsync(lease.Job.OwnerId, lease.Job.AttemptId, ct);
            if (!preview.Available || preview.Digest != lease.Job.DisclosureDigest || lease.Job.TemplateVersion != AssessmentTemplate.Version ||
                lease.Job.ContractVersion != "assessment-v1") return null;
            var route = await p.GetRequiredService<IAssessmentRoutePolicy>().ResolveAsync(lease.Job.OwnerId, lease.Job.AttemptId, ct);
            return route is not null && route.Route.Digest == lease.Job.RouteDigest && route.Origin.ToString() == lease.Job.Origin &&
                route.RouterPrivacyPolicy.AbsoluteUri == preview.Disclosure!.RouterPrivacyPolicy && route.RouterPrivacyReviewedAt == preview.Disclosure.RouterPrivacyReviewedAt ? route : null;
        }
        catch (Exception error) when (error is ResourceNotFoundException or InvalidOperationException or ArgumentException) { return null; }
    });
    private Task Obsolete(AssessmentLease lease, string code, CancellationToken ct) => Scoped(async p =>
    { await p.GetRequiredService<IAssessmentQueue>().ObsoleteAsync(lease, code, ct); return true; });
    private async Task<bool> Heartbeats(AssessmentLease lease, CancellationTokenSource call, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
                if (!await Scoped(p => p.GetRequiredService<IAssessmentQueue>().HeartbeatAsync(lease, stop))) { call.Cancel(); return false; }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { call.Cancel(); return false; } // An uncertain lease must not permit a write.
        return true;
    }
}

internal sealed class AssessmentWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AssessmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await new AssessmentProcessor(scopes, clock).ProcessOneAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error) { logger.LogWarning("Assessment worker will retry after a persistence failure ({ErrorType}).", error.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
