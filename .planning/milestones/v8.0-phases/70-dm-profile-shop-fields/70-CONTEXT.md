# Phase 70: DM Profile & Shop Fields - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Mechanically repeat Phase 66's Markdown editor + rendering pattern onto DM Profile Bio and Shop Item Description. PROFILEMD-01, PROFILEMD-02 only — no new capabilities beyond what's already scoped by ROADMAP.md. DM Profile Bio is fully symmetric and mechanical (no card/list preview anywhere, no email surface) — the same shape as Character in Phase 68. Shop Item Description is the one genuinely new wrinkle in this phase: it's the only field in the whole v8.0 milestone that already has an *existing* plain-text preview snippet (on both the customer-facing Shop Index grid and the DM-facing Shop Management dashboard) that will start leaking raw Markdown syntax once the field holds Markdown source, and it's the first `[Required]` field this migration has touched.

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research / Phase 66 level (not re-discussed, carried forward)
- **Toolbar, preview mechanism, touch targets, CDN+SRI loading:** identical to Phase 66 — reuse the shared `_MarkdownEditor.cshtml` partial and `POST /markdown/preview` endpoint verbatim. No new editor infrastructure needed.
- **Read-side rendering:** `Html.Markdown()` helper, calling the same `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` used for every prior field.
- **Paragraph-break hint (EDITOR-05):** same info-icon-next-to-label + Bootstrap tooltip pattern as Phase 66/67/68/69, same tooltip text.
- **No email surface:** neither DM Profile Bio nor Shop Item Description appears in any email template — confirmed by direct read (no `.razor` component references either field).
- **Pre-wrap cleanup — largely already solved by Phase 67's global CSS.** `.markdown-content { white-space: normal }` (`markdown-content.css`, Phase 67) already overrides inherited `pre-wrap` regardless of ancestor, so the remaining work is removing the now-redundant inline `style="white-space: pre-wrap;"` occurrences as a companion edit: `DungeonMaster/Profile.cshtml:51`, `_ShopItemDetailsContent.cshtml:136`, `Shop/Details.Mobile.cshtml:21`. Per Phase 64's established precedent, `Shop/Details.Mobile.cshtml`'s inline style stays inline rather than touching the shared `.parchment-text-muted` utility class (that class is also used by non-Description, non-multi-line text elsewhere — same reasoning Phase 64 already applied here).
- **Light-card heading-contrast fix already generic — verify, don't redesign.** `.modern-card-body > .markdown-content h1..h6` (`markdown-content.css`, added for Phase 66/67's light-card cases) already covers DM Profile's desktop "About" card and Shop's desktop Description card, since both are `.modern-card` → `.modern-card-body` with no dark-overlay box, exactly the shape this selector targets. Requires `Html.Markdown()`'s output wrapper to render as a **direct child** of `.card-body.modern-card-body` — planner/executor must preserve that structure, not nest it inside an extra wrapper `<div>`, or the selector silently stops matching.
- **DM Profile Mobile is a separate styling surface — no fix needed, but verify.** `Profile.Mobile.cshtml`'s Bio card uses bespoke `dm-profile-bio-card`/`dm-profile-bio-text` classes (`dm-profile.mobile.css`), not `.modern-card`. It's a semi-transparent dark/glass card (`background: rgba(255,255,255,0.15)`) with gold text (`#F4E4BC`) and a dark text-shadow — the same dark-background shape `.markdown-content`'s *default* (non-overridden) heading color already targets. This should render correctly with zero new CSS, but confirm visually during human verification since it's a styling surface none of the generic selectors were written against.
- **`[Required]` field — already handled.** Shop Item Description is `[Required]` on both `CreateShopItemViewModel` and `EditShopItemViewModel` — the first required field this milestone has migrated. Phase 69's Wave 3 fix (EasyMDE-to-textarea silent-submission-failure on `Required=true` fields) was made at the shared `markdown-editor.js` level specifically so every future required field, including this one, is already covered. No new work needed here — just don't skip the regression check.
- **Shop Details is read-only in both its rendering contexts — no editor-in-modal concern.** `Shop/Details.cshtml` renders `_ShopItemDetailsContent.cshtml` both as a full page and inside a Bootstrap modal (`isModal` branch), but only ever displays `Html.Markdown()` output — never an editor or Preview toggle. `ShopManagement/Create.cshtml`/`Edit.cshtml` (where the editor actually lives) are full pages, not modals. No modal-specific editor sizing/z-index handling needed.

### Shop Index card preview (customer-facing)
- **D-01:** `Views/Shop/Index.cshtml`'s browsing-grid card (`.item-description`, currently a 120-char raw substring of `Description`) switches to `IMarkdownService.ExtractPlainText()` before truncating — the same mechanism Phase 66 D-06 established for the Quest board card. This is the only existing precedent in the milestone for a plain-text card teaser derived from a Markdown field; reusing it keeps the approach consistent app-wide rather than introducing a second stripping method.

