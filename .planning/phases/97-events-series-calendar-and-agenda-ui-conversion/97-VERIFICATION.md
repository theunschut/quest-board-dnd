---
phase: 97-events-series-calendar-and-agenda-ui-conversion
verified: 2026-11-12T23:08:02Z
status: passed
score: 17/19 must-haves verified
behavior_unverified: 2
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 16/19
  previous_verified: 2026-11-12T22:57:10Z
  fix_commit: c59988e1
  gaps_closed:
    - "Every date in the area is still rendered through the wall-clock and local-time helpers (phase goal; roadmap constraint 'Dates render through Html.LocalTime and Html.WallClock only')"
  gaps_remaining: []
  regressions: []
gaps: []
deferred: []
behavior_unverified_items:
  - truth: "\"+N more\" opens the day panel on click, tap, Enter or Space only; focus moves into the panel, Tab loops, Esc, Close and an outside click close it and focus returns to the opener. The detail card opens on hover, focus and tap, is hoverable, persists, closes on Esc; a first tap opens it and a second tap follows the link (D-01, D-02)"
    test: "On Calendar/Index at 1280x720 with the crowded May 2027 month: open '+N more' with the mouse, then with Enter and Space; Tab and Shift+Tab inside the panel; close it with Esc, then Close, then an outside click. Hover an entry, move onto the card, wait, press Esc. On a touch device tap an entry once, then again, then tap elsewhere."
    expected: "The panel takes focus and keeps it; every close path returns focus to the '+N more' that opened it; the card stays while hovered with no timeout and closes on Esc; the second tap navigates; an inert linked entry opens the card on hover or tap but never navigates."
    why_human: "No repository test runs a browser. The static facts in CalendarMonthMarkupTests pin the markup and hooks, not focus or pointer behaviour. The only behavioural evidence is scratch Playwright output (after/97-15/behaviour-check.json 211/0, behaviour-panel.json 40/0, behaviour-inert.json 30/0) from port 8300, outside the repository and not re-run here."
  - truth: "The inline scripts moved into ES modules keep their behaviour: event-details.js (Yes, Maybe, No, Withdraw, delete, cancel and restore confirms), event-series-form.js (recurrence toggle, cycle strip, live preview, collision notice, save-scope dialog), agenda.js (switch-board dialog filled from the trigger) and row-link.js (whole-row click on Events/Index)"
    test: "As a player on Events/Details, answer Yes, then Maybe, then Withdraw on a one-shot board. As a DM, cancel and restore an occurrence. On Events/Create turn on recurrence, toggle cycle cells and watch the preview. On Events/Edit of a series occurrence, save and choose Only this event. On Agenda press Switch & Open. On Events/Index click a row outside its title link."
    expected: "Every request posts as before (antiforgery token from the meta element); the confirm and alert texts are unchanged; the preview and collision notice update; the dialog posts to GroupPicker/SelectGroup with the right group id and return URL; the row opens its event."
    why_human: "The repository tests pin data attributes, the meta token and the absence of inline code. None of them executes the modules. The executor's round trip (97-08) and sweeps are scratch evidence only."
human_verification:
  - test: "Real-phone check of the Calendar (phone agenda), Agenda, Events/Index, Events/Details and Series pages on a real touch device after deploy (the DoC-7 manual item from 97-17)"
    expected: "The phone layout is served; slips are one stretched link with no double activation; Show players and Show Roster open; the Today pill appears on the board's today; nothing scrolls sideways at the device width; every control is at least 44px, apart from the recorded runway-alert inline link"
    why_human: "The sweep used an emulated Pixel 7 profile; real touch and a real User-Agent are needed"
  - test: "Screen-reader reading pass on Calendar/Index (desktop): an own entry, a '+N more' button, the opened day panel and an inert linked entry"
    expected: "Each entry's name carries status, time, DM, board, the key warning and the viewer's vote; '+N more' is read as '+N more entries on {date}'; the card is never read (aria-hidden); nothing is read twice; the inert entry's hidden text is read without a link role"
    why_human: "Screen-reader output cannot be observed programmatically; the 97-17 backstop for this item has no recorded reading pass in the repository"
  - test: "After deploy, open Events/Details, Series/Details, Events/Index and the Agenda on production and compare the date lines with the pre-deploy text"
    expected: "Every moved date line reads exactly as before deploy"
    why_human: "The moved lines format with the host's current culture by design, and the production host's culture was never inspected (UI-SPEC planner assumption 4). Byte-identity was proven under the dev host culture and nl-NL, not on production"
