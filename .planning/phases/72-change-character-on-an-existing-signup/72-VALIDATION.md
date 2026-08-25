---
phase: 72
slug: change-character-on-an-existing-signup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-25
---

# Phase 72 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (no separate `xunit.runner.json`) |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UpdateSignupCharacter"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick ~15–30 s · full suite ~2–4 min |

**Build note:** if `dotnet build`/`dotnet test` fails on locked output files, Visual Studio is running the app under the debugger — stop it (Shift+F5) before retrying (CLAUDE.md).

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~UpdateSignupCharacter"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds (quick filter)

---

## Per-Task Verification Map

> Populated by `gsd-planner` once PLAN.md task IDs exist. Every task must map to an automated command or an explicit Wave 0 dependency.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| _pending planner_ | — | — | SIGNCHAR-01…07 | T-72-* | — | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UpdateSignupCharacter"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Requirement → Test Map (from RESEARCH.md)

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SIGNCHAR-01 | POST with new `characterId` updates the finalized-table signup's character | integration | `dotnet test --filter "FullyQualifiedName~UpdateSignupCharacter"` | ❌ Wave 0 |
| SIGNCHAR-02 | Same, verified via mobile User-Agent request | integration | same filter, with the `MobileUserAgent` header per `MobileViewsTests.cs` | ❌ Wave 0 |
| SIGNCHAR-03 | POST with no character selected clears `CharacterId` to `null` **in the database** | integration | same filter; assert `context.PlayerSignups...CharacterId.Should().BeNull()` | ❌ Wave 0 |
| SIGNCHAR-04 | Retired/Dead character shown as current selection; unchanged on a no-op save | integration + manual | `dotnet test --filter "FullyQualifiedName~RetiredCharacter"` for persistence; UAT for the rendered `selected` option | ❌ Wave 0 |
| SIGNCHAR-05 | Works after finalization (no `IsFinalized` guard) | integration | reuse `CreateTestQuestAsync(..., isFinalized: true, finalizedDate: ...)` from `QuestJoinFinalizedQuestTests.cs` | ❌ Wave 0 |
| SIGNCHAR-06 | Works for waitlisted signups, all 3 roles | integration | parametrized over `signupRole` (0/1/2) and `isSelected: false` via `CreatePlayerSignupAsync` | ❌ Wave 0 |
| SIGNCHAR-07 | Cross-user (same group) rejected; cross-group rejected | integration | `dotnet test --filter "FullyQualifiedName~CrossGroup\|FullyQualifiedName~AnotherUser"` | ❌ Wave 0 |

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` — **new file**; no existing test covers `UpdateSignupCharacter` at all today. Model fixture usage on `QuestJoinFinalizedQuestTests.cs` (same controller, adjacent action; already uses `TestDataHelper.CreateTestQuestAsync` / `CreatePlayerSignupAsync` / `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync`).
- [ ] Cross-group case — model on `TenantIsolationTests.cs`'s `factory.TestGroupContext.ActiveGroupId` mutable-singleton pattern (**not** `SeedCampaignGroupAsync` + real membership). Seed the character under `GroupId=2` via `factory.Database.CreateContext()` (bypasses the query filter for writes), then issue the request scoped to `ActiveGroupId=1`. This is the only pattern in this codebase actually used to test the query-filter boundary.
- [ ] No new fixture helper needed — `TestDataHelper.CreateTestCharacterAsync(..., int groupId = 1)` already accepts `groupId` and `status`; reuse it for both the cross-group and the Retired/Dead fixtures.
- [ ] Framework install: **none** — everything is already referenced in `QuestBoard.IntegrationTests.csproj`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Pencil trigger renders in the desktop finalized-participants **and** waitlist character cells at the same `btn-sm` size as the green `+` | SIGNCHAR-01 | Visual placement/sizing is not assertable from markup alone | Open a quest Details page as a player with an existing signup; confirm the pencil sits in the same cell, immediately after name + avatar, in both tables |
| Mobile pencil renders inline on the second line and does **not** increase row height | SIGNCHAR-02 | Row-height regression is visual | Load Details with a **real mobile User-Agent** (not devtools emulation — `MobileDetectionMiddleware` matches on the UA string); compare row height before/after |
| Retired/Dead character appears as the pre-selected, status-labelled option in the dropdown | SIGNCHAR-04 | The integration test proves persistence; the rendered `selected` option is a view concern | Open the change control on a signup holding a Retired character; confirm the dropdown shows it selected with its status suffix |
| `confirm()` guard fires before the Remove action | SIGNCHAR-03 | Native browser dialog | Click Remove character; confirm the dialog appears and Cancel aborts without a POST |
| Success toast appears after both swap and clear on desktop and mobile | SIGNCHAR-01/03 | `_Toasts.cshtml` rendering is layout-level | Perform a swap and a clear on each platform; confirm the toast shows |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
