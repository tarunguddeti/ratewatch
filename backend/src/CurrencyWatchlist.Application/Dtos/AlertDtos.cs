using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Dtos;

public record AlertRuleDto(Guid Id, Guid WatchlistItemId, AlertCondition Condition, decimal Threshold, DateTime CreatedAt);

public record EvaluateResultDto(bool Triggered, decimal CurrentRate, decimal Threshold, AlertCondition Condition, string Message, DateTime EvaluatedAt);
