---
phase: 71-email-safety-hardening
plan: 02
subsystem: email
tags: [markdig, razor, email-html, outlook, dotnet]

# Dependency graph
requires:
  - phase: 71-email-safety-hardening (plan 01)
    provides: IMarkdownService.RenderEmailHtml(markdown, readMoreUrl, maxTopLevelBlocks, maxPlainTextChars)
provides:
  - "QuestFinalized/SessionReminder/WaitlistPromoted Razor templates wired to RenderEmailHtml, Outlook scroll-wrapper removed"
  - "Admin-only /EmailPreview/WaitlistPromoted browser preview action"
  - "Shared SampleMarkdownDescription structured-Markdown fixture reused by all 3 quest-email previews"
affects: [71-03-live-outlook-gmail-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Email preview actions key their RenderAsync<T> parameter dictionary by nameof(Component.Property), one action per template, mirrored 1:1 across QuestFinalized/SessionReminder/WaitlistPromoted"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Components/Emails/QuestFinalized.razor
    - QuestBoard.Service/Components/Emails/SessionReminder.razor
    - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
    - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
    - QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs
    - QuestBoard.IntegrationTests/Emails/SessionReminderMarkdownRenderTests.cs
    - QuestBoard.IntegrationTests/Emails/WaitlistPromotedMarkdownRenderTests.cs

key-decisions:
  - "The 6 pre-existing regression tests pinning the old RenderToHtml(..., Email) call site's bare-tag output were updated to regex-tolerant assertions rather than left broken — they were testing the exact call site this plan intentionally changed, and the new inline styles + MSO comment are correctness requirements of RenderEmailHtml, not incidental drift."
  - "SampleMarkdownDescription (heading + 2 paragraphs + unordered list + blockquote + ordered list, 6 top-level blocks) intentionally exceeds the 5-block/650-char truncation budget so the read-more link is visible in every quest-email preview, not just WaitlistPromoted's new one."

patterns-established: []

requirements-completed: [EMAILMD-02, EMAILMD-03]

coverage:
  - id: D1
    description: "QuestFinalized, SessionReminder, and WaitlistPromoted all call RenderEmailHtml(QuestDescription, QuestUrl) exactly once, no longer call RenderToHtml(..., Email), and the Outlook-incompatible overflow-y scroll wrapper is removed while the 840px poster card and SessionReminder's 'The Adventure:' label are preserved"
    requirement: "EMAILMD-03"
    verification:
      - kind: automated_ui
        ref: "grep -c 'RenderEmailHtml(QuestDescription, QuestUrl)' on all 3 templates (each =1); grep -c 'height:840px' on all 3 (each =1); grep -rn 'overflow-y' on all 3 (0 matches); grep -c 'The Adventure:' SessionReminder.razor (=1)"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs, SessionReminderMarkdownRenderTests.cs, WaitlistPromotedMarkdownRenderTests.cs (6 tests, updated for styled output)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Admin-only WaitlistPromoted() preview action exists, keyed by WaitlistPromoted's real parameters (including PlayerName), and all 3 quest-email preview actions render the shared structured-Markdown sample so styling/truncation is visible in the browser dev-loop; Index() links to the new preview"
    requirement: "EMAILMD-02"
    verification:
      - kind: automated_ui
        ref: "grep -c 'WaitlistPromoted' EmailPreviewController.cs (=11); grep -c 'SampleMarkdownDescription' EmailPreviewController.cs (=4); grep -c '[Authorize(Policy = \"AdminOnly\")]' EmailPreviewController.cs (=1)"
        status: pass
      - kind: other
        ref: "dotnet build QuestBoard.Service (0 warnings, 0 errors)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Real Outlook desktop and Gmail webmail actually render the newly-wired styled/truncated output correctly"
    verification: []
    human_judgment: true
    rationale: "Outlook's Word rendering engine and Gmail webmail cannot be simulated by any unit test, browser preview, or headless tool available to this project — this is explicitly Plan 71-03's blocking human-verification checkpoint (D-05), not this plan's scope."

duration: 12min
completed: 2026-07-10
status: complete
---

# Phase 71 Plan 02: Quest Email Template Wiring Summary

**All 3 quest email templates (QuestFinalized, SessionReminder, WaitlistPromoted) now render Quest Description through RenderEmailHtml with the Outlook-incompatible scroll wrapper removed, plus a new Admin-only WaitlistPromoted browser preview with a structured-Markdown dev-loop fixture.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-10T22:03:04+02:00 (base commit)
- **Completed:** 2026-07-10T22:12:06+02:00
- **Tasks:** 2
- **Files modified:** 7 (4 planned + 3 pre-existing regression tests updated as a same-scope deviation)

## Accomplishments
- `QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor` each swap their Row 3 Description call from `RenderToHtml(QuestDescription, MarkdownRenderTarget.Email)` to `RenderEmailHtml(QuestDescription, QuestUrl)`, reusing the existing `QuestUrl` parameter as the read-more target — no new parameter plumbing, no Hangfire job changes
- The inner `overflow-y:auto` scroll-wrapper `<div>` is removed from all 3 templates; the outer `<td style="height:840px;overflow:hidden;">` poster card is byte-for-byte unchanged (D-01), and `SessionReminder`'s "The Adventure:" label paragraph is preserved above the Description div
- The stale Row 3 HTML comment ("fills remaining vertical space, scrolls if too long") is updated to describe the new truncate-with-read-more behavior, with no phase/requirement IDs embedded per CLAUDE.md
- New Admin-only `EmailPreviewController.WaitlistPromoted()` `[HttpGet]` action mirrors `SessionReminder()`, keyed by WaitlistPromoted's actual parameters (`QuestTitle`, `DmName`, `QuestDate`, `QuestDescription`, `PlayerName`, `QuestUrl`, `ChallengeRating`, `AppUrl`)
- New `SampleMarkdownDescription` const (heading, two paragraphs, unordered list, blockquote, ordered list — 6 top-level blocks, well over the 650-char plain-text budget) shared by `QuestFinalized()`, `SessionReminder()`, and the new `WaitlistPromoted()` preview actions so all 3 previews now visibly exercise styled headings/lists/blockquotes and truncation with a read-more link
- `Index()` gained a "Waitlist Promoted" link to `/EmailPreview/WaitlistPromoted`

## Task Commits

Each task was committed atomically:

1. **Task 1: Swap the 3 templates to RenderEmailHtml and remove the scroll wrapper** - `dd11ddf7` (feat)
2. **Task 2: Add WaitlistPromoted preview action + structured-Markdown dev-loop samples** - `033d3abc` (feat)
3. **Fix pinned regression tests for the new RenderEmailHtml output (deviation)** - `caca8942` (fix)

## Files Created/Modified
- `QuestBoard.Service/Components/Emails/QuestFinalized.razor` - Row 3 call-site swap, scroll wrapper removed, comment updated
- `QuestBoard.Service/Components/Emails/SessionReminder.razor` - same swap, "The Adventure:" label preserved
- `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` - same swap
- `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` - `SampleMarkdownDescription` const, `WaitlistPromoted()` action, `Index()` link, `QuestFinalized()`/`SessionReminder()` sample swap
- `QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs` - assertions loosened to regex to match RenderEmailHtml's styled/MSO-commented output
- `QuestBoard.IntegrationTests/Emails/SessionReminderMarkdownRenderTests.cs` - same
- `QuestBoard.IntegrationTests/Emails/WaitlistPromotedMarkdownRenderTests.cs` - same

## Decisions Made
- Updated the 6 pre-existing regression tests (2 per template) that pinned the old `RenderToHtml(..., Email)` bare-tag output, rather than leaving the suite red — they directly test the call site Task 1 intentionally changed, and the new `style=` attributes/MSO comment are documented correctness requirements of `RenderEmailHtml` (Plan 01), not incidental drift.
- `SampleMarkdownDescription` deliberately exceeds the 5-block/650-char truncation budget (6 top-level blocks: heading, 2 paragraphs, unordered list, blockquote, ordered list) so every one of the 3 quest-email previews — not just the new WaitlistPromoted one — visibly demonstrates both the styling and the truncation/read-more behavior in the browser dev-loop.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated 6 pre-existing regression tests broken by the Task 1 call-site swap**
- **Found during:** Post-task full-suite verification (`dotnet test`)
- **Issue:** `QuestFinalizedMarkdownRenderTests`, `SessionReminderMarkdownRenderTests`, and `WaitlistPromotedMarkdownRenderTests` (2 tests each, from Phases 66/67) asserted exact bare-tag markup (`<strong>bold description</strong>`, `<h2>A heading</h2>`, `<li>item 1</li>`) produced by the old `RenderToHtml(..., MarkdownRenderTarget.Email)` call site. Task 1 intentionally replaced that call site with `RenderEmailHtml`, which adds inline `style=` attributes to every styled tag and an Outlook MSO bullet-fallback comment inside every `<li>` — both are documented correctness requirements of the Plan 01 method, not bugs. The 6 tests failed as a direct, foreseeable consequence.
- **Fix:** Loosened the assertions to regex matches (`<strong\b[^>]*>bold description</strong>`, `<h2\b[^>]*>A heading</h2>`, an MSO-comment-tolerant `<li>` pattern) that still verify the same structural/content guarantees (heading text present, list item text present, bold text rendered, image stripped) without pinning to markup this plan deliberately changed.
- **Files modified:** `QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs`, `SessionReminderMarkdownRenderTests.cs`, `WaitlistPromotedMarkdownRenderTests.cs`
- **Verification:** `dotnet test` — 299 unit + 396 integration tests, all passing
- **Committed in:** `caca8942`

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug, regression test updates)
**Impact on plan:** Necessary to keep the full test suite green after Task 1's intentional behavior change; no scope creep beyond the 3 test files directly testing the changed call site.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 3 quest email templates are wired to the Plan 01 `RenderEmailHtml` engine and ready for Plan 03's live Outlook desktop / Gmail webmail verification (D-05) — the blocking human-verification checkpoint this phase exists to support.
- The Admin-only `/EmailPreview/{QuestFinalized,SessionReminder,WaitlistPromoted}` browser previews all render the same structured-Markdown sample, giving the Plan 03 operator (or anyone) a fast pre-check before the expensive live-client send.
- Truncation budgets (`maxTopLevelBlocks = 5`, `maxPlainTextChars = 650`) remain tunable parameters on `RenderEmailHtml`, unchanged by this plan — Plan 03 can adjust them without touching the 3 templates again.
- No blockers.

---
*Phase: 71-email-safety-hardening*
*Completed: 2026-07-10*
