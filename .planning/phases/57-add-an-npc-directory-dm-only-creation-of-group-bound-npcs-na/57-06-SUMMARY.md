---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 06
subsystem: ui
tags: [razor, mobile, contacts, css, mobile-parity]

# Dependency graph
requires:
  - phase: 57-05
    provides: Contacts desktop views (Index/Details/Edit/Create.cshtml), ContactsController, ViewModels
provides:
  - Four mobile Contact views (Index/Details/Edit/Create.Mobile.cshtml) with full desktop parity
  - Three mobile stylesheets (contacts.mobile.css, contact-detail.mobile.css, contact-form.mobile.css)
affects: [57-phase-closure, future-mobile-parity-audits]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mobile views mirror the Characters mobile CSS/Razor pattern (glass section card, tappable member rows, parchment text treatment) with class names renamed contact-* instead of character-*"

key-files:
  created:
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.mobile.css
    - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
    - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
  modified:
    - QuestBoard.Service/Automapper/ViewModelProfile.cs

key-decisions:
  - "Notes card note-item blocks kept as light bordered white cards (not parchment-glass text) on mobile, mirroring the desktop note-item convention exactly, rather than applying the glass-card catch-all parchment override used elsewhere in contact-detail.mobile.css"
  - "Show Hidden toggle and + Contact button laid out as a full-width two-button row (.contact-toggle-row) above the Contacts section card on mobile, since the desktop's inline header-row layout doesn't fit mobile viewport width"

patterns-established: []

requirements-completed: []

# Metrics
duration: ~2 hours (incl. human-verify checkpoint wait time)
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 06: Mobile Contacts Views Summary

**Four mobile Contact views (Index/Details/Edit/Create.Mobile.cshtml) plus three mobile stylesheets, mirroring the Characters mobile CSS/Razor pattern with full desktop parity. Human verification checkpoint passed after one real bug (found live, not in the original plan) was diagnosed and fixed.**

## Performance

- **Started:** 2026-07-06T20:24:00Z (approx)
- **Task 1 completed:** 2026-07-06T20:30:00Z (approx)
- **Task 2 (human verification) completed:** 2026-07-06 (checkpoint approved by user after mobile + image-upload re-test)
- **Tasks:** 2/2 complete
- **Files modified:** 8 (7 new, 1 fixed during checkpoint)

