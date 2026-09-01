namespace CurrencyWatchlist.Domain.Entities;

public class RateSnapshot
{
    public Guid Id { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime SourceTimestamp { get; set; }
    public DateTime FetchedAt { get; set; }
}
