# Acceptance Criteria

Status: DRAFT, written in tasks P5 (M1 and M2 features) and P6 (scheduling rules and engine search) on 2026-11-06 (AC-194 to AC-198 of section 5 in task M4-2); section 6 in task M2-7 (AC-193 in task M4-1) and section 7 in task M3-2 and section 8 in tasks M3-3, M3-4, M3-5 and M3-6 on 2026-11-18. Sources: [features.md](features.md), [domain-model.md](domain-model.md), [ARCHITECTURE.md](../ARCHITECTURE.md), [decisions.md](decisions.md).

## 1. How to read this

- Each criterion has a stable ID (`AC-n`). IDs are never renumbered or reused; a dropped criterion keeps its row with "Retired" in the Then column.
- Every criterion maps to at least one test (`TESTING.md` section "Rules for tests"). A test names the criterion it covers, for example `AC_42_order_number_must_be_unique`.
- Level says where the main test lives: Engine (unit or property test of the scheduling engine), Generator (unit test of the plant generator core or the loader's command line, without an API), API (`WebApplicationFactory` against a real database), Integration (module against a real database), UI (component test) or E2E (Playwright).
- Times are plant-local wall-clock times unless they end in `Z`. Unless a row says otherwise, the plant time zone is `Europe/Berlin` and the calendar is the plant default: Monday to Friday, shifts 00:00-08:00, 08:00-16:00, 16:00-24:00 (A3).
- "Rejected" means: the API answers with problem details (`API.md` section "Error format") carrying the status and `code` given, and nothing is saved.
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
| AC-161 | A command that writes rows of two modules, the second module joining the command's transaction (for example cancelling an order removes its placements, AC-50) | Any part of it fails, or it ends without committing | Nothing of either module is saved. A module that tries to join when no transaction is open, or a command that begins a second transaction while one is open, fails as an unexpected error | Integration |
| AC-162 | A command that has locked a row (an order, a product while its routing is saved, a calendar while its shifts are saved, DEC-33) | A second command locks the same row before the first ends | The second waits until the first commits or rolls back. A lock of a missing row finds nothing; a lock taken outside a transaction fails as an unexpected error | Integration |
| AC-164 | Data loaded from the API on any page (DEC-36) | A write succeeds | Every loaded query except the signed-in user is read again: at once if it is on screen, otherwise the next time it is shown, so no page shows data the write changed with its old values (for example the order list after a product is renamed). The data on screen is read again before the form closes or the page moves on. The query of a deleted row is dropped instead of read again, and signing out reads nothing again. A failed write reads nothing again | UI |
| AC-165 | The OpenAPI document (DEC-37) | `npm run generate:api` runs | `src/api/generated/endpoints.gen.ts` holds one function per operation, named after the operation and taking the path values in URL order, then the body, then the query; each sends the operation's method and URL and returns the operation's response type. An operation without an operation id, with a path value it does not declare, with a parameter outside the path and query, with an optional body or with a method the fetch wrapper does not send stops the generation with an error. A test fails when the committed functions differ from the document | UI |

### F-NFR-2 Validation and error messages

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-21 | Any write endpoint | A request has several missing or invalid fields | Rejected, 400 `validation.failed`; `errors` maps every invalid field to its messages in one response | API |
| AC-22 | Any write endpoint | The body is not valid JSON or has wrong value types | Rejected, 400 `request.malformed` | API |
| AC-23 | Any endpoint with an id | The id does not exist (or the order is soft-deleted) | Rejected, 404 `not_found` | API |
| AC-24 | A unique value already in use (product code, order number, resource name, resource type name, holiday date) | Another row is saved with it | Rejected, 409 `conflict.duplicate`, naming the field | API |
| AC-25 | An unexpected server error | Any request hits it | 500 with a generic `detail` and a `correlationId`; no stack trace in the body. The log entry holds the details and the same correlation id | API |
| AC-26 | Any form | The server rejects a submit | The form shows `detail` and each field error next to its field and keeps the entered values | UI |
| AC-163 | An endpoint under `/api/v1` that reads a request body, mapped with no validation step of its own (DEC-35) | A request reaches it | The body is checked as in AC-21 before the endpoint runs, whatever the position of the body among the endpoint's parameters. Every endpoint of the API that reads a body is checked this way | API |

### F-MD-1 Machine register

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-27 | An Admin | The Admin creates a resource type with a name | Saved with kind `Machine`. Any other kind is rejected, 400 `validation.failed` (v1 has machines only, DEC-7) | API |
| AC-28 | A resource type | The Admin creates a resource with name and type, without a calendar | Saved, active, on the plant default calendar. A request cannot set a status; there is no status field (DEC-9) | API |
| AC-29 | An active resource with no placements | The Admin changes its name, type or active flag | Saved | API |
| AC-30 | Resources of several types | Anyone opens the machine register | Resources are listed by type, then name, with type, calendar and active flag; the list filters by type and by active flag. The derived status column is added in M3 (F-MNT-2, AC-172, AC-173) | API, UI |
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
| AC-144 | The plant calendar with shifts and holidays | A signed-in user of any role reads them | The calendar with the plant time zone and its shifts, Monday first and then by start time, each with weekday, start and end as `HH:mm` (an end at midnight is `24:00`); the holidays sorted by date. Saving shifts replaces all shifts of the calendar (A26) | API |

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
| AC-147 | A product with a routing, or an order that is not cancelled | A Planner creates the order, changes it or cancels it | Create answers 201 with `Location` of the order and `{ id }`; change and cancel answer 204 without a body. Reading the order then shows the change (DEC-29) | API |

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
| AC-148 | An order with the `status` and `isLate` the API returns | Its order page is shown | The page shows the status, and a "Late" mark when `isLate` is true. The page computes neither | UI |

### F-ORD-3 Generate operations and lots

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-61 | A product with 4 stages in a linear chain, lag 120 minutes between stages 2 and 3 | An order for 101 tires is created | 4 operations with sequence numbers 1-4, each with the stage's resource type, `setupMinutes`, `cycleMinutes`, `unitsPerCycle` and quantity 101; 3 operation dependencies with the same lags | API |
| AC-62 | The same order | Its lots are read | Exactly one lot per operation, lot number 1, quantity 101 (A6) | API |
| AC-63 | A curing stage with setup 30, cycle 12, 2 units per cycle | A lot of 101 is created | `durationMinutes` is 642 (`domain-model.md` section 4) | Engine |
| AC-64 | A stage with the defaults (setup 0, 1 unit per cycle) and cycle 5 | A lot of 40 is created | `durationMinutes` is 200 | Engine |
| AC-145 | A stage whose values would give a lot of the order's quantity more than 2,147,483,647 minutes (the longest duration stored) | An order is created, or its product or quantity is changed | Rejected, 400 `validation.failed` on the quantity field, naming the stage | API |
| AC-146 | An order | A signed-in user of any role reads it by id | The order fields with product code and name, its operations in sequence with resource type id and name, each with its lots, and its dependencies by sequence number with lag, and the order's `status` and `isLate` (F-ORD-2). The answer comes from Queries (DEC-29) | API |

### F-ORD-4 Order list

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-65 | 120 orders | The list is requested without paging values | Page 1 with 50 orders and the total count; `pageSize` above 200 is rejected, 400 `validation.failed` | API |
| AC-66 | Orders for several customers and products | A search text is given | Orders whose order number, customer name, product code or product name contains the text, without regard to case | API |
| AC-67 | Orders in several states | Filters for status (one or more), late, customer, due date range and priority are combined | Only orders matching all filters | API |
| AC-68 | Any orders | No sort is given | Sorted by due date ascending, then order number. Sorting by order number, customer, priority, due date and creation time works both ways; an unknown sort field is rejected, 400 `validation.failed` | API |
| AC-69 | A soft-deleted order | The list is requested | It is never listed | API |
| AC-149 | Any orders | The list is requested with an unknown status or priority, or a due date range that ends before it starts | Rejected, 400 `validation.failed` naming the parameter (`status`, `priority`, `dueTo`). A value that is not a date or not true or false for `dueFrom`, `dueTo` or `late` is rejected, 400 `request.malformed`, as for any unreadable parameter | API |
| AC-150 | The order list | A user types a search text, picks filters or a sort, or moves to another page | The list asks the server with those values and shows the orders it returns with their status and a "Late" mark when `isLate` is true, and the total count. The page filters, sorts and computes nothing itself | UI |

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
| AC-76 | Press 1 with live placements | The Admin deactivates Press 1 | Saved. The placements stay and `FindConflicts` lists them under R1 (A15) | Engine, API |
| AC-77 | Press 1 with placements | The Admin changes its resource type | Rejected, 409 `conflict.in_use` (A14) | API |

### R2 No overlap on a resource

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-78 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 1 starts Mon 11:00 | Rejected, 422 `rule.R2.overlap`, conflict is Lot A's placement (worked example) | Engine, API |
| AC-79 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 1 starts Mon 12:00 | Accepted, touching is allowed (worked example) | Engine |
| AC-80 | Lot A on Press 1, Mon 08:00-12:00 | Lot B on Press 2 starts Mon 11:00 | Accepted (worked example) | Engine |
| AC-81 | Lot A on Press 1, Mon 08:00-12:00 | Lot A is moved to Press 1, Mon 10:00 | Accepted; a lot never conflicts with its own current placement | Engine |
| AC-82 | Lot A on Press 1, Mon 08:00-12:00 in a scenario version | Lot B is placed on Press 1, Mon 08:00 in the live version | Accepted; rules compare placements within one version only (DEC-11). The engine is given the placements of one version, so this is tested where the Scheduling module loads them | Integration |
| AC-83 | Two requests at the same moment placing different lots on Press 1 with overlapping times | Both are processed | Exactly one succeeds; the other is rejected, 422 `rule.R2.overlap`. The database never holds both, because every command that writes a placement locks the resource row before it checks the rules (DEC-43, `ARCHITECTURE.md` section "Persistence rules") | Integration |

### R3 No overlap with unavailability

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-84 | Planned maintenance on Press 1, Tue 06:00-10:00 | A 4-hour lot on Press 1 starts Tue 04:00 | Rejected, 422 `rule.R3.unavailable`, conflict is the maintenance (worked example) | Engine, API |
| AC-85 | A breakdown on Press 1 from Wed 09:00 with no end | A lot on Press 1 starts the following Monday | Rejected, 422 `rule.R3.unavailable`. Accepted after the breakdown gets an end before that Monday (worked example) | Engine |
| AC-86 | A breakdown on Press 1 from Wed 09:00 with no end | A 4-hour lot on Press 1 starts Wed 05:00 | Accepted, ends 09:00, touching (worked example) | Engine |
| AC-87 | Planned maintenance on Press 1, Sat 10:00-12:00 | An 8-hour lot on Press 1 starts Fri 20:00 (pauses over the weekend, ends Mon 04:00) | Rejected, 422 `rule.R3.unavailable`; overlap is checked over the whole span from start to end (A16) | Engine |
| AC-88 | Lot A on Press 1, Tue 06:00-10:00 | A Planner records a breakdown on Press 1 from Tue 08:00 | Saved. Lot A's placement stays and `FindConflicts` lists it under R3 (DEC-18). The timeline and dashboard flag it | Engine, API |

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
| AC-107 | Lot A on Press 1 from Fri 20:00 to Mon 04:00 | The Admin adds a holiday on that Monday | Saved. Lot A's `plannedEnd` becomes Tue 04:00. If the new end overlaps another placement or unavailability, or ends less than the lag before a placed successor lot starts, `FindConflicts` lists Lot A (DEC-18) | Engine, API |
| AC-108 | Lot A on Press 1 starting Mon 06:00 | The Admin removes the Monday 00:00-08:00 shift | Saved. Lot A's placement stays and `FindConflicts` lists it under R6 | Engine, API |

### R7 Reject, never adjust

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-109 | Any rejected placement | The response is read | 422 with `code`, `rule`, `conflict` (`type` is `placement`, `unavailability`, `lot` or `resource`, plus `id`) and `violations` listing every broken rule with its conflict (`API.md` section "Error format") | API |
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

Covers F-SCH-11 and the search in the [engine README](../backend/src/TireScheduler.Engine/README.md). Assumption: A45.

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
| AC-194 | A lot that has not started, of an order that is not cancelled | A signed-in user of any role calls `GET /api/v1/versions/live/lots/{id}/earliest-slot`, with or without `resourceId` and `notBefore` | 200 with the lot id, the machine (id and name), and the planned start and end in UTC of the slot `EarliestSlot` finds on the given machine or, without `resourceId`, on every active machine of the lot's resource type, within the horizon of the setting `Scheduling__SearchHorizonDays` (GUIDE.md section "Configuration"). A started lot or one of a cancelled order is rejected under R8 with every violation, as a placement is (AC-109, AC-124) | API |
| AC-195 | A lot | The earliest slot is asked with a `resourceId` that names no machine, with an inactive machine or one of another resource type, for a lot that does not exist or belongs to a deleted order, or in a version that does not exist | A machine that does not exist is rejected, 400 `validation.failed` naming `resourceId`. An inactive machine or one of another type is searched as given (engine README), so the answer is 422 `slot.none`. A missing lot or version answers as in AC-23 | API |
| AC-196 | The placement form of a Planner (`wireframes.md` section 3) | It is open | Above the Placement part, "Find earliest slot" offers a machine list of "Any &lt;resource type&gt;" followed by the machines of the Placement part, and a not-before time typed on the plant's wall clock, empty meaning now. [Find earliest slot] asks for the earliest slot with the chosen machine's id, none for "Any", and the typed time in UTC, none when empty (A38), and shows the machine, planned start and planned end the API returns on the plant's wall clock. Nothing is saved | UI |
| AC-197 | A suggested slot in the placement form | The Planner chooses [Use suggestion] | Its machine and start fill the Placement fields; nothing is sent until [Save placement] | UI |
| AC-198 | The "Find earliest slot" part | The search is rejected, for example with `slot.none` or under R8 | The part shows the `detail` and every violation as in AC-181, and no suggestion; the Placement fields keep their values | UI |

### ScheduleOrderAsap

Covers F-SCH-16, DEC-19 and `API.md` section "Scheduling calls".

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

## 6. Synthetic plant generator

Covers task M2-7 (no feature). The generator core turns parameters, a seed and a start date into a plant; the loader writes that plant through the API (DEC-32). The horizon is the start date plus the given number of whole weeks. Assumptions are A30 to A34 in `domain-model.md` section 7.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-151 | Generator parameters, a seed and a start date | The plant is generated twice with them, and once with another seed | Both plants with the same seed are identical; the plant with the other seed differs | Generator |
| AC-152 | Any preset and seed | A plant is generated | Every machine belongs to a generated machine type. Every product has one routing stage per stage parameter, in the same order, on that stage's machine type, with setup, cycle and units per cycle inside their ranges, and a linear chain of dependencies (stage N depends on stage N-1) with each lag inside its range. Product codes and order numbers are unique, and every order names a generated product with a quantity inside the range | Generator |
| AC-153 | Any preset and seed | A plant is generated | The shifts repeat the shift parameters on each working day. Holidays are distinct working days inside the horizon. Every week of the horizon has the orders-per-week count of orders, each due the drawn number of days after the start of its week, from the rush range for a rush order and from the normal range otherwise. Rush orders have priority High, the others Normal or Low | Generator |
| AC-154 | Any preset and seed | A plant is generated | Unavailabilities start on working days inside the horizon, on the 15-minute grid from 06:00 to 18:00 plant-local, and last a duration inside the range of their kind. On one machine they are at least 60 minutes apart. Only a breakdown can have no end, only when the open-ended share is above 0, and it is the last unavailability of its machine | Generator |
| AC-155 | The presets `small`, `plant` and `stress` | Each is generated with any seed | `small`: 4 machines of 3 types and 3 products. `plant`: 20 machines of 4 types (extrusion, building, curing, inspection), 40 products, and Monday to Friday with shifts 00:00-08:00, 08:00-16:00 and 16:00-24:00 (A3). `stress`: 25 machines, a 2-week horizon, and for every machine type its lots need at least the working minutes of its machines in the horizon, so a placed schedule can fill every machine (fully loaded) | Generator |
| AC-156 | Parameters with a count below its minimum, a range whose minimum is above its maximum or below the smallest allowed value, a share outside 0 to 1, a stage on an unknown machine type, a shift outside the day or not ending after it starts, a lag list whose length is not the number of stages minus 1, or more holidays than working days in the horizon | A plant is generated | Fails with an error naming the parameter; no plant is returned | Generator |
| AC-157 | An empty plant (no resource type, product or order; the plant default calendar of the first migration) and an Admin's sign-in | The loader runs with a preset, a seed and a start date | Every generated machine type, machine, shift, holiday, product with its routing, and order is created through the API and reads back with the generated values; each order has its operations and lots. Every generated unavailability is recorded on its machine and reads back with its kind, reason, and start and end in UTC, its plant-local start converted by the engine (A33). The loader prints the seed, the start date and the count of each kind | API |
| AC-158 | A plant that has any resource type, product or order | The loader runs | It stops before writing anything, with an error naming what exists, and exits with a non-zero code | API |
| AC-159 | The loader running | The API rejects one of its requests (for example a value the API does not accept, or a user who may not write master data) | The loader stops at once, with an error giving the request, the status, the problem `code` and the `detail`, and exits with a non-zero code. Rows written before the rejection stay | API |
| AC-160 | The loader's command line | A preset is unknown, the seed or start date cannot be read, an option is unknown, or the sign-in settings are missing | It stops before calling the API, with an error naming the option or setting, and exits with a non-zero code | Generator |
| AC-193 | A generated plant and a plant time zone | It is turned into a `ScheduleSnapshot` (`PlantSnapshot.From`) | One calendar with the generated shifts, the holidays and the zone; one active resource per machine, named as the machine, of its machine type, on that calendar; one lot per stage of every order, with the stage's sequence number, not started and not cancelled, lasting the `LotDuration` of the stage and the order quantity, on the stage's machine type; the product's dependencies with their lags between the lots of the same order; every unavailability on its machine with its start and end in UTC, converted by the engine (A33); nothing placed | Generator |

## 7. M3 Maintenance

Unless a row says otherwise, Press 1 is a resource of the plant, the clock is the injected `TimeProvider`, and times are sent and answered in UTC (`API.md` section "Conventions"). Assumptions are A37 and A38 in `domain-model.md` section 7.

### F-MNT-1 Unavailability

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-166 | Press 1, active or inactive, and a Planner | The Planner records an unavailability on it with kind `Planned` or `Breakdown`, a start, an end and an optional reason, or a breakdown without an end | 201 with `Location` and the unavailability: id, resource id, kind, start, end (missing for a breakdown with no known end) and reason, the times in UTC and the reason without surrounding spaces. Unavailabilities of one resource may overlap, and one may start in the past (A37) | API |
| AC-167 | Press 1 | An unavailability is saved without a kind or a start, with an end that is not after its start, as planned maintenance without an end, with a start or end that is not a whole minute, or with a reason longer than 200 characters | Rejected, 400 `validation.failed`, naming each field (`kind`, `start`, `end`, `reason`). A kind other than `Planned` or `Breakdown` is an unreadable value, rejected as in AC-22 | API |
| AC-168 | Press 1 with unavailabilities, and another machine with its own | A signed-in user of any role reads the unavailabilities of Press 1 | Only those of Press 1, latest start first. A missing resource answers as in AC-23 | API |
| AC-169 | An unavailability of Press 1 | A Planner changes its kind, start, end or reason, for example gives an open breakdown its end to bring the machine back (`domain-model.md` section 6) | 200 with the saved unavailability, checked as in AC-167. An unavailability id that belongs to another resource than the one in the path answers as in AC-23 | API |
| AC-170 | An unavailability of Press 1 | A Planner deletes it, for example one entered by mistake | 204; it is gone from the list and its id answers as in AC-23 | API |
| AC-171 | The machine page (`wireframes.md` section 5) | A Planner records, changes or deletes an unavailability | Times are entered and shown on the plant's wall clock and sent in UTC; a local time that occurs twice is read as its first occurrence, one inside a DST gap as the first time after the gap (A38). A breakdown without an end shows "no end". A rejected save shows the problem as in AC-26 and keeps the entered values. A Viewer sees the list without the actions | UI |

AC-20 covers a resource that has an unavailability: deleting it is rejected, naming the unavailability.

### F-MNT-2 Machine status

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-172 | Resources with unavailabilities | A signed-in user of any role reads the machine list or one machine | Each machine has a `status`: `Down` when a breakdown covers now, otherwise `Maintenance` when planned maintenance covers now, otherwise `Idle` (`domain-model.md` section 6). An unavailability covers the time from its start up to, not including, its end; one without an end covers everything from its start on. `Running` follows with lot progress (task M5-1). The reads come from Queries (DEC-39); the machine writes answer without a status (AC-28) | API |
| AC-173 | The machine register and the machine page | They are shown | Each machine shows the `status` the API returns; neither page derives it. Each machine name in the register opens its machine page | UI |

## 8. M3 Scheduling

Unless a row says otherwise, the version is the live version, named `live` in the path (DEC-17), and times are sent and answered in UTC. Assumptions are A39 to A41 in `domain-model.md` section 7.

### F-SCH-7 Unscheduled-lots queue

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-174 | Orders with lots, one lot placed in the live version, a cancelled order and a deleted order | A signed-in user of any role reads `GET /api/v1/versions/live/unscheduled-lots` | Every lot whose derived status in that version is Unscheduled (`domain-model.md` section 6), so none of a cancelled or deleted order and none placed in the version. Each with its id, lot number, quantity and `durationMinutes`; its order's id, order number and due date; its operation's sequence number and resource type; and the operations it depends on, each with sequence number, resource type and lag. Sorted by due date, then order number, then sequence number, and returned whole (A39). The answer comes from Queries. A version that does not exist answers as in AC-23 | API |
| AC-175 | The schedule page (`/schedule`, `wireframes.md` section 2) | Anyone opens it | It shows the live version as a fixed label and lists the lots the API returns, in that order, with their count: the order number linking to the order page, the stage with its resource type, lot number, quantity, duration in minutes, due date, and each stage it depends on with its lag, or "-" when there is none. The page filters, sorts and computes nothing. With no lot in the answer it says that every lot is placed | UI |

### F-SCH-1 Place, move and remove a lot

The rules a placement must keep are in section 4; their API-level rows (AC-74, AC-75, AC-78, AC-84, AC-89, AC-102, AC-109, AC-111 to AC-114) are tested through the endpoints below.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-176 | A lot of an order that is not cancelled, an active machine of the lot's resource type, and a start in working time with nothing else there | A Planner sends `PUT /api/v1/versions/live/lots/{id}/placement` with `resourceId` and `plannedStart` | 200 with the lot id, resource id, planned start and the `plannedEnd` the server computed from the machine's working time (R6). Sent again with another machine or start, the same placement moves there, because a lot has at most one placement per version (A40). The lot then leaves the unscheduled-lots queue (AC-174), and the lot and its order read as Scheduled (AC-58) | API |
| AC-177 | A Planner and a lot | A placement is sent without `resourceId` or `plannedStart`, with a start that is not a whole minute, or with a machine that does not exist | Rejected, 400 `validation.failed`, naming each field. A lot that does not exist or belongs to a deleted order, or a version that does not exist, answers as in AC-23 | API |
| AC-178 | A placed lot that has not started | A Planner sends `DELETE` on its placement | 204, and the lot is back in the unscheduled-lots queue. A lot with no placement in the version answers as in AC-23 | API |
| AC-179 | A rejected placement (AC-109) | The response is read | `detail` is the text of the first violation. Each entry of `violations` has `rule`, `code`, `conflict` and a `detail` text, and an R4 violation held back by a placed predecessor lot also has `earliestAllowedStart` (AC-91); the others have it null | API |
| AC-180 | The unscheduled-lots queue on the schedule page and the lots on the order page, and a Planner | The Planner chooses [Place...] on an unplaced lot or [Move...] on a placed one (`wireframes.md` section 3) | The placement form opens with the lot's order, stage with its resource type, lot number, quantity and duration. Its machine list holds the active machines of that resource type, as the API returns them for that filter. The start is typed on the plant's wall clock and sent in UTC (A38), and the planned end reads "Calculated when saved" (WQ1). After a save the form closes and the page shows what the API then returns. For a placed lot the form opens filled in and also offers [Remove placement]. A Viewer sees neither button | UI |
| AC-181 | The placement form | The server rejects a save or a removal | The form shows the `detail`, the field errors next to their fields, and every entry of `violations` with its text and code and, when given, the earliest allowed start on the plant's wall clock. It keeps the entered values (AC-26) | UI |

### F-SCH-4 Conflicts from later facts

The engine rows of the later facts (AC-76, AC-88, AC-107, AC-108) are tested at API level through the endpoints below.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-182 | Placements in the live version | The Admin replaces the plant calendar's shifts, or adds or deletes a holiday | Saved. In every version, the `plannedEnd` of each placement whose lot has not completed is recomputed from the new working time (R6), later or earlier, and the order reads and late flag follow it. A placement whose start is no longer in working time keeps its stored end (A42). The change and the recomputed ends are saved together or not at all (DEC-44) | Engine, API |
| AC-183 | Placements in the live version, some of which break a rule after a later fact | A signed-in user of any role reads `GET /api/v1/versions/live/conflicts` | Every placement `FindConflicts` lists, sorted by planned start, then placement id, and returned whole. Each with its placement id, planned start and end, and machine (id and name); its lot's id, lot number, quantity and `durationMinutes`; its order's id and order number; its operation's sequence number and resource type; and `violations`, each with `rule`, `code`, `conflict`, `detail` and `earliestAllowedStart` as in a rejection (AC-179). An empty list when nothing conflicts. The answer comes from Queries. A version that does not exist answers as in AC-23 | API |
| AC-184 | The schedule page (`/schedule`, `wireframes.md` section 2) | Anyone opens it | Above the unscheduled-lots queue it lists the conflicted placements the API returns, in that order, with their count: the order number linking to the order page, the stage with its resource type, lot number, machine, planned start and end on the plant's wall clock, and each violation with its text and code. It says when nothing conflicts. A Planner's [Move...] opens the placement form for that lot filled in with its placement (AC-180); a Viewer sees no button | UI |

### F-SCH-2 Timeline

The timeline is read-only (DEC-45, DEC-46). Assumptions are A43 and A44 in `domain-model.md` section 7.

| ID | Given | When | Then | Level |
|---|---|---|---|---|
| AC-185 | Machines of several types, one inactive machine with a placement from Tue 2026-11-17 and one without, placements and unavailabilities in and outside the range | A signed-in user of any role reads `GET /api/v1/versions/live/timeline?from=2026-11-16&to=2026-11-29` | 200 with `start` and `end`, the UTC instants at which the plant-local day `from` begins and the day `to` ends. The resource types sorted by name, each with its machines sorted by name; an inactive machine without a placement in the range is left out (A43), and so is a type with no machine left. Each machine with id, name, `isActive`, the derived `status` (AC-172), its working periods in the range (AC-186), the unavailabilities that overlap the range, each with id, kind, start, end or none, and reason, and the placements of the version that overlap the range, sorted by planned start, then placement id. Each placement with its placement id, planned start and end; its lot's id, lot number, quantity, `durationMinutes` and derived `status`; its order's id, order number, product and `isLate` in the version (`domain-model.md` section 6); and its operation's sequence number and resource type. Times are as stored, not cut at the range. The answer comes from Queries. A version that does not exist answers as in AC-23 | API |
| AC-186 | The plant default calendar, a holiday on Wed 2026-10-28, and a range from Fri 2026-10-23 to Fri 2026-10-30 | The timeline is read | Each machine's working periods are the UTC periods of its calendar's shifts on the local days of the range, as the engine computes them for R6 (the "Working time" part of the engine README), with periods that touch or overlap merged into one and every period cut at `start` and `end`. Here, in plant-local time: Fri 23 00:00 to Sat 24 00:00, Mon 26 00:00 to Wed 28 00:00, and Thu 29 00:00 to Sat 31 00:00. A holiday and a weekday without shifts give no period. A calendar with shifts on a DST day gives a period of 23 or 25 hours that day | Engine, API |
| AC-187 | Any role | The timeline is read without `from` or `to`, with `to` before `from`, with a range of more than 31 days, both days counted, or with a day outside the years 2000 to 2999 (A44) | Rejected, 400 `validation.failed`, naming each field | API |
| AC-188 | The schedule page (`wireframes.md` section 2) | Anyone opens it | Above the conflicted placements and the queue it shows the timeline of the live version, with the times in the plant zone. By default the range is the two weeks from the Monday of the current plant-local week; "Day" shows one day and "Two weeks" two weeks from the first shown day, [< Prev] and [Next >] move by the shown length, and [Today] goes back to the range that holds today (A44). The page asks the API for that range and shows it as dates. The machines are grouped by resource type in the order the API returns them, each with its name and the status the API returns; the resource type list shows one type or all. [Hide queue] hides the unscheduled-lots queue and [Show queue] brings it back | UI |
| AC-189 | The timeline | It is shown | Each placement is a bar from its planned start to its planned end, cut at the range edges, labelled with its order number and lot number. Each unavailability is a band labelled with its kind, and one without an end runs to the right edge. Time outside the machine's working periods is shaded, and a line marks the current time when it is in the range. A placement the conflict list of the API holds (AC-183) has a `!` marker; the conflicted-placements button shows how many the API lists and, pressed, shows only the bars of those placements. The page computes no rule: it never hides or moves a bar because of a conflict (DEC-18) | UI |
| AC-190 | The timeline and a Planner, then a Viewer | The user opens a bar | A popover shows the order number, stage with its resource type, lot number, product, quantity, machine, planned start and end on the plant's wall clock, the lot status and whether the order is late, as the API returns them, and every violation the API lists for the placement with its text and code. [Open order] opens the order page. A Planner's [Move...] opens the placement form filled in with the placement (AC-180), which also offers [Remove placement]; a Viewer sees no [Move...]. [x] closes the popover, and so does a range or filter that no longer draws its bar | UI |
| AC-191 | A violation in the popover, the conflict list or a rejected save of the placement form on the schedule page | It is shown | It has a link to its conflicting item (DEC-42, DEC-46): a machine opens its machine page; an unavailability opens the page of the machine it belongs to; a placement, and a lot, clear the resource type and conflicted-placements filters, select that placement's bar and open its popover. The link is shown only when the shown timeline holds the item | UI |
| AC-192 | The schedule page, with the conflict list (AC-183) still loading, then with its read failed | The timeline is shown | No bar has a `!` marker and the conflicted-placements button is not shown, and above the timeline the page says that the conflict markers are loading, or that they could not be loaded and no bar is marked. A popover says that its broken rules are not known yet instead of listing none. Once the list has loaded, the markers, the button and the popover's violations are shown as in AC-189 and AC-190 (DEC-52) | UI |
