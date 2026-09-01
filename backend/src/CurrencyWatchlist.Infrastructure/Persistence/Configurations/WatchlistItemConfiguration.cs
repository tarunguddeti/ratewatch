using CurrencyWatchlist.Application;
using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyWatchlist.Infrastructure.Persistence.Configurations;

public class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.BaseCurrency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(i => i.QuoteCurrency).IsRequired().HasMaxLength(CurrencyCode.Length);

        // FR-007: the same pair can't be tracked twice in the same watchlist.
        builder.HasIndex(i => new { i.WatchlistId, i.BaseCurrency, i.QuoteCurrency }).IsUnique();

        builder.HasMany(i => i.AlertRules)
            .WithOne(r => r.WatchlistItem)
            .HasForeignKey(r => r.WatchlistItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
