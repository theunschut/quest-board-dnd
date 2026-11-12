---
phase: 96-account-and-pickers-ui-conversion
plan: 12
subsystem: ui-review
tags: [gate-2, after-sweep, accessibility-bar, review-sheet, release-handoff]
requires:
  - phase: 96-11
    provides: mailed SetPassword link check, all area slices merged into the milestone branch
provides:
  - AFTER capture of every manifest row under both User-Agents (118 records) and the accessibility-bar table per page and layout
  - the Gate 2 review sheet with 94 cited pairs, each shown BEFORE, foundation-only and AFTER
  - Gate 2 approved by the operator; the member-count wording fix (G2-11); the fixture subscriptions revoked
affects: [97, release]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/GroupPicker/Index.cshtml
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
key-decisions:
  - "Gate 2 approved with every item at its recommendation, no exceptions to approve"
  - "G2-11 changed: picker rows read 1 member / N members"
requirements-completed: [D-02, D-03, D-06, DoC-5, DoC-6, DoC-7, DoC-10, X-2, X-4]
duration: ~4 h (sweeps, probes and sheet; the close-out after approval about 40 min)
completed: 2026-11-12
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 1
---

# Phase 96 Plan 12: AFTER sweep, Gate 2 review sheet and Gate 2 Summary

The whole account and picker area was captured AFTER on the finished build under both User-Agents and held to the accessibility bar with zero failures. A single sheet showed every pair against BEFORE and against a foundation-only frame, and the operator approved Gate 2. One answer became a small code fix (the picker member count), and the 14 fixture subscriptions were revoked.

## AFTER totals against BEFORE

`sweep.mjs --pages all --stack` on the working tree (port 8000): 28 manifest rows, 118 records, all 200, every mobile record matching its row's `expectMobileLayout`, no rate-limited record, no failed assert or action. The plan's readability-compare against the original BEFORE (`--zero-contrast phase`, empty exceptions file) printed `ok` (0 failing rows, 0 residual contrast).

BEFORE was also captured again from the frozen baseline with stack-mode contrast (the original BEFORE used the non-stack figure), and a third capture, **foundation only** (the commit just before the first area change: baseline plus the v6.0.2 card restoration), shows what the restoration did on its own.

| Measure (phase pages, content area) | BEFORE | Foundation only | AFTER |
|---|---|---|---|
| Contrast failures, desktop | 171 | 171 | 0 |
| Contrast failures, phone | 272 | 272 | 0 |
| Phone targets under 44px | 8 | 8 | 0 |
| States scrolling sideways | 9 | 9 | 0 |
| States overflowing under text-spacing stress | 9 | 9 | 0 |
| Console and page errors | 0 | 0 | 0 |
| Tab stops without a focus ring | 0 | 0 | 0 |

(The original non-stack BEFORE read 285 and 344 contrast failures.) The v6.0.2 restoration changed the surfaces (solid card, title band, gold desktop title) and none of the measured figures, and it left the phone layouts untouched. Every contrast, target and overflow fix is this phase's. Each sheet pair states which part of its visible difference is which, in words and in measured pixel shares.

## Accessibility bar per page and layout

The full per-page and per-layout tables (A-1 to A-11, each with its evidence) are in `~/qb-ui-review/phase-96/after/final/summary.md`. Result: 0 failures across all 50 page-and-layout combinations.

| Item | Result on every converted page, both layouts |
|---|---|
| A-1 text 4.5:1, non-text 3:1 | 0 text failures; every form row measured a field: border vs fill 5.69 (4.91 in error), boundary vs surface at least 5.32 |
| A-2 two-tone ring | drawn on 100% of tab stops (skip link, button, picker row, link, link button, text input, checkbox) |
| A-3 ring unclipped | 0 clipped |
| A-4 focus not under the fixed bar | 0 once the smooth scroll settles |
| A-5 44px phone targets | 0 under 44px |
| A-6 no sideways scroll | none at 320 desktop, 390 and 412 phone; text-spacing stress clean |
| A-7 reduced motion | 0 elements still moving |
| A-8 landmarks and names | skip link first, one main, labelled nav, one h1, 0 unnamed icon controls, 0 images without alt |
| A-9 toasts | not applicable on any captured state |
| A-10 status not colour alone | refusal line has an icon, words and role alert |
| A-11 autofill, paste, text spacing | autocomplete on every password and email field, paste never blocked |

