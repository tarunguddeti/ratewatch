using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;
using CurrencyWatchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Repositories;

public class AlertRuleRepository(AppDbContext db) : IAlertRuleRepository
{
    public async Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<AlertRule?> GetByIdWithItemAsync(Guid id, CancellationToken ct) =>
        await db.AlertRules.Include(r => r.WatchlistItem).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<AlertRule>> GetByWatchlistIdAsync(Guid watchlistId, CancellationToken ct) =>
        await db.AlertRules
            .Include(r => r.WatchlistItem)
            .Where(r => r.WatchlistItem!.WatchlistId == watchlistId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(AlertRule rule, CancellationToken ct)
    {
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddEventAsync(AlertEvent alertEvent, CancellationToken ct)
    {
        db.AlertEvents.Add(alertEvent);
        await db.SaveChangesAsync(ct);
    }
}
