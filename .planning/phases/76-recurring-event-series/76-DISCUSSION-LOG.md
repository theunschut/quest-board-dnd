# Phase 76: Recurring Event Series - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-28
**Phase:** 76-Recurring Event Series
**Areas discussed:** Series definition & preview, What a generated occurrence says, Cancel / move / edit one occurrence, The rolling window, Series removal (added mid-discussion)

---

## Series definition & preview

### What does one position in the cycle mask represent?

| Option | Description | Selected |
|--------|-------------|----------|
| One cadence step | `date(N) = anchor + N×IntervalWeeks weeks`; `mask[N mod len]` decides if it fires. Slot index stays stable across moves and mask edits. | ✓ |
| One calendar week | Mask position is always one week; IntervalWeeks becomes redundant or ambiguous. | |
| Drop IntervalWeeks, mask only | Every position one week, mask is the only cadence control. | |

**User's choice:** One cadence step, after asking for the recommendation to be explained further.
**Notes:** The explanation walked through a worked example (anchor Sat 2026-09-05, interval 1, mask `1,1,0,0`) and gave three reasons: the two knobs are not redundant (fortnightly-forever is interval 2 + mask `1`), the slot index must be date-independent for the locked idempotency key to survive a move, and mirrored masks interleave structurally rather than by arithmetic coincidence. → D-01

### Daily or monthly cadence in this phase?

| Option | Description | Selected |
|--------|-------------|----------|
| Weekly grid only | Exactly what EVTRECUR-01 asks. Matches the shipped schema, keeps slot→date a single multiplication. | ✓ |
| Add daily too | Cadence-unit field and `AddDays`. Cheap, but no requirement asks for it. | |
| Add monthly too | "nth weekday of every N months". Genuinely useful; needs a second date-derivation path. | |
| Weekly now, monthly as its own phase | Ship weekly, write monthly up as a roadmap candidate. | |

**User's choice:** Weekly grid only.
**Notes:** Asked directly whether the model restricted them to weekly recurrence. Answer was yes as scoped, with the honest caveat that "every 4 weeks" approximates monthly but drifts (13/year), and that "third Saturday of every month" is the one shape RFC 5545's `BYSETPOS` handles natively — a point the roadmap's no-library argument had not accounted for. Both deferred. → D-02

### How does the DM enter the cycle mask?

| Option | Description | Selected |
|--------|-------------|----------|
| Two numbers: N on, M off | Two inputs → `1,1,1,0,0`. No new JS pattern; cannot express a rhythm alternating inside the cycle. | |
| Clickable toggle strip | Row of on/off buttons with add/remove for length. Reaches every pattern; new interactive component. | ✓ |
| Raw comma-separated field | Type `1,1,1,0,0` directly. Full power, exposes the storage format. | |
| Numbers now, toggles if it chafes | Ship the pair, defer the strip. | |

**User's choice:** Clickable toggle strip.
**Notes:** First asked whether the mask was limited to 4 positions. Clarified it is not — `nvarchar(200)` holds roughly 100 positions, and `1,1,1,0,0` (three on, two off) is equally valid. Also clarified that the N-on/M-off form reaches any *length* but only rhythms shaped as a run of ones then a run of zeros, which is what pushed the choice to the strip. → D-03

### What to do with the shipped `WeekDay` column?

| Option | Description | Selected |
|--------|-------------|----------|
| Derive it from the anchor | DM picks a start date; weekday derived and shown back; column written on save. | ✓ |
| DM picks weekday, anchor snaps forward | Choose "Saturdays" and a start date; snap on save. | |
| Drop the column | Remove it in this phase's migration. | |

**User's choice:** Derive it from the anchor.
**Notes:** Raised because under D-01 the column is a stored duplicate of `AnchorDate.DayOfWeek` that can never legitimately disagree. → D-04

### How is the ~10-date preview computed?

| Option | Description | Selected |
|--------|-------------|----------|
| Server endpoint, debounced fetch | Same Domain generator that materializes occurrences; preview cannot disagree with reality. | ✓ |
| Client-side JS | Instant, no round-trip — but a second copy of the rule that decides real dates. | |
| Server-side, on button click | Same guarantee, fewer requests — but EVTRECUR-02 says "live". | |

