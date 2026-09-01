using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
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

    /// <summary>Range validation (to can't be in the future, from must be <= to, span capped at
    /// a year so the live-proxied call and the resulting chart don't try to render an unbounded
    /// number of points - FR-016) happens before this method ever runs, via HistoryQuery's
    /// IValidatableObject.Validate() (Api/Requests/HistoryQuery.cs). Defaults to the last 30
    /// days when no range is specified (FR-015), applied by HistoryQuery.EffectiveFrom/To.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<RateHistoryPointDto>>> GetHistory(
        [FromQuery] string @base, [FromQuery] string quote, [FromQuery] HistoryQuery query, CancellationToken ct)
    {
        var history = await rateService.GetHistoryAsync(@base.ToUpperInvariant(), quote.ToUpperInvariant(), query.EffectiveFrom, query.EffectiveTo, ct);
        return Ok(history);
    }
}
