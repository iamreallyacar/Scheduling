# Decisions

Status values: Accepted (in force), Proposed (written into the docs, waiting for review), Open (not decided), Superseded (replaced by a later entry). Task P4 in [TASKS.md](../TASKS.md) settled every Proposed and Open entry on 2026-11-06.

To change a decision, add a new entry that supersedes the old one and mark the old one Superseded. Do not edit an accepted entry's reasoning after the fact.

## Index

| ID | Decision | Status | Date | Where |
|---|---|---|---|---|
| DEC-1 | Late is a derived flag, not an order state | Accepted | 2026-11-04 (P1) | domain-model §6 |
| DEC-2 | Routing fixed per product, copied into each order | Accepted | 2026-11-04 (P1) | A1 |
| DEC-3 | UTC storage, one plant time zone, 15-minute UI grid | Accepted | 2026-11-04 (P1) | A2, ARCHITECTURE.md section "Time rules" |
| DEC-4 | One plant calendar in v1, as data | Accepted | 2026-11-04 (P1) | A3 |
| DEC-5 | Lots never run across an unavailability | Accepted | 2026-11-04 (P1) | A6, R3 |
| DEC-6 | Operation and Lot replace Job | Accepted | 2026-11-04 (D1) | domain-model §2, A6 |
| DEC-7 | Machines are resources with a type, kind and calendar | Accepted | 2026-11-04 (D1) | domain-model §2, A3, A4 |
| DEC-8 | Duration from setup, cycle time and units per cycle | Accepted | 2026-11-04 (D1) | domain-model §4 |
| DEC-9 | Unavailability replaces stored machine status | Accepted | 2026-11-04 (D2) | R3, R5, domain-model §6 |
| DEC-10 | Stage dependencies with lag, checked in either placement order | Accepted | 2026-11-04 (D2) | R4, A8, A11 |
| DEC-11 | Every placement belongs to a schedule version | Accepted | 2026-11-04 (D3) | A9, A10 |
| DEC-12 | All statuses derived, never stored | Accepted | 2026-11-04 (D3) | domain-model §6 |
| DEC-13 | Earliest slot and "schedule order ASAP" are P0 | Accepted | 2026-11-04 (D5) | features §2.4, §3 |
| DEC-14 | CSV import and export are P1 | Accepted | 2026-11-04 (D5) | F-MD-9, F-ORD-9 |
| DEC-15 | Synthetic plant generator instead of hand-written seed data | Accepted | 2026-11-04 (D6) | tasks M2-7 |
| DEC-16 | Upward reads between modules through ports | Accepted | 2026-11-06 (P4) | ARCHITECTURE.md section "Module rules" |
| DEC-17 | Schedule version in the URL path, `live` alias | Accepted | 2026-11-06 (P4) | API.md section "Conventions" |
| DEC-18 | Later facts flag placements instead of deleting them | Accepted | 2026-11-06 (P4) | ARCHITECTURE.md section "Persistence rules", domain-model §5 |
| DEC-19 | "Schedule order ASAP" as proposal, then commit | Accepted | 2026-11-06 (P4) | API.md section "Scheduling calls" |
| DEC-20 | Stack: .NET 10 (LTS) ASP.NET Core, EF Core, PostgreSQL, React with TypeScript | Accepted | 2026-11-06 (P4) | README.md section "Project overview" |
| DEC-21 | Cookie session instead of JWT | Accepted | 2026-11-06 (P4) | ARCHITECTURE.md section "Access rules" |
| DEC-22 | Due date ends at the end of its plant-local day | Accepted | 2026-11-06 (P4) | ARCHITECTURE.md section "Time rules" |
| DEC-23 | Engine as its own project, modules as folders in one API project | Accepted | 2026-11-06 (P4) | ARCHITECTURE.md sections "Structure" and "Module rules" |
| DEC-24 | Migrations are applied at startup, module by module | Proposed | 2026-11-10 (M1-1) | ARCHITECTURE.md section "Persistence rules" |
| DEC-25 | Minimal API endpoints; module code rejects a request by throwing a problem exception | Proposed | 2026-11-10 (M1-2) | ARCHITECTURE.md section "Module rules", API.md section "Error format" |
| DEC-26 | Session ends through the user's security stamp; lockout counted on the injected clock | Proposed | 2026-11-10 (M1-1) | ARCHITECTURE.md section "Access rules", A21 |
| DEC-27 | OpenAPI document kept in the repository; frontend types generated from it | Proposed | 2026-11-10 (M1-2) | API.md section "Conventions", ARCHITECTURE.md section "Frontend rules" |
| DEC-28 | Lot duration is computed by the engine, so Orders may use the engine | Proposed | 2026-11-18 (M2-3) | ARCHITECTURE.md section "Structure", domain-model §4 |
| DEC-29 | Queries serves the order reads; order writes answer without the order | Proposed | 2026-11-18 (M2-4) | API.md section "Conventions", ARCHITECTURE.md section "Structure" |
| DEC-30 | The late flag is computed by the engine, so Queries may use the engine | Proposed | 2026-11-18 (M2-4) | ARCHITECTURE.md section "Structure", domain-model §6 |
| DEC-31 | The order list searches, filters and sorts stored fields in the database; status and late filters are applied in Queries | Proposed | 2026-11-18 (M2-5) | API.md section "Conventions", domain-model A28 |
| DEC-32 | Plant generator as a pure core library and a loader that writes through the HTTP API | Proposed | 2026-11-18 (M2-7) | ARCHITECTURE.md section "Structure", acceptance-criteria §6 |
| DEC-33 | One transaction per request, shared by every module context; row locks named from the EF model | Proposed | 2026-11-18 (M2-8) | ARCHITECTURE.md sections "Module rules" and "Persistence rules", AC-161, AC-162 |
| DEC-34 | Order read models stay flat; records in the API use named required properties | Proposed | 2026-11-18 (M2-9) | ARCHITECTURE.md section "Module rules" |
| DEC-35 | Request bodies are checked once for the whole `/api/v1` group, not per endpoint | Proposed | 2026-11-18 (M2-10) | ARCHITECTURE.md sections "Module rules" and "Error and API rules", AC-163 |
| DEC-36 | Every successful write refreshes the cached queries in one place; query keys are defined in one file | Proposed | 2026-11-18 (M2-11) | ARCHITECTURE.md section "Frontend rules", AC-164 |
| DEC-37 | Frontend endpoint functions generated from the OpenAPI document by a project script | Proposed | 2026-11-18 (M2-12) | ARCHITECTURE.md section "Frontend rules", AC-165 |
| DEC-38 | API test data is built by shared builders in one folder; requests stay anonymous objects | Proposed | 2026-11-18 (M2-13) | TESTING.md section "Fixtures and helpers" |
| DEC-39 | Queries serves the machine reads with the derived status; the machine writes stay in MasterData | Proposed | 2026-11-18 (M3-2) | ARCHITECTURE.md section "Structure", AC-172 |
| DEC-40 | The UI converts plant-local input times to UTC and UTC to plant time for display | Proposed | 2026-11-18 (M3-2) | ARCHITECTURE.md section "Time rules", A38 |
| DEC-41 | An open-ended unavailability is stored without an end | Proposed | 2026-11-18 (M3-2) | ARCHITECTURE.md section "Persistence rules" |
| DEC-42 | Each rule violation of a rejected placement carries its own text and earliest allowed start | Proposed | 2026-11-18 (M3-4) | API.md section "Error format", AC-179 |
| DEC-43 | R2 is guarded by the resource row lock, not by an exclusion constraint, once planned ends are recomputed | Proposed | 2026-11-18 (after M3-4) | ARCHITECTURE.md section "Persistence rules", AC-83, AC-107, WQ4 |
| DEC-44 | A shift or holiday change recomputes the planned ends through a MasterData write port, in its own transaction | Proposed | 2026-11-18 (M3-5) | ARCHITECTURE.md sections "Module rules" and "Persistence rules", AC-182 |
| DEC-45 | The timeline read takes a required plant-local date range and carries the working periods the engine computes | Proposed | 2026-11-18 (M3-6) | API.md section "Scheduling calls", AC-185 to AC-187, WQ3 |
| DEC-46 | The timeline marks conflicts from the conflict list, which stays on the schedule page | Proposed | 2026-11-18 (M3-6) | wireframes.md section 2, AC-189 to AC-191 |
| DEC-47 | One placement context per snapshot checks any number of requests, and the static rule calls build one each | Proposed | 2026-11-18 (M3-7) | [engine README](../backend/src/TireScheduler.Engine/README.md), AUDIT.md item 7 |
| DEC-48 | The snapshot loader loads a version with a scope of resources and orders, or the whole version, in one method that says which rule reads which rows | Proposed | 2026-11-19 (M3-8) | AUDIT.md item 8 |
| DEC-49 | Scheduling turns the plant calendars into engine calendars in one place, and the timeline reads its working periods from Scheduling | Proposed | 2026-11-19 (M3-9) | ARCHITECTURE.md section "Structure", AUDIT.md item 10 |
| DEC-50 | Reads of a few placed lots ask Orders for those lots' context and MasterData for those machines' names, never for every order or machine | Proposed | 2026-11-19 (M3-10) | AUDIT.md item 9 |
| DEC-51 | The timeline chart draws the timeline it is given; the page that shows it applies its filters once | Proposed | 2026-11-19 (M3-11) | AUDIT.md item 11 |

