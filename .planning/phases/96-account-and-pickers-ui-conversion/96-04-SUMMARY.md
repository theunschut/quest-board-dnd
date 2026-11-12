---
phase: 96-account-and-pickers-ui-conversion
plan: 04
subsystem: ui-review
tags: [gate-1, mockups, profile, picker, home-merge]
requires:
  - phase: 96-03
    provides: Gate 1 sections A (picker), C (Home), D (overflow)
provides:
  - Profile mockup (both twins) on a shippable account.css
  - completed Gate 1 sheet (items 1 to 16) and recorded answers (gate1/answers.json)
affects: [96-05, 96-06, 96-07, 96-08, 96-09, 96-10, 96-12]
key-decisions:
  - "Gate 1 approved by the operator: all 16 recommendations, Home merge yes"
  - "Mockups re-rendered on the corrected v6.0.2 foundation before approval"
requirements-completed: [D-01, D-02, D-03, D-07, D-08]
completed: 2026-11-12
status: complete
---

# Phase 96 Plan 04: Profile mockup and Gate 1 Summary

The Profile mockup was drawn for both twins on a shippable `account.css`. The Gate 1 sheet was completed with items 1 to 16, re-rendered on the corrected v6.0.2 foundation, and approved by the operator.

## Gate 1 answers

The operator approved Gate 1 on 2026-11-12. The coordinating session first relayed their words: "This looks a lot better. I'd say approve on gate 1?". Because Gate 1 is a blocking-human gate and item 9 needs an explicit answer, the executing session asked the operator directly. The operator asked what the Home twin item was about. After the explanation, they chose **"Approve all, 9:yes"** in reply to the question "do you approve Gate 1 with all 16 recommendations (… 9:yes …)". The plan executor declined to record an approval it could only see relayed, so the orchestrator wrote this record from the operator's own answer.

| # | Item | Answer | Default used |
|---|------|--------|--------------|
| 1 | Picker row shape | flat | no (approved recommendation) |
| 2 | Picker chevron | on | no |
| 3 | Picker hover tint | default (page scrim at 60%, full scrim on press) | no |
| 4 | Picker mockup | approved | no (explicit) |
| 5 | Profile reflow | intrinsic | no |
| 6 | Drop the Actions header | yes | no |
| 7 | Row-button mapping | default | no |
| 8 | Profile mockup | approved | no (explicit) |
| 9 | Home merge | yes | no (explicit) |
| 10 | Home unified look | approved | no (explicit) |
| 11 | Home button label | Log in | no |
| 12 | Overflow fix placement | shell | no |
| 13 | Picker no-boards state | ok (existing alert stays) | no |
| 14 | No-script Delete | kept | no |
| 15 | Picker phone title step (added) | on: 20px step and parchment at small widths | no |
| 16 | Phone Copy fallback (added) | icon | no |

## Home merge

- **Merge:** yes. `Views/Home/Index.cshtml` becomes the one view, and `Index.Mobile.cshtml` is retired.
- **Look:** approved. The unified card has a gold title on desktop (7.63) and parchment on the phone (9.88), over the title band.
- **Button label:** `Log in`. The two `HomeControllerIntegrationTests` assertions change in the same commit.

## Approved picker mockup

- **Path:** `~/qb-ui-review/phase-96/mockups/v1/picker/index.html`. The sheet is `mockups/v1/picker/group-picker.css`.
- **Version:** v1, with 68 frames.
- **Design:** flat rows, with the chevron on. Hover uses the scrim at 60% (name 8.48, count 7.71) and press uses the full scrim.
- **Classes:** a sixth class, `group-picker-text`, wraps the name and count.
- **Phone title:** the picker sheet's small-screen block sets `.group-picker .modern-card-header h1 { color: var(--qb-fg); font-size: var(--qb-text-lg) }`.

## Approved Profile mockup

- **Path:** `~/qb-ui-review/phase-96/mockups/v1/profile/index.html`. The sheet is `mockups/v1/profile/account.css`.
- **Version:** v1, with 48 frames on both twins.
- **Layout:** intrinsic reflow puts the actions beside the name at 1440 and beneath it at 1280 and narrower. The Actions header is dropped.
- **Row buttons:** Copy, Open in Calendar App and Show QR Code use `btn-secondary`. Rename uses `btn-warning`, Delete uses `btn-danger`, and Add Subscription is the one `btn-primary`.
- **Delete without JavaScript:** kept.
- **Phone Copy fallback:** icon-only. The revealed address field gets focus and a status line.
- **`account.css` additions beyond the plan's list:**
  - `.calendar-subscription-actions .btn { white-space: nowrap }`
  - full-width `.calendar-subscription-address` with control fill and border
  - `overflow-wrap: anywhere` on `dd`
  - a hairline and 16px gap between phone rows
- **Required cleanup:** the Profile slice must remove the old Calendar Subscription rules from `legacy-pages.css` in the same commit that adds them to `account.css`.

## Overflow placement

The fix goes in the shell: one rule in `shell.mobile.css`. Measured on the frozen baseline, it brings AccessDenied, Events/Details and the root-level Home row to the viewport width at 390 and 412. It moves no card edge.

## Redraw history

- **v1:** drawn on the phase-94 foundation copy taken from the frozen baseline.
- **v1, re-rendered:** after v6.0.2 merged into the milestone branch (881bfe73), the foundation copy's `components.css` and `quest-manage.css` were refreshed. All 116 frames plus the Home and reference frames were re-rendered and re-probed with zero findings.
  - The visual changes are a panel blur, the title band, and gold desktop titles.
  - Contrast measured on real pixels shows no visible text under 4.5:1; Profile labels are at least 7.23. The earlier 4.25 figure was a probe artefact, because it scored the glass without the scrim.
- No frames were redrawn at the operator's request.

## Deviations

- The foundation refresh (v6.0.2) happened mid-gate and is recorded above. The old renders are kept in `mockups/v1/prev-foundation/`.
- Items 15 and 16 were added to the plan's 1 to 14. Item 15's scope grew after the refresh: it also restores parchment at small widths.
- `probes.mjs` now treats a 1px `.visually-hidden` element as hidden, and `contrast.mjs` parses `color(srgb …)` (see 96-03). Later AFTER sweeps must use stack mode.
- The remote review branch `feature/phase-96-gate1-review` was pushed once and later deleted at the operator's request. Review then moved to the local sheet.

## Self-Check: PASSED

`gate1/answers.json` exists with the keys `picker`, `profile`, `home`, `gutter`, `assumptions` and `items`. Items 4, 8, 9 and 10 were answered explicitly. No repository source file changed.
