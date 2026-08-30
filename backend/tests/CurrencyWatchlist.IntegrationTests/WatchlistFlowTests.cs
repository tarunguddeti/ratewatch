using System.Net;
using System.Net.Http.Json;
using CurrencyWatchlist.Application.Dtos;
using FluentAssertions;

namespace CurrencyWatchlist.IntegrationTests;

public class WatchlistFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WatchlistFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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
}
