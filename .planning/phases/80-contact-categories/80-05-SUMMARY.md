---
phase: 80-contact-categories
plan: 05
subsystem: web
tags: [controller, razor-views, authorization, integration-tests, css]

requires:
  - phase: 80-contact-categories
    provides: "IContactCategoryService/ContactCategoryService (ordering, counts, AddToEndAsync, MoveUp/MoveDown, DeleteAsync), ContactCategoryViewModel/ContactCategoryManagementViewModel, TestDataHelper.CreateTestContactCategoryAsync"
provides:
  - "ContactCategoryManagementController: Index/Add/Edit(GET+POST)/Delete/MoveUp/MoveDown, DungeonMasterOnly at class level"
  - "Route /ContactCategoryManagement/{action} -- add, rename, delete, reorder in one page"
  - "Manage.cshtml/Manage.Mobile.cshtml/Edit.cshtml/Edit.Mobile.cshtml under Views/ContactCategoryManagement/"
  - "ContactCategoryManagementControllerIntegrationTests -- authorization, duplicate-name, delete-orphan, reorder, escaping"
  - ".modern-card .text-danger CSS override so validation-error spans stay legible inside a modern-card body"
affects: [80-contact-categories]

tech-stack:
  added: []
  patterns:
    - "Duplicate-name catch: try/catch (DbUpdateException ex) when (...) matching 'unique'/'duplicate' in the inner exception message, mirroring GroupController -- no hand-rolled existence pre-check"
    - "Board id stamped only from IActiveGroupContext.ActiveGroupId inside the controller, never bound from the posted view model"
    - "Reorder redirects carry a #category-{id}-row fragment via RedirectToAction(action, controllerName: null, routeValues: null, fragment: ...) so a DM keeps their place across repeated posts"
    - "Test-only service decorator (WithWebHostBuilder + a wrapping IContactCategoryService) forces the exact DbUpdateException shape a live unique-index violation would raise, since the EF Core InMemory provider backing the integration suite does not enforce HasIndex().IsUnique() at all"

key-files:
  created:
    - QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs
    - QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml
    - QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml
    - QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml
    - QuestBoard.Service/Views/ContactCategoryManagement/Edit.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/modern-card.css

key-decisions:
  - "The EF Core InMemory provider used by QuestBoard.IntegrationTests does not enforce HasIndex().IsUnique() at all -- confirmed directly by writing two rows sharing (GroupId, Name) through a fresh TestDatabase context, through the app's own DI-registered QuestBoardContext, and within a single SaveChanges batch; none threw, none of the three configurations rejected the duplicate. The two ContactCategory_DuplicateName facts therefore run against a per-test host variant (factory.WithWebHostBuilder) that swaps IContactCategoryService for a decorator forcing the exact DbUpdateException shape a live SQL Server unique-index violation produces, once, for one specific name; every other call passes through to the real service unchanged. This proves what this task owns -- the controller's catch/ModelState-message/re-render reaction -- without depending on a database-layer guarantee (the collation-driven case-insensitive rejection itself) that this InMemory-backed suite was never going to be able to exercise."
  - "Added .modern-card .text-danger to modern-card.css (not originally in this plan's files_modified list). '.modern-card span/p' repaints any span inside a modern-card body in the heading's cream colour with a heavy dark shadow, which silently swallowed the validation-error styling on every asp-validation-for span this plan's four new views render (Manage.cshtml's NewCategory.Name span, Edit.cshtml's/Edit.Mobile.cshtml's Name span) -- the exact same pattern already present, unfixed, on GroupController's own Edit.cshtml, which this plan's action text names as the form shape to copy. Restated Bootstrap's danger red with the same drop-shadow every other in-card label already uses, scoped to .modern-card so validation errors read as errors instead of ordinary body text."

requirements-completed: [CONTACTCAT-01, CONTACTCAT-03, CONTACTCAT-04, CONTACTCAT-06, CONTACTCAT-07, CONTACTCAT-08, CONTACTCAT-13]

