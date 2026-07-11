# Phase 71: Email-Safety Hardening - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 7 (2 new/modified in Domain, 3 modified Razor templates, 1 modified test file, 1 optional new preview action)
**Analogs found:** 7 / 7

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `QuestBoard.Domain/Services/MarkdownService.cs` (add `RenderEmailHtml`) | service | transform | Same file's own `ExtractPlainText()` method | exact (same file, same AngleSharp-DOM-walk pattern) |
| `QuestBoard.Domain/Interfaces/IMarkdownService.cs` (add method signature) | service (interface) | transform | Same file's existing `RenderToHtml`/`ExtractPlainText` signatures | exact |
| `QuestBoard.Service/Components/Emails/QuestFinalized.razor` (call-site + card markup) | component | request-response (render) | `SessionReminder.razor` / `WaitlistPromoted.razor` (near-identical siblings) | exact |
| `QuestBoard.Service/Components/Emails/SessionReminder.razor` (call-site + card markup) | component | request-response (render) | `QuestFinalized.razor` | exact |
| `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` (call-site + card markup) | component | request-response (render) | `QuestFinalized.razor` | exact |
| `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` (new test cases for `RenderEmailHtml`) | test | transform | Same file's existing `RenderToHtml_*` test methods | exact |
| `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (optional new `WaitlistPromoted()` action) | controller | request-response | Same file's existing `SessionReminder()` action | exact |

## Pattern Assignments

### `QuestBoard.Domain/Services/MarkdownService.cs` — new `RenderEmailHtml` method (service, transform)

**Analog:** same file, `ExtractPlainText()` (lines 111-146), plus the existing `EmailSanitizer` config (lines 77-83) and `RenderToHtml` (lines 85-109).

**Imports pattern** (lines 1-5, already present, no new usings needed beyond `AngleSharp.Html.Parser` which `ExtractPlainText` already references inline):
```csharp
using Ganss.Xss;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using QuestBoard.Domain.Interfaces;
using System.Text.RegularExpressions;
```
`ExtractPlainText` uses the fully-qualified `AngleSharp.Html.Parser.HtmlParser` inline rather than a `using` — follow that same inline-qualification convention for consistency, or add `using AngleSharp.Html.Parser;` and `using AngleSharp.Dom;` if the new method needs `IElement`/`IDocument` types directly.

**DOM-walk skeleton to copy** (`ExtractPlainText`, lines 112-146):
```csharp
public string ExtractPlainText(string? markdown)
{
    var html = RenderToHtml(markdown, MarkdownRenderTarget.Web);
    if (string.IsNullOrEmpty(html))
    {
        return string.Empty;
    }

    var parser = new AngleSharp.Html.Parser.HtmlParser();
    using var document = parser.ParseDocument(html);

    var parts = document.Body!.Children
        .Select(el => el.TextContent.Trim())
        .Where(t => !string.IsNullOrEmpty(t))
        .ToList();

    if (parts.Count == 0)
    {
        return WhitespaceRun.Replace(document.Body.TextContent, " ").Trim();
    }

    var joined = string.Join(" ", parts);
    return WhitespaceRun.Replace(joined, " ").Trim();
}
```
`RenderEmailHtml` should follow the same shape: call `RenderToHtml(markdown, MarkdownRenderTarget.Email)` first (reuse the existing sanitize step, do NOT re-implement sanitization), guard on empty, then `new AngleSharp.Html.Parser.HtmlParser().ParseDocument(sanitizedHtml)`, then walk `document.Body!.Children` — same iteration pattern, different terminal action (style-injection + truncation instead of text-join).

**Empty/null guard convention to copy** (lines 88-91 in `RenderToHtml`, lines 115-118 in `ExtractPlainText` — identical guard repeated at both existing call sites):
```csharp
if (string.IsNullOrWhiteSpace(markdown))
{
    return string.Empty;
}
```

**Sanitizer reuse — do not touch `EmailSanitizer`'s config** (lines 77-83):
```csharp
private static readonly HtmlSanitizer EmailSanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
{
    AllowedTags = new HashSet<string>(BaseAllowedTags, StringComparer.OrdinalIgnoreCase),
    AllowedAttributes = AllowedAttributes,
    AllowedSchemes = AllowedSchemes,
    UriAttributes = UriAttributes,
});
```
Per RESEARCH.md's Pitfall 1, `style` must NOT be added to `AllowedAttributes` here — the new method injects `style` via `IElement.SetAttribute()` on the already-sanitized DOM, downstream of this sanitizer entirely.

**Comment style to copy** (this file's established convention — long, "why not what" comments above non-obvious decisions, e.g. lines 11-12, 15-19, 30-33, 66-68, 100-103, 122-126, 132-134, 142-144): every new block of logic in `RenderEmailHtml` (style-table lookup, MSO comment injection, truncation budget) should carry a comment in this same voice — plain-language rationale, no phase/requirement IDs per CLAUDE.md.

---

### `QuestBoard.Domain/Interfaces/IMarkdownService.cs` — new method signature (service interface)

**Analog:** same file's existing `RenderToHtml`/`ExtractPlainText` XML-doc + signature style (lines 12-19, 21-26).

**Pattern to copy:**
```csharp
/// <summary>
/// Converts Markdown text into sanitized HTML safe to render directly in a browser or email
/// client. A null, empty, or whitespace-only input returns <see cref="string.Empty"/>. Input
/// that trips Markdig's own nesting-depth guard (e.g. hundreds of nested blockquote or
/// emphasis markers) is never thrown into the caller -- it is returned HTML-encoded instead,
/// so callers never need to catch an exception from this method.
/// </summary>
string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web);
```
New method (per RESEARCH.md's proposed signature, `## Code Examples`):
```csharp
string RenderEmailHtml(string? markdown, string readMoreUrl, int maxTopLevelBlocks = 5, int maxPlainTextChars = 650);
```
Match the existing doc-comment density/tone — explain null/empty behavior, truncation behavior, and that styles are hard-coded (never derived from `markdown`) since that's a security-relevant invariant worth documenting at the interface level.

