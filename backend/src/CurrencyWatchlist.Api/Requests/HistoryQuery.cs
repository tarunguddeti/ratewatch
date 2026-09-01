using System.ComponentModel.DataAnnotations;

namespace CurrencyWatchlist.Api.Requests;

/// <summary>Bound from RatesController.GetHistory's `from`/`to` query params. Runs through the
/// same automatic ModelState pipeline as the request records in this folder, replacing that
/// endpoint's inline if/throw checks with one declarative Validate() (stretch - User Story 4,
/// specs/003-dataannotations-validation/research.md decision 10).</summary>
public record HistoryQuery(DateOnly? From, DateOnly? To) : IValidatableObject
{
    public DateOnly EffectiveTo => To ?? DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly EffectiveFrom => From ?? EffectiveTo.AddDays(-30);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveTo > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult("The end date cannot be in the future.", [nameof(To)]);
        }

        if (EffectiveFrom > EffectiveTo)
        {
            yield return new ValidationResult("The start date must be on or before the end date.", [nameof(From)]);
        }

        if (EffectiveTo.DayNumber - EffectiveFrom.DayNumber > 365)
        {
            yield return new ValidationResult("The date range cannot exceed one year.", [nameof(From), nameof(To)]);
        }
    }
}
