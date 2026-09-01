using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Services;

public class WatchlistItemService(
    IWatchlistRepository watchlistRepo,
    IWatchlistItemRepository itemRepo,
    IRateProvider rateProvider)
{
    /// <summary>The most heavily branched write in the system - four independent failure
    /// paths, each a distinct status code via the exception hierarchy
    /// (docs/architecture.md:456-501). Currency validation is two-layer and fail-closed: if
    /// the supported-currency list itself can't be verified, GetSupportedCurrenciesAsync
    /// throws RateProviderUnavailableException and this method never catches it - an
    /// unverifiable currency is never treated as valid (FR-008).</summary>
    public async Task<WatchlistItemDto> AddItemAsync(Guid watchlistId, string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        _ = await watchlistRepo.GetByIdAsync(watchlistId, ct)
            ?? throw new NotFoundException("Watchlist not found.");

        // Well-formed-shape checking (FR-002) happens before this method ever runs, via
        // [WellFormedCurrencyCode] on AddWatchlistItemRequest (Api/Requests/WatchlistRequests.cs).
        var normalizedBase = CurrencyCode.Normalize(baseCurrency);
        var normalizedQuote = CurrencyCode.Normalize(quoteCurrency);

        // FR-009 - a currency cannot be tracked against itself. Load-bearing on v2: the
        // provider itself returns 200/rate 1.0 for a same-currency pair rather than
        // rejecting it, so this guard is the only thing stopping it
        // (docs/architecture.md:1046).
        if (normalizedBase == normalizedQuote)
        {
            throw new ValidationException("Base and quote currency cannot be the same.");
        }

        // Layer two: membership against the live supported-currency list. Propagates
        // RateProviderUnavailableException unhandled if the list itself can't be fetched -
        // that is the fail-closed behavior, not an omission.
        var supported = await rateProvider.GetSupportedCurrenciesAsync(ct);
        if (!supported.ContainsKey(normalizedBase) || !supported.ContainsKey(normalizedQuote))
        {
            throw new ValidationException("One or both currencies are not supported.");
        }

        // FR-007 - the same pair can't be tracked twice in the same watchlist.
        if (await itemRepo.ExistsAsync(watchlistId, normalizedBase, normalizedQuote, ct))
        {
            throw new DuplicatePairException("This pair is already on this watchlist.");
        }

        var item = new WatchlistItem
        {
            Id = Guid.NewGuid(),
            WatchlistId = watchlistId,
            BaseCurrency = normalizedBase,
            QuoteCurrency = normalizedQuote,
        };

        await itemRepo.AddAsync(item, ct);
        return new WatchlistItemDto(item.Id, item.WatchlistId, item.BaseCurrency, item.QuoteCurrency);
    }

    /// <summary>FR-010 - cascades to the pair's alert rules and their recorded events at the
    /// database level. The "this pair has N alert rules" warning is a client-side UX step
    /// before this call is made, using data the detail page already has loaded.</summary>
    public async Task DeleteItemAsync(Guid watchlistId, Guid itemId, CancellationToken ct)
    {
        var item = await itemRepo.GetByIdAsync(itemId, ct);
        if (item is null || item.WatchlistId != watchlistId)
        {
            throw new NotFoundException("Watchlist item not found.");
        }

        await itemRepo.DeleteAsync(item, ct);
    }
}
