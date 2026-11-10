# CLAUDE

## GUIDELINES

- Large tasks: if the task given is large, decompose the task list first before starting.
- Assumptions: if you had to make any assumptions to carry out a task, clearly state the assumptions you had to make.
- Failures: if you run into errors more than three times within a session, cleanly remove the problematic code, and report the failure to the user.
- Clean solutions: if an idea, requirement, or implementation is not very well thought out and pose risks for technical debt in the future, stop before implementing, explain the risks to the user, and propose a cleaner alternative
- Transparency: before beginning any work, clearly state the goal you are working towards
- Relevant contexts: discover any relevant documentation or code before starting any tasks
- Knowledge gaps: if instruction or documentation files relevant to the task has contradictions or knowledge gaps that obstructs the task, mark the problematic instructions or statements within the files
- Task flow: `TASKS.md` is the single source for progress; start with its "Where to start" section and never copy task status into other files. Pick the next Todo task whose dependencies are Done, set it to Doing when you start and Done when its verification passes. At the end of a session, update "Where to start" so a new session can pick up from it.
- Vertical slices: an implementation task delivers data, API, UI and tests together, as described for the M tasks in `TASKS.md`.
- Acceptance criteria first: behavior is specified in `REQUIREMENTS/acceptance-criteria.md` before it is built. Each test names the criterion it covers (`AC_42_...`). If a task needs behavior that has no criterion, add the criterion first.
- Decisions and assumptions: a new or changed decision gets an entry in `REQUIREMENTS/decisions.md` (supersede, never rewrite an accepted entry). A new assumption gets an A# row in `domain-model.md` section 7.
- Docs follow code: when code changes something the docs describe, update the docs in the same change and run every check under VERIFICATION.
- Commits: only when the user asks, and only after all checks pass.

## CODING STYLE GUIDE

- Function and variables should be given specific and distinguishable names
- Comments should be written for things the code cannot explain itself
- Files and functions should each do one job at one level of detail; split code out when it represents its own concept or is reused elsewhere, never only to reduce length
- Fail loudly: errors should show up clearly, be acknowledged, and not suppressed away.
- Existing code should be cleaned up if you stumble upon them and they very clearly violate the above rules
- Strict typing: use the strictest type checking the language and project tooling support, annotate function signatures and public interfaces, and avoid escape hatches such as any, untyped casts, or ignore directives unless a comment explains why they are needed
- Config variables: do not hardcode values that may change between environments or deployments; read them from the project's existing config mechanism, never commit secrets, and document each new variable where the project documents its config
- Project rules: the rules below come from `ARCHITECTURE.md` and `REQUIREMENTS/decisions.md`; the reasons are there when in doubt.
- Module boundaries: each module under `backend/src/TireScheduler.Api/Modules/` owns its tables in its own database schema and is used by other modules only through its public interface. Dependencies follow the table in `ARCHITECTURE.md` section 2; reads in the other direction go through small read-only ports (DEC-16). When the table changes, change `ModuleDependencyTests` in the same commit.
- Engine: `TireScheduler.Engine` stays a pure library (`ARCHITECTURE.md` section 3). No database, web, module types or clock; everything it needs is passed in.
- Time: never read the system clock; use the injected `TimeProvider`. Store and send UTC; only the engine converts to and from plant-local time (`ARCHITECTURE.md` section 5).
- Statuses are derived, never stored (DEC-12), and computed in `Queries/`.
- Rules live on the server. The frontend shows what the API returns and never repeats a business rule; the server decides conflicts and computes planned ends.
- Errors use problem details with a stable `code` (`ARCHITECTURE.md` section 7). Scheduling rule violations are 422 and are rejected, never adjusted (R7).
- API: REST under `/api/v1`, schedule version in the path with the `live` alias (DEC-17).
- Packages: backend versions are set only in `backend/Directory.Packages.props`. Frontend versions are pinned exactly in `frontend/package.json`. Say why when adding a package.
- Analyzer exceptions: turn a rule off only in the narrowest `Directory.Build.props` or project file, with a comment giving the reason.

## DOCUMENTATION STYLE GUIDE

- Applies to every markdown file
- First heading is always title of the markdown file
- Line break before and after each heading except for the first
- Line break before and after bullet lists
- Headings, plain text, bullet points, and tables
- No bold, italics, underline, or strike through
- Bullet points should be combined if they share a subject and trigger

## RESPONSE STYLE

- Language: responses should be written in easy to understand high school level language where possible
- Within your response, include what actions you made and the purpose of your actions in terms of the project as a whole

## VERIFICATION

- `python3 scripts/check_docs.py`: consistency checks for the planning documents (links, section references, feature/task/assumption/rule/decision IDs, P0 count, milestones, decision statuses, acceptance criteria assigned to tasks) and the documentation style guide (no bold or strike through, blank lines around headings, lists and tables). Run after every documentation change; it must pass before committing. When adding a new kind of cross-reference to the docs, add a check for it.
- `dotnet build backend` and `dotnet test --solution backend/TireScheduler.slnx`: backend build (warnings are errors) and all backend tests. API tests need Docker. Run after every backend change.
- In `frontend/`: `npm run typecheck`, `npm run lint`, `npm test`, `npm run build`. Run after every frontend change.

## PROJECT OVERVIEW

- Title: Tire Production Scheduler
- Description: web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place. Rebuild in progress; progress is in `TASKS.md`. Business situation and problem statement: [README.md](README.md). Feature catalogue and priorities: [REQUIREMENTS/features.md](REQUIREMENTS/features.md)
- Stack: .NET 10 (LTS) ASP.NET Core Web API, EF Core with PostgreSQL, React with TypeScript (DEC-20 in [REQUIREMENTS/decisions.md](REQUIREMENTS/decisions.md)); see [ARCHITECTURE.md](ARCHITECTURE.md)

## DIRECTORY

- `README.md`: project purpose, business problem, getting started and configuration
- `TASKS.md`: task list
- `ARCHITECTURE.md`: code architecture
- `REQUIREMENTS/`: requirement folder: `features.md` (features, priorities, v1 scope), `domain-model.md` (terms, entities, rules, assumptions), `acceptance-criteria.md` (testable behavior), `wireframes.md` (screen sketches), `decisions.md` (decision log and open questions)
- `scripts/`: verification scripts (see VERIFICATION)
- `backend/`: .NET solution (`TireScheduler.slnx`). `src/TireScheduler.Api` is the API, with one folder per module under `Modules/` plus `Queries/`; `src/TireScheduler.Engine` is the scheduling engine; `tests/` holds `TireScheduler.Engine.Tests`, `TireScheduler.Architecture.Tests` (module dependency table) and `TireScheduler.Api.Tests` (API against a real database)
- `frontend/`: React with TypeScript app (Vite)
- `global.json`: .NET SDK version and test runner for every `dotnet` command
- `compose.yaml`, `.env.example`: local PostgreSQL
