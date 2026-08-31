# Phase 76: Recurring Event Series - Context

**Gathered:** 2026-08-28
**Status:** Ready for planning

<domain>
## Phase Boundary

A Dungeon Master defines a repeating schedule on their board — a base cadence of every N weeks plus a repeating on/off cycle mask anchored to a date — sees a live preview of the dates it will produce, and gets those dates materialized as real `EventEntity` occurrences on a rolling runway that a nightly job tops up, so an open-ended campaign never needs manual re-extension. Any single occurrence can be cancelled, moved, or edited without disturbing the rest, and re-running the generator never duplicates, resurrects, or overwrites anything.

**This phase is not pure code.** Phase 74 D-02 asserted that "Phases 75 and 76 then become pure code changes with no schema work." That held for 75. It does not hold here, and the gap was found by reading the shipped schema rather than assumed:

1. `EventSeriesEntity` has no `Title`, `Description`, or `StartTime` — nothing tells a generated occurrence what to be called (→ D-08).
2. There is no way to mark an occurrence cancelled. Phase 74 D-18 chose hard delete and explicitly deferred the concept to this phase; that bill is now due (→ D-14).
3. `IX_Events_SeriesId` is on `SeriesId` alone and **not unique**. The roadmap's locked idempotency key has no database-level enforcement (→ D-19). `EventSignups` did get its unique index; `Events` did not.
4. There is no `EndDate` on the series, so a finished campaign generates forever (→ D-11).

Not in this phase: the cross-event availability grid and its untouched-vs-real rendering (Phase 77, EVTVIEW-01…04); regenerating untouched future occurrences when a rule is edited (EVTRECUR-09, deferred — and made moot for cadence by D-07); any change to quest signups or date votes; any change to how one-off events behave.

</domain>

<decisions>
## Implementation Decisions

### Series definition and cadence

- **D-01: A cycle-mask position is one cadence step, not one calendar week.** The generator is exactly:

  ```
  date(N)  = AnchorDate + (N × IntervalWeeks) weeks
  fires(N) = CycleMask[ N mod CycleMask.Length ] == 1
  ```

  Every slot has a date whether or not it fires; the mask decides which become events. "Two on, two off" weekly is `IntervalWeeks = 1`, mask `1,1,0,0`. `IntervalWeeks` sets the grid spacing and the mask carves a rhythm from it, so fortnightly-forever is interval 2 + mask `1` rather than a longer mask on a weekly grid.

  **`SeriesSlotIndex` therefore counts every step including the ones that do not fire.** This is what makes the locked `(SeriesId, SeriesSlotIndex)` idempotency key work: slot 4 means "the fifth candidate from the anchor" permanently, even after a DM drags it to another weekday. Under a calendar-week reading, slot→date needs a walk through the mask and lengthening a mask silently renumbers every existing occurrence.

  It is also what satisfies EVTRECUR-08 structurally rather than arithmetically: two boards on the same anchor and interval with masks `1,1,0,0` and `0,0,1,1` share one date grid and no slot fires for both.

- **D-02: Weekly grid only.** No daily, no monthly. This is EVTRECUR-01 as written. Daily would be cheap (a cadence-unit field and `AddDays`); monthly ("nth weekday of every N months") is the genuinely useful one and is the single recurrence shape RFC 5545's `BYSETPOS` handles natively — a fact the roadmap's no-library argument did not account for. Both are deferred rather than dismissed.

- **D-03: The mask is entered as a clickable toggle strip** with controls to lengthen and shorten the cycle, so the DM sees the rhythm as a shape and can build any pattern the column stores. Chosen over an "N on / M off" number pair, which cannot express a rhythm that alternates inside the cycle (`1,0,1,0,0`), and over a raw comma-separated field, which exposes the storage format. **The mask has no fixed length** — `1`, `1,1,0,0`, and `1,1,1,0,0` are all valid. The `nvarchar(200)` column caps out around 100 positions; validate on input so a paste cannot silently truncate.

