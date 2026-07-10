# Phase 68: Character Fields - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Mechanically repeat Phase 66's Markdown editor + rendering pattern onto Character Description and Character Backstory. CHARMD-01, CHARMD-02 only — no new capabilities beyond what's already scoped by ROADMAP.md. Both fields already exist symmetrically across every relevant form/view today (unlike Phase 67, there is no missing-field asymmetry to backfill and no Manage-page equivalent for Characters), so this phase is closer to pure mechanical repetition than 66 or 67 were.

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research / Phase 66 level (not re-discussed, carried forward)
- **Toolbar, preview mechanism, touch targets, CDN+SRI loading:** identical to Phase 66 — reuse the shared `_MarkdownEditor.cshtml` partial and `POST /markdown/preview` endpoint verbatim. No new editor infrastructure needed.
- **Read-side rendering:** `Html.Markdown()` helper, calling the same `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` used for every prior field.
- **Paragraph-break hint (EDITOR-05):** same info-icon-next-to-label + Bootstrap tooltip pattern as Phase 66/67, same tooltip text ("Supports Markdown formatting. Leave a blank line between paragraphs.").
- **No email surface:** unlike Description/Rewards, Character Description/Backstory never appear in any email template — EMAILMD-01/02/03 are Quest-specific and out of scope here. Confirmed no email `.razor` component references `Character.Description` or `Character.Backstory`.
- **No card/list preview:** `Views/Characters/Index.cshtml`/`Index.Mobile.cshtml` (the roster list) show no Description/Backstory snippet — confirmed by direct read. Unlike Phase 66's Quest board card (D-06), there is no plain-text-teaser/character-count decision needed for this phase.
- **Pre-wrap cleanup — already substantially solved by Phase 67.** Phase 67 added a global, unscoped `.markdown-content { white-space: normal }` rule to `markdown-content.css` that overrides any inherited `white-space: pre-wrap` regardless of which ancestor container/page it renders inside — this applies automatically to Character Description/Backstory the moment they route through `Html.Markdown()`, with zero new CSS required for that mechanism. The remaining work is narrower than ROADMAP's phrasing implies:
  - `Views/Characters/Details.cshtml:151,159` — remove the inline `style="white-space: pre-wrap;"` from the two `<p class="form-control-plaintext">` wrappers (and restructure away from `<p>` entirely — see Claude's Discretion below, this is the same "`<p>` can't legally contain block content" issue Phase 66/67 already hit and fixed for emails).
  - `wwwroot/css/character-detail.mobile.css:57-65` — `.character-info-value`'s `white-space: pre-wrap` rule is shared with `Owned by @Model.OwnerName` and `@Model.Level` (both confirmed single-line, non-multi-line values — pre-wrap vs normal is visually identical for them either way), so removing it project-wide from this shared class carries none of Phase 67's "breaks an untouched sibling field" risk. Still fine to leave the shared class alone and rely solely on `.markdown-content`'s own override if the executor prefers not to touch shared CSS at all — either approach is safe here, unlike Phase 67's QuestLog case.

### Claude's Discretion
- Exact restructuring of `Details.cshtml`'s Description/Backstory blocks away from `<p class="form-control-plaintext" style="white-space: pre-wrap;">@Model.X</p>` — `Html.Markdown()` emits block-level HTML (headings, lists) that cannot legally sit inside a `<p>`; follow the same `<div>`-wrapper precedent already established for the email templates and for `Details.Mobile.cshtml`'s `.character-info-value` (which already wraps in non-`<p>` containers where needed). Whether `.character-info-value`/`.form-control-plaintext` classes are kept on the new wrapper, replaced, or a new class is introduced is an implementation detail.
- Whether to remove `.character-info-value`'s `white-space: pre-wrap` rule in `character-detail.mobile.css` outright (safe, confirmed above) or leave it in place and rely on `.markdown-content`'s override alone — both are correct; pick whichever is less invasive during implementation.
- Order of implementation (Description vs Backstory, desktop vs mobile) — no user preference expressed; sequence for planning convenience.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 66 (pattern this phase mechanically repeats)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-CONTEXT.md` — full decision set (D-01 through D-09) for the proof-of-concept this phase repeats
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-07-SUMMARY.md` — the CSS specificity-bump pattern for any editor/Preview text nested inside `.modern-card`/similarly-styled cards, and the accepted 44px-tall/~30-34px-wide mobile touch-target deviation (re-check whether Characters' card styling hits the same leak)

### Phase 67 (most recent repeat of the pattern; established the pre-wrap/specificity lessons directly relevant here)
- `.planning/phases/67-remaining-quest-fields-email-templates/67-05-SUMMARY.md` — the `_QuestFormScripts`-style "shared partial referenced by short name only resolves within the including view's own controller folder" lesson (does not directly apply here since Characters has no equivalent shared-scripts partial dependency, but worth the executor's awareness if a similar shared-partial pattern is introduced)
- `.planning/phases/67-remaining-quest-fields-email-templates/67-REVIEW.md` — CR-01 (CSS specificity collision from routing existing plain-text content through `Html.Markdown()` for the first time, where a page-wide `!important` text-color rule started matching Markdig's `<p>` tags) and WR-01 (pale/invisible headings when `.markdown-content` renders directly inside a light card with no dark-overlay box) are the two most likely failure modes to re-check against Character Details' actual card styling before considering this phase done.

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — CHARMD-01, CHARMD-02 (this phase's requirements)
- `.planning/ROADMAP.md` — Phase 68 goal, success criteria, dependency on Phase 66

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Views/Shared/_MarkdownEditor.cshtml` (Phase 66) — the shared EasyMDE toolbar/preview partial. Drop into Description and Backstory textareas unchanged.
- `POST /markdown/preview` endpoint (Phase 66) — reused verbatim for both fields' Preview toggle; no new endpoint needed.
- `Html.Markdown()` HtmlHelper extension (Phase 66) — reused verbatim for all read-side rendering in this phase.
- `.markdown-content { white-space: normal }` (added Phase 67, `markdown-content.css`) — already solves the "no doubled spacing" requirement globally; no new CSS needed for that mechanism specifically.

### Established Patterns
- Mobile view resolution is automatic (`MobileDetectionMiddleware` + `MobileViewLocationExpander`) — no controller branching needed for any of this phase's view edits.
- `Character.cs` (Domain model): `Description` is `[StringLength(2000)]`, `Backstory` is `[StringLength(5000)]`; both nullable `string?`, neither has `[Required]` — matches the `Required = false` / no-asterisk pattern already used for Rewards/Recap.

### Integration Points
- **Write forms (both fields on all 4):** `Views/Characters/Create.cshtml:119-128`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` — existing `<textarea asp-for="Description">`/`<textarea asp-for="Backstory">` blocks to wire with the editor partial. All 4 files confirmed to already have both fields (4 `asp-for` occurrences each) — no missing-field gap to backfill anywhere.
- **Read views:** `Views/Characters/Details.cshtml:147-161` (`<p class="form-control-plaintext" style="white-space: pre-wrap;">`, needs restructuring away from `<p>` per Claude's Discretion above), `Details.Mobile.cshtml:80-93` (`.character-info-value`, `character-detail.mobile.css:57-65`) — swap raw `@Model.Description`/`@Model.Backstory` for `Html.Markdown(...)`.
- **Confirmed out of scope:** `Views/Characters/Index.cshtml`/`Index.Mobile.cshtml` (roster list — no Description/Backstory preview to touch), any email template (no Character field ever appears in an email).

</code_context>

<specifics>
## Specific Ideas

None — the user confirmed this phase needed no discussion beyond the mechanical scope already established by Phase 66/67, after reviewing the scouted findings above (full desktop/mobile symmetry, no Manage-page equivalent, pre-wrap already mostly solved by Phase 67's global CSS fix).

</specifics>

<deferred>
## Deferred Ideas

None — no scope creep or deferred ideas surfaced during this discussion.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`gsd-sdk query todo.match-phase 68` returned 0 matches).

</deferred>

---

*Phase: 68-character-fields*
*Context gathered: 2026-07-10*
