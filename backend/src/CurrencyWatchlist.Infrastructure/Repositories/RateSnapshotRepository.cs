using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;
using CurrencyWatchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Repositories;

public class RateSnapshotRepository(AppDbContext db) : IRateSnapshotRepository
{
    public async Task<RateSnapshot?> GetLatestAsync(string baseCurrency, string quoteCurrency, CancellationToken ct) =>
        // Exactly one row can exist per pair (unique index on (BaseCurrency, QuoteCurrency)),
        // so this is a direct lookup, not a "find the newest of possibly many" query
        // (specs/005-ratesnapshot-cache-cleanup/data-model.md).
        await db.RateSnapshots
            .FirstOrDefaultAsync(r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency, ct);

    public async Task<IReadOnlyDictionary<(string Base, string Quote), RateSnapshot>> GetLatestForPairsAsync(
        IEnumerable<(string Base, string Quote)> pairs, CancellationToken ct)
    {
        var pairList = pairs.ToList();
        if (pairList.Count == 0)
        {
            return new Dictionary<(string, string), RateSnapshot>();
        }

        var bases = pairList.Select(p => p.Base).Distinct().ToList();

        // Filtered to the relevant base currencies at the DB level. No in-memory grouping
        // needed anymore - the unique (BaseCurrency, QuoteCurrency) index guarantees at most
        // one row per pair, so every row returned here already is that pair's latest
        // (specs/005-ratesnapshot-cache-cleanup/data-model.md).
        var candidates = await db.RateSnapshots
            .Where(r => bases.Contains(r.BaseCurrency))
            .AsNoTracking()
            .ToListAsync(ct);

        var pairSet = pairList.ToHashSet();
        return candidates
            .Where(r => pairSet.Contains((r.BaseCurrency, r.QuoteCurrency)))
            .ToDictionary(r => (r.BaseCurrency, r.QuoteCurrency));
    }

    public async Task UpsertAsync(string baseCurrency, string quoteCurrency, decimal rate, DateTime sourceTimestamp, DateTime fetchedAt, CancellationToken ct)
    {
        // Single atomic statement, never a check-then-insert sequence (constitution Article
        // IV) - closes the double-click/two-tab race on the unique (BaseCurrency,
        // QuoteCurrency) index. On conflict, Id is left untouched since it's not in the SET
        // clause, so the original row's identity survives. Idempotent regardless of when it
        // last ran - there is exactly one row per pair, ever.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO RateSnapshots (Id, BaseCurrency, QuoteCurrency, Rate, SourceTimestamp, FetchedAt)
               VALUES ({Guid.NewGuid()}, {baseCurrency}, {quoteCurrency}, {rate}, {sourceTimestamp}, {fetchedAt})
               ON CONFLICT(BaseCurrency, QuoteCurrency)
               DO UPDATE SET Rate = excluded.Rate, SourceTimestamp = excluded.SourceTimestamp, FetchedAt = excluded.FetchedAt;",
            ct);
    }
}
