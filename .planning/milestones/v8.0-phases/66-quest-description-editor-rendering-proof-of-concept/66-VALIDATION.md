---
phase: 66
slug: quest-description-editor-rendering-proof-of-concept
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 66 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions 8.10.0 |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` (from repo root) |
| **Estimated runtime** | ~30-60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter MarkdownServiceTests` (fast, Domain-layer only)
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green; manual browser verification (desktop + narrow-mobile viewport) required for the UI-behavior requirements below
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 66-01-XX | 01 | 1 | EDITOR-04 (server half) | CSRF/XSS | `POST /markdown/preview` returns HTML identical to `IMarkdownService.RenderToHtml(text, Web)`, requires `[ValidateAntiForgeryToken]` | integration | `dotnet test QuestBoard.IntegrationTests --filter MarkdownControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 66-01-XX | 01 | 1 | — (feeds D-06/D-07 board-card plain text) | — | Plain-text extraction preserves word boundaries across block elements (heading + paragraph + list) | unit | `dotnet test QuestBoard.UnitTests --filter MarkdownServiceTests` | ⚠️ file exists, new cases needed | ⬜ pending |
| 66-01-XX | 01 | 1 | QUESTMD-01 (email half) | XSS | `QuestFinalized.razor` renders Description as sanitized HTML with images stripped (`MarkdownRenderTarget.Email`) | unit/integration | Extend `RazorEmailRenderService` test coverage or add focused test | ❌ W0 — confirm no existing coverage | ⬜ pending |
| 66-01-XX | 01 | 1 | EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-06 | — | Toolbar inserts syntax, Preview toggles, toolbar genuinely disabled during Preview, 44px+ touch targets, no overflow at 320px | manual | — (justified: zero JS test tooling in this repo; EasyMDE's internal correctness already verified against source in research) | N/A | ⬜ pending |
| 66-01-XX | 01 | 1 | EDITOR-05 | — | Tooltip renders with exact locked wording ("Supports Markdown formatting. Leave a blank line between paragraphs.") next to the Description label | manual (recommended) or string-contains regression check | — | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs` — covers the `POST /markdown/preview` round trip, including the header-based antiforgery pattern (`RequestVerificationToken` header — zero existing integration-test precedent in this codebase for the header variant; `AntiForgeryHelper.cs` only covers the hidden-input form variant today)
- [ ] New test cases in `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` for the plain-text extraction method (multi-block word-boundary case, null/empty input, already-sanitized-input round trip)
- [ ] Confirm whether any existing test exercises `QuestFinalized.razor` rendering at all — Phase 65's SUMMARY.md doesn't mention one; if none exists, this phase is the first to need it

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Toolbar buttons insert Markdown syntax around selection/cursor | EDITOR-01 | Zero JS test tooling in this repo (no package.json, no Playwright/Jest/Cypress config); EasyMDE's own insertion logic is a third-party concern already verified against source | Open Create/Edit/Follow-Up form (desktop + mobile), select text, click each of Bold/Italic/Heading/List/Link/Blockquote, confirm correct Markdown syntax wraps/inserts |
| Preview toggle shows byte-identical rendered HTML to saved output | EDITOR-02, EDITOR-04 | Requires live browser + server round trip, no automated E2E harness in this repo | Type formatted text, toggle Preview, compare visually against the saved/rendered Details page |
| Toolbar is genuinely disabled (not just greyed out) during Preview | EDITOR-03 | Same as above — DOM interaction state, no JS test tooling | Toggle Preview, attempt to click toolbar buttons, confirm no syntax is inserted |
| 44px+ touch targets fit one row with no overflow at 320px viewport | EDITOR-06 | Research flagged this as arithmetically tight — needs live-browser verification, not just CSS review | Resize browser to 320px width (or real mobile device), confirm all 7 toolbar buttons + Preview are visible in one row with no horizontal scroll |
| Tooltip shows exact locked wording | EDITOR-05 | Visual/copy verification | Hover (desktop) / tap (mobile) the info-circle icon next to "Description" label, confirm tooltip text matches exactly |
| FA6 icons render correctly (not missing-glyph boxes) for EasyMDE's default toolbar | — (supports EDITOR-01 visual correctness) | CDN-dependent, browser-rendering concern | Load Create/Edit form, visually confirm all toolbar icons render as expected glyphs, not empty boxes |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
