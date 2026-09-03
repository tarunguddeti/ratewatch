using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Application.RateProvider;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyWatchlist.Api.Controllers;

/// <summary>Added to back the currency dropdown so entry can be selection-only instead of free
/// text. A thin proxy to the provider's own reference endpoint, not a new database table.</summary>
[ApiController]
[Route("api/currencies")]
public class CurrenciesController(IRateProvider rateProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetAll(CancellationToken ct)
    {
        var currencies = await rateProvider.GetSupportedCurrenciesAsync(ct);
        var result = currencies.Select(c => new CurrencyDto(c.Key, c.Value)).OrderBy(c => c.Code).ToList();
        return Ok(result);
    }
}
