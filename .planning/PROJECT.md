# D&D Quest Board

## Current State: v5.0 Shipped

**Current milestone shipped:** v5.0 Multi-Tenancy — 2026-07-02
**Previous milestone shipped:** v4.0.0 — 2026-06-28
**Stack:** ASP.NET Core 10 MVC + SQL Server + EF Core + Hangfire
**Deployment:** LXC container on Linux host (`/opt/questboard/`), Postfix for email relay via Resend SMTP

---

## Current Milestone: v2.0 Omphalos Integration

**Goal:** DMs can open Omphalos session notes for any quest with one click — navigated automatically into the correct session, authenticated via a short-lived signed token.

**Target features:**
- Admin Settings page (Platform/SuperAdmin area, key-value store) for Omphalos URL and shared secret — instance-wide, not per-group
- "Open DM Tool" link in the DM navbar dropdown, and an "Open Session Notes" button on quest pages
- Omphalos: new SSO endpoint that validates Quest Board tokens, auto-provisions users, and finds/creates the matching quest session
- Foundation laid for bidirectional API calls (Omphalos → Quest Board) in a future milestone — not built now, just not architecturally blocked

This redoes the abandoned `milestone/3-omphalos-integration` branch (Phases 10–11 in ROADMAP.md), whose code diverged too far from `main` to merge after v3.0–v5.0 landed. Its planning docs (research, 30 scoped requirements, HMAC token-format design) are being re-verified from scratch rather than reused directly, since a lot has changed (multi-tenancy did not exist when that research was written).

Other candidates carried forward from v5.0, deferred until after this milestone:

- `GroupSessionMiddleware` POST-body data-loss risk on session expiry mid-submission (code review flagged, not yet fixed)
- Digest batching for session reminders (EMAIL-04/REMIND-02)
- Profile picture crop/avatar selection (issue #78)

---

## What This Is

A D&D campaign management web application for a group of players and Dungeon Masters. It handles quest creation and scheduling, player signup with date voting, a character/guild system, a shop with gold economy, and email notifications (HTML-templated, Hangfire-dispatched, Resend-relayed). Built with ASP.NET Core 10 MVC, SQL Server, and Docker — deployed as a single container to a self-hosted Linux environment.

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

### Active

- [ ] Admin (SuperAdmin) can configure Omphalos URL and shared secret from a Platform Settings page, instance-wide
- [ ] DM/Admin can open Omphalos from a navbar link and from a quest page, landing in the correct session with no manual login
- [ ] Omphalos validates incoming Quest Board tokens, auto-provisions the user, and finds/creates the linked quest session
- [ ] Foundation laid for a future bidirectional (Omphalos → Quest Board) API, without building it this milestone
- [ ] Digest batching for session reminders — single combined email when player has multiple same-day quests (EMAIL-04/REMIND-02 — deferred; same-day quests have never occurred in one year)
- [ ] Profile picture crop/avatar selection for guild member page (issue #78) — paused from v2.x; SkiaSharp native lib availability needs verification on deployment host
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

**Codebase:** ~56 450 lines added / ~6 374 removed in v5.0 (754 files touched, incl. the full EuphoriaInn→QuestBoard rename). Full codebase estimated 40 000–50 000 LOC C#/Razor.

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

**Omphalos (companion app, v2.0 milestone):**
- Separate repo at `C:\Repos\omphalos`, GitHub-hosted, owned/maintained by someone else — the Quest Board author is a collaborator with write access, not the primary maintainer
- Stack: .NET 10 Minimal API (route-group-per-feature pattern, no MVC controllers) + PostgreSQL + EF Core; React SPA frontend
- Single-tenant, flat data model — no org/tenant/workspace concept of its own; every entity scopes only to `UserId`. This is why Omphalos integration settings are instance-wide on the Quest Board side rather than per-group
- Auth: bcrypt password hashing, JWT issued via a private `AuthService.GenerateToken(User)` method (not yet on `IAuthService`), delivered as an `omphalos_token` httpOnly cookie (`SameSite=Lax`; a `Secure=true` TODO is still uncommented in `AuthEndpoints.cs`)
- `GameSession` entity: client-generated string `Id`, `Title`, `SessionLog` (jsonb), plus `UserId` FK, timestamps, and nav collections to `Character`/`Location`/`Encounter` — no existing link back to a Quest Board quest
- CORS plumbing already exists (`AllowedOrigins` config array in `appsettings.json`), just empty/inert by default — adding Quest Board's origin is a config change, not new code

## Constraints

- **Compatibility:** No user-facing functionality may be removed or broken
- **Tech stack:** ASP.NET Core 10 MVC + SQL Server + EF Core — no framework changes
- **Deployment:** Must remain deployable via `dotnet run` on LXC Linux host; no additional setup steps
- **Database:** All schema changes require EF Core migrations; auto-applied on startup
- **Email:** 100 emails/day, 3 000/month Resend relay limit; 17 members — batch-first design
- **Cross-repo coordination:** Omphalos-side phases modify a separate repo (`C:\Repos\omphalos`) owned by another maintainer — changes there go through normal PR review on that repo, not this one's branch protection

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
| v2.0 Omphalos Integration redone from scratch rather than merging the old `milestone/3-omphalos-integration` branch | That branch forked before v3.0/v4.0/v5.0 landed; its code diverged too far from `main` to merge cleanly | — Pending: research is being redone independently; the old branch's planning docs are historical reference only |
| Omphalos integration settings (URL + shared secret) are instance-wide, configured under `/platform` by SuperAdmin — not per-group | Omphalos has no tenant/org concept of its own (flat, `UserId`-scoped data model); a per-group setting would have nothing on the Omphalos side to map to | — Pending: confirm during Phase 35 implementation |
| Cross-app SSO identity match key is Quest Board's `UserEntity.Id` (int), not username or email | Two independent research passes converged here, citing this codebase's own Phase 34.3 bug (display-name-based ownership check let one user impersonate another, since `User.Name` has no uniqueness constraint) as precedent for avoiding the same mistake on the Omphalos side | ✓ Confirmed — user approved 2026-07-02 after discussing the Phase 34.3 bug and a raised-but-deferred int→GUID PK migration idea |
| Migrating `AspNetUsers`/all entity PKs from `int` to GUID — considered, deferred | Would touch `IdentityUserClaim<int>`/`IdentityUserLogin<int>`/`IdentityUserRole<int>`/`IdentityUserToken<int>` and every FK across the live multi-tenant schema; unrelated in scope to the Omphalos SSO milestone, and doesn't change the SSO design's security properties since the identity claim travels inside an HMAC-signed, short-lived token (same pattern as this app's existing password-reset tokens) | — Pending: independent future decision if desired, not blocking this milestone |
| Signed HMAC redirect token (not full OAuth2/OIDC) for the Quest Board↔Omphalos bridge, built behind a swappable `IIntegrationTokenService`/`SsoService` seam | Both apps are first-party and under common operational control — OAuth's redirect-URI allowlisting/client-registration/consent machinery solves delegated *third-party* access, a different problem than this. The swappable seam keeps a future migration to a shared IdP (see next row) cheap — it would touch only these two services, not every nav link/button/endpoint consumer | ✓ Confirmed — user agreed 2026-07-02 after discussing OAuth2/OIDC tradeoffs explicitly |
| Replay protection (short-lived used-token tracking) is in scope for this milestone | Initially scoped out on a "small trusted group" justification; user pushed back — cuts should be argued on architecture/security merit, not current user count, when aiming for genuine SaaS-grade quality. Replay protection is standard practice for a signed-token handoff and cheap to add | ✓ Confirmed — user decision 2026-07-02 |
| Standing up a shared identity provider (Authentik recommended over Keycloak — lighter footprint, Python expression-policy customization, stronger self-hoster docs/community fit for this LXC/solo-ops deployment; Keycloak is the safer industry-default alternative) is an explicit future milestone, not part of this one | Migrating both apps' existing auth (Quest Board's ASP.NET Core Identity, Omphalos's bcrypt/JWT) to a central IdP is a substantial undertaking on its own — real for when a 3rd app exists, but would significantly delay this milestone's actual goal if folded in now | — Pending: future milestone, triggered by a second/third app needing shared auth |
| v2.0 phases start at Phase 35, continuing from v5.0's Phase 34.3 | The old Phase 10/11 slots (reserved between v1.0 and v3.0) belonged to the abandoned branch's attempt; reusing them would misrepresent when this work actually happened | — Pending: old Phase 10/11 ROADMAP.md entries marked superseded |

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

*Last updated: 2026-07-02 — v2.0 Omphalos Integration milestone started (Phase 35+)*
