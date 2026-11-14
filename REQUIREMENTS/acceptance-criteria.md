# Acceptance Criteria

Status: DRAFT, written in tasks P5 (M1 and M2 features) and P6 (scheduling rules and engine search) on 2026-11-06. Sources: [features.md](features.md), [domain-model.md](domain-model.md), [ARCHITECTURE.md](../ARCHITECTURE.md), [decisions.md](decisions.md).

## 1. How to read this

- Each criterion has a stable ID (`AC-n`). IDs are never renumbered or reused; a dropped criterion keeps its row with "Retired" in the Then column.
- Every criterion maps to at least one test (`ARCHITECTURE.md` section 9). A test names the criterion it covers, for example `AC_42_order_number_must_be_unique`.
- Level says where the main test lives: Engine (unit or property test of the scheduling engine), API (`WebApplicationFactory` against a real database), Integration (module against a real database), UI (component test) or E2E (Playwright).
- Times are plant-local wall-clock times unless they end in `Z`. Unless a row says otherwise, the plant time zone is `Europe/Berlin` and the calendar is the plant default: Monday to Friday, shifts 00:00-08:00, 08:00-16:00, 16:00-24:00 (A3).
- "Rejected" means: the API answers with problem details (`ARCHITECTURE.md` section 7) carrying the status and `code` given, and nothing is saved.
- Assumptions made while writing these criteria are A12 to A19 in `domain-model.md` section 7.

## 2. M1 Foundation

### F-AUTH-1 Sign in and out

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-1 | An active user with a known email and password | The user signs in with them | 200; an HTTP-only, `Secure`, `SameSite=Lax` session cookie is set; `GET /api/v1/me` returns the user's name and role | API |
| AC-2 | Any account | Someone signs in with a wrong password or an unknown email | Rejected, 401 `auth.invalid_credentials`. The response is identical for both cases, so it does not reveal which emails exist | API |
| AC-3 | A user who has failed to sign in as many times in a row as the lockout threshold allows (setting, default 5) | The user signs in with the correct password within the lockout period (setting, default 15 minutes) | Rejected, 401 `auth.locked_out`. After the lockout period the correct password works again | API |
| AC-4 | An inactive user | The user signs in with the correct password | Rejected exactly as in AC-2 | API |
| AC-5 | A signed-in user | The user signs out | The session cookie is cleared; the next API call with the old cookie gets 401 | API |
| AC-6 | No session | Any API endpoint except sign-in is called | Rejected, 401 `auth.required`. The UI shows the sign-in page and returns to the requested page after sign-in | API, UI |
| AC-7 | A signed-in user | A state-changing request is sent without a valid anti-forgery token | Rejected, 400 `auth.antiforgery` | API |
| AC-8 | An empty user table and admin bootstrap settings (email, password) in configuration | The application starts | One Admin user is created. If the settings are missing while the user table is empty, startup fails with an error naming the missing settings. If users exist, the settings are ignored | Integration |
| AC-138 | The setting `Security:CookiesRequireHttps` (default true) | A state-changing request arrives over plain HTTP | With the default it fails as an unexpected error (AC-25) and nothing is saved; reads such as the health checks still answer. With the setting false, which is for local development only, sign-in works over plain HTTP and the cookies are not marked `Secure` | API |

### F-AUTH-2 Roles

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-9 | A Viewer | The Viewer calls any read endpoint | 200 | API |
| AC-10 | A Viewer | The Viewer calls any write endpoint | Rejected, 403 `auth.forbidden` | API |
| AC-11 | A Planner | The Planner writes orders, placements, unavailabilities or lot progress | Allowed | API |
| AC-12 | A Planner | The Planner writes master data (resource types, resources, calendar shifts, holidays, products, routings) or users | Rejected, 403 `auth.forbidden` | API |
| AC-13 | An Admin | The Admin calls any endpoint | Allowed | API |
| AC-14 | The full list of endpoints | The role test runs | Every endpoint has an authorization policy; the test fails when a new endpoint has none | API |
| AC-15 | A Viewer, then a Planner | They open the order list and the machine register | The Viewer sees no create, edit, cancel or delete actions; the Planner sees order actions but no machine edit actions | UI |
| AC-139 | A signed-in user of any role | `GET /api/v1/me` is called | The answer also holds `canPlan` and `canAdminister`, true exactly when the role may write as in AC-11 to AC-13. The UI decides from these two values which actions to show (AC-15) and holds no role rules of its own | API |

