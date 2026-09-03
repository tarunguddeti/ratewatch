namespace CurrencyWatchlist.Application.RateProvider;

/// <summary>The one seam through which this system knows a third-party FX API exists.
/// Implemented by Infrastructure's FrankfurterRateProvider; the frontend never calls
/// Frankfurter directly, under any condition.</summary>
public interface IRateProvider
{
    /// <summary>Single pair - used by evaluate. Obtains a live rate at call time, never
    /// reads a stored snapshot.</summary>
    Task<RateResult> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);

    /// <summary>One base, many quotes - used by refresh's batched-by-base strategy. One
    /// RateResult per requested quote.</summary>
    Task<IReadOnlyList<RateResult>> GetLatestRatesAsync(string baseCurrency, IReadOnlyList<string> quoteCurrencies, CancellationToken ct);

    /// <summary>Date-range time series, proxied live - used by the history endpoint. Never
    /// reads or writes RateSnapshot.</summary>
    Task<RateHistoryResult> GetHistoryAsync(string baseCurrency, string quoteCurrency, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>The full supported-currency list, IMemoryCache-backed with a 24h TTL in the
    /// implementation. Unlike the rate-fetching methods above, this throws
    /// RateProviderUnavailableException directly on failure rather than returning a Result -
    /// it's a single-outcome call, never looped, so there's nothing for a Result type to buy
    /// here.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSupportedCurrenciesAsync(CancellationToken ct);
}
