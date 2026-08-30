using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Repositories;

public interface IWatchlistRepository
{
    /// <summary>Includes Items and each item's AlertRules, so the caller can project
    /// item/alert-rule counts (FR-004/SC-006) without a second round trip.</summary>
    Task<IReadOnlyList<Watchlist>> GetAllAsync(CancellationToken ct);

    /// <summary>Includes Items and each item's AlertRules - the join against latest rates
    /// happens in the service layer via IRateSnapshotRepository, since RateSnapshot has no
    /// FK to WatchlistItem by design.</summary>
    Task<Watchlist?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(Watchlist watchlist, CancellationToken ct);

    Task DeleteAsync(Watchlist watchlist, CancellationToken ct);
}
