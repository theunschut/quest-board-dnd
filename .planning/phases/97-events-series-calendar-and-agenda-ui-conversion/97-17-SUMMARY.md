---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 17
subsystem: gate-2-review
tags: [gate-2, operator-approval, exceptions, local-package, release-handoff]
requires:
  - phase: 97-16
    provides: the final AFTER capture, accessibility record, counts, test inventory and the three pending exceptions
  - phase: 97-05
    provides: the Gate 1 answers and approved drawings shown beside the relaid-out pages
provides:
  - "gate2/index.html: the Gate 2 review sheet (9 sections, 108 pairs, 251 images), local only"
  - "gate2/answers.json: the operator's Gate 2 approval and the exception answers"
  - "contrast-exceptions.json: exceptions 1 to 3 carry approved.status approved"
affects: [release]
actuals:
  tokens: 13700
  tasks: 3
  commits: 1
key-files:
  created:
    - .planning/phases/97-events-series-calendar-and-agenda-ui-conversion/97-17-SUMMARY.md
  modified: []
key-decisions:
  - "Gate 2 approved with all three exceptions accepted; t1 to t5 deferred and fixtures kept, taken from the orchestrator's recommendations under the operator's delegation."
  - "The Gate 2 package stayed local: no review ref, no tag, no push."
requirements-completed: [D-03, D-08, D-11, D-12, D-13, D-15, D-17, DoC-4, DoC-5, DoC-6, DoC-7, DoC-10]
status: complete
---

# Phase 97 Plan 17: Gate 2 Summary

The Gate 2 sheet was built locally from the final AFTER capture and the operator approved it, every pair and the three recorded exceptions. No repository source file changed in this plan; the only repository change is this summary.

## Gate 2 decision

**Approved**, 2026-11-12. The operator in chat: "looks good. i'd say approved. if needed take recommendations".

The operator approved the gate and every pair, and delegated the open items to the orchestrator's recommendations. The answer string recorded in `gate2/answers.json`:

`approved; e1:accept e2:accept e3:accept; r1:ok r2:ok r3:ok r4:ok; t1:defer t2:defer t3:defer t4:defer t5:defer; fixtures:keep`

Which answers were the operator's own and which were taken from recommendations: the approval of the gate and of every pair is the operator's. The exception answers (e1 to e3), the four confirmations (r1 to r4), the five deferrals (t1 to t5) and the fixtures answer were taken from the orchestrator's recommendations under that delegation. `answers.json` marks each with its source. No pair was rejected, so there was no gap-closure loop.

## Exceptions

Recorded in `contrast-exceptions.json` (`approved.status` is now `approved`, date 2026-11-12) and `gate2/answers.json`. Measured on the rendered page (`after/final/exception-probe.json`).

| # | Exception | Measured | Answer |
|---|---|---|---|
| 1 | Compact entries and "+N more" under the 24px target (WCAG 2.5.8), desktop month only | row 20px, link 18px, "+N more" 20px; reachable at 50.6px panel rows (71.2px for two lines), the card and the entry's own link | accept |
| 2 | Dashed outline of a linked entry under the 3:1 non-text bar | 1.66:1 against the dark cell, 7.72:1 against its own fill; the glyph, hidden words, card and panel carry the meaning | accept |
| 3 | Focus ring offset 0px on compact entries and "+N more" (z-index 2), 1px on panel rows, instead of the foundation 2px | 2px two-tone ring, colours and width unchanged; a 2px offset would be clipped by the cell | accept |

Confirmations r1 to r4, all `ok`:

- r1: the guard `CalendarSheet_KeepsMonthAndQuestDetailsRulesApart` was loosened to also accept `.cal-card`, `.cal-panel` and the from/to keyframe steps, because the overlays are mounted on the body. It is the one guard in the phase whose strictness went down.
- r2: the called-off linked chip on Quest Details is now muted ink on quieter paper.
- r3: Board Availability and My Agenda left the Calendar header (already approved at Gate 1).
- r4: the Maybe badge lost its `text-dark` utility.

The five Phase 95 weekday-header exceptions on Quest Details stay retired (3.76:1 before, 5.63:1 after).

## Deferred follow-up (t1 to t5)

Desktop controls under 24px that pre-date this phase, outside exception 1. All are unchanged from BEFORE, so none is a regression; fixing them would reopen a phase that is otherwise finished. Deferred to a later phase and not added as an exception:

- t1: Events overview event title links, 22px tall
- t2: Series occurrence "View" links, 35x22
- t3: Quest Details calendar quest links, 22px tall
- t4: Agenda board-filter checkboxes, 21px
- t5: below 768px the marker-tile link is 22px inside a 24px tile

## Host culture (informational note)

Shown on the sheet, not a question. The moved date lines are identical to the lines they replaced by construction (the new styles format with the current culture, as the replaced calls did, and `datetime` stays invariant). Date-lines probe: 18 of 18 keys and 117 visible texts equal to the frozen baseline. 17 unit rows for the styles and 18 render rows for the moved lines pass under the host culture and nl-NL; forcing the invariant culture failed 8 nl-NL rows and was reverted. The production LANG no longer affects correctness.

## Review branch

None. Gate 2 was local only (see Deviations). Review commits: none.

## Release handoff

