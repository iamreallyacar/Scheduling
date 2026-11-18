# Testing

How the project is tested. The commands to run are in CLAUDE.md, section VERIFICATION TOOLS; setup is in [GUIDE.md](GUIDE.md), Developer setup.

## Test levels

| Level | What | Tooling |
|---|---|---|
| Unit | Engine: every Engine-level acceptance criterion, including the worked examples and DST cases, and the late flag (DEC-30). Generator: every Generator-level criterion, in `TireScheduler.PlantGenerator.Tests`, for every preset over several seeds and start dates, including DST weeks | xUnit v3 |
| Property | Engine: any slot returned by `EarliestSlot` passes `Validate`; no feasible start exists before it; `ScheduleOrderAsap` proposals pass `Validate` together | FsCheck or similar, added in task M4-1 with the first property criteria (AC-115, AC-116, AC-127) |
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
- `SchedulerApiFixture`: one running API on its own empty database, shared by the tests of one class.
- `SchedulerApiFactory`: starts the API and gives browser-like clients over https. Signs in as the first Admin, or as a user of a given role that it creates directly, because v1 has no user management API (F-AUTH-3). Sets the plant time zone of `acceptance-criteria.md` section 1 unless a test passes another, and can fix the clock and replace services.
- `FakeLotActivity`: stands in for Scheduling and Progress behind the Orders ports `ILotPlacements` and `ILotProgress` and the Scheduling read interface `IPlacementReader`, so the order rules and the unscheduled-lots queue that depend on placed and started lots are tested before those modules store anything.
- `TestData/`: the builders that create rows through the API, one class per module with one method per kind of row and the paths of its routes (DEC-38). `MasterDataTestData` builds machine types, machines, products, routings, calendar shifts and holidays; `OrderTestData` builds orders, and its create and cancel helpers read the order back, because order writes answer without it (DEC-29). `SchedulingTestData` builds unavailabilities. A test class never calls another test class; a builder that a second class needs moves here.
- `ApiSession`: does what the browser and the frontend client do: keeps cookies between requests and sends the anti-forgery token with state-changing requests.
- `ProblemAssert`: checks that a response is "Rejected" in the sense of `acceptance-criteria.md` section 1.
- `RecordingLoggerProvider`: keeps log entries in memory so a test can check what was logged.
- `SchedulerApiFactory.NewCookieKeepingClient`: a client whose handler keeps the cookies, as a program outside the browser does; the plant loader tests (`PlantLoader/PlantLoaderTests.cs`) run the loader with it against an empty database per test.

Backend, in `backend/tests/TireScheduler.Engine.Tests`:

- `TestPlant`: the plant of the examples in `acceptance-criteria.md` section 4 as an engine snapshot (Press 1, Press 2, Builder 1, the plant default calendar in `Europe/Berlin`), with `With...` methods that add lots, placements, unavailabilities, dependencies, holidays and shifts, and the days of one winter week as UTC instants.
- `RuleAssert`: checks that a placement is accepted, or rejected with a given first code and conflict.

Frontend, in `frontend/src/testing`:

- `renderApp`: renders the whole app at a path, with a fresh query cache and no anti-forgery token.
- `stubApi`: replaces `fetch` with a table of routes keyed by "METHOD /path" and returns the requests made. A request with no route fails the test. The anti-forgery route is stubbed by default.
- `fakeApi.ts` also holds sample users for each role and builders for problem answers.

## Known gaps

- No CI runs the checks yet; they are run by hand (CLAUDE.md, section VERIFICATION TOOLS).
- The property tests and their library come with task M4-1, and Playwright with the first end-to-end flow.
- AC-19, AC-49, AC-51 and AC-174 are tested through `FakeLotActivity` until tasks M3-4 and M5-1 test them with real placements and progress.
- Order status and `isLate` are tested through the API without placements or progress, so only Pending and Cancelled occur and no planned end counts, until tasks M3-4 and M5-1 add them.

## Pitfalls

- The API tests need Docker running, because they start PostgreSQL through Testcontainers.
- A request has one database connection (DEC-33), so a test that holds a row lock and tries the same lock again must use a second service scope, as a second request would. `Database/RowLockTests.cs` gives the second scope a short `lock_timeout`; EF reports the timeout as an `InvalidOperationException` around the `PostgresException`.
- In the frontend, every successful write reads again each query on screen (DEC-36), so a page test whose stub answers a list once sees the list asked for again after a save; stub the route with every answer the test needs.
- Frontend tests run in jsdom, where `import.meta.url` is not a file URL; a test of a Node script under `frontend/scripts` starts with `// @vitest-environment node`.
- A test that moves the fixed clock more than 12 hours ahead must sign in again, because the session ends after 12 hours without a request (A21).
