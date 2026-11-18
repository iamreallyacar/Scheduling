# Scheduling engine

Inputs, operations and search of the scheduling engine. The rules it keeps are in `ARCHITECTURE.md` section "Engine rules". `LotDuration` came with task M2-3, `PlantTime` and `OrderLateness` with task M2-4; the rest is built in task M3-1; its expected behavior, including the worked examples, is in [acceptance-criteria.md](../../../REQUIREMENTS/acceptance-criteria.md) sections 4 and 5.

## Inputs

A snapshot for one schedule version:

- Calendars with their shifts, plant holidays, plant time zone id.
- Resources: id, type, calendar, isActive.
- Unavailabilities: resource, start, end (open end allowed), kind.
- Placements of the version: lot, resource, start, end.
- Lots: id, operation, duration, resource type, started flag, completed flag, order cancelled flag.
- Operation dependencies with lag.

## Operations

| Operation | Result |
|---|---|
| `LotDuration.Minutes(setup, cycle, unitsPerCycle, quantity)` | A lot's duration in minutes (`domain-model.md` section 4), in 64 bits so it cannot overflow. Used by Orders when it generates lots (DEC-28) |
| `PlantTime.EndOfDay(date, timeZoneId)` | The UTC instant a plant-local date ends: the first instant of the next local day, also on DST days (DEC-22, A27). An unknown zone id throws |
| `OrderLateness.IsLate(facts)` | The late flag of `domain-model.md` section 6 from due date, plant time zone, now, cancelled flag, the lots' planned ends in one version and the completion time. Used by Queries (DEC-30) |
| `ComputeEnd(resource, start, duration)` | `plannedEnd` from working time (R6), or a violation if the start is outside working time |
| `Validate(lot, resource, start)` | OK with `plannedEnd`, or every violated rule with its conflicting item (R1-R4, R6-R8) |
| `EarliestSlot(lot, resources?, notBefore)` | On each given resource (default: every active resource of the lot's type) the earliest start that passes `Validate`. Across resources the slot with the earliest end wins; on a tie the earlier start, then the resource name. The lot's own current placement is ignored. None if nothing fits within the horizon |
| `ScheduleOrderAsap(order lots, notBefore)` | A proposal: each unplaced lot in dependency order gets its `EarliestSlot`, with earlier proposals counted as placed. Existing placements never move. Fails as a whole if any lot cannot be placed |
| `FindConflicts(snapshot)` | Existing placements that now break a rule (`ARCHITECTURE.md` section "Persistence rules") |

## Earliest slot search

On each candidate resource the earliest feasible start is always one of these points, so only they are tried, in time order: `notBefore`, the dependency earliest start (`domain-model.md` section 4), the start of each working period, and the end of each placement and unavailability on that resource. The first that passes `Validate` wins. The horizon (default 90 days) is a setting, so the search always ends.

`notBefore` defaults to now, which the caller passes in from the injected clock.
