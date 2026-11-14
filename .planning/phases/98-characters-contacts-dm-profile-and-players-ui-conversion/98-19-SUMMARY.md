---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 19
subsystem: contacts-list
tags: [ui-conversion, razor, stretched-link, shared-card-sheet, twin-views, guard-tests]
requires: [98-18]
provides:
  - "Contacts/Index (desktop) and Index.Mobile (phone) on the converted list contract: page-title card, h2 aside group cards, one stretched row-nav-link per card or row, ring on the card or row"
  - "contacts.css and contacts.mobile.css rewritten as one pages-layer block each (tokens only, no important flag, no keyframes); contacts.css out of the global layout list"
  - "ContactsIndexRenderTests (both layouts), PhoneContacts_HasThePageHeadingWhenCategoriesExist, ContactsFilter_KeepsItsDeliberateTwinDifference"
affects: [98-22, 98-23]
tech-stack:
  added: []
  patterns: [member-cards.css reused beside a small per-list sheet; filter row kept as a toggle-pill row in the page-title card; Filter Tags offcanvas trigger kept as a secondary button with a status badge]
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/ContactsIndexRenderTests.cs
  modified:
    - QuestBoard.Service/Views/Contacts/Index.cshtml
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/wwwroot/css/contacts.mobile.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs
    - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Gate 1 a2 applied as drawn: the phone keeps its Filter Tags button and the existing phone label Categories, recoloured to btn-secondary, with a green Active badge (badge bg-success) while a filter is on"
  - "The phone page title card carries the h1 Contacts only when the board has categories; a board without categories keeps its Contacts section title (h2) as its heading, as the plan words it"
  - "Hidden and tag badges share one badge row; the Hidden badge is bg-secondary with fa-eye-slash and the word"
  - "The phone action row wraps (flex-wrap, label never squeezed) instead of splitting the width three ways, because the approved draft broke the word Hidden inside the narrow Show Hidden button"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 19: Contacts list twins Summary

Both Contacts list views are converted onto the shared member card sheet: stretched-link cards and rows replace the wrapping anchor and the inline click handler, the group titles are readable h2 section titles, `+ Contact` is the one primary action, the phone gains its page heading, and the phone keeps its deliberate Filter Tags button, recoloured. `contacts.css` is out of the global layout list.

## Outcome applied

From `gate1/answers.json`: `additional.a2` = as drawn (phone Filter Tags as `btn-secondary` with a green Active badge while a filter is on; the label Categories kept), `headings` (h4/h5 to h2, phone heading in the existing desktop words), `lookCheck` approved. The approved drafts (`contacts.css`, `contacts.mobile.css`) and drawings were ported; `member-cards.css` from the Characters list slice is reused, not forked.

## Task 1 (tracer, commit `0a988bea`): desktop end to end

- `Index.cshtml`: page-title card with `h1` Contacts, Show/Hide Hidden (`aria-pressed` kept, both states `btn-secondary`), Manage Categories `btn-secondary`, `+ Contact` `btn-primary` (the only one); the filter row sits in the same card with its hook classes, tag toggles as pills (a pill fills with wood when ticked, the ring shows on focus), Apply and Clear as `btn-secondary btn-sm`. One `modern-card--aside` section per category group with an `h2` (Ungrouped at full contrast, the old opacity rule is gone). Each `contact-card member-card modern-card--interactive` holds the name as the one `a.row-nav-link`, the picture only when one is stored or the placeholder tile otherwise, the town and sub-location lines, tag chips as 14px `badge bg-secondary` (hook class `contact-tag-chip` kept, DM tier only as before) and the Hidden badge with `fa-eye-slash` and the word. The two empty states are verbatim `.empty-state` blocks with an `h2` (`Clear filters` on desktop). `@section Styles` links `member-cards.css` then `contacts.css`.
- `contacts.css`: one `@layer pages` block, tokens only, `!important` count 0 (unchanged: already 0 at HEAD), the card, ripple and group-heading rules deleted (about 390 lines to about 55), the finite ripple keyframes gone. The duplicated card rules live only in `member-cards.css`. Removed from `_Layout.cshtml`.
- Guards: `contacts.css` added to `PageOnlySheets` (so `Layouts_LinkNoAreaSheet` covers it), `LayeredFiles`, `NoLiteralFiles`, `ConvertedSheets` (so `AreaSheets_HaveNoInfiniteAnimation` now also forbids keyframes there), `ConvertedViews`, `ViewSheetLinks`, `PhaseSourceFiles`.
- Tracer gate: no auto flag is set (both config values `false`) and a sequential run cannot be resumed, so (as in earlier slices) the gate was the automated re-run of the tracer verify (124 tests green) and a live capture of `contacts-index` on 8310 under `--stack`, which matched the approved drawing, before the phone work.
- Acceptance: `grep -c contacts.css _Layout.cshtml` = 0; `!important` count in `contacts.css` = 0.

