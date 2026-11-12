---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 12
subsystem: calendar-phone-ui
tags: [razor, css-layers, stretched-link, guard-tests, phone-twin]
requires:
  - phase: 97-05
    provides: Gate 1 answers and the approved phone drawing (version 2)
  - phase: 97-11
    provides: foundation page-card pattern on the phone twins, guard lists
provides:
  - "Calendar/Index.Mobile as the approved agenda list: page card with h1, named month buttons, legend behind a toggle, flags, Today pill, shared empty state"
  - "calendar.mobile.css as one @layer pages block ported from the approved sheet"
  - "CalendarPhoneAgendaTests and PhonePageRegions (header region versus navigation menu)"
affects: [97-13, 97-14]
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarPhoneAgendaTests.cs
    - QuestBoard.IntegrationTests/Helpers/PhonePageRegions.cs
  modified:
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.mobile.css
    - QuestBoard.Service/wwwroot/css/quests.mobile.css
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarStyleTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
key-decisions:
  - "Board Availability and My Agenda leave the phone Calendar header (Gate 1 item 13); both stay in the phone navigation menu, which is untouched."
  - "The viewer's id reaches the phone view through ViewBag.CurrentUserId, the value the shared calendar partial already reads on Quest Details; votes and keys come from the data the calendar query already loads."
  - "The legend card takes the darker scrim band instead of a second sheet of glass: glass over the panel measured 3.6:1 for parchment text."
requirements-completed: [D-06, D-07, D-10, D-11, D-15, D-17, DoC-1, DoC-2, DoC-4, DoC-5, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 12: Phone Calendar Summary

The phone Calendar is the approved version-2 agenda list: every entry is a paper slip with one stretched
`a.row-nav-link` (no inline handler anywhere), the legend sits behind a "What do these mean?" toggle, own quest slips
carry the viewer's vote, the No building key flag and the finalized marker, the board's today section shows a Today pill,
and the header no longer carries the Board Availability and My Agenda buttons.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: stretched-link slips on the layered sheet, linked-row assertions rewritten, guard entries | done | a52c5999 |
| 2 Twin answers (h1, legend, vote/key/finalized flags, Today pill, empty state), header buttons removed, new tests | done | 3cd0443b |
| 3 Sweep against BEFORE and the approved frames; contrast and target fixes | done | b5daf8c6 |

Tracer gate: auto mode was not active, so the plan's interactive checkpoint would have stopped here; the orchestrator
asked for a full execution, so the slice was verified instead (as in earlier plans of the phase) and expansion followed.
Evidence: the rewritten linked-row facts, `EventsConversionGuardTests`, `UiFoundationLayerGuardTests`, `MobileViewsTests`,
`CalendarBoardClockTests` and `EventsDateRenderTests` all passed on the tracer commit, and a Chromium probe under the
Pixel 7 profile on 8300 tapped the bottom-right corner of a slip (not its title) and landed on `/Quest/Details/{id}`.

## Twin answers implemented

- **Header (Q-41):** the page card's `.modern-card-header` holds the one `h1` "Quest Calendar"; `agenda-card-mobile` is kept
  as a hook class because the board-type and horizon tests identify the phone agenda by it.
- **Month navigation:** `nav[aria-label=Month]` with `Previous month` and `Next month` as 44px `btn-secondary` links
  (the old `btn-primary` is gone), month name in the body face.
- **Also showing (Q-42):** kept, `fa-link` icon, board names only.
- **Legend (Q-40):** `btn btn-secondary calendar-legend-toggle` with `data-bs-toggle="collapse"`, `aria-expanded`,
  `aria-controls`, over a collapsed nested card holding every desktop legend string verbatim; the linked-board part
  (Dashed line, names, sentence) renders only for a board with linked boards.
- **Runway alert:** unchanged wording and gating (managers only), now `alert-link` links and no bottom margin inside the card.
- **Own quest slips (Q-43 to Q-45):** `Your vote: Yes|Maybe|No` flag (icon plus word) from the viewer's own vote on that
  date, `No building key` flag when neither the DM nor any signed-up player holds a key (same rule as the desktop month),
  finalized marker with the hidden word `Finalized.`, no DM name. Linked quest slips keep board name and DM name.
- **Today (Q-46):** `agenda-day-section--today`, `aria-current="date"` and a visible `Today` pill, from `ViewBag.BoardToday`
  (kept set; no null guard, as 97-04 left it).
- **Empty month (Q-47):** `.empty-state` with `fa-calendar-alt`, `h2` Nothing This Month, the verbatim sentence.
- **Linked slips (D-06, D-07):** open slips are one stretched link with the dashed edge, board name and DM; inert slips have
  no link, no tab stop, no data attribute and keep the hidden status word (`Event.`, `Cancelled.`, `Proposed.`, `Finalized.`).
- **Handlers removed:** 4 `onclick` attributes on the former row divs (events, own quests, linked events, linked quests).
  `grep -c onclick` on the view prints 0; the 22px inner links are now the 44px stretched links.

## Guards rewritten (old assertion -> new)

- `LinkedBoardMobileCalendarTests`: `Contain("window.location.href='/Quest/Details/{id}'")` and the Events twin -> the
  slip has `NotContain("onclick")` and exactly one `href="/Quest/Details/{id}"` (Events likewise), static-row
  `NotContain("onclick")`, `<a `, `tabindex` kept; `calendar-month-nav` ordering anchor -> `agenda-month-nav`; the hidden
  cancelled word `" Cancelled."` -> `"Cancelled."` (the slip renders the word after the marker, no leading space).
  Fact count unchanged (14).
- `LinkedBoardCalendarStyleTests`: the phone strike rows `.agenda-linked-entry--cancelled` and
  `... .agenda-linked-title` -> `.agenda-linked-entry--cancelled .agenda-slip__title` (the layered sheet strikes the title
  only, quieter paper for the slip), plus a new fact for the quieter paper on `.agenda-linked-entry--cancelled`
  and the `@layer pages` opener. `quests.mobile.css` fact unchanged. New facts for the also-that-day 44px link and the
  full-tone note and legend.
- `EventsDateRenderTests.PhoneCalendar_DayLabelAndTime_KeepTheirText`: the `div.agenda-day-label text-uppercase` and
  `small.text-muted` wrappers -> `h2.agenda-day-label` and `span.agenda-time`; same intent (wall-clock `time` elements with
  the culture-correct text).
- `CalendarButtonStyleTests` and `BoardPageEventsCalendarTests` (Gate 1 item 13, phone only): the phone variants were
  split from methods that covered both layouts and rewritten to "the Calendar page content carries no Board Availability
  and no My Agenda button (text or `/Events`, `/Agenda` links); the phone navigation menu still links `/Events` (DM, board
  allows Events) and `/Agenda`". Regions are cut by `PhonePageRegions` (the `main` element and the `nav.offcanvas-body`), so
  the navbar's own words cannot satisfy the page assertion. Desktop variants are the old assertions unchanged (97-14 owns
  them): `DesktopCalendar_BoardAvailabilityLink_DmSeesFilled_NotOutline` untouched,
  `Calendar_BoardAvailabilityLink_AbsentForPlayer` -> `DesktopCalendar_...` (single User-Agent), the two
  `Calendar_Events{Off,On}_...BoardAvailabilityButton` theories -> `DesktopCalendar_` facts plus new `MobileCalendar_` facts.
