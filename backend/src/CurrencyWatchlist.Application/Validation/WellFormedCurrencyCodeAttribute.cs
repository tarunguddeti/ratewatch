using System.ComponentModel.DataAnnotations;

namespace CurrencyWatchlist.Application.Validation;

/// <summary>Wraps CurrencyCode.Normalize/IsWellFormed as a DataAnnotations attribute, so the
/// two never drift apart - this is the same tolerant-of-case-and-whitespace check
/// WatchlistItemService.AddItemAsync used to run inline, not a fresh regex on the raw field.</summary>
public sealed class WellFormedCurrencyCodeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string code)
        {
            return false;
        }

        return CurrencyCode.IsWellFormed(CurrencyCode.Normalize(code));
    }
}
