# Wireframes

Status: DRAFT, written in task P8 on 2026-11-10. Low-fidelity sketches of the three scheduling screens: layout, content and states, not visual design. Sources: [features.md](features.md), [ARCHITECTURE.md](../ARCHITECTURE.md), [acceptance-criteria.md](acceptance-criteria.md), [domain-model.md](domain-model.md).

## 1. Conventions

- All times are plant-local and the zone is always shown (`ARCHITECTURE.md` section 5). Start times are picked on a 15-minute grid (A2).
- The version is always Live in v1 and is shown as a fixed label, not a switcher (DEC-17). A scenario switcher (P2) takes its place later.
- The UI never decides a rule itself. Planned end, conflicts and suggestions come from the server; the screen shows the returned values and errors (`ARCHITECTURE.md` section 10).
- Viewer sees every screen read-only: the actions marked [Planner] are hidden. Admin sees what Planner sees (F-AUTH-2).
- Errors show the problem-details `detail` text. Rule violations also list every entry in `violations`, each with a link to its conflicting item (R7).
- In the sketches, `[ ]` is a button, `[v]` a drop-down, `( )` a radio option, `____` an input field, `===` a placed lot, `///` an unavailability, `...` non-working time, `!` a conflicted placement and `|` under "now" the current time.

## 2. Timeline view

Features F-SCH-2, F-SCH-4, F-SCH-7 and F-MNT-2; tasks M3-3, M3-5 and M3-6. Route `/schedule`. Data from `GET /versions/live/timeline`, `/versions/live/conflicts` and `/versions/live/unscheduled-lots`.

```
+----------------------------------------------------------------------------------------------+
| Tire Scheduler   Dashboard  Orders  Schedule  Machines  Products            J. Doe (Planner) |
+----------------------------------------------------------------------------------------------+
| Schedule - Live                                              Times in Europe/Berlin          |
| [< Prev] [Today] [Next >]   Mon 16 Nov - Sun 29 Nov 2026   Zoom: ( ) Day (x) Week            |
| Resource type: [All      v]                                  [! 3 conflicted placements]     |
+-------------------------+--------------------------------------------------------------------+
| Machine         Status  | Mon 16          Tue 17          Wed 18   ...   Sat 21    Sun 22    |
|                         | 00   08   16    00   08   16    00   08        ......... ......... |
|                         |            now |                                                   |
+-------------------------+--------------------------------------------------------------------+
| Building                |                                                                    |
|  Builder 1      Running | [==1042/L2==][==1051/L2====]   [==1060/L2==]   ......... ......... |
|  Builder 2      Idle    |        [===1055/L2===]                         ......... ......... |
| Curing                  |                                                                    |
|  Press 1        Down    | [==1038/L3==]////breakdown, no end//////////////////////////////// |
|                         |              ! [==1042/L3==]  ! [==1047/L3==]                      |
|  Press 2        Idle    |    [==1051/L3=====]  ////maint.////  [==1060/L3==] ......... ..... |
|  Press 3        Maint.  | ///maint.///  [==1055/L3==]                    ......... ......... |
| Inspection              |                                                                    |
|  Inspect 1      Idle    |                   [=1038/L4=]   [=1051/L4=]    ......... ......... |
+-------------------------+--------------------------------------------------------------------+
| Unscheduled lots (12)                                                    [Hide queue]        |
| Order   Stage        Lot  Qty  Duration  Due         Earliest from deps                      |
| 1063    Building     L2   200  6h 40m    Fri 20 Nov  -                     [Place...]        |
| 1063    Curing       L3   200  20h 30m   Fri 20 Nov  after L2 + 2h         [Place...]        |
| 1064    Extrusion    L1   120  2h 00m    Mon 23 Nov  -                     [Place...]        |
+----------------------------------------------------------------------------------------------+
```

Lot bar popover, opened by clicking a bar:

