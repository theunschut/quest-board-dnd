---
phase: 65-markdown-rendering-foundation
plan: 01
subsystem: domain-services
tags: [markdig, htmlsanitizer, xss, markdown, dotnet]

# Dependency graph
requires: []
provides:
  - "IMarkdownService / MarkdownService in QuestBoard.Domain, registered as an AddSingleton"
  - "Single shared Markdig pipeline (6 individually-composed extensions) feeding two immutable HtmlSanitizer profiles (web keeps <img>, email strips it)"
  - "MarkdownServiceTests.cs establishing the XSS-payload / strict-paragraph / per-extension content-survival / dual-profile test pattern for future Markdown-consuming phases"
affects: [66-quest-description-poc, 67-quest-rewards-recap-emails, 68-character, 69-contact, 70-dm-profile-shop, 71-email-safety-hardening]

# Tech tracking
tech-stack:
  added: ["Markdig 1.3.2", "HtmlSanitizer (Ganss.Xss) 9.0.892"]
  patterns:
    - "Domain-layer stateless transform service registered as AddSingleton (deviation from the file's all-AddScoped convention), matching ImageValidationService's internal-class + InternalsVisibleTo test-access shape"
    - "Single parsed-once rawHtml branched only at the sanitizer-selection step to guarantee web/email output is byte-identical except for the img allowance"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IMarkdownService.cs
    - QuestBoard.Domain/Services/MarkdownService.cs
    - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs
  modified:
    - QuestBoard.Domain/QuestBoard.Domain.csproj
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "Composed the Markdig pipeline from 6 individual Use*() calls, never .UseAdvancedExtensions() (D-04) -- confirmed via grep that neither UseAdvancedExtensions nor UseGenericAttributes appears anywhere in MarkdownService.cs"
  - "Two long-lived HtmlSanitizer instances built once, never mutated -- required for thread-safe concurrent Sanitize() calls from a singleton-registered service"
  - "id allowed in AllowedAttributes narrowly for footnote fnref:N/fn:N jump-links, since Markdig only ever emits sequential integers there, never user-supplied values"

requirements-completed: [RENDER-01, RENDER-02, RENDER-03]

# Metrics
duration: 20min
completed: 2026-07-09
---

# Phase 65 Plan 01: Markdown Rendering Foundation Summary

**Single Markdig 1.3.2 pipeline (6 individually-composed GFM extensions, no raw HTML) feeding two immutable Ganss.Xss HtmlSanitizer profiles -- web keeps `<img>`, email strips it -- registered as an `AddSingleton<IMarkdownService, MarkdownService>()`.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-07-09T09:07:00Z (approx, first file read)
- **Completed:** 2026-07-09T09:27:35Z
- **Tasks:** 3/3 completed
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments
- `IMarkdownService.RenderToHtml(string?, MarkdownRenderTarget)` is now the single entry point for Markdown-to-HTML conversion; `Markdown.ToHtml(` appears in exactly one `.cs` file across the entire solution (verified via repo-wide grep), satisfying RENDER-02
- XSS defense verified against all 5 required payload shapes (`<script>`, raw `<img onerror>`, `javascript:` in `[text](url)`, `javascript:` in a native CommonMark autolink, and Markdig generic-attribute injection) using live-HTML-structure assertions rather than naive substring checks, closing the exact gap the milestone research flagged (native autolinks are not blocked by `.DisableHtml()`)
- Strict CommonMark paragraph behavior confirmed: a single Enter stays one `<p>` with no `<br>`; a blank line produces two `<p>` tags (RENDER-03)
- Full GFM extension set (autolinks, pipe tables, task lists, definition lists, footnotes, strikethrough) enabled and content-survival-tested per extension, proving the `KeepChildNodes=false` allowlist is complete rather than silently deleting cell/definition/footnote text
- Service registered as a DI singleton, buildable and resolvable by later phases; full unit suite (261/261) green with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Add packages, contracts, throwing stub, and the full failing test suite** - `8f52a3d` (test)
2. **Task 2 (GREEN): Implement the single Markdig pipeline + two immutable HtmlSanitizer profiles** - `16f4bdc` (feat)
3. **Task 3 (WIRE + RENDER-02 gate): Register the singleton and prove a single parse call site** - `b6269e8` (feat)

**Plan metadata:** (pending final commit — this SUMMARY.md, committed separately per worktree protocol)

## Files Created/Modified
- `QuestBoard.Domain/QuestBoard.Domain.csproj` - Added Markdig 1.3.2 and HtmlSanitizer 9.0.892 package references to the existing ItemGroup
- `QuestBoard.Domain/Interfaces/IMarkdownService.cs` - New contract: `MarkdownRenderTarget` enum (Web/Email) + `IMarkdownService.RenderToHtml`
- `QuestBoard.Domain/Services/MarkdownService.cs` - New internal service: one `MarkdownPipeline` (6 individually-composed extensions), two immutable `HtmlSanitizer` instances (web/email), null/whitespace short-circuit
- `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` - New test suite: 18 tests covering XSS payloads, strict paragraphs, defensive null/whitespace input, per-extension content survival, and the web/email dual-profile split
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Added `AddSingleton<IMarkdownService, MarkdownService>()` with an inline why-comment explaining the Singleton-not-Scoped deviation

