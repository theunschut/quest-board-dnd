# Retrospective — D&D Quest Board

## Milestone: v4.0 — Email Notifications

**Shipped:** 2026-06-28
**Phases:** 6 (20–25) | **Plans:** 22

### What Was Built

1. Hangfire background job infrastructure — SQL Server storage, admin-only dashboard, `IServiceScopeFactory` scope pattern
2. HTML email templates for all notifications — `_EmailLayout`, `QuestFinalized`, `QuestDateChanged`, `SessionReminder`, `ConfirmEmail` Razor components rendered via `HtmlRenderer`
3. Quest-finalization dedup guard — `FinalizedEmailSentForDate` EF column prevents re-send on re-finalization
4. 24h automated session reminders — Hangfire daily CRON at 09:00 + DM manual trigger + `ReminderLog` idempotency table
5. Admin email stats dashboard — Resend REST API pagination, 5-min cache, graceful degraded states
6. Email confirmation flow — admin resend button, `EmailConfirmed` guard on all four email jobs, ASP.NET Identity token callback

### What Worked

- **Wave-based planning:** Independent phases (23 vs 21–22) ran in any order after Phase 20 landed — scheduling flexibility reduced blocking time
- **NullObject dispatchers:** The `NullQuestEmailDispatcher` / `NullReminderJobDispatcher` pattern isolated Hangfire from the test host cleanly — no Moq/NSubstitute complexity, no test factory pollution
- **Auto-fix deviations:** The executor caught and fixed bugs inline (UseHangfireDashboard Testing guard, BeOneOf FluentAssertions v8 syntax, NSubstitute extension method assertion) without derailing the plan
- **Scope discipline:** Dropping EMAIL-04/REMIND-02 (digest batching) early based on real usage data ("never happened in a year") prevented building unused complexity
- **Code review after Phase 24:** The post-implementation review caught three real security issues (XSS in email body, token probing via userId ≤ 0, auto-sign-in of unconfirmed users) that the plan's threat model missed

### What Was Inefficient

- **Test factory gap (IBackgroundJobClient):** Phase 21 introduced `HangfireQuestEmailDispatcher` without updating `WebApplicationFactoryBase`. This caused 4 mobile integration test failures that were noted as "out of scope" in Phase 23's SUMMARY and finally fixed only in a separate commit on 2026-06-28. The gap was visible for 3+ days before it was addressed.
- **Phase 22 progress table:** The ROADMAP progress table showed Phase 22 as "1/5 In progress" even after all plans completed — a stale metadata artifact that persisted until milestone close
- **AdminController constructor coupling:** Taking `IBackgroundJobClient` directly in `AdminController`'s primary constructor (instead of via the `IReminderJobDispatcher` abstraction) leaked the Hangfire dependency into tests unnecessarily. The `NoOpBackgroundJobClient` stub in the test factory is a workaround for a design that could be improved by routing through the abstraction.

### Patterns Established

- `IServiceScopeFactory` + `CreateAsyncScope()` inside every Hangfire job — mandatory pattern, established in Phase 20 SmokeTestJob
- NullObject dispatchers for infrastructure not available in Testing env — `NullQuestEmailDispatcher`, `NullReminderJobDispatcher`; ready template for future jobs
- `HtmlRenderer` for email rendering — explicitly chose over `IRazorViewEngine` (throws in background context); documented for all future email templates
- Razor email component structure: `_EmailLayout` wrapping, `@Parameter` for data binding, `IEmailRenderService.RenderAsync<T>()` call site

### Key Lessons

1. **Seal Testing environment gaps at the plan level, not after:** When a new controller or service depends on infrastructure guarded by `!IsEnvironment("Testing")`, add the test stub in the same plan — don't leave it as "noted, out of scope"
2. **`IBackgroundJobClient` should stay behind an abstraction:** Controllers that only enqueue one specific job type (like `ConfirmationEmailJob`) should take an `IReminderJobDispatcher`-style interface, not `IBackgroundJobClient` directly — keeps the test factory clean
3. **Resend API `last_event` semantics are non-obvious:** `opened` and `clicked` count as delivered (not separate from delivered). ResendStatsAggregator needed a comment; future maintainers will hit this
4. **Code review as a separate phase step pays off:** The post-Phase-24 review found 3 real security issues the plan's threat model missed. Running a dedicated review after the highest-risk phase (auth token handling) was worth the extra step

### Cost Observations

- Sessions: multiple across 4 days
- Notable: HtmlRenderer vs IRazorViewEngine discovery avoided a hard-to-debug runtime failure in production; catching it in planning saved significant debugging time

---

## Milestone: v5.0 — Multi-Tenancy

**Shipped:** 2026-07-02
**Phases:** 12 (26–34.3, incl. 3 inserted decimal phases) | **Plans:** 48
**Timeline:** 4 days (2026-06-29 → 2026-07-02)

### What Was Built

