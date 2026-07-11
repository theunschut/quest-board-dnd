# Phase 65: Markdown Rendering Foundation - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning

<domain>
## Phase Boundary

A single, secure, unit-tested Markdown-to-HTML rendering pipeline that every later v8.0 phase reuses for both page views and the 3 HTML email templates. No user-facing views change in this phase — it is pure plumbing (Domain-layer service + NuGet packages + unit tests). RENDER-01, RENDER-02, RENDER-03 only.

</domain>

<decisions>
## Implementation Decisions

### Markdown Extension Set

- **D-01:** Enable **full CommonMark + GFM parsing support** at the pipeline level: autolinks, strikethrough, pipe tables, task lists, definition lists, footnotes. This is a deliberate correction from the milestone research's original "keep minimal, cherry-pick only autolinks" recommendation — the user explicitly wants full Markdown expressiveness available to anyone who knows the syntax, even where the toolbar doesn't promote a button for it.
- **D-02 [informational]:** The toolbar UI stays **curated** to what's genuinely useful for this app's domain (already locked at the milestone level: Bold/Italic/Heading/List/Link/Blockquote, plus Strikethrough per D-03) — the parser supporting more syntax than the toolbar exposes is intentional, not an oversight. Power users get full expressiveness; casual users get a focused button set. Not tracked as a Phase 65 plan decision — the toolbar itself is Phase 66's scope; this phase only needs to not contradict it (which the full parser support in D-01 satisfies).
- **D-03:** Strikethrough specifically is enabled now (not deferred) — REQUIREMENTS.md's EDITOR-07 (v2) was conditional on "cheap to add," and it is (one Markdig extension call, `del` tag already needed in the sanitizer allowlist). The toolbar button for it can still ship later as a pure front-end addition with zero backend rework.
- **D-04 (CRITICAL — security):** Enabling "full" GFM support must NOT be implemented via Markdig's blanket `.UseAdvancedExtensions()` bundle. That bundle pulls in `.UseGenericAttributes()`, which lets Markdown syntax (`{onmouseover="..."}`) inject arbitrary HTML attributes including event handlers — an attribute-injection XSS vector that `.DisableHtml()` does not block (documented in `.planning/research/STACK.md`'s "What NOT to Use" table). The full extension set must be composed by enabling each desired extension individually (pipe tables, task lists, definition lists, footnotes, strikethrough, autolinks) while explicitly excluding generic attributes.
- **D-05:** Task list checkboxes render as Markdig's default **static/disabled `<input type="checkbox" disabled>`** — not interactive. This actually resolves a concern raised in `.planning/research/FEATURES.md` (which recommended skipping task lists entirely due to an assumed "interactive checkbox has no email equivalent" mismatch) — since Markdig's default rendering is already non-interactive, the same static HTML renders identically in the web app and in email with no architectural mismatch. The sanitizer allowlist must permit this specific narrow `<input>` shape (type=checkbox, disabled attribute) without opening up form/input elements generally.

### Image Handling

- **D-06:** Markdown image syntax (`![alt](url)`) renders a real `<img>` tag in the **web app** — consistent with the "full Markdown support" decision (D-01). Confirmed this does not conflict with the existing Cropper.js upload pipeline: Cropper.js stores photo bytes in the database for specific structured portrait fields (Character/Contact/DM Profile), a completely separate mechanism from these 9 free-text fields. A Markdown image reference is an external URL evaluated at render time inside a free-text field; it never touches the database or the portrait-upload flow.
- **D-07 (CRITICAL — architecture):** Images are **stripped specifically in the email-rendering path**. The rendering service needs **two sanitizer profiles sharing one Markdig parser**: a full profile (allows `<img>`) for `Html.Markdown()` / page views, and a stricter "email-safe" profile (excludes `<img>`) for the 3 email `.razor` components (`QuestFinalized`, `SessionReminder`, `WaitlistPromoted`). This does **not** violate RENDER-02's "single shared pipeline" requirement — sanitization is a policy step applied after parsing, not duplicated parsing logic; both profiles consume the exact same Markdig HTML output. Rationale: emails carry tracking-pixel/hotlinking risk specific to that channel, and most email clients block external images by default anyway (unreliable there regardless of whether it's technically allowed).
- **D-08:** This email-image-stripping distinction is a natural precursor to Phase 71 (Email-Safety Hardening), which already needs its own email-specific rendering/styling pass — implementing the two-sanitizer-profile split in Phase 65 gives Phase 71 a seam to build on rather than retrofitting it later.

### Claude's Discretion

