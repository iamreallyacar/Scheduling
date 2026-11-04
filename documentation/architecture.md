# Architecture

Status: DRAFT. Covers v1 (P0). Source: [domain-model.md](domain-model.md), [features.md](features.md). The stack in section 1 is a proposal; it is confirmed or changed in `decisions.md` (task P4) before the scaffold (P9).

## 1. Style and stack

- **Modular monolith**: one deployable API, one single-page app, one PostgreSQL database. 2-5 planners and 15-25 machines do not justify services.
- **Proposed stack** (same family as the legacy code, so existing knowledge carries over): ASP.NET Core Web API, EF Core with PostgreSQL, React with TypeScript.
- Modules mirror the domain model: **Auth, MasterData, Orders, Scheduling, Progress**. Each is its own project with a public interface and internal implementation.
- A **Queries** project holds read-only views that span modules (section 3).
- Each module owns its tables, one database schema per module. No module reads another module's tables.

## 2. Module boundaries

| Module | Owns | Depends on |
|---|---|---|
| Auth | User, roles | none |
| MasterData | MachineType, Machine, Product, RoutingStage, ShiftPattern, Holiday, plant time zone setting | none |
| Orders | Order, Job (including `Job.status`) | MasterData (routing at order creation) |
| Scheduling | Placement, MaintenanceWindow, scheduling rules R1-R8 | MasterData (machines, calendar), Orders (jobs) |
| Progress | JobProgress | Orders, Scheduling |
| Queries | read models only | all modules, read-only |

Rules:
- Dependencies point downward in the table. No cycles. An architecture test enforces this (section 8).
- **Orders owns `Job.status`.** Scheduling and Progress change it by calling an Orders interface (for example `MarkScheduled(jobId)`), never by writing the table.
- **The scheduling rules are a pure library** inside Scheduling: inputs are plain values (placements, windows, shift pattern, zone), output is a result or a rule violation. No database, no clock. This is task M3-1 and the home of the worked examples.
- **Late flag and order status.** `isLate` needs `plannedEnd` (Scheduling) and `actualEnd` (Progress), and Orders cannot depend on them. So order list, order detail and dashboard queries live in Queries, which composes the modules read-only. Order status derivation stays inside Orders because it only needs job statuses.
- Each module registers itself with one extension method (dependency injection) and exposes its own controllers.

## 3. API style

- REST over JSON, versioned by path: `/api/v1/...`. OpenAPI document generated from the code; the frontend client is generated from it.
- Resources are nouns: `/machines`, `/products`, `/products/{id}/routing`, `/orders`, `/orders/{id}`, `/jobs/{id}`, `/jobs/{id}/placement`, `/machines/{id}/maintenance-windows`, `/calendar/shift-pattern`, `/calendar/holidays`, `/dashboard`.
- Placing a job is `PUT /jobs/{id}/placement` with `{ machineId, plannedStart }`. The server computes and returns `plannedEnd`. Removing is `DELETE` on the same URL.
- Lists are paged (`page`, `pageSize`) with explicit `sort` and filter query parameters (F-ORD-4).
- Soft-deleted orders are excluded by a global query filter (F-NFR-1). Cancel is a state change, not a delete.
- Concurrent-edit protection (F-NFR-3) is P1. Keep room for it: every mutable entity can later gain a version column and an `ETag`/`If-Match` pair without changing URLs.
- No bulk endpoints in v1.

## 4. Time zone handling

Implements A2.

- The database stores `timestamptz` in UTC. The API sends and receives ISO 8601 UTC (`2026-11-04T14:00:00Z`).
- The **plant time zone** is one IANA id (for example `Europe/Berlin`) in MasterData settings. The UI always renders in the plant zone, not the browser zone, so every planner sees the same wall clock.
- `ShiftPattern` times and `Holiday` dates are plant-local. Only the scheduling rules library converts between local and UTC, using the zone id passed in.
- Working time is measured in **elapsed real time**. On a DST day a local time that does not exist is skipped and one that occurs twice counts both occurrences, so the 3x8h pattern yields 23 or 25 working hours that day.
- **Due date is a plant-local date**, due at the end of that local day. This is a choice not stated before; record it in `decisions.md`.
- Rules library tests include a spring-forward and a fall-back case.
- Server code never reads the system clock directly; it uses an injected clock so "now" is testable (needed for `isLate`).

