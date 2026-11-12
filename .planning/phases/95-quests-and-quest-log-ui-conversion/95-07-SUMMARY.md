---
phase: 95-quests-and-quest-log-ui-conversion
plan: 07
subsystem: ui-quest-details
tags: [column-split, twin-parity, avatar-module, layered-css, contrast, wall-clock]
requires:
  - phase: 95-06
    provides: quest-details.css and quest-vote.css in the pages layer, months drawn inside the signup card
provides:
  - desktop Quest Details on the foundation: col-lg-9 / col-lg-3, one page card, h2 and h3 sections, no nested cards
  - phone Quest Details as a single column of foundation cards with the twin additions copied from the desktop
  - shared .avatar-32 and .avatar-32--placeholder components and the avatar-fallback.js module
  - quests.mobile.css rewritten into the pages layer on tokens
  - zero text-contrast failures on every phone Quest Details state; desktop failures reduced to one calendar-sheet carry-over
affects: [95-08 and later slices (Manage and Quest Log reuse .avatar-32 and avatar-fallback.js), Phase 97 (calendar.css)]
tech-stack:
  added: []
  patterns: [sections as headings instead of cards inside a card, framework secondary colour re-pointed at the card colour on a page scope, own-row tint through a table variable]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/avatar-fallback.js
  modified:
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quest-details.css
    - QuestBoard.Service/wwwroot/css/quests.mobile.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs
key-decisions:
  - "Contrast was cleared by removing cards nested inside the main card, not by recolouring text: the probe scores every glass layer, so two stacked cards measured 3.6:1 (3.27:1 for muted text) while one card holds 4.68:1. The signup, join and update panels are now sections with an h2 inside the one page card"
  - "Muted text (.text-muted, .form-text, the calendar partial's time lines) measures 4.25:1 on a single glass card, so on Quest Details the framework secondary colour is re-pointed at the card colour (phone: through a custom property on the cards, desktop: .form-text in the pages layer) and the text-muted classes were dropped from the view"
  - "The viewer's own participant row is marked by the word You in a badge plus a tint: on the desktop table a gold-and-paper mix through --bs-table-bg-type, on the phone the opaque overlay surface"
  - "Quest Finalized panel keeps Participants, Waitlist and Not Selected as h3 headings inside it (the panel is the h2), rather than lifting the tables out of the panel"
requirements-completed: [D-16, D-18, D-24, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8, DoC-9]
duration: ~1 h 30 min
completed: 2026-11-11
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 95 Plan 07: Quest Details on the foundation, both layouts Summary

Quest Details is fully on the foundation on both layouts: the approved 9 / 3 column split, one page card with headed sections, the twin additions on the phone, one wording for the twin labels, no inline style, script or handler, and both page sheets in `@layer pages`. Every phone state reads at 4.5:1 or better; desktop has a single documented carry-over in the calendar sheet.

## What changed

**Desktop (`Details.cshtml`, commit `7b91807b`)**
- Columns `col-lg-9 col-md-9` / `col-lg-3 col-md-3`, order unchanged. One `.modern-card` for the quest (header holds the `h1` and the CR badge); Rewards, You're signed up!, Quest Finalized!, Join This Quest, Sign Up for This Quest, Update Your Signup, Your Date Preferences and Sign In Required are `h2` headings, with Participants, Waitlist, Not Selected, Vote on Proposed Dates and Select Your Role as `h3`, none with a trailing colon. The three signup panels are `<section aria-labelledby>` blocks, not cards. Sidebar: Quest Summary and DM Controls cards with `h2`, Current Signups as `h3` plus plain lists (icon per role, no `text-primary/success/info`).
- Manage Quest is `btn btn-warning` with `fa-cog`. The disabled Move control and its `move-reason` paragraph are unchanged.
- "You" marker: `quest-details-own-row` tint plus a visible `You` badge on the viewer's own row in all three tables.
- Avatars: `img.avatar-32` plus a hidden `avatar-32 avatar-32--placeholder` sibling; no style attribute, no `onerror`. `avatar-fallback.js` (ES module, capture-phase `error` listener plus a sweep of images that already finished with `naturalWidth === 0`; toggles `hidden` only) is loaded from the Scripts section.
- Help lines are `small d-block mt-1`; icon-only change/add character buttons gained `aria-label`; decorative icons are `aria-hidden`.
- Dates: the three finalized-date lines use `Html.WallClock(date, "date-time")` guarded by a pattern match; `CreatedAt` stays `Html.LocalTime`. Every `ViewBag.BoardToday` read is untouched.
- `components.css`: `.avatar-32`, `.avatar-32--placeholder` in the components layer with a why-comment. `quest-details.css`: heading spacing, section spacing, CR badge, own-row tint, `.form-text` colour, plus the month rules from the previous plan.