## Accepted

### DEC-1 Late is a derived flag

- Decision: `isLate` is computed at query time from due date, planned ends and actual ends. It is not an order state.
- Reason: an order can be Scheduled and late or In Progress and late; a state would clash with the lifecycle.
- Alternatives: a Delayed state (original `features.md`), rejected for the clash.

### DEC-2 Routing copied into orders (A1)

- Decision: a routing is fixed per product. Order creation copies stages, dependencies and duration values into operations.
- Reason: later routing edits must not change orders already planned.
- Alternatives: reference the routing live, rejected because history would change under the planner.

### DEC-3 Time handling (A2)

- Decision: minute-precision UTC in storage, one IANA plant time zone for display and calendars, 15-minute grid in the UI only.
- Reason: every planner sees the same wall clock; the model stays independent of the UI grid.
- Alternatives: shift-level slots, rejected as too coarse for curing cycles.

### DEC-4 One plant calendar (A3)

- Decision: v1 has one calendar, Monday to Friday, three 8-hour shifts, plus plant holidays. Shifts are data.
- Reason: enough for the scenario; a 24/7 plant is a data change.
- Alternatives: per-machine calendars in v1, deferred to P1 (F-MD-6). DEC-7 keeps that a data change.

### DEC-5 No running across unavailability (A6)

- Decision: a lot may pause over non-working time but a placement that overlaps an unavailability is rejected.
- Reason: a job interrupted by maintenance is rarely resumable as-is; rejection keeps the rule simple.
- Alternatives: pause across maintenance, may be revisited with per-machine calendars.

### DEC-6 Operation and Lot replace Job

- Decision: an operation is one stage of one order; it holds one or more lots, each placed on its own. v1 creates one lot per operation.
- Reason: curing is the bottleneck and runs on many presses in parallel. One job per stage on one machine cannot express a realistic schedule, and changing it after the scheduling code exists would touch status, R4, `isLate` and the timeline.
- Alternatives: keep Job and add splitting later (F-ORD-7), rejected for the retrofit cost.
- Consequences: manual splitting (F-ORD-7, P1) becomes a UI feature. Every rule works per lot.

### DEC-7 Resources with type, kind and calendar

- Decision: machines are resources. A resource type has a kind (`Machine` in v1); each resource references a calendar (the plant default in v1).
- Reason: molds (F-MAT-3) and per-machine calendars (F-MD-6) become data, not schema changes.
- Alternatives: a Machine entity with a plant-wide shift pattern, rejected for the same retrofit cost.
- Consequences: a lot needing two resources at once (machine and mold) still needs a second placement per lot later (`REQUIREMENTS/BACKLOG.md` section "Extension points for later versions").

### DEC-8 Cycle-based duration

- Decision: `duration = setupMinutes + ceil(quantity / unitsPerCycle) x cycleMinutes`, defaults giving `quantity x cycleMinutes`.
- Reason: multi-cavity curing presses make tires in batches; a per-unit time misstates them.
- Alternatives: minutes per unit only, rejected. Sequence-dependent changeover stays P1 (F-MD-7).

### DEC-9 Unavailability replaces machine status

- Decision: planned maintenance and breakdowns are time-bounded `Unavailability` rows (a breakdown may have no end). Machine status is derived for display. R5 is retired.
- Reason: a stored status duplicated maintenance windows and blocked a machine for all future time while it was down today.
- Alternatives: keep both, rejected as two sources of truth.

### DEC-10 Dependencies with lag (A8, A11)

- Decision: dependencies are (predecessor, successor, minLag) between operations, copied from the routing. Lag is elapsed real time. Either side may be placed first; every placed successor lot waits for every placed predecessor lot.
- Reason: green tires rest before curing, and planners often schedule the curing bottleneck first and work backwards. The original R4 allowed neither.
- Alternatives: finish-to-start in sequence order only (original R4), rejected. Lot-by-lot overlap (transfer batches) deferred.

