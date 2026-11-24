# API

The interfaces between the browser and the API: conventions, routes, scheduling calls, the anti-forgery protocol and the error format. The generated [OpenAPI document](backend/openapi/tire-scheduler-v1.json) is the source of route details (paths, parameters, request and answer shapes); this file holds the conventions behind it. Rules the API follows are in [ARCHITECTURE.md](ARCHITECTURE.md), Design.

## Conventions

- REST over JSON, versioned by path: `/api/v1/...`. The OpenAPI document is generated from the code and kept in `backend/openapi/tire-scheduler-v1.json`, where a test keeps it up to date; the frontend types are generated from it (DEC-27).
- Times are ISO 8601 UTC (`2026-11-04T14:00:00Z`). Dates (due dates, holidays) are plant-local `YYYY-MM-DD`. Shift times are plant-local `HH:mm`, where `24:00` is the midnight that ends a day (AC-144).
- Schedule versions in the path. Everything that depends on a version sits under `/versions/{versionId}/...`. `live` is an alias for the live version, and in v1 the client always uses it (DEC-17).
- Lists are paged (`page`, `pageSize`) with explicit `sort` and filter query parameters (F-ORD-4). Master data lists are small and returned whole (A22), and so is the unscheduled-lots queue (A39).
- `sort` names one field of the list items, `-` in front sorts it descending (`sort=-dueDate`). A filter that takes several values repeats the parameter (`status=Pending&status=Scheduled`).
- Concurrent-edit protection (F-NFR-3) is P1. Keep room for it: every mutable entity can later gain a version column and an `ETag`/`If-Match` pair without changing URLs.
- No bulk endpoints in v1 apart from the order proposal commit. CSV import (P1) adds them.
- Order writes answer without the order, because its status and `isLate` come from Queries: create answers 201 with `Location` and `{ id }`, change and cancel answer 204. The client then reads the order again (DEC-29).
- There is no machine status endpoint; status is derived. The machine reads (`GET /resources`, `GET /resources/{id}`) carry it and come from Queries, while the machine writes answer the machine without it (DEC-39).

## Routes

Planned route areas for v1. Routes that are built are in the OpenAPI document.

| Area | Resources |
|---|---|
| Auth | `/auth/sign-in`, `/auth/sign-out`, `/auth/antiforgery-token`, `/me` (the signed-in user) |
| Master data | `/resource-types`, `/resources`, `/calendars` (each with the plant time zone and its shifts), `/calendars/{id}/shifts` (the whole week, saved together, A26), `/holidays`, `/products`, `/products/{id}/routing` (stages and dependencies together) |
| Orders | `POST /orders`, `PUT` and `DELETE /orders/{id}`, `/orders/{id}/cancel`, `/lots/{id}` |
| Unavailability | `/resources/{id}/unavailabilities` (list, latest start first, and record), `/resources/{id}/unavailabilities/{unavailabilityId}` (change and delete) |
| Scheduling | `/versions/{v}/lots/{id}/placement`, `/versions/{v}/lots/{id}/earliest-slot`, `/versions/{v}/orders/{id}/asap-proposal`, `/versions/{v}/timeline`, `/versions/{v}/conflicts`, `/versions/{v}/unscheduled-lots` |
| Progress | `/lots/{id}/progress`: `PUT` with `{ actualStart, actualEnd }` records or corrects a lot's progress as a whole and answers it, `DELETE` removes it (DEC-62, A47) |
| Views (Queries) | `GET /orders` (paged; `search`, `status`, `late`, `customer`, `dueFrom`, `dueTo`, `priority`, `sort`, A29) and `GET /orders/{id}` (with operations, lots with their `status`, dependencies, and `progress` as `completedQuantity`, `totalQuantity` and `percentComplete`, DEC-64), each order with `status` and `isLate` (DEC-29); `/versions/{v}/dashboard` (`dueSoonUntil`, `overdueOrders` and `dueSoonOrders` as order summaries, `unscheduledLotCount`, `conflictedPlacementCount`, and `downMachines` as in the machine list, DEC-65) |

## Scheduling calls

