# Phase 82: Personal Cross-Board Event Agenda - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-29
**Phase:** 82-personal-cross-board-event-agenda
**Areas discussed:** What lands on the agenda, Where it lives and what it replaces, Crossing the boundary on click, The cross-tenant read and its proof

---

## Area selection

| Option | Description | Selected |
|--------|-------------|----------|
| What lands on the agenda | Roadmap-mandated: answered-only vs everything; global vs per-board window; interleaved vs grouped | ✓ |
| Where it lives, what it replaces | Roadmap-mandated: default landing place; entry visibility for single-board users | ✓ |
| Crossing the boundary on click | Events/Details 404s for non-active-board events; what a row click does | ✓ |
| The cross-tenant read and its proof | Which mechanism scopes the read; what the mandatory two-group test asserts | ✓ |

**User's choice:** All four.

---

## What lands on the agenda

### Which events belong on the agenda?

| Option | Description | Selected |
|--------|-------------|----------|
| Every upcoming event, all boards | Every non-cancelled event dated today-or-later on every board you belong to, row or no row. The only rule that reads the same on both board types. | ✓ |
| Only events you hold a row on | Scope to existing EventSignup rows. On Campaign that is everything; on One-Shot it hides every unopened event. | |
| Only events you have actually answered | `HasAnswered = true` only. Near-empty on Campaign until you vote; a receipt of past actions rather than an agenda. | |

**User's choice:** Every upcoming event, all boards.
**Notes:** Recorded as D-01. Structural consequence surfaced at the time and carried into Area 4 — the query must start from `Events`, not `EventSignups`, so Phase 77's `GetUpcomingWithSignupsAsync` cannot be widened into it.

### How does the next-N window apply across several boards?

| Option | Description | Selected |
|--------|-------------|----------|
| Global next-N, chronological | Next 10 across everything; paging reuses `PageIncrement`. Preserves Phase 77 D-09's property that page size is not set by data the page does not control. | ✓ |
| Up to N per board, then merged | Every board on page one, but page length scales with board count and "show more" becomes ambiguous. | |
| Global N with a per-board floor | Fairest reading, most rules to explain, test, and page through. | |

**User's choice:** Global next-N, chronological.
**Notes:** The user re-opened this after the roster decision — *"What about the upcoming 10 events in total? Not for each board? Advise?"* — and asked for a recommendation. Advice given: keep it 10 in total. Global-N's failure mode is a cadence mismatch burying a low-frequency board, which two alternating campaigns do not exhibit; and where it does arise later, the board filter is a visible control the reader can act on, versus a hidden quota. The filter therefore subsumes the per-board-floor option rather than competing with it. User confirmed. Recorded as D-03.

### Does a row carry anything about the rest of the board, or only your own answer?

| Option | Description | Selected |
|--------|-------------|----------|
| Your answer only | Event, date, board name, your cell. Keeps the cross-tenant read as narrow as possible. | |
| Your answer plus the event's counts | Reuse `_AvailabilityCounts`; widens the read to every member's rows on all your boards. | |
| Your answer plus a headcount only | Same widening of the read, less payoff. | |
| *(free text)* | Full event payload including all signups, plus a board-selection dropdown | ✓ |

**User's choice:** Free text — *"I think I want to show everything from the event. As if the user is opening the event from the board itself. It would be really useful to have a page that shows everything. In my usecase I'm playing in two boards. The two campaigns alternate in sessions so a total overview is really useful (including all signups). Perhaps the page should have a selection dropdown to select the boards the user wants to aggregate?"*
**Notes:** Accepted, with two consequences put on the record before locking. (1) It widens the read from "my rows" to "every member's rows on every board I belong to" — not a privilege escalation, since each roster is already visible on its own board's event page, but it makes the cross-tenant read the load-bearing part of the phase. (2) It stays compatible with the roadmap's "do not port the member axis": a shared member *column axis* still does not generalise across boards with different parties, but a roster carried *inside* a row does. Board filter judged in scope as a view control over the page's own data set — same category as Phase 77's paging — with the constraint that it must apply before the take, not after. Recorded as D-02 and D-04.

