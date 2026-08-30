# RateWatch — Currency Watchlist & Alert Service

Track currency pairs across named watchlists, refresh their live exchange rates, view rate
history, and define above/below threshold alerts you can evaluate on demand.

Full design spec: [`docs/architecture.md`](docs/architecture.md). This README is the
practical "how to run it" companion — the architecture doc is the authoritative source for
*why* things are built the way they are.

## Stack

- **Backend**: .NET 8, ASP.NET Core Web API, EF Core + SQLite, four-project clean architecture
  (Domain / Application / Infrastructure / Api)
- **Frontend**: React 19 + TypeScript, Vite, plain hooks (no Redux/React Query), Recharts
- **External data**: [Frankfurter](https://frankfurter.dev) v2 (no API key required)

## Setup

Prerequisites: .NET 8 SDK, Node.js 18+.

```bash
# Backend — migrations auto-apply and one sample watchlist seeds on first run
cd backend/src/CurrencyWatchlist.Api
dotnet run
# → http://localhost:5009, Swagger at /swagger

# Frontend — separate terminal
cd frontend
npm install
npm run dev
# → http://localhost:5173
```

No manual database setup is needed. Don't run the backend on port 5000 — macOS's AirPlay
Receiver occupies it by default and will silently swallow requests instead of erroring
clearly; 5009 (this project's default) avoids that.

### Environment variables

| Variable | Where | Default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Backend | Unset → production-like. Set to `Development` for Swagger + detailed errors. |
| `ConnectionStrings:DefaultConnection` | Backend (`appsettings.json`) | `Data Source=watchlist.db` |
| `RateProvider:BaseUrl` | Backend (`appsettings.json`) | `https://api.frankfurter.dev` |
| `Cors:AllowedOrigin` | Backend (`appsettings.json`) | `http://localhost:5173` |
| `VITE_API_BASE_URL` | Frontend (`.env.development`) | `http://localhost:5009` |

## Testing

```bash
# Backend — unit (mocked) then integration (real temp-file SQLite, fake Frankfurter handler)
cd backend
dotnet test

# Frontend — unit
cd frontend
npm run test

# Frontend — e2e (spins up both real servers automatically)
npm run test:e2e
```

51 backend tests, 7 frontend unit tests, 1 end-to-end happy path — all passing against real
dependencies wherever the constitution allows it (a real SQLite database for integration
tests; the live Frankfurter API for the e2e path), and strictly mocked everywhere else.

## Decisions worth knowing before you read the code

The full reasoning for all of these lives in `docs/architecture.md`'s Decisions & Tradeoffs
section — this is the short version.

- **Currency entry is dropdown-only, never free text.** The list is validated server-side
  against Frankfurter's real currency list at write time, fail-closed: if that list can't be
  verified, the add is rejected rather than silently accepted.
- **`RateSnapshot` is a shared latest-rate cache, not per-watchlist.** Two watchlists tracking
  the same pair see the same cached rate.
- **Evaluating an alert never depends on a prior refresh.** It fetches the pair's rate live at
  the moment it runs, and that fetch also updates the pair's stored latest rate as a side
  effect.
- **Refresh is fault-isolated per pair.** One pair failing to fetch or save never blocks or
  discards the others in the same batch.
- **No Unit of Work, no generic repository, no explicit transactions.** `DbContext`'s
  per-request scope already provides transactional grouping; the two genuine races in this
  system (duplicate-pair check, `RateSnapshot` upsert) are solved with atomic SQL statements,
  not transactions.
- **`AlertRule.IsActive` is stored but inert.** It's in the schema because the given data
  model includes it, but no endpoint in scope reads or sets it, and evaluate ignores it. A
  real next step would be a `PATCH /api/alerts/{id}` to toggle it plus a background evaluator
  that respects it — deliberately out of scope here.
- **No authentication, no multi-tenancy.** Single shared workspace, by design.

## What a production version would add

Covered in detail in `docs/architecture.md`'s Enterprise Diagram: a scheduled worker replacing
the manual Refresh/Evaluate buttons, a message bus decoupling alert evaluation from
notification delivery, Redis for the latest-rate cache, Postgres in place of SQLite, an API
gateway with real auth, and distributed tracing across the resulting hops.
