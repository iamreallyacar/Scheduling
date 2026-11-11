# Tasks

Status values: Todo, Doing, Done. Update this file when a task changes state. Feature IDs refer to [features.md](REQUIREMENTS/features.md).

## Where to start

Handover for a new session. Updated at the end of every session; last updated 2026-11-10, after milestone M1.

- State: M1 Foundation is built and every check passes. A planner can sign in and an Admin maintains machine types, machines and products. Nothing is committed yet for M1.
- Next task: M2-1 (routing per product) or M2-2 (plant time zone, shifts, holidays); both are unblocked. M3-1 (the scheduling engine) depends only on P9 and can be built in parallel at any time.
- To review first: DEC-24 to DEC-27 in `decisions.md` are Proposed. They were made while building M1 and the code already follows them.
- Patterns to copy for a new module: `Modules/MasterData` (context, register classes, endpoints file), its tests in `TireScheduler.Api.Tests/MasterData`, and `frontend/src/products` for a page with a form. After an API change run the API tests once with `UPDATE_OPENAPI=1`, then `npm run generate:api` (README.md, Tooling notes).
- Known limit: the request validation filter checks only the top level of a request body. M2-1 (routing with nested stages) must extend it first (DEC-25).
- Before coding a task: read its acceptance criteria and the documents its row points to. Tasks marked "Write first" begin by writing their criteria (see Implementation below).
- Open questions: all are in the Open table of `decisions.md`. WQ1 to WQ3 are settled by the task named in their row. BQ1 to BQ5 wait for the business; the docs follow the stated assumptions until it replies.
- Local setup and tooling notes: README.md, section "Getting started".
- Documentation layout: CLAUDE.md DIRECTORY. The former `documentation/` folder is gone; its business problem is now a section of README.md.

## Completed planning

Design revision (D, 2026-11-04) and preparation (P, 2026-11-04 to 2026-11-10). Kept as one line each because decisions and documents refer to these IDs; the details are in git history.

| ID | Summary | Status |
|---|---|---|
| D1 | Domain model: Operation and Lot replace Job, resources with calendars, cycle-based durations | Done |
| D2 | Domain model: Unavailability replaces machine status, stage dependencies with lag | Done |
| D3 | Domain model: schedule versions; rules R1-R8, worked examples and lifecycles rewritten | Done |
| D4 | `ARCHITECTURE.md` follows D1-D3 | Done |
| D5 | `features.md`: earliest slot and ASAP to P0, CSV import and export to P1, maintenance features merged | Done |
| D6 | `TASKS.md`: synthetic plant generator, M1-M4 updated | Done |
| D7 | `decisions.md` created | Done |
| D8 | Consistency pass across all documents | Done |
| P1 | Domain-model questions answered: Late as a derived flag, A1-A3, A6 | Done |
| P2 | `features.md` updated for the P1 outcome | Done |
| P3 | `ARCHITECTURE.md` written | Done |
| P4 | Open and proposed decisions settled (DEC-16 to DEC-23) | Done |
| P5 | Acceptance criteria for M1 and M2 features | Done |
| P6 | Acceptance criteria for rules R1-R8 and the engine search | Done |
| P8 | Wireframes for the three scheduling screens | Done |
| P9 | Repository scaffolded (backend, frontend, PostgreSQL, engine, test projects) | Done |
| P10 | `CLAUDE.md` filled in | Done |
| P11 | Milestones broken into vertical-slice tasks | Done |

## Implementation

Each task is a vertical slice: data, API, UI and tests together. Broken down in task P11 on 2026-11-10 from the acceptance criteria.

- Criteria column: the acceptance criteria the task must make pass, as IDs or ranges ("AC-1 to AC-15"). A criterion that spans levels (for example Engine, API) is listed with every task that tests one of its levels.
- "Write first": the feature has no criteria yet. The task starts by adding them to [acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md), before any code (`acceptance-criteria.md` section 1 describes the format).
- WQ1 to WQ3 are open questions in the Open table of `decisions.md`. The task that names one settles it first and records the answer there.

### Milestones

Version 1 is the P0 features of [features.md](REQUIREMENTS/features.md), delivered in five milestones. Each milestone ends with something usable, and each P0 feature belongs to exactly one milestone.

