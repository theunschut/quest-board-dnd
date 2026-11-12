---
phase: "96"
slug: "account-and-pickers-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-10"
---

# Phase 96 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `96-RESEARCH.md` § "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~ProfileCalendarSubscriptionTests\|FullyQualifiedName~CalendarSubscriptionStaticGuardTests\|FullyQualifiedName~GroupPickerControllerIntegrationTests\|FullyQualifiedName~CrossBoardPickerSkipTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~MobileCssTests\|FullyQualifiedName~MobileLayoutTests\|FullyQualifiedName~HomeControllerIntegrationTests\|FullyQualifiedName~AccountControllerIntegrationTests\|FullyQualifiedName~AccountConversionGuardTests"` |
| **Full suite command** | `dotnet test` |
| **Browser checks** | Scratch Playwright outside the repository; outputs in `~/qb-ui-review/phase-96/` |
| **Estimated runtime** | ~15 s quick (after build), ~2 min full |

Notes: re-run once on a transient `0x80131506`. Build output is shared with the Phase 95 checkout, so don't run the two phases' executions concurrently in one checkout.

---

## Sampling Rate

- **After every task commit:** the quick filter narrowed to the slice's pages:
  - Picker slice: `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests`, `MobileViewsTests`, `UiFoundation`.
  - Profile slice: the picker set plus `ProfileCalendarSubscriptionTests`, `CalendarSubscriptionStaticGuardTests`, `AccountControllerIntegrationTests`.
  - Credentials, Edit, AccessDenied and Home slices: the picker set plus `AccountControllerIntegrationTests`, `HomeControllerIntegrationTests`, `MobileCssTests`, `MobileLayoutTests`.
  - Plus the scratch probe on the touched pages under both User-Agents.
- **After every plan wave:** `dotnet test` (full suite).
- **Before `/gsd-verify-work`:** the full suite is green and `dotnet build` is clean. The AFTER sweep shows no new contrast failure and no hidden control against BEFORE.
- **Max feedback latency:** 120 seconds.

---

## Per-Requirement Verification Map

| Req | Behavior | Test Type | Automated Command | File Exists | Status |
|-----|----------|-----------|-------------------|-------------|--------|
| DoC-1 | No `style=`, `<style>`, inline `<script>` or `on*=` in the converted views | static guard | `--filter "FullyQualifiedName~AccountConversionGuardTests"` | ❌ W0 (new) | ⬜ pending |
| DoC-2 | `account.css` and `group-picker.css` carry no colour literal and no `!important`, and are in `@layer pages` | static guard | `--filter "FullyQualifiedName~UiFoundationLayerGuardTests"` (`account.css` added to the arrays) | ✅ edit | ⬜ pending |
| DoC-3 | Account views link `account.css`, `account.mobile.css` is gone, and the Profile block is out of `legacy-pages.css` | static guard | `AccountConversionGuardTests`; `UiFoundationDesktopShellGuardTests` | ❌ W0 + ✅ | ⬜ pending |
| DoC-4 | Titles on merged and phone views; merged pairs render under both User-Agents | integration | `MobileViewsTests` (rewritten facts plus new title facts) | ✅ rewrite | ⬜ pending |
| DoC-8 | Rewritten guards pin the same intent; none deleted | static and integration | `CalendarSubscriptionStaticGuardTests`, `ProfileCalendarSubscriptionTests` | ✅ rewrite | ⬜ pending |
| D-10/D-11 | Empty-state clause and delete modal on both layouts; no `confirm(`; the script file holds the copy-contract strings | integration and static | `ProfileCalendarSubscriptionTests`, `CalendarSubscriptionStaticGuardTests` | ✅ rewrite | ⬜ pending |
| R1 (Q-122) | Each board row is a `<button type="submit">` inside a `SelectGroup` form; oracle parity is unchanged | integration | `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests`, plus a picker markup fact | ✅ + 1 new fact | ⬜ pending |
| D-07 | `/` under a desktop User-Agent never contains `mobile.css` or `mobile-layout` | integration | `MobileCssTests`, `MobileLayoutTests`, `HomeControllerIntegrationTests` | ✅ | ⬜ pending |
| X-4 | No horizontal overflow at 390 (phone User-Agent) or 320 (desktop) on AccessDenied and the populated Profile | scratch browser probe | scratch `audit-shoot` overflow flag | scratch | ⬜ pending |
| DoC-5/7 | Contrast is at least 4.5:1 for text and 3:1 for non-text; phone targets are 44px; focus is visible; the picker works by keyboard; the modal handles Escape and returns focus | scratch probe and manual UAT | probe list in UAT | scratch | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs` to cover DoC-1 to DoC-3 over the converted views and sheets
- [ ] New mobile User-Agent facts: a title on each merged or twin phone view, the picker row markup, and the Q-84 wording on the phone Profile
- [ ] Scratch tooling and fixtures outside the repository
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| SetPassword renders, submits and redirects with a freshly mailed token | X-3 / Q-105 | The token is mailed to the operator and must never enter the repository or a log | The operator supplies the link; open it under both User-Agents, set a password, and confirm the redirect |
| Before/after sign-off of the relaid-out picker (Q-122) and Profile (Q-123) | DoC-6 | Operator taste gate | Compare the BEFORE and AFTER screenshots in `~/qb-ui-review/phase-96/` |
| Screen-reader announcement of the validation summary | A5 | Needs assistive technology | Submit an empty Login and confirm the summary is announced |

---

## Validation Sign-Off

- [ ] All tasks have an `<automated>` verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without an automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
