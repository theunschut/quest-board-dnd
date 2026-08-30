---
phase: 80-contact-categories
verified: 2026-08-30T12:54:50Z
status: human_needed
score: 14/15 must-haves verified
behavior_unverified: 1
overrides_applied: 0
behavior_unverified_items:
  - truth: "CONTACTCAT-04's case-insensitive uniqueness clause: two categories on the same board cannot share a name that differs only in case, because the database's ambient collation is case-insensitive"
    test: "Against the real deployed SQL Server instance, run: SELECT DATABASEPROPERTYEX('QuestBoard', 'Collation'); confirm it reports a *_CI_* (case-insensitive) collation. Then, as a DM, create category \"Guild Members\" and attempt to create \"guild members\" on the same board -- confirm the second submission is rejected with the validation message, not persisted as a second row."
    expected: "Server collation query returns a CI collation (e.g. SQL_Latin1_General_CP1_CI_AS), and the case-differing duplicate is refused."
    why_human: "The entire test suite (unit and integration) runs on EF Core's InMemory provider, which enforces neither `HasIndex().IsUnique()` nor any collation behavior at all -- confirmed directly in 80-05-SUMMARY.md by writing two rows sharing (GroupId, Name) through three different InMemory-backed paths, none of which rejected the duplicate. The two `ContactCategory_DuplicateName` facts in this phase's suite therefore run against a per-test host that decorates `IContactCategoryService` to force the exact `DbUpdateException` shape a real unique-index violation raises -- this proves the controller's catch/ModelState-message/re-render reaction, but proves nothing about whether the underlying index is actually case-insensitive in the running database. The migration and model both omit an explicit `COLLATE` clause, relying entirely on the SQL Server container's ambient default collation (unset `MSSQL_COLLATION` in docker-compose.yml, which per Microsoft's image docs defaults to `SQL_Latin1_General_CP1_CI_AS` -- case-insensitive). RESEARCH.md's own Assumptions Log (A1) flags this as \"not confirmed by directly querying SERVERPROPERTY('Collation') against a live instance.\" No automated test in this repository, for this feature or any other, can close this gap -- it requires a live SQL Server check."
gaps: []
deferred: []
human_verification:
  - test: "Query the live/deployed SQL Server instance's collation and attempt a live case-differing duplicate category name"
    expected: "See behavior_unverified_items entry above"
    why_human: "Database-level collation behavior cannot be exercised by the InMemory-backed test suite"
  - test: "On a real handset (not devtools emulation): open Contacts, confirm category headings render legibly; open Manage Categories from the index button; add, rename, reorder, and delete a category; confirm the up/down buttons are tappable and the delete confirmation names the contact count"
    expected: "Layout is usable, tap targets are adequate, a long category name does not break the heading"
    why_human: "80-VALIDATION.md's own Manual-Only Verifications table (D-08, D-09) states the automated suite proves the mobile view is selected and renders, but cannot judge layout, tap targets, or real-device legibility. Carried forward unresolved from the phase's own validation contract."
  - test: "On a board with genuinely zero categories: open Contacts -> Create. Confirm the category select is disabled with helper text linking to Manage Categories, and that the index shows no headings at all"
    expected: "Disabled dropdown reads as an obvious invitation to create the first category"
    why_human: "80-VALIDATION.md's own Manual-Only Verifications table (D-07) flags this as depending on subjective UX judgment of whether the hint reads as an invitation, not just that the markup is present (the markup itself was independently confirmed present and correct in this verification pass)."
---

# Phase 80: Contact Categories Verification Report

**Phase Goal:** A DM can group the board's NPCs under named categories -- "Corridor", "Guild Members", "Last Bastion" -- and the Contacts index renders them under those headings instead of one flat list, on both desktop and mobile.

**Verified:** 2026-08-30T12:54:50Z
**Status:** human_needed
**Re-verification:** No -- initial verification

## Summary

