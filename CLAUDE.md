# CLAUDE

## GUIDELINES

- Large tasks: if the task given is large, decompose the task list first before starting.
- Assumptions: if you had to make any assumptions to carry out a task, clearly state the assumptions you had to make.
- Failures: if you run into errors more than three times within a session, cleanly remove the problematic code, and report the failure to the user.
- Clean solutions: if an idea, requirement, or implementation is not very well thought out and pose risks for technical debt in the future, stop before implementing, explain the risks to the user, and propose a cleaner alternative
- Transparency: before beginning any work, clearly state the goal you are working towards
- Relevant contexts: discover any relevant documentation or code before starting any tasks
- Knowledge gaps: if instruction or documentation files relevant to the task has contradictions or knowledge gaps that obstructs the task, mark the problematic instructions or statements within the files
- Language: responses should be written in easy to understand high school level language where possible

## CODING STYLE GUIDE

- Function and variables should be given specific and distinguishable names
- Comments should be written for things the code cannot explain itself
- Files and functions should each do one job at one level of detail; split code out when it represents its own concept or is reused elsewhere, never only to reduce length
- Fail loudly: errors should show up clearly, be acknowledged, and not suppressed away.
- Existing code should be cleaned up if you stumble upon them and they very clearly violate the above rules
- Strict typing: use the strictest type checking the language and project tooling support, annotate function signatures and public interfaces, and avoid escape hatches such as any, untyped casts, or ignore directives unless a comment explains why they are needed
- Config variables: do not hardcode values that may change between environments or deployments; read them from the project's existing config mechanism, never commit secrets, and document each new variable where the project documents its config

## DOCUMENTATION STYLE GUIDE

- Applies to every markdown file
- First heading is always title of the markdown file
- Line break before and after each heading except for the first
- Line break before and after bullet lists
- Headings, plain text, bullet points, and tables; use a table only when rows share the same fields (IDs, statuses, dependencies)
- No bold, italics, underline, or strike through
- Bullet points should be combined if they share a subject and trigger

## VERIFICATION

- `python3 scripts/check_docs.py`: consistency checks for the planning documents (links, section references, feature/task/assumption/rule/decision IDs, P0 count, milestones, decision statuses) and the documentation style guide (no bold or strike through, blank lines around headings, lists and tables). Run after every documentation change; it must pass before committing. When adding a new kind of cross-reference to the docs, add a check for it.

## PROJECT OVERVIEW

- Title: Tire Production Scheduler
- Description: web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place. Rebuild in planning, no application code yet. Business situation and problem statement: [documentation/business-problem.md](documentation/business-problem.md). Feature catalogue and priorities: [documentation/features.md](documentation/features.md)
- Stack: .NET 10 (LTS) ASP.NET Core Web API, EF Core with PostgreSQL, React with TypeScript (DEC-20 in [documentation/decisions.md](documentation/decisions.md)); see [documentation/architecture.md](documentation/architecture.md)

## DIRECTORY

- `README.md`: project purpose and scope
- `documentation/`: planning and design documents (`business-problem.md`, `features.md`, `domain-model.md`, `architecture.md`, `decisions.md`, `acceptance-criteria.md`, `tasks.md`); task list is `documentation/tasks.md`
- `scripts/`: verification scripts (see VERIFICATION)
