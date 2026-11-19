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
| 10 | Master data was turned into engine calendars in two layers, and the placement and unavailability projections were copied | Task M3-9, DEC-49 |

## Second audit

The code was reviewed in four parts: the API project, the engine with the plant generator and loader, the backend tests, and the frontend. Every location below was checked by hand.

The M3 code follows the rules the first audit set up. Records use named required properties, transactions and locks go through `RequestTransaction` and the lock ports, modules talk only through ports, no code reads the system clock, every frontend read uses `queryKeys` and the generated endpoints, the frontend decides no rule, and there are no unexplained escape hatches. The open findings are about what M4 and M5 will build on: the engine's entry points, how engine inputs and lot context are loaded, the schedule page, and the test setup that every new test class copies.

### Open

#### 9. Queries readers load every order and every machine to look up a few rows

- Where: the same lot lookup block in `backend/src/TireScheduler.Api/Queries/Schedule/TimelineReader.cs` lines 79 to 81 and `ConflictedPlacementReader.cs` lines 20 to 22, the same flatten in `UnscheduledLotReader.cs` line 15, all on `IOrderReader.ListAllDetailsAsync` (`Modules/Orders/OrderReader.cs` line 43). The resource names come from `ListResourcesAsync(null, null)` in `ConflictedPlacementReader.cs` line 24 and `Queries/Orders/OrderViewReader.cs` line 135.
- Problem: to find the order, stage and lot of a handful of placements, each reader loads all orders with their operations, lots and dependencies, then builds the same tuple dictionary by hand. Machine names are found the same way, from the full machine list.
- Why it matters: the M4 proposal review and the M5 dashboard both need "lot id to order, stage and lot" and machine names. Each new reader would copy the block again and load whole tables on every request.
- What to do: add `IOrderReader.FindLotContextsAsync(lotIds)` that returns a lot context (order, operation, lot) for those lots only, and `IMasterDataReader.GetResourceNamesAsync(ids)` modelled on `GetResourceTypesAsync`. Use them in the three readers. The unscheduled queue really needs every order and can keep `ListAllDetailsAsync`.
- Task: M3-10.

#### 11. The timeline chart takes the page's filter state and repeats its filter rule

- Where: `frontend/src/schedule/SchedulePage.tsx` lines 105 and 106 (`isDrawn`), `frontend/src/schedule/TimelineChart.tsx` line 36 (`shownTypes`) and line 79 (`shownPlacements`); the chart's props at lines 6 to 18.
- Problem: which bars are shown is decided twice, once by the page for the popover and once by the chart for drawing. The chart's props are the page's filters (`resourceTypeId`, `onlyConflicted`, `conflictedPlacementIds`) and selection. Its row component takes all of the chart's props, through a spread.
- Why it matters: M4-4 plans to reuse `TimelineChart` to preview a proposal. That screen would have to pass dummy filter props, or the chart would gain more flags. If the two filters drift, a bar is drawn without a popover, or the other way round.
- What to do: filter once in the page (with `barsOf` and `TimelineBar`, which already exist) and pass the chart a filtered `TimelineResponse` plus the conflicted ids it marks. Give the row component only the props it uses.
- Task: M3-11.

#### 12. The schedule page component holds every piece of page state

- Where: `frontend/src/schedule/SchedulePage.tsx` lines 87 to 189 (`ScheduleContent`); `PlacementTarget` is built in `SchedulePage.tsx` line 110 (`moveTargetOf`), `UnscheduledLotQueue.tsx` line 48 and the move button of `ConflictList.tsx`.
- Problem: one component owns the open form, the selected bar, both filters, the queue toggle, the link from a violation to its bar and the mapping from a bar to a placement target, and renders everything in one 70-line block.
- Why it matters: M4-2 adds "Find earliest slot" and M4-4 a proposal review, each with more selection and open-form state. If nothing changes, they land in this component too.
- What to do: move the timeline part (filters, selection, popover) into its own component or hook, so the page keeps the layout and the open form. Add one `placementTargetOf` function next to `PlacementTarget` for the three places that build one. Doing finding 11 first makes this smaller.
- Task: M4-2, as its first step.

#### 13. Backend test classes copy JSON read helpers and their factory setup

