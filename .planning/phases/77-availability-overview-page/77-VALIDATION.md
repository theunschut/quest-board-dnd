---
phase: 77
slug: availability-overview-page
status: verified
nyquist_compliant: true
wave_0_complete: true
created: 2026-08-29
---

# Phase 77 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `77-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions 8.10.0 (already configured) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~15s quick / full suite per existing project baseline |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

> Filled by the planner once PLAN.md task IDs exist. Requirement → test-type
> mapping below is fixed by RESEARCH.md and must be preserved.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-T1 | 77-01 | 1 | EVTVIEW-01/02/03 | T-77-02 | Domain vocabulary + `MaxTake` ceiling exist | build | `dotnet build QuestBoard.Domain/QuestBoard.Domain.csproj` | n/a | ✅ green |
| 01-T2 | 77-01 | 1 | EVTVIEW-01 | T-77-01 | Bounded aggregate read rides the ambient query filters; no bypass, no manual group predicate | build + static gate | `dotnet build QuestBoard.Repository/QuestBoard.Repository.csproj` + `awk '/GetUpcomingWithSignupsAsync/,/^    }/' QuestBoard.Repository/EventRepository.cs | grep -c 'IgnoreQueryFilters'` == 0 (method-scoped — the file is now shared with a separate, membership-pinned cross-board read) | n/a | ✅ green |
| 01-T3 | 77-01 | 1 | EVTVIEW-02, EVTVIEW-03 | — | Five cell states + three counts derived in memory from the answered marker alone | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewAggregationTests"` | ❌ created by 01-T3 | ✅ green |
| 02-T1 | 77-02 | 1 | EVTVIEW-02 | — | Unconfirmed-default chip carries a non-colour signal (dashed border) | static gate | `grep -q 'dashed' QuestBoard.Service/wwwroot/css/events-overview.css` | ❌ created by 02-T1 | ✅ green |
| 02-T2 | 77-02 | 1 | EVTVIEW-01 | T-77-05 | Nav entries sit under the unchanged board-type gate with no role condition | build | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` | n/a | ✅ green |
| 02-T3 | 77-02 | 1 | EVTVIEW-01 | T-77-05 | Overview nav entry present for DM and player on both board types and both user agents, absent for anonymous | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ extends existing file | ✅ green |
| 03-T1 | 77-03 | 2 | EVTVIEW-02, EVTVIEW-03 | — | Counts and cell order survive the domain-to-view-model mapping | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewViewModelMappingTests"` | ❌ created by 03-T1 | ✅ green |
| 03-T2 | 77-03 | 2 | EVTVIEW-01, EVTVIEW-02, EVTVIEW-03 | T-77-09 | Five distinct chips, three-figure count block, no write path, Razor-encoded output | build | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` | n/a | ✅ green |
| 03-T3 | 77-03 | 2 | EVTVIEW-01, EVTVIEW-02, EVTVIEW-03 | T-77-02, T-77-03, T-77-08 | `take` clamped server-side; all-members access; no 500 for SuperAdmin with no active group | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` | ❌ created by 03-T3 | ✅ green |
| 04-T1 | 77-04 | 3 | EVTVIEW-04 | T-77-01, T-77-10, T-77-11 | Never shows another board's events, members or counts, including under a widened `take` and with no active board | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewTenantIsolationTests"` | ❌ created by 04-T1 | ✅ green |
| 04-T2 | 77-04 | 3 | EVTVIEW-04 | T-77-12 | No phase-77 production file bypasses the query filters; whole suite green | full suite + static gate | `dotnet test` | n/a | ✅ green |

*Status: ✅ green · ✅ green · ❌ red · ⚠️ flaky*

### Gap closure tasks (plans 77-05..77-10)

> Added after verification returned `gaps_found` and the code review returned 1 critical /
> 9 warnings / 8 info. Gap waves are numbered independently of the original 1–3 waves above.

