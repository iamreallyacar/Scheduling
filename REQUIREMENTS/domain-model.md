# Domain Model

Status: DRAFT. Covers v1 (P0 features) only. Source: [features.md](features.md), [README.md](../README.md). Items marked A# are assumptions; this file is their only list. Change them here first if the business answers differently. Open questions are in the Open table of [decisions.md](decisions.md).

## 1. Glossary

| Term | Meaning |
|---|---|
| Product | A tire model that can be ordered |
| Routing | The fixed set of stages a product goes through, with dependencies between them |
| Stage | One step in a routing, run on one resource type (for example Curing) |
| Dependency | A rule that one stage may start only a minimum lag after another stage ends |
| Resource type | A category of resource (extrusion line, building machine, curing press). In v1 every resource type is a machine type |
| Resource | A physical thing a lot occupies while it runs. In v1 always a machine |
| Calendar | A weekly shift pattern. Every resource follows one calendar |
| Working time | Time inside a resource's calendar, excluding plant holidays |
| Order | A customer request for a quantity of a product by a due date |
| Operation | The work for one stage of one order. An order has one operation per stage |
| Lot | A schedulable part of an operation, with its own quantity. An operation has one or more lots. The UI may call a lot a "job" |
| Schedule version | A complete set of placements. Exactly one is live; others are scenarios (P2) |
| Placement | A lot's assigned resource and start time within one schedule version |
| Unavailability | A period when a resource cannot run lots: planned maintenance or a breakdown |

## 2. Entities

### Master data (module MasterData)

| Entity | Attributes | Notes |
|---|---|---|
| ResourceType | id, name, kind | Kind is `Machine` in v1. Molds and crews (F-MAT-3) become new kinds later. Edited by Admin |
| Resource | id, name, resourceType, calendar, isActive | No status field. Displayed status is derived (section 6) |
| Calendar | id, name, isPlantDefault | Exactly one calendar in v1, the plant default (A3) |
| CalendarShift | calendar, weekday, startTime, endTime | One or more rows per weekday. Plant-local wall-clock time (A2) |
| Holiday | id, date, name | Non-working day for every calendar. Date is unique |
| Product | id, code, name, spec | Code is unique |
| RoutingStage | id, product, sequenceNo, resourceType, setupMinutes, cycleMinutes, unitsPerCycle | Unique (product, sequenceNo). Sequence starts at 1 and orders stages for display. Defaults: setupMinutes 0, unitsPerCycle 1 |
| RoutingDependency | product, predecessorStage, successorStage, minLagMinutes | No cycles. v1 UI generates a linear chain (stage N depends on N-1) with editable lag; the model allows parallel branches |

### Orders (module Orders)

| Entity | Attributes | Notes |
|---|---|---|
| Order | id, orderNo, customerName, product, quantity, dueDate, priority, createdAt, cancelledAt, isDeleted | Customer is plain text in v1 because the customer register is P1. Order number and priority as in A12. Soft delete (F-NFR-1, A13). Status is derived (section 6) |
| Operation | id, order, sequenceNo, resourceType, quantity, setupMinutes, cycleMinutes, unitsPerCycle | One per routing stage. Values copied from the routing, not referenced (A1) |
| OperationDependency | predecessorOperation, successorOperation, minLagMinutes | Copied from RoutingDependency at order creation (A1) |
| Lot | id, operation, lotNo, quantity, durationMinutes | Lot quantities of an operation add up to the operation quantity. v1 creates exactly one lot per operation (A6). `durationMinutes` is computed (section 4) |

### Scheduling (module Scheduling)

| Entity | Attributes | Notes |
|---|---|---|
| ScheduleVersion | id, name, kind, basedOn, createdBy, createdAt | Kind: Live or Scenario. v1 has exactly one Live version and no way to create scenarios (A10) |
| Placement | id, version, lot, resource, plannedStart, plannedEnd | Unique (version, lot). `plannedEnd` is computed, never typed in |
| Unavailability | id, resource, start, end, kind, reason | Kind: Planned or Breakdown. `end` may be empty for a breakdown with no known end. Shared by all versions (A9) |

### Progress (module Progress)

| Entity | Attributes | Notes |
|---|---|---|
| LotProgress | lot, actualStart, actualEnd | Recorded by the planner (F-PRG-1). Actuals are facts and belong to no version. The lot runs on its live placement's resource |

### Auth (module Auth)

