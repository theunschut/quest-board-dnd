# Feature Research: Calendar Events

**Domain:** Session/availability scheduling calendar feature for a self-hosted D&D campaign-management app (~17 trusted members)
**Researched:** 2026-08-25
**Confidence:** HIGH — grounded entirely in this codebase's existing entities, jobs, controllers, and locked v9.0 scope decisions. No external ecosystem unknowns; this is a behavioral-design problem, not a "what libraries exist" problem, so no web/docs research was needed beyond reading the app itself.

Operator-locked scope (from the milestone brief) is treated as given, not re-litigated. This document answers the *behavioral* questions that scope leaves open, grounded in QuestBoard's own precedents: the `Quest`/`ProposedDate`/`PlayerSignup`/`PlayerDateVote` voting model, `BoardType.OneShot`/`Campaign` branching, the `IMarkdownService` pipeline, Hangfire job patterns (`DailyReminderJob`, `QuestDateChangedEmailJob`), and the `SAFE-01`/`Phase 41` membership-removal precedent ("remove the live row, never touch history").

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features required for the operator-locked scope to actually function end to end.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Event CRUD (Title, Date, optional Description/Location) | The whole feature is pointless without a way to create one | LOW | Mirrors `QuestController.Create`/`Edit` shape; `DungeonMasterOnly` policy, same as Create Quest |
| Event appears on the existing Calendar (desktop grid + mobile agenda) | Operator explicitly scoped this as "calendar views only" | MEDIUM | `CalendarViewModel`/`_Calendar.cshtml`/`Calendar/Index.Mobile.cshtml` all need an `EventsOnDay` sibling to `QuestsOnDay` |
| Visual distinction from Quests in a shared day cell | A day can already hold multiple quests; adding an indistinguishable second entity type breaks scannability | LOW-MEDIUM | New CSS class family (`.calendar-event-entry`), not a new field — see Q5 |
| Opt-in signup (One-Shot) / opt-out signup (Campaign) using `VoteType` | Explicitly locked scope; this is the entire point of the availability feature | MEDIUM | Reuses `VoteType.{No,Maybe,Yes}` verbatim — zero new enum |
| Auto-signup-on-materialization for Campaign boards | Locked scope: "every board member is auto-signed-up... to Yes" | MEDIUM-HIGH | Must run at occurrence-creation time, not just event-creation time (recurrence interacts here — see Q3, Phase split) |
| Recurrence: cadence + cycle mask + anchor date, materialized as real rows | Explicitly locked scope | HIGH | Single largest ticket in the whole feature; isolate into its own phase (see MVP Definition) |
| Occurrence-level cancel/move/edit independent of the series | Explicitly locked scope | MEDIUM | Needs an `EventOccurrence` row that can diverge from its parent series without mutating the series |
| Availability overview page (events × players grid) | Explicitly locked scope, new page | MEDIUM | See Q8 for shape recommendation |
| Event creation restricted to DM-tier roles, same nav category as Create Quest | Explicitly locked scope | LOW | `DungeonMasterOnly` policy (DungeonMaster/Admin/SuperAdmin via existing role resolution), nav entry beside "Create Quest" |