- Guard lists: `Calendar/Index.Mobile.cshtml`, `_Calendar.Mobile.cshtml`, `_LinkedAlsoThatDay.cshtml` -> `ConvertedViews`;
  `calendar.mobile.css` -> `ConvertedSheets`, `ViewSheetLinks`, `LayeredFiles`, `NoLiteralFiles`, `PhaseSourceFiles`;
  the new test file -> `PhaseSourceFiles`. `LinkedBoardStaticGuardTests` is unedited and passes.

## Partials

`_Calendar.Mobile.cshtml` and `_LinkedAlsoThatDay.cshtml` needed no edit: the vote rows already carry `aria-hidden` icons
with a hidden group label, and the also-that-day lines already carry the hidden `Proposed.`, `Finalized.` and
` Cancelled.` words (Phase 95). They were appended to `ConvertedViews` and pass the inline-code scan unchanged.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 1996 passed, 0 failed (1986 at the start; +8 `CalendarPhoneAgendaTests`,
  +2 style facts). SQL-backed settings: `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` with the user-secrets connection.
- Sweep `calm-crowded, calm-current, calm-empty, calm-legend-open, qd-open, qd-voting, qd-update, qd-spectator,
  qd-finalized` into `~/qb-ui-review/phase-97/after/97-12` (33 records, all 200). `readability-compare` with
  `--allow` (the removed header link and the renamed `main` key, per row) and `--zero-contrast`: **0 failing rows**; every
  phone record: `overflow false`, no small target, no missing focus ring, no `blurredRepeats`, **0 in-main contrast
  failures** (BEFORE: agenda dates and labels at 3.94 to 4.34:1). The command's final FAIL is the desktop residual on the
  three `calm-*` rows (14, 9 and 9 desktop in-main failures on the untouched desktop calendar), which 97-14 closes.
