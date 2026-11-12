---
phase: 95-quests-and-quest-log-ui-conversion
plan: 06
subsystem: ui-quest-details
tags: [vote-buttons, calendar, contrast, aria-group, layered-css, phone-parity]
requires:
  - phase: 95-05
    provides: data-vote-section wrapper, quest-details.js module, QuestDetailsSignupFormTests
provides:
  - filled Yes / Maybe / No vote choices on both layouts (vote-choice, vote-choice--yes|maybe|no), checked state marked by a 2px border as well as the fill
  - each date's three radios in a role=group named "Your vote for" plus the date through aria-labelledby (ids vote-for-N and update-vote-for-N)
  - quest-vote.css and quest-details.css, both @layer pages with no colour literal and no important flag
  - calendar months drawn directly inside the signup, signed-up and update cards (month heading then calendar, no nested card)
affects: [95-07 and later Details layout slices, the calendar phase]
tech-stack:
  added: []
  patterns: [hidden group name inherited from a surface that sets its own text colour, in-place token sizes in the unlayered calendar sheet where a layer cannot win]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/quest-vote.css
    - QuestBoard.Service/wwwroot/css/quest-details.css
  modified:
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/QuestDetailsSignupFormTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "The checked border is dark ink on desktop and parchment on the phone (a custom property switched under .mobile-layout) instead of parchment everywhere: parchment on the white vote box is 1.3:1, ink on it is 18.8:1, and the checked Maybe fill alone is only 2.11:1 against that box"
  - "The Details week row grows from 120px to 160px (calendar.css, Details-scoped selectors only) because three 32px buttons plus the day number do not fit the old fixed row, and the day cell clips"
  - "The month heading carries a scrim band (--qb-surface-scrim) because with the nested card gone it sat on bare glass and measured 3.6:1 against the lightest board pixel; 20px at weight 600 is not large text for the project's two-weight scale"
requirements-completed: [D-18, D-24, DoC-2, DoC-3, DoC-5, DoC-8]
duration: ~50 min
completed: 2026-11-11
status: complete
actuals:
  tokens: 8300
  tasks: 2
  commits: 2
---

# Phase 95 Plan 06: Quest Details vote buttons and months inside the signup card Summary

The Yes / Maybe / No vote buttons are filled, named and grouped on both layouts and hold contrast in every state, and the calendar months now sit directly inside the one signup card as a heading followed by the calendar. What the forms post is unchanged.

## What changed

- `_Calendar.cshtml` and `_Calendar.Mobile.cshtml` (initial and update flows): each label is `btn vote-choice vote-choice--yes|maybe|no` (phone keeps `flex-fill`). Desktop label content is the icon (`aria-hidden`) plus a visually hidden word; phone keeps the visible word. A visually hidden span `Your vote for <Html.WallClock date-compact>` carries id `vote-for-N` (initial) or `update-vote-for-N` (update); the radio group wrapper (desktop `btn-group-vertical`, phone `calendar-vote-row`) is `role="group"` with `aria-labelledby` to it. Inputs, names, ids, values, `autocomplete`, checked logic, the pinned `_LinkedDayEntries` placement and the phone element order are untouched.
- `quest-vote.css` (`@layer pages`): neutral fill, 2px transparent border reserving the checked border, 14px label, 32px minimum on desktop; checked success, warning and danger fills with their paired label colours and a 2px edge; the foundation two-tone focus ring on the checked-sibling label; phone minimum height `--qb-hit-min`. `.vote-buttons` sets dark ink and `.calendar-vote-row` sets the heading parchment so the hidden group name inherits a colour that holds contrast on its surface.
- `calendar.css`, declarations changed in place (the sheet is unlayered, so a layer could not win):
  - `.vote-buttons .btn`: `width` and `height` 20px to 32px, `min-width` 16px to 32px, `font-size` 10px to `var(--qb-text-sm)`, `border` `1px solid` to `2px solid transparent`
  - `.quest-event.current-quest`: `font-size` 9px to `var(--qb-text-sm)`, `padding-right` 30px to 44px
  - `.quest-event.current-quest:has(.vote-buttons)` and `.details-page .quest-event.current-quest:hover:has(.vote-buttons)`: `height` 90px to 116px
  - `.details-page .quest-event.current-quest .calendar-quest-title`, `.calendar-quest-dm`, `.quest-time`, `.finalized-icon`: font sizes 10, 8, 8, 10px to `var(--qb-text-sm)`
  - `.details-page .quest-event.current-quest:hover`: `font-size` 9px to `var(--qb-text-sm)`, `padding-right` 30px to 44px
  - `.details-page .calendar-body` and `.quest-details-page .calendar-body`: `grid-auto-rows` 120px to 160px
  - the three dead `.btn-check:checked + .btn-outline-*` rules were left (the calendar phase owns the file)
- `Details.cshtml`: new `@section Styles` linking `calendar.css`, `quest-details.css`, `quest-vote.css`. The three month loops are now `div.quest-details-month` > `h3.quest-details-month-heading` (decorative `fa-calendar-alt`, no status colour) + the partial; `data-vote-section` still wraps exactly the signup form's months. `Details.Mobile.cshtml`: links `quest-vote.css` after its existing sheet; each month gets an `h3.quest-section-heading` above its calendar in the signup and update forms.
- `quest-details.css` (`@layer pages`): month spacing, heading padding, radius, scrim band, weight 600.
- Tests: `QuestDetailsSignupFormTests` gains two theories (desktop and phone): per date three filled choices and no `btn-outline-`, six radios still named `DateVotes[i].Vote` with values 2, 1, 0, two groups whose `aria-labelledby` resolves to an element starting "Your vote for"; the voting section holds month headings and no `modern-card`. `QuestsConversionGuardTests`: `quest-vote.css` and `quest-details.css` in `ConvertedSheets` and `PhaseSourceFiles`, Details views in `ViewSheetLinks` (desktop: calendar, quest-details, quest-vote; phone: quest-vote).

