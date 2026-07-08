---
phase: 57
slug: add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-06
---

# Phase 57 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`QuestBoard.UnitTests`, `QuestBoard.IntegrationTests` projects) |
| **Config file** | Standard `.csproj`-based xUnit setup — no separate config file |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60-120 seconds (full suite, including integration tests against a real SQL Server/InMemory provider) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests`
- **After every plan wave:** Run `dotnet test` (full suite, including `QuestBoard.IntegrationTests`)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

This phase has no `REQUIREMENTS.md` mapping (ad-hoc backlog phase, per `.planning/PROJECT.md`'s established pattern for Phases 47-56/58) — decisions from `57-CONTEXT.md` stand in for REQ-IDs.

| Task ID | Plan | Wave | Decision | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| 57-01-* | 01 | 1 | D-09b | Access Control | `[Authorize(Policy="DungeonMasterOnly")]` blocks Player from Create/Edit/Delete | integration | `dotnet test --filter FullyQualifiedName~ContactsControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-12/D-13 | Information Disclosure | Hidden Contact 404s for unauthorized viewers, filtered from Index | integration | same as above | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-14 | — | New Contact defaults `IsRevealed = false` | unit | `dotnet test --filter FullyQualifiedName~ContactServiceTests` or `ContactRepositoryTests` | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-15 | Information Disclosure | Creator-always-sees-own-hidden + toggle precedence (3 branches) | integration | same integration suite, one `[Fact]` per branch | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-15b | Session Management | Toggle is per-group, session-scoped, resets on session expiry | integration | same integration suite | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-09 | — | Any group member can edit/delete any note (no ownership guard) | integration | same integration suite | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-10 | — | Notes display newest first | unit or integration | `ContactRepositoryTests` (mirrors `CharacterRepositoryTests.cs` ordering assertions) | ❌ W0 | ⬜ pending |
| 57-01-* | 01 | 1 | D-06 | Tampering / DoS | Image upload validation (type/size) rejects non-JPG/PNG/GIF and >5MB | unit/integration | reuse `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` (verbatim reuse — no new attribute logic to test), controller-level reject-on-bad-input integration test | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Exact Task IDs are assigned by the planner once PLAN.md files exist — this table will be updated to reference real `{padded_phase}-{plan}-{task}` IDs during planning.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` — mirrors `CharacterRepositoryTests.cs`; covers note ordering (D-10), group-scoping, image-profile round-trip
- [ ] `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — mirrors `CharactersControllerIntegrationTests.cs`; covers the D-09b/D-12/D-13/D-15/D-15b role-and-visibility matrix (highest-value test file in this phase, given the 3-branch visibility logic)
- No new test framework or fixture install needed — `WebApplicationFactoryBase` (used by all existing controller integration tests) already provides the harness this phase's controller tests need.

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Full mobile parity (Index/Details/Edit/Create.Mobile.cshtml) renders correctly | D-18 | Visual/layout correctness across 4 new mobile views isn't meaningfully assertable via automated test — mirrors this app's standing practice of human-verifying new mobile views | Load each Contacts page on a mobile viewport (devtools emulation acceptable per this app's precedent for non-touch-gesture UI; no drag/pinch/camera-orientation concerns here unlike the crop-UI phase), confirm modern-card layout, Show Hidden toggle, and notes section render and behave as on desktop |
| "Show Hidden" toggle session-reset behavior | D-15b | Session-expiry timing is impractical to assert deterministically in a fast integration test without mocking the distributed session provider's clock | Manually toggle Show Hidden ON for a group, expire/clear the session (or start a fresh login), confirm the toggle reads as OFF again |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