## Task 2 (commit `8673cebf`): the phone list

- `Index.Mobile.cshtml`: title card with `h1` Contacts when categories exist, then the action row (`Show Hidden`/`Hide Hidden`, `Categories`, `+ Contact` as the one primary), `Filter Tags` as `btn-secondary` with `badge bg-success ms-1` `Active` while a filter is on, disabled (foundation disabled look, no `aria-controls`) when the board has no tags; the offcanvas keeps its id and content, gains `aria-labelledby`, Apply is `btn-primary` and Clear `btn-secondary`. Group section cards with `h2`; `contact-member-row member-row modern-card--interactive` slips with one stretched `a.row-nav-link`, the `avatar-32` picture or placeholder, location lines and one badge row (Hidden, then tags). No inline handler (`grep -c window.location` = 0; HEAD: 1). The empty states are verbatim `.empty-state` blocks (`Clear Filters` kept capitalised on the phone).
- `contacts.mobile.css`: one `@layer pages` block, no media query, `.mobile-layout` scope, tokens only, `!important` count 0 (HEAD: 9).
- Tests written first and confirmed red (11 failing), then green: 11 phone facts in `ContactsIndexRenderTests` and the two twin-answer facts. The desktop facts of Task 1 were written before the view but, because the view was written in the same step, the red run was only shown for the phone facts.
- `ContactsTagsMobileTests.Index_MobileActiveFilter_ShowsActiveBadgeAndCarriesSelectionOnToggleForm` rewritten to the same intent (an Active badge with the word Active inside Filter Tags when a filter is on; now the approved green `bg-success`, not the pinned `bg-dark` markup) and logged in `tests-renamed.md`. The other Index facts of that class, `ContactsTagsDesktopMarkupTests`, `ContactsControllerIntegrationTests` and `ContactCategoryMobileRenderTests` pass unchanged.
- Phone sheet and view appended to `LayeredFiles`, `NoLiteralFiles`, `ConvertedViews`, `ViewSheetLinks`, `ConvertedSheets`, `PageOnlySheets` and `PhaseSourceFiles`; `legacy-collision.py` prints nothing on both views.

