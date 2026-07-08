---
phase: 52
slug: add-dead-status-to-characterstatus-enum
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-06
---

# Phase 52 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 3.2.2 |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (standard xUnit project, no custom config) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterStatus_CastRoundTrips"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds (quick filter), ~3-5 minutes (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterStatus_CastRoundTrips"`
- **After every plan wave:** Run `dotnet test` (full suite — this is a small, single-wave phase)
- **Before `/gsd:verify-work`:** Full suite must be green; manual browser verification of D-03 (toggle button hidden) and D-04 (badge rendering) since no automated Razor-view-rendering harness exists in this codebase
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 52-01-01 | 01 | 1 | D-01/D-02 (Dead selectable in Create/Edit dropdowns) | — | N/A | manual/smoke | — (existing `Html.GetEnumSelectList` mechanism auto-covers) | N/A | ⬜ pending |
| 52-01-02 | 01 | 1 | Enum cast integrity — `(int)Dead` round-trips through AutoMapper cast | — | N/A | unit (automatic) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterStatus_CastRoundTrips"` | ✅ | ⬜ pending |
| 52-01-03 | 01 | 1 | D-03 — toggle button hidden when `Status == Dead` | — | N/A | manual/smoke | Browser: view a Dead character's Details page, confirm no Retire/Reactivate button renders | ❌ W0 | ⬜ pending |
| 52-01-04 | 01 | 1 | D-04 — Dead badge (`bg-dark`/`fa-skull`) renders correctly across Details, Details.Mobile, Index, Index.Mobile; `character-dead` class visually distinct from Retired | — | N/A | manual/smoke | Browser: view Dead character across all 4 views | ❌ W0 | ⬜ pending |
| 52-01-05 | 01 | 1 | D-05 — sort order unchanged (Dead falls in "not Active" bucket) | — | N/A | none needed | Not applicable — no existing test asserts sort order; explicitly "no code change" decision | N/A | ⬜ pending |
| 52-01-06 | 01 | 1 | Quest signup eligibility — Dead characters excluded | — | N/A | none needed | Not applicable — no existing test asserts this filter; explicitly "no code change" decision, confirmed via grep | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] No new test files needed — `EntityProfileEnumCastTests.cs` covers the enum addition dynamically via `Enum.GetValues<CharacterStatus>()`.
- [ ] No new test infrastructure needed — D-03/D-04 are Razor view conditionals with no existing MVC integration test pattern in this codebase; manual/smoke verification is the recommended approach per research (do not build new integration test infrastructure for a single small phase).

*Existing infrastructure covers all automatable phase behavior. The two Wave 0 gaps (D-03, D-04) are intentionally left as manual/smoke verification — building new Razor-view-rendering test infrastructure is out of proportion to this phase's scope.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Toggle button hidden when character is Dead | D-03 | No existing integration test harness for Razor view conditional rendering in this codebase | 1. Set a character's Status to Dead via Edit form. 2. Open Details page (desktop + mobile). 3. Confirm no "Retire Character"/"Reactivate Character" button renders. 4. Confirm Edit and Delete actions still render. |
| Dead badge rendering | D-04 | Same as above — no view-rendering test infrastructure | 1. View a Dead character on Details.cshtml, Details.Mobile.cshtml, Index.cshtml, Index.Mobile.cshtml. 2. Confirm dark badge (`bg-dark`) with skull icon (`fa-skull`) renders with text "Dead". 3. Confirm it is visually distinct from the Retired badge (gray + moon icon) at a glance. 4. Confirm card/row dimming (`character-dead` / mobile row `dead` class) applies. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (task 52-01-02 provides automated coverage between the manual-only D-03/D-04 checks)
- [x] Wave 0 covers all MISSING references (documented as intentional manual/smoke scope, not a gap)
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