---

# Phase 97: Events, Series, Calendar and Agenda UI conversion Verification Report

**Phase Goal:** Convert the calendar, agenda, events overview, event details and series pages and their shared partials to the foundation, with a desktop month that fits without scrolling and every date still rendered through the wall-clock and local-time helpers.
**Verified:** 2026-11-12 (initial, HEAD 39792fdc); re-verified 2026-11-13 at HEAD c59988e1
**Status:** human_needed
**Re-verification:** Yes, after gap closure (fix commit c59988e1)
**HEAD:** c59988e1 (branch feature/phase-97)

## Goal Achievement

The goal is achieved in the code. The initial verification at 39792fdc found one gap: the compact month's detail blocks printed the date with `day.Date.ToString("D")` instead of the wall-clock helper, and the area guard could not see it. Commit c59988e1 closes it:

- the detail-block date renders through `Html.WallClock(day.Date, "weekday-long-year")`;
- the quest detail's Time row renders through `Html.WallClock(questOnDay.ProposedDate.Date, "time-24h")`;
- the guard's regex now also matches one-letter standard formats;
- a new rendered fact pins both rows.

The 16 area views carry no inline code. The seven area sheets are each one `@layer pages` block with no literals and no `!important`. The three global sheets left `_Layout.cshtml`. The desktop month fits at the target sizes. Every date in the area renders through the helpers. Both test projects are green. What remains is two behaviour-dependent truths that no repository test exercises, plus the post-deploy human checks, hence `human_needed`.

### Re-verification (c59988e1)