- **D-04: `WeekDay` is derived from `AnchorDate` and written on save, never independently editable.** Under D-01 every generated date lands on the anchor's own weekday, so the shipped column is a stored duplicate that can only ever be wrong. The form takes a start date and displays the weekday back as derived text ("every 2 weeks on Saturdays"). The column is kept — deriving on save costs nothing and removes the two-sources-of-truth hazard entirely.

- **D-05: The live preview is computed server-side by a debounced fetch, running the same Domain generator that later materializes occurrences.** The preview cannot disagree with what gets created because it is the same code. Fits the existing `fetch`-POST idiom (`Views/Quest/Details.cshtml:966`). A JavaScript reimplementation was rejected: it is a second copy of the rule that decides real dates, and PROJECT.md blames exactly that duplication class for four recorded bugs — a drift here would show correct dates and create different ones.

- **D-06: Series setup is a "repeats" toggle on the existing DM-gated Create Event form**, revealing cadence, mask, and preview when switched on. One entry point, one navbar item, and a one-off event stays the simple path Phase 74 shipped. **Plus a series detail page**, reached from any occurrence's Details view, showing the rule read-only, the occurrences it produced, and the End and Delete controls.

- **D-07: The cadence rule — anchor, interval, mask — is immutable after creation.** Changing the rhythm means ending the series (D-11) and creating a new one, a path this phase already builds. This is **stronger than the roadmap's "rule edits are additive only" lock and supersedes it for cadence**, because additive-only permits a series whose early and late occurrences follow different rules with nothing recording the switch: change `IntervalWeeks` from 2 to 3 and slot 10 sits where the old rule put it while slot 20 follows the new one, so the series page shows dates its own stated cadence cannot produce. Immutability is also *less* code, not more.

### Occurrence content and edit scope

- **D-08: `Title`, `Description`, and `StartTime` are added to `EventSeriesEntity` as template fields** in this phase's migration, and the generator stamps them onto every occurrence it creates. `Description` follows `EventEntity`'s convention — unbounded Markdown (74 D-06), not a length-limited plain-text field. Because the repeat toggle lives on the Create Event form (D-06), the DM has already typed all three; they become the series template in the same submit.

  Rejected: copying from the latest existing occurrence, which quietly makes editing one occurrence change every future one — the opposite of EVTRECUR-06. Rejected: copying from slot 0, which puts the series' identity in a row that can itself be cancelled or moved.

- **D-09: Saving an edit to one occurrence prompts for scope — "Only this event" (default) or "This and all future events".** The second updates the series template so newly generated slots inherit it, **and** rewrites the future occurrences nobody has individually touched. Occurrences that were separately moved, edited, or cancelled are skipped: a deliberate one-off decision should not be silently undone by an unrelated title change.

  **No past occurrence is ever rewritten by any scope.** A third "all events" scope was rejected on those grounds — past occurrences are the record of sessions that happened.

  Note this yields Calendar's "this and following" semantics *without splitting the series*: the template governs slots not yet generated (all future by definition) and the sweep covers the already-generated future rows, so past slots need no protection mechanism.

- **D-10: The series page carries no template edit form.** Template changes flow only through D-09's scope prompt, so there is exactly one place to make them and no second dialog whose propagation rules could diverge. The series page shows the rule and template read-only.

### Series lifecycle

- **D-11: Ending a series sets a nullable `EndDate` on the series row.** No slot fires past it and generation stops. The confirm names how many future occurrences exist and how many real answers they hold, and offers to clear them in the same action. **Past occurrences are always kept** — they record sessions that happened.

  `EndDate` was chosen over a boolean because it does two jobs: ending a running series, and declaring a fixed-length arc at setup time ("run this for exactly ten sessions").

