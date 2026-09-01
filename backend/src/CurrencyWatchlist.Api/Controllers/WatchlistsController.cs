using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyWatchlist.Api.Controllers;

[ApiController]
[Route("api/watchlists")]
public class WatchlistsController(WatchlistService watchlistService, WatchlistItemService watchlistItemService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WatchlistDto>> Create([FromBody] CreateWatchlistRequest request, CancellationToken ct)
    {
        var dto = await watchlistService.CreateAsync(request.Name, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WatchlistDto>>> GetAll(CancellationToken ct)
    {
        var dtos = await watchlistService.GetAllAsync(ct);
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WatchlistDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await watchlistService.GetDetailAsync(id, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await watchlistService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<WatchlistItemDto>> AddItem(Guid id, [FromBody] AddWatchlistItemRequest request, CancellationToken ct)
    {
        var dto = await watchlistItemService.AddItemAsync(id, request.BaseCurrency, request.QuoteCurrency, ct);
        return CreatedAtAction(nameof(GetById), new { id }, dto);
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
    {
        await watchlistItemService.DeleteItemAsync(id, itemId, ct);
        return NoContent();
    }
}