| Check | Result |
|-------|--------|
| `git diff 39792fdc..HEAD` | 3 files only: `_Calendar.cshtml` (+3 / -6), `EventsConversionGuardTests.cs` (regex widened, comment reworded), `CalendarMonthMarkupTests.cs` (+1 fact). No other source changed, so the previously passed truths cannot have regressed outside these files. |
| Raw date formatting in area views | `grep '.ToString("'` over the 16 area views returns only `Events/Edit.cshtml:29` (`yyyy-MM-dd`, an allowed machine value for a hidden input). The `dayIso` and `dayText` locals are gone. |
| Detail blocks | `_Calendar.cshtml:313` (event Date) and `:368` (quest Date) use `Html.WallClock(day.Date, "weekday-long-year")`; `:370` (quest Time) uses `Html.WallClock(..., "time-24h")`. The output is a `<time class="wall-clock" data-style=...>` element, as UI-SPEC section 1.6 requires. |
| Guard | `DateFormatCall` now also matches the one-letter formats `[dDfFgGmMoOrRsStTuUyY]` as the whole format string. Allowed machine values are unchanged. `AreaViews_RenderNoRawDateFormatting` passes, so no area view uses one. |
| Rendered fact | `CalendarMonthMarkupTests.DetailBlocks_RenderDateAndTimeThroughTheWallClockHelper` asserts `data-style="weekday-long-year"` with `datetime="2031-03-10T00:00"` on a quest detail and `2031-03-16T00:00` on an event detail, and `data-style="time-24h"` with `19:00` on the quest Time row. It passes. |
| Module compatibility | `calendar-month.js:205-210` (`factsOf`) reads each `dd` by `textContent` and never reads the `datetime` attribute, so the panel rows read the same as before. `site.js` hydrates only the client styles, and `weekday-long-year` and `time-24h` are server-only styles, so their text is not rewritten in the browser. |
| Suite | `dotnet build` 0 errors; `dotnet test` (migrator env set): unit **1412 passed**, integration **2027 passed** (+1, the new fact), 0 failed. |
| Event Time row (`_Calendar.cshtml:315`, `@eventOnDay.TimeLabel`) | Left as is, by judgement. `EventOnDay.TimeLabel` renders a nullable `TimeOnly` start time ("HH:mm" or "All day"). It is not a date and has no `DateTime` for the helper to take. It pre-dates the phase (the 14fcdefb month used it), the compact event row already uses it, and UI-SPEC section 1.5 line 261 requires it: "an event prints its existing `TimeLabel` (including its all-day wording)". Not a gap. |

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DoC-1: zero inline `style=`, `<style>`, inline `<script>` and inline handlers in the area's views | VERIFIED | A grep over the 16 present area views (`_SeriesFormScripts.cshtml` retired) returns only eight `<script type="module" src=...>` includes (calendar-month, agenda ×2, row-link, event-series-form ×2, event-details ×2). Guard `ConvertedViews_CarryNoInlineStyleScriptOrHandler` and `PhaseScope_IsFullyConverted` pass. |
| 2 | DoC-2: zero hex, `rgb()` and `!important` in the area's sheets; each one `@layer pages` block | VERIFIED | `calendar.css` (1601 lines), `calendar.mobile.css`, `events-overview(.mobile).css`, `agenda(.mobile).css`, `event-forms.css`: hex 0, rgb 0, important 0, exactly one `@layer pages` each. `counts-after.json` totals all 0 (before: 65 hex, 71 rgb, 70 important). |
| 3 | DoC-3: area sheets load through `@section Styles`, not the global list | VERIFIED | `_Layout.cshtml` and `_Layout.Mobile.cshtml` link none of the seven sheets. Each view links its own sheet; `Quest/Details.cshtml:32` keeps its own `calendar.css` link. A scan of non-area desktop views found no class defined only in `calendar.css`, `events-overview.css` or `agenda.css` (only `fas` and `visually-hidden`, which are used inside scoped rules). `Layouts_LinkNoAreaSheet` passes. |
| 4 | DoC-4: twins aligned, content differences answered; no merges (Agenda kept by Gate 1 item 9) | VERIFIED | `Agenda/Index{,.Mobile}`, `Calendar/Index{,.Mobile}`, `Events/Index{,.Mobile}` all present. The phone Calendar has the `Quest Calendar` h1, the "What do these mean?" legend, the Today pill (`Index.Mobile.cshtml:130`), vote and no-key flags. `AgendaConversionTests` and `CalendarPhoneAgendaTests` pass. |
| 5 | DoC-5: accessibility bar A-1 to A-11 on each page, desktop and phone | VERIFIED | `after/final/a11y.md` (91 page-and-layout rows): 0 contrast failures (BEFORE 1876 on 46 rows), 0 missing rings, 0 overflow. The three exceptions were accepted at Gate 2 (`contrast-exceptions.json`). t1 to t5 (desktop controls under 24px that pre-date the phase) are deferred by operator decision. Scratch-harness evidence, not re-rendered here. |
| 6 | DoC-6: BEFORE and AFTER outside the repository; relaid-out pages signed off | VERIFIED | `~/qb-ui-review/phase-97/{before,after/final,gate1,gate2}`. Gate 1 approved (v2 calendar, phone agenda, Events/Details order). Gate 2 approved with no rejected pair (`gate2/answers.json`). Both packages are local only, by orchestrator override. |
| 7 | DoC-7: mobile checks run under a mobile User-Agent | VERIFIED | `after/final/report.json`: 237 records, every manifest row under both User-Agents, all 200, 0 console errors. `MobileViewsTests` phone-selection facts pass unchanged. A real-device check is in human verification. |
| 8 | DoC-8: guards rewritten to the same intent, none deleted | VERIFIED | `tests-renamed.md` maps all 24 vanished integration names (20 from this phase) and the 2 unit listing artefacts to replacements. No project count dropped. `UiFoundationLayerGuardTests` was edited append-only. One guard was loosened (`CalendarSheet_KeepsMonthAndQuestDetailsRulesApart`, for the body-mounted overlays); it was disclosed and accepted at Gate 2 as r1. |
| 9 | DoC-9: helpers untouched beyond the D-09 additions; nothing writes to Events or Quests; feed files untouched | VERIFIED | `git diff 14fcdefb..HEAD` shows nothing under Domain, Repository or Migrator, and no change to `CalendarFeedWriter`, `CalendarSubscriptionService`, `FeedRevisionStamper`, `site.js` or the four helper test files. `HtmlHelperExtensions.cs` gains only `ServerOnlyWallClockFormats` and its lookup; the four existing styles stay invariant. `CalendarController` gains two `ViewBag` reads. |
| 10 | DoC-10: every page still works; suite green | VERIFIED | Re-run here: unit **1412 passed**, integration **2026 passed**, 0 failed. `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` was set, so the 11 migrator tests ran against SQL Server. AFTER sweep: 0 page errors and 0 failed requests. |
| 11 | The desktop month fits without scrolling at 1280x720 and up (G1-R2, D-03, D-04, D-05) | VERIFIED | `calendar.css:323-351` sets `--cal-slots` 2/3/4/5 at min-width 992 and heights 712/850/980, and 0 below that. `calendar-month.js:72` reads it and hides beyond `slots - 1`, so "+1 more" cannot occur. `after/97-15/fit-check.json`: 35 rows, 0 failing at the seven fit sizes. The 992x720 overshoot of 24px is a recorded known issue. Scratch evidence. |
| 12 | Every date still rendered through the wall-clock and local-time helpers (goal clause; roadmap constraint) | VERIFIED (gap closed in c59988e1) | The 18 moved lines are converted and byte-identical (date-lines probe 18/18; `EventsDateRenderTests` passes under the host culture and nl-NL). The detail blocks the initial verification flagged now use the helper: `_Calendar.cshtml:313` and `:368` for the date (`weekday-long-year`), `:370` for the quest time (`time-24h`). The only remaining `.ToString("` in the area views is the machine value at `Events/Edit.cshtml:29`. The widened guard and `DetailBlocks_RenderDateAndTimeThroughTheWallClockHelper` pass. The event Time row's `TimeLabel` renders a `TimeOnly`, not a date; it pre-dates the phase and the UI-SPEC requires it. |
| 13 | Linked entries: dashed border, inert `--static` rule and visually hidden status text kept (D-06, D-07) | VERIFIED | `_LinkedDayEntries.cshtml`: a member gets one `<a class="cal-entry__body">`; others get a `div` with no link, no tab stop and no data attribute, and keep `<span class="visually-hidden">` with board, DM, time and status. The dashed rules are at `calendar.css:177/551/782` and `.details-page .linked-entry--static` at :1336. `LinkedBoardStaticGuardTests` (banned list intact), `LinkedBoardCalendarStyleTests` and `LinkedBoardCalendarTenantIsolationTests` pass. |
| 14 | Today follows the board clock with its own behaviour test; partials are under the ambient-clock guard (D-10) | VERIFIED | `CalendarController` sets `ViewBag.BoardToday = boardClock.Today`. `_Calendar.cshtml:9,53` and `Index.Mobile.cshtml:12,123` compare against it. No `DateTime.Now/Today/UtcNow` in the area views. `AmbientClockSeamTests` gains four guarded paths and two routed-today tokens, and loses nothing. `CalendarBoardClockTests` (the 23:30Z Europe/Amsterdam case) passes. |
| 15 | Quest Details weekday header reaches 4.5:1; the five Phase 95 exceptions retired; Quest Details re-verified (D-08) | VERIFIED | `contrast-exceptions.json` `retired` holds 5 entries (3.76 to 5.63:1). The Quest Details group shows 88 contrast failures before and 0 after. The `isDetailsPage` branch keeps the vote layout, and the Phase 95 Quest Details suites pass. |
| 16 | "+N more" day panel and detail card behaviour (D-01, D-02) | PRESENT_BEHAVIOR_UNVERIFIED | Present and wired. `+N more` is a real `button` with `aria-haspopup="dialog"` and `aria-controls`. The panel is `role="dialog" aria-modal="false"` and the card is `aria-hidden`; both are moved to `document.body` (`calendar-month.js:96`) and filled with `cloneNode` (no `innerHTML`). The panel has no hover path and no timers. No repository test drives a browser; the scratch behaviour checks (281 checks, 0 failed) were not re-run. |
| 17 | Inline scripts retired into modules with unchanged behaviour (event-details, event-series-form, agenda, row-link) | PRESENT_BEHAVIOR_UNVERIFIED | All four modules exist, are loaded as `type="module"` and read the `request-verification-token` meta. `confirm()` and `alert()` keep their wording. Repository facts (`EventDetailsAvailabilityRenderTests`, `EventFormMarkupTests`, `SeriesDetailsMarkupTests`, `AgendaConversionTests`) pin hooks only; none executes the scripts. |
| 18 | Approved relayouts built as drawn: Events/Details order, Q-116 narrow header, header buttons removed (Gate 1 item 13), Calendar h1 parchment | VERIFIED | `Events/Details.cshtml` right column: cancelled alert (:129), Your Availability (:134), Description (:204), Who's Coming (:220); asides use `.modern-card--aside`. The Calendar header has no Board Availability or My Agenda button; both are still in `_Layout(.Mobile).cshtml` (Events and Agenda links). `CalendarTitle_StaysParchment` passes. |
| 19 | The three operator-accepted exceptions are recorded in the phase verification (D-03) | VERIFIED | See "Recorded exceptions" below. All three carry `approved.status: approved`, 2026-11-12. |

