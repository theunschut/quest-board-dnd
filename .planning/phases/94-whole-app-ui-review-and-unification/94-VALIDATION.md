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
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileCssTests\|FullyQualifiedName~MobileLayoutTests\|FullyQualifiedName~PlatformAreaLayoutTests\|FullyQualifiedName~ContactCategoryContrastGuardTests\|FullyQualifiedName~CalendarSubscriptionStaticGuardTests\|FullyQualifiedName~UiFoundationStaticGuardTests"` |
| **Full suite command** | `dotnet test` (InMemory, no database needed) |
| **Browser checks** | scratch Playwright wrapper outside the repository (screenshots, console, pixel diff); results summarised in each plan SUMMARY |
| **Estimated runtime** | quick filter ~30 s after build; full suite a few minutes |

---

## Sampling Rate

- **After every task commit:** Run the quick filter above
- **After every plan wave:** Run `dotnet test` (full suite), plus the scratch Playwright sweep for any wave that touched CSS or layouts
- **Before `/gsd-verify-work`:** Full suite green, D-08 sweep reviewed, contrast report on the final `tokens.css`, console check on the five layouts under both User-Agents
- **Max feedback latency:** ~60 seconds for the quick filter
- **Linux note:** never set GC env vars; re-run once on CLR error `0x80131506`

---

## Per-Task Verification Map

Filled by the planner from the must-have → test map below; each plan task cites its row.

| Must-have (source) | Behavior | Test Type | Automated Command | File Exists | Status |
|---|---|---|---|---|---|
| D-09 no CDN on layouts | five layouts contain no CDN host, no `integrity=`, no `style="`, no bare `<script>` | static guard | `dotnet test --filter "FullyQualifiedName~UiFoundationStaticGuardTests"` | ❌ W0 | ⬜ pending |
| D-09 vendor files ship | every `/lib/` URL exists on disk; release zip contains them; packaged app serves them 200 | static guard + shell | same filter; `bash build/tests/package-release-smoke-test.sh <zip>` | guard ❌ W0; smoke test extend | ⬜ pending |
| D-10 jQuery and v4 shim removal | no `jquery` or `v4-shims` in any layout; zero console errors on five layouts × two UAs | static guard + browser | filter above; Playwright wrapper | guard ❌ W0; browser scratch | ⬜ pending |
| D-16 / A-1 contrast | every declared token pair ≥ 4.5 (3.0 large text and non-text) composited over `B-max` | static guard (C# resolver) | filter above | ❌ W0 | ⬜ pending |
| A-2 focus ring / A-7 reduced motion | `base.css` declares the ring for the selector list and a `prefers-reduced-motion` block; no bare `outline: none/0` | static guard | filter above | ❌ W0 | ⬜ pending |
| A-5 44px mobile targets | `.mobile-layout :where(...)` rule uses `--qb-hit-min: 44px` | static guard (rewrite) | `--filter "FullyQualifiedName~MobileCssTests"` | ✅ rewrite | ⬜ pending |
| A-8 landmarks, skip link, names | each layout: skip link first, `<main id="main-content" tabindex="-1">`, toggler `aria-label` | static guard | filter above | ❌ W0 | ⬜ pending |
| A-9 toast persistence | Error and Warning toasts carry `data-bs-autohide="false"`; Success and Info do not; Moved toast markup intact | static guard + existing test | `--filter "FullyQualifiedName~BoardMoveControlTests\|FullyQualifiedName~UiFoundationStaticGuardTests"` | ✅ + ❌ W0 | ⬜ pending |
| Cascade rules | layered files: no `!important` (except allowed), no colour literal outside `tokens.css` | static guard | filter above | ❌ W0 | ⬜ pending |
| D-08 pages usable | every page, both UAs: text readable, no control hidden, suite green | browser sweep + full suite | Playwright BEFORE/AFTER + pixel diff + human review | scratch | ⬜ pending |
| A1 neutral changes | zero visible difference, five mockup pages × five layouts | browser diff | Playwright + pixelmatch with stated threshold | scratch | ⬜ pending |
| D-12 no inline code in rebuilt layouts | covered by the layout guard | static guard | filter above | ❌ W0 | ⬜ pending |
| D-13 / D-15 mobile selection intact | `.Mobile` view and layout under a mobile UA | existing integration | `--filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileLayoutTests"` | ✅ | ⬜ pending |
| No planning references in new files | regex scan of new foundation files | static guard | filter above | ❌ W0 | ⬜ pending |
| Build and test health | no regression | full suite | `dotnet test` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/UiFoundationStaticGuardTests.cs` — layout hygiene, layered-file rules, token contrast, focus ring, reduced motion, toast attributes, `/lib` existence, planning-reference scan
- [ ] WCAG contrast helper (private static in that file) with known-value tests (4.34, 2.99, 3.36, 1.97, 4.94, 7.10, 4.53)
- [ ] `build/tests/package-release-smoke-test.sh` additions (vendor entries derived from `app.css`, curl 200s)
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