### F-NFR-1 Reliable persistence and soft delete

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-16 | Saved master data and orders | The application restarts | All data is unchanged | Integration |
| AC-17 | A command that writes several rows (for example order creation with its operations, dependencies and lots) | Any part of it fails | Nothing of that command is saved | Integration |
| AC-18 | A Pending order with no placement and no progress (A13) | A Planner deletes it | 204. The order is missing from the order list and its detail returns 404; the row still exists with `isDeleted` set | API, Integration |
| AC-19 | An order that is not Pending, or has a placement or progress | A Planner deletes it | Rejected, 409 `order.not_deletable`. The detail says to cancel instead | API |
| AC-20 | A product, resource type or resource referenced by other rows (A14) | An Admin deletes it | Rejected, 409 `conflict.in_use`, naming one referencing item | API |

### F-NFR-2 Validation and error messages

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-21 | Any write endpoint | A request has several missing or invalid fields | Rejected, 400 `validation.failed`; `errors` maps every invalid field to its messages in one response | API |
| AC-22 | Any write endpoint | The body is not valid JSON or has wrong value types | Rejected, 400 `request.malformed` | API |
| AC-23 | Any endpoint with an id | The id does not exist (or the order is soft-deleted) | Rejected, 404 `not_found` | API |
| AC-24 | A unique value already in use (product code, order number, resource name, resource type name, holiday date) | Another row is saved with it | Rejected, 409 `conflict.duplicate`, naming the field | API |
| AC-25 | An unexpected server error | Any request hits it | 500 with a generic `detail` and a `correlationId`; no stack trace in the body. The log entry holds the details and the same correlation id | API |
| AC-26 | Any form | The server rejects a submit | The form shows `detail` and each field error next to its field and keeps the entered values | UI |

### F-MD-1 Machine register

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-27 | An Admin | The Admin creates a resource type with a name | Saved with kind `Machine`. Any other kind is rejected, 400 `validation.failed` (v1 has machines only, DEC-7) | API |
| AC-28 | A resource type | The Admin creates a resource with name and type, without a calendar | Saved, active, on the plant default calendar. A request cannot set a status; there is no status field (DEC-9) | API |
| AC-29 | An active resource with no placements | The Admin changes its name, type or active flag | Saved | API |
| AC-30 | Resources of several types | Anyone opens the machine register | Resources are listed by type, then name, with type, calendar and active flag; the list filters by type and by active flag. The derived status column is added in M3 (F-MNT-2) | API, UI |
| AC-140 | A resource type and a resource that no other row references | The Admin renames the resource type, or deletes the resource or the resource type | Saved; a deleted item is gone from its list and its id answers as in AC-23 | API |
| AC-141 | A resource name or resource type name typed with spaces around it, or equal to an existing name except for case (A20) | The Admin saves it | The name is stored without the surrounding spaces; the name that differs only by case is rejected as in AC-24 | API |

### F-MD-2 Product register

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-31 | An Admin | The Admin creates a product with code, name and optional spec | Saved. Code is trimmed and unique without regard to case (`ps-205` equals `PS-205`) | API |
| AC-32 | A product with orders | The Admin changes its name, spec or code | Saved; existing orders keep pointing to the product | API |
| AC-33 | A product with no orders | The Admin deletes it | Deleted together with its routing | API |

## 3. M2 Orders

