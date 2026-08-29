---
status: complete
phase: 77-availability-overview-page
source: [77-VERIFICATION.md]
started: 2026-08-29T13:35:00Z
updated: 2026-08-29T14:35:00Z
---

## Current Test

number: -
name: (all tests complete)
expected: |
  The mobile availability overview renders correctly on an actual device. Tapping the
  "Show players" toggle expands the roster without navigating away. Tapping inside the
  expanded roster (a name, a badge) also does not navigate. On a board with more than 10
  upcoming events, a "Show More Events" control is present and loads a larger set.
awaiting: none

## Tests

### 1. Mobile layout behaviour under a real mobile user agent

expected: Load `/Events` on a real mobile device or browser (not devtools emulation). The mobile card list renders. Tapping "Show players" expands the roster without navigating. Tapping within the expanded roster does not navigate. On a board with more than 10 upcoming events, "Show More Events" appears and loads more.
why_human: Mobile views in this app are user-agent-selected, not breakpoint-driven — devtools emulation never exercises `Index.Mobile.cshtml`. Automated coverage now exists (`Index_MobileUserAgent_*` facts) and gives strong confidence, but only a live device confirms real touch behaviour end to end.
result: issue
severity: critical
reported: |
  The mobile overview does not use the app's established styling. Event cards render as
  opaque dark-gray slabs; text is hard to read; and a second control on the page is an
  outline button rather than filled.
diagnosis: |
  Three defects, all on the mobile overview surface.

  1. UI-SPEC violation (root cause). 77-UI-SPEC.md line 70 assigns the mobile card list to
     the .modern-card glass surfaces - rgba(255,255,255,0.15) body with cream #F4E4BC text.
     The implementation instead uses background-color #343a40, which is opaque, while its
     own comment claims it "follows the same rounded, translucent, tappable idiom". It is
     not translucent, so the card does not sit on the notice-board backdrop the way every
     other surface in the app does.
  2. Missing glass container. In the mobile calendar, #343a40 entries are small one-line
     rows nested inside .agenda-card-mobile, a translucent glass wrapper. The mobile
     overview has no equivalent wrapper - the view goes straight from container-fluid to
     bare .avail-card blocks - so an entry-level treatment is doing page-level work.
  3. Contrast failure on the count block - measured, not estimated. .avail-count-summary
     renders rgb(33,37,41) on a card background of rgb(52,58,64): a contrast ratio of
     1.34:1, where WCAG AA requires 4.5:1 for normal text. The count block never sets a
     colour, so it inherits dark text that was correct against the desktop parchment card
     and became near-invisible the moment the mobile card background went dark. The card
     title is unaffected (9.13:1) because it sets cream explicitly. This is the deliverable
     of the phase requirement that the overview must show a per-event availability count so
     a poorly-attended date is obvious at a glance - at 1.34:1 it is not obvious at all.
     This is why the severity is critical rather than cosmetic.
  4. Outline buttons, 11 of them. Every card's "Show players" toggle and the legend
     disclosure control use btn-outline-secondary with a transparent background, which the
     project UI guidelines forbid in favour of filled buttons. Against the wood backdrop
     and the dark card they read as ghosts. The same violation was fixed on the calendar
     cross-links earlier; this file was not in that sweep, and the regression test added
     during the validation audit only covers the calendar views.
functional_result: pass
functional_detail: |
  Confirmed by the user on a real device. All three behavioural assertions of this test pass:
  the "Show players" toggle expands the roster in place without navigating; tapping inside the
  expanded roster stays put; and the "Show More Events" control is present and genuinely loads
  more events. The phase's original blocking gap (no mobile paging control) and the roster
  tap-through defect are therefore both confirmed fixed against a live device, not just in test.
  This test's issue is confined to visual styling.
why_missed: |
  The UI safety gate reported hasUiFiles: false on every run of this phase, including the
  waves that added two stylesheets and four Razor views, so the UI-SPEC contract was never
  actually enforced against the implementation. The plan-checker, code review and phase
  verification all also passed without comparing rendered mobile styling to the spec.

### 2. Perceived visual distinctness of muted-default vs. confirmed cells

expected: View the availability grid and card list as a sighted user, then again under a colour-blindness filter or in greyscale. The unconfirmed-default chip (solid green, white dashed border, clock icon, italic "Yes") should read as clearly different at a glance from the confirmed-Yes chip (solid green, no border, check icon, normal weight). The empty cell (bare em-dash, no badge) should read as different from both.
why_human: This is more important than in the previous pass. The muted chip's fill weight was deliberately changed from a hollow tint to the same solid fill as the confirmed chip (commit `db469594`) because the hollow version was unreadable. The distinction now rests entirely on the dashed border, the clock icon, and the italic label rather than partly on fill weight. Code evidence confirms all three signals are present, but whether they read as *sufficiently* distinct at a glance — particularly a white dashed border on a solid button-sized badge — is an unavoidable human judgement.
result: pass
detail: |
  Confirmed by the user against the rendered mobile legend, which shows all five cell states
  together. The unconfirmed default (solid green, white dashed border, clock icon, italic
  label) reads as clearly different from the confirmed Yes (solid green, check icon, solid
  edge), and the empty cell (bare em-dash, no badge) reads as different from both. The
  readability fix that changed the muted chip to the same solid green therefore did not cost
  the distinction the accessibility requirement depends on.

## Summary

total: 2
passed: 1
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

```yaml
- truth: "The mobile availability overview renders in the application's established visual language, with readable text and filled controls"
  status: failed
  reason: "User reported: the mobile view doesn't really use our established styling; text is hard to read; the dark gray is not used elsewhere in the app; and another button on the page is outline instead of filled. Diagnosis confirmed a UI-SPEC violation plus a measured WCAG contrast failure."
  severity: critical
  test: 1
  artifacts:
    - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - .planning/phases/77-availability-overview-page/77-UI-SPEC.md
  missing:
    - "Mobile card surface must use the .modern-card glass treatment the UI-SPEC assigns it (rgba(255,255,255,0.15) body, cream #F4E4BC text) instead of the opaque #343a40 borrowed from calendar.mobile.css's entry-level idiom. The legend card on the same page already renders correctly, so the page currently shows two design languages at once."
    - "Count block (.avail-count-summary) must meet WCAG AA. Measured 1.34:1 (rgb(33,37,41) on rgb(52,58,64)); AA requires 4.5:1. It never sets a colour, inheriting dark text that was correct on the desktop parchment card. This is the deliverable of the per-event availability count requirement."
    - "The 11 btn-outline-secondary controls (each card's Show players toggle plus the legend disclosure) must be filled per the project UI guidelines."
    - "A regression guard covering the mobile overview's styling contract. The existing button-convention test covers only the calendar views, and the UI safety gate reported hasUiFiles: false throughout this phase, so the UI-SPEC was never enforced against the implementation."
```

**Passed:** test 2 (chip distinctness). **Functional assertions of test 1 also passed** — roster
expands in place, tapping inside the roster stays put, and "Show More Events" loads more. The
phase's original blocking gap is confirmed fixed on a real device; this gap is styling only.
