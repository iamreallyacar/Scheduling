# Audit

Maintainability reviews of the code. The first was done on 2026-11-18 after task M2-7; the second on 2026-11-18 after task M3-6, before M4 and M5 are built. Both look only at maintainability and code practice, not at correctness. Findings keep their number for good: completed findings are listed first, open findings are numbered by impact, highest first. Each open finding says what the problem is, where it is, why it matters, what to do about it, and the task that fixes it (TASKS.md); a finding moves to Completed when its task is Done.

## Completed

Each completed finding was fixed by a task; the decision it names gives the reasons and the resulting rules.

| Item | Finding | Fixed by |
|---|---|---|
| 1 | Row locks and transactions were hand-written per class, and M3 needed them across modules | Task M2-8, DEC-33 |
| 2 | Order, operation and lot fields were copied by hand through many types, as positional records | Task M2-9, DEC-34 |
| 3 | Request validation had to be switched on by hand for every endpoint | Task M2-10, DEC-35 |
| 4 | Frontend cache refreshes were decided separately in every form | Task M2-11, DEC-36 |
| 5 | The frontend endpoint list was written by hand, so the method and response type of a call were not checked against the OpenAPI document | Task M2-12, DEC-37 |
| 6 | API test data builders lived in test classes and were copied between them | Task M2-13, DEC-38 |
| 7 | The engine had no rule check a search could call per candidate | Task M3-7, DEC-47 |
| 8 | Each caller of the snapshot loader had its own hand-written loading scope | Task M3-8, DEC-48 |
| 9 | Queries readers loaded every order and every machine to look up a few lots and machine names | Task M3-10, DEC-50 |
| 10 | Master data was turned into engine calendars in two layers, and the placement and unavailability projections were copied | Task M3-9, DEC-49 |
| 11 | The timeline chart took the page's filter state and repeated its filter rule | Task M3-11, DEC-51 |
| 12 | The schedule page component held every piece of page state | Task M4-2, DEC-57 |
| 13 | Backend test classes copied the readers of answers and their factory setup | Task M3-13, DEC-53 |
| 15 | Every frontend form repeated the same save frame | Task M4-2, DEC-57 |
| 16 | The schedule page treated a conflict list that had not loaded as an empty one, and two selects fell back to "no choice" for a value they never offer | Task M3-12, DEC-52 |
| 17 | The whole-minute check of request times was written twice | Task M3-14, DEC-54 |
| 18 | Test plant figures were implicit and test times were written in several ways | Task M3-13, DEC-53 |
| 19 | The engine had no mapping from a generated plant to a `ScheduleSnapshot` for the property tests | Task M4-1, DEC-56 |
| 20 | Each rule violation named its rule twice, as a bare string and inside its code | Task M3-14, DEC-54 |
| 22 | The progress fakes and failing stubs were spread over the test classes | Task M3-13, DEC-53 |
| 23 | The lot title, the day length and the date locale were each written in several frontend files | Task M3-15 |
| 24 | Frontend tests found table rows and sent bodies through casts | Task M3-15 |
| 25 | The unchecked cast of `response.json()` in `request.ts` had to say why; the comment was already in place when M3-15 started | Task M3-15 |
| 26 | `check_docs.py` required a criteria section only for the M1 and M2 features | Task M3-16 |

## Second audit

The code was reviewed in four parts: the API project, the engine with the plant generator and loader, the backend tests, and the frontend. Every location below was checked by hand.

The M3 code follows the rules the first audit set up. Records use named required properties, transactions and locks go through `RequestTransaction` and the lock ports, modules talk only through ports, no code reads the system clock, every frontend read uses `queryKeys` and the generated endpoints, the frontend decides no rule, and there are no unexplained escape hatches. The open findings are about what M4 and M5 will build on: the engine's entry points, how engine inputs and lot context are loaded, the schedule page, and the test setup that every new test class copies.

### Open

#### 14. The plant time zone is read in two ways with different failure behaviour

- Where: `frontend/src/calendar/calendarQueries.ts` lines 15 to 17 (`usePlantTimeZone`, undefined while loading), `frontend/src/calendar/PlantTime.tsx` lines 6 to 9 (renders nothing without it), `frontend/src/calendar/WithPlantTimeZone.tsx` lines 6 to 18 (throws when there is no calendar); `WithPlantTimeZone` again inside `PlacementForm.tsx` and `ConflictList.tsx`, both below the one in `SchedulePage.tsx`.
- Problem: the same fact, "the first calendar's zone", has two readers. One silently shows nothing, which breaks the rule against silent defaults, and the other throws. On the schedule page the zone is passed down as a prop and also fetched again through nested wrappers.
- Why it matters: the M4 proposal screen and the M5 dashboard need the zone too and would add a third way.
- What to do: read the zone once, near the app shell, into a small context with a `usePlantTimeZone()` that throws when it is missing. Remove the render-prop wrapper and the prop chain.
- Task: M4-4, as its first step.

### Minor

Small findings, worth fixing when the file is next touched.

| Item | Finding | Where | Task |
|---|---|---|---|
| 21 | The generator's load figure adds shift minutes itself instead of using `WorkingPeriods`, so it ignores daylight saving time and would not follow a change to the working-time rule | `backend/src/TireScheduler.PlantGenerator/PlantLoad.cs` lines 32 to 45 | M4-5, as its first step |

### Looked at and left out

- `PlannedEndRecompute` and the conflict list read every placement of every version. This is a matter of performance rather than maintainability, and today there is only the live version (A41); look at it again when scenarios add versions.
- Tests that check part of an error message, and the one long placement test that covers three criteria: tolerable at today's size.
- The mapping between `StartedLotState` and the engine's `LotProgressState`: two states, and the parallel types are what DEC-16 asks for.
