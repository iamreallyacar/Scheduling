# Tire Production Scheduler

## Project overview

- Title: Tire Production Scheduler
- Description: web application that lets production planners at a single-plant tire manufacturer build, check and maintain the production schedule in one place. Rebuild in progress; progress is in [TASKS.md](TASKS.md)
- Stack: .NET 10 (LTS) ASP.NET Core Web API, EF Core with PostgreSQL, React with TypeScript (DEC-20 in [REQUIREMENTS/decisions.md](REQUIREMENTS/decisions.md)); see [ARCHITECTURE.md](ARCHITECTURE.md)

## Purpose

A fictional scenario that set the project's direction. A mid-sized tire manufacturer runs one plant with about 15-25 machines in 3 shifts and makes roughly 40 tire models. Each tire passes through stages such as extrusion, building, curing and inspection, each on its own machine type. A small group of 2-5 planners builds the weekly schedule in spreadsheets and on a whiteboard. Machine status and maintenance windows reach them verbally, so the schedule goes stale within hours, and rush orders arrive every week.

Consequences: late orders are found only near their due date, some machines sit idle while others are overloaded, sales cannot promise reliable due dates, and re-planning after a disruption takes hours. Candidate success measures, without baselines yet: on-time delivery rate, machine utilization, time to re-plan after a disruption, and share of due-date promises met.

## Scope

The rebuild serves the production planner first; shift supervisors, sales and the plant manager come later. Features, priorities and v1 scope: [REQUIREMENTS/features.md](REQUIREMENTS/features.md).
