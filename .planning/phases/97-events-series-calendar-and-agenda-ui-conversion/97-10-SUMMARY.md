---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 10
subsystem: series-details-ui
tags: [razor, accessibility, modal, guard-tests]
requires:
  - phase: 97-08
    provides: event-details.js confirm-form listener, area guard lists
  - phase: 97-09
    provides: card-role pattern, readable-surface conventions
provides:
  - "Series/Details with no inline style or handler, named links and header cell, a described read-only strip and a named foundation dialog"
affects: [97-17]
actuals:
  tokens: 22000
  tasks: 2
  commits: 2
key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/SeriesDetailsMarkupTests.cs
  modified:
    - QuestBoard.Service/Views/Series/Details.cshtml
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs
key-decisions:
  - "The strip container is aria-hidden and a visually hidden sentence follows it, rather than leaving aria-disabled on non-interactive spans."
  - "The mid-page 'Today' divider keeps its text-muted class (it sits on paper and measures); the two inline empties on glass drop text-muted and inherit the card colour, as Events/Details does."
  - "The sub-heading inside the Recurrence Rule card (the series title) is an h3 with the h5 size class, so the outline has no skipped level and the look is unchanged."
requirements-completed: [D-15, D-17, DoC-1, DoC-5, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 10: Series/Details Summary

Series/Details meets the accessibility bar on both layouts with no copy change: no inline code, named repeated links, a
spoken cycle summary and a named foundation remove dialog.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: End Series confirms through `data-confirm-message` and the shared listener | done | 4c156ec7 |
| 2 Named links and header cell, described strip, foundation dialog, headings, sweep | done | 489cefdc |

Tracer gate: the tracer verify passed (tests, then a live probe on both User-Agents) before expansion. The run was an
autonomous executor with no resume, so the autonomous form of the gate was used.

## Changes

- `Series/Details.cshtml`: `onsubmit="return confirm(...)"` replaced by `data-confirm-message` on the End form and the
  `event-details.js` module loaded from `@section Scripts`; the title card is a plain `.modern-card` with the page `h1`
  (`fa-repeat`, inherited colour, `aria-hidden`); Recurrence Rule, Occurrences and Actions are `h2` in
  `.modern-card--aside` cards; the read-only strip cells are `cycle-mask-cell--readonly` with no `style`, the strip is
  `aria-hidden` and followed by the hidden sentence `Sessions on cycle positions {list}.`; the empty header cell holds
  hidden `Actions`; each View link gains the hidden suffix ` occurrence on {date}` (via `Html.WallClock`); the dead
  `today` class is dropped (the `Today` text stays); the dialog drops `bg-dark text-light border-secondary
  btn-close-white`, is bound by `aria-labelledby="removeSeriesTitle"` to `h2.modal-title`, and its close button is
  `aria-label="Close"`. Detach, Delete everything and Cancel keep text and posts.
- `components.css`: `.cycle-mask-cell--readonly` (no pointer events) and `.mobile-layout .table-cell-link` (a link alone
  in a table cell becomes a 44px target on the phone).
- Tests: new `SeriesDetailsMarkupTests` (six facts, each over desktop and phone: `Cards_FollowTheCardRoles`,
  `EndSeries_ConfirmsThroughADataAttribute`, `ReadOnlyStrip_HasNoInlineStyleAndATextSummary`,
  `OccurrenceLinks_AndHeaderCell_AreNamed`, `RemoveDialog_IsNamedAndOnTheFoundationSurface`, `InlineEmpties_StayVerbatim`);
  `Series/Details.cshtml` joins `ConvertedViews` in the guard. `InlineEmpties_StayVerbatim` seeds its own series with no
  occurrences, so it does not depend on the optional `series.empty` fixture.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 1974 passed (1962 at the start, +12), with
  `QUESTBOARD_MIGRATOR_TEST_CONNECTION` and `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`.
- Acceptance greps on the view: `style=|onsubmit|bg-dark` 0 (was 1, 1, 1); `modern-card--aside` 3 (was 0); `<h1` 1;
  `<h4` 0; `onsubmit` 0.
- Sweep `series-details, series-remove-modal, series-details-empty` into `after/97-10` (12 captures, all 200):
  readability-compare with `--zero-contrast` exits 0: 0 failing rows, 0 residual contrast, no regression. Every record has
  no horizontal overflow at 320 desktop, 390 and 412 phone, empty `focusRingMissing`, no text-spacing overflow, no console
  or page error, desktop `titleToken` `accent` and phone `fg`. The phone View links went from 20 small targets (35x22)
  to none. The empty page's single BEFORE contrast failure is gone.
- Behaviour probe `series-details.mjs` (new, in the tools folder) on 8300, both User-Agents, all ok: `end-cancel` clicks
  End Series, reads the native confirm, dismisses it, and records no POST and the page unchanged (the dialog is never
  accepted); `modal-esc` opens the remove dialog, checks its name resolves to `Remove this series` and the close button
  is `Close`, presses Esc, and records the dialog closed, focus back on Delete Series, and no POST. The confirm text is
  byte-identical to the baseline (8301) text: `End this series after {date}? 20 upcoming session(s) will no longer be
  generated. Sessions that already happened are always kept. This cannot be undone.`
- `allow.json` records renames only: the 20 View links per row (now named), and the dialog and its close button (now
  named). The sweep tool resolves a dialog's name to the raw `aria-labelledby` id, hence `removeSeriesTitle` in the
  allow key; the real name is asserted by the probe and a markup fact.

## Findings closed

- A-events-010: End Series `btn-warning`, Delete Series `btn-danger`, pinned by facts.
- A-events-011: View links carry the hidden occurrence date; the phone targets are 44px.
- A-events-022: the empty header cell is named `Actions`; the dead `today` class is gone.
- A-events-030: no inline handler or script on the page.
- A-events-031: the confirm moved to `data-confirm-message`, wording unchanged (probe-proven equal to BEFORE).
- A-events-036: the strip is described, the dialog is named and closes on Esc with focus returned, the close button is named.
- A-events-049: cancelled rows keep the strike-through and the Cancelled badge (fact).

## Deviations from Plan

**1. [Rule 3 - Blocking] Existing date fact rewritten to the approved markup**
- `EventsDateRenderTests.SeriesDetails_Dates_KeepTheirText` expected three `<time>` texts; the approved hidden link name
  adds a fourth (the occurrence date again). The assertion now lists four, with a comment. The anchor, end-date and row
  texts and the culture checks are unchanged; no coverage removed.

**2. [Rule 2 - Missing critical functionality] Phone touch target for the View links**
- They measured 35x22 on the phone (a pre-existing small target the plan's "44px phone targets" bar covers). Added
  `.mobile-layout .table-cell-link` in `components.css` (inside the plan's file list) and the class on the link.

**3. [Rule 1 - Bug] Heading outline for the series sub-title**
- The series title inside the Recurrence Rule card was an `h5` under an `h2`; it is now `h3.h5` (same size).

**4. Decorative icons hidden**
- The page's icons carry `aria-hidden="true"` (the plan named this for the title icon only); no visual change.

**5. Tooling and commit trailer**
- `series-details.mjs` created in `~/qb-ui-review/phase-97/tools/`. Trailers name `Claude Sonnet 5.5`.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The End, Delete and Detach forms keep their tag-helper antiforgery tokens and actions (T-30); the Actions card
condition and the controller authorization are unchanged and no control was added (T-31); the confirm text is
Razor attribute-encoded and the module only passes it to `confirm()` (T-32; the fact decodes and checks it).

## Self-Check: PASSED

- Files: `SeriesDetailsMarkupTests.cs`, `Series/Details.cshtml`, `components.css`, the guard and date tests exist.
- Commits: 4c156ec7 and 489cefdc exist on feature/phase-97.
