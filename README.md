# RateWatch — Currency Watchlist & Alert Service

> **On process:** I worked through this architecture — the data model, the API contract, the
> error-handling strategy, every decision and tradeoff recorded below and in
> `docs/architecture.md` — thoroughly before writing any application code, not as an
> afterthought. I used Claude to help explore the design space and
> to implement the build itself, but the direction, the judgment calls, and every decision and
> tradeoff documented here are mine. The architecture doc is also up as a
> [Claude artifact](https://claude.ai/code/artifact/e0d7c71d-4ec1-40b1-8a30-5505627810eb) if
> you'd prefer to read it there. All the architecture diagrams and sequence diagrams are present in Claude 
> artifact and also in `docs/architecture.md` file

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

# Backend — with a coverage report (requires: dotnet tool install -g dotnet-reportgenerator-globaltool)
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
reportgenerator -reports:"./coverage/*/coverage.cobertura.xml" -targetdir:./coverage/report -reporttypes:"TextSummary;Html"

# Frontend — unit
cd frontend
npm run test

# Frontend — unit, with a coverage report
npm run test:coverage

# Frontend — e2e (spins up both real servers automatically)
npm run test:e2e
```

62 backend tests (36 unit, 26 integration), 25 frontend unit tests, 1 end-to-end happy path —
all passing against real dependencies wherever the project's own testing rules allow it (a real
SQLite database for integration tests; the live Frankfurter API for the e2e path), and strictly
mocked everywhere else. Line coverage: **81% backend**, **83.6% frontend** — both above the
project's 80% target.

## Assumptions

Things taken as given, not stated as a requirement, that shaped scope and design:

- **Single implicit user — no authentication, no multi-tenancy.** Every watchlist, item, and
  alert belongs to whoever is calling the API; there's no login and no concept of "my watchlist"
  versus someone else's. A scoping decision for a build this size, not an oversight — see the
  Enterprise Diagram in `docs/architecture.md` for where auth reappears in a real version.
- **Frankfurter is trusted as the sole source of truth for both currencies and rates.** Nothing
  in this build cross-checks it against a second provider or treats its data as anything other
  than authoritative.
- **Usage stays at human scale.** A person manually building a handful of watchlists with a
  handful of pairs each — not thousands of programmatically-generated rows. The sequential
  refresh write loop and the in-memory (rather than persisted) currency cache both lean on this
  directly; see Decisions below for why that's the right amount of engineering for this problem.
- **Multiple alert rules are allowed on the same currency pair.**  Including opposing or
  identical-threshold ones.** The brief doesn't restrict this, and a two-sided "above X, below Y"
  alert is a legitimate real use case, not a duplicate to reject.
- **A reviewer running this locally has the .NET 8 SDK and Node 18+ installed.**, and two free
  local ports — 5009 for the backend and 5173 for the frontend. No containerization or cloud environment is
  assumed.

## Decisions

The full reasoning for all of these lives in `docs/architecture.md`'s Decisions & Tradeoffs
section — this is the condensed version, grouped the same way, for a reviewer who wants the
gist before diving into the code.

### Backend project structure

- **The backend is split into four separate projects — Domain, Application, Infrastructure,
  Api — with one dependency direction enforced by the project references themselves, not by
  convention or code review.** `Domain` depends on nothing at all. `Application` depends only on
  `Domain`. `Infrastructure` depends on both. `Api` is the only project allowed to know
  `Infrastructure` exists, and it's the composition root that wires interfaces to their concrete
  implementations at startup. No project may reference in the reverse direction, ever — and that
  isn't a rule someone has to remember to follow, it's enforced by the .NET project reference
  graph itself: the solution simply won't compile if a lower layer reaches into a higher one.
- **This structure was chosen specifically to make the Controller → Service → Repository
  separation structural, not aspirational.** A single project with folders named `Controllers`,
  `Services`, and `Repositories` can still drift over time — nothing stops a controller from
  reaching straight into a `DbContext` six months in except discipline and a careful reviewer.
  Splitting those concerns into separate assemblies with an explicit, one-directional reference
  graph turns that drift into a compile error instead of something that has to be caught by eye.
  For a design meant to be reviewed, that guarantee was worth having from the start rather than
  hoped for later. The honest cost of this choice — four projects is real ceremony for a build
  this size — is recorded in Tradeoffs below, since it was taken on deliberately, not by default.

### Data model & business rules

- **`RateSnapshot`'s unique key is `(BaseCurrency, QuoteCurrency)` alone — deliberately excluding
  `SourceTimestamp` — because Frankfurter's v2 API already provides a real history endpoint, so
  this table was never meant to hold history.** Once `GET /rates/history` could proxy the
  provider's own date-range time series live, there was no reason left to also accumulate one
  `RateSnapshot` row per pair per day locally — that would just be a second, strictly worse copy
  of history the provider already answers correctly for any range, including dates before a pair
  was ever added to a watchlist. So the key was scoped to the pair alone on purpose: the table
  holds exactly one row per pair, ever, and a refresh is idempotent no matter when it last ran —
  it updates that single row in place via `ON CONFLICT DO UPDATE` instead of growing forever.
  This also means `RateSnapshot` carries no `WatchlistId` — two watchlists tracking the same pair
  correctly share the one cached row instead of duplicating it, since the cache is keyed on the
  currency pair itself, not on who's watching it.
- **`GET /api/rates/latest` exists, is fully implemented, and is intentionally not the endpoint
  the frontend uses to render a watchlist's rates — this is a deliberate choice, not a leftover
  or an unfinished wire-up.** It's a required single-pair endpoint, and it's covered by its own
  tests. But a watchlist detail view needs the latest rate for *every* item in that watchlist at
  once, and calling this endpoint once per item would be a textbook N+1 query pattern — one
  round trip per row instead of one for the whole page. So `GET /api/watchlists/{id}` batches
  every item's latest rate into a single query internally (`GetLatestForPairsAsync`) and returns
  them already joined onto the response, and the frontend was built against that batched shape
  from the start. `GET /api/rates/latest` stays fully working for a direct API consumer that
  only needs one pair — Swagger, curl, a future client that isn't this frontend — it was never
  meant to be called in a loop.
- **`WatchlistItem`'s unique constraint is scoped to `(WatchlistId, BaseCurrency, QuoteCurrency)`
  — per watchlist, not global across the whole system.** The same pair legitimately belongs in
  more than one watchlist at once — a "Travel Fund" watchlist and a "Business Trip" watchlist can
  both reasonably track USD/AUD, which is exactly why `RateSnapshot` above is shared and carries
  no `WatchlistId` of its own. What shouldn't happen is the *same* watchlist ending up with the
  *same* pair twice — a double-submitted form, a retried request — silently producing a
  duplicate row a user would have to notice and clean up themselves. Scoping the constraint to
  the watchlist blocks that real duplicate without blocking the legitimate cross-watchlist reuse:
  a repeat `POST` for a pair already on that watchlist gets a clean `409`, not a second row.
- **Delete cascades all the way down** — removing a watchlist takes its items, their alert rules,
  and their alert events with it, since there's no standalone endpoint to delete just a rule.
- **Alert conditions are four explicit values, not two.** `Above`/`Below` stay strict
  (`rate > threshold` / `rate < threshold`); `AboveOrEqual`/`BelowOrEqual` are separate,
  explicit inclusive counterparts (`>=`/`<=`) added alongside them rather than redefining what
  `Above`/`Below` mean — "goes above X" is genuinely ambiguous between the two readings, so both
  are offered instead of the system silently picking one.
- **All rate and threshold arithmetic is `decimal`, end to end**, never `double` — a single
  silent `double` anywhere in that chain would reintroduce floating-point comparison artifacts
  exactly where they'd decide whether an alert fires.
- **Multiple alert rules per item are allowed, including opposing or identical-threshold ones**
  (e.g. "above 1.60" and "below 1.50" on the same pair) — a legitimate two-sided alert, not a
  duplicate to reject.

### External API integration

- **Targets Frankfurter's v2 API, not the v1 URL the brief shows.** Verified directly rather
  than assumed: v1 still works via redirect, but v2 adds far more currency coverage, lets a
  refresh batch every quote for a base into one call, and returns trustworthy structured errors.
- **The currency list is cached in memory with a 24-hour TTL, deliberately not persisted to a
  database table.** Every argument for persisting it (avoid repeat calls, survive a restart,
  resilience) is either already solved by the in-memory cache or doesn't hold up at this scale;
  the app's actual data durability rests on `RateSnapshot`, which is already persisted.
- **A bounded ~5-second timeout and a single retry on transient failure — no Polly, no circuit
  breaker.** That resilience machinery is exactly what the production version adds instead of
  duplicating it here at small scale.
- **Migrations auto-apply on startup, and one sample watchlist seeds on first run.** A reviewer
  should be able to run the backend and immediately see a populated app, not an empty one or a
  manual migration step.
- **No `IUnitOfWork` and no explicit transactions anywhere.** `DbContext`'s per-request scope
  already commits multi-repository writes together, and the two genuine races in this system
  (the duplicate-pair check, the `RateSnapshot` upsert) aren't fixed by wrapping them in a
  transaction anyway — they're fixed by making each one a single atomic SQL statement instead.

### Currency validation

- **Currency entry is dropdown-only, never free text**, backed by our own `GET /api/currencies`
  rather than the frontend calling Frankfurter directly — keeping every third-party dependency
  behind the backend, consistently.
- **Validation is two layers, both at write time, and both must pass: format, then a real
  membership check against the live currency list — fail-closed.** If that list can't be
  verified, the add is rejected with a `502` rather than silently accepted; a currency that
  can't be verified is treated as unverified, never as valid by default.
- **Currency codes are uppercased at the boundary**, on both blur and in backend validation, so
  `usd/AUD` and `USD/AUD` can never be silently tracked as two different pairs.

### Refresh & evaluate

- **Refresh is global** (every distinct pair across every watchlist) **and fault-isolated per
  pair** — fetched concurrently, then written sequentially with each write in its own try/catch,
  so one pair failing to fetch or save never blocks or discards the others in the same batch.
- **The write phase is a plain `for` loop calling one upsert per pair — deliberately not a
  single bulk/batched SQL statement — because this data simply never reaches a size where that
  would matter.** A watchlist is built by a person clicking "Add Currency Pair" one pair at a
  time; nobody is going to hand-curate hundreds of currency pairs across dozens of watchlists,
  because there's a hard ceiling on how many currency pairs even exist to track and an even
  harder practical ceiling on how many a human will ever bother adding — this isn't a dataset
  that grows with usage the way rows in a multi-tenant SaaS product do. Reaching for a bulk-write
  path here would mean solving a throughput problem that doesn't exist in exchange for a write
  loop that's harder to reason about and — worse — harder to keep fault-isolated, since the
  per-pair try/catch above is exactly what a single bulk statement would give up. Optimizing for
  a load this application will never see isn't rigor, it's effort spent in the wrong place; the
  simple loop is the right amount of engineering for the actual problem, not a shortcut around it.
- **The `RateSnapshot` upsert is one atomic `INSERT ... ON CONFLICT DO UPDATE` statement**, not a
  check-then-insert sequence — closing a real race between two refreshes hitting the same pair
  at once.
- **Evaluating an alert never depends on a prior refresh.** It fetches the pair's rate live at
  the moment it runs, and that same fetch also updates the pair's stored latest rate as a side
  effect — so `AlertEvent` rows are written only when a rule actually triggers, never as a log of
  every check.

### Error handling & observability

- **`IRateProvider` returns a typed Result for the batch/loop path, and stays exceptions
  everywhere else** — a Result reads cleanly in `RefreshAllAsync`'s loop; every single-outcome
  call site keeps throwing, so there's still exactly one error-response mechanism at the HTTP
  boundary.
- **Three failure origins get three different messages, on purpose**: a `502` ("the rate
  provider is down"), a `500` ("our own bug"), and a network-level failure ("your request never
  arrived") are diagnostically different and shouldn't collapse into one generic banner.
- **Log level follows whether a failure is expected, not its status code** — `Warning` for
  anything the system is designed to hand back to a caller, `Error` (with a trace ID) for
  outages and bugs, `Information` for milestones regardless of outcome.
- **One server-generated trace ID per request, shown to the user only for `5xx` responses** —
  never for `4xx`, since those are already fixable from the message alone.

### Frontend & UX

- **A never-refreshed pair shows an explicit "Not fetched yet — click Refresh Rates" state**,
  not a blank cell — every empty/loading/error state in this app says plainly what's happening
  rather than leaving a gap to guess at.
- **"Refresh Rates" appears on both pages with an identical scope caption**, so wherever it's
  clicked from, its real scope (every pair, every watchlist) is stated up front.
- **Deleting a watchlist or item asks for confirmation that names what's about to be destroyed**
  — cascade delete is irreversible, so a bare delete button would be the one silent-destructive
  gap left in an otherwise explicit UI.
- **Loading state is split into "first load" and "an action is in progress," never conflated.**
  Adding, removing, or refreshing on an already-loaded screen shows a small local indicator
  instead of tearing down the table/chart/alerts back to a bare loading screen — that full-page
  reset is reserved for the very first load of a page (or a retry after it fails).
- **Delete/remove controls track their own busy state per item, not globally.** A control
  disables itself the moment its own request starts and re-enables when it settles, so a
  double-click can't fire the request twice, and one row's in-flight delete never blocks a
  different row's.
- **The rate history chart discards responses from requests it has since superseded.** Switching
  ranges or pairs while a fetch is still in flight can't let a slower, now-stale response
  silently overwrite newer data on screen — each request carries an identity that's checked
  before its result is ever applied.

### Naming & scope

- **Primary keys are `Guid` across every entity** — not for hiding record counts (there's no
  auth here to hide them from), but because IDs generated independently across services (what a
  real event-driven version would need) can't rely on one database handing out sequential
  numbers.
- **No authentication, no multi-tenancy.** Single shared workspace, by design, not omission.

## Tradeoffs

Real costs accepted on purpose, not oversights — recorded here so a reviewer sees the reasoning
rather than just the outcome. Full detail for each is in `docs/architecture.md`.

- **Four backend projects is more ceremony than a small build rewards on its own.** It earns its
  keep by making the Controller → Service → Repository separation structural instead of a
  naming convention — which is what actually matters here — but it's acknowledged overhead, not
  a free win.
- **Rate history is proxied live to Frankfurter instead of read from a local cache.** A pair
  added five minutes ago shows a genuine multi-day chart immediately, but the chart's
  availability is now tied to the provider's uptime at the moment someone views it, not just at
  refresh time.
- **Evaluate always calls the live provider instead of trusting the last cached rate** — an
  extra external call on every single evaluation, accepted at this scale, and exactly the call a
  production cache would absorb instead.
- **Currency validation chose correctness over availability, reversing an earlier version of
  this decision.** An earlier draft degraded to format-only validation when the currency list
  was unreachable; that let a bad currency slip in during an outage and only surface as wrong
  later. The current behavior is stricter and symmetrical: you simply cannot add a new pair
  while the provider is unreachable, but nothing unverified can ever reach the database.
- **Considered and rejected: one page with accordion rows instead of separate list/detail
  views.** It would have resolved the "where does Refresh Rates live" question elegantly, but at
  three real costs — the detail content is too rich to expand inline once more than one row is
  open, an accordion loses free deep-linking and back/forward behavior a real route gives you,
  and it was simply more implementation work than the two-page design it would have replaced.
- **`AlertRule.IsActive` is stored but currently inert.** It's in the schema because the data
  model calls for it, but no in-scope endpoint ever reads or sets it — evaluate deliberately
  ignores it too, since it's a manual trigger that should always run when clicked. A real next
  step would be a way to toggle it plus a background evaluator that respects it.
- **A partially-failed refresh returns `200` with a `failed[]` list, not a `207` or a thrown
  error.** Simpler for the frontend to render ("3 of 4 pairs updated") than branching on a less
  common status code, at the cost of not using the technically-more-correct one.
- **Evaluate's two writes aren't wrapped in one transaction, and they could in theory fall out of
  sync.** If the rate-snapshot write succeeds but the alert-event insert then fails, the cache
  updates but the trigger goes unrecorded. Accepted rather than solved with new machinery
  because it's narrow and self-healing — the failure surfaces as a real error, not a silent
  swallow, and evaluating again simply retries both writes cleanly.

## What a production version would add

Covered in detail in `docs/architecture.md`'s Enterprise Diagram: a scheduled worker replacing
the manual Refresh/Evaluate buttons, a message bus decoupling alert evaluation from
notification delivery, Redis for the latest-rate cache, Postgres in place of SQLite, an API
gateway with real auth, and distributed tracing across the resulting hops.