### F-MD-3 Routing per product

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-34 | A product | The Admin saves a routing (stages and dependencies together) | Saved as a whole, replacing the previous routing. Each stage has sequence number, resource type, `setupMinutes` (0 or more, default 0), `cycleMinutes` (more than 0) and `unitsPerCycle` (1 or more, default 1). A dependency names its two stages by sequence number and has `minLagMinutes` (default 0) (A24) | API |
| AC-35 | A routing | Sequence numbers are not 1, 2, 3 ... without gaps or repeats | Rejected, 400 `validation.failed` | API |
| AC-36 | A routing | Its dependencies form a cycle, link a stage to itself, repeat a pair, have a negative lag or name a stage that is not in the routing | Rejected, 400 `routing.invalid_dependency`, naming the stages | API |
| AC-37 | A routing | A stage names a resource type that does not exist | Rejected, 400 `validation.failed` on that stage's resource type field (`stages[0].resourceTypeId` for the first stage) | API |
| AC-38 | The routing editor | The Admin adds stage N | The UI adds a dependency from stage N-1 to stage N with lag 0, which the Admin can edit or remove; parallel branches are allowed | UI |
| AC-142 | A product | A signed-in user of any role reads its routing | The stages in sequence order, each with its resource type id and name, and the dependencies. A product without a routing answers with no stages and no dependencies; saving a routing with no stages removes the routing (A24) | API |
| AC-143 | The routing editor | The Admin removes a stage | The stages after it move up so the numbers stay 1, 2, 3 ..., and the dependencies of the removed stage are removed | UI |
| AC-39 | An order created from a routing | The Admin changes the routing | The order's operations, dependencies and lots are unchanged (A1) | API |

### F-MD-5 Plant calendar and holidays

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-40 | A new database | Migrations run | One calendar exists, the plant default, with shifts 00:00-08:00, 08:00-16:00, 16:00-24:00 Monday to Friday (A3) | Integration |
| AC-41 | The plant time zone setting in configuration (A18) | The application starts | The zone is used for every calendar. A value that is not a known IANA zone id stops startup with an error naming the value | Integration |
| AC-42 | The plant calendar | The Admin saves shifts for a weekday | Saved when each shift has start before end, end at most 24:00, and no two shifts of that day overlap. Otherwise rejected, 400 `validation.failed` | API |
| AC-43 | The plant calendar | The Admin adds a holiday with date and name, or deletes one | Saved or deleted. A second holiday on the same date is rejected as in AC-24 | API |
| AC-44 | v1 | A client tries to create a second calendar | There is no endpoint for it (A3) | API |

### F-ORD-1 Create, edit, cancel order

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-45 | A product with a routing | A Planner creates an order with order number, customer name, product, quantity, due date and priority | Saved. Order number is unique without regard to case; quantity is a whole number of 1 or more (A7); priority is Low, Normal or High, default Normal (A12) | API |
| AC-46 | A product without a routing | A Planner creates an order for it | Rejected, 400 `validation.failed` on the product field | API |
| AC-47 | An order that is not cancelled | A Planner changes customer name, due date or priority | Saved | API |
| AC-48 | An order with no lot placed in the live version and no lot started | A Planner changes product or quantity | Saved; operations, dependencies and lots are generated again from the product's current routing (A1) | API |
| AC-49 | An order with a lot placed in the live version or started | A Planner changes product or quantity | Rejected, 409 `order.locked` | API |
| AC-50 | An order with no lot In Progress | A Planner cancels it | `cancelledAt` is set; status Cancelled. Placements of its lots that have not started are removed in every version, in the same transaction (A13) | API |
| AC-51 | An order with a lot In Progress | A Planner cancels it | Rejected, 409 `order.lot_in_progress` | API |
| AC-52 | A cancelled order | A Planner edits or cancels it again | Rejected, 409 `order.cancelled` | API |

### F-ORD-2 Order status and late flag

