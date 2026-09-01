using CurrencyWatchlist.Domain;
using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyWatchlist.Infrastructure.Persistence.Configurations;

public class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Message).IsRequired();

        // constitution Article IV: decimal, never double, end to end.
        builder.Property(e => e.Rate).HasPrecision(MonetaryPrecision.Precision, MonetaryPrecision.Scale);
    }
}
