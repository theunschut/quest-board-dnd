---
phase: 80-contact-categories
plan: 07
subsystem: ui
tags: [razor-views, mvc-controller, integration-tests, cross-tenant-security, contact-categories]

requires:
  - phase: 80-contact-categories
    provides: "IContactCategoryService/ContactCategoryService (GetOrderedAsync board-scoped ordered read, GetByIdAsync board-scoped single read), ContactViewModel.CategoryId/CategoryOptions/HasCategories, ContactCategoryManagementController route, the grouped Contacts/Index this plan's dropdown must stay consistent with"
provides:
  - "ContactsController Create/Edit GET and POST actions populating CategoryOptions in the board's own sort order via a shared private helper, and validating a posted CategoryId through a board-filtered GetByIdAsync lookup before persisting"
  - "Identical category select field on Create.cshtml/Create.Mobile.cshtml/Edit.cshtml/Edit.Mobile.cshtml -- enabled with a blank None option when the board has categories, disabled with a Manage Categories discovery link when it does not"
  - "ContactsControllerIntegrationTests coverage for cross-board isolation (management page, create-form dropdown, index, both refused writes, null-active-board fail-closed) and both disabled-select form states"
affects: [80-contact-categories]

tech-stack:
  added: []
  patterns:
    - "A posted CategoryId is never trusted as-is -- it is resolved through IContactCategoryService.GetByIdAsync, which is board-scoped by the same global query filter as every other category read, so a category id belonging to another board simply fails to resolve and is indistinguishable from a nonexistent one. No manual ActiveGroupId comparison anywhere in the controller."
    - "A single PopulateCategoryOptionsAsync helper is called before every return View(viewModel) path across all four Create/Edit action bodies (8 call sites for 8 return-View sites), so no re-render can ever come back with an empty dropdown regardless of which validation failed."
    - "The category select markup is byte-identical across all four views (desktop/mobile x Create/Edit), placed after Sub-location and before the description editor -- no new CSS, since modern-card.css's existing .text-muted/.form-text overrides already cover the helper text and the disabled-select treatment."

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "IsCategoryAcceptableAsync trusts the board-scoped GetByIdAsync read entirely -- a null id is always acceptable, any other id is acceptable only if the board-filtered lookup returns a row. This means a foreign-board id and a genuinely nonexistent id produce the exact same refusal, by construction, with no hand-rolled ActiveGroupId comparison to get wrong."
  - "PopulateCategoryOptionsAsync and IsCategoryAcceptableAsync were added as two small private helpers rather than folded into the four action bodies, so the acceptance check and the options repopulation are each written once and cannot drift between Create and Edit."
  - "AddUserToGroupAsync (test-only) mirrors the exact inline UserGroupEntity-insert pattern already used by ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups in this same file, rather than introducing a second membership-seeding idiom."
  - "The null-active-board fact queries factory.Database.CreateContext() directly (a context wired to its own MutableGroupContext fixed at ActiveGroupId=null) rather than going through an HTTP round trip, because GroupSessionMiddleware redirects any GET request with a null active board to /groups/pick before it ever reaches ContactsController -- the same idiom TenantIsolationTests already uses to prove QuestEntity's fail-closed filter."

requirements-completed: [CONTACTCAT-02, CONTACTCAT-05, CONTACTCAT-15]

coverage:
  - id: D1
    description: "A contact belongs to exactly one category or none, assigned from a single dropdown with a blank -- None -- option on the Create and Edit forms, both desktop and mobile; every re-render keeps the dropdown populated"
    requirement: "CONTACTCAT-02"
    verification:
      - kind: unit
        ref: "dotnet build -- 0 errors; grep verification that all four views contain CategoryId/HasCategories/CategoryOptions and the literal -- None --, and that CategoryId's line number falls strictly between SubLocation and the description editor partial in all four files"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactCategory_DisabledSelect -- 2 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every category read and write in this plan's new write path is scoped to the active board through the global query filter (GetByIdAsync's board-scoped lookup rejects a foreign or nonexistent category id with a validation message rather than persisting it), a null active board resolves the category read to nothing, and no application code path bypasses the filter"
    requirement: "CONTACTCAT-05"
    verification:
      - kind: unit
        ref: "grep -cE 'IgnoreQueryFilters' QuestBoard.Service/Controllers/Contacts/ContactsController.cs, QuestBoard.Domain/Services/ContactCategoryService.cs, QuestBoard.Repository/ContactCategoryRepository.cs -- 0 for all three"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactCategory_CrossGroup -- 6 passed, 0 failed (management page, create-form dropdown, index, Create POST refusal + unstored, Edit POST refusal + unchanged reference, null-active-board fail-closed)"
        status: pass
    human_judgment: false
  - id: D3
    description: "On a board with no categories, the contact Create and Edit forms render the category select disabled with helper text linking to the Manage Categories page, on both desktop and mobile"
    requirement: "CONTACTCAT-15"
    verification:
      - kind: unit
        ref: "grep verification: all four views contain a disabled select paired with ContactCategoryManagement, and the verbatim strings 'No categories yet.' and 'to create one.'"
        status: pass
      - kind: integration
        ref: "ContactCategory_DisabledSelect_ZeroCategoryBoard_CreateFormShowsDisabledSelectAndManagementLink -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D4
    description: "Solution-wide regression: the whole suite stays green after wiring category assignment into the contact form write path"
    verification:
      - kind: unit
        ref: "dotnet test (whole solution) -- 437 unit + 662 integration passed, 0 failed (up from the 654-integration baseline recorded by 80-06)"
        status: pass
    human_judgment: false