- **D-12: Removing a series offers two outcomes at the confirm — delete or detach.** *Delete* removes the series and every occurrence it produced (past, future, cancelled, moved, edited); their availability rows cascade away via the existing `FK_EventSignups_Events_EventId`. *Detach* drops only the rule and leaves the occurrences as ordinary one-off events, which serves the "the recurrence was wrong but the sessions we played should stay" case that ending does not cover.

  **Detach must null both `SeriesId` and `SeriesSlotIndex`**, or the row reads as a series member with no series — and the D-19 filtered index would stop covering it while the data still claims a slot.

  **The shipped FK does not do this for you.** `FK_Events_EventSeries_SeriesId` declares no `onDelete`, so EF Core's optional-relationship default maps to `NO ACTION`: deleting a series while occurrences reference it throws a FK violation today. Both outcomes must be written deliberately. That is a feature — there is no accidental behaviour to inherit.

  **Not in EVTRECUR-01…08.** An operator addition requested during discussion, in the same class as D-11.

- **D-13: The series-delete confirm counts sessions split past/future, plus real answers** — e.g. *"26 sessions will be removed — 18 already held, 8 upcoming — along with 14 availability answers people actually gave."* The past/future split is the fact that distinguishes a cleanup from a loss of history, and the answer count uses `HasAnswered` (75 D-11), which exists precisely to separate a deliberate answer from an untouched default.

  **This is a narrow, deliberate divergence from Phase 75 D-26**, which locked the single-event confirm to count all signup rows and said not to "correct" it. That reasoning was scoped to one event, where an inflated count is merely noisy. At series scale it changes character: a fresh 20-occurrence series on a six-member Campaign board holds 120 auto-created rows before anyone has looked at it, and *"120 signups will be lost"* describes six people who have done nothing. **The single-event dialog is unchanged** — D-26 still governs it.

### Cancelling, moving, and editing one occurrence

- **D-14: A cancelled occurrence is a tombstone — the row stays and gains a cancelled marker.** This keeps the generator's idempotency check a single question ("is there a row for this slot?") answered identically for cancelled, moved, and edited occurrences, so there is one rule to get right instead of three. Availability answers survive, so un-cancelling is lossless and a mis-click costs nothing.

  **Accepted cost, named by Phase 74 D-18 when it deferred this:** the desktop calendar, the mobile agenda, and the details read all have to filter on the marker, and a read path that forgets shows a cancelled session as live.

  Rejected: hard-deleting and recording the slot in a separate skipped-slots table — the generator would consult two sources to answer one question, un-cancelling would regenerate a row whose answers are gone, and it is a join for something never queried independently (the shape the roadmap argued against for the cycle mask). Rejected: a per-series watermark — it cannot tell a cancelled slot from one never created, failing EVTRECUR-07 the first time a DM cancels inside the generated window.

- **D-15: Cancelled occurrences stay visible, struck through and muted**, on both the desktop calendar chip and the mobile agenda entry; the details page carries a cancelled banner with the availability buttons removed. "This session is off" tells a player more than the date not being there — an absence is indistinguishable from a bug, which is the same reasoning Phase 74 D-14 used to reject a blank time slot.

- **D-16: Cancel replaces Delete on an occurrence that belongs to a series, and the refusal is enforced server-side** by re-resolving whether the event has a `SeriesId` — not merely hidden in markup. Follows the board-type precedent at `Controllers/QuestBoard/QuestController.cs:762` that Phase 75 D-08 established. **One-off events keep Delete exactly as Phase 74 shipped it.** The two words then mean two distinct things: Delete removes an event that will never return; Cancel records that a scheduled session is off.

  This is not a nicety — a hard delete on a series occurrence removes the only record that its slot was handled, so the generator recreates it on the next run.

- **D-17: Moving an occurrence onto a date another live occurrence of the same series holds is allowed, with a notice in the save dialog** — *"Another session in this series already falls on 14 November."* A double session is legitimate, so blocking would be wrong, but a mistyped date otherwise produces two chips on one day with nothing explaining why. It rides in the scope prompt D-09 already shows, so it costs one count query in a dialog that exists. **A cancelled sibling on that date does not trigger the notice.** No date restriction is introduced — 74 D-19 deliberately has none.

### Idempotency — hard constraints

