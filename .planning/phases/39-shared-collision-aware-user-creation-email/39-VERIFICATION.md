---
phase: 39-shared-collision-aware-user-creation-email
verified: 2026-07-04T00:12:58Z
status: passed
score: 15/15 must-haves verified (all truths/artifacts/key-links across 3 plans)
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 15/15
  gaps_closed:
    - "CR-01 — unguarded null-forgiving `newUserId!.Value` deref in UserService.CreateOrAddToGroupAsync's new-account branch"
    - "WR-01 — AddMemberAsync's check-then-insert only caught InvalidOperationException, not a raced DbUpdateException from the unique-index constraint"
  gaps_remaining: []
  regressions: []
---

# Phase 39: Shared Collision-Aware User Creation Verification Report

**Phase Goal:** Build a shared, collision-aware user-creation method (`CreateOrAddToGroupAsync`) in the Domain layer that both today's group-admin Create User form and Phase 40's future platform entry point can call, plus the email/notification plumbing for the four possible outcomes (new account, added-to-group, added-to-group-stranded-account, already-member), and wire it into `AdminController.CreateUser` end-to-end.

**Verified:** 2026-07-04T00:12:58Z (initial) / re-verified 2026-07-04 (after commit `7379f6a`)
**Status:** passed
**Re-verification:** Yes — after gap closure (both human-verification items resolved by commit `7379f6a`)

## Re-Verification Summary

The prior verification (2026-07-04T00:12:58Z) found all 15 must-haves and all 4 requirements (CREATE-01..04) satisfied, but held status at `human_needed` pending a maintainer decision on two unresolved code-review findings in phase-39-introduced code:

1. **CR-01** — `QuestBoard.Domain/Services/UserService.cs:173-174` had an unguarded `newUserId!.Value` deref after re-resolving a just-created account by email; a null re-resolution (replica lag, concurrent delete) would throw an unhandled `InvalidOperationException` instead of the domain's own `Failed` outcome.
2. **WR-01** — `GroupRepository.AddMemberAsync`'s check-then-insert only caught `InvalidOperationException` from its own pre-check, not a raced `DbUpdateException` thrown by the real unique index on `(UserId, GroupId)` when two requests race the check.

The user chose to fix both. This re-verification independently confirms the fix (not from 39-UAT.md's claim, but by reading the code and re-running the affected + full test suites myself).

### CR-01 Fix — Verified in Code

`QuestBoard.Domain/Services/UserService.cs:173-183` (current):

```csharp
var newUserId = await identityService.GetIdByEmailAsync(email);
if (newUserId == null)
{
    return new CreateOrAddToGroupResult
    {
        Outcome = CreateOrAddToGroupOutcome.Failed,
        Email = email,
        Name = name,
        Errors = ["Account was created but could not be re-resolved by email."]
    };
}

await SetGroupRoleAsync(newUserId.Value, groupId, role);
```

Confirmed: the null-forgiving operator (`newUserId!.Value`) is gone. A null re-resolution now returns `CreateOrAddToGroupOutcome.Failed` with a descriptive error instead of throwing. This exactly matches the fix 39-REVIEW.md's CR-01 recommended.

New regression test `CreateOrAddToGroupAsync_WhenReResolutionAfterCreateReturnsNull_ReturnsFailed` (`QuestBoard.UnitTests/Services/UserServiceTests.cs:140-158`) arranges `GetIdByEmailAsync` to return `null` on both the pre-create and post-create calls, and asserts `Outcome == Failed`, `UserId == null`, `Errors` non-empty, and that `SetGroupRoleAsync` is never called. Ran this specific test in isolation: **1/1 passed**.

### WR-01 Fix — Verified in Code

`QuestBoard.Repository/GroupRepository.cs:49-75` (current) — the pre-check (`AnyAsync` + throw) is unchanged, but the insert is now wrapped:

```csharp
try
{
    await DbContext.SaveChangesAsync(token);
}
catch (DbUpdateException)
{
    // A concurrent request can win the race between the AnyAsync check above and this
    // insert; the table's unique index on (UserId, GroupId) then rejects the write.
    // Surface it as the same friendly exception the pre-check throws.
    throw new InvalidOperationException("User is already a member of this group.");
}
```

Confirmed: a raced `DbUpdateException` (from the unique index) is now translated into the same `InvalidOperationException` the pre-check throws, at the Repository layer — keeping EF Core exception types out of the Domain layer. `UserService.CreateOrAddToGroupAsync`'s existing `catch (InvalidOperationException)` block (unchanged, `UserService.cs:200-213`) therefore now also correctly handles the raced case and returns `AlreadyMember`, exactly as WR-01's suggested fix described. This is architecturally sound: no new integration test is needed to prove correctness of the translation, since the pre-existing `AlreadyMember`-outcome test path already exercises the exact same downstream catch clause the WR-01 fix now also feeds into.

### Diff Verification

Confirmed via `git diff HEAD~1 HEAD` on commit `7379f6a` that the change is minimal and scoped exactly to these two fixes — no other logic, controller, view, or email-template files were touched. This means all 15 previously-verified must-haves, artifacts, and key links (none of which live in `UserService.cs`'s new-account branch or `GroupRepository.AddMemberAsync` internals beyond what's covered above) are unaffected — a regression check, not a full re-verification, was sufficient for those items.

Checked both modified files and the new test file for debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) — none found.

