---
phase: 95-quests-and-quest-log-ui-conversion
plan: 13
subsystem: ui-quest-manage
tags: [quest-manage, foundation, pages-layer, phone-relayout, contrast, disclosure, quick-stats]
requires:
  - phase: 95-03
    provides: approved phone Manage mockup (v1) and the Gate 1 twin answers
  - phase: 95-12
    provides: quest-manage.js, data-action hooks, WallClock dates, the named Manage contrast carry-over
provides:
  - desktop Quest Manage on the foundation (h1 page header, h2/h3 sections, badges with icon and word, foundation tables) with unified labels
  - phone Quest Manage as the approved single-card layout with Quick Stats and the admin Remove on finalized rows
  - quest-manage.css and dm-manage.mobile.css as @layer pages sheets on tokens only
  - zero text-contrast failures on every Manage state on both layouts, with no contrastCarried key and no exception entry
affects: [95-14 and later closure plan: .quest-card and .quest-status-* are the only Manage-era unlayered rules left]
tech-stack:
  added: []
  patterns: [status words on glass remapped to the card colour through --bs-*-rgb on the page scope, hover that darkens or strengthens a border instead of lightening a band under text, :has(radio:checked) for the selected date option, Razor local functions that write markup for repeated vote blocks]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/quest-manage.css
  modified:
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/dm-manage.mobile.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "Status words written as text on a glass card take the card colour on both Manage layouts (the light status variants measure 3.19 to 3.63:1 over the brightest board pixel); the badge, the icon and the words carry the status. Tables and alerts keep their own mapping."
  - "Hover never lightens a band under text: the desktop disclosure hover darkens (scrim) and strengthens the border, the date option hover strengthens the border. A glass-hover fill measured 3.38:1 under the Description label."
  - "Selected date option: gold border plus an inset ring while its radio is picked, in addition to the filled radio."
  - "Delete Quest is the last button of its row on both layouts; on the phone it follows Refresh Data."
  - "DM Controls and Quest Summary headings stay h5 because the DM Controls tests pin the literal markup."
requirements-completed: [D-16, D-18, D-24, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8]
duration: ~2 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 39000
  tasks: 2
  commits: 2
---

# Phase 95 Plan 13: Quest Manage on the foundation, both layouts Summary

Desktop Manage keeps its main column plus sidebar and moves onto the foundation; the phone becomes the approved single card with Quick Stats and an admin Remove; both page sheets are layered, the old unlayered date and vote rules are gone, and every Manage state on both layouts now has zero text-contrast failures with nothing excepted and no `contrastCarried` entry.

## What changed

**Task 1, desktop (`024aad34`).**
- Main column is one `.modern-card` with an `h1` header (`Manage Quest: ...`) and the CR badge (not on Campaign boards). Description and Rewards are headings whose only content is a full-width button (section icon, label in the card colour, chevron turned by `transform`, `aria-expanded` and `aria-controls` kept). Sections are `h2`, player groups `h3`.
- Heading list, no trailing colon on any: Selected Date, Selected Participants, Waitlist, Quick Stats, Proposed Dates & Voting Results, Player Selection.
- Vote counts are `.badge` with icon and word (`Yes (3)`), voter names stay in lists under them. Selected Participants and Waitlist are the foundation `.table`; the admin Remove is `btn btn-danger btn-sm` with the word. Finalize `btn-success`, Edit Quest `btn-warning`, Delete Quest `btn-danger` last in its row (all three delete controls read `Delete Quest`), Refresh Data `btn-secondary`. "Send again" is a filled `btn-warning btn-sm`. Icon-only remove buttons carry an `aria-label` with the name; every icon is `aria-hidden`.
- Empty lists keep today's copy ("No participants selected for this quest.", the No Proposed Dates alert, "No proposed dates yet").
- DM Controls and Quest Summary: class strings, labels, order, the disabled Move control and `ViewBag.BoardToday` unchanged.
- `quest-manage.css`: one `@layer pages` block, tokens only, no important flag. It holds the `.date-option` and `.manage-date-option` look the page script binds (dark band, border, gold ring when its radio is picked, stronger border on hover), the disclosure buttons, and the status remap.
- Guard: `Manage.cshtml` links `quest-manage.css`; the sheet is in `ConvertedSheets` and `PhaseSourceFiles`; new fact `UnlayeredSheets_DoNotStyleTheManageDateOptions` fails if `legacy-pages.css` or `quests.css` style `.date-option`, `.manage-date-option` or `.vote-summary` again.

