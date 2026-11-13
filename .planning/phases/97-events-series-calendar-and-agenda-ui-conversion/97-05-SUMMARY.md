---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 05
subsystem: review-mockups
tags: [ui-conversion, gate-1, calendar, phone-agenda, events-details, mockup, scratch-only]
requires: ["97-03"]
provides:
  - "~/qb-ui-review/phase-97/gate1/index.html (Gate 1 sheet v2), items.md, answers.json, publish.log"
  - "~/qb-ui-review/phase-97/mockups/v1/{phone-calendar,event-details,forms-title}/ and mockups/v2/{calendar,phone-calendar}/"
  - "gate1/answers.json with the keys the Calendar, Agenda, Events and Series slices check in their preconditions"
affects: [97-06, 97-08, 97-09, 97-11, 97-12, 97-13, 97-14, 97-15]
key-files:
  created: []
  modified: []
decisions:
  - "Gate 1 approved: calendar port v2, phone agenda list v2, Events/Details reorder v1; items 1 to 12 as recommended (8 key-only); item 13 removes the Board Availability and My Agenda buttons from the Calendar header on desktop and phone"
  - "The legend stays a strip plus popover; the optional one-row header variant was not chosen"
  - "No Agenda merge; the twins stay"
  - "No review branch was published (orchestrator override): the operator reviewed from local files"
metrics:
  completed: 2026-11-12
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 97 Plan 05: Gate 1 package and operator answers Summary

The Gate 1 package (phone agenda mockup, Events/Details reorder mockup, Create/Edit title comparison, Agenda merge note, items 1
to 13, planner assumptions, one self-contained sheet) was built under `~/qb-ui-review/phase-97/`, redrawn once as version 2 for
the Calendar header, and approved by the operator. The answers are in `gate1/answers.json`. No repository source file was
created or changed; the only repository change of this plan is this SUMMARY.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: one phone agenda frame end to end | done: `mockups/v1/phone-calendar/` (planned markup, shippable `calendar.mobile.css`), captured under Pixel 7 with touch, probes clean | none (scratch) |
| 2 Remaining frames, merge note, items, assumptions, sheet | done: `mockups/v1/event-details/`, `forms-title/`, `gate1/index.html`, `items.md`, `build-gate1.py`, `publish-review.sh`, stage `gate1-branch/` | none (scratch) |
| 3 Gate 1 decision | answered by the operator; redraw loop run once (version 2); closed | this SUMMARY |

## Frames and versions

- **Phone agenda list** (`#phone`): 11 `phone-calendar-*` frames at 390 and 412 beside BEFORE (first screen, legend closed and open,
  votes and flags, today pill, empty month, focus ring), plus `phone-dm-runway-alert-390`. All clean: phone layout, no overflow,
  no target under 44px, zero contrast failures, no blur on a slip, 14px minimum, ring on the slip. **v1** had the button stack;
  **v2** removes it (first day starts at 353px instead of 413px, page 4,044px instead of 4,104px).
- **Events/Details** (`#event-details`): player, Dungeon Master, series occurrence, cancelled, Dungeon Master cancelled and answered
  with Withdraw, at 1440 and 390 (320 for three), each beside a live BEFORE; phone Dungeon Master order A (DOM order) and B
  (Availability first). Scroll width is 390 at 390 (was 394). Version 1 only.
- **Calendar port** (`#calendar`, `#header`, `#truncation`, `#legend`): the 97-03 frames, re-rendered as **v2** without the two
  header buttons (43 frames, fit/contrast/ring/text clean, behaviour 58 of 58), with the **v1** header frames beside them.
- **Create/Edit title** (`#forms-title`): BEFORE parchment `h2`; gold `h1` 7.63:1; parchment option 9.88:1.
- **Agenda merge note** (`#agenda-merge`): verdict, two reasons, cost list; nothing drawn.

## Gate 1 answers