**Score:** 17/19 truths verified (2 present but behaviour unverified; 0 failed). Initial verification: 16/19 with truth 12 failed.

### Recorded exceptions (accepted at Gate 2, as Phase 95 recorded the poster exception)

| # | Exception | Measured (after/final/exception-probe.json) | Answer |
|---|-----------|---------------------------------------------|--------|
| 1 | Compact month entries and "+N more" below the WCAG 2.5.8 24px target, desktop month at 992px and wider | rows 20px, links 18px, "+N more" 20px; every entry is also reachable through 50.6px panel rows, the card and its own link | accept |
| 2 | The dashed outline on linked entries is below the 3:1 non-text bar | 1.66:1 against the cell, 7.72:1 against the entry fill; the link glyph and the hidden words carry the meaning | accept |
| 3 | Focus ring offset 0 on compact entries and "+N more", 1px on panel rows (foundation 2px) | 2px two-tone ring, width and colours unchanged | accept |

The accepted `knownIssues` (six-week month at 992x720, phone runway-alert link 22px, `ev-index` fixture drift, resize closes card, panel heading has no year, own cancelled event name order, ui-guidelines line 63, desktop targets under 24px, marker-tile link) and t1 to t5 are operator decisions. They are not gaps.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `wwwroot/js/calendar-month.js` | Slots, +N more, card, panel | VERIFIED | 512 lines, loaded only by `Calendar/Index.cshtml:18` |
| `wwwroot/css/calendar.css` | Month under `.calendar-page`, Quest Details under `.details-page`, one layer | VERIFIED | `--cal-slots`, parchment h1 rule, compact header padding |
| `wwwroot/css/calendar.mobile.css` | Phone agenda, pages layer | VERIFIED | 348 lines |
| `wwwroot/js/event-details.js`, `event-series-form.js`, `agenda.js`, `row-link.js` | Modules replacing inline code | VERIFIED (exists, wired) | behaviour see truth 17 |
| `Views/Shared/_Calendar.cshtml` | Compact month with detail blocks; details branch keeps the vote layout | VERIFIED | Detail-block date and quest time go through `Html.WallClock` since c59988e1 |
| `Views/Shared/_LinkedDayEntries.cshtml` | Compact linked rows, inert rule kept | VERIFIED | |
| `Extensions/HtmlHelperExtensions.cs` | `ServerOnlyWallClockFormats` with four styles | VERIFIED | Existing table and invariant behaviour untouched |
| `IntegrationTests/Tests/EventsConversionGuardTests.cs` | Area guard: scope, sheets, links, dates, planning refs | VERIFIED | 25 facts, all pass; the date regex also catches one-letter standard formats since c59988e1 |
| `CalendarMonthMarkupTests.DetailBlocks_RenderDateAndTimeThroughTheWallClockHelper` | Rendered fact for the detail-block date and time | VERIFIED | Added in c59988e1, passes |
| `CalendarBoardClockTests`, `EventDetailsLayoutTests`, `EventFormMarkupTests`, `SeriesDetailsMarkupTests`, `CalendarMonthMarkupTests`, `CalendarPhoneAgendaTests`, `AgendaConversionTests`, `EventsDateRenderTests`, `WallClockAreaStylesTests` | New facts | VERIFIED | All present and passing |
| `Views/Events/_SeriesFormScripts.cshtml` | Retired | VERIFIED (gone) | No references remain |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `CalendarController` | `_Calendar.cshtml`, `Calendar/Index.Mobile.cshtml` | `ViewBag.BoardToday` cast to `DateOnly` | WIRED |
| `calendar-month.js` | `calendar.css` | `getComputedStyle(root).getPropertyValue('--cal-slots')` | WIRED |
| `calendar-month.js` | `_Calendar.cshtml` detail blocks | `cloneNode(true)` of `.cal-entry-detail`; panel title from `.calendar-day__top time` (helper output) | WIRED |
| `Calendar/Index.cshtml` | `_Calendar` | partial inside `.calendar-page`, with card and panel shells | WIRED |
| `Events/Index.cshtml` | `row-link.js` / `events-overview.css` | `@section Scripts` / `@section Styles` | WIRED |
| `Events/Details.cshtml`, `Series/Details.cshtml` | `event-details.js` | module, `data-confirm-message`, meta token | WIRED |
| `Events/Create.cshtml`, `Edit.cshtml` | `event-series-form.js` | module after `_QuestFormScripts` | WIRED |
| `Agenda/Index{,.Mobile}` | `agenda.js` | `show.bs.modal` reads `data-group-id` | WIRED |
| `_Calendar.cshtml` | `_LinkedDayEntries` | linked entries after own entries | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Calendar month / phone agenda | `Model` days, `QuestsOnDay`, `EventsOnDay`, `Linked` | `CalendarController` via calendar and linked-calendar services | Yes (fixture month May 2027 captured live) | FLOWING |
| Today marker | `ViewBag.BoardToday` | `IBoardClock.Today` | Yes (`CalendarBoardClockTests`) | FLOWING |
| Viewer vote flag | `ViewBag.CurrentUserId` and `PlayerVotes` | `userService.GetUserAsync` | Yes ("Your vote: Yes" in the live card) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build | `dotnet build` | 0 errors | PASS |
| Unit suite | `dotnet test` (migrator env set), at c59988e1 | 1412 passed, 0 failed | PASS |
| Integration suite | same run | 2027 passed, 0 failed (2026 at 39792fdc) | PASS |
| No inline code in area views | grep for style, `<style>`, `<script>`, `on*=` | only module includes | PASS |
| No literals in area sheets | grep hex, rgb, `!important` | 0 in all seven | PASS |
| No raw date formatting in area views | grep `.ToString("` | only `Events/Edit.cshtml:29` (`yyyy-MM-dd`, allowed machine value); was `_Calendar.cshtml:279` `"D"` at 39792fdc | PASS |
| No planning references in changed source | broad ID and doc-name grep over 85 changed files | only a pre-existing guard regex (`LinkedBoardStaticGuardTests.cs:275`, from a3f9e89c) | PASS |
| Protected paths untouched | `git diff --stat 14fcdefb..HEAD` on Domain, Repository, Migrator, feed files, site.js, helper tests | empty | PASS |

