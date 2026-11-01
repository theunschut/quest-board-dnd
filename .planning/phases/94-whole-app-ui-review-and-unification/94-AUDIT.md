# Phase 94 Audit: Index and Summary

Index of the whole-app UI audit: one row per manifest view from the six area reports and the shell report, totals, the cascade measurement, the operator question batch for sign-off, the relayout list, merge proposals, dead dependencies and the proposed area-phase cut. Reports: [quests](94-AUDIT-quests.md), [events](94-AUDIT-events.md), [characters](94-AUDIT-characters.md), [shop](94-AUDIT-shop.md), [account](94-AUDIT-account.md), [admin](94-AUDIT-admin.md), [shell](94-AUDIT-shell.md), [vendor](94-AUDIT-vendor.md).

`QBR` is `~/qb-ui-review/phase-94`. Screenshots are never committed; paths below are scratch paths.

## Summary

Work in progress: the desktop layout has been audited end to end and indexed (see `94-AUDIT-shell.md`); the remaining sections are completed in the following steps of the same plan.

## Index

One row per view, twins on one row, plus one row per layout.

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Shared/_Layout (desktop layout with `_Toasts`, backdrop and shell rules) | `Views/Shared/_Layout.cshtml` | `Views/Shared/_Layout.Mobile.cshtml` (next row) | Foundation (94) | a11y 13, visual-bug 4, cascade-fight 2, perf 2, inconsistency 1, dead-code 1 (23) | S2 | none (the shell has no list) | 1 (R-shell-1) |
| Shared/_Layout.Mobile (mobile layout, twin of the row above) | `Views/Shared/_Layout.cshtml` | `Views/Shared/_Layout.Mobile.cshtml` | Foundation (94) | a11y 2, visual-bug 1, twin-content 4 (7) | S2 | none | 4 (Q-shell-02, 04, 05, 06) |
| Shared/_Footer (both footers) | `Views/Shared/_Footer.cshtml` | `Views/Shared/_Footer.Mobile.cshtml` | Foundation (94) | twin-content 1, inconsistency 1, a11y 1 (3) | S2 | none | 1 (Q-shell-01) |
| Shared/_Layout.GroupPicker | `Views/Shared/_Layout.GroupPicker.cshtml` | none (served under both) | Foundation (94) | twin-content 1, inconsistency 1 (2) | S3 | none | 1 (Q-shell-03) |
| Platform/_Layout.Platform (both Platform layouts) | `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` | `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` | Foundation (94) | inconsistency 1, cascade-fight 1 (2) | S3 | none | 0 |
| All five layouts (cross-layout items) | the five files above | the five files above | Foundation (94) | a11y 1, perf 1 (2) | S2 | none | 0 |
| Shared/_Toasts (all layouts) | `Views/Shared/_Toasts.cshtml` | none (one partial, all layouts) | Foundation (94) | counted on the first row (A-shell-011 to 017) | S2 | none (copy comes from controllers) | 0 |

## Totals by class and severity

To be computed once every report is indexed.

## Cascade

What moving Bootstrap and Font Awesome into `@layer vendor` costs, measured page by page, and which legacy rules the foundation has to retire first. Evidence is on scratch copies outside the repository; nothing in the repository was edited by the probe.

### Method (cascade-probe)

