using System.ComponentModel.DataAnnotations;
using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Api.Requests;

/// <summary>Request-only wire type - AlertService.CreateAsync already takes primitives, not
/// this DTO, so this is the sole consumer of the shape attributes below. Condition is
/// AlertCondition directly rather than a validated string - an invalid value now fails at JSON
/// deserialization and is reported through the same InvalidModelStateResponseFactory pipeline
/// [AllowedValues] used to drive, so that attribute is no longer needed.</summary>
public record CreateAlertRuleRequest(
    Guid WatchlistItemId,
    AlertCondition Condition,
    [Range(typeof(decimal), "0", "79228162514264337593543950335", MinimumIsExclusive = true)] decimal Threshold);