duration: ~55min
completed: 2026-08-30
status: complete
---

# Phase 80 Plan 07: Contact Category Assignment on Create/Edit Summary

**Category assignment wired into all four contact forms with a board-filtered GetByIdAsync as the sole trust boundary for a posted CategoryId, proven end-to-end by six cross-board isolation facts and two disabled-select facts.**

## Performance

- **Duration:** ~55 min
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `ContactsController` gained two private helpers -- `PopulateCategoryOptionsAsync` (reads the board's ordered categories via `GetOrderedAsync`, projects them into `SelectListItem`s preserving sort-position-then-id order, sets `HasCategories`) and `IsCategoryAcceptableAsync` (a null id is always acceptable; any other id is acceptable only if `GetByIdAsync`'s board-scoped read resolves it) -- called consistently across all four Create/Edit action bodies
- `Create` GET became async to load the options; `Create`/`Edit` POST both call the acceptance helper immediately before the contact is mapped/persisted, adding `"Selected category is not available on this board."` as a `ModelState` error and repopulating options on refusal rather than trusting the posted value
- `Edit` POST's success path now assigns `existingContact.CategoryId = viewModel.CategoryId` alongside the existing Name/Description/TownCity/SubLocation copy, so a cleared selection persists a null reference instead of being silently dropped
- Identical category select block added to `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, positioned after Sub-location and before the description editor on every form -- enabled `<select>` bound to `CategoryId` with a blank `"— None —"` option when the board has categories, disabled `<select>` plus a `Manage Categories` discovery link when it does not. No new CSS: `modern-card.css`'s existing `.text-muted`/`.form-text` overrides already cover the helper text
- `ContactsControllerIntegrationTests.cs` gained 8 new facts plus a private `AddUserToGroupAsync` helper: 6 `ContactCategory_CrossGroup` facts (management page, create-form dropdown, index, Create POST refusal with nothing stored, Edit POST refusal with the stored reference unchanged, null-active-board resolving to zero categories) and 2 `ContactCategory_DisabledSelect` facts (zero-category board renders disabled + link, a board with a category renders enabled with no helper link)
- Solution builds with 0 errors; full suite green at 437 unit + 662 integration tests (up from the 654-integration baseline after wave 5/80-06)

## Task Commits

Each task was committed atomically:

1. **Task 1: Populate and validate the category on the contact Create and Edit actions** - `b22cf769` (feat)
2. **Task 2: Add the category field to the four contact form views** - `0bbdc01f` (feat)
3. **Task 3: Cross-board isolation and disabled-select integration facts** - `95a6d2f0` (test)

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - `PopulateCategoryOptionsAsync`/`IsCategoryAcceptableAsync` helpers; `Create` GET is now async; both POST actions validate the posted category id before persisting; `Edit` POST assigns `CategoryId` in the existing field-copy block
- `QuestBoard.Service/Views/Contacts/Create.cshtml` - category select block after the TownCity/SubLocation row, before the description editor
- `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` - category select block after SubLocation, before the description editor
- `QuestBoard.Service/Views/Contacts/Edit.cshtml` - category select block after the TownCity/SubLocation row, before the description editor
- `QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml` - category select block after SubLocation, before the description editor
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - 8 new facts plus `AddUserToGroupAsync` helper

## Decisions Made
- **`IsCategoryAcceptableAsync` never compares `ActiveGroupId` by hand.** It trusts `IContactCategoryService.GetByIdAsync` entirely -- that read is already board-scoped by the same global query filter every other category read goes through, so a foreign-board id and a genuinely nonexistent id produce the identical refusal by construction, with no second board-membership check to accidentally get wrong or skip.
- **Two small private helpers instead of inlining the logic four times.** `PopulateCategoryOptionsAsync` and `IsCategoryAcceptableAsync` are each written once and called from all four action bodies, so the options-repopulation-on-every-re-render invariant and the acceptance check cannot drift between Create and Edit as the controller evolves.
- **The null-active-board fact bypasses HTTP entirely.** `GroupSessionMiddleware` redirects any GET with a null active board to `/groups/pick` before the request reaches `ContactsController`, so proving the fail-closed category filter requires querying `factory.Database.CreateContext()` directly (its own `MutableGroupContext` fixed at `ActiveGroupId = null`) rather than round-tripping through an endpoint that would never reach the code under test. This mirrors `TenantIsolationTests`'s existing idiom for `QuestEntity`.
- **`AddUserToGroupAsync` reuses the exact inline `UserGroupEntity`-insert shape already present in this file's `ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups` test**, extracted into a helper rather than invented fresh, so cross-board membership seeding stays a single idiom in this suite.

## Deviations from Plan

None - plan executed exactly as written; no bugs, missing functionality, or blocking issues were encountered during implementation.

### Acceptance-Check Notes (not deviations, per acceptance_criteria_guidance)

**1. Literal-substring grep collision on `OrderBy(.*Name)`.** Task 1's acceptance criteria checks that `grep -cE 'OrderBy\(.*Name\)' ContactsController.cs` shows no ordering applied to the category option projection. The file-wide grep matches one pre-existing line from 80-06's `Index` action, `Contacts = g.OrderBy(c => c.Name).ToList()`, which orders *contacts* alphabetically within an index category group -- unrelated to this plan's category *options* projection, which uses no `OrderBy` at all (order comes straight from `GetOrderedAsync`). Verified the check's evident intent directly: the category-options helper this task added (`PopulateCategoryOptionsAsync`) contains no `OrderBy` call anywhere. No code was changed to dodge the collision.

**2. Directory-wide `IgnoreQueryFilters` grep catching pre-existing, unrelated repositories.** Task 3's acceptance criteria checks that no file under `QuestBoard.Service`, `QuestBoard.Domain` or `QuestBoard.Repository` contains `IgnoreQueryFilters`. A directory-wide grep also matches `EventRepository.cs`, `GroupRepository.cs`, and `QuestRepository.cs` -- all pre-existing, unmodified by this plan (confirmed via `git diff --name-only`, which lists only `ContactsControllerIntegrationTests.cs` for this task), and outside this plan's `files_modified`. Verified the check's evident intent directly: `ContactsController.cs`, `ContactCategoryService.cs`, and `ContactCategoryRepository.cs` -- the files this plan's category write path actually touches -- contain zero occurrences. No code was changed; the pre-existing occurrences are out of this plan's scope entirely.

---

**Total deviations:** 0 auto-fixed. Two acceptance-check literal-grep collisions noted and resolved by verifying intent rather than code changes.
**Impact on plan:** None. The controller, views, and test suite match the plan's action text and behavior spec exactly.

## Issues Encountered

**Bash tool cwd drift to the main repo instead of the worktree (mid-session, caught before any commit).** After the worktree HEAD/base assertions passed at startup, several early `Bash` calls used `cd "C:/Repos/quest-board"` (the main repo root) instead of the worktree path -- the Bash tool's cwd resets between calls and does not persist the worktree location by default. This was caught immediately when a `grep -c` check against the freshly-edited controller returned stale (pre-edit) results; `sed -n | cat -A` confirmed the on-disk file at that path still had the old `Create()` signature. All subsequent Bash invocations were corrected to `cd` into the worktree path explicitly (`C:/Repos/quest-board/.claude/worktrees/agent-a8abdc259612edfa9`) before any build, test, or git command, and every acceptance check was re-run from the correct location. No file was ever written to the wrong location -- `Edit`/`Write` tool calls use absolute worktree paths independent of Bash cwd -- and no commit was made before the drift was caught.

**`AssignContactCategoryAsync` failed on two new cross-board tests until `ActiveGroupId` was flipped to board 2 before calling it, not after.** The helper reads through the DI-registered, board-filtered `Contacts` DbSet via `SingleAsync`; calling it while the shared `factory.TestGroupContext.ActiveGroupId` was still at its default (1) for a contact seeded on board 2 threw `Sequence contains no elements`. Fixed by moving `factory.TestGroupContext.ActiveGroupId = 2;` to immediately before the `AssignContactCategoryAsync` call in both `ContactCategory_CrossGroup_IndexNeverShowsOtherBoardsCategory` and `ContactCategory_CrossGroup_EditPost_ForeignCategoryId_IsRefusedAndReferenceUnchanged`, inside the existing try/finally block.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The contact category feature's write path is now complete end to end: creation (80-01/80-02/80-03), the view-model contract (80-04), the management UI (80-05), the grouped index (80-06), and this plan's Create/Edit assignment with cross-board isolation all ship together.
- `ContactsController.cs` and all four contact form views are stable inputs for 80-08 (Details-view category line), which this plan does not touch (`Details.cshtml`/`Details.Mobile.cshtml` are 80-08's exclusive files per the parallel-execution boundary).
- No blockers. All three requirements this plan owns (`CONTACTCAT-02`, `CONTACTCAT-05`, `CONTACTCAT-15`) are proven by passing tests; the two `ContactCategory_CrossGroup`/`ContactCategory_DisabledSelect` filter names `80-VALIDATION.md` promises both select and pass (6 and 2 tests respectively, meeting the "at least six"/"at least two" bar).

---
*Phase: 80-contact-categories*
*Completed: 2026-08-30*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Contacts/ContactsController.cs
- FOUND: QuestBoard.Service/Views/Contacts/Create.cshtml
- FOUND: QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
- FOUND: QuestBoard.Service/Views/Contacts/Edit.cshtml
- FOUND: QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
- FOUND: QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
- FOUND: commit b22cf769 (Task 1)
- FOUND: commit 0bbdc01f (Task 2)
- FOUND: commit 95a6d2f0 (Task 3)
