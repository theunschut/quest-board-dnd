---
phase: 81-contact-tags-and-filtering
plan: 08
subsystem: ui
tags: [aspnetcore-mvc, razor, bootstrap-offcanvas, mobile-views, contact-tags]

# Dependency graph
requires:
  - phase: 81-06
    provides: "ContactsIndexViewModel.AvailableTags/SelectedTagIds/HasActiveFilters, ContactViewModel.Tags, and the ContactsController Index/ToggleShowHidden query-string wiring the mobile markup binds to"
provides:
  - "contacts.mobile.css and contact-detail.mobile.css -- .contact-tag-list/.contact-tag-chip, .contact-filter-check-label, .contact-filter-empty, matching the chip values contacts.css uses on desktop"
  - "Index.Mobile.cshtml -- full-width Filter Tags trigger with an Active badge, #contactFilterOffcanvas bottom drawer of tag checkboxes, per-row tag chips, hidden tag inputs on the Show Hidden toggle form, and a three-branch empty state (contacts-exist / no-match-with-filter / genuinely-empty)"
  - "Details.Mobile.cshtml -- a muted, escaped fa-tags line inside the portrait card for DM-tier viewers with at least one tag"
  - "ContactsTagsMobileTests.cs -- 8 integration tests proving every mobile tag surface renders under a real mobile User-Agent, is absent for a player, and genuinely differs from the same url under a desktop User-Agent"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mobile filter trigger + offcanvas drawer follows Shop/Index.Mobile.cshtml's #shopFilterOffcanvas structure exactly (trigger button with Active badge, bottom offcanvas, checkbox rows, Apply/Clear buttons in a d-flex gap-2 row), scoped to Contacts' own id and copy"
    - "The tag chip's own CSS rule is explicitly excluded from contact-detail.mobile.css's span:not(.badge) catch-all parchment override (span:not(.badge):not(.contact-tag-chip)), so the chip's own background/border/color render instead of being force-overridden by the page's muted-text convention"
    - "Empty-state branches ordered contacts-exist (two sub-cases: categorized/flat) -> no-match-with-active-filter -> genuinely-empty, preserving the original genuinely-empty copy verbatim"

key-files:
  created:
    - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/contacts.mobile.css
    - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml

key-decisions:
  - "Placed the Details.Mobile tag line after the CategoryName paragraph (not literally between SubLocation and CategoryName) so it still satisfies 'immediately before the hidden-badge div' -- CategoryName already sat between SubLocation and the hidden badge before this plan touched the file"
  - "Substituted the desktop-side marker in the paired mobile/desktop layout-selection test: asserted on the existing '.contact-card' class (desktop's card-grid wrapper) rather than the not-yet-landed '.contact-filter-row' class from the sibling desktop plan (81-07), because 81-07 runs in a parallel wave-6 worktree isolated from this one and its class does not exist in this worktree's copy of Index.cshtml at execution time. '.contact-card' still proves the same thing (the two user agents render genuinely different files) and remains true after both wave-6 worktrees merge, since tag chips are added as children of .contact-card's .contact-info, not as a replacement for it"
  - "The 'no contacts match your filters' empty-state branch is only reachable when the board has zero visible contacts at all: the shared ApplyTagFilter helper (built in an earlier phase-81 plan) falls back to the full visible list whenever a tag selection matches zero contacts, so a filter that matches nothing while contacts exist never produces an empty page. The dedicated no-match test therefore uses an empty board with a fabricated tag id in the query string rather than a tagged-but-non-matching contact"

requirements-completed: [CONTACTTAG-01, CONTACTTAG-07, CONTACTTAG-08, CONTACTTAG-10, CONTACTTAG-11, CONTACTTAG-14, CONTACTTAG-15, CONTACTTAG-16, CONTACTTAG-17]

