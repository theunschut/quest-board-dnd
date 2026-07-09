# Stack Research

**Domain:** Markdown authoring + rendering for an ASP.NET Core 10 MVC/Razor app with zero client-side build tooling
**Researched:** 2026-07-09
**Confidence:** HIGH — versions and APIs verified directly against NuGet's v3 API, npm's registry API, and the libraries' own GitHub source/readme files (not training-data recall). Context7 MCP tools were not present in this session's toolset; primary-source verification (registry APIs + GitHub source) was used instead and carries equivalent or higher confidence for version numbers.

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Markdig** | **1.3.2** (NuGet, published 2026-06-18) | Server-side Markdown → HTML conversion | The de facto standard .NET Markdown processor (67M+ downloads, actively maintained, CommonMark-compliant with GFM extensions). Multi-targets `netstandard2.0`, `net462`, and `net8.0` — no `net10.0`-specific build exists or is needed; .NET 10 consumes the `net8.0`/`netstandard2.0` assemblies transparently. Pure managed code, zero native dependencies (unlike the project's paused SkiaSharp plan) — safe for the Linux LXC deployment target. |
| **HtmlSanitizer** (package id `HtmlSanitizer`, namespace `Ganss.Xss`) | **9.0.892** (NuGet, published 2026-02-02) | Whitelist-based HTML sanitization of Markdig's output before it is stored/rendered | Markdig is explicitly **not** a sanitizer (see Gotchas below) — it needs a paired sanitizer for untrusted input. `Ganss.Xss.HtmlSanitizer` is the standard .NET pairing for Markdig in every credible source found (Rick Strahl's ASP.NET Core Markdown writeup, Westwind.AspNetCore.Markdown's own internal wiring, general .NET XSS guidance). AngleSharp-based, whitelists tags/attributes/URL schemes, and its `Sanitize()`/`SanitizeDocument()` methods are documented thread-safe for a single shared/singleton instance — cheap to register once in DI and reuse. Multi-targets `netstandard2.0`, `net462`, `net8.0` — same .NET 10 compatibility story as Markdig. |
| **EasyMDE** | **2.21.0** (npm, current `latest`) | Client-side textarea → toolbar (Bold/Italic/Heading/List) + built-in Preview toggle, zero build step | Ships a single self-contained UMD bundle (`easymde.min.js`, 327 KB; `easymde.min.css`, 13 KB — measured via CDN `Content-Length`) that works as a plain `<script>`/`<link>` include, exactly like the Cropper.js v2.1.1 precedent this app already follows. Its **default toolbar already includes a `preview` button** (`bold`, `italic`, `heading`, `unordered-list`, `ordered-list`, `preview`, …) — no custom toolbar-button JS needs to be written, only a `toolbar: [...]` array trimmed to the 5 buttons this milestone needs. Actively maintained fork of the abandoned SimpleMDE (SimpleMDE has had no releases since 2017 — do not use it, see below). MIT licensed. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Font Awesome | already present (`_Layout.cshtml` line 13, loaded via `cdnjs` CDN v6.4.0) | Icons for EasyMDE's toolbar buttons | EasyMDE depends on Font Awesome glyph classes for its toolbar icons and by default auto-detects/auto-loads Font Awesome if not already present (`autoDownloadFontAwesome` option). Since this app already loads Font Awesome app-wide, set `autoDownloadFontAwesome: false` explicitly in the EasyMDE config to skip its detection logic entirely — zero new network dependency introduced. |
| CodeMirror 5.65.x | bundled inside `easymde.min.js` | Textarea replacement / syntax-aware editing surface EasyMDE is built on | Not a separate install — it ships pre-bundled inside the single minified EasyMDE file (no way to slim this down without a build step; see "What NOT to Use" for the size trade-off discussion). |
| Marked.js 4.1.x | bundled inside `easymde.min.js` | EasyMDE's *default* client-side preview renderer | **Do not use this for the actual preview** — see "Client-side vs server-side preview rendering" below. It ships inside the bundle regardless (no build step to tree-shake it out), but this milestone should override `previewRender` so Marked.js's output is never what the user actually sees. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| *(none — by design)* | N/A | This milestone explicitly adds **zero** client-side build tooling (no npm, no bundler). EasyMDE's pre-built `dist/easymde.min.js` + `dist/easymde.min.css` are downloaded once and either (a) vendored as static files under `wwwroot/lib/easymde/` and referenced with `<script src="~/lib/easymde/easymde.min.js">`, or (b) referenced directly from a CDN with a Subresource Integrity hash, matching the actual pattern already used for both Cropper.js and Font Awesome in this codebase today (see Gotcha note below). |

## Server-Side Pipeline Configuration

Build **one** `MarkdownPipeline` (via `MarkdownPipelineBuilder`) as a singleton — `MarkdownPipelineBuilder.Build()` is not free, and the resulting pipeline is safe to reuse and pass into every `Markdown.ToHtml(text, pipeline)` call. Recommended shape, deliberately **not** `.UseAdvancedExtensions()` (see Gotchas):

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseAutoLinks()                        // bare URLs pasted into descriptions become clickable
    .DisableHtml()                         // encode raw HTML as text instead of passing it through
    // .UseSoftlineBreakAsHardlineBreak()  // OPTIONAL — see "Stack Patterns by Variant" below
    .Build();

var rawHtml = Markdown.ToHtml(markdownText, pipeline);
var safeHtml = sanitizer.Sanitize(rawHtml);   // Ganss.Xss.HtmlSanitizer instance, see below
```

Configure the `HtmlSanitizer` instance narrowly rather than trusting its ~82-tag default allowlist (which includes `form`, `input`, `button`, `textarea`, etc. — irrelevant and unnecessarily permissive for rendered prose):

```csharp
var sanitizer = new HtmlSanitizer();
sanitizer.AllowedTags.Clear();
sanitizer.AllowedTags.UnionWith(new[] {
    "p", "br", "strong", "em", "del", "code", "pre",
    "h1", "h2", "h3", "h4", "h5", "h6",
    "ul", "ol", "li", "blockquote", "a", "hr"
});
sanitizer.AllowedSchemes.Clear();
sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
```

Register both the built `MarkdownPipeline` and the configured `HtmlSanitizer` as singletons, wrapped by one small `IMarkdownRenderer`/`MarkdownRenderer` service in **`QuestBoard.Domain`** (business-logic layer, per this repo's Service → Domain → Repository dependency rule) with a single `ToSafeHtml(string? markdown) : string` method. This is the one piece of plumbing that makes "identical rendering everywhere" actually true — see next section.

## Reuse Across `.cshtml` Pages and `.razor` Email Components

Both rendering surfaces already resolve services through the same DI container (the `.razor` email templates are rendered via `HtmlRenderer` inside Hangfire jobs using `IServiceScopeFactory`, matching the existing `IEmailService` pattern this app already uses in all 4 email jobs). Inject the same `IMarkdownRenderer` into both:

- In a `.cshtml` view: `@Html.Raw(Model.DescriptionHtml)` where `DescriptionHtml` was set by a controller/service call to `markdownRenderer.ToSafeHtml(quest.Description)`.
- In a `.razor` email component: `@((MarkupString)MarkdownRenderer.ToSafeHtml(Quest.Description))` — Razor Components use `MarkupString` (not `Html.Raw`) to opt out of Blazor's default HTML-encoding of interpolated strings.

Because it is the exact same `MarkdownPipeline` + `HtmlSanitizer` instance invoked through the exact same method in both cases, there is **no** possibility of the two surfaces drifting — this directly satisfies the milestone's "formatting is guaranteed consistent everywhere it appears" goal, and is strictly safer than maintaining two separate rendering code paths (one for MVC, one for Blazor-style components).

## Client-Side Preview: AJAX Round-Trip vs Client-Side Parser (Explicit Recommendation)

**Recommendation: AJAX round-trip to the server, reusing the exact `IMarkdownRenderer` pipeline. Do not trust EasyMDE's bundled Marked.js output as the real preview.**

This is a genuine architecture risk worth calling out explicitly, as requested:

- EasyMDE's default `previewRender` calls **Marked.js 4.1.x**, a *different* CommonMark/GFM implementation than the server's Markdig 1.3.2. The two parsers have different extension defaults (autolink detection heuristics, table dialects, how raw HTML is treated, edge-case list/paragraph handling). They will not always agree byte-for-byte, and for prose like "Session Recap" or "Quest Description" the mismatches that do occur (e.g. a soft-break rendering as `<br>` client-side but not server-side, or vice versa) would be exactly the kind of "preview lied to me" bug that erodes trust in a Preview button.
- EasyMDE's `previewRender` option is documented to accept an **async** implementation: `previewRender(plainText, previewElement)` may return a placeholder synchronously and later set `previewElement.innerHTML` once an async fetch resolves. This is the exact hook needed to swap in a server round-trip with no other EasyMDE internals touched:

```javascript
const easyMDE = new EasyMDE({
  element: document.getElementById('descriptionInput'),
  autoDownloadFontAwesome: false,
  toolbar: ["bold", "italic", "heading", "|", "unordered-list", "ordered-list", "|", "preview"],
  previewRender: function (plainText, previewElement) {
    fetch('/markdown/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiForgeryToken },
      body: JSON.stringify({ markdown: plainText })
    })
      .then(r => r.text())
      .then(html => { previewElement.innerHTML = html; });
    return previewElement.innerHTML; // shown until the fetch resolves
  }
});
```

- The `POST /markdown/preview` endpoint is a few lines: accept the raw Markdown, call the same `IMarkdownRenderer.ToSafeHtml(...)` used for final storage/display, return the HTML fragment. It must require authentication (not anonymous) and carry the standard antiforgery token like every other POST in this app, but needs no database access and no new EF entity — it's a pure pass-through render call. At 17 members, the extra request-per-preview-click has no meaningful latency cost.
- This also means the client never needs its **own** correct Markdown parser at all — Marked.js still ships inside the EasyMDE bundle (no build step to remove it), but nothing in the trust boundary depends on its output being correct. That is the safest place to be for a requirement that explicitly says formatting must be "guaranteed consistent."

If bundle size (not correctness) were the overriding concern, TinyMDE (see Alternatives) avoids shipping a second parser entirely — but it has no built-in Preview toggle to hook into, so the round-trip wiring becomes fully hand-rolled instead of a one-option override. For this milestone's explicit "Preview toggle + 4-button toolbar" scope, EasyMDE + the `previewRender` override is less custom code overall.

## Installation

```bash
# Server-side — from QuestBoard.Domain (business-logic layer; the renderer
# service belongs here per this repo's Service → Domain → Repository rule,
# so it's reachable from both MVC controllers/views and Hangfire email jobs)
dotnet add QuestBoard.Domain package Markdig --version 1.3.2
dotnet add QuestBoard.Domain package HtmlSanitizer --version 9.0.892
```

```bash
# Client-side — no npm, no package.json. Download the two built files once
# and commit them as static assets (matches the Cropper.js precedent):
#   https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.js   (327 KB)
#   https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.css  (13 KB)
# → QuestBoard.Service/wwwroot/lib/easymde/easymde.min.js
# → QuestBoard.Service/wwwroot/lib/easymde/easymde.min.css
```

**Note on "vendored" vs CDN:** the milestone context describes Cropper.js as "vendored... zero build step," but the actual code in this repo (`Views/Characters/Edit.cshtml`) loads Cropper.js from `cdn.jsdelivr.net` with a `<script integrity="sha384-..." crossorigin="anonymous">` SRI tag, not a locally-committed file — and Font Awesome is loaded from `cdnjs.cloudflare.com` with no SRI hash at all. If the actual goal is "zero build step" (true either way) rather than strictly "zero external network dependency" (not actually true for the two existing libraries), CDN + SRI for EasyMDE is a legitimate, precedent-matching alternative to committing the files locally. Given the question explicitly asked for self-hosted candidates, the recommendation above defaults to committing the static files — but flag this discrepancy to whoever owns REQUIREMENTS.md so the decision is made consciously rather than by accident.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Markdig + Ganss.Xss.HtmlSanitizer (composed manually) | `Westwind.AspNetCore.Markdown` (wraps Markdig + a sanitizer + adds TagHelpers/a page-handler middleware for serving whole `.md` files as pages) | If the app needed to serve entire Markdown *files* as pages (a docs/wiki-style site). This milestone needs a plain, narrowly-scoped `string → string` render call reused identically from MVC controllers and Blazor-style email components — the extra middleware/TagHelper surface `Westwind.AspNetCore.Markdown` adds is unneeded abstraction here, and its opinionated default pipeline is harder to align exactly with the DisableHtml + custom-sanitizer configuration this app needs. |
| EasyMDE 2.21.0 | TinyMDE 0.2.31 | If minimizing client payload matters more than shipping speed: TinyMDE is ~96 KB total (JS+CSS) vs EasyMDE's ~340 KB, has zero bundled Markdown-parser dependency, and is dependency-free. Trade-off: TinyMDE has no built-in Preview-toggle mode (only live inline formatting while typing) and no built-in toolbar "preview" action — both would need to be hand-built, including the show/hide-textarea and AJAX-preview wiring EasyMDE gives for free via `previewRender`. |
| EasyMDE's default toolbar trimmed to 5 buttons | Hand-rolled toolbar (plain `<button>`s that insert `**`/`*`/`#`/`-` at the textarea cursor via `selectionStart`/`selectionEnd`) | If even EasyMDE's 327 KB felt too heavy and CodeMirror's editing affordances (syntax highlighting while typing, keyboard shortcuts) aren't wanted. This is a viable, genuinely zero-dependency option — a `document.execCommand`-free cursor-insertion helper is ~40 lines of vanilla JS — but it re-implements exactly what EasyMDE already ships, and the Preview toggle would still need to be built by hand either way. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| SimpleMDE | The original library EasyMDE forked from — no releases since 2017, unmaintained, and the search results above confirm EasyMDE is the maintained continuation of the exact same API surface | EasyMDE 2.21.0 |
| Any React/Vue-based Markdown editor (Toast UI Editor's modern ESM builds, Milkdown, `react-md-editor`, `@uiw/react-markdown-editor`, etc.) | All require an npm install + a bundler (webpack/vite/rollup) to consume sanely; several ship ESM-only with no usable single-file UMD/CDN build. This app has zero client-side build pipeline by design (no `package.json`, no `node_modules`) — introducing one is exactly the kind of scope creep this research question was designed to head off | EasyMDE's pre-built UMD `dist/easymde.min.js`, a plain `<script>` tag |
| `MarkdownPipelineBuilder().UseAdvancedExtensions()` as a reflexive default | Bundles `UseGenericAttributes()`, which parses `{#id .class attr=value}` syntax and lets a markdown author attach **arbitrary HTML attributes** — including event-handler attributes like `onmouseover="..."` — to rendered elements. This is attribute injection, not raw-HTML pass-through, so `.DisableHtml()` does **not** block it | Opt into only the specific extensions the 9 target fields actually need (e.g. `.UseAutoLinks()`); if a broader extension set is ever wanted, keep the mandatory `HtmlSanitizer` pass afterward regardless — treat sanitization as the actual security boundary, never the pipeline choice |
| Trusting `.DisableHtml()` alone as "this is now XSS-safe" | `DisableHtml()` only stops raw HTML blocks/inline HTML from passing through unescaped. It does **not** validate or block dangerous URL schemes in ordinary Markdown link syntax — `[click me](javascript:alert(1))` and the autolink form `<javascript:alert(1)>` both still render as a live `<a href="javascript:...">` unless something downstream strips the scheme. This is not hypothetical: NuGetGallery shipped and patched exactly this class of bug in its Markdown autolink handling (GHSA-gwjh-c548-f787) | Always finish with `HtmlSanitizer`'s `AllowedSchemes` allowlist (`http`, `https`, `mailto` — no `javascript:`, no `data:`) as the actual enforcement point, independent of whatever the Markdown pipeline does |
| Shipping EasyMDE's default `previewRender` (Marked.js) as the real Preview | Marked.js and Markdig are different parsers with different defaults; the two will diverge on some inputs, breaking the "consistent everywhere" requirement and eroding trust in the Preview button specifically | Override `previewRender` with an async function that calls a small server-side `POST /markdown/preview` endpoint backed by the same `IMarkdownRenderer` used for final storage/display (see dedicated section above) |

## Stack Patterns by Variant

**If REQUIREMENTS.md finalizes strict CommonMark paragraph rules** (this is what `PROJECT.md`'s current v8.0 milestone text already states as the target — "blank line separates paragraphs, replacing today's line-break-preserving plain-text display"):
- Leave `.UseSoftlineBreakAsHardlineBreak()` **out** of the pipeline builder
- A single Enter press inside a paragraph is ignored by the renderer (folded into the same paragraph) exactly per CommonMark spec; only a blank line starts a new paragraph
- This is a genuine behavior change from today's `white-space: pre-wrap` fields (Phase 64) — authors used to relying on single Enter presses for visual line breaks will need either two Enters (new paragraph) or a trailing double-space / explicit `<br>`-producing markdown (a backslash line-ending) for a hard break within a paragraph

**If a later decision reverses course and wants single Enter → visible line break** (matching today's pre-wrap behavior more closely, in case user testing during this milestone finds strict CommonMark surprising for casual authors):
- Add `.UseSoftlineBreakAsHardlineBreak()` to the same pipeline builder — this is a single extension-method call, not a rewrite
- Every soft line break (a newline that is not followed by a blank line) renders as `<br>` instead of being folded into the paragraph
- Because this is a single toggle on the shared pipeline, switching between the two behaviors later costs one line of code, not a new library — this de-risks locking in strict CommonMark now while a later requirements pass could still cheaply reverse it

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| Markdig 1.3.2 | .NET 10 / ASP.NET Core 10 | Multi-targets `netstandard2.0`, `net462`, `net8.0`; no `net10.0`-specific TFM exists or is required — .NET 10 projects consume the `net8.0`/`netstandard2.0` build transparently, same pattern already relied on elsewhere in this app's dependency set |
| HtmlSanitizer 9.0.892 | .NET 10 / ASP.NET Core 10 | Same multi-target shape (`netstandard2.0`, `net462`, `net8.0`) as Markdig; AngleSharp-based parsing, no native/unmanaged dependency (unlike the project's currently-paused SkiaSharp plan) |
| EasyMDE 2.21.0 | Any evergreen browser, no bundler required | Single self-contained minified UMD file; bundles CodeMirror ~5.65.x and Marked ~4.1.x internally — these are pinned by EasyMDE's own `package.json`, not something this app manages or updates independently |
| EasyMDE 2.21.0 | Font Awesome 6.4.0 (already loaded app-wide) | EasyMDE's toolbar icon classes are Font-Awesome-5/6-style (`fas`/`far`); confirmed compatible with the already-loaded FA 6.4.0. Set `autoDownloadFontAwesome: false` to skip EasyMDE's own detection/injection logic since FA is already present on every page |

## Sources

- https://www.nuget.org/packages/Markdig — NuGet package page, confirmed latest stable 1.3.2, published 2026-06-18
- https://api.nuget.org/v3-flatcontainer/markdig/index.json — NuGet v3 flat-container API, full version list, confirms 1.3.2 is the newest entry (HIGH confidence, primary source)
- https://github.com/xoofx/markdig — official repo readme; confirmed `UseAdvancedExtensions()`, `UseGenericAttributes()` composition, "Soft lines as hard lines" extension listing
- https://github.com/xoofx/markdig/blob/master/src/Markdig/MarkdownExtensions.cs — confirmed exact method signature `UseSoftlineBreakAsHardlineBreak(this MarkdownPipelineBuilder)` and its XML-doc summary (HIGH confidence, source code)
- https://xoofx.github.io/markdig/docs/usage/ — official usage docs; confirmed `DisableHtml()` code shape and `Markdown.ToHtml(text, pipeline)` call pattern
- https://weblog.west-wind.com/posts/2018/Aug/31/Markdown-and-Cross-Site-Scripting — Rick Strahl (maintainer of Westwind.AspNetCore.Markdown), confirms `.DisableHtml()` purpose/limits and the ASP.NET Core `services.AddMarkdown(...).DisableHtml()` wiring pattern (MEDIUM→HIGH, respected .NET community source, cross-checked against official docs)
- https://github.com/NuGet/NuGetGallery/security/advisories/GHSA-gwjh-c548-f787 — real-world confirmed vulnerability class: Markdown autolink `javascript:` scheme bypassing `DisableHtml()`-style raw-HTML defenses (HIGH confidence, official GitHub security advisory)
- https://www.nuget.org/packages/htmlsanitizer / https://api.nuget.org/v3-flatcontainer/htmlsanitizer/index.json — confirmed latest stable 9.0.892 (2026-02-02); 9.1.x entries are `-beta` prereleases, correctly excluded
- https://github.com/mganss/HtmlSanitizer — official repo; confirmed AngleSharp-based sanitization, default `AllowedSchemes` (http/https/protocol-relative), ~82-tag default `AllowedTags` list, thread-safety guarantee for a shared instance, and `netstandard2.0`/`net462`/`net8.0` target support
- https://registry.npmjs.org/easymde/latest — npm registry API, confirmed latest `2.21.0`, dependencies `marked ^4.1.0` and `codemirror ^5.65.15` bundled into the dist build (HIGH confidence, primary source)
- https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.js and .../easymde.min.css — measured actual `Content-Length` via CDN response headers: 327,475 bytes (JS) + 12,923 bytes (CSS)
- https://github.com/Ionaru/easy-markdown-editor — official repo; confirmed default toolbar array includes `preview`/`side-by-side`, `previewRender` override API (sync and async signatures), Font Awesome auto-detection behavior, `toolbar` customization API
- https://registry.npmjs.org/tiny-markdown-editor/latest — npm registry API, confirmed latest `0.2.31`, minimal dependency footprint (`core-js` only, no bundled Markdown parser)
- https://cdn.jsdelivr.net/npm/tiny-markdown-editor@0.2.31/dist/tiny-mde.min.js and .../tiny-mde.min.css — measured actual `Content-Length`: 92,583 bytes (JS) + 3,481 bytes (CSS)
- https://github.com/jefago/tiny-markdown-editor — official repo; confirmed no distinct Preview-toggle mode (inline live formatting only), customizable `commands` toolbar API
- Local repo inspection (`QuestBoard.Service/Views/Shared/_Layout.cshtml`, `QuestBoard.Service/Views/Characters/Edit.cshtml`) — confirmed actual current precedent: Font Awesome 6.4.0 loaded via `cdnjs.cloudflare.com` (no SRI), Cropper.js 2.1.1 loaded via `cdn.jsdelivr.net` with an SRI `integrity` hash — both CDN-hosted, not locally vendored files, despite the milestone context's "vendored" framing

---
*Stack research for: Markdown editing/rendering support across QuestBoard (v8.0 milestone)*
*Researched: 2026-07-09*
