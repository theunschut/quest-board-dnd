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

Keyed by CONTEXT.md decision (the phase has no REQ IDs). Task ids read `91-<plan>-T<n>`, where `<n>` is the task's position in that plan. Waves: 91-01 and 91-02 → W1; 91-03 and 91-04 → W2; 91-05 and 91-06 → W3; 91-07, 91-08 and 91-09 → W4; 91-10 → W5.

Plan map: 91-01 schema, migration and set repository · 91-02 DM-only rule on the own calendars · 91-03 Platform Linked Boards page (desktop) with the rules service · 91-04 pinned linked reads and the per-viewer set lookup · 91-05 linked overlay service and desktop `/calendar` · 91-06 Platform mobile twin and entry buttons · 91-07 desktop look, legend and deep-link proof · 91-08 mobile agenda · 91-09 Quest Details calendar and static guard · 91-10 end-to-end proof and human verification.

| Task ID | Plan | Wave | Decision | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| 91-03-T1, 91-03-T2 | 91-03 | 2 | D-01, D-02 | T-91-02, T-91-03 | Wrong-type or already-linked board refused server-side, even when posted directly; RemoveBoard refuses a board not in the posted set | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetServiceTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatformIntegrationTests"` | ❌ W0 (created by 91-03) | ⬜ pending |
| 91-01-T1 | 91-01 | 1 | D-02 (storage) | T-91-41 | A board is in at most one set by construction (one nullable column); the set repository reads unfiltered Groups with no filter bypass | unit + existing seam | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetRepositoryTests\|FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests"` | ❌ W0 (created by 91-01) | ⬜ pending |
| 91-01-T2, 91-03-T2 | 91-01, 91-03 | 1, 2 | D-03 | T-91-06 | Deleting a board in a set succeeds; set and other boards intact; deleting a set un-links boards and deletes nothing else | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetRepositoryTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatformIntegrationTests"` | ❌ W0 | ⬜ pending |
| 91-01-T2, 91-03-T2, 91-04-T1, 91-05-T1, 91-06-T2 | 91-01, 91-03, 91-04, 91-05, 91-06 | 1, 2, 3 | D-04 | — | A set with fewer than two boards is kept, shown as Inactive and adds nothing to any calendar | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetRepositoryTests\|FullyQualifiedName~LinkedCalendarReadTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatformIntegrationTests\|FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests"` | ❌ W0 | ⬜ pending |
| 91-03-T1, 91-03-T2, 91-06-T1, 91-06-T2 | 91-03, 91-06 | 2, 3 | D-05, D-18 #8 | T-91-01, T-91-04, T-91-05, T-91-21, T-91-22, T-91-23 | Non-SuperAdmin refused on GET and POST; every POST has `[ValidateAntiForgeryToken]`; names cannot break out of confirm(); mobile twin and entry buttons | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatformIntegrationTests\|FullyQualifiedName~AntiForgeryTokenCoverageTests"` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-04-T2, 91-05-T1, 91-05-T2, 91-07-T2, 91-08-T2, 91-09-T3 | 91-04, 91-05, 91-07, 91-08, 91-09 | 2, 3, 4 | D-06, D-07 | T-91-11, T-91-12, T-91-14, T-91-24, T-91-29 | Non-member sees plain text only (no href, onclick or data ids); narrow projection never loads signups, votes or players; no roster or poll answer leaks | integration + unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedCalendarReadTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests\|FullyQualifiedName~LinkedBoardMobileCalendarTests\|FullyQualifiedName~LinkedBoardStaticGuardTests"` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-05-T1, 91-07-T3 | 91-04, 91-05, 91-07 | 2, 3, 4 | D-08 | T-91-25 | Membership is the fresh UserGroups row the deep-link resolver checks; member link is the ordinary Details URL and switches the board through the existing middleware; non-member and SuperAdmin non-member get no link | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedCalendarReadTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardDeepLinkTests\|FullyQualifiedName~CrossBoardDeepLinkMiddlewareTests"` | ❌ W0 | ⬜ pending |
| 91-04-T2, 91-05-T2, 91-07-T2 | 91-04, 91-05, 91-07 | 2, 3, 4 | D-09, D-19 | — | Finalized on its chosen date, open on each proposed date; the window keeps every placeable quest; parity with the own rule | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarViewModelLinkedTests\|FullyQualifiedName~LinkedCalendarReadTests"`; integration `LinkedBoardCalendarTenantIsolationTests` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-05-T1, 91-07-T2, 91-08-T2, 91-09-T2 | 91-04, 91-05, 91-07, 91-08, 91-09 | 2, 3, 4 | D-10 (revised) | — | Cancelled linked events are read, included and struck through on desktop chip, mobile row and mobile Details line, with the state also in text | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedCalendarReadTests"`; `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoard"` | ❌ W0 | ⬜ pending |
| 91-05-T1, 91-08-T1 | 91-05, 91-08 | 3, 4 | D-11 | — | Desktop and mobile `/calendar` show linked entries, including linked-only days | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests\|FullyQualifiedName~LinkedBoardMobileCalendarTests"` | ❌ W0 | ⬜ pending |
| 91-09-T1, 91-09-T2, 91-09-T3 | 91-09 | 4 | D-12, D-18 #7 | T-91-15, T-91-31 | Quest Details shows linked dates read-only; no vote input reachable from linked entries; DateVotes input count unchanged by linking; own events stay off Details | integration + static guard | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardQuestDetailsTests\|FullyQualifiedName~LinkedBoardStaticGuardTests"` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-04-T2, 91-10-T1 | 91-04, 91-10 | 2, 5 | D-13 | T-91-16, T-91-38 | No write to Events/Quests; feed, agenda, events-overview and series files untouched | existing guard + diff | `dotnet test --filter "FullyQualifiedName~Architecture"`; `git diff --stat c4abdf84 -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs QuestBoard.Service/Controllers/AgendaController.cs QuestBoard.Service/Views/Agenda QuestBoard.Service/Controllers/Events/EventsController.cs QuestBoard.Service/Views/Events QuestBoard.Domain/Services/EventSeriesService.cs` is empty | ✅ | ⬜ pending |
| 91-07-T1, 91-07-T2, 91-08-T1, 91-08-T2, 91-09-T1 | 91-07, 91-08, 91-09 | 4 | D-14 (revised), D-15 (revised), D-16 | T-91-27 | Desktop chips muted and dashed with no board line at rest (board, DM and time on hover or focus); mobile keeps the board name as text; Legend, mobile and Details "Also showing" only when linked (the Details note renders from `_Calendar.cshtml`, fed by `QuestController.Details`); own entries before linked | integration + unit | `dotnet test --filter "FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests\|FullyQualifiedName~LinkedBoardMobileCalendarTests\|FullyQualifiedName~LinkedBoardQuestDetailsTests\|FullyQualifiedName~CalendarViewModelLinkedTests"` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-04-T2, 91-05-T1, 91-05-T3 | 91-04, 91-05 | 2, 3 | D-17 | T-91-11, T-91-17, T-91-19, T-91-40 | Bypass confined to the allowlist; pinned to the linked ids; foreign row dropped and logged; unlinked means no read | existing seam + unit | `dotnet test --filter "FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests\|FullyQualifiedName~LinkedCalendarServiceTests\|FullyQualifiedName~LinkedCalendarReadTests"` | ⚠ seam ✅ / new ❌ W0 | ⬜ pending |
| 91-05-T1, 91-10-T1 | 91-05, 91-10 | 3, 5 | D-18 #1–#3 | T-91-11, T-91-37, T-91-40 | Unlinked unchanged; A↔B linked, C not; unlink takes effect on the next request (seeded and through the real Platform POSTs) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests\|FullyQualifiedName~LinkedBoardEndToEndTests\|FullyQualifiedName~TenantIsolation\|FullyQualifiedName~CalendarBoardTypeScopeTests\|FullyQualifiedName~EventCalendarPartialTests"` | ❌ W0 | ⬜ pending |
| 91-02-T1, 91-02-T2, 91-04-T1, 91-05-T2, 91-05-T3, 91-08-T2, 91-09-T2 | 91-02, 91-04, 91-05, 91-08, 91-09 | 1, 2, 3, 4 | D-20, D-18 #9 (DM-only) | T-91-08, T-91-13 | DM-only sessions follow the viewer's role on the owning board (SuperAdmin counts as Admin) on own and linked calendars, desktop, mobile and Details | integration + unit | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarDungeonMasterSessionTests\|FullyQualifiedName~LinkedBoardCalendarTenantIsolationTests"`; `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedCalendarServiceTests\|FullyQualifiedName~LinkedCalendarReadTests"` | ❌ W0 | ⬜ pending |
| 91-04-T1, 91-05-T2, 91-05-T3 | 91-04, 91-05 | 2, 3 | D-01 (Campaign) | T-91-02 | A type-mismatched set yields no linked boards; a Campaign set never loads linked quests; linked events still show | integration + unit | `LinkedBoardCalendarTenantIsolationTests`; `LinkedCalendarServiceTests`; `LinkedCalendarReadTests` | ❌ W0 | ⬜ pending |
| 91-01-T1, 91-01-T2 | 91-01 | 1 | Schema | T-91-39 | Migration generated; no pending model changes; nothing outside a transaction; a board rename keeps its link | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MigrationPreflightTests\|FullyQualifiedName~LinkedBoardSetRepositoryTests"` | ✅ | ⬜ pending |
| 91-10-T2 | 91-10 | 5 | Schema, D-17 (translation), D-14/D-15 backstops | T-91-36 | Migration applies and the windowed linked reads translate on SQL Server; visual and phone backstops | manual | see Manual-Only Verifications | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Each new test file is created by the task that first needs it. The tracer task in each plan writes its failing facts before the code, so every file below exists before the code it proves is finished.