### The roster on each row — always visible, or behind an expand?

| Option | Description | Selected |
|--------|-------------|----------|
| Visible on desktop, behind a tap on mobile | Mirrors Phase 77 D-17/D-18 exactly — same split, same reasoning, no new idiom. | ✓ |
| Behind an expand on both surfaces | A true one-screen agenda; loses the at-a-glance cross-campaign comparison that motivated rosters. | |
| Always visible on both surfaces | Nothing hidden; a very long phone scroll, which Phase 77 already judged and went the other way on. | |

**User's choice:** Visible on desktop, behind a tap on mobile.
**Notes:** Recorded as D-06.

### How long should the board filter selection be remembered?

| Option | Description | Selected |
|--------|-------------|----------|
| For the session | ASP.NET session, SQL-Server-backed since Phase 33 — survives restarts, not logout. Same mechanism as `ActiveGroupId`. | ✓ |
| Permanently, per user | Survives logout and follows you across devices; needs a migration and a join/leave invalidation rule. | |
| Not at all | All boards every visit; the re-ticking the user asked to avoid. | |

**User's choice:** For the session. Default all boards ticked.
**Notes:** The default-and-persist answer was given ahead of the question, in the same message as the window advice request: *"Default to all, but remember changes afterwards."* This question pinned down how long "afterwards" is. Recorded as D-05.

---

## Where it lives and what it replaces

### Should the agenda change where a multi-board user lands?

| Option | Description | Selected |
|--------|-------------|----------|
| No — supplement only | Login flow untouched; the agenda is a dropdown entry beside Switch Group. | ✓ |
| Yes — land multi-board users on the agenda | Turns a forced interstitial into a useful page; edits a path every login goes through. | |
| Yes, and picking a row sets your active board | Agenda replaces the group picker; couples the page to session board state. | |

**User's choice:** No — supplement only.
**Notes:** Answers the roadmap's second mandated question. Grounding presented: `GroupPicker` is a forced interstitial that exists *only* for multi-board users (single-board users are auto-selected and redirected), and the agenda is the only surface needing no active board — so it was a genuine candidate to stand in front of it. Both rejected options preserved as deferred ideas. Recorded as D-07.

### Who sees the agenda entry in the nav?

| Option | Description | Selected |
|--------|-------------|----------|
| Every authenticated user | Same as the Switch Group entry it sits beside; no query in the layout. | ✓ |
| Only when you belong to more than one board | Truest to purpose; needs a membership count per render or a session value kept in sync. | |
| Every user, but only when it has something to show | Avoids an empty page by running the phase's heaviest query on every render. | |

**User's choice:** Every authenticated user.
**Notes:** Recorded as D-08.

### What does a SuperAdmin see on the agenda?

| Option | Description | Selected |
|--------|-------------|----------|
| Only boards they are actually a member of | One rule for everyone; membership-scoped by construction. | ✓ |
| Every board in the application | Mirrors `GroupPicker`'s all-groups behaviour; becomes an unbounded read over every event. | |
| Their memberships, with an opt-in to see all | Both behaviours to build and test, the wide one reachable by query string. | |

**User's choice:** Only boards they are actually a member of.
**Notes:** Recorded as D-09. Not raised by the roadmap; surfaced during codebase scouting from `GroupPickerController`'s `isSuperAdmin` branch.

### Besides the user dropdown, should anything else link to the agenda?

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown only | One entry point, as the roadmap places it. | |
| Also from Phase 77's board overview | Mirrors D-21's calendar→overview link; appears for single-board users too. | |
| Also from the overview and the calendar | Both events surfaces cross-link; most discoverable. | ✓ |

