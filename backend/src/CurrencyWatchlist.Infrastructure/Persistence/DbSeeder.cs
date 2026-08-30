using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Persistence;

/// <summary>Seeds one sample watchlist on first run so a reviewer's first load isn't a blank
/// app - deliberately with no RateSnapshot and no AlertRule, so the first thing shown is the
/// "Not fetched yet" empty state rather than fabricated data (docs/architecture.md:1022).</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Watchlists.AnyAsync())
        {
            return;
        }

        var watchlist = new Watchlist
        {
            Id = Guid.NewGuid(),
            Name = "Travel Fund",
            CreatedAt = DateTime.UtcNow,
            Items =
            {
                new WatchlistItem { Id = Guid.NewGuid(), BaseCurrency = "USD", QuoteCurrency = "AUD" },
                new WatchlistItem { Id = Guid.NewGuid(), BaseCurrency = "USD", QuoteCurrency = "EUR" },
            },
        };

        db.Watchlists.Add(watchlist);
        await db.SaveChangesAsync();
    }
}
