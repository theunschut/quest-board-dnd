---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 15
subsystem: calendar-month-module
tags: [es-module, progressive-enhancement, overlays, guard-tests, behaviour-check, fit-sweep]
requires:
  - phase: 97-14
    provides: the server-rendered compact month with hidden detail blocks, week-count classes and the slot variable
  - phase: 97-05
    provides: Gate 1 answers (runway alert scrolls, fit gap 2px, truncation time, trailing markers key-only)
provides:
  - "calendar-month.js: slots and one +N more per crowded day, the shared detail card, the non-modal day panel"
  - "Server-rendered card and panel shells in Calendar/Index, moved to the body by the module"
  - "Card, panel and +N more rules in calendar.css, with the reduced-motion block"
  - "Guard facts for the module and the shells; real-page behaviour suite and fit check"
affects: [97-16, 97-17]
actuals:
  tokens: 10500
  tasks: 3
  commits: 2
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/calendar-month.js
  modified:
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.IntegrationTests/Tests/CalendarMonthMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
key-decisions:
  - "The panel closes an outside press without any timer: the press closes it at pointerdown and the click that ends the press hands focus back to +N more when the press did not land on a control, so the module has no timer on the open or close path of the panel."
  - "The sheet-scope guard accepts the month's two overlay roots (.cal-card, .cal-panel) and the fade keyframe steps, because the overlays are mounted on the body and cannot sit under the page wrapper; no other page renders them."
  - "The panel heading and the +N more suffix use the cell's server date text (Wednesday, May 12), as the plan says, so the heading carries no year; the detail card shows the full date."
requirements-completed: [D-01, D-02, D-03, D-04, D-05, D-06, D-15, D-17, DoC-1, DoC-5, DoC-10]
status: complete
---

# Phase 97 Plan 15: Calendar module (slots, "+N more", detail card, day panel) Summary

The desktop month now fits its window with every entry still reachable: a crowded day shows its slots minus one entries and a real "+N more" button that opens a non-modal day panel on click, tap, Enter or Space; every entry opens a hoverable detail card on hover, focus and tap; and a reader without JavaScript keeps every entry in a scrolling cell.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: crowded day end to end (slots from CSS, overflow hidden, +N more, panel, focus return) with the module's static guard | done | 90ad1ad6 |
| 2 Detail card on hover, focus and tap, inert entries, title removal, reduced motion; full behaviour suite on the real page | done (card code ships in the Task 1 module; Task 2 adds the load-only-where-used fact) | d4d0192d |
| 3 Fit, contrast and focus sweep at every size, after-only card, panel and focus frames | done, no source change was needed; evidence only (outside the repository) | none |

Tracer gate: auto mode was not active, so the plan's interactive stop would have applied after Task 1. The orchestrator asked for the whole plan, as it did for 97-14, so the tracer's verification was run end to end before expanding: the guard, markup and Quest Details suites (112 facts) green and the panel behaviour suite on the real page (40 of 40, `behaviour-panel.json`) green on the tracer commit; Tasks 2 and 3 followed.

## What was ported, and what changed from the mockup

`calendar-month.js` is the approved mockup module with these production changes:

- **Shells from the server.** The mockup built the card and panel with `createElement`; production reads the two server-rendered shells (`div.cal-card`, `div#cal-panel` with `#cal-panel-title`, `p.cal-panel__count`, `button.cal-panel__close`, `ul.cal-panel__rows`) and moves them to `document.body` at start-up. The shells sit after the month wrapper in `Calendar/Index.cshtml`, so a blurred ancestor never holds them even before the move. If either shell is missing the module leaves every entry visible.
- **Markup of 97-14.** Rows are `li.cal-entry` in `ul.cal-entries`; the date text comes from the cell's hidden `<time>`; the panel rows are built from the entry's marker, the `.cal-detail__facts` pairs and its status line (the `, linked board` suffix is dropped in the row because the link glyph says it). Linked entries a viewer may open are links, the others plain `div` rows with no link, no `tabindex`, no control.
- **No timer on the panel path.** The mockup deferred the focus decision for an outside click with `setTimeout(0)`; production does it from the click that ends the press (see decisions). A guard fact pins that `openPanel` and `closePanel` contain no `setTimeout`, `setInterval` or `requestAnimationFrame`.
- **Unchanged behaviour:** hover card after 120ms with 180ms grace over the 8px bridge, Esc until the pointer really moves, first tap opens and second tap follows, inert entries open on hover and tap only (no focus path, nothing adds focusability), `title` removed from compact entries, Tab loop, one panel, focus return, `matchMedia` recompute on the 992px width and the 712, 850, 980 heights, whole enhancement in `try/catch` that restores every entry.
- **Sheet.** The card, panel and `+N more` rules join `calendar.css` (overlay surface `--qb-surface-overlay`, `--qb-shadow-overlay`, 352px card, 420px panel with `max-width: calc(100vw - 16px)`, 48px rows with a 24px marker, fade over `--qb-dur-fast`, reduced-motion block). The status-marker rules are shared with the overlays through grouped selectors. All colour comes from tokens, no `!important`. The card and panel CSS landed in Task 1's commit (the tracer needs positioning to be proved), not Task 2's.

## Guards