**Phone (`Details.Mobile.cshtml`, commit `bb53aedb`)**
- Single column of `.modern-card` blocks; `participant-row` markup kept, no `table-responsive`. Header card: `h1`, CR badge, DM, status badge with icon, Building Access badge, Continues in / Continues from, description, Rewards.
- Added, wording and icons copied from the desktop: Building Access, Continues in / from, Current Signups with per-role count lines, the You're signed up! panel (selected with date, waitlisted, or the open-quest thank-you), the Sign In Required notice with sign-in and register links.
- Wording: `Assistant DM` (was `Asst. DM`), `Participants` (was `Adventurers`), `Open for Signups` (Campaign `Open`/`Closed` kept), `Manage Quest` as `btn-warning` with `fa-cog` (was `fa-cogs`), `Update Your Signup` heading. `Join This Quest`, the `*FormMobile` ids and the join strings are unchanged.
- Finalized date through `Html.WallClock`; no inline style, script or handler.
- `quests.mobile.css` is one `@layer pages { }` block on tokens: no colour literal, no `!important`. `.also-that-day--cancelled` keeps `text-decoration: line-through` (its `opacity: 0.55` was dropped because it cut the line's contrast). `Admin/Quests.Mobile.cshtml` links `admin-quests.mobile.css`, not this sheet, and uses none of its classes; the `fx-admin-quests` rows are unchanged against BEFORE.

**Conversion guard** (`QuestsConversionGuardTests`): `Quest/Details.cshtml` and `Quest/Details.Mobile.cshtml` in `ConvertedViews`, `quests.mobile.css` in `ConvertedSheets` and `PhaseSourceFiles`, `avatar-fallback.js` in `PhaseSourceFiles`, phone Details in `ViewSheetLinks` (`quests.mobile.css`, `quest-vote.css`).

## Removed stylesheet rules

| File | Lines (at the start of the plan) | Rule | Where it went |
|------|----------------------------------|------|---------------|
| `quests.css` | 453-463 | `.quest-details-page .modern-card:hover` (`transform: none !important` and a fixed shadow) and `.quest-details-page .modern-card` (a transition) | Nowhere: the foundation `.modern-card` is static, so the lift they disabled no longer exists |
| `quests.css` | 269-388 | `.quest-signup-list`, `.quest-signup-item*`, `.character-mini-avatar*`, `.character-info`, responsive block | **Kept.** Neither Details view uses these classes; they style `QuestLog/Details` and `QuestLog/Details.Mobile` (and the avatar classes `Quest/Manage`). They belong to the Quest Log slices. See deviation 1 |
| `legacy-pages.css` | 299-791 | none | **Nothing removed.** The only Details-reaching selector in the range was `.cr-badge`, which Details no longer uses (the CR badge is `.quest-details-cr`); `.cr-badge` stays for the board, Manage and Quest Log. `.container.mt-4` (299-302) kept |
| `mobile.css` | whole file | none | The phone sheet holds only `.container-fluid` padding and the Move reason rule, neither reaches a Details element in a way that needed change (the Move reason rule stays for the Move plan) |

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1300 passed; integration 1775 passed, 11 skipped (pre-existing), 0 failed.
- Sweep `~/qb-ui-review/phase-95/after/95-07/` (rows `details-open`, `details-voting-checked`, `details-spectator`, `details-spectator-back`, `details-signedup-update`, `details-finalized-selected`, `details-finalized-waitlisted`, `details-finalized-notselected`, `details-dm`, `details-anon`, `fx-admin-quests`; 47 records, desktop 1440/1280/1200/992/320, phone 412/390). `readability-compare` against BEFORE with `after/95-07/allow.json` (the previous plan's allow file plus the vote-label re-keys for `details-spectator-back`) and `--zero-contrast` over the ten Details rows: exit 0, `ok`.
- Contrast, Quest Details rows, counts of in-content failures (all widths, both User-Agents): BEFORE desktop 1213 and phone 312; AFTER desktop 182 (every one the calendar weekday header, below) and phone 0. Previously carried-over phone labels, headings, help text and `h3.quest-section-heading` (4.34 and 3.94), and desktop `.form-text`, `signupCharacter-help`, role help list and h6 (3.27 to 3.6), are all cleared.
- `fx-admin-quests` (phone Admin/Quests) shows the same two contrast entries it showed BEFORE (`small.text-muted` 2.89, `strong.parchment-text` 3.61, from `admin-quests.mobile.css`); nothing new.
- No overflow at 320 desktop or 390 phone; no new small target after the sign-in link fix (deviation 3).

### Contrast exception for the operator (Gate 2)

`~/qb-ui-review/phase-95/contrast-exceptions.json` holds five entries, one per desktop row `details-open`, `details-voting-checked`, `details-signedup-update`, `details-dm`, `details-spectator-back`, selector `div.calendar-day-header` (the weekday names of the calendar months inside the signup card, 11px, 3.76:1; was 2.99:1 BEFORE). Cause: the unlayered `.calendar-header` band (translucent white, stacks on the card glass) and `.calendar-day-header` rules in **`calendar.css`**, which **Phase 97 (Events, Series, Calendar and Agenda UI conversion)** converts. The same header measures 3.76:1 on the Calendar page itself. No other failure is excepted.

### 390px abbreviation check

`Assistant DM` fits: in the Not Selected and Participants rows the role badge is about 130px wide beside a name column that wraps, there is no horizontal overflow at 390 or 412, and the readability compare shows no new overflow or small target on those rows. No label was abbreviated, so there is no abbreviation exception to record. The phone vote-change buttons (`Yes`, `Maybe`, `No`) keep their phone-only short words as before.

## Deviations from Plan

**1. [Plan assumption wrong - kept the rules] `quests.css` 269-388 is not Quest Details.** The plan asked for the "Details signup list" rules to be removed. Neither Details view renders `.quest-signup-*`, `.character-info` or `.character-mini-avatar` (a descendant rule inside `.quest-signup-item`); they serve `QuestLog/Details*` and `Quest/Manage`. Deleting them would have broken those pages, so they stay for the Quest Log and Manage slices. The criterion's intent (no unlayered Details rule left in `quests.css`) is met: the only Details rule there, 453-463, is gone.

**2. [Plan conflict - twin answer wins] No `avatar-32` on the phone view.** The plan's acceptance line expects the string `avatar-32` in `Details.Mobile.cshtml`, and the action text says "avatars as on desktop". The recorded twin answer, repeated in the must-haves, says portraits beside names stay desktop-only, and the phone never had portraits. Adding them would contradict that answer, so the phone has none and does not load `avatar-fallback.js`. That one grep criterion is not met by design; `Open for Signups`, `Assistant DM` and `Sign In Required` are present.

**3. [Rule 1 - Bug] New phone sign-in links were under 44px.** The Sign In Required notice's inline links failed the small-target check on `details-anon`. `.alert-link` in `quests.mobile.css` is now an `inline-flex` box with `--qb-hit-min` as its minimum size.

**4. [Rule 1 - Bug] `WallClockUnmovedTests` pinned the old long date text.** Two facts asserted `dddd, MMMM dd, yyyy 'at' h:mm tt` on the finalized game night. With the date now rendered through `Html.WallClock(..., "date-time")` the text is the helper's invariant `MMM d, yyyy, h:mm tt`. The facts keep their intent (identical text under two zones, never inside a `local-time` element) with the expected text recomputed from the helper's style. Not in the plan's file list; needed for the suite.

**5. [Judgement] Participants, Waitlist and Not Selected are `h3` inside the Quest Finalized! panel.** The plan lists them among the `h2` sections. They sit inside the panel whose heading is the `h2`, so `h3` keeps the outline correct and the layout unchanged.

**6. [Judgement] The avatar placeholder keeps the person icon, not an initial.** The plan says "centred initial"; the previous placeholder was an icon and the Direction A reference shows one, so the icon stays.

**7. Tracer gate.** No tracer task in this plan.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The DM Controls and Manage Quest conditions are copied unchanged (T-95-17; `QuestManageDmControlsTests` and `BoardMoveControlTests` green). `avatar-fallback.js` toggles the `hidden` attribute only and builds no markup from data (T-95-18). The phone Current Signups list shows the same names and dates the desktop already shows to the same viewers (T-95-19).

## Notes for later slices

- **Phase 78 plans are out of date.** Converting Quest Details leaves the unexecuted Phase 78 plans (Link Preview Foundation and Quest Cards), written against the old Details markup (nested cards, `h5` card headings, `col-lg-10`, `Asst. DM`, inline avatars, the removed `quests.css` hover rules), stale. They need a refresh before Phase 78 executes. Phase 78 was not edited.
- `Quest/Manage.cshtml` and the Quest Log views still carry inline avatar markup; they can adopt `.avatar-32` and `avatar-fallback.js` as they convert.
- `.cr-badge` (unlayered, `!important`, in both `quests.css` and `legacy-pages.css`) is still used by the board, Manage and Quest Log; the Quest Details copy is `.quest-details-cr`.
- The foundation's muted tone (`--qb-fg-muted`) measures 4.25:1 on a single glass card in the probe's worst-case scoring; any other converted page that puts `.text-muted` or `.header-subtitle` on glass may hit the same limit.
- Working-tree app left running on `http://localhost:8000` (rebuilt from the last commit).

## Self-Check: PASSED

Verified: `avatar-fallback.js` exists; commits `7b91807b` and `bb53aedb` exist; full `dotnet test` green; the ten Details rows pass `readability-compare` with `--zero-contrast` and the five-entry exceptions file; `fx-admin-quests` unchanged against BEFORE.