- [ ] `QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs`: create and assign, delete un-links, board-row removal keeps the set, case-insensitive names, sets of 0/1 kept, rename keeps link (91-01-T1, extended by 91-01-T2)
- [ ] `QuestBoard.UnitTests/Services/LinkedBoardSetServiceTests.cs`: same-type, at-most-one, name rules (91-03-T2)
- [ ] `QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs`: viewer set lookup, pinned predicate, window, cancelled included, never an unlinked board's row (91-04-T1); quest projection with in-window proposed dates, no signup/vote load (91-04-T2)
- [ ] `QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs`: no read when unlinked, Campaign set means no quest read, foreign row dropped and logged, role map (91-05-T3)
- [ ] `QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs`: placement parity, ordering, default-empty (91-07-T2)
- [ ] `QuestBoard.IntegrationTests/Tests/CalendarDungeonMasterSessionTests.cs`: D-20 on the own calendar (91-02-T1)
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs`: D-18 #1–#6 on desktop, D-20 linked, Campaign, legend (91-05-T1, extended by 91-05-T2 and 91-07-T1/T2)
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs`: D-11/D-14/D-15/D-16 on mobile (91-08-T1)
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardQuestDetailsTests.cs`: D-12 / D-18 #7 (91-09-T1)
- [ ] `QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs`: D-05 / D-18 #8 / D-03 (91-03-T1, extended by 91-03-T2 and 91-06)
- [ ] `QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs`: D-08 chain (91-07-T3)
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs`: linked partials contain no form controls or vote markers; linked model types expose no vote/signup/player members; no CrossBoardLink prefix; no planning references (91-09-T3)
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs`: Platform POSTs to the four surfaces to unlink (91-10-T1)
- No framework install needed (xUnit / FluentAssertions / NSubstitute already present).

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Migration applies and the windowed linked-read predicates/projections translate on SQL Server | Schema, D-17 | EF InMemory cannot prove SQL translation; SQL Server is not provisioned by the agent | 91-10-T2 step 1: user runs the app against local SQL Server (`localhost:1433`); the migration auto-applies; links two boards on Platform → Linked Boards; loads `/calendar` and a Quest Details page on both; no exception in the logs. Fallback if translation fails: drop the window predicates (91-10 recorded choice 1, from 91-04 recorded choice 2) |
| Visual treatment of linked chips on desktop and mobile | D-14, D-15 | Visual judgement | 91-10-T2 steps 2 and 4: desktop chips muted and dashed, with board, DM and time on hover or focus; mobile rows keep the board name as text; own entries unchanged |
| Legend / "Also showing" with 1 and 4+ linked boards; Platform mobile at 390px | UI-SPEC backstops | Held-out visual checks | 91-10-T2 step 3 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
