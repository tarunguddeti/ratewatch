namespace CurrencyWatchlist.Domain.Entities;

public class AlertRule
{
    public Guid Id { get; set; }
    public Guid WatchlistItemId { get; set; }

    public AlertCondition Condition { get; set; }
    public decimal Threshold { get; set; }

    /// <summary>Stored per the given schema but inert: no endpoint in scope sets or reads it,
    /// and evaluate ignores it - a documented, deliberate gap.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public WatchlistItem? WatchlistItem { get; set; }
    public ICollection<AlertEvent> Events { get; set; } = new List<AlertEvent>();
}
