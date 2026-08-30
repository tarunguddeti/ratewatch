namespace CurrencyWatchlist.Application.Dtos;

public record AlertRuleDto(Guid Id, Guid WatchlistItemId, string Condition, decimal Threshold, DateTime CreatedAt);

public record EvaluateResultDto(bool Triggered, decimal CurrentRate, decimal Threshold, string Condition, string Message, DateTime EvaluatedAt);
