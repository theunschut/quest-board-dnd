---
phase: 91
slug: linked-boards-with-a-shared-calendar
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-15
---

# Phase 91 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2 + FluentAssertions 8.10.0; NSubstitute 5.3.0 (unit); EF Core InMemory 10.0.9; `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (integration) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (no assembly/collection parallelism); unit project has none |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Linked"` (plus the integration class the task touched) |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~180 seconds (full suite) |

Never set GC env vars on Linux; a `0x80131506` CLR error is a flake — rerun once. A locked-file build failure means the debugger is running: ask the user to stop it.

---

## Sampling Rate

- **After every task commit:** Run the quick run command, plus the touched integration test class
- **After every plan wave:** Run `dotnet test` (includes `Architecture`, `MigrationPreflightTests`, `AntiForgeryTokenCoverageTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `FeedRevisionWriteSeamTests`, `AmbientClockSeamTests`)
- **Before `/gsd-verify-work`:** Full suite green + manual local-SQL checkpoint recorded
- **Max feedback latency:** 180 seconds

---

## Per-Task Verification Map

Keyed by CONTEXT.md decision (the phase has no REQ IDs). Task IDs are filled in from the PLAN.md files once planning completes.

| Task ID | Plan | Wave | Decision | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | — | D-01, D-02 | IDOR / mixed-type tamper | Wrong-type or already-linked board refused server-side | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetRepositoryTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-03 | — | Deleting a board in a set succeeds; set and other boards intact | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatform"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-04 | — | Set with <2 boards adds nothing to any calendar | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendar"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-05, D-18 #8 | Elevation / CSRF | Non-SuperAdmin refused on GET/POST; every POST has `[ValidateAntiForgeryToken]` | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatform\|FullyQualifiedName~AntiForgeryTokenCoverageTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-06, D-07 | Info disclosure | Non-member sees plain text only (no `href`/`onclick`/`data-*` ids); no signups/votes/players leak | integration + unit | `dotnet test --filter "FullyQualifiedName~LinkedBoardCalendar\|FullyQualifiedName~LinkedCalendarRead"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-08 | Info disclosure | Member link switches board via Phase 87 middleware | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardDeepLink"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-09, D-10 | — | Finalized on chosen date, open on each proposed date; cancelled linked events absent | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarViewModelLinked"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-11 | — | Mobile `/calendar` shows linked entries incl. linked-only days | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendar"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-12 | Tampering | Quest Details shows linked dates read-only; no vote input from linked entries | integration + static guard | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardQuestDetails\|FullyQualifiedName~LinkedBoardStaticGuard"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-13 | Feed integrity | No write to Events/Quests; feed files untouched | existing guard + diff | `dotnet test --filter "FullyQualifiedName~Architecture"`; `git diff --stat -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs` empty | ✅ | ⬜ pending |
| TBD | TBD | — | D-14, D-15, D-16 | — | Linked marker + board name; legend only when linked; own before linked | integration + unit | `dotnet test --filter "FullyQualifiedName~LinkedBoardCalendar\|FullyQualifiedName~CalendarViewModelLinked"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-17 | Cross-tenant leak | Bypass confined to allowlist; pinned to linked ids; foreign row dropped + logged; unlinked → no read | existing seam + unit | `dotnet test --filter "FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests\|FullyQualifiedName~LinkedCalendarServiceTests"` | ⚠ seam ✅ / new ❌ W0 | ⬜ pending |
| TBD | TBD | — | D-18 #1–#3 | Cross-tenant leak | Unlinked unchanged; A↔B linked, C not; unlink restores | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendar\|FullyQualifiedName~TenantIsolation\|FullyQualifiedName~CalendarBoardTypeScopeTests\|FullyQualifiedName~EventCalendarPartialTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | — | Schema | — | Migration generated; no pending model changes | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MigrationPreflightTests"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs` — D-01..D-05, rename keeps link
- [ ] `QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs` — pinned predicate, window, cancelled hidden, no signup/vote load, never returns an unlinked board's row
- [ ] `QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs` — unlinked → no read, Campaign set → no quest read, foreign row dropped + logged, membership flag
- [ ] `QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs` — placement parity, own-before-linked, default-empty
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs` — D-18 #1–#6 on both layouts
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardQuestDetailsTests.cs` — D-12 / D-18 #7
- [ ] `QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs` — D-05 / D-18 #8 / D-03
- [ ] `QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs` — D-08 chain
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs` — linked partials contain no `<input`, `<form`, `DateVotes`, `vote-`; linked model types expose no vote/signup/player members
- No framework install needed (xUnit / FluentAssertions / NSubstitute already present).

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Migration applies and the linked-read predicates/projections translate on SQL Server | Schema, D-17 | EF InMemory cannot prove SQL translation; SQL Server is not provisioned by the agent | User runs the app against local SQL Server (`localhost:1433`), migration auto-applies, links two boards in Platform → Linked boards, loads `/calendar` and a Quest Details page on both; no exception in logs |
| Visual treatment of linked chips on desktop + mobile | D-14, D-15 | Visual judgement | Load `/calendar` on a linked board at desktop and mobile widths; linked entries muted with board name, own entries unchanged |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