coverage:
  - id: D1
    description: "A DungeonMaster-tier user can add, rename, delete and reorder this board's categories from one page; a plain player is refused by the server on every one of those actions"
    requirement: "CONTACTCAT-06"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactCategoryManagement -- 16 passed, 0 failed, including refusal facts for Index and all five write actions and success facts for DM/Admin"
        status: pass
    human_judgment: false
  - id: D2
    description: "Submitting a name that already exists on the board returns the page with a validation message instead of an error page, on both Add and Edit"
    requirement: "CONTACTCAT-04"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactCategory_DuplicateName -- 2 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D3
    description: "Deleting a category that holds contacts leaves those contacts present with no category, asserted against the database"
    requirement: "CONTACTCAT-03"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactCategory_DeleteOrphans -- 1 passed, 0 failed; both contacts read back through a fresh untracked context with CategoryId null"
        status: pass
    human_judgment: false
  - id: D4
    description: "The page exists as both a desktop and a mobile view file for the list and the rename form, with identical reorder/delete/rename controls"
    requirement: "CONTACTCAT-08"
    verification:
      - kind: unit
        ref: "dotnet build -- 0 errors; grep verification of modern-card/modern-card-header/modern-card-body, category-delete-form + data attributes, aria-label + tabindex, location.hash focus script and contacts.mobile.css link across all four view files"
        status: pass
    human_judgment: false
  - id: D5
    description: "No action bypasses the board query filter and no board id is ever read from the request; a category id belonging to another board resolves to not-found"
    requirement: "CONTACTCAT-01"
    verification:
      - kind: unit
        ref: "grep -cE 'IgnoreQueryFilters|Request\\.(Query|Form)\\[' on the controller outputs 0"
        status: pass
    human_judgment: false
  - id: D6
    description: "The category name renders through Razor's default HTML escaping and is never routed through the Markdown pipeline"
    requirement: "CONTACTCAT-13"
    verification:
      - kind: integration
        ref: "Index_Get_CategoryNameWithMarkup_IsHtmlEscaped -- rendered body excludes the raw <script> tag and contains its &lt;script&gt; encoded form"
        status: pass
    human_judgment: false
  - id: D7
    description: "Solution-wide regression: the whole suite stays green after adding the controller, views, and integration tests"
    requirement: "CONTACTCAT-07"
    verification:
      - kind: unit
        ref: "dotnet test (whole solution) -- 437 unit + 646 integration passed, 0 failed (up from the 630 integration baseline recorded by 80-04)"
        status: pass
    human_judgment: false
duration: 95min
completed: 2026-08-30
status: complete
---

# Phase 80 Plan 05: Manage Categories Controller, Views and Integration Suite Summary

**DungeonMasterOnly management controller (add/rename/delete/reorder) with matching desktop and mobile views, and an integration suite that pins the authorization gate, the duplicate-name path via a decorator-forced exception, the delete-orphan guarantee, and reorder boundaries.**

## Session Note

This plan's execution was interrupted mid-run by an API transport error (connection lost, not a code or tooling failure) after Task 1 and Task 2 were already committed and Task 3's test file was drafted but not yet green. Work resumed from the verified worktree state (commits `eca28a85` and `786ce302` already present, an uncommitted controller fix and an untracked test file in the working tree) and continued through to completion in this session. No work was lost; nothing was redone that had already landed.

## Performance

- **Duration:** 95 min (across both sessions)
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- `ContactCategoryManagementController` added under `QuestBoard.Service/Controllers/Contacts/`, `[Authorize(Policy = "DungeonMasterOnly")]` once at class level, covering `Index`, `Add`, `Edit` (GET+POST), `Delete`, `MoveUp`, `MoveDown`
- The duplicate-name path on both `Add` and `Edit` uses `catch (DbUpdateException ex) when (...)` matching "unique"/"duplicate" in the inner exception message, mirroring `GroupController` exactly -- no hand-rolled pre-check
- The board id is stamped only from `IActiveGroupContext.ActiveGroupId`, never accepted from the request; a category id belonging to another board resolves to `NotFound()` through the board-filtered service
- Reorder redirects carry the moved row's `#category-{id}-row` fragment so a DM keeps their place across repeated posts, backed by a focus-restoring script on both list views
- Four view files added under `Views/ContactCategoryManagement/`: `Manage.cshtml`/`Manage.Mobile.cshtml` (inline add form, reorder table/rows, delete confirmation built from `data-*` attributes) and `Edit.cshtml`/`Edit.Mobile.cshtml` (rename-only forms exposing neither sort position nor board id)
- `ContactCategoryManagementControllerIntegrationTests.cs` added with 16 facts: authorization refusal on Index and all five write actions, DM/Admin success on Index, add-and-list, two duplicate-name facts (Add and Edit), delete-orphan proven against the database, three reorder facts (middle-up exchange, first-up no-op, last-down no-op), and heading-escaping
- `.modern-card .text-danger` CSS rule added to `modern-card.css` so validation-error spans inside a modern-card body render as visible red rather than being silently repainted cream by the generic `.modern-card span` rule
- Solution builds with 0 errors; full suite green at 437 unit + 646 integration tests (up from the 630-integration baseline after wave 3)