### Shop Management list preview (DM-facing)
- **D-02:** `Views/ShopManagement/Index.cshtml`'s three table sections (Items Awaiting Review / Published / the third section — all currently duplicate the identical `item.Description.Length > 50 ? ... : ...` raw-substring pattern) also switch to `IMarkdownService.ExtractPlainText()` before truncating — same mechanism as D-01, applied consistently across both the customer-facing and DM-facing surfaces rather than diverging.
- **D-03:** Since all 3 copies of the truncation logic are being touched for D-02 anyway, consolidate them into one shared helper (e.g. a `@functions` block in the view, or a small Razor/HtmlHelper extension) instead of leaving 3 independent inline copies. This is the one place in this phase where the user explicitly chose consolidation over this milestone's usual minimal-diff mechanical-repetition default — scoped narrowly to this one duplicated snippet, not a broader refactor of `ShopManagement/Index.cshtml`.

### Claude's Discretion
- Exact restructuring of `DungeonMaster/Profile.cshtml`'s `<p style="white-space: pre-wrap;">@Model.Bio</p>` and `_ShopItemDetailsContent.cshtml`'s equivalent `<p>` away from `<p>` where `Html.Markdown()`'s block-level output requires it — follow the `<div class="markdown-content">`-wrapper precedent already established in Phase 66/67/68/69, making sure the wrapper stays a **direct child** of `.card-body.modern-card-body` (see the light-card heading-contrast note above).
- Whether D-01/D-02's `ExtractPlainText()`-derived teaser keeps the existing truncation lengths (120 / 50 chars) or adjusts them — no user preference expressed; extracted plain text may read differently than a raw substring did, so pick whatever reads cleanest.
- Exact shape of D-03's shared helper (view-local `@functions` block vs. a reusable HtmlHelper extension) — implementation detail, not a user-facing decision.
- Order of implementation (DM Profile vs Shop, desktop vs mobile, Index vs Management) — no user preference expressed; sequence for planning convenience.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 66 (pattern this phase mechanically repeats)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-CONTEXT.md` — full decision set (D-01 through D-09), especially D-06 (the `ExtractPlainText()` plain-text-teaser mechanism this phase's D-01/D-02 explicitly reuse)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-07-SUMMARY.md` — the CSS specificity-bump pattern for editor/Preview text nested inside `.modern-card`-styled cards, and the accepted 44px-tall/~30-34px-wide mobile touch-target deviation

### Phase 67 (established the light-card heading-contrast fix this phase depends on)
- `.planning/phases/67-remaining-quest-fields-email-templates/67-REVIEW.md` — CR-01/WR-01, the pale/invisible-headings-in-a-light-card finding that produced the `.modern-card-body > .markdown-content h1..h6` CSS rule this phase's DM Profile and Shop Description cards rely on being generic

### Phase 64 (established the "leave `.parchment-text-muted` alone" precedent for Shop mobile)
- `.planning/phases/64-preserve-line-breaks-in-description-text-on-mobile-views/64-CONTEXT.md` — why `Shop/Details.Mobile.cshtml`'s pre-wrap fix used an inline style instead of touching the shared `.parchment-text-muted` class; same reasoning applies to this phase's pre-wrap cleanup on that file