**User's choice:** Server endpoint, debounced fetch. → D-05

### Where does series setup live?

| Option | Description | Selected |
|--------|-------------|----------|
| Repeat toggle + series page | Toggle on the existing Create Event form; series detail page reached from any occurrence. | ✓ |
| Separate Create Series screen | Second navbar entry, own form and pages. | |
| Repeat toggle only, no series page | Smallest surface; nowhere to see or end the rule. | |

**User's choice:** Repeat toggle + series page. → D-06

### Should ending a series be in this phase?

| Option | Description | Selected |
|--------|-------------|----------|
| End it, offer to clear the future | Ended marker stops generation; confirm names counts and offers to clear future occurrences. Past always kept. | ✓ |
| End it, keep everything already generated | Strictly additive-only; DM cancels unwanted ones individually. | |
| Give the series an end date instead | Nullable end date past which no slot fires. | |
| Defer it to its own phase | Ship generation with no off switch. | |

**User's choice:** End it, offer to clear the future.
**Notes:** Flagged as a gap rather than assumed — nothing in EVTRECUR-01…08 covers stopping a series, and without it a finished campaign generates forever. The user later refined the *mechanism* to a nullable `EndDate` (see Series removal below), which was adopted on top of this behaviour. → D-11

---

## What a generated occurrence says

### Where do a generated occurrence's title, description, and start time come from?

| Option | Description | Selected |
|--------|-------------|----------|
| Template fields on the series | Add Title/Description/StartTime to `EventSeriesEntity`; generator stamps them. | ✓ |
| Copy from the latest existing occurrence | No new columns; but editing one occurrence would change every future one. | |
| Copy from the first occurrence | Stable source, but the series' identity lives in a row that can be cancelled or moved. | |

**User's choice:** Template fields on the series. → D-08

### When the DM edits the series template, what happens to existing future occurrences?