| Milestone | Outcome | Features |
|---|---|---|
| M1 Foundation | Planner signs in and maintains machines and products | F-AUTH-1, F-AUTH-2, F-NFR-1, F-NFR-2, F-MD-1, F-MD-2 |
| M2 Orders | Planner enters orders and gets operations and lots generated from the product routing | F-MD-3, F-MD-5, F-ORD-1, F-ORD-2, F-ORD-3, F-ORD-4, F-ORD-5 |
| M3 Scheduling | Planner places lots on machines and is blocked from conflicts, broken stage dependencies and unavailable machines | F-SCH-7, F-SCH-1, F-SCH-2, F-SCH-5, F-SCH-6, F-SCH-4, F-MNT-1, F-MNT-2 |
| M4 Assisted scheduling | Planner gets earliest-slot suggestions and schedules a whole order in one step | F-SCH-11, F-SCH-16 |
| M5 Execution | Planner records lot progress and sees a dashboard of due, overdue, unscheduled and conflicted work | F-PRG-1, F-PRG-2, F-RPT-1 |

- F-SCH-4, F-SCH-5 and F-SCH-6 form the scheduling rules cluster, the riskiest part. Their acceptance criteria and worked examples were written first (task P6).
- M4 builds on the same engine as M3 (`ARCHITECTURE.md` section 3), so it is mostly engine search plus one review screen.
- M1 and M2 together give a working order-entry tool. M3 is where the app becomes a scheduler; M4 is where it becomes faster than the spreadsheet.

### M1 Foundation

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M1-2 | Error handling baseline: problem details with `code`, validation filter, correlation id for unexpected errors, frontend form error display, OpenAPI document and generated frontend client. Tested through a test-only endpoint until real ones exist | F-NFR-2 | AC-21, AC-22, AC-23, AC-25, AC-26 | P9 | Done |
| M1-1 | Sign in/out with ASP.NET Core Identity and cookie session, roles and policies, first Admin from configuration, sign-in page and role-aware app shell. Adds the first EF Core context (Auth schema) and the migration setup | F-AUTH-1, F-AUTH-2 | AC-1 to AC-15, AC-138, AC-139 | M1-2 | Done |
| M1-3 | Resource types and machine register (Admin edits, everyone reads). The MasterData migration creates the plant default calendar that every machine references | F-MD-1, F-NFR-1 | AC-16, AC-20, AC-24, AC-27 to AC-30, AC-40, AC-140, AC-141 | M1-1 | Done |
| M1-4 | Product register | F-MD-2 | AC-20, AC-24, AC-31 to AC-33 | M1-1 | Done |

M1-2 runs before M1-1 because sign-in errors already use the error format.

Parts of M1 criteria that need later data are tested by the task that adds the data, and are listed in its Criteria column: AC-20 for a resource type used by a routing stage (M2-1), a product used by an order (M2-3) and a resource used by an unavailability or a placement (M3-2, M3-4); AC-33 for the routing deleted with its product (M2-1); AC-32 for orders that keep pointing to a changed product (M2-3); AC-29 for a resource with placements (M3-4). AC-11 has no endpoint to test until M2-3 adds the first Planner write.

### M2 Orders

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M2-1 | Routing per product: stages with duration values, dependencies with lag (linear chain by default), routing editor | F-MD-3 | AC-20, AC-33, AC-34 to AC-38 | M1-3, M1-4 | Todo |
| M2-2 | Plant time zone setting, plant calendar shifts and holiday list | F-MD-5 | AC-24, AC-41 to AC-44 | M1-3 | Todo |
| M2-3 | Create, edit, cancel and delete order, with operation, dependency and lot generation from the routing; soft delete with a global query filter | F-ORD-1, F-ORD-3, F-NFR-1 | AC-11, AC-17 to AC-20, AC-23, AC-24, AC-32, AC-39, AC-45 to AC-52, AC-61 to AC-64 | M2-1 | Todo |
| M2-4 | Derived order status and late flag in Queries (Pending and Cancelled until M3 and M5 add the others) | F-ORD-2 | AC-53 to AC-57 | M2-3 | Todo |
| M2-5 | Order list with search, filter and sort | F-ORD-4 | AC-65 to AC-69 | M2-4 | Todo |
| M2-6 | Order detail view with operations and lots | F-ORD-5 | AC-70 | M2-4 | Todo |
| M2-7 | Synthetic plant generator (see below). It produces the seed data, demo data, the engine property-test plants and the F-NFR-5 performance data set | none | Write first, from the specification below | M2-2, M2-3 | Todo |

### M2-7 synthetic plant generator

A command that builds a consistent plant from parameters, so seed data is never written by hand and demos, tests and performance checks share one source. Until 2026-11-10 this was P7 under Preparation; it moved here because it writes through the master data and order modules, which exist only after M2-3.

