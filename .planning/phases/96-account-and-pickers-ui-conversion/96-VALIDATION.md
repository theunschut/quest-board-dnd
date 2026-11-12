---
phase: "96"
slug: "account-and-pickers-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
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

| Req | Behavior | Test Type | Automated Command | File | Status |
|-----|----------|-----------|-------------------|------|--------|
| DoC-1 | No `style=`, `<style>`, inline `<script>` or `on*=` in the converted views | static guard | `--filter "FullyQualifiedName~AccountConversionGuardTests"` | `Tests/AccountConversionGuardTests.cs` | ✅ COVERED |
| DoC-2 | `account.css` and `group-picker.css` carry no colour literal, no `!important`, and are in `@layer pages` | static guard | `--filter "FullyQualifiedName~UiFoundationLayerGuardTests"` | `Tests/UiFoundationLayerGuardTests.cs` | ✅ COVERED |
| DoC-3 | Views link `account.css`; `account.mobile.css` gone; Profile block out of `legacy-pages.css` | static guard | `AccountConversionGuardTests`, `UiFoundationDesktopShellGuardTests`, `AccountPagesRenderTests` | added in audit: `Tests/AccountPagesRenderTests.cs` | ✅ COVERED (legacy sheet part added in audit) |
| DoC-4 | Titles on merged and twin views; merged pairs render under both User-Agents | integration | `MobileViewsTests`, `AccountPagesRenderTests` | existing plus audit | ✅ COVERED (Profile/Edit desktop titles, AccessDenied both agents added) |
| DoC-8 | Rewritten guards pin the same intent; none deleted | static and integration | `CalendarSubscriptionStaticGuardTests`, `ProfileCalendarSubscriptionTests` | existing | ✅ COVERED |
| D-10/D-11 | Empty-state clause, delete modal on both layouts, no `confirm(`, script holds the copy contract | integration and static | `ProfileCalendarSubscriptionTests`, `CalendarSubscriptionStaticGuardTests`, `AccountConversionGuardTests` | existing | ✅ COVERED (focus behaviour is manual-only) |
| R1 (Q-122) | Each board row is a `<button type="submit">` in a `SelectGroup` form; oracle parity | integration | `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests`, `AccountConversionGuardTests` | existing | ✅ COVERED |
| D-07/D-08 | `/` merged; desktop never serves `mobile.css`/`mobile-layout`; phone gets the shell and one `h1` | integration | `HomeControllerIntegrationTests`, `MobileCssTests`, `MobileLayoutTests` | existing | ✅ COVERED |
| Q-84 | Building-key wording on both Profile and Edit twins; Administrator (desktop) vs Admin (phone) | integration | `AccountPagesRenderTests` | added in audit | ✅ COVERED |
| A5 (server part) | Failed log in renders the `role="alert"` refusal line under both agents | integration | `AccountPagesRenderTests` | added in audit | ✅ COVERED (announcement itself manual) |
| X-4 | No horizontal overflow at 390/320 on AccessDenied and Profile | scratch browser probe | scratch `audit-shoot` | scratch | MANUAL-ONLY |
| DoC-5/7 | Contrast, 44px targets, focus, keyboard picker, modal Escape/focus return | scratch probe and manual UAT | `96-UAT.md` | scratch | MANUAL-ONLY |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs` to cover DoC-1 to DoC-3 over the converted views and sheets
- [x] New mobile User-Agent facts: a title on each merged or twin phone view, the picker row markup, and the Q-84 wording on the phone Profile
- [x] Scratch tooling and fixtures outside the repository
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| SetPassword renders, submits and redirects with a freshly mailed token | X-3 / Q-105 | The token is mailed to the operator and must never enter the repository or a log | The operator supplies the link; open it under both User-Agents, set a password, and confirm the redirect |
| Before/after sign-off of the relaid-out picker (Q-122) and Profile (Q-123) | DoC-6 | Operator taste gate | Compare the BEFORE and AFTER screenshots in `~/qb-ui-review/phase-96/` |
| Screen-reader announcement of the validation summary | A5 | Needs assistive technology | Submit an empty Login and confirm the summary is announced |
| Delete dialog focus (open, trap, Escape, return to opener) and the QR Copy fallback inside the trap | D-11 | Needs a browser; no repo test runs one | `96-UAT.md` items 1 and 3 |
| Contrast 4.5:1, 44px targets, 320/390 overflow, wrapping of long validation messages | DoC-5/7, X-4 | Rendered measurement from the scratch harness | `96-UAT.md` items 1, 6, 7 |
| Real phone, real mailbox email-change round trip, malformed SetPassword toast | DoC-7 | Real device and mailbox | `96-UAT.md` items 1, 4, 5 |

---

## Validation Sign-Off

- [x] All tasks have an `<automated>` verify or a Wave 0 dependency
- [x] Sampling continuity: no 3 consecutive tasks without an automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-12

---

## Validation Audit 2026-11-12

| Metric | Count |
|--------|-------|
| Contract points audited | 12 |
| COVERED before audit | 9 |
| PARTIAL | 3 (Q-84 wording and desktop titles, AccessDenied under both agents, legacy-sheet retirement) |
| MISSING (automatable) | 1 (failed log in refusal line) |
| Gaps found and filled | 4 |
| Escalated | 0 |
| Manual-only | 3 groups (focus and clipboard, rendered measurements, real device and mailbox) |

Tests added: `QuestBoard.IntegrationTests/Tests/AccountPagesRenderTests.cs` (11 cases, green first run; no implementation changes, no debug iterations).