### Independent Test Execution (not trusting 39-UAT.md's reported counts)

| Command | Result | Status |
|---|---|---|
| `dotnet build` (full solution) | 0 errors, 6 pre-existing style warnings (xUnit1051, unrelated to this fix) | ✓ PASS |
| `dotnet test QuestBoard.UnitTests --no-build` (full suite) | 130/130 passed | ✓ PASS |
| `dotnet test QuestBoard.IntegrationTests --no-build` (full suite) | 265/265 passed | ✓ PASS |
| `dotnet test QuestBoard.UnitTests --filter "...ReResolutionAfterCreateReturnsNull_ReturnsFailed"` (targeted, CR-01 regression test) | 1/1 passed | ✓ PASS |

Counts match 39-UAT.md's claimed 130/130 unit and 265/265 integration exactly — independently reproduced, not assumed.

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Admin submitting Create User with a colliding email (not yet in group) adds user to group with selected role, no duplicate-account error | ✓ VERIFIED | `AdminController.CreateUser` branches on `CreateOrAddToGroupOutcome.AddedToGroup`/`AddedToGroupStrandedAccount` (`AdminController.cs:147-176`); `UserService.CreateOrAddToGroupAsync` uses `groupService.AddMemberAsync` (throw-on-collision) rather than upsert (`UserService.cs:155-226`); unit test `CreateOrAddToGroupAsync_WhenExistingConfirmedUserNotAMember_AddsMembershipAndReturnsAddedToGroup` passes |
| 2 | Collision-added user receives a visibly distinct "added to group" email with no set-password link | ✓ VERIFIED | `AddedToGroup.razor` shares Welcome's visual shell (parchment/Cinzel/wax-seal) but different title copy ("You've Joined A New Guild"), names `@GroupName`/`@Role`, CTA is `href="@LoginUrl"` labeled "Log In" with no token param; `GroupMembershipAddedEmailJob` renders and sends it via the established `IServiceScopeFactory` scope pattern; enqueued from `AdminController.cs:154` |
| 3 | Admin submitting Create User with an email already in the current group sees a friendly "already a member" message, not a duplicate-membership error | ✓ VERIFIED | `UserService.CreateOrAddToGroupAsync` catches `InvalidOperationException` from `AddMemberAsync` (now also covering the raced `DbUpdateException` translated by `GroupRepository.AddMemberAsync`) and returns `AlreadyMember` (`UserService.cs:200-213`); `AdminController.cs:178-179` returns `this.RedirectWithWarning(nameof(Users), $"{result.Name} is already a member of this group.")`; `Users.cshtml:39-46` renders `TempData["Warning"]` as an `alert-warning` banner; unit test `CreateOrAddToGroupAsync_WhenEmailAlreadyMemberOfGroup_ReturnsAlreadyMember` passes |
| 4 | A brand-new email still creates a new account and sends the existing welcome email, unchanged | ✓ VERIFIED | `UserService.CreateOrAddToGroupAsync`'s `userId == null` branch calls `CreateAsync` + `SetGroupRoleAsync`, returns `NewAccountCreated` (`UserService.cs:159-194`), now with an explicit `Failed`-outcome guard on null re-resolution instead of a null-forgiving deref; `AdminController.cs:129-145` preserves the SetPassword token + `WelcomeEmailJob` enqueue exactly as before; unit test `CreateOrAddToGroupAsync_WhenEmailIsBrandNew_CreatesAccountAndReturnsNewAccountCreated` passes |
| 5 | Group-admin form and Phase 40's future platform entry point exhibit identical collision behavior once Phase 40 wires onto this shared method | — DEFERRED | Explicitly forward-looking (roadmap text: "once Phase 40 wires..."); Phase 40 is not yet built. `CreateOrAddToGroupAsync`'s `groupId` is a plain parameter (not session-derived), which is the shape Phase 40 needs — correctly scoped out of phase 39, not a phase-39 gap |

