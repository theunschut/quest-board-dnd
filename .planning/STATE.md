---
gsd_state_version: 1.0
milestone: v5.0
milestone_name: Multi-Tenancy
status: ready_to_plan
stopped_at: Phase 34.1 complete (2/2) — ready to discuss Phase 34.2
last_updated: 2026-07-02T07:33:02.637Z
last_activity: 2026-07-02 -- Phase 34.1 execution started
progress:
  total_phases: 12
  completed_phases: 9
  total_plans: 42
  completed_plans: 37
  percent: 75
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-29 — v5.0 Multi-Tenancy started)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 34.2 — performance architecture fix tech debt refactors questcontro

## Current Position

Phase: 34.2
Plan: Not started
Status: Ready to plan
Last activity: 2026-07-02

```
v5.0 Progress [███████░░░] 62% (8/13 phases complete)
Phase 26 Namespace Rename                              [x] complete (2026-06-29)
Phase 27 Group Schema Foundation                        [x] complete (2026-06-30)
Phase 28 Tenant Isolation                               [x] complete (2026-06-30)
Phase 29 SuperAdmin + Mgmt Area                          [x] complete (2026-06-30)
Phase 30 Group UX + User Mgmt                            [x] complete (2026-06-30)
Phase 31 Unauthenticated Landing Redirect                [x] complete (2026-07-01)
Phase 32 First-Login Password Flow                       [x] complete (2026-07-01)
Phase 33 Session Persistence & Admin Email Rate Limiting [x] complete (2026-07-01)
Phase 34 Codebase Cleanup (Mechanical Cleanup slice 34a) [ ] planned, 5 plans ready
Phase 34.1 Security & Bugs                               [ ] planned, 2 plans ready
Phase 34.2 Performance & Architecture                    [ ] planned, 5 plans ready (2 waves)
```

## Deferred Items

Items acknowledged and deferred at milestone close on 2026-06-28:

| Category | Item | Status |
|----------|------|--------|
| requirement | EMAIL-04 — digest session reminder (multiple same-day quests → one email) | Deferred — same-day quests have never occurred in one year of operation |
| requirement | REMIND-02 — combined reminder for multi-quest days | Deferred — same as EMAIL-04 |

## Accumulated Context

### Key Architectural Decisions (v4.0)

- HtmlRenderer (not IRazorViewEngine) for email templates in background job context
- IServiceScopeFactory + CreateAsyncScope() in every Hangfire job — scoped services cannot be constructor-injected
- IDashboardAuthorizationFilter (not LocalRequestsOnlyAuthorizationFilter) — Docker reverse proxy bypasses localhost check
- NullObject dispatchers (NullQuestEmailDispatcher, NullReminderJobDispatcher) for Testing environment isolation
- FinalizedDate stored as server local time — DateTime.Today.AddDays(1) comparison correct for CET/CEST host
- Resend stats: plain HttpClient GET /emails with Bearer token; no SDK; 5-min IMemoryCache

### Key Architectural Decisions (v5.0)