coverage:
  - id: D1
    description: "A DM on a real mobile device sees a full-width Filter Tags button above the contact list; tapping it opens a bottom drawer (#contactFilterOffcanvas) listing a checkbox per visible tag, pre-checked from the current selection, with Apply Filters / Clear Filters actions"
    requirement: CONTACTTAG-08
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileUserAgent_RendersFilterTriggerAndDrawer"
        status: pass
    human_judgment: false
  - id: D2
    description: "Before the board has any tags, the trigger still renders (disabled) with a muted helper sentence beneath it, and no drawer markup is emitted"
    requirement: CONTACTTAG-17
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileBoardWithNoTags_RendersDisabledTriggerAndHint"
        status: pass
    human_judgment: false
  - id: D3
    description: "Each contact's tags render as chips on its mobile row and as a muted fa-tags line on its mobile details page, for DM-tier viewers with at least one tag"
    requirement: CONTACTTAG-14
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Details_MobileDungeonMaster_RendersTagChips"
        status: pass
    human_judgment: false
  - id: D4
    description: "A player receives zero tag markup on either mobile page -- no chip class, no drawer id, no filter trigger copy, and no tag name in the response"
    requirement: CONTACTTAG-16
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobilePlayer_ReceivesNoTagMarkupAtAll"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Details_MobileDungeonMaster_RendersTagChips"
        status: pass
    human_judgment: false
  - id: D5
    description: "Flipping Show Hidden from the mobile index carries the current tag selection through the redirect via hidden inputs on the toggle form, and an active filter shows the trigger's Active badge"
    requirement: CONTACTTAG-07
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileActiveFilter_ShowsActiveBadgeAndCarriesSelectionOnToggleForm"
        status: pass
    human_judgment: false
  - id: D6
    description: "A tag borne only by a contact this viewer cannot currently see (unrevealed, not their own, Show Hidden off) is absent from the drawer's checkbox vocabulary"
    requirement: CONTACTTAG-11
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileTagOnlyOnUnrevealedContact_AbsentFromDrawer"
        status: pass
    human_judgment: false
  - id: D7
    description: "A filter that matches nothing on an otherwise-empty board shows the no-match heading and body copy, distinct from the genuinely-empty message, which survives verbatim"
    requirement: CONTACTTAG-10
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileActiveFilterNoMatches_RendersNoMatchHeading"
        status: pass
    human_judgment: false
  - id: D8
    description: "Every mobile assertion is made under a real mobile User-Agent header, and a paired test proves the same url renders genuinely different markup under a mobile vs desktop User-Agent"
    requirement: CONTACTTAG-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileAndDesktopUserAgents_SelectDifferentLayouts"
        status: pass
    human_judgment: false
  - id: D9
    description: "Tag chip visual treatment (background, border, chip shape, font weight) on a real mobile device, and the drawer's open/close/apply/clear interaction with the operating system's actual offcanvas animation"
    verification: []
    human_judgment: true
    rationale: "81-VALIDATION.md's Manual-Only table defers real-device visual/interaction verification of the drawer and chips to manual testing; the integration test host never executes Bootstrap's JS-driven offcanvas open/close behavior, only the server-rendered markup it operates on"

# Metrics
duration: ~25min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 08: Contact Tags Mobile Filter and Chip Surface Summary

**Mobile Contacts index and details pages now render a full-width Filter Tags trigger with a bottom-drawer tag checkbox list, per-row and per-details-page tag chips, and a three-branch empty state, proven against a real mobile User-Agent with a paired test showing the same url renders genuinely different markup on desktop.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- `contacts.mobile.css` gained `.contact-tag-list`/`.contact-tag-chip` (identical bronze/gold/parchment values to the desktop chip), `.contact-filter-check-label` (drawer row spacing + `#ffc107` checkbox accent), and `.contact-filter-empty` (muted helper line beneath a disabled trigger)
- `contact-detail.mobile.css` gained the matching `.contact-tag-list`/`.contact-tag-chip` rules, with the page's parchment catch-all rule updated to exclude `.contact-tag-chip` so the chip's own background/border/color render unmodified
- `Details.Mobile.cshtml` renders a muted `fas fa-tags` line with one chip per tag, guarded on `Model.CanManage && Model.Tags.Count > 0`, positioned after the category line and before the hidden badge
- `Index.Mobile.cshtml` renders, inside the existing `Model.ViewerIsDmTier` conditional: a full-width `Filter Tags` trigger with an `Active` badge when a filter is applied, a `#contactFilterOffcanvas` bottom drawer (checkbox per available tag, Apply Filters / Clear Filters), a disabled trigger + helper sentence when the board has no tags, per-row tag chips, hidden `tag` inputs carrying the selection through the Show Hidden toggle form, and a three-branch empty state (contacts render as before / no-match-with-filter / genuinely-empty verbatim)
- `ContactsTagsMobileTests.cs` (8 tests) proves every one of the above under a real mobile User-Agent, proves a player gets zero tag markup on both mobile pages, and proves the same url selects genuinely different markup under a mobile vs desktop User-Agent

## Task Commits

Each task was committed atomically:

