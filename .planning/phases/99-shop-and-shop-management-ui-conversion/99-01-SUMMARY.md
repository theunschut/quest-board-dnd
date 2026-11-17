---
phase: 99-shop-and-shop-management-ui-conversion
plan: 01
subsystem: review-environment
tags: [ui-review, playwright, environment, email-suppression]
requires: []
provides:
  - scratch review environment under ~/qb-ui-review/phase-99 (outside the repository)
  - tools re-pointed to ports 8320/8321 with forced email suppression
  - proven desktop and mobile capture of /Shop
affects: [99-02, 99-03, all later Phase 99 plans]
tech-stack:
  added: []
  patterns: [tools copied from the previous phase and re-pointed; node_modules symlink instead of an install]
key-files:
  created: []
  modified: []
decisions:
  - "Requirement IDs for this phase are the CONTEXT decisions D-01..D-16 and the Definition of converted items DoC-1..DoC-10, because REQUIREMENTS.md has no Phase 99 IDs; both gates are local packages with no review branch or ref (D-11)"
  - "The API-coverage checkpoint does not apply (no external API; the D&D Beyond reference is an outbound link only); the schema gate found no schema file; the assumption-delta detector fired one 'chosen' cue on the theme being a per-page value, and 99-04 records the promote decision for the html data-theme seam"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 99 Plan 01: Review environment and operator checkpoint Summary

Phase 99 review environment built under `~/qb-ui-review/phase-99/` (Phase 98 tooling re-pointed to ports 8320/8321, email suppression forced and verified on the running process), with `/Shop` captured under both desktop and Pixel 7 User-Agents. No repository source file changed; the only commit is this summary with STATE.md and ROADMAP.md.

## Task 1: operator checkpoint (answered by the operator)

- Operator resume message: "sql up, ports free, p99 fixtures approved, migrator: yes (copy phase-98 env)". SQL Server was brought up by the operator (the executor never started or provisioned it).
- Confirmed fixtures (D-14): accounts qb-audit-p99player, qb-audit-p99dm, qb-audit-p99empty at @example.invalid; boards "P99 Audit Shop" and "P99 Audit Empty"; p99empty is the second Dungeon Master. Gold note given: the app has no gold or balance, Purchase debits nothing, so none is needed.
- Post-resume checks run by the executor, all passed: nothing on 8320/8321, 1433 accepts a connection (`ok`), `appsettings.json` clean, branch `milestone/v9-rolling-improvements`.
- Recorded in `operator-answers.json`: `migratorSuite: yes`, `migratorEnv: copied`, `fixturesApproved: true`, `secondDm: p99empty`. `migrator.env` copied from phase-98 with mode 600 (never printed).

## Task 2: scratch root, tooling and tracer capture

