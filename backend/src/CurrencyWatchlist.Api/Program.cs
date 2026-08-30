using CurrencyWatchlist.Api.Middleware;
using CurrencyWatchlist.Application.RateProvider;
using CurrencyWatchlist.Application.Repositories;
using CurrencyWatchlist.Application.Services;
using CurrencyWatchlist.Infrastructure.Persistence;
using CurrencyWatchlist.Infrastructure.RateProviders;
using CurrencyWatchlist.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The trace-ID scope only actually shows up in the terminal if the console logger is told to
// print scope values - BeginScope attaches it to the pipeline correctly regardless, but the
// default provider stays silent without this one line (docs/architecture.md:300-307).
builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();
builder.Services.AddScoped<IWatchlistItemRepository, WatchlistItemRepository>();
builder.Services.AddScoped<IRateSnapshotRepository, RateSnapshotRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();

builder.Services.AddScoped<WatchlistService>();
builder.Services.AddScoped<WatchlistItemService>();
builder.Services.AddScoped<RateService>();

// Typed HttpClient: FrankfurterRateProvider is the only place that knows Infrastructure talks
// to a third party at all. ~5s timeout, single retry on transient failure is implemented
// inside the provider itself - no Polly, no circuit breaker, at this scale
// (docs/architecture.md:1018).
var rateProviderBaseUrl = builder.Configuration["RateProvider:BaseUrl"]
    ?? throw new InvalidOperationException("RateProvider:BaseUrl is not configured.");
builder.Services.AddHttpClient<IRateProvider, FrankfurterRateProvider>(client =>
{
    client.BaseAddress = new Uri($"{rateProviderBaseUrl.TrimEnd('/')}/v2/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// One named CORS policy scoped to the frontend's exact origin, never AllowAnyOrigin
// (constitution Article IX).
const string CorsPolicyName = "Frontend";
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"]
    ?? throw new InvalidOperationException("Cors:AllowedOrigin is not configured.");
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Migrations auto-apply on startup rather than requiring a manual CLI step, and one sample
// watchlist is seeded on first run if empty - a take-home convenience, not a production
// pattern (docs/architecture.md:1020,1022; constitution Build Order step 3).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.ConfigureSqlitePragmasAsync();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TraceIdMiddleware wraps ExceptionHandlingMiddleware (not the reverse) so that even the
// exception handler's own log entry for a caught exception carries the same TraceId scope -
// registering it the other way around would mean the scope is already disposed by the time
// an exception propagates back up to the handler's catch block.
app.UseMiddleware<TraceIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.UseAuthorization();

app.MapControllers();

app.Run();