- `cal-dm-alert` (runway alert, phone): 0 contrast, no overflow, no new regression against BEFORE (see Known issues).
- Slip tap probe: a tap at the slip's corner opens the entry; link, legend toggle and month buttons measure 44px; smallest
  slip 71px; 52 links on the current month.
- Diffs against the approved frames (`mockups/v2/png`, overlays in `after/97-12/diff-*.png`): empty-390 differs in
  0.23% of pixels (a footer strip only); crowded-390-full and legend-open-390 differ on the wood backdrop (the board image
  is not pinned) and, from slip 39 of the crowded month, by 2 to 3px of accumulated height: the drawing shows
  "The Bellwright's Oath at Greywater" without a vote flag, the fixture viewer has voted Yes on it, so the slip carries the
  flag row. Paper edges match to the pixel up to that slip. No layout difference to fix.

## Findings closed

The phone answers named in the plan (A-events-009, 016, 022, 028, 030 on the phone; Q-40, Q-41, Q-44 to Q-47) are closed by
rendered result, each pinned by a fact: the 22px links and inline handlers (one 44px stretched link per slip, no handler),
the agenda dates/labels contrast (0 residual), legend, own vote, key, finalized, Today pill, empty state and h1
(`CalendarPhoneAgendaTests`), the inert linked rows (`LinkedBoardMobileCalendarTests`), and the Quest Details phone rows
(partials unchanged, qd sweep clean). The mapping of A-events ids is not re-derived here.

## Deviations from Plan

**1. [Rule 2 - Missing critical functionality] Viewer id for the vote flag**
- **Found during:** Task 2. The phone view had no way to know the viewer, so `Your vote` could not be rendered; the quest
  query already loads signups, votes and keys.
- **Fix:** `CalendarController.Index` sets `ViewBag.CurrentUserId` (the value the shared calendar partial reads on Quest
  Details). The desktop partial now receives it too; its only use there is inside the details-page branches, so nothing
  changes on the desktop month. File outside the plan's list.
- **Commit:** 3cd0443b

**2. [Rule 1 - Bug] Contrast of the Also showing note, legend text and empty sentence**
- **Found during:** Task 3 sweep. The ported sheet used the muted tone (4.25:1 on the panel for the note and the empty
  sentence) and the nested legend card lightened under its text (3.6:1 and 3.27:1).
- **Fix:** those texts take the full parchment tone; the legend card sets the scrim band and no glass image; the empty
  sentence rule is scoped to the page card. Re-swept: 0. Style facts pin it.
- **Commit:** b5daf8c6

**3. [Rule 3 - Blocking] 44px floor on the Quest Details also-that-day link**
- **Found during:** Task 3 sweep. The plan's phone assertion covers every `qd-*` record, and `qd-update` carried three links
  of 19 to 40px (also in BEFORE). `quests.mobile.css` (Phase 95's sheet, not in the plan's file list) gets
  `display: inline-flex; min-height: var(--qb-hit-min)` on `.also-that-day .row-nav-link`; its own style fact still passes
  and a new fact pins the floor. Re-swept: 0 small targets on every `qd-*` record.
- **Commit:** b5daf8c6

**4. Tracer gate** resolved by verification instead of an interactive stop (see Tasks).

**5. Tooling (outside the repository):** `manifest.json` and `tools/manifest.mjs` gave `calm-legend-open` its click on the
legend toggle (it was an `actions: []` row, so it captured the closed legend).

**6. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

## Known issues (recorded, not hidden)

- Desktop `calm-crowded`, `calm-current`, `calm-empty` still report 14, 9 and 9 in-main contrast failures: the desktop
  calendar is untouched until 97-14.
- `cal-dm-alert` phone: one inline link in the runway alert sentence measures under 44px; it was the same in BEFORE and the
  plan keeps the alert "as today". The two desktop widths of that row keep the 320px overflow already recorded in the
  handover facts.
- The phone twin's slips are about 35% longer than before; accepted at Gate 1.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or schema; the server's `CanOpen` decision is unchanged (T-97-36), inert slips carry no
link, tab stop or data attribute (T-97-37, rendered facts), and Quest Details' vote inputs, names, classes and order are
untouched (T-97-38; the vote-row partial is unedited and Quest Details suites pass).

## Self-Check: PASSED

Files found: `Index.Mobile.cshtml`, `calendar.mobile.css`, `CalendarPhoneAgendaTests.cs`, `PhonePageRegions.cs`; commits
a52c5999, 3cd0443b and b5daf8c6 present; `grep -c onclick` on the view is 0; `calendar.mobile.css` has 0 `!important`,
0 colour literals and 0 `@media`.