### DEC-11 Schedule versions (A9, A10)

- Decision: every placement carries a schedule version. v1 has one live version and no scenario UI. Unavailability and progress are shared by all versions.
- Reason: what-if (F-SCH-12) is the business problem's headline question. Adding the version later would change the exclusion constraint, every query and every URL.
- Alternatives: live placements only, rejected for the retrofit cost. Per-version unavailability deferred.

### DEC-12 Derived statuses

- Decision: lot, operation, order and machine statuses are computed, never stored.
- Reason: a stored status cannot be correct for every schedule version (DEC-11) and duplicates placements and progress.
- Alternatives: Orders owns a stored `Job.status` (original architecture), rejected.
- Consequences: statuses live in Queries; commands needing them use ports (DEC-16).

### DEC-13 Assisted scheduling in v1

- Decision: earliest slot (F-SCH-11) and "schedule order ASAP" (F-SCH-16) are P0, in a new milestone M4. P0 is 26 features.
- Reason: placing every lot by hand (~160 per week) is slower than the spreadsheet the tool replaces. The engine already validates placements, so the search is a small addition.
- Alternatives: keep them P1/P2, rejected because v1 would not beat the current way of working.

### DEC-14 CSV import and export in P1

- Decision: F-MD-9 and F-ORD-9 move from P2 to P1 and include export.
- Reason: planners already hold their data in spreadsheets; manual entry is the main barrier to trying the tool.

### DEC-15 Synthetic plant generator

- Decision: P7 builds a deterministic, parameterised generator with presets instead of hand-written seed data.
- Note (2026-11-10): P7 is now task M2-7. Only the task moved; the decision is unchanged.
- Reason: one source for seed data, demos, tests and the F-NFR-5 performance set.
- Alternatives: static seed files, rejected as hard to scale and keep valid.

### DEC-16 Upward reads through ports

- Decision: a lower module declares a small read interface for a fact it needs from a higher module (for example Orders asks "has any lot started?"); the higher module implements it.
- Reason: keeps source dependencies pointing downward despite derived statuses.
- Concern: adds wiring and indirection for a small team.
- Consequences: with DEC-23 the ports are internal interfaces inside the one API project, registered in dependency injection. No cross-project wiring is needed; the architecture test checks that folders depend only downward.

### DEC-17 Version in the URL path

- Decision: version-dependent endpoints sit under `/versions/{v}/...` with a `live` alias; v1 always uses `live`.
- Reason: scenarios need no URL change.
- Alternatives: a `version` query parameter, equally workable but easier to forget.

### DEC-18 Later facts flag placements

- Decision: a new unavailability or calendar change is always saved. Affected placements stay, are recomputed and are flagged as conflicts until the planner acts.
- Reason: a breakdown is a fact and cannot be rejected; deleting or moving placements silently would contradict R7.
- Alternatives: reject the fact, or auto-unplace affected lots, both rejected.

### DEC-19 Proposal, then commit

- Decision: "schedule order ASAP" first returns proposed placements, then commits them in one transaction after the planner confirms. The commit validates again.
- Reason: the planner sees the result before it changes the schedule; the same proposal call serves the promise lookup (F-RPT-6).
- Alternatives: place immediately with undo, rejected because undo needs the audit trail (P1).

### DEC-20 Stack

- Decision: .NET 10 (LTS) with ASP.NET Core Web API and EF Core, PostgreSQL, React with TypeScript.
- Reason: same family as the legacy code, so existing knowledge carries over. .NET 10 is the current long-term support release; .NET 9 (legacy) has a shorter support window.
- Alternatives: stay on .NET 9, rejected for the support window.

### DEC-21 Cookie session

- Decision: ASP.NET Core Identity cookie session, HTTP-only, same origin for API and SPA, anti-forgery token on state-changing requests.
- Reason: one SPA on one origin; cookies keep no token in browser storage and sessions can be revoked on the server.
- Alternatives: JWT bearer tokens (legacy), rejected because no other client or origin needs them and revocation is harder.

### DEC-22 Due date semantics

- Decision: a due date is a plant-local calendar date. An order is late if its last lot ends after the end of that local day (midnight plant time).
- Reason: planners and customers talk in dates, not times; the plant time zone (DEC-3) makes the end of day unambiguous.
- Alternatives: end of the last shift that day, rejected because it couples lateness to the calendar; an exact timestamp per order, rejected as more input for no gain in v1.

### DEC-23 Module granularity

- Decision: the scheduling engine is its own project with no references to modules or the database. Auth, MasterData, Orders, Scheduling, Progress and Queries are folders in one API project, each with its own database schema.
- Reason: one project per module adds build and wiring cost for a small team. Boundaries are kept by an architecture test on namespaces instead of by project references.
- Alternatives: one project per module, rejected for the overhead; a module can still be moved into its own project later because the folder boundaries are already enforced.
- Consequences: module boundaries are a convention checked by tests, not by the compiler, so the architecture test (`TESTING.md` section "Test levels") must run on every commit.

## Proposed

Made while building M1 (2026-11-10) and M2 (2026-11-18) and already followed by the code. Waiting for review; on approval the status becomes Accepted and the entry moves up.

### DEC-24 Migrations at startup

- Decision: the API applies pending EF Core migrations when it starts, one module after the other, each with its own history table in its own schema. Startup steps (migrations, then the first Admin) finish before the first request is taken; a failing step stops startup.
- Reason: one API instance and one database (`ARCHITECTURE.md` section "Module rules"), so nothing else can run at the same time; AC-8 and AC-40 already expect the database to be ready after a start.
- Alternatives: a separate migrate command run by the deployment, the better choice once several instances start together. The steps are behind one interface (`IStartupStep`), so moving them into a command later is a small change.
- Consequences: the readiness check cannot report an unreachable database at start, because the API does not start without it.

### DEC-25 Minimal API endpoints and problem exceptions

- Decision: endpoints are minimal API route groups, one endpoints file per area of a module, not MVC controllers. Request bodies are checked by an endpoint filter over their data annotations. Module code rejects a request by throwing `ApiProblemException` with status, `code` and `detail`; one exception handler writes every problem details answer.
- Reason: an unreadable body raises an exception in minimal APIs, which separates `request.malformed` from `validation.failed` cleanly (AC-21, AC-22); MVC reports both through the same model state. Throwing ends the request, so a transaction rolls back and nothing is saved (AC-17).
- Alternatives: MVC controllers as first written in the module rules of the first architecture draft, rejected for the reason above; result types instead of exceptions, rejected because every caller up the chain would have to pass them on.
- Consequences: the filter checks the body's own properties and, since M2-1, nested objects and lists (routing stages), naming a nested field by its path (A24).

