---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 01
subsystem: markdown-rendering
tags: [markdig, anglesharp, html-sanitizer, mvc, razor, unit-tests]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: "IMarkdownService.RenderToHtml(string?, MarkdownRenderTarget) with Markdig pipeline + HtmlSanitizer (Web/Email targets)"
provides:
  - "IMarkdownService.ExtractPlainText(string?) — AngleSharp-backed plain-text extraction reusing RenderToHtml, word-boundary-safe across block elements"
  - "Html.Markdown() Razor HtmlHelper adapter — sanitized HTML wrapped in .markdown-content, resolves IMarkdownService per-request"
  - "AngleSharp 0.17.1 as an explicit direct QuestBoard.Domain dependency"
affects: [66-quest-description-editor-rendering-read-views, 67-quest-rewards-recap-remaining-emails]

# Tech tracking
tech-stack:
  added: ["AngleSharp 0.17.1 (direct PackageReference; was already transitive via HtmlSanitizer)"]
  patterns: ["Extend an existing service interface + implementation rather than adding a parallel service", "Read-side view helper resolves a singleton via RequestServices.GetRequiredService instead of constructor injection into the view"]

key-files:
  created:
    - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
  modified:
    - QuestBoard.Domain/Interfaces/IMarkdownService.cs
    - QuestBoard.Domain/Services/MarkdownService.cs
    - QuestBoard.Domain/QuestBoard.Domain.csproj
    - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs

key-decisions:
  - "ExtractPlainText collapses whitespace runs (via a compiled Regex) after joining top-level body-child TextContent with spaces, because Markdig's own HTML formatting inserts literal newlines between sibling <li> elements that the plan's simple space-join example did not anticipate"
  - "Html.Markdown() ships without a MarkdownPlainText() sibling helper in this plan, per explicit plan instruction — the board card will inject IMarkdownService directly in plan 66-06 to compute plain text once for both length and display"

patterns-established:
  - "Plain-text extraction from rendered HTML never re-parses Markdown — it always calls RenderToHtml first, then AngleSharp-parses that HTML output, preserving the single-parser invariant (RENDER-02)"

requirements-completed: [QUESTMD-01, EDITOR-04]

# Metrics
duration: 6min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 01: Quest Description Server-Side Read Primitives Summary

**AngleSharp-backed `ExtractPlainText` plain-text extraction and a single `Html.Markdown()` Razor adapter, both built on the existing sanitized `RenderToHtml` pipeline with no second Markdown parser introduced**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-09T15:05:11Z
- **Completed:** 2026-07-09T15:10:36Z
- **Tasks:** 3
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- `IMarkdownService.ExtractPlainText(string?)` contract added and implemented via AngleSharp block iteration over `RenderToHtml`'s output, preserving word boundaries across block elements (heading + paragraph + list do not smash together)
- AngleSharp 0.17.1 promoted from transitive to an explicit direct `QuestBoard.Domain` dependency
- `Html.Markdown()` Razor HtmlHelper adapter added so views render sanitized Quest Description HTML via a single call, wrapped in a `.markdown-content` styling hook, without touching the sanitizer or Markdig directly
- Full TDD RED → GREEN cycle followed for `ExtractPlainText`; 5 new unit tests added, all passing; full 268-test suite green with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ExtractPlainText contract, throwing stub, AngleSharp reference, and failing unit tests (RED)** - `36ecce53` (test)
2. **Task 2: Implement ExtractPlainText via AngleSharp block iteration (GREEN)** - `8e540b78` (feat)
3. **Task 3: Add Html.Markdown() read-side HtmlHelper adapter** - `f9837048` (feat)

_Note: Task 1 is a RED-phase test commit per the plan's TDD structure for this feature; Task 2 is the corresponding GREEN implementation commit._

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IMarkdownService.cs` - Added `ExtractPlainText(string? markdown)` to the contract with a why-focused XML doc summary
- `QuestBoard.Domain/Services/MarkdownService.cs` - Implemented `ExtractPlainText` via `RenderToHtml` + `AngleSharp.Html.Parser.HtmlParser`, joining top-level `document.Body` children's trimmed `TextContent` with spaces, then collapsing whitespace runs
- `QuestBoard.Domain/QuestBoard.Domain.csproj` - Added explicit `<PackageReference Include="AngleSharp" Version="0.17.1" />`
- `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` - Added `ExtractPlainText_MultiBlockInput_PreservesWordBoundariesAcrossBlocks`, `ExtractPlainText_NullOrWhitespace_ReturnsEmpty` (theory, 3 cases), `ExtractPlainText_BoldParagraph_StripsMarkdownSyntaxAndHtmlTags`
- `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` (new) - `internal static class HtmlHelperExtensions` with `Markdown(this IHtmlHelper, string?)`, mirroring `ControllerExtensions.cs`'s conventions

## Decisions Made
- Collapsed whitespace runs with a compiled `Regex` after the block-join, because the plan's Pattern 3 example (`string.Join(" ", parts)` alone) left literal newlines in the output for list items — Markdig's HTML formatter inserts a newline text node between sibling `<li>` elements inside the rendered `<ul>`, and `TextContent` on the `<ul>` element (one of the top-level body children) includes that newline verbatim. This was caught by the plan's own required multi-block test case and fixed before GREEN.
- Did not add a `MarkdownPlainText()` HtmlHelper sibling method, per the plan's explicit instruction — that wiring is deferred to plan 66-06 where the board card injects `IMarkdownService` directly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Naive space-join left literal newlines between list items**
- **Found during:** Task 2 (implementing `ExtractPlainText`)
- **Issue:** The plan's Pattern 3 reference implementation (`string.Join(" ", parts)` over top-level body children only) passed the null/whitespace and bold-paragraph test cases but failed the multi-block word-boundary test: `ul.TextContent` for `<ul>\n<li>item one</li>\n<li>item two</li>\n</ul>` includes the newline text node between the two `<li>` elements verbatim, producing `"item one\nitem two"` instead of `"item one item two"`.
- **Fix:** Added a compiled `Regex` (`WhitespaceRun = new(@"\s+")`) applied to the joined result via `WhitespaceRun.Replace(joined, " ").Trim()`, collapsing any whitespace run (including newlines) into a single space.
- **Files modified:** `QuestBoard.Domain/Services/MarkdownService.cs`
- **Verification:** `ExtractPlainText_MultiBlockInput_PreservesWordBoundariesAcrossBlocks` now passes; full `QuestBoard.UnitTests` suite (268 tests) passes with no regressions.
- **Commit:** `8e540b78`

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Necessary for correctness — the plan's own required test case caught this before it could ship. No scope creep; the fix is scoped entirely to the whitespace-normalization step inside `ExtractPlainText`.

## Issues Encountered
None beyond the deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
`IMarkdownService.ExtractPlainText` and `Html.Markdown()` are both available and unit-verified. Plan 66-06 (read-view wiring) can now call `@Html.Markdown(...)` on Details/Manage views and inject `IMarkdownService.ExtractPlainText` directly for the board card, with no further server-side primitive work needed. No blockers identified.

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*

## Self-Check: PASSED

All created/modified files confirmed present on disk; all 4 task/docs commits (`36ecce53`, `8e540b78`, `f9837048`, `42b5466a`) confirmed present in git log.
