---
phase: 34
slug: codebase-cleanup-and-security-hardening-remove-unused-code-s
status: ready
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-01
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
>
> **Scope note (post D-03 split):** This phase now covers only the mechanical-cleanup slice (34a) — dead code removal, comment cleanup, XML doc backfill, dependency scan evidence. The broader CONCERNS.md items originally sketched here (Known Bugs, Security Considerations, Performance, Fragile Areas, Scaling Limits, Test Coverage Gaps) moved to Phase 34.1 (Security & Bugs) and Phase 34.2 (Performance & Architecture) — each gets its own VALIDATION.md when planned.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5), `Microsoft.NET.Test.Sdk` 18.7.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (only project with a custom runner config) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds (191 tests as of Phase 33 close; this phase makes no test-count changes — comment/doc/dead-code only) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build` (fast compile-check for comment/dead-code/doc changes)
- **After every plan wave:** Run `dotnet test` (full suite — regression check only, no new tests expected)
- **Before `/gsd-verify-work`:** Full suite must be green, plus a final `dotnet list package --vulnerable --include-transitive` re-run as phase-closing evidence
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

No REQ-IDs are mapped to this phase (cleanup/hardening, not a features phase) — mapped to CONTEXT.md decisions and PLAN.md IDs instead.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 34-01-01 | 01 | 1 | D-04/D-05: delete `RegisterViewModel` | — | N/A | build | `dotnet build` | N/A — compiler is the test | ⬜ pending |
| 34-01-02 | 01 | 1 | D-09/D-10: dependency vulnerability scan | T-34-03 (ASVS V13) | No vulnerable packages found | manual/scripted | `dotnet list package --vulnerable --include-transitive` | N/A — CLI output is the evidence | ⬜ pending |
| 34-02-01/02 | 02 | 1 | D-06/D-08: strip ID/phase comment tags — 9 non-test source files | — | N/A | build | `dotnet build` | N/A | ⬜ pending |
| 34-03-01/02 | 03 | 1 | D-06/D-08: strip ID/phase comment tags — 21 test files | — | N/A | build | `dotnet build` | N/A | ⬜ pending |
| 34-04-01/02 | 04 | 1 | D-06/D-07: XML doc backfill — 26 Domain interfaces | — | N/A | build | `dotnet build` | N/A | ⬜ pending |
| 34-05-01/02 | 05 | 1 | D-07: XML doc backfill — 9 Repository interfaces | — | N/A | build | `dotnet build` | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — no new test framework setup needed. This phase is comment/doc/dead-code cleanup only; it does not add new behavior requiring new tests (Wave 0 test gaps for Known Bugs, Security, and Test Coverage Gaps items moved to Phase 34.1/34.2 with the rest of that scope).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Dependency vulnerability scan is clean at phase close | D-09/D-10 | CLI scan output is the evidence itself, not a pass/fail unit test | Run `dotnet list package --vulnerable --include-transitive` across all 5 projects; confirm zero vulnerable packages (or document any findings + fix/defer decision) |
| Genuinely useful "why"/landmine comments preserved (not stripped) | D-08 | Requires reading diff context to confirm intent-preserving comments (e.g. `QuestService.RemoveAsync()`'s manual-cleanup-order comment) survived the sweep, not just that ID-tagged ones were removed | Grep for the named preserve-examples (`Manual cleanup`, `moved from`, `system-wide sweep`) post-cleanup; confirm they still exist verbatim |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (none — no new test infra needed)
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-01 (gsd-plan-checker: VERIFICATION PASSED)
