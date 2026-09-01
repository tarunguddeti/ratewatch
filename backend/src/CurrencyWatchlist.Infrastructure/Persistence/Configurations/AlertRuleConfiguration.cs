using CurrencyWatchlist.Domain;
using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyWatchlist.Infrastructure.Persistence.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.HasKey(r => r.Id);

        // Stored as the same "Above"/"Below" text the column already held before Condition
        // became a real enum (specs/004-strong-typing-cleanup/research.md decision 3).
        builder.Property(r => r.Condition).HasConversion<string>();

        // constitution Article IV: decimal, never double, end to end.
        builder.Property(r => r.Threshold).HasPrecision(MonetaryPrecision.Precision, MonetaryPrecision.Scale);

        // FR-018: no uniqueness constraint on WatchlistItemId - multiple rules per item,
        // including opposing conditions, are explicitly allowed.

        builder.HasMany(r => r.Events)
            .WithOne(e => e.AlertRule)
            .HasForeignKey(e => e.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
