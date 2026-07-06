---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 05
subsystem: ui
tags: [razor, mvc, bootstrap5, fontawesome, contacts, npc-directory]

# Dependency graph
requires:
  - phase: 57-04
    provides: ContactsController, ContactViewModel/ContactsIndexViewModel/ContactNoteViewModel, all Contacts routes (Index/Details/Create/Edit/Delete/ToggleReveal/ToggleShowHidden/AddNote/EditNote/DeleteNote/GetContactImage)
provides:
  - Four desktop Razor views for the Contacts feature (Index/Details/Edit/Create)
  - contacts.css scoped under .contacts-page mirroring characters.css conventions
  - Contacts nav link in both desktop and mobile shared layouts, visible to all users on all board types
affects: [57-06 (mobile views for the same feature)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Notes list inline edit-in-place via vanilla JS show/hide toggle of sibling view/edit blocks (no modal, no framework) — same lightweight-JS convention as the Character Classes add/remove list"
    - "Two-state pressed/toggle button (Show Hidden/Hide Hidden) using filled-vs-outline Bootstrap swap + aria-pressed, POST-and-redirect (no AJAX), mirroring the existing ToggleRetirement pattern"

key-files:
  created:
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/Views/Contacts/Index.cshtml
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Create.cshtml
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "Fixed a Wave 0 RED-scaffold test assertion that compared rendered HTML against a raw apostrophe string; Razor correctly HTML-encodes it to &#x27;, so the assertion was updated to match the encoded form rather than bypassing HTML encoding in the view."

patterns-established:
  - "Contacts feature views mirror Characters view structure 1:1 (grid/card/portrait/actions/form layout) with label substitutions only, per UI-SPEC Mirrored Patterns section."

requirements-completed: []

# Metrics
duration: 45min
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 05: Contacts Desktop Views Summary

**Four desktop Razor views (Index/Details/Edit/Create) plus contacts.css and nav insertions realizing the UI-SPEC's Notes list, Show Hidden toggle, Hidden badge, and Reveal/Hide action patterns.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-07-06T19:35:00Z
- **Completed:** 2026-07-06T20:21:23Z
- **Tasks:** 3 completed
- **Files modified:** 8 (5 created, 3 modified)

## Accomplishments
- Built `contacts.css` mirroring `characters.css`'s grid/card/badge rules under a `.contacts-page` scope, with a secondary-gray `.hidden-badge` (not the dark dead-badge treatment)
- Inserted the Contacts nav link into both desktop and mobile layouts, unconditionally (all board types), plus the global `contacts.css` link
- Built the Index view: flat alphabetical `.contact-grid`, DM-tier two-state Show Hidden toggle (`btn-outline-secondary`/"Show Hidden" OFF → `btn-secondary`/"Hide Hidden" ON), DM-tier "+ Contact" button, Hidden badges, empty state
- Built Edit/Create form views: h4 card headers, Name/Image/Description/TownCity/SubLocation fields, reused Character image-validation script, Save/Cancel row after `<hr>`, no notes UI
- Built Details view: portrait card with Hidden badge, CanManage-gated Actions card (Edit/Reveal-Hide toggle/Delete), Description card, collaborative Notes card (ungated) with add-note form, newest-first note list, inline edit-in-place JS, native-confirm delete, empty state

## Task Commits

Each task was committed atomically:

1. **Task 1: Create contacts.css, insert nav links + global css link, and build the desktop Index view** - `d697165` (feat)
2. **Task 2: Create desktop Edit and Create form views** - `2b675d7` (feat)
3. **Task 3: Create desktop Details view** - `cafb333` (feat)

**Deviation fix:** `7cb43eb` (fix — HTML-encoding test assertion, see below)

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/contacts.css` - `.contacts-page` grid/card/hidden-badge styling mirroring characters.css
- `QuestBoard.Service/Views/Contacts/Index.cshtml` - Flat grid list, Show Hidden toggle, + Contact button, Hidden badges
- `QuestBoard.Service/Views/Contacts/Details.cshtml` - Portrait/Actions/Description/Notes cards
- `QuestBoard.Service/Views/Contacts/Edit.cshtml` - Edit form (h4 header "Edit {Name}")
- `QuestBoard.Service/Views/Contacts/Create.cshtml` - Create form (h4 header "Create New Contact")
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Contacts nav `<li>` + contacts.css link
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Contacts nav `<li>` (mobile spacing)
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - Fixed one assertion's HTML-encoding expectation

## Decisions Made
- Reused `Url.Action` for links exactly as the Characters views do, rather than switching to tag helpers, to keep the mirror as close to verbatim as the UI-SPEC intends.
- Implemented the notes inline edit-in-place as three sibling DOM nodes (view text, view actions, edit form) with a single querySelectorAll wiring block, matching the lightweight-JS precedent set by the Character Classes add/remove list rather than introducing a component library.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed HTML-encoding mismatch in a Wave 0 scaffold test assertion**
- **Found during:** Task 3 verification (full `dotnet test` run)
- **Issue:** `Index_HiddenContact_CreatorSeesOwnHiddenContactInIndex` asserted the rendered Index HTML contained the raw string `"Creator's Own Hidden In Index"`. Razor's default `HtmlEncoder` correctly encodes the apostrophe to `&#x27;` when rendering `@contact.Name`, so the literal-apostrophe assertion could never pass once a real (XSS-safe) view existed — this was a latent bug in the Wave 0 RED scaffold, not a fault in the Index view.
- **Fix:** Updated the assertion to check for the HTML-encoded form (`Creator&#x27;s Own Hidden In Index`) instead of bypassing/disabling HTML encoding in the view (which would reintroduce an XSS risk).
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`
- **Verification:** `dotnet test` filter `ContactsControllerIntegrationTests` — 20/20 pass; full `dotnet test` — 542/542 pass (191 unit + 351 integration).
- **Committed in:** `7cb43eb`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary to satisfy the plan's "Full `dotnet test` remains green (no regressions)" verification gate. No scope creep — the view itself was not changed as part of this fix.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Desktop Contacts views are complete and fully wired to the Plan 04 controller/routes.
- Plan 06 (mobile views) can proceed independently — this plan's `contacts.css` and nav insertions are shared infrastructure already in place; Plan 06 only needs to add the mobile-specific view templates.
- Full test suite green (542/542) with no regressions from either the view work or the one test fix.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*