### Probe Execution

The phase declares no `probe-*.sh`, so this step was skipped. The scratch Playwright tools (sweep, behaviour, fit, a11y) live in `~/qb-ui-review/phase-97/tools/`. They were not re-run because no app may be started.

### Requirements Coverage

The plan frontmatter IDs (D-01 to D-17 and DoC-1 to DoC-10) are phase decision and Definition-of-converted IDs. None of them is in `.planning/REQUIREMENTS.md`, and the roadmap lists this phase's requirements as "TBD", so no REQUIREMENTS.md entry maps to Phase 97 and none is orphaned. Summaries 97-01 to 97-06 carry no `requirements-completed` field; their IDs are closed by later summaries or by the evidence below.

| ID | Status | Evidence |
|----|--------|----------|
| D-01, D-02 | SATISFIED in code; behaviour is truth 16 | `calendar-month.js` |
| D-03 | SATISFIED | 20px rows and slot table; exception 1 accepted |
| D-04, D-05 | SATISFIED | fit-check 35/35; 0 slots below 992px |
| D-06, D-07 | SATISFIED | truth 13 |
| D-08 | SATISFIED | truth 15 |
| D-09 | SATISFIED | the 18 moved lines and, since c59988e1, the new detail-block date and quest time (truth 12) |
| D-10 | SATISFIED | truth 14 |
| D-11, D-12 | SATISFIED | Gate 1 and Gate 2 approved; no redraw needed at Gate 2 |
| D-13 | SATISFIED | merge not proposed; twins kept |
| D-14 | SATISFIED | Gate 1 item 1 "time"; compact row is marker, time, title |
| D-15, D-16 | SATISFIED | scratch root, `fixtures.json` (app forms only) |
| D-17 | SATISFIED | slices on the branch, suite green; no tag created |
| DoC-1 to DoC-10 | SATISFIED | truths 1 to 10 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` | 279, 316, 371 (at 39792fdc) | Raw `ToString("D")` date in a hand-built `<time>` | Resolved | Fixed in c59988e1 |
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` | 373 (at 39792fdc) | Visible quest time from `TimeLabel` | Resolved | Fixed in c59988e1 (now :370) |
| `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs` | 165-169 | Date-format regex missed one-letter standard formats | Resolved | Widened in c59988e1 |
| `.claude/ui-guidelines.md` | 63 | States `calendar.css` is not layered | Info | Already a recorded known issue |

