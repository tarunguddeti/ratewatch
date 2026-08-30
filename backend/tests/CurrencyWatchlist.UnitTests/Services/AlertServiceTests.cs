using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Application.Services;
using CurrencyWatchlist.Domain.Entities;
using FluentAssertions;
using Moq;

namespace CurrencyWatchlist.UnitTests.Services;

public class AlertServiceTests
{
    private readonly Mock<IAlertRuleRepository> _alertRuleRepo = new();
    private readonly Mock<IWatchlistItemRepository> _itemRepo = new();
    private readonly Mock<IRateSnapshotRepository> _rateSnapshotRepo = new();
    private readonly Mock<IRateProvider> _rateProvider = new();
    private readonly AlertService _sut;
    private readonly WatchlistItem _item = new() { Id = Guid.NewGuid(), BaseCurrency = "USD", QuoteCurrency = "AUD" };

    public AlertServiceTests()
    {
        _sut = new AlertService(_alertRuleRepo.Object, _itemRepo.Object, _rateSnapshotRepo.Object, _rateProvider.Object);
    }

    [Fact]
    public async Task CreateAsync_WatchlistItemNotFound_ThrowsNotFoundException()
    {
        _itemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WatchlistItem?)null);

        var act = () => _sut.CreateAsync(Guid.NewGuid(), "Above", 1.5m, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_NonPositiveThreshold_ThrowsValidationException(decimal threshold)
    {
        var act = () => _sut.CreateAsync(_item.Id, "Above", threshold, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidCondition_ThrowsValidationException()
    {
        var act = () => _sut.CreateAsync(_item.Id, "Sideways", 1.5m, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EvaluateAsync_RuleNotFound_ThrowsNotFoundException()
    {
        _alertRuleRepo.Setup(r => r.GetByIdWithItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AlertRule?)null);

        var act = () => _sut.EvaluateAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EvaluateAsync_RateExactlyEqualsThreshold_NotTriggered()
    {
        // The specific boundary docs/architecture.md calls out as worth a dedicated test:
        // rate == threshold must never count as "above" or "below" (constitution Article IV).
        var rule = new AlertRule { Id = Guid.NewGuid(), WatchlistItem = _item, Condition = "Above", Threshold = 1.5m };
        _alertRuleRepo.Setup(r => r.GetByIdWithItemAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _rateProvider
            .Setup(p => p.GetLatestRateAsync("USD", "AUD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateResult.Ok(1.5m, DateOnly.FromDateTime(DateTime.UtcNow)));

        var result = await _sut.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeFalse();
        _alertRuleRepo.Verify(r => r.AddEventAsync(It.IsAny<AlertEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Above", 1.4, 1.5, true)]
    [InlineData("Above", 1.6, 1.5, false)]
    [InlineData("Below", 1.6, 1.5, true)]
    [InlineData("Below", 1.4, 1.5, false)]
    public async Task EvaluateAsync_StrictComparison_MatchesExpected(string condition, decimal threshold, decimal currentRate, bool expectedTriggered)
    {
        var rule = new AlertRule { Id = Guid.NewGuid(), WatchlistItem = _item, Condition = condition, Threshold = threshold };
        _alertRuleRepo.Setup(r => r.GetByIdWithItemAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _rateProvider
            .Setup(p => p.GetLatestRateAsync("USD", "AUD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateResult.Ok(currentRate, DateOnly.FromDateTime(DateTime.UtcNow)));

        var result = await _sut.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().Be(expectedTriggered);
        _alertRuleRepo.Verify(r => r.AddEventAsync(It.IsAny<AlertEvent>(), It.IsAny<CancellationToken>()), expectedTriggered ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_NoPriorRefreshNeeded_FetchesLiveAndUpsertsAsSideEffect()
    {
        // FR-020/021 - evaluate obtains the rate itself; it never depends on a prior
        // refresh, and its result also becomes the pair's new latest known rate.
        var rule = new AlertRule { Id = Guid.NewGuid(), WatchlistItem = _item, Condition = "Above", Threshold = 1.0m };
        _alertRuleRepo.Setup(r => r.GetByIdWithItemAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _rateProvider
            .Setup(p => p.GetLatestRateAsync("USD", "AUD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateResult.Ok(1.5m, DateOnly.FromDateTime(DateTime.UtcNow)));

        await _sut.EvaluateAsync(rule.Id, CancellationToken.None);

        _rateSnapshotRepo.Verify(
            r => r.UpsertAsync("USD", "AUD", 1.5m, It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_ProviderUnavailable_ThrowsRateProviderUnavailableException()
    {
        var rule = new AlertRule { Id = Guid.NewGuid(), WatchlistItem = _item, Condition = "Above", Threshold = 1.0m };
        _alertRuleRepo.Setup(r => r.GetByIdWithItemAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _rateProvider
            .Setup(p => p.GetLatestRateAsync("USD", "AUD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateResult.Error(RateFailureReason.Unavailable));

        var act = () => _sut.EvaluateAsync(rule.Id, CancellationToken.None);

        await act.Should().ThrowAsync<RateProviderUnavailableException>();
        _rateSnapshotRepo.Verify(r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
