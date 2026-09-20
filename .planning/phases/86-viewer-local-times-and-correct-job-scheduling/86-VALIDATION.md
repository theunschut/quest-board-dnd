---
phase: 86
slug: viewer-local-times-and-correct-job-scheduling
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-20
---

# Phase 86 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded by `/gsd-plan-phase` from `86-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2) with `FluentAssertions` 8.10.0 and `NSubstitute` 5.3.0 |
| **Config file** | none — settings live in `QuestBoard.UnitTests.csproj` / `QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick ~15s (no DB) · full suite dominated by integration tests |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~15 seconds for the per-task signal

---

## Per-Task Verification Map

> Task IDs are assigned by the planner. This table is seeded with the behaviors
> RESEARCH.md identified; `/gsd-validate-phase` fills in the Task ID / Plan / Wave
> columns once PLAN.md files exist.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | wall-clock unmoved | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~WallClockUnmoved` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | real-instant render | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~LocalTimeRender` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | unresolvable zone → UTC fallback + Degraded | — | fails safe to UTC, app still starts | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~BoardClockFallback` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | `/health` 200 + Degraded body when fallback active | — | N/A | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~HealthCheck` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | `EventSeriesService` results unchanged after `DateTime.Today` → board clock | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EventSeriesService` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | Hangfire `RecurringJobOptions.TimeZone` resolves to the configured board zone | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~RecurringJobOptions` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] A hand-rolled fake board clock / `FixedTimeProvider`-equivalent test double, matching the existing hand-rolled (not package-based) pattern in `QuestBoard.UnitTests/.../EventsOverviewAggregationTests.cs:20-25`
- [ ] `EventSeriesServiceTests` — RESEARCH.md found no existing test class; the 7-site `DateTime.Today` migration currently has no regression net
- [ ] A wrapped/injectable timezone resolver so a test can force `TimeZoneInfo.FindSystemTimeZoneById` to throw, exercising the UTC-fallback + Degraded path that a healthy container would never reach
- [ ] A test seam for the Hangfire recurring-job options (extract options construction into a testable method — Hangfire is not registered in the `Testing` environment per `WebApplicationFactoryBase.cs`, so the live registration cannot be integration-tested)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `tzdata` is present in the running container and `Europe/Amsterdam` resolves | RESEARCH.md Assumption A1 | Requires the real built image; CI's test host resolves zones regardless | Build the image and run `docker run --rm <image> /bin/sh -c 'ls /usr/share/zoneinfo/Europe/Amsterdam'`, or hit `/health` on a running container and confirm the board-clock check is Healthy (not Degraded) |
| No flash of UTC on first paint for a viewer outside the board zone | CONTEXT.md client-side rendering decision | Visual/timing behavior in a real browser | Load a page with rendered instants with the browser zone set to e.g. `America/New_York`; confirm the pre-hydration value carries an explicit zone label rather than a bare wrong-looking time |
| The calendar feed is byte-identical before and after the phase | ROADMAP risk "Shifting the calendar feed" | End-to-end artifact comparison against a subscribed client | Capture the `.ics` output on `main` and on the phase branch for the same seeded data; diff must be empty |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
