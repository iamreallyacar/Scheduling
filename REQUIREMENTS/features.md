# Features: Tire Production Scheduler

Status: DRAFT. Persona: Production Planner. Scenario: single-plant tire manufacturer (see README.md, section "Business problem").

## 1. How to read this

- Section 2 lists the features a fully functioning planner tool needs, grouped by area, each with an ID (`F-<area>-<n>`) and a priority.
- Priorities:
  - P0 (v1): without it the planner cannot build and maintain a schedule. Nothing else works.
  - P1 (v1.x): makes the schedule trustworthy and efficient for daily use.
  - P2 (v2): optimisation and integration.
  - P3 (later): nice to have.
- Out of scope for the planner persona (other personas, later docs): supervisor shop-floor entry, sales quoting portal, plant-manager analytics.
- Terms (operation, lot, resource, unavailability, schedule version) are defined in `domain-model.md` section 1. The UI may call a lot a "job".

## 2. Feature catalogue and priority

### 2.1 Access and users

| ID | Feature | Pri |
|---|---|---|
| F-AUTH-1 | Sign in/out with account credentials | P0 |
| F-AUTH-2 | Roles: Planner (full edit), Viewer (read-only), Admin (users and master data) | P0 |
| F-AUTH-3 | Admin manages user accounts | P1 |
| F-AUTH-4 | Single sign-on (Google/Microsoft) | P2 |
| F-AUTH-5 | Audit trail of who changed what | P1 |

### 2.2 Master data

| ID | Feature | Pri |
|---|---|---|
| F-MD-1 | Machine register: name, type, calendar, active flag. Status is shown, not entered (F-MNT-2) | P0 |
| F-MD-2 | Product (tire model) register: code, name, size/spec | P0 |
| F-MD-3 | Routing per product: stages, each with resource type, setup minutes, cycle minutes and units per cycle; dependencies between stages with a minimum lag (default: linear chain, no lag) | P0 |
| F-MD-4 | Customer register | P1 |
| F-MD-5 | Plant calendar (v1: one shift pattern used by all machines) plus a plant holiday list | P0 |
| F-MD-6 | Per-machine calendars and exceptions such as weekend overtime | P1 |
| F-MD-7 | Sequence-dependent changeover times between products (for example mold change) | P1 |
| F-MD-8 | Machine-product eligibility and machine-specific rates (which machines can run which products, how fast) | P1 |
| F-MD-9 | Bulk import and export of master data (CSV) | P1 |

### 2.3 Orders

| ID | Feature | Pri |
|---|---|---|
| F-ORD-1 | Create/edit/cancel production order: customer, product, quantity, due date, priority | P0 |
| F-ORD-2 | Order lifecycle states: Pending, Scheduled, In Progress, Completed, Cancelled, plus a derived Late flag (not a state, see `domain-model.md` section 6) | P0 |
| F-ORD-3 | Auto-generate operations and lots from the product routing when an order is created (one lot per operation) | P0 |
| F-ORD-4 | Order list with search, filter (status, late, customer, due date, priority) and sort | P0 |
| F-ORD-5 | Order detail view: operations, lots, schedule, progress, history (v1: created and cancelled times, A19) | P0 |
| F-ORD-6 | Rush/expedite flag with reason | P1 |
| F-ORD-7 | Split an operation into several lots, to run on several machines at once | P1 |
| F-ORD-8 | Order notes/attachments | P2 |
| F-ORD-9 | Order import and export (CSV) | P1 |
| F-ORD-10 | Material availability check on order (see 2.7) | P2 |

### 2.4 Scheduling (core)

| ID | Feature | Pri |
|---|---|---|
| F-SCH-1 | Assign a lot to a machine and a start time (v1: form/dialog) | P0 |
| F-SCH-2 | Gantt/timeline view: machines on one axis, time on the other, lots as bars, conflicted placements flagged (v1: read-only) | P0 |
| F-SCH-3 | Drag-and-drop move/resize of lots, with the schedule saved (v1 uses form-based assignment instead, see F-SCH-1) | P1 |
| F-SCH-4 | Conflict detection: reject invalid placements (double-booking, unavailability, outside working time), and flag existing placements made invalid by later changes such as a breakdown | P0 |
| F-SCH-5 | Enforce stage dependencies within an order, including minimum lag, whichever stage is placed first | P0 |
| F-SCH-6 | Respect the machine's calendar and holidays when computing lot end times | P0 |
| F-SCH-7 | Unscheduled-lots queue (lots awaiting placement) | P0 |
| F-SCH-8 | Lock/pin a lot so re-planning does not move it | P1 |
| F-SCH-9 | Include sequence-dependent changeover time in lot duration | P1 |
| F-SCH-10 | Late-order warning when scheduled end is after due date | P1 |
| F-SCH-11 | Suggest the earliest feasible slot for a lot, on one chosen machine or on any eligible machine | P0 |
| F-SCH-12 | What-if scenario: try a change (for example rush order) in a copy of the schedule and see displaced and late orders before committing | P2 |
| F-SCH-13 | Auto-schedule many orders by priority/due date (rule-based) | P2 |
| F-SCH-14 | Optimisation (minimise lateness/changeovers) | P3 |
| F-SCH-15 | Publish/freeze the schedule per week, with version history | P2 |
| F-SCH-16 | Schedule an order as soon as possible: propose earliest slots for all its unplaced lots in dependency order, without moving existing placements; planner reviews and commits all at once | P0 |
| F-SCH-17 | Backward scheduling: propose the latest slots that still meet the due date | P1 |

