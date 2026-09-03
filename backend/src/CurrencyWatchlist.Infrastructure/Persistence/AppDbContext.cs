using CurrencyWatchlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CurrencyWatchlist.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<RateSnapshot> RateSnapshots => Set<RateSnapshot>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>How long SQLite waits on momentary lock contention before giving up with
    /// "database is locked".</summary>
    private const int BusyTimeoutMilliseconds = 5000;

    /// <summary>WAL mode lets reads continue while a write is in progress; the busy timeout
    /// means momentary lock contention waits briefly instead of failing immediately with
    /// "database is locked" - a different failure mode than the one the RateSnapshot atomic
    /// upsert solves.</summary>
    public async Task ConfigureSqlitePragmasAsync()
    {
        await Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await Database.ExecuteSqlRawAsync($"PRAGMA busy_timeout={BusyTimeoutMilliseconds};");
    }
}
