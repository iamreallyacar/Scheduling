# Features: Tire Production Scheduler

Status: DRAFT. Persona: **Production Planner**. Scenario: single-plant tire manufacturer (see `business-problem.md`).

## 1. How to read this

- Part A lists the features a fully functioning planner tool needs, grouped by area.
- Part B assigns priorities. Each feature has an ID (`F-<area>-<n>`).
- Priorities:
  - **P0 (v1)**: without it the planner cannot build and maintain a schedule. Nothing else works.
  - **P1 (v1.x)**: makes the schedule trustworthy and efficient for daily use.
  - **P2 (v2)**: optimisation and integration.
  - **P3 (later)**: nice to have.
- Out of scope for the planner persona (other personas, later docs): supervisor shop-floor entry, sales quoting portal, plant-manager analytics.

## 2. Assumptions (to confirm)

- One plant, ~15-25 machines, 3 shifts.
- Process routing per tire model is a fixed sequence of stages (for example extrusion, building, molding/curing, inspection). Each stage runs on a machine type.
- Planners schedule at machine/job level, with hour or shift granularity. Not minute-level.
- Planners are a small group (2-5 people). Concurrent edits are possible but rare.
- No ERP/MES integration in v1. Data is entered manually.

## 3. Feature catalogue and priority

### 3.1 Access and users
| ID | Feature | Pri |
|---|---|---|
| F-AUTH-1 | Sign in/out with account credentials | P0 |
| F-AUTH-2 | Roles: Planner (full edit), Viewer (read-only), Admin (users and master data) | P0 |
| F-AUTH-3 | Admin manages user accounts | P1 |
| F-AUTH-4 | Single sign-on (Google/Microsoft) | P2 |
| F-AUTH-5 | Audit trail of who changed what | P1 |

### 3.2 Master data
| ID | Feature | Pri |
|---|---|---|
| F-MD-1 | Machine register: name, type, status, active flag | P0 |
| F-MD-2 | Product (tire model) register: code, name, size/spec | P0 |
| F-MD-3 | Routing per product: ordered stages, each with machine type and standard time per unit | P0 |
| F-MD-4 | Customer register | P1 |
| F-MD-5 | Shift calendar (v1: one fixed shift pattern plus a plant holiday list) | P0 |
| F-MD-6 | Per-machine capacity calendar (shift pattern overrides) | P1 |
| F-MD-7 | Setup/changeover times between products (for example mold change) | P1 |
| F-MD-8 | Machine-product eligibility (which machines can run which products) | P1 |
| F-MD-9 | Bulk import of master data (CSV) | P2 |

### 3.3 Orders
| ID | Feature | Pri |
|---|---|---|
| F-ORD-1 | Create/edit/cancel production order: customer, product, quantity, due date, priority | P0 |
| F-ORD-2 | Order lifecycle states: Pending, Scheduled, In Progress, Completed, Cancelled, plus a derived Late flag (not a state, see `domain-model.md` section 6) | P0 |
| F-ORD-3 | Auto-generate jobs from product routing when an order is created | P0 |
| F-ORD-4 | Order list with search, filter (status, late, customer, due date, priority) and sort | P0 |
| F-ORD-5 | Order detail view: jobs, schedule, progress, history | P0 |
| F-ORD-6 | Rush/expedite flag with reason | P1 |
| F-ORD-7 | Split an order into partial lots or batches | P1 |
| F-ORD-8 | Order notes/attachments | P2 |
| F-ORD-9 | Order import (CSV) | P2 |
| F-ORD-10 | Material availability check on order (see 3.7) | P2 |

### 3.4 Scheduling (core)
| ID | Feature | Pri |
|---|---|---|
| F-SCH-1 | Assign a job to a machine and a start time (v1: form/dialog) | P0 |
| F-SCH-2 | Gantt/timeline view: machines on one axis, time on the other, jobs as bars (v1: read-only) | P0 |
| F-SCH-3 | Drag-and-drop move/resize of jobs, with the schedule saved (v1 uses form-based assignment instead, see F-SCH-1) | P1 |
| F-SCH-4 | Conflict detection: machine double-booking, job outside machine availability | P0 |
| F-SCH-5 | Enforce stage sequence within an order (stage 2 cannot start before stage 1 ends) | P0 |
| F-SCH-6 | Respect shift calendar and holidays when computing job end times | P0 |
| F-SCH-7 | Unscheduled-jobs queue (jobs awaiting assignment) | P0 |
| F-SCH-8 | Lock/pin a job so re-planning does not move it | P1 |
| F-SCH-9 | Include setup/changeover time in job duration | P1 |
| F-SCH-10 | Late-order warning when scheduled end is after due date | P1 |
| F-SCH-11 | Suggest earliest feasible slot for a job or order | P1 |
| F-SCH-12 | What-if scenario: try a change (for example rush order) and see displaced orders before committing | P2 |
| F-SCH-13 | Auto-schedule orders by priority/due date (rule-based) | P2 |
| F-SCH-14 | Optimisation (minimise lateness/changeovers) | P3 |
| F-SCH-15 | Publish/freeze the schedule per week, with version history | P2 |

