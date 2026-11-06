# Tasks

Status values: Todo, Doing, Done. Update this file when a task changes state. Feature IDs refer to [features.md](features.md).

## Design revision (high-impact recommendations, 2026-11-04)

Applies recommendations 1-8 from the planning review. D1-D3 change the domain model; the rest follow from it. Each task leaves the docs consistent.

| ID  | Task                                                                                                                                                                          | Recommendation | Depends on | Status |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- | ---------- | ------ |
| D1  | Domain model: Operation (order x stage) and Lot (schedulable unit, 1..n per operation); resources with assigned calendars; cycle-based durations                              | 1, 5           |            | Done   |
| D2  | Domain model: replace machine status with time-bounded Unavailability (planned, breakdown); stage dependencies as (predecessor, successor, minLag), placeable in either order | 2, 4           | D1         | Done   |
| D3  | Domain model: ScheduleVersion/scenario on every placement (v1 uses "live" only). Rewrite rules R1-R8, worked examples and lifecycles for D1-D3                                | 3              | D2         | Done   |
| D4  | `architecture.md`: modules, API resources, persistence (exclusion constraint per scenario), rules engine inputs                                                               | 1-5            | D3         | Done   |
| D5  | `features.md`: earliest-slot finder and "schedule order ASAP" to P0; CSV import/export to P1; merge maintenance/breakdown features; recount P0 and milestones                 | 6, 7           | D3         | Done   |
| D6  | `tasks.md`: P7 becomes a synthetic plant generator; update M1-M4 tasks for D1-D5                                                                                              | 8              | D5         | Done   |
| D7  | Create `decisions.md` with one entry per decision above (date, reason, alternatives). Covers part of P4                                                                       | all            | D1-D6      | Done   |
| D8  | Consistency pass across all docs (IDs, cross-references, counts)                                                                                                              | all            | D7         | Done   |

## Preparation (before implementation)

| ID  | Task                                                                                                                                                       | Depends on | Status |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- | ------ |
| P1  | Answer the domain-model questions: Delayed as a derived flag, assumptions A1-A3, maintenance pausing (A6)                                                  |            | Done   |
| P2  | Update `features.md` for the P1 outcome (F-ORD-2 Delayed)                                                                                                  | P1         | Done   |
| P3  | Write `architecture.md`: module boundaries, API style, time zone handling, auth approach, error format, test strategy                                      | P1         | Done   |
| P4  | Settle the open and proposed entries in `decisions.md`: stack, cookie session, due-date semantics, module granularity, and DEC-16 to DEC-19 (D7 started the file) | D7         | Done   |
| P5  | Write acceptance criteria for M1 and M2 features                                                                                                           | D3         | Done   |
| P6  | Write acceptance criteria for scheduling rules R1-R8 and the engine operations `EarliestSlot` and `ScheduleOrderAsap` (needed before M3 and M4)          | D4         | Done   |
| P7  | Build a synthetic plant generator (see below). It produces the seed data, demo data and the F-NFR-5 performance data set                                  | P9, M2-3   | Todo   |
| P8  | Sketch wireframes: timeline view, lot placement form with earliest-slot suggestion, ASAP proposal review                                                   | D4         | Todo   |
| P9  | Scaffold the repository (backend, frontend, Postgres, engine project, test projects)                                                                       | P4         | Todo   |
| P10 | Fill in `CLAUDE.md`: coding standards, verification tools, methodology, current progress pointing here                                                    | P9         | Todo   |
| P11 | Break milestones M1-M5 into vertical-slice tasks below, using the acceptance criteria                                                                      | P5, P6     | Todo   |

### P7 synthetic plant generator

A command that builds a consistent plant from parameters, so seed data is never written by hand and demos, tests and performance checks share one source.