No `TBD`, `FIXME`, `XXX`, `TODO` or `HACK` marker was added by the phase.

### Human Verification Required

These are listed in the frontmatter and are deferred until after deploy:

1. Real-phone pass over the Calendar, Agenda, Events and Series pages (DoC-7 manual item).
2. Screen-reader reading pass of an entry, "+N more", the panel and an inert linked entry.
3. Production check that the moved date lines read as before (the production host culture was never inspected).

The two behaviour-unverified truths (16 and 17) are listed under `behavior_unverified_items`. A desktop browser is enough for both; they need no real device.

### Notes that are not gaps

- **Evidence provenance.** Contrast, fit, overflow, target-size and behaviour results come from the scratch harness in `~/qb-ui-review/phase-97/`. I checked that the records exist, agree with each other and match the code, but I did not re-render anything (no app may be started). `a11y.md` lives at `after/final/a11y.md`, not at the scratch root.
- **Gate 2 answers were delegated.** The operator approved the gate and every pair. The e1 to e3, r1 to r4, t1 to t5 and fixtures answers are the orchestrator's recommendations, taken under that delegation, and `answers.json` marks each one's source. Accepted as instructed.
- **Screenshots carry the review account's name and placeholder address** in the Quest Details frames (97-17). The package is local only; mask those frames before it is ever shared.

### Gaps Summary

No gaps remain. The one gap from the initial verification is closed at its root:

- the detail blocks render the date and the quest time through `Html.WallClock`;
- the guard that missed it now catches one-letter standard formats;
- a rendered fact pins the markup.

The fix touched three files only, and the full suite is green at c59988e1 (1412 + 2027). The status is `human_needed` for two reasons:

- two behaviour-dependent truths (the calendar panel and card, and the modules that replaced the inline scripts) are present and wired, but no repository test exercises them;
- the real-phone, screen-reader and production checks are deferred until after deploy.

---

_Verified: 2026-11-12T22:57:10Z (initial); re-verified 2026-11-12T23:08:02Z at c59988e1_
_Verifier: Claude (gsd-verifier)_
