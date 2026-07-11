---
phase: 66-quest-description-editor-rendering-proof-of-concept
fixed_at: 2026-07-09T21:50:00Z
review_path: .planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 66: Code Review Fix Report

**Fixed at:** 2026-07-09T21:50:00Z
**Source review:** .planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 6 (fix_scope: critical_warning — CR-01, WR-01 through WR-05; IN-01 excluded)
- Fixed: 6
- Skipped: 0

## Fixed Issues

### CR-01: QuestFinalized email wraps multi-block Markdown HTML in a single `<p>`, producing invalid nested-block HTML that silently drops the description's styling

**Files modified:** `QuestBoard.Service/Components/Emails/QuestFinalized.razor`, `QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs`
**Commit:** ed1c696a
**Applied fix:** Replaced the outer `<p style="...italic...">` wrapper around the rendered Description HTML with a block-safe `<div>` carrying the same styling, so multi-paragraph/heading/list Descriptions no longer implicitly close the styled wrapper. Added a regression test (`QuestFinalized_MultiBlockMarkdownDescription_KeepsStyledWrapperIntact`) that feeds a two-paragraph + heading + list Description and asserts the styled wrapper is not emptied/self-closed and all block content survives. Verified via `dotnet build` (0 errors) and `dotnet test --filter QuestFinalizedMarkdownRenderTests` (2/2 passed).

### WR-01: `MarkdownController.Preview` throws an unhandled `NullReferenceException` on an empty/malformed request body

**Files modified:** `QuestBoard.Service/Controllers/MarkdownController.cs`, `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs`
**Commit:** 812b38bc
**Applied fix:** Changed the `Preview` action's parameter to `PreviewRequest?` and switched to `request?.Markdown` so a null-bound request body no longer throws. Added a regression test (`Preview_WithEmptyBody_DoesNotServerError`) that POSTs an empty JSON body and asserts a non-5xx response. Verified via `dotnet build` (0 errors) and `dotnet test --filter MarkdownControllerIntegrationTests` (5/5 passed).

### WR-02: Removing `.quest-description-mobile` in favor of `.markdown-content` drops the Quest Description's parchment text color on the mobile Details page

**Files modified:** `QuestBoard.Service/wwwroot/css/quests.mobile.css`
**Commit:** 68e01210
**Applied fix:** Added a scoped `.quest-header-card-mobile .markdown-content` rule (parchment gold `#F4E4BC` with the same dark text-shadow used by the pre-existing `.quest-description-mobile` rule) so rendered Markdown Description text regains contrast against the dark, semi-transparent glass card. Verified via Tier 1 re-read (rule present, braces balanced, no stray selectors) — no CSS syntax checker available in this environment (Tier 3 fallback).

### WR-03: `ExtractPlainText` silently returns empty string instead of the fallback-encoded text when `RenderToHtml`'s pathological-nesting guard trips

**Files modified:** `QuestBoard.Domain/Services/MarkdownService.cs`, `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`
**Commit:** 89ef65d8
**Applied fix:** Added a fallback branch in `ExtractPlainText`: when the parsed `Body.Children` element collection is empty (the case when `RenderToHtml`'s nesting-depth guard returns bare HTML-encoded text with no wrapping element), fall back to `WhitespaceRun.Replace(document.Body.TextContent, " ").Trim()` instead of returning `""`. Added a regression test (`ExtractPlainText_DeeplyNestedInput_ReturnsFallbackEncodedTextInsteadOfEmpty`) reusing the existing 200-level nested-blockquote payload. Verified via `dotnet build` (0 errors) and `dotnet test --filter MarkdownServiceTests` (26/26 passed).

### WR-04: `MarkdownEditorViewModel.Required` only renders a visual asterisk — it is never applied to the `<textarea>` as an HTML5 `required` attribute

**Files modified:** `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml`
**Commit:** 88dffaeb
**Applied fix:** Added `required="@Model.Required"` to the `<textarea>`. Adapted from the review's literal `@(Model.Required ? "required" : "")` suggestion: that ternary produces a `string` result, so when `Required` is false it would still emit a stray `required=""` attribute (which HTML5 treats as present/required, since `required` is a presence-only boolean attribute) — the opposite of the intended behavior. Using a bare `@Model.Required` (type `bool`) as the entire attribute value instead relies on ASP.NET Core Razor's built-in conditional-attribute-rendering for boolean expressions: `true` renders `required="required"`, `false` omits the attribute entirely. Verified via `dotnet build` (0 errors, Razor views compile as part of build) and Tier 1 re-read.

### WR-05: `markdown-editor.js`'s `previewRender` has no request sequencing — a slower in-flight preview response can overwrite a newer one

**Files modified:** `QuestBoard.Service/wwwroot/js/markdown-editor.js`
**Commit:** 0a8589ca
**Applied fix:** Added a monotonically increasing `latestRequestId` counter and guarded both the success and error handlers so only the response matching the most recently issued request is written to `previewElement.innerHTML`. Adapted from the review's suggestion by scoping the counter inside `initMarkdownEditor`'s closure (per editor instance) rather than a single module-level global, since the `DOMContentLoaded` loop initializes one `EasyMDE` instance per `.markdown-editor-textarea` on the page — a shared global counter would let requests from one editor instance suppress/interfere with another's. Verified via `node -c` (syntax OK) and Tier 1 re-read.

## Skipped Issues

None — all in-scope findings were fixed.

---

_Fixed: 2026-07-09T21:50:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
