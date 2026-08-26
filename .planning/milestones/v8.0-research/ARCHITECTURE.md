# Architecture Research: Markdown Integration

**Domain:** Markdown authoring + rendering integration into an existing 3-layer ASP.NET Core 10 MVC app (D&D Quest Board, v8.0 milestone)
**Researched:** 2026-07-09
**Confidence:** HIGH (integration points — verified directly against this codebase's source); MEDIUM (Markdig/HtmlSanitizer XSS specifics — verified via WebSearch against official GitHub/NuGet sources, no Context7 available in this environment)

## Summary / Recommendation

Put the actual Markdown→HTML conversion in **one new Domain-layer service, `IMarkdownService`**, exactly mirroring the `IImageValidationService` precedent already in this codebase (stateless, pure-transformation, zero infrastructure dependency, unit-testable in isolation). Wrap it with **two thin adapters**, one per rendering pipeline this project already runs side-by-side:

- **MVC pipeline (`.cshtml`):** a `Html.Markdown(text)` extension method (`IHtmlContent`), following the same "static extension class in `Extensions/`" convention as `ControllerExtensions` — not a TagHelper (this codebase has zero TagHelpers today; a HtmlHelper extension is the smaller, more idiomatic addition given existing conventions).
- **Blazor component pipeline (`.razor` emails via `HtmlRenderer`):** the 3 email components `@inject IMarkdownService` directly and wrap the parameter in `(MarkupString)`. This works with **zero Hangfire-specific scope plumbing** — `IMarkdownService` is stateless, so unlike `ActiveGroupContextService` it needs no `HangfireJobHelper.RunInScopeAsync` bridge; ordinary constructor/`@inject` DI resolves it from whatever scope `HtmlRenderer`'s `IServiceProvider` was built from (request scope or job scope, doesn't matter).

The **Preview toggle needs zero server round-trip.** This app already vendors all client JS via CDN `<script>` tags (Bootstrap, FontAwesome, jQuery, Cropper.js — no npm/libman/build step, matching the "no additional setup steps" deployment constraint). Add a CDN-loaded **`commonmark.js`** (the reference implementation of the same CommonMark spec Markdig implements server-side, chosen specifically to minimize preview/render drift given "Strict CommonMark paragraph rules" is a locked v8.0 decision) plus **DOMPurify** (client-side sanitizer, pairs with the server-side `HtmlSanitizer` pass — see Anti-Patterns) for the live preview pane. No new controller endpoint (`POST /markdown/preview`) is needed or recommended.

The **9 read-side call sites** share logic via the single `Html.Markdown()` helper (one call replaces the current `<p style="white-space:pre-wrap">@Model.X</p>` pattern at each site). The **write-side editor UI** (textarea + Bold/Italic/Heading/List toolbar + Preview toggle) shares logic via one new `Views/Shared/_MarkdownEditor.cshtml` partial + one `wwwroot/js/markdown-editor.js` module, generalizing the existing `_QuestFormScripts.cshtml` "shared partial for form JS" pattern from single-feature to cross-feature scope.

## System Overview — Where Markdown Rendering Sits