---

### `QuestBoard.Service/Components/Emails/{QuestFinalized,SessionReminder,WaitlistPromoted}.razor` (component, request-response render)

**Analog:** each of the 3 templates is a near-identical analog for the other two — use `QuestFinalized.razor` as the canonical reference since it was read in full.

**Current call site to replace** (`QuestFinalized.razor:43`; `SessionReminder.razor:44`; `WaitlistPromoted.razor:43` — identical across all 3):
```razor
<div style="height:100%;overflow-y:auto;padding-right:6px;">
    <div style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>
</div>
```

**New call site** (per RESEARCH.md `## Code Examples`):
```razor
<div style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@((MarkupString)MarkdownService.RenderEmailHtml(QuestDescription, QuestUrl))</div>
```
The outer `overflow-y:auto` wrapper `<div>` is removed entirely (per D-01/71-UI-SPEC item 1) — the `<td style="height:840px;overflow:hidden;">` (line 8 in all 3) stays untouched, only its inner scroll-wrapper div goes away. `QuestUrl` is already an `[Parameter, EditorRequired]` on all 3 components (`QuestFinalized.razor:102`; `SessionReminder.razor:103`; `WaitlistPromoted.razor:102`) — reuse it verbatim, no new parameter plumbing needed.

**Existing "View Quest Details" CTA button to reuse for read-more messaging** (`QuestFinalized.razor:82`; identical in `SessionReminder.razor:83`, `WaitlistPromoted.razor:82`):
```razor
<a href="@QuestUrl" style="background-color:#FFD700;color:#1a0f08;font-family:'Cinzel',serif;font-size:15px;font-weight:bold;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;border:2px solid #8B4513;text-align:center;">View Quest Details</a>
```
D-02 says point the truncation's "read more" copy at this existing button rather than adding a new one — the "read more" link injected by `RenderEmailHtml`'s truncation logic (an inline `<a href="@QuestUrl">`) is a *separate*, smaller inline link appended at the cut point; it does not replace or restyle Row 6's button.

**Injection/scan note:** these 3 templates and the sample data in `EmailPreviewController.cs` contain literal `javascript:` and `&token=` strings used only as XSS-test fixtures / preview sample URLs — not live secrets or executable payloads. No action needed.

---

### `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` (test, transform)

**Analog:** same file's existing `RenderToHtml_*` test methods (lines 11-80 read; file continues beyond).

**Structure to copy** (constructor/setup, line 9):
```csharp
private static readonly IMarkdownService Service = new MarkdownService();
```

