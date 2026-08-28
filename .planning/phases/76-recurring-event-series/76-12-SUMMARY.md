---
phase: 76-recurring-event-series
plan: 12
subsystem: verification
tags: [uat, human-verification, checkpoint]

# Dependency graph
requires:
  - phase: 76-11
    provides: "The closing automated validation pass, whose green result is the precondition for spending human attention on the four manual-only behaviours"
provides:
  - "Developer verification of the four behaviours the server-side test stack structurally cannot reach: live-preview re-render fidelity, the cancelled state across three independently-written read surfaces, the mobile agenda on a real mobile user agent, and the horizon banner actually surfacing"
  - "One confirmed defect (horizon banner absent from the mobile calendar) and one design observation (warning threshold equals the runway target), both routed to gap closure rather than repaired here"
affects: [76-recurring-event-series (checkpoint plan; no production code changed)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "A browser automation surface that emulates a genuine mobile user-agent string (Android/Chrome), rather than desktop device-emulation, is sufficient for this codebase's mobile-view checks -- the server selects Index.Mobile.cshtml off the user agent, so device-emulation alone never exercises the mobile view at all."

key-files:
  created: []
  modified: []

key-decisions:
  - "The two code findings were recorded and routed to gap closure rather than fixed inside this plan, per the plan's own instruction that a checkpoint plan which also changes code hides which fix caused which result."
  - "The horizon banner's warning threshold equalling the runway target was recorded as an observation rather than raised as a defect, on the developer's explicit call -- it self-heals at the nightly top-up and is arguably correct-if-noisy."

patterns-established: []

requirements-completed: []
requirements-blocked: [EVTRECUR-03]
---

# Plan 76-12: Human verification of the four manual-only behaviours

## Outcome

Four of the five checks pass in full. One defect was found in check 5, and it blocks
EVTRECUR-03 from being claimed complete.

Verified on the Euphoria Inn board (BoardType.OneShot) against a locally running
`QuestBoard.Service`, driven through a real browser. A recurring series was created with a
weekly Saturday cadence and a four-position on-on-off-off cycle mask, anchored 2026-09-05.

## Check results

### 1. Live preview (EVTRECUR-02) — PASS

- The cadence section appears on toggling "Make this a recurring series".
- The derived line reads "Every 1 week(s) on Saturday" and tracks the chosen weekday.
- "Cycle length" tracks the cell count as positions are added (1 → 4).
- Toggling positions 3 and 4 off flips their accessible labels from "Session on this position"
  to "No session on this position", and the preview re-renders to a two-on-two-off pattern.
- **The dates created are exactly the dates previewed.** The preview showed Sep 5, Sep 12,
  Oct 3, Oct 10, Oct 31, Nov 7, Nov 28, Dec 5, Dec 26, Jan 2; the calendar after saving shows
  precisely those, with Sep 19/26 and Oct 17/24 correctly absent. This is the D-05 guarantee
  holding through the real request pipeline, which is the single most important thing this
  check exists to establish.

### 2. Cancelled state on all three surfaces (EVTRECUR-04) — PASS

- Occurrence details: grey banner "This session has been cancelled." renders; the Yes/Maybe/No
  buttons and Withdraw are **gone entirely** rather than disabled; the "Who's Coming" roster
  remains visible and the recorded answer ("Yes") is preserved.
- Desktop calendar chip: `calendar-event cancelled`, computed `opacity: 0.55` and
  `text-decoration: line-through` — present and visibly struck through, not missing. The other
  two occurrences in the same month are unaffected (`opacity: 1`, no decoration).
- Legend: gained a "Cancelled" row alongside Proposed, Finalized and Event.
- The action swaps to "Restore Occurrence" once cancelled.

### 3. Mobile agenda on a real mobile user agent (EVTRECUR-06) — PASS

- Driven with a genuine mobile user agent
  (`Mozilla/5.0 (Linux; Android 14; Pixel 8) ... Chrome/148.0.0.0 Mobile Safari/537.36`),
  not desktop device-emulation. The server selected the mobile agenda view, confirming the
  user-agent path was genuinely exercised.
- The cancelled entry renders as `agenda-event-entry cancelled` with computed `opacity: 0.55`
  and `text-decoration: line-through`, and remains present in the agenda.

### 4. Edit scope and collision notice (EVTRECUR-06) — PASS (one sub-step not exercised)

- The Edit form for a series occurrence shows no cadence, interval or mask fields.
- Save Changes opens the "Save this change" dialog with "Only this event" and
  "This and all future events".
- **The this-and-future sweep correctly skips the cancelled occurrence.** Editing the Sep 5
  anchor and choosing "This and all future events" renamed events 22, 23, 25 and 26, while
  event 24 — the cancelled Oct 3 occurrence — retained its original title. This is the
  hardest single assertion in the checkpoint and it holds.
- Not exercised: the amber collision strip on a date that another live sibling already holds
  (step 5). Worth covering during gap-closure verification.

### 5. Horizon banner (EVTRECUR-03) — FAIL (desktop passes, mobile does not)

Desktop behaviour is correct:

- The banner surfaced without contrivance: cancelling one occurrence dropped the series to 19
  upcoming against its runway of 20, and the calendar rendered
  "The 'Recurring UAT Session' series is running low — only 19 upcoming session(s) left.
  Visit its series page to check the cadence." with the title linking to `/Series/Details/{id}`.
- The series page renders the recurrence rule and template read-only with no editable field.
- "Delete Series" opens a dialog carrying real counts — "20 session(s) will be affected —
  0 already held, 20 upcoming — along with 1 availability answer(s) people actually gave" —
  and offers both "Detach sessions" and "Delete everything".
- Player-role absence was confirmed structurally rather than by a second login:
  `CalendarController.Index` populates `SeriesBelowRunway` only when `CanManage`, and
  `Index.cshtml` gates again on `Model.CanManage`. The banner is absent, not merely hidden.

The defect is on mobile — see below.

## Defects found

### D1 — The horizon banner does not render on the mobile calendar (check 5, mobile calendar surface)

`QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` contains no reference to
`SeriesBelowRunway`. The banner block exists only in `Index.cshtml`. Confirmed both by
inspecting the rendered mobile page under a real mobile user agent (no `.alert` element, body
text does not contain "running low") and by reading the two view sources.

Why this matters beyond a missing element: 76-10's own must-have describes the banner as
"the one place the silent-job failure becomes visible". On mobile that is false, so a DM who
works from a phone has no signal at all when the rolling window stops advancing — which
reproduces exactly the silent failure the banner was added to prevent.

Note also that `76-10-SUMMARY.md` states "The calendar and mobile agenda surfaces are fully
wired for the cancelled state and the horizon banner". The cancelled state is wired on both;
the banner is not. The automated gates could not catch this — a missing render breaks no test.

## Observations (recorded, not raised as defects)

### O1 — The warning threshold equals the runway target

`GetSeriesBelowRunwayAsync` reports a series as below runway at 19 of a 20-session runway, so
cancelling a single occurrence trips "running low" immediately and the banner persists until
the 3am top-up job restores the twentieth. It self-heals, and treating any shortfall as
reportable is a defensible reading. The risk is habituation: a banner that fires on routine
single cancellations trains DMs to ignore the one signal meant to be trusted. Recorded on the
developer's explicit call to leave the behaviour unchanged for now.

### O2 — Campaign boards cannot reach the calendar, where this phase's read surfaces live

Raised by the developer during this checkpoint and confirmed in code. The Calendar nav entry is
gated to `BoardType.OneShot` in both `_Layout.cshtml` and `_Layout.Mobile.cshtml` — a
deliberate Phase 37 decision (requirement NAV-01, commit `f7a31fa9`), locked in by
`LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent`.

Consequences for this phase: the horizon banner and the cancelled calendar chip are both
calendar-hosted, so on Campaign boards neither is reachable through normal navigation — and
Campaign boards are precisely the open-ended case the rolling window was built for.

One detail sharpens the fix: `CalendarController` itself is **not** board-type gated. Only the
nav link is hidden, so `/Calendar` is already reachable on a Campaign board by direct URL and
currently renders campaign quests alongside events. The desired behaviour is that Campaign
boards regain the calendar showing **events only**, with quests excluded, while OneShot boards
continue to show both. That supersedes part of NAV-01 and requires replacing its test.

## Requirements

- EVTRECUR-02 — satisfied by check 1. Not marked complete here; left for the gap-closure pass
  to claim alongside EVTRECUR-03 so both close together once the phase is actually whole.
- EVTRECUR-03 — **blocked** by D1. The rolling window works and the banner is correct on
  desktop, but the requirement's user-visible guarantee does not hold on mobile.
- EVTRECUR-04 and EVTRECUR-06 — already marked complete by 76-11; this checkpoint's checks 2,
  3 and 4 corroborate them at the UI level.

## Self-Check: FAILED

One defect (D1) blocks EVTRECUR-03. No production code was changed by this plan, per its own
instruction not to attempt repairs inside a checkpoint. Routed to gap closure via
`/gsd-plan-phase 76 --gaps`.