- Copied the 182 regular files of the Phase 98 tools (excluding `node_modules`, the `*.bak` files and `publish-review.sh`), rewrote `phase-98` to `phase-99`, 8310 to 8320 and 8311 to 8321 in every text file; the grep for leftovers is empty. `tools/node_modules` symlinks to the Phase 94 Playwright 1.64.1; nothing installed.
- `run-app.mjs` already forced `EmailSettings__SuppressSending=true`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=1025` after any `--env` pairs, kept `ASPNETCORE_ENVIRONMENT=Development`, refused unsafe overrides and verified the listener's `/proc/<pid>/environ` after `/health`; no change was needed. Checked: `--env EmailSettings__SuppressSending=false` exits 1 and starts nothing; after the start, the 8320 listener's environment holds both `EmailSettings__SuppressSending=true` and `EmailSettings__SmtpServer=127.0.0.1`; a second `run-app.mjs` on the same tree exits 0 without starting a second process.
- Capture tools: `audit-shoot.mjs`, `sweep.mjs` and `mockup-shoot.mjs` each abort `/feeds/calendar/` and dismiss native dialogs without recording the text (no change needed).
- `credentials.json` holds only the superadmin and admin entries (mode 600, never printed).
- Tracer: `/Shop` as `admin` on the Audit Board on 8320: the final URL is `/Shop` (the Audit Board has its Shop page switched on), mobile 200 with `mobileLayout=true`, desktop 200 with `mobileLayout=false`, no overflow, no console errors; one PNG and `report.json` each under `tracer/desktop` and `tracer/mobile`. Tracer gate: verified end to end, expansion may proceed.
- The 8320 app (started by this toolset) is left running for the next plan.

## Task 3: shared-file facts, environment note, roles

Full facts with line numbers are in `~/qb-ui-review/phase-99/shared-files.md`.

- `_Layout.cshtml`: global list app, tokens, base, components, shell, v4-shims, legacy-pages (24), shop (25); `<html lang="en">` has no theme attribute.
- `_Layout.Mobile.cshtml`: app, tokens, base, components, shell, shell.mobile, v4-shims, mobile (lines 14-21); links neither legacy-pages.css nor shop.css; `<html lang="en">` has no theme attribute.
- `UiFoundationLayerGuardTests.cs`: `LayeredFiles` (lines 54-100) and `NoLiteralFiles` (lines 103-104) end at the players sheets; no shop sheet is listed. The legacy-pages.css pin is `LegacyDesktopSheet_IsUnlayeredAndRenamed` (318-332: exists, no `@layer`, no site.css).
- Other legacy-pages.css pins: `UiFoundationDesktopShellGuardTests` (picker sheet follows it, no `body::before`), `QuestsConversionGuardTests` (no quest board/vote/date-option/quest-log rules), `AccountPagesRenderTests` (no `calendar-subscription`/`profile-`), `MobileCssTests` (desktop UA links it), `MobileViewsTests` line 882 (picker sheet ordering).
- `MobileViewsTests.cs` shop tests located: `MobileShopIndex_..._RendersItemGridAndFilterButton` 1056-1070 (`Filter &amp; Sort`, `shop.mobile.css`), `..._OmitsPurchaseHistoryPanel` 1078-1091, `GetMobilePage_ShopManagementIndex_` 1524-1538, `_ShopManagementCreate_` 1550-1564, `_ShopManagementEdit_` 1576-1592, `_ShopDetails_` 1604-1622 (classes `shop-mgmt-index-card-mobile`, `shop-mgmt-create-card-mobile`, `shop-mgmt-edit-card-mobile`, `shop-details-card-mobile` and their `.mobile.css` sheets).
- `ShopControllerIntegrationTests.cs`: `class="item-card"` count of 12 (line 284), `aria-label="Shop page navigation"` (336), `page-item active` (349).
- `components.css` defines no `.badge-rarity` or `.rarity-` rule; the five `.rarity-*` chip rules exist in `legacy-pages.css` (131-151), `shop.css` (134-154) and `shop.mobile.css` (122-138), so the plan-set assumption P3 holds and an unlayered legacy copy exists.
- HEAD `04e8fee7`; both `887e5dcd` (shop item creation fix) and `c781090c` (v6.0.2 card surface) are ancestors of HEAD.
- Roles checked (`roles-check.json`, all pass, status 200): superadmin `/platform/Group`, admin `/Admin/Users`, admin `/Admin/CreateUser` with its form.
- `ENVIRONMENT.md` restates the phase contract (ports 8320/8321, forced email overrides and Mailpit rule, database and data rules, purchase and 24-hour rule with no gold, capture, git and local-only gates).

## Deviations from Plan

None - plan executed exactly as written. (The roles-check helper used the `stateFile` export of `login.mjs` under a local alias; this is a scratch-script detail only.)

## Known Stubs

None. No repository file was created or changed.

## Self-Check: PASSED

Scratch records present (`ENVIRONMENT.md`, `shared-files.md`, `roles-check.json`, `operator-answers.json`, `credentials.json` mode 600, `migrator.env` mode 600, `tracer/desktop`, `tracer/mobile`); repository source trees unchanged (`git status --porcelain` on the three source projects prints nothing).
