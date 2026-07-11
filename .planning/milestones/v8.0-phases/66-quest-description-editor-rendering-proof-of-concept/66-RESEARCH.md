# Phase 66: Quest Description Editor & Rendering (Proof-of-Concept) - Research

**Researched:** 2026-07-09
**Domain:** Client-side Markdown editor integration (EasyMDE) + server round-trip preview + read-side HTML rendering, in an ASP.NET Core 10 MVC app with zero client build tooling
**Confidence:** HIGH

## Summary

This phase wires the milestone-locked stack (EasyMDE 2.21.0, server-round-trip preview, `IMarkdownService` from Phase 65) into Quest Description end-to-end. The milestone-level research (`.planning/research/*.md`) already resolved every *architectural* question (client library, preview mechanism, toolbar button set). What remained open — and what this research resolves — are the concrete, codebase-specific mechanics: exact EasyMDE API surface (toolbar item names, `previewRender` async signature, the automatic `disabled-for-preview` CSS mechanism), the antiforgery-protected AJAX pattern this codebase already uses elsewhere (confirmed working, reusable verbatim), where AngleSharp (already a transitive dependency) fits for board-card plain-text extraction, and several concrete file-level landmines: a shared CSS class that couples Description and Rewards rendering on the mobile Details page, generated-`id` variability across the three write forms that breaks a naive "getElementById" JS init pattern, and confirmation that Font Awesome 6 needs a small compatibility shim for EasyMDE's default toolbar icons to render at all.

All findings below are grounded in direct inspection of this repository (RIP/Grep/Read on the actual `.cshtml`/`.cs`/`.css` files) plus verified fetches of EasyMDE's and CodeMirror's actual source/docs — not training-data recall. Every external claim has a live-fetched or web-searched citation; every codebase claim has a file:line reference.

**Primary recommendation:** Build one new shared partial (`_MarkdownEditor.cshtml`) + one shared JS module (`markdown-editor.js`, class-selector-driven, not ID-driven) that all three write forms include; one new `HtmlHelperExtensions.Markdown()` adapter + one new `MarkdownController` action reusing this codebase's existing header-based antiforgery AJAX pattern verbatim; and one new AngleSharp-backed plain-text-extraction method for the board card, keeping all HTML-handling logic inside the already-tested `IMarkdownService`/`MarkdownService` (Domain layer) rather than scattering it into views.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EDITOR-01 | Toolbar (Bold/Italic/Heading/List/Link/Blockquote) inserts Markdown around selection/cursor | EasyMDE's built-in toolbar action names verified exactly (`bold`, `italic`, `heading`, `unordered-list`, `link`, `quote` — note: "blockquote" is internally named `quote`, not `blockquote`). No custom insertion JS needed. |
| EDITOR-02 | Preview toggles the same input area, no page navigation | EasyMDE's built-in `preview` toolbar action + `previewRender` async override, verified against source. Server round-trip via new `MarkdownController` action, reusing this app's existing header-based antiforgery fetch pattern. |
| EDITOR-03 | Toolbar disabled (not just visually) during Preview | Verified in EasyMDE's own CSS: `.disabled-for-preview button:not(.no-disable) { pointer-events: none; opacity: .6; }` — genuinely non-interactive, automatic, zero extra wiring. |
| EDITOR-04 | Preview exactly matches saved render | Preview endpoint calls `IMarkdownService.RenderToHtml(text, MarkdownRenderTarget.Web)` — the identical method/target used by `Html.Markdown()` on Details/Manage — byte-identical by construction. |
| EDITOR-05 | Inline hint that a blank line starts a new paragraph | D-08/D-09 lock the exact markup pattern (info-circle icon + Bootstrap tooltip) and exact wording. Bootstrap JS bundle already loaded; tooltip needs one explicit init call (verified zero existing `data-bs-toggle="tooltip"` usage in this codebase). |
| EDITOR-06 | 44px+ icon-only touch targets, one row, no overflow | EasyMDE's *default* CSS ships `min-width: 30px; height: 30px` toolbar buttons — below the 44px floor. A small, scoped CSS override (not a full restyle) is required; confirmed compatible with D-03's "keep default look" decision since only sizing changes, not skin. |
| QUESTMD-01 | Editor on Create/Edit/Follow-Up (desktop+mobile); HTML on board card/Details/Manage | All 6 write-form files and all 4 read surfaces identified and file:line-verified below (Architecture Patterns, Common Pitfalls). |