- Where: `Lot(order, lotId)` in `backend/tests/TireScheduler.Api.Tests/Scheduling/PlacementTests.cs` line 418, with inline copies in `Scheduling/ConflictTests.cs` line 177 and `Orders/OrderLifecycleTests.cs` line 113. `ListedOrder` in `PlacementTests.cs` line 423 and `Queries/OrderStatusTests.cs` line 79. The same field setup, `InitializeAsync` and `DisposeAsync` in `PlacementTests.cs`, `ConflictTests.cs`, `Queries/TimelineTests.cs`, `Queries/MachineStatusTests.cs`, `Queries/OrderStatusTests.cs` and `Orders/OrderLifecycleTests.cs`, each with a differently named start time (`Now`, `CreationTime`, `LastMinuteOfDueDay`, an inline date), while `Scheduling/UnavailabilityTests.cs` uses `SchedulerApiFixture` instead.
- Problem: DEC-38 moved the data builders into `TestData`, but the M3 classes went back to copying the helpers that read answers, and every class repeats about 15 lines to create its factory.
- Why it matters: M5 adds progress fields to the lot answer and replaces `FakeLotActivity`, so the same edit would be made in four to six places. Every M4 and M5 test class would copy the setup again.
- What to do: move `Lot`, `ListedOrder`, `QueuedLotIds` and `PlacementValues` next to `LotIds` in `TestData`. Give the per-class setup one home, for example `SchedulerApiFixture` with optional `clock` and `replaceServices`, and use it in the classes above. Update the TestData paragraph of TESTING.md.
- Task: M3-13.

#### 14. The plant time zone is read in two ways with different failure behaviour

- Where: `frontend/src/calendar/calendarQueries.ts` lines 15 to 17 (`usePlantTimeZone`, undefined while loading), `frontend/src/calendar/PlantTime.tsx` lines 6 to 9 (renders nothing without it), `frontend/src/calendar/WithPlantTimeZone.tsx` lines 6 to 18 (throws when there is no calendar); `WithPlantTimeZone` again inside `PlacementForm.tsx` and `ConflictList.tsx`, both below the one in `SchedulePage.tsx`.
- Problem: the same fact, "the first calendar's zone", has two readers. One silently shows nothing, which breaks the rule against silent defaults, and the other throws. On the schedule page the zone is passed down as a prop and also fetched again through nested wrappers.
- Why it matters: the M4 proposal screen and the M5 dashboard need the zone too and would add a third way.
- What to do: read the zone once, near the app shell, into a small context with a `usePlantTimeZone()` that throws when it is missing. Remove the render-prop wrapper and the prop chain.
- Task: M4-4, as its first step.

#### 15. Every frontend form repeats the same save frame

- Where: `MachineForm.tsx`, `MachineTypesPanel.tsx`, `UnavailabilityForm.tsx`, `ProductForm.tsx`, `HolidayForm.tsx`, `ShiftEditor.tsx`, `OrderForm.tsx`, `PlacementForm.tsx` (all under `frontend/src`).
- Problem: each form repeats `useMutation` with `onSuccess: onClose`, `problemOf(...)`, `ProblemSummary`, the `form className="panel"` frame and the Save and Cancel buttons. Several also repeat a `NO_..._CHOSEN = ''` constant, the `x === '' ? null : Number(x)` conversion and a comment explaining it.
- Why it matters: the earliest-slot, proposal and progress forms add three more copies. A change to every form (disable while saving, a double-submit guard, focus on the first error) has to be made in eight files.
- What to do: add a small `FormPanel` component in `src/forms/` (title, problem, pending, submit, cancel, children, extra actions) and one `emptyToNull` function. Do not build a general form-state hook.
- Task: M4-2, as its first step.

#### 16. The schedule page treats unloaded conflicts as no conflicts