```
┌────────────────────────────────────────────────────────────────────────┐
│  WRITE PATH (unchanged data model — no schema/migration needed)        │
│                                                                          │
│  Create/Edit .cshtml (+.Mobile.cshtml)                                 │
│    └─ <partial name="_MarkdownEditor" model="...">  ◄── NEW partial    │
│         textarea (raw Markdown) + toolbar + Preview pane               │
│         markdown-editor.js: commonmark.js + DOMPurify (CDN, client)    │
│    └─ POST → Controller → Domain Service → Repository                 │
│         (raw Markdown string persisted verbatim — SAME 9 columns)      │
└────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────┐
│  READ PATH — two independent rendering pipelines, ONE shared core      │
│                                                                          │
│   MVC Request Pipeline              Hangfire Background-Job Pipeline   │
│   (.cshtml, request-scoped DI)      (.razor via HtmlRenderer, scoped-  │
│                                       factory pattern)                 │
│         │                                    │                        │
│  @Html.Markdown(Model.X)          @inject IMarkdownService              │
│  (Service/Extensions/                @((MarkupString)                 │
│   HtmlHelperExtensions.cs — NEW)      MarkdownService                  │
│         │                             .RenderToHtml(X)))               │
│         │                            (QuestFinalized/SessionReminder/  │
│         │                             WaitlistPromoted .razor — MOD)   │
│         └────────────────┬───────────────────┘                        │
│                           ▼                                           │
│         QuestBoard.Domain/Services/MarkdownService.cs  ◄── NEW        │
│         QuestBoard.Domain/Interfaces/IMarkdownService.cs               │
│         Markdig (DisableHtml, base CommonMark) → HtmlSanitizer pass    │
│         registered AddSingleton in ServiceExtensions.AddDomainServices │
│         (stateless — no IServiceScopeFactory bridge needed, unlike     │
│          ActiveGroupContextService)                                   │
└────────────────────────────────────────────────────────────────────────┘
```

This slots cleanly into the existing one-way layer rule: `MarkdownService` lives in `QuestBoard.Domain`, has **no dependency on `QuestBoard.Repository`** (it takes a `string?` in, returns a `string` out — no entities, no `DbContext`, no repository interface). Both adapters (`HtmlHelperExtensions` in Service, and the `.razor` components in Service) depend *down* into Domain, exactly like every other Service→Domain call in this app. Nothing here requires touching `QuestBoard.Repository` at all — confirming the milestone's framing that this is "a rendering-layer and editing-UX change, not a data-model change."

## Component Responsibilities

| Component | Layer | New/Modified | Responsibility |
|-----------|-------|---------------|-----------------|
| `IMarkdownService` | `QuestBoard.Domain/Interfaces/` | New | Contract: `string RenderToHtml(string? markdown)` |
| `MarkdownService` | `QuestBoard.Domain/Services/` | New | Markdig parse (`.DisableHtml()`, base CommonMark only) + `HtmlSanitizer` allowlist pass; the single source of truth both pipelines call into |
| `Markdig` NuGet ref | `QuestBoard.Domain.csproj` | New | Parser — pure managed library, no ASP.NET/EF dependency, safe to add to Domain (same reasoning `AutoMapper` is already there) |
| `HtmlSanitizer` (Ganss.Xss) NuGet ref | `QuestBoard.Domain.csproj` | New | Post-parse allowlist sanitizer (tags + `AllowedSchemes`) — closes a gap Markdig's `DisableHtml()` alone does not close (see Anti-Patterns) |
| `ServiceExtensions.AddDomainServices` | `QuestBoard.Domain/Extensions/` | Modified | Register `IMarkdownService` — as **Singleton**, a deliberate one-line deviation from this file's otherwise-uniform `AddScoped` convention, justified because the service is stateless and the Markdig pipeline object is expensive-ish to build once and is documented thread-safe for reuse |
| `HtmlHelperExtensions.Markdown()` | `QuestBoard.Service/Extensions/` | New | Read-side adapter: `IHtmlContent Markdown(this IHtmlHelper html, string? markdown)`, resolves `IMarkdownService` via `html.ViewContext.HttpContext.RequestServices` (standard ASP.NET Core idiom — no `_ViewImports` `@inject` needed per-view), wraps output in `<div class="markdown-content">...</div>` |
| `Views/_ViewImports.cshtml` | `QuestBoard.Service/Views/` | Modified | Add `@using QuestBoard.Service.Extensions;` so all 9 read call sites can call `@Html.Markdown(...)` without per-view usings (mirrors how `ControllerExtensions` is already `using`-scoped per controller) |
| `Views/Shared/_MarkdownEditor.cshtml` | `QuestBoard.Service/Views/Shared/` | New | Write-side reusable partial: textarea + toolbar buttons (Bold/Italic/Heading/List) + Preview toggle markup, parameterized by field id/name/rows |
| `wwwroot/js/markdown-editor.js` | `QuestBoard.Service/wwwroot/js/` | New | Toolbar button logic (vanilla JS textarea-selection wrapping, no library) + Preview toggle (client-side `commonmark.js` render + `DOMPurify` sanitize) |
| `commonmark.js` (CDN) | `_Layout.cshtml` / `_Layout.Mobile.cshtml` | New | Client-side CommonMark-spec parser for the Preview pane, matched to server-side Markdig's base ruleset |
| `DOMPurify` (CDN) | `_Layout.cshtml` / `_Layout.Mobile.cshtml` | New | Client-side HTML sanitizer for the Preview pane, pairs with server-side `HtmlSanitizer` |
| `.markdown-content` CSS class | `wwwroot/css/site.css` | New | One shared typography ruleset (headings/lists/bold/italic/links/code) for all 9 rendered-Markdown read views — desktop and mobile both load `site.css`, so one rule covers both |
| 9 read call sites (Quest card partial, Quest Details ×2 fields, Quest Manage, Quest Log Details ×2 fields, Character Details ×2 fields, Contact Details + Notes loop, DM Profile, Shop Index/Details) | `QuestBoard.Service/Views/**` | Modified | Replace `<p style="white-space:pre-wrap">@Model.X</p>` with `@Html.Markdown(Model.X)` |
| 9 fields' Create/Edit (+`EditRecap`) forms, desktop + mobile | `QuestBoard.Service/Views/**` | Modified | Replace plain `<textarea asp-for="X">` with `<partial name="_MarkdownEditor" model="...">` |
| `QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor` | `QuestBoard.Service/Components/Emails/` | Modified | Add `@inject IMarkdownService MarkdownService`; change `@QuestDescription` → `@((MarkupString)MarkdownService.RenderToHtml(QuestDescription))` |

