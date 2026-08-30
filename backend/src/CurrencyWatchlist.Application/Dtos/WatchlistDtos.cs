namespace CurrencyWatchlist.Application.Dtos;

public record WatchlistDto(Guid Id, string Name, DateTime CreatedAt, int ItemCount, int AlertRuleCount);

public record WatchlistDetailDto(Guid Id, string Name, DateTime CreatedAt, IReadOnlyList<WatchlistItemDetailDto> Items);

public record WatchlistItemDto(Guid Id, Guid WatchlistId, string BaseCurrency, string QuoteCurrency);

public record WatchlistItemDetailDto(Guid Id, string BaseCurrency, string QuoteCurrency, RateSnapshotDto? LatestRate);

public record CreateWatchlistRequest(string Name);

public record AddWatchlistItemRequest(string BaseCurrency, string QuoteCurrency);
