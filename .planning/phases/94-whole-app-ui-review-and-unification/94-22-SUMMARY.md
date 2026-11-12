---
phase: 94-whole-app-ui-review-and-unification
plan: 22
subsystem: ui-foundation
tags: [shell, mobile, offcanvas, touch-targets, backdrop, webp, guard-tests]
requires:
  - phase: 94-21
    provides: "shell.css chrome/chrome-footer/skip-link classes, one body::before backdrop in base.css, --qb-board-photo token seam"
  - phase: 94-20
    provides: "component layer, visually-hidden-focusable helper"
provides:
  - "Both phone layouts and the phone footer on the shell contract: skip link, header, one named nav (the offcanvas), named toggler and close button, main#main-content, no inline style or script"
  - "shell.mobile.css: static 56px chrome bar without blur, offcanvas on the overlay surface, the 44px target rule, tap-sized form-check rows, wrapping footer, 900px photo"
  - "notice-board-900.webp (63,914 bytes) named through --qb-board-photo on :root; the 3 MB JPEG backdrop rule is gone from mobile.css"
  - "UiFoundationMobileShellGuardTests (10 facts)"
affects: [94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Phone shell is keyed on .mobile-layout; the look (wood bar, brand, toggler, footer) comes from shell.css through the same class names the desktop bar wears"
    - "Touch-target floor is one zero-specificity :where() rule, so a page sheet can still size a control it knows better"
key-files:
  created:
    - QuestBoard.Service/wwwroot/images/notice-board-900.webp
    - QuestBoard.IntegrationTests/Tests/UiFoundationMobileShellGuardTests.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Footer.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/shell.mobile.css
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.Service/wwwroot/js/site.js
    - QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs
decisions:
  - "The phone bar is a div inside header, not a nav: the offcanvas body is the one named navigation landmark ('Main menu' / 'Platform'), so there is exactly one nav per layout"
  - "The 900px WebP was created and the JPEG rule retired in the first task's commit, not the third: retiring the JPEG without the replacement would have left phones with no board for a commit"
  - "aria-expanded on the offcanvas togglers is kept in step by a small listener in site.js, because Bootstrap's offcanvas never updates it on the opening button"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 30000
  tasks: 3
  commits: 3
---

# Phase 94 Plan 22: Phone Shell, Platform Mobile Layout, 900px Backdrop Summary

Both phone layouts and the phone footer now follow the shell contract with 44px targets, and every phone page loads the board from one 64 KB 900px WebP through `--qb-board-photo` instead of the 3 MB JPEG.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `c1c48e1b` | `_Layout.Mobile`, `_Footer.Mobile`, `shell.mobile.css`, `mobile.css` retirements, 900px WebP, `site.js` aria-expanded sync, `UiFoundationMobileShellGuardTests`, `MobileCssTests` 44px fact re-pinned |
| 2 | `85b68b36` | `_Layout.Platform.Mobile` on the same shell, added to the guard |
| 3 | `be311bc9` | Menu rows are flex so long unbroken names wrap beside the icon (found by the edge-case run); the edge-case scripts themselves are scratch |

Tracer gate: auto mode is off in `config.json` (`auto_advance` and `_auto_chain_active` both false), but this plan is `autonomous: true` and the orchestrator asked for the full plan, so I followed the same path 94-21 took. The tracer's `<verify>` was re-run end to end before Task 2 (guard filter 308 green, `mobile-shell.mjs` all pass, `shell-m-before` against `shell-m-after` PASS) and it passed. No interactive checkpoint was returned.

## What was built

- **Main phone layout.** Skip link first in `<body>` (`visually-hidden-focusable skip-link`, `#main-content`). `<header>` holds the bar: `navbar navbar-dark chrome` (no `bg-dark`, no `py-2`), brand anchor and ids unchanged. Toggler: `aria-label="Open navigation menu"`, `aria-controls="mobileNav"`, `aria-expanded="false"`. Offcanvas: `aria-labelledby`, close button `aria-label="Close navigation menu"`, the scrolling body is `<nav class="offcanvas-body" aria-label="Main menu">`. `<main id="main-content" tabindex="-1">` keeps its classes and adds `flex-grow-1` (see Deviations). Every menu item, order, icon, label, `BoardPages` condition, the `nav-quests-mobile` id and the Logout form (action, antiforgery, `data-bs-dismiss`) are byte-for-byte as before.
- **Platform phone layout.** Same structure; `aria-controls="platformMobileNav"`, nav named `Platform`, `chrome` bar, items and Logout form unchanged.
- **Footer.** Text `D&amp;D QB v@(AppVersion.Current).` unchanged; `chrome-footer`; GitHub link `aria-label="Quest Board source on GitHub"`, `rel="noopener noreferrer"`, icon `aria-hidden`, 44px target (shell.css).
- **shell.mobile.css** (components layer, no colour literal, no important flag). `:root { --qb-board-photo: url("../images/notice-board-900.webp") }` (linked after shell.css and only by the phone layouts, so it wins by order, not by a media query; the guard also forbids a value on `body`). The bar: blur off (the translucent blur on an always-visible bar), brand a 44px-tall inline-flex link. The A-5 rule exactly as written in the sign-off table. `.btn-close` border-box (Bootstrap's content-box made it 52px). Check rows 44px with the label as the whole-row target. Offcanvas on `--qb-surface-overlay`, wood header, opaque close button, rows 48px (0.75rem padding plus 24px line) with a hairline, parchment text, gold hover, wrapping for free-text names, icons one width so labels line up. Footer text wraps inside its row.
- **mobile.css retired:** the touch-target block, the 3 MB JPEG `body::before`, `.navbar-brand`, `.mobile-layout .navbar`, and all offcanvas rules (`.offcanvas`, `.offcanvas-header.bg-dark`, `.offcanvas-title`, `.offcanvas .nav-link`, hover). Kept for 94-23: body typography (the 16px fact), `.container-fluid` padding, `h1`-`h6`, `.text-muted`, `.move-control-reason`, `.btn-primary`, `.alert-danger`.
- **Guards.** `UiFoundationMobileShellGuardTests`, 10 facts (plan listed 7): the seven named ones plus `MobileLayouts_WearTheShellClasses`, `MobileShell_NamesTheLighterPhotoOnTheRootAndRetiresTheOldBackdrop`, `MobileBackdropImage_IsASmallDerivativeOfTheSourceArtwork`. `MobileCssTests` still has four facts: the 44px fact now reads `shell.mobile.css` (the `:where(` rule with `min-height` and `min-width` from `var(--qb-hit-min)`) and `tokens.css` (`--qb-hit-min: 44px`); the 16px fact and both response facts are unchanged.

## Workarounds for unlayered legacy rules (for 94-23 to remove)

The phone layouts link `mobile.css` and not `site.css`, so none of `site.css`'s `div`/`ul, ol, li`/`p, li, span, small` rules reach them. The only legacy rule that beats a layered shell colour here is the unlayered `h1, h2, h3, h4, h5, h6 { color: ink; text-shadow }` in `mobile.css`.

1. Offcanvas title on both layouts: `<h5 class="offcanvas-title">` became `<div class="offcanvas-title h5">` (text and classes identical, plus `id` for `aria-labelledby`), because the `h1`-`h6` rule would paint it ink on the dark panel. When 94-23 removes that rule the element can stay a div (a title of a dialog is not a page heading).
2. `<span class="navbar-text">` in the phone footer and the Platform phone user name became `<div>`s for parity with the desktop footer. Not forced by a legacy rule on the phone layouts; harmless to keep.

No restatement rule was added to `mobile.css` or `site.css`; nothing for 94-23 to delete from them beyond its own list.

## Verification

- **`dotnet build` and full `dotnet test` in the worktree:** 1300 unit passed, 1748 integration passed, 11 skipped (pre-existing), 0 failed. Task 1 filter 308 green; Task 2 filter 82 green. No CLR flake hit.
- **Readability (sweep-based; `shell-m-before`, `shell-m-after` after Task 1, `shell-m-final` after Task 3; 120 records each, 0 non-200).** `readability-compare shell-m-before shell-m-after`: PASS. `shell-m-before shell-m-final`: PASS. No glass "new failure" appeared, so `probe-lib.mjs` was not needed.
- **`mobile-shell.mjs` (Pixel 7 UA, roles player and admin) ALL PASS:** mobile layout served; bar 56px and `position: relative` (static); no overflow; only the 900px WebP requested; first Tab is the skip link and Enter focuses `#main-content`; the panel opens by keyboard (Enter on the toggler) with `aria-expanded` going false to true to false, focus inside the dialog, Escape closes and focus returns to the button; the close button works by keyboard; every visible interactive element outside page content on `/quests` and every control in the panel is at least 44 by 44.
- **`platform-mobile.mjs` (SuperAdmin, Pixel 7 UA) ALL PASS** on `/Platform/Group`, `/Platform/Users`, `/Platform/LinkedBoards`: body has `mobile-layout`, nav named `Platform`, skip link, targets, `components.css` requested, no console error, no overflow, panel opens with `aria-expanded` true and 49px rows. Confirms a SuperAdmin under a mobile User-Agent really receives the Platform mobile layout.
- **`edge.mjs` ALL PASS** (long names injected into the DOM; no account field changed, so nothing to restore): footer with a long version string at 320px wraps (79px tall, link stays inside at 44 by 44) and without it the footer has no overflow; Admin at 390 by 667 with all 17 items: the body is 865px in a 611px box, scrolls to the end (254px), the last item ends at 651 of 667, the close button stays at 6 to 50px and is the element under its own centre; 60-character spaced and unbroken names at 320px on both phone layouts, panel closed and open: no horizontal scroll, every row inside the panel.
- **`img-check.mjs`:** on seven phone pages (quests, Shop, Calendar, Characters, Admin Users, Platform Group, Account Profile) exactly one board-image request, `200 /images/notice-board-900.webp`, 63,914 bytes; the JPEG and the 1600px file are never requested.
- **Image.** Pillow, same method as the 1600px file: 900x600, quality 78, method 6, 63,914 bytes. 99.9th-percentile luminance source 0.12799, encoded **0.08994** (lower, so B-max contrast can only improve; the contrast guard's lightest-pixel constant is unchanged and passes). The 900px photo takes effect on phone pages now that the shared backdrop rule (from the desktop shell plan) and this token are both merged; the retirement plan has nothing further to verify for the image.
- Port 8022 stopped; 8000 and 8001 untouched; no wait loops running; no migration, no data changed, no audit account field edited.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] 900px WebP and JPEG retirement landed in Task 1, not Task 3**
- **Why:** the orchestrator required the JPEG rule retired in the same change that names the 900px file, and the naming lives in Task 1's `shell.mobile.css`. Task 3 therefore holds the edge-case run and one fix, not the encode.

**2. [Rule 1 - Bug] Plan said mobile.css "keeps its body::before"; it is gone**
- Per the orchestrator's override. `mobile.css` no longer has `body::before` or any `Notice Board` reference; a guard fact pins it.

**3. [Rule 2 - Missing critical] aria-expanded on offcanvas togglers**
- **Issue:** the plan requires `aria-expanded="false"` on the toggler, but Bootstrap's offcanvas never updates it, so it would stay false while the panel is open.
- **Fix:** 13 lines in `site.js` (`show.bs.offcanvas` and `hidden.bs.offcanvas`, only for togglers that already declare `aria-expanded`). Verified true when open, false after Escape. **Commit:** `c1c48e1b`.

**4. [Rule 1 - Bug] Top bar is a div, not a nav**
- The plan says the nav named `Main menu` is the offcanvas's. Keeping the bar a `<nav>` as well would give two unlabelled-or-duplicate navigation landmarks, so the bar is `<div class="navbar ...">` inside `<header>`. The guard asserts exactly one `nav` per layout.

**5. [Scope] `flex-grow-1` on the phone `<main>`**
- The old footer relied on `mt-auto`, which is an important utility and would beat the footer's 32px clearance. `<main>` grows instead, so the footer still sits at the bottom of short pages and keeps its clearance. Classes otherwise kept.

**6. [Scope] The `.btn-close` is `box-sizing: border-box` and the brand link is a 44px-tall inline-flex link** (with a 4px gap, because inline-flex dropped the space between the die and the words). Both are needed to meet 44 by 44 exactly.

**7. [Scope] Page-owned targets under 44px, left to the area phases.** `/quests` on a phone has six `a.row-nav-link` quest titles 27px tall (page sheets, `quests.mobile.css`); `/Platform/Users` has 43 `a.email-link` links 22px tall. They are page content, not shell or the shared control classes, so the plan's "every interactive element on /quests at least 44 by 44" is met for everything the layouts and shared rule own and recorded for the Quests and Platform area phases. Suggested ledger entry (kind `deviation`, files `QuestBoard.Service/wwwroot/css/quests.mobile.css` and `QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml` or its sheet): "bare in-page links (quest title row-nav-link, Platform Users email-link) are under 44px on the phone layout; the A-5 rule lists only controls, so the area phases enlarge their hit area".

## Findings for the orchestrator

1. **The phone layouts never see `site.css`.** WINDOWS 15 and 20 (the bare `div`, `ul, ol, li`, `p, li, span, small` rules) do not reach the phone shell; only `mobile.css`'s `h1`-`h6` rule does (workaround 1 above). 94-23 only has to retire that one on the phone side.
2. **900px photo on tall phones is upscaled about 1.4 times** (900 by 600, `cover` on a portrait screen). The texture is soft but reads as the same board; the spec's "about 900px wide" is met. A taller crop would need a different aspect than the desktop file.
3. **Ledger candidates:** Deviation 7 above (page-owned targets). Nothing else new for WINDOWS.md.

## Known Stubs

None.

## Threat Flags

None new. T-94-42: menu conditions copied unchanged (`BoardPageNavigationTests`, `BoardMoveStaticGuardTests`, `BoardPageStaticGuardTests`, `MobileLayoutTests`, `MobileViewsTests` green; the diff of both layouts shows no change inside any `@if`). T-94-43: `rel="noopener noreferrer"` on the footer link, pinned by `MobileFooter_GitHubLinkIsNamedAndSafe`. T-94-44: user and board names are still Razor-encoded output (`@currentUser.Name`, `@User.Identity?.Name`, active group name); no `Html.Raw` added.

## Self-Check: PASSED

- Files present: `shell.mobile.css`, `mobile.css`, `notice-board-900.webp`, `_Layout.Mobile.cshtml`, `_Layout.Platform.Mobile.cshtml`, `_Footer.Mobile.cshtml`, `site.js`, `UiFoundationMobileShellGuardTests.cs`, `MobileCssTests.cs`.
- Commits present: `c1c48e1b`, `85b68b36`, `be311bc9`.
- STATE.md, ROADMAP.md and WINDOWS.md not modified. Scratch under `~/qb-ui-review/phase-94/plans/94-22/` (`mobile-shell.mjs`, `platform-mobile.mjs`, `edge.mjs`, `img-check.mjs`, `shoot-page.mjs`, `encode.py`, sweep and comparison logs, `shots/`); sweeps `shell-m-before`, `shell-m-after`, `shell-m-final`.
