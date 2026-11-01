---
phase: 94-whole-app-ui-review-and-unification
plan: 21
subsystem: ui-foundation
tags: [shell, navbar, footer, skip-link, landmarks, backdrop, webp, group-picker, guard-tests]
requires:
  - phase: 94-20
    provides: "component layer, toasts, utilities layer with the visually-hidden-focusable helper"
  - phase: 94-19
    provides: "Bootstrap in layer(vendor), --bs-* mapping, focus ring"
  - phase: 94-18
    provides: "token layer, shell.css scaffold"
provides:
  - "shell.css: wood navigation bar (.chrome), brand, links, dropdowns, toggler, collapse overlay, fixed-nav offset, skip link, user menu, footer (.chrome-footer)"
  - "The three desktop-side layouts and the desktop footer on one contract: skip link, header, named nav, main#main-content, named toggler, no inline style or script"
  - "One body::before backdrop rule in base.css, clear body fill, 1600px WebP derivative named through --qb-board-photo"
  - "group-picker.css (pages layer): 44px targets and wrapping names under 768px"
  - "UiFoundationDesktopShellGuardTests (12 facts)"
affects: [94-22, 94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "A shell surface keyed on a class (chrome, chrome-footer, shell-fixed-nav), so layouts that link shell.css but do not opt in are untouched"
    - "Where an unlayered legacy reset beats a layered background, the layered rule sets a variable and one small unlayered rule in the legacy sheet paints it"
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/group-picker.css
    - QuestBoard.Service/wwwroot/images/notice-board-1600.webp
    - QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Views/Shared/_Footer.cshtml
    - QuestBoard.Service/wwwroot/css/shell.css
    - QuestBoard.Service/wwwroot/css/base.css
    - QuestBoard.Service/wwwroot/css/site.css
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Main bar collapses below 1200px (navbar-expand-xl) and uses the 14px step with 4px link padding from 1200 to 1399px, so the bar is one 56px row at every width; at 992 and 1200 the legacy bar wrapped to two rows and covered content"
  - "The two dropdown toggles lose text-danger (it rendered parchment already through the colour mapping and blocked the gold hover)"
  - "Spans on shell surfaces became divs (footer text, picker brand, Platform user name) because the legacy p/li/span colour rule is unlayered and beats any layered colour"
  - "The desktop photo is --qb-board-photo on :root, read by --qb-board-image in tokens, instead of body { --qb-board-image } as the plan wrote, so a Shop theme set on html still replaces it"
  - "Body fill made transparent in base.css: the white Bootstrap body fill had been covering the backdrop on every page since the html fallback colour landed"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 11000
  tasks: 3
  commits: 3
---

# Phase 94 Plan 21: Desktop Shell, GroupPicker and Platform Layouts, Backdrop Summary

The three desktop-side layouts and the desktop footer now follow one shell contract (skip link, landmarks, named toggler, no inline code) with the signed-off wood chrome drawn by `shell.css`, and the board photo is drawn by one rule in `base.css` from a 163 KB WebP. Read Findings first: the backdrop task found that desktop pages had been hiding the board behind a white body fill.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `c922fba3` | main layout and footer on the contract, shell.css, legacy navbar and footer rules out of site.css, desktop shell guard |
| 2 | `ce181c33` | GroupPicker and Platform layouts, group-picker.css, both added to the guards |
| 3 | `6028ca21` | backdrop rule in base.css, WebP derivative, clear body fill, backdrop guard facts |

Tracer gate: autonomous run, so the tracer's checks were re-run end to end before Task 2 (guard filter 327 green, skip-link keyboard check, 992/1200 captures, `shell-d-after` sweep). The shared comparison flagged only the two layouts that were not converted yet (see Deviations 7); after Task 2 the cumulative comparison is clean.

## What was built

- **Layouts.** All three: skip link first in `<body>` (`visually-hidden-focusable skip-link`, href `#main-content`), `<header>`, `<nav aria-label>` ("Main" on the main and picker layouts, "Platform" on Platform), `<main id="main-content" tabindex="-1" role="main">`, nav is `chrome fixed-top` (no `bg-dark`, no `py-2`), body gets `shell-fixed-nav`. Main layout toggler: `aria-label="Toggle navigation" aria-controls="navbar" aria-expanded="false"`. Platform gained a real collapse (`#platformNavbar`) with the same named toggler and the `Styles` section; its three entries and Logout are unchanged and last in the bar. The picker layout got `group-picker` on body and links `group-picker.css` after the legacy sheets and before Styles. Menu items, order, icons, labels, `BoardPages` conditions, ids, brand anchors and every Logout form (action, antiforgery, position within the bar) are as before. The user menu's inline width is the class `user-menu`; none of the three layouts or the footer has a `style` attribute, style block or inline script.
- **Footer.** Text byte-identical. Link is `aria-label="Quest Board source on GitHub"`, `target="_blank" rel="noopener noreferrer"`, icon `aria-hidden`, a 44px target in gold on hover. Footer class `chrome-footer` (same wood as the bar, 32px clear of the content, no longer flush against it on short pages).
- **shell.css.** `.chrome`: wood gradient, parchment text (5.84 at the lightest stop, gold hover 5.09 per the sign-off), hairline edge, shadow, 12px blur, exactly `--qb-nav-h` tall (padding is what is left over the 44px toggler, minus the 1px edge), so body padding and `html:has(.shell-fixed-nav) { scroll-padding-top }` (nav height plus 8px) are exact. Brand: Cinzel 600 at `--qb-text-lg`, gold die, gold hover. Links: weight 400, never wrap, dark halo (not the legacy white one). Dropdowns open on the opaque overlay surface with parchment text. Toggler: 44px, 2px parchment border, bars drawn as a mask over a parchment token. Collapsed menu: an overlay panel under the bar, per layout breakpoint. `.user-menu`, `.skip-link`, `.chrome-footer`, `.chrome-footer-link`.
- **Backdrop.** `body::before` in `base.css`: fixed, `inset: 0`, `100vh` then `100dvh`, vignette (50% at the corners) then transparent veil then the photo (S7: strong texture, no veil). `--qb-board-photo` on `:root` in `shell.css` names `../images/notice-board-1600.webp`; `tokens.css` `--qb-board-image: var(--qb-board-photo, none)`. Verified live on four desktop pages: exactly one image request, the 1600px WebP, 200, 163,178 bytes; the 3 MB JPEG is not requested.
- **WebP.** Pillow 12.1.1 (already present, nothing installed; numpy 2.3.5), LANCZOS to 1600x1067, quality 78, method 6: **163,178 bytes**. 99.9th-percentile luminance: source 0.1280, encoded **0.1055** (lower, so B-max contrast can only improve; the contrast guard's lightest-pixel constant is unchanged and its tests pass). Per-channel 99.9th percentile source (140, 90, 43), encoded (128, 82, 53). `Notice Board-blank2.jpg` stays as the source; `Notice Board-blank1.jpg` untouched.
- **group-picker.css (`@layer pages`).** Under 768px: buttons at least `--qb-hit-min`, cards at least that tall and within the screen, long names wrap.
- **Guards.** `UiFoundationDesktopShellGuardTests`: `DesktopLayouts_StartBodyWithSkipLink`, `DesktopLayouts_HaveLandmarks`, `DesktopLayout_NamesTheToggler` (also asserts the controlled id exists, and that the picker has none), `DesktopLayouts_CarryNoInlineStyleOrScript` (layouts and footer), `DesktopFooter_GitHubLinkIsNamedAndSafe`, `DesktopFooter_TextUnchanged`, plus `DesktopLayouts_WearTheShellClasses`, `DesktopFooter_WearsTheChromeClass`, `GroupPickerLayout_LinksItsOwnSheetAfterTheLegacyOnes`, `PlatformLayout_RendersTheStylesSection`, `Backdrop_IsOneRuleInBaseAndTheDesktopPhotoIsNamedByTheShell`, `BackdropImage_IsASmallDerivativeOfTheSourceArtwork`. `group-picker.css` is in the layered-file guards (opens its layer, no important flag, no colour literal, no planning reference).

## Deleted selectors from site.css

Removed with the shell rules that replace them: `.navbar:not(footer)`, `footer.navbar`, `footer.navbar .navbar-text` (the footer text colour stopgap from the previous plan), `body { padding-top: 56px }`, `.navbar-brand`, `.navbar-brand:hover`, `.navbar-nav .nav-link`, the `@media (min-width: 992px) { .navbar-expand-lg .navbar-nav .nav-link }` rule (the nav-link spacing stopgap from the previous plan), `.navbar-nav .nav-link:hover`, `.navbar-nav .nav-link:focus`, `.navbar-toggler`, `.navbar-toggler:focus`, `.navbar-toggler-icon`, and the `body::before` backdrop rule with its comment. Added to site.css: one rule, `.chrome .navbar-collapse, .chrome .dropdown-menu { background-color: var(--qb-menu-fill, transparent) }`, next to the resets it works around (Findings 1). `div { background: transparent; }` and the Quest Log exception are untouched.

## Verification

- **Guards and suite.** `dotnet build` 0 errors (CLR flake hit twice, passed on the single rerun each time; the tests that ran against a stale binary after a failed build were rerun and are not counted). Full `dotnet test` in the worktree: **1300 unit, 1738 integration passed, 11 skipped (pre-existing), 0 failed**. The plan's filters: Task 1 set 327 green, Task 2 set 146 green, `UiFoundation` 82 green.
- **Skip link (`plans/94-21/skip-check.mjs`).** First Tab on `/quests`: "Skip to main content", fixed at 8,8, 198 by 40, visible with the gold-and-dark ring; Enter moves focus to `main#main-content`, which shows no ring of its own. Forty links on the page focused programmatically: none ends under the bar.
- **Bar at every width (`nav-check.mjs`, roles player, dm, admin, superadmin).** Height 56px and body padding 56px at 1440, 1366, 1280, 1200, 1199, 992 and 800; no horizontal overflow. Natural width of the fullest menu (SuperAdmin, 8 items plus Calendar and name) is about 1326px at body size, so a one-row bar cannot hold it from 992: at 992 and 1200 the legacy bar wrapped to a 77 and 101px bar over the 56px offset. Below 1200 the menu collapses (toggler `aria-expanded` flips, panel opens as a dark overlay below the bar, shots at 800 and 390 in `shots/menu-collapsed-*.png`); 1200 to 1399 uses the small step. Captures in `~/qb-ui-review/phase-94/plans/94-21/shots/`.
- **`shell-checks.mjs` (port 8021): ALL 17 CHECKS PASSED.** `/groups/pick` as the SuperAdmin under a mobile User-Agent at 390px (a one-board member is redirected past the picker, so the Player cannot see it): served by the picker layout, no overflow, 9 visible interactive elements all at least 44 by 44. A synthetic 60-character name saved on the audit Player through Account Edit and the longest name from the Players page (18 characters), at 992, 1200 and 1280: no overflow, user dropdown inside the viewport, bar one row at 1200 and up. A 60-character board name placed in the dropdown's group entry wraps inside the menu. Platform layout at 992 (real identity, the synthetic name, the longest real name): no overflow, every bar item inside the viewport, bar 56px. The Player's original name (12 characters) was restored by the script and the log says so. Run twice; the failures of the first runs (the Player is redirected past the picker; Platform bar wrapped at 66px with a 60-character name) led to Deviations 5 and 6.
- **Readability comparison (sweeps on port 8021, 120 records each, 0 non-200).** `shell-d-before`, `shell-d-after` (Task 1), `shell-d-backdrop` (all tasks). `readability-compare shell-d-after shell-d-backdrop`: **PASS**. `readability-compare shell-d-before shell-d-backdrop` (cumulative): **PASS**. The Task 1 comparison `shell-d-before shell-d-after` flagged 8 rows, all the two layouts not yet converted (picker `span.navbar-brand` ink on wood, six Platform pages new overflow at 320 because the legacy bar rules were gone); both cleared in Task 2. No new console error, page error or failed request. Probe contrast failure records: 2270 before, 1897 after Task 1, 1895 after Task 3. I did not need `probe-lib.mjs`: nothing flagged was a glass failure.
- **Backdrop request check (`img-check.mjs`).** Player `/quests`, DM `/Calendar`, SuperAdmin `/groups/pick` and `/Platform/Group`: one request for `notice-board-1600.webp`, 200, 163,178 bytes; `::before` is fixed, 100dvh (900px at 900 high), z-index -1; body fill transparent.
- Port 8021 stopped, nothing of mine running (8000 and 8001 untouched). No migration, no data left changed (the audit Player's name is back to its original). Worktree has no untracked files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The white body fill hid the backdrop on every desktop page**
- **Found during:** Task 1 screenshots (white page behind the quest posters) and confirmed against the base commit's sweep.
- **Issue:** since the html fallback colour landed, Bootstrap's body fill no longer propagates to the canvas, so it paints over the `z-index: -1` backdrop. Only the plan's backdrop task could fix it.
- **Fix:** `body { background-color: transparent }` in `base.css` (not `--bs-body-bg`, which modals and cards read). The phone layouts link `base.css` too, so their pages now show the board as well (Findings 3).
- **Commit:** `6028ca21`

**2. [Rule 3 - Blocking] Unlayered legacy rules beat layered shell colours and backgrounds**
- **Found during:** Task 1 screenshots of the open menus.
- **Issue:** `ul, ol, li { background: none }` and `div { background: transparent }` removed the dropdown and collapse fills; `p, li, span, small { color; text-shadow; font-weight }` made the toggler bars, footer text and picker brand ink-coloured and put a white glow on link text. A layered rule cannot beat an unlayered one without an important flag.
- **Fix:** fills go through a variable (`--qb-menu-fill`) that one unlayered rule in `site.css` paints; link text sets its own `text-shadow`; the toggler bars are filled from the parchment token; spans on shell surfaces became divs (footer text, picker brand, Platform user name; the text and classes are unchanged).
- **Files modified:** `shell.css`, `site.css`, the three layouts, `_Footer.cshtml`. **Commit:** `c922fba3`, `ce181c33`

**3. [Rule 1 - Bug] The legacy bar wrapped and covered content at 992 and 1200**
- **Fix:** `navbar-expand-xl` on the main bar, compact link step from 1200 to 1399, links never wrap, the signed-in name is clipped with an ellipsis and is the one item allowed to give way. Platform keeps `navbar-expand-lg`. **Commit:** `c922fba3`, `ce181c33`

**4. [Scope] `tokens.css` edited (not on the plan's list)**
- One line: `--qb-board-image: var(--qb-board-photo, none)`. The plan wrote `body { --qb-board-image: url(...) }` in `shell.css`, which would override a Shop theme set on `<html>` (a declaration on body beats an inherited value), and a components-layer declaration on `:root` would beat the tokens-layer Shop block. The photo is one step behind the semantic token, so a theme still replaces the whole name. **Commit:** `6028ca21`

**5. [Rule 3] Picker check uses the SuperAdmin**
- A one-board Player is redirected past `/groups/pick`; `shell-checks.mjs` loads the picker as the SuperAdmin. The plan said Player for `/quests`, which is unchanged.

**6. [Rule 1 - Bug] Platform bar wrapped to 66px with a 60-character name**
- Fixed with the clipped `.navbar-text`, a shrinkable item and `min-width: 0` on the collapse from 992. **Commit:** `ce181c33`

**7. [Scope] Tracer verify between tasks.** The plan's Task 1 verify includes the readability comparison, which flagged the two layouts that Task 2 converts (the legacy bar rules they relied on left in the same commit as the layout that replaces them, as the plan's scope note says). The Task 1 commit therefore leaves the picker and Platform bars unstyled until Task 2; the comparison passes cumulatively.

**8. [Scope] The footer line changed.** The acceptance check `git diff -U0 _Footer.cshtml | grep -c 'Created by Theun Schut'` is 2, not 0: the line changed from `<span>` to `<div>` only (Deviation 2). `DesktopFooter_TextUnchanged` pins the text.

**9. [Scope] `text-danger` removed from the Admin and Dungeon Master toggles.** It computed parchment already (the colour mapping from the previous plans) and an important utility colour cannot take a hover colour. No test or script depended on it.

**10. [Scope] `role="main"` kept** on `<main>` as the plan said (the audit would drop it).

## Findings for the orchestrator

1. **WINDOWS 15 (the bare `div` rule) is wider than a div.** `ul, ol, li { background: none }` beats layered surfaces on lists the same way (dropdown menus), and `p, li, span, small { color; text-shadow; font-weight: 500 }` beats layered colours on spans and puts a white halo on any list item's text. Both retire with the legacy globals; `site.css` carries one restatement rule (`.chrome .navbar-collapse, .chrome .dropdown-menu`) to delete with them, and the divs I swapped for spans can stay. Suggested ledger entry: kind `deviation`, file `QuestBoard.Service/wwwroot/css/site.css`, "ul/ol/li background reset and p/li/span colour rule defeat layered shell surfaces; menu fill restated from --qb-menu-fill".
2. **For 94-22 and the Shop phase: name the photo through `--qb-board-photo`.** Set `--qb-board-photo` (not `body { --qb-board-image }`) to the 900px WebP in `shell.mobile.css` on `:root`, and the Shop's `data-theme` on `<html>` will replace the photo with its gradient as designed. Shop on `<body>` would not recolour the html fallback.
3. **Phone pages now show the board.** `base.css` is linked on the phone layouts, so the transparent body fill (Deviation 1) removed the white behind the phone cards: the phone pages now show the board from `mobile.css`'s own 3 MB JPEG rule, which 94-22 replaces. The comparison passes (the glass cards read better). Worth a look at the phone Quest board capture (`shell-d-before` against `shell-d-backdrop`, mobile) when the phone shell is signed off.
4. **Legacy `.btn` rule still sets weight 600, 2px border and a text shadow** on the Logout buttons in the picker and Platform bars; left for the card/button retirement.
5. **The picker cards are still `div onclick`** (not keyboard reachable); in the picker view, not the layout, as the audit says.
6. **Dropdown contrast** (menu items on the overlay surface) was checked by eye, not by the sweep, which never opens a menu: parchment on `#2f1b0e` measures 12.98 (the overlay figure from the previous plan), the My Agenda icon is the event light text variant on it.

## Known Stubs

None.

## Threat Flags

None new. T-94-38: `rel="noopener noreferrer"` on the footer link, pinned by `DesktopFooter_GitHubLinkIsNamedAndSafe`. T-94-39: Razor encoding kept for the user name, no `Html.Raw` introduced. T-94-40: menu conditions copied unchanged (`LayoutNavigationTests`, `BoardPage*` tests, `BoardMoveStaticGuardTests` green). T-94-41: Logout forms with antiforgery kept as is (the Platform form sits inside the new collapse but is still the last entry).

## Self-Check: PASSED

- Files present: `shell.css`, `base.css`, `site.css`, `tokens.css`, `group-picker.css`, `notice-board-1600.webp`, three layouts, `_Footer.cshtml`, `UiFoundationDesktopShellGuardTests.cs`, `UiFoundationLayerGuardTests.cs`.
- Commits present: `c922fba3`, `ce181c33`, `6028ca21`.
- STATE.md, ROADMAP.md and WINDOWS.md not modified. Scratch under `~/qb-ui-review/phase-94/plans/94-21/` (`shell-checks.mjs`, `skip-check.mjs`, `nav-check.mjs`, `menu-shots.mjs`, `img-check.mjs`, `encode.py`, comparison and test logs, `shots/`); sweeps `shell-d-before`, `shell-d-after`, `shell-d-backdrop`.
