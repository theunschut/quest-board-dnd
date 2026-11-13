---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 08
subsystem: events-details-ui
tags: [razor, es-module, antiforgery-meta, card-roles, guard-tests]
requires:
  - phase: 97-05
    provides: Gate 1 answers and the approved Events/Details drawing
  - phase: 97-07
    provides: area guard lists, sweep tooling, shared availability fills
provides:
  - "Events/Details reordered to the approved drawing, with foundation headings, card roles and button roles"
  - "event-details.js: answer, withdraw and confirm-form behaviour from data attributes, token from the meta element"
  - "EventDetailsLayoutTests pinning the order, and the area guard entries for the view and the module"
affects: [97-09, 97-17]
actuals:
  tokens: 21000
  tasks: 3
  commits: 3
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/event-details.js
    - QuestBoard.IntegrationTests/Controllers/EventDetailsLayoutTests.cs
  modified:
    - QuestBoard.Service/Views/Events/Details.cshtml
    - QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
key-decisions:
  - "Phone Dungeon Master order is the Gate 1 answer dom: the view keeps one DOM order (title card, Actions, Your Availability) with no IsMobile branch and no CSS order reordering."
  - "No page sheet: foundation components and Bootstrap utilities were enough, and the sweep found no defect only a page rule could fix."
  - "Inline empties and the current-answer line drop the text-muted utility and inherit the card colour (parchment on glass), after the muted tone measured under the text bar on glass in the previous slice."
  - "shell.mobile.css is unchanged: the phone gutter rule already reaches this page."
