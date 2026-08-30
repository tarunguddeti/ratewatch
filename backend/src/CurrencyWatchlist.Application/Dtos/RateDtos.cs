namespace CurrencyWatchlist.Application.Dtos;

public record RateSnapshotDto(string BaseCurrency, string QuoteCurrency, decimal Rate, DateOnly SourceTimestamp, DateTime FetchedAt);

public record FailedPairDto(string Pair, string Reason);

public record RefreshSummaryDto(IReadOnlyList<RateSnapshotDto> Refreshed, IReadOnlyList<FailedPairDto> Failed);