**Task 2, phone (`a3519778`).**
- `dm-manage-section-card` and both nested cards replaced by one `.modern-card` (count of `.modern-card` elements 1, nested 0; `dm-manage-section-card` 0). `h1` header with the neutral CR badge on the right; Description and Rewards as `h2` disclosures (full-width button at least 44px, collapsed by default, chevron by `transform`); sections separated by a 1px `--qb-border` rule.
- Open state: Quick Stats (Best Date via `date-time-compact`, "N players available", desktop wording) directly above Proposed Dates & Voting Results, then Player Selection. `Quick Stats` count in the view: 1 (baseline at HEAD 0). Finalized: Selected Date with condensed badges, Selected Participants with an admin-only Remove per row, Waitlist.
- Action rows `flex-fill`, 44px; Refresh Data is a filled `btn-secondary`; Delete Quest last; `Create Follow-Up Quest` in full, fits at 390 and 320 so no abbreviation exception is recorded.
- `manage-date-option`, `dm-vote-summary`, the module data attributes and the `dm-manage.mobile.css` link kept. `dm-manage.mobile.css` is one `@layer pages` block on tokens (no colour literal, no important flag).
- New facts in `MobileViewsTests`: Quick Stats above the voting results on an open quest; an Admin viewing a finalized quest gets exactly one `remove-player` control per selected participant; the quest's own (non-admin) DM gets none (T-95-32).

## Removed unlayered rules

- `legacy-pages.css`: the "Date and Vote Styles" block (`.date-option`, `.date-option:hover`, `.vote-summary`; old lines 22 to 38, 17 lines) and the "Clickable date blocks" and "Enhanced clickable date blocks for manage page" blocks (`.date-option` and `.date-option:hover`, `.manage-date-option` with its hover and `:checked` rules; old lines 716 to 755, 40 lines). 57 lines total. `.quest-card` and `.quest-status-*` stay for the closure plan.
- `quests.css`: the "Date and Vote Styles" block (`.date-option`, `.date-option:hover`, `.vote-summary`; old lines 316 to 332, 17 lines). The plan's "about 1-47" had already moved; the poster work had shifted the block.
- `dm-manage.mobile.css`: all 59 old unlayered lines (glass wrapper, `!important` parchment text and shadows, rgba literals) replaced by the layered sheet.

## Contrast counts (text contrast failures, measured over the lightest board pixel)

| Row | Desktop 1440 / 320 BEFORE (and 95-12) | AFTER | Phone 390 / 412 BEFORE (and 95-12) | AFTER |
|-----|------|-------|------|-------|
| manage-open | 19 | 0 | 17 | 0 |
| manage-finalized | 5 | 0 | 12 | 0 |
| manage-campaign | 2 | 0 | 2 | 0 |

The carry-over 95-12 handed over is cleared: the five date strings (4.19:1 desktop, 4.34:1 phone), the Yes/Maybe/No vote labels (3.19 to 3.63:1), `small.text-success` (3.56:1), the gold Description toggle (2.78:1 and 2.61:1), `small` (2.99:1), the `span.text-muted` separators (3.94:1), `p.text-muted` (4.25:1 and 3.94:1), the phone headings and player names (4.34:1) and the outline Refresh Data (1.17:1). The success text that measured 3.3:1 on desktop Manage is gone with the status remap. After the last change `readability-compare --zero-contrast` over eight Manage rows, both User-Agents, every width: 0 residual failures, 0 problems against BEFORE, 0 poster-glyph violations, `ok`. The 95-13 `allow.json` has no `contrastCarried` key; the compare relied on it for nothing, and no exception entry was added or changed.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1312 passed; integration 1787 passed, 11 skipped (pre-existing), 0 failed. Task 1 filter: 163 passed. Task 2 filter (`MobileViewsTests`, guard, parity, move control, DM controls): 125 passed.
- Sweep `after/95-13`: manage-open, manage-finalized, manage-campaign plus scratch rows `*-expanded` (Description opened), `manage-finalized-admin` and `manage-signedup-admin` (admin Remove, player rows, availability status); desktop 1440 and 320, phone 412, 390 and 320; 40 captures, all 200. No horizontal overflow, no target under 44px on the phone, no missing focus ring, no action failure, no new console error (the finalized desktop rows show the two character-picture 404s that the avatar module turns into the placeholder, as in the previous plan).
- Frames: `~/qb-ui-review/phase-95/after/95-13/vs-mockup.html` puts each phone AFTER beside the approved mockup frame (open, expanded, finalized, signups at 390; open and finalized at 320).

