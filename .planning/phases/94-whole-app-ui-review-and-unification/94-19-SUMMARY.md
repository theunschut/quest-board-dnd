---
phase: 94-whole-app-ui-review-and-unification
plan: 19
subsystem: ui-foundation
tags: [bootstrap, font-awesome, css-layers, vendor-bump, bs-variables, focus-ring, reduced-motion, type-scale, guard-tests]
requires:
  - phase: 94-18
    provides: "token layer, base/components/shell scaffolds linked on all five layouts, WCAG helper"
  - phase: 94-13
    provides: "layer order statement at the top of app.css; self-hosted v4 shim"
provides:
  - "Bootstrap 5.3.8 and Font Awesome 6.7.2 (registry latest 5.3.x / 6.x) in version-named lib directories, old directories removed"
  - "Bootstrap and Font Awesome imported into layer(vendor); layouts preload both plus the Cinzel latin file"
  - "Bootstrap re-themed through --bs-* variables in base.css and components.css, with the legacy cascade fights neutralised in the same commit"
  - "two-tone focus ring, reduced-motion block, body type (own commit) and the heading/strong type scale (separate commit)"
  - "UiFoundationBaseGuardTests (10 facts) and a channel-triple guard in the layer guard"
affects: [94-20, 94-21, 94-22, 94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: ["bootstrap 5.3.8 (vendored)", "font-awesome-free 6.7.2 (vendored)"]
  patterns:
    - "Re-theme a vendor library by setting its own variables per surface; never out-specify it"
    - "Page default is the board (light text variants); paper surfaces and fills opt back to ink and fill colours"
key-files:
  created:
    - QuestBoard.Service/wwwroot/lib/bootstrap-5.3.8/
    - QuestBoard.Service/wwwroot/lib/fontawesome-6.7.2/
    - QuestBoard.IntegrationTests/Tests/UiFoundationBaseGuardTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/app.css
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.Service/wwwroot/css/base.css
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/site.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/UiFoundationVendorGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Page default is the board: --bs-secondary-color and the status colours map to their light text variants at :root; paper surfaces (tables in cards, agenda rows, notes) and bg-* elements map back to ink and fill colours. The plan listed the per-surface rule on .modern-card; inverting it needs about 8 selectors instead of about 25 and matches where the design ends up"
  - "The v4 compatibility stylesheet stays a direct link in the two editor layouts (unlayered): importing it from app.css would break the 'exactly two imports' rule and load it on layouts with no editor. Version bumped with Font Awesome to the 6.7.2 package copy"
  - "Heading and strong type scale landed as its own commit (separate from the focus ring and reduced motion) because it changes large-text classification on unconverted pages; see Findings"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 30000
  tasks: 3
  commits: 4
---

# Phase 94 Plan 19: Vendor Bump, Layering and Base Layer Summary

Bootstrap 5.3.8 and Font Awesome 6.7.2 now sit in `layer(vendor)` under the foundation, Bootstrap is re-themed through its own variables so the legacy important rules that lost to it leave no unreadable text, and the base layer carries the focus ring, reduced-motion path and (in a separate commit) the heading scale. Task 3's heading and `strong` mapping flags 132 headings in the readability comparison; read Findings first.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `8418adb5` | bump Bootstrap 5.3.0 to 5.3.8 and Font Awesome 6.4.0 to 6.7.2, old directories removed |
| 2 | `824c0dfa` | layer both under the foundation, preload, re-theme via `--bs-*`, guards |
| 3a | `b6d87b91` | two-tone focus ring, reduced-motion block, body type, guards |
| 3b | `21c68026` | heading scale, `b`/`strong` weight, display face on h1 and the brand, guard |

Tracer gate: the bump's verify (guard tests, readability comparison, differences file, directory count checks) passed end to end before Task 2 started.

## Task 1: the bump

`npm view` reported `bootstrap` latest 5.3.8 and `@fortawesome/fontawesome-free` latest 6.x 6.7.2, the expected versions. `npm pack --ignore-scripts` of both into a scratch folder, no install.

| File | SHA-256 | Matches the research table |
|------|---------|----------------------------|
| bootstrap-5.3.8.tgz | `8b02d4a7482e0f0a2b1da58f1c9c86830e96cc2f77f094f89e4681b19886bc38` | (tarball, not listed) |
| fortawesome-fontawesome-free-6.7.2.tgz | `794284b4ac7f3e30825bbeea1a073f542c45dff83f8146625f600f1015c9d52b` | (tarball, not listed) |
| bootstrap.min.css | `d85327d99c7a3ee1f9b5d0500d1370acea3ad2db39c163c2f51f232baedbdede` | yes |
| bootstrap.bundle.min.js | `e4fd49181388c48ec5040bd3fe66f57c29c8e67fcd8502b3354b96ec7ab47cc7` | yes |
| fontawesome all.min.css | `74005d7c17d4a02f2f25404ec0655d9bc2fdaa53166874c87d7b7eec69d9088a` | yes |
| fa-solid-900.woff2 | `aa75998623a391e61c6901794ace832e3ecdd288b56d608f21bea0411acc0b8e` | yes |
| fa-regular-400.woff2 | `e3456d1283b9d75337a773dfd147bf908fd02c01b4bf48576d8603a69b13cbe5` | yes |
| fa-brands-400.woff2 | `d7236a19bf23cbb2027280e8f51dc99d6c45976a2ed60de73382b034b18a2b68` | yes |
| v4-shims.min.css (same package) | `6650a50d8591259400ed356b424ff4fe633bc0d1fc7be58e17c78fbb1312a707` | (not listed) |
| LICENSE (Bootstrap) / LICENSE.txt (Font Awesome) | `4620c84a...` / `9b914eae...` | (licences, not listed) |

- **Pixel check.** `bump-before` and `bump-after` gate captures (7 default pages plus the six editor and form routes, both User-Agents, 26 captures): every capture differs, and the only cause is the redrawn `fa-dice-d20` glyph (the brand die in the bar, the die in every Quest CR badge, the One-Shot badge die on Platform Group). 16 differing pixels per desktop capture for the brand die, up to 3,138 on the Quest board where there are 14 badges. No new console error, page error, failed request or status change. Written up with overlays in `~/qb-ui-review/phase-94/plans/94-19/bump-differences.md`.
- **Shim.** The 6.7.2 shim keeps `.fa.fa-eye,.fa.fa-eye-slash{font-family:"Font Awesome 6 Free";font-weight:400}`. Proof the EasyMDE toolbar is unchanged: the computed icon weights on `/Quest/Create` are 900 for the six solid buttons and 400 for the preview eye (desktop and phone), and a 2x crop of the toolbar before the bump and after the bump plus layering is visually identical (`cmp-toolbar-desktop.png`).
- `readability-compare bump-before bump-after`: PASS. Sweeps are 120 records each, 0 non-200.

## Task 2: layering and the re-theme

- `app.css` imports both stylesheets with `@import url(...) layer(vendor)` right after the layer statement; every layout drops the two vendor links and preloads Bootstrap, Font Awesome and `cinzel-latin-wght.woff2` (same URLs as the imports, no `asp-append-version`, font preload with `as="font" type="font/woff2" crossorigin`).
- **Cascade neutralisation, each item of the audit list**, checked with `layer-check.mjs` (computed styles) and the sweep:
  - bar and footer fill: `.navbar.bg-dark { --bs-bg-opacity: 0 }`; footer computed background is transparent, bar height 64px.
  - menu toggles: `.navbar .nav-link` maps the status and light colours to parchment; both toggles compute `rgb(244, 228, 188)`.
  - phone menu header: `--bs-dark-rgb` wood at opacity 0.6 reproduces the legacy `rgba(139, 69, 19, 0.6)` exactly (opened and measured).
  - muted text, danger, success, warning, info, primary, secondary: page default in `base.css`, paper and fill overrides in `components.css`.
  - nav links widening and wrapping: no variable controls it (the unlayered rule now outranks Bootstrap's), so `site.css` states Bootstrap's wide-screen spacing explicitly. Computed padding is 8px, nothing wraps at 1440px, and the bar is 64px (it was about 75px in the probe).
  - footer text: the bare `span` colour rule now beat `.navbar-text` and turned the text dark; `site.css` sets it to the on-board token. It measures cream where it was 55% white.
- `tokens.css` gained the channel triples Bootstrap reads (`--qb-*-rgb`, text and fill) and `--bs-link-hover-color-rgb`. A new layer-guard fact keeps each `--qb-NAME-rgb` in step with `--qb-NAME` for the default and Shop themes.
- **Readability.** `layer-before` is the `bump-after` sweep (same code). The first pass with the plan's literal per-surface rule produced 22 flagged rows; moving the default to the board and adding paper and fill overrides, plus one rule the audit list missed (see Deviations 2), brought the shared comparison to 7 flagged rows. All seven are the colour-keyed probe artifact (Findings 2): each was already failing before and measures higher now (2.35 to 2.89, 2.99 to 3.94, 3.45 to 3.94, 1.05 to 3.11, 2.23 to 3.52). The corrected comparison (`compare2.py`) reports 0 genuinely new failures; 10 pre-existing failures measure lower on the board model (listed under Findings 3).
- Smoke test: 40 PASS, 0 FAIL, "All checks passed" (log `plans/94-19/smoke-test.log`, transcribed because the sandbox refuses the redirect form).
- Guards: `UiFoundationBaseGuardTests` (`AppCss_ImportsVendorIntoVendorLayer`, `Layouts_PreloadVendorWithoutVersionQuery`, `Layouts_LinkNoVendorStylesheetDirectly`, `BaseCss_MapsBootstrapVariables`, plus `ComponentsCss_MapsBootstrapVariablesPerSurface` and `Bootstrap_ReadsTheVariablesTheMappingSets`, which fails if a release stops reading the variables the mapping sets), the vendor guard's two vendor facts now assert the import and the bundle script, a new fact requires exactly one `bootstrap-5.3.x` and one `fontawesome-6.x` directory, and the layer guard has the channel-triple fact.

## Task 3: base layer

- **3a (`b6d87b91`).** Focus ring: one `:where(...)`-list rule for the 14 plan selectors with an inner 2px gold band (box-shadow spread equal to the offset, so it fills the gap) and a 2px dark outer stroke, 2px offset; the same declarations on `.form-control:focus`, `.form-select:focus`, `.form-check-input:focus`, `.btn-close:focus` and `.page-link:focus`, where Bootstrap draws a blue shadow and no outline; `.btn` gets `--bs-btn-focus-box-shadow` set to the inner band. Frames on chrome, glass (button and link), a paper table link and a form input are in `plans/94-19/focus/` (`sheet.png`); computed on all five: outline `2px solid rgb(26, 15, 8)`, offset 2px, gold band, `:focus-visible` true. Reduced motion: durations 0.01ms, one iteration, no smooth scroll, no hover or press movement on interactive elements, card-like and list-row elements and what moves inside them (`!important` only inside the block). Verified in Chromium with reduced motion emulated: the quest card, shop item card, bar link and brand all compute `transform: none` on hover (they scale or lift without it), transitions compute 1e-05s, the two infinite Shop animations drop to zero infinite (one 0.01ms pass).
- Body font, size, weight and line height come from the type tokens (same values Bootstrap had, no visible change).
- Readability, 3a: `readability-compare layer-after3 base-ring2` PASS, `compare2.py` 0 new, 0 worse.
- **3b (`21c68026`).** `h1` display step, `.mobile-layout h1` heading step, `h2`/`h3` heading step, `h4` to `h6` body step at 600, `b`/`strong` 600, `h1` and the brand in the display face. Guard `BaseCss_MapsHeadingScale` (also pins that only the two weight tokens are declared).
- Guard facts for 3a: `BaseCss_DeclaresTwoToneFocusRingForInteractiveSelectors` (verified to fail when `.row-nav-link` is removed from the list), `BaseCss_RemovesNoOutlineWithoutReplacement` (with a self-test that its scan fires on a known-bad sample), `BaseCss_HasReducedMotionBlock` (also forbids the word `infinite` in `base.css`).

## Verification

- `dotnet build`: 0 errors. Full `dotnet test`: 1300 unit and 1713 integration tests pass (11 skipped, pre-existing); no CLR flake this run. Filtered set from the plan (`UiFoundation`, MobileCss, MobileLayout, PlatformAreaLayout, MobileViews, BoardMoveStaticGuard, BoardPageStaticGuard): 250 pass after Task 2.
- Port 8019 stopped, nothing of mine running; ports 8000 and 8001 untouched; no data written, no migration.
- Entry 8 (shared probe, multi-layer glass): not the cause of anything flagged here. I did not need `probe-lib.mjs` because every flagged row is explained by a different, reproducible mechanism (Findings 1 and 2), checked against the raw report records.

## Findings for the orchestrator (decisions needed first)

1. **The heading and `strong` mapping flags 132 headings in the shared comparison (commit `21c68026`).** All are cream headings in cards on pages whose card surfaces are not converted yet: `h2.mb-0` page titles (50), phone `h5` page titles, `h4` section headers, `h3`, plus 4 `strong` item names on the phone Shop Management page. At the lightest board pixel with today's unscrimmed 5% glass they measure 3.91 and 4.34 (the `strong` 3.38). At their old sizes (32px, 24px, 20px at 700) they counted as large text and passed the 3:1 bar; at the spec's 20px/16px at 600 they are normal text and need 4.5:1. **The ratios did not change; the bar did.** The UI-SPEC treats 20px at 600 as normal text on purpose, and the scrim the components plan adds lifts cream over the 4.5 line. I kept the spec mapping rather than leave headings at their Bootstrap sizes, and split it into its own commit so you can choose: keep it (the visible effect is page titles dropping from 32px to 20px and losing no contrast, until the card plan lands), or revert `21c68026` alone and re-land it with the card surfaces. `b6d87b91` and everything before it pass the comparison on their own. Suggested ledger entry: kind `unmet-truth`, file `QuestBoard.Service/wwwroot/css/base.css`, "heading scale flags 132 cream headings at 3.91 to 4.34 (large-text bar becomes 4.5) until card surfaces get the scrim".
2. **`readability-compare.py` mis-keys colour-only failures.** For a failing element with no text sample (an icon, a glyph) the key is font size plus colour, so a failure that already existed and only changed colour reads as new. It flagged 7 rows here that were failing before and are better now (table in Task 2). A key without the colour (what `compare2.py` in `plans/94-19/t/` does) is the fix; the same colour-keying also makes a bar change look new, which is correct for Finding 1 but wrong when the ratio is below both bars. Suggested ledger entry for the shared tool (kind `unmet-truth` or a note next to entry 8).
3. **Ten pre-existing failures measure lower, not new.** The status colours switch to their light text variants, which were scored over a scrim that does not exist on the unconverted glass; against the lightest board pixel `span.text-danger` asterisks on the phone forms go 4.34 to 3.36, `Quest/Manage` success text 4.34 to 3.3, `ShopManagement` info heading 4.34 to 3.31 (desktop). They were failing 4.5 already. Expected to resolve with the scrim.
4. **Legacy blue form focus shadow survives inside cards.** `modern-card.css:165` sets `box-shadow` with `!important` on `.modern-card .form-control:focus`, which beats the layered ring's gold band; the dark outer stroke still draws (visible in `paper-input.png`). The card plan retires that rule.
5. **Audit list miss.** The cascade audit's list of legacy rules that re-colour a Bootstrap utility only matches selectors that name the utility. `events-overview.mobile.css` states white for `.avail-card .avail-card-meta` with `!important` on an element that carries `text-muted`; it lost to Bootstrap and was found by the sweep, not the list. Fixed with `.avail-card { --bs-secondary-color }`. Other such rules may exist where no page was swept in the failing state.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The plan's literal surface mapping produced 22 flagged rows; page default inverted**
- **Found during:** Task 2 first sweep
- **Issue:** `--bs-secondary-color` at the root as dark and light text only on `.modern-card` broke every phone card surface (the phone cards are not `.modern-card`), and bare-board muted text was dark brown on dark wood.
- **Fix:** board default at `:root` (`--bs-secondary-color: var(--qb-fg-muted)` and light status text variants), paper and fill overrides in `components.css`. `--qb-fg-muted` is what the plan calls the board-muted token, so the root mapping matches the plan; only the per-surface section differs.
- **Files modified:** `base.css`, `components.css`, `tokens.css`
- **Commit:** `824c0dfa`

**2. [Rule 3 - Blocking] Legacy rules edited so the layering could land**
- **Found during:** Task 2
- **Issue:** two cascade effects have no variable to set: the unlayered `.navbar-nav .nav-link` padding now beats Bootstrap's wide-screen spacing (links widen, the bar wraps), and the bare `span` colour rule beats `.navbar-text` (footer text turns dark).
- **Fix:** two small rules in `site.css` (a media-query spacing rule and a footer text colour rule from the token), a file not on the plan's list. Both are removed with the legacy navbar rules when the shell plan retires them.
- **Commit:** `824c0dfa`

**3. [Rule 3] The compatibility stylesheet stays a direct link**
- The plan says no layout links a vendor stylesheet directly and `app.css` has exactly two imports; the Font Awesome v4 shim cannot join both (94-13 proved dropping it changes the EasyMDE eye icon). It stays a `<link>` in the two layouts that host an editor, from the versioned local directory, bumped to the 6.7.2 package copy. `Layouts_LinkNoVendorStylesheetDirectly` exempts exactly that file.

**4. [Scope] Readability inputs.** Sweeps (`sweep.mjs --port 8019`) fed `readability-compare.py`, as the orchestrator noted; `layer-before` is a symlink to the `bump-after` sweep (identical code), and the layering iterated as `layer-after`, `layer-after2`, `layer-after3` (the last is the committed state).

**5. [Scope] Two extra guard facts and one extra file.** `ComponentsCss_MapsBootstrapVariablesPerSurface` and `Bootstrap_ReadsTheVariablesTheMappingSets` (so the mapping cannot silently become dead declarations after a vendor bump) and a channel-triple fact in `UiFoundationLayerGuardTests.cs`, which is not on the plan's file list.

## Visible changes beyond the type scale (token-driven, all readable)

Bootstrap's own tokens now apply: radii (6px to 8px on controls and buttons), muted text, `bg-dark` fills and `bg-primary` badges take the token colours (the "You" and "Player" badges, the current-user row on phone Quest Details), and `.p-2`/`.fs-6`-style Bootstrap utilities that legacy important rules used to override keep their Bootstrap values (the audit's card padding 12px to 8px and CR badge 17.6px to 16px; no variable controls them). 117 of 118 sweep captures differ from `layer-before` in some pixel; none by a hidden control, overflow, new request failure or console error.

## Auth gates

None.

## Known Stubs

None. `shell.css` and `shell.mobile.css` remain the empty layer scaffolds from the previous plan.

## Threat Flags

None new. T-94-SC mitigated: `npm pack --ignore-scripts` only, SHA-256 per file recorded in the commit and above, every file listed in the research table matches. T-94-33 mitigated by the sweep comparisons (Findings 1 to 3 are the only open items). T-94-34 mitigated: the version is in the directory name, preload URLs equal the import URLs with no query, pinned by `Layouts_PreloadVendorWithoutVersionQuery`.

## Self-Check: PASSED

- Files present: `bootstrap-5.3.8/`, `fontawesome-6.7.2/` under `wwwroot/lib`, `UiFoundationBaseGuardTests.cs`, the five layouts changed, `app.css`, `tokens.css`, `base.css`, `components.css`, `site.css`.
- Commits present: `8418adb5`, `824c0dfa`, `b6d87b91`, `21c68026`.
- Old `bootstrap-5.3.0` and `fontawesome-6.4.0` directories removed; one `bootstrap-5.3.*` and one `fontawesome-6.*` directory remain.
- STATE.md, ROADMAP.md and WINDOWS.md not modified. Scratch: `~/qb-ui-review/phase-94/plans/94-19/{bump-differences.md,focus/,smoke-test.log,t/}`, sweeps `bump-before`, `bump-after` (`layer-before` link), `layer-after`, `layer-after2`, `layer-after3`, `base-after` (full type scale), `base-ring`, `base-ring2`, gates `bump-before`, `bump-after`, `layer-gate`.
