using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Repositories;

public interface IRateSnapshotRepository
{
    Task<RateSnapshot?> GetLatestAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);

    /// <summary>Batch fetch for the watchlist-detail join - one query for every item's latest
    /// rate at once, avoiding an N+1.</summary>
    Task<IReadOnlyDictionary<(string Base, string Quote), RateSnapshot>> GetLatestForPairsAsync(
        IEnumerable<(string Base, string Quote)> pairs, CancellationToken ct);

    /// <summary>Single atomic INSERT ... ON CONFLICT DO UPDATE, never a check-then-insert
    /// sequence - closes the double-click/two-tab race on the (BaseCurrency, QuoteCurrency)
    /// unique index. Idempotent regardless of when it last ran - there is exactly one row per
    /// pair, ever.</summary>
    Task UpsertAsync(string baseCurrency, string quoteCurrency, decimal rate, DateTime sourceTimestamp, DateTime fetchedAt, CancellationToken ct);
}
