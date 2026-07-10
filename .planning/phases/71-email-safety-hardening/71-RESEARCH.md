# Phase 71: Email-Safety Hardening - Research

**Researched:** 2026-07-10
**Domain:** Email-safe HTML generation from sanitized Markdig output (ASP.NET Core / Markdig / Ganss.Xss / AngleSharp), Outlook Word-engine rendering quirks, block-boundary-aware HTML truncation
**Confidence:** MEDIUM-HIGH (codebase mechanics VERIFIED by direct read; Outlook/Gmail rendering behavior CITED from current email-dev community sources, not independently re-verified against a live Outlook/Gmail instance by this research pass — that verification is exactly what D-05 does)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked at milestone-research level (not re-discussed, carried forward)
- **Scope is exactly 3 templates, exactly Quest Description.** No other field, no other email template. Confirmed via direct code read — `IQuestEmailDispatcher`/`QuestService` are the only dispatch paths that pass `QuestDescription` into a Razor email component.
- **Inline styles are the only viable email-CSS approach** — `<style>` blocks are stripped by Gmail; this app's existing email convention (see `_EmailLayout.razor` and all 8 email components) already inlines every style for this exact reason. Any new heading/list/blockquote styling for Markdown output must follow the same inline-`style=` convention, matching `_EmailLayout.razor`'s existing Georgia-serif/gold-parchment palette.
- **MSO-specific bullet-visibility fixes (e.g. conditional-comment fallback markup for `<ul>/<li>` in Outlook) are an implementation detail, not a user decision** — research names this pitfall but the exact technique (inline `list-style` + explicit unit `display:list-item`, vs. MSO conditional-comment fallback markup) is Claude's Discretion / research territory.