Added to `EventsConversionGuardTests`: `CalendarMonthModule_WritesNoMarkupFromStrings` (no `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `cssText`; `.style.` only with `setProperty`/`removeProperty`; only `--pop-x`/`--pop-y`), `CalendarMonthModule_ReadsTheSlotCountAndHasNoHoverOpen` (reads `--cal-slots`; no hover or pointer-move listener reaches the panel opener; no timer in the opener or closer; no mode switch), `CalendarMonthModule_MountsOverlaysOnBody`. The module joins the planning-reference scan. Added to `CalendarMonthMarkupTests`: `Shells_RenderOnlyOnTheCalendarPage` (the shells, their roles, labels, the module script, none of it on Quest Details) and `Module_IsLoadedOnlyByTheCalendarPage` (static scan of every view). `CalendarSheet_KeepsMonthAndQuestDetailsRulesApart` now also accepts the month's overlay roots and the keyframe steps (see Deviations); nothing else was loosened.

## Behaviour results (real page, `behaviour-check.json`)

`calendar-behaviour-live.mjs` (new, ported from the mockup suite) on `cal-crowded` (p97player: member and inert linked entries, a vote) and `cal-dm` (Audit Board DM: every linked entry inert) at 1280x720 and 1440x900: mouse, `hasTouch`, `reducedMotion: 'reduce'` and `javaScriptEnabled: false` contexts, a slot sweep at 8 sizes in two months, and source checks. **211 of 211 pass, 60 distinct checks.** Every one of the 58 checks the approved mockup passed is present and passing (including `overlays-on-body`, `panel-no-hover-open`, `no-plus-one`, `nojs-all-visible`, `focus-return`, `tab-loop`, `esc-card`, `second-tap-follows`, `reduced-motion`). Two extras were added: `crowded-day-collapses` (the seven-entry day shows 2 entries and "+5 more" at 1280x720, 3 entries and "+4 more" at 1440x900) and `panel-inert-rows-not-focusable` (the T-97-44 mitigation: inert panel rows are `div`s with no link, tab stop or control).

Adaptations of the mockup checks to production data (assertions kept, expectations re-pointed): the panel heading is the cell text `Wednesday, May 12`; the card's vote line is asserted for the viewer who has a vote and its absence for the DM; the inert entry is the first visible inert row of the page; `module-form` asserts the shipped page loads the script as `type="module"` and enhances; `no-network` asserts nothing leaves the app origin; `second-tap-follows` waits for the quest page URL instead of a hash.

## Fit table (module running, `fit-check.json`)

Document does not scroll and no cell's content overflows, May 2027 six-week (`cal-crowded`, `cal-sixweek`), the five-week month, the empty month and the current month:

| Size | Slots | Result |
|---|---|---|
| 1280x720 | 3 | fits, 0 cells overflow |
| 1366x768 | 3 | fits, 0 |
| 1440x900 | 4 | fits, 0 |
| 1536x864 | 4 | fits, 0 |
| 1920x1080 | 5 | fits, 0 |
| 1280x1024 | 5 | fits, 0 |
| 1280x700 | 2 | fits, 0 |

Without the module (97-14) two to four cells per month scrolled their own list; with it none does. Geometry re-measured (`fit-measure-97-14.mjs`): card bottom equals the window at 1280x720, 1440x900 and 1100x800; at 992x720 the card bottom is 744 (24px over), the known overshoot.

Runway alert (Gate 1 `scroll`): the page scrolls by the alert's height and nothing clips. `cal-dm` (two-line alert) 97px at 1280x720, 49 at 1366x768 and 1440x900, 85 at 1536x864, 57 at 1280x1024, 1 at 1920x1080, 0 at 1280x700; `cal-dm-alert` (one-line) 73, 25, 25, 61, 33, 0, 0 at the same sizes; no cell overflows in any.

Other widths: 991, 768 and 320 desktop: no horizontal overflow on any calendar row (BEFORE had `cal-dm` and `cal-dm-alert` overflowing at 320; neither does now). At 991 the module is off (`--cal-slots` 0, nothing hidden, no "+N more"); crossing 992 recomputes without a reload (both checked).

## Sweep and readability results

12 rows (`cal-crowded, cal-sixweek, cal-fiveweek, cal-empty, cal-current, cal-dm, cal-dm-alert, cal-panel-open, cal-card-own, cal-card-inert, cal-focus, cal-nojs`), 95 records, none non-200, run in four batches with `--merge` (no hang). Zero console errors, page errors or failed requests; no action or assert failure. `readability-compare` against BEFORE with `--zero-contrast` on every row, per User-Agent:

- desktop: 0 failing rows, 0 residual in-main contrast failures;
- phone: 0 new problems from this plan; the one reported item is the series link inside the runway alert (22px inline link, `cal-dm`), unchanged from 97-14.
- the after-only state rows (open panel, own and inert linked card, keyboard focus) report no contrast failure, no missing focus ring, no small target, no overflow; the overlays' text is inside the probe's tested set.
- allow file: entries folded behind "+N more" at some size are listed as removed (they are hidden there and reachable through the panel); names are renamed as in 97-14.
- the BEFORE vs AFTER caveat for the sweep screenshots: a full-page capture grows the window, which crosses a height band and re-applies the slots, so the full-page month frames show the slot count of the taller window. Probes run before the growth and read the real window. The four state rows are captured as the window shows them (new `viewportShot` row flag, taken before the probes move focus).

## Deviations from Plan

**1. [Plan scope] Card and panel CSS in Task 1's commit.** The tracer has to prove positioning and anchoring on the real page, so the overlay rules were part of the tracer commit; Task 2 had nothing left to add to the sheet. Commit 90ad1ad6.

**2. [Rule 3 - guard maintenance] `CalendarSheet_KeepsMonthAndQuestDetailsRulesApart`.** The overlays are mounted on the body, so their selectors cannot start with `.calendar-page`. The guard now allows selectors starting with `.cal-card` or `.cal-panel` (rendered by the month alone, proven by `Shells_RenderOnlyOnTheCalendarPage` and the Quest Details fact) and the `from` and `to` steps of the fade keyframes, which the selector scanner read as unscoped rules. The protection it gives (a month rule never reaching Quest Details, and the reverse) is unchanged. Commit 90ad1ad6.

**3. [Rule 1 - Bug] Static-scan helper.** The first version of the hover-opener fact walked to the next brace after an expression-bodied listener and read past the end of the file; it now takes the call's own parenthesised arguments. Commit 90ad1ad6.

**4. Panel heading has no year.** The approved drawing shows `Wednesday, 12 May 2027`; the plan says the title comes from the cell's date text, which is `Wednesday, May 12` (the board month and year are on the page). The detail card shows the full date. A year would need a new server value on the cell; not added.

**5. `Module_IsLoadedOnlyByTheCalendarPage` is a static scan of the views** in `CalendarMonthMarkupTests` (no request needed); `Shells_RenderOnlyOnTheCalendarPage` also asserts Quest Details renders no shell and no script tag.

**6. Plan verification command for fit** lists `cal-dm` among the rows that must not scroll at the fit sizes; the approved Gate 1 answer for the runway alert is `scroll`, so `cal-dm` is held to "no cell overflows" and its scroll amount is recorded in `alertRows` of `fit-check.json`. The other three month rows are held to no scroll and no overflow.

**7. Sweep tooling (outside the repository):** `calendar-behaviour-live.mjs`, `fit-check-97-15.py` and `allow-97-15.py` are new; `sweep.mjs` gained `hover` and `focus` actions and the `viewportShot` flag, `manifest.mjs` gained the actions and assertions of the four state rows. The panel-open frame is captured with the panel open at 1280x720 and 1440x900.

**8. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

## Known issues (recorded, not fixed)

- A six-week month at 992x720 overshoots the window by 24px (legend wraps, header 98px); it fits from 1100x800.
- On a phone the series link inside the runway alert measures 22px (unchanged, inline link).
- The `ev-index` fixture drift (`Tallow Day Gathering` pushing `Candlemas Feast at the Hollow` off the list) is not a code change.
- Resizing the window across a height band closes an open card or panel (the slots are recomputed); the panel is not repositioned.
- `.claude/ui-guidelines.md` line 63 still says `calendar.css` is not in a layer (it has been since 97-13); not touched here.

## Exceptions to carry into the exceptions record (97-16)

- **D-03 row height:** compact month rows and "+N more" are 20px tall on desktop (the approved fit rule); the same entries are reachable at 48px rows through the day panel and the detail card. Below 768px the entries are 24px marker-only tiles.
- **Dashed outline of linked entries:** non-text contrast of the dashed outline, recorded by 97-16 as the non-text exception.
- The overlay (card, panel) text and its focus rings pass the bar with no exception.

## Findings closed

A-events-013 at the fit sizes (the month fits; nothing clips), A-events-018 at the fit sizes (entries reachable by keyboard and through the panel, focus ring visible on entries and "+N more" at offset 0 and on panel rows at offset 1px), A-events-034 (the full detail of an entry reaches hover, keyboard and touch users). Pinned by the behaviour suite and the guard facts above.

## Known Stubs

None.

## Threat Flags

None. No endpoint, auth path or schema; the module reads only markup the server rendered. T-97-43 (DOM XSS): content only by `cloneNode` of server-encoded nodes and `textContent`, pinned by `CalendarMonthModule_WritesNoMarkupFromStrings`. T-97-44 (inert entries as links): panel rows for inert entries are `div`s, no link or `tabindex` is added, checked by `panel-inert-rows-not-focusable` and `inert-markup`. T-97-45 (enhancement failure hides entries): `try/catch` restores every entry; the no-script case proves every entry visible.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 2024 passed, 0 failed (2019 at the start; +5: three module guards, the shells fact, the load-only-where-used fact). `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` with the user-secrets connection; no CLR flake.
- Real-page behaviour suite: 211 of 211, every mockup check present.
- Fit sweep: no page scroll and no overflowing cell at the seven fit sizes in the five- and six-week months; no "+1 more" control anywhere.
- No planning reference in the changed source (guard fact and grep clean).

## Self-Check: PASSED

Files present: `calendar-month.js`, `Calendar/Index.cshtml`, `calendar.css`, `CalendarMonthMarkupTests.cs`, `EventsConversionGuardTests.cs`; commits 90ad1ad6 and d4d0192d present; port 8300 stopped, 8301 untouched and still listening.
