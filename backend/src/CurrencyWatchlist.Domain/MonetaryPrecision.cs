namespace CurrencyWatchlist.Domain;

/// <summary>How precisely a rate or threshold value is stored - the single source of truth for
/// a rule that was previously restated independently in three separate EF Core configuration
/// classes (RateSnapshot.Rate, AlertRule.Threshold, AlertEvent.Rate)
/// (specs/004-strong-typing-cleanup/research.md decision 5).</summary>
public static class MonetaryPrecision
{
    public const int Precision = 18;
    public const int Scale = 6;
}
