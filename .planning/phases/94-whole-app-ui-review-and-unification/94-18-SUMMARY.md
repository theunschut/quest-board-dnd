---
phase: 94-whole-app-ui-review-and-unification
plan: 18
subsystem: ui-foundation
tags: [tokens, css-layers, cascade-order, wcag-contrast, guard-tests]
requires:
  - phase: 94-17
    provides: "signed-off token values (tokens-final.css) and the sign-off table"
  - phase: 94-13
    provides: "layer order statement at the top of app.css"
provides:
  - "tokens.css: the full --qb-* token set plus the --card-width family and a [data-theme='shop'] block, inside @layer tokens"
  - "base.css (html fallback background), and empty components.css, shell.css, shell.mobile.css layer scaffolds"
  - "all five layouts link the foundation in cascade order; desktop and Platform desktop layouts now render the Styles section"
  - "UiFoundationLayerGuardTests (link order, layer declarations, no important flags, no colour literals, no planning references, token contrast) and WcagContrast helper with known-value tests"
affects: [94-19, 94-20, 94-21, 94-22, 94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Foundation stylesheets sit in named cascade layers; legacy page sheets stay unlayered and keep winning"
    - "Only tokens.css holds colour literals; a static guard recomputes every declared contrast pair from it"
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.Service/wwwroot/css/base.css
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/shell.css
    - QuestBoard.Service/wwwroot/css/shell.mobile.css
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/WcagContrast.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationWcagContrastTests.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
decisions:
  - "Colour cap counted as distinct opaque hex values (38 with the Shop theme, cap 38); the alpha steps are listed as a separate set in the tokens.css header, not counted"
  - "tokens.css sets --qb-board-image to none in the root block: the file the sign-off copy named does not exist, and the backdrop plan owns the image"
  - "The Shop block re-declares --qb-surface-chrome so the gradient follows the theme's own stops"
  - "Control border is scored on the control's own surfaces (control fill, paper, paper-alt), not on glass; status text on hovered glass is a named, explained shortfall"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 12000
  tasks: 2
  commits: 2
---

# Phase 94 Plan 18: Token Layer and Cascade Order Summary

The signed-off token set is live as `@layer tokens` custom properties, every layout links the foundation in cascade order with `html` painting the board fallback colour, and a static guard recomputes 60 declared contrast pairs from `tokens.css` over the lightest board pixel.

## What was built

- **Task 1 (tracer), commit 679f2cae.** `tokens.css` is `tokens-final.css` ported value for value (all 84 contract token names plus the 11 additions the sign-off kept), with a plain-language header that states the colour budget and lists the alpha steps. `base.css` holds the one `html` fallback rule; `components.css`, `shell.css` and `shell.mobile.css` are single empty `@layer components` blocks. The five layouts link `tokens`, `base`, `components`, `shell` (and `shell.mobile` on the two phone layouts) directly after `app.css`; `_Layout.cshtml` and `_Layout.Platform.cshtml` gained `RenderSectionAsync("Styles")` as the last head line. `UiFoundationLayerGuardTests` carries the link-order, no-mobile-sheet-on-desktop, layer-declaration, no-important, no-colour-literal and no-planning-reference facts, plus a self-test that shows each scanner fires on a known-bad sample (otherwise the guards would pass vacuously on empty files).
- **Task 2, commit 3366d091.** `WcagContrast` (Parse for hex 3/4/6/8 and rgb/rgba comma, space, slash and percent syntax, Over, RelativeLuminance, Ratio, TextOn, plus a Surface helper). `UiFoundationWcagContrastTests` pins 4.34, 2.99, 3.36, 1.97, 4.94, 7.10 and 4.53 to 0.005, plus parse and compositing facts. `TokenContrast_DeclaredPairsMeetTheBar` reads `tokens.css`, resolves `var()` chains, `color-mix()` and the chrome gradient's stops, and scores a readable pair table for the default tokens and the Shop theme separately. Further facts show it fails when `--qb-fg` goes dark, throws on a non-literal contrast token, and that the Bootstrap `--bs-*-rgb` triples stay in step with their fills (default and Shop).

## Verification

- Layer guard and WCAG tests: 31 pass. Filtered set from the plan (`UiFoundation`, MobileCss, MobileLayout, PlatformAreaLayout, MobileViews, BoardMoveStaticGuard, BoardPageStaticGuard): 214 pass at the end of Task 1.
- Local negative check (not committed): setting `--qb-link` to `#1a0f08` makes the contrast guard fail with `--qb-link on glass is 2.23, needs 4.5`; `tokens.css` was restored from git.
- Tracer gate: the tracer's verify (tests plus readability comparison plus computed-style check) was re-run end to end before Task 2 and passed.
- **Readability comparison:** `readability-compare.py` reads sweep reports, not gate reports (a gate record has `name` and no contrast or interactive data), so the plan's `gate.mjs --label t-before` pair could not feed it. I ran the full `sweep.mjs` on port 8018 for the unmodified layouts (`p18-before`, obtained by reverting the five layouts, rebuilding and restarting) and for the final layouts (`p18-after`): 120 records, 0 non-200, and `readability-compare.py p18-before p18-after` printed **PASS** (no new contrast failure, hidden or lost control, console error, failed request or overflow). `gate.mjs` captures `t-before` and `t-after` were also taken (14 pages); the one failed request on desktop quest-details is identical in both. The shared probe false-positive for glass backgrounds (ledger entry 8) did not come into play: nothing was flagged, so the corrected probe was not needed.
- **Computed fallback:** a Playwright check on port 8018 (desktop, phone, group picker, Platform desktop, Platform phone) returned `getComputedStyle(documentElement).backgroundColor` = `rgb(44, 24, 16)` = `--qb-board-fallback` (`#2c1810`) on all five layouts, with the new sheets linked in order and `shell.mobile.css` only on the two phone layouts.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Dangling backdrop image reference in the ported token file**
- **Found during:** Task 1
- **Issue:** the sign-off copy sets `--qb-board-image: url('/images/nb-1600.webp')`; that file only exists in scratch, so any consumer would 404.
- **Fix:** the root block sets `--qb-board-image: none`; the Shop block keeps its navy gradient. The backdrop plan generates the real asset and sets the token.
- **Files modified:** `tokens.css` - **Commit:** 679f2cae

**2. [Rule 1 - Bug] Shop chrome gradient would not follow the theme**
- **Found during:** Task 1
- **Issue:** `--qb-surface-chrome` is built from `var(--qb-chrome-top/bottom)` in the root block. A custom property resolves its `var()` where it is declared, so a theme block that only overrides the two stops leaves an inherited wood gradient.
- **Fix:** the Shop block re-declares `--qb-surface-chrome` from its own stops; no new colour.
- **Files modified:** `tokens.css` - **Commit:** 679f2cae

**3. [Rule 2 - Missing critical] Control border scored on the control's own surface**
- **Found during:** Task 2
- **Issue:** the plan said "`--qb-control-border` against glass and paper". `#7a5a30` on dark glass is about 1.3:1 and cannot reach 3:1, but a control never sits its border on glass: the signed-off set gives inputs their own `--qb-control-bg`, and the sign-off pair lists score the border on control fill and paper-alt.
- **Fix:** scored on `--qb-control-bg`, `--qb-surface-paper` and `--qb-surface-paper-alt`.
- **Files modified:** `UiFoundationLayerGuardTests.cs` - **Commit:** 3366d091

**4. [Scope] readability inputs**: see Verification. Full sweeps instead of gate captures, same pass criteria.

### Additions beyond the plan (same files)

The scanner self-test, the negative contrast facts, the `color-mix` and gradient-stop resolution, and the Bootstrap channel facts were added so the guards cannot pass vacuously. No new files beyond the plan's list.

## Findings for the orchestrator (need a decision, none blocks)

1. **Status text on hovered glass misses 4.5:1.** The signed-off success, danger, warning and info text colours measure 4.23, 4.31, 4.30 and 4.25 on hovered glass (`rgba(255,255,255,0.18)` over the 42% scrim) at the lightest board pixel, 4.46 at the 99th percentile; the sign-off pair lists never scored status text on hovered glass. At rest they pass (5.10 to 5.19) and every other glass text token passes on hover. I did not change signed-off values. The guard row for hovered glass names the four exceptions in a comment; lightening the four status texts a few percent (success `#7fdca4` to about `#8ce4b0` gives 4.62) or easing the hover tint closes it, and the row then takes the full list. Not appended to the shared defect ledger from this worktree (parallel plans append to the same file and would conflict at merge); the orchestrator should log it as an unmet-truth entry.
2. **Where the Shop theme attribute goes matters.** The sign-off record says the layouts emit `data-theme` on the body. The mockups put it on `<html>`. On the body, the Shop block still reaches everything below the body, but the `html` fallback background keeps the wood fallback `#2c1810` (it reads `--qb-board-fallback` from the root element), so a slow image on a Shop page flashes the brown fallback. Putting it on `<html>` avoids that. Also, the backdrop plan (94-21) is told to set `--qb-board-image` on `body` from `shell.css`: that rule is in the components layer and would beat a body-level Shop override in the tokens layer; set it on `:root` instead.
3. **Bootstrap `--bs-*` overrides are inert for now.** The `--bs-*-rgb` values in `tokens.css` are in the tokens layer, and Bootstrap's own `:root` declarations are unlayered, so they win until Bootstrap is moved into the vendor layer. Nothing changes on screen today (the readability comparison confirms it); the channel-match guard already pins them for when they take effect.
4. **Colour cap.** Counted per the resolved rule: 30 opaque hex values in `:root`, 38 with the Shop theme. That is exactly at the 38 limit, so the next opaque colour needs either a removal or an explicit decision. There is no count test (the plan has none).

## Known Stubs

`components.css`, `shell.css` and `shell.mobile.css` are deliberately empty layer blocks with a header comment naming what each will hold; the following foundation plans fill them. No data is wired to UI from them.

## Threat Flags

None. Static stylesheet and test changes only; no new endpoint, auth path or schema.

## Self-Check: PASSED

- Files present: tokens.css, base.css, components.css, shell.css, shell.mobile.css, the three test files, the five layouts changed.
- Commits present: 679f2cae, 3366d091.
- `dotnet build` clean (0 errors). Full `dotnet test`: 1300 unit tests and 1701 integration tests pass (11 skipped, pre-existing); a first run died with the known transient CLR error and was rerun once.
- Port 8018 stopped; ports 8000 and 8001 untouched; STATE.md and ROADMAP.md not modified.
