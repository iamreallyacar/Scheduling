# Tasks

## Milestones

Version 1 is the P0 features of [features.md](REQUIREMENTS/features.md), delivered in five milestones. Each milestone ends with something usable, and each P0 feature belongs to exactly one milestone.

| Milestone | Outcome | Features |
|---|---|---|
| M1 Foundation | Planner signs in and maintains machines and products | F-AUTH-1, F-AUTH-2, F-NFR-1, F-NFR-2, F-MD-1, F-MD-2 |
| M2 Orders | Planner enters orders and gets operations and lots generated from the product routing | F-MD-3, F-MD-5, F-ORD-1, F-ORD-2, F-ORD-3, F-ORD-4, F-ORD-5 |
| M3 Scheduling | Planner places lots on machines and is blocked from conflicts, broken stage dependencies and unavailable machines | F-SCH-7, F-SCH-1, F-SCH-2, F-SCH-5, F-SCH-6, F-SCH-4, F-MNT-1, F-MNT-2 |
| M4 Assisted scheduling | Planner gets earliest-slot suggestions and schedules a whole order in one step | F-SCH-11, F-SCH-16 |
| M5 Execution | Planner records lot progress and sees a dashboard of due, overdue, unscheduled and conflicted work | F-PRG-1, F-PRG-2, F-RPT-1 |

- F-SCH-4, F-SCH-5 and F-SCH-6 form the scheduling rules cluster, the riskiest part. Their acceptance criteria and worked examples were written first (task P6).
- M4 builds on the same engine as M3 (`ARCHITECTURE.md` section "Scheduling engine"), so it is mostly engine search plus one review screen.
- M1 and M2 together give a working order-entry tool. M3 is where the app becomes a scheduler; M4 is where it becomes faster than the spreadsheet.

## Handover

Handover for a new session. Updated at the end of every session; last updated 2026-11-19, after task M3-16.

