---
phase: 99-shop-and-shop-management-ui-conversion
plan: 08
subsystem: shop-ui
tags: [shop, rarity-chip, amber, pages-layer, guards, razor]
requires: ["99-07"]
provides:
  - ShopItemDisplay, the one static mapping of rarity, status and type to words, class keys and icons
  - _ShopRarityChip partial, the one rarity chip markup (badge badge-rarity rarity-{key}, gem icon, the word)
  - shop-pages.css, the first layered shop page sheet (pages layer, tokens only), holding the five chip rules
  - old per-rarity rules narrowed (shop.css, legacy-pages.css) or removed (shop.mobile.css) so they cannot reach a new chip
affects: [99-09, 99-10, 99-15, every later Phase 99 plan that renders a chip or status]
tech-stack:
  added: []
  patterns: [one partial + one mapping for a shared badge, :not() narrowing of old unlayered rules until their last user converts, scanners with self-tests as guards]
key-files:
  created:
    - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs
    - QuestBoard.Service/Views/Shared/_ShopRarityChip.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.css
  modified:
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/Views/Shop/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Shop/Details.cshtml
    - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
    - QuestBoard.Service/wwwroot/css/shop.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/shop.mobile.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Legendary chip is amber (--qb-warning fill, --qb-warning-fg label, 8.92:1) with a still glow mixed from the same fill, as answered at the gate; the guidelines file is untouched"
  - "The filter-row rarity labels and the Item Properties rarity line also use the shared chip and mapping, so no old chip class remains on any shop page"
  - "The still glow is a box-shadow only: no animation, no keyframes in shop-pages.css"
status: complete
actuals:
  tokens: 8300
  tasks: 3
  commits: 2
---

# Phase 99 Plan 08: Rarity Chips Summary

Every rarity on the shop pages, in both layouts, is now one solid chip (gem icon plus the rarity word) from one partial and one mapping, styled by the pages layer with tokens only; the legendary chip is amber with a still glow, and the old rarity rules can no longer reach a converted page.

## What changed

- **Mapping.** `ShopItemDisplay` (static) has `RarityName`, `RarityKey`, `StatusName`, `StatusBadgeClass`, `StatusIcon`, `TypeName` and `TypeIcon`. This slice uses the rarity pair; the rest wait for the management list and status slices. Words: Common, Uncommon, Rare, Very Rare, Legendary; keys common, uncommon, rare, veryrare, legendary.
- **Partial.** `_ShopRarityChip` (model `ItemRarity`) renders `<span class="badge badge-rarity rarity-{key}"><i class="fas fa-gem" aria-hidden="true"></i>{word}</span>`. The word is constant text through Razor encoding, so colour is never the only signal.
- **Sheet.** `shop-pages.css` is one `@layer pages` block: `.badge-rarity` base (inline-flex, gap, uppercase, letter spacing) and five `.badge-rarity.rarity-*` rules. Common on `--qb-neutral`, uncommon on `--qb-success`, rare on `--qb-info`, very rare on `--qb-event`, legendary on `--qb-warning`, each with its `-fg` label. No colour literal, no `!important`, no new token, no animation. The legendary glow is a `box-shadow` mixed from `--qb-warning`.
- **Chip sites swapped (all on the partial).** Shop/Index cards and filter-row labels on both layouts, `_ShopItemDetailsContent` (price block chip; the Item Properties "Rarity" line now reads `ShopItemDisplay.RarityName`), and the phone Details page. `shop-pages.css` is linked from the Styles section of Shop/Index and Shop/Details on both layouts (the dialog is styled by its host page). `grep -c item-rarity` prints 0 for all four listed files.
- **Old rules.**
  - `shop.css` and `legacy-pages.css`: each of the five `.rarity-*` rules is now `.rarity-*:not(.badge-rarity)`, so they keep serving the management list's old markup until its own slice deletes them. `.item-rarity` and the legendary-glow keyframes stay for the same reason.
  - `shop.mobile.css`: the `.item-rarity` block and the five `.rarity-*` colour rules are deleted (no keyframes were used only by them). One small rule `.shop-item-card-mobile .badge-rarity { align-self: flex-start; margin: 4px 0 }` replaces the old `.item-rarity` margin, so the chip keeps its own width in the card's column instead of stretching across it (seen in the first phone screenshot).
