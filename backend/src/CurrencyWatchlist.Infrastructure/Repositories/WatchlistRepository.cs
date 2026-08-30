using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;
using CurrencyWatchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Repositories;

public class WatchlistRepository(AppDbContext db) : IWatchlistRepository
{
    public async Task<IReadOnlyList<Watchlist>> GetAllAsync(CancellationToken ct) =>
        await db.Watchlists
            .Include(w => w.Items).ThenInclude(i => i.AlertRules)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<Watchlist?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.Watchlists
            .Include(w => w.Items).ThenInclude(i => i.AlertRules)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task AddAsync(Watchlist watchlist, CancellationToken ct)
    {
        db.Watchlists.Add(watchlist);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Watchlist watchlist, CancellationToken ct)
    {
        db.Watchlists.Remove(watchlist);
        await db.SaveChangesAsync(ct);
    }
}
