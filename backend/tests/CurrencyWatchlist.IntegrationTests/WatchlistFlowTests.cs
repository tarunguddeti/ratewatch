using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CurrencyWatchlist.Api.Requests;
using CurrencyWatchlist.Application.Dtos;
using FluentAssertions;

namespace CurrencyWatchlist.IntegrationTests;

public class WatchlistFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FakeFrankfurterHandler _frankfurter;

    public WatchlistFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _frankfurter = factory.FrankfurterHandler;
    }

    [Fact]
    public async Task CreateWatchlist_AddPair_ReadBack_RoundTripsThroughRealDatabase()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Integration Test WL"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();

        var addItem = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("USD", "AUD"));
        addItem.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await _client.GetFromJsonAsync<WatchlistDetailDto>($"/api/watchlists/{watchlist.Id}");
        detail!.Items.Should().ContainSingle(i => i.BaseCurrency == "USD" && i.QuoteCurrency == "AUD");
    }

    [Fact]
    public async Task DeleteWatchlist_CascadesToItems_VerifiedAgainstRealForeignKeys()
    {
        // Verifies the actual EF Core cascade configuration (OnDelete(Cascade)) against a
        // real SQLite database - a mock repository couldn't catch a missing FK constraint.
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("To Delete"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("GBP", "JPY"));

        var delete = await _client.DeleteAsync($"/api/watchlists/{watchlist.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfterDelete = await _client.GetAsync($"/api/watchlists/{watchlist.Id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddDuplicatePair_RejectedByRealUniqueIndex()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Duplicate Test"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();

        await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("USD", "EUR"));
        var secondAttempt = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist.Id}/items", new AddWatchlistItemRequest("USD", "EUR"));

        secondAttempt.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Shape-check rejections (specs/003-dataannotations-validation - User Story 1).

    [Fact]
    public async Task CreateWatchlist_BlankName_Returns400WithSpecificDetailAndNoWatchlistCreated()
    {
        var before = await _client.GetFromJsonAsync<List<WatchlistDto>>("/api/watchlists");

        var response = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("   "));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail").GetString();
        detail.Should().NotBeNullOrWhiteSpace();
        detail.Should().NotBe("One or more validation errors occurred.");

        var after = await _client.GetFromJsonAsync<List<WatchlistDto>>("/api/watchlists");
        after.Should().HaveCount(before!.Count);
    }

    [Fact]
    public async Task AddItem_MalformedBaseCurrency_Returns400WithSpecificDetail()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Malformed Shape Test"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();

        var response = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("US", "AUD"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail").GetString();
        detail.Should().NotBeNullOrWhiteSpace();
        detail.Should().NotBe("One or more validation errors occurred.");
    }

    [Fact]
    public async Task AddItem_BothCurrenciesMalformed_ReturnsSingle400MentioningBothFields()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Multi-Field Shape Test"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();

        var response = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("US", "1D"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.TryGetProperty("BaseCurrency", out _).Should().BeTrue();
        errors.TryGetProperty("QuoteCurrency", out _).Should().BeTrue();
        var detail = body.GetProperty("detail").GetString();
        detail.Should().Contain("BaseCurrency").And.Contain("QuoteCurrency");
    }

    [Fact]
    public async Task AddItem_MalformedCurrencyShape_NeverCallsCurrencyProvider()
    {
        var create = await _client.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("Provider Call Test"));
        var watchlist = await create.Content.ReadFromJsonAsync<WatchlistDto>();
        var callsBefore = _frankfurter.CurrenciesCallCount;

        var response = await _client.PostAsJsonAsync($"/api/watchlists/{watchlist!.Id}/items", new AddWatchlistItemRequest("US", "AUD"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _frankfurter.CurrenciesCallCount.Should().Be(callsBefore);
    }
}