### DEC-26 Session end and lockout

- Decision: every request compares the security stamp in the session cookie with the user's row. Sign-out writes a new stamp, which ends every session of that user (A21). Failed sign-ins are counted in our own code on the injected clock, using the lockout columns of the Identity user.
- Reason: a cookie alone stays valid until it expires, which AC-5 does not allow. ASP.NET Core Identity's own lockout reads the system clock, which `ARCHITECTURE.md` section "Time rules" forbids and which made AC-3 untestable.
- Alternatives: a server-side session table, which would end one session at a time but adds a table and cleanup; Identity's `SignInManager`, rejected for the clock.
- Consequences: one user lookup per request, acceptable for 2-5 planners. A role change or deactivation (F-AUTH-3) must write a new stamp to take effect at once.

### DEC-27 OpenAPI document in the repository

- Decision: the OpenAPI document is committed as `backend/openapi/tire-scheduler-v1.json`. A backend test fails when it differs from what the code describes and rewrites it when run with `UPDATE_OPENAPI=1`. The frontend generates its request and response types from the file (`npm run generate:api`, `@hey-api/openapi-ts`) and calls the API through one hand-written fetch wrapper.
- Reason: API changes show up in review as a diff, and the frontend build needs no running backend. Only the types are generated because the generator's runtime client does not pass the project's strict TypeScript settings.
- Alternatives: generating the document at build time, rejected because it starts the whole application, including the database steps of DEC-24; `openapi-typescript`, rejected because it does not support TypeScript 6 yet.

### DEC-28 Lot duration in the engine

- Decision: the duration formula of `domain-model.md` section 4 lives in the engine (`LotDuration`). The Orders module calls it when it generates lots, so Orders may depend on the engine, as Scheduling does.
- Reason: AC-63 and AC-64 are engine criteria, and the engine property tests and the pure core of the plant generator (task M2-7) need the same formula without a database. The engine depends on nothing, so the new dependency points downward and cannot form a cycle.
- Alternatives: the formula inside the Orders module, rejected because the generator core and the engine tests would then need a second copy.
- Consequences: the parts table in `ARCHITECTURE.md` section "Structure" lists the engine for Orders, and `ModuleDependencyTests` checks which modules may use the engine.

### DEC-29 Order reads in Queries

- Decision: `GET /orders` and `GET /orders/{id}` are served by Queries, which adds the derived status and `isLate` to what the Orders module reads. The order writes stay in Orders and answer without the order: create answers 201 with the order's `Location` and its `id`, change and cancel answer 204. The client reads the order again after a write.
- Reason: status and `isLate` live in Queries (DEC-12), and Orders cannot call Queries, so a write answer from Orders cannot carry them. One shape per resource keeps the client from caching an order without its status. The status filter and sort of the order list (task M2-5) need the status before paging, which only Queries has.
- Alternatives: Orders declares a port that Queries implements and keeps serving the reads, rejected because Orders would ask Queries, which asks Orders back, and the status filter and sort would live in Orders.
- Consequences: one more request after each order write. The Orders module keeps a public read interface (`IOrderReader`) for Queries. The order reads stay at `/orders` although status and `isLate` depend on the schedule version: they always show the live version, because v1 has no other (DEC-17). Scenarios (F-SCH-12, P2) add `/versions/{v}/orders` for the other versions.

### DEC-30 Late flag in the engine

- Decision: the late rule of `domain-model.md` section 6 lives in the engine (`OrderLateness`), which turns the plant-local due date into the UTC instant it ends. Queries calls it with the due date, the plant time zone, now and the order's lot ends, so Queries may depend on the engine.
- Reason: only the engine converts between plant-local time and UTC (`ARCHITECTURE.md` section "Time rules"), and AC-55 to AC-57 are engine criteria. The proposal review (WQ2) and the dashboard reuse the same rule. The engine depends on nothing, so the dependency points downward.
- Alternatives: the rule inside Queries with its own time zone conversion, rejected because a second conversion could disagree with the engine on DST days.
- Consequences: the parts table in `ARCHITECTURE.md` section "Structure" lists the engine for Queries.

### DEC-31 Order list filters: stored in the database, derived in Queries

- Decision: the Orders module applies the search, the filters on stored fields (customer, due date range, priority) and the sort in the database. Without a status or late filter it also pages there, and Queries derives the status and `isLate` for that page only. With a status or late filter, Orders returns every order that matches the other filters, already sorted, and Queries derives, filters, counts and pages them.
- Reason: status and `isLate` need placements, progress and the engine (DEC-12, DEC-30), which the database query of one module cannot reach without reading other schemas. The default list, the most common request, stays one page in the database.
- Alternatives: a stored status column, rejected by DEC-12; a database view across the module schemas, rejected because modules do not read each other's tables; always deriving every order in Queries, simpler, rejected because the cost of the default list would grow with the order history.
- Concern: a status or late filter reads every matching order, so its cost grows with the order history (A28).
- Consequences: the placement and progress ports that M3-4 and M5-1 add to the order reads take a set of orders, so one call serves a whole list. The paging limits (AC-65) are checked in Queries, which pages in both cases.


### DEC-32 Plant generator: pure core and HTTP loader

- Decision: the generator core is its own library, `TireScheduler.PlantGenerator`, which turns parameters, a seed and a start date into plain values and depends only on the engine (for `LotDuration`, DEC-28). The loader, `TireScheduler.PlantLoader`, is a console program that signs in as an Admin and writes those values through `/api/v1`, the way the frontend does.
- Reason: every generated row then passes exactly the validation of user input, which is split between the request filters and the module registers, and the API gets no second mode that could run on startup. The engine property tests (AC-115, AC-116, AC-127) use the core without a database.
- Alternatives: a command inside the API that calls the module registers, rejected because it would skip or copy the request filters and add a second mode to the API program; raw SQL, rejected by the task.
- Consequences: loading needs a running API and an Admin's sign-in. The loader keeps its own small request shapes, and its API test fails when they drift from the API. Each new write the loader needs (unavailabilities in task M3-2, a placed schedule in task M4-5) uses that task's endpoint.

### DEC-33 One transaction per request, shared by the module contexts

