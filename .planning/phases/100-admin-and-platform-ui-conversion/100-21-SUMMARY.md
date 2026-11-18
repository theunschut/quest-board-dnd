---
phase: 100-admin-and-platform-ui-conversion
plan: 21
subsystem: testing
tags: [guards, census, scope, retired-sheets, writes-check, untouched-paths, test-inventory]
requires:
  - phase: 100-13
    provides: the area guard lists and scanners in AdminPlatformConversionGuardTests
  - phase: 100-20
    provides: the last converted view (Admin/EmailStats), PendingViews empty
provides:
  - The area's census from the tree, zero beside the starting figures (counts-after.json)
  - PhaseScope_IsFullyConverted, ReproductionFacts_AreAllEnabled, AreaControllers_UseNoDeleteVerb, each with a scanner self-test
  - A complete PhaseSourceFiles list and the four page sheets proven on the layered lists
  - Proof tools in the scratch root: writes-check.py, untouched.py (rewritten for this phase), findings-after.py, tests-compare.py
affects: [100-22, 100-23]
tech-stack:
  added: []
  patterns: [a closing guard is a fact plus a scanner function with a self-test on seeded strings, so an empty list cannot pass for a working scan; the phase's writes are checked by a script that maps every added write line to its enclosing method and compares with the register]
key-files:
  created: []
  modified:
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs
    - .planning/phases/100-admin-and-platform-ui-conversion/deferred-items.md
key-decisions:
  - "The census was already zero when the plan started, so no file was changed to fix a count"
  - "The plan's test-inventory one-liner splits names on whitespace and cannot read a theory row with a quoted argument, so the comparison is done line by line in tools/tests-compare.py"
  - "The Service project's design-time EF tooling package is older than the phase; the promise checked is that no EF package was ADDED outside the Repository project (the project files are unchanged)"
  - "GroupMembersViewModel.CreateMember is not removed: both Members views still take the dialog's labels from it through DisplayNameFor"
  - "Group/Delete #board-delete-help (4.25:1) is not fixed here: the closing plan adds no look change; it goes to the Gate 2 review"
requirements-completed: [D-04, D-12, D-13, D-21, DoC-1, DoC-2, DoC-3, DoC-8, DoC-9, DoC-10]
duration: ~35m
completed: 2026-11-18
status: complete
actuals:
  tokens: 2500
  tasks: 3
  commits: 3
---

# Phase 100 Plan 21: Cleanup and guards Summary

**The conversion's static promises are now passing guards: the area's census is zero across 29 views and 7 sheets, every view is converted, the six old phone sheets are gone and pinned gone, no reproduction fact is skipped, no test was lost, the phase's only writes are the two registered ones, and the four protected paths are byte-identical to the baseline.**

## Census (counts-after.json, from the tree)

`python3 tools/counts.py --area admin-platform` reads every view under `Views/Admin` and `Areas/Platform/Views/{Group,Users,LinkedBoards}` plus the three shared badge partials (29 files; 26 at the phase start) and every area sheet on disk (7; 9 at the start, six of them since deleted). It fails on a sheet that is on disk but not in its list.

| Metric | Start, nine sheets (measured) | Start, ROADMAP six-sheet figures | End |
|--------|------|------|-----|
| inline `style=` | 1 | 1 | 0 |
| `<style>` elements | 0 | 0 | 0 |
| inline scripts | 4 | 4 | 0 |
| on-event handlers | 12 | 12 | 0 |
| hex literals | 6 | 6 | 0 |
| `rgb()` literals | 57 | 57 | 0 |
| `!important` | 28 | 28 | 0 |
| utility-colour rules | 5 | 5 | 0 |
| infinite animations | 0 | 0 | 0 |

The three sheets added at the start (`platform-group-delete*.css`, `platform-users-boards.mobile.css`) were already clean, so the nine-sheet and six-sheet figures are equal. All seven end sheets open `@layer pages {`. The census was zero when the plan began: no file needed a fix, and no earlier slice missed an inline line.

## What changed
- **`PhaseScope_IsFullyConverted`** (AdminPlatformConversionGuardTests): `PendingViews` and `PendingSheets` are empty; every area view on disk (at least 29) is in `ConvertedViews` and no entry is foreign; each of the six retired phone sheets (`admin-form`, `admin-quests`, `admin-users`, `platform-group`, `platform-linked-boards`, `platform-users` `.mobile.css`) is in `RetiredFiles` and absent; every `admin-*.css` and `platform-*.css` on disk is in `AreaSheets`.
- **`ReproductionFacts_AreAllEnabled`**: the five test files that held a fact skipped with the reproduction reason (AdminAccountReachTests, AdminRoleGuardTests, AdminQuestDeleteTests, AdminControllerRateLimitIntegrationTests, GroupManagementIntegrationTests) no longer carry it. The reason is built from two string pieces so the guard never holds it; a self-test fires on a seeded skip and stays quiet on a skip for another reason and on a mention outside an attribute. Skips held for other reasons are left alone.
- **`AreaControllers_UseNoDeleteVerb`**: the Admin controller and the three Platform controllers carry no `HttpDelete` (comments stripped first); a self-test covers the bare, templated and bracket-mixed shapes. `DeleteUser` and `DeleteQuest` are `[HttpPost] [ValidateAntiForgeryToken]` (checked by `untouched.py`).
- **`PhaseSourceFiles`** gained the two phase-edited test files it lacked: `BoardPageGateMatrixTests.cs` and `LinkedBoardStaticGuardTests.cs`. Checked against `git diff --name-only --diff-filter=ACMR` from the baseline over the five source and test projects: nothing else was missing.
- **`LayeredAndNoLiteralFiles_HoldTheAdminAndPlatformSheets_AndNoRetiredPhoneSheet`** (UiFoundationLayerGuardTests): the four page sheets are on both lists and none of the six retired names is.
- **Open item handled:** `LinkedBoardStaticGuardTests.FeatureSourceFiles` named the deleted `platform-linked-boards.mobile.css` (inert, the scan keeps only files that exist). It now names the successor `platform-pages.mobile.css`, so the planning-reference scan reaches it; the file's own scanner regex had to split the word its scanner forbids so the phase guard could list the file (no behaviour change; rename recorded in tests-renamed.md).
- **Build warning:** one analyzer warning (xUnit1051) had been introduced in the refused-email-change fact by an earlier plan; the reload now passes the test cancellation token (73 build warnings, none in a file the phase touched; the rest are package-advisory and older test-file warnings).

## Tools (scratch root, outside the repository)
- `counts.py --area admin-platform --out counts-after.json` (re-pointed; start figures kept beside the end figures).
- `findings-after.py` writes `findings` into counts-after.json; `untouched.py` writes `untouched` (rewritten: the phase 98 version protected the whole Domain project, which this phase legitimately changed; the old file is kept as `untouched.phase98.py.bak`). Run order: counts, untouched, findings.
- `writes-check.py` (see below), `tests-compare.py` (see below).

## Findings
- **A-admin-038 (the same glass card in five phone sheets): closed.** The six phone sheets are deleted; no area sheet defines a `*-card-mobile` selector. (Two sheets outside the area, `dm-create.mobile.css` and `home.mobile.css`, define one; they belong to earlier phases and are out of scope.)
- **A-admin-043 (`.modern-card .table` defined twice): moot.** `modern-card.css` and `site.css` no longer exist; `components.css` defines the selector once, inside one shared selector list.
- Nothing is left over from either finding, so neither adds a row to deferred-items.md.

## Writes added by the phase (writes-check.py, from 34ede3a8)
```
registered methods: QuestRepository.RemoveAsync
write lines added since 34ede3a8: 4
  ok  QuestBoard.Repository/QuestRepository.cs:46 in QuestRepository.RemoveAsync: DbContext.ReminderLogs.RemoveRange(reminderLogs);
  ok  QuestBoard.Repository/QuestRepository.cs:53 in QuestRepository.RemoveAsync: followUp.OriginalQuestId = null;
  ok  QuestBoard.Repository/QuestRepository.cs:55 in QuestRepository.RemoveAsync: DbSet.Remove(entity);
  ok  QuestBoard.Repository/QuestRepository.cs:56 in QuestRepository.RemoveAsync: await DbContext.SaveChangesAsync(token);
delegations to an existing write method (information, not new writes):
  UserService.cs:189 in ChangeMemberRoleAsync: await repository.SetGroupRoleAsync(targetUserId, groupId, newRole);
  UserService.cs:210 in TryRemoveMemberAsync: await groupService.RemoveMemberAsync(groupId, targetUserId, token);
ok
```
The script exits 1 naming any write line outside a registered method and when a registered method has no write line (seen to fail with an empty register). The register has one method with two writes (the reminder-log removal and the follow-up unlink); both are in `QuestRepository.RemoveAsync`, a tracked `RemoveRange`, a tracked property set and a tracked `Remove` in one `SaveChangesAsync`. The two delegations are new call sites of write methods that existed before the phase (the role change and member removal now run behind the guards); they add no new kind of write and no write beyond the register.

## Untouched paths and project limits (untouched.py, from 34ede3a8, committed and working tree)
- `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`: unchanged since the baseline.
- `QuestBoard.Service.csproj` and `QuestBoard.Domain.csproj` unchanged: no EF package added outside the Repository project. (The Service project already named `Microsoft.EntityFrameworkCore.Tools` at the baseline, a design-time package.)
- No `DbContext` or `QuestBoardContext` in `Service/Controllers`, `Service/Areas` or `Domain/Services`.
- The phase diff adds no `ExecuteUpdate`, `ExecuteDelete`, raw SQL, `Attach` or hand-set state (`git diff ... | grep -cE` prints 0).
- No DELETE verb in the area's controllers; `DeleteUser` and `DeleteQuest` are POST with an antiforgery token. `DeleteQuest`'s write is the tracked remove above.

## Test inventory (tests-before.txt against tests-after.txt, line by line)
before 4369, after 4673, 314 new, 10 names no longer present under their exact name, all recorded:
- `AdminControllerIntegrationTests.RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` (4 theory rows) became `..._AnswerTheStaleToast_AndChangeNothing` (100-10).
- Three `AdminControllerRateLimitIntegrationTests` facts renamed (100-07).
- Two `LinkedBoardsPlatformIntegrationTests` confirm facts renamed (100-18).
- One `BoardPageStaticGuardTests` theory row: the retired sheet's row replaced by its successor's (100-19).

No test was deleted. Every other rewrite in tests-renamed.md (the Admin entries of MobileViewsTests, QuestStatusBoardClockTests, UsersControllerIntegrationTests, PlatformBoardDeleteTests, AdminControllerIntegrationTests delete facts, BoardPageGateMatrixTests and the rest) keeps its method name. One line was added at this plan (`LinkedBoardStaticGuardTests.FeatureSourceFiles`).

| Renamed or rewritten here | Intent kept |
|---------------------------|-------------|
| `LinkedBoardStaticGuardTests.FeatureSourceFiles` entry: `platform-linked-boards.mobile.css` -> `platform-pages.mobile.css` | the planning-reference scan still reaches the Linked Boards phone styling |

## Verification
- `dotnet build --no-incremental`: 0 errors, 73 warnings, none in a file this phase touched.
- Unit: 1636 passed, 0 failed, 0 skipped (same as before the plan).
- Integration: 3036 passed, 0 failed, 17 skipped, 3053 total. The 17 skipped are the same environment-gated SQL Server facts as before (BoardDeletionSqlServerTests, MigrationRunnerSqlServerTests, ShopItemSqlServerTests, QuestDeleteSqlServerTests and the like). Six facts are new here (the scope fact, two reproduction facts, two verb facts, the layered-lists fact).
- Plan verify commands: the tracer census (`counts.py`) printed "zero" and the guard fact class passed (28, then 56 with the new facts, 162 over the Task 2 filter); the plan's own `python3 -c` inventory check cannot pass as written (it splits names on whitespace, so a theory row with a quoted argument becomes a token that is never "recorded"); `tests-compare.py` does the same check line by line and prints "ok".

## Deviations from Plan

**1. [Rule 3 - Blocking] `counts.py` had no `--area` flag and its list was the phase-start 26 views and nine sheets**
- **Found during:** Task 1
- **Fix:** added `--area admin-platform`; the end side now reads every area view and sheet on disk (so a view added later is counted) and keeps the start figures beside it (nine-sheet and ROADMAP six-sheet).
- **Files modified:** scratch tool only (`tools/counts.py`).

**2. [Rule 3 - Blocking] `untouched.py` (copied from the Admin/Quests phase 98 work) protected the whole Domain project**
- **Found during:** Task 2
- **Fix:** rewritten for this phase's promises; the old file is saved beside it.
- **Files modified:** scratch tool only.

**3. [Rule 3 - Blocking] the plan's inventory one-liner cannot pass on a theory row with a quoted argument**
- **Fix:** `tools/tests-compare.py` compares by whole line, then by method name against tests-renamed.md.

**4. [Rule 1 - Bug] an analyzer warning an earlier plan added** (`AdminControllerRateLimitIntegrationTests`, a reload without the test cancellation token). Fixed in `dee0b685` so the build adds no warning.

**5. [Scope] `LinkedBoardStaticGuardTests` and `BoardPageGateMatrixTests` were edited by the phase but not listed in `PhaseSourceFiles`.** Added both; the first needed its scanner regex split so the planning-reference guard does not read its own forbidden word.

## Known Stubs
None.

## Threat Flags
None. No new endpoint, auth path, file access or schema change.

## Open items for 100-22 (Gate 2 list)
1. **Group/Delete `#board-delete-help` (`.form-text` on glass) measures 4.25:1**, same before and after, pre-existing. Group/Delete links only `platform-group-delete.css` and its phone twin and no plan owned them. Fix is a colour rule in one of those sheets or linking `platform-pages.css`: a look change, so it is a review decision (accept as pre-existing or fix in a follow-up). Severity medium.
2. **The white switch knob on the gold checked track is about 1.6:1** against the track (Group/Create and Group/Edit). The state is carried by track colour and knob position; no automated check flags it. Low.
3. **Group/Index at 1440: the Delete button wraps to a second line for an empty board; Platform Members at 1440: a long email wraps at hyphens in the Email cell.** Readable, no overflow. Low.
4. **`QuestService.RemoveAsync` deletes signups through separate saves before the repository's final tracked save**, so a refused final save leaves the signups gone while the toast says nothing was changed. Older than the phase; open for review. Medium.
5. **`GroupMembersViewModel.CreateMember` is not dead:** both Members views read the dialog's display names from it. Left as is.
6. The closing sweep must compare the Quest and Calendar pages unmoved (100-22 owns it); no layout file changed in this plan or the last one, so nothing is expected to move.
7. `AdminControllerIntegrationTests.EmailStats_WhenSuperAdmin_ShouldSucceed` is order-dependent in a narrow filter (302) and green in the full suite.

Items 1 to 5 are written in deferred-items.md ("Left at the close of the conversion", with the fixed linked-boards guard entry as its row 2).

## Self-Check: PASSED
- Commits 41a9eb85, 5dd99c1c and dee0b685 exist on milestone/v9-rolling-improvements.
- counts-after.json holds `totals` (all zero), `untouched` and `findings` (A-admin-038 closed, A-admin-043 moot); tests-after.txt (4673 lines) and the four tools exist in `~/qb-ui-review/phase-100`.
- The four protected files, both csproj files and the retired sheets are as stated.