```
+--------------------------------------------------+
| Order 1042, Curing, Lot L3                 [x]   |
| Product   P-205/55R16  Qty 200                   |
| Machine   Press 1                                |
| Planned   Tue 17 Nov 06:00 - Tue 17 Nov 18:30    |
| Status    Scheduled          Order: late         |
|                                                  |
| ! Conflict R3 rule.R3.unavailable                |
|   Overlaps breakdown on Press 1 from Mon 16 Nov  |
|   11:00, no end                [Show breakdown]  |
|                                                  |
| [Move...] [Remove placement]   [Open order]      |
|  Move and Remove: Planner only, hidden once the  |
|  lot has started                                 |
+--------------------------------------------------+
```

Behavior:

- Rows are grouped by resource type, sorted by name; inactive machines are hidden unless they still hold placements, which are then R1 conflicts (A15).
- Machine status is the derived display status at the current time (`domain-model.md` section 6). It is not editable here; breakdowns and maintenance are recorded on the machine page (F-MNT-1).
- Non-working time and holidays are shaded. Unavailabilities are hatched and labelled with their kind; an open-ended breakdown runs to the right edge.
- A bar shows the order number and lot number. Conflicted placements carry a `!` marker and a distinct outline, and the "conflicted placements" button filters the view to them (F-SCH-4, AC-88). A placement listed by `FindConflicts` is never hidden or moved by the UI (DEC-18).
- The timeline is read-only (F-SCH-2): no drag, no resize. Changes go through the placement form (section 3), opened from "Place..." in the queue or "Move..." in the popover.
- The default range is two weeks from the Monday of the current week, the F-NFR-5 size (~25 machines x 2 weeks).
- The unscheduled-lots queue is sorted by due date, then order number. Lots of cancelled orders never appear there. The [Place...] buttons are Planner only.

## 3. Lot placement form with earliest-slot suggestion

Features F-SCH-1 and F-SCH-11; tasks M3-4 and M4-2. A dialog opened from the queue, the lot popover or the order detail. Data from `GET /versions/live/lots/{id}/earliest-slot`; saved with `PUT /versions/live/lots/{id}/placement`.

```
+------------------------------------------------------------------------+
| Place lot - Order 1063, Curing, Lot L3                           [x]   |
+------------------------------------------------------------------------+
| Product     P-205/55R16       Quantity  200       Due  Fri 20 Nov      |
| Needs       Curing            Duration  20h 30m (working time)         |
| Depends on  Building L2, lag 2h      L2 ends Tue 17 Nov 14:40          |
|             Earliest start from dependencies: Tue 17 Nov 16:40         |
+------------------------------------------------------------------------+
| Find earliest slot                                                     |
|   Machine     [Any curing press  v]                                    |
|   Not before  [Tue 17 Nov 2026] [16:45 v]   (default: now)             |
|   [Find earliest slot]                                                 |
|                                                                        |
|   Suggested: Press 2, Tue 17 Nov 18:00 - Wed 18 Nov 14:30              |
|   Earliest end across 3 active curing presses.   [Use suggestion]      |
+------------------------------------------------------------------------+
| Placement                                                              |
|   Machine     [Press 2           v]   (active curing presses only)     |
|   Start       [Tue 17 Nov 2026] [18:00 v]   15-minute steps            |
|   Planned end Calculated when saved                                    |
+------------------------------------------------------------------------+
| [Save placement]  [Cancel]                                             |
+------------------------------------------------------------------------+
```

Rejected save, shown above the buttons; the form keeps the entered values and nothing is saved (R7, AC-111):

```
+------------------------------------------------------------------------+
| ! Placement rejected                                                   |
|   Overlaps Order 1051, Lot L3 on Press 2, Tue 17 Nov 08:00-20:15       |
|     rule.R2.overlap                              [Show on timeline]    |
|   Starts before the dependency allows; earliest start Tue 17 Nov 16:40 |
|     rule.R4.dependency                           [Show Building L2]    |
|   [Find earliest slot]                                                 |
+------------------------------------------------------------------------+
```

No slot found (`slot.none`, AC-122, AC-123):

