namespace CurrencyWatchlist.Domain.Entities;

public class Watchlist
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}
