using System.ComponentModel.DataAnnotations;
using CurrencyWatchlist.Application.Validation;

namespace CurrencyWatchlist.Api.Requests;

/// <summary>Request-only wire types - Application never references these; both
/// WatchlistService.CreateAsync and WatchlistItemService.AddItemAsync already take primitives,
/// not the DTO itself, so this is the sole consumer of the shape attributes below
/// (specs/003-dataannotations-validation/research.md decision 11).</summary>
public record CreateWatchlistRequest([Required] string Name);

public record AddWatchlistItemRequest(
    [WellFormedCurrencyCode] string BaseCurrency,
    [WellFormedCurrencyCode] string QuoteCurrency);