## Probe results on the final build

- `keyboard-picker.mjs`: ok (Tab order skip link, Logout, rows; Enter and Space choose a board; desktop and Pixel 7 with touch).
- `dialog-probe.mjs`: ok (keyboard open, focus trap, Escape, Keep and close return focus, real delete, no-script post, script-blocked post, both User-Agents).
- `copy-probe.mjs`: ok (Copied! status, icon-only buttons stay icon-only, restore, clipboard-denied fallback).
- AccessDenied and Events/Details scrollWidth (BEFORE to AFTER): AccessDenied 394 to 390 at 390 and 416 to 412 at 412, 320 and 1440 unchanged; Events/Details 394 to 390 at 390, 416 to 412 at 412.
- Counts (`after/96-10/counts.json`): area views 17 to 11; hex literals 4 to 0; rgb 14 to 0; important 11 to 0; inline style attributes 2 to 0; style blocks 0; inline scripts 2 to 0; inline handlers 2 to 0; transition-all 0.

## S1 and S2 findings of the account audit

| Finding | Severity | Closed by | AFTER evidence |
|---|---|---|---|
| A-account-031 | S1 | 96-05 | Every board is a native submit button reached by Tab and activated by Enter or Space; the keyboard pass printed ok under both User-Agents and all 216 picker-row tab stops drew a ring. |
| A-account-019 | S1 | 96-06 | Profile meta line measures 6.09:1 on both surfaces; Profile contrast failures BEFORE 7 / phone 20 (three-row fixture), AFTER 0 / 0. |
| A-account-001 | S2 | 96-08 | Forgot password link measures 6.55:1 and is underlined, on both layouts. |
| A-account-002 | S2 | 96-08 | One view for both User-Agents, so the commit button is the same btn-primary on desktop and phone (Log in). |
| A-account-003 | S2 | 96-08 | Validation messages measure 5.19:1 on both layouts; Log in empty-submit failures BEFORE 0 / 5 (desktop / phone), AFTER 0 / 0. |
| A-account-005 | S2 | 96-08 | Field border against fill 5.69:1 (4.91 in error); field boundary against the card at least 5.32:1; every credential row measured a field. |
| A-account-007 | S2 | 96-08 and 96-09 | Phone document titles are no longer empty, for example Log in "Log in - D&D Quest Board", Profile "Profile - D&D Quest Board", Edit "Edit Profile - D&D Quest Board". |
| A-account-010 | S2 | Foundation (94), verified by 96-08 | Parchment text on the cards passes: 0 contrast failures on all nine credential rows, both layouts. |
| A-account-011 | S2 | Foundation (94), verified by 96-09 | Navbar and footer text: 0 contrast failures anywhere on the page (not only inside the content) on every converted page, both layouts. |
| A-account-013 | S2 | 96-08 | Change Password uses the same btn-primary on both layouts; 0 failures. |
| A-account-016 | S2 | 96-09 | Save Changes is the primary brown (was green); Edit failures BEFORE 1, AFTER 0. |
| A-account-017 | S2 | 96-09 | Change Password is one btn-secondary on both layouts (was a blue outline link on desktop and a gold button on the phone); 0 failures. |
| A-account-020 | S2 | 96-06 | Phone labels and meta read 6.09:1; phone Profile failures BEFORE 20 (three rows), AFTER 0. |
| A-account-021 | S2 | 96-06 | Profile no longer scrolls sideways at 320 on the desktop layout: scrollWidth BEFORE 499, AFTER 320 (one, three and ten rows; 768 to 1440 clean). |
| A-account-032 | S2 | 96-05 | The glass card inside the glass panel is gone (flat rows); member count 6.09:1, board name 6.70:1; picker failures 0. |
| A-account-038 | S2 | 96-09 (rule from 96-08) | AccessDenied on the phone fits: scrollWidth 394 to 390 at 390 and 416 to 412 at 412; Events/Details 394 to 390 at 390. |
| A-account-039 | S2 | 96-09 | The info alert passes 4.5:1 and the signed-out log in link is 44px high; AccessDenied failures 0 signed in and out. |