- Decision: every module context of a request runs on one database connection, created once per request. A command begins the request's transaction through `RequestTransaction` (`backend/src/TireScheduler.Api/Database`), and a module it calls through a public interface or a port joins that transaction, so the rows of every module commit or roll back together (AC-161). Rows are locked with `LockRowAsync`, which takes the schema, table and key column from the EF model and works only inside a transaction (AC-162). This settles the two cross-module commands of task M3-4. A placement command begins the transaction in Scheduling and calls a lock method on the public interfaces of MasterData (the resource) and Orders (the order); each joins and locks its own row. Cancelling an order begins the transaction in Orders and calls one write port, `ILotPlacements.RemoveUnstartedAsync`, which Scheduling implements by joining and removing the placements (AC-50, until now an open question).
- Reason: the placement command must lock rows of two other modules and write placements in one transaction, and the cancel must remove placements with the order change, but no module may read or write another module's tables (`ARCHITECTURE.md` section "Module rules"). On one connection, each module works only on its own tables inside the same transaction (`UseTransaction`). Table and column names read from the model cannot go stale after a migration, as the hand-written lock statements could.
- Alternatives: ambient transactions (`TransactionScope`), rejected because which connection takes part depends on when it opens, which the code does not show; one context over every schema, rejected because each module could then reach the others' tables; one transaction per module with an undo step on failure, rejected because it is not atomic and adds code.
- Concern: the write port is the one exception to the read-only ports of DEC-16, which holds for every other port.
- Consequences: a request has one connection, so it cannot run two database queries at the same time. A request has at most one open transaction: beginning a second one, or joining when none is open, fails as an unexpected error, and a transaction still open when the request ends is rolled back. The lock methods and the write port are added by task M3-4, which tests AC-50 and AC-161 with them.

### DEC-34 Flat order read models with named required properties

- Decision: the order, operation and lot answers keep their flat shape, and Queries copies the stored fields from the Orders contracts into them. Every record in the API project, in that chain and in every other module, uses named `required` properties instead of positional parameters, and each pair of types in the order chain has one mapping function.
- Reason: positional records with runs of fields of the same type (quantity, setup, cycle, units per cycle) compile when two values are swapped. With `required` properties a swap cannot happen, and a new field on an answer does not compile until every place that builds it sets the field. The flat shape needs no change to the API, the generated types or the frontend.
- Alternatives: nesting the stored part in the answer (`{ order: {...}, status, isLate }`), which would make Queries copy no fields, rejected because the lots would need the same nesting inside the operations, the frontend would read two levels deep, and the API, the generated types and both order pages would change.
- Concern: a new stored field still has to be added to each record of its chain and its mapping, about four places for an order field; the compiler names them.
- Consequences: AC-71 and the later status inputs add their fields as `required` properties of `LotResponse` and the other answers. Setting a property is a few more lines than a positional argument. Records outside the API project (the engine, the plant generator and the loader) are not covered by this decision.

### DEC-35 Request bodies checked once for the API group

- Decision: `Program.cs` creates one `/api/v1` route group with `ValidateRequestBodies` and passes it to every module, which maps its endpoints inside it. The group adds the body check of DEC-25 to each of its endpoints, nested groups included: for an endpoint that reads a body, the check finds the body among the handler's parameters and runs before the handler (AC-163). Endpoints have no validation step of their own. An API test lists every endpoint that reads a body and fails when one is not checked, for example because it was mapped outside the group.
- Reason: required request fields are nullable with `[Required]` and module code reads them with `Validated()`, which is safe only after the check has run. With a step per endpoint, a forgotten step turned a missing field into a 500 instead of a 400, and only a test that sent that exact field would notice. M3 to M5 add many write endpoints.
- Alternatives: keeping the step per endpoint with an architecture test that finds endpoints without it, rejected because every endpoint would still repeat the body type; validating inside each register, rejected because module code would repeat the check and could forget it the same way.
- Consequences: the check reads the body type that the framework records for the endpoint, so it covers exactly the endpoints the OpenAPI document shows with a request body. A module maps into the group it is given and never creates its own `/api/v1` group. The health endpoints stay outside the group; they read no body.

### DEC-36 One cache refresh after every write

- Decision: every query key is defined in `frontend/src/api/queryKeys.ts`. After any successful write, the `MutationCache` `onSuccess` in `frontend/src/api/queryClient.ts` marks every cached query stale except the signed-in user and reads again the ones on screen, before the write's own follow-up (closing a form, moving to another page) runs (AC-164). Forms and pages refresh nothing themselves. A write that deletes a row names that row's query in `meta.deletedQueryKey`, and the query is dropped instead of read again; sign out sets `meta.skipsQueryRefresh`, because the session is gone and it clears the cache itself.
- Reason: a write changes data that other areas show. A renamed product is shown in orders, a renamed machine type in routings and order operations, and from M3 a placement changes the timeline, the order status and the dashboard. With a refresh list in each form, every new read had to be added to the lists of every write that affects it, and a missing entry showed old values with no error. For 2 to 5 planners the extra reads are few: only the queries on screen are read at once.
- Alternatives: a table from each write to the areas it affects, rejected because each new write and each new read must still be added to it, and a missed entry fails silently in the same way; keeping a refresh list per form (before M2-11), rejected for the same reason spread over many files; server push of changes, out of scope for v1.
- Consequences: a write reads again every query on screen, also those it did not change. Changes by other users are still seen only on the next read. The routing editor no longer puts the saved routing into the cache itself; it is read again like every other query.

### DEC-37 Endpoint functions generated by a project script

- Decision: `npm run generate:api` runs `@hey-api/openapi-ts` for the types (DEC-27) and then `frontend/scripts/generate-endpoints.ts`, which writes one function per operation of the OpenAPI document to `frontend/src/api/generated/endpoints.gen.ts`. Each function calls the hand-written fetch wrapper `requestApi` with the operation's method, URL and response type, and checks its data with `satisfies` against the generated `...Data` type (AC-165). The script stops with an error on an operation it cannot describe. A test fails when the committed file differs from what the script writes from the document.
- Reason: with a hand-written list, the URL and data were checked against the document but the method and the response type were chosen by hand, so a wrong method failed only at run time and a wrong response type made the UI trust a shape the server does not send. M3 roughly doubles the endpoints.
- Alternatives: the `@hey-api/sdk` output on the generator's fetch client, rejected for the DEC-27 reason (its runtime client does not pass the strict TypeScript settings), because it adds about 2,000 lines of generated runtime code, and because the anti-forgery token and the problem details errors of `request.ts` would have to be rebuilt as its interceptors; keeping the hand-written list (before M2-12), rejected for the reason above.
- Consequences: function names and argument order follow the script's rule (path values in URL order, then the body, then the query), so a call site changes when an operation is renamed. The script supports only what the API uses today and is extended when an operation needs more, for example an optional body.

### DEC-38 Shared API test data builders

