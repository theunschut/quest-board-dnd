---
phase: 80-contact-categories
plan: 08
subsystem: ui
tags: [razor-views, integration-tests, mobile-detection, aspnet-core-mvc]

requires:
  - phase: 80-contact-categories
    provides: "ContactViewModel.CategoryId/CategoryName/CategorySortOrder populated via ContactRepository's .Include(c => c.Category) and both AutoMapper profiles (80-02 through 80-04); ContactsController.Details already mapping the view model with no controller change needed"
provides:
  - "Details.cshtml/Details.Mobile.cshtml category line (fa-tag icon, no label prefix, muted paragraph peer of TownCity/SubLocation), rendering nothing for an uncategorised contact"
  - "ContactDetailsCategoryTests -- assigned/unassigned/HTML-escaping coverage for the Details category line"
  - "ContactCategoryMobileRenderTests -- real-User-Agent proof that Manage.Mobile.cshtml, Index.Mobile.cshtml and Details.Mobile.cshtml are the files the server actually selects, each paired against a desktop-UA request to the same URL"
  - "80-VALIDATION.md Status column reflects actual green/pending state; documents the parallel-wave gap for the two 80-07-owned filters"
affects: [80-contact-categories]

tech-stack:
  added: []
  patterns:
    - "Category line mirrors the existing TownCity/SubLocation icon+text paragraph shape exactly (desktop text-muted, mobile contact-info-value) rather than inventing a new field type -- matches UI-SPEC Component Spec 7 verbatim"
    - "Mobile-selection proof pairs every mobile-UA request against a plain request to the same URL inside the same fact, asserting a mobile-only marker class is present in one and absent from the other -- copied verbatim from AgendaMobileRenderTests.GetMobileAsync, the only mechanism that actually exercises .Mobile.cshtml selection"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/ContactDetailsCategoryTests.cs
    - QuestBoard.IntegrationTests/Tests/ContactCategoryMobileRenderTests.cs
  modified:
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - .planning/phases/80-contact-categories/80-VALIDATION.md

key-decisions:
  - "Category line placed directly after SubLocation, before the hidden badge, using the exact TownCity/SubLocation muted-paragraph markup shape from UI-SPEC Component Spec 7 -- no label prefix, fa-tag icon carries the meaning alone."
  - "No new CSS added for the category line. It mirrors SubLocation's own `<p class=\"text-muted\">`/`<p class=\"contact-info-value\">` treatment exactly; that pattern is pre-existing precedent on this same page, not new risk, and the UI-SPEC's canonical markup for this exact component specifies the identical class."
  - "Mobile-only marker classes chosen per surface: category-mgmt-row (management page, absent from desktop's <table> rows), contact-member-row (contacts index, absent from desktop's contact-card grid), contact-info-value (contact details, absent from desktop's text-muted paragraphs) -- each verified absent from its desktop counterpart before use."
  - "80-VALIDATION.md Status column left honest rather than fully green: ContactCategory_CrossGroup and ContactCategory_DisabledSelect are owned by 80-07, which is executing in a separate git worktree concurrently and has not merged into this worktree. Both filters correctly resolve to zero tests here -- not a bug in this plan's work, a structural consequence of parallel wave execution. wave_0_complete stays false with a note directing the orchestrator to re-run the two filters plus a full dotnet test after both wave-6 worktrees merge."

requirements-completed: [CONTACTCAT-08, CONTACTCAT-09, CONTACTCAT-10, CONTACTCAT-14]