- Where: `frontend/src/schedule/SchedulePage.tsx` line 104 (`conflictedPlacements.data ?? []`) and line 145 (`?? []` for the popover's violations); smaller cases in `calendar/ShiftEditor.tsx` line 53 (`WEEKDAYS.find(...) ?? ''`) and `orders/OrderFilterForm.tsx` line 47.
- Problem: the timeline is drawn as soon as its own query loads. While the conflict list is still loading, or after it fails, every bar is drawn as conflict-free, the popover says "no violations" and "only conflicted" hides everything. Nothing marks which of these `??` fallbacks are intended loading placeholders.
- Why it matters: the project rule is never to replace missing values with silent defaults, and M4's proposal preview joins the same two queries (DEC-46).
- What to do: wait for both queries before drawing the chart, or show the conflict markers as unavailable when that query has no data. For the selects, narrow the value with a type guard instead of falling back to "no choice".
- Task: M3-12.

#### 17. The whole-minute check is written twice

- Where: `backend/src/TireScheduler.Api/Modules/Scheduling/PlacementRegister.cs` lines 27 to 31 (inline) and `UnavailabilityRegister.cs` lines 80 to 89 and 111 (`IsWholeMinute`), each with its own message text.
- Problem: one rule (A2, A40), two forms.
- Why it matters: the M4 earliest-slot and schedule-whole-order requests take times too and would add a third and fourth copy.
- What to do: one helper next to `RequestBodyValidation` that returns the field error, used by both registers.
- Task: M3-14.

#### 18. Test plant figures are implicit and repeated

- Where: the comment "a building lot of 10 tires takes 2 hours, a curing lot 4 hours" in `backend/tests/TireScheduler.Api.Tests/Scheduling/PlacementTests.cs`, `Scheduling/ConflictTests.cs` and `Queries/TimelineTests.cs`; the cycle times in `TestData/SchedulingTestData.cs` line 72; `OrderQuantity = 10` per class; `Berlin(day, hour)` in `SchedulingTestData.cs` line 63, which fixes UTC+1 for November, while `TimelineTests.cs` has its own `Utc` helper.
- Problem: expected ends such as `Berlin(23, 2)` are worked out by hand from numbers that have no shared name.
- Why it matters: changing the test plant breaks many expectations at once, and the cause is hard to trace. M4's search tests rely on exactly these durations.
- What to do: name the lot quantity and the lot hours per stage in `SchedulingTestData` and refer to them in the tests and comments. Use one helper for test times.
- Task: M3-13.

### Minor

Small findings, worth fixing when the file is next touched.

| Item | Finding | Where | Task |
|---|---|---|---|
| 19 | The engine's mapping from a generated plant to a `ScheduleSnapshot` does not exist yet. The M4-1 property tests need it; write it once, in the generator project or a shared test helper, not in each test file | `backend/src/TireScheduler.PlantGenerator/GeneratedPlant.cs`, `backend/src/TireScheduler.Engine/ScheduleSnapshot.cs` | M4-1, as its first step |
| 20 | Each violation names its rule twice, as a bare `"R2"` string and inside its code, so the two can disagree | `backend/src/TireScheduler.Engine/PlacementRules.cs` (every `...Violation("R..."` call), `RuleViolation.cs` | M3-14 |
| 21 | The generator's load figure adds shift minutes itself instead of using `WorkingPeriods`, so it ignores daylight saving time and would not follow a change to the working-time rule | `backend/src/TireScheduler.PlantGenerator/PlantLoad.cs` lines 32 to 45 | M4-5, as its first step |
| 22 | `FakeLotActivity` lives in `Orders/` but Scheduling tests use it, and the throwing stubs (`FailingLotProgressStates`, the failing recompute) are private to single classes; M5 replaces them and they should be easy to find | `backend/tests/TireScheduler.Api.Tests/Orders/FakeLotActivity.cs`, `Scheduling/PlacementTests.cs` line 428, `Scheduling/ConflictTests.cs` | M3-13 |
| 23 | The lot title "Order X, stage N Type, lot M" is built in two places, `DAY_MS` is defined twice, and the `en-GB` locale is written in three files | `frontend/src/schedule/PlacementForm.tsx`, `LotPopover.tsx`, `timelineRange.ts`, `TimelineChart.tsx`, `frontend/src/calendar/plantClock.ts` | M3-15 |
| 24 | Frontend tests repeat `.closest('tr') as HTMLElement` and cast the sent request bodies; `rowOf(name)` and `sentBody(requests, method, path)` helpers in `src/testing/` would remove the casts and fail clearly | `frontend/src/**/*.test.tsx` | M3-15 |
| 25 | The unchecked cast of `response.json()` has no comment giving its reason | `frontend/src/api/request.ts` line 58 | M3-15 |
| 26 | `check_docs.py` requires a criteria section only for the M1 and M2 features, named by hand, so the M3 to M5 features are not checked | `scripts/check_docs.py` function `check_criteria` | M3-16 |

### Looked at and left out

- `PlannedEndRecompute` and the conflict list read every placement of every version. This is a matter of performance rather than maintainability, and today there is only the live version (A41); look at it again when scenarios add versions.
- Tests that check part of an error message, and the one long placement test that covers three criteria: tolerable at today's size.
- The mapping between `StartedLotState` and the engine's `LotProgressState`: two states, and the parallel types are what DEC-16 asks for.