- A version path takes `live` or a version id (A41).
- Placing a lot is `PUT /versions/live/lots/{id}/placement` with `{ resourceId, plannedStart }`. The server computes `plannedEnd` and answers the placement (`lotId`, `resourceId`, `plannedStart`, `plannedEnd`). Sending it again for a placed lot moves that placement (A40). Removing is `DELETE` on the same URL, answered 204. The order reads carry each lot's live placement (AC-71).
- Conflicts is `GET /versions/live/conflicts`: every placement of the version that breaks a rule after a later fact, returned whole, each with its lot, order and machine and its `violations` in the shape of a rejection (see Error format). Queries serves it from the engine's `FindConflicts` over the stored planned ends (AC-183).
- Timeline is `GET /versions/live/timeline?from=&to=` with two plant-local dates, both days included and at most 31 days apart: the machines by resource type, each with its status, working periods, unavailabilities and placements in the range, from Queries (DEC-45). The conflict markers come from the conflicts read (DEC-46).
- Earliest slot is `GET /versions/live/lots/{id}/earliest-slot?resourceId=&notBefore=`, both optional: without `resourceId` every active machine of the lot's type is searched, without `notBefore` the search starts now. It answers the lot id, the machine (id and name), `plannedStart` and `plannedEnd`, or 422 `slot.none` when nothing fits within the horizon (GUIDE.md section "Configuration"), or the R8 rejection of a placement. It changes nothing; the planner then places the lot with the returned values (DEC-58).
- Schedule order as soon as possible has two steps, both for a Planner (DEC-60). `POST /versions/live/orders/{id}/asap-proposal?notBefore=` saves nothing and answers the `notBefore` it started from (now without one), every lot of the order with its stage, machine, `plannedStart`, `plannedEnd` and `isProposed` (false for a lot placed already), `finishesAt` and `wouldBeLate`; or 422 `slot.none` with the `lotId` of the first lot without a slot, or the R8 rejection of a placement. `POST .../asap-proposal/commit` with `{ notBefore, placements: [{ lotId, resourceId, plannedStart }] }`, the proposed lots unchanged, saves all of them in one transaction and answers them as placements. The server makes the proposal again from that `notBefore` under its locks, and answers 409 `proposal.stale` when it differs from the body.
- Marking a machine down is `POST /resources/{id}/unavailabilities` with `kind: Breakdown`, a start and an optional end. Bringing it back is `PUT` on that unavailability with its end set.

## Sign-in and anti-forgery

- State-changing requests carry an anti-forgery token: the client reads it from `GET /auth/antiforgery-token` and sends it back in the header that answer names.
- The token belongs to the session, so the client asks again after sign-in and sign-out.
- A request without a session is answered 401 before the token is checked.
- `GET /me` returns `canPlan` and `canAdminister`.

## Error format

- All errors use RFC 9457 problem details (`application/problem+json`) with `type`, `title`, `status`, `detail`, plus these fields:
  - `code`: stable machine-readable string (for example `rule.R2.overlap`, `rule.R4.dependency`, `slot.none`, `validation.failed`). [acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md) gives the code for every case it covers.
  - `errors`: for validation failures, a map of field to messages. A field inside a nested object or list is named by its path, for example `stages[0].cycleMinutes` (A24).
  - `rule` and `conflict`: for scheduling rule violations (R7), the rule id and the conflicting item as `{ "type": "placement", "id": 42 }`. Types: `placement`, `unavailability`, `lot`, `resource`. They hold the first violation in rule order.
  - `violations`: every broken rule, because `Validate` reports all of them, each with `rule`, `code`, `conflict`, a `detail` text, and `earliestAllowedStart` (UTC) when a placed lot of an earlier stage holds the start back, otherwise null (AC-179, DEC-42). `detail` of the whole problem is the text of the first violation.
- Status codes: 400 malformed request or validation failure, 401 not signed in, 403 wrong role, 404 not found, 409 duplicate or state conflict (for example cancelling an order with a lot In Progress, or committing a stale proposal, `proposal.stale`), 422 scheduling rule violation or no slot found, 500 unexpected.
- Unexpected errors return a generic body with code `server.error` and a `correlationId`; details go to the log only, under the same id.
- A status without a body (no session, wrong role, unknown route, unreadable request) gets its `code` and `detail` from the status.
