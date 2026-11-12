---
phase: "97"
slug: "events-series-calendar-and-agenda-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
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
| D-10 | Today highlight follows the board clock (pinned 2026-09-05T23:30Z, Europe/Amsterdam: day 6 is today); calendar partials and views in the ambient guard | integration + unit static | `~CalendarBoardClockTests`; `~CalendarMonthMarkupTests.TodayCell_IsMarkedFromTheBoardClock`; `~AmbientClockSeamTests` | ✅ | ✅ green |
| D-09 | Each moved date line prints byte-identical text; detail-block date and quest time go through the helper; new styles pinned; existing helper tests unedited | unit + integration | `~WallClockAreaStylesTests`; `~EventsDateRenderTests`; `~CalendarMonthMarkupTests.DetailBlocks_RenderDateAndTimeThroughTheWallClockHelper`; `~EventsConversionGuardTests.AreaViews_RenderNoRawDateFormatting`; `~WallClockMarkupTests`, `~LocalTimeMarkupTests`, `~WallClockUnmovedTests` unchanged | ✅ | ✅ green |
| D-06 | Static linked entry: no link, no tab stop, no data attribute, no handler; hidden status text stays | static + integration | `~LinkedBoardStaticGuardTests`, `~LinkedBoardCalendarTenantIsolationTests`, `~CalendarMonthMarkupTests.StaticLinkedEntries_StayInert` | ✅ | ✅ green |
| D-07 | Member linked entry is one link to that board's page with the dashed-outline hook and glyph | integration + static | `~LinkedBoardCalendarStyleTests`, `~CalendarMonthMarkupTests.MemberLinkedEntries_AreOneLink` | ✅ | ✅ green |
| D-01/D-02/D-03 | Slot table in CSS; module has no hover-open mode, no `innerHTML`, reads `--cal-slots`, mounts overlays on body; "+N more" button and panel/card shells in markup | static + integration | `~CalendarSlotTableTests`; `~EventsConversionGuardTests` (`CalendarMonthModule_*`); `~CalendarMonthMarkupTests` (`Shells_RenderOnlyOnTheCalendarPage`, `Module_IsLoadedOnlyByTheCalendarPage`) | ✅ | ✅ green (focus/pointer behaviour Manual-Only) |
| D-04/D-05 | No fit rule below 992px; slots 2/3/4/5 by window height from 992px; grid carries its week count | static + integration | `~CalendarSlotTableTests`; `~CalendarMonthMarkupTests.Grid_CarriesItsWeekCount` | ✅ | ✅ green (rendered fit at the sizes Manual-Only) |
| D-08 | Weekday header is the wood band with parchment text on Calendar/Index and Quest Details; Quest Details keeps its vote layout | static + integration | `~EventsConversionGuardTests.CalendarWeekdayHeader_IsAWoodBandWithParchmentText`; `~CalendarMonthMarkupTests.QuestDetails_KeepsItsVoteLayoutAndNoCompactControls` | ✅ | ✅ green (measured 4.5:1 Manual-Only) |
| D-13 | Agenda twins keep their selection facts (no merge) | integration | `~AgendaMobileRenderTests`, `~AgendaConversionTests` | ✅ | ✅ green |
| DoC-1 | No `style=`, `<style>`, inline `<script>` or `on*=` in the area files | static | `~EventsConversionGuardTests` (`ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `PhaseScope_IsFullyConverted`) | ✅ | ✅ green |
| DoC-2 | Zero hex / `rgb()` / named colours / `!important` in the area sheets, each in `@layer pages` | static | `~EventsConversionGuardTests` (`ConvertedSheets_*`); `~UiFoundationLayerGuardTests` | ✅ | ✅ green |
| DoC-3 | Area sheets linked from `@section Styles`, not the layouts | static | `~EventsConversionGuardTests` (`ConvertedViews_LinkTheirSheets`, `Layouts_LinkNoAreaSheet`, `CalendarSheet_LoadsOnlyWhereItIsUsed`); `~UiFoundationLayerGuardTests` | ✅ | ✅ green |
| DoC-4 | Twin answers present (phone legend, title, own vote, key warning/finalized, today; desktop empty month) | integration | `~MobileViewsTests`, `~LinkedBoardMobileCalendarTests`, `~CalendarPhoneAgendaTests`, `~CalendarMonthMarkupTests.EmptyMonth_*` | ✅ | ✅ green |
| DoC-5/DoC-7 | Accessibility bar per page, desktop and phone under a mobile UA | integration (served view) + manual | `~MobileViewsTests` phone selection; `~CalendarMonthMarkupTests.OwnEntries_CarryTheFullAccessibleName` | ✅ | ✅ green (rendered contrast/targets/overflow Manual-Only) |
| DoC-8 | Guards rewritten to the same intent, none deleted | process | test-count comparison (unit 1412, integration 2029 at validation) | process | ✅ green |
| DoC-9 | Helpers' existing behaviour untouched; nothing writes to `Events`/`Quests`; feed files untouched | unit + diff | existing helper tests; feed-revision guard; `git diff --stat` | ✅ | ✅ green |
| DoC-10 | Every page still works | full suite | `dotnet test QuestBoard.UnitTests`; `dotnet test QuestBoard.IntegrationTests` | ✅ | ✅ green |
| Review WR-01/WR-02 | "Also that day" link stays inline; no named colour literals in area sheets | static | `~LinkedBoardCalendarStyleTests`; `~EventsConversionGuardTests.ConvertedSheets_HaveNoColourLiteral`; `~UiFoundationLayerGuardTests` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs` — clone of `QuestsConversionGuardTests` over the area's views and sheets (DoC-1, DoC-3, module static checks)
- [x] `QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs` — Q-133 facts
- [x] `QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs` — the new format styles
- [x] `QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs` — rendered text per moved line
- [x] Scratch tooling copied from `~/qb-ui-review/phase-95/tools/` and re-pointed to phase-97
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 approval of the three drawings | D-11, D-12, D-14 | Operator taste gate (approved) | Recorded in 97-VERIFICATION |
| Gate 2 before/after sign-off incl. Quest Details re-verified | D-11, DoC-6 | Operator taste gate (approved) | `~/qb-ui-review/phase-97/gate2/` |
| 20px compact entries, dashed outline, focus-ring offset recorded as exceptions | D-03 | Recorded exception, not a pass | 97-VERIFICATION "Recorded exceptions" |
| "+N more" day panel focus/Tab loop/close paths and detail card hover, persist, Esc, tap-tap | D-01, D-02 | Needs a browser running JS; no JS test runner in the repo | `97-UAT.md` item 4 |
| The four modules that replaced inline scripts (event-details, event-series-form, agenda, row-link) behave as before | DoC-1, DoC-10 | Needs a browser running JS; repo facts pin hooks only | `97-UAT.md` item 5 |
| Rendered fit at 1280x720 to 1920x1080, contrast 4.5:1, 44px targets, no sideways scroll | D-04, D-05, D-08, DoC-5 | Rendered measurement from the scratch harness | `~/qb-ui-review/phase-97/after/`; `97-UAT.md` items 1 and 4 |
| Real-phone check of the calendar, agenda, events and series pages | DoC-7 | Real touch device | `97-UAT.md` item 1 |
| Screen-reader pass of an entry, "+N more", the panel and an inert linked entry | DoC-5, D-06 | Needs assistive technology | `97-UAT.md` item 2 |
| Moved date lines read as before on production | D-09 | Production host culture never inspected | `97-UAT.md` item 3 |

----------|-------------|------------|-------------------|
| Gate 1 approval of the three drawings (Events/Details reorder; calendar port with real data, Q-116 row and phone agenda additions; truncation trade-off) | D-11, D-12, D-14 | Operator taste gate | Operator reviews the orphan review branch package and answers; redraw until approved |
| Gate 2 before/after sign-off incl. Quest Details re-verified | D-11, DoC-6 | Operator taste gate | Operator reviews the Gate 2 orphan review branch |
| 20px compact entries recorded as a target-size exception | D-03 | Recorded exception, not a pass | Listed in phase verification |
| Real-phone check of the calendar, agenda and events pages | DoC-7 | Real touch device | Operator checklist in UAT |

---

## Validation Sign-Off

- [x] All tasks have an `<automated>` verify or a Wave 0 dependency
- [x] Sampling continuity: no 3 consecutive tasks without an automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-11-13

---

## Validation Audit 2026-11-13

| Metric | Count |
|--------|-------|
| Gaps found | 1 |
| Resolved | 1 |
| Escalated | 0 |

The desktop month's slot table (zero below 992px, 2/3/4/5 by window height) had no repository guard; `CalendarSlotTableTests` now pins it. Suite at validation: unit 1412, integration 2029, 0 failed (migrator tests run against SQL Server).

