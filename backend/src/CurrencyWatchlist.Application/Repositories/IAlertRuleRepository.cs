using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Repositories;

public interface IAlertRuleRepository
{
    Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Includes the parent WatchlistItem, so evaluate can read the pair's
    /// currencies without a second lookup.</summary>
    Task<AlertRule?> GetByIdWithItemAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<AlertRule>> GetByWatchlistIdAsync(Guid watchlistId, CancellationToken ct);

    Task AddAsync(AlertRule rule, CancellationToken ct);

    /// <summary>Written only when an evaluation confirms the condition is satisfied; never
    /// called for a non-triggering evaluation.</summary>
    Task AddEventAsync(AlertEvent alertEvent, CancellationToken ct);
}
