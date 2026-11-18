# Scheduling engine

Inputs, operations and search of the scheduling engine. The rules it keeps are in `ARCHITECTURE.md` section "Engine rules". `LotDuration` came with task M2-3, `PlantTime` and `OrderLateness` with task M2-4 (`PlantTime.LocalToUtc` with task M3-2), `PlacementRules` with task M3-1, and `WorkingPeriods` and `PlantTime.StartOfDay` with task M3-6; `EarliestSlot` and `ScheduleOrderAsap` follow in tasks M4-1 and M4-3. Their expected behavior, including the worked examples, is in [acceptance-criteria.md](../../../REQUIREMENTS/acceptance-criteria.md) sections 4 and 5.

## Inputs

A `ScheduleSnapshot` for one schedule version, loaded by the caller:

- Calendars with their shifts (plant-local minutes of the day), plant holidays, plant time zone id.
- Resources: id, type, calendar, isActive.
- Unavailabilities: id, resource, start, end (open end allowed). Both kinds block the same way (R3).
- Placements of the version: id, lot, resource, start, stored planned end.
- Lots: id, operation, duration, resource type, progress (not started, In Progress, Completed), order cancelled flag.
- Operation dependencies with lag.

A lot or resource asked about that is not in the snapshot is an `ArgumentException`, because the caller loads the snapshot for the rows it asks about. A lot duration below one minute is an `ArgumentOutOfRangeException`; Orders computes durations with `LotDuration`, which never gives less.

## Operations

| Operation | Result |
|---|---|
| `LotDuration.Minutes(setup, cycle, unitsPerCycle, quantity)` | A lot's duration in minutes (`domain-model.md` section 4), in 64 bits so it cannot overflow. Used by Orders when it generates lots (DEC-28) |
| `PlantTime.EndOfDay(date, timeZoneId)` | The UTC instant a plant-local date ends: the first instant of the next local day, also on DST days (DEC-22, A27). An unknown zone id throws |
| `PlantTime.StartOfDay(date, timeZoneId)` | The UTC instant a plant-local date begins, the end of the day before. The timeline range starts there (DEC-45) |
| `PlantTime.LocalToUtc(localTime, timeZoneId)` | The UTC instant of a plant-local wall-clock time; a time that occurs twice gives its first occurrence and one inside a DST gap the first time after the gap (A27). The plant generator converts its unavailability starts with it (A33) |
| `OrderLateness.IsLate(facts)` | The late flag of `domain-model.md` section 6 from due date, plant time zone, now, cancelled flag, the lots' planned ends in one version and the completion time. Used by Queries (DEC-30) |
| `WorkingPeriods.Between(calendar, holidays, timeZoneId, start, end)` | The working periods of the calendar between two instants, as R6 reads them (Working time below), merged where they touch or overlap and cut at both ends. The timeline shades the time outside them (DEC-45) |
| `PlacementRules.ComputeEnd(snapshot, resource, start, duration)` | `plannedEnd` from working time (R6), or null if the start is outside working time |
| `PlacementRules.RecomputeEnds(snapshot)` | The new planned end of every placement whose end changes under the snapshot's working time. Completed lots and starts outside working time keep their stored end (A42). Used after a shift or holiday change (DEC-44) |
| `PlacementRules.Validate(snapshot, lot, resource, start)` | The planned end and every violated rule with its conflicting item, in rule order (R1-R4, R6, R8; A35). Accepted when there are none. The lot's own current placement is ignored |
| `PlacementRules.ValidateRemoval(snapshot, lot)` | The R8 violation if the lot has started; removing never breaks another rule |
| `EarliestSlot(lot, resources?, notBefore)` | On each given resource (default: every active resource of the lot's type) the earliest start that passes `Validate`. Across resources the slot with the earliest end wins; on a tie the earlier start, then the resource name. The lot's own current placement is ignored. None if nothing fits within the horizon |
| `ScheduleOrderAsap(order lots, notBefore)` | A proposal: each unplaced lot in dependency order gets its `EarliestSlot`, with earlier proposals counted as placed. Existing placements never move. Fails as a whole if any lot cannot be placed |
| `PlacementRules.FindConflicts(snapshot)` | Existing placements that now break a rule, each with its violations, checked with the stored planned ends; completed lots are left out (`ARCHITECTURE.md` section "Persistence rules", A36) |

## Working time

Each shift of a local date becomes a UTC period from the first occurrence of its start to the last occurrence of its end, so a local time that occurs twice counts both times and one inside a DST gap is skipped (`ARCHITECTURE.md` section "Time rules"). Holidays have no periods. Periods are half-open: the end of the last shift is not working time, and a planned end that falls on a shift end stays there.

## Earliest slot search

On each candidate resource the earliest feasible start is always one of these points, so only they are tried, in time order: `notBefore`, the dependency earliest start (`domain-model.md` section 4), the start of each working period, and the end of each placement and unavailability on that resource. The first that passes `Validate` wins. The horizon (default 90 days) is a setting, so the search always ends.

`notBefore` defaults to now, which the caller passes in from the injected clock.
