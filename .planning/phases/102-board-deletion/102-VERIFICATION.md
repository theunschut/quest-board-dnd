---
phase: 102-board-deletion
verified: 2026-11-11T18:00:00Z
status: human_needed
score: 11/11 automated must-haves verified (D-01..D-13 plus ROADMAP scope); manual UAT (plan 102-10 Task 3) pending
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Real delete on the user's own local SQL Server, desktop browser (plan 102-10 Task 3 steps 1-8)"
    expected: "Throwaway board with a quest+signup+vote, a character used by a signup on another board, an event series with an occurrence, a contact with image and tag, and a shop item bought and returned: board gone, no error page, success toast on the board list, survivor board keeps its quest signup (character cleared) and everything else, any board page sends the admin to the picker"
    why_human: "Needs the running app, a browser and the user's own data; automated SQL Server facts use a scratch database, not the app's QuestBoard database"
  - test: "/platform/Users Boards column on desktop (title on hover) and phone User-Agent (details list)"
    expected: "Counts and board names match the memberships; zero boards shows 0"
    why_human: "Hover title and touch details expansion are visual/interactive"
  - test: "Phone User-Agent at 390px: very long board name and a 40-name list (plan 102-10 Task 3 step 9)"
    expected: "No horizontal scroll, every name wraps, wrong name blocks submit"
    why_human: "Overflow/visual layout cannot be checked by grep or HTTP-level tests (backstop truth)"
  - test: "bfcache: Back after a cancelled or failed delete attempt"
    expected: "Delete button state matches the text in the input"
    why_human: "Browser back-forward cache behaviour; pageshow handler exists in platform-board-delete.js but cannot be exercised without a real browser (backstop truth)"
  - test: "No-JS path"
    expected: "Delete button never unlocks without JavaScript (rendered disabled)"
    why_human: "Needs a browser with scripting disabled; the disabled-by-default markup is present and covered by a test"
---

# Phase 102: Board deletion Verification Report

**Phase Goal:** A super admin can delete a board that no longer has any members, without a server error. Everything that belongs only to that board goes with it, and anything that must outlive the board (users, and whatever they own across boards) is unlinked instead of deleted. Plus: one transaction behind a confirmation page listing what will be removed, friendly error never a 500, quest/event writes through the change tracker, and a board-count column on /platform/Users.
**Verified:** 2026-11-11
**Status:** human_needed
**Re-verification:** No, initial verification

