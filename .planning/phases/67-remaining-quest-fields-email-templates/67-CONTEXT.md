# Phase 67: Remaining Quest Fields & Email Templates - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Mechanically repeat Phase 66's Markdown editor + rendering pattern onto Quest Rewards and Quest Recap, and finish the email side of the pattern by making the Quest Finalized, Session Reminder, and Waitlist Promoted emails all render Quest Description as formatted HTML (Quest Finalized already does this — Phase 66 shipped it). QUESTMD-02, QUESTMD-03, EMAILMD-01 only — no new capabilities beyond what's already scoped by ROADMAP.md, except the two narrow extensions locked below (both directly touch files this phase already modifies).

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research / Phase 66 level (not re-discussed, carried forward)
- **Toolbar, preview mechanism, touch targets, CDN+SRI loading:** identical to Phase 66 — reuse the shared `_MarkdownEditor.cshtml` partial and `POST /markdown/preview` endpoint verbatim. No new editor infrastructure needed.
- **Read-side rendering:** `Html.Markdown()` helper, calling the same `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` used for Description.
- **Email-side rendering:** `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Email)` via `@inject IMarkdownService`, wrapped in a `<div>` (never `<p>`) — Phase 66's code review found `<p>` cannot legally contain block-level HTML and silently drops styling for any multi-paragraph/structured content. `QuestFinalized.razor` already does this correctly; `SessionReminder.razor` and `WaitlistPromoted.razor` currently render raw `@QuestDescription` (unrendered Markdown syntax, HTML-encoded) and must be brought in line with the same `<div>` pattern.
- **Paragraph-break hint (EDITOR-05):** same info-icon-next-to-label + Bootstrap tooltip pattern as Phase 66, same tooltip text ("Supports Markdown formatting. Leave a blank line between paragraphs.").
- **Pre-wrap cleanup:** any `white-space: pre-wrap` CSS/inline-style on a container that switches to `Html.Markdown()` output should be removed in the same edit, mirroring Phase 66's D-06 precedent (Markdown's own block-level HTML supersedes the old plain-text line-preservation hack). Applies to Rewards' `.quest-description-box` inline `style="white-space: pre-wrap;"` (`Quest/Details.cshtml`) and Recap's `.recap-display-box` CSS rule (`quests.css`).

