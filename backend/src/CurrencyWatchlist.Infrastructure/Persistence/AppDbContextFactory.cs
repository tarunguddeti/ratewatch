using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CurrencyWatchlist.Infrastructure.Persistence;

/// <summary>Design-time-only factory so `dotnet ef migrations add` can construct AppDbContext
/// without the Api project's DI composition root existing yet (that wiring is T026). The
/// runtime connection string still comes from configuration via DI, not from here.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=watchlist.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}
