# Project Research Summary

**Project:** D&D Quest Board — Milestone v8.0 (Markdown Support)
**Domain:** Server-rendered ASP.NET Core MVC app; retrofitting Markdown authoring + rendering onto 9 existing plain-text fields with real production data, reused across page views and HTML email templates, with zero client-side build tooling
**Researched:** 2026-07-09
**Confidence:** HIGH (stack versions and XSS specifics verified against primary sources — NuGet/npm registry APIs, official GitHub repos, a named security advisory; architecture and pitfalls grounded directly in this codebase's own source)

## Executive Summary

The stack question is settled with high confidence: **Markdig 1.3.2 + HtmlSanitizer (Ganss.Xss) 9.0.892** server-side, **EasyMDE 2.21.0** client-side. All three researchers independently converged on Markdig as the standard .NET CommonMark processor and flagged the same critical gotcha — `Markdig.DisableHtml()` alone is **not** sufficient sanitization, since it blocks raw `<script>` tags but not `javascript:`-scheme URLs in ordinary `[text](url)` link syntax (a real, named vulnerability class: GHSA-gwjh-c548-f787). A second `HtmlSanitizer` pass with a narrow `AllowedSchemes`/`AllowedTags` allowlist is the actual security boundary, and it must be applied identically to all 9 fields — none of them get a "trusted author" exemption, since all are rendered to other group members' browsers and 3 of them also flow into HTML emails.

One architectural question had a real 2-to-1 split among the three researchers, and this summary resolves it: how should the Preview toggle render its output? STACK.md and PITFALLS.md both independently recommend routing Preview through a small server round-trip (`POST /markdown/preview`) that calls the *exact same* rendering service used for final storage/display — guaranteeing byte-identical output by construction. ARCHITECTURE.md proposed a zero-round-trip client parser (`commonmark.js` + DOMPurify) for lower latency. **Resolution: use the server round-trip.** Pitfall 4 makes the deciding argument — even a "spec-matched" client parser is a second, independently-maintained implementation, and this project's own EasyMDE default bundles `marked.js`, which fails ~25% of the official CommonMark spec suite with documented gaps in exactly the constructs D&D prose uses (nested lists, blockquotes). A round-trip at 17 users has no meaningful latency cost and eliminates an entire class of "preview lied to me" bugs by construction rather than by hoping two parsers agree.

The single biggest risk this milestone carries is not the rendering pipeline itself but what happens to **existing production data** and **the 3 email templates**. Switching every read view from Razor's auto-encoding `@Field` to `Html.Raw`/`MarkupString` removes an XSS safety net this codebase has silently relied on since inception — the sanitizing service must be built and proven before any view is touched. Separately, real D&D prose already contains characters that are live CommonMark syntax (mid-word `*asterisks*` for dice notation, leading `#`, `---` dividers, Word-paste indentation) that will silently change *meaning*, not just layout, beyond the already-accepted paragraph-reflow trade-off — a one-off diff script comparing old plain text against rendered output (tags stripped) before each field's rollout is the concrete mitigation. And the 3 HTML email templates were built and CSS-tuned for one short italic paragraph; real Markdown structure (headings, lists, blockquotes) needs its own dedicated, later styling/layout pass verified in actual Outlook and Gmail — not folded into the same phase as the core rendering pipeline.

## Key Findings

### Recommended Stack

Server-side: **Markdig 1.3.2** (Markdown → HTML, pure managed code, no native dependency) paired with **HtmlSanitizer 9.0.892** (`Ganss.Xss`, AngleSharp-based allowlist sanitizer — Markdig is explicitly not a sanitizer and needs this pairing). Both multi-target `net8.0`/`netstandard2.0` and are consumed transparently by .NET 10. Client-side: **EasyMDE 2.21.0**, a maintained fork of the abandoned SimpleMDE, self-contained UMD bundle (327 KB JS + 13 KB CSS) with zero build step — its default toolbar already ships Bold/Italic/Heading/List/Preview, trimmed via a `toolbar: [...]` config array rather than custom-built. EasyMDE's bundled Marked.js parser should be present in the bundle (no way to tree-shake it out without a build step) but never actually used for the real Preview — override the `previewRender` hook (documented to support async) to call the server round-trip endpoint instead.

**Core technologies:**
- **Markdig 1.3.2**: server Markdown→HTML — de facto .NET standard, 67M+ downloads, zero native dependency (unlike the project's paused SkiaSharp plan)
- **HtmlSanitizer 9.0.892**: post-parse HTML sanitization — closes the `javascript:`-scheme gap `DisableHtml()` leaves open; configure `AllowedTags` narrowly (`p, br, strong, em, del, code, pre, h1-h6, ul, ol, li, blockquote, a, hr`) and `AllowedSchemes` to `http, https, mailto` only
- **EasyMDE 2.21.0**: client textarea + toolbar + Preview UI, zero build step, CDN-loaded (matching this repo's actual Cropper.js/Font Awesome precedent — both are CDN-hosted with version pins, not locally vendored files, despite the milestone framing initially assuming otherwise)

### Expected Features

The locked design (textarea + toolbar with Bold/Italic/Heading/List + single Preview toggle, not split-pane) is correct and confirmed by research — EasyMDE explicitly disables side-by-side/fullscreen on mobile via a `no-mobile` class, and Discourse's own UX team independently converged on toggle-not-split. Beyond the locked 4 buttons, research identifies 2 more as genuine table stakes and a clear line for everything else:

**Must have (table stakes, beyond the locked Bold/Italic/Heading/List):**
- **Link button** — `[text](url)` selection-wrap, no modal needed; every general-purpose toolbar surveyed includes it, and D&D players will link D&D Beyond sheets/wikis
- **Blockquote button** — reuses the same prefix-insertion mechanism Heading/List already need (near-zero marginal cost); genuinely useful for NPC dialogue and read-aloud text
- **Preview toggle disables the rest of the toolbar while active** (EasyMDE's `no-disable` pattern) — a required behavior, not a button
- **44px+ icon-only touch targets on mobile** — this app already enforces this convention (`mobile.css`); the recommended 7-control set (Bold/Italic/Heading/List/Link/Blockquote/Preview) fits one row on a 320–390px viewport with no overflow mechanism needed
- **One inline hint about the strict-CommonMark paragraph rule** ("Press Enter twice for a new paragraph") — the one genuine behavior-change surprise toolbar buttons alone won't prevent

**Should have (defer to P2/P3, cheap to add later if requested):**
- Strikethrough — only if the chosen parser has GFM strikethrough at no extra wiring cost
- Horizontal rule — trivial, but strict CommonMark's paragraph spacing already reduces the need
- Cheatsheet link/popover — only if users report confusion beyond the paragraph-break hint

**Explicitly out of scope (anti-features for this domain, not just deferred):** inline code/code blocks (no dice-notation/code use case), image embed (duplicates the existing Cropper.js photo pipeline), tables (painful on mobile textareas), @mention/#ref (no data-model analog), task list/checkboxes (interactive checkboxes have no equivalent in static HTML email — a real architectural mismatch, not just added complexity), fullscreen mode (none of the 9 fields are long-form documents).

### Architecture Approach

One Domain-layer service owns the actual Markdig+HtmlSanitizer algorithm (mirrors the existing `IImageValidationService` precedent — stateless, pure `string? → string`, unit-testable, zero Repository dependency), wrapped by two thin per-pipeline adapters: an `IHtmlHelper` extension method for `.cshtml` views (following the `ControllerExtensions` convention this codebase already uses — no TagHelpers exist here today, so a HtmlHelper extension is the smaller, idiomatic fit), and direct `@inject` + `MarkupString` in the 3 email `.razor` components. Because the service is stateless, it needs **no** `IServiceScopeFactory`/`HangfireJobHelper` bridging unlike every other scoped Hangfire-job service — ordinary DI resolves it in both the request-scoped MVC pipeline and the Hangfire background-job pipeline. No schema/migration/Repository changes are needed anywhere; all 9 fields already exist as plain string columns.

**Major components:**
1. `IMarkdownService`/`MarkdownService` (Domain layer) — the single Markdig+HtmlSanitizer pipeline, registered as a singleton (a deliberate one-line deviation from this codebase's usual `AddScoped` convention, justified because it's stateless and the pipeline object is meant to be built once and reused)
2. `HtmlHelperExtensions.Markdown()` (Service layer) — read-side MVC adapter, one line per call site (`@Html.Markdown(Model.X)`), replacing the current `<p style="white-space:pre-wrap">@Model.X</p>` pattern at all 9 read call sites
3. `Views/Shared/_MarkdownEditor.cshtml` + `wwwroot/js/markdown-editor.js` — write-side shared partial + script generalizing the existing single-feature `_QuestFormScripts.cshtml` pattern across all 5 features/9 fields, desktop and mobile share the exact same partial (no mobile-specific reimplementation needed)
4. A small `POST /markdown/preview` endpoint — authenticated, antiforgery-protected, no DB access, calls the same `IMarkdownService` for the AJAX-round-trip Preview (resolves the 2-to-1 architecture disagreement above in favor of correctness-by-construction)
5. The 3 email components (`QuestFinalized`, `SessionReminder`, `WaitlistPromoted`) — `@inject IMarkdownService`, swap `@QuestDescription` for `@((MarkupString)MarkdownService.RenderToHtml(QuestDescription))`

### Critical Pitfalls

1. **Switching to `Html.Raw`/`MarkupString` removes an XSS safety net that has silently protected all 9 fields since inception** — build and unit-test the sanitizing service (feeding `<script>`, `javascript:` links, `<img onerror>`) *before* any view is touched; apply uniformly to all 9 fields, no "trusted author" exemptions.
2. **Sanitized HTML is not automatically email-safe HTML** — Outlook's Word rendering engine doesn't show `<ul>`/`<li>` bullets without proprietary MSO CSS, and Gmail strips `<style>` blocks (this app's existing email convention already inlines every style for exactly this reason). Needs its own dedicated, later phase with real Outlook/Gmail verification — not folded into the core rendering-service phase.
3. **The 3 email templates' `height:840px; overflow:hidden` description card was tuned for one short paragraph** — real Markdown structure (heading + list + blockquote) can silently clip in Outlook, which doesn't honor `overflow-y:auto`. This is a visual-design decision (truncate-with-link vs. remove fixed height) that needs explicit user input, not an engineering default.
4. **Client and server Markdown parsers will disagree unless it's literally the same renderer** — resolved above by using the AJAX round-trip instead of a second JS parser.
5. **Existing casual D&D prose contains characters that are live CommonMark syntax with side effects beyond the accepted paragraph-reflow** — mid-word `*asterisks*` (dice notation like `2*4`), leading `#`, `---` dividers colliding with setext headings, Word-paste indentation becoming code blocks. Mitigate with a one-off diff script (rendered-text-stripped vs. original) run per field before its rollout, flagging content-altering cases distinct from harmless reflow.

Two more pitfalls worth carrying into planning: Phase 64's `white-space: pre-wrap` CSS (added specifically for several of these exact fields) must be removed from *rendered-output* containers (not editor textareas) as a companion edit per field migration, or real Markdown HTML will double-space. And Contact Notes must render each note independently through the shared service (never concatenated) so one author's unclosed formatting marker can't bleed into another author's note.

## Implications for Roadmap

Based on research, suggested phase structure (numbering continues from Phase 64 — the roadmapper assigns actual numbers):

### Phase: Markdown Rendering Foundation
**Rationale:** Everything else depends on one correctly-built, well-tested sanitizing service; building it in isolation first makes it fully unit-testable (Domain layer) with zero user-visible risk, matching this project's own TDD-friendly phase pattern (e.g. Phase 61).
**Delivers:** `IMarkdownService` (Markdig + HtmlSanitizer, singleton), NuGet refs, unit tests covering XSS payloads (script tags, `javascript:` links, generic-attribute injection) and CommonMark edge cases (mid-word asterisks, ATX/setext heading collisions, indented code blocks), `HtmlHelperExtensions.Markdown()`, `.markdown-content` CSS class, `_ViewImports.cshtml` using-line. Nothing visibly changes yet.
**Addresses:** the shared-rendering-pipeline requirement (FEATURES.md's #1 dependency flag)
**Avoids:** Pitfall 1 (XSS regression)

### Phase: Quest Description End-to-End Proof-of-Concept
**Rationale:** Quest Description is the one field that already flows into an email template today — proving both adapters (MVC helper + Blazor component) and the full write→read→email loop in one phase retires the milestone's flagged cross-cutting risk early rather than deferring it.
**Delivers:** `_MarkdownEditor.cshtml` + `markdown-editor.js` (EasyMDE, 6-button toolbar + Preview toggle wired to the new `POST /markdown/preview` endpoint), wired into `Quest/Create(.Mobile)` + `Quest/Edit(.Mobile)`; Quest board card, `Quest/Details`, `Quest/Manage` swapped to `Html.Markdown()`; `QuestFinalized.razor` updated to the inject/MarkupString pattern (functional wiring only — email-safe styling is a later phase, see below).
**Uses:** EasyMDE 2.21.0, the round-trip preview endpoint
**Implements:** the write-side and read-side adapter pattern from ARCHITECTURE.md

### Phase: Remaining Quest Fields + Remaining Email Templates
**Rationale:** Mechanical reuse of the now-proven pattern — low risk.
**Delivers:** Rewards + Recap wired the same way (`Quest/Details`, `QuestLog/Details`, `QuestLog/EditRecap(.Mobile)`); `SessionReminder.razor` and `WaitlistPromoted.razor` get the identical one-line adapter change already validated.

### Phase: Character Fields
**Delivers:** Description + Backstory migrated (Create/Edit/Details, desktop+mobile), paired with the Phase 64 `pre-wrap` CSS audit and an old-data diff sweep for this field group specifically.

### Phase: Contact Fields
**Delivers:** Description + collaborative Notes migrated, with the per-note independent-rendering constraint (Pitfall 7) explicit in the plan, plus its own `pre-wrap` audit and diff sweep.

### Phase: DM Profile Bio + Shop Item Description
**Delivers:** The remaining 2 fields, mechanical reuse, `pre-wrap` audit + diff sweep.

### Phase: Email-Safety Hardening
**Rationale:** Deliberately sequenced last, after the mechanism is proven working end-to-end in earlier phases — this phase is about how Markdown-generated HTML *looks* in real email clients, which is a visual-design decision needing user input, not a mechanical follow-on.
**Delivers:** Inline `style=` attributes on every block element Markdig can produce (matching `_EmailLayout.razor`'s existing Georgia-serif/inline-style convention), MSO conditional-comment bullet fix for lists, a resolved fixed-height-card design decision (truncate + "View full quest" link vs. remove the fixed height), verified by actually opening sent test emails in real Outlook desktop and Gmail webmail with Markdown-structured (not single-paragraph) test content — not just a browser preview.

### Phase Ordering Rationale

- Foundation-first isolates all XSS/sanitization risk into one unit-testable phase before any view changes
- Quest Description proof-of-concept proves the hardest cross-cutting case (email) early, so remaining phases are genuinely mechanical
- Field-migration phases are grouped by feature (matching this project's usual phase-sizing convention) and each carries its own pre-wrap audit + diff sweep as a companion task, not a follow-up
- Email styling is deliberately the *last* phase, not bundled into the proof-of-concept, because Pitfalls 2/3 are real visual-design decisions requiring live Outlook/Gmail verification — bundling it early would block the mechanical field-migration phases on a slower, human-verification-heavy task

### Research Flags

Phases likely needing deeper research/decisions during planning:
- **Foundation phase:** exact Markdig extension set beyond base CommonMark (autolinks confirmed useful; anything beyond that should stay minimal per the `UseAdvancedExtensions()`/`UseGenericAttributes()` attribute-injection gotcha)
- **Email-safety phase:** the fixed-height card redesign is a genuine design decision — flag for discuss-phase or a live checkpoint with the user, not resolved by an engineer alone

Phases with standard patterns (skip research-phase):
- **Quest Description proof-of-concept and all subsequent field-migration phases:** the pattern is proven once and mechanically repeated — no new research needed per field

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Versions verified via NuGet/npm registry APIs and official GitHub source, not training-data recall |
| Features | MEDIUM-HIGH | Toolbar button sets verified against official library sources; one GitHub UI convention cited from general product familiarity rather than a freshly-pulled source |
| Architecture | HIGH (integration points) / MEDIUM (XSS specifics) | Integration points verified directly against this codebase's source; XSS specifics WebSearch-verified against official docs (no Context7 available this session) |
| Pitfalls | HIGH | Grounded directly in this codebase's actual email templates, entity schemas, and CSS; external claims corroborated by the CommonMark spec and multiple independent sources |

**Overall confidence:** HIGH

### Gaps to Address

- **Preview mechanism:** this summary resolves the 2-to-1 researcher split in favor of the AJAX round-trip — confirm this during requirements/roadmap rather than treating it as still-open
- **Client-side library delivery (CDN vs. vendored):** STACK.md initially assumed local vendoring; direct codebase verification during this session confirmed the actual precedent is CDN + SRI (Cropper.js) — use that same pattern for EasyMDE, pinned to an exact version
- **Outlook/Gmail email rendering claims:** corroborated by community sources but not verified against this project's actual `_EmailLayout.razor` output — budget real send-and-open verification time in the email-safety phase itself, not assumed from research alone
- **Old-data diff sweep tooling:** no existing precedent in this codebase for a one-off data-comparison script — build it as a small one-time console/test utility during the first field-migration phase, reuse for the rest

## Sources

### Primary (HIGH confidence)
- https://api.nuget.org/v3-flatcontainer/markdig/index.json and /htmlsanitizer/index.json — NuGet v3 API, confirmed latest stable versions
- https://registry.npmjs.org/easymde/latest — npm registry API, confirmed EasyMDE 2.21.0 and bundled dependencies
- https://github.com/xoofx/markdig, https://github.com/mganss/HtmlSanitizer, https://github.com/Ionaru/easy-markdown-editor — official repos, confirmed APIs/behavior
- https://github.com/NuGet/NuGetGallery/security/advisories/GHSA-gwjh-c548-f787 — real-world confirmed `javascript:`-scheme Markdown XSS vulnerability class
- https://spec.commonmark.org/0.31.2/ — official CommonMark spec, confirmed edge-case rules (delimiter-flanking, ATX/setext headings, indented code blocks)
- Direct repository inspection — `QuestBoard.Repository/Entities/*.cs`, `QuestBoard.Service/Components/Emails/*.razor`, `QuestBoard.Service/Views/Shared/_Layout*.cshtml`, repo-wide `pre-wrap` grep — confirmed actual current architecture, CDN-vendoring precedent, and email template structure firsthand

### Secondary (MEDIUM confidence)
- https://weblog.west-wind.com/posts/2018/Aug/31/Markdown-and-Cross-Site-Scripting — Rick Strahl, corroborates the `DisableHtml()` limitation
- https://github.com/markedjs/marked/discussions/1202 — maintainer-acknowledged CommonMark compliance gap (157/624 failing tests) in EasyMDE's bundled parser
- https://meta.discourse.org/t/mobile-editor-preview-button-and-toolbar/113942 — Discourse's own team's mobile preview/toolbar UX debate
- Email client CSS/list-support community sources (GetResponse, Litmus community) — corroborated across multiple independent sources on Outlook's `<ul>`/`<li>` limitations

### Tertiary (LOW confidence)
- GitHub's "Markdown is supported" compose-box hint convention — cited from general product familiarity, no fresh citable source found this session; treated as a well-known convention, not a load-bearing claim

---
*Research completed: 2026-07-09*
*Ready for roadmap: yes*