### Manage-page Rewards section
- **D-01:** Add a Rewards section to `Quest/Manage.cshtml`, mirroring the collapsible-section pattern Phase 66 added there for Description (`card-header modern-card-header` + `btn-link` chevron toggle + `collapse`/`collapse show` div, per `_QuestSection.cshtml`'s established structure). Collapsed by default. This is a deliberate small extension beyond ROADMAP's literal "Details/QuestLog" wording for QUESTMD-02 — the user confirmed it's worth doing now to avoid Manage showing Description but not Rewards, which would read as an inconsistency the moment a DM opens the collapsible. Recap does NOT get a Manage-page section — Manage is for active/pending quests, Recap only exists for completed ones (reached via QuestLog, not Quest/Manage).

### Follow-Up mobile Rewards gap
- **D-02:** `CreateFollowUp.Mobile.cshtml` is missing the Rewards textarea entirely (desktop's `CreateFollowUp.cshtml` has it — a Phase 59 asymmetry). Since this phase already touches `CreateFollowUp.cshtml` to wire in the Markdown editor, add the missing Rewards field to `CreateFollowUp.Mobile.cshtml` in the same task — per this project's standing mobile-parity lesson (Phase 43/54/61: backfilling a desktop-only fix onto mobile later has twice required its own dedicated phase). New mobile field starts blank, matching desktop's existing Phase 59 behavior (Follow-Up intentionally never copies Rewards from the original quest).

### Claude's Discretion
- Exact placement/markup of the new Manage-page Rewards collapsible section (D-01) — follow `_QuestSection.cshtml`'s and Phase 66's existing Description-section structure closely; exact class names/IDs are implementation detail.
- Exact markup for the new `CreateFollowUp.Mobile.cshtml` Rewards field (D-02) — mirror the desktop `CreateFollowUp.cshtml` Rewards block's structure/placement, adapted to the mobile form's existing layout conventions.
- Order of implementation (fields first vs. emails first) — no user preference expressed; sequence for planning convenience.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 66 (pattern this phase mechanically repeats)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-CONTEXT.md` — full decision set (D-01 through D-09) for the proof-of-concept this phase repeats: Manage-page collapsible-section pattern (D-01/D-02), toolbar styling (D-03), paragraph-break hint placement/copy (D-08/D-09), pre-wrap removal precedent (D-06)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-07-SUMMARY.md` — the `<div>`-not-`<p>` email wrapper fix, the CSS specificity-bump pattern for any editor/Preview text nested inside `.modern-card`, and the accepted 44px-tall/~30-34px-wide mobile touch-target deviation (still open if this phase's forms hit the same EasyMDE-vs-app CSS cascade tie)

### Milestone Research
- `.planning/research/SUMMARY.md` — architecture approach (Domain service + HtmlHelper extension + `POST /markdown/preview` + Blazor `@inject`), critical pitfalls (pre-wrap CSS removal, email card fixed-height clipping deferred to Phase 71)
- `.planning/research/ARCHITECTURE.md` — HtmlHelper extension vs TagHelper rationale, Blazor component injection pattern (`@inject IMarkdownService`)

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — QUESTMD-02, QUESTMD-03, EMAILMD-01 (this phase's requirements); EMAILMD-02/EMAILMD-03 are Phase 71, not this phase
- `.planning/ROADMAP.md` — Phase 67 goal, success criteria, dependency on Phase 66

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Views/Shared/_MarkdownEditor.cshtml` (Phase 66) — the shared EasyMDE toolbar/preview partial. Drop into Rewards and Recap textareas unchanged.
- `POST /markdown/preview` endpoint (Phase 66) — reused verbatim for Rewards' and Recap's Preview toggle; no new endpoint needed.
- `Html.Markdown()` HtmlHelper extension (Phase 66) — reused verbatim for all read-side rendering in this phase.
- `Views/Quest/_QuestSection.cshtml` and Phase 66's Manage-page Description collapsible block — the exact pattern to copy for the new Rewards section (D-01).

### Established Patterns
- Mobile view resolution is automatic (`MobileDetectionMiddleware` + `MobileViewLocationExpander`) — no controller branching needed for the new mobile Rewards field or any of this phase's view edits.
- Mobile parity must be in the same task as the desktop edit, not a follow-up (D-02; this project's Phase 43/54/61 lesson).

### Integration Points
- **Rewards write forms:** `Views/Quest/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `CreateFollowUp.cshtml` — existing `<textarea asp-for="Rewards">` blocks to wire with the editor partial. `CreateFollowUp.Mobile.cshtml` needs the field added from scratch (D-02).
- **Rewards read views:** `Views/Quest/Details.cshtml:38` (`.quest-description-box`, has inline `pre-wrap` to remove), `Details.Mobile.cshtml`, `Views/QuestLog/Details.cshtml:59` (`.quest-description-box`), `Details.Mobile.cshtml` — swap raw `@Model.Quest.Rewards` for `@Html.Markdown(Model.Quest.Rewards)`. Plus the new Manage.cshtml section (D-01).
- **Recap write form:** `Views/QuestLog/EditRecap.cshtml`, `EditRecap.Mobile.cshtml` — existing `<textarea asp-for="Recap">` (bound via `EditRecapViewModel`) to wire with the editor partial. Both desktop and mobile already exist — no parity gap here.
- **Recap read views:** `Views/QuestLog/Details.cshtml:122` (`.recap-display-box`), `Details.Mobile.cshtml:98` (`.recap-display-box`) — swap raw `@Model.Quest.Recap` for `@Html.Markdown(Model.Quest.Recap)`; remove `.recap-display-box`'s `pre-wrap` CSS rule in `quests.css`. `QuestLog/Index.cshtml` only shows a boolean "Session Recap Available" badge (`quest.Recap` non-empty check) — no text preview to convert, out of scope.
- **Email templates:** `Components/Emails/QuestFinalized.razor` — already correct (Phase 66), no change needed. `Components/Emails/SessionReminder.razor:42` and `Components/Emails/WaitlistPromoted.razor:41` — both currently render `<p>@QuestDescription</p>` (raw, unrendered, wrong tag) and need `@inject IMarkdownService` + the same `<div>@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>` swap QuestFinalized.razor already uses.
- **Neither Rewards nor Recap appear on any board/list card or the poster-selection character-count calculation** (`SelectPosterByContent`) — confirmed by direct read of `Quest/Index.cshtml`/`Index.Mobile.cshtml` and 66-CONTEXT.md's D-07. No plain-text-stripping decision needed for this phase (unlike Description's D-06 on the board card).

</code_context>

<specifics>
## Specific Ideas

No specific visual/copy requests beyond what Phase 66 already locked — the user's two decisions in this discussion (D-01, D-02) were both about *where the mechanical pattern should also reach* (Manage page, Follow-Up mobile form), not about changing how the pattern itself looks or behaves.

</specifics>

<deferred>
## Deferred Ideas

None — both gray areas raised in discussion were resolved as in-scope extensions (D-01, D-02) rather than deferred.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`gsd-sdk query todo.match-phase 67` returned 0 matches).

</deferred>

---

*Phase: 67-remaining-quest-fields-email-templates*
*Context gathered: 2026-07-10*
