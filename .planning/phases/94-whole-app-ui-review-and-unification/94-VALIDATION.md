---
phase: 94
slug: whole-app-ui-review-and-unification
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-30
---

# Phase 94 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2, FluentAssertions 8.10.0, Microsoft.AspNetCore.Mvc.Testing 10.0.9, EF Core InMemory 10.0.9 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (assembly and collection parallelisation off) |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileCssTests\|FullyQualifiedName~MobileLayoutTests\|FullyQualifiedName~PlatformAreaLayoutTests\|FullyQualifiedName~ContactCategoryContrastGuardTests\|FullyQualifiedName~CalendarSubscriptionStaticGuardTests\|FullyQualifiedName~UiFoundation"` (every new guard class is named `UiFoundation*`) |
| **Full suite command** | `dotnet test` (InMemory, no database needed) |
| **Browser checks** | scratch Playwright wrapper outside the repository (screenshots, console, pixel diff); results summarised in each plan SUMMARY. Ports: 8000 is the working-tree dev app the operator starts, 8001 the frozen baseline (`run-baseline.sh`), and each code plan 94-NN runs its own build on 80NN; `run-app.sh` exits 1 rather than reuse a port served by a different source tree, and role storage states are kept per port |
| **Estimated runtime** | quick filter ~30 s after build; full suite a few minutes |

---

## Sampling Rate

