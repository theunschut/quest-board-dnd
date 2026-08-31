---
phase: 81-contact-tags-and-filtering
plan: 06
subsystem: ui
tags: [aspnetcore-mvc, razor, tagify, cdn-sri, mobile-views, contact-tags]

# Dependency graph
requires:
  - phase: 81-05
    provides: "ContactViewModel.TagsInput/AvailableTagNames wired end to end through Create/Edit GET and POST, board-scoped suggestion vocabulary, and the ParseTagNames/ReplaceContactTagsAsync write path"
provides:
  - "wwwroot/js/contact-tags.js -- initContactTags(config), mirrors image-crop.js's initImageCrop() convention"
  - "Scoped Tagify theme overrides (.contact-tags-input) in contacts.css and contact-form.mobile.css, identical values on both platforms"
  - "A real, labelled Tags input (id=TagsInput) on all four Create/Edit views, wired to the pinned @yaireo/tagify 4.38.0 CDN library with SRI integrity and crossorigin attributes"
  - "ContactsTagsFormMarkupTests -- 5 integration tests proving the field and pinned library render for a DM on both a desktop and a real mobile User-Agent, the edit form pre-fills the alphabetical comma-separated tag value, a player is refused on both forms and both user agents, and the suggestion list never carries another board's tag names"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "contact-tags.js follows image-crop.js's exact convention: a single global init function taking a config object, an early-return no-op guard when its target element is absent, no module syntax"
    - "Tagify's theme custom properties are scoped to .contact-tags-input in both contacts.css and contact-form.mobile.css (identical values) rather than overridden globally, since the desktop stylesheet is not loaded on mobile"
    - "The CDN package path's at-sign is escaped as @@yaireo/tagify@4.38.0 in every Razor view; the suggestion list is emitted via @Html.Raw(Json.Serialize(Model.AvailableTagNames)) rather than string concatenation"

key-files:
  created:
    - QuestBoard.Service/wwwroot/js/contact-tags.js
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml

key-decisions:
  - "Re-verified @yaireo/tagify via npm view before writing any CDN reference: still 4.38.0, so the version and both SRI hashes recorded in 81-RESEARCH.md stand unchanged (no re-hash needed)"
  - "Followed the plan's TDD gate literally for task 3: wrote the mobile view markup once, reverted it with git checkout to prove the test suite fails for the right reason (RED), committed the test file alone, reapplied the markup, and confirmed all 5 tests pass before committing (GREEN) -- two genuine test bugs were caught during RED (a test helper that created a fresh, auto-redirect-following HttpClient instead of using the caller's own non-redirecting client, and a shared MutableGroupContext singleton that needed to point at group 2 while seeding the other board's tag) and fixed before GREEN"
  - "Corrected the pre-fill assertion from creation order (\"shopkeeper, quest giver\") to the alphabetical order (\"quest giver, shopkeeper\") the Edit GET action actually produces per 81-05's summary, rather than changing production behavior to match a wrong test assumption"

requirements-completed: [CONTACTTAG-01, CONTACTTAG-12, CONTACTTAG-13, CONTACTTAG-16]

coverage:
  - id: D1
    description: "All four create/edit views render a labelled Tags input (id=TagsInput) between the town/sub-location row and the category field, with the locked placeholder and helper text"
    requirement: CONTACTTAG-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#CreateForm_DesktopUserAgent_RendersTagInputAndPinnedLibrary"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#CreateForm_MobileUserAgent_RendersTagInputAndPinnedLibrary"
        status: pass
    human_judgment: false
  - id: D2
    description: "The edit forms pre-fill the tag input with the contact's existing tags as a comma-and-space-separated string, on both platforms"
    requirement: CONTACTTAG-12
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#EditForm_ContactWithTags_PreFillsCommaSeparatedValue"
        status: pass
    human_judgment: false
  - id: D3
    description: "The pinned @yaireo/tagify 4.38.0 library loads on every view with SRI integrity and crossorigin attributes on both the stylesheet and script, alongside the existing cropperjs pin"
    requirement: CONTACTTAG-13
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#CreateForm_DesktopUserAgent_RendersTagInputAndPinnedLibrary"
        status: pass
      - kind: unit
        ref: "dotnet build (0 errors) -- grep-verified 3x integrity=\"sha384- per plan acceptance criteria on all four views"
        status: pass
    human_judgment: false
  - id: D4
    description: "A player reaches neither the tag input nor the pinned library on either form, on both user agents -- no second, parallel audience check was introduced"
    requirement: CONTACTTAG-16
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#EditForm_PlayerTier_IsRefusedOnBothUserAgents"
        status: pass
    human_judgment: false
  - id: D5
    description: "The suggestion list serialized into the create form for a DM contains only tag names from that DM's own board, never another board's"
    requirement: CONTACTTAG-12
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs#EditForm_SuggestionList_ContainsOnlyOwnBoardTagNames"
        status: pass
    human_judgment: false
  - id: D6
    description: "Chips render, accept typed input, suggest from the board's vocabulary, remove on backspace, match the surrounding form styling, and the field degrades to a working plain text input with the CDN blocked -- all require a real browser/network condition the test host cannot exercise"
    verification: []
    human_judgment: true
    rationale: "81-VALIDATION.md's Manual-Only table defers exactly these checks to manual verification on desktop and a real mobile device; the CDN script never executes in the integration test host, so no automated test can assert client-side rendering, typeahead, or the blocked-network fallback behavior"

