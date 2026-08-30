using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Infrastructure.Persistence;
using CurrencyWatchlist.Infrastructure.RateProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CurrencyWatchlist.IntegrationTests;

/// <summary>Points AppDbContext at a real, temp-file SQLite database (verifies actual EF Core
/// behavior a mock can't - migrations, constraints, cascades) and IRateProvider at
/// FakeFrankfurterHandler instead of the live API, per constitution Article X. Each instance
/// gets its own temp file so tests don't interfere with each other's data.</summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ratewatch-test-{Guid.NewGuid():N}.db");

    public FakeFrankfurterHandler FrankfurterHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

            // Bypass IHttpClientFactory's typed-client registration entirely for tests -
            // simpler and more robust than trying to override a named client's primary
            // handler after the fact.
            services.RemoveAll<IRateProvider>();
            services.AddScoped<IRateProvider>(sp => new FrankfurterRateProvider(
                new HttpClient(FrankfurterHandler) { BaseAddress = new Uri("http://fake-frankfurter/v2/") },
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<FrankfurterRateProvider>>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var f in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
    }
}