### Fixed-height card & clipping (the phase's central decision)
- **D-01:** The `height:840px;overflow:hidden` fixed poster-frame card is **not removed** — it stays exactly as-is. Rationale (from the user, not an engineering default): the fixed height exists specifically to prevent the poster background image from growing/breaking on the actual quest board page's equivalent layout; making the email card's height dynamic was explicitly rejected.
- **D-02:** Instead, Description is **truncated with a "read more" link** when it exceeds the space available inside the fixed-height card. All 3 templates already have an identical "View Quest Details" `<a href="@QuestUrl">` button in a later row (Row 6) — the truncation messaging should point at that existing button; no new link/button needs to be added.
- **D-03:** The truncated portion **preserves Markdown formatting** rather than falling back to plain text. Truncation must cut at a complete block-element boundary (never mid-list-item, mid-heading, or mid-blockquote) — the truncated HTML must remain valid, well-formed markup up to the cut point. This is a genuinely new truncation mechanism, distinct from the `ExtractPlainText()`-based character truncation used for the Shop/Quest board card teasers elsewhere in this milestone (Phase 66 D-06, Phase 70 D-01/D-02) — those strip to flat text; this one must preserve real HTML structure up to the cutoff.
- **D-04:** When truncated, show explicit "...read more" copy (e.g. "...continue reading on the quest board") at the cut-off point — do NOT rely on a silent cutoff. Makes truncation look intentional, not like a rendering bug.
- **Explicitly rejected:** the previously-existing `overflow-y:auto` scrollable-div approach (today's actual code) does not satisfy the requirement — confirmed with the user that Outlook's Word engine does not support scrollable divs at all, so "make it scroll" cannot be the fix.

### Real-client verification method
- **D-05:** Verification happens by sending a **real test email** (via the existing Resend/Postfix relay — no new send infrastructure) to the operator's own inbox for each of the 3 templates, triggered against a throwaway test quest with a Markdown-structured Description (headings + list + blockquote, long enough to exceed the truncation threshold and exercise D-01 through D-04). The operator opens each email in their actual Gmail webmail and actual Outlook desktop client and reports what they see. This is a live, blocking human-verification checkpoint — not resolvable by browser preview or automated screenshot tooling.
- **D-06:** Claude creates the throwaway test quest (and any signups/finalization/waitlist state needed to legitimately trigger each of the 3 job dispatches) rather than asking the operator to set it up manually.
- **Known constraint (not resolved here — surface at the checkpoint):** the operator currently has ready access to only one of Outlook desktop / Gmail webmail, not both, and may need to arrange access to the other before the live verification checkpoint can fully close both EMAILMD-02 acceptance criteria (Outlook AND Gmail). Planning should not assume both are trivially available at checkpoint time — the plan's human-verify task should surface this rather than silently assume simultaneous access.

### Claude's Discretion
- Exact CSS/inline-style values for headings, lists, and blockquotes inside the email context — **resolved by `71-UI-SPEC.md`**, already approved-pending. Use its exact `style=` strings verbatim; do not re-derive them.
- Exact truncation length/threshold for D-02/D-03 — **resolved by `71-UI-SPEC.md` Truncation contract:** ~5 top-level block elements OR ~650 characters of extracted plain text, whichever hit first, tunable during D-05.
- Exact mechanism for creating/cleaning up the throwaway test quest and triggering each of the 3 job dispatches (`QuestFinalizedEmailJob`, `SessionReminderJob`/`DailyReminderJob`, `QuestWaitlistPromotedEmailJob`) for D-05/D-06 — see `## Test-Email Trigger Mechanism` below.
- Whether to reuse/extend the existing `EmailPreviewController` (Admin-only, browser-render-only, no `WaitlistPromoted` preview action) as an interim dev-loop tool — recommended as a cheap addition, not a substitute for D-05.

### Deferred Ideas (OUT OF SCOPE)
None — no scope creep surfaced during discussion.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EMAILMD-02 | A recipient viewing these 3 emails in real Outlook desktop or Gmail webmail sees correctly formatted content (visible bullets, intact styling) — not broken or missing formatting | `## Architecture Patterns` (inline-style injection mechanism, Outlook bullet fallback) + `## Common Pitfalls` (Ganss.Xss/AngleSharp.Css CSS-drop pitfall) + `## Validation Architecture` |
| EMAILMD-03 | A recipient can read the full quest description in these emails even when formatted with headings/lists/blockquotes — content is not silently clipped by a fixed-height card | `## Architecture Patterns` (block-boundary truncation mechanism) + `## Code Examples` |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- Windows development environment, CRLF line endings, Windows-style paths.
- Never commit to `main` — this phase's work must land on `milestone/v8-markdown-support` (current branch) or a phase-specific feature branch per existing convention.
- EF Core packages belong only in `QuestBoard.Repository` — not relevant to this phase (no schema changes expected: no new entity/migration needed, this phase is pure rendering-pipeline logic).
- Never embed GSD requirement IDs (`EMAILMD-02`, `D-03`, etc.) in source code comments — write plain-language "why" comments instead, matching the existing style already used throughout `MarkdownService.cs`.
- All new views/pages must use the modern-card CSS pattern — **not applicable to this phase**: the only UI surface touched is transactional email HTML (fully separate design system per `71-UI-SPEC.md`), not an app view.
- `dotnet build` / `dotnet test` may fail with locked-file errors if Visual Studio's debugger is attached — ask the user to stop debugging (Shift+F5) before build/test if this occurs.

## Summary

This phase closes two verified, pre-existing gaps in `QuestBoard.Domain/Services/MarkdownService.cs`'s Email rendering path, both scoped to the 3 quest email templates. No new NuGet packages are required — Markdig 1.3.2, HtmlSanitizer (Ganss.Xss) 9.0.892, and AngleSharp 0.17.1 are already project dependencies (confirmed in `QuestBoard.Domain/QuestBoard.Domain.csproj`), and AngleSharp is already used in this exact file's `ExtractPlainText()` method, so no new library research or legitimacy audit is needed — only new *usage* of existing libraries.

**Primary recommendation:** Do NOT route the new inline styles through `EmailSanitizer`'s `style` attribute allowlist. Instead, keep sanitization exactly as it is today (structural HTML only, no `style` attribute exposed to the sanitizer's CSS validator), and add a **new post-sanitization AngleSharp DOM pass** that (a) walks the sanitized HTML's top-level block elements and every nested tag Markdig can emit, (b) sets `style` attributes to compile-time-constant strings taken verbatim from `71-UI-SPEC.md` (never derived from user input, so no new XSS surface), (c) injects the MSO-conditional bullet-fallback comment as the first child of every `<li>`, and (d) performs the D-03 block-boundary truncation and appends the D-04 read-more link — all in the same DOM walk, in one new method. This sidesteps a verified real-world defect class in Ganss.Xss's style-attribute sanitizer (it silently drops an entire `style` attribute — not just the unrecognized property — when its bundled AngleSharp.Css parser fails to parse any part of the CSS value; this exact bug has been reported against header/text-align values and is not confirmed fixed in the specific `AngleSharp.Css 0.17.0` version this project's `HtmlSanitizer 9.0.892` pulls in). Setting `style` via `IElement.SetAttribute()` after sanitization bypasses that CSS parser entirely — AngleSharp's plain DOM API does not validate or reformat style-string content, it just writes the string.

For Outlook `<ul>/<li>` bullet visibility, the primary fix (`list-style-type:disc;display:list-item;`, already specified in `71-UI-SPEC.md`) is necessary but has a known history of being insufficient alone in Outlook desktop across versions; the UI-SPEC's own belt-and-suspenders fallback — an MSO-conditional inline comment (`<!--[if mso]>&#8226;&nbsp;<![endif]-->`) prepended inside every `<li>` — is a well-established, low-risk technique because this is 100% server-generated trusted markup (not user-authored HTML being sanitized), so injecting a raw HTML comment via `IDocument.CreateComment()` is safe.

For the D-03 truncation, no library exists for "cut HTML at a block boundary" — this is necessarily hand-rolled, but it is a small, well-bounded DOM walk (accumulate top-level `Body.Children`, stop at whichever of the two UI-SPEC budgets is hit first, serialize only the kept elements' `OuterHtml`), directly analogous to the pattern `ExtractPlainText()` already uses one file above it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Markdown → sanitized structural HTML | API / Backend (`QuestBoard.Domain.Services.MarkdownService`) | — | Existing Phase 65 responsibility, untouched by this phase except as an upstream input |
| Email-safe inline-style injection | API / Backend (new logic in/near `MarkdownService`, `QuestBoard.Domain`) | — | Must run server-side before the HTML is embedded in a Razor email component and sent; no client tier exists for a transactional email |
| Outlook MSO bullet-fallback injection | API / Backend (same pass as style injection) | — | Same DOM walk, same trust boundary (server-generated, never user-authored at this stage) |
| Block-boundary-aware truncation + read-more link | API / Backend (new logic, `QuestBoard.Domain`) | Service tier (needs `QuestUrl`, which is a Razor-component parameter) | Truncation needs the per-quest `QuestUrl` to build the read-more link — this is either passed into a new `IMarkdownService` method as a parameter, or the truncation step lives one layer up where `QuestUrl` is already in scope (the 3 Razor components / their Hangfire job callers) |
| Real Razor email rendering (`IEmailRenderService.RenderAsync`) | API / Backend | — | Pre-existing, unchanged |
| Live-client verification (D-05) | Human / Operator | — | Cannot be automated — Outlook desktop and Gmail webmail rendering cannot be verified by a browser preview or headless tool per D-05's explicit rejection of that approach |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Markdig | 1.3.2 | Markdown → HTML | Already the project's locked Markdown engine (Phase 65 research); unchanged by this phase [VERIFIED: codebase — `QuestBoard.Domain/QuestBoard.Domain.csproj`] |
| HtmlSanitizer (Ganss.Xss) | 9.0.892 | XSS-safe HTML sanitization of Markdig output | Already the project's locked sanitizer; unchanged behavior for the Email target's structural sanitization pass [VERIFIED: codebase] |
| AngleSharp | 0.17.1 | HTML DOM parsing/manipulation | Already used in this exact file (`ExtractPlainText()`); this phase adds a second consumer of the same dependency, no new package [VERIFIED: codebase — `MarkdownService.cs` lines 120-138] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AngleSharp.Css | 0.17.0 (transitive, pulled in by `HtmlSanitizer 9.0.892`) | CSS-value validation inside `HtmlSanitizer`'s style-attribute sanitizer | **Do not route new styles through this** — see Common Pitfalls. Its presence is noted only because it is the mechanism this research recommends avoiding [CITED: nuget.org/packages/HtmlSanitizer] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AngleSharp DOM post-processing (recommended) | Custom Markdig `HtmlRenderer` subclass / `IMarkdownExtension` that emits `style=` attributes directly during Markdig rendering (Option A from phase description) | Requires overriding Markdig's per-block-type object renderers (`HeadingRenderer`, `ListRenderer`, `ListItemRenderer`, `QuoteBlockRenderer`, `ParagraphRenderer`, etc.) and either building a second `MarkdownPipeline` for Email or branching renderer selection on `MarkdownRenderTarget` at call time — meaningfully more code and more Markdig-internals coupling than a DOM post-process, and the resulting `style=` attribute would then need to survive `EmailSanitizer.Sanitize()`, which re-introduces the CSS-parser-drop pitfall this research recommends avoiding. Rejected as unnecessarily complex for a single render target. [ASSUMED — general Markdig extensibility knowledge, not verified against Markdig 1.3.2's exact renderer API surface this session] |
| AngleSharp DOM post-processing (recommended) | Targeted regex over the sanitized HTML string (Option C from phase description) | Regex-editing HTML is a well-known fragility trap (nested tags, attribute-order variance, self-closing vs. not) and the codebase already has a DOM parser dependency and an established DOM-walk pattern (`ExtractPlainText`) to reuse — regex offers no advantage here and a correctness risk. Rejected. |
| `style` in `EmailSanitizer.AllowedAttributes` + `AllowedCssProperties` allowlist tuning | Post-sanitization `SetAttribute()` injection (recommended) | Requires enumerating and testing every CSS property/value combination in `71-UI-SPEC.md` (font-shorthand, `rgba()` multi-value `text-shadow`, `list-style-type`, `display:list-item`) against the exact `AngleSharp.Css 0.17.0` parser bundled with `HtmlSanitizer 9.0.892` to confirm none are silently dropped — a real, non-trivial verification burden the recommended approach entirely avoids by never sending compile-time-constant style strings through that parser. |

**Installation:** None required — no new packages.

**Version verification:**
```
QuestBoard.Domain.csproj: Markdig 1.3.2, HtmlSanitizer 9.0.892, AngleSharp 0.17.1
```
[VERIFIED: codebase — read directly from `QuestBoard.Domain/QuestBoard.Domain.csproj`]. `HtmlSanitizer 9.0.892` is confirmed as the current latest stable release on NuGet as of this research (a `9.1.949-beta` prerelease exists but is not installed and should not be adopted mid-milestone) [CITED: nuget.org/packages/HtmlSanitizer]. `AngleSharp.Css 0.17.0` is the exact transitive version `HtmlSanitizer 9.0.892` depends on, confirmed both via the installed NuGet cache (`~/.nuget/packages/anglesharp.css/0.17.0`) and via the HtmlSanitizer NuGet dependency listing [VERIFIED: local nuget cache + CITED: nuget.org].

## Package Legitimacy Audit

No new external packages are introduced by this phase — it exclusively adds new *usage* of three already-installed, already-vetted dependencies (Markdig, HtmlSanitizer, AngleSharp), all of which were verified during Phase 65's research and remain unchanged in version. The Package Legitimacy Gate is not applicable; no `npm view`/registry check was run because no `PackageReference` changes are proposed.

| Package | Registry | Status | Disposition |
|---------|----------|--------|-------------|
| Markdig 1.3.2 | NuGet | Already installed, verified Phase 65 | No change — reused |
| HtmlSanitizer 9.0.892 | NuGet | Already installed, verified Phase 65 | No change — reused |
| AngleSharp 0.17.1 | NuGet | Already installed, verified Phase 65 | No change — reused |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Quest data (DB) ──> Hangfire job (QuestFinalizedEmailJob /
                     SessionReminderJob / QuestWaitlistPromotedEmailJob)
                          │
                          │ passes questDescription, questUrl, etc.
                          ▼
              IEmailRenderService.RenderAsync<TEmailComponent>()
                          │
                          ▼
        Razor email component (QuestFinalized.razor / SessionReminder.razor /
                                 WaitlistPromoted.razor)
                          │
                          │ calls (NEW call site, replaces RenderToHtml(..., Email))
                          ▼
        IMarkdownService.RenderEmailHtml(markdown, readMoreUrl)   <-- NEW METHOD
                          │
             ┌────────────┼─────────────────────────────┐
             ▼            ▼                              ▼
     Markdig.ToHtml   EmailSanitizer.Sanitize      (existing, UNCHANGED —
     (existing,        (existing, UNCHANGED —        structural allowlist only,
      UNCHANGED)        no "style" in allowlist)      no CSS parsing of new styles)
                          │
                          ▼
          NEW: AngleSharp DOM post-process pass
          ┌─────────────────────────────────────────┐
          │ 1. Parse sanitized HTML fragment         │
          │ 2. Walk every element; SetAttribute      │
          │    ("style", constant-from-UI-SPEC)      │
          │ 3. For each <li>: prepend MSO-conditional │
          │    bullet-fallback comment node           │
          │ 4. Walk top-level Body.Children,          │
          │    accumulate budget (blocks / chars);    │
          │    stop at first budget hit (D-03)        │
          │ 5. If truncated: append <p> read-more      │
          │    link styled per UI-SPEC (D-04)          │
          │ 6. Serialize kept elements' OuterHtml       │
          └─────────────────────────────────────────┘
                          │
                          ▼
              Final email-ready HTML string
                          │
                          ▼
        @((MarkupString)...) rendered inside Row 3 <td>
        (no scrollable inner <div> — removed per UI-SPEC item 1)
                          │
                          ▼
              IEmailService.SendAsync() via SMTP relay (unchanged)
                          │
              ┌───────────┴───────────┐
              ▼                       ▼
     Real Outlook desktop      Real Gmail webmail
     (D-05 human verification, both required for EMAILMD-02)
```

### Recommended Project Structure

No new files are strictly required — the new logic can live in `QuestBoard.Domain/Services/MarkdownService.cs` as a new private helper plus one new public interface method. If the DOM-walk logic grows large enough to warrant separation (style table + truncation logic together is a few hundred lines), a sibling internal class is the established pattern for this project:

```
QuestBoard.Domain/
├── Interfaces/
│   └── IMarkdownService.cs         # add RenderEmailHtml(...) method signature
├── Services/
│   ├── MarkdownService.cs          # implement RenderEmailHtml(...), reuse existing Pipeline/EmailSanitizer
│   └── EmailMarkdownStyler.cs      # OPTIONAL: extract the AngleSharp style/truncation DOM-walk if it grows large
```

Matches the existing pattern of `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` + `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` (interface in Domain, implementation detail kept close to its single caller) [VERIFIED: codebase — `.planning/codebase/ARCHITECTURE.md` line 184].

### Pattern 1: Post-sanitization inline-style injection via AngleSharp

**What:** After `EmailSanitizer.Sanitize(rawHtml)` produces trusted, tag/attribute-allowlisted HTML, parse it with AngleSharp and call `element.SetAttribute("style", constantString)` for every element Markdig can emit (per the `71-UI-SPEC.md` typography table). The style strings are hard-coded C# constants — never built from `markdown` input — so this step introduces no new sanitization requirement.

**When to use:** Any time inline styles must be applied to server-controlled elements after they have already passed through an HTML sanitizer, without re-opening the sanitizer's attribute/CSS-value allowlist.

**Example:**
```csharp
// Source: AngleSharp.Html.Parser.HtmlParser (already used in this file's ExtractPlainText),
// combined with IElement.SetAttribute — standard AngleSharp DOM API.
private static readonly Dictionary<string, string> EmailBlockStyles = new(StringComparer.OrdinalIgnoreCase)
{
    ["h1"] = "font-size:20px;font-weight:700;font-style:normal;font-family:Georgia,serif;line-height:1.25;color:#1a0f08;margin:16px 0 8px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);",
    ["h2"] = "font-size:20px;font-weight:700;font-style:normal;font-family:Georgia,serif;line-height:1.25;color:#1a0f08;margin:16px 0 8px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);",
    // ... h3-h6, p, ul, ol, blockquote, a, strong, em, hr per 71-UI-SPEC.md verbatim
};

var parser = new AngleSharp.Html.Parser.HtmlParser();
using var document = parser.ParseDocument(sanitizedHtml);

foreach (var element in document.Body!.QuerySelectorAll(string.Join(",", EmailBlockStyles.Keys)))
{
    element.SetAttribute("style", EmailBlockStyles[element.TagName.ToLowerInvariant()]);
}

// li needs a per-parent-type style (ul vs ol) — resolve from element.ParentElement.TagName
foreach (var li in document.Body!.QuerySelectorAll("li"))
{
    var isOrdered = li.ParentElement?.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase) == true;
    li.SetAttribute("style", isOrdered ? OrderedLiStyle : UnorderedLiStyle);
}
```
[ASSUMED for exact `QuerySelectorAll` overload signature — standard AngleSharp DOM API, consistent with the `document.Body!.Children` pattern already verified in this file's `ExtractPlainText`, but the specific selector-string overload was not re-verified against AngleSharp 0.17.1's API this session]

### Pattern 2: Outlook bullet-visibility fallback (MSO-conditional inline comment)

**What:** Prepend a raw HTML comment inside each `<li>` that only Outlook's Word rendering engine interprets as conditional markup; every other client (Gmail webmail, mobile clients, browsers) sees it as an inert comment.

**When to use:** When the primary CSS fix (`list-style-type:disc;display:list-item;`, already in `71-UI-SPEC.md`) is confirmed insufficient during D-05 live verification. UI-SPEC already names this as the sanctioned fallback technique — implement it proactively (cheap, zero risk to non-Outlook clients) rather than waiting for D-05 to fail first, since D-05 is a blocking human checkpoint and a second round-trip is expensive.

**Example:**
```csharp
// Source: general HTML-email development convention (Litmus / goodemailcode.com), confirmed
// applicable here because this is 100% server-generated trusted markup, not user-authored HTML
// being re-sanitized -- the comment node is created directly via AngleSharp's DOM API and never
// passes back through EmailSanitizer.
foreach (var li in document.Body!.QuerySelectorAll("li"))
{
    var bulletFallback = document.CreateComment("[if mso]>&#8226;&nbsp;<![endif]");
    li.InsertBefore(bulletFallback, li.FirstChild);
}
```
Renders as `<li style="..."><!--[if mso]>&#8226;&nbsp;<![endif]-->...content...</li>` — Outlook's Word engine evaluates the conditional comment and shows a literal bullet character as a fallback if its native `list-style-type`/`display:list-item` rendering still fails to show one; Gmail and browsers render the comment as invisible. [CITED: litmus.com/blog/the-ultimate-guide-to-bulleted-lists-in-html-email + goodemailcode.com/email-enhancements/mso-styles.html — cross-checked across two independent email-dev sources] [ASSUMED for the exact `IDocument.CreateComment` method name / `InsertBefore` signature — not independently re-verified against AngleSharp 0.17.1's exact API this session, though both are standard W3C DOM methods AngleSharp implements]

### Pattern 3: Block-boundary-aware HTML truncation

**What:** Walk the top-level block children of the (now styled) email HTML fragment, accumulate a running block count and running plain-text character count, and stop including elements the moment either budget (~5 blocks, ~650 plain-text characters per `71-UI-SPEC.md`) is exceeded — never partially including an element.

**When to use:** Exactly this phase's D-03 requirement. Do not reuse `ExtractPlainText()`'s flat-text truncation pattern — this is deliberately a different, HTML-structure-preserving mechanism per D-03/`66-CONTEXT.md`.

**Example:**
```csharp
// Source: new pattern, modeled on this file's existing ExtractPlainText() DOM-walk
// (document.Body!.Children iteration), but stopping early instead of joining all children.
var kept = new List<IElement>();
var plainTextLength = 0;
var truncated = false;

foreach (var block in document.Body!.Children)
{
    var blockText = block.TextContent.Trim();
    var wouldExceedBlocks = kept.Count >= MaxTopLevelBlocks;
    var wouldExceedChars = plainTextLength + blockText.Length > MaxPlainTextChars;

    if (wouldExceedBlocks || wouldExceedChars)
    {
        truncated = true;
        break;
    }

    kept.Add(block);
    plainTextLength += blockText.Length;
}

if (kept.Count > 0)
{
    kept[0].SetAttribute("style", kept[0].GetAttribute("style") + "margin-top:0;"); // Spacing Scale exception
}

var html = string.Concat(kept.Select(e => e.OuterHtml));

if (truncated)
{
    html += $"""<p style="margin:0;">{ReadMoreLinkHtml(readMoreUrl)}</p>""";
}
else if (kept.Count > 0)
{
    kept[^1].SetAttribute("style", kept[^1].GetAttribute("style") + "margin-bottom:0;");
    html = string.Concat(kept.Select(e => e.OuterHtml)); // re-serialize after last-element style mutation
}
```
Note: mutating `style` via string concatenation on an already-set attribute (as shown for the margin-zero exceptions) is acceptable here specifically because the value being appended is itself a compile-time constant, not derived from `markdown` input — same trust argument as Pattern 1.

### Anti-Patterns to Avoid

- **Adding `style` to `EmailSanitizer.AllowedAttributes` and routing new styles through it:** re-opens a class of Ganss.Xss/AngleSharp.Css bugs where an entire `style` attribute is silently dropped if any single CSS declaration fails that specific parser's validation — the value would then need to be tested empirically against the pinned `AngleSharp.Css 0.17.0` version, and any future dependency bump could silently regress it. Avoid by never exposing the new styles to that code path at all (see Common Pitfalls).
- **Reusing `ExtractPlainText()`'s flat-text truncation for D-02/D-03:** explicitly rejected by `71-CONTEXT.md` D-03 — that method discards all HTML structure, which fails the "preserves Markdown formatting" requirement.
- **Modifying `WebSanitizer` or the shared `AllowedAttributes`/`BaseAllowedTags` sets:** this phase's scope is the Email target only; any change to the shared `AllowedAttributes` HashSet (used by both `WebSanitizer` and `EmailSanitizer`) risks regressing the Web target used by every other Markdown-bearing field across Phases 66-70. Keep the new logic entirely additive, downstream of sanitization, and Email-target-only.
- **Relying on the inner `overflow-y:auto` scrollable `<div>` (today's code):** confirmed by the user as non-functional in Outlook's Word rendering engine — this is the exact bug the phase exists to fix, not a fallback to preserve.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTML parsing/DOM manipulation | A custom HTML tokenizer/regex-based tag walker | AngleSharp (already installed, already used in this file) | Regex-based HTML manipulation is a well-known correctness trap (nested tags, self-closing variance, attribute quoting); AngleSharp is already proven in this exact file for a structurally similar DOM-walk (`ExtractPlainText`) |
| XSS-safe HTML sanitization | A custom tag/attribute stripper | HtmlSanitizer (Ganss.Xss), already the project's `EmailSanitizer`/`WebSanitizer` | Unchanged from Phase 65 — this phase does not touch the trust boundary for user-supplied Markdown content, only post-processes already-sanitized, server-controlled output |
| MSO conditional-comment email hacks | A custom Outlook-detection scheme (user-agent sniffing is not possible/relevant in a static email) | The standard `<!--[if mso]>...<![endif]-->` conditional-comment convention, well documented across the HTML-email development community | This is a decades-old, universally supported Outlook/Word-engine parsing convention — inventing an alternative technique has no benefit and higher risk of client-specific breakage |

**Key insight:** Everything genuinely new in this phase (style injection ordering, truncation-at-block-boundary) is glue logic around already-installed, already-proven libraries — there is no case in this phase where a library should be introduced or a novel parsing algorithm should be built from scratch.

## Common Pitfalls

### Pitfall 1: Ganss.Xss / AngleSharp.Css silently drops an entire `style` attribute on a single unparseable CSS value

**What goes wrong:** If `style` were added to `EmailSanitizer.AllowedAttributes` and the new inline styles were included in Markdig's raw output (or otherwise passed through `HtmlSanitizer.Sanitize()`), the whole `style` attribute — not just the offending property — can be stripped when the bundled `AngleSharp.Css` parser fails to parse any single declared value.

**Why it happens:** `HtmlSanitizer`'s style-sanitization path calls `element.GetStyle()` (an `AngleSharp.Css` API) to parse the attribute value into a CSS object model, converts it back to a string, then validates individual properties against `AllowedCssProperties`. A documented real-world case (GitHub Discussion #575 on `mganss/HtmlSanitizer`) showed the entire `style` attribute vanishing from `<h1>`-`<h6>` elements because the bundled `AngleSharp.Css` version at the time didn't recognize `text-align: start` — the parse failure cascaded to the whole attribute, not just that one declaration. The maintainer's fix was to upgrade to a newer prerelease with a newer `AngleSharp`/`AngleSharp.Css`. This project's installed `HtmlSanitizer 9.0.892` still pins `AngleSharp.Css 0.17.0` (confirmed via NuGet dependency listing and local package cache) — whether this specific version still exhibits the bug for values this phase would use (multi-shadow `rgba()` `text-shadow`, `list-style-type:disc`, `display:list-item`) was not tested this session.

**How to avoid:** Do not add `style` to `EmailSanitizer.AllowedAttributes`. Inject styles after sanitization via `IElement.SetAttribute("style", ...)`, which never invokes `AngleSharp.Css`'s CSS-value parser at all (see Pattern 1). This makes the pitfall structurally impossible rather than requiring case-by-case empirical verification.

**Warning signs:** If the planner or executor later decides to route styles through the sanitizer anyway (e.g. for a future field), a unit test asserting `Sanitize(rawHtmlWithStyle).Should().Contain("text-shadow")` for every planned style string is mandatory before trusting that path — do not assume the sanitizer preserves any given CSS value without a test proving it for the exact declared value shipped in `71-UI-SPEC.md`.

[VERIFIED: local nuget cache confirms `AngleSharp.Css 0.17.0` is the installed transitive dependency] + [CITED: github.com/mganss/HtmlSanitizer/discussions/575 — real reported case, root cause explained by the library maintainer] — the *conclusion that this exact version still has the bug* is NOT independently verified this session; it is a risk flag, not a confirmed defect. Treat as MEDIUM confidence.

### Pitfall 2: Outlook desktop (Word rendering engine) does not support CSS `overflow`/scroll on `<div>` at all

**What goes wrong:** Today's actual code (`overflow-y:auto` on the inner Description wrapper `<div>`) renders as if `overflow` were simply absent in Outlook — the div does not scroll, and content beyond the fixed `840px` card height is invisibly clipped with no visual indication anything is missing.

**Why it happens:** Outlook desktop (2007+) renders HTML email bodies using Microsoft Word's layout engine, not a browser engine (Trident/EdgeHTML/Chromium/WebKit) — Word's engine has never implemented CSS `overflow` scrolling on block elements. This is long-standing, well-documented behavior across the HTML-email development community.

**How to avoid:** D-01/D-02 already resolve this at the design level (fixed height stays, but content is truncated instead of relying on scroll) — this phase's job is to implement that resolution, not re-attempt scrolling. Per `71-UI-SPEC.md` item 1, remove the `overflow-y:auto`/`height:100%` inner wrapper entirely; the `<td>` keeps `vertical-align:top` only.

**Warning signs:** Any code path that re-introduces `overflow` or `height:100%` on the Description inner wrapper is regressing this fix.

[CITED: cross-referenced across Litmus and multiple email-dev sources during this session's WebSearch pass — this is uncontested, widely-known behavior, not a single-source claim]

### Pitfall 3: `<ul>/<li>` bullets can still be invisible in Outlook desktop even with correct CSS

**What goes wrong:** Even with `list-style-type:disc;display:list-item;` set correctly, some Outlook desktop versions (2007-2016 era, and inconsistently in newer 365 builds depending on how the message was composed/relayed) still fail to render a visible bullet marker.

**Why it happens:** Word's rendering engine has historically diverged from CSS list-rendering behavior; this is a long-standing, version-inconsistent quirk rather than a single deterministic bug.

**How to avoid:** Implement the belt-and-suspenders MSO-conditional-comment bullet-character fallback (Pattern 2) proactively rather than waiting to discover the gap during the D-05 blocking human-verification checkpoint — a second full plan→execute→verify round trip to fix a missed bullet would be expensive given D-05 is a live, manual, blocking gate.

**Warning signs:** D-05 verification report showing "no visible bullets" in Outlook desktop despite the primary CSS fix being present in the sent email's HTML source.

[CITED: litmus.com/blog/the-ultimate-guide-to-bulleted-lists-in-html-email + goodemailcode.com — cross-checked two independent sources, consistent findings]

### Pitfall 4: `_EmailLayout.razor` already contains one small `<style>` block — do not treat "fully inline, zero `<style>` blocks" as strictly true

**What goes wrong:** `71-CONTEXT.md`'s Established Patterns section characterizes the existing convention as "no `<style>` blocks anywhere," but `_EmailLayout.razor` (shared by all 8 email components, including the 3 in scope) actually contains a small `<style>` block in `<head>` applying a redundant `text-shadow` to all `td, p` elements.

**Why it happens:** That block is a low-stakes redundant enhancement layered on top of already-fully-inlined per-element `text-shadow` declarations — if Gmail strips it, nothing visually breaks, because every `td`/`p` in these templates already repeats the same `text-shadow` inline. It is not evidence that a head-level `<style>` block is a safe primary mechanism for anything load-bearing.

**How to avoid:** Continue treating inline `style=` as the only mechanism for anything load-bearing (per the locked constraint) — do not use this existing block as precedent for putting new, non-redundant styling (e.g. the new heading/list/blockquote rules) into a `<style>` block instead of inline.

**Warning signs:** A future contributor citing "there's already a `<style>` block in `_EmailLayout.razor`" as justification for moving load-bearing rules out of inline styles.

[VERIFIED: codebase — `QuestBoard.Service/Components/Emails/_EmailLayout.razor` lines 9-16]

## Code Examples

### New `IMarkdownService` method signature (proposed)
```csharp
// Source: new pattern for this phase, extending the existing interface
// (QuestBoard.Domain/Interfaces/IMarkdownService.cs)
/// <summary>
/// Renders Quest Description Markdown to email-safe HTML: sanitized structural HTML (same
/// EmailSanitizer as RenderToHtml(..., Email)), then every block/inline element gets an explicit
/// inline style= per the email typography contract, Outlook bullet-visibility fallback comments
/// are added to every &lt;li&gt;, and content is truncated at a complete block-element boundary
/// with a "read more" link appended if the configured budget is exceeded.
/// </summary>
string RenderEmailHtml(string? markdown, string readMoreUrl, int maxTopLevelBlocks = 5, int maxPlainTextChars = 650);
```

### Existing call-site pattern being replaced (all 3 templates, identical today)
```razor
@* Source: QuestBoard.Service/Components/Emails/QuestFinalized.razor line 43 (SessionReminder.razor:44, WaitlistPromoted.razor:43 identical) *@
@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))
```
Becomes:
```razor
@((MarkupString)MarkdownService.RenderEmailHtml(QuestDescription, QuestUrl))
```
`QuestUrl` is already a required parameter on all 3 components (`QuestFinalized.razor:102`, `SessionReminder.razor:103`, `WaitlistPromoted.razor:102`) [VERIFIED: codebase], so no new parameter needs to flow from the Hangfire jobs — the existing `@QuestUrl`/`href="@QuestUrl"` value already used by the Row 6 CTA button is reused verbatim, satisfying D-02's "no new link" requirement.

### Test-Email Trigger Mechanism (for D-05/D-06)

`IQuestEmailDispatcher` (implemented by `HangfireQuestEmailDispatcher`, `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs`) is the real dispatch path `QuestService` calls when a quest is finalized or a waitlist promotion occurs [VERIFIED: codebase]. `SessionReminderJob`/`DailyReminderJob` fire from a finalized quest with a proposed date matching "tomorrow," or can be triggered by a DM's manual resend action (`useYesMaybeVoters` parameter) [VERIFIED: codebase — `QuestBoard.Service/Jobs/SessionReminderJob.cs`].

Two viable options for D-06, in order of fidelity:
1. **End-to-end via the real UI/business flow (recommended):** create a throwaway test quest through the actual app (Create Quest with a Markdown-structured Description), add a signup, finalize it (triggers `QuestFinalizedEmailJob`), let/force the reminder path fire (triggers `SessionReminderJob`), and separately create a full quest + waitlist scenario to trigger `QuestWaitlistPromotedEmailJob`. Most faithful to production behavior, exercises the real dedup guards (`FinalizedEmailSentForDate`, `ReminderLogRepository`) rather than bypassing them.
2. **Direct Hangfire job invocation (faster, lower fidelity):** call `IBackgroundJobClient.Enqueue<TJob>(j => j.ExecuteAsync(...))` directly with hand-constructed parameters (mirroring the pattern already used in `AdminController.cs` for other job types, e.g. `jobClient.Enqueue<WelcomeEmailJob>(...)` [VERIFIED: codebase — `QuestBoard.Service/Controllers/Admin/AdminController.cs` lines 141, 156, 175, 274, 412]), bypassing the quest-state preconditions each job normally checks. Faster to set up but does not exercise the guard logic, and risks a mismatch between test data and what a real quest would produce.

Recommend Option 1 for the actual D-05 verification email (must reflect real production behavior), with Option 2 acceptable only for rapid dev-loop iteration on styling before the final verification send.

`EmailPreviewController` (`QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs`) already exists as an Admin-only, browser-render-only preview tool with `QuestFinalized()` and `SessionReminder()` actions but **no `WaitlistPromoted()` action** [VERIFIED: codebase]. Adding a `WaitlistPromoted()` preview action (mirroring the existing two, using a Markdown-structured sample description) is a cheap, useful interim dev-loop step before the real send — but per D-05, browser preview alone cannot substitute for the live-client verification since it cannot surface Outlook-specific rendering defects.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Inner `<div style="height:100%;overflow-y:auto;">` scroll wrapper around Description (today's actual code, all 3 templates) | Block-boundary-truncated HTML with an inline "read more" link, no scroll wrapper | This phase (D-01/D-02/71-UI-SPEC.md) | Fixes a real, previously undiscovered Outlook-desktop clipping bug — Outlook's Word engine has never supported this scroll technique, so any Description that overflowed 840px was silently and unrecoverably lost in Outlook prior to this phase |
| `EmailSanitizer` with zero inline styling on block elements | `EmailSanitizer` unchanged, plus a new post-sanitization AngleSharp styling pass | This phase (EMAILMD-02) | Headings/lists/blockquotes in Quest Description previously rendered with default browser/client styling only (no bullets visible in Outlook, no visual hierarchy) — this phase makes them match the app's established Georgia-serif/gold-parchment email palette |

**Deprecated/outdated:**
- The `overflow-y:auto` scrollable-div approach: confirmed non-functional in Outlook desktop, removed by this phase per `71-UI-SPEC.md` item 1.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The specific `AngleSharp.Css 0.17.0` version pinned by `HtmlSanitizer 9.0.892` still exhibits the "whole style attribute dropped on unparseable value" defect reported in GitHub Discussion #575 (that discussion did not confirm this exact version, only that the bug existed in *some* older bundled version and was fixed in a *newer prerelease* of HtmlSanitizer) | Common Pitfalls — Pitfall 1 | Low — even if this specific version does NOT still exhibit the bug, the recommended architecture (post-sanitization `SetAttribute` injection) is still strictly safer/simpler and has no downside, so no replanning is needed even if this assumption is wrong. It only affects whether "route styles through the sanitizer" would ALSO have worked — moot since it's not the recommended path. |
| A2 | Exact AngleSharp 0.17.1 method/overload names (`IDocument.CreateComment`, `IElement.QuerySelectorAll(string)`, `INode.InsertBefore`) match the standard W3C DOM API AngleSharp is known to implement | Architecture Patterns — Code Examples | Low-Medium — these are all standard, widely-documented DOM methods; if a specific overload differs, the fix is a one-line signature adjustment during implementation, not an architectural change. The planner/executor should confirm via IntelliSense/compile rather than trusting this document blindly for exact method signatures. |
| A3 | The Outlook `<ul>/<li>` bullet-visibility fix techniques cited (`list-style-type`/`display:list-item` primary + MSO-conditional-comment fallback) are still current best practice as of this research date and not superseded by a newer Outlook/Word-engine behavior change | Architecture Patterns — Pattern 2, Common Pitfalls — Pitfall 3 | Medium — this is exactly what D-05's live human verification exists to catch. If Outlook's actual current behavior differs from these cited sources, D-05 will surface it and the fallback-comment mechanism (already planned proactively) gives a second chance without a full replan. |
| A4 | The custom Markdig-renderer-override approach (Option A, rejected as an alternative) would in fact require building a second pipeline or target-conditional renderer selection — this reasoning is based on general Markdig extensibility knowledge, not a verified read of Markdig 1.3.2's exact `HtmlRenderer`/`ObjectRenderer` API this session | Standard Stack — Alternatives Considered | Low — this claim only supports a "why we didn't choose X" argument; the recommended approach (Option B, AngleSharp post-processing) does not depend on this assumption being correct. |

## Open Questions (RESOLVED)

1. **Does the recommended `RenderEmailHtml` truncation budget (5 blocks / 650 chars) actually fit visually inside the 840px card once the new heading/list/blockquote inline styles are applied?**
   - What we know: `71-UI-SPEC.md` explicitly frames this as a starting heuristic, "tune during D-05 live verification, not a hard pixel guarantee."
   - What's unclear: Whether 5 blocks / 650 chars leaves the right amount of headroom for Rows 4-6 (divider, metadata, CTA) inside the fixed 840px frame, especially on `SessionReminder.razor` which has an extra "The Adventure:" label row before the Description div that the other two templates don't have.
   - Recommendation: Implement the constants as named, easily-adjustable values (not magic numbers scattered through logic) so D-05's live-verification feedback can tune them without a structural code change. Flag `SessionReminder.razor`'s extra label row as a reason its effective budget may need to be slightly smaller than the other two templates' — the plan should not assume all 3 templates need byte-identical truncation constants if D-05 reveals otherwise.
   - **RESOLVED:** Plan 71-01 implements the budget as tunable `maxTopLevelBlocks`/`maxPlainTextChars` parameters (not magic numbers), and Plan 71-03's live D-05 checkpoint is the point where actual fit is confirmed and, if needed, tuned per-template — no structural code change required either way.

2. **Should `RenderEmailHtml`'s truncation live inside `MarkdownService` (Domain layer) or as logic in the 3 Razor components / their Hangfire job callers?**
   - What we know: `QuestUrl` (needed for the read-more link) is already available at all 3 Razor component call sites as an existing parameter.
   - What's unclear: Whether the planner prefers keeping all Markdown-related logic centralized in `IMarkdownService` (this research's recommendation, for testability and to match the existing `RenderToHtml`/`ExtractPlainText` pattern) versus keeping `MarkdownService` narrowly scoped to "Markdown → HTML" and placing truncation as a separate, email-specific concern elsewhere.
   - Recommendation: Keep it in `IMarkdownService` as a new method (`RenderEmailHtml`) — this matches the file's existing role as "the one place that knows how to safely render Markdown for a given target," keeps the 3 Razor components' call sites minimal (single method call, matching today's single-line call), and makes the new logic trivially unit-testable in `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` alongside the existing tests for the same class.
   - **RESOLVED:** Plan 71-01 implements `RenderEmailHtml` as a new method on `IMarkdownService`/`MarkdownService`, per the recommendation — centralized, testable, minimal call-site changes in the 3 Razor components (Plan 71-02).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| SMTP relay (Postfix/Resend, existing) | D-05 real test-email send | ✓ (existing, per user memory: Postfix on the Linux deployment host) | — | None needed — no new send infrastructure per D-05 |
| Real Outlook desktop client | D-05 EMAILMD-02 verification (Outlook half) | Unconfirmed — per `71-CONTEXT.md`, operator has ready access to only one of Outlook desktop / Gmail webmail, not necessarily both | — | Plan's human-verify checkpoint must explicitly surface this gap rather than assume simultaneous access; do not silently skip the Outlook half of EMAILMD-02 |
| Real Gmail webmail account | D-05 EMAILMD-02 verification (Gmail half) | Unconfirmed — same constraint as above | — | Same as above |
| AngleSharp, HtmlSanitizer, Markdig (NuGet) | All new logic in this phase | ✓ | 0.17.1 / 9.0.892 / 1.3.2 | None needed |

**Missing dependencies with no fallback:**
- None that block the code-level implementation. The Outlook-desktop/Gmail-webmail dual-access gap for D-05 is a human-verification logistics issue, not a code-blocking dependency — the plan must surface it as an explicit checkpoint note ("confirm access to both clients before scheduling this verification, or split into two separate verification sessions") rather than silently assuming it.

**Missing dependencies with fallback:**
- None applicable.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit + FluentAssertions (existing, confirmed via `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`) [VERIFIED: codebase] |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EMAILMD-02 | Every Markdig-emittable block/inline element (h1-h6, ul/ol/li, blockquote, p, a, strong, em, hr) gets the exact inline `style=` string from `71-UI-SPEC.md` when rendered via `RenderEmailHtml` | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ Wave 0 — new test cases needed |
| EMAILMD-02 | Every `<li>` gets the MSO-conditional bullet-fallback comment prepended | unit | same as above | ❌ Wave 0 |
| EMAILMD-02 | `RenderEmailHtml` output for Web-irrelevant content (no truncation, no image) still strips `<img>` (regression check against existing `RenderToHtml_EmailTarget_StripsImage` behavior) | unit | same as above | ✅ existing test covers `RenderToHtml`; new test needed for `RenderEmailHtml` specifically |
| EMAILMD-03 | Content under the block/char budget is NOT truncated, no read-more link appended | unit | same as above | ❌ Wave 0 |
| EMAILMD-03 | Content over budget is truncated exactly at a complete block boundary — output remains well-formed HTML (parseable, no unclosed tags) and never cuts mid-`<li>`/mid-`<blockquote>`/mid-heading | unit | same as above | ❌ Wave 0 |
| EMAILMD-03 | Truncated output ends with the exact D-04 copy `…continue reading on the quest board` as a link to the passed `readMoreUrl` | unit | same as above | ❌ Wave 0 |
| EMAILMD-02, EMAILMD-03 | Real Outlook desktop and real Gmail webmail render the sent test email correctly (visible bullets, intact styling, no clipping, working read-more link) | manual (D-05) | N/A — human-verify checkpoint, explicitly not automatable per D-05 | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`, AND D-05's live Outlook/Gmail human verification, both required to close EMAILMD-02/EMAILMD-03.

### Wave 0 Gaps
- [ ] New test cases in `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` covering `RenderEmailHtml`'s styling injection, MSO fallback injection, and truncation behavior (see table above) — no new test file needed, extend the existing one to match its established pattern.
- [ ] No new framework install needed — xUnit + FluentAssertions already configured and in use in this exact test file.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Not touched — this phase has no auth surface |
| V3 Session Management | No | Not touched |
| V4 Access Control | No | Not touched — `EmailPreviewController`'s existing `[Authorize(Policy = "AdminOnly")]` gate is unchanged if a `WaitlistPromoted()` preview action is added |
| V5 Input Validation | Yes (unchanged, reaffirmed) | Existing `EmailSanitizer` (Ganss.Xss `HtmlSanitizer`) continues to be the sole sanitization boundary for user-supplied Markdown → HTML on the Email target; this phase's new logic operates strictly downstream of that boundary and injects only compile-time-constant style strings and comment markup — no new user-controlled data enters the HTML at any new point |
| V6 Cryptography | No | Not touched |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Reintroducing an XSS vector by exposing `style` attribute injection to user-controlled content | Tampering / Elevation of Privilege | This phase's recommended architecture (Pattern 1) structurally prevents this: styles are always hard-coded C# string constants keyed by tag name, never built from any part of the sanitized/user-derived HTML content. The plan/executor must preserve this invariant — never build a style string using any substring of `markdown`, `rawHtml`, or the sanitized output. |
| HTML-comment injection via the MSO bullet-fallback mechanism being fed user data | Tampering | Not a risk in the recommended design — the comment content (`[if mso]>&#8226;&nbsp;<![endif]`) is a single hard-coded literal, never parameterized by any request-derived value. |
| Truncation logic accidentally serializing a partially-sanitized or partially-parsed element (e.g. if AngleSharp's re-serialization of an already-sanitized element somehow reintroduces disallowed markup) | Tampering | Low residual risk — AngleSharp's `OuterHtml` serialization operates on the already-sanitized DOM tree (post `EmailSanitizer.Sanitize()`), it does not re-parse or re-execute any user input; there is no code path in the recommended design where truncation logic re-introduces content that wasn't already present and already sanitized. |

## Sources

### Primary (HIGH confidence)
- Direct codebase reads (VERIFIED, this session): `QuestBoard.Domain/Services/MarkdownService.cs`, `QuestBoard.Domain/Interfaces/IMarkdownService.cs`, `QuestBoard.Domain/QuestBoard.Domain.csproj`, `QuestBoard.Service/Components/Emails/{QuestFinalized,SessionReminder,WaitlistPromoted,_EmailLayout}.razor`, `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs`, `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs`, `QuestBoard.Service/Jobs/{QuestFinalizedEmailJob,QuestWaitlistPromotedEmailJob,SessionReminderJob}.cs`, `QuestBoard.Domain/Interfaces/IEmailService.cs`, `QuestBoard.Domain/Interfaces/IEmailRenderService.cs`, `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`, `QuestBoard.Service/wwwroot/css/markdown-content.css`, local NuGet package cache (`~/.nuget/packages/htmlsanitizer/9.0.892`, `~/.nuget/packages/anglesharp.css/0.17.0`).
- `.planning/phases/71-email-safety-hardening/71-CONTEXT.md`, `71-UI-SPEC.md` — locked decisions and design contract for this phase.

### Secondary (MEDIUM confidence)
- [NuGet Gallery | HtmlSanitizer 9.0.892](https://www.nuget.org/packages/HtmlSanitizer/) — confirmed current stable version and its `AngleSharp.Css 0.17.0` dependency pin.
- [The Ultimate Guide to Bullet Points in HTML Email - Litmus](https://www.litmus.com/blog/the-ultimate-guide-to-bulleted-lists-in-html-email) — Outlook bullet-visibility technique, cross-checked against a second source.
- [MSO styles | good-email-code](https://www.goodemailcode.com/email-enhancements/mso-styles.html) — MSO-specific CSS property scope (Outlook desktop/Windows Mail only, not Mac/mobile/webmail Outlook).
- [Style gets removed from headers · mganss/HtmlSanitizer · Discussion #575](https://github.com/mganss/HtmlSanitizer/discussions/575) — real reported case of the whole-style-attribute-dropped defect, root cause confirmed by the library maintainer.

### Tertiary (LOW confidence)
- General Markdig `HtmlRenderer`/`ObjectRenderer` extensibility knowledge used to argue against Option A (custom renderer override) — not independently re-verified against Markdig 1.3.2's exact API this session (see Assumptions Log A4).
- Exact AngleSharp 0.17.1 method names (`CreateComment`, `QuerySelectorAll` overloads, `InsertBefore`) used in code examples — standard DOM API, not individually re-verified against this exact package version this session (see Assumptions Log A2).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, all versions read directly from the project's own `.csproj` and local NuGet cache.
- Architecture (style injection / truncation mechanism): HIGH for the recommended approach's safety argument (grounded in direct code reads of the sanitizer's actual configuration); MEDIUM for exact AngleSharp method signatures (standard DOM API, not line-by-line verified against 0.17.1).
- Pitfalls (Outlook/Gmail rendering behavior): MEDIUM — cross-checked across multiple independent, reputable email-development sources, but not independently re-verified against a live Outlook/Gmail instance this session; that verification is D-05's explicit job.

**Research date:** 2026-07-10
**Valid until:** 30 days (stable, mature libraries; the one time-sensitive risk — whether `AngleSharp.Css 0.17.0` still drops style attributes on certain values — is moot for the recommended architecture, which never exercises that code path)