### Phase 69 (most recent repeat; established the required-field EasyMDE fix this phase depends on)
- `.planning/phases/69-contact-fields/69-CONTEXT.md` — general pattern-repetition precedent
- `.planning/phases/69-contact-fields/69-*-SUMMARY.md` (Wave 3) — the EasyMDE-to-textarea silent-submission-failure fix on `Required=true` fields, made at the shared `markdown-editor.js` level; Shop Item Description is the first field in this milestone to actually depend on that fix mattering

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — PROFILEMD-01, PROFILEMD-02 (this phase's requirements; note PROFILEMD-02's wording explicitly names "Shop Index/Details/Manage" as render surfaces, which is why the Index and Management preview decisions above are in scope, not deferred)
- `.planning/ROADMAP.md` — Phase 70 goal, success criteria, dependency on Phase 66

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Views/Shared/_MarkdownEditor.cshtml` + `ViewModels/Shared/MarkdownEditorViewModel.cs` (Phase 66) — the shared EasyMDE toolbar/preview partial, reused unchanged.
- `POST /markdown/preview` endpoint (`Controllers/MarkdownController.cs`, Phase 66) — reused verbatim.
- `Html.Markdown()` HtmlHelper extension (Phase 66) — reused verbatim for all read-side rendering.
- `IMarkdownService.ExtractPlainText(string?)` (Domain layer, Phase 66) — the mechanism D-01/D-02 reuse; already injected/used as a pattern in `Views/Quest/Index.cshtml` for the board card teaser (2 call sites: display text + poster-length calc) — same injection pattern applies here.
- `~/Views/Quest/_QuestFormScripts.cshtml` — the full-path partial include that wires up `markdown-editor.js` initialization on every write form. `DungeonMaster/EditProfile.cshtml` and `ShopManagement/Create.cshtml`/`Edit.cshtml` currently have **no** reference to this partial at all (no editor exists yet) — must be added as part of wiring in the editor, using the full `~/Views/Quest/...` path per Phase 68's Wave 1 lesson (a short-name include would 404 from a different controller folder).
- `.markdown-content { white-space: normal }` (Phase 67, `markdown-content.css`) — already solves doubled-spacing globally.
- `.modern-card-body > .markdown-content h1..h6` (Phase 67, `markdown-content.css`) — already solves light-card heading contrast for both of this phase's desktop cards, contingent on direct-child structure (see Decisions).

### Established Patterns
- Mobile view resolution is automatic (`MobileDetectionMiddleware` + `MobileViewLocationExpander`) — no controller branching needed for any of this phase's view edits.
- `DungeonMasterProfile.Bio`: nullable, no `[Required]`, `maxlength="2000"` enforced only via the HTML attribute (`EditProfile.cshtml`) — matches the optional-field pattern used everywhere else in this milestone.
- `ShopItem.Description`: `[Required]` on both `CreateShopItemViewModel` and `EditShopItemViewModel` — the first required field in this migration (see Decisions).
- `Shop/Details.Mobile.cshtml` uses its own `ShopItemDetailsViewModel` and its own template — it does **not** share `_ShopItemDetailsContent.cshtml` with desktop, unlike most other mobile/desktop pairs in this codebase. Confirmed by direct read.

### Integration Points
- **DM Profile Bio write form:** `Views/DungeonMaster/EditProfile.cshtml:43-54`, `EditProfile.Mobile.cshtml:46-52` — existing `<textarea asp-for="Bio">` blocks, no `_MarkdownEditor.cshtml` wired in yet.
- **DM Profile Bio read views:** `Views/DungeonMaster/Profile.cshtml:49-56` (`.modern-card-body`, `<p style="white-space: pre-wrap;">@Model.Bio</p>`), `Profile.Mobile.cshtml:45-52` (bespoke `dm-profile-bio-card`/`dm-profile-bio-text`, no inline pre-wrap style present today — check whether `@Model.Bio` needs any wrapper change at all beyond swapping to `Html.Markdown()`).
- **Shop Description write forms:** `Views/ShopManagement/Create.cshtml:42-47`, `Create.Mobile.cshtml:39-44`, `Edit.cshtml:67-71`, `Edit.Mobile.cshtml:64-68` — existing `<textarea asp-for="Description">` blocks, no editor wired in yet. Both `Create.cshtml` and `Edit.cshtml` also have inline `<script>` JS (`document.getElementById('Description').value.trim()`, lines 239/291 respectively) that reads the raw textarea value directly — this will read stale/empty content once EasyMDE hides the underlying `<textarea>`, unless that JS is updated to read from the CodeMirror instance instead (the same class of bug the shared `markdown-editor.js` submit-sync fix targets, but this looks like bespoke inline JS, not the shared submit path — needs explicit verification during planning/research).
- **Shop Description read (Details):** `Views/Shared/_ShopItemDetailsContent.cshtml:129-138` (`.modern-card-body`, `<p class="mb-0" style="white-space: pre-wrap;">@Model.Description</p>`), `Views/Shop/Details.Mobile.cshtml:21` (`.parchment-text-muted`, inline pre-wrap) — swap for `Html.Markdown()`.
- **Shop Description preview (Index card):** `Views/Shop/Index.cshtml:304-311` — `item.Description.Length > 120 ? Substring(0,120)+"..." : item.Description` → `ExtractPlainText()`-based per D-01.
- **Shop Description preview (Management list):** `Views/ShopManagement/Index.cshtml:56,128,300` — 3 identical inline occurrences of `item.Description.Length > 50 ? Substring(0,50)+"..." : item.Description` → consolidated `ExtractPlainText()`-based helper per D-02/D-03.
- **Confirmed out of scope:** `Views/Shop/Index.Mobile.cshtml` (no Description preview at all today — confirmed by direct read, nothing to change).

</code_context>

<specifics>
## Specific Ideas

None beyond the two decided areas above — the user engaged directly with the Shop preview-text problem (the one genuinely new wrinkle this phase surfaces) and chose consolidation over the milestone's usual minimal-diff default for the one place (D-03) where 3 copies of identical logic were already being touched.

</specifics>

<deferred>
## Deferred Ideas

None — no scope creep surfaced during this discussion.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`todo.match-phase 70` returned 0 matches).

</deferred>

---

*Phase: 70-dm-profile-shop-fields*
*Context gathered: 2026-07-10*
