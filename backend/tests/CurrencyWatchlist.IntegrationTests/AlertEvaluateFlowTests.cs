using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using CurrencyWatchlist.Domain.Entities;
using FluentAssertions;

namespace CurrencyWatchlist.IntegrationTests;

public class AlertEvaluateFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AlertEvaluateFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(Guid WatchlistId, Guid ItemId)> CreateWatchlistItemAsync(string baseCurrency, string quoteCurrency)
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest($"Alert Test {Guid.NewGuid():N}"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        var addItem = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest(baseCurrency, quoteCurrency));
        var item = await addItem.Content.ReadFromJsonAsync<WatchlistItemDto>();
        return (watchlist.Id, item!.Id);
    }

    [Fact]
    public async Task CreateAlert_EvaluateTriggered_WithNoPriorRefresh()
    {
        // FakeFrankfurterHandler's USD/AUD rate is 1.5 - an "Above 1.0" rule must trigger,
        // and this happens with no /api/rates/refresh call anywhere in this test (FR-020).
        var (_, itemId) = await CreateWatchlistItemAsync("USD", "AUD");

        var createRule = await _client.PostAsJsonAsync("/api/alerts", new CreateAlertRuleRequest(itemId, AlertCondition.Above, 1.0m));
        createRule.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = await createRule.Content.ReadFromJsonAsync<AlertRuleDto>();

        var evaluate = await _client.PostAsync($"/api/alerts/{rule!.Id}/evaluate", null);
        evaluate.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await evaluate.Content.ReadFromJsonAsync<EvaluateResultDto>();

        result!.Triggered.Should().BeTrue();
        result.CurrentRate.Should().Be(1.5m);

        // FR-021 - evaluation must have upserted the pair's latest rate as a side effect.
        var latest = await _client.GetFromJsonAsync<RateSnapshotDto>("/api/rates/latest?base=USD&quote=AUD");
        latest!.Rate.Should().Be(1.5m);
    }

    [Fact]
    public async Task CreateAlert_EvaluateNotTriggered_NoEventRecorded()
    {
        var (_, itemId) = await CreateWatchlistItemAsync("USD", "AUD");
        var createRule = await _client.PostAsJsonAsync("/api/alerts", new CreateAlertRuleRequest(itemId, AlertCondition.Below, 1.0m));
        var rule = await createRule.Content.ReadFromJsonAsync<AlertRuleDto>();

        var evaluate = await _client.PostAsync($"/api/alerts/{rule!.Id}/evaluate", null);
        var result = await evaluate.Content.ReadFromJsonAsync<EvaluateResultDto>();

        result!.Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task OpposingRulesOnSamePair_CoexistAndEvaluateIndependently()
    {
        var (watchlistId, itemId) = await CreateWatchlistItemAsync("USD", "AUD");

        var aboveCreate = await _client.PostAsJsonAsync("/api/alerts", new CreateAlertRuleRequest(itemId, AlertCondition.Above, 1.0m));
        var belowCreate = await _client.PostAsJsonAsync("/api/alerts", new CreateAlertRuleRequest(itemId, AlertCondition.Below, 1.0m));
        aboveCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        belowCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var above = await aboveCreate.Content.ReadFromJsonAsync<AlertRuleDto>();
        var below = await belowCreate.Content.ReadFromJsonAsync<AlertRuleDto>();

        // FR-023 - both rules are listed for the watchlist.
        var list = await _client.GetFromJsonAsync<List<AlertRuleDto>>($"/api/alerts?watchlistId={watchlistId}");
        list.Should().HaveCount(2).And.Contain(r => r.Id == above!.Id).And.Contain(r => r.Id == below!.Id);

        var aboveResult = await (await _client.PostAsync($"/api/alerts/{above!.Id}/evaluate", null)).Content.ReadFromJsonAsync<EvaluateResultDto>();
        var belowResult = await (await _client.PostAsync($"/api/alerts/{below!.Id}/evaluate", null)).Content.ReadFromJsonAsync<EvaluateResultDto>();

        aboveResult!.Triggered.Should().BeTrue();
        belowResult!.Triggered.Should().BeFalse();
    }

    // Shape-check rejections (specs/003-dataannotations-validation - User Story 1).

    [Fact]
    public async Task CreateAlert_InvalidCondition_Returns400WithSpecificDetail()
    {
        var (_, itemId) = await CreateWatchlistItemAsync("USD", "AUD");

        // Condition is now AlertCondition (specs/004-strong-typing-cleanup), so an invalid
        // value can no longer be expressed via the strongly-typed CreateAlertRuleRequest
        // constructor at all - send it as raw JSON to exercise the deserialization-time
        // rejection path directly (research.md decision 2).
        var response = await _client.PostAsJsonAsync(
            "/api/alerts",
            new { watchlistItemId = itemId, condition = "Sideways", threshold = 1.5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail").GetString();
        detail.Should().NotBeNullOrWhiteSpace();
        detail.Should().NotBe("One or more validation errors occurred.");
    }

    [Fact]
    public async Task CreateAlert_WrongCaseCondition_Returns400()
    {
        // Regression test: System.Text.Json's built-in JsonStringEnumConverter matches enum
        // names case-insensitively on read, which would have silently accepted "above" - found
        // live during quickstart verification, not caught by any test until this one
        // (specs/004-strong-typing-cleanup/research.md decision 1).
        var (_, itemId) = await CreateWatchlistItemAsync("USD", "AUD");

        var response = await _client.PostAsJsonAsync(
            "/api/alerts",
            new { watchlistItemId = itemId, condition = "above", threshold = 1.5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAlert_NonPositiveThreshold_Returns400WithSpecificDetail(decimal threshold)
    {
        var (_, itemId) = await CreateWatchlistItemAsync("USD", "AUD");

        var response = await _client.PostAsJsonAsync("/api/alerts", new CreateAlertRuleRequest(itemId, AlertCondition.Above, threshold));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail").GetString();
        detail.Should().NotBeNullOrWhiteSpace();
        detail.Should().NotBe("One or more validation errors occurred.");
    }
}
