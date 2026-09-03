namespace CurrencyWatchlist.Application.Exceptions;

/// <summary>Malformed request, business-rule violation (blank name, base==quote,
/// non-positive threshold, currency not in the supported list) - maps to 400.</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>Watchlist / item / alert rule doesn't exist - maps to 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The exact pair is already tracked on this watchlist - maps to 409.</summary>
public class DuplicatePairException(string message) : Exception(message);

/// <summary>Frankfurter unreachable, timed out, or returned a 5xx - maps to 502. Thrown
/// rather than silently degrading, per the fail-closed currency-validation decision.</summary>
public class RateProviderUnavailableException(string message) : Exception(message);

/// <summary>The provider doesn't recognize this pair at evaluation time - maps to 422.</summary>
public class UnsupportedPairException(string message) : Exception(message);
