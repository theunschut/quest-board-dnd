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

---

## Milestone: v6.1 — Bugfixes

**Shipped:** 2026-07-04
**Phases:** 5 (38–42) | **Plans:** 16 | **Tasks:** 37
**Timeline:** ~1 day (2026-07-03 22:30 → 2026-07-04 17:51)

### What Was Built

1. Group-scoped Users page closing a live cross-tenant PII leak, plus a membership guard retrofitted onto four sibling actions the same phase's code review found (Phase 38)
2. Shared `CreateOrAddToGroupAsync` collision-handling method — new account, added-to-group, stranded-account resend, or already-member — reused identically by both the group-admin and platform create-user entry points (Phase 39)
3. Two-column Platform Members page redesign with live search and an in-page Create New User modal, extended mid-checkpoint with three user-directed scope additions (Phase 40)
4. Safe group-only user removal (replacing an account-destroying hard delete) plus a real SuperAdmin disable/enable mechanism built on Identity's `LockoutEnd` field (Phase 41)
5. Site-wide Bootstrap toast notification redesign — one shared `_Toasts.cshtml` partial across all 5 layouts (including a Platform-area layout pair CONTEXT.md's own file list implied but its "3 layouts" prose missed), replacing ~26 views' static alert banners (Phase 42)
6. Bonus: fixed a standalone navbar dropdown overflow bug (found during Phase 42 verification, unrelated to the milestone's scope)

### What Worked

- **Reusing Phase 39's shared method in Phase 40:** `CreateOrAddToGroupAsync` was built once and consumed unchanged by the Platform Members page's Create New User entry point — no duplicate collision-handling logic, exactly as the phase-ordering decision intended
- **Research before planning, even when CONTEXT.md looked complete:** Phase 42's CONTEXT.md was unusually thorough (13 locked decisions, exact reference implementation), and the "skip research" default reasoning was tempting — but running research anyway caught a real, material scope gap: the Platform Area resolves to its own separate layout pair, not one of the 3 layouts CONTEXT.md named. A planner working from CONTEXT.md alone would have shipped a phase with a functional regression in the Platform area.
- **Parallel wave execution with a pre-dispatch overlap check:** Phase 42's Wave 2 ran 4 plans simultaneously in isolated worktrees (Shop / Platform / Account / Admin+Quest — confirmed disjoint `files_modified` before dispatch) with zero merge conflicts
- **Post-merge build/test gate as a reality check on research claims:** RESEARCH.md for Phase 42 said "no test framework in this repo" (based on searching for `*.Tests` project references). Running the actual post-merge gate found 424 passing unit/integration tests — the claim was wrong, caught by running the tool instead of trusting the doc.
- **UAT approval collapsing cleanly to a single user response:** When the user said "approve for all tests" instead of walking through each of 6 UAT items individually, that mapped directly onto the existing UAT.md "approved" pass semantics — no new mechanism needed, just applying the existing rule in bulk.

### What Was Inefficient

- **A Phase 42 ROADMAP.md detail section was structurally misplaced:** at some point before milestone close, Phase 42's `### Phase 42:` detail section ended up appended after the flat `## Progress` table instead of inside the v6.1 `<details>` block alongside Phases 38–41. This silently broke `roadmap.analyze`'s phase count (it found 4 phases, not 5) and went unnoticed until the milestone-close readiness check surfaced it. Nothing in the phase-42 planning or execution flow validates ROADMAP.md's own structural integrity — only a downstream tool query happened to expose the drift.
- **4 pre-existing quick-tasks lacked the `status: complete` frontmatter field** the milestone-close audit checks for — some dating back to 2026-04-20, predating whenever that field became part of the convention. The work itself was genuinely done (all had passing Self-Check + committed changes); the gap was purely a schema-versioning issue that only surfaced at milestone-close audit time, not when the field was introduced.
- **RESEARCH.md's "no test framework" claim was stated with unwarranted confidence:** it was based on not finding a `*.Tests` project during exploration, not on an explicit `dotnet test` run. A quick verification command would have caught the 424-test suite immediately instead of it surfacing later during the actual build/test gate.

### Patterns Established

- **Research is worth running even when CONTEXT.md looks complete**, specifically for phases that touch many files across a codebase's less-obvious structural seams (multiple layouts, multiple `_ViewStart.cshtml` files) — thoroughness of user-provided context doesn't substitute for verifying the codebase's actual current-state facts
- **Post-merge build/test gates should run the project's actual verification command**, not skip based on a research doc's claim that "no tests exist" — always attempt the real command and let it fail informatively if the claim was correct

### Key Lessons

1. **ROADMAP.md's structural integrity (phase detail sections living inside the correct milestone's `<details>` block) needs an explicit check, not just visual review** — a misplaced section silently degraded `roadmap.analyze`'s phase count for an unknown period before milestone close caught it. Consider validating this whenever a phase is added or a milestone is closed.
2. **When a schema field is added to a template (like `status:` on quick-task summaries), older artifacts don't automatically get it** — they'll surface as false-positive "incomplete" items at the next milestone-close audit. Backfilling at the time the schema changes (or explicitly grandfathering pre-existing artifacts) avoids the confusion of re-diagnosing genuinely-done work later.
3. **A "no test framework" (or similar absence) claim in a research doc should be verified with the actual command, not inferred from project-file search** — this project had 424 tests that simply weren't discovered by looking for a `*.Tests` project reference in the wrong place.

### Cost Observations

- Sessions: 1 extended session covering Phase 42 planning through execution through milestone close
- Model mix: opus (planning), sonnet (research/execution/verification/UI/plan-checking) — consistent with the project's "balanced" model profile
- Notable: fastest milestone yet by wall-clock time (~1 day, 2026-07-03 22:30 → 2026-07-04 17:51) across 5 phases, 16 plans

---

## Milestone: v7.0 — Backlog Cleanup

**Shipped:** 2026-07-08
**Phases:** 22 (43–64) | **Plans:** 59
**Timeline:** ~3.1 days (2026-07-04 22:30 → 2026-07-08 00:21)

### What Was Built

1. Two mobile parity fixes — the iOS Safari `background-attachment: fixed` scroll bug (via a `body::before` pseudo-element pattern) and a missing Session Recap badge on the mobile Quest Log (Phase 43)
2. Post-finalization vote flexibility — waitlist auto-promotion, centralized `WaitlistOrdering`, and a single-recipient promotion email, with a live-verified scope extension letting Maybe also fill an open seat (Phase 44)
3. Dual-image storage (original + cropped, zero server-side image processing) and a Cropper.js v2.1.1 client-side crop UI applied to every character/DM-profile upload field — closing issue #78, deferred since v1.0 (Phases 45–46)
4. 18 ad-hoc backlog phases folded in during execution (47–64) — an NPC/Contacts directory, a full Guild Members → Characters rename, a quest Rewards field, a Dead character status, and a long tail of bug fixes
5. Two real cross-tenant security leaks found and closed mid-milestone: Character/DM-profile/PlayerSignup group-tenant filtering gaps (Phase 49) and a SuperAdmin null-ActiveGroupId escape hatch plus a SelectGroup IDOR gap (Phase 55)
6. A closing performance pass — Character/Contact/DM-profile list queries stopped eager-loading image bytes, matching the pattern QuestRepository already used (Phase 62)

### What Worked

- **PROJECT.md kept current phase-by-phase, not batched at milestone close:** every phase's Validated-section writeup and Key Decisions rows were added as part of that phase's own doc-update step, not deferred. By the time this milestone closed, PROJECT.md needed only a header rewrite, an LOC-stats update, and one Active-item removal — no retroactive reconstruction of 22 phases' worth of decisions.
- **Ad-hoc phase insertion via `/gsd-phase` scaled cleanly to 18 extra phases:** the milestone's original 4-phase scope (43–46) grew to 22 phases without ever needing a roadmap restructure — each new phase just depended on the previous one and got appended, letting genuinely independent bug reports and feature requests get folded in as they surfaced instead of queued for a future milestone.
- **User-confirmed scope extensions handled as first-class deviations, not silent reinterpretation:** Phase 44's Maybe-can-fill-a-seat behavior and Phase 46's DM-profile-shows-cropped decision were both live disagreements between shipped behavior and the original requirement wording, resolved by asking the user directly mid-checkpoint and then updating ROADMAP.md/REQUIREMENTS.md wording to match — not by either silently shipping the stricter reading or blocking on it.
- **Deferring a verification checkpoint (device access) instead of blocking the phase:** Phase 46's real-device touch/EXIF/canvas-memory checks were explicitly recorded as an open, tracked gap rather than forcing the phase to wait indefinitely for hardware access — and the user closed that gap independently before milestone close, exactly as the deferral was designed to allow.

### What Was Inefficient

- **ROADMAP.md's summary bullet list and Progress table drifted out of sync with Phase Details, again:** Phases 61–64 had full `### Phase N` detail sections and SUMMARY.md files on disk, but were missing from the v7.0 `<details>` bullet list and the `## Progress` table — the same class of structural drift v6.1's retrospective already flagged (Key Lesson #1: "ROADMAP.md's structural integrity needs an explicit check, not just visual review"). It recurred here across 4 phases instead of 1, caught only at milestone-close time by cross-referencing the Phase Details section against the summary table by hand.
- **REQUIREMENTS.md's traceability table stayed "Pending" for 13 of 14 requirements despite all being shipped:** this is the third consecutive milestone (v5.0, v6.0, and now v7.0) where a requirement-tracking table drifted from actual phase-completion status and was only reconciled at milestone close, despite v5.0's retrospective explicitly naming this failure mode and recommending a mechanical check rather than relying on memory.
- **STATE.md's `current_phase`/`stopped_at` fields lagged the real state by 2 phases:** frontmatter said "Phase 63, stopped at Phase 64 context gathered" when Phases 63 and 64 were both already fully executed with SUMMARY.md files on disk — the session-continuity metadata wasn't updated as execution continued past its last checkpoint.
- **Git history for this milestone was hard to reconstruct forensically:** the local git tooling proxy (rtk) silently returned stale/filtered `git log` output during the completion workflow (missing the branch tip and recent commits), requiring a fallback to `rtk proxy git ...` for raw output before commit-range stats could be trusted.

### Patterns Established

- **`body::before` pseudo-element (not `background-attachment: fixed`) for full-viewport fixed backgrounds** — sidesteps a known WebKit-iOS compositing bug
- **Authorization checks must validate the TARGET resource's group, not just the caller's role** — the recurring root cause behind both Phase 49 and Phase 55's security fixes
- **Mobile parity enforced by pairing desktop+mobile edits into the same task**, not merely the same wave or phase — applied proactively in Phase 61 after Phases 43 and 54 both needed backfill phases for the same gap
- **A code-review "critical" finding must be empirically verified against the real app/DOM before being treated as a blocker** — Phase 61's reviewer's plausible-but-wrong static reading was caught by a live DOM test, not by re-deriving the same reasoning

### Key Lessons

1. **A written-down lesson isn't a fix until it's enforced — this is now a 3-milestone pattern, not a 1-off.** REQUIREMENTS.md traceability drift was named as a specific failure mode in v5.0's retrospective and recurred in both v6.0 and v7.0; ROADMAP.md structural drift was named in v6.1's retrospective and recurred in v7.0 across more phases than before. Both lessons independently point to the same fix: a mechanical consistency check (diffing traceability-table status against phase-completion status, and diffing the roadmap summary/progress-table phase list against `## Phase Details` section headers) run at each phase close, not deferred to milestone close where the drift has had 20+ phases to compound.
2. **Session-continuity metadata (STATE.md's `current_phase`/`stopped_at`) needs to be updated at the same cadence as phase completion, not just at session boundaries** — it fell 2 phases behind here with no functional harm (ROADMAP.md and SUMMARY.md files were the actual source of truth), but it meant the milestone-close workflow had to reconcile actual progress from primary artifacts instead of trusting the state file at face value.
3. **When a local tool proxy silently filters or caches command output (as rtk did for `git log` during this close), verify suspicious results with the tool's own raw/debug escape hatch (`rtk proxy git ...`) before trusting derived stats** — the alternative is presenting fabricated-looking numbers (a 1.5-hour "milestone" instead of 3.1 days) with high confidence.
4. **Deferring a blocking verification item with an explicit, tracked gap (rather than forcing a wait) lets the phase close on schedule while still leaving a clear path to closure** — Phase 46's real-device gap was resolved independently by the user before milestone close specifically because it was recorded as "deferred, not skipped."

### Cost Observations

- Sessions: multiple across 4 days, plus a final milestone-close session
- Notable: largest milestone by phase count so far (22 phases vs. v5.0's previous high of 12) — driven almost entirely by ad-hoc backlog folding (18 of 22 phases had no original REQUIREMENTS.md mapping), validating that the `/gsd-phase` insertion mechanism scales past the "few inserted decimal phases" pattern seen in earlier milestones into a primary execution mode

---

## Milestone: v8.0 — Markdown Support

**Shipped:** 2026-07-11
**Phases:** 7 (65–71) | **Plans:** 26
**Timeline:** ~2 days (2026-07-09 → 2026-07-11)

### What Was Built

1. A single, secure, unit-tested Markdown-to-HTML rendering pipeline (Markdig + HtmlSanitizer) as a Domain-layer singleton, with individually-composed GFM extensions and two immutable sanitizer profiles sharing one parse (Phase 65)
2. Quest Description proof-of-concept — shared `_MarkdownEditor.cshtml` toolbar/Preview partial, a server-round-trip `/markdown/preview` endpoint, and the full write→read→email loop proven on the milestone's riskiest cross-cutting field before any other field was touched (Phase 66)
3. Mechanical repetition of the proven pattern across the remaining 8 fields — Quest Rewards/Recap (67), Character Description/Backstory (68), Contact Description/per-note Notes (69), DM Profile Bio/Shop Item Description (70)
4. The app's first multi-instance inline Markdown editor (Contact Notes), with independent per-note rendering and an auto-collapse/lazy-init registry (Phase 69)
5. Email-safety hardening — a dedicated `RenderEmailHtml` path (inline styles, MSO bullet fallback, server-side block-boundary truncation) for all 3 quest email templates (Phase 71)
6. A milestone-close audit that found and fixed one last cross-phase gap (QuestLog Description still rendering raw) before shipping, rather than shipping it as debt

### What Worked

- **The proof-of-concept phase's "by construction" guarantee paid off across every later phase:** Phase 66's decision to make `/markdown/preview` call the exact same `RenderToHtml(text, Web)` path saved-page display uses meant EDITOR-04 ("Preview matches saved") was true by architecture, not by keeping two implementations in sync — no phase after 66 had to re-verify or re-guard against preview/saved drift.
- **Mechanical field-migration scaled cleanly across 5 phases with minimal rework:** Phases 67–70 repeated Phase 66's shared editor/render pattern onto 8 more fields with no roadmap restructuring and no scope growth — a sharp contrast to v6.1/v7.0's heavy ad-hoc phase insertion. The upfront investment in a shared `_MarkdownEditor.cshtml`/`MarkdownEditorViewModel`/`markdown-editor.js` seam in Phase 66 is why.
- **Human-verification checkpoints kept finding real, non-cosmetic bugs, not just cosmetic nitpicks:** every field-migration phase's checkpoint (66, 67, 69, 70) caught at least one genuine defect live — a Critical email-wrapper bug, a cross-folder partial 404, an app-wide EasyMDE silent-submission-failure gotcha, a lost client-side length limit — reinforcing that these checkpoints are pulling real weight, not ceremony.
- **The explicit-override protocol (VERIFICATION.md frontmatter `overrides:` with reason/accepted_by/accepted_at) handled every deliberate deviation cleanly:** 3 real deviations this milestone (Phase 66's mobile toolbar width, Phase 69's Contact-Index scope call, Phase 71's Outlook/Gmail live-client gap) were each surfaced, reasoned about, and explicitly accepted rather than either silently shipped or silently blocking the phase.
- **The milestone-close cross-phase integration check earned its keep again** (as it did in v6.0's BoardType-lookup finding) — it independently re-derived Phase 67's already-known-but-never-revisited QuestLog raw-Description gap and this time the gap was fixed inline before shipping, not just logged as another round of debt.

### What Was Inefficient

- **A phase-scoped "deliberately deferred" finding had no forward-tracking mechanism:** Phase 67's WR-02 (QuestLog still renders Description raw) was correctly identified and reasoned about at the time, but nothing linked it to a future phase or a milestone-close checklist item — it only got caught because the milestone audit's integration checker happened to re-derive it independently from source, five phases later. Writing "deferred" in a REVIEW.md is not the same as guaranteeing it gets revisited.
- **A live-send verification attempt in Phase 71 nearly shipped a real production risk:** an executor's first attempt at proving the email fix worked added an `[AllowAnonymous]` endpoint that could trigger real Hangfire email jobs against an arbitrary quest ID with no auth check. Caught by the operator reading agent output in real time, not by any automated gate — verification tooling itself needs the same security scrutiny as product code, not an exemption for being "just for testing."
- **Nyquist `VALIDATION.md` coverage was inconsistent:** produced for Phases 65, 66, 69, and 71, but never generated for 67, 68, or 70 — a discovery-only gap the milestone audit surfaced but didn't block on.

### Patterns Established

- **Server-round-trip preview over a second client-side parser** — guarantees "Preview matches saved" by construction rather than by keeping two rendering implementations in sync; the milestone's single most load-bearing architectural decision
- **A dedicated `RenderEmailHtml` path, distinct from the page-rendering path** — for any future HTML-email work where the client (Outlook's Word engine especially) can't load external CSS and needs verbatim inline styles plus a real server-side content boundary instead of a CSS clip
- **`IMarkdownService.ExtractPlainText()` as the single mechanism for every plain-text card/teaser preview** — prevents raw Markdown syntax from leaking into any surface that predates a field's Markdown-authoring migration

### Key Lessons

1. **A "deliberately deferred, out of scope" finding needs an explicit forward link, not just a note in that phase's own REVIEW.md** — Phase 67's WR-02 was reasoned about correctly at the time but nothing guaranteed a later phase or the milestone audit would re-surface it; it worked out here because the integration checker happened to re-derive it independently, not because the deferral itself was tracked anywhere actionable.
2. **Verification/testing tooling that can trigger real side effects (a real email send, in this case) is production code and needs the same authorization scrutiny as the feature it's testing** — Phase 71's near-miss `[AllowAnonymous]` endpoint is a concrete instance of a class of risk that's easy to wave off as "just for the dev loop."
3. **An architectural guarantee ("by construction") beats a passing test suite for the specific property it guarantees** — Phase 66's shared-render-call design made preview/saved drift structurally impossible rather than merely covered by a test that could bit-rot; every later phase inherited that guarantee for free.

### Cost Observations

- Sessions: multiple across 2 days, plus a final milestone-close session
- Model mix: opus (planning), sonnet (research/execution/verification/review/integration-check) — consistent with the project's "balanced" model profile
- Notable: fastest-shipping phase-per-day rate yet (7 phases / ~2 days) with zero ad-hoc scope growth — a contrast to v6.1 and v7.0's heavy mid-milestone phase insertion, attributable to the proof-of-concept phase (66) absorbing nearly all of the milestone's architectural risk up front
