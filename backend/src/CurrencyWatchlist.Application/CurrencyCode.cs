using System.Text.RegularExpressions;

namespace CurrencyWatchlist.Application;

/// <summary>Layer one of the two-layer currency validation: cheap, always run first, format
/// only. Layer two (membership against the live supported-currency list) lives in the
/// services that call IRateProvider - this class has no provider dependency
/// (docs/architecture.md:1050).</summary>
public static partial class CurrencyCode
{
    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex FormatPattern();

    /// <summary>Uppercases and trims. Without this, "usd"/"AUD" and "USD"/"AUD" would be
    /// treated as different pairs by the WatchlistItem uniqueness constraint
    /// (docs/architecture.md:1048).</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsWellFormed(string normalizedCode) => FormatPattern().IsMatch(normalizedCode);
}