A-account-043's audit title places the desktop calendar-subscription rules in `site.css`; that file no longer exists and the rules sat in the Profile block of `legacy-pages.css` until 96-06 moved them to `account.css`.

## Fixture declaration

`event.single` (id 2, `/Events/Details/2`, Audit Board) is the one Events row the review uses, a review fixture under D-05 and Phase 94 D-25. It was reused from Phase 94 (`created: false`, `reusedFrom: phase-94`), so **this phase wrote no new Events row** (the plan text expected one created through Events/Create). Definition of converted item 9 holds for the code: no source change in the phase writes Events or Quests rows. Removal of the row is left to the operator. The other fixtures (boards Ashenford Watch and the 60-character board with their memberships, the `multi` and super admin memberships, `noboard` removed from the Audit Board) remain; only the subscriptions were revoked, as the plan says.

## Gate 2 decision

**Approved.** The operator typed "yes looks good, approved" directly into the orchestrating session's conversation, in reply to the orchestrator's summary of every pair and the G2-01 to G2-18 recommendations. The orchestrating session relayed the approval to the executor and took responsibility for it. The operator gave no item answers, so every item is recorded at its recommendation (also in `~/qb-ui-review/phase-96/gate2/answers.json`). No pair was rejected, so there is no "Gate 2 rejections" section and the plan does not end gaps-found.

| Item | Answer |
|---|---|
| G2-01 empty-state clause | yes |
| G2-02 Edit Profile as btn-warning | yes (keep) |
| G2-03 Save Name and Keep Current Name | yes |
| G2-04 Delete Subscription and Keep Subscription | yes |
| G2-05 Back to Profile | yes |
| G2-06 row-action names | yes |
| G2-07 copy status announcement | yes (screen-reader check in UAT) |
| G2-08 title icons in the inherited colour | yes |
| G2-09 labels without colons | yes |
| G2-10 AccessDenied sentence at body colour | yes |
| G2-11 member-count plural | **change**: done, see below |
| G2-12 empty submit repeats its messages (96-08) | accept, keep the "All" summary |
| G2-13 picker brand stays a non-link (96-05) | accept |
| G2-14 reused link shows "Invalid token." (96-11) | accept (existing controller behaviour) |
| G2-15 first email-enabled start reached the real relay for about a minute (96-11): no requests, 0 sent lines; the later sink run caught no leftover jobs | acknowledge |
| G2-16 section heading weight 500 against 600 labels | no change (optional, not requested) |
| G2-17 picker bar wraps at 320 desktop | accept plus a follow-up (below) |
| G2-18 backstop truths not run in a browser | accept as UAT (below) |

Contrast exceptions approved: **none** (`contrast-exceptions.json` has no entries and every converted state has zero failures).

## G2-11: member count fix

Commit `61829ecd` (`fix(96-12): read 1 member and N members on the picker rows`): `GroupPicker/Index.cshtml` now prints `@group.MemberCount @(group.MemberCount == 1 ? "member" : "members")`. New integration test `GroupPicker_MemberCount_ReadsSingularForOneAndPluralOtherwise` seeds a one-member and a two-member board and pins "1 member", "2 members" and the absence of "member(s)". Guards stay green. Checked on the running app: the multi account's picker reads "10 members", "2 members", "1 member".

## Fixture revoke

`tools/revoke-fixtures.mjs` (scratch, outside the repository) deleted the fixture subscriptions through the Profile Delete dialog under the desktop User-Agent: player 1, multi 3, dm 10, **14 revoked, 0 left**. No address was printed; no captured address stays live.

## Follow-ups

- **G2-17 (follow-up, foundation-owned):** on a 320px desktop window the picker's fixed bar wraps to two lines and covers about 10px of the first element (Go to Platform for super admins, the panel title otherwise). It is identical in BEFORE, foundation-only and AFTER, and clean at 390. Fix belongs to the picker layout bar or the body padding (the bar is taller than the 56px the page reserves); log for a later foundation touch.
- **G2-16 (not changed):** the section heading (500) reads lighter than the 600 field labels on the phone Profile and Edit; it matches the approved Gate 1 frame. Left alone unless asked.