## Task 3: suites, sweep, look check, focus

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2245 passed, 0 failed, 16 skipped, 2261 total (2239 before; plus 22 new cases).
- **Sweep** of `contacts-index`, `contacts-index-player`, `contacts-index-filtered`, `contacts-index-nomatch`, `contacts-index-empty` and `contacts-filter-open` under both User-Agents at every width (24 captures, 0 not 200) into `~/qb-ui-review/phase-98/after/98-19/`, run with `--stack` (glass-stack contrast scoring, the mode phase 98's Characters slice used; nested glass cards are scored as painted: scrim then glass). `readability-compare.py` against BEFORE with `allow.json`: 24 compared, 0 failing rows, 0 poster-glyph violations, `ok`.
  - Contrast failures: BEFORE 1 to 11 per row; AFTER 0 on every row.
  - Overflow: BEFORE every desktop `contacts-*` row overflowed at 320 (scrollWidth 485); AFTER none (scrollWidth 320). No overflow at 390 or 412.
  - Small phone targets: BEFORE 1 to 5 per row; AFTER 0. No missing focus ring, no console or page error.
- **Allowed differences** (`allow.json`, no control lost): a card or row is one stretched link named by the contact name only (it was a wrapping anchor named by all its content); the phone `main` landmark gains the page heading and loses the removed inline-handler row text; the phone filter drawer is named by its title. Two differences are state, not markup: the review account's session holds Show Hidden on (the button reads Hide Hidden) and the board holds the extra `p98 tags probe` contact left by the earlier tag round-trip check; the toggle was not changed because it is the fixture session's state.
- **Look check** (`lookcheck.json`): desktop against `quest-details.html`, 31 properties, 2 intended differences (glass hover value, button border), pass; phone against `quest-details.mobile.html`, 31 properties, same 2, pass.
- **Reduced motion and focus** (`motion-focus.json`, `motion-focus-focus-*.png`): with `reducedMotion: 'reduce'` no card or row lifts, `document.getAnimations()` is empty after load and after hover on both layouts, nothing on the page has an infinite animation. With normal motion the desktop card lifts 2px and nothing animates. Tab to the first card (desktop) and first row (phone) draws the two-tone ring on the card or row (2px dark outline at 2px offset plus the 2px gold inner stroke), none on the link, not clipped by any ancestor. Viewed: gold ring around the whole slip on the phone.
- I viewed the desktop and phone captures: gold Cinzel title with the secondary and primary actions on the title band, the filter row as pills under it, aside group cards with the square media cards in a wrapping grid and the Hidden badge on the picture; on the phone the three actions wrap cleanly, Filter Tags carries the green Active badge, rows are 44px or taller with avatar, name and badges.
- `QuestBoard.IntegrationTests/TestHostFileWatching.cs` and `QuestBoard.Service/Controllers` are untouched (git diff against PLAN_START is empty).

## Deviations from Plan

### Plan interpretations

1. **[Active badge colour]** The plan says to keep the pinned `bg-dark` Active badge unless the sweep shows it fails; the approved Gate 1 drawing (a2) and the draft frame show the green `bg-success` badge, which holds contrast on the grey `btn-secondary`. I followed the approved drawing and rewrote the one pinned assertion to the same intent (logged in `tests-renamed.md`).
2. **[Phone action row]** The approved draft split the action row into three equal flex columns; on a 412px phone the narrow Show Hidden button broke the word Hidden across lines. The row now wraps (each button takes the width its label needs, labels never wrap). Only `contacts.mobile.css` differs from the draft, in the two rules for that row.
3. **[Guard timing]** `contacts.css` joined `LayeredFiles`, `NoLiteralFiles` and `UiFoundationLayerGuardTests` in Task 1 instead of Task 2, because the desktop sheet was rewritten in Task 1 and the guard should cover it from that commit.
4. **[Tracer gate]** No auto flag is set and a sequential subagent cannot be resumed, so the gate was the automated re-run plus a live capture (as in earlier slices).
5. **[Contrast scoring mode]** The sweep ran with `--stack` (see Task 3), the mode phase 98's Characters slice used for nested glass cards; the plan's sweep command has no flag for it.
6. **[Tool]** `list-motion-focus.mjs` (scratch, outside the repository) gained `--desktop-sel` and `--mobile-sel` so it serves the Contacts item classes; its Characters defaults are unchanged.

No bugs or missing-functionality fixes were needed outside the plan.

## Known Stubs

None.

## Threat Flags

None. No controller, view model or query changed (T-98-46, T-98-47): the visibility and tag rules are untouched, the player-view facts (no tag markup, no hidden contacts, no add action) run under both User-Agents, names reach the markup through Razor encoding (no `Html.Raw`), and the stretched link is a plain anchor to the existing details route.

## Self-Check: PASSED

- `ContactsIndexRenderTests.cs`, the two converted views and the two converted sheets exist; commits `0a988bea` and `8673cebf` exist.
- Report, sweep captures, `allow.json`, `lookcheck.json`, `motion-focus.json` and the focus screenshots exist under `~/qb-ui-review/phase-98/after/98-19/`.
