# Phase 75: Event Availability Signups - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-27
**Phase:** 75-event-availability-signups
**Areas discussed:** Answer surface & roster, One-Shot "not answered" state, Real answer vs untouched default, Deleting an event with answers, Campaign auto-signup (who & when), Joining mid-stream, Leaving & rejoining

---

## Answer surface & roster

**Q: On the event details page, what does a player see besides their own Yes/Maybe/No buttons?**

| Option | Description | Selected |
|--------|-------------|----------|
| Own answer only | Three buttons, nothing else; Phase 77's grid is the only place anyone sees others | |
| Own answer + names | Buttons plus a list of who answered what, reusing the Quest/Details participant-vote idiom | ✓ |
| Own answer + counts | Buttons plus "5 Yes · 2 Maybe · 1 No" with no names | |

**Notes:** The user first asked for a recap of what Phase 77 delivers before answering. Clarified that Phase 77 owns the cross-event *grid* (EVTVIEW-01) and the per-event *count* (EVTVIEW-03), but nothing on the single-event page — so a per-event roster is an early slice of the same data rather than a duplicate feature.

**Q: On a One-Shot board most members will have no signup row at all. What does the roster show there?**

| Option | Description | Selected |
|--------|-------------|----------|
| Answered members only | The list is exactly the rows that exist; no second membership query | ✓ |
| Everyone, unanswered grouped | All members listed with a "No answer yet" group | |
| Answered + a count of the rest | The answered list plus "and 4 members haven't answered" | |

**Q: Does availability show anywhere outside the event details page?**

| Option | Description | Selected |
|--------|-------------|----------|
| Details page only | Calendar untouched; the five protected `_Calendar.cshtml` call sites stay out of the blast radius | ✓ |
| Read-only marker on the chip | Your own answer shown on the desktop chip and mobile agenda entry | |
| Marker plus a Yes count | Answer and board Yes count on the chip | |

**Q: Who can see the roster of names on an event?**

| Option | Description | Selected |
|--------|-------------|----------|
| Every board member | Matches Quest/Details, where participants see each other's votes | ✓ |
| DM tier only | Roster behind the existing `CanManage` flag | |

**Notes:** Flagged at the time that choosing "every board member" effectively pre-answers Phase 77's own open DM-only-vs-everyone question.

---

## One-Shot "not answered" state

**Q: Once a player has answered on a One-Shot board, can they get back to "not answered"?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — a Withdraw action | Deletes the row, mirroring `revokeSignup()` | ✓ |
| No — first answer is permanent | Only movable among Yes/Maybe/No | |

**Q: Does Withdraw exist on Campaign boards too?**

| Option | Description | Selected |
|--------|-------------|----------|
| One-Shot only | Enforced server-side, following the QuestController Close/Reopen precedent | ✓ |
| Both, but Campaign re-creates | Uniform UI; the auto pass restores a Yes row | |

**Q: How does a One-Shot player with no row create one?**

| Option | Description | Selected |
|--------|-------------|----------|
| One click on Yes/Maybe/No | Signing up and answering are one gesture | ✓ |
| Two-step: Sign Up, then answer | Mirrors the quest flow | |

---

## Real answer vs untouched default

**Q: How should "this is a real answer" be recorded, so Phase 77 can render EVTVIEW-02?**

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit `IsExplicit` column | New additive migration; unambiguous name | |
| Stamp `UpdatedAt` on first write | Player writes always stamp it, auto passes never do; no schema change | ✓ |
| Derive from board type | `UpdatedAt != null OR board is One-Shot`; no code or schema change | |

**Notes:** The user leaned toward stamping `UpdatedAt` and asked for a pros/cons comparison against the explicit column before committing. The comparison turned on failure mode: with one field the answer write *is* the flag write so they cannot diverge, whereas two fields can silently drift and only surface as a wrong colour in a later phase. Claude's initial recommendation (explicit column) was revised to match the user's lean on that basis. Paired mitigation agreed: expose a named `HasAnswered` property on the domain model and rewrite the now-inaccurate entity comment.

**Q: Should the Phase 75 roster distinguish an untouched Campaign default from a real Yes?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — mark untouched | One extra rendering branch, no extra query | |
| No — plain Yes/Maybe/No | Distinction stays entirely in Phase 77 | ✓ |

---

## Deleting an event with answers

Opened because Phase 74 D-17 explicitly deferred it here.

**Q: What should the delete confirmation become?**

| Option | Description | Selected |
|--------|-------------|----------|
| `confirm()` with a count | Same native dialog, message names what is destroyed | ✓ |
| Bootstrap modal | Weightier, but a new UI pattern on one page | |
| Type-to-confirm | Strongest guard, out of step with the rest of the app | |
| Leave `confirm()` as-is | Declines Phase 74's deferred question | |

**Q: What does the count count?**

| Option | Description | Selected |
|--------|-------------|----------|
| Real answers only | Accurate about what is lost; silent on a fresh Campaign event | |
| All signup rows | Always reports the full member count on a Campaign board | ✓ |

**Notes:** The trade-off was stated in the option text — on a Campaign board this warning always fires at maximum volume and is not strictly accurate about what is lost. The user chose it anyway; recorded in CONTEXT.md as a deliberate decision with the cost named, so the planner does not "correct" it.

---

## Campaign auto-signup — who & when

**Q: Who gets a Yes row when a DM creates an event on a Campaign board?**

| Option | Description | Selected |
|--------|-------------|----------|
| Every member, all roles | Matches the role-agnostic `GetAllGroupMembers` | ✓ |
| Players only | Filters to `GroupRole.Player` | |

**Notes:** Players-only would have locked DMs out of the feature entirely, since Campaign boards have no opt-in path.

