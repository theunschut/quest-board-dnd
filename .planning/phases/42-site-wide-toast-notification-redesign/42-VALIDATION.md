---
phase: 42
slug: site-wide-toast-notification-redesign
status: draft
nyquist_compliant: false
wave_0_complete: true
created: 2026-07-04
---

# Phase 42 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None detected — no `*.Tests` project exists in the solution (confirmed by research, 2026-07-04) |
| **Config file** | none |
| **Quick run command** | n/a — manual browser verification |
| **Full suite command** | n/a — manual browser verification |
| **Estimated runtime** | n/a |

This project has no automated test suite. Verification for this phase is manual/UAT-based, consistent with prior phases in this milestone (38, 39, 40, 41's human-verify checkpoints).

---

## Sampling Rate

- **After every task commit:** Manual browser check of the specific view(s) touched by that task.
- **After every plan wave:** Full click-through of all 5 layouts (`_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`, `_Layout.Platform.cshtml`, `_Layout.Platform.Mobile.cshtml`) — at least one view each — plus the Shop 4-file group (`Index`/`Details`, desktop + mobile).
- **Before `/gsd-verify-work`:** Full manual UAT pass per Manual-Only Verifications below must be green.
- **Max feedback latency:** n/a (manual; no automated feedback loop exists in this repo)

---

## Per-Task Verification Map

No REQ-IDs are mapped to this phase (confirmed: `.planning/REQUIREMENTS.md` v6.1 traceability table has no Phase 42 entry — deferred-idea promotion, not a numbered requirement). Per-task rows below are populated by the planner against each plan's tasks; every task's verification is manual (no automated test framework exists).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| *(filled in by planner per PLAN.md task)* | | | — | — | N/A | manual | n/a | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — no test framework exists to install or stub since verification is entirely manual/UAT-based.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Success/Error/Warning/Info toasts render top-right, correct color/icon, correct autohide behavior | D-04, D-10, D-11, D-12 | No test framework in repo; visual/timing behavior in a browser | For each of the 4 types, trigger the corresponding TempData flow and confirm: fixed top-right position, solid colored header (`bg-success`/`bg-danger`/`bg-warning`/`bg-info`), correct FontAwesome icon, Success/Info auto-hide at 5000ms, Error/Warning stay sticky until manual dismiss |
| All 5 layouts render the shared partial | Critical Discrepancy #1 (RESEARCH.md) | No test project to assert layout composition | Manually load one view under each of the 5 layouts (`_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`, `_Layout.Platform`, `_Layout.Platform.Mobile`) and confirm a toast renders when a TempData key is set |
| Shop's GoldReceived "+X gp" toast still shows alongside (not replacing) the generic Success toast | Claude's Discretion (GoldReceived) | Visual layering behavior, no automated DOM assertion available | Complete a sale in Shop Details and confirm both the GoldReceived badge toast and the generic Success toast appear together |
| Mystical Merchant novelty toast is visually unchanged | D-06 | Explicitly out of scope; regression check only | Trigger the merchant dialog in Shop Index and confirm yellow/`bg-warning` styling and behavior are untouched |
| `Account/Profile` now shows an Info toast for pending email-change confirmations | Pitfall 2 (RESEARCH.md) — new-but-intentional visible behavior, not a regression | No test framework; this is a previously-silently-broken path becoming visible for the first time | Request an email change from Profile and confirm an Info toast now appears (did not appear before this phase — call this out as intentional in the SUMMARY, not scope creep) |
| Toast-init script consolidation leaves exactly 2 `new bootstrap.Toast(...)` call sites | Pitfall 3 (RESEARCH.md) | No test framework; static code check | After migration, grep the codebase for `new bootstrap.Toast` — should appear exactly twice: once in `site.js`'s consolidated init, once in Shop's `showMerchantToast()` |

---

## Validation Sign-Off

- [ ] All tasks have manual verification steps documented in PLAN.md (no automated test framework exists)
- [ ] Sampling continuity: every task wave gets a full click-through per Sampling Rate above
- [ ] Wave 0 covers all MISSING references — n/a, no test infrastructure to stand up
- [ ] No watch-mode flags — n/a
- [ ] Feedback latency — n/a (manual)
- [ ] `nyquist_compliant: true` set in frontmatter once sign-off complete

**Approval:** pending
