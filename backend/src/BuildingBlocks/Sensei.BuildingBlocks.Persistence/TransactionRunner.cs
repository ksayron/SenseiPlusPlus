using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Application;

namespace Sensei.BuildingBlocks.Persistence;

public sealed class TransactionRunner(SenseiDbContext db) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) return await action();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await db.CommitAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
    }
    public Task LockAsync(string key, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Locks require a transaction.");
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
}
