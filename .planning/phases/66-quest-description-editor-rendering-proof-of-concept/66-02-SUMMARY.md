---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 02
subsystem: api
tags: [markdown, antiforgery, csrf, integration-testing, aspnet-core-mvc]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: IMarkdownService.RenderToHtml(markdown, MarkdownRenderTarget) — Markdig + HtmlSanitizer, registered AddSingleton
provides:
  - "POST /markdown/preview endpoint (MarkdownController.Preview) that renders arbitrary Markdown text through the identical IMarkdownService.RenderToHtml(text, Web) call/target used by saved page display"
  - "AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync — this codebase's first header-token extraction helper for JSON-body antiforgery-protected POSTs"
  - "This codebase's first integration test coverage for a header-based-antiforgery action (QuestController.RevokeSignup/RemovePlayerSignup previously had none)"
affects: [66-01, 67-quest-rewards-recap-migration, quest-description-editor]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Server round-trip Markdown preview: client POSTs raw text, server renders via the same sanitizer/target used for final display, guaranteeing byte-identical output by construction"
    - "Reflection-by-string CSRF-attribute assertion (Type.GetType by full name, not typeof) so an integration test file can assert a not-yet-built controller's attributes without a hard compile-time reference — keeps RED failures at runtime (404) rather than at build time"

key-files:
  created:
    - QuestBoard.Service/Controllers/MarkdownController.cs
    - QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Helpers/AntiForgeryHelper.cs

key-decisions:
  - "The no-token-returns-400 acceptance criterion was reworked to a structural (reflection) assertion because this test harness's WebApplicationFactoryBase installs a TestAntiforgeryDecorator that always succeeds validation regardless of what a request sends — an established, already-documented limitation (see Security/AntiForgeryTokenCoverageTests.cs and UsersControllerIntegrationTests's identical precedent), not something introduced by this plan."
  - "Used Quest/Details/{id} (any authenticated group member can reach it) rather than a DM-only page like Quest/Edit to source the antiforgery token for MarkdownController's broader [Authorize] scope."

requirements-completed: [EDITOR-02, EDITOR-04]

# Metrics
duration: 25min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 02: Markdown Preview Endpoint Summary

**`POST /markdown/preview` renders Markdown through the identical `IMarkdownService.RenderToHtml(text, Web)` path used by saved page display, proven by a new header-based-antiforgery + JSON-body integration test pattern — this codebase's first.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-09
- **Completed:** 2026-07-09
- **Tasks:** 2 (TDD: RED then GREEN)
- **Files modified:** 3 (2 created, 1 modified)

## Accomplishments
- `MarkdownController.Preview` — `[Authorize]`, `[HttpPost]`, `[Route("markdown/preview")]`, `[ValidateAntiForgeryToken]` — delegates to `IMarkdownService.RenderToHtml(request.Markdown, MarkdownRenderTarget.Web)`, guaranteeing EDITOR-04's "preview exactly matches saved render" by construction (same method, same target)
- `AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync` — new helper exposing the same `AntiforgeryTokenSet.RequestToken` value the existing form-field helper already extracts, for use as an HTTP header on a JSON POST
- `MarkdownControllerIntegrationTests` (4 tests): authenticated round-trip matching `RenderToHtml` byte-for-byte, structural CSRF-attribute proof, no-server-error-without-token regression, and anonymous-denial check
- Zero `Program.cs` changes — confirmed via `git diff --name-only`, matching the app's existing default header-antiforgery convention

## Task Commits

Each task was committed atomically:

1. **Task 1: Add header-token helper + failing MarkdownController integration test (RED)** - `b6d10226` (test)
2. **Task 2: Implement MarkdownController.Preview (GREEN)** - `0b58da1f` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified
- `QuestBoard.Service/Controllers/MarkdownController.cs` - New `POST /markdown/preview` action
- `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs` - Round-trip, CSRF-attribute, no-token, and anonymous-denial coverage
- `QuestBoard.IntegrationTests/Helpers/AntiForgeryHelper.cs` - Added `ExtractHeaderAntiForgeryTokenAsync`, existing form-field method untouched

## Decisions Made
- The Preview endpoint uses `MarkdownRenderTarget.Web` exclusively (never `.Email`), matching D-06/EDITOR-04's requirement that preview match the *page* display, not the email-safe render — verified by an explicit "does not contain `MarkdownRenderTarget.Email`" absence check via `grep`.
- Reused `Quest/Details/{id}` (broadly accessible to any authenticated group member) rather than a DM-only form page to source the antiforgery token, since `MarkdownController` is intentionally not DM-scoped (several of the milestone's 9 future fields are player-authored, per ASVS V4 in 66-RESEARCH.md).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Reworked the "missing-token returns HTTP 400" acceptance criterion to a structural assertion**
- **Found during:** Task 1 (writing the RED integration tests)
- **Issue:** The plan's acceptance criteria required a live integration test asserting a POST without the `RequestVerificationToken` header returns HTTP 400. This is not achievable in this codebase's test harness: `WebApplicationFactoryBase` replaces `IAntiforgery` with a `TestAntiforgeryDecorator` whose `ValidateRequestAsync`/`IsRequestValidAsync` always succeed regardless of request content — a pre-existing, already-documented limitation (see `QuestBoard.IntegrationTests/Security/AntiForgeryTokenCoverageTests.cs`'s own doc comment, and the identical precedent already shipped in `UsersControllerIntegrationTests.Disable_And_Enable_Actions_CarryValidateAntiForgeryToken`/`Disable_Post_WithoutAntiForgeryToken_IsRejected`). A literal live-400 assertion would either be permanently false (asserting behavior the harness cannot produce) or would require weakening the harness's antiforgery test double app-wide, which is out of scope for this plan and would affect every other controller's test coverage.
- **Fix:** Added `MarkdownController_Preview_CarriesValidateAntiForgeryToken`, a reflection-based test (looked up by assembly-qualified type name, not `typeof`, so the test file compiles and correctly fails at RED before the controller exists) asserting the `Preview` action carries `[ValidateAntiForgeryToken]` and the controller carries `[Authorize]` — the same structural-proof pattern this codebase already established for this exact limitation. Kept a live `Preview_WithoutAntiForgeryHeader_DoesNotServerError` regression test (mirrors `Disable_Post_WithoutAntiForgeryToken_IsRejected`) asserting no 5xx, since the token-carrying attribute is what actually gates CSRF in production (the test harness's bypass is test-only).
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs`
- **Verification:** `dotnet test QuestBoard.IntegrationTests --filter MarkdownControllerIntegrationTests` — all 4 tests pass; the reflection test independently corroborated by the pre-existing app-wide `AntiForgeryTokenCoverageTests.AllHttpPostActions_CarryValidateAntiForgeryToken` sweep, which also passes with `MarkdownController` in scope.
- **Committed in:** `b6d10226` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking — test-harness limitation, not a code defect)
**Impact on plan:** The production CSRF mitigation (T-66-03) is unchanged and fully implemented as planned (`[ValidateAntiForgeryToken]` on the action); only the *test's* proof mechanism differs from the literal plan text, following an already-established codebase convention for the identical limitation. No scope creep.

## Issues Encountered
None beyond the deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `POST /markdown/preview` is live and ready for the EasyMDE client-side wiring (66-01 or a subsequent plan) to call via `previewRender`.
- The header-based-antiforgery + JSON-body test pattern (`AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync` + reflection-based CSRF proof) is now a reusable precedent for any future JSON POST endpoint in this codebase.
- No blockers.

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*