These three are locked together. Any one of them missing reintroduces the resurrection bug EVTRECUR-07 exists to prevent, and D-19 is what makes a failure in the other two loud instead of silent.

- **D-18: The existence check is keyed on `(SeriesId, SeriesSlotIndex)`. Never on date.** A date-keyed check ("is there an event for series X on 7 November?") returns nothing for a moved occurrence and recreates it on the original date, leaving both.

- **D-19: A filtered unique index on `(SeriesId, SeriesSlotIndex) WHERE SeriesId IS NOT NULL` is added in this phase's migration.** One-off events carry null in both columns and are unaffected. Nothing at the database level currently stops two rows claiming the same slot, and `Program.cs:260` registers a global `AutomaticRetryAttribute { Attempts = 5 }` — a job that dies partway re-runs from the top, which is exactly the check-then-insert race that produces a duplicate. `IX_EventSignups_EventId_UserId` is `unique: true` for this same reason; `IX_Events_SeriesId` is not.

- **D-20: The existence query loads the series' slot indexes with no date predicate.** The obvious optimisation — loading only occurrences inside the runway window — silently breaks the guarantee, because a moved occurrence may no longer be in that window. Move a slot past the runway or backwards into the past and the query does not return it, the slot reads as free, and it regenerates on the original date. This only manifests when someone moves an occurrence a long way, which is rare enough to escape casual testing and not rare enough to never happen.

### The rolling window

- **D-21: The runway is measured in live (non-cancelled) future occurrences, not as a date horizon.** A fortnightly series then gets as many upcoming sessions as a weekly one, and cancelling three sessions near the edge pulls three more in so the runway stays honest. It also makes the required health check one cadence-independent query — "does every active series have at least N future occurrences?" — with no date arithmetic.

- **D-22: The runway is a global 20, as a code default overridable through configuration.** About five months on a weekly series, about nine on a two-on-two-off one. **Not per-series**, and not because it would be expensive: it is a knob about the mechanism rather than the schedule, and EVTRECUR-03 exists specifically so "an open-ended campaign never needs manual re-extension". Per-series values would also make "this series has enough runway" mean something different for every series, so a DM who picked 3 would read as healthy while sitting one failed job away from empty. A code default means nothing has to change on the deployment's server env file for this to work.

- **D-23: Only slots dated today or later are materialized.** Past slots are computed so numbering and cycle phase stay correct, but never created. A DM sets a past anchor to fix *where the rhythm sits* — "we meet every other Saturday and the cycle started 5 September" — not to declare where the records begin. Matches EVTRECUR-02's "the next ~10 dates it **will generate**" and Phase 75 D-17's today-included boundary, and avoids fanning campaign signups out across sessions nobody can attend. A DM who does want a past session on the board can still create it as a one-off (74 D-19).

- **D-24: Generation is one Domain service method, called directly.** The controller calls it synchronously on save and writes the full runway; the recurring job calls the same method on schedule. **Not enqueued to Hangfire on save.** Enqueuing does not buy shared logic — one method with two callers already gives that — and it costs three specific things: the DM can land on the calendar before the worker finishes and see nothing (`WorkerCount = 2`); a failure after the global 5 retries exhaust is invisible because the success toast already fired; and `Program.cs` skips Hangfire entirely in the Testing environment (the reason `NullReminderJobDispatcher` exists), so an enqueue-on-save path does nothing in integration tests and the thing under test stops being the thing that ships. This is neither slow nor failure-tolerant work — it is the data the DM just asked for.

- **D-25: Commit granularity differs by path, deliberately.** In the job, each occurrence commits with its own campaign signups, so a failure at nineteen keeps eighteen and the retry adds the rest — monotonic progress, which is what slot-keyed idempotency is for. On the controller path, the series row and the whole first generation pass are additionally wrapped in one transaction, so a failed save leaves nothing behind and the DM sees a clean error rather than a half-built series. Occurrence-plus-its-signups stays atomic in both (75 D-15).