| Task ID | Plan | Gap wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|----------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-T1 | 77-05 | 1 | EVTVIEW-01 | T-77-02 | Paging is offered only when the next window is genuinely larger, so a clamped window never links to itself | build + static gate | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` + `grep -c 'Model.CanShowMore' QuestBoard.Service/Views/Events/Index.cshtml` == 1 | ✅ existing files | ✅ green |
| 05-T2 | 77-05 | 1 | EVTVIEW-01 | T-77-02, T-77-09 | The mobile surface can reach every upcoming event; the expanded roster is inert to click-through | build + static gate | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` + `grep -c 'event.stopPropagation' QuestBoard.Service/Views/Events/Index.Mobile.cshtml` == 2 | ✅ existing files | ✅ green |
| 06-T1 | 77-06 | 1 | EVTVIEW-01 | — | The viewer's own column is visually attributable rather than silently overpainted | static gate | `grep -c 'modern-card .table td\.avail-col-self' QuestBoard.Service/wwwroot/css/events-overview.css` == 1 | ✅ existing file | ✅ green |
| 06-T2 | 77-06 | 1 | EVTVIEW-01 | T-77-13 | Frozen columns cannot overlap and misattribute a chip to the wrong member | static gate | `grep -c 'left: 200px' QuestBoard.Service/wwwroot/css/events-overview.css` == 0 | ✅ existing file | ✅ green |
| 07-T1 | 77-07 | 1 | EVTVIEW-01 | T-77-14 | The upcoming-window boundary is read from an injected UTC clock and is pinnable to an exact instant | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewAggregationTests"` | ✅ extends existing file | ✅ green |
| 07-T2 | 77-07 | 1 | EVTVIEW-01 | T-77-02 | An out-of-range page-size ceiling fails at application start; the clamp itself cannot throw | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewOptionsValidationTests"` | ❌ created by 07-T2 | ✅ green |
| 08-T1 | 77-08 | 1 | EVTVIEW-01 | — | Test documentation stays true independent of phase state; no tracking ids in source | static gate | `grep -c 'NAV-0' QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` == 0 | ✅ existing file | ✅ green |
| 08-T2 | 77-08 | 1 | EVTVIEW-01 | T-77-05, T-77-15 | Navigation gating evidence is not order-dependent | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ existing file | ✅ green |
| 09-T1 | 77-09 | 2 | EVTVIEW-01, EVTVIEW-02 | T-77-16 | The user-agent-selected mobile surface is actually rendered by tests | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` | ✅ extends existing file | ✅ green |
| 09-T2 | 77-09 | 2 | EVTVIEW-01, EVTVIEW-03 | T-77-02 | The page-size clamp and the three per-event figures are covered by assertions that fail when the control is removed | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverview"` | ✅ existing file | ✅ green |
| 09-T3 | 77-09 | 2 | EVTVIEW-04 | T-77-01 | The requirements ledger matches the verified tenant-isolation evidence | static gate | `grep -Ec '\| EVTVIEW-0[1-4] \| Phase 77 \| Complete \|' .planning/REQUIREMENTS.md` == 4 | ✅ existing file | ✅ green |
| 10-T1 | 77-10 | 3 | EVTVIEW-01 | T-77-17 | Ownership-conditional destinations keep their conditional on the keyboard path | build + static gate | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` + `grep -c 'currentUserId.Value == quest.DungeonMaster?.Id' QuestBoard.Service/Views/Quest/Index.cshtml` == 2 | ✅ existing files | ✅ green |
| 10-T2 | 77-10 | 3 | EVTVIEW-01 | T-77-18 | Mobile rows expose a focusable link without disturbing the paging control or the roster guard | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverview"` | ✅ existing files | ✅ green |
| 10-T3 | 77-10 | 3 | EVTVIEW-01 | T-77-17 | A focusable link to the same destination exists on a representative desktop and mobile surface | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~RowNavigationAccessibilityTests"` | ❌ created by 10-T3 | ✅ green |

*Status: ✅ green · ✅ green · ❌ red · ⚠️ flaky*

### Gap closure tasks (plans 77-11..77-12)

> Added after `/gsd-verify-work` returned a critical styling gap on a real mobile device while
> every automated assertion in the phase was green. This wave is numbered independently of the
> gap wave above.

