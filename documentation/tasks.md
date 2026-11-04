# Tasks

Status values: Todo, Doing, Done. Update this file when a task changes state. Feature IDs refer to [features.md](features.md).

## Design revision (high-impact recommendations, 2026-11-04)

Applies recommendations 1-8 from the planning review. D1-D3 change the domain model; the rest follow from it. Each task leaves the docs consistent.

| ID  | Task                                                                                                                                                                          | Recommendation | Depends on | Status |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- | ---------- | ------ |
| D1  | Domain model: Operation (order x stage) and Lot (schedulable unit, 1..n per operation); resources with assigned calendars; cycle-based durations                              | 1, 5           |            | Todo   |
| D2  | Domain model: replace machine status with time-bounded Unavailability (planned, breakdown); stage dependencies as (predecessor, successor, minLag), placeable in either order | 2, 4           | D1         | Todo   |
| D3  | Domain model: ScheduleVersion/scenario on every placement (v1 uses "live" only). Rewrite rules R1-R8, worked examples and lifecycles for D1-D3                                | 3              | D2         | Todo   |
| D4  | `architecture.md`: modules, API resources, persistence (exclusion constraint per scenario), rules engine inputs                                                               | 1-5            | D3         | Todo   |
| D5  | `features.md`: earliest-slot finder and "schedule order ASAP" to P0; CSV import/export to P1; merge maintenance/breakdown features; recount P0 and milestones                 | 6, 7           | D3         | Todo   |
| D6  | `tasks.md`: P7 becomes a synthetic plant generator; update M1-M4 tasks for D1-D5                                                                                              | 8              | D5         | Todo   |
| D7  | Create `decisions.md` with one entry per decision above (date, reason, alternatives). Covers part of P4                                                                       | all            | D1-D6      | Todo   |
| D8  | Consistency pass across all docs (IDs, cross-references, counts)                                                                                                              | all            | D7         | Todo   |

## Preparation (before implementation)

| ID  | Task                                                                                                                  | Depends on | Status |
| --- | --------------------------------------------------------------------------------------------------------------------- | ---------- | ------ |
| P1  | Answer the domain-model questions: Delayed as a derived flag, assumptions A1-A3, maintenance pausing (A6)             |            | Done   |
| P2  | Update `features.md` for the P1 outcome (F-ORD-2 Delayed)                                                             | P1         | Done   |
| P3  | Write `architecture.md`: module boundaries, API style, time zone handling, auth approach, error format, test strategy | P1         | Done   |
| P4  | Start `decisions.md`: stack, architecture style, A1-A7 with date and reason                                           | P1         | Todo   |
| P5  | Write acceptance criteria for M1 and M2 features                                                                      | P1         | Todo   |
| P6  | Write acceptance criteria for scheduling rules R1-R8 (needed before M3)                                               | P1         | Todo   |
| P7  | Create seed data: machines, products, routings, shift pattern, holidays                                               | P1         | Todo   |
| P8  | Sketch wireframes: timeline view and job placement form                                                               | P3         | Todo   |
| P9  | Scaffold the repository (backend, frontend, Postgres, test projects)                                                  | P3         | Todo   |
| P10 | Fill in `CLAUDE.md`: coding standards, verification tools, methodology, current progress pointing here                | P9         | Todo   |
| P11 | Break milestones M1-M4 into vertical-slice tasks below, using the acceptance criteria                                 | P5, P6     | Todo   |

## Implementation (draft, refine in P11)

Each task is a vertical slice: data, API, UI and tests together. Task IDs are provisional.

### M1 Foundation

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M1-1 | Sign in/out and roles | F-AUTH-1, F-AUTH-2 | P9 | Todo |
| M1-2 | Validation and error handling baseline, soft delete convention | F-NFR-1, F-NFR-2 | P9 | Todo |
| M1-3 | Machine register | F-MD-1 | M1-1, M1-2 | Todo |
| M1-4 | Product register | F-MD-2 | M1-1, M1-2 | Todo |

### M2 Orders

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M2-1 | Routing per product | F-MD-3 | M1-4 | Todo |
| M2-2 | Shift pattern and holiday list | F-MD-5 | M1-1 | Todo |
| M2-3 | Create, edit, cancel order, with job generation from routing | F-ORD-1, F-ORD-3 | M2-1 | Todo |
| M2-4 | Order status derivation | F-ORD-2 | M2-3 | Todo |
| M2-5 | Order list with search, filter and sort | F-ORD-4 | M2-3 | Todo |
| M2-6 | Order detail view | F-ORD-5 | M2-3, M2-4 | Todo |

### M3 Scheduling

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M3-1 | Pure scheduling-rules module: end-time calculation, conflict, stage order (unit tests from worked examples) | F-SCH-4, F-SCH-5, F-SCH-6 | P6, M2-2 | Todo |
| M3-2 | Machine status and maintenance windows | F-MNT-1, F-MNT-2 | M1-3 | Todo |
| M3-3 | Unscheduled-jobs queue | F-SCH-7 | M2-3 | Todo |
| M3-4 | Place a job via form, enforcing the rules | F-SCH-1 | M3-1, M3-2, M3-3 | Todo |
| M3-5 | Read-only timeline view | F-SCH-2 | M3-4 | Todo |

### M4 Execution

| ID | Task | Features | Depends on | Status |
|---|---|---|---|---|
| M4-1 | Record job status and actual start/end | F-PRG-1 | M3-4 | Todo |
| M4-2 | Order progress from job completion | F-PRG-2 | M4-1 | Todo |
| M4-3 | Planner dashboard | F-RPT-1 | M4-2 | Todo |

## Later (P1 and above)

Not tasked yet. See `features.md`.