coverage:
  - id: D1
    description: "An assigned contact's category name renders on both Details.cshtml and Details.Mobile.cshtml as a peer of the TownCity/SubLocation lines, with no label prefix"
    requirement: "CONTACTCAT-14"
    verification:
      - kind: integration
        ref: "ContactDetailsCategoryTests.ContactDetails_Category_AssignedContactShowsCategoryName -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D2
    description: "An unassigned contact's Details page shows no category line and no 'Ungrouped' leak, since that label is an index-level grouping device, not a contact property"
    requirement: "CONTACTCAT-14"
    verification:
      - kind: integration
        ref: "ContactDetailsCategoryTests.ContactDetails_Category_UnassignedContactShowsNoCategoryLineOrUngroupedLabel -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D3
    description: "A category name containing markup renders HTML-escaped on the Details page and is never routed through the Markdown service"
    requirement: "CONTACTCAT-14"
    verification:
      - kind: integration
        ref: "ContactDetailsCategoryTests.ContactDetails_Category_NameWithAngleBracketsRendersEscaped -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D4
    description: "Manage.Mobile.cshtml is the file actually selected and rendered under a real mobile User-Agent, proven against a paired desktop-UA request to the same URL"
    requirement: "CONTACTCAT-08"
    verification:
      - kind: integration
        ref: "ContactCategoryMobileRenderTests.ContactCategoryMobileRender_ManagementPage_MobileUserAgentSelectsMobileFile_DesktopUserAgentDoesNot -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D5
    description: "Index.Mobile.cshtml is selected under a real mobile User-Agent, rendering both category names and the Ungrouped heading positioned after them, on a board with two categories and one uncategorised contact"
    requirement: "CONTACTCAT-09, CONTACTCAT-10"
    verification:
      - kind: integration
        ref: "ContactCategoryMobileRenderTests.ContactCategoryMobileRender_ContactsIndex_MobileUserAgentSelectsMobileFileWithCategoriesAndUngroupedLast_DesktopUserAgentDoesNot -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D6
    description: "Details.Mobile.cshtml is selected under a real mobile User-Agent and renders the contact's category name"
    requirement: "CONTACTCAT-14"
    verification:
      - kind: integration
        ref: "ContactCategoryMobileRenderTests.ContactCategoryMobileRender_ContactDetails_MobileUserAgentSelectsMobileFileWithCategoryName_DesktopUserAgentDoesNot -- 1 passed, 0 failed"
        status: pass
    human_judgment: false
  - id: D7
    description: "Solution-wide regression from this worktree's vantage point: whole suite stays green after the Details edits and the new mobile-render suite"
    requirement: "CONTACTCAT-08, CONTACTCAT-09, CONTACTCAT-10, CONTACTCAT-14"
    verification:
      - kind: unit
        ref: "dotnet test (whole solution, this worktree) -- 437 unit + 660 integration passed, 0 failed (up from the 654-integration baseline recorded by 80-06)"
        status: pass
    human_judgment: false
  - id: D8
    description: "The two validation-contract filters owned by 80-07 (ContactCategory_CrossGroup, ContactCategory_DisabledSelect) require 80-07's worktree to merge before they can resolve from here -- a human/orchestrator must re-run them post-merge"
    verification: []
    human_judgment: true
    rationale: "This plan ran in an isolated parallel worktree that branched before 80-07's commits existed. The two filters correctly find zero tests here because the files they target live in 80-07's branch, not a defect this plan can fix or verify. Only the orchestrator, after merging both wave-6 worktrees, can confirm these filters pass and flip wave_0_complete to true."

duration: 25min
completed: 2026-08-30
status: complete
---

# Phase 80 Plan 08: Contact Details Category Line and Mobile Selection Proof Summary

**Category line on both Contacts/Details views (fa-tag icon, no label, omitted entirely when uncategorised) plus a real-User-Agent integration suite proving Manage.Mobile.cshtml, Index.Mobile.cshtml and Details.Mobile.cshtml are the files the server actually serves, each paired against a desktop request to the same URL.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3
- **Files modified:** 5 (2 views edited, 2 test files created, 1 validation doc updated)

## Accomplishments
- `Details.cshtml` and `Details.Mobile.cshtml` each gained a category line directly after `SubLocation` and before the hidden badge -- muted paragraph, `fa-tag` icon, plain Razor interpolation (default-encoded, never routed through `Html.Markdown`), rendered only when `Model.CategoryName` is non-empty
- `ContactDetailsCategoryTests.cs` (new) proves the assigned/unassigned/escaping trio the plan's `<behavior>` block specifies, with a private `AssignContactCategoryAsync` helper matching the pattern `ContactsControllerIntegrationTests.cs` already established
- `ContactCategoryMobileRenderTests.cs` (new) reuses the `GetMobileAsync` helper shape verbatim from `AgendaMobileRenderTests` and proves, for each of the three mobile surfaces this phase touches, that a real mobile User-Agent selects the `.Mobile.cshtml` file (via a marker class absent from that file's desktop counterpart) while a plain request to the identical URL does not
- The index-mobile fact also asserts the "Ungrouped" heading renders after both real category names on the mobile-selected response specifically, not just somewhere in a merged desktop+mobile string
- Ran the whole solution's test suite from this worktree: 437 unit + 660 integration passed, 0 failed (654 baseline from 80-06 plus this plan's 6 new facts)
- Updated `80-VALIDATION.md`'s Status column: 10 of 12 named filters verified green from this worktree; the 2 filters owned by 80-07 left pending with an explanatory note (see Deviations)

## Task Commits

Each task was committed atomically:

1. **Task 1: Show the category on both contact Details views** - `fb0d307d` (feat)
2. **Task 2: Prove the mobile views are actually selected, with a real mobile user agent** - `3c1453e6` (test)
3. **Task 3: Phase gate -- full suite green and the manual verification handoff** - `1cdcfc5f` (docs)

## Files Created/Modified
- `QuestBoard.Service/Views/Contacts/Details.cshtml` - category line added after `SubLocation`, matching its own muted-paragraph shape
- `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` - equivalent line using `contact-info-value`
- `QuestBoard.IntegrationTests/Controllers/ContactDetailsCategoryTests.cs` - 3 new facts (assigned, unassigned, escaping)
- `QuestBoard.IntegrationTests/Tests/ContactCategoryMobileRenderTests.cs` - 3 new facts (management page, contacts index, contact details), each pairing a mobile-UA request against a plain request to the same URL
- `.planning/phases/80-contact-categories/80-VALIDATION.md` - Status column updated for verifiable rows; parallel-wave note added; `wave_0_complete` left `false` pending post-merge re-check

