# CLAUDE

## GUIDELINES

- Purpose check: Before starting a task, check it against the project's purpose as described in its README. If the task conflicts with that purpose or doesn't move the project toward it, stop, explain why, and wait for my answer, even if I asked for the task directly.
- Simple, maintainable design: Prefer the simplest solution that stays easy to change later. If a request or approach risks technical debt or hard-to-maintain code, stop before implementing, explain the risk, propose a cleaner alternative, and wait for my answer.
- Resolve ambiguity: If instructions or docs relevant to the task contradict each other or leave a gap that blocks the work, investigate, explain the issue, propose options, and wait for my answer. For minor or easily reversible gaps, make a reasonable assumption and note it instead.
- When to stop: The three checks above apply to work that would be costly to redo. For small, easily reversible changes, skip the stop, go ahead, and mention any concern in your summary.
- Small, safe tasks: Decompose work into small, independently verifiable tasks. Each task should be small enough to review in a few minutes and end in a working, tested state.
- Tests first: For new behavior and bug fixes, write tests that define the expected behavior as a contract before implementing. Confirm they fail, then write code until they pass. For refactors, make sure tests covering the affected behavior exist and pass before and after the change. Docs, config, and trivial changes need no new tests.
- Test changes: Never edit tests to make the implementation pass. If a test seems wrong or the requirement has changed, stop and tell me instead of changing it.
- Meaningful tests: Only write tests that would fail if real behavior broke. Test observable behavior and meaningful edge cases, not implementation details or trivial code. Use mocks only for slow or external dependencies, and never write tests that only check what a mock returns. A few tests that catch real bugs beat many that don't.
- Subagents: Use subagents for self-contained parts of a task, to keep the main context clean, and for an unbiased review once a task is done. Always pass model: "sonnet" explicitly, unless I name a different model for that task.
- Long waits, who does what: Subagents must not wait on slow work (bulk downloads, long builds, rate-limited services), since every poll costs a full turn. A subagent or the main agent writes the script, the main agent runs it in the background and waits for the notification, and a subagent processes the results.
- Long waits, how scripts behave: Scripts log everything to a file, print only a short summary, and pause when rate limited. Only one script runs against a given service at a time.

- Relevant contexts: discover any relevant documentation or code before starting any tasks
- Assumptions: if you had to make any assumptions to carry out a task, clearly state the assumptions you had to make
- Transparency: before beginning any work, clearly state the goal you are working towards
- Failures: if you fail to resolve an error more than three times within a session, cleanly remove any new code that may have introduced the error, stop, and report the problem to the user
- Task list: always decompose tasks before writing them into the task list
- Task flow: `TASKS.md` is the single source for progress; start with its "Handover" section and never copy task status into other files. Pick the next Todo task whose dependencies are Done, set it to Doing when you start and Done when its verification passes. At the end of a session, update "Handover" so a new session can pick up from it.
- Vertical slices: an implementation task delivers data, API, UI and tests together, as described for the M tasks in `TASKS.md`.
- Acceptance criteria first: behavior is specified in `REQUIREMENTS/acceptance-criteria.md` before it is built. Each test names the criterion it covers (`AC_42_...`). If a task needs behavior that has no criterion, add the criterion first.
- Test changes follow requirements: when a requirement changes, change the tests before the code.
- Decisions and assumptions: a new or changed decision gets an entry in `REQUIREMENTS/decisions.md` (supersede, never rewrite an accepted entry). A new assumption gets an A# row in `domain-model.md` section 7.
- Docs follow code: when code changes something the docs describe, update the docs in the same change and run every check under VERIFICATION TOOLS.
- Commits: only when the user asks, and only after all checks pass.

## CODE STYLE