- State: M1 Foundation, M2 Orders and M3 Scheduling are built and every check passes. An Admin maintains machines, products, routings, the plant calendar shifts and the holidays; a Planner creates, changes, cancels and deletes orders, the operations, dependencies and lots are generated from the routing, every order read carries its derived status and late flag, the order list searches, filters, sorts and pages on the server, and the order detail shows each lot's derived status and the order's progress. The synthetic plant generator builds `small`, `plant` and `stress` plants from a seed, and the plant loader writes them into an empty plant through the API (GUIDE.md section "Load a generated plant"). The scheduling engine checks placements against R1 to R4, R6 and R8 and finds the placements that later facts made break a rule (`PlacementRules`, engine README); one `PlacementContext` checks many requests against one indexed snapshot (DEC-47). A Planner records, changes and deletes the unavailabilities of a machine on its machine page (`/machines/{id}`), the machine reads carry the derived status Down, Maintenance or Idle from Queries (DEC-39), and the plant loader writes the generated unavailabilities. Anyone sees the lots that wait for placement on the schedule page (`/schedule`), which Queries serves from `GET /versions/live/unscheduled-lots` (F-SCH-7). A Planner places, moves and removes a lot in the live version through the placement form, opened from the queue and from the lots of the order page (F-SCH-1); the server checks every rule through the engine and rejects a broken one with every violation (AC-179), the order and lot read as Scheduled, the order detail shows each lot's live machine and planned times, and a planned end after the due day makes the order late. Cancelling an order removes the placements of its unstarted lots in every version. Later facts keep the placements (DEC-18): a shift or holiday change recomputes every stored planned end in the same transaction through the write port `IPlannedEndRecompute` (DEC-44, A42), the exclusion constraint on placements is gone and R2 across requests rests on the resource lock (DEC-43), and the schedule page lists the conflicted placements above the queue from `GET /versions/live/conflicts` with [Move...] for a Planner (AC-182 to AC-184). The schedule page shows the read-only timeline of the live version from `GET /versions/live/timeline?from=&to=` (DEC-45): machines by type with status, working periods from the engine's `WorkingPeriods` through Scheduling (DEC-49), unavailabilities and placement bars, one day or two weeks with the range in the address, conflicted bars marked from the conflict list (DEC-46), a lot popover with [Move...] and [Open order], and [Show ...] links from each violation to its item on the timeline (AC-185 to AC-191); until the conflict list has loaded no bar is marked and the page says so (AC-192).
- Next task: M4-1 can start now, and so can the maintenance task M3-15 from the second audit (AUDIT.md section "Second audit"); the M4 rows name the maintenance tasks they wait for. M5-1 can also start now. `EarliestSlot` checks each candidate with one `PlacementContext` built from the snapshot and its `Validate`, so it copies no rule (DEC-47). The engine property tests of M4-1 build their snapshots from `SyntheticPlant.Generate` (the `small` preset has open-ended breakdowns and lags), converting the plant-local unavailability starts to UTC through the engine (A33).
- Criteria sections: `check_docs.py` requires one for every feature of a milestone with a started task; F-SCH-5, F-SCH-6, F-SCH-11 and F-SCH-16 count through the section named in its `CRITERIA_SECTION_OF_FEATURE`, so starting M5 needs the F-PRG-1, F-PRG-2 and F-RPT-1 sections first.
- To review first: DEC-24 to DEC-38 in `decisions.md` are Proposed; DEC-33 (one transaction per request across module contexts, which settles the former open question on cancelling with one write port as an exception to DEC-16), DEC-34 (flat order answers with named required properties), DEC-35 (request bodies checked once for the `/api/v1` group) DEC-36 (one frontend cache refresh after every write) DEC-37 (frontend endpoint functions generated by a project script) and DEC-38 (shared API test data builders) are new, DEC-39 to DEC-41 from task M3-2 (machine reads in Queries, the UI converting typed plant-local times to UTC, an open-ended unavailability stored without an end), DEC-42 from task M3-4 (each rule violation with its own text and earliest allowed start), DEC-43 after it (R2 kept by the resource lock once M3-5 drops the exclusion constraint), and DEC-44 from task M3-5 (planned ends recomputed through a MasterData write port), DEC-45 and DEC-46 from task M3-6 (the timeline read and its range, which settles WQ3; conflict markers from the conflict list, which stays), DEC-47 from task M3-7 (one placement context checks many requests), DEC-48 from task M3-8 (the snapshot loaded from one scope), DEC-49 from task M3-9 (engine calendars built in one place, in Scheduling), DEC-50 from task M3-10 (lot context and machine names read for the given ids only), DEC-51 from task M3-11 (the timeline chart draws what it is given; the page filters once), DEC-52 from task M3-12 (a conflict list that has not loaded is unknown, not empty), DEC-53 from task M3-13 (shared API test setup, answer readers and fakes), and DEC-54 from task M3-14 (one whole-minute check, and the rule read from its code). New: A30 to A39 in `domain-model.md` (A35 and A36 from task M3-1: how violations are listed, and what `FindConflicts` checks; A37 and A38 from task M3-2: what an unavailability allows, and how typed local times are read; A39 from task M3-3: the queue returned whole; A40 and A41 from task M3-4: one placement per lot and version, and the live version as the only one until scenarios; A42 from task M3-5: which ends a working-time change keeps; A43 and A44 from task M3-6: which inactive machines the timeline shows, and its range), AC-151 to AC-192, WQ1, WQ3 and WQ4 settled, and the BQ2 to BQ4 rows, which now say an answer changes only the preset values.
- For M5-1: `LotStatusRules` and `OrderStatusRules` have no Completed or In Progress yet; when M5-1 adds them, the queue leaves out started lots by itself, and the timeline popover shows the status the API returns. Hide [Move...] in the popover (`frontend/src/schedule/LotPopover.tsx`) and the conflict list for a started lot, as the order page will. The machine status is derived in `Queries/Machines/ResourceStatusRules`; M5-1 adds Running from the lots in progress, and the timeline takes it from `MachineViewReader`. Typed plant-local times go through `frontend/src/calendar/plantClock.ts` (DEC-40). The recompute and `FindConflicts` leave completed lots out through `ILotProgressStates` (A36, A42), so they follow real progress at once.
- For M4-4 and M5-3: a read that describes a few placed lots takes their order, stage and lot from `IOrderReader.FindLotContextsAsync` and the machine names from `IMasterDataReader.GetResourceNamesAsync`, never from `ListAllDetailsAsync` or the full machine list (DEC-50).
- For M4-4: the timeline preview of a proposal can reuse `frontend/src/schedule/TimelineChart.tsx`, which draws every machine and bar of the `TimelineResponse` it is given and takes no filters (DEC-51); the conflicts come from `IPlacementConflictReader` (Scheduling) and the page joins them to the bars by `placementId` (DEC-46), marking none until they have loaded and saying so (DEC-52, as `SchedulePage.tsx` does).
- For M4-2 and M4-4: a request time is checked with `WholeMinuteTime.ErrorOf` (`Errors/`), and a new rule code keeps the form `rule.<rule>.<name>`, from which `RuleViolation` reads its rule (DEC-54).
- For M4-2: the placement form has the Placement part, the rejection list and [Remove placement]; add the "Find earliest slot" part above it (`wireframes.md` section 3). Placements are written only by `PlacementRegister` (Scheduling), which locks the resource and then the order (ARCHITECTURE.md section "Persistence rules"); the earliest-slot read builds its snapshot through `ScheduleSnapshotLoader` with a new wrapper whose scope is the resources of the lot's type and the lot's order (DEC-48), never with a loading query of its own.
- For M5-1: Progress answers `ILotProgress` (Orders) and `ILotProgressStates` (Scheduling) from `Modules/Progress/LotProgressFacts.cs` with "nothing started"; replace it with the stored progress, and the R8 checks, the cancel and the order rules use it at once. The API tests give started and completed lots through `FakeLotActivity` (`TireScheduler.Api.Tests/Fakes`), which stands in for both ports; test AC-19, AC-49, AC-51, AC-112, AC-113 and AC-50 again with real progress. A start can be recorded only for a lot placed in the live version (`IPlacementReader`). The order page shows no place or move action for a started lot; the read-only placement form with the reason comes with M5-1 (`wireframes.md` section 3).
- Patterns to copy for a new module: `Modules/Orders` (context, register for writes, reader for answers, endpoints file, ports), its tests in `TireScheduler.Api.Tests/Orders`, `frontend/src/orders` for a paged list whose filters live in the address under the API's parameter names (`OrderListPage.tsx`) and a detail page, and `frontend/src/calendar` and `frontend/src/routing` for a list of rows saved as a whole.
- For every new frontend read or write (M3 on): add its query key to `frontend/src/api/queryKeys.ts`; the query client reads the cached queries again after each successful write, so forms add no refresh code, and a delete names the deleted row's query in `meta.deletedQueryKey` (DEC-36). Its endpoint function comes from `npm run generate:api` into `src/api/generated/endpoints.gen.ts`; an operation the generator cannot describe (for example an optional body or a PATCH) stops the generation, and `frontend/scripts/endpointSource.ts` is extended then (DEC-37).
- For M3 and later API tests: build rows with the builders in `TireScheduler.Api.Tests/TestData` and add a class there for each new module (DEC-38); a reader of answers that a second class needs moves there too, a class whose tests each need a new API takes `SchedulerApiFixture.ForEachTest`, expected times are written with `PlantTime.Berlin`, and lot lengths come from `SchedulingTestData` (DEC-53). For the first test that needs a whole plant, check writing the generator's `small` preset through the loader's `PlantWriter` before adding a plant builder.
- Before coding a task: read its acceptance criteria and the documents its row points to. Tasks marked "Write first" begin by writing their criteria (see Implementation below).
- Open questions: all are in the Open table of `decisions.md`. WQ1, WQ3 and WQ4 are settled (tasks M3-4 and M3-6, DEC-43, DEC-45); WQ2 is settled by the task named in its row. BQ1 to BQ5 wait for the business; the docs follow the stated assumptions until it replies.
- Local setup and tooling notes: GUIDE.md, sections User setup and Developer setup. The API now needs `Plant__TimeZone` outside development.
- Documentation layout: CLAUDE.md DIRECTORY.

