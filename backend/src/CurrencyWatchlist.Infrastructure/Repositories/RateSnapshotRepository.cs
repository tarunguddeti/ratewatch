using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;
using CurrencyWatchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Repositories;

public class RateSnapshotRepository(AppDbContext db) : IRateSnapshotRepository
{
    public async Task<RateSnapshot?> GetLatestAsync(string baseCurrency, string quoteCurrency, CancellationToken ct) =>
        await db.RateSnapshots
            .Where(r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency)
            .OrderByDescending(r => r.SourceTimestamp)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<(string Base, string Quote), RateSnapshot>> GetLatestForPairsAsync(
        IEnumerable<(string Base, string Quote)> pairs, CancellationToken ct)
    {
        var pairList = pairs.ToList();
        if (pairList.Count == 0)
        {
            return new Dictionary<(string, string), RateSnapshot>();
        }

        var bases = pairList.Select(p => p.Base).Distinct().ToList();

        // Filtered to the relevant base currencies at the DB level, then grouped in memory to
        // pick the latest row per pair - simple and correct at this scale (composite "latest
        // per group" doesn't translate cleanly to SQL via LINQ, and a small in-memory pass
        // over one base currency's rows is not a real cost here).
        var candidates = await db.RateSnapshots
            .Where(r => bases.Contains(r.BaseCurrency))
            .AsNoTracking()
            .ToListAsync(ct);

        var pairSet = pairList.ToHashSet();
        var result = new Dictionary<(string, string), RateSnapshot>();
        foreach (var group in candidates.GroupBy(r => (r.BaseCurrency, r.QuoteCurrency)))
        {
            if (pairSet.Contains(group.Key))
            {
                result[group.Key] = group.OrderByDescending(r => r.SourceTimestamp).First();
            }
        }

        return result;
    }

    public async Task UpsertAsync(string baseCurrency, string quoteCurrency, decimal rate, DateOnly sourceTimestamp, DateTime fetchedAt, CancellationToken ct)
    {
        // Single atomic statement, never a check-then-insert sequence (constitution Article
        // IV) - closes the double-click/two-tab race on the unique
        // (BaseCurrency, QuoteCurrency, SourceTimestamp) index. On conflict, Id is left
        // untouched since it's not in the SET clause, so the original row's identity survives.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO RateSnapshots (Id, BaseCurrency, QuoteCurrency, Rate, SourceTimestamp, FetchedAt)
               VALUES ({Guid.NewGuid()}, {baseCurrency}, {quoteCurrency}, {rate}, {sourceTimestamp}, {fetchedAt})
               ON CONFLICT(BaseCurrency, QuoteCurrency, SourceTimestamp)
               DO UPDATE SET Rate = excluded.Rate, FetchedAt = excluded.FetchedAt;",
            ct);
    }
}