| Entity | Attributes | Notes |
|---|---|---|
| User | id, name, email, role, isActive | Roles: Planner, Viewer, Admin |

## 3. Relationships

```mermaid
erDiagram
    ResourceType ||--o{ Resource : "has"
    Calendar ||--|{ CalendarShift : "pattern"
    Calendar ||--o{ Resource : "followed by"
    Product ||--|{ RoutingStage : "routing"
    RoutingStage }o--|| ResourceType : "needs"
    RoutingStage ||--o{ RoutingDependency : "linked by"
    Product ||--o{ Order : "ordered as"
    Order ||--|{ Operation : "generates"
    Operation ||--o{ OperationDependency : "linked by"
    Operation ||--|{ Lot : "split into"
    ScheduleVersion ||--o{ Placement : "contains"
    Lot ||--o{ Placement : "placed as (one per version)"
    Placement }o--|| Resource : "runs on"
    Resource ||--o{ Unavailability : "blocked by"
    Lot ||--o| LotProgress : "tracked by"
```

Key points:

- An operation needs a resource type; each placement picks a specific resource of that type (rule R1).
- Operations and their dependencies are copies. Editing a product's routing later does not change existing orders (A1).
- Every placement belongs to a schedule version. Rules are always checked within one version (section 5).

## 4. Calculations

Lot duration: calculated when the lot is created, from the operation's copied values:

`durationMinutes = setupMinutes + ceil(lot.quantity / unitsPerCycle) x cycleMinutes`

With the defaults (setup 0, one unit per cycle) this is `quantity x cycleMinutes`. Each lot pays its own setup. Sequence-dependent changeover (for example mold change between products, F-MD-7) is P1 and not included.

Worked example. Curing on a two-cavity press: setupMinutes 30, cycleMinutes 12, unitsPerCycle 2. Lot of 101 tires: `30 + ceil(101 / 2) x 12 = 30 + 51 x 12 = 642` minutes.

Planned end (R6): start at `plannedStart`, consume `durationMinutes` of working time from the placed resource's calendar, skipping non-working time. The result is `plannedEnd`.

Worked example. Calendar: Monday to Friday, 00:00-24:00 (three 8-hour shifts, A3). Lot duration 8 hours, start Friday 20:00.

- Friday 20:00-24:00 gives 4 hours of work.
- The weekend is non-working.
- Monday 00:00-04:00 gives the remaining 4 hours.
- `plannedEnd` = Monday 04:00.

Earliest start from dependencies (R4): for a successor lot, `max(predecessor lot plannedEnd + minLag)` over all placed lots of all predecessor operations. Lag is elapsed real time, not working time (A8).

## 5. Scheduling rules

All rules are checked among placements of one schedule version. Unavailability and progress are shared by all versions.

Rules are enforced when a placement is written. Facts recorded later (a breakdown, a calendar or holiday change) can make existing placements break R3 or R6. Those placements are kept and flagged as conflicts until the planner moves or removes them, never deleted or moved silently (`ARCHITECTURE.md` section "Persistence rules").

| ID | Rule | Features |
|---|---|---|
| R1 | A lot can only be placed on an active resource of the type its operation needs | F-SCH-1 |
| R2 | No two placements on one resource overlap. Touching is allowed (one ends at 10:00, next starts at 10:00) | F-SCH-4 |
| R3 | A placement may not overlap an unavailability of its resource, of either kind. An unavailability with no end blocks everything from its start onward | F-MNT-1, F-SCH-4 |
| R4 | For every dependency, every placed successor lot starts no earlier than every placed predecessor lot's `plannedEnd` plus `minLag`. Lots can be placed in either order; an unplaced lot imposes no constraint until it is placed | F-SCH-5 |
| R5 | Retired. Machine status no longer blocks placements; a down or maintained resource is an unavailability under R3. The ID is kept so it is not reused | none |
| R6 | `plannedEnd` is computed from the resource's working time as in section 4. The start itself must fall inside working time | F-SCH-6 |
| R7 | Placement is rejected, not silently adjusted. The error names the rule broken and the conflicting item (placement, unavailability or lot) | F-NFR-2 |
| R8 | A lot that has started (In Progress or Completed) cannot be placed, moved or unplaced (A17). A lot of a cancelled order cannot be placed | F-ORD-2 |

Worked examples for every rule are acceptance criteria, in [acceptance-criteria.md](acceptance-criteria.md) section 4. That file is their only copy.

## 6. Lifecycles and derived status