## Task list

Status values: Todo, Doing, Done. Update this file when a task changes state. Feature IDs refer to [features.md](REQUIREMENTS/features.md).

### Completed planning

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

### Implementation

Each task is a vertical slice: data, API, UI and tests together. Broken down in task P11 on 2026-11-10 from the acceptance criteria.

- Criteria column: the acceptance criteria the task must make pass, as IDs or ranges ("AC-1 to AC-15"). A criterion that spans levels (for example Engine, API) is listed with every task that tests one of its levels.
- "Write first": the feature has no criteria yet. The task starts by adding them to [acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md), before any code (`acceptance-criteria.md` section 1 describes the format).
- WQ1 to WQ3 are open questions in the Open table of `decisions.md`. The task that names one settles it first and records the answer there.

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
| M2-1 | Routing per product: stages with duration values, dependencies with lag (linear chain by default), routing editor | F-MD-3 | AC-20, AC-33, AC-34 to AC-38, AC-142, AC-143 | M1-3, M1-4 | Done |
| M2-2 | Plant time zone setting, plant calendar shifts and holiday list | F-MD-5 | AC-24, AC-41 to AC-44, AC-144 | M1-3 | Done |
| M2-3 | Create, edit, cancel and delete order, with operation, dependency and lot generation from the routing; soft delete with a global query filter | F-ORD-1, F-ORD-3, F-NFR-1 | AC-11, AC-17 to AC-20, AC-23, AC-24, AC-32, AC-39, AC-45 to AC-52, AC-61 to AC-64, AC-65, AC-68, AC-69, AC-145, AC-146 | M2-1 | Done |
| M2-4 | Derived order status and late flag in Queries (Pending and Cancelled until M3 and M5 add the others); Queries takes over the order reads and the order writes answer without the order (DEC-29) | F-ORD-2 | AC-53 to AC-57, AC-146, AC-147, AC-148 | M2-3 | Done |
| M2-5 | Order list with search, filter and sort | F-ORD-4 | AC-65 to AC-69, AC-149, AC-150 | M2-4 | Done |
| M2-6 | Order detail view with operations and lots | F-ORD-5 | AC-70 | M2-4 | Done |
| M2-7 | Synthetic plant generator (see below). It produces the seed data, demo data, the engine property-test plants and the F-NFR-5 performance data set | none | AC-151 to AC-160 | M2-2, M2-3 | Done |
| M2-8 | Maintenance before M3 (AUDIT.md item 1): one connection and transaction per request shared by every module context, row locks named from the EF model, and the cross-module transactions of M3-4 settled (DEC-33) | F-NFR-1 | AC-161, AC-162 | M2-3 | Done |
| M2-9 | Maintenance before M3 (AUDIT.md item 2): every record in the API project uses named required properties, and the order fields are mapped once in Orders (DEC-34). No change to the API | F-ORD-2, F-ORD-5 | none | M2-6 | Done |
| M2-10 | Maintenance before M3 (AUDIT.md item 3): request bodies are checked once for the whole `/api/v1` group instead of a step per endpoint, and a test fails for an endpoint that reads a body without the check (DEC-35). No change to the API | F-NFR-2 | AC-163 | M1-2 | Done |
| M2-11 | Maintenance before M3 (AUDIT.md item 4): every query key in one file, and every successful write refreshes the cached queries in one place instead of each form choosing its own (DEC-36). No change to the API | F-NFR-1 | AC-164 | M2-6 | Done |
| M2-12 | Maintenance before M3 (AUDIT.md item 5): the frontend endpoint functions are generated from the OpenAPI document with the types, so the method, URL, data and response type of each call come from one source (DEC-37). No change to the API | F-NFR-1 | AC-165 | M2-11 | Done |
| M2-13 | Maintenance before M3 (AUDIT.md item 6): the API test data builders move out of the test classes into one `TestData` folder, one class per module, and the copies of the product and routing builders are removed (DEC-38). No change to the API | none | none | M2-12 | Done |

