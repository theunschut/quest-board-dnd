---
phase: 94-whole-app-ui-review-and-unification
plan: 11
subsystem: ui-foundation
tags: [vendor, self-hosting, bootstrap, font-awesome, cinzel, release-smoke-test, static-guard]
requires:
  - phase: 94-02
    provides: "gate.mjs / gate-compare.py zero-difference gate, run-app.sh, role storage states"
provides:
  - "Bootstrap 5.3.0, Font Awesome 6.4.0 and Cinzel served same-origin from versioned wwwroot/lib directories on all five layouts"
  - "wwwroot/css/app.css: Cinzel @font-face (latin and latin-ext, font-display swap), first stylesheet on every layout"
  - "UiFoundationVendorGuardTests: static guard over layouts, lib references and licences"
  - "Release smoke test asserts the zip carries every committed lib file and the packaged app serves every layout-referenced /lib URL"
affects: [94-12, 94-13]
tech-stack:
  added: ["bootstrap 5.3.0 (vendored)", "font-awesome-free 6.4.0 (vendored)", "Cinzel v26 variable woff2 (vendored)"]
  patterns: ["versioned vendor directory busts caches (no asp-append-version on vendor files)", "static source-scan guard test with host strings built from fragments"]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/app.css
    - QuestBoard.Service/wwwroot/lib/bootstrap-5.3.0/ (css, js, LICENSE)
    - QuestBoard.Service/wwwroot/lib/fontawesome-6.4.0/ (css, three woff2, LICENSE.txt)
    - QuestBoard.Service/wwwroot/lib/cinzel/ (latin and latin-ext woff2, OFL.txt)
    - QuestBoard.IntegrationTests/Tests/UiFoundationVendorGuardTests.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
    - build/tests/package-release-smoke-test.sh
decisions:
  - "Cinzel files are the ones Google serves a desktop Chrome UA (as the plan specifies); Android UAs get a differently hinted build of the same font, so a single file cannot be pixel-identical on both gate User-Agents"
  - "Vendor files carry no SRI on the layouts today, so each is verified by recorded SHA-256 and source URL (and, for Font Awesome, equality with the npm package) rather than SHA-384"
  - "Dockerfile, .dockerignore and the csproj needed no change: dotnet publish carries wwwroot/lib and nothing excludes it"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 3600
  tasks: 3
  commits: 3
---

# Phase 94 Plan 11: Vendor Self-Hosting Summary

Bootstrap 5.3.0, Font Awesome 6.4.0 and Cinzel now load from versioned `wwwroot/lib` directories on all five layouts, with `app.css` first on every layout, no request to a Google host, a static guard test, and a release smoke test that fails when a vendor file is missing from the zip or not served. `actuals.tokens` counts the hand-written files only (guard test, smoke test, app.css, layout lines), not the 11 byte-for-byte vendor files.

## What changed