## UAT list (G2-18, human checks)

1. On a real phone (or a mobile User-Agent with touch): sign in, choose a board on the picker with the keyboard or switch access if available, open Profile, copy an address, open and dismiss the QR, Rename and Delete dialogs, open Edit, Change Password, Forgot Password, AccessDenied signed in and out, and the signed-out landing page.
2. With a screen reader: submit an empty Log in and press Copy; the refusal line and the copy status are announced (a live region becomes a gap only if either stays silent).
3. Expected: the phone layout is served on every page except the picker (own layout), every control is at least 44px, nothing scrolls sideways at 390, the dialogs return focus to their opener.
4. QR dialog with the clipboard-denied Copy path: the revealed field sits behind the dialog's focus trap; confirm nothing throws.
5. A real Edit with a changed email: confirm the email-change confirmation round trip.
6. A malformed SetPassword link (not a spent one): confirm the persistent "Password reset failed" toast.
7. Long validation messages wrapping at 320 and 390 with an identity error present (Log in with a wrong password).

## Release handoff

The phase ships as one release through the existing draft-release `deploy` approval gate, with deployment timing left to the operator. Each slice is already merged into `milestone/v9-rolling-improvements`; nothing new is built for release. `docker-publish.yml` publishes the GHCR image on every tag push regardless of that approval, so holding the release holds the production deployment only, not the public image. No tag was created and nothing was pushed.

## Deviations from Plan

### Operator-approved environment substitutions (ENVIRONMENT.md)
Port 8000 for 8096 and 8001 for 8097, `python3`, bundled Chromium, `qb-audit-*` accounts, `--stack` on every AFTER sweep.

### Auto-fixed and process

1. **[Process] Three-column sheet.** The BEFORE frames predate the v6.0.2 restoration, so a foundation-only frame was added per pair. The foundation-only build (a `git archive` of dbdee86d) ran temporarily on port 8001; the baseline was restored from `baseline-src` afterwards.
2. **[Process] BEFORE re-captured** (whole set, stack-mode contrast, current database) for the phase 97 fixture drift: the Audit Board's member count and the super admin picker had changed. The original `before/` is kept for readability-compare. Real board names on the super admin picker are blurred on the sheet and the foundation-only frame for that row is omitted.
3. **[Rule 1 - Bug] Focus-under-bar false positives.** The new focus probe measured mid smooth-scroll and reported a few targets under the fixed bar. It now waits for the scroll to settle; a settled run over the ten-row Profile showed none.
4. **[Process] Plan expectation of a new Events row** did not hold: `event.single` was reused from Phase 94 (declared above).
5. **[Process] Approval relay.** The gate was a blocking-human checkpoint and the executor first declined to treat a relayed approval as the operator's own. The orchestrating session then took responsibility for it explicitly and the close-out ran on that instruction.
6. **[Authorized code change] G2-11** changed one view and one test (commit `61829ecd`) although the plan expected the repository to be unchanged.

## Known Stubs

None.

## Threat Flags

None new. T-96-31: fantasy fixtures only on the sheet, QR captures reduced, real board names blurred, no token or address in the sheet (checked), subscriptions revoked after approval. T-96-32: release and GHCR caveat stated; no tag, nothing pushed. T-96-33: the operator's words, the answers and the pair ids are recorded verbatim above and in `gate2/answers.json`.

## Verification results

- `dotnet build`: 0 errors.
- Full test suite after the G2-11 change (SQL-backed tests required): unit 1389 passed; integration 1892 passed; 0 failed (first full run before the change: 1889 plus the earlier slices, 1891 passed).
- Working-tree app on 8000 restarted on the final build and left running; the baseline runs on 8001.

## Self-Check: PASSED

- `gate2/index.html` (94 `data-pair` ids, every S1 and S2 finding, `deploy`, `GHCR`, `event.single`), `gate2/answers.json`, `after/final/report.json`, `after/final/summary.md` and `tools/revoke-fixtures.mjs` exist.
- Commit `61829ecd` exists on `milestone/v9-rolling-improvements`.