- Precedence: The guidelines above take priority over these rules when they pull in different directions. In an existing codebase, follow its established conventions where they differ from these rules; apply these rules to new code, and don't change existing code to match them unless that's the task. If existing code breaks the error rule below, mention it rather than fixing it unasked.
- Naming: Give functions and variables specific names that can't be confused with each other.
- Comments: Comments explain why, not what. Don't add comments that restate the code, and don't remove existing comments unless they're wrong.
- Structure: Each file and function does one job at one level of detail. Split code out when it is its own concept or is actually reused, not in anticipation, even when that might make later changes easier; a few clear inline lines beat a helper used once.
- Errors: Never swallow errors or replace missing values with silent defaults. Handle an error only when there is a real recovery, such as a retry or a documented fallback, and log it when you do; otherwise let it propagate with enough context to diagnose it.
- Typing: Use the strictest type settings the project already has, and start new projects strict. Don't tighten an existing project's type config unless that's the task. Annotate function signatures and public interfaces, and avoid escape hatches such as any, untyped casts, or ignore directives unless a comment explains why they are needed.
- Config: Read values that differ between environments or deployments from config instead of hardcoding them; use named constants for other fixed values. A config value may have a default only if the default is documented. Never put secrets in code or tracked files. Document each new config variable where the project documents its config, or in a .env.example if there's no such place.

- Project rules: the rules below come from `ARCHITECTURE.md` and `REQUIREMENTS/decisions.md`; the reasons are there when in doubt.
- Module boundaries: each module under `backend/src/TireScheduler.Api/Modules/` owns its tables in its own database schema and is used by other modules only through its public interface. Dependencies follow the table in `ARCHITECTURE.md` section "Structure"; reads in the other direction go through small read-only ports (DEC-16). When the table changes, change `ModuleDependencyTests` in the same commit.
- Engine: `TireScheduler.Engine` stays a pure library (`ARCHITECTURE.md` section "Engine rules"). No database, web, module types or clock; everything it needs is passed in.
- Time: never read the system clock; use the injected `TimeProvider`. Store and send UTC; only the engine converts to and from plant-local time (`ARCHITECTURE.md` section "Time rules").
- Transactions: begin them through `RequestTransaction`, never on a module context, and lock rows with `LockRowAsync`, never with hand-written SQL (DEC-33).
- Statuses are derived, never stored (DEC-12), and computed in `Queries/`.
- Records in the API project use named `required` properties, never positional parameters (DEC-34).
- Rules live on the server. The frontend shows what the API returns and never repeats a business rule; the server decides conflicts and computes planned ends.
- Errors use problem details with a stable `code` (`API.md` section "Error format"). Scheduling rule violations are 422 and are rejected, never adjusted (R7).
- API: REST under `/api/v1`, schedule version in the path with the `live` alias (DEC-17).
- Packages: backend versions are set only in `backend/Directory.Packages.props`. Frontend versions are pinned exactly in `frontend/package.json`. Say why when adding a package.
- Analyzer exceptions: turn a rule off only in the narrowest `Directory.Build.props` or project file, with a comment giving the reason.

## DOCUMENTATION STYLE

- Scope: These rules apply to every markdown file. They don't apply to chat replies, which follow the response style below. In an existing project, follow its established markdown conventions where they differ from these rules.
- Structure: The first heading is the document's title. Put a blank line before and after every heading except the first, and before and after every list.
- Elements: Use only headings, plain text, bullet points, numbered lists, tables, links, and code blocks. No bold, italics, underline, or strikethrough.
- Lists: Merge list items that cover the same subject and apply in the same situation.
- Single source of truth: Each piece of information lives in exactly one place. Elsewhere, link or refer to it instead of restating it, and when it changes, update only that place.

## COMMIT MESSAGE STYLE

- Precedence: If the repository already uses a different commit message format, follow it.
- Summary line: Start with a summary of about 50 characters or less, written as a command ("Add retry to payment client"), with no period at the end.
- Body: After a blank line, explain why the change was made and anything surprising about it, such as trade-offs or side effects. Don't restate what the diff already shows. If the reason isn't known, keep the body short or leave it out rather than guessing.
- One change per commit: Each commit covers one logical change. If the summary needs "and", split it into separate commits.
- Subagent handoff: When a subagent writes the message, pass it the reason for the change along with the diff, since it can't work out the reason from the code.

## RESPONSE STYLE

- Language: Write responses in easy-to-understand, high-school-level language where possible.

- Goal first: start your responses with the goal of the assigned task
- Sections: within your responses, group your actions sequentially into sections, explain the actions you carried out within each section and why you performed them.

## VERIFICATION TOOLS

