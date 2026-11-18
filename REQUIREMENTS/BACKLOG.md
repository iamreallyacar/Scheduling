# Backlog

Ideas and work that are not part of the current version and not yet planned as tasks. Feature details are in [features.md](features.md).

## Extension points for later versions

Chosen so P1 and later features do not force a rewrite:

- Per-resource calendars (F-MD-6): `Resource.calendar` already exists and the engine takes calendars as input.
- Setup and changeover (F-MD-7, F-SCH-9): fixed setup is already in the duration; sequence-dependent changeover adds an engine input.
- Lot splitting (F-ORD-7): operations already hold 1..n lots, and every rule works per lot.
- Backward scheduling (F-SCH-17): same engine search, mirrored.
- Scenarios (F-SCH-12, F-SCH-15): placements already carry a version, and URLs already contain it. Adds version creation, copy, compare and commit.
- Molds and crews (F-MAT-3): new `ResourceType.kind` values. A lot occupying two resources at once needs a second placement row per lot, the main later change.
- Audit trail (F-AUTH-5): all writes go through module interfaces, so one interception point exists.
- Optimistic concurrency (F-NFR-3): see API.md section "Conventions".
- ERP/MES integration (F-NFR-6): add an integration module that talks to the Orders interface.
