using CurrencyWatchlist.Application.Exceptions;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Application.Services;
using CurrencyWatchlist.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CurrencyWatchlist.UnitTests.Services;

public class RateServiceTests
{
    private readonly Mock<IWatchlistItemRepository> _itemRepo = new();
    private readonly Mock<IRateSnapshotRepository> _rateSnapshotRepo = new();
    private readonly Mock<IRateProvider> _rateProvider = new();
    private readonly Mock<ILogger<RateService>> _logger = new();
    private readonly RateService _sut;

    public RateServiceTests()
    {
        _sut = new RateService(_itemRepo.Object, _rateSnapshotRepo.Object, _rateProvider.Object, _logger.Object);
    }

    private void VerifyLog(LogLevel level, Times times) =>
        _logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Fact]
    public async Task RefreshAllAsync_OneQuoteFailsUnderABase_OthersStillSucceed()
    {
        // A refresh must treat each pair independently; one failure must never prevent other
        // pairs in the same batch from succeeding and being saved.
        _itemRepo
            .Setup(r => r.GetDistinctPairsGroupedByBaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, List<string>> { ["USD"] = new() { "AUD", "ZZZ" } });

        _rateProvider
            .Setup(p => p.GetLatestRatesAsync("USD", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                RateResult.Ok(1.5m, DateTime.UtcNow, "AUD"),
                RateResult.Error(RateFailureReason.UnsupportedPair, "ZZZ"),
            });

        var summary = await _sut.RefreshAllAsync(CancellationToken.None);

        summary.Refreshed.Should().ContainSingle(r => r.QuoteCurrency == "AUD");
        summary.Failed.Should().ContainSingle(f => f.Pair == "USD/ZZZ");
        _rateSnapshotRepo.Verify(r => r.UpsertAsync("USD", "AUD", 1.5m, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        // A refresh always logs a completion summary, regardless of whether the failure came
        // from the fetch stage or the write stage.
        VerifyLog(LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task RefreshAllAsync_UpsertThrows_LandsInFailedWithoutLosingOthers()
    {
        // A write failure must land in failed[] exactly like a fetch failure, not silently
        // discard other already-fetched results still waiting to be saved.
        _itemRepo
            .Setup(r => r.GetDistinctPairsGroupedByBaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, List<string>> { ["USD"] = new() { "AUD", "EUR" } });

        _rateProvider
            .Setup(p => p.GetLatestRatesAsync("USD", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                RateResult.Ok(1.5m, DateTime.UtcNow, "AUD"),
                RateResult.Ok(0.9m, DateTime.UtcNow, "EUR"),
            });

        _rateSnapshotRepo
            .Setup(r => r.UpsertAsync("USD", "AUD", It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        var summary = await _sut.RefreshAllAsync(CancellationToken.None);

        summary.Failed.Should().ContainSingle(f => f.Pair == "USD/AUD");
        summary.Refreshed.Should().ContainSingle(r => r.QuoteCurrency == "EUR");

        // A per-pair write failure must leave a Warning-level log entry naming the pair, not
        // just land silently in failed[].
        VerifyLog(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task GetLatestAsync_NoSnapshotYet_ThrowsNotFoundException()
    {
        _rateSnapshotRepo
            .Setup(r => r.GetLatestAsync("USD", "AUD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RateSnapshot?)null);

        var act = () => _sut.GetLatestAsync("USD", "AUD", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetHistoryAsync_ProviderUnavailable_ThrowsRateProviderUnavailableException()
    {
        _rateProvider
            .Setup(p => p.GetHistoryAsync("USD", "EUR", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateHistoryResult.Error(RateFailureReason.Unavailable));

        var act = () => _sut.GetHistoryAsync("USD", "EUR", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<RateProviderUnavailableException>();
    }

    [Fact]
    public async Task GetHistoryAsync_UnsupportedPair_ThrowsValidationException()
    {
        _rateProvider
            .Setup(p => p.GetHistoryAsync("USD", "ZZZ", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateHistoryResult.Error(RateFailureReason.UnsupportedPair));

        var act = () => _sut.GetHistoryAsync("USD", "ZZZ", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
