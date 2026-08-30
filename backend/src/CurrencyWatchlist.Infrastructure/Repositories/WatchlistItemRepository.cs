using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;
using CurrencyWatchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Repositories;

public class WatchlistItemRepository(AppDbContext db) : IWatchlistItemRepository
{
    public async Task<WatchlistItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.WatchlistItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<bool> ExistsAsync(Guid watchlistId, string baseCurrency, string quoteCurrency, CancellationToken ct) =>
        await db.WatchlistItems.AnyAsync(
            i => i.WatchlistId == watchlistId && i.BaseCurrency == baseCurrency && i.QuoteCurrency == quoteCurrency, ct);

    public async Task AddAsync(WatchlistItem item, CancellationToken ct)
    {
        db.WatchlistItems.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(WatchlistItem item, CancellationToken ct)
    {
        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, List<string>>> GetDistinctPairsGroupedByBaseAsync(CancellationToken ct)
    {
        var pairs = await db.WatchlistItems
            .Select(i => new { i.BaseCurrency, i.QuoteCurrency })
            .Distinct()
            .ToListAsync(ct);

        return pairs
            .GroupBy(p => p.BaseCurrency)
            .ToDictionary(g => g.Key, g => g.Select(p => p.QuoteCurrency).ToList());
    }
}
