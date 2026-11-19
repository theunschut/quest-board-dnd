---
phase: 100-admin-and-platform-ui-conversion
plan: 20
subsystem: ui
tags: [razor, admin, email-stats, stat-tiles, layered-css, render-tests, platform-layouts, probes]
requires:
  - phase: 100-07
    provides: the Development-only Resend base address seam, CannedResendHandler, EmailStatsWebApplicationFactory, EmailStatsRenderTests
  - phase: 100-12
    provides: Gate 1 answers (tiles accepted as drawn, Refresh renamed Refresh Email Stats)
  - phase: 100-13
    provides: area guard lists, admin-pages.css
  - phase: 100-19
    provides: the last converted Platform pair, PendingViews down to EmailStats alone
provides:
  - Admin/EmailStats on the shared look, tiles as siblings, measured on a running page
  - A scratch Resend stub server and a per-tile measurement probe
  - A seven-page, both-User-Agent proof of the two Platform layouts (no layout change was needed)
affects: [100-21]
tech-stack:
  added: []
  patterns: [a page whose data comes from a third party is measured by pointing a Development host at a scratch local server through the one setting that host honours; a text sitting straight on a glass card takes the card colour rather than the muted token]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Admin/EmailStats.cshtml
    - QuestBoard.Service/wwwroot/css/admin-pages.css
    - QuestBoard.IntegrationTests/Tests/EmailStatsRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
key-decisions:
  - "O-6 answered get: Refresh stays a link to ?force=true, so AdminController.EmailStats is untouched"
  - "O-13 dev-setting: the capture ran the app in Development (run-app.mjs) with a stub key and EmailSettings__ResendBaseUrl pointing at a scratch stub on 127.0.0.1; nothing else honours the setting, appsettings is untouched"
  - "Every Platform layout probe passed on the first run, so the two Platform layouts, shell.css, shell.mobile.css and both shell guard tests are not touched"
  - "Group/Delete #board-delete-help (4.25:1) NOT fixed: this plan owns no sheet that Group/Delete links"
requirements-completed: [D-05, D-10, D-15, D-17, D-19, D-21, DoC-1, DoC-5, DoC-7, DoC-10]
duration: ~1h15m
completed: 2026-11-18
status: complete
actuals:
  tokens: 4400
  tasks: 3
  commits: 2
---

# Phase 100 Plan 20: EmailStats and Platform layouts Summary

**The one page whose figures were never seen is now measured, not read: four sibling tiles clear 9.87:1 on the rendered page, and the Platform shell is proven on all seven Platform pages under both User-Agents without a single layout change.**

## What was built
- **Email Stats:** one card, one `h1` (`fa-envelope-open-text`, no colour utility) in the card header. The key-missing and API-error alerts keep today's words and `role="alert"`, icons now `aria-hidden`. The statistics branch is four `div.stat-tile` siblings in a `.stat-tiles` grid directly inside the card body (140px auto-fit, 16px gap, no media query), each on the opaque overlay surface with a 1px glass border, 8px radius, 16px padding, no blur, no shadow, no lift; icon, a 28px body-face 600 figure in the tile's status text token, and a 14px muted label, read "figure label". The "Stats as of" line goes through `Html.LocalTime(..., "date-time")`. Refresh is the page's one `btn-primary`, a link to `?force=true` (O-6 get), labelled "Refresh Email Stats" (Gate 1 item 20). `admin-pages.css` gained the tile rules, `.stat-asof` and `.alert code`; the view links it from `@section Styles`.
- **Render facts (`EmailStatsRenderTests`):** four new facts and the tile-reading helper rewritten: one h1 in the header, four tiles as siblings with the four status classes in order and no nested `modern-card`, no Bootstrap status colour or `fs-1`/`fw-bold`; a zero count renders `0` and the grid stays; Refresh is one link (no form) with the new label and is the only `btn-primary`; the as-of line is a `time.local-time` through the helper; both alerts carry `role="alert"` and the refresh link stays when there is nothing to show.
- **Guards:** `Admin/EmailStats.cshtml` moved from PendingViews to ConvertedViews (PendingViews is now empty), got its `ViewSheetLinks` entry and a `PhaseSourceFiles` entry; a new sheet fact pins the grid rule, the tile fill token, the `.alert code` rule and the view's lack of status colour utilities.
- **Tools (scratch root, outside the repository):** `tools/resend-stub.mjs` (node `http` on 127.0.0.1 port 8349, the emails list with pagination, modes `figures`, `small`, `error`), `tools/tiles-100.mjs` (per-tile contrast, grid, reflow and text-spacing probe), `tools/layouts-100.mjs` (the seven-page layout probe), two new afterOnly manifest rows (`admin-emailstats-stats`, `admin-emailstats-error`; manifest.json regenerated, 32 rows).

