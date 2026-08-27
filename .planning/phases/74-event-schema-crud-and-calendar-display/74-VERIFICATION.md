---
phase: 74-event-schema-crud-and-calendar-display
verified: 2026-08-27T06:33:02Z
status: passed
score: 6/6 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 74: Event Schema, CRUD and Calendar Display Verification Report

**Phase Goal:** "A DM can put a dated event on their board's calendar — informational only — and everyone sees it on both the desktop and mobile calendar, clearly distinct from a quest."
**Verified:** 2026-08-27T06:33:02Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | EVENT-01: DM can create an event with title, optional description, date, optional start time | ✓ VERIFIED | `EventsController.Create` POST persists via `IEventService.AddAsync`; `EventViewModel` has `[Required] Title`, `Description` nullable, `[Required] Date`, nullable `StartTime`. `EventsControllerIntegrationTests.Create_Post_ValidEvent_PersistsAndRedirectsToEventMonth`, `Create_Post_WithoutStartTime_IsAccepted` pass (re-ran: 10/10 in this file). |
| 2 | EVENT-02: DM can edit/delete own-board events; events never visible to another board | ✓ VERIFIED | `EventsController.Edit`/`Delete` gated `[Authorize(Policy="DungeonMasterOnly")]`; both call `GetEventWithDetailsAsync` which relies on the fail-closed `EventEntity` query filter, so a cross-board id 404s. `EventTenantIsolationTests` (8 tests, re-ran: 8/8 pass) proves cross-board invisibility on desktop, mobile, and direct id, the positive in-board case, that a posted `GroupId` cannot override the server stamp, and that a cross-board schedule reference is rejected on Edit with 400. |
| 3 | EVENT-03: Events appear on desktop calendar, visually distinguishable from quests | ✓ VERIFIED | `_Calendar.cshtml` renders `.calendar-events` block strictly above `.quest-events`, chip has `#6f42c1` left border (`calendar.css:236`) and `fa-calendar-day` icon absent from quest chips; Legend card gained an Event swatch row and hint text "Click quests or events for details". `CalendarControllerIntegrationTests.Index_WithEventAndQuestOnSameDay_RendersEventBlockAboveQuestBlock` asserts position via string-index ordering (not just presence); `Index_WithEvent_RendersClickableChipLinkingToEventDetails` and `Index_LegendExplainsEventChip` also pass (re-ran with the class: 85/85 across the three files below). |
| 4 | EVENT-04: Events appear on mobile calendar, which today lists only days with quests | ✓ VERIFIED | `Index.Mobile.cshtml:9` widens the agenda-day filter to `d.QuestsOnDay.Any() || d.EventsOnDay.Any()`; events render before quests within a day (line 52-64 before 66-77); empty-state copy rewritten to "Nothing This Month" / "No quests or events are planned" (old quest-only strings removed). `MobileViewsTests` event-specific facts (`MobileCalendar_DayWithEventButNoQuest_AppearsInAgenda`, `_EventWithoutStartTime_RendersAllDayWording`, `_EventAndQuestOnSameDay_EventEntryRendersFirst`, `_MonthWithNeitherQuestNorEvent_RendersNeutralEmptyState`) included in the 85/85 re-run. |
| 5 | EVENT-05: Events never appear on the quest board main page and never block/constrain quest creation | ✓ VERIFIED | `QuestController.cs:354-360` constructs `CalendarViewModel` for quest-details calendar months without setting `Events`, which defaults to `[]` (`CalendarViewModel.cs:16`) — fail-closed by structure, not a flag. `EventCalendarPartialTests` (3/3, re-ran) proves both Quest Details pages (desktop + mobile) render zero `calendar-events`/`calendar-event` markup with a same-day event present, and `QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged` proves quest creation redirects (succeeds) and persists with an event already on the chosen date. No `IQuestService`/`IQuestRepository`/`QuestViewModel` reference exists in `EventsController.cs` or `EventViewModel.cs`; `QuestController.cs` carries no diff from this phase. |
| 6 | EVENT-06: "Create Event" sits in the same navbar category as "Create Quest", available to all DM roles | ✓ VERIFIED | Both `_Layout.cshtml:100-104` and `_Layout.Mobile.cshtml:83-87` place the "Create Event" item immediately after "Create Quest," inside the `DungeonMasterOnly`-policy-gated dropdown/section, outside the `activeBoardType == BoardType.OneShot` conditional that starts two lines later. `LayoutNavigationTests.Nav_DungeonMaster_CreateEventEntryPresent` (desktop+mobile UA), `Nav_Player_CreateEventEntryAbsent`, and `Nav_CampaignBoard_DungeonMaster_CreateEventEntryStillPresent` (proves availability on both board types) all pass. |