Statuses Pending and Cancelled arrive in M2; Scheduled with M3; In Progress and Completed with M5. All are computed in Queries, never stored (DEC-12).

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-53 | A new order | Its status is read | Pending | API |
| AC-54 | A cancelled order | Its status is read | Cancelled, and `isLate` is false whatever the dates | API |
| AC-55 | An unfinished order due 2026-11-10 with no planned end after that day | The clock reads 2026-11-10 23:59 plant time | `isLate` is false | Engine |
| AC-56 | The same order | The clock reads 2026-11-11 00:00 plant time | `isLate` is true (due at the end of the plant-local day, DEC-22) | Engine |
| AC-57 | An unfinished order due 2026-11-10, now before that day | Any lot's `plannedEnd` in the live version is after 2026-11-10 24:00 | `isLate` is true | Engine |
| AC-58 | An order with one lot placed and none started | Its status is read | Scheduled (M3) | API |
| AC-59 | An order with a lot started and not all lots completed | Its status is read | In Progress (M5) | API |
| AC-60 | An order with all lots completed, the last `actualEnd` after the due day | Its status and late flag are read | Completed, `isLate` true (M5) | API |

### F-ORD-3 Generate operations and lots

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-61 | A product with 4 stages in a linear chain, lag 120 minutes between stages 2 and 3 | An order for 101 tires is created | 4 operations with sequence numbers 1-4, each with the stage's resource type, `setupMinutes`, `cycleMinutes`, `unitsPerCycle` and quantity 101; 3 operation dependencies with the same lags | API |
| AC-62 | The same order | Its lots are read | Exactly one lot per operation, lot number 1, quantity 101 (A6) | API |
| AC-63 | A curing stage with setup 30, cycle 12, 2 units per cycle | A lot of 101 is created | `durationMinutes` is 642 (`domain-model.md` section 4) | Engine |
| AC-64 | A stage with the defaults (setup 0, 1 unit per cycle) and cycle 5 | A lot of 40 is created | `durationMinutes` is 200 | Engine |

### F-ORD-4 Order list

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-65 | 120 orders | The list is requested without paging values | Page 1 with 50 orders and the total count; `pageSize` above 200 is rejected, 400 `validation.failed` | API |
| AC-66 | Orders for several customers and products | A search text is given | Orders whose order number, customer name, product code or product name contains the text, without regard to case | API |
| AC-67 | Orders in several states | Filters for status (one or more), late, customer, due date range and priority are combined | Only orders matching all filters | API |
| AC-68 | Any orders | No sort is given | Sorted by due date ascending, then order number. Sorting by order number, customer, priority, due date and creation time works both ways; an unknown sort field is rejected, 400 `validation.failed` | API |
| AC-69 | A soft-deleted order | The list is requested | It is never listed | API |

### F-ORD-5 Order detail

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-70 | An order | Its detail is opened | Shows order fields, status, late flag, progress, creation time and cancellation time (A19), and its operations in sequence with resource type, quantity, duration values, dependencies with lag, and lots with duration and status | API, UI |
| AC-71 | An order with placed lots (M3) | Its detail is opened | Each lot shows its live resource, planned start and planned end | API, UI |
| AC-72 | An order with progress (M5) | Its detail is opened | Each lot shows actual start and end; the order shows progress as completed quantity over total quantity | API, UI |

## 4. Scheduling rules

Applies to `Validate`, `ComputeEnd` and `FindConflicts` in the engine (task M3-1) and to the placement endpoint (task M3-4). Unless a row says otherwise, Press 1 and Press 2 are active resources of type Curing, Builder 1 is of type Building, and all placements are in the live version. This section is the only copy of the rules' worked examples (rows marked "worked example"); `domain-model.md` section 5 states the rules and points here.

### R1 Resource type and active resource

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-73 | A curing lot | It is placed on Press 1 at a working time with nothing else there | Accepted | Engine |
| AC-74 | A curing lot | It is placed on Builder 1 | Rejected, 422 `rule.R1.resource_type`, conflict is the resource | Engine, API |
| AC-75 | Press 1 inactive | A curing lot is placed on Press 1 | Rejected, 422 `rule.R1.inactive`, conflict is the resource | Engine, API |
| AC-76 | Press 1 with live placements | The Admin deactivates Press 1 | Saved. The placements stay and `FindConflicts` lists them under R1 (A15) | API |
| AC-77 | Press 1 with placements | The Admin changes its resource type | Rejected, 409 `conflict.in_use` (A14) | API |