requirements-completed: [D-11, D-15, D-17, DoC-1, DoC-5, DoC-6, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 08: Events/Details on the layered foundation Summary

Events/Details now opens with the availability answer right after the title block, works through a small ES module
from data attributes with the antiforgery token read from a meta element, and carries no inline script or handler.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: data-attribute buttons, `event-details.js`, meta token, rewritten render test, guard entry | done | d8b0b692 |
| 2 Approved order, headings, button roles, confirm forms through data attributes, layout test | done | 4d10bd62 |
| 3 Sweep, round trip, comparison; answer row sized for the phone | done | a3e9fe9f |

Tracer gate: the tracer verify (67 rows) passed before expansion; auto mode is off in config, but the run was spawned as
an autonomous executor with no resume, so the autonomous form of the gate was used (re-verify, halt on failure), as in the
previous slice. The page was then exercised end to end in Chromium on 8300 in Task 3.

## Order applied

Gate 1 answers: `eventDetails` approved v1, `phoneDmOrder: dom`, `alertAboveAvailability: true`.
- Left column: title card (its `.modern-card-header` holds the `h1`; date via `Html.WallClock(..., "weekday-long-year")`
  with `fa-calendar-day`, time with `fa-clock`), then for managers the Actions card.
- Right column: the cancelled alert (when cancelled), `Your Availability`, `Description`, `Who's Coming`.
- Phone Dungeon Master order: title card, Actions, Your Availability (`dom`: the markup order is the visual order, so the
  `availability-first` branch on `Context.Items["IsMobile"]` was not needed and was not written). A player sees the
  title card then Your Availability.

## Removed and ported

- Removed from the view: 4 `onclick` handlers (Withdraw and the three answers) and the 3 `onsubmit` handlers, both inline
  `<script>` blocks, and the token written into script text. Acceptance grep
  `onclick|onsubmit|<script>` prints 0 (baseline: 7 handlers, 2 inline scripts).
- `event-details.js` functions: `antiforgeryToken` (reads the meta, returns `''` when absent), `submitAnswer` (shared
  FormData post, reload on success, the same alert sentences), `setAvailability(button)` (POST to `data-answer-url`,
  field `availability` from `data-answer`), `withdrawAvailability(button)` (`confirm(data-confirm-message)`, then DELETE
  to `data-withdraw-url`), and one submit listener per `form[data-confirm-message]` that calls `preventDefault()` when
  `confirm()` is declined. The four alert sentences and the withdraw, delete, cancel and restore wording are verbatim;
  no markup is built from strings.
- Data attributes: `data-answer`, `data-answer-url`, `data-withdraw-url`, `data-confirm-message` (4 occurrences).
- Roles: Edit `btn-warning`, View Series Details `btn-secondary`, Restore `btn-success`, Cancel `btn-warning`, Delete
  `btn-danger`, Move `btn-info` (disabled variant keeps `aria-disabled`, `aria-describedby`, `move-control-reason`),
  Withdraw `btn-danger`, answers success / warning / danger with icons, decorative icons `aria-hidden`. Headings:
  one `h1`, four `h2`, no `h4`; Actions, Your Availability, Description and Who's Coming carry `modern-card--aside`.
  The answer row is `hr`, then Withdraw left and the three answers right; from 768px up the answers keep their size, on
  the phone they share the row width (`flex-fill flex-md-grow-0`).

## Gutter decision

`shell.mobile.css` is unchanged. The existing rule `.mobile-layout main > .row { --bs-gutter-x: var(--qb-space-md); }`
(lines 40-42, written for AccessDenied) matches the page's bare root `.row`. AFTER measurement: 390 and 412 phone
`scrollWidth` equals the viewport on every Events/Details row, with no text-spacing overflow.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 1945 passed (1937 at the start, +8: 7 in `EventDetailsLayoutTests`, 1
  `EventDetailsModule_WritesNoMarkupFromStrings`). The 16 SQL Server facts only pass with the migrator connection
  variable set; run once without it they failed with "not configured", and with
  `QUESTBOARD_MIGRATOR_TEST_CONNECTION` plus `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` all 16 passed.
- Sweep `ev-details-player, -dm, -cancelled, -oneshot` into `after/97-08` (18 records, all 200): `readability-compare`
  with `--zero-contrast` reports 0 failing rows, 0 residual contrast, no regression; every record has no overflow
  (320 desktop, 390 and 412 phone), no text-spacing overflow, empty `focusRingMissing`, empty `smallTargets` on the phone,
  no console or page errors, no assert failures; h1 token `accent` on desktop and `fg` on the phone.
- Round trip `event-answer.mjs` (p97player on `event.oneshot-answered`): desktop original Yes -> No-via-Maybe -> Yes, ok;
  phone original Yes, ok; no console error, page error or dialog; the player's own answer is restored.
- Findings closed: A-events-019 (398px at 390 BEFORE; AFTER 390 at 390 and 412 at 412; the known desktop 320 overflow of
  `ev-details-oneshot` also reads 320 at 320 now, so it is closed too), A-events-025 / 027 (headings and button roles per
  the foundation, Withdraw and Cancel Occurrence filled, View Series Details filled), A-events-030 (no inline script or
  handler, token off the script text), A-events-031 (confirm and alert wording kept, still native dialogs).

## diff.py overlays against the approved frames (`after/97-08/diff/`)

Differing-pixel ratio at tolerance 24: player 1440 0.036, dm 1440 0.043, cancelled 1440 0.036, oneshot 1440 0.070,
player 390 0.161, dm 390 0.196, oneshot 390 0.177, cancelled 390 0.137, player 320 0.155. Desktop frames match the drawing
layout (order, column split, answer row); the remaining desktop differences are the real roster rows and navbar text the
mockup does not carry. On the phone the page is 8 to 50px taller than the drawing (card body padding and answer-row
wrapping with real text, plus the oneshot Withdraw row wrapping above the answers), which shifts every later card and
accounts for the high ratios; order, card roles and button fills match. Nothing was left as a layout difference for Gate 2
beyond that vertical offset.

## Deviations from Plan

**1. [Rule 1 - Bug] Warning badge label**
- The Maybe badges drop `text-dark` (the foundation badge rule gives amber its near-black label); the roster fact in
  `EventDetailsAvailabilityRenderTests` now asserts `badge bg-warning` instead of `badge bg-warning text-dark`. No fact
  was removed.

**2. Muted empties**
- The three inline empties and the current-answer paragraph use no `text-muted`; they inherit the card colour. The copy is
  verbatim. This follows the contrast finding of the Events overview slice; the sweep reports zero contrast failures.

**3. AccessDenied row**
- Phase 96 left no AccessDenied row in the manifest, so it was not swept ad hoc; the shared gutter rule is proven by
  Events/Details reading no overflow on both phone widths.

**4. Tooling and commit trailer**
- `event-answer.mjs` created in `~/qb-ui-review/phase-97/tools/`; `allow.json` is `{}`. The trailer names `Claude Sonnet 5.5`.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The antiforgery token now travels in a meta element and the same `__RequestVerificationToken` FormData field; the
controller actions, routes and policies are untouched, and `EventDetailsLayoutTests.Player_SeesAvailabilityBeforeDescription`
asserts a player sees no Actions card.

## Self-Check: PASSED

Files exist (view, `event-details.js`, `EventDetailsLayoutTests.cs`, this summary); commits d8b0b692, 4d10bd62 and a3e9fe9f are on `feature/phase-97`; both suites green; STATE.md, ROADMAP.md and REQUIREMENTS.md untouched; nothing pushed.