- **D-26: The horizon check surfaces as a DM-visible banner on the calendar page** when any active series on the board is below its runway. This is the roadmap's named risk — "the job silently stopping: the calendar quietly runs dry at the horizon with no error anyone sees. Surface a horizon check somewhere a human actually looks." The calendar is the page a DM already opens constantly, and it degrades honestly: even if the check itself is wrong, the DM is looking at the calendar that would be running dry. The series page is where a DM goes once something is already suspected, which is the failure being described.

- **D-27: A dedicated daily recurring job at an off-peak hour**, registered beside `daily-session-reminders` in `Program.cs:355` and following the `HangfireJobHelper.RunInScopeAsync` pattern with `IServiceScopeFactory`. Daily rather than weekly so a failed run self-heals the next night; separate from `DailyReminderJob` so neither failure can take the other down.

- **D-28: The job iterates groups with `SetGroupId()` per group — never `IgnoreQueryFilters()`.** Locked by the roadmap, restated because the codebase contains a precedent that points the other way: `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` (`QuestBoard.Repository/QuestRepository.cs:265`) uses `IgnoreQueryFilters()` for a cross-group sweep. That one is read-only. This job **writes**, and needs a real group context anyway for the campaign fan-out. Also note the stale doc comment on `ActiveGroupContextService` claiming a null `ActiveGroupId` means "see all" — the Phase 55 filters are fail-closed and return zero rows.

### Inherited discipline (restated, not rediscovered)

- **D-29: The generator's campaign fan-out must not stamp `UpdatedAt`.** Phase 75 D-13 named this phase explicitly so it would not have to be rediscovered. `UpdatedAt != null` means a human deliberately set the answer; an auto-created row must leave it null or Phase 77's EVTVIEW-02 becomes unimplementable.

- **D-30: Fan-out happens at occurrence-create time, in the same unit of work as the occurrence** (75 D-15), for every member regardless of role (75 D-14), regardless of the occurrence's date (75 D-16). Campaign boards only.

### Claude's Discretion

Not discussed — planner decides:

- Whether `EventSeries` gets its own domain model / repository / service triple (it currently has none — no code reads or writes the table) or is served through the existing `IEventService`; and whether the series page is a new controller or actions on `EventsController`.
- Naming and type of the cancelled marker — a bool, or a nullable `CancelledAt` timestamp that also records when.
- Exact preview count (~10 per EVTRECUR-02) and whether the preview shows anything for a past-dated anchor.
- Toggle-strip styling and any UI-level cap on cycle length below the column's ~100-position ceiling.
- The off-peak hour for the recurring job, and its Hangfire job id.
- Wording of every confirm, banner, and toast — including the D-13 delete confirm, the D-11 end confirm, the D-15 cancelled banner, the D-17 collision notice, and the D-26 horizon banner.
- Whether the D-09 scope prompt is a native `confirm()`, a two-button dialog, or radio buttons on the form. Note the app's established idiom is native `confirm()` (74 D-17, 75 D-25), but that idiom is binary and this prompt has two affirmative outcomes.
- Index strategy beyond the mandated D-19 unique index.
- Where the `DateOnly` → `DateTime` conversion seam sits for any new calendar-facing view model (74 D-01 requires it stay a single well-named point).
- Whether a detached occurrence (D-12) that was cancelled keeps its cancelled marker or is deleted — a cancelled one-off is a coherent state under D-15, but it is new.
- Test structure beyond the mandated two-group tenant isolation test (74 D-22, 75 D-29) and the idempotency tests EVTRECUR-07 implies — at minimum: run the generator twice, cancel-then-run, move-then-run, move-outside-the-runway-then-run (D-20), and the EVTRECUR-08 mirrored-mask interleaving test.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements

- `.planning/ROADMAP.md` — the Phase 76 entry: goal, 6 success criteria, scope notes, 2 locked decisions, 3 named risks. Also read the **Phase 74** and **Phase 75** entries (this phase inherits from both) and the **Phase 77** entry, whose grid must exclude cancelled occurrences (D-14/D-15) and depends on `HasAnswered` staying meaningful (D-29).
- `.planning/REQUIREMENTS.md:47–56` — EVTRECUR-01 … EVTRECUR-08 in full. Also the Future Requirements block: **EVTRECUR-09** (regenerate untouched future occurrences on a rule edit) is deferred, and D-07 makes it moot for cadence by forbidding rule edits outright.
- `.planning/phases/74-event-schema-crud-and-calendar-display/74-CONTEXT.md` — the schema this phase extends. D-01 (`DateOnly`/`TimeOnly?` and the conversion-seam warning), D-02 (the "76 is pure code" claim this phase contradicts — see `<domain>`), D-03 (nullable series FK), D-04 (tenant scoping shape and the fail-closed filter rule), D-05 (no author column), D-06 (Markdown description convention), D-09 (the five protected `_Calendar.cshtml` call sites), D-10/D-11 (details view is the one surface, DM-gated), **D-17 and D-18 (delete idiom, and hard-delete deferring the cancelled concept to this phase → D-14)**, D-19 (past dates allowed → D-23), D-20 (redirect to the event's month), D-22 (two-group test mandatory).
- `.planning/phases/75-event-availability-signups/75-CONTEXT.md` — the availability layer the generator must feed. **D-13 (do not stamp `UpdatedAt` — written for this phase), D-15 (fan-out at create time, same unit of work), D-14 (every member regardless of role), D-16 (fan-out ignores date)**, D-10/D-11 (`UpdatedAt` semantics and the `HasAnswered` property D-13 here relies on), D-25/**D-26 (the single-event confirm rule this phase narrowly diverges from — see D-13)**, D-27 (signup cascade on event delete), D-08 (the server-side enforcement precedent D-16 follows), D-29 (two-group test), D-30 (narrow scalar updates, `BaseRepository.UpdateAsync` off-limits).

### Project conventions

- `CLAUDE.md` — EF packages only in `QuestBoard.Repository`; the `modern-card` / `modern-card-header` / `modern-card-body` view pattern for the new series page; **no GSD references in source comments** (applies to every comment written this phase); migrations auto-apply on startup.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository one-way dependency and the two AutoMapper boundaries. The generator is Domain-layer (D-24), called by both a Service-layer controller and a Service-layer job.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns.
- `.planning/codebase/TESTING.md` — integration vs unit test placement. The date generator is pure and belongs in unit tests; idempotency and tenant isolation belong in integration tests.

### Code the phase must read before changing

- `QuestBoard.Repository/Entities/EventSeriesEntity.cs` — the shipped series row. Currently carries `AnchorDate`, `IntervalWeeks`, `WeekDay`, `CycleMask`, `CreatedAt`, `GroupId` and nothing else. D-08 adds the template fields, D-11 adds `EndDate`. Its class comment ("No code reads or writes it yet") stops being true this phase and must be rewritten.
- `QuestBoard.Repository/Entities/EventEntity.cs` — `SeriesId` and `SeriesSlotIndex` already exist and are nullable. D-14 adds the cancelled marker. Note the existing `Signups` navigation and its comment explaining why it exists.
- `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` — the shipped schema: `IX_Events_GroupId_Date`, non-unique `IX_Events_SeriesId` (→ D-19), unique `IX_EventSignups_EventId_UserId`, cascade from `Events` to `EventSignups`, and `FK_Events_EventSeries_SeriesId` with **no** `onDelete` (→ D-12).
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — the fail-closed global query filter block, including the "do not capture `ActiveGroupId` into a local var" warning. Relevant to D-28.
- `QuestBoard.Domain/Interfaces/IEventService.cs` and `IEventRepository.cs` — **`AddWithCampaignFanOutAsync` already exists** and is the D-30 fan-out hook the generator should reuse rather than reimplement. `GetSeriesGroupIdAsync` already exists for the cross-board series check (74 D-21).
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — Details / Create / Edit / Delete / SetAvailability / Withdraw as shipped. D-06, D-09, D-16 all land here or beside it.
- `QuestBoard.Service/Views/Events/Details.cshtml` — the one event surface; gains the D-15 cancelled banner and the D-09 scope prompt. No `.Mobile` variant exists.
- `QuestBoard.Service/Views/Events/Create.cshtml` and `Edit.cshtml` — where the D-06 repeat toggle and D-03 mask strip go.
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` and `QuestBoard.Service/Views/Calendar/Index.cshtml` — the desktop chip needs the D-15 cancelled style and the page needs the D-26 banner. **Phase 74 D-09's five protected call sites still apply** (`Quest/Details.cshtml:604,648,696` and `Quest/Details.Mobile.cshtml:158,196`) — they must render no series-specific markup, and the empty-collection default is what protects them.
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` — hand-rolled agenda loop, needs the D-15 cancelled style.
- `QuestBoard.Service/Jobs/DailyReminderJob.cs` and `QuestBoard.Service/Jobs/HangfireJobHelper.cs` — the D-27 pattern to follow, including `IServiceScopeFactory` and `RunInScopeAsync`.
- `QuestBoard.Service/Program.cs:260` (global `AutomaticRetryAttribute { Attempts = 5 }` — the reason D-19 exists), `:263` (`WorkerCount = 2` — the reason D-24 rejects enqueue-on-save), `:266–272` (Hangfire skipped in Testing — the other reason), `:355` (where D-27's job is registered).
- `QuestBoard.Repository/QuestRepository.cs:265` — `GetQuestsForTomorrowAllGroupsAsync`, the `IgnoreQueryFilters()` precedent D-28 explicitly does **not** follow.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:762` — the server-side enforcement precedent D-16 follows.
- `QuestBoard.Service/Views/Quest/Details.cshtml:966` — the `fetch`-POST idiom D-05's preview endpoint follows.
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — the two-group test precedent and why the default harness (`ActiveGroupId = 1`) is structurally blind without it.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`IEventService.AddWithCampaignFanOutAsync(newEvent, memberIds, token)`** — already written for Phase 75 D-15, already writes an event plus one signup per member in a single save, already leaves the answered marker unset. The generator's per-occurrence write (D-25, D-29, D-30) should call this rather than reimplement the fan-out.
- **`UserRepository.GetAllGroupMembers`** (`QuestBoard.Repository/UserRepository.cs:51`) — the member list the fan-out needs, membership regardless of role (75 D-14).
- **`HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, action)`** — takes a `groupId` and calls `SetGroupId` before any repository call. Exactly what D-28's per-group iteration needs; pass a real group id rather than the null that `DailyReminderJob` passes.
- **`IEventService.GetSeriesGroupIdAsync`** — the cross-board series check Phase 74 D-21 required, already implemented.
- **`_Toasts.cshtml`** — picked up from `TempData["Success"]` in every layout with no view changes (72 D-14), so create/end/delete feedback needs no new plumbing.
- **`VoteType { No = 0, Maybe = 1, Yes = 2 }`** and the whole `EventSignup` stack — unchanged by this phase; occurrences simply acquire signups the same way one-off events do.

### Established Patterns

- **Narrow scalar-update repository methods** (75 D-30, `PlayerSignupRepository.ChangeVoteAsync` at `QuestBoard.Repository/PlayerSignupRepository.cs:43`). `BaseRepository.UpdateAsync` is off-limits for entities with loaded navigation collections — and `EventEntity` now has a `Signups` collection, which makes this load-bearing rather than precautionary for the D-09 template sweep and the D-14 cancel write.
- **Fail-closed query filters** — a null `ActiveGroupId` returns zero rows, never all rows. The `ActiveGroupContextService` doc comment claiming otherwise is stale and known-wrong (D-28).
- **Server-side re-resolution over client-rendered visibility** (75 D-08 / `QuestController.cs:762`) — D-16's Delete refusal follows it.
- **Native `confirm()` as the destructive-action idiom** (74 D-17, 75 D-25) — D-11 and D-13's confirms follow it; D-09's two-affirmative-outcome prompt is the one place it may not fit (planner's call).
- **Recurring jobs registered after `ConfigureDatabase()`** so migrations have run before a job can fire (`Program.cs:349–359`).

### Integration Points

- **Create Event form → series creation** (D-06): the repeat toggle turns one submit into series row + template + full first generation pass, wrapped in one transaction (D-25).
- **Generator → `AddWithCampaignFanOutAsync`** (D-30): one call per materialized occurrence, on Campaign boards.
- **Nightly job → generator, per group** (D-27, D-28): iterate groups, `SetGroupId` each, top each active series back to its runway.
- **Calendar page → horizon banner** (D-26): one count query alongside the existing event load, DM-gated.
- **Calendar and agenda reads → cancelled filter** (D-14, D-15): every read path that renders events must account for the marker. This is the phase's most easily-forgotten surface.
- **Occurrence Details → series page** (D-06): a link from any occurrence to the rule that produced it.

</code_context>

<specifics>
## Specific Ideas

- **The Google Calendar edit-scope prompt was the operator's proposal**, not a suggestion offered to them — *"the save button then asks: change this event or change the series?"* It replaced a weaker design (a propagation checkbox on a separate series edit form) and is the reason D-09 reads the way it does. Preserve the two-scope shape; it was chosen deliberately over Calendar's three.
- **`EndDate` as the ending mechanism was also the operator's refinement** — *"a saved recurrence setting with a name, start date, nullable end date"*. It merges the ending action and the fixed-length-arc case into one column (D-11).
- **The operator pushed back on the moved-occurrence guarantee** (*"are you sure a single moved event does not get regenerated?"*), which is what surfaced D-19 and D-20. Both were real gaps, not hypotheticals — the unique index genuinely does not exist, and a date-scoped existence query genuinely would resurrect a far-moved occurrence. Do not treat D-18 alone as sufficient.
- **"Logic stays in one place" was an explicit operator requirement** for the generator. D-24 satisfies it with one Domain method and two callers rather than with a Hangfire enqueue.
- **Removing a series with its events was requested outright** ("I want the ability to remove all related events together with the removal of a series setting") — D-12. The detach alternative was added on top; delete-everything is the case that was asked for.

</specifics>

<deferred>
## Deferred Ideas

- **Daily cadence** — a cadence-unit field and `AddDays` instead of `AddWeeks`; `WeekDay` would go unused. Cheap and would not disturb the slot arithmetic. No requirement asks for it and a daily D&D session is not a real pattern for this board.
- **Monthly cadence — "nth weekday of every N months"** — the shape a group meeting "first Saturday monthly" actually wants, and the one recurrence pattern RFC 5545's `BYSETPOS` handles natively (the roadmap's no-library argument did not account for this). Needs a second date-derivation path alongside the week-counted one, and both must stay slot-stable for the D-18 key. The largest of the deferred items.
- **Per-series runway override** — one nullable column defaulting to the D-22 global. Addable later with no generator rework. Deferred because it asks the DM an implementation question and makes the D-26 health check's threshold per-series.
- **An advanced mask editor beyond the D-03 toggle strip** — the strip already reaches every pattern the column stores, so this is only relevant if a longer-form editor is ever wanted for very long cycles.
- **EVTRECUR-09 (already deferred in REQUIREMENTS.md)** — regenerating untouched future occurrences on a rule edit. D-07 makes it moot for cadence by forbidding rule edits; if rule edits are ever reintroduced, this comes back with them.

### Consequence for Phase 77 (not deferred — must be handled there)

- **The availability overview grid must exclude cancelled occurrences.** Under D-14 a cancelled session keeps its signup rows, so a naive join shows a cancelled date as a row everyone said yes to. Phase 77's EVTVIEW-01 and EVTVIEW-03 both need the filter.

</deferred>

---

*Phase: 76-Recurring Event Series*
*Context gathered: 2026-08-28*