### 3.5 Machine availability and maintenance
| ID | Feature | Pri |
|---|---|---|
| F-MNT-1 | Set a machine's status (Running, Idle, Maintenance, Down) | P0 |
| F-MNT-2 | Planned maintenance windows block scheduling on the machine | P0 |
| F-MNT-3 | Breakdown handling: mark a machine down and list affected jobs | P1 |
| F-MNT-4 | Reassign affected jobs to alternate eligible machines | P1 |
| F-MNT-5 | Recurring maintenance schedules | P2 |
| F-MNT-6 | Maintenance history log | P2 |

### 3.6 Progress tracking
| ID | Feature | Pri |
|---|---|---|
| F-PRG-1 | Planner updates job status and actual start/end | P0 |
| F-PRG-2 | Order progress derived from job completion | P0 |
| F-PRG-3 | Record quantity produced and scrap per job | P1 |
| F-PRG-4 | Planned vs actual comparison per job | P1 |
| F-PRG-5 | Automatic re-flow of downstream jobs when a job runs late | P2 |
| F-PRG-6 | Shop-floor/supervisor progress entry (separate persona) | P3 |

### 3.7 Materials (optional for plant realism)
| ID | Feature | Pri |
|---|---|---|
| F-MAT-1 | Bill of materials per product (rubber compound, steel belt, bead, etc.) | P2 |
| F-MAT-2 | Material stock levels and shortage flags against scheduled jobs | P2 |
| F-MAT-3 | Mold/tooling availability as a constrained resource | P2 |

### 3.8 Visibility and reporting
| ID | Feature | Pri |
|---|---|---|
| F-RPT-1 | Planner dashboard: orders due soon, overdue, unscheduled count, machines down | P0 |
| F-RPT-2 | Machine load/utilisation view over a date range | P1 |
| F-RPT-3 | At-risk orders list (predicted late) | P1 |
| F-RPT-4 | Printable/exportable schedule (PDF/CSV) for shift handover | P1 |
| F-RPT-5 | On-time delivery rate and schedule adherence KPIs | P2 |
| F-RPT-6 | Due-date promise lookup: "when can I deliver N units of product X?" | P2 |

### 3.9 Notifications
| ID | Feature | Pri |
|---|---|---|
| F-NTF-1 | In-app alerts for conflicts, late orders and machine breakdowns | P1 |
| F-NTF-2 | Email/chat notifications | P3 |

### 3.10 Platform and non-functional
| ID | Feature | Pri |
|---|---|---|
| F-NFR-1 | Data persisted reliably, with soft delete of orders | P0 |
| F-NFR-2 | Input validation and clear error messages | P0 |
| F-NFR-3 | Concurrent-edit protection (optimistic concurrency) so two planners do not overwrite each other | P1 |
| F-NFR-4 | Backup/restore procedure | P1 |
| F-NFR-5 | Schedule view usable with ~25 machines x 2 weeks without lag | P1 |
| F-NFR-6 | ERP/MES integration | P3 |

## 4. v1 scope (all P0)

A planner can: sign in; maintain machines, products, routings and the shift calendar; create an order and get its jobs generated from the routing; place jobs on machines via a form and see them on a read-only timeline; be blocked from conflicts, wrong stage order and maintenance windows; record job progress; and see a dashboard of what is due, overdue or unscheduled.

P0 count: 24 features, after two cuts: F-SCH-3 (drag-and-drop) moved to P1, and F-MD-5 simplified to a fixed shift pattern.

### Milestones

Each milestone ends with something usable.

| Milestone | Outcome | Features |
|---|---|---|
| M1 Foundation | Planner signs in and maintains the plant's master data | F-AUTH-1, F-AUTH-2, F-NFR-1, F-NFR-2, F-MD-1, F-MD-2 |
| M2 Orders | Planner enters orders and gets jobs generated | F-MD-3, F-MD-5, F-ORD-1, F-ORD-2, F-ORD-3, F-ORD-4, F-ORD-5 |
| M3 Scheduling | Planner places jobs on machines and is blocked from invalid placements | F-SCH-7, F-SCH-1, F-SCH-2, F-SCH-5, F-SCH-6, F-SCH-4, F-MNT-1, F-MNT-2 |
| M4 Execution | Planner tracks progress and sees what needs attention | F-PRG-1, F-PRG-2, F-RPT-1 |

Notes:
- F-SCH-4, F-SCH-5 and F-SCH-6 form the scheduling rules cluster, the riskiest part. Specify it with acceptance criteria and worked examples before M3 starts.
- M1 and M2 together give a working order-entry tool. M3 is where the app becomes a scheduler.

## 5. Open questions

1. ~~Is P0 too large?~~ Resolved: decomposed into four milestones and two cuts (section 4).
2. ~~Are routings really fixed per product, or can planners alter them per order?~~ Assumed fixed (A1 in `domain-model.md`).
3. ~~Is minute-level scheduling needed, or are shift-level slots enough?~~ Assumed minute precision in the model, 15-minute grid in the UI (A2).
4. ~~Is material and mold availability a real constraint, or ignorable at first?~~ Assumed ignorable in v1 (A4).
5. ~~Is the planner the only editor in v1, with other roles read-only?~~ Assumed: Planner and Admin edit, Viewer is read-only (A5).
6. ~~Does the plant run 24/7 or follow a weekday pattern with weekend overtime?~~ Assumed Monday to Friday (A3). Still to confirm with the business, along with DST (`domain-model.md` section 9).

## 6. Next documents (proposed)

Project brief, then personas/user stories for the planner, then v1 requirements with acceptance criteria, then a domain model, then architecture.