- **After every task commit:** Run the quick filter above
- **After every plan wave:** Run `dotnet test` (full suite), plus the scratch Playwright sweep for any wave that touched CSS or layouts
- **Before `/gsd-verify-work`:** Full suite green, D-08 sweep reviewed, contrast report on the final `tokens.css`, console check on the five layouts under both User-Agents
- **Max feedback latency:** ~60 seconds for the quick filter
- **Wave isolation:** no two plans that edit tracked source, build or run the working tree, or commit code share a wave. Waves 6 to 16 hold one plan each; in waves 3, 4 and 5 the only such plan is 94-11, 94-12 and 94-13 respectively, and the plans beside it write only scratch output or planning docs and probe the frozen baseline on 8001 (94-14's cascade probe builds a `git archive` copy outside the repository on 8014)
- **Linux note:** never set GC env vars; re-run once on CLR error `0x80131506`

---

## Per-Task Verification Map

Filled by the planner from the must-have → test map below; each plan task cites its row.

| Must-have (source) | Behavior | Test Type | Automated Command | File Exists | Status |
|---|---|---|---|---|---|
| D-09 no CDN on layouts | five layouts contain no CDN host, no `integrity=`, no `style="`, no bare `<script>` | static guard | `dotnet test --filter "FullyQualifiedName~UiFoundation"` | created by 94-11, 94-13, 94-21, 94-22 | ⬜ pending |
| D-09 vendor files ship | every `/lib/` URL exists on disk; release zip contains them; packaged app serves them 200 | static guard + shell | same filter; `bash build/tests/package-release-smoke-test.sh` (run with no argument, so it packages the current tree itself); scratch `dotnet publish` of QuestBoard.Service contains `wwwroot/lib/{bootstrap-5.3.0,fontawesome-6.4.0,cinzel}` (what the Dockerfile copies) | guard ❌ W0; smoke test extend | ⬜ pending |
| D-10 jQuery and v4 shim removal | no `jquery` or `v4-shims` in any layout; zero console errors on five layouts × two UAs | static guard + browser | filter above; Playwright wrapper | guard ❌ W0; browser scratch | ⬜ pending |
| D-16 / A-1 contrast | every declared token pair ≥ 4.5 (3.0 large text and non-text) composited over `B-max` | static guard (C# resolver) | filter above | ❌ W0 | ⬜ pending |
| A-2 focus ring / A-7 reduced motion | `base.css` declares the ring for the selector list and a `prefers-reduced-motion` block; no bare `outline: none/0` | static guard | filter above | ❌ W0 | ⬜ pending |
| A-5 44px mobile targets | `.mobile-layout :where(...)` rule uses `--qb-hit-min: 44px` | static guard (rewrite) | `--filter "FullyQualifiedName~MobileCssTests"` | ✅ rewrite | ⬜ pending |
| A-8 landmarks, skip link, names | each layout: skip link first, `<main id="main-content" tabindex="-1">`, toggler `aria-label` | static guard | filter above | ❌ W0 | ⬜ pending |
| A-9 toast persistence | Error and Warning toasts carry `data-bs-autohide="false"`; Success and Info do not; Moved toast markup intact | static guard + existing test | `--filter "FullyQualifiedName~BoardMoveControlTests\|FullyQualifiedName~UiFoundationToastGuardTests"` | ✅ + ❌ W0 | ⬜ pending |
| Cascade rules | layered files: no `!important` (except allowed), no colour literal outside `tokens.css` | static guard | filter above | ❌ W0 | ⬜ pending |
| D-08 pages usable | every page, both UAs: text readable, no control hidden, suite green | browser sweep + full suite | Playwright BEFORE/AFTER + pixel diff + human review | scratch | ⬜ pending |
| A1 neutral changes | zero visible difference, five mockup pages × five layouts | browser diff | Playwright capture (`gate.mjs`) + `gate-compare.py` (Pillow + numpy, tolerance 24, masks; pixelmatch is not used) | scratch | ⬜ pending |
| D-12 no inline code in rebuilt layouts | covered by the layout guard | static guard | filter above | ❌ W0 | ⬜ pending |
| D-13 / D-15 mobile selection intact | `.Mobile` view and layout under a mobile UA | existing integration | `--filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileLayoutTests"` | ✅ | ⬜ pending |
| No planning references in new files | regex scan of new foundation files | static guard | filter above | ❌ W0 | ⬜ pending |
| Build and test health | no regression | full suite | `dotnet test` | ✅ | ⬜ pending |

### Plan task map (filled by the planner)

New guard classes are created by the plan that introduces the rule they pin (no separate Wave 0 plan): `UiFoundationVendorGuardTests` (94-11, extended 94-13, 94-19), `UiFoundationViewLibraryGuardTests` (94-12), `UiFoundationLayerGuardTests`, the `WcagContrast` helper and `UiFoundationWcagContrastTests` (94-18, extended 94-21, 94-23, 94-25), `UiFoundationBaseGuardTests` (94-19), `UiFoundationComponentGuardTests`, `UiFoundationToastGuardTests` (94-20), `UiFoundationDesktopShellGuardTests` (94-21), `UiFoundationMobileShellGuardTests` (94-22). 94-PATTERNS.md maps a single `UiFoundationStaticGuardTests.cs`; the plans deliberately split it into one class per rule family so each plan owns its own test file, and the same analog (repo-root finder from `BoardMoveStaticGuardTests.cs` lines 14-30, fragment-built scan lists, `ExtractCssRule`) applies to each. Every new test class name starts with `UiFoundation`, so the quick filter's `FullyQualifiedName~UiFoundation` term runs all of them; `WcagContrast` is a helper, not a test class. Scratch tools (`audit-shoot.mjs`, `sweep.mjs`, `probes.mjs`, `gate.mjs`, `gate-compare.py`, `readability-compare.py`, `contrast.mjs`, `diff.py`, `run-app.sh`, `run-baseline.sh`) are built in 94-01 and 94-02 under `~/qb-ui-review/phase-94/tools/`; mockup tools (`render.sh`, `contrast-report.mjs`) in 94-09 under `~/qb-ui-review/phase-94/mockups/tools/`.

| Task | Wave | Must-have row | Type | Automated command |
|---|---|---|---|---|
| 94-01 T1 | 1 | environment and package legitimacy (D-23 as amended) | checkpoint (blocking-human) | post-resume port 1433, `/health` on 8000, credentials mode 600, appsettings untouched |
| 94-01 T2-T3 | 1 | D-15 mobile selection, D-23 roles | browser | `node audit-shoot.mjs ...` mobileLayout flag; roles-check.json |
| 94-02 T1-T3 | 2 | baseline for D-08 / A1, D-25 fixtures | browser + scripts | manifest summary 57/51/6; baseline on 8001 idempotent and wrong-source refusal; `quest.voteFixture` created; before/report.json coverage; gate self-test PASS |
| 94-03..08 T1-T2 | 3 | audit (D-13, D-14, D-04) | doc checks | grep of sections, index rows, finding IDs, no mail domains |
| 94-09 T1-T2 | 3 | D-16/A-1 mockups, D-02 baselines | scripts | `contrast-report.mjs` passBmax; render outputs for all five baselines |
| 94-10 T1-T3 | 4 | D-16/A-1 mockups, D-03 Shop both ways | scripts | `contrast-report.mjs` passBmax across five pages + shop-a/b; WRITEUP headings |
| 94-11 T1-T2 | 3 | D-09 no CDN, A1 neutral | static guard + browser | `--filter UiFoundation...` + `gate-compare.py c1-before c1-after` |
| 94-11 T3 | 3 | D-09 vendor files ship | shell | smoke test exit 0 and no FAIL line in its log; scratch `dotnet publish` contains the three vendor directories; `.dockerignore` excludes none of them |
| 94-12 T1-T2 | 4 | D-20, A1 neutral | static guard + browser | `--filter UiFoundationViewLibraryGuardTests|QuestControllerIntegrationTests_Comprehensive|QuestFinalizedEditTests|QuestCampaignUiParityTests|MobileViewsTests|ContactsTagsFormMarkupTests|...` + `gate-compare.py d20-before d20-after` + smoke.mjs |
| 94-13 T1-T3 | 5 | D-10 jQuery and shim removal; layer statement; D-25 vote on fixture only | static guard + browser | `--filter UiFoundation` + `gate-compare.py c2-before c2-after` + flows.mjs + glyphs.py + smoke test exit 0 and no FAIL line |
| 94-14 T1-T3 | 4 | audit index, cascade probe, cut, D-04 relayout AFTER | doc checks + sweep | grep checks chained with `&&`; cascade-probe report exists; probe imports only in the scratch copy; every relayout row has an AFTER (mocked-page directory note or not-yet-drawn line, independent of mockup timing) |
| 94-15 T1-T2 | 4 | D-16/A-1 mockups, D-03, D-01 differs in kind | scripts | `contrast-report.mjs` passBmax in both action-fill states; Direction C section |
| 94-16 T1-T2 | 5 | D-01 switchers, D-16 combined contrast | scripts | five `data-switch` values; contrast-all passBmax with B-hi; SIGNOFF.md S12 and prompt |
| 94-17 T1-T3 | 6 | sign-off (D-01, D-03, D-04) | checkpoint + script | README path check incl. relayout AFTER; no pending S rows; contrast-final passBmax; every approved relayout reads "approved in principle — re-confirmed with its AFTER in the area phase" |
| 94-18 T1 | 7 | cascade rules, link order, D-08 | static guard + browser | `--filter UiFoundation|MobileCssTests|...` + `readability-compare.py t-before t-after` |
| 94-18 T2 | 7 | D-16 / A-1 contrast | static guard | `--filter UiFoundationWcagContrastTests|UiFoundationLayerGuardTests` |
| 94-19 T1-T3 | 8 | D-11 bump, layering, A-2 ring, A-7 motion | static guard + browser | `--filter UiFoundation` + `readability-compare.py` per commit + smoke test |
| 94-20 T1-T2 | 9 | components, A-1 | static guard + gallery probe | `--filter UiFoundation` + gallery contrastFailures empty |
| 94-20 T3 | 9 | A-9 toast persistence (D-24) | static guard + existing + browser | `--filter UiFoundationToastGuardTests|BoardMoveControlTests|CrossBoardPickerSkipTests` + toasts.mjs (created in that task) + no stylesheet name in the toast comment |
| 94-21 T1-T3 | 10 | A-8 landmarks, D-12, A-4 | static guard + browser | `--filter UiFoundation|MobileLayoutTests|BoardPageNavigationTests|LayoutNavigationTests|...` + readability-compare + shell-checks.mjs (created in T2) |
| 94-22 T1-T3 | 11 | A-5 44px, A-8, D-12 | static guard (rewrite MobileCssTests) + browser | `--filter UiFoundation|MobileCssTests|MobileLayoutTests|MobileViewsTests` + readability-compare + platform-mobile.mjs (created in T2) + edge.mjs |
| 94-23 T1-T3 | 12 | D-22 rename, D-08 | static guard rewrites + browser | `--filter MobileCssTests|CalendarSubscriptionStaticGuardTests|UiFoundation` + readability-compare per commit |
| 94-24 T1-T2 | 13 | cream rule retired, D-08 | static guard rewrites + browser | `--filter PlatformAreaLayoutTests|ContactCategoryContrastGuardTests|LinkedBoardStaticGuardTests|UiFoundation` + readability-compare + no `modern-card.css` mention in Views, Areas or wwwroot/css |
| 94-24 T3 | 13 | D-08 full sweep | browser + human | `readability-compare.py before final` + operator review |
| 94-25 T1-T2 | 14 | guidelines (no planning refs) | script + static guard | exact-line heading check (`grep -qx`) + `check-classes.py` + `--filter UiFoundationLayerGuardTests` |
| 94-26 T1-T3 | 15 | final a11y/console/overflow, D-05 scope | browser + git | `shell-final.mjs` + `diff` of the sorted `git diff --no-renames --name-only <baseline>..HEAD` over Views and Areas against the explicit 19-path allowed list (fails on extra or missing) + empty diff for emails, Domain, Repository, date helpers, site.js |
| 94-27 T1-T3 | 16 | D-06 cut, D-18 email phase, D-07 caveat | script + checkpoint | `coverage.py` + cut.md names `docker-publish.yml` + ROADMAP phase heading and per-phase caveat check |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] The `UiFoundation*` guard classes listed in the plan task map — layout hygiene, layered-file rules, token contrast, focus ring, reduced motion, toast attributes, `/lib` existence, planning-reference scan (each created with the rule it pins)
- [ ] WCAG contrast helper (private static in that file) with known-value tests (4.34, 2.99, 3.36, 1.97, 4.94, 7.10, 4.53)
- [ ] `build/tests/package-release-smoke-test.sh` additions (vendor zip entries derived from `git ls-files QuestBoard.Service/wwwroot/lib`, curl 200s for every layout-referenced `/lib` URL)
- [ ] Scratch tooling outside the repository (`audit-shoot.mjs`, manifest generator, `contrast.mjs`, diff script), recreated from `94-RESEARCH.md`
- [ ] Framework install: none (xUnit and FluentAssertions already referenced)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Direction sign-off (S1–S12) | D-01, D-02, D-03 | operator taste and approval | operator reviews the mockup package (desktop and phone widths, baseline included) and records each S value |
| Page relayout approval | D-04 | operator approval per page | before/after screenshots per re-laid-out page, signed off one by one |
| Twin content differences | D-14 | each non-deliberate difference needs an operator answer | audit question list answered by the operator |
| Area-phase cut | D-06 | operator approves the proposed split | operator approves before `/gsd-phase` appends |
| D-08 readability sweep | D-08 | rendered-page judgement beyond pixel diffs | human review of the flagged before/after pairs |
| Real-phone check (optional) | D-15 | browser emulation with a mobile UA is acceptable; a phone is optional | LAN dev build or batched production run after deployment |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
