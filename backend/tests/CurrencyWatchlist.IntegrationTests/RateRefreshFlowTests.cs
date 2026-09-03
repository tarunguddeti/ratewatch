using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CurrencyWatchlist.IntegrationTests;

public class RateRefreshFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly FakeFrankfurterHandler _frankfurter;

    public RateRefreshFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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

    // Date-range validation logic lives at the controller boundary by design
    // (RatesController.GetHistory) - covered here since WebApplicationFactory is the natural
    // place to verify controller-level HTTP status codes end to end, without introducing an
    // IRateService interface purely for controller-unit-test mockability.

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
        var points = await response.Content.ReadFromJsonAsync<List<RateHistoryPointDto>>();
        points.Should().HaveCount(5);
    }

    // At most one RateSnapshot row can ever exist per pair, and refreshing an already-cached
    // pair updates that row in place.

    [Fact]
    public async Task Refresh_SamePairTwice_UpdatesOneRowInPlace_DoesNotAccumulate()
    {
        _frankfurter.Rates["USD/GBP"] = 1.30m;

        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("US1 Repeat Refresh"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("USD", "GBP"));

        var firstRefresh = await _client.PostAsync("/api/rates/refresh", null);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Simulate a later refresh (a different day's rate) for the same pair.
        _frankfurter.Rates["USD/GBP"] = 1.35m;
        var secondRefresh = await _client.PostAsync("/api/rates/refresh", null);
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = db.RateSnapshots.Where(r => r.BaseCurrency == "USD" && r.QuoteCurrency == "GBP").ToList();

        rows.Should().ContainSingle("a second refresh must update the pair's one row, not add another");
        rows[0].Rate.Should().Be(1.35m, "the second refresh's value must win");
    }

    [Fact]
    public async Task Refresh_SamePairOnTwoWatchlists_BothSeeTheSameSingleUpdatedRecord()
    {
        var createA = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("US1 Watchlist A"));
        var watchlistA = await createA.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlistA!.Id}/items", new AddWatchlistItemRequest("USD", "EUR"));

        var createB = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("US1 Watchlist B"));
        var watchlistB = await createB.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlistB!.Id}/items", new AddWatchlistItemRequest("USD", "EUR"));

        var refresh = await _client.PostAsync("/api/rates/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = db.RateSnapshots.Where(r => r.BaseCurrency == "USD" && r.QuoteCurrency == "EUR").ToList();
        rows.Should().ContainSingle("two watchlists tracking the same pair must share one cached row, not one each");

        var detailA = await _client.GetFromJsonAsync<WatchlistDetailDto>($"/api/watchlists/{watchlistA.Id}");
        var detailB = await _client.GetFromJsonAsync<WatchlistDetailDto>($"/api/watchlists/{watchlistB.Id}");
        var rateA = detailA!.Items.Single(i => i.QuoteCurrency == "EUR").LatestRate;
        var rateB = detailB!.Items.Single(i => i.QuoteCurrency == "EUR").LatestRate;
        rateA!.Rate.Should().Be(rateB!.Rate);
        rateA.FetchedAt.Should().Be(rateB.FetchedAt);
    }

    // /rates/latest and /rates/refresh carry full date-and-time precision; /rates/history's
    // wire format is untouched.

    [Fact]
    public async Task Refresh_ThenGetLatest_SourceTimestampCarriesFullDateTimePrecision()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("US2 Precision"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("USD", "AUD"));
        await _client.PostAsync("/api/rates/refresh", null);

        var latestResponse = await _client.GetAsync("/api/rates/latest?base=USD&quote=AUD");
        using var latestJson = JsonDocument.Parse(await latestResponse.Content.ReadAsStringAsync());
        var latestTimestamp = latestJson.RootElement.GetProperty("sourceTimestamp").GetString();

        // A bare ISO date ("2026-02-23") is exactly 10 characters; a full ISO date-time
        // ("2026-02-23T00:00:00") is longer - confirms the response was not truncated back to
        // date-only.
        latestTimestamp!.Length.Should().BeGreaterThan(10, "the latest-rate response must carry a full timestamp, not just a date");
        DateTime.Parse(latestTimestamp).Should().NotBe(default);
    }

    [Fact]
    public async Task GetHistory_SourceTimestamp_RemainsBareDate_UnaffectedByLatestPrecisionChange()
    {
        var response = await _client.GetAsync("/api/rates/history?base=USD&quote=EUR&from=2026-08-01&to=2026-08-01");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var historyTimestamp = json.RootElement[0].GetProperty("sourceTimestamp").GetString();

        // History's wire format must be byte-for-byte unchanged - still a bare "YYYY-MM-DD"
        // date, exactly 10 characters, never widened.
        historyTimestamp.Should().HaveLength(10);
        historyTimestamp.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }
}
