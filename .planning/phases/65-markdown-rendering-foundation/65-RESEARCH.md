# Phase 65: Markdown Rendering Foundation - Research

**Researched:** 2026-07-09
**Domain:** Server-side Markdown-to-HTML rendering pipeline (Markdig + Ganss.Xss HtmlSanitizer) for ASP.NET Core 10 / .NET 10, Domain-layer service design, dual-sanitizer-profile architecture
**Confidence:** HIGH — every claim about Markdig's extension methods, exact HTML output shapes, and HtmlSanitizer's tag/attribute-removal semantics was verified by reading the actual library source code (not training-data recall or summarized secondary sources). Package versions re-verified today directly against the NuGet v3 API.

## Summary

This phase builds one Domain-layer service that wraps a single shared Markdig `MarkdownPipeline` and **two** separate `Ganss.Xss.HtmlSanitizer` instances (web profile allows `<img>`, email profile strips it — D-06/D-07). The milestone-level `STACK.md`/`ARCHITECTURE.md` already established the package choices (Markdig 1.3.2, HtmlSanitizer 9.0.892) and the single-service-two-adapters shape; this research goes one level deeper into the specific technical mechanics CONTEXT.md's D-01–D-08 decisions require, verified directly against source code on GitHub (both `xoofx/markdig` and `mganss/HtmlSanitizer`, `master` branch, fetched 2026-07-09).

Three findings materially change how precise the sanitizer allowlist must be, beyond what the milestone-level research covered:

1. **`HtmlSanitizer.KeepChildNodes` defaults to `false`.** This is the single most important finding in this research. It means an HTML tag that is *not* in `AllowedTags` does not just get its wrapper stripped while keeping the inner text (the intuitive assumption) — the sanitizer deletes the **entire subtree**, text content included. Forgetting to add `table`/`thead`/`tbody`/`tr`/`th`/`td` to the allowlist doesn't degrade a pipe table to plain text — it silently deletes every word inside it. The same applies to `dl`/`dt`/`dd` (definition lists) and the `div` that wraps the footnotes section. This must be treated as a hard requirement in the sanitizer configuration, not a nice-to-have.
2. **Markdig's native CommonMark autolink (`<scheme:...>`) is not disabled by `.DisableHtml()`**, and it accepts *any* syntactically valid scheme — including `javascript:`. `.DisableHtml()` only removes the `HtmlBlockParser` and turns off the *fallback* raw-inline-HTML branch inside `AutolinkInlineParser`; the primary autolink-matching branch (`LinkHelper.TryParseAutolink`) runs unconditionally. So `<javascript:alert(1)>` typed literally in a description still produces a live `<a href="javascript:alert(1)">` from Markdig — the `HtmlSanitizer`'s `AllowedSchemes` allowlist on the `href`/`src` `UriAttributes` is the *only* thing that blocks it, confirmed by reading `AutolinkInlineParser.Match()` directly. This is exactly the "javascript:-scheme link... inside autolink syntax" test case the phase's success criteria call for.
3. **Ganss.Xss's default `AllowedAttributes` list contains no `id` and no `class`.** Footnotes (`D-01`) are functionally useless without `id` (the `fnref:N`/`fn:N` jump-link pairing depends on it) — this is a required addition, not optional, and needs to be called out as a deliberate, scoped exception (values are Markdig-generated sequential tokens like `fn:1`, `fn:2`, never arbitrary user text, which bounds — but does not eliminate — DOM-clobbering-adjacent risk).