- **Scratch copies.** `git archive HEAD` of this branch (source identical to the phase base `b961a96f`, which already self-hosts Bootstrap, Font Awesome and Cinzel) was extracted twice under `$QBR/cascade-probe/`: `src/` (the probe) and `control-src/` (untouched control). In `src/` only, `wwwroot/css/app.css` got the layer statement `@layer vendor, tokens, base, components, pages, utilities;` followed by `@import url("../lib/bootstrap-5.3.0/css/bootstrap.min.css") layer(vendor);` and `@import url("../lib/fontawesome-6.4.0/css/all.min.css") layer(vendor);`, and the Bootstrap and Font Awesome `<link>` tags were removed from the five layouts (10 lines). Nothing else changed: no token file, no new rule, the legacy sheets stay unlayered.
- **Runs.** Each copy ran on port 8014 with `run-app.sh` against the audit board; `node tools/sweep.mjs cascade-probe --port 8014` (and `cascade-control` for the control, kept at `$QBR/cascade-probe/control-sweep`) captured every manifest row, 118 captures (59 pages by two User-Agents: desktop 1440, Pixel 7), plus the reflow checks. The probe and control apps were stopped afterwards (nothing listens on 8014).
- **Diffs.** `diff.py` at tolerance 24 with the masks of the `before` record (`$QBR/audit/shell/cascade-diff.py`). Three comparisons: probe against `before` (the plan's reference), control against `before`, and probe against control. Control against `before` differs on 59 captures, all mobile, at most 0.52 percent of pixels: the Cinzel glyph-edge difference plan 94-11 already accepted (desktop is exactly 0), so probe against control isolates the layering effect. A fourth pass masks the bar (top 110px desktop, 190px mobile) and footer (bottom 110px / 200px) to separate chrome changes from page-content changes.
- **Attribution.** A computed-style snapshot of every element on every page (`color`, `background-color`, `display`, padding, margin, `font-weight`, `font-size`, `border-top-color`, `opacity`, `white-space`, `position`, `text-shadow`) was taken on both copies and compared element by element (`style-snapshot.mjs`, `snap-analyze.py`); each changed value is grouped by selector. Properties not sampled (`box-shadow`, `background-image`, `border-radius`, `transition`) can hide further changes; the overlays below show none that the table lacks on the ones viewed.
- **Reading the overlays.** The vs-control overlay of a capture is `$QBR/cascade-probe/vs-control-overlay/{ua}/{id}.png` (red marks differing pixels); the control and probe captures are `$QBR/cascade-probe/control-sweep/{ua}/{id}.png` and `$QBR/cascade-probe/{ua}/{id}.png`; side-by-side crops of the worst pages are in `$QBR/audit/shell/shots/cascade/`.

### Result in one paragraph

Layering Bootstrap alone changes every page: all 118 captures differ from `before` (the bar and footer carry the change), 81 differ outside the bar and footer, 63 by more than 0.1 percent of pixels, 37 are identical in their content. The change is not neutral in either direction. It repairs the cream-on-cream figures on the Events overview rows (the legacy `.modern-card span` rule loses to Bootstrap's `!important` muted colour, so the dark grey is read on paper at 5.97:1 instead of 1.00:1) and it breaks everything that is muted, danger-coloured or `bg-dark` on glass: grey muted text on glass measures 1.57 to 2.24:1, `.text-danger` 1.21 to 1.93:1, and the footer turns into an opaque near-black bar with near-black text at 1.22:1. This confirms the contract's rule: re-theme Bootstrap through its variables in the same commit that retires the matching legacy rule, never by out-specifying.

### (a) Legacy rule families the foundation retires

File:line of each family in the current files (the replacement is the one in the cascade-architecture table of the UI-SPEC).

| Family | Where | Replacement |
|--------|-------|-------------|
| Element-enumerated cream colour: `h1`-`h6` (`color #F4E4BC !important`) and `p, li, span, small` (same) inside `.modern-card` | `modern-card.css:78-90`, `modern-card.css:91-100` | Surfaces set `color`; descendants inherit |
| `.modern-card .form-label`, `.form-text`, `.form-text a`; `.modern-card .text-muted` and `.text-danger` repairs | `modern-card.css:102-117`, `121-141`, `142-145` | token-driven classes (`--qb-fg-muted`, `--qb-danger-text`) |
| `.modern-card .table` double definition (cream cells then parchment cells) | `modern-card.css:252-272` and `274-295` | one `.table` in `components.css` |
| Global element colour rules in `site.css`: `h1`-`h6` (586-592), `.form-label` (594-600), `.form-text` (602-608), `.card-body` (626-630), `p, li, span, small` (632-637), `.text-muted` (639-643) | `site.css:586-643` | `--qb-fg-on-board` inherited from `body` |
| `div { background: transparent }` and `.container, .container-fluid { background: none !important }` | `site.css:473-476`, `site.css:399-401` | deleted |
| Bootstrap component re-themes: 54 rules in `site.css` (lines 40-1701), 32 in `modern-card.css` (32-340), 11 in `mobile.css` (2-135) by the component-class regex used here (the contract counts 213 with a broader rule; the full list is `$QBR/audit/shell/css-retheme.json`) | `site.css`, `modern-card.css`, `mobile.css` | one set in `components.css` through `--bs-btn-*` and the other component variables |
| Navbar and brand rules with `!important`, repeated in two files: `.navbar:not(footer)` (`site.css:501-512`), `footer.navbar` (`site.css:514-517`), `.navbar-brand` (`site.css:524-543`), `.navbar-nav .nav-link` (`site.css:545-570`), `.mobile-layout .navbar` (`mobile.css:80-87`), `.navbar-brand` (`mobile.css:67-77`), `.offcanvas*` (`mobile.css:89-120`) | `site.css`, `mobile.css` | one `shell.css` |
| Duplicated backdrop `body::before` | `site.css:381-395` and `mobile.css:22-35` | one in `base.css` |

### (b) Legacy `!important` colour or background rules that re-colour a Bootstrap utility

Regex used (`$QBR/audit/shell/css-fights.py`, run over every `QuestBoard.Service/wwwroot/css/*.css` with comments blanked and `@media` blocks descended): a style rule whose selector matches `\.(text|bg)-(muted|primary|secondary|success|danger|warning|info|light|dark|white|black|body|body-secondary|body-tertiary|black-50|white-50)\b` and whose declarations contain `color`, `background`, `background-color` or `border-color` followed by `!important`. It finds **45** rules, matching the research count of 45 with a loose regex: **28** name `.text-muted` (the contract's "28 of them `.text-muted`") and 17 name another utility. The contract's figure of 41 was not reproduced with its own regex; the 45 here include three `.bg-dark` background rules (`mobile.css:93`, `site.css:305`, `site.css:311`) and the `.item-name.text-light` rule present in two files, which are the likeliest source of the difference.

| # | File:line | Property | Selector |
|---|-----------|----------|----------|
| 1 | `account.mobile.css:48` | color | `.account-card-mobile .text-muted` |
| 2 | `agenda.css:45` | color | `.agenda-row .text-muted` |
| 3 | `calendar.mobile.css:14` | color | `.agenda-card-mobile .text-muted` |
| 4 | `character-detail.mobile.css:55` | color | `.character-detail-card .text-muted, .character-detail-card small, .character-info-value` |
| 5 | `character-form.mobile.css:37` | color | `.character-form-card .text-muted` |
| 6 | `characters.mobile.css:107` | color | `.character-section-card .text-muted` |
| 7 | `contact-detail.mobile.css:55` | color | `.contact-detail-card .text-muted, .contact-detail-card small, .contact-info-value` |
| 8 | `contact-detail.mobile.css:107` | color | `.contact-detail-card .note-item small, .contact-detail-card .note-item .text-muted` |
| 9 | `contact-form.mobile.css:46` | color | `.contact-form-card .text-muted` |
| 10 | `contacts.mobile.css:106` | color | `.contact-section-card .text-muted` |
| 11 | `dm-manage.mobile.css:44` | color | `.dm-manage-section-card small, .dm-manage-section-card .text-muted` |
| 12 | `home.mobile.css:46` | color | `.quest-card-mobile .text-muted` |
| 13 | `mobile.css:53` | color | `.text-muted` |
| 14 | `mobile.css:93` | background | `.offcanvas-header.bg-dark` |
| 15 | `modern-card.css:102` | color | `.modern-card .text-muted` |
| 16 | `modern-card.css:114` | color | `.modern-card .text-danger` |
| 17 | `platform-group.mobile.css:39` | color | `.platform-group-card-mobile small, .platform-group-card-mobile .form-text, .platform-gr...` |
| 18 | `platform-group.mobile.css:52` | color | `.platform-group-card-mobile .fa-3x.text-muted` |
| 19 | `platform-group.mobile.css:84` | color | `.platform-group-card-mobile [role="alert"] .text-danger` |
| 20 | `platform-users.mobile.css:32` | color | `.platform-users-card-mobile small, .platform-users-card-mobile .text-muted` |
| 21 | `platform-users.mobile.css:44` | color | `.platform-users-card-mobile .fa-3x.text-muted` |
| 22 | `players.mobile.css:73` | color | `.players-section-card .text-muted` |
| 23 | `quest-log-detail.mobile.css:52` | color | `.quest-log-detail-main-card .text-muted, .quest-log-detail-main-card small` |
| 24 | `quest-log.mobile.css:33` | color | `.quest-log-item small, .quest-log-item .text-muted` |
| 25 | `quests.css:805` | color | `.quest-log-details .text-muted` |
| 26 | `quests.css:809` | color | `.quest-log-details .text-white` |
| 27 | `quests.mobile.css:26` | color | `.participant-list-mobile small, .participant-list-mobile .text-muted` |
| 28 | `quests.mobile.css:67` | color | `.quest-header-card-mobile small, .quest-header-card-mobile .text-muted` |
| 29 | `shop.css:119` | color | `.item-name.text-light` |
| 30 | `shop.css:468` | color | `.purchase-details .text-muted` |
| 31 | `shop.css:473` | color | `.purchase-details .text-warning` |
| 32 | `shop.css:482` | color | `.purchase-details .text-success` |
| 33 | `shop.css:565` | color | `#denyModal .form-label .text-danger` |
| 34 | `shop.css:629` | color | `#addCharacterModal .form-label .text-danger` |
| 35 | `shop.mobile.css:101` | color | `.shop-item-card-mobile .text-muted, .shop-header-card .text-muted, .shop-empty-state .t...` |
| 36 | `site.css:160` | color | `.item-name.text-light` |
| 37 | `site.css:305` | background-color | `.form-control.bg-dark` |
| 38 | `site.css:311` | background-color | `.form-control.bg-dark:focus` |
| 39 | `site.css:639` | color | `.text-muted` |
| 40 | `site.css:1292` | color | `.purchase-details .text-muted` |
| 41 | `site.css:1297` | color | `.purchase-details .text-warning` |
| 42 | `site.css:1307` | color | `.purchase-item small.text-muted` |
| 43 | `site.css:1395` | color | `.purchase-details .text-success` |
| 44 | `site.css:1584` | color | `.purchase-item.sold-returned .purchase-details .text-warning` |
| 45 | `site.css:1589` | color | `.purchase-item.sold-returned .purchase-details .text-success` |

### (c) Rules that win only because they are unlayered

Found from the computed-style diff (a rule that previously lost on specificity or source order to a Bootstrap rule and now wins because every layered rule loses to every unlayered rule):

1. **`.navbar-nav .nav-link { padding: .4rem .8rem; margin: 0 .25rem }`** (`site.css:545-556`). Before, Bootstrap's `.navbar-expand-lg .navbar-nav .nav-link` (higher specificity) set the horizontal padding to 8px; unlayered, the legacy rule now sets 12.8px. 291 links on 51 captures widen by 9.6px each, and the bar wraps (icon over label, "Quest Log" on two lines) on the 1440px Dungeon Master capture (`$QBR/cascade-probe/desktop/Quest-Manage.png` against `$QBR/cascade-probe/control-sweep/desktop/Quest-Manage.png`; the bar grows from 64px to about 75px).
2. **`p, li, span, small { color: #1a0f08 }`** (`site.css:632-637`). Before, Bootstrap's `.navbar-dark .navbar-text` (0,2,0) beat the bare-element rule on the footer text; now the bare-element rule wins, so the footer text turns `#1a0f08` on the layered `bg-dark` fill: 1.22:1 (A-shell-002 becomes worse). 58 captures.
3. The same mechanism applies to every other bare-element colour rule in `site.css` (8 rules) and `modern-card.css` (7 rules): they now beat any non-important Bootstrap colour on their elements. The computed-style diff shows no further visible instance beyond items 1 and 2 and the `!important` inversions in (d).

### (d) Pages whose rendering changed

All 118 captures differ from `before`. By cause (captures affected; one capture can have several):

| Cause | Captures | Effect |
|-------|----------|--------|
| Bootstrap `.bg-dark` (`background-color: ... !important`, layer vendor) beats the legacy `footer.navbar { background: transparent !important }` and the offcanvas header colour | footer on 116, desktop and mobile bar fill on 118, offcanvas header on 58 | Footer becomes an opaque `#212529` band; bar darker under its 50 percent gradient; mobile offcanvas header loses its brown |
| (c) item 2: bare-element `span` colour beats `.navbar-text` | 58 | footer text `#1a0f08` on `#212529`: 1.22:1 |
| (c) item 1: legacy nav-link padding wins | 58 | nav links wider; the bar wraps on the 1440px Dungeon Master capture |
| Bootstrap `.text-danger` (`#dc3545`, `!important`) beats the legacy `.text-danger` cream and `#ff6b6b` rules | 32 captures plus the Dungeon Master and Admin menu toggles on 28 | validation and refusal lines go from 1.97:1 to 1.21 to 1.93:1 on glass (3.59:1 on paper); the red menu toggles are 1.05 to 2.19:1 on the bar (A-shell-021 is no longer latent) |
| Bootstrap `.text-muted` (`rgba(33,37,41,.75)`, `!important`) beats the 28 legacy `.text-muted` rules | cream to grey on 59 captures, dark to grey on 20 | on glass: 1.57 to 2.24:1 at the three boards (the old cream at 70 percent alpha was 2.99:1 at `B-max`, also failing); on paper 5.97:1 (the old cream-on-paper was 1.00:1) |
| Other utilities win over legacy `!important` rules: `.text-light`, `.text-info`, `.text-white`, `.text-success`, `.text-primary`, `.text-secondary`, `.badge .text-dark`, `.p-2`, `.fs-6` | 8, 4, 1, 1, 1, 1, 6, 4, 4 | colour shifts to Bootstrap values (for example `text-success` `#198754` on glass 1.21:1), card padding 12px to 8px, CR badge 17.6px to 16px |

Pages that need a human look first, by the share of pixels that changed outside the bar and footer (S1 candidates are the muted and danger lines on glass): Account/Profile on mobile (2.73 percent: the empty-state sentence and icon become grey on glass, `$QBR/audit/shell/shots/cascade/mobile-Account-Profile.png`), Calendar/Index on desktop (1.75 percent: the legend hint becomes grey on glass), Events/Index on desktop (1.65 percent: the attendance counts become readable on paper, the legend hint turns grey), Quest Index, Admin Quests, Quest Details and Events Index on mobile (1.3 to 1.5 percent), Quest Log details on desktop (1.05 percent). The foundation must land the variable re-theme for `.text-muted`, `.text-danger` and `.bg-dark` and retire the matching rules in one step per layout; no page may be merged with the layer in place until its before-and-after readability gate passes (D-08).

Per-capture figures follow, widest content change first. Columns: share of differing pixels against `before`; against the control (isolates layering); content only (bar and footer masked); chrome causes (B bar fill, F footer, N nav links wider, D Dungeon Master and Admin toggles red, O offcanvas header) and content causes with the number of elements.

| Page | UA | Differing, vs `before` | vs control | Content only | Chrome causes | Content causes (elements) |
|------|----|-----------------------|-----------|--------------|---------------|---------------------------|
| Account-Profile | mobile | 3.22% | 3.04% | 2.73% | BFO | muted cream>gray x14 |
| Calendar-Index | desktop | 9.13% | 9.13% | 1.75% | BFN | muted cream>gray x63, p-2 padding x8 |
| Calendar-Index--crowded | desktop | 9.13% | 9.13% | 1.75% | BFN | muted cream>gray x63, p-2 padding x8 |
| Events-Index | desktop | 9.02% | 9.02% | 1.65% | BFN | muted cream>gray x68, p-2 padding x8, badge text-dark x4 |
| Quest-Index | mobile | 3.01% | 2.90% | 1.54% | BDFO | muted cream>gray x60 |
| Admin-Quests | mobile | 1.62% | 1.55% | 1.53% | BFO | muted cream>gray x60 |
| Quest-Details | mobile | 1.77% | 1.56% | 1.40% | BDFO | muted cream>gray x61 |
| Events-Index | mobile | 2.96% | 2.85% | 1.29% | BFO | muted cream>gray x14, p-2 padding x8, badge text-dark x4 |
| QuestLog-Details | desktop | 8.53% | 8.53% | 1.05% | BFN | muted cream>gray x14, text-white x10, colour utility x10, fs-6 size x8 |
| ShopManagement-Index | desktop | 10.77% | 10.77% | 0.88% | BDFN | muted cream>gray x60, text-danger x29, muted dark>gray x2, text-primary x1, text-secondary x1 |
| Events-Details | mobile | 1.17% | 0.97% | 0.81% | BFO | muted cream>gray x10 |
| Home-Index | mobile | 1.17% | 0.96% | 0.80% | BFO | muted dark>gray x1 |
| QuestLog-Index | mobile | 1.50% | 0.96% | 0.80% | BDFO | muted cream>gray x60, muted dark>gray x1, colour utility x1 |
| Account-Profile | desktop | 8.92% | 8.92% | 0.79% | BFN | muted cream>gray x14 |
| GroupPicker-Index | mobile | 1.92% | 1.67% | 0.67% | B | muted cream>gray x14 |
| ShopManagement-Edit | mobile | 1.85% | 1.76% | 0.67% | BFO | muted cream>gray x60, text-info x5, colour utility x1 |
| Contacts-Details | mobile | 1.81% | 1.66% | 0.59% | BFO | muted cream>gray x9, text-danger x7 |
| Agenda-Index | mobile | 1.98% | 1.88% | 0.55% | BFO | muted dark>gray x15, muted cream>gray x2 |
| Quest-Create | desktop | 8.23% | 8.23% | 0.53% | BDFN | text-danger x62, muted cream>gray x6 |
| Calendar-Index | mobile | 0.65% | 0.53% | 0.52% | BFO | muted cream>gray x64 |
| Calendar-Index--crowded | mobile | 0.65% | 0.53% | 0.52% | BFO | muted cream>gray x64 |
| Contacts-Details | desktop | 7.95% | 7.95% | 0.50% | BFN | text-danger x33, muted cream>gray x23 |
| Shop-Index--empty-category | mobile | 0.85% | 0.64% | 0.48% | BFO | muted cream>gray x7 |
| Quest-Edit | desktop | 8.61% | 8.61% | 0.44% | BDFN | text-danger x62, muted cream>gray x6 |
| Platform-Group-Edit | mobile | 1.50% | 1.18% | 0.43% | BFO | muted cream>gray x1 |
| DungeonMaster-Profile | desktop | 7.35% | 7.35% | 0.36% | BDFN | muted cream>gray x15, muted dark>gray x2 |
| Platform-Group-Create | mobile | 0.94% | 0.51% | 0.35% | BFO | muted cream>gray x15 |
| Quest-Details--voting | mobile | 0.72% | 0.51% | 0.35% | BDFO | muted cream>gray x60, muted dark>gray x2 |
| Shared-AccessDenied | mobile | 0.69% | 0.49% | 0.34% | BFO | muted cream>gray x14 |
| QuestLog-Details | mobile | 1.81% | 1.68% | 0.32% | BFO | muted cream>gray x3 |
| GroupPicker-Index | desktop | 1.59% | 1.59% | 0.31% | B | muted cream>gray x14 |
| Characters-Details | desktop | 8.47% | 8.47% | 0.31% | BFN | muted cream>gray x15 |
| Quest-CreateFollowUp | desktop | 7.88% | 7.88% | 0.30% | BDFN | text-danger x62, muted cream>gray x20 |
| ShopManagement-Index | mobile | 1.09% | 0.92% | 0.29% | BFO | muted cream>gray x60, muted dark>gray x2 |
| Events-Details | desktop | 8.42% | 8.42% | 0.29% | BFN | muted cream>gray x10 |
| Shop-Index | desktop | 6.53% | 6.53% | 0.29% | BFN | text-light x3 |
| Quest-CreateFollowUp | mobile | 1.80% | 1.70% | 0.26% | BFO | not isolated by the sampled properties |
| Admin-Quests | desktop | 7.93% | 7.93% | 0.26% | BDFN | text-danger x29 |
| ShopManagement-Create | mobile | 1.70% | 1.60% | 0.25% | BFO | text-info x4 |
| Contacts-Create | mobile | 1.59% | 1.45% | 0.25% | BFO | muted cream>gray x15, muted dark>gray x10, text-danger x7 |
| Contacts-Edit | mobile | 1.59% | 1.45% | 0.25% | BFO | muted cream>gray x15, muted dark>gray x10, text-danger x7 |
| Characters-Create | desktop | 6.35% | 6.35% | 0.24% | BFN | text-danger x33, muted cream>gray x17, muted dark>gray x10 |
| Characters-Edit | desktop | 6.34% | 6.34% | 0.24% | BFN | text-danger x33, muted cream>gray x17, muted dark>gray x10 |
| DungeonMaster-EditProfile | desktop | 11.25% | 11.25% | 0.22% | BDFN | text-danger x29, muted cream>gray x17, muted dark>gray x10 |
| Platform-LinkedBoards-Index | desktop | 3.79% | 3.79% | 0.22% | BFN | muted cream>gray x14, text-light x7, badge text-dark x3 |
| Admin-EditUser | desktop | 11.23% | 11.23% | 0.21% | BDFN | text-danger x29, muted cream>gray x14 |
| Contacts-Edit | desktop | 10.17% | 10.17% | 0.20% | BDFN | text-danger x62, muted cream>gray x15, muted dark>gray x10 |
| Characters-Index | desktop | 7.01% | 7.01% | 0.20% | BFN | muted cream>gray x14 |
| Contacts-Create | desktop | 10.16% | 10.16% | 0.19% | BDFN | text-danger x62, muted cream>gray x15, muted dark>gray x10 |
| Platform-Users-Index | mobile | 0.23% | 0.19% | 0.18% | BFO | not isolated by the sampled properties |
| Quest-Manage | desktop | 11.41% | 11.41% | 0.18% | BDFN | text-danger x29, fs-6 size x8, colour utility x5, text-success x1 |
| DungeonMaster-EditProfile | mobile | 0.54% | 0.33% | 0.18% | BFO | muted dark>gray x10, muted cream>gray x2, colour utility x1 |
| Shop-Details | desktop | 6.58% | 6.58% | 0.17% | BFN | muted cream>gray x74, badge text-dark x4, colour utility x1 |
| Platform-Users-Index | desktop | 2.49% | 2.49% | 0.15% | BFN | text-light x7 |
| Platform-Group-Edit | desktop | 8.11% | 8.11% | 0.14% | BFN | text-light x7, muted cream>gray x3 |
| ShopManagement-Edit | desktop | 8.27% | 8.27% | 0.14% | BDFN | text-danger x62, muted cream>gray x60, text-info x4 |
| Quest-Details--voting | desktop | 5.02% | 5.02% | 0.14% | BFN | fs-6 size x8, muted cream>gray x1 |
| Admin-Users | mobile | 0.61% | 0.43% | 0.13% | BFO | not isolated by the sampled properties |
| Shared-AccessDenied | desktop | 8.25% | 8.25% | 0.12% | BFN | muted cream>gray x14 |
| ShopManagement-Create | desktop | 8.46% | 8.46% | 0.12% | BDFN | text-danger x62, text-info x4 |
| Quest-Manage | mobile | 0.49% | 0.28% | 0.12% | BFO | muted cream>gray x2 |
| Characters-Details | mobile | 1.26% | 1.11% | 0.12% | BFO | muted cream>gray x7 |
| QuestLog-EditRecap | desktop | 11.28% | 11.28% | 0.11% | BDFN | text-danger x29, muted cream>gray x15 |
| ContactCategoryManagement-Manage | mobile | 0.46% | 0.25% | 0.09% | BFO | muted cream>gray x60 |
| Platform-Group-Create | desktop | 7.96% | 7.96% | 0.09% | BFN | muted cream>gray x15, text-light x7 |
| Account-Edit | desktop | 8.22% | 8.22% | 0.09% | BFN | muted cream>gray x14 |
| Platform-Group-Index | desktop | 7.93% | 7.93% | 0.07% | BFN | text-light x7, badge text-dark x3 |
| Admin-Users | desktop | 11.32% | 11.32% | 0.06% | BDFN | text-danger x29 |
| Series-Details | mobile | 1.42% | 1.29% | 0.04% | BFO | muted dark>gray x2 |
| Shop-Index--empty-category | desktop | 6.28% | 6.28% | 0.03% | BFN | not isolated by the sampled properties |
| Quest-Details | desktop | 8.22% | 8.22% | 0.03% | BFN | fs-6 size x8, colour utility x2 |
| Platform-Group-Members | desktop | 1.99% | 1.99% | 0.02% | BDFN | text-light x7, badge text-dark x4 |
| Series-Details | desktop | 7.01% | 7.01% | 0.01% | BFN | muted dark>gray x2 |
| Events-Edit | mobile | 0.44% | 0.24% | 0.01% | BFO | text-danger x33 |
| Events-Create | mobile | 0.48% | 0.30% | 0.01% | BFO | text-danger x33, muted cream>gray x2 |
| Platform-Group-Delete | desktop | 7.87% | 7.87% | 0.01% | BFN | text-light x7 |
| Characters-Create | mobile | 1.04% | 0.95% | 0.01% | BFO | muted dark>gray x10, text-danger x7 |
| Characters-Edit | mobile | 0.98% | 0.89% | 0.01% | BFO | muted dark>gray x10, text-danger x7 |
| Admin-ResetPassword | desktop | 11.16% | 11.16% | 0.00% | BDFN | text-danger x29 |
| Events-Create | desktop | 11.24% | 11.24% | 0.00% | BDFN | text-danger x62, muted cream>gray x2 |
| Events-Edit | desktop | 11.24% | 11.24% | 0.00% | BDFN | text-danger x62 |
| Account-ChangePassword | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Account-ChangePassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-Edit | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-ForgotPassword | desktop | 7.51% | 7.51% | 0.00% | BFN | none |
| Account-ForgotPassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-Login | desktop | 7.51% | 7.51% | 0.00% | BFN | none |
| Account-Login | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-CreateUser | desktop | 11.01% | 11.01% | 0.00% | BDFN | text-danger x29 |
| Admin-CreateUser | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-EditUser | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-EmailStats | desktop | 11.25% | 11.25% | 0.00% | BDFN | text-danger x29 |
| Admin-EmailStats | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-ResetPassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Agenda-Index | desktop | 6.37% | 6.37% | 0.00% | BFN | muted dark>gray x20, muted cream>gray x2 |
| Characters-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| ContactCategoryManagement-Edit | desktop | 11.13% | 11.13% | 0.00% | BDFN | text-danger x29 |
| ContactCategoryManagement-Edit | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| ContactCategoryManagement-Manage | desktop | 11.23% | 11.23% | 0.00% | BDFN | text-danger x29 |
| Contacts-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Contacts-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| DungeonMaster-Profile | mobile | 1.74% | 1.49% | 0.00% | BFO | none |
| Home-Index | desktop | 7.50% | 7.50% | 0.00% | BFN | none |
| Move-Index | desktop | 11.25% | 11.25% | 0.00% | BDFN | text-danger x29 |
| Move-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Platform-Group-Delete | mobile | 0.59% | 0.16% | 0.00% | BFO | none |
| Platform-Group-Index | mobile | 1.69% | 1.47% | 0.00% | BFO | none |
| Platform-Group-Members | mobile | 0.05% | 0.00% | 0.00% | BFO | none |
| Platform-LinkedBoards-Index | mobile | 0.28% | 0.12% | 0.00% | BFO | none |
| Players-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Players-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Quest-Create | mobile | 1.52% | 1.39% | 0.00% | BFO | none |
| Quest-Edit | mobile | 1.33% | 1.19% | 0.00% | BFO | none |
| Quest-Index | desktop | 4.06% | 4.06% | 0.00% | BFN | none |
| QuestLog-EditRecap | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| QuestLog-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Shop-Details | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Shop-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |

## Operator questions for sign-off

To be assembled from the seven reports.

## Relayout list

To be assembled from the seven reports.

## Merge proposals

To be assembled from the seven reports.

## Dead dependencies

The dead-dependency proofs (jQuery, the Font Awesome v4 shim, and the other vendor lines) are in [94-AUDIT-vendor.md](94-AUDIT-vendor.md), written by plan 94-13; this index links to it and does not copy it.

## Proposed area-phase cut

To be written after the totals.