The operator wrote "i think all recommendations are good", then asked for item 13 and approved the v2 header redraw ("yes redraw
is approved"). Every answer below is the recommendation on the sheet; none was defaulted by silence.

| Item | Answer |
|---|---|
| Calendar port | approved, version 2 |
| Phone agenda list | approved, version 2 |
| Events/Details reorder | approved, version 1 |
| 1 truncation | keep the time |
| 2 month text | 14px |
| 3 today | parchment (ring, pill, hidden word) |
| 4 phone Dungeon Master order | DOM order; cancelled alert directly above Your Availability |
| 5 legend | strip plus popover (the optional one-row variant was not chosen) |
| 6 runway alert | the page scrolls by the alert height |
| 7 narrow windows | marker-only 24px tiles below 768px |
| 8 trailing markers | key-only (plan default was both; the operator accepted the recommendation) |
| 9 Agenda merge | no merge |
| 10 legend sentence | verbatim |
| 11 fit values | accept the two 2px values and the bottom page padding of 0, as on the sheet |
| 12 Create/Edit desktop `h1` | gold |
| 13 (operator's request) | remove Board Availability and My Agenda from the Calendar page header, desktop and phone; both stay in the navbar (Dungeon Master menu, account dropdown, phone nav); the Events pages keep their My Agenda button |

## Drawings approved

| Drawing | Path | Version |
|---|---|---|
| Calendar (desktop month, header, legend, panel, cards) | `~/qb-ui-review/phase-97/mockups/v2/calendar/index.html` | 2 |
| Phone agenda list | `~/qb-ui-review/phase-97/mockups/v2/phone-calendar/index.html` and `calendar.mobile.css` | 2 |
| Events/Details | `~/qb-ui-review/phase-97/mockups/v1/event-details/index.html` (phone `index.mobile.html`) | 1 |

## Agenda merge

Not proposed and not requested: `merge` is false. Agenda/Index stays two twins (layout-only but not CSS-only); the Agenda slice
(97-11) runs unchanged.

## Planner assumptions

The operator did not answer the planner assumptions individually. Recorded in `answers.json` as follows; none is an explicit
operator answer.

| Assumption | Status |
|---|---|
| 1 server-render and enhance | corrected-by-measurement: the calendar page does not know the viewer's vote (only Quest Details reads it); the vote markers and flags need it added to the calendar view model |
| 3 status and key separate markers | corrected-by-measurement: same vote-data fact (the markers themselves were not contested) |
| 8 Phase 96 shares the checkout | corrected-by-measurement: out of date; Phase 97 ran in its own worktree in parallel; the phone `.row` gutter rule does not exist in this tree (97-08 must apply it); Phase 96 facts recorded here are provisional until it merges |
| 2, 4, 5, 6, 7, 9, 10, 11, 12 | not-contested |

Plan facts corrected by measurement (also in `answers.json`, `planFactCorrections`): no one-row header from 992px (the header is
two rows, 73px, 98px for a board with linked boards between 992 and 1,199px, so a "at most 48px, one-row state" fact has nothing
to measure); entries wrap to two or three lines from 768 to 991px; bottom page padding is 0 (accepted through item 11); phone
slips make the month about 35 percent longer; a six-week month at 992x720 overshoots by 24px.

## Review branch

None. By orchestrator override no review ref was created or pushed: the operator read the sheet from local files
(`~/qb-ui-review/phase-97/gate1/index.html`). `publish-review.sh` was run with `--dry-run` only (throwaway bare repository, no
write to the repository's refs or objects): `assert=pass`, `commit=none`, `pushed=no`, checkout HEAD, branch and status
unchanged; the tree is `README.md`, `index.html` and `img/` only. `reviewCommits` is `[]`.

## Redraw history

- **v1:** phone agenda mockup, Events/Details reorder, Create/Edit title, Agenda note, items 1 to 12, assumptions; calendar frames
  from 97-03.
- **v2 (item 13):** the Board Availability and My Agenda buttons removed from the Calendar header on desktop and phone.
  Re-rendered every calendar frame and the phone calendar frames; re-measured the header: row 1 45px in both versions and on one
  line from 992px, whole header 73px in both (98px with linked boards between 992 and 1,199px), narrow headers 207.6 to 168.6px
  (991 and 768) and 266.2 to 227.2px (320). The legend strip, not the buttons, is what makes the second row; one row would need
  about 1,459px (linked boards) or 1,216px (none) against 1,262px, so no one-row header as drawn. An optional one-row variant
  (strip folded into the popover) was drawn and not chosen. Fit, behaviour (58 of 58), contrast and small-target probes clean.

## Deviations from Plan

1. **[Orchestrator override] No review branch and no push.** The sheet and stage were built and the tree assertion kept as a
   hygiene check (`--dry-run`); the checkpoint text about the review ref and `git clone` is replaced by local files.
2. **[97-03 findings carried]** the six findings (two-row header from 992px; container narrows cells to 159 and 185px; both
   trailing markers leave 3.6 characters at 1280; bottom padding 0; 768 to 991px entries wrap; three-letter weekdays under 768px)
   and the vote-marker note are on the sheet and shaped the recommendations (item 8 became key-only).
3. **[New finding] A six-week month at 992x720 overshoots by 24px** in version 1 and version 2 (legend wraps, header 98px); the
   earlier extras used 768px of height. It fits at 1100x800.
4. **[Rule 3] Phone data** came from 97-03's `month-data.json` (the phone view lists the same entries as the desktop month);
   the phone shell and BEFORE frames were captured live from the frozen baseline.
5. The runway-alert frame is named `phone-dm-runway-alert-390`, apart from the `phone-calendar-*` set, because the series name
   in the alert is an inline text link (22px, as today) that the small-target probe lists.
6. Mockup-only glue (`phone-frame.js`, `evd-frame.js`, `forms-frame.js`, `frame-extras.css`) uses classic scripts for file://;
   phone Order B is a mockup-only visual reorder. "Move to Linked Board" is not on the fixtures' pages, so its `btn-info` role is
   not drawn.
7. Scratch `mockups/v1/_saved/*.html` holds saved baseline pages (with antiforgery token values), outside the repository and
   outside the package.

## Gate state

Gate 1 is closed (approved). Nothing beyond Gate 1 was started. The phase holds until phase 96 merges into `feature/phase-97`;
the provisional `phase96-landed.md` must be re-recorded then, and 97-08 re-checks the phone gutter rule.

## Known Stubs

None. No repository source file was added or changed.

## Threat Flags

None. The review package holds only README.md, index.html and img/; no credential, connection string, localhost URL or account
address (asserted); nothing was published.

## Self-Check: PASSED

Present: `gate1/{index.html,items.md,answers.json,message.txt,publish.log}`, `gate1-branch/{index.html,README.md,img/}`,
`mockups/v1/{phone-calendar,event-details,forms-title}/index.html`, `mockups/v2/{calendar,phone-calendar}/index.html`,
`tools/{build-gate1.py,publish-review.sh}`; `answers.json` parses and carries `calendar`, `phoneCalendar`, `eventDetails`,
`agenda`, `eventForms`, `assumptions`, `items` (1 to 13) and `reviewCommits`; both sheets open from file:// with 0 failed
requests; `git status --porcelain` showed no repository change other than this file.
