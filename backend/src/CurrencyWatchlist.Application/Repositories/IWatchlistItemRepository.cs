using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Repositories;

public interface IWatchlistItemRepository
{
    Task<WatchlistItem?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The same pair can't be tracked twice in the same watchlist. Callers pass
    /// already-uppercased currency codes (case-insensitive matching is satisfied by
    /// normalizing before this call, not here).</summary>
    Task<bool> ExistsAsync(Guid watchlistId, string baseCurrency, string quoteCurrency, CancellationToken ct);

    Task AddAsync(WatchlistItem item, CancellationToken ct);

    Task DeleteAsync(WatchlistItem item, CancellationToken ct);

    /// <summary>Every distinct tracked pair, grouped by base currency, for refresh's batched
    /// Frankfurter calls (one call per base, not per pair).</summary>
    Task<IReadOnlyDictionary<string, List<string>>> GetDistinctPairsGroupedByBaseAsync(CancellationToken ct);
}