## Measured vote-button ratios

Computed from the token values (the sweep reports zero `vote-choice` contrast failures on every row, both User-Agents):

| State | Label on fill | Fill vs white vote box (desktop) | Edge (ink 18.82 vs white; parchment on phone glass) |
|-------|---------------|----------------------------------|-----------------------------------------------------|
| Unchecked (neutral) | 6.09 | 6.09 | none |
| Yes checked (success) | 5.35 | 5.35 | ink edge vs fill 3.52; phone parchment vs fill 4.24 |
| Maybe checked (warning) | 8.92 | 2.11 (the 2px ink edge carries the 3:1 boundary) | ink edge vs fill 8.92 |
| No checked (danger) | 5.44 | 5.44 | ink edge vs fill 3.46 |

Label size 14px at weight 600; desktop buttons 32px square; no small-target, focus-ring or overflow regression on any compared row (desktop 1440 and 320, phone 390 and 412).

## Verification

- `dotnet build` 0 errors. `dotnet test` full: unit 1300 passed; integration 1775 passed, 11 skipped (pre-existing), 0 failed.
- Sweep `~/qb-ui-review/phase-95/after/95-06/`, rows `details-open`, `details-voting-checked`, `details-spectator`, `details-signedup-update`, `details-dm`, `fx-calendar`. readability-compare against BEFORE with `after/95-06/allow.json` (95-05's allow file plus the desktop vote label re-key: unnamed `label|||||` to `label|||||Yes|Maybe|No`, for `details-open`, `details-voting-checked`, `details-signedup-update`, `details-dm`): `fx-calendar` clean, so the Calendar page is unchanged. The 21 failing rows are contrast only and all inherited (see deviation 1).
- Gate 2 captures, before and after, desktop 1440 and phone 390 voting state: `after/95-06/gate2/`. The Direction A references show only finalized quests, so there is no reference capture of the voting card to set them beside; the before and after pairs stand in.

## Deviations from Plan

**1. [Accepted - inherited contrast] readability-compare exits 1 on contrast only.** After filtering, every remaining `newContrastFailures` entry is one of the carried-over classes from sheets this plan does not convert: desktop `small#signupCharacter-help`, the role help list, the `Select ...` labels and the `Vote on Proposed Dates` h6 (3.27 to 3.6, same elements and ratios as BEFORE, re-keyed by 95-05's id and wording changes); phone help list, labels and h6 at 3.94 / 4.34 from the unlayered `quests.mobile.css`; and the new phone month `h3.quest-section-heading` in the update form at 4.34 (the same class and root cause as the existing phone `h6`; the heading is new, the failing sheet is not). Recorded in `allow.json` under `_inheritedContrast`. Nothing non-contrast regresses.

**2. [Rule 1 - Bug] Vote cells were clipped by the fixed calendar row.** The first render with 32px buttons showed the third button cut off by the 120px day cell. Fixed in place in `calendar.css` (116px event, 160px Details week row).

**3. [Rule 1 - Bug] Hidden group name failed the contrast probe.** The visually hidden `Your vote for` span and its `time` inherited parchment inside the white vote box (desktop) and the bare-board colour on the phone, so the probe measured it under 4.5:1. Fixed by giving `.vote-buttons` dark ink and `.calendar-vote-row` the heading colour in `quest-vote.css`.

**4. [Rule 1 - Bug] New month heading failed contrast.** With the nested card gone the heading sat on plain glass (3.6:1). The scrim band in `quest-details.css` fixes it; weight 600 does not, because the probe treats only 700 as large text.

**5. Checked border colour differs from the plan text.** The plan named a 2px `--qb-fg` border. On desktop the vote box is white, where parchment is 1.3:1, so the desktop edge is `--qb-ink` and the phone keeps `--qb-fg`.

**6. Plan said new `quest-details-months` wrapper class optional; used `quest-details-month` per month plus a heading class.** Spacing is per month block rather than a single wrapper.

**7. Tracer gate and Gate 2 sheet.** No tracer task here. No Direction A reference exists for the voting card (see Verification).

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Only label classes, label content and the group's name attributes changed; input names, ids, values and checked logic are untouched and pinned by the new facts (T-95-15). The `calendar.css` edits sit in Details-scoped current-quest, vote and week-row rules and the `fx-calendar` row is unchanged against BEFORE (T-95-16).

## Notes for later slices

- The unlayered `.vote-buttons` box (white, 0.95 alpha) and the white current-quest cell are the calendar phase's to convert; `quest-vote.css` only sets text colour on the box.
- The phone Details text colour (4.34) and `.form-text` are still what the zero-contrast gate of 95-07 must clear, now including the phone month heading.
- Working-tree app left running on `http://localhost:8000`.

## Self-Check: PASSED

Verified: `quest-vote.css`, `quest-details.css` exist; commits `507eba4a` and `01e3c4da` exist; full `dotnet test` green; vote groups and months-in-card facts pass on both layouts.
