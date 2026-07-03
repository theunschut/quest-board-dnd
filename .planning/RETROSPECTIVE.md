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

---

## Milestone: v6.0 — Board Types (Campaign Mode)

**Shipped:** 2026-07-03
**Phases:** 3 (35–37) | **Plans:** 11 | **Tasks:** 28
**Timeline:** ~1.4 days (2026-07-02 → 2026-07-03)

### What Was Built

1. `BoardType` enum (`OneShot`/`Campaign`) on groups, chosen by SuperAdmin at creation and immutable afterward (enforced by both convention and `[BindNever]`)
2. Campaign quest lifecycle — additive `IsClosed`/`ClosedDate` + `CloseQuestAsync`/`ReopenQuestAsync`, structurally separate from the one-shot `Finalize`/`Open` flow (zero dispatcher reference, so no email can ever fire)
3. Campaign views (board/Manage/Details/Create, desktop + mobile) drop the date picker, per-quest signup, and CR badge for a single Close/Reopen control; closed quests appear in the Quest Log immediately (no next-day wait)
4. Board-type-aware navigation — desktop and mobile nav hide Calendar/Shop/Manage Shop/Edit My Profile/Players for campaign groups via an allowlist, never a blocklist
5. SuperAdmin-only Email Stats with a real app-wide `AccessDenied` page (`ConfigureApplicationCookie`) replacing silent 404s

### What Worked

- **Interface-first foundation plans:** Phase 37-01 defined the `GetBoardTypeAsync` contract and a RED test scaffold before Phase 37-03 consumed it in the layout — the executor never had to scavenger-hunt for the seam
- **Parallel waves with zero file overlap:** 37-01/37-02 and 36-04/36-05 ran as parallel plans within the same wave since their `files_modified` never intersected — the intra-wave overlap check worked as designed
- **Human-verify checkpoints caught real, non-cosmetic bugs:** Phase 37's checkpoint surfaced a genuine app-startup-blocking circular DI dependency, not just a visual nitpick — validates that these checkpoints earn their keep beyond CSS review
- **Code review as a standing gate:** Caught a follow-up-quest `GroupId` bug, a SuperAdmin-crashing board-type lookup, a Quest Log DM-session leak, and (independently) the mobile Email Stats discoverability gap — all before or during the same phase, not post-ship

### What Was Inefficient

- **REQUIREMENTS.md checkbox drift — again:** v5.0's own retrospective (above) logged this exact failure mode as Key Lesson #3 ("traceability-table status and checkbox status can drift independently... reconcile them explicitly at milestone close"). v6.0 hit the identical issue — all 15 checkboxes stayed unchecked despite phases shipping and the status column saying "Complete"/"Pending" inconsistently. The lesson was written down but not turned into an enforced mechanism, so it recurred verbatim.
- **Circular DI dependency invisible to build and automated tests:** `dotnet build` and the full test suite both stayed green while the app was completely unable to start, because integration tests register a test-double `IActiveGroupContext`/`IBoardTypeResolver` that never exercises the real `Program.cs` DI graph. Only a live `dotnet run` (first done manually by the user, then repeated by the verifier) caught it. No automated regression guard for this class of bug exists yet.
- **New interface didn't trigger a consolidation pass:** Introducing `IBoardTypeResolver` to fix the DI cycle left the pre-existing `QuestController`/`QuestLogController` board-type lookups un-migrated onto the new seam — caught by the milestone-level integration checker, not during Phase 37 itself, because Phase 37's own scope didn't touch those files.

### Patterns Established

- **Keep DbContext-adjacent interfaces constructor-thin:** if a service is (even transitively) a constructor dependency of `QuestBoardContext`, it must never depend on anything that itself needs `QuestBoardContext` — split a richer capability into its own interface (`IBoardTypeResolver`) rather than growing the thin one (`IActiveGroupContext`)
- **Allowlist over blocklist for "show only in state X" nav gating:** `== BoardType.OneShot` (not `!= BoardType.Campaign`) so every indeterminate case (anonymous, no active group) naturally resolves to hidden without a separate null-handling branch

### Key Lessons

1. **A written-down lesson isn't a fix until it's enforced:** the checkbox-drift lesson from v5.0 recurred in v6.0 verbatim. Consider a mechanical check (e.g., a phase-close gate that diffs `REQUIREMENTS.md` checkboxes against `VERIFICATION.md` status) rather than relying on the retrospective alone to prevent repeat occurrences.
2. **DI-graph changes need a live-boot check, not just build+test green:** whenever a constructor dependency changes on a type that sits between `Program.cs` and a DbContext, run the app for real (not just `dotnet build`/`dotnet test`) before considering the change safe — mocked test doubles in integration tests can fully hide a circular dependency that only manifests against the real DI container.
3. **When a new interface is introduced mid-phase to fix an architectural issue, immediately grep for existing duplicate implementations of the same concern** — the milestone-level integration checker found the 3x-duplicated BoardType lookup that Phase 37's own review didn't flag, since it was outside that phase's file scope.

### Cost Observations

- Sessions: 1 extended session
- Model mix: opus (planning), sonnet (research/execution/verification/review/integration-check), haiku (codebase mapping) — consistent with the project's "balanced" model profile
- Notable: 3 phases in ~1.4 days, continuing the accelerating pace from v5.0 (12 phases/4 days) — driven by the same short-scoped-phase + parallel-wave approach, plus the milestone-level integration checker catching cross-phase issues that no single phase's own review would surface