## 5. Authentication and authorization

- **Cookie-based session** with ASP.NET Core Identity: HTTP-only, `Secure`, `SameSite=Lax` cookie, same origin for API and SPA. No token is held in browser storage. (The legacy code used JWT; with one SPA on one origin, cookies are simpler and safer.) SSO (F-AUTH-4) can be added later as an external login provider.
- Passwords are hashed by Identity; accounts lock out after repeated failures. State-changing requests carry an anti-forgery token.
- **Roles**: Planner, Viewer, Admin (F-AUTH-2). Enforced by authorization policies on API endpoints:
  - Viewer: read everything.
  - Planner: edit orders, placements, maintenance windows, job progress, machine status.
  - Admin: Planner rights plus master data and users.
- The UI hides actions a role cannot use, but only the API check counts.
- The first Admin is created from configuration or an environment variable at startup. No credentials in the repository.

## 6. Error format

- All errors use **RFC 9457 problem details** (`application/problem+json`) with `type`, `title`, `status`, `detail`, plus these fields:
  - `code`: stable machine-readable string (for example `rule.R2.overlap`, `validation.failed`).
  - `errors`: for validation failures, a map of field to messages.
  - `rule` and `conflict`: for scheduling rule violations (R7), the rule id and the conflicting item as `{ "type": "placement", "id": 42 }`.
- Status codes: 400 malformed request or validation failure, 401 not signed in, 403 wrong role, 404 not found, 409 duplicate or state conflict (for example cancelling an order with a job In Progress), 422 scheduling rule violation, 500 unexpected.
- Unexpected errors return a generic body with a correlation id; details go to the log only.
- The frontend shows `detail` to the user and uses `code` for behavior.

## 7. Persistence

- EF Core migrations, one schema per module. Constraints back the domain rules where possible: unique `Product.code`, unique `(product, sequenceNo)`, positive quantity (A7).
- Overlap rules (R2, R3) are checked in the rules library and also guarded by a database exclusion constraint on `(machine, time range)` where PostgreSQL allows it, so a race between two requests cannot double-book a machine.
- Seed data (task P7) is loaded by a separate command, never on normal startup in production.

## 8. Test strategy

| Level | What | Tooling (proposed) |
|---|---|---|
| Unit | Scheduling rules library: every worked example in `domain-model.md` plus DST cases; order status and `isLate` derivation | xUnit |
| Module integration | Each module against a real PostgreSQL, including constraints and soft delete | xUnit with Testcontainers |
| API | Auth and role policies, problem-details shape, rule violations returned as 422 with `rule` and `conflict` | `WebApplicationFactory` |
| Architecture | Module dependency table in section 2 holds | NetArchTest or similar |
| Frontend | Components and forms (placement dialog, order form) | Vitest, Testing Library |
| End to end | A few smoke flows only: sign in, create order, place job, see it on the timeline | Playwright |

- Acceptance criteria (P5, P6) are written first and each maps to at least one test.
- Tests run on every commit; a failing test blocks merge.

## 9. Frontend notes

- React with TypeScript, routes per module area, API client generated from OpenAPI.
- The timeline (F-SCH-2) is read-only in v1, so it can be a simple custom component. Drag-and-drop (P1) is the point at which a Gantt library should be evaluated.
- Server state via a query cache library; no duplicated client-side business rules. Conflicts are always decided by the server, and the form shows the returned error.

## 10. Extension points for later versions

Chosen so P1+ features do not force a rewrite:
- Per-machine calendars (F-MD-6): the rules library already takes the calendar as input.
- Setup times (F-MD-7, F-SCH-9): job duration stays a stored value, so adding setup is a change at order creation and placement.
- Audit trail (F-AUTH-5): all writes go through module interfaces, so one interception point exists.
- Optimistic concurrency (F-NFR-3): see section 3.
- ERP/MES integration (F-NFR-6): add an integration module that talks to the Orders interface.

## 11. Open points for `decisions.md`

1. Confirm the stack (section 1).
2. Cookie session instead of JWT (section 5).
3. Due date as plant-local date, end of day (section 4).
4. Orders owns `Job.status`; cross-module reads live in Queries (section 2).