## Deviations from Plan

**1. [Rule 1 - Bug] Light hover band cost the label its contrast.** The first desktop disclosure hover (`--qb-surface-glass-hover`) put the expanded Description label at 3.38:1. Hover now darkens (scrim) and strengthens the border; the date option hover is a stronger border only. Fixed in `a3519778` with the phone commit.

**2. [Rule 2 - Missing critical] Status remap on the page scope.** Not in the plan's wording: `--bs-secondary-color` and the success, warning and danger `-rgb` variables are set to the card colour on `.quest-manage-page` and `.manage-card`, because the light variants the foundation maps for glass fail 4.5:1 at the lightest board pixel. Meaning stays in badges, icons and words. `.text-muted` and `text-success` text classes were removed from the markup where a plain line serves.

**3. [Rule 2 - Missing critical] Accessible names.** Icon-only remove buttons gained `aria-label="Remove player <name>"` (spectator and participant variants likewise); icons are `aria-hidden`.

**4. [Judgement] Two departures from the approved phone frame, both toward the written contract.** (a) Delete Quest is last, after Refresh Data (the mockup drew Refresh last). (b) Edit Quest is `btn-warning` in the finalized phone state too (the frame drew `btn-primary`), so one role has one button class on both layouts. The finalized rows now carry two amber buttons (Edit Quest, Open Quest) on both layouts; their labels and icons differ. Flagged for Gate 2.

**5. [Judgement] DM Controls and Quest Summary stay `h5`.** `QuestManageDmControlsTests` pins `<h5>DM Controls</h5>` and `<h5>Quest Summary</h5>` literally and is outside this plan's files; Quick Stats under it is an `h6`.

**6. [Judgement] Quick Stats on desktop stays inside the Quest Summary card** (layout unchanged by the contract); the phone shows it as its own section.

**7. [Rule 3 - Blocking] Scratch tooling.** Manifest rows added: `manage-open-expanded`, `manage-finalized-expanded`, `manage-campaign-expanded`, `manage-finalized-admin`, `manage-signedup-admin`, and phone width 320 on every `manage-*` row. `after/95-13/allow.json` renames the old Delete anchors and the phone "Create Follow-Up" control.

## Known limits

- No Audit Board fixture has Rewards, so the Rewards disclosure was not opened in a sweep. Its markup and CSS are the Description disclosure's, and the Rewards branch is the same Razor condition as before.
- Confirm and alert dialogs stay native, as the previous plan recorded.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-32: the phone Remove renders under the same `isAdmin` condition the desktop uses (a fact covers Admin present and DM absent); the endpoint authorises server-side. T-95-33: Quick Stats shows the figures the desktop shows the same DM; Manage stays DM and Admin only server-side.

## State left behind

- Working-tree app running on `http://localhost:8000` on this build; port 8001 untouched; WINDOWS.md #30 left alone; the untracked Phase 102 file not staged.
- No real data changed: the sweep only read pages; no form was submitted.

## Self-Check: PASSED

Verified: `quest-manage.css` exists and opens `@layer pages`; commits `024aad34` and `a3519778` exist; `Manage.Mobile.cshtml` has `data-bs-toggle="collapse"`, `aria-controls`, `Quick Stats` once and no `dm-manage-section-card`; no `.date-option`, `.manage-date-option` or `.vote-summary` in `legacy-pages.css` or `quests.css`; full build and test green; `readability-compare` with zero-contrast on the eight Manage rows printed `ok`; `after/95-13/vs-mockup.html` exists.
