using System.Net;
using System.Net.Http.Json;
using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using FluentAssertions;

namespace CurrencyWatchlist.IntegrationTests;

public class RateRefreshFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FakeFrankfurterHandler _frankfurter;

    public RateRefreshFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _frankfurter = factory.FrankfurterHandler;
    }

    [Fact]
    public async Task AddPair_Refresh_ReadLatestRate_RoundTripsThroughFakeProvider()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Refresh Test"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("USD", "AUD"));

        var refreshResponse = await _client.PostAsync("/api/rates/refresh", null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await refreshResponse.Content.ReadFromJsonAsync<RefreshSummaryDto>();
        summary!.Refreshed.Should().ContainSingle(r => r.BaseCurrency == "USD" && r.QuoteCurrency == "AUD");

        var latest = await _client.GetFromJsonAsync<RateSnapshotDto>("/api/rates/latest?base=USD&quote=AUD");
        latest!.Rate.Should().Be(_frankfurter.Rates["USD/AUD"]);
    }

    [Fact]
    public async Task GetLatest_NoSnapshotYet_Returns404()
    {
        var response = await _client.GetAsync("/api/rates/latest?base=USD&quote=GBP");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // The date-range validation task (T036) targeted RateService, but that logic lives at
    // the controller boundary by design (contracts/api-contracts.md, RatesController.GetHistory)
    // - reallocated here since WebApplicationFactory is the natural place to verify
    // controller-level HTTP status codes end to end, without introducing an IRateService
    // interface purely for controller-unit-test mockability (constitution Article III).

    [Fact]
    public async Task GetHistory_FutureEndDate_Returns400()
    {
        var response = await _client.GetAsync($"/api/rates/history?base=USD&quote=AUD&to={DateTime.UtcNow.AddYears(1):yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_FromAfterTo_Returns400()
    {
        var response = await _client.GetAsync("/api/rates/history?base=USD&quote=AUD&from=2026-06-01&to=2026-01-01");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_RangeOverOneYear_Returns400()
    {
        var response = await _client.GetAsync("/api/rates/history?base=USD&quote=AUD&from=2020-01-01&to=2026-01-01");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_ValidRange_Returns200WithPoints()
    {
        var response = await _client.GetAsync("/api/rates/history?base=USD&quote=EUR&from=2026-08-01&to=2026-08-05");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = await response.Content.ReadFromJsonAsync<List<RateSnapshotDto>>();
        points.Should().HaveCount(5);
    }
}
