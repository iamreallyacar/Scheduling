# Decisions

Status values: **Accepted** (in force), **Proposed** (written into the docs, waiting for review), **Open** (not decided). Settling Proposed and Open entries is task P4 in [tasks.md](tasks.md).

To change a decision, add a new entry that supersedes the old one and mark the old one Superseded. Do not edit an accepted entry's reasoning after the fact.

## Index

| ID | Decision | Status | Date | Where |
|---|---|---|---|---|
| DEC-1 | Late is a derived flag, not an order state | Accepted | 2026-11-04 (P1) | domain-model §6 |
| DEC-2 | Routing fixed per product, copied into each order | Accepted | 2026-11-04 (P1) | A1 |
| DEC-3 | UTC storage, one plant time zone, 15-minute UI grid | Accepted | 2026-11-04 (P1) | A2, architecture §5 |
| DEC-4 | One plant calendar in v1, as data | Accepted | 2026-11-04 (P1) | A3 |
| DEC-5 | Lots never run across an unavailability | Accepted | 2026-11-04 (P1) | A6, R3 |
| DEC-6 | Operation and Lot replace Job | Accepted | 2026-11-04 (D1) | domain-model §2, A6 |
| DEC-7 | Machines are resources with a type, kind and calendar | Accepted | 2026-11-04 (D1) | domain-model §2, A3, A4 |
| DEC-8 | Duration from setup, cycle time and units per cycle | Accepted | 2026-11-04 (D1) | domain-model §4 |
| DEC-9 | Unavailability replaces stored machine status | Accepted | 2026-11-04 (D2) | R3, R5, domain-model §6 |
| DEC-10 | Stage dependencies with lag, checked in either placement order | Accepted | 2026-11-04 (D2) | R4, A8, A11 |
| DEC-11 | Every placement belongs to a schedule version | Accepted | 2026-11-04 (D3) | A9, A10 |
| DEC-12 | All statuses derived, never stored | Accepted | 2026-11-04 (D3) | domain-model §6 |
| DEC-13 | Earliest slot and "schedule order ASAP" are P0 | Accepted | 2026-11-04 (D5) | features §3.4, §4 |
| DEC-14 | CSV import and export are P1 | Accepted | 2026-11-04 (D5) | F-MD-9, F-ORD-9 |
| DEC-15 | Synthetic plant generator instead of hand-written seed data | Accepted | 2026-11-04 (D6) | tasks P7 |
| DEC-16 | Upward reads between modules through ports | Proposed | 2026-11-04 (D4) | architecture §2 |
| DEC-17 | Schedule version in the URL path, `live` alias | Proposed | 2026-11-04 (D4) | architecture §4 |
| DEC-18 | Later facts flag placements instead of deleting them | Proposed | 2026-11-04 (D4) | architecture §8, domain-model §5 |
| DEC-19 | "Schedule order ASAP" as proposal, then commit | Proposed | 2026-11-04 (D4) | architecture §4 |
| DEC-20 | Stack: ASP.NET Core, EF Core, PostgreSQL, React with TypeScript | Open | | architecture §1 |
| DEC-21 | Cookie session instead of JWT | Open | | architecture §6 |
| DEC-22 | Due date ends at the end of its plant-local day | Open | | architecture §5 |
| DEC-23 | Module granularity: one project per module, or one API project with folders | Open | | architecture §1-2 |

## Accepted

### DEC-1 Late is a derived flag
- **Decision:** `isLate` is computed at query time from due date, planned ends and actual ends. It is not an order state.
- **Reason:** an order can be Scheduled and late or In Progress and late; a state would clash with the lifecycle.
- **Alternatives:** a Delayed state (original `features.md`), rejected for the clash.

### DEC-2 Routing copied into orders (A1)
- **Decision:** a routing is fixed per product. Order creation copies stages, dependencies and duration values into operations.
- **Reason:** later routing edits must not change orders already planned.
- **Alternatives:** reference the routing live, rejected because history would change under the planner.

### DEC-3 Time handling (A2)
- **Decision:** minute-precision UTC in storage, one IANA plant time zone for display and calendars, 15-minute grid in the UI only.
- **Reason:** every planner sees the same wall clock; the model stays independent of the UI grid.
- **Alternatives:** shift-level slots, rejected as too coarse for curing cycles.

### DEC-4 One plant calendar (A3)
- **Decision:** v1 has one calendar, Monday to Friday, three 8-hour shifts, plus plant holidays. Shifts are data.
- **Reason:** enough for the scenario; a 24/7 plant is a data change.
- **Alternatives:** per-machine calendars in v1, deferred to P1 (F-MD-6). DEC-7 keeps that a data change.

### DEC-5 No running across unavailability (A6)
- **Decision:** a lot may pause over non-working time but a placement that overlaps an unavailability is rejected.
- **Reason:** a job interrupted by maintenance is rarely resumable as-is; rejection keeps the rule simple.
- **Alternatives:** pause across maintenance, may be revisited with per-machine calendars.

### DEC-6 Operation and Lot replace Job
- **Decision:** an operation is one stage of one order; it holds one or more lots, each placed on its own. v1 creates one lot per operation.
- **Reason:** curing is the bottleneck and runs on many presses in parallel. One job per stage on one machine cannot express a realistic schedule, and changing it after the scheduling code exists would touch status, R4, `isLate` and the timeline.
- **Alternatives:** keep Job and add splitting later (F-ORD-7), rejected for the retrofit cost.
- **Consequences:** manual splitting (F-ORD-7, P1) becomes a UI feature. Every rule works per lot.