## Decisions Made
- **No new CSS for the category line.** It reuses `SubLocation`'s exact existing class (`text-muted` desktop, `contact-info-value` mobile) per UI-SPEC Component Spec 7's canonical markup -- this is precedent already established on this same page, not new risk introduced by this plan. Verified the rendered markup (not just the stylesheet) via the passing integration tests, per the css_constraint guidance in this plan's brief.
- **Mobile-only markers chosen deliberately per surface**, each confirmed absent from its desktop counterpart before use: `category-mgmt-row` (Manage.Mobile vs. Manage.cshtml's `<table>`), `contact-member-row` (Index.Mobile vs. Index.cshtml's `.contact-card` grid), `contact-info-value` (Details.Mobile vs. Details.cshtml's `text-muted` paragraphs).
- **80-VALIDATION.md Status column left honest, not fully green.** The plan's own Task 3 action text says: "If a row's command does not pass, leave that row red and say so in the summary rather than adjusting the row." Two filters (`ContactCategory_CrossGroup`, `ContactCategory_DisabledSelect`) are owned by 80-07, executing concurrently in a separate git worktree that has not merged into this one -- both correctly resolve to zero tests here. Rather than fabricate a green status for work this worktree cannot see, both rows are left pending with a note, and `wave_0_complete` stays `false`. This is a genuine structural gap from parallel wave execution, not a literal-grep wording mismatch, so it is documented here rather than smoothed over per the acceptance-criteria guidance's own boundary (verifying evident intent applies to wording collisions, not to substantive unverifiable claims).

## Deviations from Plan

### Acceptance-Check Note (not a Rule 1-3 auto-fix; parallel-execution structural gap)

**`80-VALIDATION.md` acceptance criteria demand `wave_0_complete: true` and all 12 named filters resolving.** From this isolated worktree, 10 of 12 resolve and pass. The remaining two (`ContactCategory_CrossGroup`, `ContactCategory_DisabledSelect`) are owned by plan 80-07, which is executing in a separate worktree concurrently (per this plan's `<parallel_execution>` context) and has not merged into this branch. Both filters correctly report "No test matches" here because the files they target genuinely do not exist in this worktree yet -- not a defect in this plan's own Task 1/2 work. Per the plan's own Task 3 action text ("leave that row red and say so in the summary rather than adjusting the row"), both rows were left pending and `wave_0_complete` was left `false`, with a note in `80-VALIDATION.md` and here directing the orchestrator to re-run the two filters plus a full `dotnet test` after both wave-6 worktrees merge, then flip the flag.

No code was contorted and no false status was recorded to force a literal match. This is the one open item this plan hands off to the orchestrator's post-merge verification step.

---

**Total deviations:** 0 auto-fixed. One structural acceptance gap (wave_0_complete / 2 pending filters) documented and handed off rather than fabricated, per the plan's own explicit fallback instruction.
**Impact on plan:** None on this plan's own deliverables (Tasks 1 and 2 fully verified and green). Task 3's phase-gate claim is honestly scoped to what this worktree can see; full closure requires the wave-6 merge.

## Issues Encountered

**cwd drift into the main repo during Task 1 verification.** The plan's literal `<verify>` commands use `cd "C:/Repos/quest-board"`, which is this worktree's *main* repo root, not this parallel worktree's own root (`.claude/worktrees/agent-a0a8be04c0a7a5d17`). Running that literal command builds/tests the main checkout instead of this worktree's changes and silently reports "no test matches" for newly added files. Recovered by re-running every build/test command from the actual worktree root (`git rev-parse --show-toplevel`) for the remainder of the plan. No files were affected; this only affected which binaries got exercised during verification.

**Write tool emits LF-only files; this repo requires CRLF (CLAUDE.md).** Both new test files were converted to CRLF with `sed -i 's/$/\r/'` immediately after creation, matching the same fix 80-06 documented for the same tool behavior. Files edited via the `Edit` tool (both `.cshtml` views) preserved their existing CRLF convention with no extra step.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The Details pages (desktop and mobile) are now the feature's complete category-visibility surface, alongside the grouped index from 80-06.
- Once 80-07 merges (Create/Edit disabled-select, cross-group isolation, and `ContactsController.cs`), the orchestrator must re-run `ContactCategory_CrossGroup` and `ContactCategory_DisabledSelect` plus a full `dotnet test`, update the two remaining `80-VALIDATION.md` rows to green, and set `wave_0_complete: true`.
- The two Manual-Only Verifications recorded in `80-VALIDATION.md` (real-handset legibility/tap-targets, and the first-run zero-category discovery path) remain outstanding by design -- this is the phase's handoff to `/gsd-verify-work`, not a gap in this plan.

---
*Phase: 80-contact-categories*
*Completed: 2026-08-30*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Controllers/ContactDetailsCategoryTests.cs
- FOUND: QuestBoard.IntegrationTests/Tests/ContactCategoryMobileRenderTests.cs
- FOUND: QuestBoard.Service/Views/Contacts/Details.cshtml
- FOUND: QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
- FOUND: commit fb0d307d (Task 1)
- FOUND: commit 3c1453e6 (Task 2)
- FOUND: commit 1cdcfc5f (Task 3)
