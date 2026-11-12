---
phase: "97"
slug: "events-series-calendar-and-agenda-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-12"
---

# Phase 97 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `97-RESEARCH.md` § "9. Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command (integration)** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~EventsConversionGuardTests\|FullyQualifiedName~LinkedBoard\|FullyQualifiedName~EventsOverview\|FullyQualifiedName~Agenda\|FullyQualifiedName~EventDetailsAvailability\|FullyQualifiedName~RowNavigation\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~CalendarControllerIntegrationTests\|FullyQualifiedName~CalendarBoardClockTests\|FullyQualifiedName~EventsDateRenderTests\|FullyQualifiedName~EventCalendarPartialTests\|FullyQualifiedName~CalendarBoardTypeScopeTests\|FullyQualifiedName~BoardPageStaticGuardTests"` |
| **Quick run command (unit)** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClock\|FullyQualifiedName~LocalTimeMarkupTests"` |
| **Full suite command** | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project avoids the CLR flake). SQL-backed migrator tests need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL Server at `localhost:1433`; run once at the end only |
| **Browser checks** | Scratch Playwright (bundled Chromium, reused from `~/qb-ui-review/phase-94/tools/node_modules`) under `~/qb-ui-review/phase-97/`; phone checks under a mobile User-Agent with touch |
| **Estimated runtime** | ~20 s quick (after build), ~2-3 min full |

Notes: re-run once on a transient `0x80131506`; never set GC env vars. Do not run while another phase's executor builds in the same checkout.

---

## Sampling Rate

- **After every task commit:** the quick filters narrowed to the slice's pages, plus the scratch probe on the touched pages under both User-Agents.
- **After every plan wave:** full suite, per project.
- **Before `/gsd-verify-work`:** full suite green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`.
- **Max feedback latency:** 120 seconds.

---

## Per-Requirement Verification Map

| Req | Behavior | Test Type | Automated Command | File Exists | Status |
|-----|----------|-----------|-------------------|-------------|--------|
| D-10 | Today highlight follows the board clock (pinned 2026-09-05T23:30Z, Europe/Amsterdam: day 6 is today); calendar partials and views in the ambient guard | integration + unit static | `~CalendarBoardClockTests`; `~AmbientClockSeamTests` | ❌ W0 new / ✅ edit (append only) | ⬜ pending |
| D-09 | Each of the ~18 moved lines prints byte-identical text; new styles pinned; existing helper tests unedited | unit + integration | `~WallClockAreaStylesTests`; `~EventsDateRenderTests`; `~WallClockMarkupTests`, `~LocalTimeMarkupTests`, `~WallClockUnmovedTests` unchanged | ❌ W0 new | ⬜ pending |
| D-06 | Static linked entry: no link, no tab stop, no data attribute, no handler; hidden status text stays | static + integration | `~LinkedBoardStaticGuardTests`, `~LinkedBoardCalendarTenantIsolationTests` | ✅ edit | ⬜ pending |
| D-07 | Member linked entry is one link to that board's page with the dashed-outline hook and glyph | integration + static | `~LinkedBoardCalendarStyleTests` | ✅ rewrite | ⬜ pending |
| D-01/D-02/D-03 | Slot table in CSS; module has no hover-open mode, no `innerHTML`, reads `--cal-slots`; card and panel behaviour | static + Playwright | `~EventsConversionGuardTests`; scratch `behaviour-check` | ❌ W0 new | ⬜ pending |
| D-04/D-05 | Month fits at 1280x720, 1366x768, 1440x900, 1920x1080; no fit rule below 992px | Playwright | scratch `fit-check` sweep incl. 991x720 | scratch | ⬜ pending |
| D-08 | Weekday header ≥ 4.5:1 on Calendar/Index and Quest Details; exception retired | Playwright contrast probe | scratch `contrast.mjs`; `contrast-exceptions.json` holds no weekday entry | scratch | ⬜ pending |
| D-13 | Agenda twins keep their selection facts (no merge unless approved) | integration | `~AgendaMobileRenderTests` | ✅ | ⬜ pending |
| DoC-1 | No `style=`, `<style>`, inline `<script>` or `on*=` in the 17 area files | static | `~EventsConversionGuardTests` | ❌ W0 new | ⬜ pending |
| DoC-2 | Zero hex / `rgb()` / `!important` in the six area sheets, all in `@layer pages` | static | `~UiFoundationLayerGuardTests` (arrays appended) | ✅ edit | ⬜ pending |
| DoC-3 | Area sheets linked from `@section Styles`, not `_Layout.cshtml`'s global list | static | `~EventsConversionGuardTests`; `~UiFoundationLayerGuardTests` | ❌ W0 + ✅ | ⬜ pending |
| DoC-4 | Twin answers present (phone legend, 20px title, own vote, key warning/finalized, today; desktop empty month) | integration | `~MobileViewsTests`, `~LinkedBoardMobileCalendarTests`, new facts | ✅ + new | ⬜ pending |
| DoC-5/DoC-7 | Accessibility bar per page, desktop and phone under a mobile UA | Playwright sweeps | scratch `sweep.mjs` | scratch | ⬜ pending |
| DoC-8 | Guards rewritten to the same intent, none deleted | static/process | test-count comparison before/after (`dotnet test --list-tests`) | process | ⬜ pending |
| DoC-9 | Helpers' existing behaviour untouched; nothing writes to `Events`/`Quests`; feed files untouched | unit + diff | existing helper tests; feed-revision guard stays green; `git diff --stat` | ✅ | ⬜ pending |
| DoC-10 | Every page still works and reads | full suite + sweep | `dotnet test` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs` — clone of `QuestsConversionGuardTests` over the area's views and sheets (DoC-1, DoC-3, module static checks)
- [ ] `QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs` — Q-133 facts
- [ ] `QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs` — the new format styles
- [ ] `QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs` — rendered text per moved line
- [ ] Scratch tooling copied from `~/qb-ui-review/phase-95/tools/` and re-pointed to phase-97
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 approval of the three drawings (Events/Details reorder; calendar port with real data, Q-116 row and phone agenda additions; truncation trade-off) | D-11, D-12, D-14 | Operator taste gate | Operator reviews the orphan review branch package and answers; redraw until approved |
| Gate 2 before/after sign-off incl. Quest Details re-verified | D-11, DoC-6 | Operator taste gate | Operator reviews the Gate 2 orphan review branch |
| 20px compact entries recorded as a target-size exception | D-03 | Recorded exception, not a pass | Listed in phase verification |
| Real-phone check of the calendar, agenda and events pages | DoC-7 | Real touch device | Operator checklist in UAT |

---

## Validation Sign-Off

- [ ] All tasks have an `<automated>` verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without an automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
