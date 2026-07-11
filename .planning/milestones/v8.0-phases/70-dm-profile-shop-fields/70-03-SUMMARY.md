---
phase: 70-dm-profile-shop-fields
plan: 03
subsystem: ui
tags: [markdown, razor, shop, html.markdown, extractplaintext]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: Html.Markdown() HtmlHelper, IMarkdownService.ExtractPlainText(), .markdown-content CSS, the ExtractPlainText-before-truncate card-teaser mechanism (D-06)
  - phase: 67-remaining-quest-fields-email-templates
    provides: .modern-card-body > .markdown-content h1..h6 light-card heading-contrast CSS fix
provides:
  - Shop Item Description rendered as formatted HTML on Shop Details desktop (full page + modal, via _ShopItemDetailsContent) and mobile
  - Shop Index customer-facing card teaser derived from ExtractPlainText (D-01)
  - ShopManagement dashboard's three list teasers consolidated into one DescriptionTeaser helper (D-02/D-03)
affects: [70-04 (Wave-2 verification plan for this phase)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "View-local @functions helper for consolidating duplicated teaser-truncation logic across multiple render sites in the same view (first use of this shape in the codebase)"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
    - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/Views/ShopManagement/Index.cshtml

key-decisions:
  - "D-01: Shop Index card teaser switches from a raw 120-char substring to ExtractPlainText()-before-truncate, reusing Phase 66 D-06's mechanism"
  - "D-02/D-03: ShopManagement's three identical 50-char inline substring teasers are replaced by ExtractPlainText()-before-truncate AND consolidated into one shared DescriptionTeaser @functions helper instead of three independent inline copies"

patterns-established:
  - "DescriptionTeaser(string) view-local @functions helper in ShopManagement/Index.cshtml: the first consolidated-teaser-helper shape in the codebase, for future phases that touch 3+ duplicated inline teaser snippets in one view"

requirements-completed: [PROFILEMD-02]

coverage:
  - id: D1
    description: "Shop Item Description renders as formatted HTML on Shop Details desktop (full page + Bootstrap modal via _ShopItemDetailsContent) and mobile, via Html.Markdown inside a markdown-content wrapper; inline pre-wrap removed"
    requirement: "PROFILEMD-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests ShopControllerIntegrationTests#Details_WithValidItemId_ShouldReturnItemDetails"
        status: pass
      - kind: other
        ref: "grep assertions: Html.Markdown(Model.Description) x1 each file, markdown-content wrapper present, white-space: pre-wrap absent, Html.Raw absent (Task 1 acceptance_criteria)"
        status: pass
    human_judgment: true
    rationale: "Automated checks confirm the render sink and wrapper structure compile and pass integration tests, but visual confirmation of the modal render path, mobile styling, and heading-contrast CSS is explicitly deferred to the Wave-2 verification plan (70-04) per this plan's <verification> section."
  - id: D2
    description: "Shop Index customer-facing card teaser (D-01) derives from ExtractPlainText(item.Description) truncated at 120 chars instead of a raw substring"
    requirement: "PROFILEMD-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests ShopControllerIntegrationTests#Index_ShouldReturnShopPage"
        status: pass
      - kind: other
        ref: "grep assertions: IMarkdownService injected, MarkdownService.ExtractPlainText present, 120-char length preserved, item.Description.Substring absent (Task 2 acceptance_criteria)"
        status: pass
    human_judgment: false
  - id: D3
    description: "All three ShopManagement dashboard list teasers (Items Awaiting Review, My Items, All Other Items) call one consolidated DescriptionTeaser helper using ExtractPlainText at 50 chars (D-02/D-03)"
    requirement: "PROFILEMD-02"
    verification:
      - kind: other
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj (compiles the @functions helper + injected service)"
        status: pass
      - kind: other
        ref: "grep assertions: IMarkdownService injected, DescriptionTeaser count=4 (1 def + 3 call sites), ExtractPlainText present, item.Description.Substring absent, Html.Raw absent (Task 3 acceptance_criteria)"
        status: pass
    human_judgment: false

# Metrics
duration: ~20min
completed: 2026-07-10
status: complete
---

# Phase 70 Plan 03: Shop Item Description Rendering & Teaser Consolidation Summary

**Shop Item Description now renders as formatted HTML on both Shop Details read surfaces (desktop page+modal, mobile) via Html.Markdown, and both existing plain-text preview teasers (Shop Index customer card, ShopManagement DM dashboard) now strip Markdown via IMarkdownService.ExtractPlainText() before truncating — the DM dashboard's three duplicate teaser copies consolidated into one DescriptionTeaser helper.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-10
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- Shop Item Description renders as formatted HTML on Shop Details desktop (`_ShopItemDetailsContent.cshtml`, shared by the full page and the Bootstrap modal) and mobile (`Shop/Details.Mobile.cshtml`) via `Html.Markdown()` inside a `markdown-content` wrapper; the redundant inline `white-space: pre-wrap` styles were removed.
- D-01: The customer-facing Shop Index browsing-card teaser now derives from `IMarkdownService.ExtractPlainText(item.Description)` before applying the existing 120-char truncation, so raw Markdown syntax (`#`, `*`, `>`, link markup) no longer leaks into the card.
- D-02/D-03: The DM-facing ShopManagement dashboard's three identical 50-char inline substring teasers (Items Awaiting Review, My Items, All Other Items) were replaced by calls to one new consolidated `DescriptionTeaser(string)` view-local `@functions` helper that also uses `ExtractPlainText()` before truncating.

## Task Commits

Each task was committed atomically:

1. **Task 1: Render Shop Item Description as formatted HTML on both Details read views** - `2f2cc213` (feat)
2. **Task 2: Switch the customer-facing Shop Index card teaser to extracted plain text (D-01)** - `c7a02d2b` (feat)
3. **Task 3: Consolidate the DM-facing ShopManagement list teasers into one ExtractPlainText helper (D-02/D-03)** - `8c49a16e` (feat)

**Plan metadata:** committed separately by the orchestrator after wave completion (worktree mode — this plan does not create its own docs commit).

## Files Created/Modified
- `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml` - Description card now renders via `Html.Markdown(Model.Description)` inside a `markdown-content` div (direct child of `.card-body.modern-card-body`); inline pre-wrap removed; added `@using QuestBoard.Service.Extensions`.
- `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml` - Description now renders via `Html.Markdown(Model.Description)` inside a `markdown-content parchment-text-muted mt-2` wrapper (keeps the existing muted-text styling); inline pre-wrap removed; added `@using QuestBoard.Service.Extensions`.
- `QuestBoard.Service/Views/Shop/Index.cshtml` - Injected `IMarkdownService MarkdownService`; the `.item-description` card teaser now computes `MarkdownService.ExtractPlainText(item.Description)` before the existing 120-char/`"..."` truncation.
- `QuestBoard.Service/Views/ShopManagement/Index.cshtml` - Injected `IMarkdownService MarkdownService`; added a `DescriptionTeaser(string description)` `@functions` helper (`ExtractPlainText()` + 50-char truncation); all three list-teaser call sites now call the shared helper instead of three independent inline substring expressions.

## Decisions Made
- D-01: Kept the existing 120-char truncation length and `"..."` suffix on the Shop Index teaser (per CONTEXT.md's Claude's Discretion) — only the source of the pre-truncation text changed (extracted plain text instead of the raw field).
- D-02/D-03: Kept the existing 50-char truncation length on the ShopManagement teasers, and implemented the D-03 consolidation as a view-local `@functions` block (rather than a reusable HtmlHelper extension) since no other file needs this specific helper — the lower-friction choice explicitly permitted by CONTEXT.md's Claude's Discretion note.
- No empty-state branch was added to either Details read view for Description, since the field is `[Required]` on both view models and no such branch existed before this change (matches the plan's explicit instruction not to add one).

## Deviations from Plan

None - plan executed exactly as written. All three tasks matched their `<action>` specifications; every grep-based `acceptance_criteria` assertion in the plan passed on the first attempt with no rework needed.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three read/teaser surfaces for Shop Item Description are converted; build is clean and `ShopControllerIntegrationTests` (15/15) pass.
- Live visual confirmation of the modal render path, mobile Details styling, and both teaser surfaces is explicitly deferred to the Wave-2 verification plan (70-04), per this plan's `<verification>` section — nothing further is needed from this plan to unblock 70-04.

---
*Phase: 70-dm-profile-shop-fields*
*Completed: 2026-07-10*

## Self-Check: PASSED

All modified files confirmed present on disk; all three task commit hashes (2f2cc213, c7a02d2b, 8c49a16e) confirmed in git log.
