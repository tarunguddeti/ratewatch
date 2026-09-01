using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Application.Services;
using CurrencyWatchlist.Domain.Entities;
using FluentAssertions;
using Moq;

namespace CurrencyWatchlist.UnitTests.Services;

public class WatchlistItemServiceTests
{
    private readonly Mock<IWatchlistRepository> _watchlistRepo = new();
    private readonly Mock<IWatchlistItemRepository> _itemRepo = new();
    private readonly Mock<IRateProvider> _rateProvider = new();
    private readonly WatchlistItemService _sut;
    private readonly Watchlist _watchlist = new() { Id = Guid.NewGuid(), Name = "Travel Fund" };

    private static readonly IReadOnlyDictionary<string, string> SupportedCurrencies =
        new Dictionary<string, string> { ["USD"] = "US Dollar", ["AUD"] = "Australian Dollar" };

    public WatchlistItemServiceTests()
    {
        _sut = new WatchlistItemService(_watchlistRepo.Object, _itemRepo.Object, _rateProvider.Object);
        _watchlistRepo.Setup(r => r.GetByIdAsync(_watchlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_watchlist);
    }

    [Fact]
    public async Task AddItemAsync_WatchlistNotFound_ThrowsNotFoundException()
    {
        _watchlistRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Watchlist?)null);

        var act = () => _sut.AddItemAsync(Guid.NewGuid(), "USD", "AUD", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddItemAsync_BaseEqualsQuote_ThrowsValidationException()
    {
        // Load-bearing on v2: the provider itself returns 200/rate 1.0 for a same-currency
        // pair rather than rejecting it (docs/architecture.md:1046) - this guard must fire
        // before the provider is ever consulted.
        var act = () => _sut.AddItemAsync(_watchlist.Id, "usd", "USD", CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _rateProvider.Verify(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_UnsupportedCurrency_ThrowsValidationException()
    {
        _rateProvider.Setup(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SupportedCurrencies);

        var act = () => _sut.AddItemAsync(_watchlist.Id, "USD", "ZZZ", CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _itemRepo.Verify(r => r.AddAsync(It.IsAny<WatchlistItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_CurrencyListUnavailable_PropagatesFailClosed()
    {
        // FR-008 - validation fails closed, never open. No try/catch should swallow this;
        // an unverifiable currency is never treated as valid.
        _rateProvider
            .Setup(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateProviderUnavailableException("down"));

        var act = () => _sut.AddItemAsync(_watchlist.Id, "USD", "AUD", CancellationToken.None);

        await act.Should().ThrowAsync<RateProviderUnavailableException>();
        _itemRepo.Verify(r => r.AddAsync(It.IsAny<WatchlistItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_DuplicatePair_ThrowsDuplicatePairException()
    {
        _rateProvider.Setup(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SupportedCurrencies);
        _itemRepo.Setup(r => r.ExistsAsync(_watchlist.Id, "USD", "AUD", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.AddItemAsync(_watchlist.Id, "USD", "AUD", CancellationToken.None);

        await act.Should().ThrowAsync<DuplicatePairException>();
    }

    [Fact]
    public async Task AddItemAsync_Valid_NormalizesCaseAndPersists()
    {
        _rateProvider.Setup(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SupportedCurrencies);
        _itemRepo.Setup(r => r.ExistsAsync(_watchlist.Id, "USD", "AUD", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.AddItemAsync(_watchlist.Id, "usd", "aud", CancellationToken.None);

        result.BaseCurrency.Should().Be("USD");
        result.QuoteCurrency.Should().Be("AUD");
        _itemRepo.Verify(r => r.AddAsync(It.Is<WatchlistItem>(i => i.BaseCurrency == "USD" && i.QuoteCurrency == "AUD"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_NotFound_ThrowsNotFoundException()
    {
        _itemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WatchlistItem?)null);

        var act = () => _sut.DeleteItemAsync(_watchlist.Id, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteItemAsync_BelongsToDifferentWatchlist_ThrowsNotFoundException()
    {
        var item = new WatchlistItem { Id = Guid.NewGuid(), WatchlistId = Guid.NewGuid() };
        _itemRepo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var act = () => _sut.DeleteItemAsync(_watchlist.Id, item.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _itemRepo.Verify(r => r.DeleteAsync(It.IsAny<WatchlistItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItemAsync_Valid_Deletes()
    {
        var item = new WatchlistItem { Id = Guid.NewGuid(), WatchlistId = _watchlist.Id };
        _itemRepo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        await _sut.DeleteItemAsync(_watchlist.Id, item.Id, CancellationToken.None);

        _itemRepo.Verify(r => r.DeleteAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }
}
