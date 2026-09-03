namespace CurrencyWatchlist.Application.RateProvider;

/// <summary>Why a rate-fetch failed. Frankfurter unreachable/timeout/5xx maps to Unavailable;
/// a pair the provider doesn't recognize maps to UnsupportedPair.</summary>
public enum RateFailureReason
{
    Unavailable,
    UnsupportedPair,
}

/// <summary>IRateProvider returns a Result, not an exception, because RefreshAllAsync loops
/// over every distinct pair and must keep going after one fails. Single-call consumers
/// (AlertService, RateService's history/latest reads) unwrap this back into the shared
/// exception vocabulary at their own layer - the Result type itself stays a loop-friendly
/// detail.</summary>
public record RateResult
{
    public bool IsSuccess { get; private init; }

    /// <summary>Which quote currency this result is for - populated on batched calls
    /// (GetLatestRatesAsync); null for single-pair calls where the caller already knows.</summary>
    public string? Quote { get; private init; }

    public decimal Rate { get; private init; }
    public DateTime SourceTimestamp { get; private init; }
    public RateFailureReason? FailureReason { get; private init; }

    public static RateResult Ok(decimal rate, DateTime sourceTimestamp, string? quote = null) =>
        new() { IsSuccess = true, Rate = rate, SourceTimestamp = sourceTimestamp, Quote = quote };

    public static RateResult Error(RateFailureReason reason, string? quote = null) =>
        new() { IsSuccess = false, FailureReason = reason, Quote = quote };
}

public record RateHistoryPoint(DateOnly Date, decimal Rate);

public record RateHistoryResult
{
    public bool IsSuccess { get; private init; }
    public IReadOnlyList<RateHistoryPoint>? Points { get; private init; }
    public RateFailureReason? FailureReason { get; private init; }

    public static RateHistoryResult Ok(IReadOnlyList<RateHistoryPoint> points) =>
        new() { IsSuccess = true, Points = points };

    public static RateHistoryResult Error(RateFailureReason reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}
