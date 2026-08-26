---
phase: 74-event-schema-crud-and-calendar-display
plan: 05
subsystem: ui
tags: [razor-views, modern-card, markdown-editor, navbar]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 04)
    provides: EventsController six-action CRUD surface, EventViewModel (Title/Description/Date/StartTime/CanManage/TimeLabel)
provides:
  - Views/Events/Create.cshtml, Edit.cshtml, Details.cshtml — the only Dungeon Master surface for event CRUD
  - Create Event navbar entry on both _Layout.cshtml and _Layout.Mobile.cshtml, ungated by board type
affects: [76-event-recurrence, 77-availability-overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Events CRUD views are structural clones of Contacts/Create.cshtml, Contacts/Details.cshtml minus the image-upload column — same modern-card shell, same _MarkdownEditor partial call shape, same _QuestFormScripts include for EasyMDE"
    - "Edit and Delete confined to the details view only, gated on Model.CanManage (display-only) with the server-side DungeonMasterOnly policy as the real boundary — no write control anywhere in the shared calendar partial"

key-files:
  created:
    - QuestBoard.Service/Views/Events/Create.cshtml
    - QuestBoard.Service/Views/Events/Edit.cshtml
    - QuestBoard.Service/Views/Events/Details.cshtml
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml

key-decisions: []

patterns-established:
  - "One shared Details.cshtml with no .Mobile variant, per UI-SPEC's discretion clause — the content is a plain info card with no platform-specific interaction"

requirements-completed: [EVENT-01, EVENT-02, EVENT-06]

coverage:
  - id: D5
    description: "A DM reaches a Create Event form from the Dungeon Master navbar category on both desktop and mobile, with no board-type gate"
    requirement: "EVENT-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests (existing suite, unaffected by insertion)"
        status: pass
      - kind: manual
        ref: "git diff line-number check: Create Event anchor sits between Create Quest anchor and first activeBoardType conditional in both layouts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Create and Edit forms collect title, Markdown description, date and optional start time via the shared Markdown editor partial; Create GET renders 200, no-title POST redisplays the form with a validation error"
    requirement: "EVENT-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Get_DungeonMasterAccess_ShouldSucceed"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Post_WithoutTitle_ReturnsFormWithValidationError"
        status: pass
    human_judgment: false
  - id: D7
    description: "Every board member can read an event's Markdown description through the sanitized render helper; Edit/Delete appear only on the details view, gated on CanManage, with Delete confirmed by a native confirm dialog"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Details_Get_BoardMember_CanRead"
        status: pass
      - kind: static
        ref: "grep -c onsubmit=\"return confirm( QuestBoard.Service/Views/Events/Details.cshtml == 1; grep -c Html.Markdown == 1"
        status: pass
    human_judgment: false

# Metrics
duration: ~20min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 05: Events Views and Navbar Entry Summary

**Three Razor views (Create, Edit, Details) for the Events CRUD surface, structurally cloned from the Contacts pattern, plus a "Create Event" navbar entry inserted beneath "Create Quest" on both layouts — turning all ten EventsControllerIntegrationTests facts green.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-26 (approx)
- **Completed:** 2026-08-26
- **Tasks:** 3/3
- **Files modified:** 5 (3 created, 2 edited)

## Accomplishments
- `Views/Events/Create.cshtml` and `Edit.cshtml` — `modern-card` shell, Title/Date/StartTime fields, the shared `_MarkdownEditor` partial for Description, `_QuestFormScripts.cshtml` include for the EasyMDE editor assets (its submit handler is scoped to `form[action*="/Quest/"]` so it loads the editor without attaching quest-specific validation to these forms)
- `Views/Events/Details.cshtml` — two-column layout mirroring `Contacts/Details.cshtml`; title/date/time always render from `EventViewModel.TimeLabel` (never conditionally, so a blank slot never reads as a rendering bug); description rendered through `Html.Markdown`, the single sanitized render path; Edit and Delete confined to an `Actions` card gated on `Model.CanManage`, with Delete behind a native `confirm()` dialog
- `Views/Shared/_Layout.cshtml` and `_Layout.Mobile.cshtml` — "Create Event" list item inserted immediately after "Create Quest" and before the first `activeBoardType == BoardType.OneShot` conditional in each layout's Dungeon Master block; not board-type gated, matching Create Quest
- All ten `EventsControllerIntegrationTests` facts pass (the three that require a rendered view — `Create_Get_DungeonMasterAccess_ShouldSucceed`, `Create_Post_WithoutTitle_ReturnsFormWithValidationError`, `Details_Get_BoardMember_CanRead` — turned from RED to GREEN); all sixteen `LayoutNavigationTests` facts remain green
- `Views/Shared/_Calendar.cshtml` and `Views/Quest/` show no modification — confirmed by `git status --porcelain` — so no Dungeon Master control leaked into the shared calendar partial

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the Create and Edit event forms** - `a99cbeb` (feat)
2. **Task 2: Create the event details view with DM-gated actions** - `ce08fce` (feat)
3. **Task 3: Add the Create Event navbar entry to both layouts** - `ba719b9` (feat)

_This is a worktree-mode execution; the plan-metadata commit (this SUMMARY.md) is committed separately per the worktree protocol — no STATE.md/ROADMAP.md changes are made here._

## Files Created/Modified
- `QuestBoard.Service/Views/Events/Create.cshtml` - Create Event form: Title, Date, StartTime, Markdown description
- `QuestBoard.Service/Views/Events/Edit.cshtml` - Edit Event form; cancel returns to the event's own calendar month
- `QuestBoard.Service/Views/Events/Details.cshtml` - Event details view; sole location of Edit/Delete, gated on CanManage
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Added Create Event item to the desktop Dungeon Master dropdown
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Added Create Event item to the mobile Dungeon Master nav block

## Decisions Made
None - plan executed exactly as written; no ambiguity encountered during implementation.

## Deviations from Plan

None - plan executed exactly as written. Every grep-based acceptance criterion passed on the first attempt for all three tasks; `dotnet build` succeeded with zero errors (only pre-existing, unrelated NU1608 NuGet warnings about `AngleSharp` version constraints).

## Issues Encountered

None. `dotnet build` and both targeted `dotnet test` filters passed on the first run for every task. The three previously-RED integration facts turned GREEN exactly as plan 74-04's summary predicted, with no controller or view-model changes required.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The Events CRUD surface is complete: three views, one navbar entry per layout, all ten `EventsControllerIntegrationTests` facts and all sixteen `LayoutNavigationTests` facts green.
- No Events index or management page exists — `ls QuestBoard.Service/Views/Events/` lists exactly `Create.cshtml`, `Details.cshtml`, `Edit.cshtml`, per the plan's prohibition.
- `Views/Shared/_Calendar.cshtml` remains untouched by this plan, so the calendar-rendering work in the sibling wave-4 plan (74-06, executed concurrently in a separate worktree) has no risk of DM-control drift from this plan's changes.
- No blockers or concerns for downstream phases (76-event-recurrence, 77-availability-overview), which will extend this same Events surface.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Events/Create.cshtml
- FOUND: QuestBoard.Service/Views/Events/Edit.cshtml
- FOUND: QuestBoard.Service/Views/Events/Details.cshtml
- FOUND: QuestBoard.Service/Views/Shared/_Layout.cshtml
- FOUND: QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
- FOUND: .planning/phases/74-event-schema-crud-and-calendar-display/74-05-SUMMARY.md
- FOUND commit a99cbeb (Task 1)
- FOUND commit ce08fce (Task 2)
- FOUND commit ba719b9 (Task 3)