### R2 No overlap on a resource

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-78 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 1 starts Mon 11:00 | Rejected, 422 `rule.R2.overlap`, conflict is Lot A's placement (worked example) | Engine, API |
| AC-79 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 1 starts Mon 12:00 | Accepted, touching is allowed (worked example) | Engine |
| AC-80 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 2 starts Mon 11:00 | Accepted (worked example) | Engine |
| AC-81 | Lot A on Press 1, Mon 08:00-12:00 | Lot A is moved to Press 1, Mon 10:00 | Accepted; a lot never conflicts with its own current placement | Engine |
| AC-82 | Lot A on Press 1, Mon 08:00-12:00 in a scenario version | Lot B is placed on Press 1, Mon 08:00 in the live version | Accepted; rules compare placements within one version only (DEC-11) | Engine |
| AC-83 | Two requests at the same moment placing different lots on Press 1 with overlapping times | Both are processed | Exactly one succeeds; the other is rejected, 422 `rule.R2.overlap`. The database never holds both (exclusion constraint, `ARCHITECTURE.md` section 8) | Integration |

### R3 No overlap with unavailability

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-84 | Planned maintenance on Press 1, Tue 06:00-10:00 | A 4-hour lot on Press 1 starts Tue 04:00 | Rejected, 422 `rule.R3.unavailable`, conflict is the maintenance (worked example) | Engine, API |
| AC-85 | A breakdown on Press 1 from Wed 09:00 with no end | A lot on Press 1 starts the following Monday | Rejected, 422 `rule.R3.unavailable`. Accepted after the breakdown gets an end before that Monday (worked example) | Engine |
| AC-86 | A breakdown on Press 1 from Wed 09:00 with no end | A 4-hour lot on Press 1 starts Wed 05:00 | Accepted, ends 09:00, touching (worked example) | Engine |
| AC-87 | Planned maintenance on Press 1, Sat 10:00-12:00 | An 8-hour lot on Press 1 starts Fri 20:00 (pauses over the weekend, ends Mon 04:00) | Rejected, 422 `rule.R3.unavailable`; overlap is checked over the whole span from start to end (A16) | Engine |
| AC-88 | Lot A on Press 1, Tue 06:00-10:00 | A Planner records a breakdown on Press 1 from Tue 08:00 | Saved. Lot A's placement stays and `FindConflicts` lists it under R3 (DEC-18). The timeline and dashboard flag it | API |

### R4 Stage dependencies with lag

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-89 | Stage 1 lot ends Mon 10:00, lag 0 | Stage 2 lot starts Mon 09:00 | Rejected, 422 `rule.R4.dependency`, conflict is the stage 1 lot (worked example) | Engine, API |
| AC-90 | Stage 1 lot ends Mon 10:00, lag 0 | Stage 2 lot starts Mon 10:00 | Accepted (worked example) | Engine |
| AC-91 | Building lot ends Mon 10:00, lag 120 minutes before curing | Curing lot starts Mon 11:00 | Rejected; the error detail gives the earliest allowed start, Mon 12:00 (worked example) | Engine |
| AC-92 | Building lot ends Fri 23:00, lag 120 minutes | Curing lot starts Mon 00:00 | Accepted; lag is elapsed time and ended Sat 01:00 (worked example, A8) | Engine |
| AC-93 | Curing lot placed Wed 08:00, lag 120 minutes, building lot unplaced | A 6-hour building lot starts Wed 02:00 | Rejected, conflict is the curing lot (worked example) | Engine |
| AC-94 | The same | A 6-hour building lot starts Wed 00:00 | Accepted (worked example) | Engine |
| AC-95 | Stage 1 lots end Mon 10:00 and Mon 14:00, lag 0 | Stage 2 lot starts Mon 12:00 | Rejected, conflict is the lot ending 14:00 (worked example, A11) | Engine |
| AC-96 | Stage 3 depends on stages 1 and 2 (parallel branches); their lots end Mon 10:00 and Mon 12:00 | Stage 3 lot starts Mon 11:00 | Rejected, conflict is the stage 2 lot | Engine |
| AC-97 | Stage 1 lot unplaced | Stage 2 lot is placed at any working time | Accepted; an unplaced lot imposes nothing | Engine |
| AC-98 | Stage 1 lot Mon 06:00-10:00 and stage 2 lot from Mon 10:00, lag 0 | The stage 1 lot is moved to start Mon 08:00 | Rejected, conflict is the stage 2 lot | Engine |
| AC-99 | Any placed lots | A placement is removed | Always accepted by R4; removing never breaks a dependency | Engine |

