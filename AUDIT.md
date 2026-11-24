# Audit

Reviews of the code. The first was done on 2026-11-18 after task M2-7 and the second on 2026-11-18 after task M3-6, both for maintainability and code practice only. The third, on 2026-11-24 after task M5-3, looked at maintainability and, for the first time, at correctness: behaviour that breaks a rule, criterion or decision. Findings keep their number for good: completed findings are listed first, open findings are numbered by impact, highest first, maintainability before correctness. Each open finding says what the problem is, where it is, why it matters, what to do about it, and the task that fixes it (TASKS.md); a finding moves to Completed when its task is Done.

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
| 14 | The plant time zone was read in two ways with different failure behaviour, and passed down as a prop | Task M4-4 |
| 15 | Every frontend form repeated the same save frame | Task M4-2, DEC-57 |
| 16 | The schedule page treated a conflict list that had not loaded as an empty one, and two selects fell back to "no choice" for a value they never offer | Task M3-12, DEC-52 |
| 17 | The whole-minute check of request times was written twice | Task M3-14, DEC-54 |
| 18 | Test plant figures were implicit and test times were written in several ways | Task M3-13, DEC-53 |
| 19 | The engine had no mapping from a generated plant to a `ScheduleSnapshot` for the property tests | Task M4-1, DEC-56 |
| 20 | Each rule violation named its rule twice, as a bare string and inside its code | Task M3-14, DEC-54 |
| 21 | The generator's load figure added shift minutes itself instead of using the engine's working periods, so it ignored daylight saving time | Task M4-5 |
| 22 | The progress fakes and failing stubs were spread over the test classes | Task M3-13, DEC-53 |
| 23 | The lot title, the day length and the date locale were each written in several frontend files | Task M3-15 |
| 24 | Frontend tests found table rows and sent bodies through casts | Task M3-15 |
| 25 | The unchecked cast of `response.json()` in `request.ts` had to say why; the comment was already in place when M3-15 started | Task M3-15 |
| 26 | `check_docs.py` required a criteria section only for the M1 and M2 features | Task M3-16 |
| 27 | The architecture test took its module list as typed in, so a new module folder escaped every dependency rule | Task M5-4 |
| 32 | API tests read the field names of a 400 answer with a copied expression | Task M5-5 |
| 35 | Deleting a product or machine type could race an order create, a routing save or a machine write | Task M5-7, DEC-66 |
| 36 | Only the R2 race had a lock test | Task M5-6 |
| 40 | A holiday or shift save, and a second unavailability write, could answer 500 when a delete ran at the same moment | Task M5-8 |
| 28 | The frontend decided which lot actions to offer from the status, against the rule that it repeats no business rule | Task M5-20, DEC-70 |

## Second audit

The code was reviewed in four parts: the API project, the engine with the plant generator and loader, the backend tests, and the frontend. Every location below was checked by hand.

The M3 code follows the rules the first audit set up. Records use named required properties, transactions and locks go through `RequestTransaction` and the lock ports, modules talk only through ports, no code reads the system clock, every frontend read uses `queryKeys` and the generated endpoints, the frontend decides no rule, and there are no unexplained escape hatches. The open findings are about what M4 and M5 will build on: the engine's entry points, how engine inputs and lot context are loaded, the schedule page, and the test setup that every new test class copies.

### Looked at and left out

- `PlannedEndRecompute` and the conflict list read every placement of every version. This is a matter of performance rather than maintainability, and today there is only the live version (A41); look at it again when scenarios add versions.
- Tests that check part of an error message, and the one long placement test that covers three criteria: tolerable at today's size.
- The mapping between `StartedLotState` and the engine's `LotProgressState`: two states, and the parallel types are what DEC-16 asks for.

## Third audit

Eight reviewers looked at the code after M5, four for maintainability and four for correctness, in the same four parts as before: the API project, the engine with the plant generator and loader, the backend tests, and the frontend. Two more reviewers then checked every finding against the code and the docs; only the findings they confirmed are listed. Every check passed at the time (540 backend and 131 frontend tests).

The rules of the first two audits still hold: no system clock reads, no hand-written locks, no stored statuses, named required records, and modules talk through ports. The engine rules R1 to R8, the earliest-slot search, the derived statuses and the late flag held up under the correctness review. The open correctness findings are about two writes that meet at the same time, inputs with no upper bound, and a few edge cases in the frontend's time fields.

### Open

#### 29. Response fields are copied in two places where DEC-34 asks for one mapping

- Where: `backend/src/TireScheduler.Api/Queries/Orders/OrderViewReader.cs` lines 23 to 34 and 136 to 147 (the same ten order fields for `OrderResponse` and `OrderSummaryResponse`); `Modules/Scheduling/PlacementRegister.cs`, where `LotPlacementResponse` is built by hand in `PlaceAsync` and again in `CommitProposalAsync` (lines 132 to 137).
- Problem: DEC-34 keeps the answers flat with one mapping function per pair of types; these two pairs have two each. In the same file, `ListUnfinishedDueByAsync` serves only the dashboard, and the `StatusOf` parameter `livePlacements` carries the placements of any version.
- Why it matters: a new order or placement field has to be added to every copy, and a missed copy is an answer that silently lacks the field.
- What to do: one `From` mapping per response type, used by both callers; move `ListUnfinishedDueByAsync` to `Queries/Dashboard`; rename `livePlacements` to `placements`.
- Task: M5-11.