## Why a Domain Service (not just a view helper)

This project has an explicit, already-validated requirement: *"Business logic lives in services, not controllers"* (v1.x, Phase 02) — and the more recent `IImageValidationService` (v7.0, Phase 45) is the exact template to follow for "small, pure, cross-cutting transformation logic that several controllers/views need identically." Putting the actual Markdig/HtmlSanitizer logic only in a view helper would:

1. Make it untestable without spinning up MVC's `IHtmlHelper` machinery (Domain-layer unit tests are cheap and already the norm — see `QuestBoard.UnitTests/`).
2. Be unreachable from the `.razor` email pipeline, which has no `IHtmlHelper` at all — `HtmlRenderer` renders Blazor components via `IServiceProvider`, not MVC's view engine. A view-only helper would force the email adapter to duplicate the parsing/sanitizing logic, which is the "logic duplicated 9(+3) times" outcome the question explicitly wants avoided.

So: **Domain owns the algorithm, Service-layer owns two thin per-pipeline adapters.** This is the "both" option the question raises, and it is the right call — not because "both" is safe/wishy-washy, but because the two adapters genuinely cannot be unified: MVC's `IHtmlContent`/`HtmlString` and Blazor's `MarkupString` are different rendering-pipeline primitives, which is inherent to this project already choosing to run two parallel Razor rendering engines (documented in this codebase's own `ARCHITECTURE.md`: *"IRazorViewEngine throws NullReferenceException in background job context, which is why HtmlRenderer was adopted"*). The adapters are unavoidable plumbing; the algorithm is not duplicated.

## Why HtmlHelper, not a TagHelper

Both are legitimate ASP.NET Core choices. This codebase currently has **zero TagHelpers** — `Views/_ViewImports.cshtml` only registers the built-in `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`. It does, however, have an established, repeatedly-used convention: a `internal static class ...Extensions` in `QuestBoard.Service/Extensions/` (see `ControllerExtensions.cs`, cited directly in the milestone context as the "extract shared logic into helpers when duplicated 3+ times" precedent). A `HtmlHelperExtensions.Markdown()` method is the same shape of thing, in the same folder, following the same naming convention — it is the smaller, more consistent addition. Introducing a TagHelper for the first time in this milestone would mean inventing a second reusable-view-logic convention where one already exists and fits.

Concretely:
```csharp
// QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
internal static class HtmlHelperExtensions
{
    internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
    {
        var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
        var rendered = service.RenderToHtml(markdown);
        return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
    }
}
```
Call site (all 9, desktop and mobile identically, since both share `Views/_ViewImports.cshtml`):
```html
@Html.Markdown(Model.Quest?.Description)
```
This one line replaces both the old inline `<p style="white-space:pre-wrap">@Model.X</p>` markup **and** the per-page CSS reliance on `white-space: pre-wrap` (which becomes obsolete for these fields once Markdown owns paragraph semantics — this is literally the "Strict CommonMark paragraph rules... replacing today's line-break-preserving plain-text display" requirement).

## Preview Toggle — Zero Round-Trip, Not a New Endpoint

**Recommendation: no `POST /markdown/preview` controller.** Reasons, in order of weight:

1. **Deployment constraint fit.** This app already vendors 100% of its client JS via CDN `<script>` tags in `_Layout.cshtml`/`_Layout.Mobile.cshtml` (Bootstrap, FontAwesome, jQuery, Cropper.js v2.1.1) with zero npm/libman/build tooling — matching the project's own "must remain deployable via `dotnet run` on LXC host; no additional setup steps" constraint. A CDN-loaded client Markdown parser is the same pattern, not a new one.
2. **Fidelity, not just speed.** A round-trip endpoint's only real advantage over client rendering is guaranteeing the preview is byte-identical to the real render. That guarantee is achievable client-side instead by choosing a **spec-matched** parser: `commonmark.js` is the reference implementation of the same CommonMark spec Markdig implements, and "Strict CommonMark paragraph rules" is already a locked v8.0 decision — so keeping the server pipeline to base CommonMark (no extra Markdig extensions beyond what's needed) and pointing the client at the spec reference implementation gets near-identical output without a network hop.
3. **No new attack surface / no antiforgery plumbing for a preview-only action.** A `POST /markdown/preview` endpoint accepting arbitrary raw text from any authenticated user is a new unauthenticated-content-reflection surface that then needs its own rate limiting, its own antiforgery token wiring in 9+ forms, and its own test coverage — for a feature (live preview) whose entire value proposition is "just show me what this looks like," where a slight parser-edge-case mismatch is a cosmetic risk, not a functional one.

