# Phase 70: DM Profile & Shop Fields - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-10
**Phase:** 70-dm-profile-shop-fields
**Areas discussed:** Shop Index card preview, Shop Management list preview

---

## Scope Assessment

Before presenting gray areas, Claude scouted the codebase (`Views/DungeonMaster/*.cshtml`, `Views/Shop/*.cshtml`, `Views/ShopManagement/*.cshtml`, `Views/Shared/_ShopItemDetailsContent.cshtml`, `dm-profile.mobile.css`, `markdown-content.css`) and found:

- DM Profile Bio is fully symmetric/mechanical — no card/list preview anywhere, no email surface, matches Character's Phase 68 shape exactly.
- Shop Item Description is the one genuinely new wrinkle: it's the only field in the v8.0 milestone with an *existing* plain-text preview (Shop Index grid, 120 chars; Shop Management dashboard, 50 chars ×3 duplicated table sections) that will leak raw Markdown syntax once the field holds Markdown source.
- Shop Item Description is the first `[Required]` field this migration has touched — already covered by Phase 69's shared-JS fix.
- The light-card heading-contrast CSS fix from Phase 67 is already generic enough to cover both of this phase's desktop cards, contingent on `Html.Markdown()`'s wrapper staying a direct child of `.modern-card-body`.

## Shop Index card preview

| Option | Description | Selected |
|--------|-------------|----------|
| ExtractPlainText() truncated teaser | Reuse the exact Phase 66 D-06 mechanism the Quest board card already uses | ✓ |
| Render actual formatted HTML | Show a small rendered-HTML snippet instead of plain text | |
| Remove the preview entirely | Drop the Description snippet, matching Character/Contact's no-preview precedent | |

**User's choice:** ExtractPlainText() truncated teaser (recommended option)
**Notes:** No follow-up — user confirmed this was a straightforward "next area" after selecting.

---

## Shop Management list preview

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, same ExtractPlainText() mechanism | Consistent treatment across both surfaces | ✓ |
| Different treatment — DMs see raw Markdown source | Staff might prefer raw syntax for spotting formatting mistakes | |
| Remove the preview from Manage entirely | Drop the snippet, click through to Edit instead | |

**User's choice:** Yes, same ExtractPlainText() mechanism (recommended option)

**Follow-up question — code-quality companion decision:**

| Option | Description | Selected |
|--------|-------------|----------|
| Leave 3 separate inline copies | Matches this milestone's minimal-diff, mechanical-repetition default | |
| Consolidate into one shared helper/partial | Extract the truncate-and-preview logic since all 3 call sites are being touched anyway | ✓ |

**User's choice:** Consolidate into one shared helper/partial
**Notes:** The one place this phase deliberately deviates from the milestone's usual minimal-diff default — scoped narrowly to this one duplicated snippet.

---

## Claude's Discretion

- Exact restructuring of `Profile.cshtml`'s and `_ShopItemDetailsContent.cshtml`'s `<p>` wrappers away from `<p>` for `Html.Markdown()`'s block-level output — follow the established `<div class="markdown-content">` precedent, staying a direct child of `.card-body.modern-card-body`.
- Whether D-01/D-02's extracted-plain-text teaser keeps the existing truncation lengths (120 / 50 chars) or adjusts them.
- Exact shape of the D-03 shared helper (view-local `@functions` block vs. reusable HtmlHelper extension).
- Order of implementation (DM Profile vs Shop, desktop vs mobile, Index vs Management).

## Deferred Ideas

None — no scope creep surfaced during this discussion.