- **Tests.**
  - `ShopRenderTests` (+11 cases): five chips with matching keys and words on Shop/Index under both User-Agents, one chip on the details page (desktop, phone) and dialog fragment, Very Rare key and words, no `item-rarity` anywhere on those pages, `shop-pages.css` linked on four pages, the management list still on its old markup.
  - `ShopConversionGuardTests` (+4): `ShopSheets_LegendaryChipFollowsTheRecordedColour` (holds the answer as a constant, `amber`; the legendary rule must use `--qb-warning` with `--qb-warning-fg`, no other chip rule may use `--qb-accent`, nothing animates), `ShopSheets_OldRarityRulesNeverReachANewChip`, and a self-test for each scanner on seeded bad samples. `PhaseSourceFiles` gained every file this plan touched (except `.claude/ui-guidelines.md`).
  - `UiFoundationLayerGuardTests`: `shop-pages.css` appended to `LayeredFiles` and `NoLiteralFiles`.
- **Self-tests tried once.** Seeding an unnarrowed `.rarity-rare` in `shop.css` and an accent fill in `shop-pages.css` made both sheet facts fail; both files were restored with `git checkout` and the facts pass again.

## Legendary answer applied

`gate1/answers.json` has `legendaryChip.colour = "amber"` and `glowAndButtons.stillGlow = true`. Applied: amber fill with the near-black label, plus the still glow. `.claude/ui-guidelines.md` was not touched (`git diff --quiet` against the plan start prints guideline-ok), so the "Gold is reserved" list is unchanged.

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2350 passed, 0 failed, 16 skipped (the SQL Server migrator tests), total 2366 (2335 before; +15 new facts).
- Sweep on 8320 of `shop-index`, `shop-modal`, `details-legendary`, `details-published`, `details-draft` and `mgmt-index` under both User-Agents (24 captures, all 200): `readability-compare.py` against BEFORE reports 0 failing rows, `ok`. Contrast failures fell where chips were the cause (`shop-index` desktop 15 to 13, phone 39 to 27; `details-legendary` desktop 12 to 3, phone 2 to 1). `mgmt-index` is identical to BEFORE: same contrast failures (27 desktop, 1 phone), same overflow and control counts at every width.
- Measured chip contrast (computed colours of the live page, both User-Agents identical): legendary 8.92:1 (BEFORE 1.63:1), rare 5.67:1 (BEFORE 2.15:1), common 6.09:1, uncommon 5.35:1, very rare 6.51:1.
- Chip look check against Direction A (`lookcheck.mjs --only chips`, written to `after/99-08/lookcheck-desktop.json` and `lookcheck-mobile.json`): `pass` true under both User-Agents, 83 compared rows each, 0 differences other than the legendary still glow (Gate 1 approved; the render has none). The legendary chip equals the render's amber.
- No file under Domain, Repository, Controllers, `tokens.css`, `components.css` or `Views/ShopManagement` changed (scope check printed scope-ok). No fresh purchase was made (18 day-0 purchases already exist) and nothing was posted.

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate met by an automated probe, not a human checkpoint**
- The run is not in auto mode, so the tracer gate asks for a human-verify checkpoint; this sequential executor cannot be resumed after one. As in the previous slice, the committed tracer was swept on 8320 under both User-Agents (`after/99-08/tracer/`) and the screenshots read before any expansion task: chips on cards, filter row and details look right on both layouts.

**2. [Rule 1 - Bug] Phone chip stretched across the card**
- **Found during:** tracer screenshots. The phone card is a flex column, so the inline-flex chip stretched to full width.
- **Fix:** the `align-self` rule in `shop.mobile.css` described above. **Commit:** 75a082c4.

**3. [Rule 3 - Blocking] The filter-row labels also carry chips**
- The plan names four chip sites, but the filter rows on Shop/Index (both layouts) also render the old `item-rarity` span, and the acceptance criterion needs zero `item-rarity` in those views. They now use the same partial; the filter card's layout is untouched (it follows in its own slice).

**4. [Scratch tool] `lookcheck.mjs --only chips`**
- Added as the plan says. Two choices to know: a chip inside a filter label is only compared when no card chip of that rarity is visible on the page, and its `display` is skipped there because the label makes it a flex item on one side only; the legendary still glow is the only difference allowed. The tool's old required map file is now optional (phase 99 has none).

**5. [Note] `legacy-collision.py` still lists `filter-rarity-group`**
- The acceptance line asks that the tool list no rarity or chip class. It lists `.filter-rarity-group` from `shop.css`, the old layout class of the filter row, not a chip or rarity rule. It stays until the filter card is converted; no chip class is listed.

## Known Stubs

None.

## Threat Flags

None. T-99-21 is mitigated (the partial takes an enum value and renders constant words through Razor encoding); T-99-56 is mitigated (the guard pins the token pair to the recorded answer; the guideline file is untouched on amber).

## Self-Check: PASSED

Present: `ShopItemDisplay.cs`, `_ShopRarityChip.cshtml`, `shop-pages.css`, `after/99-08/` (report.json, allow.json, lookcheck-desktop.json, lookcheck-mobile.json, PLAN_START). Commits found: 75a082c4, 9711f8fa. Both test projects green; port 8320 stopped, 8321 left running.