## How the tiles were measured
Seam: **dev-setting**. 8340 ran through `run-app.mjs` (Development, email suppression forced on; the listener's `/proc` environment showed `ASPNETCORE_ENVIRONMENT=Development`) with a stub key and `EmailSettings__ResendBaseUrl=http://127.0.0.1:8349/`. The stub returned 1299 canned records over 13 pages (12 in transit, 1284 delivered counting opened and clicked, 3 bounced, 0 failed), so the pagination path and a zero tile were both exercised. No real key and no real Resend call; `appsettings.json` and `appsettings.Development.json` have no diff since the plan start. The stub and the stub environment were removed afterwards (8340 restarted without them; no process listens on 8349).

| Tile | Figure on the tile fill | Label (14px muted) | Icon |
|------|-------------------------|--------------------|------|
| In Transit | 10.88:1 | 11.79:1 | 10.88:1 |
| Delivered | 9.87:1 | 11.79:1 | 9.87:1 |
| Bounced | 10.02:1 | 11.79:1 | 10.02:1 |
| Failed | 10.05:1 | 11.79:1 | 10.05:1 |

Measured at 1440, 390 and 320 under the desktop User-Agent and at 412, 390 and 320 under the Pixel 7 User-Agent (`after/100-20/tiles.json`): 4 columns at 1440, 2 at 390 and 412, 1 at 320; tile padding 16px, gap 16px, fill fully opaque, no shadow, no backdrop filter, no nested card; no horizontal scroll anywhere, including under the 1.4.12 text-spacing stress (line height 1.5, letter spacing 0.12em, word spacing 0.16em, paragraph spacing 2em): no clipped tile at any width. The sweep of the stats row and the API-error row (the stub answering 500) recorded contrast 0, small targets 0, missing focus rings 0, overflow none, no console or page errors on 10 captures. No contrast exception is needed.

## Platform layouts (seven pages, both User-Agents)
`after/100-20/layouts.json`: 231 records, 7 pages, 0 failing. Per page: layout served as expected, no horizontal scroll, foundation sheets in order with page sheets after them, nav contrast, and the reach, toggler and landmark items below.

| Item | Result |
|------|--------|
| No horizontal scroll | desktop UA at 1440 and 320; Pixel 7 UA at 412, 390 and 320: all pass on all seven pages |
| User name and Logout through the collapsed toggler | desktop UA at 320 (collapse) and Pixel 7 UA at 412, 390, 320 (offcanvas): both visible, inside the viewport, not covered; at 1440 both visible in the bar |
| Nav contrast | brand, user name and nav link 5.84:1 on the chrome's top gradient stop over the lightest board pixel (brand also against the bottom stop); phone offcanvas text 12.98:1 on its own fill |
| Same foundation sheets | `app`, `tokens`, `base`, `components`, `shell` in that order on both layouts, `shell.mobile` added on the phone, page sheets after them (arrive through `@section Styles`) |
| Landmarks and names | skip link first in the DOM and moving focus to `main#main-content`; one `header`, one named `nav`, one `main#main-content`, one `footer`; toggler named "Toggle navigation" or "Open navigation menu"; offcanvas close named "Close navigation menu" |
| Phone toggler size | 58 by 44 px on every page and width (the audit's 56 by 40 is gone) |

Layout change: **none**. Every probe passed, so `_Layout.Platform.cshtml`, `_Layout.Platform.Mobile.cshtml`, `shell.css`, `shell.mobile.css` and both shell guard tests are untouched.

## Verification
- Unit 1636 passed. Integration 3030 passed, 17 skipped (3025 before; 5 new facts: four in `EmailStatsRenderTests`, one sheet fact), 0 failed. The 17 skipped are the same as before.
- Sweep of the missing-key row against BEFORE (`admin-emailstats`, desktop 1440 and 320, phone 412 and 390): 4 compared, 0 failing with an allow file that holds the one rename (Refresh to Refresh Email Stats); the BEFORE failure on `code` (3.63:1) is closed.
- `AdminControllerIntegrationTests.EmailStats_WhenSuperAdmin_ShouldSucceed` answers 302 when run in a narrow filter (known order dependence from the earlier plan); it passes in the full suite.

## Deviations from Plan
- **[Rule 1 - Bug] `.stat-asof` colour:** the drawing's muted colour on the as-of line measured 4.25:1 over the lightest board pixel on the glass body. The line keeps the card colour (the same fix earlier plans applied to empty-state sentences); the tile labels, which sit on the opaque tile fill, keep the muted token (11.79:1).
- **[Rule 1 - Bug] `code` in the key-missing alert:** Bootstrap's pink `code` colour was 3.63:1 on the alert panel (a pre-existing failure, same BEFORE, but this plan owns the page and no exception is allowed). The page contract names `--qb-ink` for it, which would be dark text on the dark panel; `.alert code { color: inherit }` in `admin-pages.css` takes the alert's light text instead (measured 0 contrast failures afterwards). It applies to every page that links the sheet; none of the others has a `code` in an alert.
- **`admin-emailstats` row run without the stub:** the plan sweeps both rows with the stub running, but the existing row asserts the key-missing state, which a stub key defeats. It was swept after 8340 was restarted without the stub environment and compared with BEFORE; the stats and API-error rows ran with the stub.
- **No controller change:** O-6 answered get, so `AdminController.cs` is untouched (listed in the plan only for the post answer).
- **Tasks 2 changed no repository file:** the layout probes all passed, so the second commit holds only the two follow-up fixes found while measuring; the probes, the stub, the manifest rows and the results live in the scratch root.
- **Open item (Group/Delete `#board-delete-help`, 4.25:1):** not fixed; Group/Delete links only the board deletion sheets and this plan owns none of them.

## Known Stubs
None.

## Threat Flags
None. T-100-67 holds (stub key only, canned records, stub on 127.0.0.1 and stopped, appsettings diff empty); T-100-68 has no surface (O-6 get: no POST refresh); T-100-69 holds (no layout or shell file changed).

## Self-Check: PASSED
- Commits 4d55fbdb and a8909582 exist; `layouts.json` holds a pass for every record on all seven pages; no process listens on the stub port.
