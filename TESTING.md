# Testing

How the project is tested. The commands to run are in CLAUDE.md, section VERIFICATION TOOLS; setup is in [GUIDE.md](GUIDE.md), Developer setup.

## Test levels

| Level | What | Tooling |
|---|---|---|
| Unit | Engine: every Engine-level acceptance criterion, including the worked examples and DST cases, and the late flag (DEC-30). Generator: every Generator-level criterion, in `TireScheduler.PlantGenerator.Tests`, for every preset over several seeds and start dates, including DST weeks | xUnit v3 |
| Property | Engine: any slot returned by `EarliestSlot` passes `Validate`; no feasible start exists before it; `ScheduleOrderAsap` proposals pass `Validate` together. The cases are generated plants over a list of seeds and start dates, including a DST week (DEC-56) | xUnit v3 theories over `SyntheticPlant` and `PlantSnapshot` |
| Module integration | Each module against a real PostgreSQL, including constraints, locking and soft delete | xUnit v3 with Testcontainers |
| API | Auth and role policies, problem-details shape, rule violations returned as 422 with `rule` and `conflict` | `WebApplicationFactory` |
| Architecture | Module dependency table in `ARCHITECTURE.md` section "Structure" holds between module namespaces; the engine project references no module; the generator core references only the engine, and neither the engine nor the API references it | NetArchTest |
| Frontend | Components and forms (placement dialog, order form, ASAP proposal review); the plant clock conversion of typed times, with the DST cases of the engine (DEC-40); the endpoint generator in `frontend/scripts`, including a check that the committed endpoint functions match the OpenAPI document (DEC-37) | Vitest, Testing Library |
| End to end | A few smoke flows only: sign in, create order, schedule it as soon as possible, see it on the timeline | Playwright, added with the first flow |

## Rules for tests

- Acceptance criteria ([acceptance-criteria.md](REQUIREMENTS/acceptance-criteria.md), tasks P5 and P6) are written first and each maps to at least one test named after its ID.

## Fixtures and helpers

Backend, in `backend/tests/TireScheduler.Api.Tests`:

- `PostgresFixture`: one real PostgreSQL container per test run, and a new empty database for each caller, so tests cannot see each other's rows. The API's migrations create the tables.
- `SchedulerApiFixture`: one running API on its own empty database, optionally on a fixed clock or with replaced services. As a class fixture it is shared by the tests of one class; a class whose tests each need a new database, a clock of their own or the fakes below makes one with `SchedulerApiFixture.ForEachTest` in its constructor and disposes it, so each test gets its own (DEC-53). xUnit cannot fill optional constructor arguments of a class fixture, so the clock and replaced services are given only through `ForEachTest`.
- `SchedulerApiFactory`: starts the API and gives browser-like clients over https. Signs in as the first Admin, or as a user of a given role that it creates directly, because v1 has no user management API (F-AUTH-3). Sets the plant time zone of `acceptance-criteria.md` section 1 unless a test passes another, and can fix the clock and replace services.
- `Fakes/`: the stand-ins for ports, kept together. `FakeLotActivity` stands in for Progress behind the ports `ILotProgress` (Orders) and `ILotProgressStates` (Scheduling), so the rules of those modules that depend on started and completed lots are tested on their own, also in states the API cannot reach, such as a completed lot without a live placement (DEC-62). It keeps no actual times, and Queries still reads the real Progress, so a test that uses it checks the rules, not the statuses; `Progress/LotProgressTests.cs` tests the same rules with real progress. Placements are always real. `FailingLotProgressStates` and `FailingPlannedEndRecompute` throw, so a test can check that a write which needs them saves nothing (AC-161, AC-182).
- `TestData/`: the builders that create rows through the API, one class per module with one method per kind of row and the paths of its routes (DEC-38). `MasterDataTestData` builds machine types, machines, products, routings, calendar shifts and holidays; `OrderTestData` builds orders, and its create and cancel helpers read the order back, because order writes answer without it (DEC-29). `SchedulingTestData` builds the building-and-curing plant of `acceptance-criteria.md` section 4 (`CreatePlantAsync`, with `LotQuantity`, `BuildingLotMinutes` and `CuringLotMinutes` naming its order quantity and lot lengths), unavailabilities, and places lots in the live version. The readers of answers that more than one class needs sit next to the builders of the same module: `LotIds`, `Lot` and `ListedOrder` in `OrderTestData`, `PlacementValues` and `QueuedLotIds` in `SchedulingTestData` (DEC-53). `ProgressTestData` records lot progress. `PlantTime.Berlin` turns a plant-local time into the UTC instant the API takes, through the plant time zone. Scenario versions have no API before F-SCH-12, so its `AddScenarioPlacementAsync` writes them and their placements through `SchedulingDbContext` (AC-82, AC-50, AC-182, AC-218). A test class never calls another test class; a builder that a second class needs moves here.
- `ApiSession`: does what the browser and the frontend client do: keeps cookies between requests and sends the anti-forgery token with state-changing requests.
- `HeldRowLock`: holds a row lock in a transaction of its own, as a concurrent command would, and fails when a write answers while the row is held, so a lock test checks the lock itself instead of a race between two requests that passes by luck without it (AC-211, the lock tests of task M5-6). A test of a write that meets a delete at the same moment holds the row and deletes it in the holding transaction, so the write reads the row before it is gone (task M5-8). A test needs one per lock: remove the lock and see its test fail. `ChangeWhileHeldAsync` changes rows in the holding transaction before the write is sent, as the holding command would. A write that changes the held row is a pitfall (section "Pitfalls").
- `ProblemAssert`: checks that a response is "Rejected" in the sense of `acceptance-criteria.md` section 1; `ErrorFields` gives the field names of a 400 answer in ordinal order, for comparing with an expected list.
- `RecordingLoggerProvider`: keeps log entries in memory so a test can check what was logged.
- `SchedulerApiFactory.NewCookieKeepingClient`: a client whose handler keeps the cookies, as a program outside the browser does; the plant loader tests (`PlantLoader/PlantLoaderTests.cs`) run the loader with it against an empty database per test.

