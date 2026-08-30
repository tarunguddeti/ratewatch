using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyWatchlist.Infrastructure.Persistence.Configurations;

public class RateSnapshotConfiguration : IEntityTypeConfiguration<RateSnapshot>
{
    public void Configure(EntityTypeBuilder<RateSnapshot> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BaseCurrency).IsRequired().HasMaxLength(3);
        builder.Property(r => r.QuoteCurrency).IsRequired().HasMaxLength(3);

        // constitution Article IV: decimal, never double, end to end.
        builder.Property(r => r.Rate).HasPrecision(18, 6);

        // Makes the upsert idempotent: a same-day refresh updates FetchedAt on the
        // existing row instead of inserting a duplicate (data-model.md).
        builder.HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency, r.SourceTimestamp }).IsUnique();
    }
}