**One real, acknowledged gap:** the recommended client stack (`commonmark.js` + DOMPurify) cannot be made to exactly replicate the server's second-stage `HtmlSanitizer` allowlist pass (see Anti-Patterns below) without also shipping a client sanitizer configuration kept in lockstep with the server one. This is an accepted, narrow asymmetry — the client preview is only ever shown to the authoring user before they submit; the server-side sanitizer is what actually protects *other* users and the emailed HTML once the content is saved and re-rendered. Recommend pairing `commonmark.js` with **DOMPurify** (industry-standard, dependency-free, CDN-friendly) in the preview pane specifically so this gap is small (raw-tag injection is still stripped client-side too), not absent.

Toolbar buttons (Bold/Italic/Heading/List) need no library at all — pure vanilla JS wrapping of the current `textarea` selection with `**`/`_`/`## `/`- ` markers, consistent with this app's already-vanilla-JS front end (no SPA framework, no bundler anywhere in the codebase).

## Sharing Logic Across the 9 Write-Side Call Sites

This project already has the right precedent for this: `Views/Quest/_QuestFormScripts.cshtml`, a partial view holding JS shared across `Quest/Create(.Mobile)` and `Quest/Edit(.Mobile)`. The Markdown editor needs the same idea, generalized from one feature to five. New component:

- `Views/Shared/_MarkdownEditor.cshtml` — a partial view accepting the bound field's `id`/`name` (or a small parameter object), rendering the textarea + toolbar + preview-pane markup identically everywhere it's included.
- `wwwroot/js/markdown-editor.js` — one shared script (loaded once via the layout, same as `site.js` — note this project already consolidated "4 duplicated toast-init scripts into one `site.js` listener" in Phase 42, the exact same kind of dedup this is), exposing an `initMarkdownEditor(...)` entry point the partial's inline script calls per-instance.

Each of the 9 fields' Create/Edit views (18 files counting desktop+mobile pairs, plus `QuestLog/EditRecap(.Mobile).cshtml` for Recap specifically) becomes a one-line `<partial name="_MarkdownEditor" model="...">` swap-in for what is today a plain `<textarea asp-for="Description" class="form-control" rows="6"></textarea>`. Because the partial is pure markup + client JS with no server dependency, mobile and desktop forms include the exact same partial and get identical behavior for free — there is nothing mobile-specific to re-implement, which matters given this project's own repeated lesson (flagged multiple times in `PROJECT.md`, e.g. Phase 43/54) that mobile parity gets missed when a feature is built desktop-only first.

## Anti-Patterns to Avoid

### Relying on `Markdig.DisableHtml()` alone as "the" sanitizer

**What people do:** Configure the Markdig pipeline with `.DisableHtml()` and consider the output safe to render as raw HTML, reasoning "no raw HTML in, no raw HTML out."

**Why it's wrong:** `DisableHtml()` stops literal `<script>`/`<iframe>`/etc. tags typed *inside* the Markdown source from being echoed as live HTML (it encodes them as text instead) — but it does **not** restrict the URI scheme of a normal Markdown link. `[Click here](javascript:alert(document.cookie))` is completely valid CommonMark and Markdig will happily emit a live `<a href="javascript:...">`. This is a well-documented, real-world Markdown XSS vector (verified via Rick Strahl's "Markdown and Cross Site Scripting" writeup and Markdig's own usage docs — MEDIUM confidence, WebSearch-verified against primary sources, no Context7 available in this environment). Given these 9 fields are writable by ordinary group members (Contact Notes = any member, Character fields = the owning player) and the rendered output is injected as raw HTML into both pages *and* the 3 HTML email templates, this is not a theoretical risk.

**Do this instead:** Pipe Markdig's HTML output through a second pass with **`HtmlSanitizer`** (Ganss.Xss NuGet package — pure managed, MIT-licensed, thread-safe `Sanitize()`), configured with:
- An allowed-tag list matching exactly what Markdig's base CommonMark renderer can ever produce (`p, strong, em, h1–h6, ul, ol, li, a, code, pre, blockquote, br, hr, img` if images are ever allowed),
- `AllowedSchemes` restricted to `http`, `https`, `mailto` — this is the specific configuration that closes the `javascript:` gap `DisableHtml()` leaves open.

Both stages (`DisableHtml()` + `HtmlSanitizer`) belong inside `MarkdownService.RenderToHtml`, not scattered across call sites — this is precisely why the algorithm belongs in one Domain service rather than being reimplemented per adapter.

### Persisting rendered HTML instead of rendering on read

**What people do:** Since Markdown rendering has a cost, cache/store the rendered HTML alongside the raw Markdown (e.g., a new `DescriptionHtml` column) to avoid re-rendering on every page view.

**Why it's wrong:** This milestone is explicitly scoped as "no schema change needed — this is a rendering-layer and editing-UX change, not a data-model change." Storing rendered HTML also creates a cache-invalidation problem (stale HTML if the sanitizer/renderer config ever changes) and doubles the surface that could drift from the source of truth. For description-length text, Markdig rendering is sub-millisecond regardless of request volume — there is no performance case for pre-rendering.