- IActiveGroupContext defined in Domain layer (not Service) — QuestBoardContext in Repository must consume it; Repository depends on Domain
- ActiveGroupContextService in Service layer reads ActiveGroupId from ASP.NET Core Session; returns null when user holds SuperAdmin role
- EF Core Global Query Filters applied to QuestEntity and ShopItemEntity only — UserEntity must NOT receive a filter (breaks Identity)
- Per-group roles live in UserGroups.GroupRole — AspNetUserRoles is used only for SuperAdmin (system-wide)
- AdminHandler and DungeonMasterHandler read GroupRole from UserGroups for the active group; SuperAdmin Identity role bypasses both handlers
- SuperAdmin management area routed at /platform (not /superadmin)
- Phase 26 is a pure rename — zero behavior change required; all 191 tests must pass before merging
- Phase 28 is highest complexity — test factory stub and Hangfire adaptation must land in same PR as HasQueryFilter
- GroupRole stored as int on UserGroupEntity; enum cast at AutoMapper boundary (consistent with SignupRole/CharacterStatus/CharacterRole patterns)
- UserGroupEntity uses auto-increment int PK with composite unique index on (UserId, GroupId) — not a composite PK (avoids EF composite-PK pitfall)
- Quest/ShopItem→Group FKs use NoAction delete to prevent SQL Server cascade cycle errors
- UserGroup→User and UserGroup→Group FKs use Cascade so membership rows clean up automatically
- Groups.Name has a DB-layer unique index (D-08)
- Hangfire jobs resolve ActiveGroupContextService (concrete, not interface) to call SetGroupId(groupId) before any repo call; SetGroupId is on the concrete class only (D-09)
- GetQuestsForTomorrowAllGroupsAsync uses IgnoreQueryFilters() for cross-group sweep — method name makes intent explicit; only one call site in codebase (D-08)
- QuestController passes activeGroupContext.ActiveGroupId ?? 1 to EnqueueSessionReminder — null means no session (Phase 28 temporary); GroupId=1 is correct single-group fallback until Phase 30 enforces group selection
- TestDataHelper.ClearDatabaseAsync preferred over factory.ResetDatabase() in isolation tests — former also seeds roles and Group 1 FK dependency preventing FK constraint failures
- Phase 28 human verify: quest list, shop, Send Reminder all confirmed working; empty /players is pre-existing dev-DB issue (AspNetUserRoles empty after Phase 26 rename + DB reset) not caused by Phase 28
- Phase 29 Plan 01: AuthenticationHelper must seed UserGroups rows for DM/Admin test users (group ID 1) alongside AspNetUserRoles — auth handlers now read UserGroups.GroupRole exclusively; tests that set "DungeonMaster"/"Admin" in the auth header must have matching UserGroups membership
- Phase 29 Plan 01: xUnit v3 IAsyncLifetime requires ValueTask return types (not Task) — TenantIsolationTests fixed
- Phase 29 Plan 03: IGroupRepository interface lives in QuestBoard.Domain/Interfaces/ (same as IUserRepository pattern) — Domain must not reference Repository
- Phase 29 Plan 05: SessionKeys.ActiveGroupId is never written pre-Phase-30 — GetAllPlayers, GetAllDungeonMasters, and AdminController.Users use `?? 1` fallback so queries work against EuphoriaInn group; **Phase 30 must remove these three `?? 1` fallbacks** once the group-picker sets the session key at login, otherwise group isolation breaks for multi-group deployments
- Phase 29 Plan 03: GroupWithMemberCount is a plain DTO (not AutoMapper-mapped) — LINQ projection from GroupEntity.UserGroups.Count in a single query; no EntityProfile mapping needed
- Phase 29 Plan 03: GroupService.AddAsync overrides base to enforce non-blank name and stamp CreatedAt; DbUpdateException for unique name violation bubbles to GroupController (plan 29-04)
- Phase 29 Plan 04: UserGroup.User? navigation property added to domain model + EntityProfile mapping — GetMembersAsync uses .Include(ug => ug.User) so data was available but AutoMapper did not surface it; Members view requires Name/Email per row
- Phase 29 Plan 04: _Layout.Platform.cshtml links only site.css — page-specific CSS files (calendar.css, quests.css, etc.) excluded from platform area
- Phase 29 Plan 02 (D-11): First SuperAdmin user assignment is a manual post-deploy step — run once after deployment:
  ```sql
  -- Assign first SuperAdmin user (run once after deploy)
  -- Find userId in AspNetUsers WHERE UserName = '<username>'
  INSERT INTO AspNetUserRoles (UserId, RoleId)
  VALUES (<userId>, 4);
  ```

### Roadmap Evolution