Statuses are derived, never stored. A stored status could not be correct for every schedule version, and it would duplicate placements and progress.

Lot status (first matching row wins):

| Status | Condition |
|---|---|
| Cancelled | The order is cancelled and the lot has not started |
| Completed | Progress has `actualEnd` |
| In Progress | Progress has `actualStart` and no `actualEnd`. A start can only be recorded for a lot placed in the live version |
| Scheduled | The lot has a placement in the version being viewed (live by default) |
| Unscheduled | Otherwise |

Operation status follows the same rules over its lots: Completed when all lots are completed, In Progress when any lot has started, Scheduled when all lots are placed, Partly Scheduled when some are, otherwise Unscheduled. With one lot per operation (v1), Partly Scheduled does not occur.

Order status (F-ORD-2):

| Status | Condition |
|---|---|
| Cancelled | `cancelledAt` is set. Allowed only while no lot is In Progress |
| Completed | All lots completed |
| In Progress | At least one lot started, not all completed |
| Scheduled | At least one lot placed, none started |
| Pending | No lot placed |

Resource display status (replaces the stored machine status, F-MNT-2):

| Status | Condition at the current time |
|---|---|
| Down | A breakdown unavailability covers now |
| Maintenance | A planned unavailability covers now |
| Running | A lot on this resource is In Progress |
| Idle | Otherwise |

Marking a machine down means creating a breakdown unavailability starting now, with an estimated end if known. Bringing it back means setting the end.

Late is a flag, not a state (decided). An order can be Scheduled and late, or In Progress and late. It is computed at query time for a given version, never stored. Cancelled orders are never late. Due date ends at the end of its plant-local day (`ARCHITECTURE.md` section "Time rules").

- Unfinished order: `isLate` when now is after the due date, or any lot's `plannedEnd` in the version is after it.
- Completed order: `isLate` when the last lot's `actualEnd` is after the due date.

The dashboard (F-RPT-1) counts it without a state clash. The order list needs a separate "late" filter. F-SCH-10, F-RPT-3 (P1) and the scenario comparison (F-SCH-12, P2) reuse it.

Order progress (F-PRG-2) = completed quantity / total quantity over the lots of all operations, so lots of different sizes count fairly. With one lot per operation it equals completed operations / total operations.

## 7. Assumptions

