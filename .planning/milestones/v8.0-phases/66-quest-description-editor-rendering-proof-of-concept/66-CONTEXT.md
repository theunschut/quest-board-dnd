# Phase 66: Quest Description Editor & Rendering (Proof-of-Concept) - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire the shared Markdown editor (EasyMDE, calling Phase 65's `IMarkdownService`) into the Quest Description field end-to-end: a formatting toolbar + Preview toggle on the Create/Edit/Follow-Up forms (desktop and mobile), and formatted-HTML rendering everywhere Description is displayed — the desktop quest board card, Quest Details, Quest Manage, and the Quest Finalized email. This is the milestone's proof-of-concept phase: it proves the full write→read→email loop once, so Phases 67–70 can mechanically repeat the pattern on the remaining 8 fields. EDITOR-01 through EDITOR-06 and QUESTMD-01 only — no new capabilities beyond what's already scoped by ROADMAP.md.

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research level (not re-discussed, carried forward)
- **Toolbar buttons:** Bold, Italic, Heading, List, Link, Blockquote (6 buttons) + Preview toggle — confirmed by `.planning/research/SUMMARY.md` and REQUIREMENTS.md EDITOR-01.
- **Preview mechanism:** server round-trip (`POST /markdown/preview`, calls the same `IMarkdownService` used for final storage/display) — NOT a second client-side parser. Resolves a real 2-to-1 researcher split; see `.planning/research/SUMMARY.md` for the reasoning (EasyMDE's bundled `marked.js` fails ~25% of the CommonMark spec suite).
- **Preview disables the rest of the toolbar while active** (EasyMDE's `no-disable` pattern) — EDITOR-03.
- **Client library:** EasyMDE 2.21.0, CDN-loaded with SRI (matching the Cropper.js precedent — see `<code_context>` below), zero build step.
- **44px+ icon-only touch targets, one row, no overflow/scroll mechanism** on mobile — EDITOR-06.
- **`IMarkdownService.RenderToHtml(string? markdown, MarkdownRenderTarget target)`** already exists (Phase 65 shipped) — `MarkdownRenderTarget.Web` for page views (keeps `<img>`), `MarkdownRenderTarget.Email` for the email template (strips `<img>`). No further Domain-layer work needed to call it.

### Manage page placement
- **D-01:** Quest Description does not currently appear anywhere on `/Quest/Manage/{id}` (`QuestController.Manage`, `Views/Quest/Manage.cshtml` + `Manage.Mobile.cshtml`) — confirmed by direct grep, zero references, no partial renders it. This is genuinely new surface area, not a modification of existing markup.
- **D-02:** Description renders inside a **collapsible section**, reusing the existing Bootstrap-collapse pattern already established in `Views/Quest/_QuestSection.cshtml` (a `card-header modern-card-header` with a `btn-link` toggle button containing an icon + `fas fa-chevron-down`, targeting a `collapse`/`collapse show` div). **Collapsed by default on both desktop and mobile** — consistent behavior across breakpoints, since Manage is action-focused (votes, dates, finalize) and Description is secondary context there.

### Toolbar visual styling
- **D-03:** Keep EasyMDE's **default out-of-the-box toolbar appearance** — do not restyle it to match this app's filled-colored-button/FontAwesome 6 convention (CLAUDE.md's UI/UX guidelines apply to the app's own card/button patterns, not to this third-party editor widget). No custom toolbar CSS skin planned for this phase.
- **Note for planner:** EasyMDE's default toolbar icons use FA4-era class names (e.g. `fa fa-bold`). This app loads FontAwesome 6 (`_Layout.cshtml`/`_Layout.Mobile.cshtml`, cdnjs 6.4.0) with no v4-shim loaded. Verify the default icons actually render before treating this as done — if they don't, the minimal fix is adding FA6's v4-shim stylesheet (not restyling), since the decision above is to keep the default look, not redesign it.

### Board card rendering (desktop board only)
- **D-04:** The live desktop board card is `Views/Quest/Index.cshtml`'s `.fantasy-quest-card` (NOT `.modern-card .card-text` / `_QuestCard.cshtml`, which is dead code — no controller or view renders that partial chain; confirmed by search). Inside `.fantasy-quest-card`, `.quest-description` is `flex-grow: 1; overflow-y: auto` with a custom scrollbar (site.css:854, quests.css:178) — not a fixed-height box.
- **D-05:** The **mobile board list** (`Views/Quest/Index.Mobile.cshtml`, class `.quest-card-mobile`) does not show Description at all today, on any device — it's a compact list (title, DM name, status badge, CR, finalized date). **Stays as-is** — Description is not added to the mobile board list in this phase. Description remains reachable via Details/Manage on mobile.
- **D-06 (important — overrides an earlier answer given mid-discussion):** The desktop board card shows Description as **plain text only** — Markdown syntax stripped (headings/bold/lists collapse to unstyled text), matching today's visual behavior. This is produced by post-processing the already-rendered HTML (strip tags/entities) rather than a second Markdown parser, keeping this consistent with Phase 65's single-parse architecture (RENDER-02). Full rendered HTML (headings, lists, bold, blockquotes styled) is used on Details, Manage, and the email — **only the board card gets the plain-text treatment**.
- **D-07:** `SelectPosterByContent()` (`Views/Quest/Index.cshtml:32-53`) picks the card's poster image — and therefore its displayed height, via `wwwroot/js/site.js:88-102`'s aspect-ratio calculation — from `title.Length + description.Length` (raw character count): ≤130→Poster4 (ratio 0.98), ≤250→Poster5 (1.125), 251-499→random between Poster1/Poster6 (1.4/1.33), ≥500→Poster3 (2.33, uncapped ceiling). **Because D-06 means the card displays the same stripped plain text used for this length calculation, no threshold rework is needed** — `SelectPosterByContent` should measure the same stripped plain-text length that D-06 produces for card display, which keeps today's calibration intact by construction (the Markdown-syntax-inflation problem that motivated this whole sub-discussion is resolved as a side effect of D-06, not by changing the 130/250/500 numbers).
- **Context (not a requirement, informational):** the user separately flagged that even before Markdown, the ≥500 "always Poster3" bucket felt too eager ("almost always resulting in the biggest image being chosen") and that a scrollbar in a smaller poster is an acceptable trade-off. No threshold change was requested for this phase (D-07 makes it moot for Markdown's sake specifically), but if the planner/user wants to revisit the 130/250/500 calibration itself later, that's a separate, pre-existing tuning concern — not blocking for Phase 66.

### Paragraph-break hint
- **D-08:** The hint (EDITOR-05) is an info-circle icon (FontAwesome) placed next to the field's existing label (e.g. next to "Description"), **not** in the toolbar and **not** a permanent caption under the textarea. Shows a Bootstrap tooltip on hover (desktop) / tap (mobile).
- **D-09:** Tooltip text: **"Supports Markdown formatting. Leave a blank line between paragraphs."**
- **Note for planner:** this app does not currently use Bootstrap tooltips anywhere (`data-bs-toggle="tooltip"` — zero matches repo-wide). Bootstrap's JS bundle is already loaded (used for the `_QuestSection.cshtml` collapse component), but tooltips need an explicit one-time JS init call (`new bootstrap.Tooltip(...)` per element or a `document.querySelectorAll('[data-bs-toggle="tooltip"]')` loop) — this is new wiring, not zero-cost.

### Claude's Discretion
- Exact placement/markup of the `POST /markdown/preview` endpoint — there is **no existing AJAX + antiforgery-token precedent anywhere in this codebase** (confirmed: zero `fetch`/`.ajax`/`XMLHttpRequest` calls in any `.js` file, and even the Cropper.js crop pipeline rides along on a normal form POST via a hidden input rather than AJAX). This phase introduces the pattern for the first time — controller placement (new `MarkdownController` vs. an action on `QuestController`), route shape, and response format are implementation details for planning/research, not user decisions.
- Whether `_QuestFormScripts.cshtml` (currently dates-only JS, included by Create/Edit but NOT by CreateFollowUp) gets generalized to also wire EasyMDE, or whether a separate shared partial is introduced — and how CreateFollowUp(.Mobile) picks up the same editor wiring it's currently missing entirely (QUESTMD-01 explicitly requires Follow-Up forms to have the editor too).
- Removing the Phase 64 `white-space: pre-wrap` CSS from *rendered-output* containers for Description specifically (`Details.cshtml` has it; `Details.Mobile.cshtml` and `_QuestCard.cshtml` do not) as a companion edit — mechanical, follows the same pattern later phases (68/69/70) are already scoped to repeat per-field.
- Exact HTML markup/CSS for the collapsible Manage section (D-02) — follow `_QuestSection.cshtml`'s existing structure closely; exact class names/IDs are implementation detail.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 65 (foundation this phase builds on)
- `.planning/phases/65-markdown-rendering-foundation/65-CONTEXT.md` — D-01 through D-08 decisions (full GFM extension set, dual sanitizer profiles, image handling)
- `.planning/phases/65-markdown-rendering-foundation/65-01-SUMMARY.md` — actual shipped interface: `IMarkdownService.RenderToHtml(string?, MarkdownRenderTarget)`, `MarkdownRenderTarget.Web`/`.Email`, registered as `AddSingleton` in `QuestBoard.Domain/Extensions/ServiceExtensions.cs`

### Milestone Research
- `.planning/research/SUMMARY.md` — resolves the Preview-mechanism split (server round-trip, not a second parser), locked toolbar button set, architecture approach (Domain service + HtmlHelper extension + `POST /markdown/preview` + Blazor `@inject`), critical pitfalls (pre-wrap CSS removal, per-note independent rendering for a later phase, email card fixed-height clipping deferred to Phase 71)
- `.planning/research/STACK.md` — Markdig/HtmlSanitizer/EasyMDE exact versions and configuration
- `.planning/research/ARCHITECTURE.md` — HtmlHelper extension vs TagHelper rationale, Blazor component injection pattern
- `.planning/research/FEATURES.md` — full toolbar button rationale, must-have vs should-have vs out-of-scope feature lines

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — EDITOR-01 through EDITOR-06, QUESTMD-01, Out of Scope table (image embed / table / task-list / fullscreen exclusions apply to the toolbar, not the underlying parser)
- `.planning/ROADMAP.md` — Phase 66 goal, success criteria, dependency chain (Phase 66 is itself the dependency every later field-migration phase builds on)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IMarkdownService`/`MarkdownService` (`QuestBoard.Domain`) — already registered, already unit-tested (261/261 green). Call directly, no Domain-layer changes needed.
- `Views/Quest/_QuestSection.cshtml` — the existing Bootstrap-collapse pattern to reuse for the Manage-page Description section (D-02): card-header button + chevron icon + `collapse`/`collapse show` toggle div.
- `Views/Quest/_QuestFormScripts.cshtml` — existing shared partial for quest-form JS (currently dates-only), included by `Create(.Mobile)` and `Edit(.Mobile)` but **not** `CreateFollowUp(.Mobile)`. Candidate to generalize for EasyMDE wiring (see Claude's Discretion).
- Cropper.js CDN+SRI pattern (e.g. `Views/Characters/Create.cshtml:234-236`) — the precedent to mirror for loading EasyMDE: per-view `<script>` tag with `integrity`/`crossorigin` attributes, not a global `_Layout.cshtml` include (Bootstrap/FontAwesome/jQuery in the layout carry no SRI today — Cropper.js is the only precedent that does).

### Established Patterns
- Mobile view resolution: `MobileDetectionMiddleware` sets `context.Items["IsMobile"]` from User-Agent; `MobileViewLocationExpander` (an `IViewLocationExpander`) transparently prefers `*.Mobile.cshtml` when present. Controllers never branch on mobile explicitly — `return View(...)` is enough. Any new Markdown-editor partial should work when included from both the desktop and Mobile view without controller changes.
- No AJAX/antiforgery-JSON convention exists anywhere in this codebase today (see Claude's Discretion) — the `POST /markdown/preview` endpoint is a first-of-its-kind pattern here.

### Integration Points
- Form fields to wire: `Views/Quest/Create.cshtml:28-32`, `Create.Mobile.cshtml:35-42`, `Edit.cshtml:30-34`, `Edit.Mobile.cshtml:44-48`, `CreateFollowUp.cshtml:34-38`, `CreateFollowUp.Mobile.cshtml:39-43` — all currently plain `<textarea asp-for="...Description">`.
- Read views to swap to `Html.Markdown()`: `Views/Quest/Details.cshtml:31` (has `pre-wrap`, needs removal), `Details.Mobile.cshtml:97-103` (no `pre-wrap`), `Views/Quest/Index.cshtml` `.fantasy-quest-card`/`.quest-description` block (plain-text treatment per D-06), and the new Manage-page collapsible section (D-02).
- Email: `QuestBoard.Service/Components/Emails/QuestFinalized.razor:41` — currently plain `@QuestDescription` (HTML-encoded). No `@inject` exists anywhere in `Components/Emails/` yet — first one to add. `RazorEmailRenderService.RenderAsync` constructs `HtmlRenderer` from the real app `IServiceProvider`, so `@inject IMarkdownService` will resolve correctly.
- `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs:52` passes `questDescription` as a raw string parameter today — no rendering applied anywhere in that path yet.

</code_context>

<specifics>
## Specific Ideas

The user actively drove the Board card rendering decision toward a materially different (and better-reasoned) outcome than the original framing: initial scouting had incorrectly pointed at dead code (`.modern-card .card-text`), and mid-discussion the user's domain knowledge (the poster-selection-by-description-length mechanism they originally built) surfaced a real interaction between Markdown's character overhead and an existing content-driven visual feature. The resolution (D-06/D-07: strip Markdown to plain text for card display, and let that same plain text feed the existing length calculation) is a "fix by construction" — it avoids a threshold-tuning exercise the user had mixed feelings about starting mid-phase, while still being open to revisiting the underlying calibration later if wanted.

The user also proactively proposed the info-icon-next-to-label pattern for the paragraph-break hint (D-08) rather than picking from either originally offered option — a better fit for this app's existing "label above every textbox" convention than either presented alternative.

</specifics>

<deferred>
## Deferred Ideas

- **Poster-selection threshold recalibration** (the 130/250/500 character-count bucket boundaries in `SelectPosterByContent`) — the user noted the ≥500 "always Poster3" bucket already feels too eager even independent of Markdown, and a scrollbar in a smaller poster would be an acceptable trade-off. Not addressed in Phase 66 (D-07 sidesteps the Markdown-specific version of this problem without touching the numbers). Worth a dedicated look in a future phase if it's still bothering the user after Phase 66 ships.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`gsd-sdk query todo.match-phase 66` returned 0 matches).

</deferred>

---

*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Context gathered: 2026-07-09*