1. **Task 1: Mobile chip and filter styles, and the mobile details tag line** - `1bd30222` (feat)
2. **Task 2: Mobile index filter trigger, bottom drawer, chips, and empty states** - `fcf7ac07` (feat)
3. **Task 3: Mobile markup tests under a real mobile user agent** - `d8f209ca` (test)

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/contacts.mobile.css` - mobile chip, filter-check-label, and filter-empty rules
- `QuestBoard.Service/wwwroot/css/contact-detail.mobile.css` - matching chip rules, catch-all exclusion for the chip class
- `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` - muted tag line in the portrait card
- `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` - filter trigger, drawer, per-row chips, toggle-form hidden inputs, three-branch empty state
- `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs` - new: 8 mobile-user-agent integration tests

## Decisions Made
- Placed the Details.Mobile tag line after the existing CategoryName paragraph (immediately before the hidden badge), rather than literally between SubLocation and CategoryName, since CategoryName already occupied that position and the plan's binding constraint was "immediately before the hidden-badge div"
- In the paired mobile/desktop layout-selection test, asserted on the existing `.contact-card` desktop class rather than the sibling desktop plan's (81-07) not-yet-landed `.contact-filter-row` class -- see Deviations
- Used an empty board with a fabricated tag id to exercise the no-match empty-state branch, because the shared tag-filter helper (from an earlier phase-81 plan) falls back to the full visible list whenever a selection matches zero contacts, so the branch is only reachable when there are no visible contacts to begin with

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test file's Write output used LF line endings; converted to CRLF**
- **Found during:** Task 3, before running tests
- **Issue:** The Write tool emitted `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs` with LF-only line endings, inconsistent with this project's Windows/CRLF convention (CLAUDE.md) and with every other file in the same directory
- **Fix:** Converted the file's line endings to CRLF before staging, and re-ran the filtered test suite to confirm nothing broke
- **Files modified:** `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs`
- **Verification:** `dotnet test --filter "FullyQualifiedName~ContactsTagsMobileTests"` still green (8/8) after conversion
- **Committed in:** `d8f209ca` (test commit)

**2. [Cross-plan wave-parallelism adaptation, not a Rule 1-4 fix] Desktop marker substitution in the paired user-agent test**
- **Found during:** Task 3, while designing the mobile/desktop layout-selection test
- **Issue:** The plan's action text calls for asserting that the desktop response under the paired test contains "the desktop filter row class" (`.contact-filter-row`, per `81-UI-SPEC.md` Component Inventory §2). That class belongs to plan 81-07, which shares this plan's wave (wave 6) and depends only on 81-06, so it runs in a separate, parallel worktree isolated from this one. At this plan's execution time, `.contact-filter-row` does not exist in this worktree's copy of `Index.cshtml`, so an assertion on it would fail here even though it will be true once both wave-6 worktrees merge
- **Fix:** Asserted on `.contact-card` instead -- the desktop card-grid wrapper class that already exists today and is untouched by 81-07's planned change (chips are added as children of `.contact-card`'s `.contact-info`, not as a replacement for the card itself). This still proves the same claim: the same url renders genuinely different markup under the two headers
- **Files modified:** `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs`
- **Verification:** `Index_MobileAndDesktopUserAgents_SelectDifferentLayouts` passes in this worktree; the assertion remains true after 81-07 merges since `.contact-card` is not removed by that plan
- **Committed in:** `d8f209ca` (test commit)

---

**Total deviations:** 2 (1 auto-fixed line-ending bug, 1 cross-plan wave-parallelism test adaptation)
**Impact on plan:** No production code was affected by either item. The desktop-marker substitution is worth flagging for whoever reviews the wave-6 merge: once 81-07 lands, the paired test could optionally be strengthened to also assert `.contact-filter-row` on the desktop side, though `.contact-card` already proves the layout-selection claim on its own.

## Issues Encountered
None beyond the two items documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Both mobile tag surfaces (index filter/drawer/chips, details tag line) are implemented and proven under a real mobile User-Agent
- `dotnet build` and `dotnet test` (full suite, this worktree) are green at 454 unit + 700 integration tests
- Manual-only verification (drawer open/close animation, tick-two-tags-apply-then-clear on a real device) remains deferred to `81-VALIDATION.md`'s Manual-Only table, as this plan's own verification section specifies
- This plan's mobile surface depends only on 81-06; it does not depend on 81-07's desktop surface, so the two can merge in either order. The one cross-reference (the paired user-agent test's desktop-side assertion) uses a marker that predates this phase and is unaffected by 81-07's landing

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/wwwroot/css/contacts.mobile.css
- FOUND: QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
- FOUND: QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
- FOUND: QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
- FOUND: QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
- FOUND: commit 1bd30222 (feat: mobile chip/filter styles, details tag line)
- FOUND: commit fcf7ac07 (feat: mobile index trigger/drawer/chips/empty states)
- FOUND: commit d8f209ca (test: mobile markup tests under a real mobile user agent)
