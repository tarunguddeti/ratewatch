using System.ComponentModel.DataAnnotations;

namespace CurrencyWatchlist.Api.Requests;

/// <summary>Request-only wire type - AlertService.CreateAsync already takes primitives, not
/// this DTO, so this is the sole consumer of the shape attributes below
/// (specs/003-dataannotations-validation/research.md decision 11).</summary>
public record CreateAlertRuleRequest(
    Guid WatchlistItemId,
    [AllowedValues("Above", "Below")] string Condition,
    [Range(typeof(decimal), "0", "79228162514264337593543950335", MinimumIsExclusive = true)] decimal Threshold);