### R6 Working time and planned end

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-100 | The plant calendar | An 8-hour lot starts Fri 20:00 | `plannedEnd` is Mon 04:00 (`domain-model.md` section 4) | Engine |
| AC-101 | A holiday on Monday | An 8-hour lot starts Fri 20:00 | `plannedEnd` is Tue 04:00 (worked example) | Engine |
| AC-102 | The plant calendar | A lot starts Sat 10:00, Fri 24:00 (that is Sat 00:00), or on a holiday | Rejected, 422 `rule.R6.outside_working_time` | Engine, API |
| AC-103 | A test calendar with one shift, 06:00-14:00 every weekday | A 4-hour lot starts Mon 10:00 | `plannedEnd` is Mon 14:00, not Tue 06:00; an end that falls on a shift end stays there | Engine |
| AC-104 | The same calendar | A 10-hour lot starts Mon 10:00 | `plannedEnd` is Tue 12:00 | Engine |
| AC-105 | A test calendar with shifts 00:00-24:00 every day, zone `Europe/Berlin` | A 4-hour lot starts 2027-03-28 00:00 (clocks go from 02:00 to 03:00) | `plannedEnd` is 2027-03-28 05:00 local, 03:00Z (A2) | Engine |
| AC-106 | The same calendar | A 4-hour lot starts 2026-10-25 00:00 (02:00-03:00 happens twice) | `plannedEnd` is 2026-10-25 03:00 local, 02:00Z (A2) | Engine |
| AC-107 | Lot A on Press 1 from Fri 20:00 to Mon 04:00 | The Admin adds a holiday on that Monday | Saved. Lot A's `plannedEnd` becomes Tue 04:00. If the new end overlaps another placement or unavailability, `FindConflicts` lists Lot A (DEC-18) | API |
| AC-108 | Lot A on Press 1 starting Mon 06:00 | The Admin removes the Monday 00:00-08:00 shift | Saved. Lot A's placement stays and `FindConflicts` lists it under R6 | API |

### R7 Reject, never adjust

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-109 | Any rejected placement | The response is read | 422 with `code`, `rule`, `conflict` (`type` is `placement`, `unavailability`, `lot` or `resource`, plus `id`) and `violations` listing every broken rule with its conflict (`ARCHITECTURE.md` section 7) | API |
| AC-110 | A placement that breaks R2 and R4 at once | `Validate` runs | Both violations are returned; `rule` and `conflict` hold the first in rule order | Engine |
| AC-111 | Lot A on Press 1, Mon 08:00-12:00 | Lot B is placed on Press 1, Mon 11:00 | Lot B is not moved to Mon 12:00 or anywhere else; the request is rejected and nothing is saved | Engine, API |

### R8 Started, completed and cancelled lots

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-112 | A completed lot | A Planner places, moves or removes its placement | Rejected, 422 `rule.R8.started` | Engine, API |
| AC-113 | An In Progress lot | A Planner moves or removes its live placement | Rejected, 422 `rule.R8.started` (A17) | Engine, API |
| AC-114 | A lot of a cancelled order | A Planner places it | Rejected, 422 `rule.R8.cancelled` | Engine, API |