**Score:** 6/6 truths verified (0 present-but-behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/Entities/EventEntity.cs` | Entity, `IEntity`, mapped to `Events` | ✓ VERIFIED | 11 members (Id, Title, Description, Date, StartTime, Series, SeriesId, SeriesSlotIndex, CreatedAt, GroupId, Group) — matches the field-by-field spec in the plan (the plan's own prose "exactly ten" was an arithmetic slip noted in 74-02-SUMMARY; the typed field list was followed and is what's implemented). |
| `QuestBoard.Repository/Entities/EventSeriesEntity.cs` | Entity, `IEntity`, mapped to `EventSeries` | ✓ VERIFIED | GroupId-scoped, own query filter. |
| `QuestBoard.Repository/Entities/EventSignupEntity.cs` | Entity, `IEntity`, mapped to `EventSignups` | ✓ VERIFIED | No `GroupId`; scoped through required `Event` navigation. No production code reads/writes it (confirmed by `grep -rln EventSignup` — only entity/context/migration files). |
| `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` | One additive migration | ✓ VERIFIED | `Up()` contains only `CreateTable`/`CreateIndex`; `Down()` only `DropTable`; correct dependency order (EventSeries → Events → EventSignups); `Date` maps to SQL `date`, `StartTime` to `time`. |
| `QuestBoard.Domain/Models/Event.cs`, `IEventRepository.cs`, `IEventService.cs` | Domain model + 2 interfaces | ✓ VERIFIED | Exactly the fields/methods specified; no `EntityFrameworkCore` reference anywhere in `QuestBoard.Domain` (`grep` returns 0 matches), csproj unchanged. |
| `QuestBoard.Repository/EventRepository.cs`, `QuestBoard.Domain/Services/EventService.cs` | Repository + Service | ✓ VERIFIED | No manual `GroupId ==` filter, no `IgnoreQueryFilters` in either file (checked directly, both project-wide). |
| `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs` | 7-member view model | ✓ VERIFIED | Id, Title, Description, Date, StartTime, CanManage, TimeLabel = 7. No `SeriesId`/schedule field present — cannot be model-bound. |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` | 6-action DM-gated controller | ✓ VERIFIED | Details/Create/Edit/Delete present; GroupId stamped server-side via `activeGroupContext.RequireActiveGroupId()` on Create and Edit, never from the posted model; series cross-board check (`SeriesIsOnActiveBoardAsync`) enforced before save. |
| `Views/Events/{Create,Edit,Details}.cshtml` | 3 views, no index/manage page | ✓ VERIFIED | `ls QuestBoard.Service/Views/Events/` (implicit via directory reads) shows exactly these three; modern-card shell used; Edit/Delete gated on `Model.CanManage`; native `confirm()` on delete. |
| `QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs`, updated `CalendarDay.cs`/`CalendarViewModel.cs` | Desktop rendering models | ✓ VERIFIED | `EventsOnDay`/`Events` both default to `[]`; single named `GetEventsForDate` performs the one `DateOnly.FromDateTime` conversion. |
| `QuestBoard.Service/wwwroot/css/calendar.mobile.css` | Mobile agenda styles | ✓ VERIFIED | `.agenda-event-entry` present, `#6f42c1` accent, mirrors `.agenda-quest-entry` geometry. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `EventsController` | `IEventService` | constructor injection | ✓ WIRED | No direct `DbContext`/repository access from the controller. |
| `CalendarController.Index` | `IEventService.GetEventsForCalendarAsync` | direct call | ✓ WIRED | Populates `CalendarViewModel.Events` only on the real calendar page. |
| `QuestController` (Quest Details) | `CalendarViewModel.Events` | never set | ✓ WIRED (fail-closed) | Confirmed empty-by-default is a structural property, not a flag — 5 quest-detail call sites verified to render nothing via `EventCalendarPartialTests`. |
| `_Calendar.cshtml` chip | `/Events/Details/{id}` | `<a>` anchor | ✓ WIRED | No DM control inside the shared partial (`git status --porcelain` on `Views/Quest/` shows no modification per the plan's own prohibition verification, confirmed unchanged). |
| `Index.Mobile.cshtml` agenda entry | `/Events/Details/{id}` | `onclick` navigation | ✓ WIRED | Matches the pre-existing `.agenda-quest-entry` pattern (known, accepted a11y debt — not new to this phase). |
| `EntityProfile`/`ViewModelProfile` | `EventEntity`↔`Event`↔`EventViewModel` | AutoMapper | ✓ WIRED | `GroupId`, `SeriesId`, `SeriesSlotIndex` ignored on the ViewModel→domain map; `Group`/`Series` navigations ignored on the domain→entity map, preventing null-out of tracked navigations on update. |

### Behavioral Spot-Checks / Test Re-Execution

Re-ran (not merely trusted from SUMMARY) the phase's own behavioral test classes directly against the current codebase:

| Test class | Command | Result | Status |
|---|---|---|---|
| `EventTenantIsolationTests` | `dotnet test --filter FullyQualifiedName~EventTenantIsolationTests` | 8/8 passed | ✓ PASS |
| `EventCalendarPartialTests` | `dotnet test --filter FullyQualifiedName~EventCalendarPartialTests` | 3/3 passed | ✓ PASS |
| `EventsControllerIntegrationTests` | `dotnet test --filter FullyQualifiedName~EventsControllerIntegrationTests` | 10/10 passed | ✓ PASS |
| `LayoutNavigationTests` + `MobileViewsTests` + `CalendarControllerIntegrationTests` | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~CalendarControllerIntegrationTests"` | 85/85 passed | ✓ PASS |

These four runs cover every event-specific test file the phase added or extended. Combined with the orchestrator-reported full-suite 779/779 (which these are a subset of), the phase's tests are genuinely green, not merely claimed.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|---|---|---|---|---|
| EVENT-01 | 74-01, 74-02, 74-04, 74-05 | Create event: title, optional description, date, optional start time | ✓ SATISFIED | See Truth 1. |
| EVENT-02 | 74-01, 74-02, 74-03, 74-04, 74-05, 74-08 | Edit/delete own-board events; scoped, never cross-board | ✓ SATISFIED | See Truth 2. |
| EVENT-03 | 74-06, 74-08 | Desktop calendar visual distinction | ✓ SATISFIED | See Truth 3. |
| EVENT-04 | 74-07, 74-08 | Mobile calendar shows events | ✓ SATISFIED | See Truth 4. |
| EVENT-05 | 74-01, 74-04, 74-08 | Informational only; quest board/creation unaffected | ✓ SATISFIED | See Truth 5. |
| EVENT-06 | 74-05, 74-08 | Navbar "Create Event" alongside "Create Quest," all DM roles | ✓ SATISFIED | See Truth 6. |

No orphaned requirements: `REQUIREMENTS.md`'s "Calendar Events — Foundation" section defines exactly EVENT-01 through EVENT-06, and the Traceability table maps all six to Phase 74 with no others. Every plan's `requirements:` frontmatter references only IDs that exist in `REQUIREMENTS.md`; no plan claims an undefined ID.

**Documentation note (non-blocking):** `REQUIREMENTS.md` still shows `- [ ]` and "Not started" for EVENT-01..06 as of this verification pass. That is a stale-checkbox issue for the milestone-close/ship step to update, not a code gap — flagging so it isn't missed.

### Anti-Patterns Found

No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers, no "not yet implemented" / "coming soon" strings, and no stub return patterns (`return null`/empty collections feeding rendered output without a real data path) found in any of the 21 core files this phase touched (entities, domain, repository, service, controller, view models, views, CSS). Empty-collection defaults (`CalendarViewModel.Events = []`, `EventViewModel` has none) are the deliberate fail-closed design, not stubs — confirmed populated by a real query on the one code path that needs it (`CalendarController.Index`).

### Tenant Isolation Depth (focus area 3)

- Isolation rests entirely on `QuestBoardContext`'s three `HasQueryFilter` entries (`EventEntity`, `EventSeriesEntity`, `EventSignupEntity`), each fail-closed (`ActiveGroupId != null && ... == ActiveGroupId`), each dereferencing `activeGroupContext.ActiveGroupId` inline — no captured local, matching the existing `QuestEntity` convention.
- No manual `GroupId ==` filter exists in `EventRepository.cs` (checked directly — the repository does zero extra scoping, purely relies on the entity filter).
- `IgnoreQueryFilters` does not appear anywhere in `QuestBoard.Service`, `QuestBoard.Domain`, or in this phase's `QuestBoard.Repository` files. The one production hit (`QuestRepository.cs`) is pre-existing (commit `6f4f219`, unrelated to Phase 74, a documented SuperAdmin cross-group feature) and the two integration-test hits noted in `known_open_items` are pre-existing test code from earlier phases, confirmed unrelated to this phase's files.
- Write path: `EventsController.Create`/`Edit` both stamp `GroupId` from `activeGroupContext.RequireActiveGroupId()`, never from `viewModel`. `EventTenantIsolationTests.Create_Post_PostedGroupIdIsIgnored_ServerStampsActiveBoard` proves a posted `GroupId=2` is silently ignored and the row is stamped `GroupId=1`. `Edit_Post_EventPointingAtAnotherBoardSchedule_ReturnsBadRequest` proves the second, independent `GetSeriesGroupIdAsync` layer rejects a cross-board schedule reference even though the read filter already hides it.

### Layering (focus area 4)

- `grep -rn EntityFrameworkCore QuestBoard.Domain --include=*.cs` → 0 matches; `QuestBoard.Domain.csproj` carries no EF reference.
- `QuestBoard.Service.csproj` carries only `Microsoft.EntityFrameworkCore.Tools` (a design-time CLI package for `dotnet ef`, pre-existing since commit `b49607f`, unrelated to Phase 74) — no EF *runtime* type is used in Service code; `EventsController` resolves `IEventService`/`IUserService`/`IActiveGroupContext`/`IMapper` only, no `DbContext`, no repository interface.
- Service → Domain → Repository one-way dependency holds for all new Phase 74 types.

### Regression Surface (focus area 5)

- `EventCalendarPartialTests.QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged` behaviorally proves quest creation succeeds (redirect, not a redisplayed form or 400) and persists with an event already seeded on the identical date — a genuine assertion, not an inferred claim.
- `QuestController.cs` carries no diff attributable to this phase (confirmed via the files-modified list across all 8 plans — `QuestController.cs` never appears).
- `Views/Quest/Details.cshtml` and `Details.Mobile.cshtml` are untouched by this phase (not in any plan's `files_modified`) and both render zero event markup per `EventCalendarPartialTests` (2/2 of those facts pass).
- The three test-class re-runs above (106 tests total) include pre-existing quest, mobile, and navigation facts within the same files, all passing alongside the new event facts — evidence the new code did not regress adjacent quest behavior within those files.

### "Informational Only" (focus area 2)

- `EventSignupEntity` exists in schema only; confirmed by `grep -rln EventSignup` across `QuestBoard.Service`, `QuestBoard.Domain`, `QuestBoard.Repository` (excluding `bin`/`obj`) returning only `EventSignupEntity.cs`, `QuestBoardContext.cs`, and the migration files — no controller, service, or view references it.
- No availability/vote UI exists on `Views/Events/Details.cshtml` or anywhere else in this phase — the view renders title, date, `TimeLabel`, and Markdown description only, plus DM-gated Edit/Delete.
- `EventEntity` carries no `EventType`/category discriminator and no FK to a quest (confirmed by reading the entity — matches the explicit Out-of-Scope item in `REQUIREMENTS.md`).
- This confirms EVTAVAIL-01..05 (Phase 75 scope) genuinely has not leaked into Phase 74.

## Deviations From Plan Text (documented, not gaps)

**Desktop calendar cell sizing (74-06):** the plan's accepted design (D-08) was a growable grid row (`grid-auto-rows: minmax(120px, auto)`). Live human-verify at the Task 4 checkpoint showed the cell growing to 152px with 3 events + 1 quest, which the developer explicitly rejected in favor of a fixed 120px cell with an internal `.day-cell-items` scroll region. This is confirmed in the actual CSS (`calendar.css:41`, `grid-auto-rows: 120px`, fixed — not `minmax`) and matches the orchestrator's live-verified state. The change is developer-approved, documented in `74-06-SUMMARY.md`, and does not weaken EVENT-03 (events remain visually distinct and reachable) — it is a stricter, human-chosen design, not a defect.

## Human Verification Required

None. Both human-verify checkpoints for this phase (74-06 desktop, 74-07 mobile) were already conducted in a real browser and approved by the developer per the orchestrator's established state, and this verification pass found no additional behavior-dependent truth requiring a test that doesn't already exist and pass.

## Gaps Summary

None found. All 6 EVENT-01..06 requirements are satisfied with concrete, re-executed test evidence (106 tests re-run directly by this verification pass, all green), tenant isolation rests structurally on query filters with a server-side write stamp and a second independent schedule-ownership check, layering is clean (no EF leak into Domain, no direct data access from the controller), and "informational only" holds — no signup/availability surface exists in this phase's code.

---

_Verified: 2026-08-27T06:33:02Z_
_Verifier: Claude (gsd-verifier)_
