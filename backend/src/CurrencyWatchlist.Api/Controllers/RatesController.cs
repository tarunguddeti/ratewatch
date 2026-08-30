using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyWatchlist.Api.Controllers;

[ApiController]
[Route("api/rates")]
public class RatesController(RateService rateService) : ControllerBase
{
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshSummaryDto>> Refresh(CancellationToken ct)
    {
        // Always 200, even on partial failure - the failed[] list carries per-pair reasons
        // instead (docs/architecture.md's API Contract table).
        var summary = await rateService.RefreshAllAsync(ct);
        return Ok(summary);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<RateSnapshotDto>> GetLatest([FromQuery] string @base, [FromQuery] string quote, CancellationToken ct)
    {
        var dto = await rateService.GetLatestAsync(@base.ToUpperInvariant(), quote.ToUpperInvariant(), ct);
        return Ok(dto);
    }

    /// <summary>Range validation happens here, at the controller boundary, before the service
    /// or provider is ever called (contracts/api-contracts.md, docs/architecture.md:612) -
    /// to can't be in the future, from must be <= to, and the span is capped at a year so the
    /// live-proxied call and the resulting chart don't try to render an unbounded number of
    /// points (FR-016). Defaults to the last 30 days when no range is specified (FR-015).</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<RateSnapshotDto>>> GetHistory(
        [FromQuery] string @base, [FromQuery] string quote,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveTo = to ?? today;
        var effectiveFrom = from ?? effectiveTo.AddDays(-30);

        if (effectiveTo > today)
        {
            throw new ValidationException("The end date cannot be in the future.");
        }

        if (effectiveFrom > effectiveTo)
        {
            throw new ValidationException("The start date must be on or before the end date.");
        }

        if (effectiveTo.DayNumber - effectiveFrom.DayNumber > 365)
        {
            throw new ValidationException("The date range cannot exceed one year.");
        }

        var history = await rateService.GetHistoryAsync(@base.ToUpperInvariant(), quote.ToUpperInvariant(), effectiveFrom, effectiveTo, ct);
        return Ok(history);
    }
}
