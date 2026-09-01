using System.Net.Http.Json;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CurrencyWatchlist.Infrastructure.RateProviders;

/// <summary>Targets api.frankfurter.dev/v2, verified directly against the live API (not the
/// v1 URL shown literally in the brief). Endpoint shapes confirmed against the real API's
/// OpenAPI spec during implementation: GET /rates?base=&amp;quotes=[&amp;from=&amp;to=] for
/// both latest and historical rates (docs/architecture.md's diagrams), GET /currencies for
/// the supported-currency list.</summary>
public class FrankfurterRateProvider : IRateProvider
{
    private const string CurrenciesCacheKey = "frankfurter:supported-currencies";

    /// <summary>How long the supported-currency list is cached before the next request
    /// re-fetches it (specs/004-strong-typing-cleanup - User Story 3).</summary>
    private static readonly TimeSpan CurrenciesCacheDuration = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FrankfurterRateProvider> _logger;

    public FrankfurterRateProvider(HttpClient http, IMemoryCache cache, ILogger<FrankfurterRateProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RateResult> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        var results = await GetLatestRatesAsync(baseCurrency, new[] { quoteCurrency }, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<RateResult>> GetLatestRatesAsync(string baseCurrency, IReadOnlyList<string> quoteCurrencies, CancellationToken ct)
    {
        var url = $"rates?base={Uri.EscapeDataString(baseCurrency)}&quotes={Uri.EscapeDataString(string.Join(",", quoteCurrencies))}";

        var (rates, failure) = await FetchRatesAsync(url, ct);
        if (failure is not null)
        {
            // A single bad currency under a base fails that base's entire batch on v2
            // (docs/architecture.md's Refresh Flow decisions) - every requested quote in
            // this batch gets the same failure reason.
            return quoteCurrencies.Select(q => RateResult.Error(failure.Value, q)).ToList();
        }

        return quoteCurrencies
            .Select(q =>
            {
                var match = rates!.FirstOrDefault(r => string.Equals(r.Quote, q, StringComparison.OrdinalIgnoreCase));
                return match is null
                    ? RateResult.Error(RateFailureReason.UnsupportedPair, q)
                    // Frankfurter's "date" field never carries a time-of-day, so this normalizes
                    // to midnight UTC on that date rather than fabricating precision the source
                    // never provided (specs/005-ratesnapshot-cache-cleanup/research.md Decision 3).
                    : RateResult.Ok(match.Rate, match.Date.ToDateTime(TimeOnly.MinValue), q);
            })
            .ToList();
    }

    public async Task<RateHistoryResult> GetHistoryAsync(string baseCurrency, string quoteCurrency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var url = $"rates?base={Uri.EscapeDataString(baseCurrency)}&quotes={Uri.EscapeDataString(quoteCurrency)}" +
                  $"&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        var (rates, failure) = await FetchRatesAsync(url, ct);
        if (failure is not null)
        {
            return RateHistoryResult.Error(failure.Value);
        }

        var points = rates!.Select(r => new RateHistoryPoint(r.Date, r.Rate)).ToList();
        return RateHistoryResult.Ok(points);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSupportedCurrenciesAsync(CancellationToken ct)
    {
        var cached = await _cache.GetOrCreateAsync(CurrenciesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CurrenciesCacheDuration;

            HttpResponseMessage response;
            try
            {
                response = await SendWithRetryAsync("currencies", ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // No try/catch swallowing the failure here beyond translating it to our own
                // exception type - if the list can't be fetched, this propagates to the
                // middleware as a 502. A currency that can't be verified is treated as not
                // verified, not as accepted (docs/architecture.md:1053-1059).
                throw new RateProviderUnavailableException("Could not reach the currency provider.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new RateProviderUnavailableException($"Currency provider returned {(int)response.StatusCode}.");
            }

            var list = await response.Content.ReadFromJsonAsync<List<FrankfurterCurrencyDto>>(cancellationToken: ct)
                ?? new List<FrankfurterCurrencyDto>();
            return (IReadOnlyDictionary<string, string>)list.ToDictionary(c => c.IsoCode, c => c.Name);
        });

        return cached!;
    }

    /// <summary>Shared fetch+parse for both GetLatestRatesAsync and GetHistoryAsync - both hit
    /// the same /rates endpoint shape, differing only in query string. Returns either the
    /// parsed rows or a failure reason, never both.</summary>
    private async Task<(List<FrankfurterRateDto>? Rates, RateFailureReason? Failure)> FetchRatesAsync(string relativeUrl, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await SendWithRetryAsync(relativeUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Frankfurter request failed for {Url}", relativeUrl);
            return (null, RateFailureReason.Unavailable);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // {"status":422,"message":"invalid currency: ZZZ"} - a pair the provider itself
            // doesn't recognize (write-time validation should already prevent this reaching
            // here in practice, but refresh treats it as UnsupportedPair regardless).
            return (null, RateFailureReason.UnsupportedPair);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (null, RateFailureReason.Unavailable);
        }

        var rates = await response.Content.ReadFromJsonAsync<List<FrankfurterRateDto>>(cancellationToken: ct);
        return (rates ?? new List<FrankfurterRateDto>(), null);
    }

    /// <summary>~5s HttpClient timeout (configured on the client itself in Program.cs) and a
    /// single retry on transient failure - no Polly, no circuit breaker, at this scale
    /// (docs/architecture.md:1018).</summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(string relativeUrl, CancellationToken ct)
    {
        try
        {
            return await _http.GetAsync(relativeUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Frankfurter request to {Url} failed, retrying once", relativeUrl);
            return await _http.GetAsync(relativeUrl, ct);
        }
    }
}