```
+------------------------------------------------------------------------+
| No free slot on any active curing press within 90 days after           |
| Tue 17 Nov 16:45. Check breakdowns and inactive machines.              |
+------------------------------------------------------------------------+
```

Behavior:

- The machine list for saving holds only active machines of the lot's resource type (R1). The search list adds "Any <type>", which searches every active machine of that type (AC-118). Saving always needs one machine.
- "Find earliest slot" changes nothing (AC-126). "Use suggestion" copies the machine and start into the placement fields; the planner still saves. When the lot is already placed, the search ignores its own placement (AC-121).
- The start picker offers 15-minute steps. A start outside working time is not blocked by the picker; the server rejects it under R6 and the form shows the error.
- After a successful save the dialog closes and the timeline shows the server's `plannedEnd`.
- Moving an existing placement opens the same form, filled in, with an extra [Remove placement] button. For a started or completed lot the form opens read-only with the reason (R8, AC-112, AC-113).

## 4. ASAP proposal review

Feature F-SCH-16; task M4-4; DEC-19 and AC-137. Opened from "Schedule as soon as possible" on the order detail view (F-ORD-5) [Planner]. Data from `POST /versions/live/orders/{id}/asap-proposal`; saved with `POST .../asap-proposal/commit`.

```
+----------------------------------------------------------------------------------------------+
| Schedule order 1063 as soon as possible                                                [x]   |
| Product P-205/55R16   Qty 200   Due Fri 20 Nov 2026   Not before [Tue 17 Nov] [16:45 v]      |
|                                                                        [Recalculate]         |
+----------------------------------------------------------------------------------------------+
| Seq  Stage        Lot  Machine     Start              End                Note                |
| 1    Extrusion    L1   Extruder 1  Mon 16 Nov 08:00   Mon 16 Nov 12:00   Already placed      |
| 2    Building     L2   Builder 2   Tue 17 Nov 16:45   Tue 17 Nov 23:25   Proposed            |
| 3    Curing       L3   Press 2     Wed 18 Nov 01:30   Wed 18 Nov 22:00   Proposed, lag 2h    |
| 4    Inspection   L4   Inspect 1   Wed 18 Nov 22:00   Thu 19 Nov 02:00   Proposed            |
+----------------------------------------------------------------------------------------------+
| Preview (machines used by this order)                                                        |
|                 Tue 17            Wed 18            Thu 19                                   |
|  Builder 2      [=1055=]  [::1063/L2::]                                                      |
|  Press 2        [====1051====]       [:::::::1063/L3:::::::]                                 |
|  Inspect 1                                          [::L4::]                                 |
|  === existing placement   ::: proposed                                                       |
+----------------------------------------------------------------------------------------------+
| Order would finish Thu 19 Nov 02:00, due Fri 20 Nov: on time                                 |
| Nothing is saved until you commit. Existing placements are never moved.                      |
| [Commit 3 placements]  [Cancel]                                                              |
+----------------------------------------------------------------------------------------------+
```

States:

| State | Trigger | Screen |
|---|---|---|
| Proposal | 200 with proposed lots | Table, preview and summary as above |
| Nothing to place | 200 with an empty proposal (AC-132) | "Every lot of this order is already placed." No commit button |
| No slot | 422 `slot.none` naming a lot (AC-131) | No table. "No slot within 90 days for Lot L3 (Curing)." [Open lot] |
| Cancelled order | 422 `rule.R8.cancelled` (AC-133) | Not reachable from the UI: the action is hidden for cancelled orders. Shown as an error if it happens |
| Committed | 200 on commit (AC-134) | Dialog closes; the order detail shows the new placements |
| Stale | 409 `proposal.stale` on commit (AC-135) | "The schedule changed since this proposal was made. Nothing was saved." [Recalculate] |

Behavior:

- Already placed and started lots are listed for context, marked "Already placed", and are not part of the commit (AC-130).
- The proposal cannot be edited row by row. To change one lot, the planner commits and then moves that lot with the placement form, or cancels and places lots one by one.
- The commit sends the proposal unchanged (AC-136).
