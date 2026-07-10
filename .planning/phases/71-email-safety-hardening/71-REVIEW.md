---
phase: 71-email-safety-hardening
reviewed: 2026-07-10T00:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IMarkdownService.cs
  - QuestBoard.Domain/Services/MarkdownService.cs
  - QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs
  - QuestBoard.IntegrationTests/Emails/SessionReminderMarkdownRenderTests.cs
  - QuestBoard.IntegrationTests/Emails/WaitlistPromotedMarkdownRenderTests.cs
  - QuestBoard.Service/Components/Emails/QuestFinalized.razor
  - QuestBoard.Service/Components/Emails/SessionReminder.razor
  - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
  - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
  - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: fixed
---

# Phase 71: Code Review Report

**Reviewed:** 2026-07-10T00:00:00Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** fixed (both warnings resolved same-session, see Fixes Applied below; 3 of 4 info items remain, logged not blocking)

## Fixes Applied

Both warnings were fixed directly by the orchestrator (not a spawned gsd-code-fixer) immediately after this review, since they were real correctness bugs in code touched moments earlier while closing out this phase:

- **WR-01** (first block alone exceeds char budget → empty-looking email): `TruncateAtBlockBoundary` restructured to always keep at least the first block unconditionally, computing `truncated` as `kept.Count < allBlocks.Count` rather than tracking a flag through early-break branches. Commit `7cec5d04`.
- **WR-02** (pathological-nesting fallback bypasses truncation entirely): the `!document.Body!.Children.Any()` branch in `RenderEmailHtml` now applies `maxPlainTextChars` as a length cap on the encoded fallback string, appending the read-more link when truncated. Commit `7cec5d04`.
- Two new regression tests added (`RenderEmailHtml_OnlyBlockAloneExceedsCharBudget_IsKeptInFullWithNoReadMoreLink`, `RenderEmailHtml_FirstBlockAloneExceedsCharBudget_IsStillKeptAndLaterBlocksTruncated`, `RenderEmailHtml_PathologicalNestingFallback_RespectsCharBudget`) in `MarkdownServiceTests.cs`.
- IN-02 (stale "5 blocks/650 chars" comment in `EmailPreviewController.cs`) also fixed in the same commit, since it was directly related and trivial.
- IN-01 (dead ternary arm) was incidentally resolved by the WR-01 restructure — the `kept.Count == 0` branch no longer uses a confusing ternary.
- IN-03 (footnote/`<dl>` blocks consume budget but get no inline styling) and IN-04 (inconsistent null-forgiving operator) remain open, logged as non-blocking.

Full suite re-verified green after fixes: 302 unit + 396 integration tests passing.

## Summary

Reviewed the Markdown-to-email-HTML rendering pipeline (`MarkdownService.RenderEmailHtml`/`RenderToHtml`/`ExtractPlainText`), the three quest email templates that consume it, the admin preview controller, and both unit/integration test suites added for this phase.

The sanitization design is sound: two independently-constructed `HtmlSanitizer` instances (Web vs. Email), `DisableHtml()` on the Markdig pipeline to prevent raw-HTML injection, a scheme allowlist that excludes `javascript:`, and a hard-coded (never markdown-derived) style-attribute table applied strictly after sanitization. The XSS-payload theory tests in `MarkdownServiceTests` exercise the payloads I'd otherwise reach for (script tags, `onerror`, `javascript:` autolinks/hrefs, generic-attribute injection) and the sanitizer allowlist backs them up. I did not find an XSS or injection bypass.