- Parameters: machines per resource type, number of products, routing shape (stages, lags, cycle times, units per cycle), plant calendar and holidays, orders per week, quantity range, due-date spread, share of rush orders, breakdown and maintenance frequency, horizon.
- Deterministic: the same random seed gives the same plant, so tests and bug reports can name a seed.
- Presets: `small` (a few machines, for tests), `plant` (the scenario in `business-problem.md`: ~20 machines, ~40 products, 3 shifts), `stress` (25 machines x 2 weeks, fully loaded, for F-NFR-5).
- Output: master data and orders written through the module interfaces, never raw SQL, so every generated row passes the same validation as user input. Optionally a placed live schedule, built with `ScheduleOrderAsap` once M4 exists.
- Never runs on normal startup in production (`architecture.md` section 8).
- Order: the generator writes through the module interfaces, so it needs the scaffold (P9) and the master data and order modules (up to M2-3). It was listed under D3 before; moved on 2026-11-06. The parameters and presets above are its specification.

## Implementation (draft, refine in P11)

Each task is a vertical slice: data, API, UI and tests together. Task IDs are provisional.

### M1 Foundation

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M1-1 | Sign in/out and roles | F-AUTH-1, F-AUTH-2 | P9 | Todo |
| M1-2 | Validation and error handling baseline, soft delete convention | F-NFR-1, F-NFR-2 | P9 | Todo |
| M1-3 | Resource types and machine register. Machines reference the plant default calendar, created by migration | F-MD-1 | M1-1, M1-2 | Todo |
| M1-4 | Product register | F-MD-2 | M1-1, M1-2 | Todo |

### M2 Orders

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M2-1 | Routing per product: stages with duration values, dependencies with lag (linear chain by default) | F-MD-3 | M1-4 | Todo |
| M2-2 | Plant calendar shifts and holiday list | F-MD-5 | M1-1 | Todo |
| M2-3 | Create, edit, cancel order, with operation, dependency and lot generation from the routing | F-ORD-1, F-ORD-3 | M2-1 | Todo |
| M2-4 | Derived order status in Queries (Pending and Cancelled until M3 and M5 add the others) | F-ORD-2 | M2-3 | Todo |
| M2-5 | Order list with search, filter and sort | F-ORD-4 | M2-3 | Todo |
| M2-6 | Order detail view with operations and lots | F-ORD-5 | M2-3, M2-4 | Todo |

### M3 Scheduling

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M3-1 | Scheduling engine: `ComputeEnd`, `Validate` (R1-R8), `FindConflicts`; unit tests from every worked example plus DST cases | F-SCH-4, F-SCH-5, F-SCH-6 | P6, M2-2 | Todo |
| M3-2 | Unavailability (planned, breakdown, open end) and derived machine status (Down, Maintenance, Idle; Running follows in M5-1) | F-MNT-1, F-MNT-2 | M1-3 | Todo |
| M3-3 | Unscheduled-lots queue | F-SCH-7 | M2-3 | Todo |
| M3-4 | Place a lot via form in the live version: locking, exclusion constraint, problem details for violations | F-SCH-1 | M3-1, M3-2, M3-3 | Todo |
| M3-5 | Read-only timeline view with conflicted placements flagged | F-SCH-2, F-SCH-4 | M3-4 | Todo |

### M4 Assisted scheduling

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M4-1 | Engine `EarliestSlot` with property tests (result passes `Validate`, nothing earlier is feasible) | F-SCH-11 | M3-1 | Todo |
| M4-2 | Earliest-slot suggestion in the placement form | F-SCH-11 | M4-1, M3-4 | Todo |
| M4-3 | Engine `ScheduleOrderAsap`, proposal and commit API, proposal review screen | F-SCH-16 | M4-1, M3-5 | Todo |

### M5 Execution

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M5-1 | Record lot actual start and end; adds In Progress, Completed and Running statuses | F-PRG-1 | M3-4 | Todo |
| M5-2 | Order progress from lot completion | F-PRG-2 | M5-1 | Todo |
| M5-3 | Planner dashboard: due soon, overdue, unscheduled lots, conflicted placements, machines down | F-RPT-1 | M5-2, M3-5 | Todo |

## Later (P1 and above)

Not tasked yet. See `features.md`.