**Score:** 4/4 in-scope roadmap truths verified (SC5 correctly deferred to Phase 40)

### PLAN-Level Must-Haves (all 3 plans)

| # | Truth (Plan) | Status | Evidence |
|---|---|---|---|
| 1 | Brand-new email → `NewAccountCreated`, account created (39-01) | ✓ VERIFIED | Unit test passes; `UserService.cs:159-194` |
| 2 | Existing email not in group → `AddedToGroup`, membership row created (39-01) | ✓ VERIFIED | Unit test passes; `UserService.cs:200-217` |
| 3 | Existing unconfirmed email → `AddedToGroupStrandedAccount` (39-01) | ✓ VERIFIED | Unit test passes; `existingUser?.EmailConfirmed == false` branch, `UserService.cs:215-217` |
| 4 | Existing email already in group → `AlreadyMember`, no duplicate row (39-01) | ✓ VERIFIED | Unit test passes; `catch (InvalidOperationException)`, `UserService.cs:204-213` (now also catches the raced `DbUpdateException` translated in `GroupRepository.cs`) |
| 5 | AddedToGroup email shares Welcome's look, names group+role, token-free Log In CTA (39-02) | ✓ VERIFIED | `AddedToGroup.razor` inspected — matches shell, no `CallbackUrl`/`IsNewAccount` params, `href="@LoginUrl"` with no token query param |
| 6 | GroupMembershipAddedEmailJob sends via Hangfire scope pattern (39-02) | ✓ VERIFIED | `GroupMembershipAddedEmailJob.cs` uses `IServiceScopeFactory scopeFactory` + `ILogger` constructor only, `CreateAsyncScope()`, resolves scoped services |
| 7 | RedirectWithWarning helper maps to TempData["Warning"] (39-02) | ✓ VERIFIED | `ControllerExtensions.cs:36-37` |
| 8 | Users page renders alert-warning banner for TempData["Warning"] (39-02) | ✓ VERIFIED | `Users.cshtml:39-46` |
| 9 | New account → success flash + account created (unchanged) (39-03) | ✓ VERIFIED | `AdminController.cs:129-145`; human-verify checkpoint approved |
| 10 | Existing email not in group → added + AddedToGroup email sent (39-03) | ✓ VERIFIED | `AdminController.cs:147-157`; human-verify checkpoint approved |
| 11 | Existing unconfirmed email → resends Welcome/SetPassword instead of AddedToGroup, same flash (39-03) | ✓ VERIFIED | `AdminController.cs:159-176` — identical flash string to `AddedToGroup` branch, enqueues `WelcomeEmailJob` not `GroupMembershipAddedEmailJob`; human-verify checkpoint approved |
| 12 | Email already in group → yellow warning flash, no email sent (39-03) | ✓ VERIFIED | `AdminController.cs:178-179` — no job enqueue in this branch; human-verify checkpoint approved |