| Task ID | Plan | Gap wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|----------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 11-T1 | 77-11 | 1 | EVTVIEW-01 | T-77-19 | The mobile card is the shared glass surface rather than a bespoke opaque slab | static gate | the grep-based surface gate from 77-11 Task 1 | ✅ existing file | ✅ green |
| 11-T2 | 77-11 | 1 | EVTVIEW-03 | T-77-20 | Every text run in the card states its own colour and clears the 4.5:1 floor | build + static gate | the build plus colour-rule gate from 77-11 Task 2 | ✅ existing files | ✅ green |
| 11-T3 | 77-11 | 1 | EVTVIEW-01 | T-77-20 | Controls are filled, and the tap target and click-through guards are unchanged | build + static gate | the button gate from 77-11 Task 3 | ✅ existing file | ✅ green |
| 12-T1 | 77-12 | 2 | EVTVIEW-01, EVTVIEW-03 | T-77-22 | The styling contract is sampled by tests rather than by eye | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewMobileStyleTests"` | ❌ created by 12-T1 | ✅ green |
| 12-T2 | 77-12 | 2 | EVTVIEW-01, EVTVIEW-03 | T-77-23 | The whole suite still passes with the new surface and the new facts | full suite | `dotnet test` | ✅ existing files | ✅ green |

*Status: ✅ green · ✅ green · ❌ red · ⚠️ flaky*

**Why this was missed:** two mechanisms let the defect ship. First, the UI safety gate's block
condition fires only when a UI-SPEC is *absent* — it reported `hasUiFiles: false` on every run of
this phase, including the waves that added two stylesheets and four Razor views, so it never
compared the implementation against a UI-SPEC that in fact existed. Second, the only
style-conformance tests in the repository before this wave (`CalendarButtonStyleTests`,
`PlatformAreaLayoutTests`) covered the calendar and the Platform area only — nothing sampled the
mobile availability overview's own surface, colours or button convention, so a visual regression
there had no automated path to fail.

---

## Wave 0 Requirements

> **Compiled-language note (planner):** this solution will not build if a test references a
> method that does not exist yet, so a literal wave-0-only test pass would red the whole
> repository and block every other plan. Each test file below is therefore created inside the
> plan that ships the seam it exercises, written before the implementation within that task
> (see the `<behavior>` blocks in 77-01 T3 and 77-03 T1). Every task still carries a real
> `<automated>` command and no three consecutive tasks lack one.

- [x] `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` — covers EVTVIEW-04, modeled on `EventAvailabilityTenantIsolationTests.cs` (two-group isolation, D-27 mandatory)
- [x] `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` (created) — covers EVTVIEW-01/02/03 happy-path + SuperAdmin-no-active-group + empty-state
- [x] `QuestBoard.UnitTests/...` (new file for the aggregation/mapping logic) — covers EVTVIEW-02/03 in isolation from HTTP, using constructed `EventSignup` lists across all 5 cell states and the Yes/confirmed/maybe count math
- [x] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend existing file) — new cases for the "Availability Overview" nav entry on both layouts; existing 4 Calendar-string cases must stay green (D-22)
- [x] No framework install needed — xUnit v3 + FluentAssertions already configured project-wide

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Mobile layout of the overview under a real mobile user agent | EVTVIEW-01 | Mobile views are UA-selected; devtools emulation does not exercise them | Load the overview page with a real mobile UA (or a real device) and confirm the mobile layout locked in `77-UI-SPEC.md` renders |
| Visual distinctness of muted-default vs. confirmed cell to the eye | EVTVIEW-02 | Automated tests assert the CSS class; perceived contrast is a human judgement | Open the page with a mix of answered and untouched signups; confirm the two states read as different at a glance |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (each test file is created in the plan that ships its seam — see note above)
- [x] No watch-mode flags
- [x] Feedback latency < 60s (filtered runs; full suite reserved for wave merges)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planner-signed 2026-08-29, mapped to 77-01..77-04 task ids

---

## Validation Audit 2026-08-29

Retroactive Nyquist audit of the completed phase (all 10 plans, original + gap closure).

| Metric | Count |
|--------|-------|
| Gaps found | 4 |
| Resolved | 4 |
| Escalated | 0 |

