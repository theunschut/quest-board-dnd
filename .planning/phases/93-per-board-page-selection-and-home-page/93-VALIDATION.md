---
phase: "93"
slug: "per-board-page-selection-and-home-page"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-21"
---

# Phase 93 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, net10.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial); unit tests: none |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPage|FullyQualifiedName~Architecture"` |
| **Full suite command** | `dotnet test` (repo root, both test projects) |
| **Estimated runtime** | ~25 seconds quick slice; full suite several minutes |

---

## Sampling Rate

- **After every task commit:** Run the quick command plus the task's own `--filter` slice
- **After every plan wave:** Run `dotnet test` (both projects)
- **Before `/gsd-verify-work`:** Full suite must be green, plus the static checks below
- **Max feedback latency:** ~60 seconds per task slice

---

## Per-Task Verification Map

Filled in by the planner from the test map in `93-RESEARCH.md` § Validation Architecture. Every
decision D-01…D-21 maps to at least one automated command there.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 93-01-T1 | 01 | 1 | D-05, D-11, D-12, D-18, D-19 | T-93-01, T-93-02, T-93-03 | Tracer: Shop off refused with toast and lands on the stored home; migration defaults 0/0 | integration + migrator | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate"` and `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator"` | created in task | ⬜ pending |
| 93-01-T2 | 01 | 1 | D-01, D-06, D-09, D-18 | T-93-01, T-93-04, T-93-06 | Campaign ceiling; SuperAdmin refused; default board unchanged; unresolved passes; Edit POST keeps the link | integration + architecture | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate\|FullyQualifiedName~LayoutNavigation\|FullyQualifiedName~GroupManagement\|FullyQualifiedName~LinkedBoardsPlatform"` and `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator\|FullyQualifiedName~Architecture"` | extends T1 file | ⬜ pending |
| 93-02-T1 | 02 | 2 | D-02, D-05 | T-93-45 | Calendar, Events (availability overview) and Series refused when off | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate"` | created in task | ⬜ pending |
| 93-02-T2 | 02 | 2 | D-02, D-05, D-07, D-19 | T-93-45, T-93-10, T-93-12 | Quest list, Quest Log and admin quests refused; UpdateSignupCharacter needs Characters | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate\|FullyQualifiedName~QuestUpdateSignupCharacter"` | extends T1 file | ⬜ pending |
| 93-03-T1 | 03 | 2 | D-18 | T-93-15 | Allows/Serves null semantics; Campaign ceiling through the helpers | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPageAvailabilityExtensions"` | created in task | ⬜ pending |
| 93-03-T2 | 03 | 2 | D-07, D-11, D-18, D-21 | T-93-14, T-93-15 | Menus and My Agenda's button hide off pages on both UAs; brand to home; Quests item only when home elsewhere; unresolved unchanged | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageNavigation\|FullyQualifiedName~LayoutNavigation"` | created in task | ⬜ pending |
| 93-04-T1 | 04 | 2 | D-08 | T-93-17 | Cross-board counts incl. cancelled and open series, scalar only | unit (InMemory) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPageCountQuery\|FullyQualifiedName~CrossBoardIgnoreQueryFilters"` | created in task | ⬜ pending |
| 93-04-T2 | 04 | 2 | D-01, D-08, D-10, D-13, D-15, D-19 | T-93-16, T-93-18 | Tamper-proof sanitising; home rule; Events refusal on transition only; board clock | unit (fake clock) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPageSettingsService\|FullyQualifiedName~Architecture"` | created in task | ⬜ pending |
| 93-05-T1 | 05 | 2 | D-04, D-07 | T-93-20, T-93-22 | CanOpen from membership and owning board's availability | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedCalendar"` | extends existing | ⬜ pending |
| 93-05-T2 | 05 | 2 | D-04, D-07, D-09 | T-93-21 | Linked entries inert on desktop grid and both mobile surfaces | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageLinkedCalendar\|FullyQualifiedName~LinkedBoard"` | created in task | ⬜ pending |
| 93-13-T1 | 13 | 2 | D-10, D-11 | T-93-49 | `/` per candidate; switched-off and disallowed homes fall back; anonymous and board-less unchanged | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageHome"` | created in task | ⬜ pending |
| 93-13-T2 | 13 | 2 | D-11, D-12 | T-93-13 | Picker fallback to home; return URL wins; per-board homes in a linked set; Account back link | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageHome\|FullyQualifiedName~GroupPicker"` | extends T1 file | ⬜ pending |
| 93-14-T1 | 14 | 2 | D-01, D-05, D-10, D-19 | T-93-47 | Type ceiling for all 512 masks; EffectiveHome invariant; route table; toast sentences | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPagePolicy\|FullyQualifiedName~BoardPageRoutes"` | created in task | ⬜ pending |
| 93-14-T2 | 14 | 2 | D-09, D-18 | T-93-48 | Enum casts; mask mapping and defaults; LinkedBoardSetId untouched | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~EntityProfileEnumCast\|FullyQualifiedName~BoardPageSettingsMapping"` | created in task | ⬜ pending |
| 93-15-T1 | 15 | 2 | D-16 | T-93-46 | Both board-list projections carry the stored home | unit (InMemory) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupRepository"` | extends existing | ⬜ pending |
| 93-15-T2 | 15 | 2 | D-16, D-18 | T-93-46 | Home badge only for a non-default home, on both UAs | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageBoardList"` | created in task | ⬜ pending |
| 93-06-T1 | 06 | 3 | D-01, D-08, D-09, D-13, D-15, D-17 | T-93-23..T-93-27 | Absent = unchanged; Campaign tamper ignored; refusals as form errors; link kept; concurrency handled | integration + unit | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPagePlatform\|FullyQualifiedName~GroupManagement\|FullyQualifiedName~LinkedBoardsPlatform"` and `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupControllerEditConcurrency"` | created in task | ⬜ pending |
| 93-06-T2 | 06 | 3 | D-10, D-14, D-15, D-19 | T-93-24 | Pages section on both Edit twins; greyed rows with reason; field names round-trip | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPagePlatform"` | extends T1 file | ⬜ pending |
| 93-07-T1 | 07 | 3 | D-07 | T-93-28, T-93-29 | Crafted character ids refused while Characters off | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageCharacterLinks"` | created in task | ⬜ pending |
| 93-07-T2 | 07 | 3 | D-07, D-09 | T-93-30 | Pickers, modal and script absent; names and portraits kept; DM names plain text | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageCharacterLinks\|FullyQualifiedName~MobileViews"` | extends T1 file | ⬜ pending |
| 93-08-T1 | 08 | 3 | D-07, D-09 | T-93-31, T-93-32 | No own events or runway with Events off; saves avoid an off Calendar | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageEventsCalendar\|FullyQualifiedName~Calendar"` | created in task | ⬜ pending |
| 93-08-T2 | 08 | 3 | D-07 | T-93-32 | The Calendar's Board Availability button and the event form Cancel links follow the switches | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageEventsCalendar\|FullyQualifiedName~CalendarButtonStyle"` | extends T1 file | ⬜ pending |
| 93-09-T1 | 09 | 3 | D-04, D-08 | T-93-34 | TargetPageOff before any write; control needs a taker | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardMove"` | created in task | ⬜ pending |
| 93-09-T2 | 09 | 3 | D-08, D-09 | T-93-34, T-93-36 | GET/POST refusals explained; GroupId unchanged; picker option disabled | integration (both UAs) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageMove\|FullyQualifiedName~BoardMove"` | created in task | ⬜ pending |
| 93-12-T1 | 12 | 3 | D-01, D-05, D-06, D-07 | T-93-07, T-93-09, T-93-11 | Campaign closure; four-role refusal; POST refused; DM pictures open | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate"` | extends plan 02 file | ⬜ pending |
| 93-12-T2 | 12 | 3 | D-02, D-07 | T-93-08, T-93-11 | Characters and Contacts refused; character pictures open; every action classified; pinned map; route invariant | reflection + integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageCoverage\|FullyQualifiedName~BoardPageGate"` | created in task | ⬜ pending |
| 93-10-T1 | 10 | 4 | D-03, D-13, D-19, D-20 | T-93-37, T-93-38 | Quests switchable; live-quest refusal with HasBeenPlayed parity | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPage\|FullyQualifiedName~Architecture"` | extends existing | ⬜ pending |
| 93-10-T2 | 10 | 4 | D-04, D-05, D-07, D-09, D-21 | T-93-39, T-93-40, T-93-41 | Every quest route redirects home; menus, calendar and DM history quest-free; restore on switch-back; feed files untouched | integration (both UAs) + git | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPage"` and `git diff --name-only c35512f3 HEAD -- <three feed files>` empty | created in task | ⬜ pending |
| 93-11-T1 | 11 | 5 | all (source hygiene) | T-93-42, T-93-43 | No planning references in phase source; production DI graph resolves | static + integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageStaticGuard\|FullyQualifiedName~BoardPageDependencyInjection"` | created in task | ⬜ pending |
| 93-11-T2 | 11 | 5 | D-05, D-07, D-09, D-10, D-11, D-14, D-15, D-16, D-18, D-21 | T-93-44 | Operator walk on both layouts through the Platform form; full suite green | integration (both UAs) + full suite | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageEndToEnd"` then `dotnet test` | created in task | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs` — set page settings on a group row (plan 01, before every later plan)
- [ ] `QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs` — the tracer and the gate boundaries (plan 01)
- [ ] `QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs`, `BoardPageRoutesTests.cs` — type matrix, Quest Log dependency, effective home chain and invariant, route table and toasts (plan 14)
- [ ] `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` (add the page enum) and `QuestBoard.UnitTests/Repository/BoardPageSettingsMappingTests.cs` (plan 14)
- [ ] `QuestBoard.UnitTests/Services/BoardPageAvailabilityExtensionsTests.cs` — Allows/Serves null semantics (plan 03)
- [ ] `QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs` — refusal counts on a fake `IBoardClock`, home-page refusal, sanitising, absent rows (plan 04)
- [ ] `QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs` (plans 02 and 12), `QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs` (reflection, plan 12), `BoardPageNavigationTests.cs` (both user agents, plan 03), `BoardPageHomeTests.cs` (plan 13), `BoardPageBoardListTests.cs` (plan 15), `BoardPagePlatformTests.cs` (plan 06), `BoardPageQuestsOffTests.cs` (plan 10)
- [ ] `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs` — no planning references in touched source (plan 11)
- [ ] DI-graph factory + test proving the real resolver resolves with the real `IBoardTypeResolver` (plan 11)
- [ ] Framework install: none

---

## Static Checks (phase gate)

- `git diff --name-only main...HEAD -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs` prints nothing
- `AmbientClockSeamTests` green with the new settings service on the board-clock consumer list
- `IgnoreQueryFilters_IsConfinedToTheAllowlist` green with the allowlist unchanged
- `AntiForgeryTokenCoverageTests` green

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Migration applies on a real SQL Server and existing boards look unchanged | D-18 | InMemory provider does not run migrations or enforce column defaults | Start the app against `localhost:1433`, confirm the migration applied, open an unconfigured OneShot and Campaign board and compare menus to before |
| Mobile layouts hide switched-off pages and show the Quests item | D-07, D-21 | Mobile views are chosen by a real mobile user agent; devtools emulation does not exercise them | Open a configured board on a phone (or a real mobile UA) and check the menu and Quest Details picker |
| A board set to open on Calendar lands there after login and via the brand link | D-10, D-11 | End-to-end login and board-picker flow on the real host | Set a board's home to Calendar in Platform, log out, log in, pick the board |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
