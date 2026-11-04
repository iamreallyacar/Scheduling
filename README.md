# Tire Production Scheduler

Status: rebuild in planning. No application code yet.

A web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place.

## Business problem

Planners build the weekly schedule in spreadsheets and on a whiteboard. Machine status and maintenance windows reach them verbally, so the schedule goes stale within hours. Planners cannot see which orders are at risk, which machines are free, or what a rush order would displace. Consequences: late orders found late, uneven machine load, unreliable due-date promises, and hours spent re-planning after a disruption.

The rebuild focuses on the **production planner**. Full detail: [documentation/business-problem.md](documentation/business-problem.md).

## Scope

Features are catalogued with priorities in [documentation/features.md](documentation/features.md). Version 1 contains the 24 P0 features, delivered in four milestones:

| Milestone | Outcome |
|---|---|
| M1 Foundation | Planner signs in and maintains machines and products |
| M2 Orders | Planner enters orders and gets jobs generated from the product routing |
| M3 Scheduling | Planner places jobs on machines and is blocked from conflicts, wrong stage order and maintenance windows |
| M4 Execution | Planner records job progress and sees a dashboard of due, overdue and unscheduled orders |

Not in v1: drag-and-drop scheduling, auto-scheduling, what-if scenarios, materials, notifications, ERP/MES integration, other personas (supervisor, sales, plant manager).

## Stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core Web API (.NET 9) |
| Frontend | React with TypeScript |
| Database | PostgreSQL |

## Architecture

Modular monolith: one deployable backend, split into modules with explicit boundaries.

- **Auth**: sign-in, roles (Planner, Viewer, Admin)
- **MasterData**: machines, products, routings, shift calendar
- **Orders**: orders, lifecycle states, job generation from routing
- **Scheduling**: job placement, timeline data, maintenance windows
- **Progress**: job status, actuals, order progress, dashboard queries

Design rules:
- The scheduling rules (conflict detection, stage sequence, shift calendar and holiday handling) live in a pure domain module with no database or web dependencies, so they can be unit tested with worked examples.
- Modules talk to each other through public interfaces, not through each other's tables.
- The frontend talks to the backend over a REST API only.

These decisions are provisional until the domain model is written. They depend on open questions in the features document (fixed routings, shift vs minute granularity).

## Repository layout

- `documentation/`: planning and design documents
- `legacy/`: previous implementation and README, kept for reference only and not part of the rebuild

## License

See [LICENSE](LICENSE).