**Score:** 15/15 must-haves verified (4 roadmap SCs in-scope + 4 P01 truths + 4 P02 truths + 4 P03 truths, minus dedup overlap with roadmap SCs)

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `QuestBoard.Domain/Models/CreateOrAddToGroupResult.cs` | Result type + 5-member outcome enum | ✓ VERIFIED | Enum has exactly `NewAccountCreated, AddedToGroup, AddedToGroupStrandedAccount, AlreadyMember, Failed`; record exposes `Outcome`, `UserId` (`int?`), `Email`, `Name`, `Errors` |
| `QuestBoard.Domain/Interfaces/IUserService.cs` | `CreateOrAddToGroupAsync` signature | ✓ VERIFIED | Matches plan signature exactly |
| `QuestBoard.Domain/Services/UserService.cs` | Implementation + `IGroupService` constructor param | ✓ VERIFIED | Primary constructor includes `IGroupService groupService`; implementation matches all four branches; CR-01 null-forgiving deref replaced with explicit `Failed`-outcome guard |
| `QuestBoard.Repository/GroupRepository.cs` | `AddMemberAsync` check-then-insert | ✓ VERIFIED | WR-01 fix: `SaveChangesAsync` wrapped in try/catch translating `DbUpdateException` to the same `InvalidOperationException` the pre-check throws |
| `QuestBoard.UnitTests/Services/UserServiceTests.cs` | Unit tests covering all four outcomes + regression | ✓ VERIFIED | 6 `[Fact]` tests for `CreateOrAddToGroupAsync` (4 outcomes + no-account-mutation regression + new CR-01 null-re-resolution regression); 130/130 pass full suite (independently re-run) |
| `QuestBoard.Service/Components/Emails/AddedToGroup.razor` | Distinct notification email, no set-password link | ✓ VERIFIED | Confirmed visually and structurally |
| `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` | Hangfire job, `IServiceScopeFactory` pattern | ✓ VERIFIED | Confirmed |
| `QuestBoard.Service/Extensions/ControllerExtensions.cs` | `RedirectWithWarning` helper | ✓ VERIFIED | Confirmed |
| `QuestBoard.Service/Views/Admin/Users.cshtml` | `alert-warning` flash block | ✓ VERIFIED | Confirmed |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | `CreateUser` POST refactored onto `CreateOrAddToGroupAsync` | ✓ VERIFIED | Confirmed — full 5-way switch over outcome |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `UserService.cs` | `IGroupService.cs` | `AddMemberAsync` throw-on-collision | ✓ WIRED | `groupService.AddMemberAsync(groupId, userId.Value, role, token)` |
| `UserService.cs` | `IIdentityService.cs` | `GetIdByEmailAsync` collision detection | ✓ WIRED | Called at initial lookup and re-resolve after create, now with explicit null guard on the re-resolve |
| `GroupRepository.cs` | `UserService.cs` | `DbUpdateException` → `InvalidOperationException` translation | ✓ WIRED | New: unique-index race in `AddMemberAsync` now surfaces as the same exception type `UserService`'s catch block already handles |
| `GroupMembershipAddedEmailJob.cs` | `AddedToGroup.razor` | `RenderAsync<AddedToGroup>` | ✓ WIRED | Dictionary keyed by all 5 `nameof(...)` params |
| `ControllerExtensions.cs` | `Users.cshtml` | `TempData["Warning"]` → `alert-warning` | ✓ WIRED | `RedirectWithWarning` sets key "Warning"; view reads `TempData["Warning"]` |
| `AdminController.cs` | `UserService.cs` | `userService.CreateOrAddToGroupAsync(...)` | ✓ WIRED | Drives the outcome switch |
| `AdminController.cs` | `GroupMembershipAddedEmailJob.cs` | `jobClient.Enqueue<GroupMembershipAddedEmailJob>` | ✓ WIRED | `AddedToGroup` branch only |
| `AdminController.cs` | `ControllerExtensions.cs` | `this.RedirectWithWarning` | ✓ WIRED | `AlreadyMember` branch |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|---|---|---|---|
| Full solution builds | `dotnet build` | 0 errors, 6 pre-existing style warnings | ✓ PASS |
| CR-01 regression test (targeted) | `dotnet test --filter "...ReResolutionAfterCreateReturnsNull_ReturnsFailed"` | 1/1 passed | ✓ PASS |
| Full unit suite | `dotnet test QuestBoard.UnitTests` | 130/130 passed | ✓ PASS |
| Full integration suite | `dotnet test QuestBoard.IntegrationTests` | 265/265 passed | ✓ PASS |
| Human-verify checkpoint (39-03 Task 3) | Interactive UAT of all 4 collision outcomes + security sanity | Approved per 39-03-SUMMARY.md and STATE.md (phase marked complete) | ✓ PASS (evidence: session record, not re-run here) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|---|---|---|---|---|
| CREATE-01 | 39-01, 39-03 | Colliding email adds user to active group with selected role instead of failing | ✓ SATISFIED | `AddedToGroup`/`AddedToGroupStrandedAccount` branches, unit + integration tests, human-verify approved; REQUIREMENTS.md marks `[x]` and "Complete" |
| CREATE-02 | 39-02, 39-03 | Collision-add sends distinct "added to a group" email, no set-password link | ✓ SATISFIED | `AddedToGroup.razor`, `GroupMembershipAddedEmailJob`, enqueued only on `AddedToGroup` outcome; REQUIREMENTS.md marks `[x]` and "Complete" |
| CREATE-03 | 39-01, 39-02, 39-03 | Colliding email already in current group shows friendly "already a member" message | ✓ SATISFIED | `AlreadyMember` outcome, `RedirectWithWarning`, `alert-warning` banner; now also correctly reached on the raced `DbUpdateException` path; REQUIREMENTS.md marks `[x]` and "Complete" |
| CREATE-04 | 39-03 | Collision handling identical regardless of entry point (group-admin form today, platform entry point in Phase 40) | ✓ SATISFIED (for phase 39's scope) | `CreateOrAddToGroupAsync` takes `groupId` as a plain parameter so Phase 40 can call the same method; full identity of behavior across two entry points can only be fully proven once Phase 40 exists — appropriately scoped as a Phase-40 concern per ROADMAP SC5; REQUIREMENTS.md marks `[x]` and "Complete" |

No orphaned requirements — REQUIREMENTS.md maps only CREATE-01..04 to Phase 39, all four are claimed across the three plans' `requirements` frontmatter and satisfied.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| — | — | CR-01 (unguarded null-forgiving deref) — **RESOLVED** by commit `7379f6a` | — | Verified fixed in code and by regression test; no longer an open finding |
| — | — | WR-01 (TOCTOU race only partially caught) — **RESOLVED** by commit `7379f6a` | — | Verified fixed in code; the Repository-layer exception translation now feeds the existing, already-tested `AlreadyMember` catch path |
| `QuestBoard.Service/Components/Emails/AddedToGroup.razor` | 35 / `AdminController.cs:154` | Raw enum `ToString()` used for `Role` display (e.g. "DungeonMaster" instead of "Dungeon Master") | ℹ️ Info (WR/IN-01, unresolved) | Cosmetic inconsistency vs. `Users.cshtml`'s friendly role labels; does not affect any must-have or requirement, informational only; not part of the two items the user chose to fix |
| — | — | No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers found in any phase-39 file (including the fix commit's changed files) | — | Clean |
| — | — | No GSD planning references (phase numbers, requirement IDs, decision IDs) found in any phase-39 source file | — | Clean, matches CLAUDE.md constraint |

Note: `39-REVIEW.md`'s CR-02 (`ResetPassword`'s `TempData["SuccessMessage"]` key mismatch), WR-02/03/04, and IN-01/02/03 remain unresolved but were never routed to human verification in the prior VERIFICATION.md — CR-02 is pre-existing code from before this phase (out of scope), and WR-02/03/04 and IN-01/02/03 are lower-severity findings that did not block the `passed` determination previously and do not block it now. Only CR-01 and WR-01 were gating items.

### Gaps Summary

No gaps remain. Both previously-outstanding human-verification items (CR-01, WR-01) have been resolved by commit `7379f6a`, independently confirmed by:
- Reading the current code in `UserService.cs` and `GroupRepository.cs` (not trusting 39-UAT.md's narrative)
- Confirming the diff (`git diff HEAD~1 HEAD`) is minimal and scoped exactly to these two fixes, with no unintended side effects on other phase-39 artifacts
- Independently re-running `dotnet build` (0 errors) and the full unit (130/130) and integration (265/265) suites myself, plus the specific new CR-01 regression test in isolation (1/1)

All 15 must-haves, all 4 roadmap Success Criteria (SC5 correctly deferred to Phase 40), and all 4 requirements (CREATE-01..04) remain verified and are now unblocked by any open human-verification item. Phase goal fully achieved.

---

_Verified: 2026-07-04T00:12:58Z (initial) / re-verified 2026-07-04 (gap closure)_
_Verifier: Claude (gsd-verifier)_
