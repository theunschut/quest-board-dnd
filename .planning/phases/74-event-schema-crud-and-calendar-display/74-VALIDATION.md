---
phase: 74
slug: event-schema-crud-and-calendar-display
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-26
---

# Phase 74 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions v8.10.0 + NSubstitute v5.3.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Event"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds (quick) / ~5 minutes (full) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Event"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

> Populated by the planner/executor once PLAN.md task IDs exist. Requirement→test
> mapping below is lifted from `74-RESEARCH.md` § Validation Architecture and is the
> authoritative source for which test covers which requirement.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | EVENT-01 | — | DM creates event with title/description/date/optional start time | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests.Create"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVENT-02 | T-74-01 (cross-tenant write) | Edit/delete scoped to own board; never cross-board | integration | `dotnet test --filter "FullyQualifiedName~EventTenantIsolationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVENT-03 | — | Desktop calendar renders events visually distinct from quests | integration (HTML assertion) | `dotnet test --filter "FullyQualifiedName~CalendarControllerIntegrationTests"` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | EVENT-04 | — | Mobile calendar renders event-only days | integration (real mobile UA) | `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | EVENT-05 | — | Quest creation unaffected by any event on the chosen date | integration (negative assertion) | `dotnet test --filter "FullyQualifiedName~QuestControllerIntegrationTests_Comprehensive"` | ✅ extend / ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVENT-06 | T-74-02 (authz bypass) | "Create Event" navbar entry visible to DM roles only | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | D-09 | — | `Quest/Details` renders zero event markup even with a same-day event | integration | new `EventCalendarPartialTests.cs`-style file | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` — stubs for EVENT-01, EVENT-02
- [ ] `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` (or extend `TenantIsolationTests.cs`) — two distinct groups, covers EVENT-02 cross-board clause
- [ ] D-09 structural-protection test — asserts no event markup on `Quest/Details` with a same-day event
- [ ] EVENT-05 negative-assertion test — quest creation provably unaffected by existing events
- [ ] Framework install: **none** — xUnit / FluentAssertions / NSubstitute / EF InMemory all already referenced

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Event is "visually distinguishable from a quest at a glance" | EVENT-03 | Subjective visual judgement; the automated test can assert distinct CSS classes/markup but not perceptual distinctness | Open `/Calendar` on a board with both a quest and an event on the same day; confirm the two are distinguishable without reading the text |
| Calendar cell does not clip stacked content | EVENT-03 | `calendar.css` `grid-auto-rows: 120px` + `overflow: hidden` clips silently — no DOM assertion catches it | Load a day with 1 event + 3 quests at 1280px and 1920px widths; confirm nothing is cut off |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