### 2.5 Machine availability and maintenance

| ID | Feature | Pri |
|---|---|---|
| F-MNT-1 | Record unavailability per machine: planned maintenance or breakdown, with start, optional end and reason. Blocks placements in that period | P0 |
| F-MNT-2 | Show each machine's current status, derived from unavailability and progress: Down, Maintenance, Running, Idle | P0 |
| F-MNT-3 | Breakdown impact view: affected lots grouped by order, with the resulting lateness (v1 already flags them as conflicts, F-SCH-4) | P1 |
| F-MNT-4 | Reassign affected lots to alternate eligible machines | P1 |
| F-MNT-5 | Recurring maintenance schedules | P2 |
| F-MNT-6 | Maintenance history log | P2 |

### 2.6 Progress tracking

| ID | Feature | Pri |
|---|---|---|
| F-PRG-1 | Planner records actual start and end of a lot | P0 |
| F-PRG-2 | Order progress derived from lot completion | P0 |
| F-PRG-3 | Record quantity produced and scrap per lot | P1 |
| F-PRG-4 | Planned vs actual comparison per lot | P1 |
| F-PRG-5 | Automatic re-flow of downstream lots when a lot runs late | P2 |
| F-PRG-6 | Shop-floor/supervisor progress entry (separate persona) | P3 |

### 2.7 Materials (optional for plant realism)

| ID | Feature | Pri |
|---|---|---|
| F-MAT-1 | Bill of materials per product (rubber compound, steel belt, bead, etc.) | P2 |
| F-MAT-2 | Material stock levels and shortage flags against scheduled lots | P2 |
| F-MAT-3 | Molds/tooling as constrained resources (a new resource kind) | P2 |

### 2.8 Visibility and reporting

| ID | Feature | Pri |
|---|---|---|
| F-RPT-1 | Planner dashboard: orders due soon, overdue, unscheduled lots, conflicted placements, machines down | P0 |
| F-RPT-2 | Machine load/utilisation view over a date range | P1 |
| F-RPT-3 | At-risk orders list (predicted late) | P1 |
| F-RPT-4 | Printable/exportable schedule (PDF/CSV) for shift handover | P1 |
| F-RPT-5 | On-time delivery rate and schedule adherence KPIs | P2 |
| F-RPT-6 | Due-date promise lookup: "when can I deliver N units of product X?" (a dry run of F-SCH-16) | P2 |

### 2.9 Notifications

| ID | Feature | Pri |
|---|---|---|
| F-NTF-1 | In-app alerts for conflicts, late orders and machine breakdowns | P1 |
| F-NTF-2 | Email/chat notifications | P3 |

### 2.10 Platform and non-functional

| ID | Feature | Pri |
|---|---|---|
| F-NFR-1 | Data persisted reliably, with soft delete of orders | P0 |
| F-NFR-2 | Input validation and clear error messages | P0 |
| F-NFR-3 | Concurrent-edit protection (optimistic concurrency) so two planners do not overwrite each other | P1 |
| F-NFR-4 | Backup/restore procedure | P1 |
| F-NFR-5 | Schedule view usable with ~25 machines x 2 weeks without lag | P1 |
| F-NFR-6 | ERP/MES integration | P3 |

## 3. v1 scope (all P0)

A planner can: sign in; maintain machines, products, routings with dependencies, and the plant calendar; create an order and get its operations and lots generated from the routing; schedule the order as soon as possible in one step, or place single lots via a form with an "earliest slot" suggestion; see the result on a read-only timeline; be blocked from conflicts, broken dependencies and unavailable machines, and see placements that a later breakdown made invalid; record maintenance and breakdowns; record lot progress; and see a dashboard of what is due, overdue, unscheduled or conflicted.

Not in v1: drag-and-drop scheduling, multi-order auto-scheduling, what-if scenarios (the data model already supports them), materials, notifications, ERP/MES integration, other personas (supervisor, sales, plant manager).

P0 count: 26 features. The milestones that deliver them are in `TASKS.md` section "Milestones".