| ID | Assumption |
|---|---|
| A1 | Routing is fixed per product. Orders copy its stages, dependencies and duration values into operations at creation, so later routing edits do not affect existing orders. Per-order alteration can be added later without changing the copy approach. Editing an order's product or quantity is allowed only while none of its lots is placed in the live version or started; operations and lots are then regenerated |
| A2 | Times are stored in minutes-precision UTC and shown in one configured plant time zone. Calendar times are wall-clock time in that zone, so DST days are 23 or 25 hours long (BQ1). Planners pick starts on a 15-minute grid in the UI (UI-only, the model does not depend on it). `plannedEnd` is stored and recomputed when a calendar or the holidays change |
| A3 | One calendar in v1, the plant default: Monday to Friday, three 8-hour shifts. Holidays apply to every calendar. Every resource references a calendar, so per-resource calendars (F-MD-6, P1) are new data, not a schema change. A 24/7 plant is expressed as calendar rows. Until F-MD-6, weekend starts are rejected by R6 (BQ2) |
| A4 | Materials and molds are ignored in v1 (P2). Molds will be resources of a new kind |
| A5 | Only Planner and Admin edit; Viewer is read-only |
| A6 | A lot runs on one resource without interruption, except that it pauses across non-working time (weekends, holidays). It never runs across an unavailability: such a placement is rejected under R3. An operation can be split into several lots on different resources; v1 always creates one lot per operation, and manual splitting (F-ORD-7) is P1 |
| A7 | Order and lot quantities are positive whole numbers of tires |
| A8 | Dependency lag is elapsed real time, not working time, because the processes it models (resting, cooling) continue outside shifts |
| A9 | Unavailability is shared by all schedule versions because it is a fact about the plant, not a planning choice. Moving planned maintenance per scenario can be added later |
| A10 | v1 has exactly one live schedule version. Creating, comparing and committing scenarios is P2 (F-SCH-12). Every placement carries a version from v1 so scenarios need no schema change |
| A11 | A dependency links operations, so every successor lot waits for every predecessor lot. Overlapping stages lot by lot (transfer batches) can be added later as a dependency option |
| A12 | The planner types the order number (unique without regard to case), so it matches the numbers already used in spreadsheets. Priority is Low, Normal or High (default Normal) and only sorts and filters in v1 |
| A13 | Delete is for orders entered by mistake: allowed only while the order is Pending with no live placement and no progress, as a soft delete. Every other order is cancelled instead. Cancelling removes the placements of its lots that have not started, in every version, so they stop blocking machines |
| A14 | Master data that other rows reference cannot be deleted; a resource is retired by deactivating it. A resource with placements cannot change its type. Resource names and resource type names are unique |
| A15 | Deactivating a resource is always saved. Its placements stay and are flagged as R1 conflicts, as for other later facts (DEC-18) |
| A16 | R3 overlap is checked over the whole span from `plannedStart` to `plannedEnd`, including non-working time in which the lot pauses. Planned maintenance on a weekend therefore blocks a lot that pauses over that weekend. To confirm with the business (BQ5) |
| A17 | A started lot stays where it is in the live version: it runs on that machine, and its actuals are recorded against that placement. Completed lots keep their placement as the record of where they ran |
| A18 | The plant time zone is set once in configuration and not editable in v1, because changing it would shift every calendar and stored local date. A shift never crosses midnight; a night shift across midnight is two rows |
| A19 | The order history in F-ORD-5 is the creation time and the cancellation time in v1. A full change history needs the audit trail (F-AUTH-5, P1) |
| A20 | Names and codes are stored without surrounding spaces. Resource names and resource type names are unique without regard to case, like the product code, so "Press 1" and "press 1" cannot both exist |
| A21 | Signing out ends every session of that user, on every device, because sessions are ended through the user and not one by one (DEC-26). A session ends after 12 hours without a request (setting) |
| A22 | Master data lists (resource types, resources, products) are returned whole, without paging, because a plant has tens of rows of each. Paging stays for lists that grow, such as orders |
| A23 | Text lengths: resource, resource type, calendar and holiday names up to 100 characters, product code and order number up to 50, product name and customer name up to 200, product spec up to 500. The first Admin is created with the name "Administrator" |
| A24 | The routing API names the stages of a dependency by sequence number, because a save replaces the whole routing and stage ids do not survive it. A routing saved with no stages means the product has no routing. A field error inside a list is named by its path with a position counted from 0, for example `stages[0].cycleMinutes` |
| A25 | The order number can be changed like the other order fields of AC-47. A deleted order frees its order number, so an order entered by mistake can be entered again under the same number |
| A26 | The shifts of a calendar are read and saved as one weekly list, like a routing, so a save is one transaction and the form has one save button. A calendar may have no shifts on some weekdays; a weekday without shifts is non-working |
| A27 | The end of a due date is the first instant of the next plant-local day. Where a DST change skips local midnight, that is the first local time after the gap; where local midnight occurs twice, it is the first occurrence. An order is late from that instant on, and a lot that ends exactly then is on time (AC-56, AC-57) |
| A28 | The order list holds a few thousand orders at most (about a year of orders), because cancelled and completed orders stay listed. With a status or late filter the server reads every order that matches the other filters to derive their status (DEC-31), which is fast at that size. Hiding or archiving old orders waits until the list grows past it |
| A29 | In the order list, the search text and the customer filter match when the text is contained in the value, without regard to case, with `%` and `_` taken literally and surrounding spaces ignored. The status and priority filters take one or more values and match any of them. The due date range includes both ends, and either end may be left out |
| A30 | The generator presets follow the open business questions' assumptions: Monday to Friday (BQ2), curing presses with two cavities (BQ4), and synthetic lags of 60 to 240 minutes from building to curing (green tire rest) and 0 to 30 minutes from curing to inspection, other lags 0 (BQ3), so rule R4 is exercised. An answer changes preset values only |
| A31 | The loader writes only into an empty plant (no resource type, product or order), so generated rows never mix with entered ones; a run that failed halfway is repeated on a new database. It replaces the shifts of the plant default calendar |
| A32 | Without a start date the loader starts on the Monday of the current week by the UTC date, because only the engine converts to plant-local time; being a day off near midnight does not matter for generated data. The loader prints the start date, so a run can be repeated |
| A33 | Generated unavailabilities are plant-local starts with an elapsed duration. They start from 06:00 to 18:00 on the 15-minute grid, so no start falls into a DST gap of the European zones, and on one machine they are at least 60 minutes apart, so a DST change between two of them cannot make them overlap in UTC. Machine names are a type prefix and a number ("Press 1"), as in the examples of `acceptance-criteria.md` section 4 |
| A34 | "Fully loaded" (stress preset) compares the minutes the lots of each machine type need with the working time of its machines in the horizon, taken from the engine's working periods (R6), so a DST day counts its elapsed minutes and holidays count none. It ignores unavailabilities, which only make the load higher |
| A35 | A rejected placement lists one violation per conflicting item, so two overlapped placements give two R2 violations. Among R4 violations, the placed predecessor that holds the start back the most comes first and carries the earliest allowed start. The conflict of an R1 or R6 violation is the resource. A start outside working time has no planned end, so the rules that need the span (R2, R3, and R4 towards placed successors) are not checked until the start is fixed; the other rules are still reported |
| A36 | `FindConflicts` checks the stored planned ends, which are recomputed before it runs (A2). It leaves out completed lots, because their placement is the record of where they ran (A17) and a later fact such as a deactivation must not flag history. In Progress lots are checked, so a breakdown under a running lot is shown |
| A37 | An unavailability is a fact about a machine, so it can be recorded for an inactive machine, can start in the past, and may overlap another unavailability of the same machine (a breakdown during planned maintenance). Its times are whole minutes (A2), its reason is optional text of up to 200 characters, and it stays on the machine it was recorded for; a wrong machine is fixed by deleting it and recording it again. Deleting is for entries made by mistake; a maintenance history is P2 (F-MNT-6) |
| A38 | The API sends and receives UTC, so the UI converts the plant-local times a planner types to UTC, and UTC to plant time for display (DEC-40). A typed local time that occurs twice is its first occurrence, and one inside a DST gap is the first time after the gap, the same reading as the end of a due date (A27) |
| A39 | The unscheduled-lots queue holds only the open lots that wait for placement, which planners place within days or weeks, so it is returned whole like the master data lists (A22). It is derived from the lots of every order, so its cost grows with the order history, which A28 bounds to a few thousand orders. |
| A40 | A lot has at most one placement per schedule version, so placing a placed lot again moves that placement, which keeps its id. A placement start is a whole minute (A2); the 15-minute grid is the UI's only. The placement form lists only the active machines of the lot's resource type, but the API takes any machine and rejects a wrong one under R1, because the rules live on the server |
| A41 | Until scenarios (F-SCH-12, P2), the live version is the only version the application creates; it comes with the migration that adds the version table. A version path also takes a version's id, so scenarios need no URL change (DEC-17) |
| A42 | When the shifts or holidays change, a placement whose start is no longer in working time keeps its stored planned end, because no new end exists (A35), and `FindConflicts` lists it under R6 (AC-108) until the planner moves it. The ends of completed lots are not recomputed, because their placement is the record of where they ran (A36) |
| A43 | An inactive machine appears on the timeline only when it holds a placement in the shown range, which is then an R1 conflict (A15); one with nothing in the range has nothing to show. Its unavailabilities are shown with it |
| A44 | The timeline shows one day or two weeks, and two weeks start on the Monday of the current plant week by default. The API takes up to 31 days, which covers the F-NFR-5 size (about 25 machines over 2 weeks) and a month, and keeps the answer small, and only days in the years 2000 to 2999, which a plant schedule never leaves, so the day arithmetic never reaches the ends of the calendar |
| A45 | The earliest-slot search tries whole minutes only, because a placement start is one (A40): a `notBefore` with seconds counts from the next whole minute. The horizon counts from there, and a slot must start before the horizon ends; it may end after it |
| A46 | The proposal review previews the two weeks from the plant day of the first lot's start, which holds an order of a few lots; a bar after them is not drawn, and the table still lists it. It marks no conflicts, which the schedule page shows. The "lag 2h" notes and the [Open lot] link of `wireframes.md` section 4 are left out, because no read gives the lag a lot waited for and there is no lot page |

## 8. Out of the model for v1

Customer entity, sequence-dependent changeover, machine-product eligibility, per-resource calendar exceptions and overtime, manual lot splitting, non-machine resources (molds, crews), scenario creation and commit, locks, audit trail, quantity produced and scrap, materials. Each is P1 or later. The model has a place for each: `Order.customerName` becomes a foreign key, `Resource.calendar` takes other calendars, `ResourceType.kind` takes new kinds, operations already hold 1..n lots, and placements already carry a version.
