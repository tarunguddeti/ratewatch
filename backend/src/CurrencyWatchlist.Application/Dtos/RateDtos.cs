namespace CurrencyWatchlist.Application.Dtos;

public record CurrencyDto(string Code, string Name);

public record RateSnapshotDto(string BaseCurrency, string QuoteCurrency, decimal Rate, DateTime SourceTimestamp, DateTime FetchedAt);

/// <summary>Wire type for GET /api/rates/history, distinct from RateSnapshotDto so that
/// RateSnapshotDto's SourceTimestamp precision (widened to DateTime for the latest-rate cache)
/// never leaks into the history response - history is proxied live and stays DateOnly.</summary>
public record RateHistoryPointDto(string BaseCurrency, string QuoteCurrency, decimal Rate, DateOnly SourceTimestamp, DateTime FetchedAt);

public record FailedPairDto(string Pair, string Reason);

public record RefreshSummaryDto(IReadOnlyList<RateSnapshotDto> Refreshed, IReadOnlyList<FailedPairDto> Failed);
