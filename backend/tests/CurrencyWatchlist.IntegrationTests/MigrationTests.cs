using CurrencyWatchlist.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CurrencyWatchlist.IntegrationTests;

/// <summary>US3 (specs/005-ratesnapshot-cache-cleanup/spec.md) - the migration that narrows
/// RateSnapshot's unique key must apply cleanly against a database that already accumulated
/// the old per-day-per-pair rows, and must leave the table in the new, empty, one-row-per-pair
/// shape rather than failing on a duplicate-key violation. This intentionally bypasses
/// CustomWebApplicationFactory (which auto-migrates a fresh database to the latest migration on
/// startup) - the whole point here is controlling exactly which migration has been applied
/// before the legacy data is seeded.</summary>
public class MigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ratewatch-migration-test-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ApplyingLatestOnlyCacheMigration_AgainstPreExistingPerDayRows_ClearsThemWithoutFailing()
    {
        var connectionString = $"Data Source={_dbPath}";

        // Stand the database up on the OLD schema only (InitialCreate: three-column unique
        // index, one row per pair per day allowed) and seed exactly the shape that behavior
        // produces - two rows for the same pair, different SourceTimestamp.
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options))
        {
            await db.Database.GetService<IMigrator>().MigrateAsync("20260830073508_InitialCreate");

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO RateSnapshots (Id, BaseCurrency, QuoteCurrency, Rate, SourceTimestamp, FetchedAt)
                VALUES (lower(hex(randomblob(16))), 'USD', 'AUD', 1.51, '2026-01-01', '2026-01-01 00:00:00'),
                       (lower(hex(randomblob(16))), 'USD', 'AUD', 1.52, '2026-01-02', '2026-01-02 00:00:00');
                """);

            (await db.RateSnapshots.CountAsync()).Should().Be(2, "the old per-day behavior must have produced two rows before the migration runs");
        }

        // Apply the rest of the pending migrations (just RateSnapshotLatestOnlyCache) the same
        // way the app does on startup - db.Database.MigrateAsync() with no target.
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options))
        {
            var act = () => db.Database.MigrateAsync();

            await act.Should().NotThrowAsync("the migration must clear conflicting legacy data before creating the new unique index, never fail on it");
        }

        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options))
        {
            (await db.RateSnapshots.CountAsync()).Should().Be(0, "FR-004: the historical per-day rows must be cleared, not carried forward or collapsed");

            // "Not yet fetched" for USD/AUD now reads identically to a pair that was never
            // refreshed at all - the same GetLatestAsync(...) == null path that
            // RateService.GetLatestAsync (RateService.cs) turns into a 404, already covered by
            // GetLatestAsync_NoSnapshotYet_ThrowsNotFoundException (unit) and
            // GetLatest_NoSnapshotYet_Returns404 (integration).
            var snapshot = await db.RateSnapshots.FirstOrDefaultAsync(r => r.BaseCurrency == "USD" && r.QuoteCurrency == "AUD");
            snapshot.Should().BeNull();
        }
    }

    public void Dispose()
    {
        foreach (var f in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
    }
}
