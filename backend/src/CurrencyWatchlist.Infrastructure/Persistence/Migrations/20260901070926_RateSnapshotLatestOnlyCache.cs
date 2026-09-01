using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CurrencyWatchlist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RateSnapshotLatestOnlyCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Any pre-existing database may have multiple rows per (BaseCurrency, QuoteCurrency)
            // accumulated under the old per-day unique key - creating the new two-column unique
            // index below would fail on a duplicate-key violation against that data. Clearing
            // first guarantees the index creation always succeeds and directly satisfies "clear
            // the historical records" (specs/005-ratesnapshot-cache-cleanup/spec.md FR-004) -
            // RateSnapshot is disposable cache data, fully and correctly repopulated by the next
            // refresh or alert evaluation (data-model.md's migration section).
            migrationBuilder.Sql("DELETE FROM \"RateSnapshots\";");

            migrationBuilder.DropIndex(
                name: "IX_RateSnapshots_BaseCurrency_QuoteCurrency_SourceTimestamp",
                table: "RateSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_RateSnapshots_BaseCurrency_QuoteCurrency",
                table: "RateSnapshots",
                columns: new[] { "BaseCurrency", "QuoteCurrency" },
                unique: true);

            // No AlterColumn for SourceTimestamp: SQLite has no strict column typing, so
            // DateOnly -> DateTime is a TEXT -> TEXT no-op at the storage level. EF Core's
            // SQLite provider confirms this by not scaffolding one - only the model snapshot's
            // CLR-type metadata needed to change, which it already has.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The DELETE above is not reversible - there is no per-day history left to restore
            // by this point. Down() only reverts the index shape, consistent with this being an
            // intentionally one-way data cleanup (spec.md Assumptions).
            migrationBuilder.DropIndex(
                name: "IX_RateSnapshots_BaseCurrency_QuoteCurrency",
                table: "RateSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_RateSnapshots_BaseCurrency_QuoteCurrency_SourceTimestamp",
                table: "RateSnapshots",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "SourceTimestamp" },
                unique: true);
        }
    }
}
