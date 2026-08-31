---
phase: 75-event-availability-signups
verified: 2026-08-28T00:00:00Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 75: Event Availability Signups Verification Report

**Phase Goal:** Players can say whether they are available for an event, with the right default for the board type — opt-in on One-Shot boards, opt-out on Campaign boards.
**Verified:** 2026-08-28
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | On a One-Shot board, no signup exists for an event until a player creates one, and they can record Yes/Maybe/No (EVTAVAIL-01) | ✓ VERIFIED | `EventsController.Create` calls plain `eventService.AddAsync(newEvent, token)` (no fan-out) whenever `boardType != BoardType.Campaign` (`EventsController.cs:92-95`). `EventSignupRepository.SetAvailabilityAsync` creates a row only on a player's post (`EventSignupRepository.cs:13-46`). Behavioral test `EventsControllerIntegrationTests#SetAvailability_OneShot_NoExistingRow_CreatesRowForActingUser` passes (ran `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` → 61/61 pass, includes this fact). |
| 2 | On a Campaign board, every member has a Yes signup on each event from the moment it exists, and opting out flips the answer to No rather than deleting the row (EVTAVAIL-02) | ✓ VERIFIED | `EventsController.Create` branches to `eventService.AddWithCampaignFanOutAsync(newEvent, members.Select(m => m.Id).ToList(), token)` when `boardType == BoardType.Campaign` (`EventsController.cs:83-91`), using the role-agnostic `GetAllGroupMembersAsync`. `EventRepository.AddWithCampaignFanOutAsync` stages one `EventSignupEntity` per member with `Availability = Yes` and no `UpdatedAt`, in exactly one `SaveChangesAsync` (`EventRepository.cs:48-71`). `SetAvailabilityAsync` mutates the existing row in place (no delete) when a row already exists. Behavioral tests `Create_CampaignBoard_AutoSignsUpEveryMember_WithNullAnsweredTimestamp` and `SetAvailability_CampaignBoard_OptOut_FlipsAutoRowToNo_WithoutDeletingIt` both pass. |
| 3 | A player can change their own availability at any time, and cannot change anyone else's (EVTAVAIL-03) | ✓ VERIFIED | `SetAvailability` resolves the acting user only from `userService.GetUserAsync(User)` and passes `currentUser.Id` to `SetAvailabilityAsync(id, currentUser.Id, ...)` — no user/member/signup id is read from route or form (`EventsController.cs:214-258`). No date comparison exists in `SetAvailability`/`Withdraw`, so past-dated events remain changeable. Behavioral tests `SetAvailability_Ownership_EachMemberChangesOnlyTheirOwnRow`, `SetAvailability_Ownership_SpoofedUserIdField_OnlyChangesActingUsersRow`, and `SetAvailability_PastDatedEvent_AcceptsChangedAnswer` all pass. |
| 4 | A member joining a Campaign board is auto-signed-up to every event dated today or later; a member leaving has all event signups on that board removed, past and future (EVTAVAIL-04) | ✓ VERIFIED | `GroupRepository.AddMemberAsync` reads `GroupEntity.BoardType` by the `groupId` argument (not the caller's active board) and, for a Campaign board, backfills a Yes row for every event with `Date >= today` via `GetFutureEventIdsForGroupIgnoringActiveBoardAsync`, staged in the same `SaveChangesAsync` as the membership row (`GroupRepository.cs:49-107`). `RemoveMemberAsync` deletes every signup the member holds on that board (no date filter) via `GetEventSignupsForMemberIgnoringActiveBoardAsync`, in the same save as the membership removal (`GroupRepository.cs:110-126`, confirmed no `Date` comparison in the method body). Named test run: `dotnet test --filter "FullyQualifiedName~GroupRepositoryTests"` → 9/9 pass, including atomicity, cross-board, and cross-member isolation facts. |
| 5 | An integration test using two distinct groups proves a player can neither read nor write availability on another board's event (EVTAVAIL-05) | ✓ VERIFIED | `EventsController.EventIsOnActiveBoard` is a second explicit `GroupId` comparison layered over the read-side query filter, checked before every write (`EventsController.cs:210-215, 250-253, 276-279`). Named test run: `dotnet test --filter "FullyQualifiedName~EventAvailabilityTenantIsolationTests"` → 5/5 pass — read refusal, write refusal (with DB state re-checked through an unfiltered context), withdraw refusal, no-active-board refusal, and roster cross-board isolation. |
| 6 | The distinction between a deliberate answer and a backfilled default is preserved via a derived "has answered" flag from `UpdatedAt` | ✓ VERIFIED | `EventSignup.HasAnswered => UpdatedAt != null` (`EventSignup.cs:25`). Automatic rows (campaign backfill on join, create-time fan-out) never set `UpdatedAt`; every player-initiated write in `SetAvailabilityAsync` stamps `UpdatedAt = DateTime.UtcNow` on both the creating write and later changes. Named test run: `EventSignupRepositoryTests` (9/9 pass) includes the automatic-pass-row fact asserting `HasAnswered` is false until `SetAvailabilityAsync` touches it, then true. |
| 7 | The two production bugs found by plan 75-05 (disconnected EF relationship causing `EventId=0` on campaign fan-out rows; missing `ModelState.IsValid` check letting an invalid `VoteType` silently bind to `No`) are actually fixed in shipped code | ✓ VERIFIED | `QuestBoardContext.cs:292-296`: `modelBuilder.Entity<EventSignupEntity>().HasOne(es => es.Event).WithMany(e => e.Signups)...` — points at the real `EventEntity.Signups` navigation (was a bare `WithMany()`). `EventsController.SetAvailability` (`EventsController.cs:222-231`) checks `if (!ModelState.IsValid) return BadRequest(...)` as the first statement in the action body, before any event lookup. Both fixes are exercised by passing tests in the full suite. |
| 8 | CR-01 (code review Critical): the Remove-Member confirmation dialog's broken HTML-entity apostrophe, which silently defeated the only warning before an irreversible destructive delete, is fixed | ✓ VERIFIED | Both `Members.cshtml:77` and `Members.Mobile.cshtml:66` now read `onsubmit="return confirm('Remove this member from the group? Their availability answers for events on this board will be deleted and cannot be recovered.');"` — the apostrophe that triggered the `&#39;`-decoding JS syntax break was removed entirely rather than re-escaped, and the wording is byte-identical between the two files (confirmed via commit `62cfa06`). |

**Score:** 8/8 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Models/EventSignup.cs` | Domain model with `HasAnswered` | ✓ VERIFIED | Exists, contains `HasAnswered => UpdatedAt != null` and `Availability` as `VoteType` |
| `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` / `IEventSignupService.cs` | Three-method contract | ✓ VERIFIED | `SetAvailabilityAsync`, `WithdrawAsync`, `GetRosterForEventAsync` present on both |
| `QuestBoard.Repository/EventSignupRepository.cs` | Narrow scalar-update implementation | ✓ VERIFIED | Data-tier `AnyAsync` existence probe before insert; single `SaveChangesAsync` per method; no `Mapper.Map(model, entity)` in-place form used |
| `QuestBoard.Repository/GroupRepository.cs` | Atomic backfill on join, cleanup on leave | ✓ VERIFIED | Both `AddMemberAsync`/`RemoveMemberAsync` stage signup mutations on the same `DbContext` as the membership row, one `SaveChangesAsync` each; board type resolved from the `groupId` argument, not the caller's active board |
| `QuestBoard.Repository/EventRepository.cs` (`AddWithCampaignFanOutAsync`) | Atomic event + fan-out insert | ✓ VERIFIED | One `SaveChangesAsync` for the whole graph; `memberIds.Distinct()`; no answered-marker set on automatic rows |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` (`SetAvailability`, `Withdraw`) | Write actions with two-layer board defence | ✓ VERIFIED | `EventIsOnActiveBoard` explicit second check; `ModelState.IsValid` check present; acting user from `User` only |
| `QuestBoard.Service/Views/Events/Details.cshtml` | Answer buttons, withdraw control, roster, delete confirmation | ✓ VERIFIED | `IsOneShotBoard`/`HasOwnSignup`/`MyAvailability`/`Roster`/`SignupCount` all rendered and wired to `setAvailability`/`withdrawAvailability` fetch calls |
| `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` | Two-group isolation proof | ✓ VERIFIED | Exists, 5 facts, all pass |
| `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs` | Backfill/cleanup atomicity proof | ✓ VERIFIED | Exists, 9 facts, all pass |
| `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` | `UpdatedAt` stamping proof | ✓ VERIFIED | Exists, 9 facts, all pass |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `EventsController.SetAvailability`/`Withdraw` | `User` (authenticated principal) | `userService.GetUserAsync(User)` | ✓ WIRED | No user/member/signup id read from route or form (confirmed by direct read of both action bodies) |
| `EventsController` | Active board | `EventIsOnActiveBoard(existingEvent)` | ✓ WIRED | Explicit `GroupId` comparison invoked before every write, independent of the read-side query filter |
| `GroupRepository.AddMemberAsync`/`RemoveMemberAsync` | `EventSignups` table | Single-`DbContext` staged mutation + one `SaveChangesAsync` | ✓ WIRED | Confirmed via `sed`-scoped grep: exactly one `SaveChangesAsync` per method |
| `EventRepository.AddWithCampaignFanOutAsync` | `EventEntity.Signups` navigation | `HasOne(es => es.Event).WithMany(e => e.Signups)` | ✓ WIRED | Confirmed fixed relationship in `QuestBoardContext.cs`; this was the root cause of the `EventId=0` production bug, now correctly pointed at the real navigation |
| `Details.cshtml` buttons/withdraw/roster | `EventsController` write actions | `setAvailability()`/`withdrawAvailability()` fetch calls | ✓ WIRED | Both scripts present, carrying `__RequestVerificationToken` |
| `Members.cshtml`/`Members.Mobile.cshtml` remove form | Browser `confirm()` | `onsubmit="return confirm(...)"` | ✓ WIRED (post-fix) | CR-01 fix confirmed present in both files, identical wording, no HTML-entity apostrophe |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution suite green | `dotnet test` | 333 unit + 496 integration passing, 0 failed | ✓ PASS |
| One-shot/campaign lifecycle facts | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | 61/61 pass | ✓ PASS |
| Cross-board isolation facts | `dotnet test --filter "FullyQualifiedName~EventAvailabilityTenantIsolationTests"` | 5/5 pass | ✓ PASS |
| Membership backfill/cleanup facts | `dotnet test --filter "FullyQualifiedName~GroupRepositoryTests"` | 9/9 pass | ✓ PASS |
| `UpdatedAt` stamping / `HasAnswered` facts | `dotnet test --filter "FullyQualifiedName~EventSignupRepositoryTests"` | 9/9 pass | ✓ PASS |
| Debt-marker scan (TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER) on all 22 phase-touched files | `grep -nE` per file | No matches in any file | ✓ PASS |
| Git working tree clean, all phase commits present | `git status --porcelain` | Clean | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|-----------------|-------------|--------|----------|
| EVTAVAIL-01 | 75-01, 75-03, 75-04, 75-05 | One-Shot opt-in, no signup until answered | ✓ SATISFIED | Code + `EventsControllerIntegrationTests` one-shot facts |
| EVTAVAIL-02 | 75-01, 75-02, 75-03, 75-04, 75-05 | Campaign opt-out via flip, not delete | ✓ SATISFIED | Code + campaign lifecycle facts |
| EVTAVAIL-03 | 75-01, 75-03, 75-04, 75-05 | Player can change own answer any time, not another's | ✓ SATISFIED | Code + ownership facts (including spoofed-field fact) |
| EVTAVAIL-04 | 75-02, 75-05 | Join backfills, leave removes all | ✓ SATISFIED | Code + `GroupRepositoryTests` |
| EVTAVAIL-05 | 75-05 | Two-group cross-board isolation | ✓ SATISFIED (code/tests) — ⚠️ **not reflected in `.planning/REQUIREMENTS.md`**, still shows `[ ]` unchecked | `EventAvailabilityTenantIsolationTests` (5/5 pass); `git log -p -- .planning/REQUIREMENTS.md` shows 75-04's commit `99fd791` checked EVTAVAIL-01/03 but no later commit checked EVTAVAIL-05 even after 75-05 completed and its own SUMMARY frontmatter claims `requirements-completed: [..., EVTAVAIL-05]` |

**Note on requirement attribution:** the task brief stated EVTAVAIL-01/03 were marked complete by plan 75-04 and EVTAVAIL-02/04 by plan 75-02. Git history confirms this exactly (commits `99fd791` and `d4831f6`). However, every plan's own frontmatter (`75-01` through `75-05`) declares overlapping `requirements-completed` lists, which is expected — a requirement typically spans the data tier (75-01), the membership sync (75-02), the controller (75-03), the view (75-04), and the automated proof (75-05). All five requirements are independently justified by real, passing code as itemized above regardless of which commit ticked the REQUIREMENTS.md checkbox.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No debt markers (TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER) found in any of the 22 files this phase touched | — | None — clean |
| `QuestBoard.Repository/EventSignupRepository.cs:17-21` (WR-01, open) | 17-21 | `SetAvailabilityAsync` re-probes existence and throws unhandled `ArgumentException` on a delete/write race; caller has no try/catch | Warning (pre-existing review finding, not a blocker) | Low-probability race (event deleted between controller read and this write) surfaces as an unhandled exception / ugly error page instead of a clean 404 |
| `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs:6`, `IEventSignupService.cs:6` (WR-03, open) | 6 | Interfaces inherit unguarded generic `AddAsync`/`UpdateAsync` from `IBaseRepository`/`IBaseService` that bypass the board check `SetAvailabilityAsync` performs | Warning (pre-existing review finding, not a blocker) | Not exploited today (nothing calls the generic methods), but the surface invites future misuse |
| `.planning/REQUIREMENTS.md` | 45 | EVTAVAIL-05 checkbox left unchecked despite being fully implemented and tested | Warning (documentation drift) | Traceability document is stale relative to delivered code; does not affect runtime behavior |

No Critical or Blocker-severity findings remain open. The one Critical finding from `75-REVIEW.md` (CR-01, broken confirmation dialog) was verified fixed in commit `62cfa06` (see Observable Truth #8). WR-02 (stale entity comment) and IN-01/IN-02 (informational) from the review are cosmetic/documentation items with no functional impact and were not re-verified here since the review already correctly scoped them as non-blocking.

### Human Verification Required

### 1. Availability buttons and roster render correctly on a real mobile device

**Test:** Open an event's details page on a real mobile device / real mobile User-Agent (not devtools emulation).
**Expected:** The three answer buttons, the withdraw control (when applicable), and the roster table are usable and the layout does not break.
**Why human:** `Events/Details.cshtml` has no `.Mobile` variant — one view serves both platforms. `75-VALIDATION.md` notes devtools emulation has previously masked a live case of mobile markup never being selected; this is a rendering/UX judgment call that cannot be settled by grep.

### 2. Both confirmation dialogs read correctly in a real browser

**Test:** Delete an event with signups; remove a member from the Platform group page, in an actual browser (not a headless test harness).
**Expected:** Each native `confirm()` dialog pops up and clearly names what will be lost (signup count / availability answers).
**Why human:** Native `confirm()` text and behavior is not assertable through the integration test harness. Code inspection confirms the CR-01 JavaScript-syntax-breaking bug is fixed (the apostrophe was removed, wording is identical in both files), which substantially de-risks this item, but an end-to-end browser check is still the only way to confirm the dialog actually renders as intended.

### 3. Answering a past-dated event reads as "correcting the record," not as a bug

**Test:** As a player, open an event dated well in the past and change your answer; as a Dungeon Master, confirm this reads as intentional record-correction rather than a bug that should have been blocked.
**Expected:** The product experience feels like correcting who actually attended, not like a validation hole.
**Why human:** This is a judgment call about product intent (planner decision PD-01), not a fact a passing test can settle. The automated facts already prove the code permits the behavior (`SetAvailability_PastDatedEvent_AcceptsChangedAnswer`, `Withdraw_PastDatedOneShotEvent_Succeeds`); what's open is whether that's the *right* UX, not whether it works.

### Gaps Summary

No blocking gaps. All five EVTAVAIL requirements are independently justified by real, passing code and tests, verified directly against the codebase rather than trusted from SUMMARY.md claims. Both production bugs plan 75-05 found (the disconnected `EventSignupEntity.Event`/`EventEntity.Signups` relationship producing `EventId=0`, and the missing `ModelState.IsValid` check) are confirmed fixed in the current code. The one Critical code-review finding (CR-01, the broken member-removal confirmation dialog) is confirmed fixed in commit `62cfa06`, with identical, HTML-entity-free wording in both the desktop and mobile views. The full solution test suite is green (333 unit + 496 integration, 0 failures), matching the phase's own reporting exactly.

The only non-blocking items are: (1) three manual-only verification items already anticipated by `75-VALIDATION.md` (mobile rendering, in-browser dialog confirmation, and a product-intent judgment call on past-date answerability) that inherently cannot be settled by static analysis; (2) two open code-review Warnings (WR-01 unhandled race exception, WR-03 unguarded generic interface surface) that are real but low-severity and were already correctly triaged as non-blocking by the code review; and (3) a documentation-only gap where `.planning/REQUIREMENTS.md` never checked off EVTAVAIL-05 even though the requirement is fully delivered and tested — worth a follow-up commit to keep the traceability document honest, but not a gap in the delivered feature.

---

_Verified: 2026-08-28_
_Verifier: Claude (gsd-verifier)_
