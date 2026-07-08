---
phase: 48-add-an-open-board-action-to-the-platform-group-index-table-r
plan: 01
subsystem: ui
tags: [razor, mvc, platform-area, group-picker, forms]

# Dependency graph
requires: []
provides:
  - "Open Board" button on the desktop Platform Group index (Index.cshtml), left of Members/Edit
  - "Open Board" button on the mobile Platform Group index (Index.Mobile.cshtml), left of Members/Edit
  - Both buttons POST to the existing GroupPickerController.SelectGroup action (no new backend code)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cross-area form POST: asp-controller + asp-action + explicit asp-area=\"\" to escape an ambient Razor Area and target a default-area controller"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml

key-decisions:
  - "Reused GroupPickerController.SelectGroup verbatim instead of adding a new controller action (D-01)"
  - "No member-count gate on the new button — shows on all groups, including 0-member groups (D-01 discretion)"
  - "No confirmation dialog on click — navigation action, not destructive (D-02)"
  - "No nav-level shortcut back to /platform added; _Layout files untouched (D-03)"
  - "Button added to both desktop and mobile views for parity (D-04)"

patterns-established:
  - "Pattern: a per-row/per-card action button that must POST data (vs. a plain link) uses a small inline <form> with antiforgery token + hidden input, styled with the same btn btn-sm btn-{color} classes as sibling anchor buttons, wrapped in class=\"d-inline\" so it doesn't break the button row layout."

requirements-completed: []

# Metrics
duration: ~35min
completed: 2026-07-04
status: complete
---

# Phase 48 Plan 01: Add an Open Board action to the Platform Group index table Summary

**Added an "Open Board" button (btn-primary, fa-door-open) to both desktop and mobile Platform Group index views that POSTs to the existing `GroupPickerController.SelectGroup`, letting a SuperAdmin jump directly from `/platform/group` into any group's quest board without the `/groups/pick` detour.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-07-04
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files modified:** 2

## Accomplishments
- Desktop Platform Group index (`Index.cshtml`) renders an "Open Board" button as the leftmost action on every row, before "Members"
- Mobile Platform Group index (`Index.Mobile.cshtml`) renders the same button as the leftmost action in each card's button row
- Both buttons reuse `GroupPickerController.SelectGroup` unchanged — zero new controllers, actions, services, entities, or migrations
- Live end-to-end verification confirmed clicking "Open Board" sets the active group in session and redirects straight to that group's quest board, including for a 0-member group

## Task Commits

Each task was committed atomically:

1. **Task 1: Add "Open Board" button to desktop Platform Group index** - `8e6bd27` (feat)
2. **Task 2: Add "Open Board" button to mobile Platform Group index** - `01a9df6` (feat)
3. **Task 3: Human verification — Open Board works on desktop and mobile** - approved (no code change; verification-only checkpoint)

**Plan metadata:** this commit (docs: complete plan)

_Note: Task 3 was a `checkpoint:human-verify` gate with no associated code commit. The prior worktree was merged into the working branch via `eae3b86` ("chore: merge executor worktree")._

## Files Created/Modified
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` - Added a per-row `<form asp-controller="GroupPicker" asp-action="SelectGroup" asp-area="">` with antiforgery token, hidden `groupId`, and a `btn btn-sm btn-primary me-2` submit button (`fa-door-open` icon), inserted before the "Members" anchor. No member-count gate.
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` - Same form/button pattern added to the mobile card's action row, styled without `me-2` (parent `gap-2` wrapper handles spacing), matching sibling mobile buttons.

## Decisions Made
- Followed CONTEXT.md decisions D-01 through D-04 exactly as specified: reuse `SelectGroup` verbatim, no confirmation dialog, no nav-level Platform shortcut, button on both desktop and mobile.
- Used the recommended `btn-primary` + `fa-door-open` styling from CONTEXT.md's discretion note (info/warning/danger already taken by Members/Edit/Delete).
- No member-count gate, per CONTEXT.md discretion note — an empty group still has a valid, if empty, board.

## Deviations from Plan

None - plan executed exactly as written. Both views match the plan's acceptance criteria verbatim (form structure, `asp-area=""`, antiforgery token, hidden `groupId`, no `returnUrl`, button classes/icon/text, no member-count gate, Members/Edit/Delete unchanged, no phase/requirement IDs in markup).

## Issues Encountered

None during implementation. During the human-verify checkpoint, the mobile view's rendering could not be visually confirmed in an actual mobile viewport because this app selects mobile views via `MobileDetectionMiddleware` (User-Agent based), not a CSS media query, and the preview browser tool available during verification had no way to spoof a mobile User-Agent. This gap was disclosed to the user, who reviewed the mobile markup directly (confirmed structurally identical and correct to the desktop form) and approved anyway.

## Verification Performed (Task 3 — Human Checkpoint)

Live verification was performed in a real browser session (user already authenticated as SuperAdmin) before requesting user approval:
- App started via `dotnet run --project QuestBoard.Service`; clean startup, no errors.
- Navigated to `/platform/group` as SuperAdmin: "Open Board" renders on all 3 existing groups (EuphoriaInn, Test Group, The Boundless Domain), positioned left of Members/Edit, styled `btn btn-sm btn-primary` with `fa-door-open` icon — matching the plan exactly.
- Clicked "Open Board" on EuphoriaInn -> landed on `/quests` showing EuphoriaInn's quest list (no `/groups/pick` detour).
- Clicked "Open Board" on Test Group (different group) -> landed on `/quests` with different (empty) content, confirming the active group switched server-side.
- Clicked "Open Board" on The Boundless Domain (0 members) -> worked without error, confirming no member-count gate blocks it.
- Confirmed Members, Edit, and Delete (on the 0-member group) are all still present and unchanged.
- Read `Index.Mobile.cshtml` directly and confirmed identical, correct form/button markup — structurally verified but not visually rendered in a real mobile viewport (see Issues Encountered above).
- `dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug`: 0 errors, 0 warnings.
- `dotnet test QuestBoard.IntegrationTests`: 289/289 passed, no regressions.

User typed "approved" to close out the checkpoint.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 48 is fully complete: both views ship the "Open Board" action, backend reused unchanged, all existing group actions verified intact.
- No blockers or concerns for subsequent phases. The deferred nav-level "return to /platform" shortcut (D-03) remains a candidate for a future small phase if it proves useful in practice.

---
*Phase: 48-add-an-open-board-action-to-the-platform-group-index-table-r*
*Completed: 2026-07-04*