- `python3 scripts/check_docs.py`: consistency checks for the planning documents (links, section references in docs and code comments, feature/task/assumption/rule/decision IDs, P0 count, milestones, decision statuses, acceptance criteria assigned to tasks) and the documentation style (no bold or strike through, blank lines around headings, lists and tables). Run after every documentation change; it must pass before committing. When adding a new kind of cross-reference to the docs, add a check for it.
- `dotnet build backend` and `dotnet test --solution backend/TireScheduler.slnx`: backend build (warnings are errors) and all backend tests. API tests need Docker. Run after every backend change. After an intended API change, run the tests once with `UPDATE_OPENAPI=1` to rewrite `backend/openapi/tire-scheduler-v1.json`, then `npm run generate:api` in `frontend/`.
- In `frontend/`: `npm run typecheck`, `npm run lint`, `npm test`, `npm run build`. Run after every frontend change.

## DIRECTORY

- `CLAUDE.md`: rules for agents working in the project
    - GUIDELINES
    - CODE STYLE
    - DOCUMENTATION STYLE
    - COMMIT MESSAGE STYLE
    - RESPONSE STYLE
    - VERIFICATION TOOLS: the commands that build, test, lint, and type-check the project
    - DIRECTORY
- `README.md`: what the project is and why it exists; the first heading is the project name
    - OVERVIEW: a short description and the stack
    - PURPOSE: the problem the project solves, used for the purpose check in GUIDELINES
    - SCOPE: a high-level list of what the project does and does not do; details live in `REQUIREMENTS/`
- `GUIDE.md`: how to get the project running
    - USER SETUP: steps to clone, install, and start using the project
    - DEVELOPER SETUP: extra steps needed for development, on top of user setup
- `ARCHITECTURE.md`: how the code is organized and why
    - STRUCTURE: a mermaid diagram and description of how responsibility is divided in the code base
    - DESIGN: design choices and the rules that follow from them
    - DATA FLOW: a mermaid diagram and description of how data moves when the project runs
    - SYSTEMS: one short section per system naming the part that owns it, the rules it keeps, and where its detailed documentation lives
    - EXTENDING: where to add new systems
- `TASKS.md`: current progress; read at the start of each session and update before it ends
    - MILESTONES: the larger goals for the current version, in order
    - HANDOVER: where the last session stopped and what the next session needs to know
    - TASK LIST: planned tasks and their status
- `REQUIREMENTS/`: what the current version must do, one file per feature or area
    - `REQUIREMENTS/README.md`: index of the requirement files, with one line on what each covers
    - `REQUIREMENTS/BACKLOG.md`: ideas and work not in the current version and not yet planned as tasks
- `API.md`: the interfaces between the project's parts and to the outside world: routes, messages, request and response shapes, error format, and the conventions specific to them
- `TESTING.md`: how the project is tested: test levels and what each covers, tools, fixtures and helpers, known gaps, and pitfalls when writing tests

- `scripts/`: verification scripts (see VERIFICATION TOOLS)
- `backend/`: .NET solution (`TireScheduler.slnx`). `src/TireScheduler.Api` is the API, with one folder per module under `Modules/` plus `Queries/`, and the shared folders `Errors/`, `Security/` and `Database/` (`ARCHITECTURE.md` section "Structure"); `openapi/` holds the OpenAPI document the frontend types are generated from; `src/TireScheduler.Engine` is the scheduling engine (its README.md describes inputs, operations and search); `src/TireScheduler.PlantGenerator` and `src/TireScheduler.PlantLoader` are the synthetic plant generator's core and loader (DEC-32); `tests/` holds `TireScheduler.Engine.Tests`, `TireScheduler.PlantGenerator.Tests` (generator core and loader command line), `TireScheduler.Architecture.Tests` (module dependency table) and `TireScheduler.Api.Tests` (API against a real database, including the plant loader)
- `frontend/`: React with TypeScript app (Vite). `src/api/generated` (types and endpoint functions) is generated from the OpenAPI document and never edited by hand; `scripts/` holds the endpoint function generator
- `global.json`: .NET SDK version and test runner for every `dotnet` command
- `dotnet-tools.json`: local .NET tools (`dotnet ef` for migrations); install with `dotnet tool restore`
- `compose.yaml`, `.env.example`: local PostgreSQL
