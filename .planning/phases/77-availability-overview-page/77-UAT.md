---
status: complete
phase: 77-availability-overview-page
source: [77-VERIFICATION.md]
started: 2026-08-29T13:35:00Z
updated: 2026-08-30T09:30:00Z
---

## Current Test

number: -
name: (all tests complete)
expected: |
  Event cards render as translucent glass matching the rest of the app - the wood backdrop
  visible through them, consistent with the legend card on the same page - not as opaque dark
  slabs. The count figures, the date/time line and the roster names are all comfortably
  readable. Every button ("What do these mean?", each card's "Show players") is filled, not a
  ghost outline.

  Also worth eyeballing while you are there: does the event title itself read comfortably
  against the glass? It sits at 4.02:1, which clears the WCAG large-text floor only on a
  lenient reading of 20px/600. It is pre-existing and was not introduced by this fix.
awaiting: none

## Tests

### 1. Mobile layout behaviour under a real mobile user agent

expected: Load `/Events` on a real mobile device or browser (not devtools emulation). The mobile card list renders. Tapping "Show players" expands the roster without navigating. Tapping within the expanded roster does not navigate. On a board with more than 10 upcoming events, "Show More Events" appears and loads more.
why_human: Mobile views in this app are user-agent-selected, not breakpoint-driven — devtools emulation never exercises `Index.Mobile.cshtml`. Automated coverage now exists (`Index_MobileUserAgent_*` facts) and gives strong confidence, but only a live device confirms real touch behaviour end to end.
result: pass
originally: issue
resolved_by: [77-11, 77-12]
reconfirmed_by: test 3
history_note: |
  This test FAILED when first run, on styling. Its result is recorded as pass only because the
  failure was closed by gap plans 77-11 and 77-12 and independently re-confirmed on a real device
  in test 3. The original report, diagnosis and root cause are preserved verbatim below and must
  not be removed - they are the record of a defect that passed five automated gates and was caught
  only by a human looking at a screen.
resolution: |
  Closed by gap plans 77-11 and 77-12 (merged 2026-08-30). All four defects verified fixed
  against the implementation, not against the summaries: .avail-card now carries glass
  declarations byte-identical to .modern-card; the count block, meta line and roster names set
  white explicitly, measured at 5.07:1 worst case against the backdrop (was 1.34:1); zero
  btn-outline- occurrences remain in the mobile view; and five regression facts now guard the
  contract. The guard was mutation-tested twice independently - reintroducing the opaque slab
  fails exactly one of the five facts with no false-positive coupling. Re-check on a real
  device is recorded as test 3.
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

### 3. Restyled mobile surface on a real device

expected: Load `/Events` on a real mobile device. Event cards should render as translucent glass matching the rest of the app - the wood backdrop visible through them, consistent with the legend card on the same page - not as opaque dark slabs. The count figures, the date/time line and the roster names should all be comfortably readable. Every button ("What do these mean?", each card's "Show players") should be filled, not a ghost outline.
why_human: The styling fix is code-verified and the contrast is measured, but no human has seen the restyled result on an actual phone. The defect it replaces passed five automated gates and was caught only by a person looking at a screen, so a person should confirm the fix the same way.
also_check: The card title uses cream on glass at 4.02:1. That clears the WCAG large-text floor only if 20px/600 counts as "large" - the strict reading requires weight 700. It is pre-existing and was not introduced by this fix, but worth eyeballing while you are there: does the event title read comfortably against the glass?
result: pass
detail: |
  Confirmed by the user on a real device. The mobile availability overview now renders in the
  application's established visual language: translucent glass cards consistent with the legend
  card on the same page, readable count figures, meta line and roster names, and filled buttons
  throughout. The card title's cream-on-glass contrast was eyeballed at the same time and raised
  no objection, so the borderline 4.02:1 large-text classification is accepted as-is and is not
  carried forward as a gap.

## Summary

total: 3
passed: 2
issues: 0 open (1 raised and resolved)
pending: 0
skipped: 0
blocked: 0

## Gaps

No open gaps. The one issue raised (test 1, critical) was closed by gap plans 77-11 and 77-12 and
re-confirmed on a real device in test 3.

**Session outcome:** 3 tests, 2 passed, 1 issue raised and resolved, 0 pending.

- Test 1 raised a critical styling gap on the mobile surface. Its three functional assertions
  passed at the time; only the visual contract failed. Closed and re-verified.
- Test 2 (chip distinctness) passed on the first run and was unaffected by the fix - the gap
  plans deliberately avoided touching the vote chips for exactly that reason.
- Test 3 confirmed the restyled surface on a real device.