ROADMAP lists "Requirements: TBD", so the binding contract is D-01..D-13 in 102-CONTEXT.md plus the ROADMAP scope bullets.

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | D-01/D-02/D-03: all board-scoped content, characters with classes/images, and purchase history (incl. chained returns) are deleted with the board | VERIFIED | `GroupRepository.DeleteBoardAsync` (GroupRepository.cs:229) stages date votes, signups, proposed dates, reminder logs, quests, event signups, events, series, contact notes, contacts, categories, tags, character classes, characters, transactions (shop item board OR original-transaction shop item board), shop items, then the group. Image rows and tag join rows cascade at DB level (snapshot FKs confirmed Cascade). Images live in DB entities (CharacterImage/ContactImage), no disk files to clean. `GroupRepositoryBoardDeletionTests` and the SQL Server `BoardDeletionSqlServerTests` full-delete fact pass |
| 2  | D-04: users, DM profiles, trade items, calendar subscriptions untouched | VERIFIED | None are staged for removal in `DeleteBoardAsync`; unit test asserts survival (Group delete tests pass) |
| 3  | D-05: other-board follow-up `OriginalQuestId`, signup `CharacterId` and (defensive) event `SeriesId`/`SeriesSlotIndex` are cleared, rows kept | VERIFIED | `FollowUpsOnOtherBoards`, `OtherBoardSignupsUsingBoardCharacters`, `OtherBoardEventsInBoardSeries` set pointers null as tracked writes. I swept the model snapshot's non-cascade FKs independently: every NoAction/SetNull FK into a board-owned type is covered, and `BoardOwnedForeignKeysTests` pins this against future model drift |
| 4  | D-06: linked board set untouched, board's membership cleared | VERIFIED | Only `Groups` row is removed; snapshot FK Group -> LinkedBoardSet is SetNull; set never loaded or removed; covered by unit + SQL Server tests |
| 5  | D-07: confirmation page with per-type counts, collapsed `<details>` name lists, zero-count hidden, unlinked rows shown separately | VERIFIED | `GetDeletionPreviewAsync` built from the delete's own scoped helpers (names for named types, counts for others); `_BoardDeleteBody.cshtml` renders deleted rows, "Kept, but unlinked", "Not affected", `<details>` per named row; hides empty/`HasNothingStored` path; `PlatformBoardDelete` HTTP facts pass for desktop and phone UA |
| 6  | D-08: Delete disabled until the exact name typed; server rechecks on POST | VERIFIED (UI gate backstops pending, see human items) | Button rendered `disabled`; `platform-board-delete.js` enables on exact ordinal match after shared edge-whitespace trim (CR-01 fix); `GroupService.DeleteBoardAsync` compares ordinal and refuses `NameMismatch` before the repository; mismatch re-renders the page with a refusal line; tests pass |
| 7  | D-09: members rechecked at confirm time, refused with friendly message; content added since preview is simply deleted | VERIFIED | Early check plus an in-transaction (Serializable, relational only) re-check before `SaveChangesAsync` (WR-01 fix); `HasMembers` -> `TempData["Error"]` + redirect. Gated member-race SQL Server fact passes |
| 8  | D-10: both `Delete.cshtml` and `Delete.Mobile.cshtml` kept in converted Platform style via shared partial | VERIFIED | Both files exist and render `_BoardDeleteBody`; CSS `platform-group-delete(.mobile).css`; static guard tests (no inline style/script/handler) pass |
| 9  | D-11: single transaction, any failure rolls back with a friendly error never a 500; quest/event writes via change tracker | VERIFIED (one residual, see warning) | One `SaveChangesAsync` (EF wraps in one DB transaction) inside an explicit serializable transaction on relational providers; catch clears the tracker and rethrows; controller `catch (Exception)` sets `TempData["Error"]` and redirects. No `ExecuteDelete`/raw SQL in the path; FeedRevisionWriteSeamTests, IgnoreQueryFilters allowlist and Architecture tests are in the green 1389/1855 runs. Gated `AFailureInsideTheSave_RollsEveryRowBack` passes on real SQL Server |
| 10 | D-12/D-13: /platform/Users Boards column (count + names on hover/expand), desktop and phone, display only | VERIFIED (visual backstop pending) | `UsersController.Index` fills `BoardNames` from `GetBoardNamesByUserAsync` (UserRepository.cs:78, UserGroups join, sorted); desktop `<td title=...>` count with visually-hidden names; phone `<details class="user-boards">` list; 0 boards shows "0 boards"; no sort/filter added; `UserRepositoryBoardNamesTests` and `UsersControllerIntegrationTests` pass |
| 11 | Stale-board recovery (Claude's discretion item): session naming a deleted board falls back cleanly | VERIFIED | `GroupSessionMiddleware` now existence-checks the board for super admins and revalidates on every non-GET/HEAD request for members (WR-02 fix); deleting admin's own session cleared via `ClearActiveBoardIfAsync`, best-effort and logged on failure. Middleware revalidation tests pass |

**Score:** 11/11 automated truths verified. 0 present-but-behavior-unverified. The remaining items are the manual browser UAT.

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `QuestBoard.Repository/GroupRepository.cs` (DeleteBoardAsync, GetDeletionPreviewAsync) | VERIFIED | Substantive, wired via `IGroupRepository` -> `GroupService` -> `GroupController` |
| `QuestBoard.Domain/Services/GroupService.cs`, `Extensions/BoardNameText.cs`, `Models/BoardDeletion/*` | VERIFIED | Name check and result/preview models in use |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (Delete GET/POST) | VERIFIED | `[Authorize(Policy="SuperAdminOnly")]`, `[ValidateAntiForgeryToken]`, friendly-error switch |
| `Views/Group/Delete.cshtml`, `Delete.Mobile.cshtml`, `_BoardDeleteBody.cshtml` | VERIFIED | Both twins present, shared body |
| `wwwroot/js/platform-board-delete.js`, `platform-group-delete(.mobile).css` | VERIFIED | Loaded by the view; no inline script |
| `Views/Users/Index.cshtml`, `Index.Mobile.cshtml`, `PlatformUserViewModel` | VERIFIED | Boards column on both |
| Test files (unit: repository deletion, preview, FK pin, service, controller; integration: PlatformBoardDelete, SqlServer, static guard) | VERIFIED | Exist and pass |

### Key Link Verification

| From | To | Status |
|------|----|--------|
| `GroupController.DeleteConfirmed` | `IGroupService.DeleteBoardAsync` -> `IGroupRepository.DeleteBoardAsync` | WIRED |
| `GroupController.Delete` GET | `GetDeletionPreviewAsync` -> `BoardDeletePageViewModel.Create` -> view | WIRED |
| `UsersController.Index` | `IUserService.GetBoardNamesByUserAsync` -> `PlatformUserViewModel.BoardNames` -> both views | WIRED |
| Delete views | `platform-board-delete.js` + `data-board-delete-name` / `data-board-delete-submit` hooks | WIRED |
| Controller success | `ClearActiveBoardIfAsync` and `GroupSessionMiddleware` existence check | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| `_BoardDeleteBody` rows | `Model.DeletedRows/UnlinkedRows` | `GetDeletionPreviewAsync` DB queries (counts, name projections) | Yes | FLOWING |
| Users Boards column | `item.BoardNames` | `DbContext.UserGroups` join to Group name | Yes | FLOWING |

### Behavioral Spot-Checks (run by verifier)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build | `dotnet build` | 0 errors, 44 warnings | PASS |
| Unit suite | `dotnet test QuestBoard.UnitTests --no-build` | 1389 passed, 0 failed | PASS |
| Delete, preview, Users column and group-management facts incl. the SQL Server-gated tests against real SQL Server (`QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, scratch database) | `dotnet test QuestBoard.IntegrationTests --no-build --filter "BoardDeletion\|PlatformBoardDelete\|UsersControllerIntegration\|GroupManagement"` | 76 passed, 0 failed, 0 skipped | PASS |

The orchestrator's full-suite figures (1855 integration, 0 skipped) were not re-run in full by the verifier.

### Probe Execution

Step 7c: SKIPPED. No probe scripts are declared by the phase.

### Requirements Coverage

No requirement IDs (ROADMAP "TBD"). Decisions D-01..D-13: all SATISFIED per the truth table above. No orphaned requirements.

### Anti-Patterns Found

Scanned all phase source files for TBD/FIXME/XXX/TODO/HACK and for planning references in source (CLAUDE.md rule): none found. No stubs; no ExecuteDelete or raw SQL in the delete path.

### Warnings (non-blocking)

| Item | Severity | Detail |
|------|----------|--------|
| IN-05 (review, left open) | WARNING | `RenderNameMismatchAsync` runs `GetByIdAsync` and `GetDeletionPreviewAsync` outside the try/catch, so a database failure on the name-mismatch re-render path would surface as a 500. The delete itself and its failure path are covered by the friendly error; this is a read-only edge on a secondary path |
| IN-01 | INFO | The transaction sweep also removes a chained transaction whose own shop item lives on another board. This follows D-03 (chained rows go with the board) |
| IN-02, IN-03, IN-04 | INFO | Phone heading level skip, vacuous body assertion after a redirect, no test pinning anti-forgery on the POST (the attribute is present) |
| VALIDATION.md | INFO | Frontmatter still `status: draft`, `nyquist_compliant: false`; sign-off boxes unticked. Bookkeeping only, tests named in the map exist and pass |

### Human Verification Required

Plan 102-10 Task 3 has not been done. Treated as pending, not passed and not failed.

1. **Real delete on the user's SQL Server (desktop).** Follow the ten steps in 102-10-SUMMARY.md. Expected: board gone, no 500, toast on board list, survivor board intact with its signup's character cleared, board picker for any board URL. Why human: needs the running app and the user's DB; automated SQL Server facts use a scratch database.
2. **Users column, desktop hover and phone details list.** Expected: counts and names correct; zero shows 0. Why human: interactive/visual.
3. **Phone 390px overflow with a very long name and about 40 names.** Expected: no horizontal scroll, names wrap. Why human: visual layout (backstop truth).
4. **bfcache Back state.** Expected: button state matches the input after Back. Why human: browser behaviour (backstop truth).
5. **No-JS path.** Expected: Delete never unlocks. Why human: needs scripting disabled in a browser.

### Gaps Summary

No automated gaps. Every decision D-01..D-13 is implemented, wired and backed by passing tests, including the SQL Server-gated full-delete, rollback and member-race facts. The review's 1 critical and 4 warnings are fixed in code (commits fa180daf, e5a9a061, 507aedcc, d8c21d48, 4d629323). The phase is not signed off as complete until the user finishes the manual browser UAT above, hence `human_needed`.

---

_Verified: 2026-11-11_
_Verifier: Claude (gsd-verifier)_
