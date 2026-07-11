---
phase: 71
slug: email-safety-hardening
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-10
---

# Phase 71 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + FluentAssertions (existing, confirmed via `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds (full suite, per Phase 70's measured 269 unit + 396 integration run) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green, AND D-05's live Outlook/Gmail human verification must be complete — both required to close EMAILMD-02/EMAILMD-03.
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 71-01-01 | 01 | 1 | EMAILMD-02 | — | Every Markdig-emittable block/inline element (h1-h6, ul/ol/li, blockquote, p, a, strong, em, hr) gets the exact inline `style=` string from 71-UI-SPEC.md when rendered via the new email-render path | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ W0 | ⬜ pending |
| 71-01-02 | 01 | 1 | EMAILMD-02 | — | Every `<li>` gets the MSO-conditional bullet-fallback comment prepended | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ W0 | ⬜ pending |
| 71-01-03 | 01 | 1 | EMAILMD-02 | — | Email-target render still strips `<img>` (regression check against existing `RenderToHtml_EmailTarget_StripsImage` behavior) for the new method | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ✅ existing pattern / ❌ new test needed | ⬜ pending |
| 71-02-01 | 02 | 1-2 | EMAILMD-03 | — | Content under the block/char budget is NOT truncated, no read-more link appended | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ W0 | ⬜ pending |
| 71-02-02 | 02 | 1-2 | EMAILMD-03 | — | Content over budget is truncated exactly at a complete block boundary — output remains well-formed HTML, never cuts mid-`<li>`/mid-`<blockquote>`/mid-heading | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ W0 | ⬜ pending |
| 71-02-03 | 02 | 1-2 | EMAILMD-03 | — | Truncated output ends with the exact D-04 copy "…continue reading on the quest board" linking to the passed `readMoreUrl` | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MarkdownServiceTests` | ❌ W0 | ⬜ pending |
| 71-03-01 | 03 | 2 | EMAILMD-02, EMAILMD-03 | — | Real Outlook desktop and real Gmail webmail render the sent test email correctly (visible bullets, intact styling, no clipping, working read-more link) | manual | N/A — human-verify checkpoint (D-05), explicitly not automatable | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Exact plan/wave numbers above are provisional — the planner assigns final plan IDs; this map exists to guarantee no requirement goes untested, not to prescribe plan structure.*

---

## Wave 0 Requirements

- [ ] New test cases in `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` covering the new email-render method's styling injection, MSO fallback injection, and truncation behavior (see table above) — extend the existing test file to match its established pattern, no new test file needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Real Outlook desktop rendering (visible bullets, intact heading/blockquote styling, no clipping) | EMAILMD-02, EMAILMD-03 | Outlook's Word rendering engine cannot be simulated by any browser preview, headless tool, or automated screenshot service available to this project — must be opened in the real client (locked at discuss-phase, D-05) | Claude sends a real test email (Quest Finalized, Session Reminder, and Waitlist Promoted) via the existing Resend/Postfix relay to the operator's inbox, using a throwaway test quest with a Markdown-structured Description (headings + list + blockquote, long enough to exceed the truncation budget). Operator opens each in real Outlook desktop and reports what they see. |
| Real Gmail webmail rendering (same content) | EMAILMD-02, EMAILMD-03 | Same reasoning — Gmail's `<style>`-block-stripping behavior and rendering quirks must be confirmed in the real webmail client, not assumed from research alone | Same test emails, opened in real Gmail webmail. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-10 (via gsd-plan-checker Dimension 8 verification, plan revision iteration 1)
