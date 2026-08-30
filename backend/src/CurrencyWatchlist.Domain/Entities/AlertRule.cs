namespace CurrencyWatchlist.Domain.Entities;

public class AlertRule
{
    public Guid Id { get; set; }
    public Guid WatchlistItemId { get; set; }

    /// <summary>"Above" or "Below". No enum in Domain (Article II keeps Domain dependency-free
    /// of any serialization/validation concern) - Application validates the allowed values.</summary>
    public string Condition { get; set; } = string.Empty;
    public decimal Threshold { get; set; }

    /// <summary>Stored per the given schema but inert: no endpoint in scope sets or reads it,
    /// and evaluate ignores it (docs/architecture.md:1133 - a documented, deliberate gap).</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public WatchlistItem? WatchlistItem { get; set; }
    public ICollection<AlertEvent> Events { get; set; } = new List<AlertEvent>();
}
