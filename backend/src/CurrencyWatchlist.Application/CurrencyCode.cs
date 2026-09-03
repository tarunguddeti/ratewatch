using System.Text.RegularExpressions;

namespace CurrencyWatchlist.Application;

/// <summary>Layer one of the two-layer currency validation: cheap, always run first, format
/// only. Layer two (membership against the live supported-currency list) lives in the
/// services that call IRateProvider - this class has no provider dependency.</summary>
public static class CurrencyCode
{
    /// <summary>The single source of truth for "how many letters make up a currency code" -
    /// referenced by FormatPattern below and by every EF HasMaxLength(...) call on a
    /// BaseCurrency/QuoteCurrency column, which previously restated this number
    /// independently.</summary>
    public const int Length = 3;

    // [GeneratedRegex] requires a compile-time constant argument, which can't reference Length
    // (C# doesn't support building a const string by concatenating a const int into it) - a
    // regular Regex trades that source-generation for true single-sourcing of Length, a
    // deliberate and negligible-impact tradeoff at this call frequency.
    private static readonly Regex FormatPattern = new($"^[A-Z]{{{Length}}}$", RegexOptions.Compiled);

    /// <summary>Uppercases and trims. Without this, "usd"/"AUD" and "USD"/"AUD" would be
    /// treated as different pairs by the WatchlistItem uniqueness constraint.</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsWellFormed(string normalizedCode) => FormatPattern.IsMatch(normalizedCode);
}
