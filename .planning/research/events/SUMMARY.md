# Project Research Summary — Calendar Events

**Project:** D&D Quest Board — v9.0 Rolling Improvements, Calendar Events feature
**Domain:** Scheduling / availability tracking inside an existing multi-tenant ASP.NET Core 10 MVC app
**Researched:** 2026-08-25
**Confidence:** HIGH

## Executive Summary

Calendar Events adds a second kind of thing to the calendar alongside quests: a dated, informational entry that players sign up to in order to declare availability. On One-Shot boards it marks days already spoken for; on Campaign boards it *is* the play session. It never blocks quest creation and never appears on the quest board main page.

The feature is bigger than it first appears, and the weight is not in the CRUD — it is in three places. First, **recurrence**: the operator's real use case is two campaigns sharing one group of six players on a biweekly Saturday cadence, alternating in blocks of two. Research confirmed RFC 5545 RRULE **cannot** express that (`BYSETPOS` selects only within a single interval, not across periods), so a small custom cycle-mask generator is both simpler and more correct than adopting a library that would need post-filtering anyway. Second, **tenant scoping in a background job**: the materialization job runs outside `GroupSessionMiddleware`, which is precisely where this codebase's two prior cross-tenant leaks would have been easiest to reintroduce. Third, **the calendar's real blast radius**, which is not what a call-site count suggests.

The single most dangerous finding is that `ActiveGroupContextService.cs` carries a doc comment stating that a null `ActiveGroupId` means "see all". The `HasQueryFilter` predicates actually shipped in Phase 55 are fail-closed — a null group yields **zero** rows. A developer trusting that comment inside the Hangfire job would get silent no-ops on reads. Worse, EF query filters do not constrain **writes** at all, so a mis-scoped `GroupId` on an inserted occurrence is a real cross-tenant leak with no schema-level safety net.

## Key Findings

### Recommended Stack

**No new packages.** Everything needed is already present and running.

**Core technologies:**
- **Custom cycle-mask generator** (Domain layer, ~40 lines, unit-testable in isolation) — chosen over `Ical.Net` (v5.2.3, MIT) because RRULE genuinely cannot express a repeating on/off mask riding a base cadence. Adopting a library for a rule it cannot represent would mean post-filtering its output anyway, adding a dependency for negative benefit.
- **`DateOnly` + `TimeOnly?`** for the occurrence date, series anchor, and optional start time — mapped natively by EF Core 10 to SQL Server `date` / `time`, no extra package.
- **Hangfire 1.8** recurring job for rolling-window top-up — the pattern is already in this repo (`DailyReminderJob.cs`, `HangfireJobHelper.RunInScopeAsync`), including the `IServiceScopeFactory` requirement and a global `AutomaticRetryAttribute`.
- **The existing hand-rolled `_Calendar.cshtml` month grid stays.** It is fused with quest vote-button and signup rendering, not a generic widget. Events extend it with a parallel per-day collection; they do not justify a JS calendar library.

**Explicitly do NOT add:** `Ical.Net` or any RRULE library, a client-side calendar component, an external scheduler, `DateTimeOffset` for occurrence dates, or a JSON column for the cycle mask.

### Expected Features

**Must have (table stakes):**
- DMs create, edit, and delete events on their board; events are group-scoped and never block quest creation
- Events render on the desktop calendar page **and** the mobile calendar page, visually distinct from quests
- Players declare availability per event using the existing `VoteType` (No / Maybe / Yes) — no date voting, the date is fixed
- Campaign boards auto-sign-up every member with vote = Yes (opt-out); One-Shot boards are fully opt-in
- Optional recurrence via base cadence + cycle mask + anchor, materialized on a rolling window
- Individual occurrences can be cancelled, moved, or edited without touching the series
- A live preview of the next ~10 generated dates while configuring a series
- An availability overview page: upcoming events × players, scoped to the current board

**Deliberately not modelled:**
- **No `EventType` field.** Meaning derives entirely from the board's already-immutable `BoardType`, matching the established `CloseQuestAsync`/`ReopenQuestAsync` vs `FinalizeQuestAsync`/`OpenQuestAsync` split.
- **No link between an Event and a Quest entity.** The operator scoped events as informational; a relation would invite exactly the blocking semantics that were ruled out.

**Deferred (not in the opening scope):**
- Email on a cancelled or moved occurrence. This is a genuine two-sided trade-off, not an obvious no: `QuestDateChangedEmailJob.cs` is direct precedent *for* notifying on a moved date, while Phase 36 deliberately engineered Campaign boards to never fire scheduling email. Research recommends deciding this after real Campaign usage exists rather than guessing at rate-limit impact up front.
- A mixed-purpose Campaign board event that skips auto-signup (e.g. "holiday, no session").

### Architecture Approach

Three new entities, one migration, and a smaller calendar blast radius than the call-site count implies.

