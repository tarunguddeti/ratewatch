using System.Text.Json.Serialization;

namespace CurrencyWatchlist.Domain.Entities;

/// <summary>The alert rule's exhaustive, closed set of valid conditions - was previously a
/// plain string that only ever held one of these two values by convention. Attribute-scoped
/// (not a global Program.cs registration) so every JsonSerializer call, including
/// IntegrationTests' own HttpClient, handles it identically with no extra configuration
/// (specs/004-strong-typing-cleanup/research.md decision 1). Uses the case-sensitive
/// AlertConditionJsonConverter, not the built-in JsonStringEnumConverter, which matches names
/// case-insensitively on read - see that converter's doc comment.</summary>
[JsonConverter(typeof(AlertConditionJsonConverter))]
public enum AlertCondition
{
    Above,
    Below,
}