Parts of M2 criteria that need later data are tested by the task that adds the data, and are listed in its Criteria column. M2-3 tests AC-19, AC-49 and AC-51 with the placement and progress facts given through the Orders ports (`TESTING.md` section "Fixtures and helpers"); M3-4 tests them again with real placements, together with the removal of placements when an order is cancelled (AC-50, DEC-33), and M5-1 with real progress. M2-3 built the order list with paging and the default sort (AC-65, AC-68, AC-69); M2-5 adds search, filters, the other sorts and the status column.

### M2-7 synthetic plant generator

A command that builds a consistent plant from parameters, so seed data is never written by hand and demos, tests and performance checks share one source. Until 2026-11-10 this was P7 under Preparation; it moved here because it writes through the master data and order modules, which exist only after M2-3.

- Parameters: machines per resource type, number of products, routing shape (stages, lags, cycle times, units per cycle), plant calendar and holidays, orders per week, quantity range, due-date spread, share of rush orders, breakdown and maintenance frequency, horizon.
- Deterministic: the same random seed gives the same plant, so tests and bug reports can name a seed.
- Presets: `small` (a few machines, for tests), `plant` (the scenario in README.md: ~20 machines, ~40 products, 3 shifts), `stress` (25 machines x 2 weeks, fully loaded, for F-NFR-5).
- Two parts (DEC-32): a pure core that turns parameters and a seed into plain values, with no database or module types, and a load command that writes those values through the API, never raw SQL, so every generated row passes the same validation as user input. The engine property tests (AC-115, AC-116, AC-127) build their snapshots from the core directly.
- A placed live schedule, built with `ScheduleOrderAsap`, is added by task M4-5.
- Never runs on normal startup in production (`ARCHITECTURE.md` section "Persistence rules").