**Major components:**
1. **`EventSeriesEntity`** — the recurrence rule: base cadence, cycle mask, anchor date. Group-scoped with `HasQueryFilter`.
2. **`EventEntity`** — a materialized occurrence. Carries `SeriesSlotIndex` as the idempotency key — **not** the date, since a moved occurrence keeps its slot identity while changing date. Nullable series reference, so a one-off event is the same entity with no series.
3. **`EventSignupEntity`** — reuses the existing `VoteType`. Much simpler than `PlayerSignupEntity` + `PlayerDateVoteEntity`: no `SignupRole`, no `CharacterId`, no waitlist ordering, because events have no roster concept.

**Cycle mask storage:** a comma-delimited `nvarchar(200)` string, argued against JSON (no precedent anywhere in this schema), an int bitmask (opaque, caps cycle length), and a child table (a join for something never queried independently).

**Calendar blast radius — the count is misleading:**
- `Views/Calendar/Index.cshtml` → calls `_Calendar` → **in scope**
- `Views/Quest/Details.cshtml` ×3 and `Views/Quest/Details.Mobile.cshtml` ×2 → also call `_Calendar`, but render the per-quest date-picker widget and never populate events → **zero changes**
- `Views/Calendar/Index.Mobile.cshtml` → **a 7th touch point that does not call the partial at all.** It hand-rolls its own agenda loop and filters with `.Where(d => !d.IsEmpty && d.QuestsOnDay.Any())`, so events stay invisible there until that filter changes. Easy to miss; squarely in scope given the operator asked for "the page and the partials views".

**Signup writes** must use narrow scalar-update methods mirroring `PlayerSignupRepository.ChangeVoteAsync`, never the generic `BaseRepository.UpdateAsync` — that override exists precisely because AutoMapper overwrites loaded navigation collections too aggressively, and `EventEntity` with loaded `Signups` hits the same trap.

**Migration** follows the `AddContactsFeature` precedent: one migration, three ordered `CreateTable` calls, purely additive — no backfill, unlike `AddGroupIdToCharacters`.

### Critical Pitfalls

1. **The stale `ActiveGroupContextService` comment, and writes that filters never protect.** The doc comment says null `ActiveGroupId` means "see all"; the Phase 55 filters are fail-closed and return **zero** rows. A job trusting that comment silently does nothing. Separately, EF query filters constrain reads only — a mis-scoped `GroupId` on insert leaks across tenants with no safety net. *Avoid:* per-group scoped iteration inside the job (`SetGroupId()` per group), never `IgnoreQueryFilters()`, and an integration test using **two** groups.
2. **The test harness is structurally blind to this.** `WebApplicationFactoryBase`'s `MutableGroupContext` defaults to a single group (`ActiveGroupId = 1`), so the standard integration test cannot see the multi-group bug class this feature is most likely to introduce. A dedicated 2+-group test is not optional.
3. **Idempotency keyed on the wrong column.** Keying the existence check on `(EventSeriesId, Date)` resurrects moved occurrences and cannot distinguish "cancelled" from "never created" — made worse by the global `AutomaticRetryAttribute`, which re-runs a failed job from scratch. *Avoid:* key on `(EventSeriesId, SeriesSlotIndex)` with a unique index.
4. **The job silently stopping.** The calendar quietly runs dry at the horizon with no error anyone sees — the failure mode the rolling window trades for never needing manual extension. *Avoid:* a horizon check surfaced somewhere a human looks, not just job logs.
5. **"Yes by default" misread as a real answer.** On Campaign boards every member starts at Yes. A DM reading the overview page cannot distinguish "said yes" from "never looked". *Avoid:* make the overview visually distinguish a default from a deliberate answer; note this was an explicit operator decision, so the fix is presentation, not data.
6. **Calendar view drift.** Extending a 6-call-site shared partial in a codebase with four documented instances of near-duplicate view drift. *Avoid:* bake "did not touch the other 5 call sites" into the phase's acceptance criteria rather than leaving it to code review.

## Implications for Roadmap

Two researchers independently proposed a five-phase split and agreed on its shape. Consolidated to four, folding calendar display into the first phase so that every phase ships something a user can actually see, and deferring notifications out of the opening scope.

### Phase 74: Event Schema, CRUD, and Calendar Display
**Rationale:** Nothing else can exist first, and CRUD without calendar display delivers nothing visible. Owns the storage convention, tenant scoping, and migration — all far cheaper to get right before data exists than to correct after people have voted on it.
**Delivers:** DMs create/edit/delete one-off events; events render on the desktop calendar page and the mobile agenda page, distinct from quests.
**Avoids:** Pitfalls 1, 2, 6, and the migration-safety trap.

### Phase 75: Event Availability Signups
**Rationale:** Availability is the point of the feature; it needs events to exist first.
**Delivers:** One-Shot opt-in signup with No/Maybe/Yes; Campaign auto-signup at Yes with opt-out; membership join/leave handling.
**Avoids:** Pitfall 5, and the `UpdateAsync` navigation-overwrite trap.