The defects found are all in the block-truncation logic added for the 3 quest email templates (`RenderEmailHtml`'s `maxTopLevelBlocks`/`maxPlainTextChars` budget): one edge case silently drops all visible content, and one code path bypasses the truncation budget entirely. Neither is a security issue, but both undermine the feature's stated purpose (fit a Description into a fixed-height email card without breaking layout). The rest are quality/consistency nits.

## Warnings

### WR-01: A single over-budget block renders zero preview content, only the "read more" link

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:252-277`
**Issue:** `TruncateAtBlockBoundary` only ever keeps *whole* blocks. If the very first top-level block's own text already exceeds `maxPlainTextChars` (or `maxTopLevelBlocks` is exhausted before anything is kept — not currently reachable via the fixed call sites, but true for the first-block-too-long case), the loop breaks on the first iteration with `kept.Count == 0` and `truncated == true`, and `RenderEmailHtml` returns *only* `BuildReadMoreParagraph` — no Description text at all, just the "…continue reading on the quest board" link.

  With the production defaults (`maxPlainTextChars: 400` for `QuestFinalized`/`WaitlistPromoted`, `350` for `SessionReminder`), any Quest Description written as a single paragraph (no blank line — very plausible for a DM who doesn't format with Markdown) that happens to be longer than ~400 characters will render a completely empty card body in the email, which looks broken rather than "truncated". This is a realistic, non-adversarial input, not a pathological edge case.

**Fix:** When `kept.Count == 0` after the first block is rejected for exceeding the char budget, keep the first block anyway and let CSS `overflow:hidden`/`max-height` on the wrapping `<div>` clip it, or clip its `TextContent` to `maxPlainTextChars` before re-wrapping. E.g.:
```csharp
if (kept.Count == 0)
{
    // Never drop 100% of the content — always show at least the first block, even if it
    // alone exceeds the char budget; the wrapping <div>'s max-height/overflow:hidden clips
    // any remaining overflow visually.
    kept.Add(document.Body!.Children.First());
    truncated = true;
}
```

### WR-02: Pathological-nesting fallback in `RenderEmailHtml` completely bypasses the truncation budget

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:236-245`
**Issue:** When `RenderToHtml` hits Markdig's nesting-depth guard, it falls back to returning the whole input HTML-encoded with no wrapping element (see `RenderToHtml:146-152`). `RenderEmailHtml` detects this (`!document.Body!.Children.Any()`) and returns `sanitizedHtml` verbatim — with **no** `maxTopLevelBlocks`/`maxPlainTextChars` enforcement and no "read more" link appended. Unlike the normal path, there is no length cap on this branch at all: a Quest Description containing e.g. 200+ nested `>`/`*` characters plus arbitrary trailing text (something a DM could produce completely by accident, not just as an attack) will dump the *entire* raw text into the fixed-height email card, unbounded by any of the budgets the feature exists to enforce. `overflow:hidden`/`max-height` on the wrapping `<div>` provides some client-side mitigation, but is well known to be unreliable in Outlook (already called out elsewhere in this file re: bullet rendering), and doesn't prevent an oversized message.
**Fix:** Apply the same plain-text truncation used elsewhere (or reuse the character budget) to this fallback branch instead of returning it unbounded, e.g.:
```csharp
if (!document.Body!.Children.Any())
{
    var text = sanitizedHtml.Length > maxPlainTextChars
        ? sanitizedHtml[..maxPlainTextChars]
        : sanitizedHtml;
    return sanitizedHtml.Length > maxPlainTextChars
        ? text + BuildReadMoreParagraph(document, readMoreUrl)
        : sanitizedHtml;
}
```

## Info

### IN-01: Stale truncation-budget numbers in comment

**File:** `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs:15-17`
**Issue:** The comment on `SampleMarkdownDescription` says it's sized to exceed "the default truncation budget (5 top-level blocks / 650 plain-text characters)". The actual current defaults, per `MarkdownService.RenderEmailHtml` (`maxTopLevelBlocks = 3, maxPlainTextChars = 400`), were tightened from 5/650 to 3/400 during this same phase (see `MarkdownService.cs:196-203`, which explicitly documents "Tightened from 5 to fit..."). The comment here was not updated to match and now describes numbers that no longer exist anywhere in the codebase.
**Fix:** Update the comment to read "the default truncation budget (3 top-level blocks / 400 plain-text characters)".

### IN-02: Dead branch in `TruncateAtBlockBoundary`

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:274-277`
**Issue:** `if (kept.Count == 0) { return truncated ? BuildReadMoreParagraph(...) : string.Empty; }` — the `: string.Empty` arm is unreachable. `TruncateAtBlockBoundary` is only invoked (from `RenderEmailHtml:245`) after the caller has already verified `document.Body!.Children.Any()`; given at least one child, the loop's first iteration either adds it to `kept` or breaks with `truncated = true`, so `kept.Count == 0 && truncated == false` can never happen with the current single call site.
**Fix:** Either remove the dead ternary arm (`Debug.Assert(truncated)` before returning `BuildReadMoreParagraph`), or, if this method is meant to be safely callable in isolation later, add an explicit early-return for an empty `document.Body!.Children` at the top of the method so the invariant is self-documented rather than implied by the caller.

### IN-03: Footnote `<div>`/other allowed-but-unstyled containers consume block budget with no inline styling

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:96-112`, `218-235`
**Issue:** `BaseAllowedTags` allows `div` (the Markdig footnote-group wrapper) and `dl`/`dt`/`dd`, but `EmailBlockStyles` only has entries for `h1-h6, p, ul, ol, blockquote, a, strong, em, hr`. If a Quest Description uses footnotes or a definition list, that top-level block still counts against `maxTopLevelBlocks`/`maxPlainTextChars` in `TruncateAtBlockBoundary`, but is rendered with zero inline `style=`, so it depends entirely on the email client's default/inherited styling — inconsistent with every other block type in the same card, which are all explicitly typography-styled for email-client portability (per the design-contract comment at the top of the style-constant block).
**Fix:** Either add explicit styles for `div`/`dl`/`dt`/`dd` to `EmailBlockStyles` (or a parallel table), or explicitly document that footnotes/definition-lists in Quest Descriptions render unstyled in email as a known, accepted gap.

### IN-04: Inconsistent null-forgiving operator usage on `document.Body`

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:175, 185`
**Issue:** `ExtractPlainText` uses `document.Body!.Children` (line 175, null-forgiving) but then `document.Body.TextContent` without `!` two lines later on the fallback path (line 185). `QuestBoard.Domain.csproj` has `<Nullable>enable</Nullable>`, and AngleSharp's `IDocument.Body` is a nullable property (`IHtmlElement?`), so line 185 is a possible-null dereference the compiler should flag (CS8602) unless suppressed elsewhere — inconsistent with every other `document.Body!` access in this file.
**Fix:** Use `document.Body!.TextContent` for consistency with the rest of the file, or add a null-check with a clear fallback if a genuinely-null `Body` should be handled distinctly from the "zero children" case.

---

_Reviewed: 2026-07-10T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
