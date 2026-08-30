namespace CurrencyWatchlist.Domain.Entities;

public class WatchlistItem
{
    public Guid Id { get; set; }
    public Guid WatchlistId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;

    public Watchlist? Watchlist { get; set; }
    public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
}