**User's choice:** Also from the overview and the calendar.
**Notes:** Implication flagged at the time and carried into D-10: both of those surfaces sit behind the `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate, so those links exist only once a board type has resolved. The dropdown entry remains the unconditional path in.

---

## Crossing the boundary on click

### What happens when you act on a row for an event on a board that isn't your active one?

| Option | Description | Selected |
|--------|-------------|----------|
| Prompt, then switch and open Details | Antiforgery-protected post reusing `GroupPicker.SelectGroup`; active-board rows skip the prompt. | ✓ |
| Silently switch and open Details | Fewest clicks; moves quests, shop, gold and nav to the other campaign unasked, and sets session state on a GET. | |
| Answer inline on the agenda itself | Best experience; makes Phase 75 D-01 false and requires replacing `EventIsOnActiveBoard`. | |
| Rows are not clickable | Zero risk; a dead end for the one action the page makes you want to take. | |

**User's choice:** Prompt, then switch and open Details.
**Notes:** Grounding presented: `EventsController.Details` has no explicit guard and 404s for another board's event via the ambient filter, while `SetAvailability` additionally checks `EventIsOnActiveBoard` as a deliberate second layer. Because D-02 puts the roster on the row, the only thing `Details` still offers is changing your own answer — so this is "how do I reply on a board I'm not in", not "how do I see more". Recorded as D-11.

### What is the click target on a row now that it carries a full roster?

| Option | Description | Selected |
|--------|-------------|----------|
| An explicit control on the row | A fixed "Open"/"Answer" control; a roster-bearing row is a different object from a grid line. | ✓ |
| The whole row, as in Phase 77 | Keeps D-24/D-25 literally true; a tall target whose consequence is a board switch. | |
| The event title only | Predictable and small; a title-sized tap target on mobile. | |

**User's choice:** An explicit control on the row.
**Notes:** Recorded as D-12, and explicitly marked in CONTEXT.md as a *deliberate divergence* from Phase 77 D-24/D-25 so a later reader does not "restore consistency".

### After the switch takes you to an event on another board, is there a way back?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — Details carries a back link when you arrived from the agenda | Reuses `SelectGroup`'s existing `returnUrl` threading; no new mechanism. | ✓ |
| No — you are on that board now | Nothing new to build; you retrace your own steps. | |
| Yes, and switch your active board back on the way | Tidiest outcome; the active board changes twice per answer. | |

**User's choice:** Yes — Details carries a back link.
**Notes:** Recorded as D-13. The active board is deliberately *not* switched back on return.

---

## The cross-tenant read and its proof

### Which mechanism scopes the cross-board read?

| Option | Description | Selected |
|--------|-------------|----------|
| One query pinned to your membership set | `IgnoreQueryFilters()` with `memberGroupIds.Contains(e.GroupId)` — the `GetEventSignupsForMemberIgnoringActiveBoardAsync` shape generalised to a set. One round trip, global take in SQL, no ambient mutation. | ✓ |
| Per-board `SetGroupId` iteration | Reuses a shipped query; one query per board, cannot take a global N, and mutates a scoped service the layout reads later. | |
| Per-board iteration in child DI scopes | Structurally safest reading of mechanism two; N scopes and N DbContexts to render one page. | |

**User's choice:** One query pinned to your membership set.
**Notes:** Recorded as D-14. Scouting surfaced a **third** mechanism the roadmap did not count — `QuestRepository.GetQuestsForTomorrowAllGroupsAsync`, a bare `IgnoreQueryFilters()` with no group predicate for a background job — recorded in CONTEXT.md explicitly as *not* a precedent.

### Where does the membership set come from?

| Option | Description | Selected |
|--------|-------------|----------|
| Read fresh from `UserGroups` every request | Membership is the authorisation, checked at read time; one indexed lookup. | ✓ |
| Cached in session, refreshed on board switch | Saves a query; creates a window where a left board is still in the cached set. | |
| From the user's claims | Free to read; a stale set can outlive a leave by the full cookie lifetime. | |

**User's choice:** Read fresh from `UserGroups` every request.
**Notes:** Recorded as D-15. Closes the roadmap's second named risk directly, and matters more under D-01 than it otherwise would: because the query starts from `Events`, a left board's rows still exist and still match the date predicate, so only the membership check removes them. Phase 75 D-20's signup deletion on leave is cleanup, not access control.

### Should there be a second defence layer after the query returns?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, but be honest about what it catches | Re-check materialized rows against the membership set, with a comment stating the limits. | ✓ |
| No — the pinned predicate is the mechanism | Argues a second check over the same variable is theatre. | |
| Yes, and check against a separately-read membership set | Genuinely independent layers; two queries and two sources that can disagree. | |

**User's choice:** Yes, but be honest about what it catches.
**Notes:** Recorded as D-16. Both layers read the same `memberGroupIds` list, so the guard catches a dropped predicate or a bad `Contains` translation but not a wrong membership set — weaker than `EventIsOnActiveBoard`, which compares against independent session state. The code comment must say so.

### What must the mandatory integration test prove? *(multi-select)*

| Option | Description | Selected |
|--------|-------------|----------|
| A non-member board is fully absent | Viewer in A, not B: none of B's titles, member names, or board name reach the page. | ✓ |
| Two joined boards both appear — and a third does not | Viewer in A and B, not C: A and B render interleaved, C does not. | ✓ |
| A board you left disappears | Viewer left B: B's events and name are gone. | ✓ |
| The board filter cannot widen the set | A non-member board id in the filter is ignored or rejected, never honoured. | ✓ |

**User's choice:** All four.
**Notes:** Recorded as D-17, together with the `DisposeAsync` reset of `ActiveGroupId` to `1` that `EventAvailabilityTenantIsolationTests` establishes. The second case is the one that proves the *feature* rather than the isolation — a suite built only from Phase 77's test would stay green if the aggregation collapsed to a single board.

---

## Claude's Discretion

Offered as a further round and declined ("I'm ready for context"), so these were left to the planner and recorded in CONTEXT.md:

- Page name, icon, route, and controller home (`EventsController.Index` is taken by Phase 77's overview).
- Whether the agenda gets its own take default rather than sharing `EventsOverviewOptions.DefaultTake = 10` — ten full-roster rows is a much heavier page than ten grid rows.
- How the board name is rendered on a row (text, badge, per-board colour chip, with or without board type).
- Day/date group headers versus fully self-contained rows.
- Empty-state copy for three cases: no boards, no upcoming events, everything filtered out.
- Whether the switch prompt is a modal, an inline confirm, or an interstitial page, and its copy.
- How the mobile card carries D-12's control alongside D-06's roster disclosure without the two competing for the same tap.
- Whether cancelled occurrences get any acknowledgement or simply vanish.
- Whether the viewer's own roster entry is highlighted, and roster ordering within a row.
- Naming, file placement, and the AutoMapper entries at both boundaries.
- Test structure beyond D-17's four mandated cases.

## Deferred Ideas

- Landing a multi-board user on the agenda instead of the group picker (declined under D-07 — considered seriously).
- The agenda as the group picker's replacement, with a row click setting the active board (declined under D-07/D-11).
- Answering availability inline on the agenda (declined under D-11 — would need the `EventIsOnActiveBoard` guard swapped for a membership check; deserves its own phase if ever wanted).
- A per-user persisted board filter preference surviving logout and following across devices (declined under D-05).
- Gating the nav entry on belonging to more than one board (declined under D-08).
- A SuperAdmin view of every board's events (declined under D-09).
- Per-board quotas or a per-board floor in the window (declined under D-03; D-04's filter covers the same problem visibly).
- Quests on the agenda (roadmap-excluded; not discussed).
- Switching the active board back when returning from `Details` to the agenda (declined under D-13).