This phase is substantively well-built. Every artifact claimed in the 8 plan SUMMARYs was independently confirmed to exist, be wired, and behave correctly by reading the actual merged code on `milestone/v9-rolling-improvements` (not the SUMMARY narrative) and by independently re-running the test suite from this session (not trusting the plans' self-reported results, several of which were run from the wrong working directory per the phase's own admission). Build is 0 errors; `dotnet test` run fresh in this session reproduces exactly 437 unit + 668 integration tests, 0 failures, matching the reported current state.

One genuine, honestly-disclosed residual risk survives: **CONTACTCAT-04's case-insensitive uniqueness constraint is architecturally sound but empirically unverified against a live SQL Server instance.** The unique index on `(GroupId, Name)` carries no explicit `COLLATE` clause and depends entirely on the database's ambient collation being case-insensitive (the SQL Server Docker image's undocumented default when `MSSQL_COLLATION` is unset). This is not a code defect -- it is the same pattern `GroupEntity.Name` already uses elsewhere in this codebase -- but no automated test anywhere in this repository can prove it, because the entire suite runs on EF Core's InMemory provider, which enforces neither unique indexes nor collation at all. This was flagged as an open assumption in the phase's own RESEARCH.md before implementation began, and remains open now. Worst-case failure mode if the assumption is wrong: two category names differing only in case could both be created (a UX inconsistency), not a crash or security issue.

No other blocking gaps were found. All five specifically-flagged risk areas were independently re-derived from the code and found sound.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | (CONTACTCAT-01) DM creates a named category from a dedicated Manage Categories page reached from the Contacts index, scoped to the active board | VERIFIED | `ContactCategoryManagementController.Add` stamps `category.GroupId = activeGroupId` from `activeGroupContext.ActiveGroupId`; `[Authorize(Policy = "DungeonMasterOnly")]` at class level; `Views/Contacts/Index.cshtml:71-73` links to it from `Url.Action("Index", "ContactCategoryManagement")` |
| 2 | (CONTACTCAT-02) A contact belongs to exactly one category or none, via a single dropdown with blank "-- None --" on Create/Edit, desktop and mobile | VERIFIED | Confirmed identical `asp-for="CategoryId"` select + "-- None --" option in all four views: `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` |
| 3 | (CONTACTCAT-03) DM renames/deletes a category; deleting a non-empty category moves its contacts to Ungrouped rather than deleting or blocking | VERIFIED | `ContactCategoryRepository.DeleteWithDependentsLoadedAsync` loads dependents so the configured `SetNull` behaviour applies; `ContactCategoryRepositoryTests.DeleteWithDependentsLoadedAsync_ContactsSurviveWithNullCategory` asserts both contacts survive with `CategoryId == null`. Re-ran this fact fresh in this session -- passed |
| 4 | (CONTACTCAT-04) Category names unique per board, case-insensitive; duplicate returns a validation message, not a 500 | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Controller catch/message/re-render path (`ContactCategoryManagementController.Add`/`Edit`) is real and independently confirmed working via `ContactCategory_DuplicateName` tests, re-run fresh and green. **But** these tests use a decorator that forces the exception shape rather than exercising the real unique index, because EF Core InMemory enforces neither `IsUnique()` nor collation. The actual case-insensitive DB enforcement (`IX_ContactCategories_GroupId_Name`, no explicit `COLLATE`, relies on ambient SQL Server default collation) has never been exercised against a real database in this repo. See `behavior_unverified_items` |
| 5 | (CONTACTCAT-05) Every category read/write is scoped to the active board by the global query filter; null active board resolves zero categories; no app path bypasses it | VERIFIED | `ContactCategoryEntity.HasQueryFilter` in `QuestBoardContext.cs:467-470` fails closed on null `ActiveGroupId`; grepped `QuestBoard.Service`, `QuestBoard.Domain`, `QuestBoard.Repository` for `IgnoreQueryFilters` on category/contact paths -- zero occurrences outside test infrastructure. Five dedicated `ContactCategory_CrossGroup_*` integration tests re-run fresh and green, including the foreign-`CategoryId` POST-refusal path on both Create and Edit |
| 6 | (CONTACTCAT-06) Only DM-tier users can create/rename/delete/reorder categories, enforced server-side | VERIFIED | `[Authorize(Policy = "DungeonMasterOnly")]` at class level on `ContactCategoryManagementController`; six `*_PlayerAccess_ShouldBeBlocked` integration tests re-run fresh and green |
| 7 | (CONTACTCAT-07) DM reorders with up/down; index renders headings in that order, not alphabetically | VERIFIED | `ContactCategoryService.MoveUpAsync`/`MoveDownAsync` swap `SortOrder` via position in the board-scoped ordered list; `ContactsIndex_CategoryOrdering_FollowsSortPositionNotAlphabet` (deliberately reversed alphabetical vs. sort order) re-run fresh and green |
| 8 | (CONTACTCAT-08) Manage Categories ships desktop and mobile; the mobile view is proven selected under a real mobile User-Agent | VERIFIED | `ContactCategoryMobileRenderTests` sends a real iPhone Safari UA and asserts mobile-only markup (`category-mgmt-row`) present under mobile UA and absent under default UA, and the reverse for desktop-only markup; re-run fresh and green |
| 9 | (CONTACTCAT-09) Contacts index renders contacts under category headings, both desktop and mobile, alphabetical by name within each heading | VERIFIED | `ContactsController.Index` groups the already-visibility-filtered view models by category, `.OrderBy(c => c.Name)` within each group; `Index.cshtml` and `Index.Mobile.cshtml` both iterate `Model.CategoryGroups`; `ContactsIndex_CategoryOrdering_ContactsWithinCategoryAreAlphabetical` re-run fresh and green |
| 10 | (CONTACTCAT-10) Ungrouped contacts render under a synthetic "Ungrouped" heading pinned after every real category, not renameable or orderable | VERIFIED | `.OrderBy(g => g.Key.CategoryId is null)` pins the null-category group last; `ContactCategoryGroupViewModel.IsUngrouped` has no `Id`/edit route wired anywhere; `ContactsIndex_CategoryOrdering_UngroupedHeadingAppearsAfterEveryRealCategory` re-run fresh and green |
| 11 | (CONTACTCAT-11) A board with no categories renders the flat contact list exactly as it renders today, no headings at all | VERIFIED | Diffed current `Index.cshtml`/`Index.Mobile.cshtml` against the pre-phase-80 commit (`a146d8d3`): the `else` (no-categories) branch reproduces the identical prior markup structure on both desktop (bare `contact-grid`, no heading) and mobile (`contact-section-card` + "Contacts" heading, matching the pre-existing unconditional wrapper). `ContactsIndex_CategoryOrdering_ZeroCategoryBoardRendersFlatListWithNoHeadings` present |
| 12 | (CONTACTCAT-12) A category heading renders only when at least one contact beneath it is visible; carries the name alone, no count | VERIFIED | `ContactCategoryGroupViewModel` has no count field by design (comment explicitly rejects it); grouping runs over the already-visibility-filtered `contactViewModels`, so an all-hidden category produces no group key at all. Three `ContactCategory_EmptyHeadingSuppression_*` tests (player, DM-toggle-on, DM-toggle-off) independently re-run fresh and green -- this is the exact heading-disclosure concern flagged in the verification brief, and it holds |
| 13 | (CONTACTCAT-13) Category name stored with 60-char cap, rendered as plain escaped text, never through Markdown | VERIFIED | `ContactCategoryEntity.Name` is `[StringLength(60)]`; `ContactCategoryViewModel.Name` is `[StringLength(60, ErrorMessage = ...)]`; `@group.Title` in both Index views uses default Razor HTML-encoding, never `Html.Raw` or `IMarkdownService`; `ContactCategory_NameRendersEscaped_AngleBracketsAreEncoded` re-run fresh and green. No dedicated regression test exists for the 61-char boundary itself (informational, not a gap -- `[StringLength]` is a standard, independently-tested ASP.NET Core validation attribute) |
| 14 | (CONTACTCAT-14) Contact's category shown on Details, desktop and mobile; no category = no line | VERIFIED | `Details.cshtml:36-41` and `Details.Mobile.cshtml:34-39` both gate the category line on `!string.IsNullOrEmpty(Model.CategoryName)`; AutoMapper wiring (`EntityProfile.cs`, `ViewModelProfile.cs`) confirmed carrying `CategoryName`/`CategorySortOrder` as read-only projections (`.Ignore()` on the reverse map so a POST cannot inject them) |
| 15 | (CONTACTCAT-15) Zero-category board: Create/Edit render a disabled select with helper text linking to Manage Categories | VERIFIED | Confirmed identical disabled-`<select>` + "Manage Categories" link fallback in all four views (`Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`), gated on `Model.HasCategories` |

**Score:** 14/15 truths verified (1 present + wired, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/Entities/ContactCategoryEntity.cs` | Entity: Name (60 cap), SortOrder, GroupId | VERIFIED | Present, substantive, wired into `QuestBoardContext` |
| `QuestBoard.Repository/Migrations/20260830094351_AddContactCategories.cs` | Table, FKs, unique index | VERIFIED | Table created, `IX_ContactCategories_GroupId_Name` unique (no explicit collation -- see truth #4), `SetNull` FK from Contacts |
| `QuestBoard.Domain/Services/ContactCategoryService.cs` | Ordered read, reorder, delete-orphan | VERIFIED | All methods delegate through board-scoped repository calls |
| `QuestBoard.Repository/ContactCategoryRepository.cs` | Board-scoped CRUD + reorder + counts | VERIFIED | No `IgnoreQueryFilters()` in any application path |
| `QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs` | CRUD, reorder, class-level DM-only auth | VERIFIED | `[Authorize(Policy = "DungeonMasterOnly")]` at class level |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (modified) | Grouping, visibility-safe headings, cross-board CategoryId rejection | VERIFIED | `IsCategoryAcceptableAsync` resolves through board-filtered `GetByIdAsync` before any write |
| `QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml` + `.Mobile.cshtml` | Desktop + mobile management UI | VERIFIED | Both present, distinct markup (`category-mgmt-row` mobile-only class), reorder buttons, delete confirmation with contact count |
| `QuestBoard.Service/Views/Contacts/Index.cshtml` + `.Mobile.cshtml` | Grouped headings, desktop + mobile | VERIFIED | Both render `Model.CategoryGroups`, flat fallback matches pre-phase markup |
| `QuestBoard.Service/Views/Contacts/Details.cshtml` + `.Mobile.cshtml` | Category line, desktop + mobile | VERIFIED | Both gate on non-empty `CategoryName` |
| `QuestBoard.Service/Views/Contacts/Create.cshtml/.Mobile/Edit.cshtml/.Mobile` | Category dropdown / disabled-select fallback | VERIFIED | All four views confirmed |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `ContactsController.Create/Edit` POST | `ContactCategoryService.GetByIdAsync` | `IsCategoryAcceptableAsync` | WIRED | Board-filtered lookup rejects a foreign `CategoryId` before any write; confirmed by two dedicated cross-group POST tests, re-run fresh and green |
| `ContactsController.Index` | `ContactCategoryService.GetOrderedAsync` | direct call | WIRED | Board-scoped, ordered by SortOrder then Id |
| `ContactCategoryManagementController.Add/Edit` | `IContactCategoryService` -> `DbUpdateException` catch | try/catch on "unique"/"duplicate" substring | WIRED (controller layer only; DB layer unverified -- see truth #4) | |
| `Index.cshtml`/`Index.Mobile.cshtml` | `ContactsController.Index` view model | `Model.CategoryGroups`, `Model.HasCategories` | WIRED | Grouping runs over already-visibility-filtered data; no disclosure path found |
| `Create/Edit.cshtml` (all 4) | `PopulateCategoryOptionsAsync` | `Model.CategoryOptions`, `Model.HasCategories` | WIRED | Options sourced from board-scoped `GetOrderedAsync`, never re-sorted client-side |

### Behavioral Spot-Checks / Test Re-Execution

Re-ran independently in this verification session (not trusted from SUMMARY claims -- the phase's own plans admit several were run from the wrong working directory):

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Build | `dotnet build` | 0 errors, 20 pre-existing unrelated NuGet warnings | PASS |
| ContactCategory-scoped tests | `dotnet test --filter "FullyQualifiedName~ContactCategory"` | 13 unit + 31 integration, 0 failures | PASS |
| Contact-scoped tests (broader) | `dotnet test --filter "FullyQualifiedName~Contact"` | 37 unit + 70 integration, 0 failures | PASS |
| Full suite | `dotnet test` | 437 unit + 668 integration, 0 failures | PASS -- matches reported current state exactly |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
|-------------|-------------|--------|----------|
| CONTACTCAT-01 | 80-01 (minted), 80-05 (built) | SATISFIED | Truth #1 |
| CONTACTCAT-02 | 80-01, 80-04/80-07 | SATISFIED | Truth #2 |
| CONTACTCAT-03 | 80-01, 80-05 | SATISFIED | Truth #3 |
| CONTACTCAT-04 | 80-01, 80-02/80-05 | SATISFIED WITH CAVEAT | Truth #4 -- controller path proven, DB-layer case-insensitivity unverified against a live instance |
| CONTACTCAT-05 | 80-01, 80-02/80-03/80-07 | SATISFIED | Truth #5 |
| CONTACTCAT-06 | 80-01, 80-05 | SATISFIED | Truth #6 |
| CONTACTCAT-07 | 80-01, 80-05/80-06 | SATISFIED | Truth #7 |
| CONTACTCAT-08 | 80-01, 80-05/80-08 | SATISFIED | Truth #8 |
| CONTACTCAT-09 | 80-01, 80-06 | SATISFIED | Truth #9 |
| CONTACTCAT-10 | 80-01, 80-06 | SATISFIED | Truth #10 |
| CONTACTCAT-11 | 80-01, 80-06 | SATISFIED | Truth #11 |
| CONTACTCAT-12 | 80-01, 80-06 | SATISFIED | Truth #12 |
| CONTACTCAT-13 | 80-01, 80-04/80-06 | SATISFIED | Truth #13 |
| CONTACTCAT-14 | 80-01, 80-08 | SATISFIED | Truth #14 |
| CONTACTCAT-15 | 80-01, 80-07 | SATISFIED | Truth #15 |

No orphaned requirements found -- all 15 IDs declared in REQUIREMENTS.md map to at least one phase-80 plan and to verified implementation.

**REQUIREMENTS.md currently shows all 15 as "Not started."** Based on the evidence above, 14 of 15 should be marked "Complete." CONTACTCAT-04 should be marked "Complete" only after the human verification item above is resolved, or marked complete with the caveat explicitly noted, per the operator's judgment.

### Anti-Patterns Found

None. Scanned every file this phase created or modified (`ContactCategoryEntity`, `IContactCategoryRepository`, `IContactCategoryService`, `ContactCategory` domain model, `ContactCategoryService`, `ContactCategoryRepository`, `ContactCategoryManagementController`, `ContactsController`, all three category view models) for `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`/empty-implementation patterns. Zero matches beyond legitimate user-facing error copy ("Selected category is not available on this board.").

### Deviation / Risk Notes Independently Confirmed

- **80-04's two literal-grep false positives** (`StringLength(60, ErrorMessage=...)` vs. bare `StringLength(60)`; the word "count" appearing in an explanatory comment about *not* having a count field) -- independently confirmed both were genuine grep-pattern collisions, not disguised shortcuts. Note: a later manual commit (`7a2f0587`, outside the 8 plans) re-added the `ErrorMessage` argument for a better validation UX once the acceptance-check pressure was gone; this is a net improvement, not a regression.
- **80-06's `Ungrouped`/`IsUngrouped` substring collision and 80-07's `OrderBy` / directory-wide `IgnoreQueryFilters` collisions** -- independently re-derived from the code: `PopulateCategoryOptionsAsync` contains no `OrderBy` call, and `IgnoreQueryFilters` does not appear anywhere in `ContactsController.cs`, `ContactCategoryService.cs`, or `ContactCategoryRepository.cs`. Both deviation notes are accurate.
- **Wrong-worktree concern (`cd "C:/Repos/quest-board"` in every plan's `<verify>` block)** -- the phase's own final tracking commit (`7243d10d`) already re-ran the two filters that couldn't be verified from an isolated 80-08 worktree, against the actual merged tree, before setting `wave_0_complete: true`. This verification pass independently re-ran the entire suite fresh from the current `milestone/v9-rolling-improvements` checkout and reproduced identical counts (437/668, 0 failures), closing this concern.
- **Cross-board isolation (Phase 49/55 leak class)** -- independently confirmed at three layers: the global `HasQueryFilter` on `ContactCategoryEntity` fails closed on null `ActiveGroupId`; the controller resolves any client-supplied `CategoryId` through the same board-filtered lookup before every write; and five dedicated `ContactCategory_CrossGroup_*` integration tests (management list, create-form dropdown, index, create-POST refusal, edit-POST refusal, null-active-board) were re-run fresh in this session and are green. No leak found.
- **Heading disclosure of hidden contacts** -- independently confirmed the grouping in `ContactsController.Index` runs over the already-visibility-filtered contact list, so a category with only hidden contacts produces no group key at all for a viewer who can't see them. Three dedicated tests spanning player / DM-toggle-on / DM-toggle-off were re-run fresh and green.

## Human Verification Required

### 1. Live-database collation check for CONTACTCAT-04

**Test:** Against the deployed/production SQL Server instance, run `SELECT DATABASEPROPERTYEX('QuestBoard', 'Collation');` and confirm it reports a `*_CI_*` (case-insensitive) collation. Then create a category "Guild Members" as a DM, and attempt to create "guild members" on the same board.
**Expected:** The collation query returns a case-insensitive collation, and the second submission is rejected with "A category with that name already exists," not silently persisted as a second row.
**Why human:** No automated test in this repository can exercise this -- the entire suite runs on EF Core's InMemory provider, which enforces neither unique indexes nor collation. This is a pre-existing architectural pattern (shared with `GroupEntity.Name`), not a defect introduced by this phase, and the failure mode if wrong is a UX inconsistency rather than a crash or security issue -- but it is a genuinely unverified claim in a requirement that explicitly promises case-insensitivity.

### 2. Real-device mobile usability

**Test:** On a real phone (not devtools emulation): open Contacts, confirm headings render and are readable; open Manage Categories from the index button; add, rename, reorder, and delete a category; confirm the up/down buttons are tappable and the delete confirmation names the contact count.
**Expected:** Layout is usable, tap targets are adequate, a long category name does not break the heading.
**Why human:** Carried forward from the phase's own `80-VALIDATION.md` Manual-Only Verifications table (D-08/D-09) -- the automated suite proves the mobile view is selected and renders (confirmed above), not that it is legible or usable.

### 3. First-run discovery path subjective UX

**Test:** On a board with genuinely zero categories: open Contacts -> Create. Confirm the category select is disabled with helper text linking to Manage Categories, and the index shows no headings at all.
**Expected:** The disabled dropdown reads as an obvious invitation to create the first category, not a confusing dead control.
**Why human:** Carried forward from `80-VALIDATION.md` (D-07) -- the markup itself is independently confirmed present and correctly gated in this verification pass; only the subjective "does it read as an invitation" judgment remains open.

---

_Verified: 2026-08-30T12:54:50Z_
_Verifier: Claude (gsd-verifier)_