Backend, in `backend/tests/TireScheduler.Engine.Tests`:

- `TestPlant`: the plant of the examples in `acceptance-criteria.md` section 4 as an engine snapshot (Press 1, Press 2, Builder 1, the plant default calendar in `Europe/Berlin`), with `With...` methods that add lots, placements, unavailabilities, dependencies, holidays and shifts, and the days of one winter week as UTC instants.
- `RuleAssert`: checks that a placement is accepted, or rejected with a given first code and conflict.
- `PlantSnapshot.From` (in the generator core): a generated plant as an unplaced engine snapshot, for the property tests (AC-193, DEC-56). `GeneratedPlants` holds the seeds and start dates of the property tests and first places some lots at random starts that pass `Validate`, so the searches meet placements and dependencies too; it also gives the lot ids of each generated order, which `ScheduleOrderAsapPropertyTests` proposes one order at a time.

Frontend, in `frontend/src/testing`:

- `renderApp`: renders the whole app at a path, with a fresh query cache and no anti-forgery token.
- `stubApi`: replaces `fetch` with a table of routes keyed by "METHOD /path" and returns the requests made. A request with no route fails the test. The anti-forgery route and the calendars route (one plant calendar, `testing/plantCalendar.ts`) are stubbed by default, because every signed-in page waits for the plant time zone; a route a test passes overrides them. A route may answer with a promise, so a test can look at the page while a read is still loading and then resolve it.
- `sentBody` (in `fakeApi.ts`): the body of the one request sent as "METHOD /path", failing when none or several were sent, so a test compares it without a cast.
- `fakeApi.ts` also holds sample users for each role and builders for problem answers.
- `rowOf`: the table row holding a cell, failing when there is none, so a test can look inside one row.

## Known gaps

- No CI runs the checks yet; they are run by hand (CLAUDE.md, section VERIFICATION TOOLS).
- Playwright comes with the first end-to-end flow.
- The property tests run fixed seeds and do not shrink a failing case; a failure names the lot, the resource and the start, and the seed is in the test case (DEC-56).

## Pitfalls

- The API tests need Docker running, because they start PostgreSQL through Testcontainers.
- A row written directly through a module context needs its times in UTC: Npgsql refuses a `DateTimeOffset` with another offset for `timestamptz`, which the API never sends because it converts first.
- A write that updates the row it should lock waits for a held row even without its lock, but only at its update, after its checks have read the old state. Its lock test therefore changes what the checks read while the row is held (`HeldRowLock` `whileHeld`) and expects the write to see the change, as `Orders/OrderLifecycleTests.cs` does for order update, cancel and delete. A write that deletes the held row, or saves a row with a foreign key to it, waits the same way: its test deletes the row or adds a row that refers to it with `ChangeWhileHeldAsync` before sending the write, as `MasterData/ReferenceLockTests.cs` does (AC-222).
- A request has one database connection (DEC-33), so a test that holds a row lock and tries the same lock again must use a second service scope, as a second request would. `Database/RowLockTests.cs` gives the second scope a short `lock_timeout`; EF reports the timeout as an `InvalidOperationException` around the `PostgresException`.
- In the frontend, every successful write reads again each query on screen (DEC-36), so a page test whose stub answers a list once sees the list asked for again after a save; stub the route with every answer the test needs.
- Frontend tests run in jsdom, where `import.meta.url` is not a file URL; a test of a Node script under `frontend/scripts` starts with `// @vitest-environment node`.
- A test that moves the fixed clock more than 12 hours ahead must sign in again, because the session ends after 12 hours without a request (A21).