- Exact service/interface naming (`IMarkdownService` vs `IMarkdownRenderer` vs similar) — research used slightly different names across STACK.md/ARCHITECTURE.md/PITFALLS.md; not user-facing, pick one consistent name during planning.
- Exact HtmlSanitizer `AllowedTags`/`AllowedAttributes` list needed to support the expanded D-01 extension set (tables need `table/thead/tbody/tr/th/td`; definition lists need `dl/dt/dd`; footnotes need `sup` + footnote-ref anchors + a footnotes `<ol>/<li>` section; task lists need the narrow `input[type=checkbox][disabled]` allowance per D-05) — this needs careful, verified configuration during research/planning, not guessed. Get the exact HTML shape each Markdig extension produces before finalizing the allowlist.
- Whether to guard against pathologically large render input (defensive sizing) — not raised as a product decision; a reasonable technical default if the researcher/planner judges it worth adding, not required by any locked requirement.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone Research (primary source for this phase)
- `.planning/research/STACK.md` — Markdig 1.3.2 + HtmlSanitizer 9.0.892 versions, pipeline configuration, the `.UseAdvancedExtensions()` attribute-injection gotcha (directly relevant to D-04), `javascript:`-scheme link gotcha, cross-surface reuse pattern for `.cshtml` + `.razor`
- `.planning/research/ARCHITECTURE.md` — Domain-layer service placement (mirrors `IImageValidationService` precedent), HtmlHelper extension vs TagHelper rationale, singleton registration rationale, why no `IServiceScopeFactory` bridging is needed, suggested build order
- `.planning/research/PITFALLS.md` — Pitfall 1 (XSS regression from removing Razor's auto-encoding), Pitfall 5 (existing-data character-level corruption), sanitizer allowlist starting point
- `.planning/research/SUMMARY.md` — reconciles the stack/architecture/pitfalls research into one recommendation, including the resolved AJAX-round-trip-preview decision (relevant context for Phase 66, not this phase, but explains why Phase 65's service needs to be cleanly callable from a controller endpoint too)

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — RENDER-01/02/03 (this phase's scope), EDITOR-07 (v2 Strikethrough, now resolved by D-03), Out of Scope table (Image embed toolbar button reasoning — distinct from D-06/D-07's parser-level image handling decision)
- `.planning/ROADMAP.md` — Phase 65 goal/success criteria, dependency chain (Phase 66 depends on this phase)

### Codebase Maps
- `.planning/codebase/ARCHITECTURE.md` — 3-layer dependency rule (Service → Domain → Repository), Hangfire scoped-DI pattern (`HangfireJobHelper`), confirms this phase's service needs no scope-factory bridging since it will be stateless
- `.planning/codebase/STACK.md` — confirms .NET 10 / NuGet package compatibility shape

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IImageValidationService` / `ImageValidationService` (Domain layer, from v7.0 Phase 45) — the direct precedent to mirror: stateless, pure-transformation, zero Repository dependency, unit-testable in isolation
- `ServiceExtensions.AddDomainServices` (Domain layer) — where the new service gets registered; note the file's otherwise-uniform `AddScoped` convention — this phase's registration is a deliberate one-line `AddSingleton` deviation, justified because the service is stateless

### Established Patterns
- `ControllerExtensions.cs` (Service/Extensions) — the existing "shared static extension class" convention this phase's `HtmlHelperExtensions.Markdown()` should follow (a future phase's concern, but the Domain service built in this phase must expose a method shape that adapter can call cleanly)
- `HangfireJobHelper` / `IServiceScopeFactory` pattern used by every existing Hangfire-job scoped service — explicitly NOT needed for this phase's service, since it has no group-scoped state (confirmed by ARCHITECTURE.md's research)

### Integration Points
- `QuestBoard.Domain.csproj` — where the Markdig + HtmlSanitizer NuGet package references get added
- No `QuestBoard.Repository` changes — this service takes `string?` in, returns `string` out; no entities, no DbContext

</code_context>

<specifics>
## Specific Ideas

The user wants genuine full Markdown/CommonMark+GFM parsing support (not a minimal cherry-picked subset), explicitly reasoning that the toolbar UI and the parser's actual capability are two separate concerns — the toolbar can stay narrow and curated for this app's casual D&D use case while the underlying parser handles anything a user who knows raw Markdown syntax might type. This is a deliberate expansion beyond the milestone research's original "keep minimal" recommendation, made with full awareness of the tradeoffs (raised and walked through: extension set scope, the `UseAdvancedExtensions()` XSS gotcha, image-specific risk in the email channel).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within Phase 65's scope (the two gray areas raised both concerned this phase's core pipeline configuration, not new capabilities).

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`gsd-sdk query todo.match-phase 65` returned 0 matches).

</deferred>

---

*Phase: 65-markdown-rendering-foundation*
*Context gathered: 2026-07-09*