The phase is ready for one release, the next minor (about v6.2.0), through the existing draft-release deploy approval gate. The timing is the operator's. Every slice is on the phase branch; the orchestrator pushes branches. This plan created no tag and pushed nothing.

Caveat: `docker-publish.yml` publishes the image on every tag push, regardless of the draft-release approval. A tag push is therefore a publication of the image.

## Fixtures for the operator

Kept (answer `keep`) so later re-verification can use them. Created through the app's own forms, no SQL writes; roles by key.

- Accounts: `p97player` (Player on the Audit Board and Emberfall Wardens), `p97dm2` (Dungeon Master on both fixture boards), `quiet` (Quiet Hollow only, removed from the Audit Board). `p97dm2`'s building-key flag was set through Admin > Edit user (previous value off); it is not restored, by decision. No existing account was changed.
- Boards (One-Shot): Emberfall Wardens, Saltmarsh Concord, Quiet Hollow. Emberfall Wardens and Saltmarsh Concord joined the existing linked set "Audit Pair of Lanterns" (a board belongs to one set only).
- Events and series: the crowded May 2027 day (seven entries: Moth Court, Cartographers' Muster at Greywater, Drowned Lantern of Vael: Second Telling, Embers of the Quiet Throne Revel, Glass Moth Auction, The Hollow Crown Accord, Final Reading, Midsummer Vigil) and its filler days, Lanternlight Muster, Tallow Day Gathering, Ember Wardens Briefing, Frostmantle Moot, the January 2027 five-week events, linked-board events (Wardens' Muster at Cinderfall, Harbor Bell Muster) and the series Gloamhaven Weekly Muster, Emberwatch Rota, Tidewatch Rota and Cold Hearth Rota, with the cancelled occurrences.
- Quests: The Candlelit Ledger of Wren Hollow, The Bellwright's Oath at Greywater, The Unlit Stair of Hallowmere, and the linked-board quests The Ember Weir Reckoning, Siege of the Cinder Gate, Tidewrack Parley, The Brinebound Charter.
- Full keyed list with ids and app paths: `~/qb-ui-review/phase-97/fixtures.json` (and the Decisions section of the sheet).

## Deviations from Plan

**1. [Orchestrator override] Gate 2 is local only.** The plan publishes an orphan review branch with `publish-review.sh` and verifies it with `ls-remote`. Instead the package was built under `~/qb-ui-review/phase-97/gate2/` (`index.html`, `README.md`, `img/`) and verified locally: the page loads over file:// with no failed request, all nine section ids are present, no localhost, 127.0.0.1 or example.invalid string appears in the page or README, and all 251 images resolve. Not produced: `publish.log`, `message.txt`, `commit.txt`, `gate2-branch/`, the ref `feature/phase-97-gate2-review`. No tag, no push. The plan's `reviewCommits` list is therefore empty.

**2. [Tooling] New builder.** The plan named `gate2-sheet.mjs`, `gate2-images.py` and `webp.py` (copied from Phase 95); they were not used. `tools/build-gate2.py` was written instead, which the plan allowed.

**3. [Capture caveat] Full-page month frames.** The AFTER month frames are full-page captures, which grow the window and show the slot count of the taller window (as recorded in 97-15). The sheet says so and points to the window-sized Gate 1 frames and the fit table for 1280x720. The card, panel, focus and phone frames are window-sized.

**4. [Scope] After-only state rows.** `cal-card-own`, `cal-card-inert`, `cal-focus` and `cal-panel-open` have no BEFORE; the sheet says so and shows the approved Gate 1 frame beside them. `cal-card-inert` and `cal-focus` are paired with 1280x720 Gate 1 frames beside 1440x900 captures, captioned.

**5. [Scope] Phone frames.** Pairs use the 390px phone captures; the 412px captures remain in the sweep record. `cal-sixweek` pairs have no Gate 1 frame (the month shape is the crowded month's).

**6. [Answers] Delegated.** The exception, confirmation, target and fixture answers were taken from the orchestrator's recommendations under the operator's delegation (see Gate 2 decision).

## Screenshots and publication caveat

The Quest Details frames (qd-open, qd-voting, qd-update and similar, desktop and phone) show the review account's display name and its placeholder address in the "Signing up as" strip and the navbar, in pixels. The sheet's text does not contain the address, so the text check passes, but the images are not masked. Mask those frames before the package is ever published or shared beyond the local machine.

## Known Stubs

None.

## Threat Flags

None. No endpoint, auth path or schema change; no repository source changed. T-97-50 (information disclosure): the package is local only and was never pushed; the pixel caveat above applies if that changes. T-97-51 and T-97-52: no git plumbing was run against the shared checkout, no ref was created, no tag, no push. T-97-53 (exceptions shipped without sign-off): all three carry an explicit recorded answer.

## Self-Check: PASSED

Present: `~/qb-ui-review/phase-97/gate2/index.html`, `README.md`, `img/` (251 files), `operator-answers-pending.md`, `answers.json`; `contrast-exceptions.json` shows exceptions 1 to 3 `approved`; `tools/build-gate2.py`. No review ref, no tag. Port 8301 left running; 8300, 8000 and 8001 not used. STATE.md, ROADMAP.md and REQUIREMENTS.md untouched.
