using System.Net;
using System.Text;
using System.Text.Json;

namespace CurrencyWatchlist.IntegrationTests;

/// <summary>Stands in for api.frankfurter.dev/v2 in every integration test - the suite must
/// never depend on a live third party's uptime to pass. Wired in as the typed HttpClient's
/// primary handler via CustomWebApplicationFactory, which is the in-process equivalent of
/// pointing RateProvider__BaseUrl at a local mock server.</summary>
public class FakeFrankfurterHandler : HttpMessageHandler
{
    /// <summary>Rate to return for any /rates request, keyed by "BASE/QUOTE". Currency codes
    /// not in this set are treated as unsupported (422), matching the real API's behavior for
    /// GetSupportedCurrenciesAsync's membership check.</summary>
    public Dictionary<string, decimal> Rates { get; } = new()
    {
        ["USD/AUD"] = 1.5m,
        ["USD/EUR"] = 0.9m,
    };

    public bool SimulateUnavailable { get; set; }

    /// <summary>How many times /currencies has been served - lets a test assert "the provider
    /// was never called" for a request that should be rejected before reaching this handler.</summary>
    public int CurrenciesCallCount { get; private set; }

    private static readonly IReadOnlyDictionary<string, string> SupportedCurrencies = new Dictionary<string, string>
    {
        ["USD"] = "US Dollar",
        ["AUD"] = "Australian Dollar",
        ["EUR"] = "Euro",
        ["GBP"] = "British Pound",
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (SimulateUnavailable)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        var path = request.RequestUri!.AbsolutePath;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);

        string? QueryValue(string key) => query.TryGetValue(key, out var v) ? v.ToString() : null;

        if (path.EndsWith("/currencies"))
        {
            CurrenciesCallCount++;
            var body = SupportedCurrencies.Select(c => new { iso_code = c.Key, name = c.Value });
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, body));
        }

        if (path.EndsWith("/rates"))
        {
            var baseCurrency = QueryValue("base") ?? "";
            var quotes = (QueryValue("quotes") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var from = QueryValue("from");
            var to = QueryValue("to");

            var rows = new List<object>();
            foreach (var quote in quotes)
            {
                var key = $"{baseCurrency}/{quote}";
                if (!Rates.TryGetValue(key, out var rate))
                {
                    // Matches the real API: one bad currency fails the entire batch (422).
                    return Task.FromResult(JsonResponse(HttpStatusCode.UnprocessableEntity, new { status = 422, message = $"invalid currency: {quote}" }));
                }

                if (from is not null && to is not null)
                {
                    // A short synthetic time series so history tests have >1 point to assert on.
                    var fromDate = DateOnly.Parse(from);
                    var toDate = DateOnly.Parse(to);
                    for (var d = fromDate; d <= toDate; d = d.AddDays(1))
                    {
                        rows.Add(new { date = d.ToString("yyyy-MM-dd"), @base = baseCurrency, quote, rate });
                    }
                }
                else
                {
                    rows.Add(new { date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), @base = baseCurrency, quote, rate });
                }
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, rows));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };
}
