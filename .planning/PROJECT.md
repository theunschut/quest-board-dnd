# D&D Quest Board

## Current Milestone: v7.0 Backlog Cleanup

**Goal:** Close out four standing backlog items — two mobile UI bugs and two long-deferred feature requests — restoring mobile parity and adding requested flexibility to post-finalization voting and character/profile image cropping.

**Target features:**
- Fix iOS Safari's `background-attachment: fixed` mobile background bug — content scrolls with the image instead of it staying fixed (#116)
- Add the missing "Session Recap Available" badge to the mobile Quest Log list view, present on desktop (#115)
- Post-finalization vote flexibility on One-Shot quests — no hard capacity block, waitlist promotion (Yes > Maybe > No, then signup time), and a targeted email only for players auto-promoted by someone else's action (#104)
- Client-side crop-before-save UI applied to every image upload field (character photo, DM profile photo) — user picks the frame in-browser before the image is saved, and both the original and cropped image are stored so the character details page can keep showing the original (#78 / v1.0 Phase 8, deferred since v1.0 pending image-tooling verification)

**Stack:** ASP.NET Core 10 MVC + SQL Server + EF Core + Hangfire
**Deployment:** LXC container on Linux host (`/opt/questboard/`), Postfix for email relay via Resend SMTP

---

## What This Is

A D&D campaign management web application for a group of players and Dungeon Masters. It handles quest creation and scheduling, player signup with date voting, a character/guild system, a shop with gold economy, and email notifications (HTML-templated, Hangfire-dispatched, Resend-relayed). Groups choose a board type at creation — a One-Shot board (date voting + finalization, the original flow) or a Campaign board for ongoing games with a fixed party (no date voting/signup, simple Close/Reopen lifecycle, no scheduling emails). Built with ASP.NET Core 10 MVC, SQL Server, and Docker — deployed as a single container to a self-hosted Linux environment.

## Core Value

The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## Requirements

### Validated

- ✓ Quest creation with proposed dates and difficulty selection — existing
- ✓ Player signup with Yes/No/Maybe date voting — existing
- ✓ DM quest finalization with player selection and email notification — existing
- ✓ User authentication and registration (ASP.NET Core Identity) — existing
- ✓ Role-based access control (Admin, DungeonMaster, Player) — existing
- ✓ Character creation and guild member directory — existing
- ✓ Shop with gold economy and item transactions — existing
- ✓ Monthly calendar view for quest scheduling — existing
- ✓ Admin panel for user and quest management — existing
- ✓ Docker deployment with SQL Server — existing
- ✓ Domain layer must not depend directly on Repository entities — v1.x (Phase 01)
- ✓ Business logic lives in services, not controllers — v1.x (Phase 02)
- ✓ Controllers reduced to: validate input → call service → return view/redirect — v1.x (Phase 02)
- ✓ Dead code removed (SecurityConfiguration, magic numbers, UpdateQuestPropertiesAsync) — v1.x (Phase 03)
- ✓ Account lockout (5 attempts, 15-min lock), 8-char minimum password — v1.x (Phase 04)
- ✓ DM profile page (photo, name, bio) — v2.x (Phase 07)
- ✓ Shop filter and sort by price/rarity — v2.x (Phase 05)
- ✓ Follow-up quest creation (pre-filled players, new date required) — v2.x (Phase 06)
- ✓ Purpose-built mobile views via `.Mobile.cshtml` + view-location expander — v3.0 (Phases 12–19)
- ✓ HTML email templates (Razor + HtmlRenderer) replacing all plain-text emails — v4.0 (Phase 21)
- ✓ Hangfire infrastructure — SQL Server storage, admin dashboard at `/hangfire` — v4.0 (Phase 20)
- ✓ 24h automated session reminders via Hangfire CRON + DM manual trigger — v4.0 (Phase 22)
- ✓ Idempotent reminder dedup via ReminderLog table — v4.0 (Phase 22)
- ✓ Admin email stats dashboard (Resend API: sent/delivered/bounced/failed) — v4.0 (Phase 23)
- ✓ Email confirmation flow — admin resend button, job guards, callback endpoint — v4.0 (Phase 24)
- ✓ First-login password flow — admin-created accounts are passwordless until the user sets their own password via a Welcome email link; self-service Forgot Password flow with enumeration-safe, rate-limited reset; old confirm-email-only flow retired — v5.0 (Phase 32)
- ✓ Session persistence — `ActiveGroupId` (and all other session data) survives app restarts via a SQL Server-backed distributed cache (`AddDistributedSqlServerCache`), replacing the in-memory session store — v5.0 (Phase 33)
- ✓ Admin email-resend rate limiting — "Resend Welcome/Confirmation Email" and `EditUser`'s email-change confirmation are limited to 3/hour per target user, protecting the Resend relay's quota from accidental button-mashing; one-shot automated sends (e.g. `CreateUser`'s welcome email) remain exempt — v5.0 (Phase 33)
- ✓ Codebase cleanup and security hardening — dead code removed (`RegisterViewModel`), GSD requirement-ID/phase-number comment tags stripped across the entire codebase (C#, Razor views, CSS, dotfiles) while preserving substantive comments, XML `<summary>` doc backfill on all 37 public Domain/Repository interfaces, clean dependency vulnerability scan captured as evidence — v5.0 (Phase 34)
- ✓ Known Bugs and Security Considerations closure — Production-only fail-fast startup guard when email config is missing, `ResendStatsClient` extracted with bounded 429 retry-with-backoff, Resend Bearer-token and secret-safe logging patterns documented, regression tests for all three plus a reflection-based CSRF `[ValidateAntiForgeryToken]` coverage sweep and a verify-and-close test for the stale `SessionReminderJob` null-dereference claim — v5.0 (Phase 34.1)
- ✓ Performance & Architecture closure — follow-up quest create+rollback consolidated into `QuestService.CreateFollowUpQuestWithDetailsAsync`, net-new `ControllerExtensions` MVC-boilerplate helpers applied to `AdminController`, composite index on `Quests(IsFinalized, FinalizedDate)`, lean shop list-view projection, global Hangfire `AutomaticRetryAttribute` retry policy, `HangfireJobHelper` scope/group-context helper across all 3 jobs, EF Core Global Query Filter documentation, AutoMapper enum-cast round-trip test, and the `RequireActiveGroupId()` ASVS V4 null-guard wired into `QuestController.SendReminder`'s job dispatch — v5.0 (Phase 34.2), closes the v5.0 Multi-Tenancy milestone's 9 phases
- ✓ Group role authorization regression fix — ~20 inline `IsInRole("Admin"/"DungeonMaster")` call sites across `QuestController`, `QuestLogController`, `DungeonMasterController`, and `Admin/AccountController`, orphaned by Phase 27's move of per-group roles out of `AspNetUserRoles`, migrated to a shared `GetEffectiveGroupRoleAsync` helper (SuperAdmin bypass preserved); code review then caught and fixed a display-name authorization-bypass bug and a SuperAdmin-reachable `RequireActiveGroupId()` crash the migration itself introduced — v5.0 (Phase 34.3), discovered during manual pre-ship testing
- ✓ Board type configuration (BOARD-01, BOARD-02) — `BoardType` enum (`OneShot`/`Campaign`) threaded through entity/domain/projection models and an EF Core migration; SuperAdmin picks a board type on group creation via a required, blank-first dropdown; the value is displayed read-only afterward (disabled dropdown on Edit, badge on Index) and is immutable server-side — Edit POST never reads `BoardType` (`[BindNever]` plus omission from the controller assignment), and Create rejects out-of-range values via `[EnumDataType]` — v6.0 (Phase 35)
- ✓ Campaign quest posting & closing (CQUEST-01 through CQUEST-06) — additive `IsClosed`/`ClosedDate` fields and `CloseQuestAsync`/`ReopenQuestAsync` on `QuestService`, kept separate from `FinalizeQuestAsync`/`OpenQuestAsync` so the one-shot flow is untouched; campaign quest board/Manage/Details/Create views (desktop + mobile) drop the date picker, per-quest signup, and CR badge, replacing them with a single Close/Reopen control; closed campaign quests appear in the Quest Log immediately (no next-day wait) while one-shot next-day behavior is preserved; no quest-related emails fire for campaign boards. Code review caught and fixed a server-side authorization gap (`Close`/`Reopen`/`Edit` never validated `BoardType`, allowing a DM to close a one-shot quest or leave `DungeonMasterSession` set on a campaign quest) plus a Quest Log DM-session leak and a pre-existing follow-up-quest `GroupId` bug from Phase 34.2 — v6.0 (Phase 36)
- ✓ Navigation & access control (NAV-01 through NAV-06, ACCESS-01) — `_Layout.cshtml`/`_Layout.Mobile.cshtml` hide Calendar, Shop, Manage Shop, Edit My Profile, and Players in campaign groups via an allowlist ("show only when confirmed One-Shot"), never a Campaign blocklist, so anonymous/no-group states default to hidden; Guild Members and Quest Log stay visible for all board types; Calendar additionally hidden for anonymous visitors; `AdminController.EmailStats` locked to `SuperAdminOnly` (ANDed with the class-level `AdminOnly`) with a real `/Account/AccessDenied` page wired app-wide via `ConfigureApplicationCookie`, replacing silent 404s on every policy failure. Human-verify checkpoint caught a genuine app-startup-blocking circular DI dependency (`ActiveGroupContextService` had grown a dependency on `IGroupService`, which transitively depends back on the `QuestBoardContext` that itself depends on `IActiveGroupContext`) — fixed by splitting the BoardType lookup onto a new `IBoardTypeResolver` service — v6.0 (Phase 37), closes the v6.0 Board Types (Campaign Mode) milestone's 3 phases
- ✓ Group-scoped user list (USERS-01) — `AdminController.Users()` now reads via a new `GetAllGroupMembers`/`GetAllGroupMembersAsync` method (manual `UserGroups` join, no role filter) instead of the unscoped `GetAllAsync()`, closing a cross-tenant PII leak; the four role-change POST actions (`PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`) gained a membership guard rejecting out-of-group `userId`s. Code review then found the identical authorization gap on four sibling actions in the same controller (`EditUser`, `ResetPassword`, `DeleteUser`, `SendConfirmationEmail`) — fixed in the same phase via the same guard pattern, plus a regression test for the write-path guards and a follow-up fix for two mobile tests the new guard broke — v6.1 (Phase 38)
- ✓ Existing-email collision handling in Create User flows (CREATE-01 through CREATE-04) — shared `CreateOrAddToGroupAsync` method on `IUserService` collision-detects by email and returns one of four outcomes (new account, added-to-group, added-to-group-stranded-account, already-member); wired into `AdminController.CreateUser` with the three locked flash messages and a distinct `AddedToGroup` notification email (no set-password link) for the collision-add case, while stranded (never-confirmed) accounts get a fresh Welcome/SetPassword resend instead. `groupId` is a plain parameter (not session-derived), the call shape Phase 40's platform entry point will reuse. Code review caught and fixed an unhandled null-deref on account re-resolution and a TOCTOU race in membership-collision detection — v6.1 (Phase 39)
- ✓ Platform group Members page redesign (MEMBERS-01 through MEMBERS-03) — two-column desktop / stacked mobile layout (current members left, searchable non-members right) replacing the plain `<select>` dropdown; `UserRepository.GetAvailableUsers`/`GetAvailableUsersAsync` filters non-members by name/email with `groupId` sourced strictly from the method parameter; a "Create New User" modal reuses Phase 39's `CreateOrAddToGroupAsync` scoped to the route group. A live blocking human-verify checkpoint drove three rounds of user-directed scope additions beyond the original plan text, all completed and re-verified in the same phase: a matching server-side search added to the Current Members column (own backend query, controller wiring, and dual-search-term preservation across every Add/Remove/CreateMember redirect), and a header-bar restructure (action buttons moved into the title bar) applied to Members.cshtml, Members.Mobile.cshtml, and the out-of-plan Group/Index.cshtml. Code review found 3 warnings (case-insensitivity relies on DB collation rather than explicit comparison, the search-filter predicate duplicated across `GroupRepository`/`UserRepository`, and `AddMember`/`RemoveMember` lack the group-existence guard `CreateMember` has) — none blocking — v6.1 (Phase 40)
- ✓ Safe user removal & account disable (SAFE-01 through SAFE-04) — `AdminController.DeleteUser` repurposed from a hard account-delete to `groupService.RemoveMemberAsync(groupId, userId)` (the same primitive `GroupController.RemoveMember` already used), removing only the active group's `UserGroupEntity` row; account, other memberships, characters, and quest/shop/transaction/reminder history are all untouched, eliminating every `DbUpdateException` risk from `NoAction` FKs. "Delete" renamed to "Remove from Group" (desktop + mobile). New SuperAdmin-only `Areas/Platform/Controllers/UsersController.cs` (cross-group user list, desktop + mobile) adds Disable/Enable actions built entirely on ASP.NET Core Identity's existing `LockoutEnd` mechanism (`DisableUserAsync` sets it to `DateTimeOffset.MaxValue` + bumps the security stamp; `EnableUserAsync` clears it) — deliberately never touches `LockoutEnabled`, preserving it as a DB-only manual escape hatch for specific trusted accounts. Self-disable blocked; disabling another SuperAdmin allowed. `SecurityStampValidatorOptions.ValidationInterval` shortened to 5 minutes app-wide so a disabled account's already-active session is force-expired quickly. `AccountController.Login`'s lockout branch now distinguishes a disabled account (exact `LockoutEnd == DateTimeOffset.MaxValue` match) from an ordinary 15-minute failed-attempt lockout with accurate messaging. Code review found and fixed 2 warnings same phase: `Disable`/`Enable` were discarding the `IdentityResult` and always reporting success (now surfaces failure via `TempData["Error"]`), and the new mobile Platform Users view had no stylesheet loaded (added `platform-users.mobile.css` + `@section Styles`) — v6.1 (Phase 41)
- ✓ Site-wide toast notification redesign (no REQ-IDs — deferred-idea promotion from Phase 39's discussion) — unified ~24 view files plus Shop's existing local toast implementation onto one shared `_Toasts.cshtml` Razor partial rendered from all 5 layouts (`_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`, and the Platform Area's separate `_Layout.Platform`/`_Layout.Platform.Mobile` pair — a scope correction research found beyond CONTEXT.md's stated 3 layouts). Added a 4th `Info` toast type + `RedirectWithInfo` helper on `ControllerExtensions`; standardized TempData key naming app-wide to `Success`/`Error`/`Warning`/`Info`, retiring `AccountController`'s outlier `SuccessMessage`/`ErrorMessage`/`InfoMessage` keys (incidentally fixing two previously-silent broken message paths — an expired email-confirmation Error toast and a Profile email-change Info toast — as an intentional side effect, not scope creep); consolidated 4 duplicated toast-init scripts into one `site.js` listener. Shop's unrelated "Mystical Merchant" novelty toast and all ASP.NET form-validation markup were explicitly left untouched. All 13 locked decisions and both delegated-discretion items verified against source; 6 UI/timing behaviors requiring live-browser confirmation were user-approved via UAT — v6.1 (Phase 42), closes the v6.1 Bugfixes milestone's now-5 phases
- ✓ Mobile parity fixes (MOBILE-01, MOBILE-02) — iOS Safari's `background-attachment: fixed` bug (#116) fixed in both `mobile.css` and `site.css` via a `body::before` pseudo-element (`position: fixed`, `100dvh`/`100vh` fallback, `z-index: -1`); mobile Quest Log now shows the "Session Recap Available" amber/gold badge (#115), ported pixel-for-pixel from desktop's `.recap-badge`. Both verified on a real iPhone 17 Pro (iOS 26) over LAN, not devtools emulation, per this project's own standing PITFALLS.md requirement — v7.0 (Phase 43)

### Active
- [ ] Digest batching for session reminders — single combined email when player has multiple same-day quests (EMAIL-04/REMIND-02 — deferred; same-day quests have never occurred in one year)
- [ ] Post-finalization vote changes and waitlist auto-promotion for One-Shot quests, with a targeted email for passively-promoted players (issue #104)
- [ ] Client-side crop-before-save for character and DM profile photo uploads, storing both original and cropped image (issue #78 / v1.0 Phase 8) — paused since v1.0 pending image-tooling verification, now in progress
- [ ] `GroupSessionMiddleware` redirects on all HTTP verbs including POST — a POST-body data-loss risk if the session expires mid-submission; flagged by code review during Phase 31, not yet fixed

### Out of Scope

- D&D Beyond PDF character sheet parser (#84) — large standalone feature, future milestone
- 5etools integration (#82) — large standalone feature, future milestone
- Miniature request page (#59) — large standalone feature, future milestone
- Email verification on registration — small trusted group; manual confirmation flow added instead
- Resend SDK for sending — delivery path stays SmtpClient → Postfix → Resend SMTP relay
- Webhook-based delivery tracking — polling sufficient at current scale (17 members)
- Per-user email opt-out preferences — defer; small trusted group
- Image blob storage migration — performance acceptable at current scale

## Context

**Codebase:** ~56 450 lines added / ~6 374 removed in v5.0 (754 files touched, incl. the full EuphoriaInn→QuestBoard rename). ~13 717 lines added / ~535 removed in v6.0 (121 files touched, 28 tasks across 11 plans). ~2 720 lines added / ~597 removed in v6.1 (63 files touched, 37 tasks across 16 plans, 5 phases). Full codebase estimated 40 000–50 000 LOC C#/Razor.

**Tech stack:**
- ASP.NET Core 10 MVC with Razor views (`.cshtml`) and Razor components (`.razor`) for email templates
- SQL Server via EF Core 10 (auto-migrated on startup)
- Hangfire 1.8 with SQL Server storage (2 workers) for background jobs
- Resend SMTP relay via Postfix; Resend REST API for stats
- HtmlRenderer (`Microsoft.AspNetCore.Components.Web`) for email template rendering in job context

**Deployment:** Linux host at `/opt/questboard/`, env overrides at `/etc/questboard/.env`. Postfix for outbound mail → Resend SMTP relay. No Docker required — direct `dotnet run` on host.

**Known issues / tech debt:**
- `NoOpBackgroundJobClient` stub registered in test factory alongside NullObject dispatchers — AdminController takes `IBackgroundJobClient` directly as constructor arg (fixed 2026-06-28)
- `FinalizedDate` stored as server local time (CET/CEST) — reminder job uses `DateTime.Today.AddDays(1)` which is correct for LXC host timezone but should be reviewed if deployment timezone changes
- Resend API stats only paginate backwards from now; historical data beyond 30 days not surfaced
- BoardType lookup is implemented 3 times with near-identical logic (`QuestController.GetActiveBoardTypeAsync`, `QuestLogController.GetActiveBoardTypeAsync`, `BoardTypeResolver.GetBoardTypeAsync`) — verified safe (all three agree for every reachable request, since `GroupSessionMiddleware` guarantees a resolved `ActiveGroupId` for non-SuperAdmin users) but not consolidated onto the `IBoardTypeResolver` seam — v6.0 (Phase 37 integration audit)
- Mobile nav (`_Layout.Mobile.cshtml`) has no Email Stats or Background Jobs link at all — pre-existing gap predating v6.0, not a regression; SuperAdmin can still reach both by direct URL on mobile, just without an in-app nav shortcut. Fix is ready-to-apply per `37-REVIEW.md` WR-01, not yet actioned — v6.0 (Phase 37)
- Integration tests always override `IActiveGroupContext`/`IBoardTypeResolver` with a test double (`MutableGroupContext`), so no automated test exercises `Program.cs`'s real production DI graph end-to-end — a regression of the circular DI cycle fixed in Phase 37 wouldn't be caught by the current suite (mitigated once by a live `dotnet run` smoke test during verification, no permanent guard) — v6.0 (Phase 37)
- `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` appears to be dead code — the Platform area's `_ViewStart.cshtml` unconditionally selects the desktop `_Layout.Platform`, unlike the root `_ViewStart.cshtml` which branches on `IsMobile`, so Platform `.Mobile.cshtml` view files actually render inside the desktop layout's chrome today. Two CSS file header comments (`platform-group.mobile.css`, `platform-users.mobile.css`) incorrectly claim otherwise. Discovered during Phase 42 research; deliberately left unfixed (toast partial was still wired into the unreachable layout for future-proofing) since it's a layout-selection bug unrelated to toasts — v6.1 (Phase 42 research)

## Constraints

- **Compatibility:** No user-facing functionality may be removed or broken
- **Tech stack:** ASP.NET Core 10 MVC + SQL Server + EF Core — no framework changes
- **Deployment:** Must remain deployable via `dotnet run` on LXC Linux host; no additional setup steps
- **Database:** All schema changes require EF Core migrations; auto-applied on startup
- **Email:** 100 emails/day, 3 000/month Resend relay limit; 17 members — batch-first design

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Refactor + new features in same milestone (v1.x) | Avoids two sequential code-freeze windows | ✓ Validated: phases 1-4 refactor complete, features landed on clean arch |
| HtmlRenderer for email templates | IRazorViewEngine throws NullReferenceException in background job context | ✓ Good — stable across all 4 email jobs |
| IServiceScopeFactory in all Hangfire jobs | Scoped services (DbContext, IEmailService) cannot be constructor-injected in jobs | ✓ Good — established in Phase 20, followed consistently |
| IDashboardAuthorizationFilter for Hangfire dashboard | LocalRequestsOnlyAuthorizationFilter bypassed by Docker reverse proxy | ✓ Good — admin-only enforcement works behind nginx |
| NullObject dispatchers in Testing env | Hangfire not registered in Testing; NullQuestEmailDispatcher / NullReminderJobDispatcher satisfy interface contracts | ✓ Good — 191 tests green |
| Resend stats via plain HttpClient (no SDK) | Read-only stats endpoint; avoids unnecessary package dependency | ✓ Good — simple, maintainable |
| Dropped digest batching (EMAIL-04/REMIND-02) | Same-day quests have never occurred in one year; complexity not justified yet | — Pending: revisit when scheduling density increases |
| Profile picture crop paused | SkiaSharp native lib availability on deployment host unverified | — Pending: verify libSkiaSharp on aspnet:10 Debian Bookworm before resuming |
| SuperAdminOnly policy uses RequireRole("SuperAdmin") — no custom handler | SuperAdmin is a system-wide Identity role, not group-scoped; RequireRole is sufficient and simpler | ✓ Good — Phase 29 |
| Platform area at /platform (not /superadmin) | SuperAdmin manages all groups; the area is about the platform, not the user's title | ✓ Good — Phase 29 |
| Hangfire dashboard restricted to SuperAdmin; nav link hidden for non-SuperAdmins | SuperAdmin is the system-level admin; group-scoped Admins should not access background job infrastructure | ✓ Good — Phase 29 |
| Promote/demote write actions guard on null ActiveGroupId (no ?? 1 fallback for writes) | Writes without an active group context could mutate the wrong group; reads use ?? 1 as a display-only workaround until Phase 30 sets the session key | ✓ Phase 30 must set session key and remove all ?? 1 fallbacks |
| [Bind(Prefix = "AddMember")] on AddMember action parameter | Members view renders AddMemberViewModel fields with "AddMember." prefix (nested asp-for); Bind prefix aligns binder with posted field names | ✓ Good — Phase 29 |
| Landing/quests split: `/` becomes a public marketing page, quest board moved to `/quests` | Unauthenticated visitors must not see group-scoped content at the app's root | ✓ Good — Phase 31; authenticated visitors hitting `/` auto-redirect to `/quests` (added mid-verification after user found the logged-out landing copy confusing when already signed in) |
| Session-recovery middleware redirects an authenticated user with no active group to `/groups/pick` | Prevents broken/empty group-scoped pages when the session's group selection expires but the auth cookie persists | ✓ Good — Phase 31; SuperAdmin and picker/auth/platform/error paths exempted to avoid redirect loops |
| GroupSessionMiddleware redirects on all HTTP verbs, including POST | Simplicity — one redirect check before Authorization, no verb-specific branching | — Pending: code review flagged a POST-body data-loss risk if session expires mid-submission (31-REVIEW.md CR-01); not yet fixed |
| SetPassword gets its own "set-password" rate-limit policy rather than sharing ForgotPassword's | A legitimate forgot-password + set-password flow by one user would otherwise consume 2 of the same 3-request/15-min budget; confirmed concretely when reusing the shared policy broke an integration test | ✓ Good — Phase 32 |
| ForwardedHeaders trust is config-driven (ReverseProxy:KnownProxies, empty by default) rather than hardcoded | Traefik runs on a separate CT from the App CT; without trusting its IP, RemoteIpAddress-based rate limiting collapses into one shared bucket for all users in production | ✓ Good — Phase 32; env var set at deploy time via docs/server-setup.md |
| No custom Hangfire cleanup job for `AspNetSessionState` expired rows | `SqlServerCache`'s own internal polling (`ExpiredItemsDeletionInterval`, default 30min) already purges expired rows; verified against source — a duplicate job would race it for no benefit | ✓ Good — Phase 33; code review initially flagged this as missing (reviewer lacked research-doc context) but it was already correctly decided against |
| Admin email rate limit partitioned by target userId, not admin identity | Protects any one recipient's inbox from repeated sends regardless of which admin triggers it | ✓ Good — Phase 33 |
| Comment-tag cleanup (D-06) took 4 gap-closure rounds before verification passed | Each verifier ran a fresh, independently-derived grep pattern that only covered previously-known comment syntax (C#, then Razor/HTML, then CSS, then dotfiles) — genuinely different syntax per file type kept surfacing new occurrences, not sloppy execution | ✓ Good — Phase 34; codified as a CLAUDE.md "Code Comments" rule banning GSD tracking IDs from source going forward, so a dedicated cleanup phase is never needed again |
| Production email-config startup guard checked `Email:FromEmail`/`Email:SmtpServer` instead of the actual `EmailSettings:FromEmail`/`EmailSettings:SmtpServer` binding | Planning-stage typo carried faithfully through execution; the wrong keys never exist so the guard would have thrown on every Production boot regardless of real config | ✓ Fixed — Phase 34.1; caught by code review (CR-01) same phase, before shipping |
| `RequireActiveGroupId()` guard built as an opt-in seam (Phase 34.2 plan scope), not force-wired into controllers | Avoids widening one plan's blast radius across every `ActiveGroupId` call site in one pass | ✓ Wired into `QuestController.SendReminder` same phase — code review (WR-01) and verification both flagged the guard's own target bug (`?? 1` fallback) as still live; user chose "fix now" over deferring |
| Ownership checks standardized on `User.Id` comparison, not `User.Name` | `User.Name` has no uniqueness constraint (`AccountController.Edit` lets any user rename freely); a name-based DM-ownership check let one user impersonate another by colliding display names | ✓ Fixed — Phase 34.3; code review (CR-01) caught it same phase, before shipping |
| SuperAdmin short-circuit for `GetEffectiveGroupRoleAsync`/`RequireActiveGroupId()` must be applied per call site, not just once | 34.3-06's own self-caught fix only special-cased `QuestController.Index`; code review (CR-02) found ~13 sibling call sites across 3 controllers still crashed for a SuperAdmin with no active group | ✓ Fixed — Phase 34.3; user chose "fix now" over shipping with known SuperAdmin 500s |
| `BoardType` immutability enforced by both convention (Edit POST never assigns it) and binding (`[BindNever]` on `GroupEditViewModel.BoardType`) | Code review (WR-01) found the original convention-only guard could silently regress under a future Edit-action refactor | ✓ Good — Phase 35 |
| Disabled form-control/form-select `:disabled` styling added globally, not scoped to the Group Edit view | Human verification found disabled selects were visually indistinguishable from interactive ones (pre-existing unconditional `!important` background rules overrode Bootstrap's default disabled styling); the same latent bug already affected other disabled inputs elsewhere in the app (e.g. `ShopManagement/Edit`) | ✓ Good — Phase 35 |
| `CloseQuestAsync`/`ReopenQuestAsync` kept as thin passthroughs on `QuestService`, structurally separate from `FinalizeQuestAsync`/`OpenQuestAsync` (zero dispatcher reference) | Guarantees no quest-related email can ever fire for a campaign-group action, by construction rather than by a conditional check that could be forgotten | ✓ Good — Phase 36 |
| Per-action `GetActiveBoardTypeAsync` helper (`QuestController`/`QuestLogController`) defaults to `BoardType.OneShot` on a null active group, deliberately diverging from `IBoardTypeResolver`'s null-preserving nav-gating lookup (Phase 37) | The two callers have different correctness needs: quest-rendering logic needs a concrete board type to pick a view shape; nav-gating needs to treat "no group" as its own hidden state. Milestone audit traced this and confirmed `GroupSessionMiddleware` makes the divergence unobservable for any reachable non-SuperAdmin request | ✓ Good — Phase 36/37; confirmed safe by v6.0 milestone audit, not consolidated (see Known issues) |
| `IBoardTypeResolver` split out of `IActiveGroupContext`/`ActiveGroupContextService` into its own interface + service | `ActiveGroupContextService` is a constructor dependency of `QuestBoardContext` (via `IActiveGroupContext`, for the tenant query filter); giving it a dependency on `IGroupService` (whose repository chain needs `QuestBoardContext`) created a circular DI graph that a factory-based registration hid from .NET's build-time cycle detector, so the app silently failed to start instead of throwing a clean error. Caught at the phase's human-verify checkpoint, not by `dotnet build` or automated tests | ✓ Fixed — Phase 37; no automated regression guard yet (see Known issues) |
| Current Members search built server-side (own query + redirect-preservation), not a client-side filter | User explicitly requested "same logic, consistency" with the pre-existing Available Users search rather than a cheaper client-side approach, even though the members list is already fully loaded | ✓ Good — Phase 40; both search terms now survive every Add/Remove/CreateMember redirect independently |
| Header-bar action buttons (Back to Groups, Create New User, Create Group) moved into the title row across three views mid-checkpoint | User-driven consistency request during live verification; agent declined to apply the change to the out-of-plan Group/Index.cshtml until the user explicitly confirmed it, rather than silently expanding the diff under review | ✓ Good — Phase 40 |
| `LockoutEnabled` deliberately never set by the Disable/Enable feature — only `LockoutEnd` | User wants a DB-only manual escape hatch: flipping `LockoutEnabled = false` directly in production makes a trusted account permanently immune to in-app disable, since `UserManager.IsLockedOutAsync` short-circuits to `false` whenever `LockoutEnabled` is `false` regardless of `LockoutEnd` | ✓ Good — Phase 41; enforced by a `grep -c "LockoutEnabled" == 0` acceptance criterion, not just a code-review convention |
| Disable also bumps `SecurityStamp` and shortens `SecurityStampValidatorOptions.ValidationInterval` to 5 minutes app-wide (from Identity's 30-min default) | An already-issued auth cookie is only invalidated on the next stamp re-validation; without this, a disabled user's active session could persist up to 30 minutes | ✓ Good — Phase 41; negligible extra DB load at this app's scale (17 members) |
| Login distinguishes disabled vs. temporary lockout via an exact `LockoutEnd == DateTimeOffset.MaxValue` match, not a fuzzy threshold | `MaxValue` is the literal sentinel the disable feature sets; a real failed-attempt lockout is always exactly 15 minutes (`options.Lockout.DefaultLockoutTimeSpan`), so no ambiguity exists between the two cases | ✓ Good — Phase 41 |
| Shared `_Toasts.cshtml` built as a plain Razor partial (not a View Component), wired into all 5 layouts including the Platform Area's separate pair | Matches the codebase's existing lightweight-partial convention (`_Calendar.cshtml`, `_ShopItemDetailsContent.cshtml`); RESEARCH found CONTEXT.md's stated "3 layouts" was incomplete — the Platform Area resolves to its own `_Layout.Platform`/`_Layout.Platform.Mobile` pair via a separate area-level `_ViewStart.cshtml` | ✓ Good — Phase 42 |
| TempData key naming standardized to `Success`/`Error`/`Warning`/`Info` app-wide, retiring `AccountController`'s outlier `SuccessMessage`/`ErrorMessage`/`InfoMessage` keys | One shared partial needs one consistent key scheme rather than dual-scheme branching logic; standardizing incidentally fixed two message paths that were silently dropped today due to a key mismatch between where they were written and where they were read | ✓ Good — Phase 42 |
| GoldReceived "+X gp" celebratory toast kept as a bespoke Shop-specific block layered alongside the shared partial's generic Success toast, not folded into it | It's a unique celebratory UI element with its own badge treatment; forcing it into the generic message system would lose that distinct visual | ✓ Good — Phase 42 |
| `body::before` pseudo-element (not `background-attachment: fixed`) is the standing pattern for any future full-viewport fixed-background CSS | `background-attachment: fixed` on `body` has a known WebKit-iOS compositing bug (scrolls with content instead of staying fixed); the pseudo-element sidesteps it entirely | ✓ Good — Phase 43 |
| Real-device checkpoints for parallel worktree-isolated plans must be tested only after merging the plans' worktree branches back into the working branch | A checkpoint task can only verify code that's actually reachable by the running dev server; worktree-isolated executor commits are invisible to the main checkout until merged, so an early test against the main tree will see stale (pre-fix) code and produce a false failure — this exact false-negative happened live during Phase 43's iOS Safari checkpoint | ⚠ Process note for future phases — Phase 46 (crop UI) will also need real-device verification and should merge worktrees before, not after, the human test |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---

*Last updated: 2026-07-04 — Phase 43 (Mobile Parity Fixes) complete*