# Metrics
duration: ~25min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 06: Contact Tag Entry Widget (CDN Chips on All Four Views) Summary

**All four contact create/edit views (desktop and mobile) now carry a real, labelled Tags input wrapped by a pinned `@yaireo/tagify` 4.38.0 CDN library with SRI integrity, wired through a thin `contact-tags.js` init module that mirrors the existing image-cropper convention -- proven present for a DM and absent for a player on both a desktop and a real mobile User-Agent.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 8 (2 created, 6 modified)

## Accomplishments
- `contact-tags.js` exposes `initContactTags(config)`: an element-id lookup with a defensive no-op guard (matching `initImageCrop`'s convention), a whitelist of suggestion strings, `enforceWhitelist: false` so a DM can type a genuinely new tag, and a comma-and-space `originalInputValueFormat` so the enhanced and no-JS submission paths post the identical value shape
- `contacts.css` and `contact-form.mobile.css` each scope Tagify's theme custom properties to `.contact-tags-input` with identical bronze-background/parchment-text/gold-focus values, so the widget's chips look the same on both platforms without leaking the override elsewhere
- Re-verified `@yaireo/tagify` is still version 4.38.0 via `npm view` before writing any reference -- the version and both SRI hashes recorded in `81-RESEARCH.md` stand unchanged
- All four views (`Create.cshtml`, `Edit.cshtml`, `Create.Mobile.cshtml`, `Edit.Mobile.cshtml`) render the tag field between the town/sub-location row and the category field, and load the pinned stylesheet/script plus the local init module and an inline `initContactTags` call whose whitelist is emitted via `Json.Serialize(Model.AvailableTagNames)`
- `ContactsTagsFormMarkupTests` (5 tests) proves the field and pinned library reach a DM on both user agents, the edit form pre-fills the alphabetical comma-separated tag value on both platforms, a player is refused on both forms and both user agents, and the suggestion list is board-scoped

## Task Commits

Each task was committed atomically, with task 3 following the full RED/GREEN TDD sequence:

1. **Task 1: Re-verify the CDN pin, write the init module, and scope the theme overrides** - `3bb35798` (feat)
2. **Task 2: Tag field on the desktop create and edit forms** - `157c6cb5` (feat)
3. **Task 3: Tag field on the mobile create and edit forms, with markup tests for both platforms**
   - RED: `11b35b4d` (test) -- failing tests committed first; confirmed the two mobile-dependent assertions failed for the right reason (missing markup) while desktop-only assertions already passed
   - GREEN: `f3d9aebc` (feat) -- mobile view markup added, all 5 tests pass

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/js/contact-tags.js` - new: `initContactTags(config)` init module
- `QuestBoard.Service/wwwroot/css/contacts.css` - `.contacts-page .contact-tags-input` Tagify theme override block
- `QuestBoard.Service/wwwroot/css/contact-form.mobile.css` - `.contact-tags-input` Tagify theme override block (identical values, mobile stylesheet)
- `QuestBoard.Service/Views/Contacts/Create.cshtml` / `Edit.cshtml` - tag field + pinned CDN script/link + init call
- `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` / `Edit.Mobile.cshtml` - same, mobile markup shape
- `QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs` - new: 5 markup/audience-gate tests

## Decisions Made
- Re-verified the CDN pin (`npm view @yaireo/tagify version`) rather than assuming the research-session hashes were still valid; version unchanged at 4.38.0, so the recorded hashes carry forward unmodified
- Followed the plan's TDD instruction literally for task 3 by reverting the mobile markup with `git checkout -- <file>` (a sanctioned per-file discard, not a blanket reset) after writing it once, to prove a genuine RED failure before committing the test file, then reapplying the markup for GREEN -- this surfaced two real bugs in the test file itself (see Deviations) that a "write everything then commit once" approach would have shipped unnoticed
- Corrected the edit-form pre-fill assertion to the alphabetical tag order the Edit GET action actually produces, rather than the creation order the test originally assumed

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test helper silently followed redirects, masking the player-refusal assertion**
- **Found during:** Task 3, RED-phase test run
- **Issue:** The test file's shared `GetAsync` helper accepted a `client` parameter but called `factory.CreateClient()` (a fresh, default client with `AllowAutoRedirect = true`) instead of using it, so a DM-tier `Forbid()` redirect to `/Account/AccessDenied` was auto-followed and reported as `200 OK` rather than the expected 302/403/401
- **Fix:** Changed the helper to call `client.SendAsync(...)` on the caller's own client, which every existing helper (`AuthenticationHelper.CreateAuthenticatedClientWithUserAsync`) already constructs with `AllowAutoRedirect = false`
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs`
- **Verification:** `EditForm_PlayerTier_IsRefusedOnBothUserAgents` now correctly asserts a redirect/forbidden status
- **Committed in:** `11b35b4d` (test commit, caught before the GREEN commit)

**2. [Rule 1 - Bug] Cross-group tag seeding queried through the wrong active-group filter**
- **Found during:** Task 3, RED-phase test run
- **Issue:** `TestDataHelper.CreateTestContactTagAsync`'s join-attach step re-queries the target contact through `QuestBoardContext`'s fail-closed group filter. Seeding a tag on a group-2 contact while the shared `MutableGroupContext` singleton still pointed at group 1 (the suite's default) made that query return zero rows (`Sequence contains no elements`)
- **Fix:** Temporarily set `factory.TestGroupContext.ActiveGroupId = 2` around the group-2 tag-seeding call, restoring it to `1` in a `finally` block before the DM's own request -- the same pattern already established in `GroupSessionMiddlewareIntegrationTests`
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs`
- **Verification:** `EditForm_SuggestionList_ContainsOnlyOwnBoardTagNames` now seeds and asserts correctly
- **Committed in:** `11b35b4d` (test commit, caught before the GREEN commit)

**3. [Rule 1 - Bug] Pre-fill assertion assumed the wrong tag order**
- **Found during:** Task 3, first GREEN-phase test run (after mobile markup was reapplied)
- **Issue:** The test asserted `value="shopkeeper, quest giver"` (creation order), but the Edit GET action pre-fills `TagsInput` in the contact's own already-alphabetical tag order (per 81-05's summary), producing `"quest giver, shopkeeper"`
- **Fix:** Corrected the expected string to the alphabetical order; no production code was touched, since the alphabetical pre-fill is the intended, already-implemented behavior from plan 05
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs`
- **Verification:** All 5 tests pass; full suite green at 454 unit + 692 integration tests
- **Committed in:** `f3d9aebc` (GREEN commit)

---

**Total deviations:** 3 auto-fixed (all Rule 1 -- bugs in the plan's own new test file, caught by following the TDD RED step literally rather than skipped)
**Impact on plan:** All three were self-inflicted test bugs, not production defects; none required touching the four views, the CSS, or the init module. Zero scope creep.

## Issues Encountered

None beyond the three test-authoring bugs documented above, all caught and fixed within task 3's own RED/GREEN cycle before the GREEN commit landed.

## User Setup Required

None - no external service configuration required. The pinned CDN references require no server-side setup; jsDelivr availability was already confirmed in `81-RESEARCH.md`.

## Next Phase Readiness
- All four contact create/edit views now carry the tag entry widget end to end: server-rendered input, board-scoped suggestion vocabulary, pinned integrity-checked CDN library, and a no-JS-safe fallback shape
- `dotnet test` (full suite) is green at 454 unit + 692 integration tests
- Manual-only verification (chip rendering, typeahead, backspace removal, visual match to surrounding controls, and the blocked-CDN degradation) remains deferred to `81-VALIDATION.md`'s Manual-Only table, as this plan's own action text specifies -- no automated test can exercise real browser/network behavior against the test host
- This plan closes out the tag-entry-widget surface named in `81-RESEARCH.md`'s "Recommended Project Structure"; remaining phase 81 work (if any) is scoped to later plans per the roadmap

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*
