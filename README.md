# Tire Production Scheduler

Status: rebuild in planning. No application code yet.

A web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place.

## Business problem

Planners build the weekly schedule in spreadsheets and on a whiteboard. Machine status and maintenance windows reach them verbally, so the schedule goes stale within hours. Planners cannot see which orders are at risk, which machines are free, or what a rush order would displace. Consequences: late orders found late, uneven machine load, unreliable due-date promises, and hours spent re-planning after a disruption.

The rebuild focuses on the production planner. Full detail: [documentation/business-problem.md](documentation/business-problem.md).

## Scope

Features are catalogued with priorities in [documentation/features.md](documentation/features.md). Version 1 contains the 26 P0 features, delivered in five milestones:

| Milestone | Outcome |
|---|---|
| M1 Foundation | Planner signs in and maintains machines and products |
| M2 Orders | Planner enters orders and gets operations and lots generated from the product routing |
| M3 Scheduling | Planner places lots on machines and is blocked from conflicts, broken stage dependencies and unavailable machines |
| M4 Assisted scheduling | Planner gets earliest-slot suggestions and schedules a whole order in one step |
| M5 Execution | Planner records lot progress and sees a dashboard of due, overdue, unscheduled and conflicted work |

Not in v1: drag-and-drop scheduling, multi-order auto-scheduling, what-if scenarios (the data model already supports them), materials, notifications, ERP/MES integration, other personas (supervisor, sales, plant manager).

## Stack

Accepted in [documentation/decisions.md](documentation/decisions.md) (DEC-20):

| Layer | Choice |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core Web API, EF Core |
| Frontend | React with TypeScript |
| Database | PostgreSQL |

## Architecture

Modular monolith: one deployable backend, split into module folders with explicit boundaries checked by an architecture test, plus a separate scheduling engine project (DEC-23). Details: [documentation/architecture.md](documentation/architecture.md).

- Auth: sign-in, roles (Planner, Viewer, Admin)
- MasterData: resources (machines), calendars, holidays, products, routings with stage dependencies
- Orders: orders, operations and lots generated from the routing
- Scheduling: placements per schedule version, machine unavailability (maintenance, breakdowns)
- Progress: actual start and end of lots
- Queries: derived statuses, late flags, dashboard and list views

Design rules:

- The scheduling engine (rule checks, working-time calculation, earliest-slot search) is a pure library with no database or web dependencies, unit tested with the worked examples in the domain model.
- Modules talk to each other through public interfaces, not through each other's tables.
- The frontend talks to the backend over a REST API only.

## Verification

`python3 scripts/check_docs.py` checks that the planning documents agree with each other (links, section references, feature and task IDs, P0 counts, milestones, decisions).

## Repository layout

- `documentation/`: planning and design documents
- `scripts/`: verification scripts
- `legacy/`: previous implementation and README, kept for reference only and not part of the rebuild

## License

See [LICENSE](LICENSE).
