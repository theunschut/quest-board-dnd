---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 09
subsystem: events-forms-ui
tags: [razor, es-module, antiforgery-meta, accessibility, guard-tests]
requires:
  - phase: 97-08
    provides: meta-token module pattern, area guard lists
provides:
  - "event-series-form.js: recurrence, cycle strip, live preview, save-scope dialog and collision notice for Create and Edit"
  - "Events/Create and Events/Edit with no inline script, foundation form pattern, named and phone-sized cycle controls"
  - "event-forms.css page sheet for readable form messages"
affects: [97-17]
actuals:
  tokens: 30000
  tasks: 3
  commits: 3
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/event-series-form.js
    - QuestBoard.Service/wwwroot/css/event-forms.css
    - QuestBoard.IntegrationTests/Controllers/EventFormMarkupTests.cs
  modified:
    - QuestBoard.Service/Views/Events/Create.cshtml
    - QuestBoard.Service/Views/Events/Edit.cshtml
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
  deleted:
    - QuestBoard.Service/Views/Events/_SeriesFormScripts.cshtml
key-decisions:
  - "Gate 1 item 12 answered gold: nothing is added for the title; the foundation's gold desktop h1 applies and the quest form's parchment opt-in class is absent (pinned by a fact)."
  - "A small page sheet, event-forms.css, was added (not in the plan's file list) because the danger text, required marker and help lines measured under the text bar on the card's glass; it mirrors what the quest form sheet does, scoped to an event-form class on the card."
  - "The two scope buttons carry data-scope-value so the module holds no enum numbers."
