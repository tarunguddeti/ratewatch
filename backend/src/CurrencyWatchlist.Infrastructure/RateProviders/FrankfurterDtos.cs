using System.Text.Json.Serialization;

namespace CurrencyWatchlist.Infrastructure.RateProviders;

internal record FrankfurterCurrencyDto(
    [property: JsonPropertyName("iso_code")] string IsoCode,
    [property: JsonPropertyName("name")] string Name);

internal record FrankfurterRateDto(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("quote")] string Quote,
    [property: JsonPropertyName("rate")] decimal Rate);

internal record FrankfurterErrorDto(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message);
