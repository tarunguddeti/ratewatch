using CurrencyWatchlist.Application;
using CurrencyWatchlist.Domain;
using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyWatchlist.Infrastructure.Persistence.Configurations;

public class RateSnapshotConfiguration : IEntityTypeConfiguration<RateSnapshot>
{
    public void Configure(EntityTypeBuilder<RateSnapshot> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BaseCurrency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(r => r.QuoteCurrency).IsRequired().HasMaxLength(CurrencyCode.Length);

        // decimal, never double, end to end.
        builder.Property(r => r.Rate).HasPrecision(MonetaryPrecision.Precision, MonetaryPrecision.Scale);

        // Exactly one row per pair, ever - not per pair per day. Makes the upsert idempotent
        // regardless of when it last ran.
        builder.HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency }).IsUnique();
    }
}
