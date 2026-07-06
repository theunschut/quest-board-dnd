---
phase: 58
slug: rename-the-guild-members-feature-to-characters-everywhere-co
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-06
---

# Phase 58 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`TestContext.Current.CancellationToken` usage pattern), `Microsoft.AspNetCore.Mvc.Testing` |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Characters\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~LayoutNavigationTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick, scoped filter), full suite varies |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Characters|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~LayoutNavigationTests"`
- **After every plan wave:** Run `dotnet test` (full suite — catches any cross-project reference RESEARCH.md's sweep missed)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 90 seconds

**Note:** This is a rename phase with existing (not new) test coverage. Production-code renames and their corresponding test-string updates MUST land in the same atomic task/commit — RESEARCH.md's Pitfall 1 (controller/views-folder coupling) and Pitfall 2 (`MobileViewsTests.cs` gap) both produce a red/404 suite if a rename and its test update are split across separate commits.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 58-01-01 | 01 | 1 | D-01 | — | `characters.css`/`characters.mobile.css` exist with renamed classes; no `guild-*` class remains | build/grep | `grep -rn "guild-" QuestBoard.Service/wwwroot/css/` (expect 0 hits) | ✅ | ⬜ pending |
| 58-02-01 | 02 | 1 | D-02 | — | `PlayersController` still renders its Index page correctly after `GuildMembersIndexViewModel` → `PlayersIndexViewModel` rename | integration | `dotnet test --filter "FullyQualifiedName~PlayersController"` (or nearest existing Players coverage) | ⚠️ verify exists | ⬜ pending |
| 58-03-01 | 03 | 2 | D-01 | V4 Access Control (regression) | `/Characters` Index renders and lists all characters (formerly `/GuildMembers`) | integration | `dotnet test --filter Index_ShouldReturnCharactersPage` | ✅ (renamed from existing) | ⬜ pending |
| 58-03-02 | 03 | 2 | D-01 | V4 Access Control (regression) | Admin/SuperAdmin can still Edit another player's character; Player still cannot (Phase 56 guard survives rename) | integration | `dotnet test --filter "FullyQualifiedName~CharactersControllerIntegrationTests"` | ✅ (renamed from existing, all 17 methods) | ⬜ pending |
| 58-04-01 | 04 | 3 | D-01 | — | 6 `Url.Action(..., "GuildMembers", ...)` cross-references (QuestLog×2, Quest/Details×2, Quest/Manage×1, Quest/_QuestCard×1) all resolve to `"Characters"` — profile picture images load, not broken | manual/visual | Load a Quest details page + Quest Log with a signed-up character; confirm avatar renders (Wave 0 gap — no existing automated test asserts this) | ❌ W0 | ⬜ pending |
| 58-05-01 | 05 | 3 | D-01 | — | Nav shows "Characters" (formerly "Guild Members") link for authenticated users | integration | `dotnet test --filter Nav_CampaignAuthenticated_CharactersLinkPresent` | ✅ (renamed from existing) | ⬜ pending |
| 58-06-01 | 06 | 4 | D-01 | — | Mobile UA renders correct list rows / detail card / form card + correct CSS `<link>` on `/Characters/*` routes | integration | `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` | ✅ (renamed from existing, 4 methods) | ⬜ pending |
| 58-07-01 | 07 | 4 | D-01, D-02, D-03 | — | Final sweep: zero remaining "guild" (case-insensitive) references in `QuestBoard.Service/` and `QuestBoard.IntegrationTests/` (excluding `.planning/`, `README.md`) | grep | `grep -ril "guild" --include="*.cs" --include="*.cshtml" --include="*.css" QuestBoard.Service/ QuestBoard.IntegrationTests/` (expect 0 hits) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] **Cross-reference visual check** (task 58-04-01) — no existing automated test asserts that `Url.Action("GetProfilePicture", "Characters", ...)` cross-references from Quest/QuestLog views (including the shared `_QuestCard.cshtml` partial) resolve to a working image URL post-rename. RESEARCH.md flags this as a genuine Wave 0 gap: either a manual/visual check during execution, or a new lightweight assertion added to `CharactersControllerIntegrationTests.cs` that a Quest/QuestLog page containing a signed-up character's avatar renders a non-broken `<img>` src.
- [ ] **Verify `PlayersController` test coverage exists** (task 58-02-01) — RESEARCH.md did not confirm whether any existing integration test exercises `PlayersController.Index` at all; if none exists, this is a pre-existing gap (not introduced by this phase) and the rename's correctness for that page falls back to manual/visual verification only.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Cross-controller profile picture links resolve after rename (Quest/QuestLog/`_QuestCard` avatar images) | D-01 | No dedicated automated test currently asserts on this cross-reference chain (RESEARCH.md Wave 0 Gap) | Run the app locally, open a Quest with signed-up characters on both the Quest board and Quest Log, confirm character avatar thumbnails render (not broken-image icons) |
| `PlayersController.Index` still renders correctly after `GuildMembersIndexViewModel` → `PlayersIndexViewModel` rename | D-02 | Existing test coverage for this controller is unconfirmed by RESEARCH.md | Run the app locally, navigate to `/Players`, confirm the DM/Player roster still lists correctly with no error |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
