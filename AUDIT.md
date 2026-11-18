# Audit

Maintainability review of the code as of 2026-11-18 (after task M2-7). It lists only the findings that will cost the most as M3 to M5 are built. Completed findings keep their number and are listed first. Each finding says what the problem is, where it is, why it matters, and what to do about it. The findings are numbered by impact, highest first.

The code is in good shape overall: small files, clear names, one job per class, strict typing, a dependency test for the modules, and tests named after their criteria. The findings below are about patterns that work at today's size but will not scale to the scheduling milestones without change.

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

## Open

None.