- Decision: the API tests build their rows through static builders in `backend/tests/TireScheduler.Api.Tests/TestData`, one class per module (`MasterDataTestData`, `OrderTestData`) with one method per kind of row and the paths of its routes. Test classes keep only the readers and assertions of their own area and are never called by other test classes. A module added from M3 on gets its own class there. Requests stay anonymous objects and answers stay `JsonElement`.
- Reason: the builders lived in test classes (`MachineRegisterTests` was called 25 times by other classes) or were copied: products and routings were built by three classes each. Changing one test class could break unrelated tests, and the M3 and M4 tests need whole plants.
- Alternatives: requests and answers as the API's contract records, rejected because a test then follows a renamed property instead of failing, while the tests should see the JSON the frontend sees; a builder object for whole plants now, deferred until the first test needs a whole plant (M3), when writing the generator's `small` preset through the loader's `PlantWriter` is the first option to check.
- Consequences: a builder sends every field it has a value for, so a test of a default (for example AC-34) writes its request out in full.

### DEC-39 Machine reads in Queries

- Decision: `GET /resources` and `GET /resources/{id}` are served by Queries, which adds the derived `status` (`domain-model.md` section 6) to the machine fields that MasterData reads through `IMasterDataReader`, and the unavailabilities covering now that Scheduling reads through `IUnavailabilityReader`. The machine writes stay in MasterData, keep their URLs and keep answering the machine without a status (AC-28). The unavailabilities are written and read by Scheduling under `/resources/{id}/unavailabilities`.
- Reason: statuses are derived in Queries (DEC-12), and MasterData, the lowest module, cannot ask Scheduling or Progress for what the status needs. It is the order pattern of DEC-29, so the machine register keeps one read per page, and the URLs and operation names stay the same for the frontend and the loader.
- Alternatives: a separate status list (`GET /resource-statuses`) that the pages match to the machines by id, rejected because every machine page would need two reads and API.md says there is no machine status endpoint; changing the machine writes to answer without the machine, as the order writes do, rejected as an API change with no gain, because the frontend reads the machine again after every write anyway (DEC-36).
- Consequences: the machine read answer (`ResourceViewResponse`) and the write answer (`ResourceResponse`) are two shapes, the read one with `status`. Running (task M5-1) adds the lots in progress to the same derivation.

### DEC-40 Plant time conversion in the UI

- Decision: the UI shows every UTC time on the plant's wall clock, as it already did, and converts a plant-local time a planner types to the UTC instant it sends, with `Intl.DateTimeFormat` in the plant time zone that the calendar answer carries. A local time that occurs twice is read as its first occurrence and one inside a DST gap as the first time after the gap (A38). On the server only the engine converts.
- Reason: the API sends and receives UTC (`API.md` section "Conventions"), and the planner thinks in plant time. The unavailability form (task M3-2) is the first input of a time of day with a date; the placement form (task M3-4) is the next.
- Alternatives: requests with plant-local times that the server converts through the engine, rejected because the API would then take local times and answer UTC ones; a date library such as `date-fns-tz`, rejected because a few lines on `Intl` do it without a package; the `Temporal` API, rejected because Node 24, which runs the frontend tests, does not have it.
- Concern: the reading of an ambiguous local time is written twice, in the engine (A27) and in the UI. The UI tests check the same DST cases as the engine tests.

### DEC-41 Open-ended unavailability without an end

- Decision: an unavailability is stored with a start and a nullable end; a breakdown with no known end has none. A check constraint allows a missing end only for a breakdown and requires the end after the start.
- Reason: the API, the engine snapshot (`SnapshotUnavailability.End`) and the generator already describe an open end as a missing value, and no exclusion constraint covers unavailabilities (R3 spans tables), so a range column with an infinite upper bound, as the persistence rules first said, would only add a conversion at each end.
- Alternatives: a `tstzrange` column with an infinite upper bound, rejected for the reason above; an end of `infinity`, rejected because every reader would have to turn it back into a missing end.
- Consequences: a query for what covers a time reads a missing end as "still going on" (`EndsAt == null || EndsAt > time`).

### DEC-42 Rule violations with their own text

- Decision: every entry of `violations` in a rejected placement or removal has a `detail` text written by the Scheduling module for its rule code, and `earliestAllowedStart` in UTC when a placed lot of an earlier stage holds the start back (AC-91). The problem's own `detail` is the first violation's text. The placement form lists the entries as they come and shows the earliest allowed start on the plant's wall clock.
- Reason: the form must say what each broken rule means, and the UI holds no rule knowledge (`ARCHITECTURE.md` section "Frontend rules"). A time in the text would be UTC or need the server to convert to plant time outside the engine, so the time stays a field that the UI converts for display (DEC-40).
- Alternatives: the UI maps each code to a text, rejected because a new or changed rule would then need a frontend change; texts that name the conflicting order, lot and times, as the wireframe sketches, deferred because they need reads of Orders and MasterData for every rejection, and the timeline (task M3-6) can show the conflicting item by its id.
- Consequences: a new rule code needs its text in `RuleViolationProblems`, which fails for an unknown code. The links from a violation to its conflicting item come with the timeline (task M3-6).

### DEC-43 R2 guarded by the resource lock

- Decision: task M3-5 drops the exclusion constraint `EX_Placements_NoOverlap` with a migration. From then on R2 is kept by the engine's check under the resource row lock, which every command that writes a placement takes before it builds its snapshot (DEC-33). A command that writes placements on several resources, such as the commit of a proposal (task M4-4), locks them in ascending id order, so two such commands never wait on each other. This settles WQ4.
- Reason: a later fact keeps every placement and stores its recomputed planned end even when that end now overlaps another placement, which `FindConflicts` then lists (DEC-18, AC-107). The constraint would refuse to store that end. The lock already makes two placements on one resource run one after the other: the concurrent test of AC-83 is answered by the engine's R2 check, not by the constraint.
- Alternatives: keep the constraint on a booked range that is fixed when the lot is placed and never recomputed, rejected because the database and the engine would then check different times: a booked range longer than the planned one refuses a placement the engine accepted, answered as an unexpected error, and a shorter one guards nothing; every move would also have to keep both in step. Keep the constraint and refuse the later fact instead, rejected because a breakdown or holiday is true whatever the schedule says (DEC-18).
- Concern: the database no longer guards R2 by itself, so a new command that writes placements without the resource lock could double-book a resource. Placements are written only by the Scheduling module; a command added there must take the lock.
- Consequences: task M3-5 changes AC-83 so that the resource lock, not the constraint, is what keeps the database from holding both placements, retires the test that writes overlapping rows directly, keeps the concurrent test, and updates `ARCHITECTURE.md` section "Persistence rules". Until then the constraint stays in place.