- Task 1 (commit bfea9340): the Bootstrap CSS and bundle JS plus `LICENSE` copied byte-for-byte from the exact jsDelivr URLs the layouts used (`bootstrap@5.3.0`); the five layouts point at `~/lib/bootstrap-5.3.0/...`. `UiFoundationVendorGuardTests` added with `Layouts_LoadBootstrapFromAVersionedLibDirectory`, `LibReferences_ResolveToCommittedFiles`, `VendorDirectories_ShipTheirLicence`.
- Task 2 (commit 0a5604ac): Font Awesome `all.min.css`, the solid, regular and brands woff2 and `LICENSE.txt`; the Cinzel latin and latin-ext variable woff2 and `OFL.txt`; `app.css` with the two `@font-face` rules (`font-weight: 400 700`, `font-display: swap`, Google's exact `unicode-range` values); `app.css` linked first with `asp-append-version`; Font Awesome link switched; Google Fonts link deleted; four more guard facts (`Layouts_LoadFontAwesomeFromAVersionedLibDirectory`, `Layouts_RequestNoWebFontFromGoogle`, `AppCss_IsTheFirstStylesheetOnEveryLayout`, `AppCss_DeclaresCinzelLocallyWithSwap`). The v4-shim link and the jQuery tag are untouched.
- Task 3 (commit b723879a): the smoke test loops over `git ls-files QuestBoard.Service/wwwroot/lib` asserting each is a zip entry (plus an at-least-one assertion), and requests every `~/lib/...` URL from the five layouts against the packaged app expecting 200.

### Vendor provenance (also in the commit messages)

| File (under `wwwroot/lib/`) | Source URL | SHA-256 |
|---|---|---|
| bootstrap-5.3.0/css/bootstrap.min.css | https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css | 7f1d37f0d90b6385354c2ac10e2bb91563c46bd7a266ed351222ebcac8496c2a |
| bootstrap-5.3.0/js/bootstrap.bundle.min.js | https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js | aa53d582f97eb594c2a5cc5824574707f9ba9837bce3046bfa5f3556860f4e04 |
| bootstrap-5.3.0/LICENSE | https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/LICENSE | 3d0b0c88216e4752b9afd3d24e36faaf27873b5a45a61458bb3c83e794c26832 |
| fontawesome-6.4.0/css/all.min.css | https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css | 1edb1725a9ea8ca4dcf2f5508cee183218aa1685e47c1b23056717f754f58ebf |
| fontawesome-6.4.0/webfonts/fa-solid-900.woff2 | .../font-awesome/6.4.0/webfonts/fa-solid-900.woff2 | 7152a6933ee3d690ec2af3d09da9d701723d16aa3410a6d80f28ff8866f3b880 |
| fontawesome-6.4.0/webfonts/fa-regular-400.woff2 | .../font-awesome/6.4.0/webfonts/fa-regular-400.woff2 | 8e7e5ea1b15f62ab14dbd41768e8fbcd21cc859a4ea5da812457ee714299fb35 |
| fontawesome-6.4.0/webfonts/fa-brands-400.woff2 | .../font-awesome/6.4.0/webfonts/fa-brands-400.woff2 | 748332090c4b8e20f95d0ff59f0be20fa9c889359d3b36d4b886d73376054207 |
| fontawesome-6.4.0/LICENSE.txt | https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.4.0/LICENSE.txt | 0aa8f86525273b2efa4f40f4272a188e187704252170e979dc06879adf68d43c |
| cinzel/cinzel-latin-wght.woff2 | https://fonts.gstatic.com/s/cinzel/v26/8vIJ7ww63mVu7gt79mT7.woff2 | 09941fb1c169c38fd414536b37690057fc01b3117a5c63dd6571186540c8f370 |
| cinzel/cinzel-latin-ext-wght.woff2 | https://fonts.gstatic.com/s/cinzel/v26/8vIJ7ww63mVu7gt7-GT7LEc.woff2 | 8fc2606f8ae6c569f444e138dba052d9a6e3f0657f9031796503bbb94eced600 |
| cinzel/OFL.txt | https://raw.githubusercontent.com/google/fonts/main/ofl/cinzel/OFL.txt | f2b3029aba64c378bf0963b62945eee15e564fe4330b934c8f2eb058282b5e83 |

The Font Awesome `all.min.css` and `fa-solid-900.woff2` hashes were cross-checked against the `@fortawesome/fontawesome-free@6.4.0` npm package through jsDelivr and match.

## Verification results

- Task 1 gate: `c1-before` against `c1-after-bs` PASS, 14 captures, 0 differing pixels; the report shows Bootstrap requested only from `localhost:8011/lib/bootstrap-5.3.0/...`.
- Task 2 gate: `c1-before` against `c1-after`: all 7 desktop captures 0 differing pixels; the 7 mobile captures do NOT reach zero (see Deviations 1). No new console error, page error, failed request or status change on any capture. The only failed request in both before and after is the pre-existing 404 for a cropped character picture on quest details. No request goes to a Google host, to `bootstrap@` or to the Font Awesome `all.min.css` CDN URL; the remaining third-party requests are the v4 shim, jQuery and EasyMDE (css and js), which later plans remove. No `fa-v4compatibility` or other webfont request outside the committed set occurred.
- Filtered tests (guard, Mobile CSS, Mobile layout, Platform layout, board-move and board-page static guards): 147 passed. Full `dotnet build`: 0 errors. Full `dotnet test`: unit 1300 passed; integration 1663 passed, 11 skipped (pre-existing skips, SQL Server-dependent migrator tests), 0 failed.
- Smoke test: exit 0, 30 PASS lines, 0 FAIL; log at `~/qb-ui-review/phase-94/plans/94-11/smoke-test.log` has a PASS for each of the 11 committed lib files in the zip, `zip carries vendor files`, and a PASS GET 200 for all three layout-referenced lib URLs.
- Docker path: scratch `dotnet publish QuestBoard.Service -c Release` (exit 0) holds `wwwroot/lib/bootstrap-5.3.0` (css and js), `fontawesome-6.4.0` (css and webfonts) and `cinzel` (both woff2 and OFL.txt); `.dockerignore` has no matching pattern; Dockerfile and `.dockerignore` unchanged.
- `git check-ignore` on the vendored Bootstrap CSS exits 1 (not ignored).
- Port 8011 app stopped; nothing left listening on it. Ports 8000 and 8001 untouched.

## Deviations from Plan

**1. [Gate result - needs a decision] Mobile gate captures are not pixel-identical on Cinzel glyph edges**
- **Found during:** Task 2 gate (`c1-after`).
- **Issue:** gate-compare prints FAIL: on the 7 mobile captures 4,479 to 9,505 pixels differ at tolerance 24 inside the Cinzel headings (the nav brand, and on the calendar some more headings); desktop is exactly 0. Cause, proven by experiment: Google serves a different build of the same Cinzel variable font to an Android UA (`...mT7PkRXMw.woff2`, no `prep` hinting table, different bytes) than to a desktop UA (`...mT7.woff2`, hinted). The mobile BEFORE captures used the Android build and the desktop BEFORE captures the desktop build. With the desktop pair committed, desktop is exact and mobile edges differ; with the Android pair swapped in (tested then reverted), mobile is exact (0 on all 7) and desktop differs (1,761 to 55,122 pixels). One local file cannot match both.
- **Judgement:** I compared before and after crops of the nav brand and the calendar header at 2x and at 6x zoom: the difference is sub-pixel anti-aliasing and edge placement (about a third of a CSS pixel; max channel delta 213 on edge pixels only), with identical glyph shapes, weight, widths (the brand box is 198 by 46 CSS px in both) and layout. I do not consider it a visible difference, so I kept the plan's desktop-UA pair and did not revert. This is a human-look call the plan allows ("any non-zero page gets a human look"), but the plan's automated verify (`gate-compare` exit 0) does NOT pass as written. If a strict zero is required, the cheap option is to declare both builds and pick by `@media (hover: none) and (pointer: coarse)` (two more woff2 files, about 40 KB), which I did not do because it adds files outside the plan's list and a device-class heuristic that has no relation to which layout the server picks.
- **Files:** `QuestBoard.Service/wwwroot/lib/cinzel/*.woff2`, `QuestBoard.Service/wwwroot/css/app.css`.
- **Commit:** 0a5604ac.
- **Ledger:** I did not add a `.planning/WINDOWS.md` entry because that shared file would conflict with the other parallel executors; the orchestrator may want to record it.

### Other differences

- The Task 1 tracer feedback gate: `auto_advance` is false (interactive), which normally means a human checkpoint after the tracer. The tracer's verify is fully automated, passed (tests 143 passed, gate PASS 14 captures), and I re-ran nothing beyond it before expanding because the orchestrator asked for the full plan; no checkpoint was returned.
- The smoke-test addition also asserts a "layouts reference vendor files" count line, so the GET loop cannot pass vacuously.
- Quality-gate note: the plan's "a visible difference blocks the change" applies only to the judgement above.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. Threat register status: T-94-15 (tampering) mitigated by exact-URL downloads with recorded SHA-256 and npm cross-check; T-94-16 mitigated for Bootstrap, Font Awesome and Cinzel (the v4 shim, jQuery and EasyMDE remain for later plans); T-94-17 mitigated by the smoke test; T-94-18 mitigated, the gate report shows no Google host contacted.

## Self-Check: PASSED

- Files exist: `QuestBoard.Service/wwwroot/lib/{bootstrap-5.3.0,fontawesome-6.4.0,cinzel}`, `QuestBoard.Service/wwwroot/css/app.css`, `QuestBoard.IntegrationTests/Tests/UiFoundationVendorGuardTests.cs`.
- Commits exist: bfea9340, 0a5604ac, b723879a.
- Scratch outputs exist under `~/qb-ui-review/phase-94/`: `gates/{c1-before,c1-after-bs,c1-after}`, `plans/94-11/{smoke-test.log,publish,publish.log}`.