**Do this instead:** Render on every read, in the adapter, from the always-current raw Markdown string. If profiling ever shows this matters (it won't at this scale), memoize per-request only, never persist.

### Reflecting `IMarkdownService` through `IServiceScopeFactory`/`HangfireJobHelper` "for consistency" with other Hangfire-job services

**What people do:** Because every existing Hangfire job resolves its scoped services via `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, ...)`, it's tempting to route `IMarkdownService` through the same ceremony "to match the pattern."

**Why it's wrong:** That ceremony exists specifically to bridge `HttpContext.Session`-dependent state (`ActiveGroupContextService.SetGroupId`) into a background thread that has no `HttpContext`. `IMarkdownService` has no such state — it is a pure function of its string input. Forcing it through the scope-factory dance adds indirection with no benefit, and — more importantly — the `.razor` email components render via `HtmlRenderer`'s own `IServiceProvider` (already the scoped provider from `RazorEmailRenderService`'s constructor), so `@inject IMarkdownService` on the component resolves it directly, no bridge needed at all.

**Do this instead:** Register `IMarkdownService` as `AddSingleton` in `QuestBoard.Domain/Extensions/ServiceExtensions.cs` and let both pipelines resolve it through ordinary DI — MVC via `RequestServices` (inside the `Html.Markdown()` extension), Blazor via `@inject` on the 3 email components.

## Integration Points

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `QuestBoard.Service` (Html.Markdown extension) → `QuestBoard.Domain` (IMarkdownService) | Direct DI resolution via `RequestServices` | Matches existing Service→Domain dependency direction; no new boundary type |
| `QuestBoard.Service` (email `.razor` components) → `QuestBoard.Domain` (IMarkdownService) | `@inject`, resolved from `HtmlRenderer`'s scoped `IServiceProvider` | Same Domain service, second adapter — proves the "one core, two adapters" design under the project's own two-Razor-pipeline constraint |
| `QuestBoard.Domain` (MarkdownService) → `QuestBoard.Repository` | **None** | Correctly respects the one-way layer rule — this service touches no entities, no `DbContext`; it is a pure string-transform, so there is nothing for it to depend on downward |
| Write-side `_MarkdownEditor.cshtml` partial → existing Create/Edit controller actions | Unchanged — the partial only replaces the `<textarea>` markup; the posted field name/model binding is untouched, so no controller/ViewModel changes are needed beyond the view swap | Confirms "no data-model change" holds for the write path too, not just the read path |

### External Services (CDN-vendored, matching existing convention)

| Library | Integration Pattern | Notes |
|---------|---------------------|-------|
| `commonmark.js` | `<script src="https://cdn.jsdelivr.net/...">` in `_Layout.cshtml`/`_Layout.Mobile.cshtml`, same tier as Bootstrap/FontAwesome/Cropper.js | Client-side Preview parser; chosen for spec parity with server-side Markdig, not for feature richness |
| DOMPurify | Same CDN pattern | Client-side sanitizer for the Preview pane only — does not replace the server-side `HtmlSanitizer` pass, which remains authoritative |
| `Markdig` (NuGet) | `QuestBoard.Domain.csproj` `PackageReference` | Server-side parser |
| `HtmlSanitizer` (Ganss.Xss, NuGet) | `QuestBoard.Domain.csproj` `PackageReference` | Server-side sanitizer, second stage after Markdig |

## Suggested Build Order

1. **Foundation (no field wired yet).** `IMarkdownService`/`MarkdownService` in Domain (+ Markdig/HtmlSanitizer NuGet refs, unit tests covering the `javascript:`-scheme and raw-`<script>` cases specifically), `AddSingleton` registration, `HtmlHelperExtensions.Markdown()` in Service, the `.markdown-content` CSS class, and the `_ViewImports.cshtml` using-line. Nothing user-visible changes yet — this phase is pure plumbing and is fully unit-testable in isolation (Domain layer), matching this project's TDD-friendly phase pattern (see Phase 61's "Wave 1: failing tests, Wave 2: implementation" shape).

2. **One feature end-to-end, proof-of-concept: Quest Description.** Wire `_MarkdownEditor.cshtml` + `markdown-editor.js` (toolbar + CDN preview) into `Quest/Create(.Mobile)` and `Quest/Edit(.Mobile)`; swap the Quest board card partial, `Quest/Details`, and `Quest/Manage` read call sites to `Html.Markdown()`; update `QuestFinalized.razor` to the `@inject`/`MarkupString` pattern. This single field is deliberately chosen as the proof-of-concept because it is the **one field that already flows into an email template today** — proving both adapters (MVC helper and Blazor component) and the full write→read→email loop in one phase directly retires the milestone's flagged cross-cutting risk ("emails need the same renderer as pages") early, rather than deferring it to a separate, later "email" phase where a design gap would be more expensive to unwind.

3. **Remaining Quest fields (Rewards, Recap) + remaining 2 email templates.** `Quest/Details` (Rewards) and `QuestLog/Details` + `QuestLog/EditRecap(.Mobile)` (Recap) reuse the now-proven partial/helper with zero new plumbing. `SessionReminder.razor` and `WaitlistPromoted.razor` get the identical one-line adapter change already validated in step 2 — low-risk, mechanical.

4. **Remaining 6 fields across 4 features.** Character (Description, Backstory), Contact (Description, Notes — note the Notes list needs `Html.Markdown()` called per-item inside the existing `@foreach`, not a special case), DM Profile (Bio), Shop Item (Description). Each is a mechanical repeat of the now-proven write/read pattern; a roadmapper can size this as one wide phase or split by feature (e.g., Characters+Contacts, then DM Profile+Shop) without any sequencing risk between them — none of these 6 fields depend on each other or on anything not already proven in steps 1–3.

## Sources

- This codebase, verified directly (HIGH confidence): `QuestBoard.Domain/Interfaces/IImageValidationService.cs`, `QuestBoard.Domain/Services/ImageValidationService.cs`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Service/Extensions/ControllerExtensions.cs`, `QuestBoard.Service/Services/RazorEmailRenderService.cs`, `QuestBoard.Domain/Interfaces/IEmailRenderService.cs`, `QuestBoard.Service/Jobs/HangfireJobHelper.cs`, `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs`, `QuestBoard.Service/Components/Emails/{QuestFinalized,SessionReminder,WaitlistPromoted}.razor`, `QuestBoard.Service/Views/_ViewImports.cshtml`, `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml`, `QuestBoard.Service/Views/Quest/{_QuestCard,Details,Manage}.cshtml`, `QuestBoard.Service/Views/Shared/_Layout.cshtml` (CDN vendoring pattern), `.planning/codebase/ARCHITECTURE.md`, `.planning/PROJECT.md`
- [Markdown and Cross Site Scripting — Rick Strahl](https://weblog.west-wind.com/posts/2018/Aug/31/Markdown-and-Cross-Site-Scripting) — MEDIUM confidence, corroborates the `javascript:`-scheme gap in Markdown renderers generally, including Markdig-based ones
- [Markdig usage docs — DisableHtml](https://xoofx.github.io/markdig/docs/usage/) — MEDIUM confidence, official project docs, confirms `DisableHtml()` behavior and scope
- [HtmlSanitizer (Ganss.Xss) — GitHub](https://github.com/mganss/HtmlSanitizer) and [NuGet Gallery](https://www.nuget.org/packages/htmlsanitizer) — MEDIUM-HIGH confidence, official repo/package docs, confirms `AllowedSchemes`/`UriAttributes` configuration and thread-safety of `Sanitize()`

---
*Architecture research for: Markdown editing/rendering integration, D&D Quest Board v8.0*
*Researched: 2026-07-09*