- Parameters: machines per resource type, number of products, routing shape (stages, lags, cycle times, units per cycle), plant calendar and holidays, orders per week, quantity range, due-date spread, share of rush orders, breakdown and maintenance frequency, horizon.
- Deterministic: the same random seed gives the same plant, so tests and bug reports can name a seed.
- Presets: `small` (a few machines, for tests), `plant` (the scenario in README.md: ~20 machines, ~40 products, 3 shifts), `stress` (25 machines x 2 weeks, fully loaded, for F-NFR-5).
- Two parts (assumption made in P11): a pure core that turns parameters and a seed into plain values, with no database or module types, and a load command that writes those values through the module interfaces, never raw SQL, so every generated row passes the same validation as user input. The engine property tests (AC-115, AC-116, AC-127) build their snapshots from the core directly.
- A placed live schedule, built with `ScheduleOrderAsap`, is added by task M4-5.
- Never runs on normal startup in production (`ARCHITECTURE.md` section 8).

### M3 Scheduling

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M3-1 | Scheduling engine: `ComputeEnd`, `Validate` (R1-R8), `FindConflicts`; unit tests for its criteria, including the DST cases (engine level) | F-SCH-4, F-SCH-5, F-SCH-6 | AC-73 to AC-75, AC-78 to AC-82, AC-84 to AC-87, AC-89 to AC-106, AC-110 to AC-114 | P9 | Todo |
| M3-2 | Unavailability (planned, breakdown, open end) recorded on the machine page, and derived machine status (Down, Maintenance, Idle; Running follows in M5-1) | F-MNT-1, F-MNT-2 | AC-20; write first for the rest | M1-3 | Todo |
| M3-3 | Unscheduled-lots queue | F-SCH-7 | Write first | M2-3 | Todo |
| M3-4 | Place, move and remove a lot via the placement form in the live version: locking, exclusion constraint, problem details for violations (API level of the M3-1 rules). Settles WQ1 | F-SCH-1, F-SCH-4, F-SCH-5 | AC-20, AC-29, AC-58, AC-71, AC-74, AC-75, AC-77, AC-78, AC-83, AC-84, AC-89, AC-102, AC-109, AC-111 to AC-114; write first for the placement form | M2-2, M3-1, M3-2, M3-3 | Todo |
| M3-5 | Conflicts from later facts: `/conflicts` endpoint, `plannedEnd` recomputed after calendar and holiday changes, placements flagged after a breakdown or deactivation | F-SCH-4 | AC-76, AC-88, AC-107, AC-108 | M3-4 | Todo |
| M3-6 | Read-only timeline view with conflicted placements flagged, as in `wireframes.md` section 2. Settles WQ3 | F-SCH-2 | Write first | M3-5 | Todo |

### M4 Assisted scheduling

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M4-1 | Engine `EarliestSlot` with property tests (result passes `Validate`, nothing earlier is feasible) | F-SCH-11 | AC-115 to AC-124 | M3-1, M2-7 | Todo |
| M4-2 | Earliest-slot endpoint and suggestion in the placement form, as in `wireframes.md` section 3 | F-SCH-11 | AC-122, AC-125, AC-126; write first for the form | M4-1, M3-4 | Todo |
| M4-3 | Engine `ScheduleOrderAsap` (engine level) | F-SCH-16 | AC-127 to AC-131 | M4-1 | Todo |
| M4-4 | Proposal and commit API, proposal review screen opened from the order detail, as in `wireframes.md` section 4. Settles WQ2 | F-SCH-16 | AC-131 to AC-137 | M4-3, M3-6, M2-6 | Todo |
| M4-5 | Generator option: place a live schedule for the generated orders with `ScheduleOrderAsap` | none | Write first | M4-4, M2-7 | Todo |

### M5 Execution

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M5-1 | Record lot actual start and end; adds In Progress, Completed and Running statuses | F-PRG-1, F-MNT-2 | AC-59, AC-60, AC-72 (actuals); write first for F-PRG-1 | M3-4 | Todo |
| M5-2 | Order progress from lot completion | F-PRG-2 | AC-72 (progress); write first for F-PRG-2 | M5-1 | Todo |
| M5-3 | Planner dashboard: due soon, overdue, unscheduled lots, conflicted placements, machines down | F-RPT-1 | Write first | M5-2, M3-5, M3-3 | Todo |

## Later (P1 and above)

Not tasked yet. See `features.md`.