### DEC-44 Planned ends recomputed through a write port

- Decision: MasterData declares the port `IPlannedEndRecompute`, which Scheduling implements. Replacing a calendar's shifts and adding or deleting a holiday lock their rows as before, then lock every resource row in ascending id order, save the change, and call the port in the same transaction (DEC-33). The port recomputes the planned end of every placement whose lot has not completed, in every version, with the engine's `PlacementRules.RecomputeEnds` (AC-182, A42).
- Reason: the planned end is stored (A2), and the order reads, the late flag and `FindConflicts` read it, so it must change with the fact that changes it. Locking every resource first keeps the resource lock of DEC-43 true for these writes too: a placement command that holds a resource lock finishes first and its placement is recomputed, and one that comes later waits and sees the new working time. Placement commands lock one resource and then an order and never a calendar, and these saves lock resources in ascending order, so no two commands wait on each other.
- Alternatives: recompute on read, before `/conflicts` and before each placement command, rejected because a read would then write rows and the order reads and late flag would show stale ends until such a read ran; Scheduling maps the shift and holiday writes and calls MasterData, rejected because MasterData would then not own the writes of its own rows.
- Concern: a second write port, next to the one of DEC-33, where DEC-16 keeps ports read-only. A shift or holiday save now waits for running placement commands and rewrites every placement. A machine created while such a save runs is not locked by it, so a placement on it in that moment keeps an end from the old working time until the next change; with 2-5 planners and machines created rarely this is accepted.
- Consequences: a new kind of change to working time, such as per-machine calendars (P1), calls the port the same way.

### DEC-45 Timeline read with a plant-local date range

- Decision: `GET /versions/{v}/timeline?from=&to=` takes two required plant-local dates, both days included, at most 31 days apart (A44). It answers the range as the UTC instants where `from` begins and `to` ends, and the resource types with their machines, each with its derived status, working periods, unavailabilities and placements in the range (AC-185). The working periods come from the engine's `WorkingPeriods.Between`, merged where they touch. The UI picks the range; there is no default on the server. This settles WQ3.
- Reason: the page must know the range to show it and to step it, and [Today] needs the current plant week in the UI anyway, so a server default would be a second place that decides the same range. On the server only the engine reads shift times (`ARCHITECTURE.md` section "Time rules"), so the UI shades non-working time without knowing the shifts or the R6 reading of a DST day.
- Alternatives: `from` and `to` optional with a two-week default on the server, as WQ3 proposed, rejected for the reason above; UTC instants as parameters, rejected because the range is made of plant days, and the server then reads the day boundaries the way the due date does (A27); shading from the calendar answer in the UI, rejected because the UI would then repeat the working-time rule.
- Consequences: the answer grows with machines and days, about 800 working periods at 25 machines and 31 days. A per-machine calendar (F-MD-6, P1) needs no change, because each machine carries its own periods.

### DEC-46 Conflict markers from the conflict list

- Decision: the timeline answer carries no violations. The schedule page marks the bars whose placement id the conflict list (`GET /versions/{v}/conflicts`, AC-183) holds, and the popover of such a bar shows that entry's violations. The conflict list stays on the schedule page, below the timeline. A violation's [Show ...] link is resolved from what the timeline holds (AC-191). The popover has no removal of its own: [Move...] opens the placement form, which offers [Remove placement].
- Reason: `FindConflicts` needs the snapshot of the whole version, which the conflict read already builds, so the timeline read stays a plain read of stored rows. The list is the only place that shows a conflict outside the shown range, and it keeps [Move...] one click away. A violation names only the type and id of its item, and the timeline already holds the placements, lots and unavailabilities with their machine, so the links need no new read. One removal path keeps one display of a rejected removal (R8).
- Alternatives: violations on each timeline placement, rejected because each timeline read would build the version's snapshot again; dropping the list once the bars are marked, rejected because a conflict outside the range would then be invisible; links that read their item when it is not on the timeline, deferred until planners ask for them.
- Concern: the page combines two answers, read at slightly different times; after a write both are read again (DEC-36), so they differ only while one of them loads.
- Consequences: an item that is not on the shown timeline has no link, so the conflict list links only the items of the shown range. The dashboard (task M5-3) can reuse the conflict list.

### DEC-47 One placement context checks many requests

- Decision: the rules R1 to R4, R6 and R8 live in the engine's public `PlacementContext`, built once from a `ScheduleSnapshot`, which indexes the snapshot in its constructor and keeps no state between calls. `Validate(request)` checks one request and `EndAfter(resource, start, duration)` gives a planned end. Each static `PlacementRules` method builds a context for its one call. A search, such as `EarliestSlot` (task M4-1), checks its candidates with one context's `Validate` and never copies a rule.
- Reason: building the index looks up the time zone and groups every row of the snapshot, so a search that called `PlacementRules.Validate` per candidate would repeat that for every start, and a copy of the rules inside the search would let the M4-1 property tests compare two versions of the rules (AUDIT.md item 7).
- Alternatives: drop the static methods so every caller builds a context, rejected because each one-off call in the API would then repeat the construction for no gain; make `SnapshotIndex` and the violation list public, rejected because callers would then put the rules together themselves.
- Consequences: a check that must count earlier proposals as placed (`ScheduleOrderAsap`, task M4-3) builds its context from a snapshot that holds them.

### DEC-48 Snapshot loaded from one scope

- Decision: `ScheduleSnapshotLoader` (Scheduling) builds every snapshot in one private load method that takes a version and either a scope of resource ids with the scheduling facts of their orders, or no scope for the whole version. A scope loads the placements on those resources or of those orders' lots, the unavailabilities of those resources, and the lots, dependencies and progress of those orders; the whole version loads every placement and unavailability and takes its orders from the same placement rows, so every placed lot is in the snapshot. Every resource and calendar is always loaded. The comment on the load method says which rule reads which rows. `ForOrderOnResourceAsync` and `ForVersionAsync` are one-line wrappers that name the scope.
- Reason: each caller had its own loading query, with the placement and unavailability projections copied and the rows each rule needs written only in a comment on one of them. The earliest-slot search and schedule-whole-order (M4) look at several resources of a type, and a scope that leaves out a row a rule reads fails quietly, because the rule does not see that row (AUDIT.md item 8).
- Alternatives: order ids in the scope instead of their facts, rejected because a placement command has already read the facts under the order lock and would read them twice; the whole version as a scope of every resource with the orders read first, rejected because the placements would be read twice and a lot placed between the two reads would be in the snapshot without its lot, which the engine rejects; a public load method that takes any scope, rejected because a scope with a few resources holds placements of other orders' lots without those lots, so it suits only checks of its own orders' lots and must not reach `FindConflicts` or `RecomputeEnds`.
- Consequences: a new kind of check adds a wrapper next to the two existing ones and never writes a loading query; for example the earliest-slot search adds one for the order's lot on every resource of its type.