**Assertion style to copy** (FluentAssertions, e.g. lines 19-31, 39-40, 59, 67-68):
```csharp
[Fact]
public void RenderToHtml_SingleNewlineNoBlankLine_StaysOneParagraph()
{
    var html = Service.RenderToHtml("Line one\nLine two");

    Regex.Matches(html, "<p>").Count.Should().Be(1);
    html.Should().NotContain("<br");
}
```
New `RenderEmailHtml_*` tests (per RESEARCH.md's Validation Architecture table) should follow this exact `[Fact]`/`[Theory]` + `Should()` convention: styling-injection assertions (`html.Should().Contain("text-shadow:2px 2px 4px")` etc. for each tag), MSO-comment-injection assertions, truncation-boundary assertions (well-formed HTML, no mid-`<li>` cuts), and read-more-link assertions (`html.Should().Contain(readMoreUrl)` plus the exact D-04 copy). Also add a `RenderEmailHtml_*_StripsImage` regression test mirroring the existing image-stripping coverage for `RenderToHtml`.

---

### `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` — optional new `WaitlistPromoted()` action (controller, request-response)

**Analog:** same file's existing `SessionReminder()` action (lines 127-143), and `QuestFinalized()` (lines 40-56) for the `nameof(...)`-keyed dictionary convention.

**Pattern to copy:**
```csharp
[HttpGet]
public async Task<IActionResult> SessionReminder()
{
    var appUrl = emailOptions.Value.AppUrl;
    var html = await emailRenderService.RenderAsync<Components.Emails.SessionReminder>(new()
    {
        [nameof(Components.Emails.SessionReminder.QuestTitle)] = "The Tomb of Annihilation",
        [nameof(Components.Emails.SessionReminder.DmName)] = "Dungeon Master Theomund",
        [nameof(Components.Emails.SessionReminder.QuestDate)] = DateTime.Today.AddDays(1),
        [nameof(Components.Emails.SessionReminder.QuestDescription)] = "Deep in the jungles of Chult lies an ancient tomb that devours the souls of the dead. Your party must venture into the heart of darkness to stop the death curse before it claims you all.",
        [nameof(Components.Emails.SessionReminder.ConfirmedPlayerNames)] = SamplePlayers,
        [nameof(Components.Emails.SessionReminder.QuestUrl)] = $"{appUrl}/Quest",
        [nameof(Components.Emails.SessionReminder.ChallengeRating)] = 9,
        [nameof(Components.Emails.SessionReminder.AppUrl)] = appUrl,
    });
    return Content(html, "text/html");
}
```
A new `WaitlistPromoted()` action follows this identically, swapping the component type and its actual parameter list (check `WaitlistPromoted.razor`'s `@code` block for its exact `[Parameter]` set — it differs slightly from `QuestFinalized`/`SessionReminder`, e.g. no `ChallengeRating`/`ConfirmedPlayerNames` may not apply). Also add the sample-Markdown Description text long enough to exercise truncation (headings + list + blockquote), per RESEARCH.md's D-05 dev-loop recommendation — reuse the existing flat-prose sample only for the other actions, not this new one, since the whole point of a `WaitlistPromoted` preview addition here is to dev-loop-test the new structured-Markdown styling before the real send-test-email verification. Also add the corresponding `<li><a href="{{appUrl}}/EmailPreview/WaitlistPromoted">Waitlist Promoted</a></li>` entry to the `Index()` action's link list (line 27-34) — currently missing.

---

## Shared Patterns

### AngleSharp DOM-walk (the core mechanism this entire phase is built on)
**Source:** `QuestBoard.Domain/Services/MarkdownService.cs` `ExtractPlainText()` lines 112-146
**Apply to:** `RenderEmailHtml`'s style-injection, MSO bullet-fallback injection, and block-boundary truncation passes — all three walk `document.Body!.Children` (or `QuerySelectorAll`) using the same `new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html)` entry point already proven in this file.

### Sanitizer separation — never touch shared `AllowedAttributes`
**Source:** `QuestBoard.Domain/Services/MarkdownService.cs` lines 45-54 (`AllowedAttributes`, shared by both `WebSanitizer` and `EmailSanitizer`)
**Apply to:** `RenderEmailHtml` — must never add `"style"` to this shared set (would also affect `WebSanitizer`/the Web target used by every other Markdown field across Phases 66-70). New styles are injected via `IElement.SetAttribute()` strictly after `EmailSanitizer.Sanitize()` has already run, never through the sanitizer's own attribute allowlist.

### Inline-style-only email convention
**Source:** `QuestBoard.Service/Components/Emails/_EmailLayout.razor` (Georgia-serif/gold-parchment palette, referenced but not re-read this pass — already summarized in RESEARCH.md/CONTEXT.md) and every existing inline `style="..."` attribute throughout the 3 templates (e.g. `QuestFinalized.razor` lines 16, 27, 30, 33, 58-59, 82)
**Apply to:** All new heading/list/blockquote `style=` constants injected by `RenderEmailHtml` — match this file's existing color/font/text-shadow values exactly (per `71-UI-SPEC.md`, which is the authoritative source for the exact strings — do not re-derive).

### Doc-comment / in-code rationale voice
**Source:** `QuestBoard.Domain/Services/MarkdownService.cs` — nearly every non-trivial block has a preceding "why, not what" comment (lines 11-12, 15-19, 30-33, 49-51, 66-68, 100-103, 122-126, 132-134, 142-144)
**Apply to:** All new code in this phase — no phase/requirement IDs in comments per CLAUDE.md; explain the reasoning (e.g. why styles are hard-coded constants rather than derived from input, why truncation cuts at block boundaries not mid-element) in the same plain-language style already established in this file.

## No Analog Found

None — every file this phase touches has a direct, exact-match analog either in the same file (new method beside `ExtractPlainText`) or in a near-identical sibling file (the other 2 email templates, the other `EmailPreviewController` actions, the other `MarkdownServiceTests` test methods).

## Metadata

**Analog search scope:** `QuestBoard.Domain/Services/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Service/Components/Emails/`, `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.UnitTests/Services/`
**Files scanned:** `MarkdownService.cs`, `IMarkdownService.cs`, `QuestFinalized.razor` (full), `SessionReminder.razor`/`WaitlistPromoted.razor` (targeted grep on call-site/height/QuestUrl lines — identical structure to `QuestFinalized.razor` confirmed), `MarkdownServiceTests.cs` (first 80 lines), `EmailPreviewController.cs` (full)
**Pattern extraction date:** 2026-07-10
