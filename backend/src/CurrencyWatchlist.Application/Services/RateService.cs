using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;

namespace CurrencyWatchlist.Application.Services;

public class RateService(
    IWatchlistItemRepository itemRepo,
    IRateSnapshotRepository rateSnapshotRepo,
    IRateProvider rateProvider)
{
    /// <summary>FR-011/012/013 - global across all watchlists, batched by base currency
    /// (one Frankfurter call per distinct base, not per pair). Fetch concurrently, write
    /// sequentially: the per-base calls are independent I/O, but DbContext isn't thread-safe,
    /// so the write phase happens one upsert at a time, each independently fault-isolated -
    /// one failed write never discards other already-fetched, already-successful results
    /// (docs/architecture.md:551-571).</summary>
    public async Task<RefreshSummaryDto> RefreshAllAsync(CancellationToken ct)
    {
        var pairsByBase = await itemRepo.GetDistinctPairsGroupedByBaseAsync(ct);

        var fetchTasks = pairsByBase.Select(async kvp =>
        {
            var (baseCurrency, quotes) = (kvp.Key, kvp.Value);
            var results = await rateProvider.GetLatestRatesAsync(baseCurrency, quotes, ct);
            return results.Select(r => (BaseCurrency: baseCurrency, Result: r));
        });

        var fetchedByBase = await Task.WhenAll(fetchTasks);
        var allResults = fetchedByBase.SelectMany(x => x).ToList();

        var refreshed = new List<RateSnapshotDto>();
        var failed = new List<FailedPairDto>();
        var fetchedAt = DateTime.UtcNow;

        foreach (var (baseCurrency, result) in allResults)
        {
            var pairLabel = $"{baseCurrency}/{result.Quote}";

            if (!result.IsSuccess)
            {
                failed.Add(new FailedPairDto(pairLabel, DescribeFailure(result.FailureReason!.Value)));
                continue;
            }

            try
            {
                await rateSnapshotRepo.UpsertAsync(baseCurrency, result.Quote!, result.Rate, result.SourceTimestamp, fetchedAt, ct);
                refreshed.Add(new RateSnapshotDto(baseCurrency, result.Quote!, result.Rate, result.SourceTimestamp, fetchedAt));
            }
            catch (Exception)
            {
                // A write failure lands in failed[] exactly like a fetch failure - same
                // response shape, same frontend treatment - rather than that one write
                // failure silently discarding every other already-fetched result still
                // waiting to be saved (docs/architecture.md:1075).
                failed.Add(new FailedPairDto(pairLabel, "Could not save this rate."));
            }
        }

        return new RefreshSummaryDto(refreshed, failed);
    }

    /// <summary>FR-014 - a pure cache read, never calls the provider.</summary>
    public async Task<RateSnapshotDto> GetLatestAsync(string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        var snapshot = await rateSnapshotRepo.GetLatestAsync(baseCurrency, quoteCurrency, ct)
            ?? throw new NotFoundException("No rate has been fetched for this pair yet.");

        return new RateSnapshotDto(snapshot.BaseCurrency, snapshot.QuoteCurrency, snapshot.Rate, snapshot.SourceTimestamp, snapshot.FetchedAt);
    }

    /// <summary>FR-015 - proxied live, never touches RateSnapshot. Range validation
    /// (FR-016) happens at the controller boundary before this is ever called
    /// (contracts/api-contracts.md), so this method trusts its inputs.</summary>
    public async Task<IReadOnlyList<RateHistoryPointDto>> GetHistoryAsync(string baseCurrency, string quoteCurrency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = await rateProvider.GetHistoryAsync(baseCurrency, quoteCurrency, from, to, ct);

        if (!result.IsSuccess)
        {
            // UnsupportedPair here means the caller's own base/quote query params are bad -
            // a client input error (400), distinct from provider-unreachable (502). Unlike
            // evaluate/refresh, history takes arbitrary query params not tied to a
            // write-time-validated WatchlistItem, so this branch is genuinely reachable.
            if (result.FailureReason == RateFailureReason.UnsupportedPair)
            {
                throw new ValidationException("One or both currencies are not supported.");
            }

            throw new RateProviderUnavailableException("Could not reach the rate provider.");
        }

        return result.Points!.Select(p => new RateHistoryPointDto(baseCurrency, quoteCurrency, p.Rate, p.Date, DateTime.UtcNow)).ToList();
    }

    private static string DescribeFailure(RateFailureReason reason) => reason switch
    {
        RateFailureReason.Unavailable => "Rate provider unavailable.",
        RateFailureReason.UnsupportedPair => "Currency pair not supported by the rate provider.",
        _ => "Unknown failure.",
    };
}