**Requirement coverage:** EVTVIEW-01 through EVTVIEW-04 all COVERED. 87 tests across 7 classes
directly exercise this phase, every filter verified to resolve and pass:
`EventsOverviewAggregationTests` (12), `EventsOverviewViewModelMappingTests` (3),
`EventsOverviewOptionsValidationTests` (8), `LayoutNavigationTests` (40),
`EventsOverviewControllerIntegrationTests` (17), `EventsOverviewTenantIsolationTests` (5),
`RowNavigationAccessibilityTests` (6, expanded from 2 by this audit).

### Gap 1 — broken gate (fixed in place, no test needed)

Task 01-T2's filter-bypass gate was written as a **file-level** grep over
`QuestBoard.Repository/EventRepository.cs`. That file is no longer owned by this phase alone:
a separate, membership-pinned cross-board read was later added to it, which deliberately steps
outside the ambient filter and immediately re-imposes scope from a caller-supplied group list.
The file-level count is therefore no longer 0 and the gate failed for a reason unrelated to this
phase's guarantee.

The gate is now **method-scoped** to `GetUpcomingWithSignupsAsync`, which still contains zero
bypasses. This phase's actual guarantee — its own aggregate read rides the ambient fail-closed
filters — is unchanged and still proven end-to-end by `EventsOverviewTenantIsolationTests` (5/5).
The corresponding evidence line in `77-SECURITY.md` was corrected to match.

### Gaps 2-4 — missing regression guards (tests added)

| Gap | What was unguarded | Test added | Breaking mutation |
|-----|--------------------|-----------|-------------------|
| Ownership-conditional quest navigation | The two quest-index anchors choose Manage vs Details by ownership. The only protection was a static occurrence count, which an edit changing both sites identically would still satisfy | `RowNavigationAccessibilityTests` - `Desktop_QuestCard_QuestOwnerSeesManageLink`, `Desktop_QuestCard_NonOwnerSeesDetailsLink`, and the two `Mobile_QuestCard_*` counterparts | Dropping or inverting the ownership conditional in either view surfaces a manage-surface link to a non-owner and fails the non-owner facts |
| Platform area layout stylesheet link | Six Platform views use the shared card classes, but the area layout's stylesheet link went missing after an earlier refactor and every one of those pages rendered unstyled. Found in live use, by no test | `PlatformAreaLayoutTests.PlatformGroupIndex_RendersModernCardCssLink` | Removing the stylesheet link from the Platform area layout fails the fact |
| Filled-vs-outline button convention on the calendar cross-links | Project UI guidelines require filled buttons, not outline. An outline button shipped and was only caught by eye | `CalendarButtonStyleTests` - `DesktopCalendar_AvailabilityOverviewLink_UsesFilled_NotOutline` and the mobile counterpart | Reverting either cross-link to an outline button class fails the corresponding fact |

**Suite after audit:** 420 unit + 610 integration = 1030 tests, 0 failures.

**Manual-only items unchanged:** real-device mobile behaviour, and perceived visual distinctness
of the unconfirmed-default chip. Both are tracked in `77-UAT.md` for `/gsd-verify-work`.

## Validation Audit — styling gap closure

`/gsd-verify-work` returned a critical styling gap on a real mobile device: the card surface was
an opaque slab instead of the app's shared glass treatment, the count block measured 1.34:1
against the WCAG AA 4.5:1 floor, and the roster toggle used an outline button, all while every
prior automated gate — including the retroactive Nyquist audit above — reported green. Plan 77-11
fixed the three defects; plan 77-12 (this wave) adds `EventsOverviewMobileStyleTests`, a five-fact
class that samples the styling contract directly instead of relying on a human looking at a
device, and records why the two prior mechanisms (the UI safety gate's absent-only trigger, and a
style-conformance suite that stopped at the calendar and Platform area) both missed it.

**Suite after this wave:** 422 unit + 617 integration = 1039 tests, 0 failures — the 422 unit and
612 integration recorded going into this wave (per `77-11-SUMMARY.md`), plus the 5 new facts in
`EventsOverviewMobileStyleTests`.

**Manual-only items unchanged:** the two rows in the "Manual-Only Verifications" table above
remain manual. Real-device mobile behaviour and perceived visual distinctness of the
unconfirmed-default chip are not, and are not intended to be, replaced by an automated fact —
`EventsOverviewMobileStyleTests` samples the styling contract's *implementation* (the CSS
declarations and the classes the view emits), not a human's perception of the rendered result.
