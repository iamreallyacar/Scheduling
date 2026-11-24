# Guide

## User setup

Needs the .NET 10 SDK with the ASP.NET Core runtime, Node.js 24 or later, and Docker with Compose. On Arch-based systems the ASP.NET Core runtime is a separate package (`aspnet-runtime`, `aspnet-targeting-pack`).

Clone the repository, then run these from its root:

```
cp .env.example .env                      # then choose a password in .env
docker compose up -d --wait               # PostgreSQL
export ConnectionStrings__Scheduler="Host=localhost;Port=5432;Database=tire_scheduler;Username=tire_scheduler;Password=<your password>"
export Auth__FirstAdmin__Email="<your email>" Auth__FirstAdmin__Password="<a password>"   # needed only while no user exists
dotnet run --project backend/src/TireScheduler.Api    # API on http://localhost:5080
cd frontend && npm install && npm run dev            # app on http://localhost:5173, /api proxied to the API
```

`GET /api/v1/health/live` answers when the API runs; `GET /api/v1/health/ready` also checks the database.

The API creates and updates its database tables when it starts. On the first start it also creates the first Admin from the two `Auth__FirstAdmin__` variables; sign in with them at http://localhost:5173. The password needs at least 6 characters with an upper-case letter, a lower-case letter, a digit and a symbol. Further users cannot be added in the app yet (F-AUTH-3).

### Configuration

Settings are read from environment variables (or `dotnet user-secrets` for the API in development). Secrets are never committed.

| Variable | Read by | Meaning |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `compose.yaml`, from `.env` | Database name, user and password of the local PostgreSQL container. Required |
| `POSTGRES_PORT` | `compose.yaml`, from `.env` | Host port for PostgreSQL. Default 5432 |
| `ConnectionStrings__Scheduler` | API | PostgreSQL connection string. Required; the API stops at startup with an error naming it if it is missing |
| `Auth__FirstAdmin__Email`, `Auth__FirstAdmin__Password` | API | Sign-in of the first Admin, created at startup while no user exists. Required then (startup stops with an error naming them), ignored afterwards. Never put them in a committed file |
| `Auth__Lockout__MaxFailedAttempts`, `Auth__Lockout__Minutes` | API | Failed sign-ins in a row before an account is locked, and for how long. Default 5 and 15 (`appsettings.json`) |
| `Auth__SessionHours` | API | Hours without a request after which a session ends. Default 12 |
| `Plant__TimeZone` | API | IANA time zone id of the plant, for example `Europe/Berlin`. Every calendar and every shown time uses it (A18). Required; startup stops with an error naming the setting or a value that is not an IANA id. Set to `Europe/Berlin` in `appsettings.Development.json` only |
| `Scheduling__SearchHorizonDays` | API | How many days after its start the earliest-slot search and the whole-order proposal look for a slot (AC-123). Default 90 (`appsettings.json`); startup stops with an error naming it if it is not more than 0 |
| `Security__CookiesRequireHttps` | API | `true` marks the session and anti-forgery cookies HTTPS-only. Default `true`; `false` only in `appsettings.Development.json`. Behind a proxy that ends HTTPS, the proxy must pass the original scheme on (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`) |
| `PlantLoader__Email`, `PlantLoader__Password` | plant loader | Sign-in of the Admin the loader writes as. Required by the loader only; never put them in a committed file |
| `PlantLoader__RequestTimeoutSeconds` | plant loader | Seconds one request to the API may take before the loader stops with an error (AC-226). Optional; default 300, a whole number above 0 |
| `API_PROXY_TARGET` | frontend dev server | Address of the API for the `/api` proxy. Set in `frontend/.env.development` |

### Load a generated plant

A demo or test plant comes from the synthetic plant generator (DEC-32), never from hand-written seed data. With the API running and the database empty (no resource types, products or orders, A31):

```
export PlantLoader__Email="<admin email>" PlantLoader__Password="<admin password>"
dotnet run --project backend/src/TireScheduler.PlantLoader -- --preset plant --seed 7
```

- `--preset` (required): `small` (4 machines, for tests), `plant` (the README.md scenario: 20 machines, 40 products, 8 weeks of orders) or `stress` (25 machines, 2 weeks, more work than working time).
- `--seed` (default 1): the same seed, preset and start date always give the same plant, so a bug report can name them.
- `--start` (default the Monday of the current week, A32): first day of the horizon, `yyyy-MM-dd`.
- `--api` (default `http://localhost:5080`): address of the API.
- `--schedule` (no value): after loading, place every order in the live schedule with the as-soon-as-possible proposal, earliest due date first, from the start of the horizon (DEC-61). An order with no free slot stays unplaced and is named. With the default start, lots on days before today are planned too; give a later `--start` for a schedule that lies ahead.
- The loader prints the seed, start date and counts, and with `--schedule` the placed lots and orders. Exit code 0 means loaded; 1 means the API rejected a request, did not answer in time or answered something that is not JSON, or the plant was not empty, with the reason printed; 2 means the command line or settings were wrong. A run that stopped halfway leaves the rows written so far, so start again on a new database.

## Developer setup

Follow User setup first. The checks to run after each kind of change are in CLAUDE.md, section VERIFICATION TOOLS.

Tooling notes:

- Database changes: `dotnet tool restore` once, then from `backend/` run `ConnectionStrings__Scheduler="Host=localhost" dotnet ef migrations add <Name> --project src/TireScheduler.Api --context <Module>DbContext --output-dir Modules/<Module>/Migrations`. The connection string is only read, not used, by this command.
- Local development runs over plain HTTP, so `appsettings.Development.json` turns off the HTTPS-only cookies. Chrome and Firefox are fine with this on localhost.
- `global.json` sits at the repository root and sets the .NET SDK version and the test runner (Microsoft Testing Platform), so `dotnet test` works from any folder.
- TypeScript stays on 6.0 until typescript-eslint supports 7.
- `frontend/package.json` overrides `minimatch` to 11.0.1 or later to fix advisory GHSA-g3hv-75wg-m249 in a lint dependency, and `js-yaml` to 4.3.2 or later to fix advisory GHSA-52cp-r559-cp3m in the type generator.