### Phase 76: Recurring Event Series
**Rationale:** The highest-complexity, highest-risk phase, deliberately isolated. Depends on both the occurrence entity and the signup model, since materialized occurrences must carry availability.
**Delivers:** Cycle-mask series with rolling-window materialization via Hangfire; per-occurrence cancel, move, and edit; a live next-10-dates preview.
**Avoids:** Pitfalls 3 and 4.

### Phase 77: Availability Overview Page
**Rationale:** Most valuable once recurring campaign sessions populate it, so it comes last. Independent of the others in code.
**Delivers:** Upcoming events × players availability grid, scoped to the current board.
**Avoids:** The overview-page N+1 and a repeat of Pitfall 1 on an aggregating page.

### Phase Ordering Rationale

- Strict dependency chain: schema → signups → recurrence → overview. Each phase ships a usable increment.
- A trimmed 74 → 75 sequence delivers a fully working non-recurring events feature if scope needs cutting mid-milestone. Recurrence and the overview grid are separable value-adds, not blocking dependencies.
- Phase 76 is sequenced after 75 rather than before because materialized occurrences must carry availability from the moment they exist.

### Research Flags

Phases likely needing deeper research during planning: **none.** The recurrence question — the one genuine unknown — is closed: RRULE cannot express the model, and the custom generator is specified.

Needing a discuss-phase decision:
- **Phase 74:** confirm the `DateOnly`/`TimeOnly?` storage decision (see Gaps below).
- **Phase 76:** rule-edit regeneration semantics. Recommended initial behaviour is additive-only — never retroactively delete or regenerate untouched future occurrences. More aggressive regeneration is a future enhancement.
- **Phase 77:** whether the overview page is DM-only or visible to all board members.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | RRULE expressibility grounded in RFC 5545 text; Hangfire pattern read from this repo's own `DailyReminderJob.cs` |
| Features | HIGH | Every recommendation traced to a named prior phase decision in this codebase |
| Architecture | HIGH | All 7 calendar touch points opened and read directly, not inferred |
| Pitfalls | HIGH | Each grounded in code read from this repo or a documented prior incident |

**Overall confidence:** HIGH

### Gaps to Address

- **Date/time type — a resolved conflict between two research documents.** STACK.md recommends `DateOnly` + `TimeOnly?`; PITFALLS.md recommends naive-local `DateTime` to match `Quest.FinalizedDate`'s convention. **Resolved in favour of `DateOnly`/`TimeOnly?`**, because PITFALLS.md itself documents that the existing convention is only half-correct — `FinalizedDate` is stored local while several call sites compare it against `DateTime.UtcNow`. Consistency does not justify propagating a pattern the same document calls broken, and `DateOnly` makes the DST bug class structurally impossible rather than merely avoided by discipline. Confirm at Phase 74's discuss-phase; the cost of reversing rises sharply once occurrences exist.
- **A pre-existing bug found in passing, unrelated to events:** several call sites compare the locally-stored `Quest.FinalizedDate` against `DateTime.UtcNow`. Logged separately rather than folded into this feature.
- **Single-timezone assumption.** No client-side timezone picker exists anywhere in the app, so all members are assumed to share one. Worth confirming at discuss-phase rather than assuming permanently.
- **`EventSeriesId`/`SeriesSlotIndex` migration timing.** Ship as unused nullable columns in Phase 74, or add in Phase 76? Recommended: Phase 76, keeping each migration scoped to what it uses. A planner's call.

## Sources

### Primary (HIGH confidence)
- Direct source reads: `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs`, `CalendarDay.cs`, `Controllers/QuestBoard/CalendarController.cs`, `Views/Shared/_Calendar.cshtml`, `_Calendar.Mobile.cshtml`, `Views/Calendar/Index.cshtml`, `Views/Calendar/Index.Mobile.cshtml`, `Jobs/DailyReminderJob.cs`, `Jobs/QuestDateChangedEmailJob.cs`, `HangfireJobHelper.cs`, `Program.cs`, `QuestBoardContext.cs`, `ActiveGroupContextService.cs`, `GroupSessionMiddleware.cs`, `PlayerSignupRepository.cs`
- RFC 5545 (iCalendar) — `INTERVAL`, `BYDAY`, `BYSETPOS` semantics
- `.planning/PROJECT.md` — Known issues / tech debt, Constraints, Key Decisions
- nuget.org — `Ical.Net` 5.2.3 (MIT, published 2026-06-23); Hangfire 1.8.24 (2026-07-16)

### Secondary (MEDIUM confidence)
- Microsoft Learn — `TimeZoneInfo` on Linux requires IANA ids (`net-10.0`, updated 2026-08-03)
- EF Core `DateOnly`/`TimeOnly` → SQL Server `date`/`time` mapping, native since EF Core 8

---
*Research completed: 2026-08-25*
*Ready for roadmap: yes*
