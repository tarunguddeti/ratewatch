using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Services;

public class WatchlistService(IWatchlistRepository watchlistRepo, IRateSnapshotRepository rateSnapshotRepo)
{
    /// <summary>FR-001 - blank/invalid names are rejected and nothing is created.</summary>
    public async Task<WatchlistDto> CreateAsync(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Watchlist name is required.");
        }

        var watchlist = new Watchlist
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await watchlistRepo.AddAsync(watchlist, ct);
        return ToDto(watchlist);
    }

    /// <summary>FR-002. Each entry's itemCount/alertRuleCount back the "this also removes N
    /// pairs and M alert rules" confirmation on WatchlistCard (FR-004/SC-006) - the Watchlists
    /// overview is the only screen where whole-watchlist deletion happens, so this is the only
    /// place that data can come from without an N+1 call per card.</summary>
    public async Task<IReadOnlyList<WatchlistDto>> GetAllAsync(CancellationToken ct)
    {
        var watchlists = await watchlistRepo.GetAllAsync(ct);
        return watchlists.Select(ToDto).ToList();
    }

    /// <summary>FR-003 - tracked pairs together with each pair's latest known rate and defined
    /// alert rules. The rate join happens here, not in the repository, since RateSnapshot has
    /// no FK to WatchlistItem by design (data-model.md).</summary>
    public async Task<WatchlistDetailDto> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var watchlist = await watchlistRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Watchlist not found.");

        var pairs = watchlist.Items.Select(i => (i.BaseCurrency, i.QuoteCurrency)).ToList();
        var latestRates = await rateSnapshotRepo.GetLatestForPairsAsync(pairs, ct);

        var items = watchlist.Items.Select(item =>
        {
            latestRates.TryGetValue((item.BaseCurrency, item.QuoteCurrency), out var snapshot);
            var rateDto = snapshot is null
                ? null
                : new RateSnapshotDto(snapshot.BaseCurrency, snapshot.QuoteCurrency, snapshot.Rate, snapshot.SourceTimestamp, snapshot.FetchedAt);
            return new WatchlistItemDetailDto(item.Id, item.BaseCurrency, item.QuoteCurrency, rateDto);
        }).ToList();

        return new WatchlistDetailDto(watchlist.Id, watchlist.Name, watchlist.CreatedAt, items);
    }

    /// <summary>FR-004 - cascades to every tracked pair, their alert rules, and those rules'
    /// recorded trigger events (data-model.md's cross-entity invariants). The "this removes N
    /// pairs and M rules" warning itself is a client-side UX step before this call is ever
    /// made (contracts/api-contracts.md).</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var watchlist = await watchlistRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Watchlist not found.");

        await watchlistRepo.DeleteAsync(watchlist, ct);
    }

    private static WatchlistDto ToDto(Watchlist watchlist) => new(
        watchlist.Id,
        watchlist.Name,
        watchlist.CreatedAt,
        watchlist.Items.Count,
        watchlist.Items.Sum(i => i.AlertRules.Count));
}
