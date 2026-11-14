# Architecture

Status: settled in task P4 (2026-11-06). Covers v1 (P0). Source: [domain-model.md](REQUIREMENTS/domain-model.md), [features.md](REQUIREMENTS/features.md). Decisions are recorded in [decisions.md](REQUIREMENTS/decisions.md).

## 1. Style and stack

- Modular monolith: one deployable API, one single-page app, one PostgreSQL database. 2-5 planners and 15-25 machines do not justify services.
- Stack (DEC-20): .NET 10 (LTS) with ASP.NET Core Web API and EF Core, PostgreSQL, React with TypeScript.
- Modules mirror the domain model: Auth, MasterData, Orders, Scheduling, Progress. Each is a folder (and namespace) in one API project, with a public interface and internal implementation (DEC-23).
- The scheduling engine is a separate project with no dependencies on the modules or the database (section 3).
- A Queries folder holds read-only views that span modules (section 2).
- Each module owns its tables, one database schema per module. No module reads another module's tables.

## 2. Module boundaries

| Module | Owns | Depends on |
|---|---|---|
| Auth | User, roles | none |
| MasterData | ResourceType, Resource, Calendar, CalendarShift, Holiday, Product, RoutingStage, RoutingDependency, plant time zone setting | none |
| Orders | Order, Operation, OperationDependency, Lot | MasterData (routing at order creation) |
| Scheduling | ScheduleVersion, Placement, Unavailability; calls the engine | MasterData (resources, calendars), Orders (lots, dependencies), Engine |
| Progress | LotProgress | Orders, Scheduling (a lot must be placed in the live version before it starts) |
| Queries | read models only, including all derived statuses and `isLate` | all modules, read-only |

Rules:

- Dependencies point downward in the table. No cycles. An architecture test enforces this (section 9).
- Statuses are derived, not stored (`domain-model.md` section 6). Lot, operation and order status need placements (Scheduling) and progress (Progress), so they are computed in Queries. No module writes another module's status.
- Reading upward through ports. A few commands need a fact from a module above them:
  - Orders, to cancel or edit an order, needs "has any lot started?" (Progress) and "is any lot placed in the live version?" (Scheduling).
  - Scheduling, for R8, needs "is this lot completed?" (Progress).

  The lower module declares a small read interface (for example `ILotActivity` in Orders); the higher module implements it and registers it in dependency injection. Source-code dependencies still point downward. Keep these ports few and read-only.
- Each module registers its services with one extension method and maps its own endpoints with another. Endpoints are minimal API route groups (DEC-25).
- Three folders beside the modules hold what every module uses and depend on no module: `Errors` (problem details, request validation filter, section 7), `Security` (roles, access policies, anti-forgery check, section 6) and `Database` (connection, module contexts, startup steps, section 8).

## 3. Scheduling engine

A pure library: inputs are plain values, outputs are results or rule violations. No database, no clock, no module types. Built in task M3-1; its expected behavior, including the worked examples, is in [acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md) sections 4 and 5.

Inputs (a snapshot for one schedule version):

- Calendars with their shifts, plant holidays, plant time zone id.
- Resources: id, type, calendar, isActive.
- Unavailabilities: resource, start, end (open end allowed), kind.
- Placements of the version: lot, resource, start, end.
- Lots: id, operation, duration, resource type, started flag, completed flag, order cancelled flag.
- Operation dependencies with lag.

Operations:

