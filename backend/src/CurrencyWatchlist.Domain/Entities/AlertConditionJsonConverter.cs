using System.Text.Json;
using System.Text.Json.Serialization;

namespace CurrencyWatchlist.Domain.Entities;

/// <summary>The built-in JsonStringEnumConverter matches enum member names case-insensitively
/// on read - confirmed live during quickstart verification that this would silently accept
/// "above"/"below", a real regression from today's exact-match check
/// (spec.md's explicit edge case: casing must be rejected exactly as it is today). Enum.TryParse
/// defaults to case-sensitive, so it's used directly instead of the built-in converter.</summary>
public sealed class AlertConditionJsonConverter : JsonConverter<AlertCondition>
{
    public override AlertCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is not null && Enum.TryParse<AlertCondition>(value, out var condition))
        {
            return condition;
        }

        throw new JsonException($"The JSON value '{value}' is not a valid AlertCondition.");
    }

    public override void Write(Utf8JsonWriter writer, AlertCondition value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