## 5. Engine search

### EarliestSlot

Covers F-SCH-11 and `ARCHITECTURE.md` section 3.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-115 | Any generated plant (task M2-7, `small` preset) and any lot | `EarliestSlot` returns a slot | The slot passes `Validate` | Engine (property) |
| AC-116 | The same | `EarliestSlot` returns a slot on a resource | No earlier start on that resource, checked minute by minute from `notBefore`, passes `Validate` | Engine (property) |
| AC-117 | Lot A on Press 1, Mon 08:00-12:00; a 4-hour curing lot; `notBefore` Mon 08:00 | `EarliestSlot` is asked for Press 1 only | Press 1, Mon 12:00 | Engine |
| AC-118 | The same | `EarliestSlot` is asked without resources | Press 2, Mon 08:00. Across resources the slot with the earliest end wins; on a tie, the earlier start, then the resource name | Engine |
| AC-119 | Nothing placed | `notBefore` is Sat 10:00 | Mon 00:00 | Engine |
| AC-120 | The lot's predecessor ends Mon 10:00 with lag 120 minutes | `notBefore` is Mon 08:00 | Start Mon 12:00 | Engine |
| AC-121 | The lot is already placed on Press 1, Mon 08:00-12:00 | `EarliestSlot` is asked for Press 1 with `notBefore` Mon 08:00 | Press 1, Mon 08:00; the lot's own placement is ignored | Engine |
| AC-122 | Press 2 inactive, Press 1 broken down from now with no end | `EarliestSlot` is asked without resources | No slot; the API answers 422 `slot.none` | Engine, API |
| AC-123 | Every curing press booked for longer than the horizon (setting, default 90 days) | `EarliestSlot` is asked | No slot, 422 `slot.none`; the search stops at the horizon | Engine |
| AC-124 | A completed lot, or a lot of a cancelled order | `EarliestSlot` is asked | Rejected under R8, not `slot.none` | Engine |
| AC-125 | No `notBefore` given | The API is called | The injected clock's now is used | API |
| AC-126 | Any state | `GET .../earliest-slot` is called | Nothing is saved | API |

### ScheduleOrderAsap

Covers F-SCH-16, DEC-19 and `ARCHITECTURE.md` section 4.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-127 | Any generated plant and any order | A proposal is returned | All proposed placements together pass `Validate`, counting each other as placed | Engine (property) |
| AC-128 | An order with 4 unplaced lots in a linear chain, lag 120 minutes before curing | A proposal is requested | Each lot gets its earliest slot in dependency order; the curing lot starts at least 120 minutes after the building lot's proposed end | Engine |
| AC-129 | Stage 3 depends on stages 1 and 2 | A proposal is requested | Stages 1 and 2 are proposed before stage 3; a tie between independent stages goes to the lower sequence number | Engine |
| AC-130 | An order whose curing lot is already placed | A proposal is requested | Only the unplaced lots are proposed; the curing placement stays, and the proposed building lot ends at least the lag before it | Engine |
| AC-131 | An order where one lot has no slot within the horizon | A proposal is requested | No proposal; 422 `slot.none` naming that lot | Engine, API |
| AC-132 | An order with every lot placed | A proposal is requested | 200 with an empty proposal; committing it saves nothing | API |
| AC-133 | A cancelled order | A proposal is requested | Rejected, 422 `rule.R8.cancelled` | API |
| AC-134 | A proposal | The Planner commits it unchanged and nothing changed meanwhile | All placements are saved in one transaction | API |
| AC-135 | A proposal | Another placement takes one of its slots before the commit | Rejected, 409 `proposal.stale`; nothing is saved | API |
| AC-136 | A proposal | The commit body holds a lot of another order, or a changed start | Rejected, 400 `validation.failed`; nothing is saved | API |
| AC-137 | A proposal | It is shown to the Planner | The review screen lists every proposed lot with resource, start and end, shows them on a timeline preview, and saves only on confirm | UI |
