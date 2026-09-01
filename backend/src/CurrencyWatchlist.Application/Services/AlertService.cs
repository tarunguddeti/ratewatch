using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Domain.Entities;

namespace CurrencyWatchlist.Application.Services;

public class AlertService(
    IAlertRuleRepository alertRuleRepo,
    IWatchlistItemRepository itemRepo,
    IRateSnapshotRepository rateSnapshotRepo,
    IRateProvider rateProvider)
{
    /// <summary>FR-017 - condition + positive threshold checked before this method ever runs,
    /// via [AllowedValues] and [Range] on CreateAlertRuleRequest (Api/Requests/AlertRequests.cs);
    /// 404 if the watchlist item doesn't exist. No restriction on multiple rules per item,
    /// including opposing conditions (FR-018) - a second rule on the same pair is just another
    /// row.</summary>
    public async Task<AlertRuleDto> CreateAsync(Guid watchlistItemId, string condition, decimal threshold, CancellationToken ct)
    {
        _ = await itemRepo.GetByIdAsync(watchlistItemId, ct)
            ?? throw new NotFoundException("Watchlist item not found.");

        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            WatchlistItemId = watchlistItemId,
            Condition = condition,
            Threshold = threshold,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await alertRuleRepo.AddAsync(rule, ct);
        return ToDto(rule);
    }

    /// <summary>FR-023.</summary>
    public async Task<IReadOnlyList<AlertRuleDto>> GetByWatchlistAsync(Guid watchlistId, CancellationToken ct)
    {
        var rules = await alertRuleRepo.GetByWatchlistIdAsync(watchlistId, ct);
        return rules.Select(ToDto).ToList();
    }

    /// <summary>FR-019/020/021/022 - obtains the pair's rate live at the moment it runs
    /// (never reads a stored snapshot, so it never depends on a prior refresh having
    /// happened), upserts RateSnapshot as a side effect, compares with strict decimal
    /// inequality, and records a trigger event only when the condition is actually satisfied
    /// (docs/architecture.md:727-758).</summary>
    public async Task<EvaluateResultDto> EvaluateAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await alertRuleRepo.GetByIdWithItemAsync(ruleId, ct)
            ?? throw new NotFoundException("Alert rule not found.");

        var item = rule.WatchlistItem!;
        var rateResult = await rateProvider.GetLatestRateAsync(item.BaseCurrency, item.QuoteCurrency, ct);

        if (!rateResult.IsSuccess)
        {
            // Single-call site: unwrap the Result and throw, rather than propagating the
            // Result type itself past this layer (docs/architecture.md:1101).
            throw rateResult.FailureReason == RateFailureReason.UnsupportedPair
                ? new UnsupportedPairException($"{item.BaseCurrency}/{item.QuoteCurrency} isn't a supported currency pair.")
                : new RateProviderUnavailableException("Could not reach the rate provider.");
        }

        var now = DateTime.UtcNow;
        await rateSnapshotRepo.UpsertAsync(item.BaseCurrency, item.QuoteCurrency, rateResult.Rate, rateResult.SourceTimestamp, now, ct);

        // Strict comparison - a rate exactly at the threshold has not gone above or below it
        // (constitution Article IV).
        var triggered = rule.Condition == "Above"
            ? rateResult.Rate > rule.Threshold
            : rateResult.Rate < rule.Threshold;

        var message = triggered
            ? $"{item.BaseCurrency}/{item.QuoteCurrency} is {rule.Condition.ToLowerInvariant()} {rule.Threshold} (currently {rateResult.Rate})."
            : $"{item.BaseCurrency}/{item.QuoteCurrency} has not gone {rule.Condition.ToLowerInvariant()} {rule.Threshold} (currently {rateResult.Rate}).";

        if (triggered)
        {
            await alertRuleRepo.AddEventAsync(new AlertEvent
            {
                Id = Guid.NewGuid(),
                AlertRuleId = rule.Id,
                TriggeredAt = now,
                Rate = rateResult.Rate,
                Message = message,
            }, ct);
        }

        return new EvaluateResultDto(triggered, rateResult.Rate, rule.Threshold, rule.Condition, message, now);
    }

    private static AlertRuleDto ToDto(AlertRule rule) => new(rule.Id, rule.WatchlistItemId, rule.Condition, rule.Threshold, rule.CreatedAt);
}