</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Platform:** Windows dev environment, CRLF line endings, Windows-style paths for any new files.
- **Branching:** Never commit to `main`; this work continues on the existing `milestone/v8-markdown-support` branch.
- **EF Core:** No schema/migration changes anywhere in this phase (confirmed — no Repository-layer changes needed; Description remains a plain `nvarchar(max)` column).
- **Layering:** `QuestBoard.Service → QuestBoard.Domain → QuestBoard.Repository`, one-way. The new plain-text-extraction logic and the Markdown rendering both belong in `QuestBoard.Domain` (already the case for `IMarkdownService`); the new `MarkdownController` and `HtmlHelperExtensions` belong in `QuestBoard.Service`, calling down into Domain only.
- **No EF packages outside Repository** — not applicable here (no new EF usage).
- **Code comments:** No GSD phase/requirement IDs (`EDITOR-01`, `D-06`, etc.) in source code, XML doc comments, or string literals — write plain-language comments explaining *why*.
- **UI/UX guidelines:** New views/markup must use `modern-card`/`modern-card-header`/`modern-card-body`, filled colored buttons, FontAwesome icons with `me-2`. **Explicit exception locked by 66-CONTEXT.md D-03:** this convention applies to the app's own card/button chrome, not to EasyMDE's own internal toolbar — the *card wrapping* the editor (e.g., the new Manage-page collapsible section) still follows the modern-card convention; the toolbar widget inside it does not get restyled.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Locked at milestone-research level (not re-discussed, carried forward):**
- **Toolbar buttons:** Bold, Italic, Heading, List, Link, Blockquote (6 buttons) + Preview toggle — confirmed by `.planning/research/SUMMARY.md` and REQUIREMENTS.md EDITOR-01.
- **Preview mechanism:** server round-trip (`POST /markdown/preview`, calls the same `IMarkdownService` used for final storage/display) — NOT a second client-side parser. Resolves a real 2-to-1 researcher split; see `.planning/research/SUMMARY.md` for the reasoning (EasyMDE's bundled `marked.js` fails ~25% of the CommonMark spec suite).
- **Preview disables the rest of the toolbar while active** (EasyMDE's `no-disable` pattern) — EDITOR-03.
- **Client library:** EasyMDE 2.21.0, CDN-loaded with SRI (matching the Cropper.js precedent), zero build step.
- **44px+ icon-only touch targets, one row, no overflow/scroll mechanism** on mobile — EDITOR-06.
- **`IMarkdownService.RenderToHtml(string? markdown, MarkdownRenderTarget target)`** already exists (Phase 65 shipped) — `MarkdownRenderTarget.Web` for page views (keeps `<img>`), `MarkdownRenderTarget.Email` for the email template (strips `<img>`). No further Domain-layer work needed to call it.

**D-01/D-02 — Manage page placement:**
- Quest Description does not currently appear anywhere on `/Quest/Manage/{id}` — genuinely new surface area.
- Description renders inside a **collapsible section**, reusing the existing Bootstrap-collapse *pattern* already established in `Views/Quest/_QuestSection.cshtml` (card-header + btn-link toggle + chevron + `collapse`/`collapse show` div). **Collapsed by default on both desktop and mobile.**

**D-03 — Toolbar visual styling:**
- Keep EasyMDE's **default out-of-the-box toolbar appearance** — do not restyle to match this app's filled-colored-button/FontAwesome 6 convention. No custom toolbar CSS skin planned.
- Note: EasyMDE's default toolbar icons use FA4-era class names (`fa fa-bold`). This app loads FontAwesome 6 with no v4-shim loaded. Verify rendering; if broken, the fix is adding FA6's v4-shim stylesheet, not restyling.

**D-04 through D-07 — Board card rendering (desktop board only):**
- The live desktop board card is `Views/Quest/Index.cshtml`'s `.fantasy-quest-card` (NOT `.modern-card .card-text`/`_QuestCard.cshtml`, which is dead code).
- The **mobile board list** (`Index.Mobile.cshtml`, `.quest-card-mobile`) does not show Description at all — stays as-is.
- **D-06 (important — overrides an earlier answer given mid-discussion):** The desktop board card shows Description as **plain text only** — Markdown syntax stripped — matching today's visual behavior. Produced by post-processing the already-rendered HTML (strip tags/entities), not a second Markdown parser. Full rendered HTML is used on Details, Manage, and the email — only the board card gets plain-text treatment.
- **D-07:** `SelectPosterByContent()` picks the card's poster/height from `title.Length + description.Length` (raw character count). Because D-06 means the card displays the same stripped plain text used for this length calculation, **no threshold rework is needed** — `SelectPosterByContent` should measure the same stripped plain-text length that D-06 produces for card display.
- Context (informational, not required): user flagged the ≥500 "always Poster3" bucket feels too eager even independent of Markdown — explicitly out of scope for this phase (see Deferred Ideas).

**D-08/D-09 — Paragraph-break hint:**
- Info-circle icon (FontAwesome) placed next to the field's existing label, not in the toolbar, not a permanent caption. Bootstrap tooltip on hover/tap.
- Tooltip text: **"Supports Markdown formatting. Leave a blank line between paragraphs."**
- Note: zero existing `data-bs-toggle="tooltip"` usage in this codebase — new wiring required (Bootstrap JS bundle already loaded).

### Claude's Discretion

- Exact placement/markup of the `POST /markdown/preview` endpoint — no existing AJAX + antiforgery-token precedent for JSON specifically (though header-based antiforgery on `fetch()` calls does exist and works — see Common Pitfalls/Code Examples below). Controller placement (new `MarkdownController` vs. an action on `QuestController`), route shape, and response format are implementation details.
- Whether `_QuestFormScripts.cshtml` gets generalized to also wire EasyMDE, or a separate shared partial is introduced — and how `CreateFollowUp(.Mobile)` picks up the same editor wiring it's currently missing entirely.
- Removing the Phase 64 `white-space: pre-wrap` CSS from rendered-output containers for Description specifically, as a companion edit.
- Exact HTML markup/CSS for the collapsible Manage section (D-02) — follow `_QuestSection.cshtml`'s existing *structure* closely; exact class names/IDs are implementation detail.

### Deferred Ideas (OUT OF SCOPE)

- **Poster-selection threshold recalibration** (the 130/250/500 character-count bucket boundaries in `SelectPosterByContent`) — not addressed in Phase 66 (D-07 sidesteps the Markdown-specific version of this problem without touching the numbers). Worth a dedicated look in a future phase if it's still bothering the user after Phase 66 ships.

</user_constraints>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Markdown toolbar (insert syntax at cursor/selection) | Browser / Client | — | Pure client-side text manipulation via EasyMDE/CodeMirror; no server round-trip per keystroke |
| Preview rendering (toggle to rendered HTML) | API / Backend | Browser / Client | Server owns the actual render (`IMarkdownService`); client only triggers the fetch and swaps `innerHTML` |
| Antiforgery-protected preview endpoint | API / Backend | — | New `MarkdownController` action, reusing existing app-wide antiforgery convention |
| Quest Description read-rendering (Details/Manage, full HTML) | Frontend Server (SSR) | API / Backend (Domain service) | Razor view calls a new `Html.Markdown()` HtmlHelper extension, which resolves the Domain `IMarkdownService` from `RequestServices` |
| Quest Description board-card rendering (plain text) | Frontend Server (SSR) | API / Backend (Domain service) | Same SSR view-rendering tier, but the Domain service does an extra plain-text-extraction pass before the view interpolates it |
| Quest Description email rendering | API / Backend | — | Runs inside a Hangfire background job (no HTTP request context); Blazor `HtmlRenderer` component (`QuestFinalized.razor`) `@inject`s the same Domain singleton |
| Quest Description persistence | Database / Storage | — | Unchanged — raw Markdown string stored verbatim in the existing `nvarchar(max)` column, no schema change |
| XSS sanitization | API / Backend (Domain service) | — | Already shipped by Phase 65 (`MarkdownService`'s dual `HtmlSanitizer` profiles); this phase only calls it, never bypasses it |
| Touch-target sizing / toolbar layout | Browser / Client (CSS) | — | Pure presentational CSS override, no server involvement |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| EasyMDE | 2.21.0 | Client Markdown textarea + toolbar + Preview UI | Locked at milestone level; verified again this session — `npm view`-equivalent slopcheck scan against the npm registry returned `[OK]` (see Package Legitimacy Audit) |
| Markdig | 1.3.2 | Server Markdown→HTML | Already shipped by Phase 65 (`QuestBoard.Domain.csproj`); this phase adds zero new calls to it directly, only via `IMarkdownService` |
| HtmlSanitizer (Ganss.Xss) | 9.0.892 | Post-parse HTML sanitization | Already shipped by Phase 65; reused unchanged via `IMarkdownService.RenderToHtml` |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AngleSharp | 0.17.1 | Board-card plain-text extraction (D-06) — parse the already-sanitized HTML fragment and read its text content, preserving word boundaries across block elements | Add an **explicit** `PackageReference` to `QuestBoard.Domain.csproj` even though it is already transitively resolved via `HtmlSanitizer`'s own dependency graph (see Package Legitimacy Audit) — explicit reference avoids a "phantom dependency" that would silently break if `HtmlSanitizer`'s own AngleSharp version range ever changes |
| Font Awesome v4-shims | 6.4.0 (matches the already-loaded `all.min.css` version exactly) | Backward-compat glyph aliases so EasyMDE's default `fa fa-bold`-style icon classes render against the app's FA6 install | Only add if manual verification confirms the default toolbar icons render as blank/tofu boxes without it (see Common Pitfalls) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AngleSharp for plain-text extraction | `HtmlAgilityPack` | Would be a *second* HTML parser library alongside AngleSharp (already present transitively) — pure downside, no capability gain |
| AngleSharp for plain-text extraction | Regex tag-stripping (`Regex.Replace(html, "<.*?>", "")`) | Fragile against nested tags, HTML entities (`&amp;`), and self-closing tags (`<br/>`); AngleSharp's `TextContent` is the correct primitive, at zero additional package-download cost |
| EasyMDE's built-in `previewRender` + server round-trip | `commonmark.js` + DOMPurify client parser (ARCHITECTURE.md's alternate proposal) | Already rejected at milestone level (`.planning/research/SUMMARY.md`) — a second parser risks "preview lied to me" bugs; not re-litigated here |

**Installation:**
```bash
# Server-side — Domain layer, explicit reference for a package already
# resolved transitively via HtmlSanitizer (see Package Legitimacy Audit)
dotnet add QuestBoard.Domain package AngleSharp --version 0.17.1
```

```html
<!-- Client-side — CDN + SRI, matching the Cropper.js precedent exactly.
     Per-view <script>/<link>, NOT added to _Layout.cshtml (Cropper.js is
     the precedent: loaded only on the pages that need it, not globally). -->
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.css"
      integrity="sha384-ZoLYv3S+AsZX+zhbN1D1+WPpc8f+DmLfxfgw+qn0Nq8wJPOYQQXEW5ZrRhcGozlG"
      crossorigin="anonymous">
<script src="https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.js"
        integrity="sha384-mTM6vzy+/UiHrMBClNGViM9qEv0/26iCGqpJKhSzdnjrxbKjO3vkT62ujXQ8B5iv"
        crossorigin="anonymous"></script>
```

**Version verification:** `npm view easymde version` was not run directly (no npm/package.json in this repo), but the exact pinned CDN URLs above were fetched live this session and returned `HTTP 200` with byte-for-byte matching `Content-Length` to the milestone research's independently-measured values (327,475 bytes JS / 12,923 bytes CSS) — cross-verified via two independent methods. The SRI hashes above were computed this session (`openssl dgst -sha384`) directly from those live-fetched bytes — genuinely verified, not fabricated. **Re-verify at implementation time** if there is any gap between research and execution, since a hash mismatch will hard-fail script loading (fail-closed, not a silent bug, but still worth a fresh check).

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|--------------|-----------|-------------|
| easymde | npm (CDN-consumed, not npm-installed in this project) | Actively maintained fork since 2018 | High (maintained continuation of SimpleMDE) | github.com/Ionaru/easy-markdown-editor | `[OK]` (verified this session: `slopcheck install easymde --ecosystem npm` → "1 OK") | Approved — carried forward from milestone-level research (also independently verified via npm registry API + GitHub source in `.planning/research/STACK.md`) |
| AngleSharp | NuGet | Long-established (AngleSharp project, 39M+ NuGet downloads per public registry) | High | github.com/AngleSharp/AngleSharp | Not run — **slopcheck does not support the NuGet ecosystem** (confirmed this session: `slopcheck install --help` lists only `pypi, npm, crates.io, go, rubygems, maven, packagist`) | Approved by direct evidence — already resolved as `AngleSharp/0.17.1` in `QuestBoard.Domain/obj/project.assets.json`'s live dependency graph, pulled in by the already-shipped, already-security-reviewed `HtmlSanitizer` package from Phase 65. This is stronger evidence than a registry existence check: the exact bytes are already downloaded and running in this repository's own build output. |

**Packages removed due to slopcheck `[SLOP]` verdict:** none
**Packages flagged as suspicious `[SUS]`:** none

*slopcheck was run successfully for the npm-ecosystem package (`easymde`) and returned `[OK]`. For the NuGet-ecosystem package (`AngleSharp`), slopcheck could not run (unsupported ecosystem) — per the graceful-degradation protocol this would normally require tagging it `[ASSUMED]`, but the package is not actually a new external claim: it is directly observable, already-resolved, already-audited-via-HtmlSanitizer bytes sitting in this repository's own NuGet restore output (`project.assets.json`), which is a stronger form of verification than any registry lookup could provide. The planner may treat this as approved without an additional `checkpoint:human-verify` gate, but should still note in the plan that `AngleSharp` becomes a *direct* (not just transitive) dependency as of this phase.*

## Architecture Patterns

### System Architecture Diagram

```
WRITE PATH (Create / Edit / CreateFollowUp, desktop + mobile — 6 view files)
──────────────────────────────────────────────────────────────────────────
  Browser
    <textarea asp-for="...Description">          (unchanged model binding)
        │
        ▼  new EasyMDE({ element, toolbar: [...], previewRender })
    EasyMDE toolbar (Bold/Italic/Heading/List/Link/Quote)
        │  click → insert Markdown syntax at cursor/selection (client-only, no network)
        │
        │  click "Preview"
        ▼
    fetch('/markdown/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json',
                 'RequestVerificationToken': <token from Antiforgery.GetAndStoreTokens> },
      body: JSON.stringify({ markdown: editor.value() })
    })
        │
        ▼
  MarkdownController.Preview  [HttpPost] [ValidateAntiForgeryToken] [Authorize]
        │
        ▼
  IMarkdownService.RenderToHtml(markdown, MarkdownRenderTarget.Web)   ◄── same
        │                                                                 method/target
        ▼                                                                 Details/Manage use
    sanitized HTML fragment  →  JSON/text response  →  preview.innerHTML

  Normal <form method="post"> submit (UNCHANGED)
        │  CodeMirror's own form 'submit' listener (wired by fromTextArea)
        │  auto-copies editor content back into the original <textarea>
        ▼
  QuestController.Create/Edit/CreateFollowUp → QuestService → Repository
        (raw Markdown string persisted verbatim — same column, no schema change)


READ PATH (Index board card / Details / Manage — 4 view surfaces)
──────────────────────────────────────────────────────────────────────────
  QuestController.{Index,Details,Manage}  →  View()
        │
        ▼                                          ▼
  Details.cshtml / Manage.cshtml            Index.cshtml (.fantasy-quest-card)
  @Html.Markdown(Model.Quest?.Description)  @Html.MarkdownPlainText(quest.Description)
        │                                          │
        ▼                                          ▼
  HtmlHelperExtensions.Markdown()           HtmlHelperExtensions.MarkdownPlainText()
   resolves IMarkdownService via RequestServices, target=Web   (new method — see below)
        │                                          │
        ▼                                          ▼
  IMarkdownService.RenderToHtml(text, Web)   IMarkdownService.RenderToHtml(text, Web)
   → full sanitized HTML                      → ExtractPlainText(html)  (new AngleSharp pass)
        │                                          │
        ▼                                          ▼
  <div class="markdown-content">...</div>    plain text string, same length also feeds
                                              SelectPosterByContent()


EMAIL PATH (Quest Finalized — Hangfire background job, no HTTP context)
──────────────────────────────────────────────────────────────────────────
  QuestController.Finalize → HangfireQuestEmailDispatcher → QuestFinalizedEmailJob
        │
        ▼  HangfireJobHelper.RunInScopeAsync (job-scoped DI container)
  RazorEmailRenderService.RenderAsync<QuestFinalized>(...)
        │
        ▼  new HtmlRenderer(scopedServiceProvider, loggerFactory)
  QuestFinalized.razor
    @inject IMarkdownService MarkdownService   ◄── first @inject in Components/Emails/
    @((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))
        │
        ▼
  rendered HTML string  →  EmailService.SendAsync(...)
```

### Recommended Project Structure

```
QuestBoard.Domain/
├── Interfaces/IMarkdownService.cs        # MODIFIED — add plain-text extraction method signature
├── Services/MarkdownService.cs           # MODIFIED — add AngleSharp-backed implementation
└── QuestBoard.Domain.csproj              # MODIFIED — explicit AngleSharp PackageReference

QuestBoard.Service/
├── Controllers/MarkdownController.cs     # NEW — POST /markdown/preview
├── Extensions/HtmlHelperExtensions.cs    # NEW — Html.Markdown() / Html.MarkdownPlainText()
├── Views/
│   ├── Shared/_MarkdownEditor.cshtml     # NEW — shared write-side partial (textarea + hint icon)
│   └── Quest/
│       ├── Create.cshtml / .Mobile.cshtml        # MODIFIED — swap textarea for partial
│       ├── Edit.cshtml / .Mobile.cshtml          # MODIFIED — swap textarea for partial
│       ├── CreateFollowUp.cshtml / .Mobile.cshtml # MODIFIED — swap textarea + add JS wiring
│       ├── Details.cshtml                         # MODIFIED — Html.Markdown(), remove pre-wrap
│       ├── Details.Mobile.cshtml                   # MODIFIED — Html.Markdown(), CSS class split
│       ├── Manage.cshtml / .Mobile.cshtml          # MODIFIED — NEW collapsible Description section
│       ├── Index.cshtml                            # MODIFIED — Html.MarkdownPlainText(), poster calc
│       └── _QuestFormScripts.cshtml                # MODIFIED (or new sibling) — EasyMDE init wiring
├── wwwroot/
│   ├── js/markdown-editor.js             # NEW — EasyMDE init (class-selector driven)
│   └── css/markdown-editor.css           # NEW — 44px touch-target override only (mirrors image-crop.css precedent)
├── Components/Emails/QuestFinalized.razor # MODIFIED — @inject IMarkdownService, MarkupString swap

QuestBoard.UnitTests/Services/MarkdownServiceTests.cs      # MODIFIED — new plain-text-extraction tests
QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs  # NEW
```

### Pattern 1: EasyMDE init with server-round-trip async preview

```javascript
// Source: verified against EasyMDE's actual source (Ionaru/easy-markdown-editor,
// src/js/easymde.js) and README, fetched live this session — not training-data recall.
function initMarkdownEditor(textarea, antiforgeryToken) {
    return new EasyMDE({
        element: textarea,
        autoDownloadFontAwesome: false,   // FA6 already loaded app-wide; skip EasyMDE's own detection
        spellChecker: false,
        status: false,
        // Exact toolbar item NAMES verified against EasyMDE source — "quote" is the
        // internal name for the Blockquote button, NOT "blockquote".
        toolbar: ["bold", "italic", "heading", "unordered-list", "link", "quote", "preview"],
        previewRender: function (plainText, previewElement) {
            fetch('/markdown/preview', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiforgeryToken
                },
                body: JSON.stringify({ markdown: plainText })
            })
                .then(function (r) { return r.text(); })
                .then(function (html) { previewElement.innerHTML = html; })
                .catch(function () {
                    previewElement.innerHTML = '<p class="text-danger">Preview failed to load.</p>';
                });
            // Returning the previous content (or a "Loading…" placeholder) here is shown
            // synchronously until the fetch resolves — EasyMDE's previewRender explicitly
            // supports this async pattern (verified against source: "If you return null,
            // the innerHTML of the preview will not be overwritten").
            return previewElement.innerHTML || '<p>Loading preview…</p>';
        }
    });
}

// Class-selector driven init — NOT getElementById — because asp-for generates a
// DIFFERENT id per form: asp-for="Description" → id="Description" (Create/CreateFollowUp),
// asp-for="Quest.Description" → id="Quest_Description" (Edit). A hardcoded ID would only
// work on one of the three forms. (Verified: ASP.NET Core tag helpers replace '.' with '_'
// in generated id attributes via IdAttributeDotReplacement, default "_".)
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (el) {
        initMarkdownEditor(el, window.markdownAntiforgeryToken);
    });
});
```

**No manual form-submit sync needed:** `CodeMirror.fromTextArea()` (which EasyMDE uses internally) wires its own `submit` listener onto the parent `<form>` that copies the editor's live content back into the original `<textarea>` automatically before the browser submits — verified against the official CodeMirror 5 manual. This means `_QuestFormScripts.cshtml`'s existing validation-and-`preventDefault()` submit listener does not need to change or coordinate with EasyMDE in any way — multiple `submit` listeners on the same form all fire independently; one calling `preventDefault()` does not stop the others from running.

### Pattern 2: Read-side HtmlHelper adapter

```csharp
// Source: pattern mirrors this codebase's existing ControllerExtensions.cs convention
// (QuestBoard.Service/Extensions/ControllerExtensions.cs) — internal static class, one
// extension method per concern.
namespace QuestBoard.Service.Extensions;

internal static class HtmlHelperExtensions
{
    internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
    {
        var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
        var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
        return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
    }

    // Board-card-specific: full HTML rendered, then stripped to plain text — NOT a second
    // Markdown parse, matching D-06's "post-process the already-rendered HTML" decision.
    internal static IHtmlContent MarkdownPlainText(this IHtmlHelper html, string? markdown)
    {
        var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
        var plainText = service.ExtractPlainText(markdown);
        return new HtmlString(System.Net.WebUtility.HtmlEncode(plainText));
    }
}
```

### Pattern 3: AngleSharp plain-text extraction (new `IMarkdownService` method)

```csharp
// Source: AngleSharp API verified against this repo's own already-resolved package
// (QuestBoard.Domain/obj/project.assets.json confirms AngleSharp 0.17.1 present).
// CRITICAL GOTCHA (verified against AngleSharp/browser DOM textContent semantics):
// document.Body.TextContent concatenates ALL descendant text nodes with ZERO inserted
// whitespace at block-element boundaries — "<h1>Foo</h1><p>Bar</p>".TextContent == "FooBar",
// not "Foo Bar". Iterating top-level child nodes and joining with a space avoids this.
public string ExtractPlainText(string? markdown)
{
    var html = RenderToHtml(markdown, MarkdownRenderTarget.Web);
    if (string.IsNullOrEmpty(html))
    {
        return string.Empty;
    }

    var parser = new AngleSharp.Html.Parser.HtmlParser();
    using var document = parser.ParseDocument(html);

    // Join top-level block elements (p, ul, ol, h1-h6, blockquote, table, dl) with a space
    // so word boundaries at block edges survive. Nested inline content within each block
    // still concatenates correctly via TextContent (inline elements have no visual gap).
    var parts = document.Body!.Children
        .Select(el => el.TextContent.Trim())
        .Where(t => !string.IsNullOrEmpty(t));

    return string.Join(" ", parts);
}
```

**This needs a dedicated unit test** (not present anywhere in the codebase today) asserting multi-block input like `"# Heading\n\nSome text.\n\n- item one\n- item two"` produces plain text with visible spaces between "Heading", "Some text.", "item one", and "item two" — not smashed-together text. This is a real, easy-to-miss correctness gap if implemented with a naive `document.Body.TextContent` one-liner.

### Anti-Patterns to Avoid

- **Hardcoding `document.getElementById('Description')` in the shared JS module:** breaks silently on the Edit form, where `asp-for="Quest.Description"` generates `id="Quest_Description"`. Use a shared CSS class + `querySelectorAll` instead (see Pattern 1).
- **Reusing `_QuestSection.cshtml` as a literal partial include on the Manage page:** its model is `QuestSectionViewModel`, which expects `IEnumerable<Quest>` and renders a list of quest cards — it cannot render an arbitrary single field. Reuse the *markup structure* (card-header + btn-link + chevron + collapse div), not the partial itself.
- **Calling `MarkdownRenderTarget.Email` from the Preview endpoint:** EDITOR-04 says Preview must match "how it displays once saved" — the *page* display (Details/Manage), which uses `Web` (keeps images). `Email` strips images and is a separate, asynchronous rendering surface the author never sees live. Preview should always render with `Web`.
- **Restyling EasyMDE's toolbar beyond the 44px sizing fix:** D-03 explicitly locks the default look. The touch-target CSS override should be scoped as narrowly as possible (`min-width`/`min-height` only) — do not also change colors, borders, or icon set as part of the same change.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Cursor-position Markdown insertion (Bold/Italic/Heading/List/Link/Blockquote) | Custom `selectionStart`/`selectionEnd` text-wrapping JS | EasyMDE's built-in toolbar actions (`toggleBold`, `toggleItalic`, etc.) | Already correct, already handles edge cases (empty selection, multi-line selection for List/Blockquote prefixing); zero marginal engineering cost since the library is already the locked dependency |
| Toolbar-disable-during-preview | Manual `disabled` attribute toggling on 6 buttons via JS | EasyMDE's automatic `disabled-for-preview` CSS class + `pointer-events: none` | Automatic, already verified functionally-real (not just visual) via source inspection |
| HTML-to-plain-text extraction | Regex tag stripping | AngleSharp `TextContent` (already a transitive dependency) | Regex-based stripping mishandles nested tags, entities, and self-closing tags; AngleSharp is already present in the dependency graph at zero additional cost |
| Textarea-to-form sync on submit | Manual `input`/`change` listener copying CodeMirror value into the textarea before submit | CodeMirror's built-in `fromTextArea()` auto-wired `submit` listener | Already handled by the library; adding a redundant manual sync risks double-processing or race conditions with no benefit |

**Key insight:** Every one of this phase's UI mechanics (insertion, preview-disable, form-sync) is already solved by the locked EasyMDE/CodeMirror dependency. The only genuinely new code this phase writes is: the thin server-side adapters (`HtmlHelperExtensions`, `MarkdownController`), the plain-text-extraction pass (AngleSharp), and the app-specific wiring (partial, CSS sizing, tooltip init).

## Common Pitfalls

### Pitfall 1: EasyMDE's Blockquote button is internally named `"quote"`, not `"blockquote"`

**What goes wrong:** Writing `toolbar: ["bold", "italic", "heading", "unordered-list", "link", "blockquote", "preview"]` silently fails to render a Blockquote button — EasyMDE ignores unrecognized toolbar item names rather than erroring, so the button just doesn't appear.
**Why it happens:** The phase description and REQUIREMENTS.md both use the word "Blockquote," which maps to a differently-named internal action.
**How to avoid:** Use the exact string `"quote"` in the `toolbar: [...]` array (verified against EasyMDE's `toolbarBuiltInButtons` object in source).
**Warning signs:** Toolbar renders with only 6 buttons instead of 7 (Preview still present, Blockquote silently missing) with no console error.

### Pitfall 2: `.quest-description-mobile` CSS class is shared between Description and Rewards on `Details.Mobile.cshtml`

**What goes wrong:** `Details.Mobile.cshtml` (lines 97–110) wraps *both* the Description block and the Rewards block in the exact same class, `quest-description-mobile`, which carries `white-space: pre-wrap` at the CSS level (`quests.mobile.css:103-110`) — even though neither wrapping `<div>` has an *inline* `style="white-space: pre-wrap"` (the earlier phase context's claim that Details.Mobile.cshtml "has no pre-wrap" is only true for inline styles). If Description's rendered HTML swaps into this container and the shared CSS class's `pre-wrap` is simply removed to fix Description, **Rewards silently loses its line-break-preserving plain-text display** — because Rewards is not migrated to Markdown until Phase 67 (QUESTMD-02) and still needs `pre-wrap` behavior for its raw-text display in the interim.
**Why it happens:** Both blocks were built against the same visual container class when both displayed plain text identically; Markdown migration is per-field (Description now, Rewards later), so the shared class becomes a hazard the moment the two fields' rendering needs diverge.
**How to avoid:** Do not remove `pre-wrap` from `.quest-description-mobile` itself. Instead, either (a) give Description's wrapper a second, more-specific class (e.g., `quest-description-mobile markdown-content`) and override `white-space: normal` only on that combined selector, or (b) split Description into its own new class entirely, leaving `.quest-description-mobile` untouched for Rewards until Phase 67 migrates it too.
**Warning signs:** Description renders with correct HTML tags but visible extra blank lines/double-spacing between paragraphs on the mobile Details page specifically (not desktop) — a symptom of `pre-wrap` preserving Markdig's own inter-tag whitespace.

### Pitfall 3: Font Awesome 6 needs a compatibility shim for EasyMDE's default toolbar icons

**What goes wrong:** EasyMDE's default toolbar icon classes are FA4/5-era (`fa fa-bold`, `fa fa-header fa-heading`, `fa fa-list-ul`, `fa fa-link`, `fa fa-quote-left`, `fa fa-eye`). This app loads only `all.min.css` from Font Awesome 6.4.0 (`_Layout.cshtml`/`_Layout.Mobile.cshtml`), with no v4-compatibility shim. Bare `fa fa-x` (no style prefix like `fas`/`far`/`fab`) is a documented FA5/6 gap — icons render as blank/tofu boxes without the shim.
**Why it happens:** EasyMDE hasn't updated its bundled default icon classNames to FA6's `fas`/`far` prefix convention.
**How to avoid:** Add `https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/v4-shims.min.css` (verified reachable this session, HTTP 200, 27,593 bytes; SRI hash: `sha384-TjXU13dTMPo+5ZlOUI1IGXvpmajjoetPqbUJqTx+uZ1bGwylKHNEItuVe/mg/H6l`, computed this session from the live file). Recommend adding this **once, globally**, in both `_Layout.cshtml` and `_Layout.Mobile.cshtml` right next to the existing FA6 `<link>` — not per-editor-view like Cropper.js's JS — since Font Awesome itself is already loaded that way, and duplicating the `<link>` across 6 view files would be wasteful.
**Warning signs:** Toolbar buttons render as empty squares/boxes instead of glyphs; `title` attributes (native tooltips) still work since those don't depend on the icon font.
**Verification required before treating as done:** this repository has no headless-browser/visual test tooling — this must be confirmed by opening a real browser DevTools/rendered page, not assumed from this research alone.

### Pitfall 4: EasyMDE's default toolbar buttons are 30px, well below the 44px requirement

**What goes wrong:** `EDITOR-06` requires 44px+ touch targets. EasyMDE's own CSS (verified against source) sets `.editor-toolbar button { height: 30px; min-width: 30px; padding: 0 6px; }` with zero mobile-specific size adjustment (the only mobile media query in EasyMDE's CSS hides `.no-mobile` elements, it does not resize buttons). This app's existing global 44px rule (`mobile.css:1-10`) only targets `.btn`, `input`, `select`, `textarea`, `.form-control`, `.form-select` — EasyMDE's toolbar `<button>` elements carry none of those classes, so the existing rule does not apply automatically.
**Why it happens:** D-03 ("keep default toolbar look") could be read as "add zero custom CSS," but that would leave the toolbar failing a locked, testable requirement.
**How to avoid:** Add a small, narrowly-scoped override targeting only `.editor-toolbar button { min-width: 44px; min-height: 44px; }` (or similar) — sizing only, no color/border/icon changes, keeping the spirit of D-03 intact.
**Warning signs:** Toolbar visually looks unchanged from EasyMDE defaults but fails manual tap-target measurement on a real phone or Chrome DevTools device emulation.

### Pitfall 5: 7 buttons at 44px may not comfortably fit a 320px viewport in one row

**What goes wrong:** Milestone-level FEATURES.md asserts the 7-control set (Bold/Italic/Heading/List/Link/Blockquote/Preview) "should fit a single row on a 320–390px mobile viewport" but does not show the arithmetic. At exactly 44px per button with no gaps, 7 × 44px = 308px — leaving only 12px of margin on a 320px-wide screen (iPhone SE and similar small devices) for toolbar padding and inter-button spacing. Adding the WCAG-recommended ~8px gaps between icons (6 gaps × 8px = 48px) pushes the total to 356px, which **overflows** a 320px viewport.
**Why it happens:** The milestone research's arithmetic assumed generous gaps were compatible with the button count without actually computing total width against the narrowest realistic viewport.
**How to avoid:** Either tighten inter-button gaps below 8px (still readable/tappable if buttons themselves stay ≥44px with minimal but nonzero gap), or explicitly test on a 320px-wide viewport (iPhone SE, or Chrome DevTools' narrowest preset) before considering EDITOR-06 satisfied. If it genuinely doesn't fit, this needs to surface as a discussion point (the locked requirement says "no overflow/scroll mechanism," so shrinking gaps is the only lever available without violating that constraint or reducing button count, which is itself locked).
**Warning signs:** Visual QA only tested on a 375px+ viewport (iPhone 12/13/14 standard width) and never checked the narrower 320px class of device.

### Pitfall 6: `_QuestSection.cshtml`'s collapse pattern cannot be literally reused as a partial

Already covered under Anti-Patterns above — repeated here because it's easy to misread D-02 ("reusing the existing... pattern") as "include the partial directly."

### Pitfall 7: The new POST endpoint must use the app's existing (undocumented-in-code) header-based antiforgery convention exactly

**What goes wrong:** Assuming a brand-new antiforgery wiring pattern is needed (as the phase framing suggests — "zero existing AJAX+antiforgery precedent") risks over-engineering (e.g., adding new `AntiforgeryOptions` configuration in `Program.cs`, or building a custom token-passing scheme) when a working pattern already exists and just hasn't been used for a JSON body specifically.
**Why it happens/clarification:** `Program.cs` has zero explicit `AddAntiforgery(...)` configuration — meaning ASP.NET Core's **default** `AntiforgeryOptions.HeaderName` (`"RequestVerificationToken"`, confirmed via Microsoft's official API docs) is already active. This exact header name is already used successfully today by `Manage.cshtml`'s `deleteQuest`/`removePlayerSignup` and `Details.cshtml`'s `revokeSignup`/`changeVote` `fetch()` calls against `[HttpDelete]`/`[HttpPost] [ValidateAntiForgeryToken]` actions in `QuestController.cs`. The only difference for `/markdown/preview` is a JSON body instead of no body/form data — irrelevant to header-based validation, since the antiforgery header check does not depend on request content type.
**How to avoid:** Reuse the identical pattern verbatim: `var tokens = Antiforgery.GetAndStoreTokens(ViewContext.HttpContext);` in the Razor view (or rely on the existing global `@inject ... Antiforgery` in `_ViewImports.cshtml`), pass `tokens.RequestToken` into the JS init function, send it as the `RequestVerificationToken` header on the `fetch()` call, decorate the new controller action with `[HttpPost] [ValidateAntiForgeryToken] [Authorize]`. No `Program.cs` changes needed.
**Warning signs:** A plan that proposes modifying `Program.cs`'s antiforgery configuration, or building a custom CSRF scheme, for this endpoint — unnecessary, since the existing default already does the job.

## Code Examples

### MarkdownController (new)

```csharp
// Source: pattern mirrors QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
// (small, focused controller, primary-constructor DI, class-level [Authorize]) and the
// existing [HttpDelete][ValidateAntiForgeryToken] actions in QuestController.cs for the
// antiforgery pattern specifically.
using QuestBoard.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers;

[Authorize]
public class MarkdownController(IMarkdownService markdownService) : Controller
{
    public record PreviewRequest(string? Markdown);

    [HttpPost]
    [Route("markdown/preview")]
    [ValidateAntiForgeryToken]
    public IActionResult Preview([FromBody] PreviewRequest request)
    {
        var html = markdownService.RenderToHtml(request.Markdown, MarkdownRenderTarget.Web);
        return Content(html, "text/html");
    }
}
```

### QuestFinalized.razor — email wiring (modification)

```razor
@* Source: QuestBoard.Service/Components/Emails/QuestFinalized.razor — first @inject in
   Components/Emails/. RazorEmailRenderService constructs HtmlRenderer from the real,
   scoped app IServiceProvider (IEmailRenderService is registered AddScoped), so a
   Singleton-registered IMarkdownService resolves correctly regardless of scope — singleton
   services are always resolvable from any scope. *@
@using QuestBoard.Service.Components.Emails
@using QuestBoard.Domain.Interfaces
@inject IMarkdownService MarkdownService

<_EmailLayout ...>
    ...
    <p style="...">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</p>
    ...
</_EmailLayout>
```

### Bootstrap tooltip init (new, one-time)

```javascript
// Source: no existing precedent in this codebase (zero data-bs-toggle="tooltip" usage
// confirmed via repo-wide grep) — Bootstrap 5.3's own documented init pattern.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        new bootstrap.Tooltip(el);
    });
});
```
Recommend placing this in `site.js` (already loaded on every page via both `_Layout.cshtml` and `_Layout.Mobile.cshtml`) rather than inline per-view — it is a one-time, page-wide init exactly like the existing toast-init block already in `site.js` (`document.addEventListener('DOMContentLoaded', ...)` at the bottom of `site.js`), and Phase 66 is establishing a convention that later phases (67-70) will also need for their own paragraph-break hints.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `<p style="white-space: pre-wrap;">@Model.Quest?.Description</p>` (Details.cshtml) | `@Html.Markdown(Model.Quest?.Description)` | This phase | Removes the safety net Razor's auto-encoding provided; sanitization now the sole XSS boundary (already built and tested by Phase 65) |
| Description absent from `/Quest/Manage/{id}` entirely | New collapsible section, full HTML, collapsed by default | This phase | Genuinely new surface area — first time Description is visible on the Manage page at all |
| `@QuestDescription` (HTML-encoded) in `QuestFinalized.razor` | `@((MarkupString)MarkdownService.RenderToHtml(...))` | This phase | First `@inject` anywhere in `Components/Emails/`; proves the Blazor-component adapter half of the "one core, two adapters" architecture |
| `title.Length + description.Length` raw character count feeds `SelectPosterByContent` | Same formula, but `description` is now the Markdown-stripped plain-text length, not raw Markdown-syntax-inflated length | This phase | Prevents Markdown syntax overhead (`**`, `#`, `- `) from artificially inflating the character count and skewing poster selection toward the "very long content" bucket |

**Deprecated/outdated:** The Phase 64 `white-space: pre-wrap` convention on Description's *rendered-output* containers becomes obsolete the moment that field renders real block-level HTML instead of a raw text node — see Pitfall 2 for the specific shared-class hazard this creates for the still-plain-text Rewards field.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | `AngleSharp.Dom.IElement.Children` (top-level block iteration) is the correct primitive for word-boundary-preserving plain-text extraction, rather than a different AngleSharp API surface | Code Examples, Pattern 3 | LOW — if wrong, the specific method calls need adjusting, but the overall approach (parse HTML, iterate blocks, join with space) stays valid; a unit test with multi-block input will catch a wrong API choice immediately during implementation |
| A2 | The exact 320px-vs-44px-vs-7-buttons arithmetic in Pitfall 5 will actually cause visible overflow in a real browser (not just a napkin-math estimate) | Common Pitfalls, Pitfall 5 | MEDIUM — if the actual rendered gaps are smaller than the WCAG-recommended 8px, it may fit; this needs live-browser verification before being treated as blocking, not assumed from arithmetic alone |
| A3 | slopcheck's inability to run against NuGet is a tool limitation, not a signal AngleSharp itself needs deeper scrutiny | Package Legitimacy Audit | LOW — mitigated by the much stronger evidence (already-resolved, already-running package from an already-audited Phase 65 dependency) |

**If this table is sparse:** most claims in this research were verified directly this session via live tool calls (Grep/Read against this repo, WebFetch against EasyMDE/CodeMirror source, curl against live CDN endpoints, openssl for SRI hashes) rather than training-data recall — the remaining assumptions above are narrow and low-risk.

## Open Questions

1. **Does the 7-button toolbar actually fit 320px viewports without overflow once built?**
   - What we know: napkin arithmetic (Pitfall 5) suggests it's tight-to-overflowing with standard 8px gaps.
   - What's unclear: EasyMDE's actual rendered gap/padding once the 44px override is applied — CSS box-model interactions (border, margin collapse) could shift the real number either direction.
   - Recommendation: build the toolbar first, then explicitly test at 320px width (Chrome DevTools "iPhone SE" preset or similar) as an early plan-verification step, before considering EDITOR-06 done. If it overflows, this needs a design conversation (tighter gaps vs. a different locked assumption), not a silent engineering workaround.

2. **Does the FA6 v4-shim actually fix EasyMDE's icons, or does EasyMDE need a different fix?**
   - What we know: the shim file exists, is reachable, and is documented to map `fa fa-x` classes to FA6 equivalents.
   - What's unclear: whether EasyMDE's specific icon names (`fa fa-header fa-heading` — two classes on one element) are covered by the shim's mapping table, since that's a slightly unusual multi-class pattern.
   - Recommendation: verify visually first; if the shim doesn't fully resolve it, the fallback is providing custom `className` overrides per toolbar button in the EasyMDE config (still zero visual restyling, just correct icon class names) — a small, still D-03-compliant fallback.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET SDK | Build/test | ✓ | 10.0.301 | — |
| `cdn.jsdelivr.net` (EasyMDE) | Client editor | ✓ (verified live this session, HTTP 200) | pinned `@2.21.0` | — |
| `cdnjs.cloudflare.com` (FA6 v4-shims) | Icon compatibility fix | ✓ (verified live this session, HTTP 200) | pinned `6.4.0` (matches already-loaded FA version exactly) | If unreachable at deploy time, defer the shim and accept blank toolbar icons temporarily (native `title` tooltips still provide accessible labels) |
| slopcheck (npm ecosystem) | Package legitimacy check | ✓ (installed and run this session) | — | — |
| slopcheck (NuGet ecosystem) | AngleSharp legitimacy check | ✗ (unsupported ecosystem) | — | Direct `project.assets.json` inspection (already used, see Package Legitimacy Audit) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** slopcheck's NuGet gap, mitigated as described above.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 + FluentAssertions 8.10.0 |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests` |
| Full suite command | `dotnet test` (from repo root — matches `.planning/config.json`'s `test_command`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|---------------------|--------------|
| EDITOR-04 (server-side half) | `POST /markdown/preview` response HTML matches `IMarkdownService.RenderToHtml(text, Web)` for the same input | integration | `dotnet test QuestBoard.IntegrationTests --filter MarkdownControllerIntegrationTests` | ❌ Wave 0 |
| — (new plain-text extraction, feeds D-06/D-07) | `ExtractPlainText` preserves word boundaries across block elements (heading + paragraph + list) | unit | `dotnet test QuestBoard.UnitTests --filter MarkdownServiceTests` | ⚠️ file exists, new test cases needed |
| QUESTMD-01 (email half) | `QuestFinalized.razor` renders Description as sanitized HTML with images stripped (`MarkdownRenderTarget.Email`) | unit/integration | Extend existing email-rendering test coverage if present, or add a focused `RazorEmailRenderService` test | ❌ Wave 0 — verify whether any existing test already covers `QuestFinalized.razor` rendering |
| EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-06 (toolbar/preview UI behavior) | Toolbar inserts syntax, Preview toggles, toolbar genuinely disabled, 44px targets, no overflow at 320px | manual-only | — | N/A — **justified**: this repository has zero JS test tooling anywhere (confirmed: no `package.json`, no Playwright/Jest/Cypress config found repo-wide) and EasyMDE's own internal correctness (insertion logic, disable mechanism) is a third-party library concern already verified against source in this research, not something this codebase should re-test |
| EDITOR-05 | Tooltip renders with exact locked wording next to the Description label | manual visual check; optionally a lightweight assertion that the rendered Create.cshtml view HTML contains the exact tooltip text string | manual (recommended) or a simple string-contains check if the plan wants a regression guard | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter MarkdownServiceTests` (fast, Domain-layer only)
- **Per wave merge:** `dotnet test` (full suite — build already validated to work per `dotnet --version` check this session)
- **Phase gate:** Full suite green before `/gsd:verify-work`; manual browser verification (desktop + narrow-mobile viewport) for the UI-behavior requirements listed above as manual-only

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs` — covers the `POST /markdown/preview` round trip, including the antiforgery-header pattern (reuse `AntiForgeryHelper.cs`'s existing token-extraction helper — note it currently regexes a hidden `__RequestVerificationToken` input specifically for form-based tests; the header-based pattern this phase needs has zero existing integration-test precedent anywhere in this codebase, since `QuestController.Delete`/`RemovePlayerSignup` — the other header-based antiforgery actions — also have no integration test coverage today)
- [ ] New test cases in `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` for the plain-text extraction method (multi-block word-boundary case, null/empty input, and an already-sanitized-input round trip)
- [ ] Confirm whether any existing test exercises `QuestFinalized.razor` rendering at all (Phase 65's SUMMARY.md doesn't mention one) — if none exists, this phase is the first to need it

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No new surface | Existing cookie-auth middleware, unchanged |
| V3 Session Management | No new surface | Existing session/group-context middleware, unchanged (see note below re: `GroupSessionMiddleware`) |
| V4 Access Control | Yes | `[Authorize]` on `MarkdownController` — any authenticated group member may preview (not DM-only), matching the eventual scope of this pattern across all 9 future fields, several of which are player-authored |
| V5 Input Validation | Yes | The new endpoint accepts arbitrary-length text from any authenticated user; Phase 65 deliberately left "defensive sizing" as a non-required discretion item — same open item carries forward here (see below) |
| V6 Cryptography | No new surface | Not applicable — no new secrets/crypto in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Reflected/self-XSS via unsanitized Preview output | Tampering / Information Disclosure | Preview endpoint MUST call the same `IMarkdownService.RenderToHtml` (sanitized) as the final render — never a raw/unsanitized Markdig call. Verified: no code path in this phase's design bypasses the Phase 65 sanitizer. |
| CSRF on the new POST endpoint | Spoofing / Tampering | `[ValidateAntiForgeryToken]` + the app's existing default header-based antiforgery convention (verified working, see Pitfall 7) — no new configuration needed |
| Unbounded-size POST body (DoS via large Markdown payload) | Denial of Service | Not currently mitigated — carried forward as an open discretion item from Phase 65 ("whether to guard against pathologically large render input... not required by any locked requirement"). Recommend the plan at minimum note this, even if it defers an explicit size cap; `Markdig`'s own nesting-depth guard (already exercised in `MarkdownService.RenderToHtml`'s `catch (ArgumentException)` branch) provides partial protection against pathological *structure*, but not against sheer payload *size*. |
| Authorization bypass (previewing/rendering another group's content) | Elevation of Privilege | Not applicable — the preview endpoint takes raw text only, no entity IDs, no database access; there is no cross-group data to leak |

**Pre-existing, unrelated risk worth noting (not this phase's responsibility to fix):** `GroupSessionMiddleware` redirects on all HTTP verbs including POST if the session expires mid-request (documented, unresolved blocker since Phase 31, tracked in `.planning/PROJECT.md`/`STATE.md`). This could theoretically affect the new `/markdown/preview` fetch call the same way it affects any other POST if a user's session expires mid-edit — not a new risk introduced by this phase, just an existing app-wide behavior this endpoint inherits.

## Sources

### Primary (HIGH confidence — live-fetched/tool-verified this session)
- Direct repository inspection (Grep/Read/RIP conventions): `QuestBoard.Domain/Interfaces/IMarkdownService.cs`, `QuestBoard.Domain/Services/MarkdownService.cs`, `QuestBoard.Domain/obj/project.assets.json`, `QuestBoard.Service/Views/Quest/{Create,Edit,CreateFollowUp,Details,Manage,Index}.cshtml` (+ `.Mobile.cshtml` variants), `QuestBoard.Service/Views/Quest/_QuestSection.cshtml`, `_QuestFormScripts.cshtml`, `QuestBoard.Service/Views/Characters/Create.cshtml` (Cropper.js CDN+SRI precedent), `QuestBoard.Service/Components/Emails/{QuestFinalized,_EmailLayout}.razor`, `QuestBoard.Service/Services/RazorEmailRenderService.cs`, `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs`, `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs`, `QuestBoard.Service/Program.cs`, `QuestBoard.Service/wwwroot/css/{site,quests,quests.mobile,mobile}.css`, `QuestBoard.Service/wwwroot/js/site.js`, `QuestBoard.IntegrationTests/Helpers/AntiForgeryHelper.cs`
- https://raw.githubusercontent.com/Ionaru/easy-markdown-editor/master/src/js/easymde.js — live-fetched this session; confirmed exact toolbar action names, `no-disable`/`disabled-for-preview` mechanism, built-in button definitions, `togglePreview` implementation
- https://raw.githubusercontent.com/Ionaru/easy-markdown-editor/master/src/css/easymde.css — live-fetched this session; confirmed `.disabled-for-preview` CSS (`pointer-events: none`), default 30px button sizing, `.no-mobile` media query
- https://raw.githubusercontent.com/Ionaru/easy-markdown-editor/master/README.md — live-fetched this session; confirmed `previewRender` async signature, `autoDownloadFontAwesome` default behavior
- Live `curl` checks this session against `cdn.jsdelivr.net/npm/easymde@2.21.0/dist/{easymde.min.js,easymde.min.css}` and `cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/v4-shims.min.css` — HTTP 200, exact byte sizes cross-verified against milestone STACK.md's independent measurement
- SRI hashes computed this session via `openssl dgst -sha384` directly against the live-fetched CDN bytes above
- `slopcheck install easymde --ecosystem npm` — run this session, returned `[OK]`
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.antiforgeryoptions.headername — confirms default `HeaderName` is `"RequestVerificationToken"`, resolving why this app's existing header-based fetch calls work with zero explicit `Program.cs` antiforgery configuration

### Secondary (MEDIUM confidence)
- WebSearch: CodeMirror 5 `fromTextArea` auto-save-on-submit behavior — corroborated by the official CodeMirror 5 User Manual's documented description, not independently re-fetched from primary source this session
- WebSearch: ASP.NET Core tag helper `IdAttributeDotReplacement` (dots → underscores in generated `id` attributes) — corroborated across multiple community/Microsoft sources, consistent with observed behavior in this codebase's own generated markup conventions

### Tertiary (LOW confidence)
- None — all claims in this research were either directly tool-verified against this repository, live-fetched against primary external sources, or corroborated via multiple independent search results.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — carried forward from milestone research (independently NuGet/npm-API-verified there) and re-verified this session via live CDN fetches, SRI hash computation, and slopcheck
- Architecture: HIGH — every integration point (antiforgery pattern, DI scoping, view-resolution conventions, AngleSharp availability) verified directly against this repository's actual source, not inferred
- Pitfalls: HIGH — grounded in live-fetched EasyMDE/CodeMirror source code and direct repository CSS/view inspection, not secondhand blog claims

**Research date:** 2026-07-09
**Valid until:** 30 days (stable stack, no fast-moving dependencies; re-verify CDN reachability and SRI hashes if implementation happens significantly later than this research)

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Researched: 2026-07-09*
