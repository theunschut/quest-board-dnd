---
phase: 65-markdown-rendering-foundation
reviewed: 2026-07-09T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IMarkdownService.cs
  - QuestBoard.Domain/Services/MarkdownService.cs
  - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs
  - QuestBoard.Domain/QuestBoard.Domain.csproj
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 65: Code Review Report

**Reviewed:** 2026-07-09T00:00:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the new `IMarkdownService` / `MarkdownService` Markdig + HtmlSanitizer rendering pipeline, its DI registration, and its unit tests. The XSS-defense design is deliberate and well-reasoned (raw HTML disabled, extensions composed individually instead of via the generic-attribute-enabling bundle, dual sanitizer profiles, footnote `id` values proven attacker-uncontrolled).

To go beyond static reading, I built a throwaway console harness (outside the repo, in scratchpad) that pulled the exact same `Markdig 1.3.2` / `HtmlSanitizer 9.0.892` package versions and mirrored the pipeline/sanitizer configuration verbatim, then ran it against the 18 existing unit tests' payloads plus a battery of additional bypass attempts (uppercase/entity-encoded/tab-split `javascript:` schemes, `data:` URIs, `vbscript:`, angle-bracket link destinations, protocol-relative URLs) and a 200,000-iteration concurrent stress test of the shared singleton sanitizer/pipeline instances. All 18 existing tests pass, none of the additional bypass payloads escaped sanitization, and no thread-safety issues were found under concurrent load — the singleton registration and shared static sanitizer/pipeline instances are safe as implemented.

The same harness did surface one real, reproducible robustness gap (see WR-01 below): `Markdown.ToHtml` throws an uncaught exception for markdown with realistic (not extreme) nesting depth, and `RenderToHtml` has no defensive handling for it. Two minor completeness gaps in the sanitizer's attribute allowlist are also noted (INFO).

## Warnings

### WR-01: RenderToHtml has no defensive handling for Markdig's "too deeply nested" exception, reachable with realistic user input

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:88` (also affects the documented contract in `QuestBoard.Domain/Interfaces/IMarkdownService.cs:12-16`)

**Issue:** `Markdown.ToHtml(markdown, Pipeline)` is called with no try/catch. Markdig has its own self-protective guard against pathologically nested input, and it throws a plain `System.ArgumentException("Markdown elements in the input are too deeply nested - depth limit exceeded...")` when that guard trips. This is not a hypothetical edge case — I reproduced it directly against the exact pipeline configuration used by `MarkdownService`:

- ~200 repetitions of `"> "` (nested blockquote markers) followed by any text reliably throws.
- ~300 leading/trailing `*` characters around text (nested emphasis) reliably throws.

Both are trivial to produce, either deliberately (a user pastes `>>>>>>>>>>...` a few hundred times into a quest description) or semi-accidentally (a heavily block-quoted forwarded email/chat thread pasted into a text field). `RenderToHtml`'s XML doc only documents the null/empty/whitespace-input fallback (`string.Empty`) — it says nothing about this exception, so any future caller has no way to know they need to catch it. Because this service exists specifically to safely turn untrusted, user-authored markdown into HTML, and its whole design intent (per the extensive comments in this file) is defending against adversarial input, this particular gap runs counter to that intent. If the raw markdown this throws on is ever persisted (e.g., a quest description), every future render of that record — by every viewer, on every page load — would throw again until the stored content is manually edited.

Note: this does **not** crash the host process (Kestrel/the app domain survives; ASP.NET Core's exception-handling middleware will turn this into a request-level failure), which is why this is filed as a Warning rather than Critical — but it is a real, easily-triggered, undocumented, and currently untested failure mode in a service designed for resilience against untrusted input.

**Fix:**
```csharp
public string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web)
{
    if (string.IsNullOrWhiteSpace(markdown))
    {
        return string.Empty;
    }

    string rawHtml;
    try
    {
        rawHtml = Markdown.ToHtml(markdown, Pipeline);
    }
    catch (ArgumentException)
    {
        // Markdig's own nesting-depth guard tripped on pathological input (e.g. hundreds of
        // nested blockquote/emphasis markers). Fail safe instead of throwing into the caller.
        return System.Net.WebUtility.HtmlEncode(markdown);
    }

    return target == MarkdownRenderTarget.Email
        ? EmailSanitizer.Sanitize(rawHtml)
        : WebSanitizer.Sanitize(rawHtml);
}
```
Also update the `RenderToHtml` XML doc on `IMarkdownService` to state the guaranteed fallback behavior for pathologically-nested input, and add a unit test that pins this behavior down (the existing 18 tests do not cover it at all).

## Info

### IN-01: `MarkdownRenderTarget` enum breaks the established `Enums/` folder convention

**File:** `QuestBoard.Domain/Interfaces/IMarkdownService.cs:8`

**Issue:** Every other domain enum in this codebase lives in its own file under `QuestBoard.Domain/Enums/` (`GroupRole.cs`, `Role.cs`, `VoteType.cs`, `ItemRarity.cs`, `ItemStatus.cs`, `ItemType.cs`, `CharacterRole.cs`, `CharacterStatus.cs`, `DndClass.cs`, `SignupRole.cs`, `TransactionType.cs`, `BoardType.cs` — 12 precedents). `MarkdownRenderTarget` is instead declared inline at the top of `IMarkdownService.cs`, inside `Interfaces/`. This doesn't cause a bug, but it breaks discoverability for anyone browsing `Enums/` for the full set of domain enums, and is inconsistent with every other enum in the project.

**Fix:** Move the enum to `QuestBoard.Domain/Enums/MarkdownRenderTarget.cs`, matching the existing pattern (`QuestBoard.Domain.Enums` namespace, `using QuestBoard.Domain.Enums;` added to the two files that reference it).

### IN-02: Sanitizer's attribute allowlist silently discards two supported Markdig formatting features

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:40-49` (`AllowedAttributes`)

**Issue:** Verified directly against the pipeline: two syntaxes that the enabled extensions parse successfully end up visually broken after sanitization, with no test coverage or documented limitation:

- An ordered list with a custom start number (`5. five`) is rendered by Markdig as `<ol start="5">`, but `start` is not in `AllowedAttributes`, so the sanitizer strips it and the list silently renumbers from 1.
- Pipe-table column alignment (`:--`, `--:`, `:-:`, part of `UsePipeTables()`) is rendered as `style="text-align: left|right|center;"` on `<th>`/`<td>`, but `style` is not in `AllowedAttributes`, so alignment is silently dropped and columns render left-aligned regardless of the author's markup.

Neither is a security issue — both are self-contained to the intended use of the enabled extensions — but both are surprising, silent losses of authored intent that the current test suite does not pin down either way (accepting or rejecting).

**Fix:** Either explicitly allow `start` (safe — Markdig only ever emits an integer here) and the fixed three-value `text-align` `style` output (e.g. validate/allowlist the exact value rather than accepting arbitrary `style` content), or, if the current lossy behavior is intentional (e.g. to avoid any `style` attribute surface at all), add tests that lock in "start/alignment is not preserved" as expected behavior so a future contributor doesn't mistake it for a bug.

---

_Reviewed: 2026-07-09T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
