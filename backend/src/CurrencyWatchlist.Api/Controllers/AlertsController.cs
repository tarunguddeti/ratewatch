using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyWatchlist.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController(AlertService alertService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AlertRuleDto>> Create([FromBody] CreateAlertRuleRequest request, CancellationToken ct)
    {
        var dto = await alertService.CreateAsync(request.WatchlistItemId, request.Condition, request.Threshold, ct);
        // No single-rule GET-by-id endpoint exists to point a Location header at (only
        // GetByWatchlist, which needs a watchlistId this DTO doesn't carry) - 201 with the
        // body is sufficient per the API Contract table.
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertRuleDto>>> GetByWatchlist([FromQuery] Guid watchlistId, CancellationToken ct)
    {
        var dtos = await alertService.GetByWatchlistAsync(watchlistId, ct);
        return Ok(dtos);
    }

    [HttpPost("{id:guid}/evaluate")]
    public async Task<ActionResult<EvaluateResultDto>> Evaluate(Guid id, CancellationToken ct)
    {
        var result = await alertService.EvaluateAsync(id, ct);
        return Ok(result);
    }
}