#### 30. The routing is read by two copies of the same queries

- Where: `backend/src/TireScheduler.Api/Modules/MasterData/MasterDataReader.cs` lines 22 to 44 and `Modules/MasterData/Routings/RoutingRegister.cs` lines 72 to 94.
- Problem: both read the stages and the dependencies with the same ordering (`SuccessorStage!.SequenceNo`, then `PredecessorStage!.SequenceNo`); only the target type differs.
- Why it matters: that ordering decides the order of the generated operations and of the routing answer, so a change to one copy makes orders and the routing editor disagree.
- What to do: one ordered query in MasterData that both use, each mapping to its own type.
- Task: M5-11.

#### 31. A silent default and an unexplained cast in the frontend

- Where: `frontend/src/orders/OrderForm.tsx` line 91 (`?? 'Normal'`); `frontend/src/schedule/TimelineToolbar.tsx` line 50 (`Object.keys(ZOOM_LABELS) as TimelineZoom[]`).
- Problem: the priority select turns a value it does not know into Normal, which the error rule in CLAUDE.md forbids and which item 16 removed from two other selects. The cast has no comment saying why it is safe.
- Why it matters: a priority added to the options but not to `ORDER_PRIORITIES` would be saved as Normal without a message.
- What to do: throw on an unknown value, as `ShiftEditor` does for weekdays; replace the cast with a typed list of zooms or explain it.
- Task: M5-14.

#### 33. The schedule page reads the clock twice

- Where: `frontend/src/schedule/SchedulePage.tsx` line 60 (`new Date()`) next to the `useNow` value at line 46.
- Problem: the "today" button reads the browser clock outside the hook that the rest of the page uses.
- Why it matters: the two reads can name different plant days around midnight, and tests that set the time through `useNow` do not cover the second read.
- What to do: use the `useNow` value for both.
- Task: M5-14.

#### 34. The plant generator hides part of its seed contract and builds a whole snapshot for one calendar

- Where: `backend/src/TireScheduler.PlantGenerator/DowntimeGeneration.cs` line 55; `PlantLoad.cs` lines 34 to 43 with `PlantSnapshot.cs` lines 12 to 65.
- Problem: a downtime's duration is drawn before the open-end chance even when it is not used, and nothing says the draw order is part of what a seed means (AC-151). `PlantLoad` builds the engine snapshot of every order only to read its one calendar and the holidays, and walks the demand again as `PlantSnapshot` does.
- Why it matters: a tidy-up that skips the unused draw changes every seeded plant and every test that names a seed; the calendar mapping exists only inside the whole snapshot.
- What to do: a comment at the draw; `PlantSnapshot.CalendarOf` and `HolidaysOf` used by both.
- Task: M5-12.

#### 37. Parallel wrong passwords are counted once and answer 500

- Where: `backend/src/TireScheduler.Api/Modules/Auth/PasswordSignIn.cs` lines 46 to 66.
- Problem: the failure count is read, raised in memory and saved through `UserManager.UpdateAsync`. With the concurrency stamp, parallel attempts fail to save, and `SaveAsync` then throws.
- Why it matters: of N wrong passwords sent at once, one counts and the others answer 500, so the lockout of AC-3 allows many more guesses than its threshold, and AC-25 errors appear for a normal request.
- What to do: raise the count atomically in one statement, or read the user again and retry on a concurrency failure.
- Task: M5-9.

#### 38. A time field the parser rejects can blank the page

- Where: `frontend/src/calendar/plantClock.ts` lines 70 to 74 (only a four-digit year is read); `frontend/src/schedule/EarliestSlotFinder.tsx` lines 32 to 34 convert during render; the submit handlers of `PlacementForm.tsx` line 40, `LotProgressForm.tsx` lines 35 and 36, `UnavailabilityForm.tsx` lines 32 and 33 and `AsapProposalReview.tsx` line 46. There is no error boundary in `frontend/src`.
- Problem: a `datetime-local` value with a longer year, which browsers can let a user type, throws. In the earliest-slot finder it throws during render, which unmounts the whole app; in the forms the save never starts and nothing is shown.
- Why it matters: the planner loses the form and its input, against AC-26.
- What to do: convert inside the search and the submit handlers and show a field error for text that does not parse; add an error boundary in the app shell.
- Task: M5-15.

#### 39. Lot durations and routing sizes have no upper bound