**Q: Written at create time, or materialized lazily on read?**

| Option | Description | Selected |
|--------|-------------|----------|
| Written at create time | Literal reading of "from the moment the event exists" | ✓ |
| Materialized lazily on read | Cheap create, but a read path that writes | |
| Neither — infer the default | No rows until someone answers | |

**Q: Does a past-dated event (allowed by Phase 74 D-19) get the fan-out too?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — regardless of date | One rule, inherited unchanged by Phase 76 | ✓ |
| No — future events only | A second date rule in a second place | |

---

## Joining mid-stream

**Q: Where is the "future events" boundary for a joining member?**

| Option | Description | Selected |
|--------|-------------|----------|
| `Date >= today` | Today's event included | ✓ |
| `Date > today` | Strictly future | |

**Notes:** During this area it was verified that `GroupService.AddMemberAsync` is a single chokepoint — the invite flow (`UserService.CreateOrAddToGroupAsync:178`) routes through it — so there is only one place to hook, not two.

**Q: If the backfill fails while adding a member, what happens to the membership?**

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic — both or neither | Failure rolls back the join; no half-synced state | ✓ |
| Member joins + repair action | Idempotent "sync availability" action, safe to re-run | |
| Best-effort, log only | Join commits, nothing repairs it | |

**Notes:** The user proposed a third option — let the member join, add a retry button — and asked for advice. Advice given: the atomic failure is rare and *attended* (a loud error the DM retries), whereas a repair button only helps if someone notices a gap they may never look at. The user's instinct was assessed as sound but aimed at the wrong phase — an idempotent repair pass earns its keep in Phase 76, where the generator runs unattended and the ROADMAP already names "the job silently stopping" as a risk. The user took the atomic option and the repair pass was deferred to Phase 76.

---

## Leaving & rejoining

**Q: Does a leaver's deliberate "No" on a future event get removed along with their auto-signups?**

The user did not understand the question as first framed and asked for elaboration; a concrete four-event scenario (Tom, Aug 1 past / Sep 5 / Sep 19 answered No / Oct 3) was walked through before re-asking.

The user then proposed a broader change: **automatically deleting past events entirely**, on the grounds that events are forward-looking scheduling artefacts rather than reference material — and asked whether that would mean simply removing *all* of a leaver's signups.

Advice given, splitting the two:
- **Purging past events is out of scope for Phase 75** and was pushed back on generally: it reverses shipped Phase 74 D-19, requires a low-water mark on `EventSeriesEntity` so Phase 76's slot-index idempotency does not resurrect purged occurrences, and introduces unattended irreversible deletion. A cheaper alternative was offered — hide past events from DM-facing surfaces rather than deleting them.
- **Removing all of a leaver's signups is in scope and is the simpler rule**, but it edits EVTAVAIL-04, whose first clause ("keeps their past answers") would become false.

**Q: What happens to a departing member's signups?**

| Option | Description | Selected |
|--------|-------------|----------|
| All of them, every event | No date boundary, no touched/untouched branch | ✓ |
| All future rows only | Matches EVTAVAIL-04 as written | |
| Future untouched rows only | Most literal reading of the requirement | |

**Q: What should happen to the past-events idea?**

| Option | Description | Selected |
|--------|-------------|----------|
| Hide, don't delete | Deferred idea aimed at Phase 77 | |
| New roadmap phase for purging | Specified properly rather than bolted on | |
| Drop it | Past events stay as they are today | ✓ |

**Q: Does the Remove Member control need to warn?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — warn on removal | `GroupController.RemoveMember` has no confirmation today | ✓ |
| No — leave it as-is | | |

**Q (user-initiated): What happens to a leaver's quests, character selections, and votes today?**

The user asked this before finalising, to check consistency. Verified in code: `RemoveMemberAsync` deletes exactly one row — the `UserGroups` membership — and nothing else. Quest signups, date votes, characters, gold and transaction history all survive, and the account itself survives (`AdminController.DeleteUser` is membership-removal only).

This was reported back with the observation that D-20 would make event availability the **only** thing erased on leave, and the decision was re-offered:

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it — delete all | Availability is deliberately different; inconsistency accepted | ✓ |
| Match quests — keep everything | Perfectly consistent, no cleanup code at all | |
| Middle — future rows only | Consistent for anything that already happened | |

**Notes:** The user re-confirmed after seeing the inconsistency. Recorded in CONTEXT.md D-22 as a knowing choice with its cost stated (past rosters silently lose departed members).

---

## Claude's Discretion

- Whether a past event still accepts new or changed answers — raised during discussion, never settled.
- Roster ordering and the One-Shot empty-state copy.
- Whether an availability change produces a toast, an email, or nothing.
- Inline vs Hangfire for the join-time backfill (inline expected; D-19's atomicity argues for it).
- Exact wording of both confirmation dialogs and any toast messages.
- Naming and file placement across Domain/Repository/Service, and both AutoMapper profile entries.
- Whether the roster is inline or a partial.
- Whether `EventEntity` gains a `Signups` navigation collection, and the roster query shape.
- Test structure beyond the mandated two-group isolation test and the board-type enforcement test.

## Deferred Ideas

- An idempotent "sync availability" repair pass — aimed at Phase 76.
- Automatically purging past events — considered and declined; recorded so it is not re-litigated.
- Hiding past events from DM-facing surfaces — offered as the cheaper alternative, also declined.
- Rendering the untouched-vs-real distinction in the UI — Phase 77 (EVTVIEW-02).
- A per-event availability count — Phase 77 (EVTVIEW-03).
- Marking availability on the calendar chip or mobile agenda entry — rejected to keep `_Calendar.cshtml` untouched.
- Guarding against a remove-and-re-add losing deliberate answers — accepted cost.