*(Asked twice — the first attempt was superseded by the user's counter-proposal.)*

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit opt-in, with a count | Checkbox on the series edit form: "also update the N untouched future occurrences". | |
| Auto-propagate to untouched future occurrences | Silent rewrite, no confirmation. | |
| Strict additive-only | Only newly generated slots get the change. | |
| You decide | Planner discretion. | |
| **Occurrence-level scope prompt (user's proposal)** | **On saving an occurrence edit: "change this event or change the series?"** | **✓** |

**User's choice:** Their own proposal — a Google Calendar–style scope prompt at the point of saving an occurrence edit.
**Notes:** The user first said "I really don't know what to answer here" and asked a side question about moving a single occurrence two months into a series. Answering that (the row keeps `SeriesId`/`SeriesSlotIndex`, only `Date` changes; the generator's slot-keyed check skips it) made the propagation question concrete, and the user then proposed moving the choice onto the occurrence edit itself. This was better than the offered options and replaced them.

### On saving an occurrence edit, what scopes should the prompt offer?

| Option | Description | Selected |
|--------|-------------|----------|
| This / this + future, skipping touched ones | Second scope updates the template and rewrites only untouched future occurrences. | ✓ |
| This / this + future, overwriting everything ahead | Closer to Calendar's real behaviour; can wipe a deliberate one-off adjustment. | |
| This / this + future / whole series | Adds a scope that rewrites past occurrences. | |

**User's choice:** This / this + future, skipping touched ones.
**Notes:** Noted that Calendar's "all events" scope is out because past occurrences are a record of sessions that happened, and that "this and following" falls out without needing a series split. → D-09

### Can the cadence rule be changed after a series exists?

| Option | Description | Selected |
|--------|-------------|----------|
| No — end it and start a new one | Rule immutable. Every occurrence stays derivable from its own series' rule. | ✓ |
| Yes, additive-only as the roadmap locked it | Editable; existing occurrences keep their dates. | |
| Yes, and regenerate untouched future occurrences | EVTRECUR-09 verbatim — explicitly deferred. | |

**User's choice:** No — end it and start a new one.
**Notes:** Raised because additive-only permits a series whose early and late occurrences follow different rules with nothing recording the switch, so the series page would show dates its own stated cadence cannot produce. The chosen option is stronger than the roadmap's lock and is less code, not more. → D-07, and it also produced D-10 (no template edit form on the series page).

---

## Cancel / move / edit one occurrence

### How is a cancelled occurrence remembered?

*(Asked twice — the first answer addressed series-level cancellation rather than a single occurrence.)*

| Option | Description | Selected |
|--------|-------------|----------|
| Keep the row, mark it cancelled | Tombstone. One idempotency question for cancelled/moved/edited; answers survive; un-cancel is lossless. | ✓ |
| Delete the row, record the slot separately | Skipped-slots table. `Events` stays clean; generator consults two sources. | |
| Store cancelled slots on the series row | Second comma-delimited column; grows unbounded, unqueryable. | |

**User's choice:** Keep the row, mark it cancelled.
**Notes:** The user's first response described the overall architecture (a saved recurrence setting with a name, start date, nullable end date, mask; generation triggered on save; occurrences linked underneath). That model matched every decision already locked, and its `EndDate` refinement was adopted into D-11. The single-occurrence cancel question was then re-asked with a concrete scenario (a holiday falling on 21 November while the series keeps running) to distinguish it from ending the series. → D-14

### Does a moved occurrence get regenerated? *(user challenge, not a multiple choice)*

**User's question:** "are you sure a single moved event does not get regenerated like this?"
**Answer given:** No — but only if three things hold, and the shipped schema guaranteed none of them. (1) The existence check must be keyed on `(SeriesId, SeriesSlotIndex)`, never on date. (2) Nothing at the database level stops a duplicate slot: `IX_Events_SeriesId` is non-unique on `SeriesId` alone, while `IX_EventSignups_EventId_UserId` is `unique: true` — and `Program.cs:260` registers a global `AutomaticRetryAttribute { Attempts = 5 }`, so a partially-failed job re-runs from the top. (3) The existence query must carry no date predicate, because a far-moved occurrence can sit outside the runway window and would then read as a free slot.
**Notes:** All three were verified against `20260826134133_AddCalendarEventsFeature.cs` before answering. The challenge surfaced two real gaps, not hypotheticals. → D-18, D-19, D-20

### How does a cancelled occurrence appear to players?

| Option | Description | Selected |
|--------|-------------|----------|
| Shown, visibly cancelled | Struck through and muted on calendar and agenda; details page banner, no answer buttons. | ✓ |
| Hidden from calendar and agenda | Cleanest calendar; the date silently evaporates. | |
| Hidden from the calendar, link still resolves | Shared links explain themselves; others still see nothing. | |
| You decide | Planner discretion. | |

**User's choice:** Shown, visibly cancelled.
**Notes:** The user dismissed this question on first ask and sent the moved-occurrence challenge instead; it was re-offered after that was answered. → D-15

### What happens to Delete on a series occurrence?

| Option | Description | Selected |
|--------|-------------|----------|
| Replaced by Cancel, enforced server-side | Controller re-resolves `SeriesId` and refuses; one-off events keep Delete. | ✓ |
| Keep Delete, make it cancel underneath | No new control; the label lies about what happened. | |
| Offer both Cancel and Delete | Delete would free the slot, so the generator recreates the session. | |

**User's choice:** Replaced by Cancel, enforced server-side. → D-16

---

## The rolling window

### How far ahead should occurrences be materialized?

| Option | Description | Selected |
|--------|-------------|----------|
| 12 months | ~52 occurrences, ~312 signup rows on a six-member campaign board. | |
| 6 months | Half the rows and lag; tighter grace on a dead job. | |
| Rolling count — keep N occurrences ahead | Adapts to cadence rather than the calendar. | ✓ |

**User's choice:** Rolling count.
**Notes:** Presented with a sizing table. The count framing also makes the required health check one cadence-independent query. → D-21

### How many, and is it per-series?

| Option | Description | Selected |
|--------|-------------|----------|
| Global 20, config-overridable | ~5 months weekly, ~9 on two-on-two-off. Code default, no server env change needed. | ✓ |
| Global, but 26 | ~a year of weekly sessions; bigger fan-out and sweep. | |
| Per-series, chosen at creation | Maximum control; asks the DM an implementation question. | |
| Per-series, but optional | Global default with an advanced override. | |

**User's choice:** Global 20, config-overridable.
**Notes:** The user asked whether the runway should be selectable at creation and requested advice. Advice given against: EVTRECUR-03 exists specifically to remove this management burden; per-series thresholds make "has enough runway" mean something different per series, so a DM picking 3 reads as healthy while one failed job from empty; and nobody tunes a knob like this on a single group's board. Noted it is cheap to add later as a nullable column defaulting to the global. → D-22

### When a DM saves a new series, how much is generated right then?

*(Asked twice — the user proposed a third mechanism after the first round.)*

| Option | Description | Selected |
|--------|-------------|----------|
| The full runway, inline | Save writes all 20 occurrences and their signups; no lag. | |
| First occurrence inline, rest by the job | Series is visibly real; calendar wrong for up to a day. | |
| Nothing inline, the job does it all | One code path; calendar shows nothing after save. | |
| **Shared method, called directly** | **One Domain generator; controller calls it synchronously, job calls it on schedule.** | **✓** |
| Enqueue the job on save | Save enqueues the generator; matches how emails are dispatched. | |
| Direct call, plus enqueue as a safety net | Both paths; correctness rests entirely on idempotency. | |

**User's choice:** Shared method, called directly.
**Notes:** The user's proposal was "I want the job to do it all, but the save of the new series should trigger the job immediately — logic stays in one place". Advice given: the "one place" goal is satisfied by one Domain method with two callers, which enqueuing does not improve on; enqueuing separately costs a possible empty calendar after save (`WorkerCount = 2`), an invisible failure once the global 5 retries exhaust behind an already-shown success toast, and zero integration-test coverage because `Program.cs` skips Hangfire in the Testing environment. The user accepted the direct-call form of their own requirement. → D-24

### Where does the horizon check surface?

| Option | Description | Selected |
|--------|-------------|----------|
| DM-visible banner on the calendar | The page a DM already opens constantly. | ✓ |
| Banner on the series page only | Most contextual; only seen by someone already looking. | |
| Health check endpoint | Reaches monitoring, not the DM. | |
| Calendar banner and /health | Both surfaces, one query. | |

**User's choice:** DM-visible banner on the calendar. → D-26

### How should the top-up job be scheduled?

| Option | Description | Selected |
|--------|-------------|----------|
| Own daily recurring job, off-peak | Separate from the reminder sweep; a failed run self-heals the next night. | ✓ |
| Extend the existing daily reminder job | One job to look at; couples generation to email dispatch. | |
| Own job, weekly | Ample for a 20-session runway; a week between retries. | |

**User's choice:** Own daily recurring job, off-peak. → D-27

### With an anchor two months back, does the generator create sessions that already happened?

| Option | Description | Selected |
|--------|-------------|----------|
| No — only slots dated today or later | Past slots computed for numbering, never materialized. | ✓ |
| Yes — everything from the anchor forward | Full history on the calendar; manufactures attendance records. | |
| Ask at creation | Checkbox per series. | |

**User's choice:** No — only slots dated today or later.
**Notes:** Framed around why a DM sets a past anchor at all — to fix the phase of the rhythm, not to declare where records begin. → D-23

### The DM moves slot 9 onto slot 10's date. What happens?

| Option | Description | Selected |
|--------|-------------|----------|
| Allow, but say so in the save dialog | Rides in the scope prompt that already exists; cancelled siblings do not trigger it. | ✓ |
| Allow silently | Consistent with 74 D-19's refusal to restrict dates; no signal either way. | |
| Block the move | Forbids a genuine double session; first date restriction in the feature. | |

**User's choice:** Allow, but say so in the save dialog. → D-17

### How should the generator commit a run of twenty occurrences?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-occurrence, whole save wrapped | Job makes monotonic progress; controller path wraps series + generation in one transaction. | ✓ |
| One transaction for the whole run | No partial runway ever; a transient error at nineteen discards eighteen. | |
| Per-occurrence everywhere, no wrap | One rule both paths; a failed save leaves a short runway and an ambiguous error. | |

**User's choice:** Per-occurrence, whole save wrapped. → D-25

---

## Series removal *(added mid-discussion, after the four selected areas)*

**User's request:** "I want the ability to remove all related events together with the removal of a series setting."

Checked before answering: `FK_Events_EventSeries_SeriesId` declares no `onDelete`, so EF Core's optional-relationship default maps to `NO ACTION` — deleting a series today throws a FK violation rather than doing anything surprising. `FK_EventSignups_Events_EventId` is `Cascade`, so signups follow occurrences for free.

### What happens to a series' occurrences when it is removed?

| Option | Description | Selected |
|--------|-------------|----------|
| Delete everything, no alternative | Exactly as asked; one destructive path with one meaning. | |
| Offer delete or detach | Confirm offers both; detach nulls `SeriesId` and `SeriesSlotIndex`. | ✓ |
| Detach only, never delete the events | Nothing lost by accident; not what was asked for. | |

**User's choice:** Offer delete or detach.
**Notes:** Flagged as not being in EVTRECUR-01…08 — an operator addition in the same class as ending a series. → D-12

### What should the series-delete confirm count?

| Option | Description | Selected |
|--------|-------------|----------|
| Sessions split past/future, plus real answers | Uses `HasAnswered`; narrow stated divergence from 75 D-26. | ✓ |
| Follow D-26 — count every signup row | Strictly consistent; always fires at maximum volume. | |
| Sessions only, no signup count | Shortest; hides that other people's answers are destroyed. | |

**User's choice:** Sessions split past/future, plus real answers.
**Notes:** Raised as an explicit tension with Phase 75 D-26, which locked the single-event confirm to all signup rows and said not to "correct" it. That reasoning was scoped to one event; at series scale a fresh 20-occurrence series on a six-member board holds 120 auto-created rows before anyone has looked. The single-event dialog is left unchanged. → D-13

---

## Claude's Discretion

Not discussed; recorded in CONTEXT.md for the planner:

- Whether `EventSeries` gets its own domain model / repository / service triple, and whether the series page is a new controller or actions on `EventsController`
- Naming and type of the cancelled marker (bool vs nullable `CancelledAt`)
- Exact preview count, and whether the preview shows anything for a past-dated anchor
- Toggle-strip styling and any UI cap on cycle length
- The off-peak hour and Hangfire job id for the top-up job
- Wording of every confirm, banner, and toast
- Whether the D-09 scope prompt is a native `confirm()`, a two-button dialog, or radio buttons — the app's idiom is native `confirm()`, but that is binary and this prompt has two affirmative outcomes
- Index strategy beyond the mandated D-19 unique index
- Where the `DateOnly` → `DateTime` conversion seam sits for new view models
- Whether a detached occurrence that was cancelled keeps its marker or is deleted
- Test structure beyond the mandated two-group isolation test and the idempotency tests EVTRECUR-07 implies

## Deferred Ideas

- **Daily cadence** — cheap (cadence-unit field + `AddDays`), but no requirement asks and it is not a real pattern for this board
- **Monthly cadence, "nth weekday of every N months"** — the largest deferred item; the one shape RFC 5545's `BYSETPOS` handles natively, needing a second date-derivation path that stays slot-stable
- **Per-series runway override** — one nullable column defaulting to the global; addable later with no generator rework
- **An advanced mask editor beyond the toggle strip** — only relevant for very long cycles
- **EVTRECUR-09** — already deferred in REQUIREMENTS.md; D-07 makes it moot for cadence by forbidding rule edits

### Not deferred — a consequence Phase 77 must handle

- The availability overview grid must exclude cancelled occurrences. Under D-14 a cancelled session keeps its signup rows, so a naive join shows it as a date everyone said yes to.