### DEC-49 Engine calendars built in one place

- Decision: `EngineWorkingTimeLoader` (Scheduling) is the only code that turns the calendars, shifts and holidays of `IMasterDataReader.GetWorkingTimeAsync` into the engine's `SnapshotCalendar` values, with the plant time zone. `ScheduleSnapshotLoader` builds every snapshot with it, and the public read port `IWorkingPeriodReader` (Scheduling) answers the working periods of every calendar in a range through the engine's `WorkingPeriods.Between`. The timeline (Queries) asks that port for the working periods instead of building calendars itself, and keeps only the mapping to its answer. The placement values and the unavailability answer are each projected by one selector in Scheduling.
- Reason: the snapshot and the timeline built engine calendars the same way in two layers, so a new shift field or the M4 search would need both copies changed together, and a missed copy would make the timeline's shading disagree with R6 (AUDIT.md item 10).
- Alternatives: the mapping in MasterData next to `PlantWorkingTime`, as the audit proposed, rejected because MasterData may not use the engine (ARCHITECTURE.md section "Structure") and would then need a new row in the dependency table; a shared static mapping that Queries calls, rejected because the timeline would still build engine inputs and load the working time itself.
- Consequences: Queries uses the engine only for the late flag (DEC-30) and the engine's working period type in the port's answer. A per-machine calendar (F-MD-6, P1) changes only the loader and the port.

### DEC-50 Lot context and machine names read for the given ids

- Decision: `IOrderReader.FindLotContextsAsync(lotIds)` answers the order, stage and lot of each given lot as a `LotContext`, loading only the orders that hold those lots, and `IMasterDataReader.GetResourceNamesAsync(ids)` answers the names of the given machines, active or not. The timeline and the conflicted placements read through both, and the order detail reads its machine names through the second. `ListAllDetailsAsync` stays only for the views derived from every lot, such as the unscheduled queue (A39).
- Reason: each of those readers loaded every order with its operations, lots and dependencies, or the full machine list, to describe a handful of placements, and built the same lot lookup by hand. The proposal review (task M4-4) and the dashboard (task M5-3) need the same lookups and would have copied the block again (AUDIT.md item 9).
- Alternatives: a lookup over `ListAllDetailsAsync` in one shared Queries helper, rejected because it removes the copies but still loads whole tables on every read; a `LotContext` holding the whole order detail, rejected because no reader needs the other stages or the dependencies.
- Consequences: a lot that is missing from the answer means its order was deleted, so a reader that expects every lot to be found says why in a comment, as the timeline and the conflict list do.

### DEC-51 Timeline chart draws what it is given

- Decision: `TimelineChart` (`frontend/src/schedule`) draws every resource type, machine and bar of the `TimelineResponse` it is given and takes no filter state; it marks the conflicted placement ids and the selected bar it is passed. The schedule page applies the resource type and conflicted-only filters once, to the loaded timeline, and opens the popover only on a bar of that filtered timeline.
- Reason: the chart took the page's filters and repeated the filter rule that the page used for the popover, so a drift between the two would draw a bar without a popover or keep a popover for a hidden bar, and the proposal preview (task M4-4) would have had to pass filter props it does not have (AUDIT.md item 11).
- Alternatives: a filter helper shared by the page and the chart, rejected because the chart would still take filter props a preview does not need; filtering in the API, rejected because the filters change no data and the page switches them without a new read.
- Consequences: a new screen that shows a timeline, such as the proposal preview, passes the `TimelineResponse` it wants drawn. A conflict link still looks up items on the unfiltered timeline, so it can clear the filters and open a bar they hid (AC-191).

## Open

The only list of open questions. BQ questions wait for the business; until it answers, the docs follow the assumption named. WQ questions were found while sketching the wireframes or building the tasks. Each must be answered before the task in "Needed by"; record the answer in the row (or as a new decision) and update the documents it affects.

| ID | Question | Current proposal | Needed by |
|---|---|---|---|
| BQ1 | Does the plant's time zone observe DST? | Assume yes (A2); the engine handles DST either way, so an answer needs no change (task M3-1) | Before go-live; no code change |
| BQ2 | Does the real plant run weekends? | Assume Monday to Friday (A3). If not, the generator presets get seven working days instead of five (A30) | Before go-live; preset values only |
| BQ3 | Which stages need a dependency lag, and how long (for example green tire rest before curing)? | None yet. Routings default to no lag (F-MD-3); lags are routing data, so an answer needs no code change. The generator presets use synthetic lags (A30) | Before go-live; preset values only |
| BQ4 | How many cavities do the curing presses have (`unitsPerCycle`), and does it differ between presses? | `unitsPerCycle` is set per routing stage, so all presses of a type share it. Different rates per press need machine-specific rates (F-MD-8, P1). The generator presets assume two cavities (A30) | Before go-live; preset values only |
| BQ5 | Does planned maintenance during non-working time block a lot that pauses across it? | Assume yes, overlap is checked over the whole span (A16). A "no" changes only the R3 check in the engine's `PlacementRules` | Before go-live; one engine rule |
| WQ1 | The placement form cannot show the planned end before saving, because no endpoint validates without saving | Settled in task M3-4: the form shows "Calculated when saved" in v1 and shows the server's `plannedEnd` after the save (AC-180). If planners need a preview, add `GET .../placement/preview` returning `Validate` output | M3-4 |
| WQ4 | The exclusion constraint on placements (R2, `ARCHITECTURE.md` section "Persistence rules") rejects a stored `plannedEnd` that overlaps another placement on the machine, but AC-107 saves a recomputed end that may do exactly that and lets `FindConflicts` list it | Settled after task M3-4 by DEC-43: task M3-5 drops the constraint and R2 relies on the resource row lock that every placement takes | M3-5 |
| WQ2 | The proposal review shows whether the order would be late, but the proposal response has no lateness field, and the UI must not compute rules | Add `finishesAt` and `wouldBeLate` to the proposal response, computed in Queries with the same `isLate` rule | M4-4 |
| WQ3 | The timeline needs a time range, but `/versions/{v}/timeline` has no range parameters yet | Settled in task M3-6 by DEC-45: `from` and `to` are required plant-local dates, at most 31 days apart, and the UI picks the default two weeks from the Monday of the current plant week | M3-6 |