| Operation | Result |
|---|---|
| `ComputeEnd(resource, start, duration)` | `plannedEnd` from working time (R6), or a violation if the start is outside working time |
| `Validate(lot, resource, start)` | OK with `plannedEnd`, or every violated rule with its conflicting item (R1-R4, R6-R8) |
| `EarliestSlot(lot, resources?, notBefore)` | On each given resource (default: every active resource of the lot's type) the earliest start that passes `Validate`. Across resources the slot with the earliest end wins; on a tie the earlier start, then the resource name. The lot's own current placement is ignored. None if nothing fits within the horizon |
| `ScheduleOrderAsap(order lots, notBefore)` | A proposal: each unplaced lot in dependency order gets its `EarliestSlot`, with earlier proposals counted as placed. Existing placements never move. Fails as a whole if any lot cannot be placed |
| `FindConflicts(snapshot)` | Existing placements that now break a rule (section 8) |

Earliest slot search. On each candidate resource the earliest feasible start is always one of these points, so only they are tried, in time order: `notBefore`, the dependency earliest start (`domain-model.md` section 4), the start of each working period, and the end of each placement and unavailability on that resource. The first that passes `Validate` wins. The horizon (default 90 days) is a setting, so the search always ends. Backward search (latest start that meets the due date, P1) uses the same points mirrored.

`notBefore` defaults to now, from the injected clock. The engine never reads the clock itself.

## 4. API style

- REST over JSON, versioned by path: `/api/v1/...`. The OpenAPI document is generated from the code and kept in `backend/openapi/tire-scheduler-v1.json`, where a test keeps it up to date; the frontend types are generated from it (DEC-27).
- Schedule versions in the path. Everything that depends on a version sits under `/versions/{versionId}/...`. `live` is an alias for the live version, and in v1 the client always uses it. Scenarios (P2) then need no URL change.
- Resources:

| Area | Resources |
|---|---|
| Auth | `/auth/sign-in`, `/auth/sign-out`, `/auth/antiforgery-token`, `/me` (the signed-in user) |
| Master data | `/resource-types`, `/resources`, `/calendars`, `/calendars/{id}/shifts`, `/holidays`, `/products`, `/products/{id}/routing` (stages and dependencies together) |
| Orders | `/orders`, `/orders/{id}` (with operations and lots), `/lots/{id}` |
| Unavailability | `/resources/{id}/unavailabilities` |
| Scheduling | `/versions/{v}/lots/{id}/placement`, `/versions/{v}/lots/{id}/earliest-slot`, `/versions/{v}/orders/{id}/asap-proposal`, `/versions/{v}/timeline`, `/versions/{v}/conflicts`, `/versions/{v}/unscheduled-lots` |
| Progress | `/lots/{id}/progress` |
| Views | `/versions/{v}/dashboard`, order list and detail views |

- Placing a lot is `PUT /versions/live/lots/{id}/placement` with `{ resourceId, plannedStart }`. The server computes and returns `plannedEnd`. Removing is `DELETE` on the same URL.
- Earliest slot is `GET /versions/live/lots/{id}/earliest-slot?resourceId=&notBefore=`. It changes nothing; the planner then places the lot with the returned values.
- Schedule order as soon as possible has two steps: `POST /versions/live/orders/{id}/asap-proposal` returns proposed placements without saving them; `POST .../asap-proposal/commit` with the same proposal saves all of them in one transaction. The commit validates again and fails as a whole if anything changed in between.
- Marking a machine down is `POST /resources/{id}/unavailabilities` with `kind: Breakdown`, a start and an optional end. There is no machine status endpoint; status is derived.
- Lists are paged (`page`, `pageSize`) with explicit `sort` and filter query parameters (F-ORD-4). Master data lists are small and returned whole (A22).
- Soft-deleted orders are excluded by a global query filter (F-NFR-1). Cancel is a state change, not a delete.
- Concurrent-edit protection (F-NFR-3) is P1. Keep room for it: every mutable entity can later gain a version column and an `ETag`/`If-Match` pair without changing URLs.
- No bulk endpoints in v1 apart from the order proposal commit. CSV import (P1) adds them.

## 5. Time zone handling

Implements A2.

- The database stores `timestamptz` in UTC. The API sends and receives ISO 8601 UTC (`2026-11-04T14:00:00Z`).
- The plant time zone is one IANA id (for example `Europe/Berlin`) in MasterData settings. The UI always renders in the plant zone, not the browser zone, so every planner sees the same wall clock.
- Calendar shift times and holiday dates are plant-local. Only the engine converts between local and UTC, using the zone id passed in.
- Working time is measured in elapsed real time. On a DST day a local time that does not exist is skipped and one that occurs twice counts both occurrences, so the 3x8h pattern yields 23 or 25 working hours that day.
- Dependency lag is elapsed real time and ignores calendars (A8).
- Due date is a plant-local date, due at the end of that local day (DEC-22).
- Engine tests include a spring-forward and a fall-back case.
- Server code never reads the system clock directly; it uses an injected clock so "now" is testable (needed for `isLate`, display status and `notBefore`).

## 6. Authentication and authorization

- Cookie-based session with ASP.NET Core Identity (DEC-21): HTTP-only, `Secure`, `SameSite=Lax` cookie, same origin for API and SPA. No token is held in browser storage. (The legacy code used JWT; with one SPA on one origin, cookies are simpler and safer.) SSO (F-AUTH-4) can be added later as an external login provider.
- Passwords are hashed by Identity; accounts lock out after repeated failures (threshold and lockout period are settings, default 5 failures and 15 minutes). The failures are counted on the injected clock (DEC-26).
- State-changing requests carry an anti-forgery token: the client reads it from `GET /auth/antiforgery-token` and sends it back in the header that answer names. The token belongs to the session, so the client asks again after sign-in and sign-out. The check runs after the role check, so a request without a session is answered 401 first.
- Every request compares the session with the user's row, so sign-out and deactivation end a session at once (DEC-26, A21).
- The cookies are marked `Secure` unless the setting `Security:CookiesRequireHttps` is false, which is for local development over plain HTTP only.
- Roles: Planner, Viewer, Admin (F-AUTH-2). Enforced by authorization policies on API endpoints:
  - Viewer: read everything.
  - Planner: edit orders, placements, unavailabilities and lot progress.
  - Admin: Planner rights plus master data (including calendars and routings) and users.
- The UI hides actions a role cannot use, but only the API check counts. `GET /me` returns `canPlan` and `canAdminister`, so the UI holds no role rules of its own.
- The first Admin is created from configuration or an environment variable at startup when the user table is empty. If the settings are missing then, startup fails with an error naming them. No credentials in the repository.

## 7. Error format

- All errors use RFC 9457 problem details (`application/problem+json`) with `type`, `title`, `status`, `detail`, plus these fields:
  - `code`: stable machine-readable string (for example `rule.R2.overlap`, `rule.R4.dependency`, `slot.none`, `validation.failed`). [acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md) gives the code for every case it covers.
  - `errors`: for validation failures, a map of field to messages. A field inside a nested object or list is named by its path, for example `stages[0].cycleMinutes` (A24).
  - `rule` and `conflict`: for scheduling rule violations (R7), the rule id and the conflicting item as `{ "type": "placement", "id": 42 }`. Types: `placement`, `unavailability`, `lot`, `resource`. They hold the first violation in rule order.
  - `violations`: every broken rule with its conflicting item, because `Validate` reports all of them.
- Status codes: 400 malformed request or validation failure, 401 not signed in, 403 wrong role, 404 not found, 409 duplicate or state conflict (for example cancelling an order with a lot In Progress, or committing a stale proposal), 422 scheduling rule violation or no slot found, 500 unexpected.
- Unexpected errors return a generic body with code `server.error` and a `correlationId`; details go to the log only, under the same id.
- Module code rejects a request by throwing `ApiProblemException`; one handler writes every problem details answer (DEC-25). A status without a body (no session, wrong role, unknown route, unreadable request) gets its `code` and `detail` from the status.
- The frontend shows `detail` to the user and uses `code` for behavior.

## 8. Persistence

- EF Core migrations, one schema per module, each with its own context and migration history, applied at startup (DEC-24). Create one with `dotnet ef migrations add <Name> --context <Module>DbContext --output-dir Modules/<Module>/Migrations` (README.md).
- Text that is unique without regard to case (A20) has a stored upper-case copy of the column, and the unique index is on the copy. A broken unique index is answered as 409 `conflict.duplicate`, so two requests at once cannot both save the same value.
- Constraints back the domain rules where possible: unique `Product.code`, unique `(product, sequenceNo)`, routing duration values and lags within their ranges, unique `(version, lot)` on placements, positive quantities (A7).
- R2 is also guarded by a PostgreSQL exclusion constraint on placements over `(version, resource, tstzrange(plannedStart, plannedEnd))`, so a race between two requests cannot double-book a resource. Touching ranges are allowed (half-open ranges).
- A routing is replaced as a whole in one transaction that locks the product row (`SELECT ... FOR UPDATE`), so two saves of the same routing run one after the other. Its stages get new ids on every save; nothing outside the routing refers to them (A1, A24).
- R3 and R4 span tables, so no single constraint covers them. A placement command locks the resource row and the order row (`SELECT ... FOR UPDATE`), builds the engine snapshot, validates and writes in one transaction. Open-ended unavailabilities are stored with an infinite upper bound.
- Later facts never delete placements. A new unavailability or a calendar change can make existing placements break R3 or R6. The fact is saved anyway (it is true), affected `plannedEnd` values are recomputed, and the placements stay. `FindConflicts` lists them, and the timeline and dashboard flag them until the planner moves or removes them.
- The live schedule version row is created by the first migration.
- Seed data (task M2-7) is loaded by a separate command, never on normal startup in production.

## 9. Test strategy

| Level | What | Tooling |
|---|---|---|
| Unit | Engine: every Engine-level acceptance criterion, including the worked examples and DST cases; derived statuses and `isLate` | xUnit v3 |
| Property | Engine: any slot returned by `EarliestSlot` passes `Validate`; no feasible start exists before it; `ScheduleOrderAsap` proposals pass `Validate` together | FsCheck or similar, added in task M3-1 |
| Module integration | Each module against a real PostgreSQL, including constraints, locking and soft delete | xUnit v3 with Testcontainers |
| API | Auth and role policies, problem-details shape, rule violations returned as 422 with `rule` and `conflict` | `WebApplicationFactory` |
| Architecture | Module dependency table in section 2 holds between module namespaces; the engine project references no module | NetArchTest |
| Frontend | Components and forms (placement dialog, order form, ASAP proposal review) | Vitest, Testing Library |
| End to end | A few smoke flows only: sign in, create order, schedule it as soon as possible, see it on the timeline | Playwright, added with the first flow |

- Acceptance criteria ([acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md), tasks P5 and P6) are written first and each maps to at least one test named after its ID.
- Tests run on every commit; a failing test blocks merge.

## 10. Frontend notes

- React with TypeScript, routes per module area. Request and response types are generated from the OpenAPI document (`src/api/generated`, never edited by hand). One fetch wrapper (`src/api/request.ts`) adds the anti-forgery token and turns every rejection into an error that carries the problem details; form fields show the messages of their field from it.
- The timeline (F-SCH-2) shows lots as bars per resource and flags conflicted placements. It is read-only in v1, so it can be a simple custom component. Drag-and-drop (P1) is the point at which a Gantt library should be evaluated.
- The placement form offers "earliest slot" next to the manual start field. The order detail view offers "schedule as soon as possible", shows the proposal on a timeline preview, and commits on confirmation.
- Server state via a query cache library; no duplicated client-side business rules. Conflicts are always decided by the server, and the form shows the returned error.
- Wireframes for the timeline, the placement form and the proposal review are in [wireframes.md](REQUIREMENTS/wireframes.md) (task P8).
- The UI passes `live` as the version everywhere, through one setting, so a scenario switcher (P2) changes one value.

## 11. Extension points for later versions

Chosen so P1+ features do not force a rewrite:

- Per-resource calendars (F-MD-6): `Resource.calendar` already exists and the engine takes calendars as input.
- Setup and changeover (F-MD-7, F-SCH-9): fixed setup is already in the duration; sequence-dependent changeover adds an engine input.
- Lot splitting (F-ORD-7): operations already hold 1..n lots, and every rule works per lot.
- Backward scheduling (F-SCH-17): same engine search, mirrored.
- Scenarios (F-SCH-12, F-SCH-15): placements already carry a version, and URLs already contain it. Adds version creation, copy, compare and commit.
- Molds and crews (F-MAT-3): new `ResourceType.kind` values. A lot occupying two resources at once needs a second placement row per lot, the main later change.
- Audit trail (F-AUTH-5): all writes go through module interfaces, so one interception point exists.
- Optimistic concurrency (F-NFR-3): see section 4.
- ERP/MES integration (F-NFR-6): add an integration module that talks to the Orders interface.
