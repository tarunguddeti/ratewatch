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
    /// via Condition's type and [Range] on CreateAlertRuleRequest (Api/Requests/AlertRequests.cs);
    /// 404 if the watchlist item doesn't exist. No restriction on multiple rules per item,
    /// including opposing conditions (FR-018) - a second rule on the same pair is just another
    /// row.</summary>
    public async Task<AlertRuleDto> CreateAsync(Guid watchlistItemId, AlertCondition condition, decimal threshold, CancellationToken ct)
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
    /// happened), upserts RateSnapshot as a side effect, compares against the rule's condition
    /// (Above/Below strict, AboveOrEqual/BelowOrEqual inclusive - docs/architecture.md's
    /// Decisions & Tradeoffs → Data Model & Business Rules), and records a trigger event only
    /// when the condition is actually satisfied.</summary>
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

        // Above/Below are strict - a rate exactly at the threshold has not gone above or below
        // it; AboveOrEqual/BelowOrEqual are their explicit inclusive counterparts, where that
        // same rate does count (constitution Article IV, specs/007-inclusive-alert-conditions).
        // Exhaustive over AlertCondition's four members, not a string-equality check
        // (specs/004-strong-typing-cleanup).
        var triggered = rule.Condition switch
        {
            AlertCondition.Above => rateResult.Rate > rule.Threshold,
            AlertCondition.Below => rateResult.Rate < rule.Threshold,
            AlertCondition.AboveOrEqual => rateResult.Rate >= rule.Threshold,
            AlertCondition.BelowOrEqual => rateResult.Rate <= rule.Threshold,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Condition, "Unrecognized alert condition."),
        };

        // Explicit phrase map, not rule.Condition.ToString().ToLowerInvariant() - that would
        // render "aboveorequal" verbatim for the two inclusive conditions.
        var conditionText = rule.Condition switch
        {
            AlertCondition.Above => "above",
            AlertCondition.Below => "below",
            AlertCondition.AboveOrEqual => "at or above",
            AlertCondition.BelowOrEqual => "at or below",
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Condition, "Unrecognized alert condition."),
        };
        var message = triggered
            ? $"{item.BaseCurrency}/{item.QuoteCurrency} is {conditionText} {rule.Threshold} (currently {rateResult.Rate})."
            : $"{item.BaseCurrency}/{item.QuoteCurrency} has not gone {conditionText} {rule.Threshold} (currently {rateResult.Rate}).";

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