**Primary recommendation:** Compose the pipeline from six individual `Use*()` calls (never `.UseAdvancedExtensions()`), build it once as a singleton, and construct exactly two long-lived `HtmlSanitizer` instances at startup (never reconfigure a shared instance per-call — `Sanitize()` is documented thread-safe only when the instance's properties are set once and never mutated concurrently with in-flight calls). See Code Examples for the exact composition.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RENDER-01 | Markdown text in any of the 9 target fields is converted to safe, sanitized HTML — no raw HTML or script execution possible | Verified `.DisableHtml()` mechanics (blocks both block-level and inline raw HTML recognition at the parser level — confirmed via `AutolinkInlineParser` source) + verified exact `HtmlSanitizer` `AllowedSchemes`/`UriAttributes`/`AllowedTags` configuration needed to close the residual `javascript:`-autolink and generic-attribute-injection gaps that `.DisableHtml()` alone does not close |
| RENDER-02 | Page views and the 3 HTML email templates use the exact same rendering pipeline — no duplicated parsing logic | Verified the two-sanitizer-profile pattern shares ONE Markdig parse (one `MarkdownPipeline`, one `Markdown.ToHtml()` call) with sanitization branching only at the post-parse policy step — confirmed via `HtmlSanitizer`'s thread-safety contract that this requires two separate sanitizer *instances*, not one reconfigurable instance, which is the concrete implementation shape for D-06/D-07/D-08 |
| RENDER-03 | A blank line (not a single Enter) is required to start a new paragraph (strict CommonMark) | Confirmed by omitting `.UseSoftlineBreakAsHardlineBreak()` from the pipeline (already established at milestone level); this phase adds concrete unit test payload shapes for both the single-Enter and blank-line cases |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Markdown parsing (CommonMark + selected GFM extensions) | API / Backend (Domain layer) | — | Pure `string? → string` transformation, no UI concerns; must be identical for MVC and email consumers per RENDER-02 |
| HTML sanitization (XSS defense) | API / Backend (Domain layer) | — | Security boundary must live server-side, applied uniformly regardless of which surface (web page vs. email) later calls it |
| Sanitizer profile selection (web vs. email) | API / Backend (Domain layer) | — | A policy decision made once, at the point of rendering, not duplicated per caller — the *caller* (Phase 66+ MVC helper or Razor email component) only says which profile it wants, never touches sanitizer configuration itself |
| NuGet package registration | API / Backend (`QuestBoard.Domain.csproj`) | — | Matches existing `AutoMapper` precedent — pure managed libraries with no ASP.NET/EF dependency belong in Domain |
| Unit test coverage of XSS payloads and CommonMark edge cases | API / Backend (`QuestBoard.UnitTests/Services/`) | — | Domain-layer service, testable in isolation with zero MVC/Blazor machinery, per this project's established `ImageValidationServiceTests.cs` pattern |

No Browser/Client, Frontend-SSR-rendering, or CDN/Static tier involvement in this phase — it is server-side plumbing only, confirmed by the phase's own "no live user-facing views change yet" boundary.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| **Markdig** | **1.3.2** | Markdown → HTML parsing | Re-verified today directly against `api.nuget.org/v3-flatcontainer/markdig/index.json` — `1.3.2` remains the newest published version (no newer release since the milestone-level research). `[VERIFIED: NuGet registry + official GitHub source]` |
| **HtmlSanitizer** (`Ganss.Xss` namespace) | **9.0.892** | Post-parse HTML allowlist sanitization | Re-verified today directly against `api.nuget.org/v3-flatcontainer/htmlsanitizer/index.json` — `9.0.892` remains the newest **stable** version (`9.1.x` entries are all `-beta` prereleases). `[VERIFIED: NuGet registry + official GitHub source]` |

**Installation** (unchanged from milestone research, re-confirmed):
```bash
dotnet add QuestBoard.Domain package Markdig --version 1.3.2
dotnet add QuestBoard.Domain package HtmlSanitizer --version 9.0.892
```

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Composing 6 individual `Use*()` extension calls | `.UseAdvancedExtensions()` followed by manually removing `GenericAttributesExtension` from `pipeline.Extensions` afterward | **Rejected.** Confirmed via source (`MarkdownExtensions.cs`) that `UseAdvancedExtensions()` unconditionally chains 19 extensions — not just the 6 this phase wants — including `AlertBlocks`, `Abbreviations`, `AutoIdentifiers`, `Citations`, `CustomContainers`, `Figures`, `Footers`, `GridTables`, `Mathematics`, `MediaLinks`, `ListExtras`, `Diagrams`, none of which D-01 asked for. Post-hoc extension removal is also fragile (undocumented API surface, easy to regress on a Markdig upgrade) versus the individually-composed list being self-documenting and exactly matching D-01's explicit scope. |
| Two separate `HtmlSanitizer` instances (web/email) | One `HtmlSanitizer` instance, reconfigured (`AllowedTags.Remove("img")` / `.Add("img")`) immediately before each `Sanitize()` call | **Rejected.** `HtmlSanitizer`'s own documentation states `Sanitize()`/`SanitizeDocument()` are thread-safe "provided you do not simultaneously set instance or static properties" — i.e., safe for concurrent reads once configured, NOT safe for concurrent configure-then-call in a multi-request ASP.NET Core app. A registered singleton service serving concurrent requests would race on the shared instance's mutable `AllowedTags` set. Two immutable-after-construction instances is the only safe pattern for a singleton-registered service. |
| Allowing `class`/`id` broadly via Ganss.Xss defaults | Restoring the library's full ~82-tag/~80-attribute out-of-box default allowlist instead of narrowing it | **Rejected** (already established at milestone level, reaffirmed here) — the out-of-box default includes `form`, `button`, `select`, `textarea`, and dozens of attributes with no use case in this app's 9 free-text fields. Narrowing remains correct; this phase's contribution is identifying exactly which of the narrow additions (`table` family, `dl`/`dt`/`dd`, `input`, `sup`, `div`, `id`) are required by the newly-enabled D-01 extensions specifically, rather than guessing. |

## Package Legitimacy Audit

> `slopcheck` (installed successfully this session) supports `pypi`, `npm`, `crates.io`, `go`, `rubygems`, `maven`, `packagist` — **NuGet/.NET is not a supported ecosystem** for this tool. Verification instead relied on the two authoritative sources the source hierarchy ranks above unverified WebSearch: the official NuGet v3 registry API and the packages' own official GitHub source repositories, both fetched and read directly this session (not summarized secondhand).

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Markdig | NuGet | Long-established (versioning back through 0.x for years; current major line 1.x since 2026) | 67M+ (per milestone-level STACK.md, corroborated by the package's prominence as the de facto .NET CommonMark processor) | `github.com/xoofx/markdig` — confirmed live, active, matches package metadata | N/A (ecosystem unsupported) | Approved — manually verified via NuGet v3 API + direct source-code read of `master` branch |
| HtmlSanitizer (`Ganss.Xss`) | NuGet | Long-established | Widely used as *the* standard .NET HTML sanitizer (cited independently by Rick Strahl's ASP.NET Core Markdown writeup and general .NET XSS guidance per milestone STACK.md) | `github.com/mganss/HtmlSanitizer` — confirmed live, active, matches package metadata | N/A (ecosystem unsupported) | Approved — manually verified via NuGet v3 API + direct source-code read of `master` branch |

**Packages removed due to slopcheck `[SLOP]` verdict:** none (slopcheck did not run against these — ecosystem unsupported).
**Packages flagged as suspicious `[SUS]`:** none.

Both packages are tagged `[VERIFIED: NuGet registry + official GitHub source]` rather than `[ASSUMED]` — this session read the actual renderer/parser/sanitizer source files (not just the README) for both libraries, which is a stronger verification bar than slopcheck's registry-metadata heuristics would have provided even if the ecosystem were supported.

## Architecture Patterns

### System Architecture Diagram — the two-sanitizer-profile detail (new in this phase, extends milestone ARCHITECTURE.md)

```
 Markdown text (string?)
          │
          ▼
┌──────────────────────────────────────┐
│  MarkdownPipeline (ONE instance,      │   built once via MarkdownPipelineBuilder:
│  built once, registered as a          │     .DisableHtml()
│  singleton, reused for every call)    │     .UseAutoLinks()
│                                        │     .UsePipeTables()
│                                        │     .UseTaskLists()
│                                        │     .UseDefinitionLists()
│                                        │     .UseFootnotes()
│                                        │     .UseEmphasisExtras(Strikethrough)
└───────────────────┬────────────────────┘
                     │  Markdig.Markdown.ToHtml(text, pipeline)
                     │  — parsed EXACTLY ONCE regardless of target
                     ▼
              rawHtml (string)
                     │
         ┌───────────┴────────────┐
         ▼                        ▼
 ┌──────────────────┐    ┌──────────────────────┐
 │ Web HtmlSanitizer │    │ Email HtmlSanitizer  │   TWO separate singleton
 │ instance          │    │ instance             │   instances, each built
 │ AllowedTags        │    │ AllowedTags           │   ONCE at DI registration
 │  includes "img"    │    │  excludes "img"       │   time and never mutated
 └─────────┬──────────┘    └───────────┬───────────┘   thereafter (required for
           │                           │                thread-safety — see
           ▼                           ▼                Alternatives Considered)
   safeHtml (web)              safeHtml (email)
           │                           │
           ▼                           ▼
   Html.Markdown() helper      .razor @inject
   (.cshtml views —            (QuestFinalized,
    Phase 66+)                  SessionReminder,
                                 WaitlistPromoted —
                                 Phase 66/67)
```

This satisfies RENDER-02 ("no separate or duplicated parsing logic") because the branch point is strictly *after* parsing — both profiles consume byte-identical `rawHtml`. Only the allowlist policy differs.

### Recommended Project Structure

```
QuestBoard.Domain/
├── Interfaces/
│   └── IMarkdownService.cs      # NEW — contract (naming is Claude's Discretion per CONTEXT.md)
├── Services/
│   └── MarkdownService.cs       # NEW — internal class (matches ImageValidationService precedent);
│                                 #       owns the MarkdownPipeline + both HtmlSanitizer instances
├── Extensions/
│   └── ServiceExtensions.cs     # MODIFIED — AddSingleton<IMarkdownService, MarkdownService>()
└── QuestBoard.Domain.csproj     # MODIFIED — + Markdig 1.3.2, + HtmlSanitizer 9.0.892

QuestBoard.UnitTests/
└── Services/
    └── MarkdownServiceTests.cs  # NEW — XSS payload tests + CommonMark paragraph tests +
                                  #       per-extension HTML-shape tests (task list, footnotes, etc.)
```

No `QuestBoard.Repository` or `QuestBoard.Service` changes in this phase — confirmed no entities/DbContext/views are touched (matches the phase's own "no live user-facing views change yet" boundary).

### Pattern 1: Single method, target-scoped by parameter (not two interfaces)

**What:** Expose exactly one public rendering method that takes a target/profile discriminator, rather than two differently-named methods or two interfaces.
**When to use:** Whenever RENDER-02's "single shared pipeline" requirement needs to be enforced at the type level, not just by convention.
**Example:**
```csharp
// QuestBoard.Domain/Interfaces/IMarkdownService.cs
namespace QuestBoard.Domain.Interfaces;

public enum MarkdownRenderTarget { Web, Email }

public interface IMarkdownService
{
    string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web);
}
```
This keeps exactly one call site shape for every consumer (`service.RenderToHtml(text)` for web, `service.RenderToHtml(text, MarkdownRenderTarget.Email)` for the 3 email components), while internally the method only branches at the sanitizer-selection step — the parse call is identical either way. This is a recommendation, not a locked decision; CONTEXT.md leaves exact naming to the planner.

### Pattern 2: Individually-composed Markdig pipeline (never `.UseAdvancedExtensions()`)

**What:** Build the pipeline from the 6 specific extension calls D-01 needs.
**When to use:** Always, for this phase — this is the D-04 critical security constraint, not a style preference.
**Example (verified against `xoofx/markdig` `master` branch source, 2026-07-09):**
```csharp
// Source: https://github.com/xoofx/markdig/blob/master/src/Markdig/MarkdownExtensions.cs
using Markdig;
using Markdig.Extensions.EmphasisExtras;

var pipeline = new MarkdownPipelineBuilder()
    .DisableHtml()                                       // blocks BOTH block-level and inline raw HTML
    .UseAutoLinks()                                       // bare http(s)://, ftp://, mailto:, www. text → <a>
    .UsePipeTables()                                       // GFM pipe tables → table/thead/tbody/tr/th/td
    .UseTaskLists()                                        // - [ ]/- [x] → disabled <input type="checkbox">
    .UseDefinitionLists()                                  // Term\n: Definition → dl/dt/dd
    .UseFootnotes()                                        // [^1] ... [^1]: text → sup/a + trailing <div><ol><li>
    .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough) // ~~text~~ ONLY → <del> (NOT sub/sup/ins/mark)
    .Build();
```
`EmphasisExtraOptions.Strikethrough` alone (verified via `EmphasisExtraExtension.Setup()` source) sets the internal delimiter requirement to *exactly* 2 tilde characters — a single `~` (which would otherwise be Pandoc-style subscript) has no matching descriptor and stays literal text. Superscript (`^^`), inserted (`++`), and marked (`==`) syntax are not recognized at all since their flags are never set. This exact call is confirmed to enable **only** GFM strikethrough, nothing more.

### Pattern 3: Two immutable-after-construction `HtmlSanitizer` instances sharing a common `HtmlSanitizerOptions` base

**What:** Build a shared base config, derive two variants, construct two separate sanitizer instances.
**When to use:** Whenever the same parsed content needs two different output policies without duplicating parsing (D-06/D-07/D-08).
**Example (verified against `mganss/HtmlSanitizer` `master` branch source, 2026-07-09):**
```csharp
// Source: https://github.com/mganss/HtmlSanitizer/blob/master/src/HtmlSanitizer/HtmlSanitizerOptions.cs
using Ganss.Xss;

var baseTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    // Base CommonMark
    "p", "br", "strong", "em", "code", "pre", "blockquote", "hr", "a",
    "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
    // D-03 Strikethrough
    "del",
    // D-01 Pipe tables — REQUIRED: KeepChildNodes defaults to false, so omitting any one of
    // these deletes the ENTIRE cell/row content, not just the wrapping tag (see Pitfalls)
    "table", "thead", "tbody", "tr", "th", "td",
    // D-01/D-05 Task lists — the checkbox itself
    "input",
    // D-01 Definition lists — REQUIRED, same KeepChildNodes risk as tables
    "dl", "dt", "dd",
    // D-01 Footnotes — REQUIRED: "sup" for the reference number, "div" for the footnote-group
    // wrapper (KeepChildNodes=false means omitting "div" deletes the ENTIRE footnotes section)
    "sup", "div",
};

var webOptions = new HtmlSanitizerOptions
{
    AllowedTags = new HashSet<string>(baseTags, StringComparer.OrdinalIgnoreCase) { "img" }, // D-06
    AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "href",       // <a>
        "src", "alt", // <img> — web profile only
        "id",         // footnote fnref:N / fn:N pairing — see Pitfalls for the DOM-clobbering note
        "type", "disabled", "checked", // narrow <input> shape per D-05
    },
    AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
    UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
};

var emailOptions = new HtmlSanitizerOptions
{
    AllowedTags = new HashSet<string>(baseTags, StringComparer.OrdinalIgnoreCase), // D-07: "img" excluded
    AllowedAttributes = webOptions.AllowedAttributes, // shared — email needs no "src"/"alt" since no <img>
    AllowedSchemes = webOptions.AllowedSchemes,
    UriAttributes = webOptions.UriAttributes,
};

var webSanitizer = new HtmlSanitizer(webOptions);     // singleton #1
var emailSanitizer = new HtmlSanitizer(emailOptions); // singleton #2
```
Both instances are constructed once (e.g., in `MarkdownService`'s constructor, or as two singleton-registered fields) and never have their properties mutated afterward — this is what makes concurrent `Sanitize()` calls from multiple ASP.NET Core requests safe, per the library's own thread-safety contract.

**Note on `class`:** Ganss.Xss's defaults exclude `class` (documented "classjacking" prevention). Markdig's footnote renderer emits fixed literal classes (`footnote-ref`, `footnote-back-ref`, `footnotes`) that are not attacker-influenceable. If CSS hooks for these are wanted later, add `class` to `AllowedAttributes` **and** scope `AllowedCssClasses` to exactly those 3 literal strings (a dedicated `HtmlSanitizerOptions.AllowedCssClasses` property exists for this) rather than allowing arbitrary class values. Not required for this phase's 3 success criteria — flagged as Claude's Discretion per CONTEXT.md, left out of the baseline above for minimalism.

### Anti-Patterns to Avoid

- **Assuming a disallowed tag "just loses its formatting."** `HtmlSanitizer.DefaultKeepChildNodes = false` (confirmed via source: `RemoveTag()` calls `tag.Remove()`, not `tag.Replace(childNodes)`, when `KeepChildNodes` is false). An incomplete `AllowedTags` list for this phase's expanded D-01 scope doesn't degrade content — it deletes it. Treat every tag listed in "Code Examples" above as required, not optional, for the extensions D-01 turns on.
- **Reconfiguring one shared `HtmlSanitizer` instance per request/call to switch between web/email profiles.** Confirmed unsafe by the library's own thread-safety documentation — build two instances once, at startup.
- **Calling `.UseAdvancedExtensions()` "just this once" as a shortcut, planning to strip `UseGenericAttributes()` later.** Confirmed via source it bundles 19 extensions, not the 6 D-01 wants, and removing one extension post-hoc is unsupported/fragile API usage.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CommonMark + GFM parsing | A custom regex-based Markdown-to-HTML converter | Markdig 1.3.2 | CommonMark's grammar (delimiter-flanking rules, lazy paragraph continuation, setext-vs-ATX heading disambiguation) is notoriously hard to get bit-for-bit correct; Markdig is spec-compliant and this app already needs its exact output for the client-preview-fidelity requirement tracked in Phase 66 |
| HTML sanitization / XSS defense | A custom tag/attribute blacklist (regex-stripping `<script>`, blacklisting `javascript:`) | Ganss.Xss `HtmlSanitizer` | Blacklists are trivially bypassed (case variation, whitespace injection, encoded entities, nested tags); `HtmlSanitizer` uses AngleSharp's real HTML5 parser and an allowlist model, which is the only sound approach for untrusted-HTML-in-a-browser-context |
| URL scheme validation | A custom regex checking if a href starts with `javascript:` | `HtmlSanitizer.AllowedSchemes` + `UriAttributes` | Operates on the *parsed* URI (post-normalization), not a raw string prefix check — closes cases a naive regex would miss (e.g. leading whitespace/control characters before the scheme, which some regex blacklists fail to strip before matching) |

**Key insight:** Every hand-rolled alternative in this domain has a well-documented CVE-class history (see the milestone-level PITFALLS.md's citation of `GHSA-gwjh-c548-f787`, a real Markdown-autolink-XSS vulnerability in NuGetGallery itself). This phase's entire purpose is to centralize that risk into one professionally-maintained pair of libraries instead of re-deriving the same mistakes.

## Common Pitfalls

### Pitfall 1: `HtmlSanitizer`'s `KeepChildNodes = false` default turns an incomplete allowlist into silent content deletion, not graceful degradation

**What goes wrong:** A developer configuring the sanitizer allowlist for the D-01 extension set forgets one container tag (e.g. `table`, or the footnote-group `div`) assuming the worst case is "the table loses its borders" or "shows as plain text." Instead, `RemoveTag()` (verified in source) calls `tag.Remove()` — which removes the tag **and every descendant node**, including all text — whenever `KeepChildNodes` is false, which is the library default (`DefaultKeepChildNodes = false`).

**Why it happens:** Every other HTML sanitizer most .NET developers have used (and the general mental model of "sanitizing" HTML) treats disallowed tags as "unwrap, keep content" by default. Ganss.Xss inverts that default specifically because unwrapping is *not* always safe (e.g. unwrapping a disallowed `<script>` tag while keeping its text content would display raw JS source — arguably fine for text, but the library chose the more conservative default across the board).

**How to avoid:** Treat the "Code Examples" allowlist above as a hard checklist, cross-referenced against every enabled Markdig extension's actual renderer output (verified via source in this research, not guessed). Add a unit test per extension that asserts the *text content* survives sanitization (not just "no exception thrown") — e.g., render a pipe table, sanitize it, and assert the sanitized HTML still contains the cell text, not just that it doesn't throw.

**Warning signs:** A unit test only checks `sanitizedHtml.Should().NotBeNullOrEmpty()` instead of asserting specific expected substrings; a manual QA pass with a short table "looks fine" only because the tester didn't notice missing cells that happened to be at the end of a long paragraph.

### Pitfall 2: `.DisableHtml()` does not disable Markdig's native CommonMark autolink, and native autolinks accept any scheme including `javascript:`

**What goes wrong:** A developer assumes `.DisableHtml()` closes all "raw HTML in, HTML out" vectors uniformly. It does not — it removes `HtmlBlockParser` (block-level raw HTML) and sets `AutolinkInlineParser.Options.EnableHtmlParsing = false` (the parser's *inline raw-HTML-tag* fallback branch). But `AutolinkInlineParser.Match()` (verified via source) tries `LinkHelper.TryParseAutolink()` **first**, unconditionally — this is the native CommonMark spec's `<scheme:...>` autolink syntax, and it is never gated by the `EnableHtmlParsing` flag `.DisableHtml()` sets. CommonMark's autolink grammar accepts any 2-32 character scheme followed by `:` — `javascript` qualifies. So `<javascript:alert(document.cookie)>` typed literally in a Quest Description still becomes `<a href="javascript:alert(document.cookie)">javascript:alert(document.cookie)</a>` in Markdig's raw output, with `.DisableHtml()` fully enabled.

**Why it happens:** The name "DisableHtml" reads as "disables all raw-HTML-shaped output," but it specifically targets literal-HTML-tag recognition, not the separately-specified CommonMark autolink production rule (which produces an `<a>` tag through the *link* rendering path, not the *raw HTML* rendering path — a distinction that matters to Markdig's parser architecture but is invisible from the method name alone).

**How to avoid:** Never treat `.DisableHtml()` as the sole XSS defense for links/autolinks (the milestone-level PITFALLS.md already established this for `[text](url)` syntax; this research extends the same conclusion to angle-bracket autolinks specifically, with source-level confirmation). The `HtmlSanitizer`'s `AllowedSchemes` restricted to `http`/`https`/`mailto`, applied to the `href`/`src` `UriAttributes`, is what actually blocks this — verify with a dedicated unit test using literal angle-bracket autolink syntax, not just `[text](url)` syntax.

**Warning signs:** A test suite only covers `[text](javascript:...)` link syntax and treats the autolink case as "probably the same" without a dedicated test — the phase's own success criteria explicitly call out testing both forms.

### Pitfall 3: Footnotes require `id` on `<a>`/`<li>`, which is not in Ganss.Xss's default `AllowedAttributes` — a scoped exception, not an oversight to fix by allowing `id` everywhere

**What goes wrong:** A developer enables `.UseFootnotes()`, sees footnote numbers render, but the superscript reference links (`<a href="#fn:1">`) and the target anchors (`<li id="fn:1">`) don't actually jump anywhere after sanitization, because `id` was stripped (not in the default `AllowedAttributes` list — confirmed by reading the full default list in the library README, cross-checked against the actual attribute names Markdig's `HtmlFootnoteLinkRenderer`/`HtmlFootnoteGroupRenderer` emit: `id="fnref:{index}"`, `href="#fn:{order}"`, `id="fn:{order}"`).

**Why it happens:** Ganss.Xss deliberately excludes `id` by default (it's not documented as explicitly as the `class` classjacking rationale, but the pattern is consistent — arbitrary attacker-controlled `id` values are a known DOM-clobbering vector in browsers, where an element with `id="someGlobalVarName"` can shadow/override a same-named global JS variable or `document.getElementById` lookup relied on by the host page's own scripts).

**How to avoid:** This app's Markdig-generated `id` values are constrained to the literal pattern `fn:{sequential integer}` / `fnref:{sequential integer}` — never arbitrary user-supplied text (there is no Markdown syntax, absent `.UseGenericAttributes()`, that lets a user choose an arbitrary `id` value). Adding `id` to `AllowedAttributes` is safe in this specific, scoped context. Document this reasoning inline (a comment on the `AllowedAttributes` set) so a future contributor doesn't assume `id` is safe to allow for unrelated reasons.

**Warning signs:** A future phase adds a different extension that also produces `id` attributes from less-constrained input, without re-examining whether the "sequential-integer-only" assumption still holds.

### Pitfall 4: `EmphasisExtraOptions.Default` (the parameterless `.UseEmphasisExtras()` overload) silently enables 4 syntaxes beyond strikethrough

**What goes wrong:** Copy-pasting `.UseEmphasisExtras()` without the explicit `EmphasisExtraOptions.Strikethrough` argument enables the enum's `Default` value, which is a bitwise combination of `Strikethrough | Subscript | Superscript | Inserted | Marked` (confirmed via source) — silently turning on `~text~` (subscript), `^text^` (superscript), `++text++` (inserted/`<ins>`), and `==text==` (marked/`<mark>`), none of which D-01/D-03 asked for, and none of which have a corresponding entry in the sanitizer's `AllowedTags` allowlist above (so they'd render as tags Markdig produces and the sanitizer then deletes — including their text content, per Pitfall 1).

**Why it happens:** `.UseEmphasisExtras()` is one of the few Markdig extension methods where the "just call it" reflex (that works fine for `.UseAutoLinks()`, `.UsePipeTables()`, etc.) produces broader behavior than a single-feature name suggests.

**How to avoid:** Always call `.UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)` explicitly — never the parameterless overload — for this phase's scope.

**Warning signs:** A unit test for `~single tilde~` unexpectedly renders as `<sub>` instead of literal text with tildes.

## Code Examples

### Verified pipeline construction (all extension names/signatures confirmed against `xoofx/markdig` master branch source)

```csharp
// Source: https://github.com/xoofx/markdig/blob/master/src/Markdig/MarkdownExtensions.cs
using Markdig;
using Markdig.Extensions.EmphasisExtras;

private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .DisableHtml()
    .UseAutoLinks()
    .UsePipeTables()
    .UseTaskLists()
    .UseDefinitionLists()
    .UseFootnotes()
    .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
    .Build();
```

### Exact HTML shapes Markdig's renderers produce (verified by reading the actual renderer source — needed to write a precise sanitizer allowlist and precise unit test assertions)

```
// Source: https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/TaskLists/HtmlTaskListRenderer.cs
// "- [ ] unchecked" produces:
<input disabled="disabled" type="checkbox" />
// "- [x] checked" produces:
<input disabled="disabled" type="checkbox" checked="checked" />
// (D-05 CONFIRMED: static/disabled by default, not assumed — WriteAttributes(obj) is a no-op
//  here since no HtmlAttributes are ever attached without UseGenericAttributes(), which is excluded)

// Source: https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/Footnotes/HtmlFootnoteLinkRenderer.cs
// A footnote reference "text[^1]" produces:
<a id="fnref:1" href="#fn:1" class="footnote-ref"><sup>1</sup></a>
// Source: .../Extensions/Footnotes/HtmlFootnoteGroupRenderer.cs
// The trailing footnote definitions block produces:
<div class="footnotes">
<hr />
<ol>
<li id="fn:1">
  <!-- footnote body content -->
  <a href="#fnref:1" class="footnote-back-ref">&#8617;</a>
</li>
</ol>
</div>

// Source: .../Extensions/DefinitionLists/HtmlDefinitionListRenderer.cs
// "Term\n: Definition" produces:
<dl>
<dt>Term</dt>
<dd>Definition</dd>
</dl>

// Source: .../Extensions/Tables/HtmlTableRenderer.cs
// A pipe table produces standard table/thead/tbody/tr/th/td; column alignment (":---:") emits
// an inline style="text-align: center|left|right;" attribute directly on <td>/<th> (fixed literal
// values only — not user-controlled attribute injection, but the sanitizer will strip the `style`
// attribute entirely unless "style" is added to AllowedAttributes + AllowedCssProperties includes
// "text-align"; this is a cosmetic fidelity trade-off, not a security requirement — see Open Questions)
```

### Confirmed: `.UseAdvancedExtensions()`'s exact bundle (why individual composition is required, not just preferred)

```csharp
// Source: https://github.com/xoofx/markdig/blob/master/src/Markdig/MarkdownExtensions.cs
public static MarkdownPipelineBuilder UseAdvancedExtensions(this MarkdownPipelineBuilder pipeline)
{
    return pipeline
        .UseAlertBlocks().UseAbbreviations().UseAutoIdentifiers().UseCitations()
        .UseCustomContainers().UseDefinitionLists().UseEmphasisExtras().UseFigures()
        .UseFooters().UseFootnotes().UseGridTables().UseMathematics().UseMediaLinks()
        .UsePipeTables().UseListExtras().UseTaskLists().UseDiagrams().UseAutoLinks()
        .UseGenericAttributes(); // Must be last as it is one parser that is modifying other parsers
}
```

### Unit test payloads for the phase's 3 success criteria

```csharp
// SUCCESS CRITERION 1: XSS-blocking payloads
// Source for autolink-scheme-acceptance claim: https://github.com/xoofx/markdig/blob/master/
//   src/Markdig/Parsers/Inlines/AutolinkInlineParser.cs — LinkHelper.TryParseAutolink() runs
//   BEFORE the EnableHtmlParsing-gated fallback branch, so scheme is never validated there.

[Theory]
[InlineData("<script>alert(1)</script>")]                          // raw HTML block/inline attempt
[InlineData("Hello <img src=x onerror=alert(1)>")]                 // raw HTML inline attempt
[InlineData("[Click me](javascript:alert(document.cookie))")]       // javascript: in [text](url)
[InlineData("<javascript:alert(document.cookie)>")]                 // javascript: in native autolink
[InlineData("[Click me](http://example.com){onmouseover=\"alert(1)\"}")] // generic-attribute injection
public void RenderToHtml_XssPayload_ProducesNoLiveScriptOrHandler(string markdown)
{
    var html = Service.RenderToHtml(markdown);

    html.Should().NotContainEquivalentOf("<script");
    html.Should().NotContain("onerror=");
    html.Should().NotContain("onmouseover=");
    html.Should().NotContain("javascript:");
}

// SUCCESS CRITERION 3: strict CommonMark paragraph behavior
[Fact]
public void RenderToHtml_SingleNewlineNoBlankLine_StaysOneParagraph()
{
    var html = Service.RenderToHtml("Line one\nLine two");

    // A soft line break within one paragraph — NOT two <p> tags, NOT a <br>
    Regex.Matches(html, "<p>").Count.Should().Be(1);
    html.Should().NotContain("<br");
}

[Fact]
public void RenderToHtml_BlankLineBetweenLines_ProducesTwoParagraphs()
{
    var html = Service.RenderToHtml("Paragraph one\n\nParagraph two");

    Regex.Matches(html, "<p>").Count.Should().Be(2);
}
```

Note on **RENDER-02** (single shared pipeline, no duplicated logic): this is primarily an *architectural* constraint, not something one runtime unit test can fully capture — Phase 65 wires no real MVC/email callers yet (per the phase boundary), so the concrete "same output for both surfaces" proof happens in Phase 66 when the first real consumer (`.cshtml` view + `QuestFinalized.razor`) exists. For this phase, satisfy it via: (a) the type-level design (one `IMarkdownService.RenderToHtml(markdown, target)` entry point, not two services/two parse calls — Pattern 1 above), and (b) a code-review checklist item confirming no other file references `Markdig.Markdown.ToHtml(...)` directly.

## State of the Art

| Old Approach (milestone-level STACK.md's original recommendation) | Current Approach (this phase, per CONTEXT.md D-01) | When Changed | Impact |
|--------------|------------------|--------------|--------|
| "Keep minimal, cherry-pick only autolinks" — `.UseAutoLinks().DisableHtml()` and nothing else | Full GFM support: autolinks + pipe tables + task lists + definition lists + footnotes + strikethrough, each individually composed | Discuss-phase for Phase 65 (2026-07-09), a deliberate user correction to the milestone research | Sanitizer allowlist complexity increases substantially (6 new tag categories vs. the original ~13-tag baseline); toolbar UI (Phase 66+) remains unaffected since it stays curated separately per D-02 |
| FEATURES.md's original recommendation to skip task lists entirely ("interactive checkbox has no email equivalent — architectural mismatch") | Task lists enabled; D-05 confirms Markdig's default rendering is already static/disabled, so the "interactive checkbox" mismatch this concern was based on doesn't actually exist | Same discuss-phase, resolved by verifying (this research, and D-05 itself) that Markdig never emits an interactive checkbox by default | The original FEATURES.md concern is now known to be based on a false premise about Markdig's default output — worth noting for anyone reading FEATURES.md later without this correction |

**Deprecated/outdated:** The milestone-level STACK.md's minimal-extension-set code sample (`UseAutoLinks().DisableHtml()` only) is superseded by this phase's 6-extension composition — do not use the milestone STACK.md's pipeline code sample as-is; use the "Code Examples" section above instead.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Markdig's pipe-table alignment syntax (`:---:`) practically never sets `TableColumnDefinition.Width` to a non-default value for the plain pipe-table syntax this app would realistically receive (only affects whether a `<col style="width:...">` tag appears) — based on reading `HtmlTableRenderer.cs`'s `hasColumnWidth` check but not exhaustively tracing every code path that could set `Width` | Code Examples (pipe table renderer notes) | Low — if wrong, a `<col>` tag would appear in output; since `col` is not in the recommended `AllowedTags` list, it gets silently removed by the sanitizer (safe fallback, `col` has no children so no content-loss risk per Pitfall 1 — this is one of the few tags where the `KeepChildNodes=false` default is actually harmless) |
| A2 | Recommending `class`/`AllowedCssClasses` be left out of the baseline allowlist (footnotes/task-lists lose their CSS hook classes but remain functional) is an acceptable trade-off for this phase, deferred as Claude's Discretion per CONTEXT.md | Pattern 3 (Note on `class`) | Low — purely cosmetic; if the planner decides styling hooks matter now rather than later, adding `class` + scoped `AllowedCssClasses` is a small, well-understood addition documented above |
| A3 | Ganss.Xss's default `AllowedCssProperties` list includes `text-align` (used for pipe-table column alignment styling) — inferred from the property being an extremely standard CSS property in an otherwise very inclusive default list, but not individually grepped/confirmed character-for-character in this session | Code Examples (pipe table renderer notes) | Low — this only affects whether table-column alignment survives sanitization cosmetically; doesn't affect any of the 3 locked success criteria; easy to verify with one test during implementation |

**If this table is empty:** N/A — see entries above. All three are low-risk, cosmetic-only items; every claim touching the 3 locked success criteria (XSS blocking, single-shared-pipeline, strict-paragraph behavior) was verified directly against source code, not assumed.

## Open Questions

1. **Should the sanitizer allowlist preserve pipe-table column alignment (`style="text-align:..."`) and footnote/task-list CSS hook classes, or ship the minimal functional baseline first?**
   - What we know: Neither is required by RENDER-01/02/03's success criteria. Both are small, well-understood additions (documented in Pattern 3 and Assumption A2/A3) if wanted.
   - What's unclear: Whether Phase 66+'s toolbar/preview work has an implicit expectation that tables render with visible alignment, or whether "curated toolbar, full parser underneath" (D-02) means power users who hand-type a pipe table shouldn't expect polished styling anyway.
   - Recommendation: Ship the minimal baseline in this phase (functional, not styled) — matches the phase's "pure plumbing, nothing user-visible yet" boundary; revisit if Phase 66+ hits a concrete need.

2. **Exact `IMarkdownService` interface shape (single method + enum parameter vs. two named methods vs. two interfaces) — left to the planner per CONTEXT.md's Claude's Discretion.**
   - What we know: Pattern 1 above recommends single-method-plus-enum as the cleanest way to satisfy RENDER-02 at the type level.
   - What's unclear: Whether the planner prefers named methods (`RenderToHtml`/`RenderToEmailHtml`) for readability at call sites, which would still satisfy RENDER-02 in spirit (both internally call the same private parse step) even though it's two public methods.
   - Recommendation: Either is acceptable given RENDER-02's actual requirement is "no duplicated parsing logic," not "exactly one public method name" — planner's call.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Building/testing the new Domain service | ✓ | 10.0.301 (also 8.0.416, 10.0.100-rc.1 present) | — |
| NuGet registry (`api.nuget.org`) reachability | Installing Markdig/HtmlSanitizer packages | ✓ | — (confirmed reachable this session via direct API queries) | — |
| xUnit v3 / FluentAssertions / NSubstitute test stack | Unit testing the new service | ✓ | xunit.v3 3.2.2, FluentAssertions 8.10.0 (already in `QuestBoard.UnitTests.csproj`) | — |

No missing dependencies — this phase has no Docker/database/external-service requirements (pure Domain-layer + unit tests).

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 (`xunit.v3` + `xunit.runner.visualstudio`) |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (no separate `xunit.runner.json` needed for unit tests — that file only exists for `QuestBoard.IntegrationTests`, to force serial execution against a shared in-memory DB, which this phase's stateless service doesn't need) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests"` |
| Full suite command | `dotnet test QuestBoard.UnitTests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RENDER-01 | XSS payloads (script tags, `javascript:` in link/autolink syntax, generic-attribute injection) produce no live script/handler in output | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_XssPayload"` | ❌ Wave 0 |
| RENDER-02 | Single shared entry point; no duplicated Markdig/sanitizer wiring elsewhere in the codebase | code review + type-level design | N/A (architectural constraint — see Code Examples note) | ❌ Wave 0 (service doesn't exist yet) |
| RENDER-03 | Single Enter stays one paragraph; blank line starts a new paragraph | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_.*Paragraph"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests"`
- **Per wave merge:** `dotnet test QuestBoard.UnitTests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` — covers REQ RENDER-01, RENDER-03; new file, no existing test to extend
- [ ] `QuestBoard.Domain/Interfaces/IMarkdownService.cs` + `QuestBoard.Domain/Services/MarkdownService.cs` — the service under test doesn't exist yet; this phase's Wave 1/2 build order (per milestone ARCHITECTURE.md's suggested build order) should follow this project's established "failing tests, then implementation" pattern
- [ ] No new shared fixture needed — `MarkdownService` is stateless and constructible with a parameterless constructor (or possibly `internal` per the `ImageValidationService` precedent, testable directly via the existing `InternalsVisibleTo("QuestBoard.UnitTests")` in `QuestBoard.Domain/Properties/AssemblyInfo.cs`, already confirmed present)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | This phase has no auth surface — pure string-transform service |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A — access control for who can submit the 9 fields is unchanged, out of this phase's scope |
| V5 Input Validation, Sanitization, Encoding | **yes** | `Markdig` (`.DisableHtml()`, no `.UseGenericAttributes()`) for parsing + `Ganss.Xss.HtmlSanitizer` (allowlist-based, two profiles) for output sanitization — never hand-rolled regex filtering |
| V6 Cryptography | no | N/A |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS via raw `<script>`/event-handler HTML typed into a free-text field | Tampering / Elevation of Privilege | `.DisableHtml()` at the Markdig parser level (confirmed to block both block- and inline-level raw HTML recognition) — verified this is necessary AND sufficient for literal-tag injection, since it prevents the tag from ever being recognized as HTML in the first place |
| `javascript:`-scheme XSS via `[text](url)` link syntax | Tampering / Elevation of Privilege | `HtmlSanitizer.AllowedSchemes` restricted to `http`/`https`/`mailto`, applied via `UriAttributes` on `href` |
| `javascript:`-scheme XSS via native CommonMark angle-bracket autolink (`<javascript:...>`) — confirmed via source NOT blocked by `.DisableHtml()` | Tampering / Elevation of Privilege | Same `AllowedSchemes`/`UriAttributes` mechanism — this is the ONLY defense for this specific vector, so it must be tested explicitly, not assumed covered by the `[text](url)` test case |
| Attribute injection (`{onmouseover="..."}`) via Markdig's Generic Attributes syntax | Tampering / Elevation of Privilege | Never call `.UseGenericAttributes()` — confirmed via source this syntax is inert (renders as literal text) when the extension isn't in the pipeline |
| DOM clobbering via attacker-controlled `id` attribute values | Tampering | Not applicable in the general case (this app doesn't allow arbitrary `id` values) — the one narrow exception (footnote `id="fn:N"`) uses only Markdig-generated sequential integers, never user-supplied strings, bounding the risk |
| Silent content deletion masquerading as a sanitization "success" (not a classic security vulnerability, but a data-integrity risk introduced by this exact library's non-default-intuitive behavior) | Tampering (of legitimate user content, by the app itself) | `KeepChildNodes = false` default — mitigated by the exhaustive per-extension `AllowedTags` checklist in Code Examples, verified against actual renderer output rather than guessed |

## Sources

### Primary (HIGH confidence — read directly this session)
- https://github.com/xoofx/markdig/blob/master/src/Markdig/MarkdownExtensions.cs — confirmed exact signatures for `UsePipeTables`, `UseTaskLists`, `UseDefinitionLists`, `UseFootnotes`, `UseEmphasisExtras`, `UseAutoLinks`, `DisableHtml`, and the full `UseAdvancedExtensions()` bundle contents
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/TaskLists/HtmlTaskListRenderer.cs — confirmed exact task-list checkbox HTML output (`disabled="disabled" type="checkbox"`)
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/DefinitionLists/HtmlDefinitionListRenderer.cs — confirmed `dl`/`dt`/`dd` output shape
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/Footnotes/HtmlFootnoteLinkRenderer.cs and `HtmlFootnoteGroupRenderer.cs` — confirmed exact footnote `id`/`href`/`class` attribute shapes
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/Tables/HtmlTableRenderer.cs — confirmed pipe table tag structure and inline alignment-style behavior
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/EmphasisExtras/EmphasisExtraExtension.cs and `EmphasisExtraOptions.cs` — confirmed `Strikethrough`-only flag behavior and the tilde-delimiter-count logic
- https://github.com/xoofx/markdig/blob/master/src/Markdig/Parsers/Inlines/AutolinkInlineParser.cs — confirmed native CommonMark autolink parsing is NOT gated by `.DisableHtml()`'s `EnableHtmlParsing` flag
- https://github.com/xoofx/markdig/blob/master/src/Markdig.Tests/Specs/GenericAttributesSpecs.md — confirmed exact `{...}` generic-attribute syntax and that it requires `.UseGenericAttributes()` to have any effect
- https://github.com/mganss/HtmlSanitizer/blob/master/README.md — confirmed default `AllowedTags`/`AllowedAttributes`/`AllowedCssProperties`/`AllowedSchemes` lists, thread-safety contract, `class` exclusion rationale
- https://github.com/mganss/HtmlSanitizer/blob/master/src/HtmlSanitizer/HtmlSanitizer.cs — confirmed `KeepChildNodes`/`DefaultKeepChildNodes = false`, `RemoveTag()` behavior, `DoSanitize()` removal-order logic, `HtmlSanitizerOptions`-based constructor overload
- https://github.com/mganss/HtmlSanitizer/blob/master/src/HtmlSanitizer/HtmlSanitizerOptions.cs — confirmed the options object's exact property set (`AllowedTags`, `AllowedAttributes`, `AllowedCssClasses`, `AllowedCssProperties`, `AllowedAtRules`, `AllowedSchemes`, `UriAttributes`)
- https://api.nuget.org/v3-flatcontainer/markdig/index.json — re-confirmed `1.3.2` is still the newest published version as of 2026-07-09
- https://api.nuget.org/v3-flatcontainer/htmlsanitizer/index.json — re-confirmed `9.0.892` is still the newest stable version (9.1.x are betas) as of 2026-07-09
- Direct repository inspection this session: `QuestBoard.Domain/Interfaces/IImageValidationService.cs`, `QuestBoard.Domain/Services/ImageValidationService.cs`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/QuestBoard.Domain.csproj`, `QuestBoard.Domain/Properties/AssemblyInfo.cs` (confirmed `InternalsVisibleTo("QuestBoard.UnitTests")`), `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs`, `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.Service/Components/Emails/QuestFinalized.razor`

### Secondary (MEDIUM confidence)
- `.planning/research/STACK.md`, `.planning/research/ARCHITECTURE.md`, `.planning/research/PITFALLS.md`, `.planning/research/SUMMARY.md` — milestone-level research this phase builds on; carried forward without re-verification where this phase's scope didn't touch the claim (e.g., EasyMDE/client-side details, which are Phase 66's concern, not re-verified here)

### Tertiary (LOW confidence)
- None — every claim in this document that touches the 3 locked success criteria was source-verified this session.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions re-verified directly against NuGet v3 API today
- Architecture (two-sanitizer-profile pattern): HIGH — thread-safety contract and `HtmlSanitizerOptions` constructor confirmed directly from source
- Pitfalls (KeepChildNodes, autolink/DisableHtml interplay, footnote id gap): HIGH — all three confirmed by reading actual renderer/parser/sanitizer source code, not inferred from documentation prose alone
- Cosmetic details (table alignment `style`/`AllowedCssProperties` coverage, `<col>` width edge case): LOW-MEDIUM — flagged explicitly in Assumptions Log, does not affect any locked success criterion

**Research date:** 2026-07-09
**Valid until:** 2026-08-08 (30 days — stable, mature libraries with infrequent breaking changes; re-verify version numbers if planning is delayed past this window)