- Where: `backend/src/TireScheduler.Engine/WorkingTime.cs` lines 39 to 61 (`EndAfter` walks day by day), with the cap of `int.MaxValue` minutes in `Modules/Orders/OperationGenerator.cs` (AC-145); `Modules/MasterData/Routings/RoutingRules.cs` lines 57 to 82 (recursive cycle check) with no limit on `Stages` in `RoutingContracts.cs`.
- Problem: a lot of millions of minutes makes every candidate of a search walk years of days, and on a sparse calendar the walk passes the year 9999 and throws. A routing with tens of thousands of chained stages overflows the stack in the cycle check.
- Why it matters: the first answers 500 instead of a 422 rejection (R7) and holds a request thread for long; the second ends the API process, which no handler can catch (an Admin request).
- What to do: add criteria for the largest lot duration and the largest number of stages, then reject larger ones with 400 and document them in API.md.
- Task: M5-10, after WQ5.

#### 41. An order list page past the last one says there are no orders

- Where: `frontend/src/orders/OrderListPage.tsx` lines 38 to 41; the page number is read in `orders/orderListQuery.ts` lines 14 to 17.
- Problem: an empty page shows "No orders yet." (or the filter message) without the pager, and `narrowsTheList` ignores the page.
- Why it matters: the page is in the address (AC-150), so an old link after deletes or a narrower filter states something false and offers no way back except editing the address.
- What to do: for an empty page after the first, say the page has no orders and show the pager.
- Task: M5-17.

#### 42. Saving a form again moves a time in the repeated autumn hour

- Where: `frontend/src/calendar/plantClock.ts` lines 15 to 32; the forms start from `utcToPlantLocal` and send `plantLocalToUtc` in `schedule/PlacementForm.tsx` lines 33 and 40, `orders/LotProgressForm.tsx` lines 28 and 29, `machines/UnavailabilityForm.tsx` lines 24 and 25.
- Problem: both instants of the repeated hour show the same text, and the text is always read as the first (A38). A time in the second one, which the earliest-slot search can propose, is sent one hour earlier when the form is saved with that field untouched.
- Why it matters: actual times are saved whole (A47), so a planner who edits only the end of a lot moves its start; this breaks AC-171 and AC-212 on that night.
- What to do: send the original UTC value while the field still shows its text, and convert only an edited field.
- Task: M5-16.

#### 43. The plant loader can crash instead of reporting, and accepts repeated machine names

- Where: `backend/src/TireScheduler.PlantLoader/LoaderRun.cs` lines 15 to 48 and `LoaderProgram.cs` line 21; `backend/src/TireScheduler.PlantGenerator/PlantParameterChecks.cs` lines 28 to 43 with `PlantSnapshot.cs` line 20.
- Problem: only `LoaderException` and `HttpRequestException` are caught, so a request that runs past the default 100-second timeout, an answer that is not JSON, or a failure in `SyntheticPlant.Generate` (outside the `try`) ends with a stack trace. Two machine types with the same name prefix pass the parameter checks.
- Why it matters: AC-159 asks for a message and a non-zero exit, and a `--schedule` run of the stress preset is the likely trigger; the repeated prefix fails later with an unnamed `ArgumentException` or halfway through a load, against AC-156.
- What to do: turn those exceptions into `LoaderException` in `SchedulerApiClient`, set the timeout explicitly as a documented setting, move `Generate` inside the `try`; reject repeated prefixes, compared as the API compares machine names.
- Task: M5-13.

#### 44. Two small gaps in the planner's screens

- Where: `frontend/src/orders/AsapProposalReview.tsx` lines 27 to 30 and 76 to 79; `frontend/src/forms/Fields.tsx` lines 20 to 37.
- Problem: after the planner types a new not-before time, Commit still saves the proposal shown, from the old time, without saying so. No time field uses the 15-minute step that DEC-3, A2 and `wireframes.md` section 3 describe; no criterion covers the step.
- Why it matters: a planner can commit a proposal they believe was recalculated, and starts off the grid reach the schedule.
- What to do: disable Commit, or say "Recalculate to use the new time", while the typed time differs from the proposal's; add a criterion for the start step, then set `step` on the start fields.
- Task: M5-18 and M5-19.

### Looked at and left out

- Sign-in answering faster for an unknown email: the lockout of AC-3 already tells existing emails apart, so a fixed-time check would not hide them.
- Progress times checked before the lot is looked up: intended (TASKS.md section "Handover"); AC-209 could say that the field check comes first.
- The loader's empty-plant check and its shift replacement: as AC-158 and A31 ask.
- Readers that expect every placed lot to exist, and the lot status inputs gathered per reader: true while only the live version exists (A41); look again when scenarios are planned.
- The proposal search building a snapshot per lot (DEC-47), the candidate starts kept beside the rules (guarded by the AC-115 and AC-116 property tests), the `PlacementRules` wrapper, the loader's copies of API names and paths, the parameter checks, the shift record names, the version id naming, a shared field error collector and the plant time zone on `IMasterDataReader`: each change is wanted only by a later feature.
- The routing defaults in `RoutingRegister`: documented in AC-34.
- Raw JSON reads in the API tests: they check the names on the wire; long test files: tolerable at today's size, as before.
- The order list query copy, the timeline chart's size and the two `PlantTime` instant functions: small and well tested; tidy them when the code is next changed.
