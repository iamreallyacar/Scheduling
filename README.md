# Tire Production Scheduler

Status: rebuild in progress. Milestone M1 is built: sign-in with roles, the machine register and the product register. Progress: [TASKS.md](TASKS.md).

A web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place.

## Business problem

A fictional scenario that set the project's direction. A mid-sized tire manufacturer runs one plant with about 15-25 machines in 3 shifts and makes roughly 40 tire models. Each tire passes through stages such as extrusion, building, curing and inspection, each on its own machine type. A small group of 2-5 planners builds the weekly schedule in spreadsheets and on a whiteboard. Machine status and maintenance windows reach them verbally, so the schedule goes stale within hours, and rush orders arrive every week.

Consequences: late orders are found only near their due date, some machines sit idle while others are overloaded, sales cannot promise reliable due dates, and re-planning after a disruption takes hours. The rebuild serves the production planner first; shift supervisors, sales and the plant manager come later. Candidate success measures, without baselines yet: on-time delivery rate, machine utilization, time to re-plan after a disruption, and share of due-date promises met.

## Where to read more

| Topic | Document |
|---|---|
| Features, priorities and v1 scope | [REQUIREMENTS/features.md](REQUIREMENTS/features.md) |
| Milestones, tasks and progress | [TASKS.md](TASKS.md) |
| Stack, modules and design rules | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Why each choice was made, open questions | [REQUIREMENTS/decisions.md](REQUIREMENTS/decisions.md) |
| Checks to run and repository layout | [CLAUDE.md](CLAUDE.md), sections VERIFICATION and DIRECTORY |

## Getting started

Needs the .NET 10 SDK with the ASP.NET Core runtime, Node.js 24 or later, and Docker with Compose. On Arch-based systems the ASP.NET Core runtime is a separate package (`aspnet-runtime`, `aspnet-targeting-pack`).

```
cp .env.example .env                      # then choose a password in .env
docker compose up -d --wait               # PostgreSQL
export ConnectionStrings__Scheduler="Host=localhost;Port=5432;Database=tire_scheduler;Username=tire_scheduler;Password=<your password>"
export Auth__FirstAdmin__Email="<your email>" Auth__FirstAdmin__Password="<a password>"   # needed only while no user exists
dotnet run --project backend/src/TireScheduler.Api    # API on http://localhost:5080
cd frontend && npm install && npm run dev            # app on http://localhost:5173, /api proxied to the API
```

`GET /api/v1/health/live` answers when the API runs; `GET /api/v1/health/ready` also checks the database. The API tests start PostgreSQL through Docker (Testcontainers), so Docker must be running.

The API creates and updates its database tables when it starts. On the first start it also creates the first Admin from the two `Auth__FirstAdmin__` variables; sign in with them at http://localhost:5173. The password needs at least 6 characters with an upper-case letter, a lower-case letter, a digit and a symbol. Further users cannot be added in the app yet (F-AUTH-3).

Tooling notes:

- After an API change, run the API tests once with `UPDATE_OPENAPI=1` to rewrite `backend/openapi/tire-scheduler-v1.json`, then `npm run generate:api` in `frontend/` to regenerate the TypeScript types in `frontend/src/api/generated`. A test fails while the file is out of date.
- Database changes: `dotnet tool restore` once, then from `backend/` run `ConnectionStrings__Scheduler="Host=localhost" dotnet ef migrations add <Name> --project src/TireScheduler.Api --context <Module>DbContext --output-dir Modules/<Module>/Migrations`. The connection string is only read, not used, by this command.
- Local development runs over plain HTTP, so `appsettings.Development.json` turns off the HTTPS-only cookies. Chrome and Firefox are fine with this on localhost.
- `global.json` sits at the repository root and sets the .NET SDK version and the test runner (Microsoft Testing Platform), so `dotnet test` works from any folder.
- TypeScript stays on 6.0 until typescript-eslint supports 7.
- `frontend/package.json` overrides `minimatch` to 11.0.1 or later to fix advisory GHSA-g3hv-75wg-m249 in a lint dependency, and `js-yaml` to 4.3.2 or later to fix advisory GHSA-52cp-r559-cp3m in the type generator.

## Configuration

Settings are read from environment variables (or `dotnet user-secrets` for the API in development). Secrets are never committed.

| Variable | Read by | Meaning |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `compose.yaml`, from `.env` | Database name, user and password of the local PostgreSQL container. Required |
| `POSTGRES_PORT` | `compose.yaml`, from `.env` | Host port for PostgreSQL. Default 5432 |
| `ConnectionStrings__Scheduler` | API | PostgreSQL connection string. Required; the API stops at startup with an error naming it if it is missing |
| `Auth__FirstAdmin__Email`, `Auth__FirstAdmin__Password` | API | Sign-in of the first Admin, created at startup while no user exists. Required then (startup stops with an error naming them), ignored afterwards. Never put them in a committed file |
| `Auth__Lockout__MaxFailedAttempts`, `Auth__Lockout__Minutes` | API | Failed sign-ins in a row before an account is locked, and for how long. Default 5 and 15 (`appsettings.json`) |
| `Auth__SessionHours` | API | Hours without a request after which a session ends. Default 12 |
| `Security__CookiesRequireHttps` | API | `true` marks the session and anti-forgery cookies HTTPS-only. Default `true`; `false` only in `appsettings.Development.json`. Behind a proxy that ends HTTPS, the proxy must pass the original scheme on (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`) |
| `API_PROXY_TARGET` | frontend dev server | Address of the API for the `/api` proxy. Set in `frontend/.env.development` |

## License

See [LICENSE](LICENSE).
