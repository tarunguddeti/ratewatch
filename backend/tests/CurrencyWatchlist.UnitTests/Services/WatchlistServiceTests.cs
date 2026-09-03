using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Application.Services;
using CurrencyWatchlist.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CurrencyWatchlist.UnitTests.Services;

public class WatchlistServiceTests
{
    private readonly Mock<IWatchlistRepository> _watchlistRepo = new();
    private readonly Mock<IRateSnapshotRepository> _rateSnapshotRepo = new();
    private readonly Mock<ILogger<WatchlistService>> _logger = new();
    private readonly WatchlistService _sut;

    public WatchlistServiceTests()
    {
        _sut = new WatchlistService(_watchlistRepo.Object, _rateSnapshotRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidName_PersistsAndReturnsDto()
    {
        var result = await _sut.CreateAsync("Travel Fund", CancellationToken.None);

        result.Name.Should().Be("Travel Fund");
        result.ItemCount.Should().Be(0);
        result.AlertRuleCount.Should().Be(0);
        _watchlistRepo.Verify(r => r.AddAsync(It.Is<Watchlist>(w => w.Name == "Travel Fund"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ValidName_LogsCreation()
    {
        // A successful create leaves an Information-level log entry, not just a DTO the
        // caller might discard.
        await _sut.CreateAsync("Travel Fund", CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ProjectsItemAndAlertRuleCounts()
    {
        // Backs the delete confirmation on the Watchlists overview page.
        var watchlist = new Watchlist
        {
            Id = Guid.NewGuid(),
            Name = "Travel Fund",
            Items =
            {
                new WatchlistItem { AlertRules = { new AlertRule(), new AlertRule() } },
                new WatchlistItem(),
            },
        };
        _watchlistRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { watchlist });

        var result = await _sut.GetAllAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ItemCount.Should().Be(2);
        result[0].AlertRuleCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDetailAsync_NotFound_ThrowsNotFoundException()
    {
        _watchlistRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Watchlist?)null);

        var act = () => _sut.GetDetailAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetDetailAsync_JoinsLatestRatePerItem()
    {
        var item = new WatchlistItem { Id = Guid.NewGuid(), BaseCurrency = "USD", QuoteCurrency = "AUD" };
        var watchlist = new Watchlist { Id = Guid.NewGuid(), Name = "Travel Fund", Items = { item } };
        _watchlistRepo.Setup(r => r.GetByIdAsync(watchlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(watchlist);

        var snapshot = new RateSnapshot { BaseCurrency = "USD", QuoteCurrency = "AUD", Rate = 1.5m, SourceTimestamp = DateTime.UtcNow };
        _rateSnapshotRepo
            .Setup(r => r.GetLatestForPairsAsync(It.IsAny<IEnumerable<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string, string), RateSnapshot> { [("USD", "AUD")] = snapshot });

        var result = await _sut.GetDetailAsync(watchlist.Id, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].LatestRate.Should().NotBeNull();
        result.Items[0].LatestRate!.Rate.Should().Be(1.5m);
    }

    [Fact]
    public async Task GetDetailAsync_ItemWithNoRateYet_LatestRateIsNull()
    {
        var item = new WatchlistItem { Id = Guid.NewGuid(), BaseCurrency = "USD", QuoteCurrency = "AUD" };
        var watchlist = new Watchlist { Id = Guid.NewGuid(), Name = "Travel Fund", Items = { item } };
        _watchlistRepo.Setup(r => r.GetByIdAsync(watchlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(watchlist);
        _rateSnapshotRepo
            .Setup(r => r.GetLatestForPairsAsync(It.IsAny<IEnumerable<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string, string), RateSnapshot>());

        var result = await _sut.GetDetailAsync(watchlist.Id, CancellationToken.None);

        result.Items[0].LatestRate.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        _watchlistRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Watchlist?)null);

        var act = () => _sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _watchlistRepo.Verify(r => r.DeleteAsync(It.IsAny<Watchlist>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Found_DeletesWatchlist()
    {
        // Cascade to items/rules/events is a database-level concern (EF configuration,
        // covered by integration tests) - this test only verifies the service delegates
        // deletion of the correct aggregate root.
        var watchlist = new Watchlist { Id = Guid.NewGuid(), Name = "Travel Fund" };
        _watchlistRepo.Setup(r => r.GetByIdAsync(watchlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(watchlist);

        await _sut.DeleteAsync(watchlist.Id, CancellationToken.None);

        _watchlistRepo.Verify(r => r.DeleteAsync(watchlist, It.IsAny<CancellationToken>()), Times.Once);
    }
}
