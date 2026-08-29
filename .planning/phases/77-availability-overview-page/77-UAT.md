---
status: testing
phase: 77-availability-overview-page
source: [77-VERIFICATION.md]
started: 2026-08-29T13:35:00Z
updated: 2026-08-29T13:35:00Z
---

## Current Test

number: 1
name: Mobile layout behaviour under a real mobile user agent
expected: |
  The mobile availability overview renders correctly on an actual device. Tapping the
  "Show players" toggle expands the roster without navigating away. Tapping inside the
  expanded roster (a name, a badge) also does not navigate. On a board with more than 10
  upcoming events, a "Show More Events" control is present and loads a larger set.
awaiting: user response

## Tests

### 1. Mobile layout behaviour under a real mobile user agent

expected: Load `/Events` on a real mobile device or browser (not devtools emulation). The mobile card list renders. Tapping "Show players" expands the roster without navigating. Tapping within the expanded roster does not navigate. On a board with more than 10 upcoming events, "Show More Events" appears and loads more.
why_human: Mobile views in this app are user-agent-selected, not breakpoint-driven — devtools emulation never exercises `Index.Mobile.cshtml`. Automated coverage now exists (`Index_MobileUserAgent_*` facts) and gives strong confidence, but only a live device confirms real touch behaviour end to end.
result: [pending]

### 2. Perceived visual distinctness of muted-default vs. confirmed cells

expected: View the availability grid and card list as a sighted user, then again under a colour-blindness filter or in greyscale. The unconfirmed-default chip (solid green, white dashed border, clock icon, italic "Yes") should read as clearly different at a glance from the confirmed-Yes chip (solid green, no border, check icon, normal weight). The empty cell (bare em-dash, no badge) should read as different from both.
why_human: This is more important than in the previous pass. The muted chip's fill weight was deliberately changed from a hollow tint to the same solid fill as the confirmed chip (commit `db469594`) because the hollow version was unreadable. The distinction now rests entirely on the dashed border, the clock icon, and the italic label rather than partly on fill weight. Code evidence confirms all three signals are present, but whether they read as *sufficiently* distinct at a glance — particularly a white dashed border on a solid button-sized badge — is an unavoidable human judgement.
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