## Accomplishments
- Created `contacts.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css` mirroring the Characters mobile CSS trio (glass section card, tappable list rows, mobile toggle/badge sizing, stacked-card detail/form layout), with `character-*` selectors renamed to `contact-*`.
- Created `Index.Mobile.cshtml`: flat alphabetical tappable list (thumbnail + name + town/city + sub-location per row → Details), the two-state Show Hidden toggle (`btn-outline-secondary`/"Show Hidden" ↔ `btn-secondary`/"Hide Hidden", both `aria-pressed`) and the "+ Contact" `btn-warning` button gated on `Model.ViewerIsDmTier`, plus a secondary "Hidden" badge on unrevealed rows.
- Created `Details.Mobile.cshtml`: stacked portrait card, Actions card gated on `Model.CanManage` (Edit/Reveal-or-Hide/Delete with the exact confirm string), Description card, and the full collaborative Notes UI (add-note form, newest-first note list with inline edit-in-place JS toggle and native-confirm delete) — the notes UI is NOT gated on `CanManage`, available to all viewers per D-09.
- Created `Edit.Mobile.cshtml` / `Create.Mobile.cshtml`: core-field forms (Name/Image/Description/TownCity/SubLocation) with the same client-side image validation script as desktop, no notes UI.
- Verified via `dotnet build QuestBoard.Service` (succeeded, 0 warnings/errors) and `dotnet test` (542/542 tests passing: 191 unit + 351 integration).

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the three mobile stylesheets and the four mobile Contact views** - `28de384` (feat)
2. **Task 2: Human verification checkpoint** - approved by user after mobile parity + image-upload re-test. The bug found mid-checkpoint (see Deviations) was fixed in `d9ea6d3`.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/contacts.mobile.css` - Mobile Index list styling (glass section card, tappable rows, hidden badge, toggle row)
- `QuestBoard.Service/wwwroot/css/contact-detail.mobile.css` - Mobile Details stacked-card styling (portrait, actions, description, notes)
- `QuestBoard.Service/wwwroot/css/contact-form.mobile.css` - Mobile Edit/Create form styling
- `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` - Mobile Contacts list view
- `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` - Mobile Contact detail incl. notes UI
- `QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml` - Mobile Edit form
- `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` - Mobile Create form

## Decisions Made
- Notes card `.note-item` blocks are explicitly excluded from the glass-card parchment-text catch-all in `contact-detail.mobile.css` (given `color: inherit`/`text-shadow: none` overrides) so they render as plain bordered white cards on mobile, matching the desktop note-item visual exactly rather than adopting the surrounding glass-card's parchment treatment.
- The Show Hidden toggle and "+ Contact" button are laid out as a full-width two-button flex row (`.contact-toggle-row`) positioned above the Contacts section card, since the desktop's `d-flex justify-content-between` header-row (title + buttons side by side) doesn't fit a narrow mobile viewport — this mirrors how Characters mobile puts its "Create New Character" button as a standalone `w-100` button above the section cards.

## Deviations from Plan

### Bug Found During Human Verification (not auto-fixed by an executor — diagnosed live with the user)

**1. [Bug] AutoMapper silently dropped the uploaded image on Contact Create**
- **Found during:** Task 2 human verification, step 2 (Create with image upload) — user reported "uploaded a 543 KB PNG, image not saved."
- **Root cause:** `ContactViewModel.ContactImage` and `Contact.ContactImageData` (the domain model, from Plan 03) have different property names. `CreateMap<ContactViewModel, Contact>()`/`CreateMap<Contact, ContactViewModel>()` in `ViewModelProfile.cs` relied on AutoMapper's default convention-based member matching, which only connects properties with matching names — so the two were never wired. The `Contact`↔`ContactEntity` mapping (Entity/Domain boundary, Plan 03) was correctly configured and unaffected; only the Service-layer ViewModel↔Domain boundary had the gap. Character's equivalent (`ProfilePicture`) never hit this because both sides use the same name there.
- **Fix:** Added explicit `.ForMember(dest => dest.ContactImage, opt => opt.MapFrom(src => src.ContactImageData))` and the reverse `.ForMember(dest => dest.ContactImageData, opt => opt.MapFrom(src => src.ContactImage))` to both `CreateMap` calls in `ViewModelProfile.cs`.
- **Why the Wave 0 test suite didn't catch this:** `ContactRepositoryTests` exercises the repository directly against `ContactEntity`/`Contact` (bypassing the ViewModel layer entirely), and no integration test asserted an end-to-end Create-with-image round-trip through the full ViewModel→Domain→Entity chain. This is a real test-coverage gap, not a process failure — the human-verify checkpoint existed precisely to catch exactly this class of bug.
- **Files modified:** `QuestBoard.Service/Automapper/ViewModelProfile.cs`
- **Verification:** `dotnet build`/`dotnet test` (542/542, run after Wave 4) confirmed no regression; user manually re-tested image upload after the fix and approved.
- **Committed in:** `d9ea6d3` (orchestrator-applied directly to `milestone/v7-backlog-cleanup`, since the app was running live under the user's debugger/IDE during the checkpoint — not inside this plan's worktree).

---

**Total deviations:** 1 bug (found and fixed live during human verification, not by an executor's self-check).
**Impact on plan:** Necessary — without this fix, Create-with-image silently produced imageless Contacts. No scope creep; the fix is a 2-line explicit AutoMapper wiring addition, not a redesign.

## Issues Encountered

The AutoMapper gap above. No other issues.

## Checkpoint Reached

**Type:** human-verify
**Task:** Task 2 — Human verification of the full Contacts feature (desktop + mobile)
**Status:** **Approved.** User verified nav visibility, create/default-hidden, three-branch hidden visibility (including as Player), per-group toggle + session reset, reveal, collaborative notes, edit/delete, mobile parity (all 4 mobile views), and — after the AutoMapper fix — image upload. UI-SPEC copy/style conformance confirmed.

Mid-checkpoint, the mobile-views worktree (`worktree-agent-a174bfb9cda7ea130`, Task 1's commits) was merged into `milestone/v7-backlog-cleanup` ahead of the normal end-of-plan merge point, specifically so the user's already-running app could serve the new mobile view files for testing — the standard workflow only merges a plan's worktree once the plan is fully complete, but the files needed to exist in the live checkout for verification to be possible at all.

## Next Phase Readiness

- Full Contacts feature (Phase 57) complete: entities/migration, domain/repository/service, controller, desktop + mobile views, nav integration, and the human-verified visibility/notes/toggle/image-upload behaviors.
- Full test suite green (542/542) after the AutoMapper fix, confirmed via `dotnet build`/`dotnet test` re-run.
- Ready for phase-level goal verification and closure.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*

## Self-Check: PASSED

All created files verified present on disk (mobile views + stylesheets); `ViewModelProfile.cs` fix verified present; task/commit hashes verified in git log (`28de384`, `8b64138`, `d9ea6d3`, merge commit for the worktree).