### DEC-7 Resources with type, kind and calendar
- **Decision:** machines are resources. A resource type has a kind (`Machine` in v1); each resource references a calendar (the plant default in v1).
- **Reason:** molds (F-MAT-3) and per-machine calendars (F-MD-6) become data, not schema changes.
- **Alternatives:** a Machine entity with a plant-wide shift pattern, rejected for the same retrofit cost.
- **Consequences:** a lot needing two resources at once (machine and mold) still needs a second placement per lot later (architecture §11).

### DEC-8 Cycle-based duration
- **Decision:** `duration = setupMinutes + ceil(quantity / unitsPerCycle) x cycleMinutes`, defaults giving `quantity x cycleMinutes`.
- **Reason:** multi-cavity curing presses make tires in batches; a per-unit time misstates them.
- **Alternatives:** minutes per unit only, rejected. Sequence-dependent changeover stays P1 (F-MD-7).

### DEC-9 Unavailability replaces machine status
- **Decision:** planned maintenance and breakdowns are time-bounded `Unavailability` rows (a breakdown may have no end). Machine status is derived for display. R5 is retired.
- **Reason:** a stored status duplicated maintenance windows and blocked a machine for all future time while it was down today.
- **Alternatives:** keep both, rejected as two sources of truth.

### DEC-10 Dependencies with lag (A8, A11)
- **Decision:** dependencies are (predecessor, successor, minLag) between operations, copied from the routing. Lag is elapsed real time. Either side may be placed first; every placed successor lot waits for every placed predecessor lot.
- **Reason:** green tires rest before curing, and planners often schedule the curing bottleneck first and work backwards. The original R4 allowed neither.
- **Alternatives:** finish-to-start in sequence order only (original R4), rejected. Lot-by-lot overlap (transfer batches) deferred.

### DEC-11 Schedule versions (A9, A10)
- **Decision:** every placement carries a schedule version. v1 has one live version and no scenario UI. Unavailability and progress are shared by all versions.
- **Reason:** what-if (F-SCH-12) is the business problem's headline question. Adding the version later would change the exclusion constraint, every query and every URL.
- **Alternatives:** live placements only, rejected for the retrofit cost. Per-version unavailability deferred.

### DEC-12 Derived statuses
- **Decision:** lot, operation, order and machine statuses are computed, never stored.
- **Reason:** a stored status cannot be correct for every schedule version (DEC-11) and duplicates placements and progress.
- **Alternatives:** Orders owns a stored `Job.status` (original architecture), rejected.
- **Consequences:** statuses live in Queries; commands needing them use ports (DEC-16).

### DEC-13 Assisted scheduling in v1
- **Decision:** earliest slot (F-SCH-11) and "schedule order ASAP" (F-SCH-16) are P0, in a new milestone M4. P0 is 26 features.
- **Reason:** placing every lot by hand (~160 per week) is slower than the spreadsheet the tool replaces. The engine already validates placements, so the search is a small addition.
- **Alternatives:** keep them P1/P2, rejected because v1 would not beat the current way of working.

### DEC-14 CSV import and export in P1
- **Decision:** F-MD-9 and F-ORD-9 move from P2 to P1 and include export.
- **Reason:** planners already hold their data in spreadsheets; manual entry is the main barrier to trying the tool.

### DEC-15 Synthetic plant generator
- **Decision:** P7 builds a deterministic, parameterised generator with presets instead of hand-written seed data.
- **Reason:** one source for seed data, demos, tests and the F-NFR-5 performance set.
- **Alternatives:** static seed files, rejected as hard to scale and keep valid.

## Proposed

Introduced while writing `architecture.md` in D4. In force in the docs; confirm or replace in P4.

### DEC-16 Upward reads through ports
- **Proposal:** a lower module declares a small read interface for a fact it needs from a higher module (for example Orders asks "has any lot started?"); the higher module implements it.
- **Reason:** keeps source dependencies pointing downward despite derived statuses.
- **Concern:** adds wiring and indirection for a small team. DEC-23 (fewer projects) would remove most of it.

### DEC-17 Version in the URL path
- **Proposal:** version-dependent endpoints sit under `/versions/{v}/...` with a `live` alias; v1 always uses `live`.
- **Reason:** scenarios need no URL change.
- **Alternatives:** a `version` query parameter, equally workable but easier to forget.

### DEC-18 Later facts flag placements
- **Proposal:** a new unavailability or calendar change is always saved. Affected placements stay, are recomputed and are flagged as conflicts until the planner acts.
- **Reason:** a breakdown is a fact and cannot be rejected; deleting or moving placements silently would contradict R7.
- **Alternatives:** reject the fact, or auto-unplace affected lots, both rejected.

### DEC-19 Proposal, then commit
- **Proposal:** "schedule order ASAP" first returns proposed placements, then commits them in one transaction after the planner confirms. The commit validates again.
- **Reason:** the planner sees the result before it changes the schedule; the same proposal call serves the promise lookup (F-RPT-6).
- **Alternatives:** place immediately with undo, rejected because undo needs the audit trail (P1).

## Open

| ID | Question | Current proposal |
|---|---|---|
| DEC-20 | Stack | ASP.NET Core Web API, EF Core, PostgreSQL, React with TypeScript (architecture §1). Consider .NET 10 (LTS) rather than the legacy .NET 9 |
| DEC-21 | Session mechanism | Cookie session with ASP.NET Core Identity, same origin (architecture §6) |
| DEC-22 | Due-date semantics | Plant-local date, due at the end of that day (architecture §5) |
| DEC-23 | Module granularity | Currently one project per module plus Queries and the engine. Alternative: the engine as its own project, everything else as folders in one API project with one schema |
