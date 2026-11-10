# Tire Production Scheduler

Status: rebuild in progress. Planning is finished and the repository is scaffolded; no features are built yet. Progress: [TASKS.md](TASKS.md).

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
dotnet run --project backend/src/TireScheduler.Api    # API on http://localhost:5080
cd frontend && npm install && npm run dev            # app on http://localhost:5173, /api proxied to the API
```

`GET /api/v1/health/live` answers when the API runs; `GET /api/v1/health/ready` also checks the database. The API tests start PostgreSQL through Docker (Testcontainers), so Docker must be running.

Tooling notes:

- `global.json` sits at the repository root and sets the .NET SDK version and the test runner (Microsoft Testing Platform), so `dotnet test` works from any folder.
- TypeScript stays on 6.0 until typescript-eslint supports 7.
- `frontend/package.json` overrides `minimatch` to 11.0.1 or later to fix advisory GHSA-g3hv-75wg-m249 in a lint dependency.

## Configuration

Settings are read from environment variables (or `dotnet user-secrets` for the API in development). Secrets are never committed.

| Variable | Read by | Meaning |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `compose.yaml`, from `.env` | Database name, user and password of the local PostgreSQL container. Required |
| `POSTGRES_PORT` | `compose.yaml`, from `.env` | Host port for PostgreSQL. Default 5432 |
| `ConnectionStrings__Scheduler` | API | PostgreSQL connection string. Required; the API stops at startup with an error naming it if it is missing |
| `API_PROXY_TARGET` | frontend dev server | Address of the API for the `/api` proxy. Set in `frontend/.env.development` |

## License

See [LICENSE](LICENSE).