## Decisions Made
- Followed RESEARCH.md's Pattern 1/2/3 exactly: single-method-plus-enum interface shape, individually-composed pipeline, two immutable sanitizer instances sharing a common base tag set
- Used explicit `new HtmlSanitizer(new HtmlSanitizerOptions { ... })` construction (not target-typed `new(...)`) so the plan's acceptance-criteria grep for `new HtmlSanitizer(` matches both instances literally
- Kept the `.UseAdvancedExtensions()` explanatory comment free of the literal substring `UseAdvancedExtensions` (referred to it as "the bundled convenience method" instead), since the acceptance criteria required the file to contain zero occurrences of that string, including in comments

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed incorrect definition-list test input**
- **Found during:** Task 2 (GREEN implementation)
- **Issue:** `RenderToHtml_DefinitionList_PreservesTermAndDefinition` used `"Sword\n: A blade"` (one space after the colon), mirroring RESEARCH.md's prose example literally. Markdig's `DefinitionListParser` requires 3+ spaces (or a tab) after the `:` marker to recognize a definition line — with only one space, the input parses as an ordinary paragraph (`<p>Sword\n: A blade</p>`), not a `<dl>`. Confirmed by a throwaway debug test exercising 4 candidate input shapes before settling on the fix.
- **Fix:** Changed the test input to `"Sword\n:   A blade"` (3 spaces after the colon)
- **Files modified:** QuestBoard.UnitTests/Services/MarkdownServiceTests.cs
- **Verification:** Test passes; full suite green (18/18 Markdown tests, 261/261 overall)
- **Committed in:** `16f4bdc` (Task 2 commit)

**2. [Rule 1 - Bug] Removed the literal substring `UseAdvancedExtensions` from an explanatory code comment**
- **Found during:** Task 2 (GREEN implementation), while verifying Task 2's own acceptance criteria
- **Issue:** The why-comment above the pipeline construction explained the D-04 rationale by name-dropping `.UseAdvancedExtensions()` directly, which technically satisfied the plan's narrative intent but violated the plan's own machine-checkable acceptance criterion ("MarkdownService.cs does NOT contain the substring UseAdvancedExtensions")
- **Fix:** Reworded the comment to describe the same rationale ("the bundled convenience method that chains 19 extensions at once") without using the literal method name
- **Files modified:** QuestBoard.Domain/Services/MarkdownService.cs
- **Verification:** `grep -c UseAdvancedExtensions MarkdownService.cs` returns 0; full suite still green
- **Committed in:** `16f4bdc` (Task 2 commit)

**3. [Rule 1 - Bug] Switched sanitizer construction from target-typed `new(...)` to explicit `new HtmlSanitizer(...)`**
- **Found during:** Task 2 (GREEN implementation), while verifying Task 2's own acceptance criteria
- **Issue:** The initial implementation used C# target-typed `new()` for both sanitizer fields, which compiles identically but does not contain the literal substring `new HtmlSanitizer(` the plan's acceptance criteria greps for
- **Fix:** Changed both field initializers to explicit `new HtmlSanitizer(new HtmlSanitizerOptions { ... })`
- **Files modified:** QuestBoard.Domain/Services/MarkdownService.cs
- **Verification:** `grep -c "new HtmlSanitizer("` returns 2; full suite still green
- **Committed in:** `16f4bdc` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (all Rule 1 — test/verification correctness, no scope creep)
**Impact on plan:** All three fixes were required to make the plan's own written acceptance criteria (test correctness, grep-checkable constraints) actually hold. No behavior, scope, or architecture changed beyond what the plan specified.

## Issues Encountered
None beyond the deviations above.

## User Setup Required
None - no external service configuration required. This phase adds no new environment variables, secrets, or external dependencies (Markdig/HtmlSanitizer are pure managed NuGet packages with no runtime configuration).

## Next Phase Readiness
- `IMarkdownService` is registered, resolvable, and unit-tested — Phase 66 can inject it directly into a new `Html.Markdown()` MVC helper and the `QuestFinalized.razor` email component without further Domain-layer work
- The web/email dual-profile split (D-06/D-07/D-08) is proven at the type level and by the `RenderToHtml_NoImageContent_WebAndEmailIdentical` test; Phase 66/67's email wiring can rely on `MarkdownRenderTarget.Email` stripping `<img>` with no further sanitizer configuration needed
- No blockers. The one open cosmetic item from RESEARCH.md (pipe-table `style="text-align:..."` alignment, footnote/task-list CSS hook classes) was deliberately left out of the baseline allowlist per the research's own recommendation — revisit only if Phase 66+ hits a concrete styling need

## Known Stubs
None — this phase is pure Domain-layer plumbing with no UI wiring; there is no partially-wired data path to stub.

## Self-Check: PASSED

All 6 claimed files verified present on disk; all 4 claimed commit hashes (8f52a3d, 16f4bdc, b6269e8, 442fac9) verified present in git log.

---
*Phase: 65-markdown-rendering-foundation*
*Completed: 2026-07-09*