### M3 Scheduling

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M3-1 | Scheduling engine: `ComputeEnd`, `Validate` (R1-R8), `FindConflicts`; unit tests for its criteria, including the DST cases (engine level) | F-SCH-4, F-SCH-5, F-SCH-6 | AC-73 to AC-76, AC-78 to AC-81, AC-84 to AC-108, AC-110 to AC-114 | P9 | Done |
| M3-2 | Unavailability (planned, breakdown, open end) recorded on the machine page, and derived machine status (Down, Maintenance, Idle; Running follows in M5-1). The plant loader then writes the generated unavailabilities | F-MNT-1, F-MNT-2 | AC-11, AC-20, AC-157, AC-166 to AC-173 | M1-3 | Done |
| M3-3 | Unscheduled-lots queue on the schedule page, served by Queries | F-SCH-7 | AC-174, AC-175 | M2-3 | Done |
| M3-4 | Place, move and remove a lot via the placement form in the live version: locking, exclusion constraint, problem details for violations (API level of the M3-1 rules). Settles WQ1 | F-SCH-1, F-SCH-4, F-SCH-5 | AC-19, AC-20, AC-29, AC-49, AC-50, AC-58, AC-71, AC-74, AC-75, AC-77, AC-78, AC-82, AC-83, AC-84, AC-89, AC-102, AC-109, AC-111 to AC-114, AC-174, AC-176 to AC-181 | M2-2, M3-1, M3-2, M3-3 | Done |
| M3-5 | Conflicts from later facts: `/conflicts` endpoint, `plannedEnd` recomputed after calendar and holiday changes, placements flagged after a breakdown or deactivation, listed on the schedule page. Drops the exclusion constraint (DEC-43) | F-SCH-4 | AC-76, AC-83, AC-88, AC-107, AC-108, AC-182 to AC-184 | M3-4 | Done |
| M3-6 | Read-only timeline view with conflicted placements flagged, as in `wireframes.md` section 2. Settles WQ3 | F-SCH-2 | AC-185 to AC-191 | M3-5 | Done |
| M3-7 | Maintenance before M4 (AUDIT.md item 7): the engine checks many placement requests against one indexed snapshot, and the static `PlacementRules` methods call that check, so the earliest-slot search reuses the rules instead of copying them. No change to the API | F-SCH-5 | none | M3-1 | Done |
| M3-8 | Maintenance before M4 (AUDIT.md item 8): the snapshot loader loads one scope (version, resources, orders), the two existing loads become wrappers around it, and which rule reads which rows is written in one place. No change to the API | F-SCH-4 | none | M3-5 | Done |
| M3-9 | Maintenance before M4 (AUDIT.md item 10): plant calendars become engine calendars in one place, the timeline asks for working periods instead of building them, and the placement and unavailability projections are written once. No change to the API | F-SCH-2 | none | M3-8 | Done |
| M3-10 | Maintenance before M4 (AUDIT.md item 9): Orders answers the order, stage and lot of given lot ids, and MasterData the names of given machines, through their read ports; the timeline and conflict readers use them instead of loading every order and machine. No change to the API | F-SCH-2, F-SCH-4 | none | M3-6 | Done |
| M3-11 | Maintenance before M4 (AUDIT.md item 11): the schedule page filters the timeline once and the chart draws what it is given, so M4-4 can reuse the chart for a proposal. No change to the API | F-SCH-2 | none | M3-6 | Done |
| M3-12 | Maintenance before M4 (AUDIT.md item 16): the schedule page shows the conflict markers only once the conflict list has loaded, and says so when it failed, instead of drawing every bar as conflict-free | F-SCH-2 | AC-192 | M3-11 | Done |
| M3-13 | Maintenance before M4 (AUDIT.md items 13, 18 and 22): the backend test helpers that read answers move to `TestData`, the per-class API factory setup moves to one fixture, the test plant's lot quantity and hours get names, and the progress fakes and failing stubs sit together. No change to the API | none | none | M3-6 | Done |
| M3-14 | Maintenance before M4 (AUDIT.md items 17 and 20): the whole-minute check of request times is written once, and each rule violation names its rule once. No change to the API | F-NFR-2, F-SCH-5 | none | M3-5 | Done |
| M3-15 | Maintenance before M4 (AUDIT.md items 23, 24 and 25): the frontend defines the lot title, the day length and the date locale once, its tests find table rows and sent bodies through shared helpers without casts, and the unchecked cast in `request.ts` says why. No change to the API | none | none | M3-6 | Todo |
| M3-16 | Maintenance before M4 (AUDIT.md item 26): `check_docs.py` requires a criteria section for the features of every milestone with a started task, not only M1 and M2; the features whose criteria sit under a rule section (F-SCH-5, F-SCH-6) are named in the script | none | none | M3-6 | Done |