requirements-completed: [D-15, D-17, DoC-1, DoC-5, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 09: Events/Create and Events/Edit on the layered foundation Summary

Both event forms run from one ES module with no inline script, the recurrence label names its checkbox, and the cycle
strip controls are named and touch-sized.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: Create through the module, label fix, ported series script, retired partial, guard | done | 48223763 |
| 2 Edit save-scope and collision notice through the module; foundation form pattern on both forms | done | 8e4a46f4 |
| 3 Named, phone-sized cycle controls; readable messages; sweep and behaviour probe | done | e5b5af6b |

Tracer gate: the tracer verify (77 facts and the live preview probe on both User-Agents) passed before expansion. Auto
mode is off in config; the run was an autonomous executor with no resume, so the autonomous form of the gate was used.

## Functions ported

`event-series-form.js` (no globals, no markup from strings): `antiforgeryToken` (reads the meta, `''` when absent),
`parseCycleMask`, `formatCycleMask`, and inside `initSeriesForm`: `renderCycleMaskStrip`, `syncCycleMask`,
`updateDerivedWeekday`, `renderPreviewMessage`, `renderPreviewDates`, `refreshSeriesPreview` (stale-response counter and
400 ms debounce kept), `scheduleSeriesPreview`. The Edit behaviour is `initSaveScope`: submit interception, the
collision lookup (`data-event-id`, `data-collision-url`, posts `id`, `date` and the token; a failed lookup opens the
dialog anyway), `chooseScope` (sets `EditScope` from `data-scope-value`, hides the dialog, submits). Each feature switches
on only when its elements exist, so one file serves both pages. Titles, ids, notice and preview sentences are verbatim.

## Deleted and fixed

- `_SeriesFormScripts.cshtml` deleted (225 lines); `window.seriesAntiforgeryToken` and the Edit inline script (token
  and ids in script text) are gone. Acceptance greps: `<script>|seriesAntiforgeryToken` in Create prints 0; `<script>`
  and `RequestToken` in Edit print 0; `innerHTML` in the module prints 0.
- Label fix: `for="enableRecurrence"` replaces the tag-helper `for`; clicking "Make this a recurring series" ticks the box
  (probe-proven) and the checkbox is now named. Its phone target is 44 (BEFORE 20x20, a small target).
- Cycle controls: cells carry `aria-pressed` and `aria-label` "Position {n}, session on|off", keep their titles, take the
  control border on the paper fill, 32px on desktop and 44px under `.mobile-layout` (`components.css`); focus returns to the
  toggled cell after the strip rebuilds. Add and remove are `btn btn-secondary btn-sm` with `aria-label` equal to the
  titles, 44px on the phone through the shell rule.
- Form pattern: `h1` in the card header (was `h2`), `btn-primary` commit (was `btn-warning`), Cancel `btn-secondary`
  after an `hr`, decorative icons `aria-hidden`, validation spans without a static class (the tag helper adds the error
  class only on error). The dialog text is verbatim and `Only this event` stays `btn-primary`; the safe `EditScope`
  default (0, only this event) is pinned.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 1962 passed (1945 at the start, +17: `EventFormMarkupTests` theories for
  label, module, scope default, one primary and h1, title colour, named cycle controls, and the two guard facts
  `RetiredFiles_AreGone` and `SeriesFormModule_WritesNoMarkupFromStrings`), run with
  `QUESTBOARD_MIGRATOR_TEST_CONNECTION` and `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`.
- `series-form.mjs --case all` on 8300, both User-Agents, all green: `create-preview` (label click ticks, preview fills with
  dates, weekday line), `cycle-toggle` (aria-pressed, name, title, hidden mask, focus), `cycle-add-remove` (strip, length
  label, mask, limit 24, Add disabled at the limit), `edit-scope` (dialog opens with its texts, notice hidden on a free day,
  notice shown on a day with a live sibling, safe default kept, Cancel then reload proves nothing was saved). No console
  error, page error, native dialog or form submit in any case.
- Sweep `ev-create, ev-create-recurring, ev-create-errors, ev-edit, ev-edit-series, ev-edit-scope` into `after/97-09`
  (24 records, all 200): readability-compare with `--zero-contrast` reports 0 failing rows, 0 residual contrast, no
  regression; every record has empty `smallTargets` (phone), empty `focusRingMissing`, no overflow at 320, 390 and 412, no
  text-spacing overflow, no console or page errors. Desktop `titleToken` is `accent` (gold, per the Gate 1 answer) and
  phone `fg`, on every row.
- `allow.json` records two key renames only: the recurrence checkbox (now named) and the first cycle cell (new name).

## Findings closed (AFTER evidence)

- A-events-020: label `for` and `id` agree; probe ticks the box from the label; checkbox 44px on the phone.
- A-events-021: cycle cells named, pressed state exposed, 44px on the phone (`smallTargets` empty); add/remove named.
- A-events-007, 010: commit action is the single `btn-primary`; Cancel secondary; markup tests pin one primary per page.
- A-events-027: the h1 sits in the card header and the page has one h1 (fact).
- A-events-030: no inline script on either form; token read from the meta element (facts and greps).
- A-events-038: the recurrence box uses the foundation's checked fill (screenshot `after/97-09/mobile/ev-create-recurring.png`).

## Deviations from Plan

**1. [Rule 2 - Missing critical functionality] event-forms.css page sheet**
- Found during: Task 3 sweep. The required marker, validation message and summary (3.63:1) and the help lines (4.25:1)
  failed the zero-contrast bar on the card's glass; the quest form fixes the same with `quest-form.css`, which cannot be
  linked here (its page class would turn the title parchment, against the gold answer).
- Fix: `event-forms.css` in the pages layer scoped by an `event-form` class on the card: overlay surface for the marker and
  messages, card colour for help lines, and a phone minimum height on the editable line (the empty description line was
  a 24px target). Linked from both views; added to the guard's sheet and link lists. Commit e5b5af6b.

**2. [Rule 1 - Bug] Recurrence section hidden when the box is already ticked**
- The old script only reacted to `change`, so a form returned with the box ticked (validation round trip, restored page)
  kept its recurrence fields hidden. The module now shows the section and schedules a preview on load when the box is
  ticked. No other behaviour changed.

**3. Test assertion rewritten to the approved markup**
- `BoardPageEventsCalendarTests.CancelHref` matched the Cancel icon markup exactly; it now includes `aria-hidden="true"`
  (the Cancel link and its target are still asserted). No coverage removed.

**4. Edit submit hook**
- The module intercepts the form's `submit` event (plan wording) instead of the old Save button click; the button is
  the default submit control so Enter and click behave the same, and the dialog buttons use `form.submit()`.

**5. Preview muted tone**
- The preview prompt and notices no longer use `text-muted` (they inherit the card colour); the sentences are verbatim.

**6. Tooling and commit trailer**
- `series-form.mjs` created in `~/qb-ui-review/phase-97/tools/`. Trailers name `Claude Sonnet 5.5`.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The preview and collision posts keep the same fields; the token now comes from the meta element and no longer
appears in script text (T-27); preview items and notices use `createElement` and `textContent`, forbidden otherwise by
`SeriesFormModule_WritesNoMarkupFromStrings` (T-28); `EditScope` renders as only-this-event and is changed only by a
dialog choice, pinned by a fact and the `edit-scope` probe (T-29).

## Self-Check: PASSED

Files exist (module, sheet, test class, views, components rule); commits 48223763, 8e4a46f4, e5b5af6b present;
`_SeriesFormScripts.cshtml` absent.