1. Full `EuphoriaInn.*` → `QuestBoard.*` namespace rename across namespaces, project files, and config, with zero behavior change
2. Multi-group data model — `GroupEntity`, `UserGroups` junction, `GroupId` FKs — with EF Core Global Query Filters for tenant isolation
3. `SuperAdmin` role with a dedicated `/platform` management area for group and membership administration
4. Group-picker UX, admin-only user creation, and removal of public self-registration
5. Unauthenticated-visitor lockdown with a public landing page at `/` and the quest board moved to `/quests`
6. Passwordless first-login flow (welcome email sets password + confirms email in one step) plus a self-service, enumeration-safe Forgot Password flow
7. Session persistence across app restarts via `AddDistributedSqlServerCache`, and rate limiting on repeatable admin email-send actions
8. Closing cleanup/hardening pass (Phases 34/34.1/34.2) plus an urgent pre-ship fix for a group-role authorization regression (Phase 34.3)

### What Worked

- **Dedicated zero-behavior-change rename phase first (Phase 26):** Doing the `EuphoriaInn` → `QuestBoard` rename as its own phase, gated on the full test suite, before any schema work meant the multi-tenancy phases that followed never had to reason about renamed code and stayed easy to review
- **Incremental PROJECT.md evolution:** Requirements were moved to Validated and Key Decisions were logged phase-by-phase rather than saved for milestone close — by the time this milestone closed, PROJECT.md needed almost no rework
- **Splitting an oversized cleanup phase (D-03):** Phase 34's full scope (~26 fix items, 120 comment occurrences, 35-interface doc backfill) was pre-emptively split into 34/34.1/34.2 before execution started, keeping each phase within a reviewable size
- **Catching the group-role regression before deploy:** Manual pre-ship testing (Phase 34.3) caught ~20 inline `IsInRole` call sites orphaned by Phase 27's `AspNetUserRoles` cleanup — a real production-breaking regression — before it ever reached the live environment
- **Code review as a standing gate:** Review caught a display-name authorization-bypass bug and a SuperAdmin-reachable crash in Phase 34.3's own migration, and a typo'd config-key startup guard in Phase 34.1, both before shipping

### What Was Inefficient

- **Comment-tag cleanup (D-06) took 4 gap-closure rounds:** Each verification pass used an independently-derived grep pattern that only covered previously-known comment syntax (C#, then Razor/HTML, then CSS, then dotfiles) — genuinely different syntax per file type kept surfacing new occurrences. Now codified as a CLAUDE.md rule banning GSD tracking IDs from source going forward, so this class of cleanup phase should not recur.
- **REQUIREMENTS.md checkbox drift:** GROUP-04/05/06 and TENANT-01/02/03/05 were marked complete in the traceability table but left unchecked in the requirement list itself, surfacing only at milestone close — the same class of staleness a quick task had already fixed once earlier in the project (260420-bqj)
- **`?? 1` fallback churn across Phases 29–30:** Several call sites used `activeGroupContext.ActiveGroupId ?? 1` as a temporary workaround before Phase 30 wired up the real session key, requiring a second pass to remove once the picker landed

### Patterns Established

- Shared `GetEffectiveGroupRoleAsync` helper as the single source of truth for group-role + SuperAdmin-bypass logic, replacing scattered inline `IsInRole` checks
- `RequireActiveGroupId()` as the fail-hard null-guard pattern for controller paths that must never proceed without an active group, vs. fail-soft `?? 1`/`is { }` patterns for genuinely optional contexts (e.g. a user's own profile page)
- `HangfireJobHelper` scope + group-context helper applied uniformly across background jobs
- `*-HUMAN-UAT.md` per phase as the standing convention for recording human-verification checkpoint results (SQL Server spot-checks, live browser session behavior) that automated tests structurally cannot cover

### Key Lessons

1. **Split oversized phases before execution, not after:** Recognizing Phase 34's scope was too large and splitting it into 34/34.1/34.2 up front (D-03) kept each phase reviewable — waiting until a phase was already underway would have been more disruptive
2. **A dedicated pre-ship regression pass is worth it for cross-cutting migrations:** Any change that moves authorization data out of one system into another (here, `AspNetUserRoles` → `UserGroups.GroupRole`) creates a search problem, not just a rewrite problem — budget a phase for finding every call site, not just the ones the plan already knows about
3. **Traceability-table status and checkbox status can drift independently:** When a document tracks the same fact in two places (a checkbox list and a status table), reconcile them explicitly at milestone close rather than assuming they stayed in sync
4. **Ban tracking-ID comments in source from day one:** The 4-round comment-cleanup slog directly motivated a standing CLAUDE.md rule; codifying the lesson immediately (not just noting it) is what prevents the next cleanup phase

### Cost Observations

- Sessions: multiple across 4 days
- Notable: 12 phases (9 planned + 3 inserted) shipped in 4 days is the fastest milestone pace so far — driven by short, well-scoped phases and parallel waves within phases