M3-3 tested AC-174 with the placed lots given through a stand-in; M3-4 tests it with real placements and adds the place action to the queue.

### M4 Assisted scheduling

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M4-1 | Engine `EarliestSlot` with property tests (result passes `Validate`, nothing earlier is feasible). Starts with AUDIT.md item 19: one mapping from a generated plant to a `ScheduleSnapshot`, shared by the property tests | F-SCH-11 | AC-115 to AC-124 | M3-1, M2-7, M3-7 | Todo |
| M4-2 | Earliest-slot endpoint and suggestion in the placement form, as in `wireframes.md` section 3. Starts with AUDIT.md items 12 and 15: the timeline state moves out of the schedule page component, and the forms share one save frame | F-SCH-11 | AC-122, AC-125, AC-126; write first for the form | M4-1, M3-4, M3-8, M3-14 | Todo |
| M4-3 | Engine `ScheduleOrderAsap` (engine level) | F-SCH-16 | AC-127 to AC-131 | M4-1 | Todo |
| M4-4 | Proposal and commit API, proposal review screen opened from the order detail, as in `wireframes.md` section 4. Settles WQ2. Starts with AUDIT.md item 14: the plant time zone read once, in one way | F-SCH-16 | AC-131 to AC-137 | M4-3, M3-6, M2-6, M3-9, M3-10, M3-11 | Todo |
| M4-5 | Generator option: place a live schedule for the generated orders with `ScheduleOrderAsap`. Starts with AUDIT.md item 21: the plant load sums the engine's working periods | none | Write first | M4-4, M2-7 | Todo |

### M5 Execution

| ID | Task | Features | Criteria | Depends on | Status |
|---|---|---|---|---|---|
| M5-1 | Record lot actual start and end; adds In Progress, Completed and Running statuses | F-PRG-1, F-MNT-2 | AC-19, AC-49, AC-51, AC-59, AC-60, AC-72 (actuals); write first for F-PRG-1 | M3-4 | Todo |
| M5-2 | Order progress from lot completion | F-PRG-2 | AC-72 (progress); write first for F-PRG-2 | M5-1 | Todo |
| M5-3 | Planner dashboard: due soon, overdue, unscheduled lots, conflicted placements, machines down | F-RPT-1 | Write first | M5-2, M3-5, M3-3, M3-10 | Todo |
