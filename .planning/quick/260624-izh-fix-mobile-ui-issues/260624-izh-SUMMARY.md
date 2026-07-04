---
phase: quick
plan: 260624-izh
status: complete
subsystem: mobile-ui
tags: [mobile, bugfix, dark-theme, offcanvas, badges, css]
key-files:
  modified:
    - EuphoriaInn.Service/Views/Shared/_Layout.Mobile.cshtml
    - EuphoriaInn.Service/Views/Home/Index.Mobile.cshtml
    - EuphoriaInn.Service/wwwroot/css/home.mobile.css
decisions:
  - Moved Styles section render from body bottom to head end — ensures page-specific CSS loads before body renders, preventing flash of unstyled content
  - Replaced absolute-positioned "Signed up" badge with flex-column badge stack — eliminates z-index and overlap issues, works with any card height
metrics:
  duration: ~5 minutes
  completed: 2026-06-24
---

# Quick Fix 260624-izh: Fix Mobile UI Issues

Fixed 4 confirmed mobile UI bugs across two Razor views and one CSS file — dark theme attribute, offcanvas slide direction, badge overlap, and CSS-in-head ordering.

## Changes Made

### Task 1 — `_Layout.Mobile.cshtml`

1. **Dark theme attribute** — Added `data-bs-theme="dark"` to `<html>` tag. Ensures Bootstrap 5 dark mode is active for all mobile views without relying on manual per-component dark classes.

2. **Offcanvas direction** — Changed `offcanvas-start` to `offcanvas-end`. The nav drawer now slides in from the right, matching the toggler button position in the top-right corner.

3. **Styles section moved to `<head>`** — Moved `@await RenderSectionAsync("Styles", required: false)` from the bottom of `<body>` (after `<script>` tags) to the end of `<head>`. Page-specific CSS (e.g. `home.mobile.css`) now loads before the DOM renders.

### Task 2 — `Index.Mobile.cshtml`

Removed `position-relative` from the quest card div and removed the absolute-positioned "Signed up" badge. Replaced the title row with a two-badge flex-column stack:

- Status badge (Open/Finalized/Done) always shown
- "Signed up" badge shown conditionally below it, right-aligned

Both badges share a `d-flex flex-column align-items-end gap-1` wrapper, eliminating overlap at any card height.

### Task 3 — `home.mobile.css`

- Removed stale comment `/* position: relative required — signed-up badge uses position: absolute inside */`
- Removed `position: relative` from `.quest-card-mobile` rule (no longer needed)
- Added `:hover` state `background-color: #3d444b` between the base color and the `:active` state

## Deviations from Plan

None — all changes executed exactly as specified.

## Build Verification

`dotnet build EuphoriaInn.Service` — 3 projects, 0 errors, 0 warnings.

Note: `--no-restore -q` produced a spurious MSBuild incremental-build error unrelated to these changes. Full build with restore confirmed clean.

## Self-Check: PASSED

- Commit `415535c` exists and contains all 3 modified files
- `_Layout.Mobile.cshtml` — `data-bs-theme="dark"` added, `offcanvas-end` set, Styles section in `<head>`
- `Index.Mobile.cshtml` — `position-relative` removed, badge block restructured
- `home.mobile.css` — stale comment removed, `position: relative` removed, `:hover` state added