### Differentiators (Valuable, Not Required for Launch)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Cancel/move email notification | A moved/cancelled session invalidates a player's already-given "Yes" — genuinely different from a cosmetic edit (see Q9) | MEDIUM | Direct precedent exists: `QuestDateChangedEmailJob` already does this for finalized One-Shot quests |
| Below-threshold highlighting on the overview grid ("4 of 6 available" in warning color) | Turns a passive grid into an actionable "this session is at risk" signal — the actual reason a DM would check the page | LOW | Pure display logic once the grid data exists |
| Event → Quest linking (optional reference, not FK-required) | Lets a DM tie a Campaign session event to a specific Quest instance for continuity | LOW-MEDIUM | Explicitly NOT required by locked scope ("informational... never blocks") — see Q10 |
| Configurable materialization horizon / on-demand "extend now" admin action | Operational safety valve if the 12-month rolling window ever needs a manual nudge (e.g., after a long outage) | LOW | Cheap add-on once the Hangfire job exists (mirrors `DailyReminderJob`'s manual-trigger precedent, Phase 22) |

### Anti-Features (Would Seem Reasonable, Actively Avoid)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| Events blocking Quest creation on a busy day | "Company BBQ" naturally reads as a scheduling conflict to prevent | Operator explicitly locked this as **informational only, never blocking** — enforcing it server-side would silently violate a stated design decision and remove a legitimate use case (DM posting a Quest anyway, aware of the conflict) | Show the busy day as a visual warning on the calendar only, never a hard stop |
| Events on the main quest-board page | Feels consistent with "show everything important" | Operator explicitly locked "never appear on the quest board main page" — the quest board's whole design principle (Core Value: "DMs post quests, players sign up — everything else enhances that loop") is protected by keeping non-actionable info out of it | Calendar page + overview page only |
| Full RRULE-style recurrence editor (day-of-month, Nth-weekday, multiple weekdays, etc.) | "Might as well support everything iCal does" | Massively higher UI/data-model complexity for a use case that is, in practice, "every 1 or 2 weeks on a fixed weeknight, occasionally skip a week" — the exact shape the operator already locked (cadence + cycle mask + anchor) | Ship exactly the locked cadence+mask+anchor model; do not gold-plate |
| Per-edit-type email notifications (any field change fires mail) | Feels "complete" and matches how some SaaS calendar tools behave | Direct, repeated precedent against this in this codebase: **no** existing edit-type feature (Quest Edit, Recap Edit, Campaign quest Close/Reopen) fires email; the relay is hard rate-limited (100/day, 3000/month) for 17 members | Reserve notification for date-changing/cancelling events only, and even then treat as a differentiator to add once real usage justifies the email budget (see Q9) |
| Hard FK link from Event/Occurrence to Quest (mandatory relationship) | Seems like natural modeling — "the session IS the quest" | Locked scope says informational/non-blocking; forcing a required FK would mean every Campaign session needs a Quest row to exist first, re-introducing exactly the coupling the operator is avoiding by keeping Events independent | Optional nullable reference only, and only if/when a DM actually asks for it (see Q10) |
| DM-only visibility of the availability grid/overview | Superficially "protects" scheduling info from players | Directly contradicts this app's established openness for anything self-coordination-related — Quest rosters, waitlists, and the Characters directory are all visible to the whole group; hiding availability defeats the overview page's actual purpose (players self-organizing around a session date) | Visible to all board members; DM-only gate only the *creation/edit* actions, which is already locked scope |

---

## Behavioral Design Decisions (Q1–Q10)

### Q1 — What fields does an Event need?

**Recommendation:**

| Field | Required? | Type | Rationale |
|---|---|---|---|
| Title | Required | `string(200)` | Mirrors `QuestEntity.Title` exactly (`[Required][StringLength(200)]`) |
| Description | Optional | `string` via `IMarkdownService` | It's the 10th free-text field — the v8.0 milestone built exactly one shared Markdown pipeline "used identically" everywhere for this reason. Making it *optional* (unlike `ShopItemEntity.Description`, the one `[Required]` precedent) is the right call here: a "company BBQ" blocker needs zero prose, a campaign session might just say "Session 12." |
| Date (+ time) | Required | single `DateTime` | Follow `ProposedDateEntity.Date`'s exact shape — one combined date+time value, not split columns. A time genuinely is needed for the Campaign/session use case (players need to know when to show up, and the existing calendar already renders `HH:mm` for quests); for the One-Shot blocker use case the time is simply ignorable in the UI. Don't special-case it into two field shapes — one `DateTime` field serves both. |
| End time / Duration | Optional | nullable `TimeSpan` or `DateTime? EndDate` | Not needed for the blocker use case; valuable for sessions so the calendar can render "20:00–23:00" the way it already renders quest start times. Keep optional — don't block Event creation on it. |
| Location | Optional | `string` | Table stakes for physical sessions but genuinely optional (many groups play in the same place every time, or online). Do **not** wire it to the existing `HasKey`/building-key warning-icon system (`_Calendar.cshtml`'s `anyoneHasKey` logic) — that's Quest-specific domain logic about physical-key holders and doesn't generalize to a free-text location string without real design work. Plain text field only, v1. |

**Sizing:** all of this is LOW-MEDIUM complexity — it's a smaller field set than `QuestEntity` (no `ChallengeRating`, no `TotalPlayerCount`, no `DungeonMasterSession`).

### Q2 — Event types/categories: does "company BBQ" vs "campaign session" need a visible type field?

**Recommendation: No separate `EventType` field — the meaning is derived entirely from `BoardType`, same as everything else Quest-related already works.**

Rationale: this project already has the exact precedent for "one entity, meaning branches on `BoardType`" — `QuestEntity` doesn't carry a "is this a campaign quest or one-shot quest" flag; the *board* the quest lives on determines whether Close/Reopen or Finalize/Vote applies (Phase 36's `CloseQuestAsync`/`ReopenQuestAsync` vs `FinalizeQuestAsync`/`OpenQuestAsync`, kept structurally separate specifically to avoid a "which mode am I in" flag). A group's `BoardType` is immutable after creation (Phase 35), so an Event never needs to answer "am I a blocker or a session" independently of the board it's on — the board already answers that, permanently, for every event on it.

Adding a redundant `EventType` field here would be the anti-pattern this codebase has repeatedly paid down (see: `BoardType` lookup triplication flagged as tech debt in `PROJECT.md`'s Known Issues) — a second source of truth that can drift from the board's actual type.

**Visual distinction still needed — but from Quests, not from other Events on the same board:** since a board only ever has one Event "flavor," the real visual problem is telling an Event apart from a Quest in a shared day cell (see Q5), not telling two Event types apart from each other.

**Explicit operator flag:** if a future need arises for a *mixed-purpose* board (e.g., a Campaign DM occasionally wants a pure "no session, holiday" blocker note that shouldn't auto-signup anyone), that's a genuine differentiator requiring an actual type field — but nothing in the locked scope asks for it, and speculatively building it now would be exactly the kind of anti-feature this table flags above. Defer until requested.

### Q3 — Membership changes vs auto-signup

This is the highest-risk data-model question in the whole feature; get it wrong and stale rows silently misrepresent who's actually coming.

**New member joins after 20 occurrences are already materialized:**
Recommendation: auto-create Yes signup rows for every **future** (not-yet-occurred) materialized occurrence at the moment they join, and explicitly do **not** backfill past occurrences. Rationale: this mirrors the "auto-signup = default assumed availability going forward" semantics the operator already locked for Campaign boards — a new member wasn't part of the group when those past sessions happened, so a retroactive "Yes" would falsify the historical availability record the overview page and any future reporting would read.

**Member leaves the board:**
Recommendation: two different rules for past vs. future occurrences, both directly modeled on the `Phase 41` (`SAFE-01`) precedent — *"account, other memberships, characters, and quest/shop/transaction/reminder history are all untouched"* when a member is removed from a group.
- **Past occurrences:** preserve the signup row untouched. It's a factual historical record ("this person said Yes/No to session 12"), exactly like Quest signup history isn't touched by member removal today.
- **Future occurrences:** this is a genuinely new judgment call, not directly precedented — Quest signups were never auto-deleted on member removal because a Quest signup represents a real, deliberate commitment a DM manages. An Event auto-signup is different: it's a *default*, not a deliberate act, and leaving it in place would show a departed member as "attending" a session they can no longer join. **Recommendation: remove (not just hide) the departed member's signup rows for occurrences that haven't happened yet.** This keeps the overview grid honest without touching history. Flag this explicitly to the operator as a deviation from the strict "never touch on removal" precedent, justified because auto-signups are structurally different from deliberate Quest signups.

**Complexity:** MEDIUM-HIGH. This logic needs to run from two triggers (join/leave via `GroupController`/`AdminController`'s existing member-management actions) and must be scoped per-board (a member can be in multiple groups; only touch that board's occurrences).

### Q4 — Who can see an event's availability?

**Recommendation: all board members see everyone's vote, not just their own — and this is not DM-gated.**

Rationale: the app's existing self-coordination surfaces (Quest rosters/waitlists showing who's selected, the Characters/Contacts directories) are visible to the whole group; only *management actions* are DM-gated, not *visibility* of who's doing what. An availability overview whose whole purpose is "can we actually get a quorum for Saturday" is useless if players can't see each other's answers — that's the entire value proposition of Q8's overview page. Gating it to DM-only would mean a player literally cannot self-organize around a session date, which contradicts the stated purpose of the feature ("players sign up... to indicate availability").

**Operator flag (legitimate alternate view):** a DM might prefer to privately gauge interest before committing to a date, the way some real-world scheduling tools hide responses until the organizer reveals them. Nothing in the locked scope asks for this, and it would add a visibility-state field this app has no precedent for on anything Quest/Event-shaped (Contacts' hidden/reveal precedent is DM-authored-content-specific, not response-privacy). Recommend the open-by-default design above; flag the private-poll alternative explicitly as an operator decision if it turns out DMs want it.

### Q5 — What does the calendar cell show, given crowding risk?

Grounded in the real code, not assumption: the desktop grid (`_Calendar.cshtml`) already caps quest entries at `Take(3)` per cell with a "+N more" overflow, and each quest entry is a fairly heavy block (title, DM name, time, status icons, vote buttons). The mobile view (`Calendar/Index.Mobile.cshtml`) is a genuine agenda list — one section per day-with-quests, no 3-item cap, each quest a single-line entry ("Title — HH:mm").

**Recommendation:**
- **Desktop grid:** add a visually lighter `.calendar-event-entry` — a compact chip (icon + time, no DM name, no vote buttons in the cell itself) rendered in its own sub-list beneath the existing `.quest-events` block, sharing one combined "+N more" overflow count with quests rather than a second independent cap. Quests render first (they're the primary purpose of the board per Core Value); events are secondary, at-a-glance context.
- **Mobile agenda:** extend the `agendaDays` filter (`Model.GetCalendarDays().Where(d => !d.IsEmpty && d.QuestsOnDay.Any())`) to also include days with events (`|| d.EventsOnDay.Any()`), and add a second `agenda-event-entry` block per day-section, styled distinctly (e.g., muted/outline style vs. the quest entries' filled style) so a day with "1 quest + 1 event" reads immediately as two different kinds of thing, not a list of four interchangeable rows.
- **Icon convention:** a distinct icon (e.g., `fa-calendar-day` or `fa-users` for events vs. the quest board's existing scroll/sword iconography) plus a muted/grey treatment for One-Shot "blocker" events specifically communicates "informational, not clickable-into-a-signup-flow" at a glance — consistent with how `_Calendar.cshtml` already uses icon-only status indicators (`fa-exclamation-triangle`/`fa-check-circle`) rather than extra text.

**Complexity:** LOW-MEDIUM — this is CSS/partial work on top of the existing `CalendarViewModel`/`_Calendar.cshtml`/`Calendar/Index.Mobile.cshtml` structure, not a new pattern.

### Q6 — Should a cancelled occurrence still appear on the calendar?

**Recommendation: still appear, visually struck-through/"Cancelled," not vanish.**

Rationale: this is the same logic the app already applies elsewhere — deletion vs. soft-state. `CharacterStatus.Dead`/`Retired` don't delete the character row; `QuestEntity.IsClosed` doesn't delete a Campaign quest; a removed group member's history rows survive (Q3/Phase 41). A cancelled occurrence that people already voted on carries the same historical-integrity concern: if it vanishes, the overview page's "did we actually meet last month" record silently disappears, and any player who voted Yes/No has no way to see what happened to the session they responded to. Struck-through + "Cancelled" label costs almost nothing (one boolean + one CSS class) and preserves the record.

**Complexity:** LOW.

### Q7 — Moving an occurrence: do votes travel with it, or reset?

Argued both ways, as requested:

- **Votes travel with it:** the person is the same, their general "I'm usually free" signal is roughly stable, and forcing everyone to re-vote for a one-day shift feels like unnecessary friction in a 17-person trusted group where people don't want busywork.
- **Votes reset:** a "Yes" for Saturday is not evidence of availability for Sunday — day-of-week availability is exactly the kind of thing that varies (work schedules, other commitments). Silently carrying a stale "Yes" forward risks a DM believing they have quorum when they don't, which is the one failure mode this whole feature exists to prevent.

**Recommendation: reset votes on move, but only for a genuine date change (different day); a same-day time-only edit (e.g., 19:00 → 20:00) should preserve votes.** Rationale: the risk profile is asymmetric — a stale "Yes" that turns out wrong silently undermines the DM's confidence in the exact number the overview page (Q8) is designed to surface, while re-voting costs a player one tap using the same Yes/Maybe/No control they already use for Quest signups. This also matches the *closest* real precedent in the codebase, `QuestDateChangedEmailJob` — the app already treats "the date changed" for a finalized quest as a distinct, notification-worthy event, not a silent edit; resetting votes is the natural data-side analog of that same "this is a materially different thing now" judgment.

**Complexity:** LOW — a `Moved` occurrence just clears its signup rows and, for a Campaign board, re-runs the same auto-signup-to-Yes routine the original materialization used (see Q3).

### Q8 — Overview page shape

**Recommendation:**
- **Rows = events (upcoming occurrences), columns = players.** Rationale: a DM scans down a small number of upcoming sessions far more often than they scan across all group members; rows-as-events keeps the page short and lets each row's rightmost cell hold the "4 of 6 available" summary without horizontal scrolling on a page with 17 columns.
- **Default time range:** upcoming occurrences only (no past history on this page — that's what the calendar itself is for), capped to a sensible window rather than "all future materialized rows" (which, at a 12-month materialization horizon, could be 50+ rows for a weekly campaign). Recommend defaulting to the next 8–12 upcoming occurrences or a rolling 60–90 day window, whichever is smaller, with the same month-navigation affordance the Calendar page already has if the operator wants deeper lookback/lookahead.
- **Per-event count + threshold highlight:** yes — "4 of 6 available" (Yes count / total board members, per Q3's future-signup semantics) with a warning color when it drops below a configurable-but-simple threshold (recommend a flat "less than half" default rather than exposing a settings UI — this app has no precedent for per-feature threshold configuration, and one is not worth building for 17 users). This is the single feature that actually justifies the page's existence — without it, it's just a bigger, slower version of the calendar.
- **DM-only?** No — see Q4. Visible to all board members, same reasoning.

**Complexity:** MEDIUM — new controller/view/viewmodel, but no new data beyond what Q2/Q3's signup rows already produce; the complexity is entirely in the grid-rendering and threshold logic, not new persistence.

### Q9 — Does an event ever need to notify anyone?

The brief is right that the precedent is *mostly* against email — no edit-type feature in this app fires mail, and Campaign boards specifically were engineered (`Phase 36`, `CloseQuestAsync`/`ReopenQuestAsync` kept structurally separate from the emailing `FinalizeQuestAsync`/`OpenQuestAsync`, "by construction rather than by a conditional check") to guarantee *zero* quest-related email ever fires for a campaign-group action. That's a strong, deliberate signal that this operator wants Campaign boards quiet.

**But it's not a clean "no" — there is a direct, on-point counter-precedent:** `QuestDateChangedEmailJob` exists today and fires *specifically* when a finalized One-Shot quest's date changes, sent to every already-committed player. That is the exact same shape of problem Q7 is describing for a moved occurrence — "you said yes to a date that no longer exists." The app has already decided, once, that this specific case (not a generic edit, a *date change on something people committed to*) crosses the bar for email.

**Honest recommendation:** cancel/move of an occurrence that already has non-trivial signup activity (i.e., someone besides the auto-signup default has actually responded, or — for Campaign boards — simply any occurrence, since every member is "signed up" by default) is the one case worth notifying on, reusing `QuestDateChangedEmailJob`'s pattern almost verbatim for "moved" and a small new `EventCancelledEmailJob` for "cancelled." Everything else (title/description/location edits, creating a new event, a One-Shot player's own opt-in vote) stays silent, matching the app's blanket no-email-on-edit precedent.

**Given the explicit ask to argue honestly rather than default to "no":** the counter-argument for *still* saying no even to cancel/move is real and worth stating plainly — Campaign boards can plausibly run a weekly session, and if life happens and a DM needs to move or cancel 2-3 sessions in a month, that's 2-3 × ~15-17 recipients = 30-51 emails eaten from a 100/day, 3000/month budget shared with every other feature in the app, for a 17-person group that could just as easily see the change by opening the calendar. **This is a genuine trade-off, not a settled call** — recommend treating it as a Phase 2+ differentiator (see MVP Definition) rather than blocking Phase 1 launch on it, and confirming with the operator once real Campaign-board usage patterns are known rather than guessing budget impact up front.

### Q10 — Interaction with quests: does a Campaign event relate to a Quest entity?

The operator's own framing ("informational only... never blocks... never appears on the quest board") already answers the data-model question: **no required relationship.** An Event/Occurrence must be fully independent — no FK from `QuestEntity` to an Event, no FK requirement the other direction. This is consistent with keeping the two entities' lifecycles uncoupled: a Campaign board can close a Quest via `CloseQuestAsync` without any Event awareness, and an Event can be cancelled without touching any Quest.

**Would a DM ever want to link one?** Plausibly yes, as a convenience — "Session 12" the calendar Event and the actual Quest record covering that session's recap/rewards are conceptually the same evening, and a DM might want one click from the availability grid to the Quest they're going to close afterward. **Recommend an optional, nullable `QuestId` reference on the Event/Occurrence (DM sets it manually, never inferred/required)** — purely a convenience navigation link, never read by any authorization, blocking, or notification logic. This is explicitly a differentiator (Feature Landscape table), not table stakes: locked scope doesn't ask for it, and it adds zero value until a DM has actually created both records and wants to connect them after the fact.

---

## Feature Dependencies

```
Event CRUD (non-recurring, single occurrence)
    └──requires──> Calendar display support (EventsOnDay on CalendarViewModel)

Availability signup (VoteType reuse)
    └──requires──> Event CRUD
    └──requires (Campaign auto-signup)──> current board membership list (UserGroupEntity)

Recurrence (cadence + cycle mask + anchor + materialization)
    └──requires──> Event CRUD (recurring event is a superset: series + occurrences)
    └──enhances──> Availability signup (auto-signup must re-run per materialized occurrence, not just once)

Membership-change sync (join/leave auto-signup handling)
    └──requires──> Availability signup
    └──requires──> Recurrence (future-occurrence sync is meaningless without materialized future rows)

Occurrence cancel/move
    └──requires──> Recurrence (a non-recurring event's "cancel" is just delete; "move" is just edit)
    └──enhances──> Availability signup (Q7's vote-reset-on-date-change logic)

Availability overview page
    └──requires──> Availability signup (needs real vote data to render)
    └──enhances (materially, not strictly)──> Recurrence (a handful of one-off events makes a thin, low-value grid; recurring Campaign sessions are the real use case)

Cancel/move notifications
    └──requires──> Occurrence cancel/move
    └──conflicts (softly)──> Campaign-board "no scheduling email" precedent — see Q9, treat as operator-confirmed opt-in, not a default

Event ↔ Quest optional link
    └──enhances──> Event CRUD (pure convenience field, no functional dependency either direction)
```

### Dependency Notes

- **Recurrence requires Event CRUD:** a recurring event is architecturally a series header + N materialized occurrence rows; you cannot build the materialization job before the base entity shape exists.
- **Membership-change sync requires Recurrence:** Q3's "new member joins after 20 occurrences are materialized" scenario is *only meaningful* once occurrences are actually pre-materialized rows — with non-recurring events there's nothing to sync against future dates.
- **Overview page enhances (not strictly requires) Recurrence:** it can technically render against non-recurring events alone, but the entire "4 of 6 available, here's the risk" value proposition assumes an ongoing cadence of Campaign sessions, which only exists once recurrence ships.
- **Cancel/move notifications conflicts (softly) with the Campaign no-email precedent:** flagged, not blocking — this is exactly the kind of judgment call Q9 argues honestly rather than resolves unilaterally; sequence it late and confirm with the operator once real usage exists.

---

## MVP Definition — Proposed Phase Split

The operator has already stated this ships across multiple phases. Recommended split, in dependency order:

### Phase EVT-1: Event data model + non-recurring CRUD + calendar display
*Ships alone, is immediately useful.*
- `EventEntity` (Title, Description via `IMarkdownService`, Date, optional EndTime/Duration, optional Location, `GroupId`)
- `EventsController` (Create/Edit/Delete under `DungeonMasterOnly`, nav entry beside "Create Quest")
- `CalendarViewModel` gains `EventsOnDay`; `_Calendar.cshtml` (desktop grid) and `Calendar/Index.Mobile.cshtml` (mobile agenda) render events distinctly from quests (Q5)
- No recurrence, no signup/voting yet — a DM can post a single "company BBQ" or a single dated "Session 12" and see it on the calendar
- **Why first:** delivers the operator's core complaint (no way to mark a busy day / post a session date) with the lowest-risk slice of the feature, mirroring this project's own established pattern of shipping a thin vertical slice before expanding it (`Phase 35` BoardType config → `Phase 36` Campaign quest behavior).

### Phase EVT-2: Availability signup + membership sync (still non-recurring)
- `EventSignupEntity` (EventId, PlayerId, Vote via `VoteType`, SignupTime) — reuses `VoteType` verbatim, no new enum
- One-Shot: fully opt-in signup UI (Yes/Maybe/No), mirroring the existing Quest vote-button partial pattern
- Campaign: auto-signup-to-Yes at event creation time; opt-out flips the row to No (row is never deleted by the player)
- Membership join/leave handling for the (still-non-recurring) event set (Q3)
- **Why second:** proves the harder opt-in/opt-out + auto-signup logic against the simplest possible occurrence shape (one row, not N materialized rows) before recurrence multiplies the surface area.

### Phase EVT-3: Recurrence (cadence + cycle mask + anchor date) + materialization job
*Highest complexity — isolate it.*
- Series/occurrence split: `EventEntity` becomes the series definition (cadence, cycle mask, anchor date), new `EventOccurrenceEntity` rows are the real, individually-editable materialized dates
- Hangfire CRON materialization job extending a rolling ~12-month window, directly modeled on `DailyReminderJob`'s existing recurring-job pattern (`HangfireJobHelper` DI-scope precedent)
- Occurrence-level cancel (Q6, struck-through not deleted) and move (Q7, date-change resets votes / time-only edit preserves them)
- Phase EVT-2's signup/auto-signup logic now runs per-materialized-occurrence, and Phase EVT-2's membership-sync logic now has real future rows to act on (Q3's "20 occurrences already materialized" scenario only exists from this phase onward)
- **Why third, not first:** this is where genuine risk lives (materialization correctness, occurrence-vs-series data integrity); shipping EVT-1/EVT-2 first means recurrence lands on top of already-verified CRUD and signup logic instead of everything landing at once.

### Phase EVT-4: Availability overview page
- New page: events (rows) × players (columns), upcoming-only window, per-event Yes-count + below-threshold highlight (Q8)
- **Sequenced after EVT-3, not before:** the page is technically buildable against EVT-2 alone, but its actual value (spotting an at-risk *recurring* session) only exists once Campaign sessions are genuinely recurring — shipping it earlier would ship a thin, low-value grid.

### Phase EVT-5 (differentiator, defer-eligible): Cancel/move email notifications
- `EventCancelledEmailJob` / an `EventDateChangedEmailJob` reusing `QuestDateChangedEmailJob`'s exact pattern, scoped to occurrences with real (non-default) signup activity
- **Explicitly the one item this document does not recommend committing to a phase number up front** — Q9's trade-off (rate-limit budget vs. the genuine value of not silently stranding a "Yes"-voter) should be resolved with the operator once EVT-3 has shipped and real Campaign-board cancel/move frequency is known, not guessed at during initial roadmapping.

**What must ship together vs. what can follow later:**
- EVT-1 and EVT-2 are each independently shippable and each deliver real user value alone — do not force them into one phase.
- EVT-3 must not ship before EVT-1/EVT-2 (recurrence has nothing to recur without the base entity and signup shape existing first).
- EVT-4 should follow EVT-3, not precede it, for value-density reasons (not a hard technical dependency).
- EVT-5 is the one phase explicitly safe to defer indefinitely without leaving the feature "incomplete" — the operator's own locked scope never asked for notifications, Q9 raises it as an honest trade-off, not a requirement.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Event CRUD + calendar display (EVT-1) | HIGH | LOW | P1 |
| Availability signup + auto-signup (EVT-2) | HIGH | MEDIUM | P1 |
| Recurrence + materialization (EVT-3) | HIGH | HIGH | P1 |
| Occurrence cancel/move | HIGH | LOW (bundled in EVT-3) | P1 |
| Membership-change sync | MEDIUM-HIGH | MEDIUM | P1 |
| Availability overview page (EVT-4) | HIGH | MEDIUM | P1 |
| Visual type distinction from Quests | MEDIUM | LOW | P1 (bundled in EVT-1) |
| Below-threshold highlight on overview | MEDIUM | LOW | P2 |
| Cancel/move email notifications (EVT-5) | MEDIUM | MEDIUM | P2 |
| Event ↔ Quest optional link | LOW-MEDIUM | LOW | P3 |
| Configurable overview threshold / date range settings | LOW | LOW-MEDIUM | P3 |
| Blocker vs. session `EventType` field | LOW (redundant with `BoardType`) | LOW | Not recommended — see Q2 |

**Priority key:**
- P1: Must have — directly implements locked v9.0 scope
- P2: Should have, add once P1 has shipped and real usage patterns are known
- P3: Nice to have, only if explicitly requested later

---

## Sources

- `C:\Repos\quest-board\.planning\PROJECT.md` — Key Decisions, Requirements, Constraints, milestone history (primary source for every precedent cited above)
- `C:\Repos\quest-board\QuestBoard.Service\ViewModels\CalendarViewModels\CalendarViewModel.cs`
- `C:\Repos\quest-board\QuestBoard.Service\Views\Shared\_Calendar.cshtml` (desktop grid partial)
- `C:\Repos\quest-board\QuestBoard.Service\Views\Shared\_Calendar.Mobile.cshtml` (Quest Details per-date voting partial — not the main mobile calendar, see note below)
- `C:\Repos\quest-board\QuestBoard.Service\Views\Calendar\Index.cshtml` / `Index.Mobile.cshtml` (actual desktop grid host and mobile agenda-list view)
- `C:\Repos\quest-board\QuestBoard.Repository\Entities\{QuestEntity,PlayerSignupEntity,ProposedDateEntity,PlayerDateVoteEntity,GroupEntity,UserGroupEntity}.cs`
- `C:\Repos\quest-board\QuestBoard.Domain\Enums\{VoteType,SignupRole,BoardType}.cs`
- `C:\Repos\quest-board\QuestBoard.Service\Jobs\QuestDateChangedEmailJob.cs` (direct precedent for Q7/Q9's date-change/move notification recommendation)
- `C:\Repos\quest-board\QuestBoard.Service\Jobs\DailyReminderJob.cs` (recurring Hangfire CRON precedent for Q3/EVT-3's materialization job)

**Correction to the research brief's framing:** the "mobile agenda-style calendar" referenced in Q5 is `Calendar/Index.Mobile.cshtml`, not `_Calendar.Mobile.cshtml` — the latter is actually a Quest Details-scoped partial rendering per-date vote buttons, reused via the same `_Calendar`-prefixed naming convention but serving a different page. Verified by reading both files directly rather than assuming from filename similarity.

---
*Feature research for: Calendar Events (v9.0 milestone)*
*Researched: 2026-08-25*