- Phase 31 added: Unauthenticated landing redirect — unauthenticated requests to group-scoped pages should redirect to login, not expose a specific group's content
- Phase 32 added: First-login password flow — admin-created users set their own password via a password-reset link in the welcome email; removes admin-set password from CreateUser form
- Phase 33 added: Session persistence — ASP.NET Core Session has no distributed cache registered (confirmed via grep — no AddDistributedMemoryCache/AddStackExchangeRedisCache/AddDistributedSqlServerCache/IDistributedCache anywhere in the solution), so it falls back to an in-memory store wiped on every app restart. Auth cookie survives restarts (Identity's cookie is self-contained via Data Protection) but ActiveGroupContextService's ActiveGroupId does not, forcing every logged-in user to re-pick their group after every deploy. Discovered incidentally during Phase 32 UAT. Recommended fix: Microsoft.Extensions.Caching.SqlServer + AddDistributedSqlServerCache against the existing SQL Server connection (no new infrastructure like Redis needed) plus a small periodic cleanup job.
- Phase 33 scope extended (2026-07-01): also rate-limit manual/admin-triggered email-sending buttons (e.g. "Resend Welcome Email" on /Admin/Users, EditUser's email-change confirmation) to protect the mail relay's send quota — discovered when testing showed SendConfirmationEmail has no rate limit (only ForgotPassword does, by design — PWFLOW-04/D-12 scoped it to the anonymous self-service form). User wants one-shot automated sends (e.g. CreateUser's welcome email) exempted, only repeatable manual buttons limited.
- Phase 34 added (2026-07-01): Codebase cleanup and security hardening, requested as the closing phase of v5.0 — full-codebase review (not scoped to GSD-tracked work only): remove unused/dead code, strip low-value inline comments (especially ones referencing GSD requirement IDs or phase numbers — user finds these unhelpful and unread later) in favor of XML doc comments (`///<summary>`) on interfaces, and audit for security vulnerabilities and other known issues. User considers the v5.0 milestone done once this phase completes.
- Phase 34.1 inserted after Phase 34: Security & Bugs slice split off Phase 34 per D-03 (Known Bugs, Security Considerations, related Test Coverage Gaps) (URGENT)
- Phase 34.2 inserted after Phase 34: Performance & Architecture slice split off Phase 34 per D-03 (Tech Debt, Performance Bottlenecks, Fragile Areas, Scaling Limits, Dependencies at Risk, remaining Test Coverage Gaps) (URGENT)

### Pending for Next Milestone

- Profile picture crop/avatar selection (issue #78) — paused from v2.x; verify SkiaSharp native lib on aspnet:10 Debian Bookworm
- Digest batching (EMAIL-04/REMIND-02) — revisit when same-day quest scheduling becomes common
- Any backlog items in ROADMAP.md

## Session Continuity

**Resume file:** None

Last session: 2026-07-01T21:05:06.708Z
Stopped at: Completed 34-03-PLAN.md
Next step: /gsd-execute-phase 34 (then /gsd-execute-phase 34.1, then /gsd-execute-phase 34.2 — strict order required per 34.2-CONTEXT.md D-05)

## Performance Metrics

| Phase | Plan | Duration | Notes |
|-------|------|----------|-------|
| Phase 26 P02 | 12 | 3 tasks | 0 files |
| Phase 27 P01 | 15 | 2 tasks | 10 files |
| Phase 27 P02 | 25 | 2 tasks + checkpoint | 4 files |
| Phase 27 P03 | 15 | 1 task + checkpoint | 1 file |
| Phase 28 P01 | 4 | 2 tasks | 9 files |
| Phase 28 P02 | 6 | 2 tasks | 17 files |
| Phase 28 P03 | 41 | 1 task + checkpoint | 1 file |
| Phase 29 P01 | 8 | 3 tasks | 10 files |
| Phase 29 P02 | 5 | 1 task | 3 files |
| Phase 29 P03 | 7 | 2 tasks | 7 files |
| Phase 29 P04 | 5 | 2 tasks | 17 files |
| Phase 29 P05 | 8 | 2 tasks + checkpoint | 5 files |
| Phase 30 P01 | 25min | 4 tasks | 10 files |
| Phase 30 P03 | 20min | 3 tasks | 5 files |
| Phase 30 P02 | 15min | 2 tasks | 6 files |
| Phase 30 P04 | 10min | 2 tasks | 2 files |
| Phase 33 P01 | 12min | 3 tasks | 4 files |
| Phase 33 P02 | 2min | 2 tasks | 2 files |
| Phase 33 P03 | 15min | 3 tasks | 2 files |
| Phase 34 P01 | 4min | 2 tasks | 1 files |
| Phase 34 P02 | 9min | 2 tasks | 9 files |
| Phase 34 P03 | 10min | 2 tasks | 21 files |
| Phase 34 P04 | 22min | 2 tasks | 26 files |
| Phase 34 P05 | 5min | - tasks | - files |

## Decisions

- [Phase ?]: GroupPickerController uses [Authorize] only (no policy) — non-SuperAdmin loads scoped via GetGroupsForUserAsync to prevent cross-group enumeration
- [Phase ?]: RedirectToLocal logic replicated inline per-controller (not a shared base) to match existing AccountController convention
- [Phase ?]: CreateUser.Mobile.cshtml follows EditUser.Mobile.cshtml admin-form-card-mobile pattern (not Login.Mobile.cshtml) — closer existing analog within Views/Admin/
- [Phase ?]: UserRepository list methods (GetAllPlayers/GetAllDungeonMasters) return empty list on null ActiveGroupId rather than throwing — no controller layer to redirect from at repository level
- [Phase 30-02]: RegisterViewModel left in place (unused but harmless) — removing it was out of scope for plan 30-02
- [Phase 30-02]: ConfirmationEmailJob enqueue logic removed from AccountController.Register entirely (not relocated) — already exists in AdminController.CreateUser from plan 30-03
- [Phase ?]: [Phase 30-04]: Used SessionKeys.ActiveGroupName constant reference (not literal string) in both layouts, importing QuestBoard.Service.Constants
- [Phase 33-01]: Distributed cache registered before AddSession, Testing-guarded exactly like the existing Hangfire branch — No new structural pattern needed; matches established convention
- [Phase 33-01]: ExpiredItemsDeletionInterval left unset (framework default 30 min) — Per plan D-04; no Hangfire cleanup job needed for single-tenant scale
- [Phase ?]: Used a programmatic PartitionedRateLimiter<int> singleton + AttemptAcquire instead of an AddRateLimiter policy (Phase 33-02) — userId/Id are POST form fields, not route values
- [Phase ?]: EditUser's email-resend rate-limit guard placed inside the emailChanged branch only, not at method entry (Phase 33-02, D-07) — non-email-changing saves are not counted
- [Phase ?]: [Phase 33-03]: Human verification results recorded in a dedicated 33-HUMAN-UAT.md (mirrors 27-HUMAN-UAT.md/32-HUMAN-UAT.md convention) rather than only inline in the plan summary
- [Phase ?]: [Phase 34-01]: No .sln file exists in repo — build/vulnerability-scan commands adapted to per-project invocation (Service + UnitTests + IntegrationTests) covering all 5 projects instead of the plan's literal QuestBoard.sln reference
- [Phase 34-02]: AccountController.cs (D-11) and DailyReminderJob.cs (D-05) still carry ID-tagged comments — out of this plan's declared file scope, flagged for a later cleanup plan
- [Phase ?]: [Phase 34-03]: Widened the ID-tag verification grep from [A-Z]{2,12}-[0-9]{1,3} to [A-Z]{1,12}-[0-9]{1,3} to also catch single-letter D-xx tags (D-02, D-05, D-08) the plan's literal acceptance-criteria pattern missed — applied D-06/D-08 rule consistently
- [Phase 34-04]: Base-interface members (IBaseService<T>, IBaseRepository<T>) documented once at their declaring interface — derived interfaces not re-documented for inherited members, per plan instruction
- [Phase ?]: [Phase 34-05]: IBaseRepository<T> members documented once at the base interface, not repeated on the 8 derived Repository interfaces (matches 34-04 convention for Domain base interfaces)
