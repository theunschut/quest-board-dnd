---
phase: 65
slug: markdown-rendering-foundation
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-09
---

# Phase 65 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 (`xunit.v3` + `xunit.runner.visualstudio`) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (no separate `xunit.runner.json` needed — that file only exists for `QuestBoard.IntegrationTests`, to force serial execution against a shared in-memory DB, which this phase's stateless service doesn't need) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests"` |
| **Full suite command** | `dotnet test QuestBoard.UnitTests` |
| **Estimated runtime** | ~15 seconds (unit tests only, no DB) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests"`
- **After every plan wave:** Run `dotnet test QuestBoard.UnitTests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 65-01-01 | 01 | 0 | RENDER-01 | T-65-01 | Stub test file created, tests fail red (no service yet) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests"` | ❌ W0 | ⬜ pending |
| 65-01-02 | 01 | 1 | RENDER-01 | T-65-01 | `<script>` tags produce no live script in output | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_XssPayload"` | ❌ W0 | ⬜ pending |
| 65-01-03 | 01 | 1 | RENDER-01 | T-65-02 | `[text](javascript:...)` link syntax produces no live `javascript:` href | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_XssPayload"` | ❌ W0 | ⬜ pending |
| 65-01-04 | 01 | 1 | RENDER-01 | T-65-03 | Native autolink `<javascript:...>` produces no live `javascript:` href (NOT blocked by `.DisableHtml()` alone — confirmed via source) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_XssPayload"` | ❌ W0 | ⬜ pending |
| 65-01-05 | 01 | 1 | RENDER-01 | T-65-04 | Generic-attribute injection (`{onmouseover="..."}`) renders as inert literal text, not a live attribute | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_XssPayload"` | ❌ W0 | ⬜ pending |
| 65-01-06 | 01 | 1 | RENDER-03 | — | Single Enter stays one paragraph; blank line starts a new paragraph | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MarkdownServiceTests.RenderToHtml_.*Paragraph"` | ❌ W0 | ⬜ pending |
| 65-01-07 | 01 | 1 | RENDER-02 | — | Single shared entry point; no duplicated Markdig/sanitizer wiring elsewhere in the codebase | code review + type-level design | N/A (architectural constraint, verified by grep for duplicate `Markdown.ToHtml(` call sites) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` — stub test file covering RENDER-01, RENDER-03; new file, no existing test to extend
- [ ] `QuestBoard.Domain/Interfaces/IMarkdownService.cs` + `QuestBoard.Domain/Services/MarkdownService.cs` — the service under test doesn't exist yet; build order follows this project's established "failing tests, then implementation" pattern (per milestone ARCHITECTURE.md's suggested build order)
- [ ] No new shared fixture needed — `MarkdownService` is stateless and constructible with a parameterless constructor (testable directly via the existing `InternalsVisibleTo("QuestBoard.UnitTests")` in `QuestBoard.Domain/Properties/AssemblyInfo.cs`, confirmed present)

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification. This phase is pure backend plumbing with no UI surface.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