## Task Commits

Each task was committed atomically:

1. **Task 1: ContactCategoryManagementController** - `eca28a85` (feat)
2. **Task 2: The four Manage Categories view files** - `786ce302` (feat)
3. **Deviation fix: RedirectToAction overload + CRLF restoration + text-danger visibility** - `629748f2` (fix)
4. **Task 3: Management integration suite** - `1484eff5` (test)

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs` - new: six-action management controller
- `QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml` - new: desktop list + inline add form
- `QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml` - new: mobile list + inline add form
- `QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml` - new: desktop rename form
- `QuestBoard.Service/Views/ContactCategoryManagement/Edit.Mobile.cshtml` - new: mobile rename form
- `QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs` - new: 16-fact management suite
- `QuestBoard.Service/wwwroot/css/modern-card.css` - added `.modern-card .text-danger` override

## Decisions Made
- **EF Core InMemory does not enforce `HasIndex().IsUnique()`.** Verified directly with a throwaway diagnostic (removed before the final commit): writing two `ContactCategoryEntity` rows sharing `(GroupId, Name)` through a fresh `TestDatabase` context, through the app's own DI-registered `QuestBoardContext`, and within a single `SaveChangesAsync` batch all succeeded with two rows persisted and no exception, for both an exact-case and a case-differing name. Production's rejection comes entirely from the unique index's SQL Server column collation, which the InMemory provider backing this suite does not replicate at any level. The `ContactCategory_DuplicateName` facts therefore build a per-test host via `factory.WithWebHostBuilder(...)` that swaps `IContactCategoryService` for a decorator (`DuplicateNameThrowingContactCategoryService`) forcing the exact `DbUpdateException` shape a real violation produces, for one specific name, on `AddToEndAsync`/`UpdateAsync` only -- every other call (`GetOrderedAsync`, `GetContactCountsAsync`, etc.) passes through to the real EF-backed service unchanged, so the request completes and re-renders normally. This proves the piece this task actually owns and controls -- the controller's `catch`/`ModelState.AddModelError`/re-render reaction -- without depending on a database-layer guarantee this test harness cannot exercise.
- **`.modern-card .text-danger` was added even though `modern-card.css` isn't in this plan's declared `files_modified`.** Flagged by a reviewer mid-session: `.modern-card span/p` (defined earlier in the same file) repaints any bare `<span>`/`<p>` inside a `.modern-card-body` in cream with a heavy dark shadow, and there was no `.text-danger` override to win on specificity -- meaning every `asp-validation-for` span this plan's four new views render (the duplicate-name error included) would have rendered as ordinary cream body text instead of a visible error. This is a pre-existing gap already present, unfixed, on `GroupController`'s own `Edit.cshtml` (the exact file this plan's action text names as the rename-form template), not something this plan's markup introduced -- but since this plan's own acceptance criteria and behavior spec depend on the duplicate-name message actually being visible, it was fixed here as a Rule 2 (missing critical functionality) addition, scoped to a single new CSS rule matching the existing `.text-muted`/`.form-text` sibling pattern in the same file.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `RedirectToAction` two-argument overload does not exist**
- **Found during:** Task 3, first build against the correct worktree tree (an earlier `dotnet build` had accidentally targeted the shared main checkout, not this worktree, and reported a false "0 errors" -- see Issues Encountered)
- **Issue:** `RedirectToAction(nameof(Index), fragment: $"category-{id}-row")` does not compile. `ControllerBase` has no `(string actionName, string fragment)` overload; the only overload accepting a fragment requires `controllerName` and `routeValues` too.
- **Fix:** Changed both `MoveUp` and `MoveDown` to `RedirectToAction(nameof(Index), controllerName: null, routeValues: null, fragment: $"category-{id}-row")`.
- **Files modified:** `QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs`
- **Verification:** `dotnet build` on the actual worktree tree now succeeds with 0 errors; the `category-{id}-row` acceptance grep still matches.
- **Committed in:** `629748f2` (fix)

**2. [Rule 2 - Missing critical functionality] Validation-error spans invisible inside `.modern-card`**
- **Found during:** Coordinator flag mid-session, referencing the same class of bug fixed by Phase 83 (commit `43a8f052`) for a different element
- **Issue:** `.modern-card span, .modern-card p, .modern-card li, .modern-card small` (specificity `(0,1,1)`, `!important`) repaints every bare `<span>` inside a card body cream, and no `.modern-card .text-danger` override existed to win on specificity -- so every `asp-validation-for="..." class="text-danger"` span in this plan's four new views, including the duplicate-name error message the plan's own behavior spec requires to be visible, would have rendered as unstyled cream text rather than a visible red error.
- **Fix:** Added `.modern-card .text-danger { color: #ff6b6b !important; text-shadow: ... !important; }` to `modern-card.css`, matching the file's existing `.text-muted`/`.form-label`/`.form-text` sibling-rule pattern and color/shadow values.
- **Files modified:** `QuestBoard.Service/wwwroot/css/modern-card.css`
- **Verification:** `dotnet build` succeeds; the rule's specificity `(0,2,0)` beats `.modern-card span`'s `(0,1,1)`.
- **Committed in:** `629748f2` (fix)

**3. [Rule 3 - Blocking issue] EF Core InMemory does not enforce the ContactCategory unique index**
- **Found during:** Task 3, first test run of the two `ContactCategory_DuplicateName` facts (both failed with `302 Found` instead of `200 OK` -- the Add/Edit succeeded instead of being rejected)
- **Issue:** The plan's behavior spec and `80-VALIDATION.md` both require proving a duplicate name is rejected end-to-end through the real HTTP+DB stack. A throwaway diagnostic confirmed the InMemory provider backing this whole suite never enforces `HasIndex(cc => new { cc.GroupId, cc.Name }).IsUnique()`, in any configuration tried (fresh context, DI-registered context, same-batch insert) -- this is a pre-existing test-infrastructure limitation, not a defect in the controller or in this plan's schema.
- **Fix:** Rather than weaken the tests to assert wrong behavior (a successful duplicate create) or silently drop the required coverage, built a per-test host variant (`factory.WithWebHostBuilder`) with a decorator around the real `IContactCategoryService` that forces the exact `DbUpdateException` shape a live unique-index violation produces, for one specific name, on `AddToEndAsync`/`UpdateAsync` only. This is confined entirely to the test file; no controller, service, or repository code changed.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs`
- **Verification:** `dotnet test --filter FullyQualifiedName~ContactCategory_DuplicateName` -- 2 passed, 0 failed; both facts assert the exact ModelState message and that no duplicate row lands in the database.
- **Committed in:** `1484eff5` (test)

---

**Total deviations:** 3 auto-fixed (1 Rule 1, 1 Rule 2, 1 Rule 3). None required architectural sign-off; the Rule 3 fix is confined to test infrastructure and touches no application code.
**Impact on plan:** No scope creep on application behavior. The controller and views match the plan's action text exactly. The CSS fix and the InMemory-limitation workaround are both additive and reversible without touching this plan's core deliverables.

## Issues Encountered

**Wrong build target during initial verification (self-caught, no lasting effect).** Early in this session, `dotnet build`/`dotnet test` commands were run with `cd "C:/Repos/quest-board"` (the shared main checkout) instead of the isolated worktree at `.claude/worktrees/agent-aa78b9bff25ca0101`. Because the shared checkout's own tree happened to build cleanly on old code, this produced a false "0 errors" for Task 1 and Task 2 before the `RedirectToAction` bug (deviation 1 above) was ever caught. Every acceptance-criteria `grep` in this session was run against explicit absolute worktree paths throughout and was never affected. Once noticed, every subsequent build/test command targeted the worktree's own `QuestBoard.slnx`/`.csproj` files by absolute path with no `cd`, and the real compile error surfaced immediately on the first correctly-targeted build.

**EF Core InMemory unique-index non-enforcement.** See deviation 3 above. This is a suite-wide test-infrastructure characteristic (confirmed to affect `GroupEntity.Name`'s equivalent unique index too, by inspection -- no existing integration test in this codebase had ever previously tried to exercise a `DbUpdateException`-driven duplicate-name path, so the gap had not surfaced before this plan). Left as-is beyond this plan's own two tests; not this plan's job to re-architect the suite's database provider.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `ContactCategoryManagementController` and its four views are complete and match the UI-SPEC's Component Spec 3 markup.
- The management integration suite selects and passes under all three filter names `80-VALIDATION.md` promises: `ContactCategoryManagement` (16 tests), `ContactCategory_DeleteOrphans` (1 test), `ContactCategory_DuplicateName` (2 tests).
- The `.modern-card .text-danger` override is now available to any other card-hosted validation span in the app, not just this plan's views.
- No blockers for the remaining phase-80 plans (grouped-index rendering, mobile-render tests, cross-group isolation) -- this plan's controller, views and CSS surface are stable inputs for them.

---
*Phase: 80-contact-categories*
*Completed: 2026-08-30*
