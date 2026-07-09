---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 03
subsystem: email
tags: [markdown, blazor, razor-components, email-rendering, markdig, html-sanitizer]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: IMarkdownService.RenderToHtml(markdown, target) with Web/Email sanitizer profiles, registered AddSingleton
provides:
  - QuestFinalized.razor email component renders Quest Description Markdown as sanitized HTML instead of HTML-encoded raw text
  - First @inject usage in QuestBoard.Service/Components/Emails/, proving singleton services resolve correctly in the Hangfire-job-scoped HtmlRenderer
  - Integration test proving the write-once-render-in-email half of the "one core, two adapters" architecture
affects: [67-quest-rewards-recap-and-remaining-emails]

# Tech tracking
tech-stack:
  added: []
  patterns: ["@inject IMarkdownService in a Blazor email component, resolved via RazorEmailRenderService's HtmlRenderer built from the real app IServiceProvider"]

key-files:
  created: [QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs]
  modified: [QuestBoard.Service/Components/Emails/QuestFinalized.razor]

key-decisions:
  - "Scoped the 'images stripped' test assertion to the test-supplied image URL/alt text rather than a blanket NotContain(\"<img\") check, since the email layout's static Wax Seal image is legitimate template markup unrelated to Description markdown."

patterns-established:
  - "Email Razor components resolve Domain singletons via @inject even outside HTTP request scope, because RazorEmailRenderService.RenderAsync builds its HtmlRenderer from the app's real IServiceProvider."

requirements-completed: [QUESTMD-01]

# Metrics
duration: 15min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 03: Quest Finalized Email Markdown Rendering Summary

**Wired the shared `IMarkdownService` into `QuestFinalized.razor` so the Quest Finalized email renders Description Markdown as formatted, sanitized HTML with embedded images stripped — the first `@inject` anywhere in `Components/Emails/`.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-07-09T15:09:55Z
- **Tasks:** 2/2 completed
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- Added a failing integration test (`QuestFinalizedMarkdownRenderTests`) proving the email previously HTML-encoded raw Markdown instead of rendering it
- Wired `@inject IMarkdownService MarkdownService` + `RenderToHtml(QuestDescription, MarkdownRenderTarget.Email)` into `QuestFinalized.razor`, replacing the bare `@QuestDescription` interpolation
- Confirmed the Phase-65 email sanitizer profile strips embedded `<img>` tags and renders bold/lists/headings as real HTML in the finalized-quest confirmation email
- No caller or parameter contract changed (`EmailPreviewController.QuestFinalized()` and `QuestFinalizedEmailJob` continue passing `QuestDescription` as a plain string)

## Task Commits

Each task was committed atomically:

1. **Task 1: Failing integration test — Quest Finalized email renders Description markdown as HTML, images stripped (RED)** - `308b5414` (test)
2. **Task 2: Wire IMarkdownService into QuestFinalized.razor (GREEN)** - `719fad38` (feat)

_TDD cycle: RED (`308b5414`) → GREEN (`719fad38`). No REFACTOR commit needed — the GREEN change was already minimal (3-line diff)._

## Files Created/Modified
- `QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs` - New integration test resolving `IEmailRenderService` from a DI scope, rendering `QuestFinalized` with a Markdown Description (bold + embedded image), asserting formatted `<strong>` HTML present, raw `**` syntax gone, and the embedded image URL/alt stripped
- `QuestBoard.Service/Components/Emails/QuestFinalized.razor` - Added `@using QuestBoard.Domain.Interfaces` + `@inject IMarkdownService MarkdownService`; replaced `@QuestDescription` with `@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))` inside the description `<p>`, keeping the existing inline styles unchanged

## Decisions Made
- The test's "images stripped" assertion checks for the specific test-supplied image URL (`http://example.com/x.png`) and alt text (`alt="logo"`) rather than a blanket `NotContain("<img")`, because the email layout template itself renders a legitimate static `<img>` (the Wax Seal decoration) that is unrelated to and unaffected by the Description-markdown sanitizer. A blanket assertion would have produced a false failure even after the fix.

## Deviations from Plan

None — plan executed exactly as written. The test-scoping decision above was a refinement made during Task 1 authoring (before the RED run), not a fix to a discovered bug.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The Blazor-email adapter pattern (`@inject IMarkdownService` + `RenderToHtml(..., MarkdownRenderTarget.Email)`) is proven and ready to be copied verbatim into `SessionReminder.razor` and `WaitlistPromoted.razor` in Phase 67 (EMAILMD-01 completion)
- No blockers or concerns

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*
