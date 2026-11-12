# Milestone v9.0: Rolling Improvements

**Status:** 🚧 IN PROGRESS
**Phases:** 72–94 so far (open-ended)
**Working branch:** `milestone/v9-rolling-improvements`

## Overview

A rolling bucket milestone for small, ad-hoc features and bug fixes. Unlike v1.0–v8.0, this milestone has no fixed end-state and no unifying theme — phases are appended as work arrives, and the milestone closes when the operator decides to cut it. Phase numbering continues from v8.0 (which ended at Phase 71).

The opening scope was three items. Two are small and independent: closing a UX gap where a player cannot change the character on a quest they have already signed up for, and resolving five stale HIGH GitHub security alerts left behind by the v5.0 EuphoriaInn→QuestBoard rename. The third is substantial — Calendar Events, spanning four phases, which adds dated informational entries to the calendar with per-event player availability and an optional recurrence model.

Appended 2026-08-26: **Link Previews**, spanning two phases, so that a quest, character, or contact link pasted into Discord or Slack renders a rich preview card instead of a bare URL. The cards are gated behind explicitly-minted signed share links rather than being public, because a board is private and external unfurl caches are permanent.

Appended 2026-08-27: **NPC Contact Organisation**, spanning two phases, from a board user's request — categories first, so a long flat Contacts list can be broken into named headings, then free-form tags with a filter on top. The two are split because the requester staged them that way and because they are different data models: a category is a single grouping a contact sits under, a tag is a many-to-many label it carries. Phase 81 can sit unplanned indefinitely without blocking 80.

Appended 2026-09-17: **Calendar Subscription**, spanning two phases, from the operator — a personal `text/calendar` feed at a token-authenticated URL so a phone's calendar picks up board dates on its own instead of having them retyped. The two are split because they draw on different reads: events already have a membership-scoped cross-board query from Phase 82, while quests have none and must be restricted to one-shot boards on top of it. Phase 84 is a complete, subscribable feed on its own; Phase 85 adds a second source to it.

## Phases

### Phase 72: Change Character on an Existing Signup

**Goal**: A player who has already signed up for a quest can change which character they are bringing — or clear it back to none — from both the desktop and mobile quest Details pages, without a DM having to intervene.
**Depends on**: Nothing (first phase of v9.0; builds on the shipped v8.0 codebase)
**Requirements**: SIGNCHAR-01, SIGNCHAR-02, SIGNCHAR-03, SIGNCHAR-04, SIGNCHAR-05, SIGNCHAR-06, SIGNCHAR-07
**Plans**: 4/4 plans complete

Plans:
**Wave 1**

- [x] 72-01-PLAN.md — Server-side change rules: widen the character list at its single writer, rework `UpdateSignupCharacter` (drop the status gate, add the explicit board check, split the failure paths, add toasts), and build the first automated coverage this action has ever had (wave 1)
- [x] 72-02-PLAN.md — Shared `_CharacterSelectModal.cshtml` partial with self-priming and remove-character script, plus the single-source `ToSelectLabel` option-label function and its unit tests (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 72-03-PLAN.md — Desktop `Details.cshtml`: replace the add-only modal with the shared partial, add the change trigger to both character cells, route both signup-time pickers through the shared label, pin the markup with tests (wave 2)
- [x] 72-04-PLAN.md — Mobile `Details.Mobile.cshtml`: inline change trigger on participant and waitlist rows, render the shared partial once, route the mobile picker through the shared label, prove it renders with a real mobile User-Agent (wave 2)

**Success criteria:**

1. A player whose signup already has a character can open a change control on the desktop Details page — in both the finalized-participants table and the waitlist table — pick a different character, and see the new one reflected after save.
2. The same player can do all of that from the mobile Details page, which today shows the character as plain text with no control at all.
3. Submitting the change form with no character selected clears the signup's `CharacterId` to null, verified in the database rather than inferred from the UI.
4. A signup holding a Retired or Dead character shows that character as the current selection with its status labelled — opening the control and saving without touching the dropdown leaves the selection unchanged.
5. An automated integration test proves a player cannot assign a character owned by another user or belonging to another group.

**Scope notes:**

- Service-layer only. The full nullable-`characterId` path (controller → `PlayerSignupService` → `PlayerSignupRepository` override → `PlayerSignupEntity.CharacterId`) already works; no Domain, Repository, or migration change is needed. Clearing is already a supported server-side code path.
- Desktop and mobile ship in **one phase**, not two. Splitting them risks the platforms diverging on behaviour — the explicit lesson from Phases 43/54, and the pattern followed through the Phase 66–71 Markdown rollout.
- Internal order: extract the shared `_CharacterSelectModal.cshtml` partial first (both host views depend on it existing), then wire desktop, then wire mobile.
- No new packages, no new JS library. Bootstrap 5.3.0 + Popper is already loaded on both layouts, and `show.bs.modal` + `event.relatedTarget` is an established idiom here (`Shop/Index.cshtml`, `ShopManagement/Index.cshtml`, and both `.Mobile` counterparts).

**Decisions locked before planning:**

- **Post-finalization changes stay allowed, with no time cutoff.** `UpdateSignupCharacter` has no `IsFinalized` guard today while its sibling `UpdateSignup` (date votes) does; that asymmetry is now a deliberate, documented decision rather than an accident. Character choice matters most right up to game night; date votes are meaningless once the date is locked. Matches the Phase 61 precedent.
- **No DM notification on swap.** The relay budget is 100/day and no existing edit feature fires email. The DM's read surface on Details/Manage is already live and authoritative.

**Risks this phase must actively avoid:**

- **Silent character wipe.** `ViewBag.UserCharacters` is filtered to `CharacterStatus.Active`. A naive `select.value = characterId` finds no matching `<option>` for a Retired/Dead character, falls back to `""`, and a save silently clears the signup. SIGNCHAR-04 exists specifically to close this; it is not deferrable once the change control ships.
- **A third near-duplicate view block.** `Details.cshtml` already holds two structurally identical character cells; mobile makes a third. This is the same drift class PROJECT.md blames for the `Characters/Edit.cshtml` `classIndex` bug and three other recorded instances. Extract the partial rather than hand-copying a fourth.
- **The `required` attribute.** Reusing the existing modal verbatim blocks SIGNCHAR-03 at the browser level.
- **Mobile markup that never renders.** Verify with a real mobile User-Agent, not devtools emulation — PROJECT.md records a live case (`_Layout.Platform.Mobile.cshtml`) of mobile markup that is never selected.

### Phase 73: Resolve Stale HIGH Security Alerts

**Goal**: The repository's GitHub Security tab shows zero open HIGH alerts, with each of the five closed on recorded evidence rather than assumption — and the reasoning preserved where a future reviewer will find it.
**Depends on**: Nothing — fully independent of Phase 72 (no shared code, files, or data)
**Requirements**: SECALERT-01, SECALERT-02, SECALERT-03, SECALERT-04, SECALERT-05
**Plans**: 3/3 plans complete

Plans:
**Wave 1**

- [x] 73-01-PLAN.md — Re-verify all five alerts and GitHub's server-side dependency graph live, and write `.planning/SECURITY-TRIAGE.md` entry one (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 73-02-PLAN.md — Pre-flight, draft five comments, single operator approval gate, then per-alert gate + PATCH + read-back (wave 2, not autonomous)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 73-03-PLAN.md — Complete the durable record, add the two `PROJECT.md` hooks, run the phase gate (wave 3)

**Success criteria:**

1. Each of alerts #17–#21 has been opened individually and its branch attribution, manifest path, and detection timestamp recorded.
2. GitHub's dependency graph has been force-refreshed and the alerts re-checked afterwards, so the staleness conclusion rests on GitHub's own re-scan rather than local CLI output.
3. All five alerts are closed, each with its own dismissal reason citing the specific evidence — no bulk action, no generic reason.
4. The investigation and its outcome are written into `.planning/PROJECT.md`.
5. `gh api repos/theunschut/quest-board/dependabot/alerts` returns no open HIGH alerts.

**Scope notes:**

- Not a code phase. No `.csproj`, NuGet, or migration work applies — `System.Security.Cryptography.Xml` is absent from every tracked manifest and from the transitive graph.
- The dismissal mechanism is `PATCH /repos/{owner}/{repo}/dependabot/alerts/{alert_number}` with `state=dismissed`, `dismissed_reason=not_used`. There is no bulk endpoint, which conveniently forces the per-alert reasoning SECALERT-03 requires.
- The graph refresh is rate-limited to once per hour — trigger it early in the phase, not as a final step.
- **Dismissing alerts is an outward-facing action on the GitHub repo.** Confirm with the operator before posting any dismissal.

**Evidence gathered so far** (during v9.0 research, 2026-08-25):

- All five are `System.Security.Cryptography.Xml` 8.0.0–8.0.3 against `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`.
- That manifest was deleted in commit `a477ab9` on 2026-06-29; the default branch is `main` and the manifest is absent there.
- The package appears in zero tracked `.csproj` files and does not surface in `dotnet list package --include-transitive`.
- **The alerts were created 2026-08-10 — six weeks after the manifest was deleted.** GitHub is minting fresh alerts off a stale cached dependency-graph snapshot. This is the strongest single piece of evidence, and also the reason a bare "the file is gone" dismissal is not defensible on its own.
- The leftover `EuphoriaInn.*` directories in the working tree contain only `bin/` and `obj/` — zero `.csproj`, zero `.cs` outside build output — and both are matched by `.gitignore:25`/`:26`. Deleting them is optional cleanup, not a deliverable.

**Risks this phase must actively avoid:**

- **Rubber-stamping.** A clean `dotnet list package` is the developer-facing view; alerts are driven by GitHub's separately-cached dependency graph. If any alert is in fact tracking a still-reachable reference, dismissing it would permanently suppress a real vulnerability with no further warning.
- **Losing the audit trail.** Five identical dismissal reasons with the same timestamp is indistinguishable from a rubber stamp six months later.

### Phase 74: Event Schema, CRUD, and Calendar Display

**Goal**: A DM can put a dated event on their board's calendar — informational only — and everyone sees it on both the desktop and mobile calendar, clearly distinct from a quest.
**Depends on**: Nothing in this milestone (independent of 72 and 73)
**Requirements**: EVENT-01, EVENT-02, EVENT-03, EVENT-04, EVENT-05, EVENT-06
**Plans**: 8/8 plans complete

Plans:
**Wave 1**

- [x] 74-01-PLAN.md — Wave 0 RED test scaffold: route-based Events CRUD facts, the quest-detail zero-event-markup assertion, and the quest-creation-unaffected negative (wave 1)
- [x] 74-02-PLAN.md — Three event entities, fail-closed query filters, delete behaviour, indexes, and one additive migration (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 74-03-PLAN.md — Event domain model, repository/service interfaces and implementations, AutoMapper entity maps, DI registration (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 74-04-PLAN.md — EventViewModel, view-model maps, and the DM-gated EventsController with server-side board stamping (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 74-05-PLAN.md — Events Create/Edit/Details views and the Create Event navbar entry on both layouts (wave 4)
- [x] 74-06-PLAN.md — Desktop calendar event block, chip styles, growable grid row, and Legend row (wave 4, not autonomous)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 74-07-PLAN.md — Mobile agenda filter, neutral empty state, event entries and their styles (wave 5, not autonomous)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 74-08-PLAN.md — Two-group tenant isolation suite plus desktop, mobile and navbar render assertions (wave 6)

**Success criteria:**

1. A DM can create, edit, and delete a one-off event with title, optional description, date, and optional start time, from a "Create Event" entry in the same navbar category as "Create Quest".
2. The event renders on the desktop calendar page, visually distinguishable from a quest at a glance.
3. The event renders on the mobile calendar page — which today lists only days that have quests.
4. Quest creation is provably unaffected: no validation, no warning, no blocking, regardless of what events exist on the chosen date.
5. An integration test using two distinct groups proves a board's events are invisible to the other.

**Scope notes:**

- Three new entities in one purely additive migration, following the `AddContactsFeature` precedent (ordered `CreateTable` calls, no backfill).
- Owns the storage convention and tenant scoping for the whole feature — both far cheaper to get right before any occurrence data exists than to correct after people have voted on it.
- `EventEntity` carries a nullable series reference, so a one-off event and a materialized occurrence are the same entity.

**Decisions locked before planning:**

- **`DateOnly` for the occurrence date and series anchor, `TimeOnly?` for the optional start time.** Both map natively to SQL Server `date`/`time` in EF Core 10. This deliberately does *not* follow `Quest.FinalizedDate`'s naive-local `DateTime` convention: that convention is only half-observed today (see the mixed local-vs-UTC comparison logged separately), and `DateOnly` makes the DST bug class structurally impossible rather than merely avoided by discipline. Confirm at discuss-phase — reversing gets sharply more expensive once occurrences exist.
- **No `EventType` field.** Meaning comes from the board's immutable `BoardType`.
- **No relation to `Quest`.** Events are informational by definition.

**Risks this phase must actively avoid:**

- **Cross-tenant leakage on write.** `HasQueryFilter` constrains reads only. A mis-scoped `GroupId` on an inserted event leaks across boards with no schema-level safety net. This app has shipped two real cross-tenant leaks (Phases 49/55).
- **A test harness that cannot see the bug.** `WebApplicationFactoryBase`'s `MutableGroupContext` defaults to a single group (`ActiveGroupId = 1`), so the standard integration test is structurally blind to the multi-group bug class. A dedicated two-group test is not optional.
- **Calendar view drift.** `_Calendar.cshtml` is called from 6 sites, but only `Views/Calendar/Index.cshtml` is in scope — the 5 `Quest/Details(.Mobile)` sites render the per-quest date-picker widget and must be left untouched. Bake that into the acceptance criteria rather than leaving it to code review.
- **The 7th touch point.** `Views/Calendar/Index.Mobile.cshtml` does not call the partial at all; it hand-rolls an agenda loop filtered by `.Where(d => !d.IsEmpty && d.QuestsOnDay.Any())`. Events stay invisible there until that filter changes.
- **A bad migration breaks boot** — migrations auto-apply on startup.

### Phase 75: Event Availability Signups

**Goal**: Players can say whether they are available for an event, with the right default for the board type — opt-in on One-Shot boards, opt-out on Campaign boards.
**Depends on**: Phase 74
**Requirements**: EVTAVAIL-01, EVTAVAIL-02, EVTAVAIL-03, EVTAVAIL-04, EVTAVAIL-05
**Plans:** 5/5 plans complete

Plans:
**Wave 1**

- [x] 75-01-PLAN.md — EventSignup domain model, repository, service, mapper and DI wiring, with the answered-marker stamping rule (wave 1)
- [x] 75-02-PLAN.md — Atomic campaign backfill on join and signup cleanup on leave, plus the Platform remove confirmation (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 75-03-PLAN.md — Availability write actions, roster view models, and the atomic create-time campaign fan-out (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 75-04-PLAN.md — Event details availability surface: answer buttons, withdraw control, roster, and the signup-aware delete confirmation (wave 3)
- [x] 75-05-PLAN.md — Lifecycle, ownership and two-group tenant isolation integration tests, and the validation sign-off (wave 3)

**Success criteria:**

1. On a One-Shot board, no signup exists for an event until a player creates one, and they can record Yes, Maybe, or No.
2. On a Campaign board, every member has a signup on each event with availability Yes from the moment the event exists, and opting out flips their own answer to No rather than deleting the signup.
3. A player can change their own availability at any time, and cannot change anyone else's.
4. A member joining a Campaign board is auto-signed-up to every event dated today or later; a member leaving has all of their event signups on that board removed, past and future.
5. An integration test using two distinct groups proves a player cannot read or write availability on another board's event.

**Scope notes:**

- `EventSignupEntity` reuses the existing `VoteType` { No, Maybe, Yes } and is far simpler than `PlayerSignupEntity` + `PlayerDateVoteEntity` — no `SignupRole`, no `CharacterId`, no waitlist ordering, because events have no roster concept.
- Signup writes must use narrow scalar-update methods mirroring `PlayerSignupRepository.ChangeVoteAsync`. The generic `BaseRepository.UpdateAsync` is off-limits for an entity with loaded `Signups` — the existing override exists precisely because AutoMapper overwrites navigation collections too aggressively.

**Risks this phase must actively avoid:**

- **"Yes by default" read as a real answer.** Every Campaign member starts at Yes, so a DM cannot tell "said yes" from "never looked". That is an accepted operator decision, so the fix belongs in presentation (Phase 77), but this phase must preserve enough state to make the distinction possible.
- **Silently resetting a deliberate answer.** A member who set No must never have it reset by a later auto-signup pass.

### Phase 76: Recurring Event Series

**Goal**: A DM can set up a repeating schedule — including "two sessions on, two off" — and get correct dates generated indefinitely, while still being able to cancel, move, or edit any single occurrence.
**Depends on**: Phase 75 (materialized occurrences must carry availability from the moment they exist)
**Requirements**: EVTRECUR-01, EVTRECUR-02, EVTRECUR-03, EVTRECUR-04, EVTRECUR-05, EVTRECUR-06, EVTRECUR-07, EVTRECUR-08
**Plans**: 15/15 plans complete

Plans:
**Wave 1**

- [x] 76-01-PLAN.md — Pure cycle-mask date generator with mask parsing, validation and unit tests (wave 1)
- [x] 76-02-PLAN.md — Series template fields, end date, cancelled marker, domain models and the filtered unique idempotency index migration (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 76-03-PLAN.md — Series repository plus the narrow occurrence-write methods, with repository constraint tests (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 76-04-PLAN.md — Domain series service: idempotent materializer, runway top-up, preview, lifecycle and edit-scope eligibility, with idempotency tests (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 76-05-PLAN.md — Nightly per-group rolling-window top-up job, its registration, and the corrected group-context documentation (wave 4)
- [x] 76-06-PLAN.md — Repeats toggle, cycle-mask strip and live server-computed preview on the Create Event form (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 76-07-PLAN.md — Occurrence cancel and restore, the server-side Delete refusal, cancelled banner and series link (wave 5)
- [x] 76-09-PLAN.md — Series detail page with read-only rule, occurrence table, End and the delete-or-detach removal (wave 5)
- [x] 76-10-PLAN.md — Cancelled chip and agenda styling, Legend row, and the DM-gated horizon banner (wave 5)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 76-08-PLAN.md — Save-scope prompt, this-and-future template sweep and the live-sibling collision notice on Edit (wave 6)

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 76-11-PLAN.md — Two-board series tenant isolation and refusal integration tests, plus full-suite coverage sign-off (wave 7)

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 76-12-PLAN.md — Developer verification of the four manual-only behaviours (wave 8, not autonomous)

**Wave 9** *(gap closure — blocked on the phase verification that found the gaps)*

- [x] 76-13-PLAN.md — Mobile calendar horizon banner, with the first automated coverage of the banner on either surface (wave 9, gap closure)
- [x] 76-14-PLAN.md — Campaign calendar navigation entry and events-only quest filtering, superseding the calendar clause of NAV-01 (wave 9, gap closure)

**Wave 10** *(blocked on Wave 9 completion)*

- [x] 76-15-PLAN.md — Requirements register correction and the navigation supersession record (wave 10, gap closure)

**Success criteria:**

1. A DM can define a series as base cadence (every N weeks on a weekday) + anchor date + repeating on/off cycle mask, and the generated dates match the mask exactly.
2. The setup screen previews the next ~10 generated dates live, before saving.
3. Occurrences exist ahead of time on a rolling window and are topped up automatically — an open-ended campaign never needs manual re-extension.
4. A single occurrence can be cancelled, moved to another date, or edited, with the rest of the series unaffected.
5. Running the generator twice produces no duplicates, does not resurrect a cancelled occurrence, and does not overwrite one that was moved or edited.
6. Two boards configured with mirrored masks on the same cadence and anchor produce interleaved, non-colliding dates.

**Scope notes:**

- The highest-complexity, highest-risk phase, deliberately isolated.
- **Custom cycle-mask generator, no library.** RFC 5545's `BYSETPOS` selects only within a single interval, not across periods, so this pattern is not expressible in RRULE — any library would need post-filtering anyway. A small Domain-layer generator is unit-testable in isolation with no dependency.
- Cycle mask stored as a comma-delimited `nvarchar(200)`; argued against JSON (no precedent in this schema), an int bitmask (opaque, caps cycle length), and a child table (a join for something never queried independently).
- Hangfire top-up job follows the existing `DailyReminderJob` / `HangfireJobHelper.RunInScopeAsync` pattern, including `IServiceScopeFactory`.

**Decisions locked before planning:**

- **Idempotency keys on `(EventSeriesId, SeriesSlotIndex)`, never on date.** A date-keyed check resurrects moved occurrences and cannot distinguish "cancelled" from "never created".
- **Rule edits are additive only.** Editing a series never retroactively deletes or rewrites occurrences that already exist — especially ones people have voted on. More aggressive regeneration is deferred (EVTRECUR-09).

**Decisions amended by this phase:**

- **The calendar clause of NAV-01 (Phase 37) is superseded.** Campaign boards regain the Calendar
  navigation entry on both layouts, because this phase put two campaign-relevant read surfaces on
  the calendar — the DM horizon banner and the cancelled-occurrence chip — and neither was
  reachable by navigation before. The campaign calendar is events-only: quests are excluded in
  `CalendarController` rather than hidden in a view, which also closed a leak present in shipped
  code — the calendar route never had a board-type gate at all. The four other campaign navigation
  restrictions (shop, manage shop, edit my profile, players) and the logged-out-visitor rule are
  untouched. `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent` was replaced by
  `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkPresent` in plan `76-14`.

**Gap closure:**

- Verification found two gaps against success criterion 3 (rolling occurrences topped up
  automatically): the horizon banner was missing from the mobile calendar, and the calendar was
  unreachable by navigation on campaign boards. Plans `76-13`, `76-14`, and `76-15` closed them —
  `76-13` ported the banner to the mobile calendar view, `76-14` restored campaign-board navigation
  and made the calendar events-only, and `76-15` corrected the tracking documents to match. The
  banner defect survived to verification because no automated test asserted the banner rendered on
  either calendar surface; both surfaces now have that coverage (`CalendarHorizonBannerTests`).

**Risks this phase must actively avoid:**

- **The stale `ActiveGroupContextService` doc comment.** It claims a null `ActiveGroupId` means "see all"; the Phase 55 filters are fail-closed and return **zero** rows. The job runs outside `GroupSessionMiddleware`, so it must call `SetGroupId()` per group and iterate — never `IgnoreQueryFilters()`.
- **The job silently stopping.** The calendar quietly runs dry at the horizon with no error anyone sees — the failure mode the rolling window trades for never needing manual extension. Surface a horizon check somewhere a human actually looks.
- **Retry re-running from scratch.** A global `AutomaticRetryAttribute` is already registered app-wide, so a partially-failed run re-executes fully. Idempotency is a hard requirement, not a nicety.

### Phase 77: Availability Overview Page

**Goal**: A DM can see, in one place, who is available for which upcoming events — and tell a real answer apart from an untouched default.
**Depends on**: Phase 75 (and most valuable once Phase 76 populates it with recurring sessions)
**Requirements**: EVTVIEW-01, EVTVIEW-02, EVTVIEW-03, EVTVIEW-04
**Plans**: 12/12 plans complete

Plans:
**Wave 1**

- [x] 77-01-PLAN.md — Bounded aggregate read path and in-memory availability aggregation (wave 1)
- [x] 77-02-PLAN.md — Cell/count stylesheets, navigation entries and calendar cross-links (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 77-03-PLAN.md — View models, desktop grid, mobile cards and the clamped controller action (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 77-04-PLAN.md — Two-group tenant isolation test and phase-wide filter-bypass audit (wave 3)

**Gap closure — Wave 1** *(run with `/gsd-execute-phase 77 --gaps-only`)*

- [x] 77-05-PLAN.md — Mobile paging control, growth-gated Show More, inert roster and bounded cells (gap wave 1)
- [x] 77-06-PLAN.md — Own-column highlight specificity and frozen-column overlap (gap wave 1)
- [x] 77-07-PLAN.md — Injected UTC clock for the upcoming window and validated overview options (gap wave 1)
- [x] 77-08-PLAN.md — Navigation test documentation and shared fixture state restore (gap wave 1)

**Gap closure — Wave 2** *(blocked on gap Wave 1)*

- [x] 77-09-PLAN.md — Mobile-user-agent rendering coverage, clamp/count assertions that can fail, requirements traceability (gap wave 2)

**Gap closure — Wave 3** *(blocked on gap Wave 2)*

- [x] 77-10-PLAN.md — Keyboard-accessible row and card navigation across all thirteen clickable sites (gap wave 3)

**UAT gap closure — Wave 1** *(from `77-UAT.md` test 1; run with `/gsd-execute-phase 77 --gaps-only`)*

- [x] 77-11-PLAN.md — Mobile card glass surface, WCAG-AA count contrast and filled controls (UAT gap wave 1)

**UAT gap closure — Wave 2** *(blocked on UAT gap Wave 1)*

- [x] 77-12-PLAN.md — Mobile overview styling-contract regression guard and validation map (UAT gap wave 2)

**Success criteria:**

1. A page shows upcoming events for the current board as a grid of events against players, with each player's availability.
2. An untouched default is visually distinct from an answer the player actually gave.
3. Each event shows an availability count, so a poorly-attended date is obvious at a glance.
4. No event or member from another board ever appears, proven by a two-group integration test.

**Scope notes:**

- Independent of the other event phases in code; sequenced last because its value density rises once recurring sessions exist.
- Query shape must avoid an N+1 across events × members × signups.

**Requires a discuss-phase decision:** whether the overview is DM-only or visible to all board members.

**Risks this phase must actively avoid:**

- **A repeat of the tenant-scoping trap on an aggregating page.** This page joins across members and signups, which is exactly where `IgnoreQueryFilters()` gets reached for. It must not be.

### Phase 78: Link Preview Foundation and Quest Cards

**Goal**: A quest link shared through a new "Copy shareable link" control renders a rich preview card — image, title, description snippet — in Discord, Slack, and iMessage, while an ordinary quest URL behaves exactly as it does today and the page itself still requires login.
**Depends on**: Nothing in this milestone (independent of 72–77; touches no event, signup, or calendar code)
**Requirements**: LINKPREV-01, LINKPREV-02, LINKPREV-03, LINKPREV-04, LINKPREV-05, LINKPREV-06, LINKPREV-07, LINKPREV-08, LINKPREV-09
**Plans**: 9 plans

Plans:
**Wave 1**

- [ ] 78-01-PLAN.md — Persist the Data Protection key ring to the database with a migration, and extend forwarded-header trust to scheme and host (wave 1)
- [ ] 78-02-PLAN.md — Confirm the reverse-proxy trust and public base URL on the App CT, and complete the deployment doc's environment contract (wave 1, not autonomous)
- [ ] 78-03-PLAN.md — Require a login on the quest Details GET and rewrite the anonymous-access regression test to assert the redirect (wave 1)
- [ ] 78-04-PLAN.md — Widen IActiveGroupContext with SetGroupId and implement it across all thirteen implementations (wave 1)
- [ ] 78-06-PLAN.md — Compose the branded 1200x630 card image from existing board art and prove it serves unauthenticated with no redirect (wave 1, not autonomous)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 78-05-PLAN.md — Link signing service, card description builder, and public absolute-URL builder, all unit-tested (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 78-07-PLAN.md — Anonymous signed preview route, standalone card view, group-session exemption, and the tamper/cross-board/token-scope tests (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 78-08-PLAN.md — Shared Copy shareable link partial, render-time minting, and both quest views (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [ ] 78-09-PLAN.md — Link-preview documentation and the deployed-host plus real-client acceptance pass (wave 5, not autonomous)

**Success criteria:**

1. Pasting a copied quest link into a real Discord channel shows a card carrying the quest title, a plain-text description snippet, and the site name — verified against Discord's own crawler, not a local `curl` that only proves the markup exists.
2. Pasting the plain, uncopied quest URL shows no card at all, and the page itself still behaves exactly as it does today.
3. Changing a single character of the signature renders no card — the request is rejected, not degraded to a generic card.
4. `curl -A Discordbot` against a signed URL on the deployed host returns `og:url` and `og:image` as absolute `https://` URLs on the real hostname, not `http://localhost`.
5. An integration test proves a signature minted for a quest in group A yields no data when replayed against a quest id in group B.
6. Opening a signed link in a logged-out browser still lands on the login page — the token unlocks card metadata, never page access or the ability to sign up.

**Scope notes:**

- **Forwarded headers must be fixed first, or nothing else in this phase can work.** `Program.cs:103` sets `ForwardedHeaders.XForwardedFor` only — no `XForwardedProto`, no `XForwardedHost`. Behind Traefik that makes every absolute URL the app generates wrong in both scheme and host, and a crawler silently renders no card with no error anywhere. This is a prerequisite, not a nice-to-have.
- Sign with ASP.NET Core Data Protection, which Identity already registers — no hand-rolled secret, no new key management, and keys already survive container restarts the same way auth cookies do.
- The meta block ships as a shared partial rendered into `_Layout.cshtml`'s `<head>` through a section, so Phase 79 extends it rather than copying it. `_Layout.cshtml` currently has no OG tags at all — this is greenfield markup.
- `Quest.Description` is Markdown (the Phase 66–71 rollout). The card description is derived plain text: Markdown stripped, whitespace collapsed, truncated to ~200 chars, HTML-escaped. Not the raw field.
- `Quest` has no image field, so the card image is a single branded static asset served unauthenticated at an absolute URL. Per-quest generated images are explicitly out of scope.
- Both desktop `Details.cshtml` and `Details.Mobile.cshtml` get the copy control — the same both-platforms-in-one-phase rule Phase 72 followed.

**Decisions locked before planning:**

- **Signed share links, not public cards and not a board-level toggle.** The card renders only for a URL carrying a valid signature minted by an authenticated member through an explicit "Copy shareable link" action. Sharing therefore requires a deliberate act, and an ordinary URL leaks nothing. Operator decision, taken 2026-08-26.
- **The signature covers entity type, entity id, and group id together** — never the id alone. A signature that authenticates only "some quest, id 47" is replayable across boards the moment two boards both have a quest 47.
- **Card presence is decided by the signature, never by User-Agent.** Crawler sniffing would make the card's behaviour depend on a header the sender controls, and would put this feature in the same bug class as the mobile-markup-that-never-renders case PROJECT.md already records.
- **An interactive Spotify-style iframe widget is not achievable and is not the target.** Those come from Discord's and Slack's hardcoded provider allowlists, which a self-hosted app cannot join. The deliverable is the standard rich card.

**Requires a discuss-phase decision:** whether the signature carries an expiry — and if so, whether an expired link degrades to no card or to a generic one — and whether a minted link can be revoked after the fact.

**Constraint added by Phase 93 (2026-10-21):** boards can now switch pages off, and Quests is one of the switchable pages. The anonymous preview route must ask `IBoardPageResolver` whether Quests is available on the signed board (the route scopes its read with `SetGroupId(verified groupId)`, which the resolver honours) and fall back to the generic response when it is not. The route stays unannotated, outside the page filter, and makes this check itself. The "Copy shareable link" control is hidden while Quests is off, like any other link into a switched-off page. A stale share URL then stops rendering its card while the page is off and works again once it is switched back on. This phase's 9 plans were written before this rule existed, so check them against it before executing. Full text: `.planning/phases/93-per-board-page-selection-and-home-page/93-RESEARCH.md` § Share-link previews.

**Risks this phase must actively avoid:**

- **Reaching for `IgnoreQueryFilters()`.** The preview read path must serve a quest whose group the caller has no session for, which is exactly the shape that invites bypassing the fail-closed filter at `QuestBoardContext.cs:281`. This app has shipped two real cross-tenant leaks (Phases 49/55); the filter is the remedy. The correct move is to set the group context from the *signature's* verified group id, not to switch the filter off.
- **A card that quietly never appears.** A wrong scheme, a relative `og:image`, or a redirect on the image URL each produce silence rather than an error — crawlers send no cookies and do not reliably follow redirects. Acceptance has to be an actual paste into Discord, not markup inspection.
- **Leaking more than the sender intended.** Truncation must happen on the rendered plain text; truncating the Markdown source can strip a fence and expose text the author had hidden inside it.
- **Treating the token as an access grant.** The signature must unlock metadata only. If it is ever accepted as authentication for the page or for a POST, a shared link becomes a permanent unauthenticated door into a private board.
- **External unfurl caches are permanent.** Discord and Slack cache a card server-side; deleting the quest or rotating the key does not retract a card already posted in a channel. That is a real limit of the feature and belongs in the docs, not in an assumption.

### Phase 79: Character and Contact Link Cards

**Goal**: The same signed-link mechanism extends to characters and contacts — portraits included — with an unrevealed contact never previewable, checked at the moment the card is served rather than the moment the link was made.
**Depends on**: Phase 78 (inherits the signing scheme, the meta partial, the absolute-URL helper, and the plain-text summarizer)
**Requirements**: LINKCARD-01, LINKCARD-02, LINKCARD-03, LINKCARD-04, LINKCARD-05, LINKCARD-06
**Plans**: TBD (run `/gsd-plan-phase 79`)

**Success criteria:**

1. A copied character link pasted into Discord shows a card with the character's name, level, and class, and their portrait as the image.
2. A copied link for a revealed contact shows name and location with the contact's image; the copy control is unavailable while a contact is unrevealed.
3. Reveal a contact, copy its link, un-reveal it, then re-fetch the signed URL: no card renders. The gate is evaluated at serve time, not baked into the token.
4. The signed image endpoint returns the stored bytes with an explicit correct `Content-Type` and `X-Content-Type-Options: nosniff`, is fetchable with no cookies, and falls back to the branded image when the entity has no portrait.
5. A two-group integration test proves a signature minted in group A returns nothing when replayed against a character or contact id in group B.
6. No `ContactNote` text ever reaches a card or an image response.

**Scope notes:**

- Both controllers are `[Authorize]` at class level (`CharactersController.cs:12`, `ContactsController.cs:13`), unlike `Quest/Details`. The preview path is therefore a narrow anonymous-allowed addition alongside them, not a relaxation of the existing attribute.
- Portrait bytes already exist on the entities — `Character.ProfilePicture` and `Contact.ContactImageData` — but today's serving endpoints sit behind `[Authorize]`. The signed image endpoint is net-new and must not widen the existing ones.
- `Character.HasProfilePicture` / `Contact.HasContactImage` already exist precisely so a view can branch on presence without loading the bytes; the fallback decision should use those, not a byte-length check.
- Card fields are deliberately narrow: name, level, and class for a character; name and location for a contact. `Backstory`, `Description`, and notes stay off the card.

**Requires a discuss-phase decision:** whether any board member can mint a share link for a character they do not own, or only the character's owner.

**Constraint added by Phase 93 (2026-10-21):** boards can now switch the Characters and Contacts pages off. The preview route must ask `IBoardPageResolver` whether Characters (for a character card) or Contacts (for a contact card) is available on the signed board, and fall back to the generic response when it is not, checked at serve time like `IsRevealed`. The route and the signed image endpoint stay unannotated, outside the page filter, and make this check themselves. The copy controls are hidden while the page is off. Full text: `.planning/phases/93-per-board-page-selection-and-home-page/93-RESEARCH.md` § Share-link previews.

**Risks this phase must actively avoid:**

- **`IsRevealed` checked at mint time only.** This is the sharpest risk in the phase and the reason contacts are separated from quests. `Contact.IsRevealed` is a DM-controlled spoiler gate; a token that captured "revealed" when it was minted turns un-revealing into a no-op and leaks an NPC into party chat. The check belongs on the serve path, every time.
- **Un-revealing cannot retract an already-posted card.** Discord and Slack cache unfurls server-side. Serve-time checking stops *new* leaks; it does not undo one already sitting in a channel. This must be stated in the UI or docs rather than assumed away, because it changes how a DM should treat the copy button.
- **Serving unauthenticated bytes from the database.** Without an explicit `Content-Type` and `nosniff`, an uploaded file that is not really an image becomes a content-sniffing vector on a path that requires no login at all. A size cap matters for the same reason — this endpoint is reachable by anyone holding the link, and by every crawler that sees it.
- **A third copy of the card markup.** Quests, characters, and contacts across desktop and mobile is six call sites. Extend Phase 78's partial; do not hand-copy it — the exact drift class PROJECT.md blames for the `Characters/Edit.cshtml` `classIndex` bug.

### Phase 80: Contact Categories

**Goal**: A DM can group the board's NPCs under named categories — "Corridor", "Guild Members", "Last Bastion" — and the Contacts index renders them under those headings instead of one flat list, on both desktop and mobile.
**Depends on**: Nothing (independent of the events chain 74–77 and the link-preview chain 78–79; touches only the Contacts feature)
**Requirements**: CONTACTCAT-01, CONTACTCAT-02, CONTACTCAT-03, CONTACTCAT-04, CONTACTCAT-05, CONTACTCAT-06, CONTACTCAT-07, CONTACTCAT-08, CONTACTCAT-09, CONTACTCAT-10, CONTACTCAT-11, CONTACTCAT-12, CONTACTCAT-13, CONTACTCAT-14, CONTACTCAT-15
**Plans**: 9/9 plans complete

Plans:
**Wave 1**

- [x] 80-01-PLAN.md — Mint the CONTACTCAT requirement family and complete the phase validation contract (wave 1)
- [x] 80-02-PLAN.md — ContactCategory entity, schema, migration, and the test seeding helper (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 80-03-PLAN.md — Category repository, service, DI wiring, and the unit suite (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 80-04-PLAN.md — View models, AutoMapper wiring, and category CSS (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 80-05-PLAN.md — Manage Categories controller, desktop and mobile views, and the management integration suite (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 80-06-PLAN.md — Grouped Contacts index on both platforms plus the suppression and ordering suite (wave 5)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 80-07-PLAN.md — Category assignment on the four Create/Edit forms plus the cross-group isolation suite (wave 6)
- [x] 80-08-PLAN.md — Category on both Details views plus the real-User-Agent mobile render suite (wave 6)

**Gap closure** *(from 80-UAT.md — both issues are contrast/legibility defects of the same family: card text is themed by element enumeration, and anything unenumerated falls back to a Bootstrap default that is wrong on the dark background)*

- [x] 80-09-PLAN.md — Mobile add-category label and zero-category helper link contrast fixes plus the styling guard suite (gap closure)

**Origin:** operator-relayed feature request from a board user, 2026-08-27 — "Misschien leuk … om NPC's in categorieën te kunnen onderverdelen? Dat ik verschillende kopjes/categorieën kan maken om de boel wat overzichtelijk te houden."

**Scope notes:**

- NPCs are `Contact` in this codebase (`QuestBoard.Domain/Models/Contact.cs`, `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`). Categories are a Contacts-only concern; nothing about Characters or Quests changes.
- Categories are per-group data, not global. Every read must sit behind the existing fail-closed group query filter (`QuestBoardContext.cs`) — this app has shipped two real cross-tenant leaks (Phases 49/55), and a category name is itself campaign-revealing.
- The index already carries two visibility gates that grouping must not disturb: `IsRevealed` (the DM spoiler gate) and the per-group "show hidden" toggle. A category heading must never disclose the existence of a contact the viewer cannot see — including an empty-looking or count-bearing heading.
- Uncategorised contacts need a defined home. Whether that is an "Ungrouped" heading or a flat remainder block is a discuss-phase decision, not an implementation detail.
- Both the desktop and mobile Contacts index views render the list — two call sites that should share one partial rather than diverge (the drift class PROJECT.md blames for the `Characters/Edit.cshtml` `classIndex` bug).

**Requires a discuss-phase decision:** whether a contact belongs to exactly one category (a single heading, matching how the requester described it) or to several, and who may create or rename categories — any DM-tier user, or admins only.

### Phase 81: Contact Tags and Filtering

**Goal**: Contacts can carry free-form tags — "shopkeeper", "quest giver" — independently of which category they sit under, and the Contacts index offers a filter that narrows the list to the selected tags.
**Depends on**: Phase 80 (shares the Contacts index rendering surface and whatever grouping partial that phase establishes)
**Requirements**: CONTACTTAG-01, CONTACTTAG-02, CONTACTTAG-03, CONTACTTAG-04, CONTACTTAG-05, CONTACTTAG-06, CONTACTTAG-07, CONTACTTAG-08, CONTACTTAG-09, CONTACTTAG-10, CONTACTTAG-11, CONTACTTAG-12, CONTACTTAG-13, CONTACTTAG-14, CONTACTTAG-15, CONTACTTAG-16, CONTACTTAG-17
**Plans**: 8/8 plans complete

Plans:
**Wave 1**

- [x] 81-01-PLAN.md — Mint the `CONTACTTAG-*` requirement family into REQUIREMENTS.md and the roadmap coverage table, and complete the phase validation contract (wave 1)
- [x] 81-02-PLAN.md — Data foundation: the board-scoped `ContactTag` entity, the app's first many-to-many join, its fail-closed query filter, a collation-backed unique index, the migration, the domain model, the entity mapping, the test seed helper, and cross-board filter coverage (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 81-03-PLAN.md — Repository and service tag persistence: split-queried tag loading, upsert-by-name reconciliation through a board-filtered query, orphan pruning on save and delete, comma-list parsing, and unit tests asserting against the database (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 81-04-PLAN.md — Index read path: tag view models, repeated-tag-id query-string binding, vocabulary derived from the visible-but-unfiltered set, in-memory union filtering after the visibility gate, the Show Hidden round trip, and filter-semantics integration tests (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 81-05-PLAN.md — Contact write path: viewer-scoped suggestion lists on both form GETs, comma-separated tag persistence on both POSTs, name-length validation, and no-script write-path integration tests (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 81-06-PLAN.md — Tag entry widget on all four create and edit views: a re-verified SRI-pinned CDN library, a thin local init module, scoped theme overrides on both platforms, and form markup tests under both user agents (wave 5)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 81-07-PLAN.md — Desktop display: the index filter row and its disabled state, chips on cards, the details tag line, the two-branch empty state, and markup tests for audience, vocabulary scoping, and escaping (wave 6)
- [x] 81-08-PLAN.md — Mobile display: the filter trigger and bottom drawer, chips on rows, the mobile details tag line, the two-branch empty state, and markup tests driven by a real mobile user agent (wave 6)

**Origin:** same request as Phase 80 — "Misschien later nog een filter optie, dat ik tags kan maken op bv shopkeeper en dat er dan gefilterd kan worden erop." The requester explicitly staged this after categories; it is separated here for that reason and can stay unplanned until wanted.

**Scope notes:**

- Tags are many-to-many and orthogonal to Phase 80's category — a contact in "Last Bastion" can also be tagged `shopkeeper`. Do not model tags as a second category column.
- Filtering must compose with the existing visibility gates rather than route around them: a tag filter narrows what the viewer could already see, never widens it. The filtered query has to run through the same group filter and reveal/hidden logic as the unfiltered index.
- Tag vocabulary is per-group. A tag list rendered in the filter UI leaks the group's tag names, so the vocabulary read needs the same tenancy treatment as the contacts themselves.
- Filter state belongs in the URL query string, not in session — the "show hidden" toggle's per-group session scoping (`ToggleShowHidden`) exists for a different reason and is not the pattern to copy here.
- **Phase 80 is not a hard blocker.** Phase 80 has a discussion record but no plans and no code, so Phase 81 is planned and verified against today's flat Contacts index. The composition requirement — category headings survive an active filter and empty ones drop out — is carried as a forward-compatibility guarantee: the tag filter is applied before any grouping step, so a later heading pass groups an already-narrowed list under its own suppression rule with no second rendering mode.

**Discuss-phase decisions settled:** multi-tag selection is OR (ticking more tags widens the result, matching the shop's rarity checkboxes), and every tag surface — authoring, chips, and the filter — is DM-tier only, so players neither see nor create tags. Both are recorded in `.planning/phases/81-contact-tags-and-filtering/81-CONTEXT.md`.

### Phase 82: Personal Cross-Board Event Agenda

**Goal**: A member who belongs to more than one board can see, in one place, every upcoming event they are expected at across all of their boards — with the board each event belongs to named on every row.
**Depends on**: Phase 77 (inherits its cell vocabulary, its next-N-with-paging window, and its date-only lower bound; the aggregate read is a different query and a different tenancy mechanism)
**Requirements**: EVTAGENDA-01, EVTAGENDA-02, EVTAGENDA-03, EVTAGENDA-04, EVTAGENDA-05, EVTAGENDA-06, EVTAGENDA-07, EVTAGENDA-08, EVTAGENDA-09, EVTAGENDA-10
**Plans**: 6/6 plans complete

Plans:
**Wave 1**

- [x] 82-01-PLAN.md — Mint the EVTAGENDA requirement family into REQUIREMENTS.md and ROADMAP.md, and complete the phase validation contract
- [x] 82-02-PLAN.md — Membership-pinned cross-board query, agenda service with its second-layer re-check, and the unit suite

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 82-03-PLAN.md — Agenda controller with the session-remembered board filter, desktop view with inline rosters, and the no-active-board middleware exemption

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 82-04-PLAN.md — Mobile agenda view with tap-to-reveal rosters, its stylesheet, and mobile render tests
- [x] 82-05-PLAN.md — Unconditional nav entries on both layouts, cross-links from the overview and calendar, and the details back-link

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 82-06-PLAN.md — Four-case cross-board tenant isolation suite, filter behaviour facts, and the phase-gate static audit

**Origin:** raised by the operator during Phase 77's discuss pass (2026-08-29) and deliberately not folded into it. Phase 77's EVTVIEW-04 is *"never displays events or members from another board"*, and its success criterion 4 requires a two-group integration test proving exactly that. A cross-board mode on the same page would make one test both prove and disprove the same property depending on a toggle, so this is a separate surface rather than a flag.

**Scope notes:**

- **This is a personal agenda, not a grid.** Across boards there is no single membership set, so the events × members matrix does not generalise. The row is one event; the payload is the viewer's own availability plus which board it is on. Do not port Phase 77's member axis.
- **It must not default to replacing the board-scoped view.** Every other surface in this app is board-scoped — quests, characters, shop, gold, nav. Phase 77's overview stays the board-scoped page it was built as.
- **It cannot live behind the Calendar nav gate.** That entry is gated on `activeBoardType is BoardType.OneShot or BoardType.Campaign`; a cross-board page has no active board type. It belongs beside **Switch Group** in the user dropdown — the control that exists because a user has more than one board.
- **Two safe cross-group mechanisms already exist, and both follow the same rule: bypass the ambient filter only while supplying the group explicitly.** Pick one, do not invent a third — `EventSeriesGenerationJob`'s per-group `SetGroupId()` iteration (Phase 76 D-126), and `GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`, which uses `IgnoreQueryFilters()` but pins `es.Event.GroupId == groupId` in the predicate and is `private`.
- Quests are explicitly out of scope. This is events only.

**Risks this phase must actively avoid:**

- **A bare `IgnoreQueryFilters()` with no group predicate.** This is the single highest-risk read in the application — it is an aggregating page whose whole purpose is to cross the tenancy boundary safely. The app has shipped two real cross-tenant leaks (Phases 49/55) and a third live gap was found during Phase 72's discussion. The scoping must be *by the viewer's own memberships*, never by "no filter".
- **Leaking board names or event titles from a board the viewer has left.** Membership is the authorisation, and it is checked at read time — not inferred from the existence of a signup row. Phase 75 D-20 deletes signup rows on leave, but that is a cleanup, not an access control.

**Requires a discuss-phase decision:** whether the agenda shows only events the viewer has answered, or every upcoming event on every board they belong to; and whether it replaces or supplements the board-scoped overview as the default landing place for a multi-board user.

## Phase Ordering Rationale

- Phases 72 and 73 share no code, no files, and no data — either order works, and neither blocks the other.
- 72 is sequenced first because it is the operator's driving request and carries the user-visible value; 73 is maintenance. Research suggested the reverse (bank the zero-risk win first, and start the rate-limited graph refresh early), so flipping them costs nothing if preferred.
- Within Phase 72, the shared partial must exist before either host view can call it.
- The events feature (74–77) is a strict dependency chain: schema → signups → recurrence → overview. Each phase ships a usable increment rather than scaffolding.
- Phases 80 and 81 share no code or data with the events chain or the link-preview chain and can be scheduled at any point. 80 must precede 81 only because 81 filters the list 80 reorganises; if categories are ever dropped, 81 stands on its own against a flat list.
- Recurrence (76) is sequenced *after* signups (75) rather than before, because materialized occurrences must carry availability from the moment they exist.
- **If scope needs cutting mid-milestone**, stopping after 75 leaves a complete, usable non-recurring events feature. Recurrence and the overview grid are separable value-adds, not blocking dependencies.
- Phases 78 and 79 (link previews) were appended on 2026-08-26 and are independent of the events chain — they touch no event, signup, or calendar code, so they can run before, after, or alongside 74–77.
- 78 must precede 79: it owns the signing scheme, the meta partial, the absolute-URL helper, and the Markdown-to-plain-text summarizer that 79 consumes. It also ships usable value on its own (quest cards), so stopping after 78 leaves a complete feature.
- Phase 82 was appended on 2026-08-29 out of Phase 77's discuss pass. It must follow 77 — not because it needs 77's code, but because 77 settles the cell vocabulary, the window, and the date boundary that 82 should reuse rather than re-decide. It is the last phase in the milestone by value density, not by dependency: nothing blocks on it, and dropping it leaves the events feature complete.
- Splitting 78 from 79 is a decision-boundary split, not a size split. 78 settles what a share link exposes and proves it on quests, the least sensitive entity. 79 inherits that mechanism and adds the rules unique to the sensitive ones — the `IsRevealed` spoiler gate and unauthenticated image serving.

## Requirements Coverage

| Requirement | Phase |
|-------------|-------|
| SIGNCHAR-01 | Phase 72 |
| SIGNCHAR-02 | Phase 72 |
| SIGNCHAR-03 | Phase 72 |
| SIGNCHAR-04 | Phase 72 |
| SIGNCHAR-05 | Phase 72 |
| SIGNCHAR-06 | Phase 72 |
| SIGNCHAR-07 | Phase 72 |
| SECALERT-01 | Phase 73 |
| SECALERT-02 | Phase 73 |
| SECALERT-03 | Phase 73 |
| SECALERT-04 | Phase 73 |
| SECALERT-05 | Phase 73 |
| EVENT-01 | Phase 74 |
| EVENT-02 | Phase 74 |
| EVENT-03 | Phase 74 |
| EVENT-04 | Phase 74 |
| EVENT-05 | Phase 74 |
| EVENT-06 | Phase 74 |
| EVTAVAIL-01 | Phase 75 |
| EVTAVAIL-02 | Phase 75 |
| EVTAVAIL-03 | Phase 75 |
| EVTAVAIL-04 | Phase 75 |
| EVTAVAIL-05 | Phase 75 |
| EVTRECUR-01 | Phase 76 |
| EVTRECUR-02 | Phase 76 |
| EVTRECUR-03 | Phase 76 |
| EVTRECUR-04 | Phase 76 |
| EVTRECUR-05 | Phase 76 |
| EVTRECUR-06 | Phase 76 |
| EVTRECUR-07 | Phase 76 |
| EVTRECUR-08 | Phase 76 |
| EVTVIEW-01 | Phase 77 |
| EVTVIEW-02 | Phase 77 |
| EVTVIEW-03 | Phase 77 |
| EVTVIEW-04 | Phase 77 |
| LINKPREV-01 | Phase 78 |
| LINKPREV-02 | Phase 78 |
| LINKPREV-03 | Phase 78 |
| LINKPREV-04 | Phase 78 |
| LINKPREV-05 | Phase 78 |
| LINKPREV-06 | Phase 78 |
| LINKPREV-07 | Phase 78 |
| LINKPREV-08 | Phase 78 |
| LINKPREV-09 | Phase 78 |
| LINKCARD-01 | Phase 79 |
| LINKCARD-02 | Phase 79 |
| LINKCARD-03 | Phase 79 |
| LINKCARD-04 | Phase 79 |
| LINKCARD-05 | Phase 79 |
| LINKCARD-06 | Phase 79 |
| CONTACTCAT-01 | Phase 80 |
| CONTACTCAT-02 | Phase 80 |
| CONTACTCAT-03 | Phase 80 |
| CONTACTCAT-04 | Phase 80 |
| CONTACTCAT-05 | Phase 80 |
| CONTACTCAT-06 | Phase 80 |
| CONTACTCAT-07 | Phase 80 |
| CONTACTCAT-08 | Phase 80 |
| CONTACTCAT-09 | Phase 80 |
| CONTACTCAT-10 | Phase 80 |
| CONTACTCAT-11 | Phase 80 |
| CONTACTCAT-12 | Phase 80 |
| CONTACTCAT-13 | Phase 80 |
| CONTACTCAT-14 | Phase 80 |
| CONTACTCAT-15 | Phase 80 |
| EVTAGENDA-01 | Phase 82 |
| EVTAGENDA-02 | Phase 82 |
| EVTAGENDA-03 | Phase 82 |
| EVTAGENDA-04 | Phase 82 |
| EVTAGENDA-05 | Phase 82 |
| EVTAGENDA-06 | Phase 82 |
| EVTAGENDA-07 | Phase 82 |
| EVTAGENDA-08 | Phase 82 |
| EVTAGENDA-09 | Phase 82 |
| EVTAGENDA-10 | Phase 82 |
| EVTNAME-01 | Phase 83 |
| EVTNAME-02 | Phase 83 |
| EVTNAME-03 | Phase 83 |
| EVTNAME-04 | Phase 83 |
| EVTNAME-05 | Phase 83 |
| EVTNAME-06 | Phase 83 |
| EVTNAME-07 | Phase 83 |
| CONTACTTAG-01 | Phase 81 |
| CONTACTTAG-02 | Phase 81 |
| CONTACTTAG-03 | Phase 81 |
| CONTACTTAG-04 | Phase 81 |
| CONTACTTAG-05 | Phase 81 |
| CONTACTTAG-06 | Phase 81 |
| CONTACTTAG-07 | Phase 81 |
| CONTACTTAG-08 | Phase 81 |
| CONTACTTAG-09 | Phase 81 |
| CONTACTTAG-10 | Phase 81 |
| CONTACTTAG-11 | Phase 81 |
| CONTACTTAG-12 | Phase 81 |
| CONTACTTAG-13 | Phase 81 |
| CONTACTTAG-14 | Phase 81 |
| CONTACTTAG-15 | Phase 81 |
| CONTACTTAG-16 | Phase 81 |
| CONTACTTAG-17 | Phase 81 |
| CALFEED-01 | Phase 84 |
| CALFEED-02 | Phase 84 |
| CALFEED-03 | Phase 84 |
| CALFEED-04 | Phase 84 |
| CALFEED-05 | Phase 84 |
| CALFEED-06 | Phase 84 |
| CALFEED-07 | Phase 84 |
| CALFEED-08 | Phase 84 |
| CALFEED-09 | Phase 84 |
| CALFEED-10 | Phase 84 |
| CALFEED-11 | Phase 84 |
| CALFEED-12 | Phase 84 |
| CALFEED-13 | Phase 84 |
| CALFEED-14 | Phase 84 |
| CALFEED-15 | Phase 84 |
| CALFEED-16 | Phase 84 |
| CALFEED-17 | Phase 84 |
| QUESTFEED-01 | Phase 85 |
| QUESTFEED-02 | Phase 85 |
| QUESTFEED-03 | Phase 85 |
| QUESTFEED-04 | Phase 85 |
| QUESTFEED-05 | Phase 85 |
| QUESTFEED-06 | Phase 85 |
| QUESTFEED-07 | Phase 85 |
| QUESTFEED-08 | Phase 85 |
| QUESTFEED-09 | Phase 85 |
| QUESTFEED-10 | Phase 85 |
| QUESTFEED-11 | Phase 85 |
| QUESTFEED-12 | Phase 85 |
| QUESTFEED-13 | Phase 85 |
| QUESTFEED-14 | Phase 85 |
| QUESTFEED-15 | Phase 85 |
| QUESTFEED-16 | Phase 85 |
| QUESTFEED-17 | Phase 85 |
| QUESTFEED-18 | Phase 85 |
| CALTZ-01 | Phase 88 |
| CALTZ-02 | Phase 88 |
| CALTZ-03 | Phase 88 |
| CALTZ-04 | Phase 88 |
| CALTZ-05 | Phase 88 |
| CALTZ-06 | Phase 88 |
| CALTZ-07 | Phase 88 |
| CALTZ-08 | Phase 88 |
| CALTZ-09 | Phase 88 |
| CALTZ-10 | Phase 88 |
| CALTZ-11 | Phase 88 |
| CALTZ-12 | Phase 88 |
| CALTZ-13 | Phase 88 |
| CALTZ-14 | Phase 88 |
| CALTZ-15 | Phase 88 |
| CALTZ-16 | Phase 88 |

**Coverage:** 150/150 requirements mapped ✓ · 0 unmapped · 0 phases awaiting requirements

## Research Flags

Phases 72 and 73 needed no research step — both were researched to implementation-ready depth with verified file paths and line numbers. See `.planning/research/SUMMARY.md`.

**Phase 78 needs a research step.** Open Graph and Twitter Card behaviour is defined by each consumer, not by a spec: Discord, Slack, iMessage, and WhatsApp differ on description length, image aspect ratio and size limits, redirect handling, and cache invalidation. Getting those wrong produces a silently absent card rather than an error, so the limits must be established before planning rather than discovered by trial. Phase 79 inherits the findings and needs no separate research pass.

**Phases 80 and 81 need no external research.** Both are ordinary EF Core schema-plus-CRUD work against a feature that already exists in this codebase; what they need is a discuss-phase pass on the modelling questions noted under each phase, not a web search.

**Phase 82 needs no external research.** The two safe cross-group read mechanisms it must choose between already exist in this codebase and are named in its scope notes; what it needs is a discuss-phase pass, not a web search.

**Phase 84 needs a research step.** RFC 5545 defines the file format but not how a client behaves, and the behaviour is what decides whether a subscription works: Apple Calendar, Google Calendar and Outlook differ on how often they refetch a subscribed URL (Google's interval is measured in hours and is not controllable by the publisher), on whether they honour `X-PUBLISHED-TTL` and `REFRESH-INTERVAL` at all, on how they treat `STATUS:CANCELLED` versus a VEVENT that simply vanishes, and on floating-time versus `TZID` interpretation. Every one of those fails silently — a feed that parses cleanly and shows the wrong day, or never updates — so the limits need establishing before planning rather than after a subscriber notices. This is the same class of problem as the Phase 78 unfurl research. Phase 85 inherits the findings and needs no separate pass; its open questions are product decisions about which quests count, which belong in a discuss pass.

### Phase 83: Availability Surface Naming and Placement

**Goal**: The two availability surfaces say what they are and sit where the people who need them will look — the cross-board personal view and the board-scoped grid stop competing for the same reader.
**Requirements**: EVTNAME-01, EVTNAME-02, EVTNAME-03, EVTNAME-04, EVTNAME-05, EVTNAME-06, EVTNAME-07
**Depends on**: Phase 82 (both surfaces must exist before they can be named against each other)
**Plans**: 4/4 plans complete

**Origin:** raised by the operator on 2026-08-30, immediately after Phase 82's UAT. Once the personal agenda shipped, the board-scoped overview from Phase 77 felt redundant to players, and its name — "Availability Overview" — reads as though it covers everything on the board rather than events only.

**Scope notes:**

- **This is a naming and discoverability phase, not an authorization phase.** No permission gate is added to either surface.
- **Rename the pair so each says whose availability it shows.** "My Agenda" (the viewer's own, across every board they belong to) and "Board Availability" (this board, every member). The current name does not say whose availability or which board, which is the actual defect — the events-versus-quests ambiguity is secondary.
- **Move the board-scoped overview's navigation entry under the Dungeon Master menu.** Players stop tripping over a page whose remaining value to them is a duplicate of what the agenda already shows.
- **Every discoverable link to the overview becomes DM-only** — the navigation entry, the calendar page's cross-link, and the header buttons the two surfaces point at each other with. A player's remaining route is a bookmark or a shared URL. *(Amended 2026-08-30 during the discuss pass; the scope note previously kept the calendar cross-link visible to everyone. See Phase 83 CONTEXT D-08.)*
- **The overview page itself keeps no authorization gate.** A player who reaches it gets a normal 200 and the full grid — no 403, no redirect. This is a soft hide, not a permission change; the rejection below still stands and must not be quietly implemented by adding a policy to the controller action.
- Nobody is stranded by the above: "My Agenda" sits in the user dropdown unconditionally for every authenticated user (Phase 82 D-08), outside every board-type condition.

**Why not make the overview DM-only** (considered and rejected, 2026-08-30): gating it would hide strictly *less* than the personal agenda already reveals. Phase 82 D-02 puts the full roster on every agenda row, across every board the viewer belongs to, on an unrestricted page — so restricting the grid while the list shows the same facts one click away buys no privacy and costs a regression for anyone using it. The legibility concern that motivates gating (an aggregate makes "who never answers" visible in a way per-event views do not) applies at least as strongly to the agenda, so a gate on the overview alone does not address it.

**Reopen condition:** if the agenda's per-row roster is ever reduced to counts, the two surfaces stop overlapping and a DM-only gate on the overview becomes coherent. That is a scope change to Phase 82's D-02, not a permission tweak, and would need its own decision.

**Risks this phase must actively avoid:**

- **Breaking navigation.** This touches both layouts, and navigation regressions have bitten this codebase before — a prior phase shipped a nav entry on desktop but not mobile, and another gated a nav entry to the wrong board type and made a whole surface unreachable. `LayoutNavigationTests` asserts on strings rather than markup structure, so a move can pass existing tests while leaving the entry unreachable in practice. New cases are needed for the moved entry on both layouts.
- **Renaming only the visible label.** The route, controller, view folder, page title, cross-link copy, nav entry, and the tests that assert on the old string all carry the name. A partial rename leaves two names for one page.

Plans:

**Wave 1**

- [x] 83-01-PLAN.md — Rename and subtitle the Board Availability page on both layouts, gate its My Agenda header button to Dungeon Masters, and add the shared header-subtitle style rule (wave 1)
- [x] 83-02-PLAN.md — My Agenda's matching subtitle and Dungeon-Master-only return link, the renamed and gated Calendar cross-link, and the re-seeded Calendar button test class (wave 1)
- [x] 83-03-PLAN.md — Move the navigation entry into the Dungeon Master menu on both layouts, collapse the desktop Calendar dropdown, and replace the four role-blind navigation cases with the six role-flip theories (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 83-04-PLAN.md — Player reachability case for the deliberately open page, the retired-label guard class across all three surfaces, and the requirement and roadmap ledger close-out (wave 2)

### Phase 84: Calendar Feed Foundation and Event Subscription

**Goal**: A board member can point their phone's calendar at a personal subscription URL once and have every event from every board they belong to appear there on its own — all-day entries, timed entries, later edits, and cancellations included — without opening the quest board.
**Requirements**: CALFEED-01, CALFEED-02, CALFEED-03, CALFEED-04, CALFEED-05, CALFEED-06, CALFEED-07, CALFEED-08, CALFEED-09, CALFEED-10, CALFEED-11, CALFEED-12, CALFEED-13, CALFEED-14, CALFEED-15, CALFEED-16, CALFEED-17
**Depends on**: Phase 82 (reuses the membership-scoped cross-board event read built there) and Phase 83 (the two availability surfaces must be settled before a third read surface is added over the same data)
**Plans**: 8/8 plans complete

**Origin:** raised by the operator on 2026-09-17 — the board already knows every date, but getting those dates onto a phone means retyping them by hand.

**Scope notes:**

- **A one-way subscription feed, not two-way sync.** The endpoint serves `text/calendar` over HTTPS and the calendar client polls it on its own schedule. This is deliberately not CalDAV: answering availability and signing up stay on the board, and nothing a reader does in their calendar app ever writes back.
- **One personal feed per user, covering every board they belong to.** This follows the Phase 82 agenda precedent rather than the board-scoped overview's. There is deliberately no per-board feed URL — a reader who wants one board only can filter in their calendar client, and a second URL shape would double the token surface for no gain.
- **The URL is the credential.** A calendar client sends no cookies and cannot be asked to log in, so the endpoint is anonymous and the token in the path is what authorises it. That makes the token a bearer secret with no expiry: it must be unguessable, per-user, and revocable.
- **Series occurrences are emitted one VEVENT per occurrence, not as an `RRULE`.** Phase 76 already materialises each occurrence as its own row with its own `CancelledAt` tombstone, so a recurrence rule would have to reconstruct exceptions that the rows already express exactly.
- **Both Profile layouts ship together.** `Views/Account/Profile.cshtml` has a `.Mobile` twin, and shipping a control on one and not the other is a recorded failure mode in this codebase (Phases 43, 54, 72). The subscribe surface is the whole point of the phase for a phone user, so a desktop-only version delivers nothing.
- **Revocation ships with minting, not after.** See the first risk below.

**Decisions reached** — settled in the discuss pass on 2026-09-17 and recorded in `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md`, which is authoritative over the summaries below:

- **What a wall-clock time means.** The codebase has no timezone handling anywhere — `grep TimeZoneInfo` across the solution returns nothing, and an event stores a naive `DateOnly` plus a nullable `TimeOnly`. A calendar client has to be told how to read `19:00`. Floating local time (no `TZID`) is the smallest change and matches the naive model exactly, but a reader whose phone is in another timezone sees the wrong hour. A real `VTIMEZONE` block is correct but introduces this application's first timezone concept.
- **How long an event lasts.** There is no end time in the schema at all. The feed has to emit something — a fixed default duration, or a start-only entry — and the choice is visible on every row of every subscriber's calendar.
- **Whether a cancellation is emitted or omitted.** `STATUS:CANCELLED` shows the reader that the thing was called off; dropping the VEVENT entirely relies on the client noticing its disappearance, which clients handle inconsistently.
- **Hand-rolled writer or a library.** RFC 5545 requires CRLF line endings, folding at 75 octets with a specific continuation rule, and escaping of `,`, `;`, `\` and newlines inside text values. Getting any of these subtly wrong produces a feed that one client accepts and another silently rejects. This project has taken on focused packages before (Markdig, HtmlSanitizer, AngleSharp) where the format was the risk.

**Risks this phase must actively avoid:**

- **A leaked subscription URL cannot be un-leaked.** Apple and Google store the URL on their own servers and refetch it indefinitely, so it outlives the device it was entered on. The only remedy is rotation, which is why a regenerate control belongs in the same phase that mints the first token — and why the token must never be written to a log line in full.
- **Losing the tenancy guard on a surface nobody watches.** The cross-board agenda pairs its membership-scoped query with a second-layer re-check and a `LogError` on any surviving foreign row, precisely because a dropped predicate is invisible in a rendered page. A feed is worse: it is read by a machine, so a leak could run for months with no reader to notice. The same guard has to apply here.
- **Assuming an active group.** Every read surface in this application except the agenda leans on `IActiveGroupContext` and the tenant query filter. This endpoint has no session, no cookie and no active group, so any dependency that quietly expects one will either throw or return an empty feed that looks like "no events scheduled".
- **Unstable VEVENT `UID`s.** A UID that changes between polls makes the subscriber's phone accumulate a fresh copy of every event on every refresh rather than updating in place. The UID has to derive from the event's identity, not from anything regenerated per request.
- ~~**Markdown leaking into `DESCRIPTION`.**~~ **Retired** — 84 D-10 drops the description entirely, so there is nothing for Markdown to leak into and `IMarkdownService` needs no plain-text target.
- **Updates that clients refuse.** `EventEntity` carries `CreatedAt` but no modified timestamp, so there is nothing to drive a `SEQUENCE` bump when an event is edited. Clients that honour `SEQUENCE` may keep serving the stale copy.
- **Real-device subscription check deferred to deployment, not observed.** The closing plan's real-phone checkpoint was deferred by operator decision rather than run: Outlook and Google Calendar fetch server-side from their own infrastructure, so no localhost or LAN address can satisfy them, and the check needs either a public tunnel or the deployed application. Refresh latency and whether an edited event ever stays stale after several refreshes were therefore not observed and are not recorded here — they remain open until someone runs the check against a real deployment. Independently of that gap, a live feed response was run through an external RFC 5545 conformance validator and returned zero errors and zero warnings, so the document itself is proven even though no client's actual poll-and-render behaviour has been.

Plans:

**Wave 1**

- [x] 84-01-PLAN.md — Mint the CALFEED requirement family into REQUIREMENTS.md and ROADMAP.md, and complete the phase validation contract (wave 1)
- [x] 84-02-PLAN.md — End-to-end tracer: subscription table and migration, the signup-rooted cross-board feed query with its second-layer re-check, a minimal RFC 5545 writer, and the anonymous token-authenticated feed endpoint (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 84-03-PLAN.md — Full RFC 5545 writer: timed and all-day branches, floating local time, transparency, board-prefixed titles with vote suffixes, source-namespaced stable identifiers, folding and escaping, plus the exact-byte unit suite (wave 2)
- [x] 84-04-PLAN.md — Feed hardening: configurable rolling window, tombstone status codes, throttled last-fetched write, per-address rate limiting and address-free logging (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 84-05-PLAN.md — Feed behaviour suite: log-capture harness, four two-group tenant isolation cases, every response code, the inclusion and exclusion rules, both window bounds and the fetch-time throttle (wave 3)
- [x] 84-06-PLAN.md — Subscription management plumbing: QR package and renderer, absolute address builder, view models, and the add, rename and revoke Profile POST actions (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 84-07-PLAN.md — The Calendar Subscription section on both Profile layouts, with copy, calendar-handoff link, QR modal, rename modal and delete confirm (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 84-08-PLAN.md — Both-layout markup and round-trip suite, the phase static guard, the real-device subscription check, and the requirement and roadmap ledger close-out (wave 5)

### Phase 85: One-Shot Quests in the Calendar Feed

**Goal**: The same subscription also carries the quest sessions the reader is actually part of — from their one-shot boards only — so a phone calendar shows the night they are playing, not just the board's informational events.
**Requirements**: QUESTFEED-01, QUESTFEED-02, QUESTFEED-03, QUESTFEED-04, QUESTFEED-05, QUESTFEED-06, QUESTFEED-07, QUESTFEED-08, QUESTFEED-09, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-13, QUESTFEED-14, QUESTFEED-15, QUESTFEED-16, QUESTFEED-17, QUESTFEED-18
**Depends on**: Phase 84 (the token, the endpoint, the writer and the subscribe surface must all exist before a second source can be added to the feed)
**Plans**: 6/6 plans executed

**Origin:** raised by the operator on 2026-09-17 alongside Phase 84, with the board-type restriction stated up front.

**Scope notes:**

- **Quests come only from boards where `BoardType` is `OneShot`.** This is the operator's constraint, not an inference: a campaign board's quests are not wanted in a personal calendar. `BoardType` already sits on `GroupEntity` as an int (`OneShot = 0`, `Campaign = 1`, `QuestBoard.Domain/Enums/BoardType.cs`), so this is a predicate on the query rather than anything new in the schema.
- **`IBoardTypeResolver` cannot be used here.** It resolves the *active* group's type and returns null when no group is active — and this feed has no session and no active group by construction. The board type has to be read per-group from the rows the query already loads. Its own doc comment warns that the null case is a distinct state and must not be defaulted, which is exactly the trap a cross-board caller would fall into.
- **Events keep their existing reach.** Phase 84 puts events from *every* board in the feed. This phase narrows quests only; it must not retroactively restrict events to one-shot boards.
- **No new read surface in the application UI.** This phase adds quests to a feed, not a page. If a cross-board quest list turns out to be useful on screen, that is its own phase.

**Locked by the operator during Phase 84's discuss pass (2026-09-17)** — recorded here so this phase does not relitigate them. Full context in `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md`:

- **Only quests the reader is signed up for.** Not every quest on their one-shot boards. This matches the event rule Phase 84 settled (84 D-17), so both sources in the feed read the same way.
- **Only once the quest is finalized.** A quest with `FinalizedDate` set. Proposed dates never reach the feed — a phone should not fill with candidate dates that will mostly not happen, and a date vote in progress is not a commitment.

**Decisions reached** — settled in the discuss pass on 2026-09-18 and recorded in `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-CONTEXT.md`, which is authoritative over the summary below:

- **A session the reader runs as DM counts (D-01).** A DM holds no signup row on their own quest, so the query is a union of two branches — signup rows for the owner, plus quests where the reader is the Dungeon Master — deduplicated by quest id so a quest matching both branches still emits exactly one entry with one identifier.
- **A waitlisted signup does not count (D-02).** Only a confirmed seat (`IsSelected == true`) reaches the feed; a promotion off the waitlist reaches the phone at the next fetch like any other change.
- **All three signup roles count identically (D-03).** Player, Spectator and AssistantDM seats all reach the feed the same way once confirmed — this falls out of D-02 at zero extra cost, since only a Player ever lands on the waitlist.
- **The `DungeonMasterSession` flag does not filter a quest out (D-04).** The flag hides a quest from the board listing but is not an access control anywhere else in the codebase; a granted seat is honoured regardless.
- **A quest is a fixed, configurable block starting at `FinalizedDate` (D-05).** Four hours by default, always timed and never all-day, with the duration on `CalendarFeedOptions.QuestDurationHours` and a refuse-to-start guard below one hour.
- **Every quest entry is `TRANSP:TRANSPARENT` (D-06).** One transparency rule for the whole feed, matching events; the operator considered and declined `OPAQUE`.
- **A quest entry's title carries no quest marker (D-07).** `[Board] Title`, unchanged from the event convention — a narrow phone day view truncates a wider title anyway.
- **A quest entry carries no `(DM)` suffix (D-08).** A session the reader runs reads identically to one they play; there is one `SUMMARY` rule for every quest row.
- **A quest that stops qualifying disappears silently (D-09).** No `STATUS:CANCELLED`, matching the event rule for cancellations.
- **Quests share the event feed's rolling window (D-10).** The existing `MonthsBack`/`MonthsAhead` bounds, no second pair of knobs.
- **Query shape.** The D-01 union is expressed as a single `Quests`-rooted query with a seat-or-Dungeon-Master `Any()`/`||` disjunction inside one `Where`, rather than a literal `Union`+`Distinct` — each quest is visited at most once, so the dedup requirement is satisfied structurally rather than by a later distinct step.
- **Merge ordering.** The combined event+quest document orders every entry by date, then start time with no sentinel substituted for an absent time, then source, then source id — matching the event query's own null-first ordering rather than reversing it with a sentinel.

**Two of this section's original open questions became moot rather than being answered.** D-02 excludes every waitlisted signup, so there is nothing left to mark and no quest suffix was needed or added — the roadmap's "whether the vote-marker convention carries over" question dissolved along with it. And `Close` is rejected outright on a one-shot board (`QuestController.cs:760`), so `IsClosed` is unreachable for any quest this phase can ever emit — the "or a quest is closed" question never had a case to answer.

**Risks this phase must actively avoid:**

- **A brand-new cross-board read with no guard.** No cross-board quest query exists today — `GetUpcomingAcrossGroupsWithSignupsAsync` is the only one of its kind and it is events-only. This phase writes the second, and it must carry the same membership scoping, the same second-layer re-check and the same `LogError` on a surviving foreign row. A fresh query is exactly where that pattern gets forgotten. **Addressed:** `CalendarSubscriptionService.GetFeedAsync`'s quest branch carries the same pinned-membership-set query, the same `IgnoreQueryFilters()` + in-memory re-check, and the same `LogError` on a surviving foreign row that the event branch already had — proven by a unit suite (`CalendarSubscriptionQuestRecheckTests`) that drives the service directly with a misbehaving fake repository to reach the drop-and-log branch a real, filtered repository cannot trigger.
- **Two filters that can be confused for one.** Membership and board type are independent: a reader belongs to boards of both types, and the quest predicate has to apply *both*. Collapsing them — filtering on board type and trusting the tenant filter for membership, or the reverse — produces either a leak or a silently empty section. **Addressed:** membership and board type are two independent predicates applied inside one condition (the reader's one-shot board-id set, derived from the same membership read the feed already holds), each proven independently against a second board in the same fetch — a campaign board's quest excluded while its event still appears, and a non-member board's seeded seat excluded while a genuinely qualifying quest still appears.
- **Colliding `UID`s with events.** A quest and an event can share an integer id. If the UID scheme is not namespaced per source, subscribing makes one silently overwrite the other in the reader's calendar. **Addressed:** identifiers are namespaced by source (`CalendarFeedSource.Quest` alongside `Event`), with a unit fact pinning a quest and an event sharing the same numeric id to two distinct, non-colliding `UID`s.

**Gaps this phase does not claim to have closed:**

- **Relational translation of the new quest predicate is unproven.** Every integration fact in this phase runs on the EF Core in-memory provider, which cannot prove the seat-or-Dungeon-Master disjunction and the board-type predicate translate to SQL Server. This is the third consecutive phase to carry this gap; the compensating manual check is recorded in `85-VALIDATION.md`'s Manual-Only Verifications table and is not closed here.
- **The real-device subscription check inherited from Phase 84 remains unobserved.** Outlook and Google Calendar fetch server-side and cannot reach a localhost or LAN address, so no client's poll-and-render behaviour — refresh latency, in-place update, or what a client does when an entry disappears between polls — has been observed by this phase or the one before it. No output of this phase makes any claim about it.

Plans:

**Wave 1**

- [x] 85-01-PLAN.md — Mint the QUESTFEED requirement family into REQUIREMENTS.md and ROADMAP.md, and key the phase validation contract to real task ids (wave 1)
- [x] 85-02-PLAN.md — End-to-end tracer: the quest source wired through repository, service, writer and the live feed address, plus the configurable session length and the source-aware writer suite (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 85-03-PLAN.md — Seat and Dungeon Master predicate suite: both branches that put a quest in the feed, the single-entry guarantee when a reader is both, the confirmed-seat rule, all three seat kinds, and the Dungeon Master session flag (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 85-04-PLAN.md — Disappearance and window suite: every way a quest stops qualifying, and both bounds of the shared rolling window (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 85-05-PLAN.md — Board-type narrowing, cross-board isolation with its logged drop, the events-keep-their-reach guard, and merged-document ordering (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 85-06-PLAN.md — Requirement and roadmap ledger close-out, validation sign-off, and the phase static guard (wave 5)

### Phase 86: Viewer-Local Times and Correct Job Scheduling

**Goal**: A reader sees every real timestamp in their own browser's timezone instead of UTC, and the three nightly sweeps fire at the hour their registration claims — without moving a single game night by so much as a minute.
**Requirements**: None — `.planning/REQUIREMENTS.md` carries no Phase 86 rows. Traceability runs on `86-CONTEXT.md`'s locked decision IDs D-01…D-07 instead; no REQ-IDs were invented.
**Depends on**: No hard dependency. It must not regress Phase 84's floating-local-time contract or Phase 85's quest entries — see the first risk below.
**Plans**: 7/7 plans executed

**Origin:** raised by the operator on 2026-09-18, immediately after noticing that the Calendar Subscription section's "Last fetched" timestamp reads two hours behind a Dutch wall clock. The investigation that followed found the display defect the operator reported *and* a scheduling defect they had assumed was working.

**Two defects, one root cause — this application has no timezone concept at all.** `grep TimeZoneInfo` across the solution still returns nothing outside test fixtures, exactly as Phase 84's research recorded.

- **Every rendered instant is UTC.** `CalendarSubscriptionService` stores `timeProvider.GetUtcNow().UtcDateTime` and `Profile.cshtml` renders it with no conversion and no zone label, which is representative rather than exceptional: there are roughly 71 date-render call sites across the views in 19 distinct format strings, none of which convert.
- **The nightly sweeps do not fire when their comments say they do.** Two independent causes that compound. `docker-compose.yml` and `Dockerfile` set no `TZ` and mount no `/etc/localtime`, so the container clock is UTC and every `DateTime.Today`/`DateTime.Now` in production is UTC — 18 call sites across `EventSeriesService`, `CalendarController`, `GroupRepository` and `DailyReminderJob`. Independently, Hangfire's `RecurringJob.AddOrUpdate` defaults to `TimeZoneInfo.Utc` and none of the three registrations in `Program.cs` passes one. The "09:00" reminder sweep therefore fires at 11:00 Dutch summer time. The comments at `Program.cs` and `DailyReminderJob.cs` that assert "server local time (CET/CEST)" are wrong and must be corrected rather than preserved.

**Scope notes:**

- **The two defects ship together because they share the classifying work.** Deciding which `DateTime` is a real instant and which is naive wall-clock is the bulk of the effort for either half; splitting them would mean doing that analysis twice and risking two different answers.
- **Rendering is client-side (operator's decision, 2026-09-18).** Emit the instant in a machine-readable `<time datetime="...Z">` attribute and format it in the browser with `Intl.DateTimeFormat` against the viewer's resolved zone. No stored preference, no cookie, no server-side zone detection, and DST handled by the platform. A server-rendered fallback must be present so a pre-hydration or no-JS view shows UTC *with an explicit zone label* rather than a bare wrong-looking time.
- **The container's `TZ` stays unset.** Setting it would move `DateTime.Today` to local while Hangfire's cron stayed UTC, converting today's consistent-but-wrong behaviour into a genuine mismatch between the two clocks. The fix for job timing is an explicit `TimeZoneInfo` on each registration, not an ambient container setting.
- **This is a display and scheduling phase, not a schema migration.** No stored value changes meaning and no column is rewritten. If the instant-versus-wall-clock distinction turns out to deserve expression in the type system, that is its own phase.

**The distinction the whole phase turns on.** `DateTime` carries two incompatible meanings in this schema and the type does not tell them apart:

| Real instants — stored UTC, *must* convert | Naive wall-clock — *must not* convert |
|---|---|
| `CreatedAt` (9 entities), `UpdatedAt`, `CancelledAt` | `QuestEntity.FinalizedDate` |
| `LastFetchedAt`, `RevokedAt`, `SentAt` | `ProposedDateEntity.Date` |
| `SignupTime`, `LastVoteChangeTime` | `EventEntity.Date` + `StartTime` |
| `TransactionDate`, `ListedDate`, `DeniedAt` | `ShopItemEntity.AvailableFrom`/`AvailableUntil` |

The right-hand column is what a Dungeon Master typed — "seven o'clock on the twelfth" — and is already local by intent. `QuestEntity.ClosedDate` and `FinalizedEmailSentForDate` are unclassified and need a decision in the discuss pass rather than an assumption.

**Open questions for the discuss pass:**

- Which side of the table `ClosedDate` and `FinalizedEmailSentForDate` fall on.
- Which timezone the three sweeps should actually run in — a fixed `Europe/Amsterdam`, or configuration, given the board has no per-user zone and the operator is the only audience for job timing.
- Whether emails (rendered server-side, with no browser to ask) should carry an explicit zone label, since the client-side mechanism cannot reach them.
- Whether a relative rendering ("2 hours ago") is wanted for audit-style timestamps where the exact instant matters less than recency.

**Risks this phase must actively avoid:**

- **Shifting the calendar feed.** `QuestEntity.FinalizedDate` is what Phase 85 emits into the subscription, and Phase 84 D-09 fixed it as floating local time with no `TZID`. Anything that treats it as UTC moves every subscriber's session by the offset — and because a client refreshes on its own schedule, nobody would see it for hours. The feed writer must be provably untouched by this phase.
- **Converting a wall-clock date.** The same mistake inside the application shifts a game night on the board, on the quest page, in the date-vote list and in every reminder email. The classification above is the control, and it needs tests that pin a wall-clock value as unmoved across a non-UTC viewer zone.
- **Fixing one clock and not the other.** Today's two UTC clocks agree. Correcting the Hangfire schedule without correcting `DateTime.Today` inside `DailyReminderJob` — or the reverse — leaves the sweep computing "tomorrow" on a different clock than the one that woke it, which is a worse state than the one this phase starts from.
- **Missing the mobile twins.** There are 53 `.Mobile.cshtml` views and 15 of them render dates. Shipping a change on one layout and not its twin is a recorded failure mode in this codebase (Phases 43, 54, 72, and called out again in Phase 84's scope notes).
- **A flash of UTC.** Client-side formatting rewrites the DOM after paint. Without a deliberate fallback the reader sees the wrong time first and the right one a moment later, which reads as a bug even though the final value is correct.

Plans:

- [x] 86-07-PLAN.md

**Wave 1**

- [x] 86-01-PLAN.md — Board-clock seam (`TimeZoneOptions`/`IBoardClock`), `Html.LocalTime`, `site.js` hydration, proven end-to-end on the Profile "Last fetched" timestamp (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 86-02-PLAN.md — All three Hangfire sweeps pinned to the board zone, `board-timezone` health check reporting Degraded, `DailyReminderJob` on the same clock (wave 2)
- [x] 86-03-PLAN.md — The D-07 ambient-clock migration: `EventSeriesService` ×7, `GroupRepository`, `CalendarController`, `EventsController`, `SeriesController`, `Series/Details.cshtml` (wave 2)
- [x] 86-04-PLAN.md — Render sites A: QuestLog, Contacts, Platform Group — including splitting the four `FinalizedDate ?? ClosedDate` coalesce sites (wave 2)
- [x] 86-05-PLAN.md — Render sites B: Quest Manage/Details/_QuestCard, Shop, ShopManagement, Admin EmailStats (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 86-06-PLAN.md — Regression guards (wall-clock unmoved, calendar feed untouched, ambient-clock invariant), tech-debt correction, human verification (wave 3)

### Phase 87: Cross-Board Deep Link Recovery

**Goal:** A member who follows a link to a quest, event, character or contact on a board they belong to lands on that page, on that board -- instead of the error they get today because a different board happens to be selected in their session.
**Requirements**: No REQ-IDs. Settled in the discuss pass -- this phase runs on the locked `D-NN` decision IDs in `87-CONTEXT.md`, the way Phase 86 did. The decisions are implementation choices about a single recovery path rather than independently verifiable user-facing capabilities, so REQUIREMENTS.md rows would restate the same content one level vaguer.
**Depends on:** No hard dependency. Phase 82 built the only cross-board switch UX that exists today -- the Agenda's confirm-then-switch modal -- and this phase generalises it, so that work is a prerequisite in practice and already shipped.
**Plans:** 4/4 plans complete

**Origin:** raised by the operator on 2026-09-21 from live use across two boards. Following a link that points at another board's page returns an error rather than the page, and the objection is that the restriction lands on the wrong person: "They have access to the board, so why restrict it?" This is deliberately a revision of a decision the operator remembers making -- strict session-scoped tenancy -- not a bug report against it. The complaint is about working in multiple boards being a pain, so "fewer clicks" is part of the goal, not a nice-to-have.

**What actually happens today.** `IActiveGroupContext.ActiveGroupId` reads a single board id out of Session (`ActiveGroupContextService`), and `QuestBoardContext` keys 18 global query filters off it. A read for an entity on any other board therefore returns nothing, the controller cannot tell "does not exist" apart from "exists on your other board", and it answers `NotFound()` -- see `QuestController.Edit` and every sibling action shaped like it. The 404 is a correct consequence of the filter. What is wrong is the *response to a legitimate member*, and nothing in the current design carries enough information to answer differently.

**The security property that must survive.** A viewer who is not a member of the target board must keep getting exactly what they get today, with no new signal. In particular, a recovery flow must not become an existence oracle: offering "switch to Board 2?" for a board the viewer does not belong to, while a nonexistent board returns a flat 404, leaks both board membership and board existence. Whatever this phase adds has to produce the same observable output in both cases.

**The pattern to generalise, not invent.** Phase 82's Agenda already solves this once: `Views/Agenda/Index.cshtml` renders a confirm-then-switch modal that POSTs `GroupPicker.SelectGroup` with a `returnUrl`, and `SelectGroup` re-verifies membership server-side before it touches Session. That is the trusted seam, and it already carries the warning text explaining that switching changes what the viewer sees everywhere else. The open work is reaching that seam from an arbitrary deep link, rather than only from a page built knowing the target board up front.

**Open questions for the discuss pass:**

- **Auto-switch or confirm-then-switch.** Switching silently is the fewest clicks and the biggest surprise -- it repoints quests, shop, gold, characters and navigation, which is exactly what the Agenda modal warns about before doing it. Confirming keeps the warning but adds a click to every cross-board link, which is the friction the operator is complaining about.
- **Where the resolution lives.** Middleware ahead of the query filters, a per-controller resolution helper, or board-qualified routes (`/b/{board}/quest/42`) that make the target explicit instead of inferred. The third removes the guessing entirely but touches every URL the app emits -- including links already sitting in somebody's mail.
- **How the target board gets identified at all.** Current URLs carry an entity id and no board, so something has to resolve the entity with the filter off. That means a deliberate, narrow, audited escape hatch from the 18 filters and an answer to who may call it.
- **Whether writes participate.** A POST arriving for another board cannot be answered with a redirect -- `GroupSessionMiddleware` already documents why a 302 re-issues as a GET and drops the body. The 409 it returns today may simply be the right answer for non-idempotent requests, leaving this phase to GET/HEAD.
- **Whether emails and the calendar feed are in scope.** Those are the links most likely to be opened days later against a stale session, which makes them the strongest argument for the phase -- and also the ones that cannot be re-rendered after the fact.

**Risks this phase must actively avoid:**

- **Weakening the 18 query filters.** Any mechanism that reads across boards is a hole in the tenancy boundary by construction. It has to be one narrow call path with its own tests, not an `IgnoreQueryFilters()` that spreads by copy-paste.
- **Becoming a membership oracle.** Covered above; it is the most likely way to get this phase wrong while appearing to work.
- **Repointing a session as a side effect of a GET.** A link merely fetched -- a preview, a crawler, a browser prefetch -- must not silently change which board the viewer is on.
- **Blurring into the no-active-board path.** `GroupSessionMiddleware`'s null-`ActiveGroupId` gate and its `returnUrl` round-trip are separately tested. A wrong-board path added beside it must stay distinguishable from a missing-board one.
- **Missing the mobile twins.** Any new confirm surface needs its `.Mobile.cshtml` twin, and those are user-agent-selected rather than viewport-selected, so devtools emulation never exercises them. Shipping one layout and not the other is a recorded failure mode here (Phases 43, 54, 72).

Plans:
**Wave 1**

- [x] 87-01-PLAN.md — Tracer: one cross-board deep link end to end (registry, filter-bypassing lookup, shared switcher, middleware, banner)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 87-02-PLAN.md — Widen to all 18 routes, with the IgnoreQueryFilters allowlist test landing first
- [x] 87-03-PLAN.md — Skip the group picker when the return URL already names one of the viewer’s boards

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 87-04-PLAN.md — Oracle parity and authorization-boundary proofs, then blocking human verification

### Phase 88: Calendar Feed Times Anchored to the Board Timezone

**Goal:** A game night set for 18:00 on the board shows as 18:00 in every subscriber's phone calendar, whichever calendar app they use — instead of 19:00 on one phone and 20:00 on another, as it does today.
**Requirements**: CALTZ-01, CALTZ-02, CALTZ-03, CALTZ-04, CALTZ-05, CALTZ-06, CALTZ-07, CALTZ-08, CALTZ-09, CALTZ-10, CALTZ-11, CALTZ-12, CALTZ-13, CALTZ-14, CALTZ-15, CALTZ-16
**Depends on:** No hard dependency. It builds on Phase 84's writer and Phase 85's quest entries, and needs the board timezone that Phase 86 introduced (`IBoardClock.TimeZone`, configured by `TimeZoneOptions.BoardTimeZoneId`, default `Europe/Amsterdam`).
**Plans:** 9/9 plans complete

**Origin:** raised by the operator on 2026-09-30 from production use. A quest set to 18:00 on the board appears as 19:00 in the operator's phone calendar and as 20:00 on a friend's phone.

**Root cause — the feed never says which timezone its times are in.** `CalendarFeedWriter.AppendTimedEvent` writes `DTSTART:yyyyMMddTHHmmss` and `DTEND` as floating local time: no trailing `Z`, no `TZID`, no `VTIMEZONE`, and no `X-WR-TIMEZONE` fallback. This was Phase 84's D-01, which assumed the phone would show a floating time in whatever zone the phone is in. Production shows that assumption does not hold for every client. Both wrong hours match a client reading 18:00 as UTC and converting it to Amsterdam time: +2 during summer time (until 25 October 2026) and +1 after it. So either the two entries fall on opposite sides of the clock change, or the two phones' calendar apps read floating time differently. Record which app and OS each phone runs, and the date of the affected entry, before choosing a fix.

**D-01's reason for rejecting a timezone no longer applies.** It turned down `TZID=Europe/Amsterdam` because that "becomes this application's first written-down timezone". Phase 86 has since written one down. The board zone exists and is what `IBoardClock` already uses for every server-side "what day is it" read. One caveat carries straight into this phase: startup only checks that a zone id is *present*. `BoardClock` resolves the id once and falls back to UTC on an id it cannot resolve, instead of failing. A feed that stamps the *configured* id while the clock has quietly fallen back to UTC would be wrong in a new way. Whatever zone the feed declares must be the zone actually resolved.

**Scope notes:**

- **This deliberately reverses a test-pinned contract.** `CalendarFeedFloatingTimeGuardTests`, the `NotContain("TZID")` / `NotContain("VTIMEZONE")` facts in `CalendarFeedWriterTests`, and the `…Z`-absence assertions in `CalendarSubscriptionFeedTests` and `CalendarSubscriptionQuestFeedTests` were written to stop exactly this change. Rewrite them to pin the new contract; do not delete them. The same goes for `.claude/architecture.md`, whose "Time and the board clock" section describes the feed as floating, and for the writer's own comments.
- **The stored value does not change meaning.** A game night stays a wall-clock value: 18:00 in the board's zone. The fix tells the client *which* zone that 18:00 belongs to. It must not convert the stored value into a different hour. A subscriber in the board's zone sees 18:00. A subscriber abroad sees the correct local equivalent, which is the right answer for a session held at a physical table.
- **Candidate encodings for the discuss pass.** (a) `DTSTART;TZID=Europe/Amsterdam` plus a `VTIMEZONE` block carrying the DST rules. This is the RFC 5545-correct route; the block has to be generated from the configured zone, not hard-coded. (b) UTC with a trailing `Z`, converted per entry date using the board zone's rules. This is the simplest thing for a client to read, but a DST mistake makes the entry an hour off for part of the year. (c) `X-WR-TIMEZONE` alone. It is non-standard and not honoured everywhere, so it fits a supplement better than a fix.
- **All-day entries are out of scope.** `DTSTART;VALUE=DATE` has no time of day to shift.
- **Proving it needs real phones.** Unit tests can pin the bytes. They cannot show how iOS Calendar, Google Calendar and Outlook render them. Verification must include an already-subscribed phone correcting its *existing* entries on its next refresh, not just new ones, because the UID stays the same and `SEQUENCE` is fixed at 0.

Plans:

**Wave 1**

- [x] 88-01-PLAN.md — Tracer: the board zone declared end to end on the live feed (TZID on timed entries, generated VTIMEZONE, X-WR-TIMEZONE, zone taken only from the board clock), plus minting the CALTZ requirement family (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 88-02-PLAN.md — The zone document pinned byte by byte: guard renamed and rewritten, clock-change, southern-hemisphere, Windows-id, UTC, all-day and quoting facts, and the one-way SEQUENCE bump (wave 2)
- [x] 88-03-PLAN.md — Live-feed zone variants (non-default, unresolvable and Windows-style zone) and the board-clock seam guard extended to the feed (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 88-04-PLAN.md — Byte pins proven on Linux, the architecture guidance rewritten, full-suite gate and validation sign-off, and the production check on Google and Apple Calendar (wave 3)

**Gap closure G-88-4 — Wave 1** *(run with `/gsd-execute-phase 88 --gaps-only`; a rescheduled entry carried no revision signal, so revision-comparing clients kept the old time)*

- [x] 88-05-PLAN.md — Tracer: per-entry revision columns with a one-time bump of every existing row, the store-side revision hook, and a rescheduled quest going out with a higher SEQUENCE and a later DTSTAMP; plus the hook's unit proofs and write-seam guards (gap wave 1)

**Gap closure G-88-4 — Wave 2** *(blocked on Wave 1 completion)*

- [x] 88-06-PLAN.md — Every event write path proven through the real controllers (edit, series sweep, cancel and restore, no-change save), and a reader's own availability change moving only that reader's stamp (gap wave 2)
- [x] 88-07-PLAN.md — Every quest write path proven through the real controllers (reopen then finalize at another date and at the same date, title edit, no-change save) (gap wave 2)
- [x] 88-08-PLAN.md — The writer's revision contract pinned byte by byte: LAST-MODIFIED, the sequence floor, rewritten whole-block, stamp and guard pins (gap wave 2)

**Gap closure G-88-4 — Wave 3** *(blocked on Wave 2 completion)*

- [x] 88-09-PLAN.md — Architecture guidance and validation rewritten, full suite, byte pins proven on Linux, the migration and bump proven on the local SQL Server, and the production reschedule re-test handed to verify-work (gap wave 3)

### Phase 89: Pull-Based Release Deployment

**Goal:** A tagged release reaches production because the server goes and fetches it, not because GitHub pushes it there. The server checks for new releases on a timer, verifies the download, installs it, and confirms the app came back healthy. GitHub does not need a runner, credential or any other link to the production box.
**Requirements**: TBD
**Depends on:** No hard dependency on earlier v9.0 phases. It touches the release pipeline and the server, not application behaviour.
**Plans:** 12/12 plans complete

**Origin:** raised by the operator on 2026-10-04: bring this app's deployment in line with the `ing-dashboard` project. The deploy should be a pull, not a push through a runner, so GitHub never has a handle on the server.

**How it works today:** `binary-release.yml` builds and publishes `questboard-<tag>.zip` to a GitHub release on a hosted runner when a `v*.*.*` tag is pushed. Its `deploy` job then runs with `runs-on: self-hosted` and calls `/home/questboard/deploy.sh <tag>`. That runner is a GitHub Actions runner registered to this public repository and installed on the production App CT (`docs/server-setup.md`, section "Install the GitHub Actions runner"). Any workflow that targets `self-hosted` therefore runs code on the production server. The deploy script exists only on the server, not in the repo. It downloads the zip with `wget`, runs `rm -rf /opt/questboard/*`, unzips and restarts. It does no integrity check, no health check and no rollback.

**Reference model — `ing-dashboard` (`deploy/`, `.github/workflows/release.yml`, `docs/deploy.md`):**

- A hosted runner builds the archive and attests its provenance with `actions/attest-build-provenance`. It saves the sigstore bundle as a release asset and attaches everything to a **draft** release. Approval through a `deploy` environment publishes that release, and approval is the only manual step.
- On the server, a systemd timer (`*-deploy-poll.timer`: 2 min after boot, then every 5 min with jitter) reads the public `releases/latest` endpoint without a token. When it finds a newer version, it hands the tag to an installer that lives in the repo and ships inside each release.
- The installer downloads the archive and its bundle and runs `gh attestation verify` offline, refusing anything that fails. It unpacks into a versioned `releases/<version>` directory and switches the active release. It then restarts the app and waits for `/health`. If the new release comes up unhealthy and no migration ran, it rolls back automatically; if a migration ran, it stops for a person instead. It records the outcome by email, in metrics and in the journal.
- Manual subcommands are `poll`, `install <tag>`, `rollback <version>` and `verify`.

**Scope notes:**

- **This is a port, not a copy.** `ing-dashboard` runs a separate EF migration bundle before activating a release. This app applies migrations in `context.Database.Migrate()` on startup instead, so "did a migration run" has to be worked out differently. One option is to compare the release's migration list with `__EFMigrationsHistory` before and after. The rollback hazard is unchanged: once a newer schema is in place, putting the previous release back is unsafe.
- **Remove the push path completely.** Delete the `deploy` job and its `workflow_dispatch` redeploy input from `binary-release.yml`. A manual redeploy becomes an `install <tag>` run on the server. Deregistering the runner from the repository and uninstalling it from the App CT are operator steps, so the plan has to hand them over explicitly. If the runner stays registered, a later workflow could still target it.
- **The installer moves into the repo.** Replace the server-only `/home/questboard/deploy.sh` with a versioned script and systemd units under a `deploy/` directory, and rewrite `docs/server-setup.md` sections 3–5 to match.
- **The Docker path is untouched.** `docker-publish.yml`, the `Dockerfile` and `docker-compose.yml` stay a working self-host route with no new setup steps.
- **Mind the email budget.** The Resend relay has a hard daily send limit. A 5-minute poll that emails on every run would use it up, so only install outcomes should send mail, not idle polls.
- **Keep the existing paths.** `/opt/questboard`, `/etc/questboard/.env` and `questboard.service` stay where they are unless the discuss pass decides otherwise, so the first pull-based deploy does not also become a relocation.

**Requires a discuss-phase decision:** whether to adopt provenance attestation and offline verification, or settle for a checksum; whether to add the draft-release approval gate, or keep "push a tag, it ships"; how failed installs are reported (email, journal only, or metrics); and the poll interval.

Plans:
**Wave 1**

- [x] 89-01-PLAN.md — Tracer: release artifact contract (package script, migrator status, /health version header, versioned-layout drop-in) plus DB-less migrator guards

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 89-02-PLAN.md — Migrator backup and all-or-nothing apply, gated real-SQL proof, migrator-sql CI job
- [x] 89-03-PLAN.md — Installer core: config loader, secret-free mail, outcome decision, remember-and-skip, activation, pruning
- [x] 89-04-PLAN.md — release.yml (build, test, attest, draft, approved publish), tag validation, GitHub settings checker; binary-release.yml removed

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 89-05-PLAN.md — Installer verification (checksum, pinned attestation, main ancestry) and release handling (staging, migrator via systemd-run, health wait)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 89-06-PLAN.md — questboard-deploy dispatcher: poll, install, rollback, verify; full outcome-matrix flow tests
- [x] 89-07-PLAN.md — Poll unit and timer, deploy.conf, setup with adoption of the running install, SQL-CT backup pruning

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 89-08-PLAN.md — Network tamper-refusal test, workstation release verifier, CI jobs for scripts, lint and network
- [x] 89-09-PLAN.md — docs/deploy.md, docs/releasing.md, server-setup.md rewrite, PROJECT.md env path fix

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 89-10-PLAN.md — Operator handover 1: hosted CI proof, GitHub gates, first attested release

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 89-11-PLAN.md — Operator handover 2: workstation verification, CT cutover with setup, first pull-based install

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 89-12-PLAN.md — Operator handover 3: retire the self-hosted runner, two-sided verification, idle-poll mail check

### Phase 90: Server OS and Database Platform Upgrade

**Goal:** Production runs on operating system and database versions that stay supported. The App CT and the SQL CT each move to a target version chosen in the discuss pass, without losing data and with the pull-based deploy still working end to end afterwards. The SQL CT must be off Ubuntu 22.04 before its standard support ends (about May/June 2027).
**Requirements**: TBD
**Depends on:** Phase 89. The App CT's verification uses the pull-based installer (setup, poll, `/health`), and the SQL CT move relies on the installer's backup and status commands.
**Plans:** 0 plans

**Origin:** raised by the operator on 2026-10-06, after moving CI to `ubuntu-26.04` (PR theunschut/quest-board-dnd#159): keep the servers in step, and check whether SQL Server can follow.

**Starting point:**

- **Proxmox host** `pve` (192.168.6.6): PVE 9.2.5, `pve-container` 6.1.11, kernel 7.0.12. A single node that also runs Traefik, Postfix, foundryvtt, Ledger and cabinet, so a host reboot stops all of them.
- **App CT** (CT 102 `QuestBoard`, 192.168.6.12): Ubuntu 24.04.4. ASP.NET Core 10 app from the Ubuntu archive packages `dotnet-runtime-10.0` / `aspnetcore-runtime-10.0` (10.0.9 installed, 10.0.12 available). Versioned layout under `/opt/questboard`, poll timer, `gh` from GitHub's apt repo, Data Protection keys in `/home/questboard/.aspnet`.
- **SQL CT** (CT 103 `MS-Sql`, 192.168.6.10): Ubuntu 22.04, SQL Server 2022 from `packages.microsoft.com/ubuntu/22.04/mssql-server-2022`, Microsoft key in the legacy `/etc/apt/trusted.gpg`. Database about 70 MB. Pre-migration backups and the prune script live in `/var/opt/mssql/data`.

**Research:** `.planning/research/ubuntu-26.04-ct-upgrade.md` (2026-10-06, 37 cited sources). Headline facts:

- The host already supports Ubuntu 26.04 guests: `pve-container` 6.0.12+ lists 26.04, and the official 26.04 template exists. No host upgrade is required, only routine 9.x updates.
- App CT: an in-place 24.04 → 26.04 upgrade is supported (LTS path open since 26.04.1). .NET 10 is in the 26.04 archive and supported by Microsoft. Unconfirmed: the migrator's sandboxed `systemd-run` inside a 26.04 LXC (needs `nesting=1`), and 26.04's Rust coreutils and sudo-rs against the installer's commands.
- SQL CT: SQL Server 2022 supports only Ubuntu 20.04/22.04. SQL Server 2025 supports 22.04 and 24.04 (CU1+) but **not 26.04**, and Microsoft publishes no `ubuntu/26.04` mssql-server repository (checked 2026-10-06). No SQL Server 2026 exists. Microsoft also supports SQL Server as a Docker container on any recent Linux, at the cost of Docker inside LXC.
- Moving SQL Server 2022 → 2025 is one-way (no restore to an older version). The database keeps compatibility level 160 until raised, the app's SQL login has to be recreated on a new instance, the edition must be one licensed for production, and APT 3.x on newer Ubuntu no longer trusts `/etc/apt/trusted.gpg`.

**To decide in discuss-phase:**

- Target per CT: App CT to 26.04 or stay on 24.04 for now; SQL CT to a new 24.04 CT with SQL Server 2025, an in-place 2022 → 2025 package upgrade first, or SQL Server in Docker on 26.04.
- In place vs a fresh CT for each, and whether the IPs stay the same.
- Order and timing (the research suggests host updates first, App CT around November 2026, SQL CT by Q1 2027), maintenance windows, and how much rehearsal on a clone is required.
- Rollback for each step, given that the SQL move is one-way once production writes land on the new instance.
- Whether CT access for the work runs through the operator or another temporary, scoped root grant like phase 89's.

**Constraints:** no data loss; Proxmox snapshot and an off-box, test-restored database backup before anything touches production; the pull-based installer must pass `setup`, a poll and a version-checked `/health` after the App CT move; `/home/questboard/.aspnet` must survive (losing it signs everyone out); never print `/etc/questboard/env`.

Plans:

- [ ] TBD (run /gsd-plan-phase 90 to break down)

### Phase 91: Linked Boards with a Shared Calendar

**Goal:** A super admin can link two or more boards of the same board type in the Platform area. On every linked board, `/calendar` then shows the events and quests of all boards in the link as well as its own, each marked with the board it belongs to, so a campaign split across boards for the same group of players never gets a date booked twice. Unlinking restores each board to its own calendar. Boards that are not linked behave exactly as they do today.
**Requirements**: TBD
**Depends on:** Phase 87. Calendar links to an entry on another board go through its cross-board deep-link switch. Independent of Phase 90.
**Plans:** 10/10 plans complete

**Origin:** raised by the operator on 2026-10-15. The same group of players runs several campaigns as separate boards. The operator wants to start a new one-shot board for another campaign and still see its quests and events on the existing board's calendar, so the two never plan the same date.

**Starting point** (scoped 2026-10-15):

- **Boards:** a board is `Group` (`QuestBoard.Domain/Models/Group.cs`, entity `GroupEntity`). The active board comes from the session through `IActiveGroupContext`. Every tenant entity has a fail-closed global query filter on `GroupId == ActiveGroupId` (`QuestBoardContext.cs`). Any read that crosses boards must use `IgnoreQueryFilters` from a file on the `CrossBoardIgnoreQueryFiltersSeamTests` allowlist (currently five repository files).
- **Board type:** `BoardType` has two values, `OneShot` and `Campaign`. It is set when a board is created and cannot be changed afterwards (Edit shows it disabled), so a same-type link cannot later become mixed.
- **Platform area:** `Areas/Platform/Controllers/GroupController.cs` under the `SuperAdminOnly` policy has Index, Create, Edit, Delete (empty boards only) and member management. There is no link concept yet.
- **`/calendar`:** `CalendarController.Index` reads quests via `QuestRepository` and events via `EventRepository`. Both rely only on the query filter and fetch every row; `CalendarViewModel` filters to the month. Quests are skipped entirely on Campaign boards. DM tier also gets the series-runway warning.
- **Views:** desktop `Calendar/Index.cshtml` renders the shared `_Calendar` partial. `Calendar/Index.Mobile.cshtml` does **not** use the partial; it builds its own agenda list from `GetCalendarDays()`, so both layouts need the change.
- **Other `_Calendar` users:** `Quest/Details.cshtml` (three call sites) and `Quest/Details.Mobile.cshtml` (two). There the partial is quests-only and doubles as the date-vote form (`IsDetailsPage`, `VoteIndexLookup`). This is the place where seeing a linked board's dates would most directly prevent a clash while proposing or voting.
- **Existing cross-board reads to reuse:** Phase 82's `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync` (filter-bypassing and narrowed to the viewer's board ids) and Phase 87's `CrossBoardLinkRegistry` / `CrossBoardDeepLinkMiddleware`, which switch the session board when a member opens Quest or Event Details on another board. A non-member gets the normal 404.
- **Calendar feed:** `CalendarSubscriptionService` already spans all of the subscriber's boards by membership, and quests come only from OneShot boards. A board link does not touch that rule, so the feed is unaffected unless the discussion decides otherwise.
- **My Agenda and the Events availability overview** already work across boards or within one board on their own rules. They are not `_Calendar` users.

**To decide in discuss-phase:**

- **Link model:** a named link group that a board belongs to (at most one), or pairwise links. Whether links are transitive. What happens to a link when a board is deleted.
- **Visibility for non-members:** a player on board A who is not a member of linked board B. Do they see B's entries in full, as an anonymous "busy" block, or not at all? A link into B would 404 for them under the Phase 87 rules.
- **Which surfaces show linked entries:** `/calendar` on both layouts for sure. Whether the Quest Details date-vote calendar also shows linked boards' dates (read-only, never votable), and whether the DM series-runway warning stays per-board.
- **Which quests count:** finalized only, or open quests with proposed dates too. The current calendar shows every quest it fetches.
- **Presentation:** how a linked entry is marked (board-name badge or prefix, muted colour), and whether a board's own entries can be told apart at a glance.
- **Whether linking ever changes the calendar feed.** The default is no.

**Constraints:** linking is super-admin only and only between boards of the same type (enforced server-side, not just in the form). Every cross-board read stays narrowed to the linked board ids and goes through the `IgnoreQueryFilters` allowlist. The tenant-isolation tests keep passing for unlinked boards. The link is read-only: nothing on a linked board can be edited, signed up for or voted on from another board's calendar. The change is a pure read with no write to `Events` or `Quests`, so feed revisions do not move. The schema change ships as an EF Core migration. Both `.Mobile.cshtml` twins are updated.

Plans:
**Wave 1**

- [x] 91-01-PLAN.md — Linked board set schema and repository: set table + Groups.LinkedBoardSetId migration, set CRUD and board assignment, rename keeps the link (W1)
- [x] 91-02-PLAN.md — DM-only sessions follow the viewer's board role on /calendar and the Quest Details calendar (W1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 91-03-PLAN.md — Linked Boards Platform page (desktop): same-type / at-most-one rules server-side, SuperAdminOnly controller and view (W2)
- [x] 91-04-PLAN.md — Pinned linked reads: per-viewer linked-board lookup and narrow, windowed filter-bypassing quest and event reads (W2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 91-05-PLAN.md — Linked boards' quests and events on desktop /calendar: re-checked read-only overlay service, view model and partial (W3)
- [x] 91-06-PLAN.md — Linked Boards mobile twin and Group index entry buttons (W3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 91-07-PLAN.md — Desktop look and legend for linked entries, parity/no-leak proofs, member deep-link switch (W4)
- [x] 91-08-PLAN.md — Linked entries on the mobile /calendar agenda (W4)
- [x] 91-09-PLAN.md — Linked entries on the Quest Details calendar (desktop grid, mobile "Also that day") and the read-only static guard (W4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 91-10-PLAN.md — End-to-end Platform-to-calendar proof, feed untouched, local SQL Server and phone verification (W5, checkpoint)

### Phase 92: Move a Quest or Event to a Linked Board

**Goal:** A super admin can move an existing quest or event from its board to another board in the same link group, from that entry's Details page. After the move the entry lives on the target board as if it had been created there, and the boards in the group stop disagreeing about where it belongs. Boards that are not linked offer no move.
**Requirements**: TBD
**Depends on:** Phase 91. A move is only offered between boards in the same link group, so the link model has to exist first.
**Plans:** 9/9 plans complete

**Origin:** raised by the operator on 2026-10-15 at the end of Phase 91's discussion. It was kept out of Phase 91 on purpose: that phase is read-only and makes no write to `Events` or `Quests`, and a move is the opposite, a cross-board write. See `91-CONTEXT.md` § Deferred Ideas.

**To decide in discuss-phase:**

- **Signups and characters:** characters belong to one board. A player signed up on the source board with that board's character has no valid character on the target board, and may not be a member of it at all. Votes, the waitlist and gold rewards hang off those signups. Whether a move keeps, drops, or blocks on existing signups.
- **Event series:** an event that belongs to a recurring series on the source board either leaves the series or is refused. Whether a whole series can move.
- **Calendar feed:** the board is a feed-visible field, so a move raises the entry's `FeedRevision` through `FeedRevisionStamper`, and the entry leaves the feed of subscribers who are only on the source board. Whether that is acceptable as is.
- **Notification:** whether affected players and DMs are told, and how.
- **Which entries can move:** finalized, open, past, closed, cancelled.

**Constraints:** super-admin only, enforced server-side. Only between boards in the same link group (which implies the same board type). The write goes through the change tracker so the feed revision rises, per `.claude/architecture.md`. Both `.Mobile.cshtml` twins are updated.

Plans:
**Wave 1**

- [x] 92-01-PLAN.md — Tracer: a super admin's confirm POST moves a quest to the other linked board in one tracked save (W1)
- [x] 92-02-PLAN.md — Move mail plumbing: Quest Moved template (three audiences), Waitlist Promoted moved line, dispatcher methods and jobs, Email Preview entries, capturing test factory (W1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 92-03-PLAN.md — Quest preview contract, change-detector stamp and eligibility rule on the tracer's path; closed, played and stale-preview refusals (W2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 92-04-PLAN.md — Move page frame, desktop and mobile: GET preview states, changed-preview re-render, member toast keys, authorization and target proofs (W3)
- [x] 92-05-PLAN.md — Quest people, DM rule, chain cut, waitlist promotion and mail, all in the single save (W3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 92-06-PLAN.md — Event move: answers reconciled, Campaign automatic rows, series and past refused, no mail (W4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 92-07-PLAN.md — Move page consequence rows and styling for quests and events (W5)
- [x] 92-08-PLAN.md — "Move to Linked Board" on Quest Details (both layouts) and Event Details, and the Moved toast (W5)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 92-09-PLAN.md — Guards (ambient clock, single save, no planning references) and the end-to-end walk, audit line and feed proof (W6)

### Phase 93: Per-Board Page Selection and Home Page

**Goal:** In the Platform area, a super admin can choose which pages each board offers, such as Shop, Contacts, Players, Characters, Quest Log, Events and Calendar. A page that is switched off disappears from both menus, and the server refuses it on that board when its URL is typed. A super admin can also set which page a board opens on. The default stays the quest list, as it is today, and at least one board can open on `/calendar` instead. Boards nobody configures behave exactly as they do today.
**Requirements**: TBD
**Depends on:** Phase 92, for sequencing only. It edits the same layouts and Platform `GroupController` that Phases 91 and 92 changed, but needs nothing they built.
**Plans:** 15/15 plans complete

**Origin:** raised by the operator on 2026-10-20. Boards differ in which pages they actually use, and the operator wants at least one board in their group of boards to open on its calendar instead of the quest list.

**Starting point** (scoped 2026-10-20):

- **Boards:** a board is `Group` (entity `GroupEntity`: `Name`, `CreatedAt`, `BoardType`, `LinkedBoardSetId`). It has no per-board settings yet. The Platform `GroupController` Edit form (`SuperAdminOnly`) only changes the name, and shows `BoardType` read-only.
- **Today's page gating is by board type, and in the menus only.** `_Layout.cshtml` and `_Layout.Mobile.cshtml` read `IBoardTypeResolver` and hide Shop, Players, Shop Management and the DM profile on boards that are not OneShot. Events and Calendar show on OneShot and Campaign boards. Quest Log, Characters and Contacts always show. `ShopController` and `PlayersController` have no board-type check, so on a Campaign board `/shop` and `/players` still open when typed. Some controllers also branch on board type internally (`CalendarController` skips quests on Campaign boards; `EventsController` and `QuestLogController` vary by type).
- **Home today:** `/` is `HomeController.Index`, which sends a signed-in user to `Quest/Index`. The navbar brand in both layouts links straight to `Quest/Index`. After login or a board switch with no return URL, `GroupPickerController.RedirectToLocal` falls back to `Quest/Index`, and `MoveController` has one more such fallback. A per-board home has to reach all of these.
- **Board switching:** `GroupSessionMiddleware` sends a user with no active board to `/groups/pick?returnUrl=…`. Phase 87's cross-board deep-link switch changes the session board when a member opens a quest or event on another board. A return URL that names a page switched off on the target board has to land somewhere sensible.

**To decide in discuss-phase:**

- **Which pages can be switched off.** Candidates: Shop (with Shop Management), Contacts, Players, Characters, Quest Log, Events, Calendar, and the DM profile. Whether the quest list itself can be switched off on a board that opens elsewhere, or always stays reachable. Personal and cross-board pages (My Agenda, Profile, the board picker) are not per-board and stay as they are.
- **How it relates to board type.** Either board type sets a new board's default pages, which a super admin then adjusts, or it stays a hard rule that a toggle can only narrow (for example, Shop could never be switched on for a Campaign board). Either way, existing boards keep exactly the pages they show now.
- **What a switched-off page does when its URL is opened:** a 404, or a redirect to the board's home with a message. Whether DMs and admins are blocked too, or only players.
- **Pages other flows rely on.** Quest signup can pick a character, and the shop belongs to the gold economy. Whether switching off Characters or Shop is allowed while those flows use them, or affects them.
- **Links into a switched-off page:** character names on signups, the Phase 78/79 link cards for characters and contacts in quest text, and links in emails. Whether they render as plain text, still resolve, or are left alone.
- **Cross-board surfaces.** Whether switching off Calendar or Events on a board also removes that board from the calendar feed, My Agenda and linked boards' calendars, or only from its own pages.
- **Which pages can be the home page.** At least the quest list (the default) and Calendar. Whether any enabled page can be chosen, and what happens when the chosen home page is later switched off.
- **Scope of the home page.** Set per board, or shared by the boards in a linked set.

**Constraints:** super-admin only, enforced server-side. The server refuses a switched-off page; hiding it in the menu is not enough. Settings are stored per board and ship as an EF Core migration whose defaults leave every existing board unchanged. Both layouts and every affected `.Mobile.cshtml` twin are updated. Nothing writes to `Events` or `Quests`, so feed revisions do not move.

Plans:

**Wave 1**

- [x] 93-01-PLAN.md — Tracer: page catalogue, policy, resolver, two Groups columns and the global gate; Shop refused with a toast and sent to the board's home (W1)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 93-02-PLAN.md — Server gate on Calendar, Events, Series, quest pages, Quest Log, admin quest list and moves (W2)
- [x] 93-03-PLAN.md — Allows/Serves view helpers; both layouts and My Agenda's button gated by the active board, brand link to the home page, conditional Quests menu item (W2)
- [x] 93-04-PLAN.md — Save-time settings rules: type ceiling, home-page refusal, Events refused while upcoming events or running series exist (W2)
- [x] 93-05-PLAN.md — Linked calendars render entries from a switched-off page as plain text (W2)
- [x] 93-13-PLAN.md — `/`, the board picker (and so login) and the Account back link land on the board's home page (W2)
- [x] 93-14-PLAN.md — Unit pins: type ceiling over every mask, effective-home invariant, route table and toasts, storage mapping (W2)
- [x] 93-15-PLAN.md — "Home: X" badge on the Platform board list, both twins (W2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 93-06-PLAN.md — Platform Edit "Pages" section and home dropdown on both twins, refusals as form errors (W3)
- [x] 93-07-PLAN.md — Character pickers and DM-profile links hidden when those pages are off; crafted character ids refused (W3)
- [x] 93-08-PLAN.md — Events and Calendar off: no own events on the calendar, its Board Availability button hidden, saves and cancels land on Events (W3)
- [x] 93-09-PLAN.md — Moves onto a board with that kind of entry switched off refused; picker and control follow (W3)
- [x] 93-12-PLAN.md — Server gate on Shop management, Players, DM profile, Characters and Contacts (Campaign gap closed, picture endpoints exempt) and the reflection coverage test (W3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 93-10-PLAN.md — Quest list switchable: live-quest refusal, calendar without quests, DM history as text, full quests-off proof (W4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 93-11-PLAN.md — No-planning-reference guard, production DI proof, and the operator's end-to-end walk with the full suite (W5)

### Phase 94: Whole-App UI Review and Unification

**Goal:** Every page in the quest board, on desktop and mobile, is reviewed with the `senior-frontend` skill, and the app gets the one consistent, polished visual system every page will move onto. Phase 94 is the first phase of that sweep: it delivers the page-by-page audit (visual bugs, inconsistencies, cascade fights, mobile-twin drift and accessibility failures), a visual direction the operator signs off from mockups, the foundation (tokens, the layered cascade, shared components, all layouts, self-hosted vendor assets, dead dependencies removed), a rewritten `.claude/ui-guidelines.md` that records the system so new views follow it, and the appended area phases that move the remaining pages onto it and upgrade those that would clearly benefit. Every existing flow keeps working exactly as it does today.

*Goal restated on 2026-10-30 to match the scope decided in discuss-phase (Phase 94 CONTEXT, D-05); the original sentence promised every page converted within this one phase.*
**Requirements**: TBD
**Depends on:** Phase 93, for sequencing only. It touches every view, and Phase 93 was the last phase to change both layouts.
**Plans:** 27/27 plans complete

**Origin:** raised by the operator on 2026-10-30, who expects many enhancements are possible across the views and wants the `senior-frontend` skill to update, fix, unify and possibly upgrade the UI.

**Starting point** (scoped 2026-10-30):

- **Size:** 133 `.cshtml` files across `Views/` and `Areas/Platform/Views/` (pages, partials and layouts), 55 of them `.Mobile.cshtml` twins. The largest folders are Quest (15), Shared (14), Account (12), Admin (11) and Platform Group (10).
- **Styling:** 48 stylesheets in `wwwroot/css`, about 8,900 lines; `site.css` alone is about 1,700. Bootstrap 5.3.0 and Font Awesome 6.4.0 come from CDNs, plus Font Awesome's v4 shim stylesheet, which suggests old icon names are still in use. The Bootstrap and main Font Awesome links carry no SRI hash; the shim does.
- **No shared tokens to speak of:** the CSS defines 18 custom properties but contains about 615 hex colour literals and about 814 `!important` declarations. Phases 66–68 each hit a specificity collision (the `.modern-card` gold rule leaking into the editor, a page-wide `!important` hiding recap text on mobile), so the cascade is a known source of bugs, not only of inconsistency.
- **Markup outside the stylesheets:** 97 inline `style="…"` attributes, 4 views with `<style>` blocks and 43 views with inline `<script>` blocks. There is no Content-Security-Policy header, and `senior-frontend` assumes a strict CSP, so its default patterns will not match this code as it stands.
- **Existing rules:** `.claude/ui-guidelines.md` sets the modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`), the button layout and the `Html.LocalTime` / `Html.WallClock` date helpers, and warns that mobile twins drift and are sometimes deliberately not content-equivalent.

**To decide in discuss-phase:**

- **How far "upgrade" goes.** Either unify and fix within today's look (parchment, gold, modern cards), or settle a refreshed visual direction first, with mockups from `senior-frontend` or `/gsd-sketch`, and then move every page onto it. The second is a much larger phase and needs the direction signed off before any view changes.
- **Whether this stays one phase.** 133 views will not fit one reviewable phase. One likely shape: Phase 94 does the audit, the shared tokens and component classes, and the layouts, and per-area phases are appended after it (quests and quest log; events, series and calendar; characters, contacts and DM profile; shop; account, admin and Platform). The audit's findings decide the split.
- **Bootstrap and Font Awesome.** Whether both stay, whether to pin them with SRI or self-host them, and whether to migrate the v4 icon names and drop the shim.
- **Inline scripts and styles.** Whether moving them into stylesheets and ES modules is in scope. Adopting a CSP header is a security change and is better split into its own phase, but the move would prepare for it.
- **Mobile twins.** Whether any desktop and mobile pair is merged into one responsive view, or every twin stays and is only brought into line with its partner.
- **The accessibility bar:** contrast on the parchment and gold theme, visible focus, and 44px touch targets on mobile.
- **Email templates** (`.razor` components) are not views and are left out unless discuss-phase pulls them in.

**Constraints:** no user-facing functionality is removed or broken, as the project-wide rule requires. Every desktop change is checked against its mobile twin. Dates keep going through `Html.LocalTime` and `Html.WallClock`. Nothing writes to `Events` or `Quests`, so feed revisions do not move. Verification is by before-and-after screenshots of each page and, for mobile, on a real phone, as this project's earlier mobile work required. The Docker self-hosting path keeps working. CSS comments carry no planning references.

Plans:
**Wave 1**

- [x] 94-01-PLAN.md — Audit environment: one operator checkpoint (tooling, database, privileged accounts), player and DM accounts, capture harness (wave 1, checkpoint)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 94-02-PLAN.md — Route manifest, fixtures, frozen baseline app on its own port and full BEFORE sweep with gate tools (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 94-03-PLAN.md — Audit: Quests and Quest Log (wave 3)
- [x] 94-04-PLAN.md — Audit: Events, Series, Calendar and Agenda (wave 3)
- [x] 94-05-PLAN.md — Audit: Characters, Contacts, DM profile and Players (wave 3)
- [x] 94-06-PLAN.md — Audit: Shop and Shop management (wave 3)
- [x] 94-07-PLAN.md — Audit: Account, pickers and access denied (wave 3)
- [x] 94-08-PLAN.md — Audit: Admin and Platform (wave 3)
- [x] 94-09-PLAN.md — Mockups: fixture content, tools, all five baselines, Direction A Quest board and Details (wave 3)
- [x] 94-11-PLAN.md — Self-host Bootstrap, Font Awesome and Cinzel at today's versions; release smoke test (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 94-10-PLAN.md — Mockups: Direction A form, Calendar, Shop both ways, crops and write-up (wave 4)
- [x] 94-12-PLAN.md — Self-host Cropper.js, Tagify and EasyMDE at today's versions (wave 4, after 94-11)
- [x] 94-14-PLAN.md — Shell audit, cascade probe, audit index, operator questions, proposed cut (wave 4)
- [x] 94-15-PLAN.md — Mockups: Direction B with the primary-action switcher (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 94-13-PLAN.md — Remove jQuery and the v4 shim after proofs; layer order statement (wave 5)
- [x] 94-16-PLAN.md — Mockups: comparison page, all switchers, combined contrast report, sign-off sheet (wave 5)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 94-17-PLAN.md — Direction sign-off gate G1 and final token values (wave 6, checkpoint)

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 94-18-PLAN.md — Token layer, foundation link order, layer and contrast guards (wave 7)

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 94-19-PLAN.md — Vendor bump, Bootstrap into layer(vendor) with variable re-theme, base layer (wave 8)

**Wave 9** *(blocked on Wave 8 completion)*

- [x] 94-20-PLAN.md — Component layer and toasts with persistent errors and warnings (wave 9)

**Wave 10** *(blocked on Wave 9 completion)*

- [x] 94-21-PLAN.md — Desktop shell: three layouts, desktop footer, backdrop (wave 10, after 94-20)

**Wave 11** *(blocked on Wave 10 completion)*

- [x] 94-22-PLAN.md — Mobile shell: two mobile layouts, mobile footer, 44px targets (wave 11, after 94-21)

**Wave 12** *(blocked on Wave 11 completion)*

- [x] 94-23-PLAN.md — Retire desktop and mobile global rules; site.css renamed legacy-pages.css (wave 12)

**Wave 13** *(blocked on Wave 12 completion)*

- [x] 94-24-PLAN.md — Retire modern-card.css and the cream rule; full readability sweep reviewed (wave 13, checkpoint)

**Wave 14** *(blocked on Wave 13 completion)*

- [x] 94-25-PLAN.md — Rewrite .claude/ui-guidelines.md; correct the Platform mobile note (wave 14)

**Wave 15** *(blocked on Wave 14 completion)*

- [x] 94-26-PLAN.md — Final verification, scope proof, audit refresh (wave 15, after 94-25)

**Wave 16** *(blocked on Wave 15 completion)*

- [x] 94-27-PLAN.md — Area-phase cut gate G2 and roadmap appends (wave 16, checkpoint)

**Cross-cutting constraints:**

- Merge proposals appear only for identical or layout-only pairs whose mobile differences are CSS-only, each listing the precedent views, the CSS-loading change in both layouts and the MobileViewsTests facts that would change, flagged costly to reverse and awaiting operator approval (D-13)
- Relayout proposals keep every field, link, button and action and each control's type, and never collapse a section behind a toggle; each is listed for sign-off with its BEFORE screenshot (D-04)
- Contrast findings are measured against the D-16 bar (4.5:1 text, 3:1 large and non-text, composited over B-max) and propose a token use that keeps the signature colour per D-17
- The report quotes no member name, email or user-entered text from real data and references screenshots only by scratch path
- Each twin pair is classified identical, layout-only, content-differs or deliberate from extracted inventories (asp-for and name fields, asp-action/href links and submit buttons, h1-h6 and card blocks, BoardPages conditions); no deliberate differences are pre-recorded for this area; every content difference not recorded as deliberate is an operator question in the standard wording with only 'Deliberate, record it' or 'Add it to the other view' (D-14)
- Mockups live only under ~/qb-ui-review/phase-94/mockups/ and nothing is committed (D-02)

Wave note: plans that edit tracked source, build or run the working tree, or commit code never share a wave. Waves 6 to 16 hold one plan each; in waves 3, 4 and 5 the only such plan is 94-11, 94-12 and 94-13, and the plans beside it write only scratch output or planning docs and probe the frozen baseline.

### Phase 95: Quests and Quest Log UI conversion

**Goal:** Convert the core loop (quest board, quest details, create and edit forms, DM manage, quest log, move) to the foundation, carrying the shared pieces the loop depends on, so a DM can post a quest and a player can sign up on converted pages. Convert the quest board, quest details, quest forms, DM manage, quest log and move pages, with the shared editor, signup modal and vote UI, to the foundation: scrimmed card surfaces, no inline styles or scripts, the approved poster relayout, and every page readable at 4.5:1 on phone and desktop.
**Requirements**: TBD
**Depends on:** Phase 94
**Plans:** 18/18 plans complete

**Scope:**

- Views: 10 view pairs, 20 files: `Quest/{Index,Details,Create,Edit,CreateFollowUp,Manage}`, `QuestLog/{Index,Details,EditRecap}`, `Move/Index` (serves quests and events). The landing page (Home/Index) is not here: adjustment 1 moved it to Phase 96. Partials: `Quest/_QuestFormScripts`, `Shared/_MarkdownEditor` (also fixes the keyboard trap for six Characters-area pages), and, by the proposed adjustments, `Shared/_CharacterSelectModal`. The vote UI of the shared calendar partial is a scoped patch made here (Q-134); Phase 97 owns the partial itself. `Quest/_QuestCard` and `_QuestSection` are unreferenced (Q-130).
- Stylesheets: `quests.css`, `quests.mobile.css`, `quest-edit.mobile.css`, `quest-followup.mobile.css`, `quest-log.mobile.css`, `quest-log-detail.mobile.css`, `home.mobile.css`, `move-entry.css`, `move-entry.mobile.css`, `markdown-editor.css`, `markdown-content.css`, and by adjustment `dm-create.mobile.css` and `dm-manage.mobile.css`
- Moves into `@section Styles`: `quests.css`, `markdown-editor.css`, `markdown-content.css` and `move-entry.css` leave the global list of `_Layout.cshtml`; `markdown-editor.css` and `markdown-content.css` also leave `_Layout.Mobile.cshtml`. `calendar.css` stays global until Phase 97 moves it; Quest Details then links it itself for the vote UI.
- Counts after the foundation: 25 view files (the 10 view pairs of the quest, quest log and move pages, 20 files, plus `_QuestFormScripts`, the two unreferenced `_QuestCard` and `_QuestSection`, `_MarkdownEditor` and `_CharacterSelectModal`); 13 stylesheets plus the Quest, board, vote and Quest Log ranges of `legacy-pages.css` (lines 1-38, 299-791, 1185-1264) and the Move rule of `mobile.css`: **126 hex, 276 `rgb()`, 212 `!important`**; in the views **26 inline `style=`, 0 `<style>`, 11 inline `<script>`, 59 inline event handlers**; 8 utility-colour fights (`home.mobile`, `quest-log.mobile`, `quest-log-detail.mobile`, `quests.css` 2, `quests.mobile` 2, `dm-manage.mobile`); rendered contrast **123 failing rows on 37 elements on 13 pages** (outline buttons on glass 1.17 to 1.88:1, the poster date 2.14:1, the phone form labels and help text 2.99 to 4.34:1); overflow at 320 on `Quest/Details` and `QuestLog/Index`; 5 test files read its sheets (`MobileViewsTests` 16 references).
- Findings owned: S1 8 and S2 22 of 100 findings (S3 65, S4 5); the S1 items are contrast (table cells, role labels, vote buttons, required marks, outline controls, dark ink on bare board)
- Deferred items landing here: G1-O1 (desktop quest poster relayout: drawn first, the text zone gently lightened so body text reaches 4.5:1, no slip surface [Gate 1 outcome, 2026-11-11: the operator rejected the visible zone; posters use heavier ink only, with no zone or halo, a recorded contrast exception below 4.5:1 held to per-poster floors]); WINDOWS 12 for Quest Create, CreateFollowUp and Edit (phone required asterisk, 14 rows at 3.36:1, until the cards move to the scrimmed surface); WINDOWS 24 (six `a.row-nav-link` quest titles 27px tall on the phone `/quests`); the vote buttons (`btn-outline-success`, `-danger` and `-warning` at 1.21 to 3.36:1) and the Bootstrap outline buttons on glass; the five Quest findings of the 94-AUDIT-quests.md report (S1 8, S2 22 of 100).

**Guard tests to rewrite to the same intent (none deleted):** `Mobile/MobileViewsTests` facts for the Quest, QuestLog, Home and Move pages (including the `quest-edit.mobile.css` link fact near line 888 and the Quest Log detail fact near lines 953-987); `Tests/BoardMoveViewsTests` (Move desktop sheet only, mobile sheet only, `move-card-mobile`), `BoardMoveEventTests`, `BoardMovePageTests`, `BoardMoveEndToEndTests` and the presence lists in `BoardMoveStaticGuardTests` and `BoardPageStaticGuardTests`; `QuestLogControllerIntegrationTests` (recap edit); `LinkedBoardCalendarStyleTests` and `LinkedBoardStaticGuardTests` for `quests.mobile.css` and the calendar partials; the closed `AmbientClockSeamTests` list if the dead partials go

**Inherited decisions:**

- Desktop top bar (the relayout approved in principle for 992 to 1280 px): already addressed by the desktop shell delivered in Phase 94 (a 56px bar at every width, collapsing below 1200px, accepted by the operator). In this phase it is a verification check only, on the quest pages at 992 to 1280 px; there is no new work and no AFTER to draw.
- Operator answers: 21 "add it to the other view" answers (Q-07 to Q-39, 33 questions, 12 deliberate), 2 approved merges (Q-95, Q-96), 5 approved relayouts (Q-111 to Q-114 and the poster relayout G1-R1), plus a verification of the desktop top bar (Q-127, adjustment 12).
- Open questions inherited: twin questions Q-07 to Q-39, merges Q-95 to Q-96 (QuestLog/EditRecap and QuestLog/Details), relayouts Q-111 to Q-114 and the Q-127 verification (the desktop top bar, adjustment 12), and the other decisions on the Markdown editor host, dead files, posters and ownership
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved merge: M-quests-01 (Q-95) QuestLog/EditRecap: approved at G1 (2026-11-01), carried into the Quests and Quest Log phase; costly to reverse
- Approved merge: M-quests-02 (Q-96) QuestLog/Details: approved at G1 (2026-11-01), carried into the Quests and Quest Log phase; costly to reverse
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - Quest/Details: Q-111 (R-quests-01): Quest/Details (desktop): split the columns col-lg-9 / col-lg-3 instead of col-lg-10 / col-lg-2 so the Quest Summary reads without wrapping (width change only, order unchanged). BEFORE desktop `$QBR/before/desktop/Quest-Details.png`, mobile `$QBR/before/mobile/Quest-Details.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Details (voting): Q-111 (R-quests-01): Quest/Details (desktop): split the columns col-lg-9 / col-lg-3 instead of col-lg-10 / col-lg-2 so the Quest Summary reads without wrapping (width change only, order unchanged). BEFORE desktop `$QBR/before/desktop/Quest-Details--voting.png`, mobile `$QBR/before/mobile/Quest-Details--voting.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png` (the mockups draw the finalized Quest Details page; the voting calendar is not drawn). Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Details (voting): Q-112 (R-quests-02): Quest/Details (desktop, voting and update states): draw the calendar months directly inside the one signup card instead of one nested card per month (removes two levels of blur). BEFORE desktop `$QBR/before/desktop/Quest-Details--voting.png`, mobile `$QBR/before/mobile/Quest-Details--voting.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png` (the mockups draw the finalized Quest Details page; the voting calendar is not drawn). Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Create: Q-113 (R-quests-03): Quest/Create and Quest/Edit (desktop): action row Cancel left and the commit action right after an hr, instead of commit left with no hr (reordering inside the action row only). BEFORE desktop `$QBR/before/desktop/Quest-Create.png`, mobile `$QBR/before/mobile/Quest-Create.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-form-1440.png`, `$QBR/mockups/direction-a/png/quest-form-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Edit: Q-113 (R-quests-03): Quest/Create and Quest/Edit (desktop): action row Cancel left and the commit action right after an hr, instead of commit left with no hr (reordering inside the action row only). BEFORE desktop `$QBR/before/desktop/Quest-Edit.png`, mobile `$QBR/before/mobile/Quest-Edit.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-form--edit-1440.png`, `$QBR/mockups/direction-a/png/quest-form--edit-390.png` (Direction B draws the form once, as Create; Edit uses the same layout). Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Manage: Q-114 (R-quests-04): Quest/Manage (mobile): remove the card-in-card nesting; Description and Rewards become headed sections inside the single card, collapsible exactly as today. BEFORE desktop `$QBR/before/desktop/Quest-Manage.png`, mobile `$QBR/before/mobile/Quest-Manage.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Quest/Index (desktop poster cards): G1-R1 (operator-initiated at G1): keep today's parchment poster images; re-lay out only the information inside the poster, with a very gently lightened text zone so body text reaches 4.5:1 at B-max (ink on the bare poster measured 3.8 to 4.2:1 at the 5th percentile); titles stay large text at 3:1. [Gate 1 outcome, 2026-11-11: the operator rejected the visible zone; posters use heavier ink only, with no zone or halo, a recorded contrast exception below 4.5:1 held to per-poster floors] BEFORE desktop `$QBR/before/desktop/Quest-Index.png`, mobile not applicable (the poster cards are desktop only; the phone keeps its glass cards). AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out (Direction A's pinned-slip render of this page is not the AFTER: the operator kept the poster artwork). Answer: approved in principle — re-confirmed with its AFTER in the area phase; Quests phase.

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: the Quest board mockup page is built from this phase (it is first because it carries it); the role-select script no longer hides the DM Controls card; the vote buttons on Quest Details pass the bar in both layouts; Quest Details is re-verified with `_Calendar` in both layouts

**Constraints:**

- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:
**Wave 1**

- [x] 95-01-PLAN.md — Review environment: operator checkpoint (database, privileged accounts, scratch tooling), player and DM accounts, Edge capture harness

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 95-02-PLAN.md — Fixtures through the app forms, frozen baseline app, probes and BEFORE capture under both User-Agents

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 95-03-PLAN.md — Gate 1 package and gate: poster mockup with switchers and measured pool, phone Manage mockup, merge evidence (operator, redraw loop)
- [x] 95-04-PLAN.md — Phase tracer: Markdown editor keyboard trap fix, visible hint, form scripts out of inline code, conversion guard test

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 95-05-PLAN.md — Quest Details A: Spectator marker, phone signup selects, details module, character modal

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 95-06-PLAN.md — Quest Details B: scoped vote-button patch, calendar months inside the signup card

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 95-07-PLAN.md — Quest Details C: column split, twin wording, dates, layered sheets

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 95-08-PLAN.md — Desktop quest board from the approved mockup: poster picker, WebP art, layered poster CSS, masonry module, bare empty board

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 95-09-PLAN.md — Phone quest board: stretched card links, empty state

**Wave 9** *(blocked on Wave 8 completion)*

- [x] 95-10-PLAN.md — Quest Create and Edit (Edit merges if approved at Gate 1)

**Wave 10** *(blocked on Wave 9 completion)*

- [x] 95-11-PLAN.md — Quest CreateFollowUp (merges if approved at Gate 1)

**Wave 11** *(blocked on Wave 10 completion)*

- [x] 95-12-PLAN.md — Quest Manage scripts and handlers into one module

**Wave 12** *(blocked on Wave 11 completion)*

- [x] 95-13-PLAN.md — Quest Manage conversion and the approved phone relayout

**Wave 13** *(blocked on Wave 12 completion)*

- [x] 95-14-PLAN.md — Quest Log index plus the two approved merges (Details, EditRecap)

**Wave 14** *(blocked on Wave 13 completion)*

- [x] 95-15-PLAN.md — Move page and the shared move reason line in components

**Wave 15** *(blocked on Wave 14 completion)*

- [x] 95-16-PLAN.md — Shared Markdown stylesheets and where they load

**Wave 16** *(blocked on Wave 15 completion)*

- [x] 95-17-PLAN.md — Closure: dead partials and legacy rules, global sheet list trimmed, counts

**Wave 17** *(blocked on Wave 16 completion)*

- [x] 95-18-PLAN.md — AFTER sweep, Gate 2 review sheet and gate (operator, gap-closure loop)

### Phase 96: Account and pickers UI conversion

**Goal:** Convert the sign-in, profile and board-picker pages; the picker first because it is the one S1 that stops a keyboard user entering the app at all. Convert the sign-in, profile and board-picker pages to the foundation and merge the five approved twins, so a keyboard user can enter the app and every account page reads at 4.5:1.
**Requirements**: TBD
**Depends on:** Phase 94 (the order puts it after Phase 95 but it has no dependency on it)
**Plans:** 7/12 plans executed

**Scope:**

- Views: 6 Account pairs, the picker pair and the twin-less `Shared/AccessDenied`: `Account/{Login,ForgotPassword,SetPassword,ChangePassword,Edit,Profile}`, `GroupPicker/Index`, `Shared/AccessDenied`; plus `Home/Index` (moved here by adjustment 1, Q-136). 17 files in all.
- Stylesheets: `account.mobile.css` and the Profile block of legacy-pages.css (line range in the post-foundation numbers below)
- Moves into `@section Styles`: no sheet leaves a desktop layout's global list (Account pages have no desktop page sheet); the Profile block moves out of `legacy-pages.css` into the account sheet, which the five merged views (Login, ForgotPassword, SetPassword, ChangePassword, GroupPicker) link once through `@section Styles` for both layouts.
- Counts after the foundation: 17 view files (6 Account pairs, the picker pair, `AccessDenied` and the `Home/Index` pair); `account.mobile.css`, `group-picker.css` (layered, already clean) and the Profile block of `legacy-pages.css` (lines 1265-1329): **4 hex, 14 `rgb()`, 11 `!important`**; in the views **2 inline `style=`, 0 `<style>`, 2 inline `<script>`, 2 inline event handlers**; 1 utility-colour fight (`account.mobile.css`); rendered contrast **12 failing rows on 7 elements on 4 pages** (the outline-primary "Change Password" link on the desktop Account Edit at 1.88:1, the phone account field labels at 2.99:1); overflow of `Shared/AccessDenied` on the phone (394 at 390); 5 test files read its sheets.
- Findings owned: S1 2 and S2 15 of 43 findings (S3 14, S4 12); S1: picker cards not keyboard-reachable (A-account-031), profile meta text (A-account-019)
- Deferred items landing here: the `AccessDenied` overflow (reused on `Events/Details` by Phase 97: one fix, here); the `Home/Index` landing page (Q-136); the 43 account findings of 94-AUDIT-account.md (S1 2, S2 15), the picker keyboard finding first.

**Guard tests to rewrite to the same intent (none deleted):** `MobileViewsTests` Login (lines 552-575), GroupPicker (619-663), Edit and Profile facts; `ProfileCalendarSubscriptionTests` (both subscription shapes are pinned deliberately); `CalendarSubscriptionStaticGuardTests` (the QR sizing rule on `site.css` and `account.mobile.css`); `GroupPickerControllerIntegrationTests` and `CrossBoardPickerSkipTests` (do not branch on User-Agent; keep green)

**Inherited decisions:**

- Operator answers: 7 "add it to the other view" answers (Q-76 to Q-84, 9 questions, 2 deliberate), 5 approved merges (Q-103 to Q-107), 2 approved relayouts (Q-122, Q-123).
- Open questions inherited: twin questions Q-76 to Q-84, merges Q-103 to Q-107 (Login, ForgotPassword, SetPassword, ChangePassword, GroupPicker), relayouts Q-122 to Q-123
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved merge: M-account-1 (Q-103) Account/Login: approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-1 must not be answered "Deliberate" (an untitled mobile page cannot merge with a titled desktop one); costly to reverse
- Approved merge: M-account-2 (Q-104) Account/ForgotPassword: approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-2; costly to reverse
- Approved merge: M-account-3 (Q-105) Account/SetPassword: approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-3; source-only audit, so the merge must be verified with a freshly mailed token; costly to reverse
- Approved merge: M-account-4 (Q-106) Account/ChangePassword: approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-4; costly to reverse
- Approved merge: M-account-5 (Q-107) GroupPicker/Index: approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: A-account-031 and the R1 relayout land in the same change; costly to reverse
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - GroupPicker/Index: Q-122 (R1 (account)): GroupPicker/Index (desktop and mobile): the per-board cards become flat rows on the one panel, each row being the submit control; every board entry, hidden input and the Go to Platform link stay. BEFORE desktop `$QBR/before/desktop/GroupPicker-Index.png`, mobile `$QBR/before/mobile/GroupPicker-Index.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Account/Profile: Q-123 (R2 (account)): Account/Profile (desktop, populated, narrow widths): the five row actions drop beneath the subscription name and dates instead of sitting to their right (no BEFORE capture of the populated state exists; two scratch frames were taken for the audit). BEFORE desktop `$QBR/before/desktop/Account-Profile.png`, mobile `$QBR/before/mobile/Account-Profile.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: the foundation delivers `.modern-card--interactive`, the opaque modal surface, `--qb-fg-muted` and `--qb-danger-text` first; the AFTER sweep adds Profile with one subscription, AccessDenied signed out and the Login validation state; SetPassword is verified with a freshly mailed token; the AccessDenied overflow fix is reused on `Events/Details`

**Constraints:**

- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:
**Wave 1**

- [x] 96-01-PLAN.md — Review environment: operator checkpoint (scratch tooling, database, privileged accounts, no other phase executing), capture-harness tracer, review accounts

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 96-02-PLAN.md — Fixtures through the app forms, tools, frozen baseline on port 8097, fresh BEFORE capture of every page including the populated Profile

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 96-03-PLAN.md — Gate 1 package A: picker flat-row mockup, Home look and merge proposal, measured AccessDenied overflow fix

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 96-04-PLAN.md — Gate 1 package B: Profile mockups for both twins, item list, Gate 1 (operator, redraw loop)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 96-05-PLAN.md — Picker first: merge, flat rows and keyboard fix in one change; account conversion guard test

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 96-06-PLAN.md — Profile onto the foundation: account sheet from the legacy Profile block, row-action relayout, meta-text contrast

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 96-07-PLAN.md — Profile script module, Delete dialog (still posts without script), empty-state wording, named row actions

**Wave 8** *(blocked on Wave 7 completion)*

- [ ] 96-08-PLAN.md — The four credential pages merged as one change; phone overflow fix

**Wave 9** *(blocked on Wave 8 completion)*

- [ ] 96-09-PLAN.md — Edit twins, AccessDenied, phone account sheet retired

**Wave 10** *(blocked on Wave 9 completion)*

- [ ] 96-10-PLAN.md — Home landing page (merged only on an explicit Gate 1 yes), area-wide guard and counts

**Wave 11** *(blocked on Wave 10 completion)*

- [ ] 96-11-PLAN.md — SetPassword verified with a freshly mailed link (operator)

**Wave 12** *(blocked on Wave 11 completion)*

- [ ] 96-12-PLAN.md — AFTER sweep, accessibility-bar table, Gate 2 review sheet and gate (operator, gap-closure loop), release handoff

### Phase 97: Events, Series, Calendar and Agenda UI conversion

**Goal:** Convert the calendar, agenda and events overview with their shared partials, keeping the date helpers and the linked-board entries intact. Convert the calendar, agenda, events overview, event details and series pages and their shared partials to the foundation, with a desktop month that fits without scrolling and every date still rendered through the wall-clock and local-time helpers.
**Requirements**: TBD
**Depends on:** Phase 94 and Phase 95 (Phase 95 patches the calendar vote UI this phase inherits)
**Plans:** 17 plans

**Scope:**

- Views: 7 views and 10 files: `Calendar/Index`, `Agenda/Index`, `Events/Index` (each with a twin), the four twin-less `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`; partials `Shared/_Calendar`, `_Calendar.Mobile`, `_LinkedDayEntries`, `_LinkedAlsoThatDay`, `Events/_AvailabilityCell`, `_AvailabilityCounts`, `_SeriesFormScripts`
- Stylesheets: `calendar.css`, `calendar.mobile.css`, `events-overview.css`, `events-overview.mobile.css`, `agenda.css`, `agenda.mobile.css` (the vote and current-quest block of `calendar.css`, lines 385-440 and 673-765, per Q-134)
- Moves into `@section Styles`: `calendar.css`, `events-overview.css` and `agenda.css` leave the global list of `_Layout.cshtml` (Quest Details links `calendar.css` itself once Phase 95 and this phase agree on the vote block).
- Counts after the foundation: 17 view files; 6 stylesheets: **65 hex, 71 `rgb()`, 70 `!important`**; in the views **2 inline `style=`, 0 `<style>`, 7 inline `<script>`, 18 inline event handlers**; 2 utility-colour fights (`agenda.css`, `calendar.mobile.css`); rendered contrast **78 failing rows on 6 elements on 5 pages** (the empty availability dash at 2.28:1, phone agenda dates and day labels at 3.94 to 4.34:1); overflow of `Events/Details` on the phone (394 at 390); 4 test files read its sheets (`EventsOverviewMobileStyleTests` 7 references).
- Findings owned: S1 3 and S2 23 of 51 findings (S3 22, S4 3); S1: attendance counts cream on cream, availability badge labels, dark subtitle on bare board
- Deferred items landing here: G1-O3 (the calendar month view spec: the desktop month fits without scrolling at 1280 by 720 and up; the operator confirms "+N more" against `$QBR/mockups/calendar-fit/` before this phase starts); the `Events/Details` 4px overflow (reusing the `AccessDenied` fix); the 51 events findings of 94-AUDIT-events.md (S1 3, S2 23).

**Guard tests to rewrite to the same intent (none deleted):** `EventsOverviewMobileStyleTests` (its literal colours and `!important` conflict with the foundation: re-pin readable counts, a glass-or-paper surface, 44px targets), `LinkedBoardStaticGuardTests`, `LinkedBoardCalendarStyleTests`, and the Events parts of `MobileViewsTests` and `BoardPageStaticGuardTests`; the `AmbientClockSeamTests` list gains the corrected today read if Q-133 is approved

**Inherited decisions:**

- Before this phase starts, the operator confirms the "+N more" behaviour against the calendar-fit mockup (`$QBR/mockups/calendar-fit/`).
- Operator answers: 6 "add it to the other view" answers (Q-40 to Q-49, 10 questions, 4 deliberate), no merges, 2 approved relayouts (Q-115, Q-116) plus the calendar month fit (G1-R2).
- Open questions inherited: twin questions Q-40 to Q-49, no merges (none qualifies), relayouts Q-115 to Q-116
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - Events/Details: Q-115 (R1 (events)): Events/Details (both layouts): move the "Your Availability" card above "Description" so the main player action is the first card after the title block (order only). BEFORE desktop `$QBR/before/desktop/Events-Details.png`, mobile `$QBR/before/mobile/Events-Details.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Calendar/Index: Q-116 (R2 (events)): Calendar/Index (desktop, below the large breakpoint): month navigation on its own row under the title, "Board Availability" and "My Agenda" in a wrapping button row; same four controls and labels. BEFORE desktop `$QBR/before/desktop/Calendar-Index.png`, mobile `$QBR/before/mobile/Calendar-Index.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/calendar-1440.png`, `$QBR/mockups/direction-a/png/calendar-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase; constraint G1-R2: the whole desktop month stays visible without scrolling at 1280 by 720 and up.
  - Calendar/Index (desktop month fit): G1-R2 (operator requirement at G1): the desktop month fits without scrolling at 1280 by 720 and up: compact one-line entries (marker with a non-colour cue, title with an ellipsis, 4.5:1), "+N more" in busy cells so cells never grow, an entry expands on hover, keyboard focus and tap (dismissible with Esc, hoverable, persistent); phones keep the agenda list. Entries collapse behind "+N more": an operator-requested exception to the no-collapse rule, limited to day cells, every entry stays reachable. BEFORE desktop `$QBR/before/desktop/Calendar-Index.png`, mobile `$QBR/before/mobile/Calendar-Index.png`. AFTER: Direction A calendar at 1440 and 390: `$QBR/mockups/direction-a/png/calendar-1440.png`, `$QBR/mockups/direction-a/png/calendar-390.png`; the "+N more" interaction is shown in the calendar-fit mockup the operator tries before the Events phase (`$QBR/mockups/calendar-fit/`, being built; open item G1-O3). Answer: approved in principle — re-confirmed with its AFTER in the area phase; "+N more" behaviour confirmed against the calendar-fit mockup before the Events phase.

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: linked-entry dashed border, the inert `--static` rule and the visually hidden status text stay; no date conversion; every date line keeps its format; calendar-feed files are untouched; the twin-less views get their narrow-width rules in the `pages` layer

**Constraints:**

- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:

**Wave 1**

- [ ] 97-01-PLAN.md — Opening operator checkpoint (Phase 96 finished, SQL Server, Audit Board, review-ref pushes), Linux scratch setup, capture tracer

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 97-02-PLAN.md — Fixtures through the app forms, frozen baseline, BEFORE capture and BEFORE date-line text

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 97-03-PLAN.md — Gate 1 package A: calendar port mockups with real data, truncation strips, narrow header, legend, fit and behaviour checks
- [ ] 97-04-PLAN.md — Tracer: today highlight from the board clock, behaviour test, ambient-clock guard additions

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 97-05-PLAN.md — Gate 1 package B: phone agenda and Events/Details reorder mockups, Agenda merge note, orphan review branch, Gate 1 (operator, redraw loop)
- [ ] 97-06-PLAN.md — Date lines to Html.WallClock with byte-identical text (new server-only styles, nl-NL proof), area conversion guard

**Wave 5** *(blocked on Wave 4 completion)*

- [ ] 97-07-PLAN.md — Availability partials and Events/Index on both layouts

**Wave 6** *(blocked on Wave 5 completion)*

- [ ] 97-08-PLAN.md — Events/Details: approved reorder, script module, phone overflow fix

**Wave 7** *(blocked on Wave 6 completion)*

- [ ] 97-09-PLAN.md — Events/Create and Edit, series form module, recurrence label fix

**Wave 8** *(blocked on Wave 7 completion)*

- [ ] 97-10-PLAN.md — Series/Details

**Wave 9** *(blocked on Wave 8 completion)*

- [ ] 97-11-PLAN.md — Agenda on both layouts

**Wave 10** *(blocked on Wave 9 completion)*

- [ ] 97-12-PLAN.md — Phone calendar with the approved additions, Quest Details phone partial hidden words

**Wave 11** *(blocked on Wave 10 completion)*

- [ ] 97-13-PLAN.md — calendar.css into the pages layer, 4.5:1 weekday header, Quest Details re-verified, Phase 95 exceptions retired

**Wave 12** *(blocked on Wave 11 completion)*

- [ ] 97-14-PLAN.md — Server-rendered compact month, header, legend, linked-board guards, no-script sweep

**Wave 13** *(blocked on Wave 12 completion)*

- [ ] 97-15-PLAN.md — calendar-month.js: slots, "+N more" day panel, detail card; fit and behaviour checks

**Wave 14** *(blocked on Wave 13 completion)*

- [ ] 97-16-PLAN.md — Closing guards, zero counts, untouched-paths diff, recorded exceptions, full AFTER, accessibility table

**Wave 15** *(blocked on Wave 14 completion)*

- [ ] 97-17-PLAN.md — Gate 2 review sheet and orphan review branch, Gate 2 (operator, redraw loop), release handoff

### Phase 98: Characters, Contacts, DM profile and Players UI conversion

**Goal:** Convert characters, contacts, categories, the DM profile and the players list, and the image crop dialog. Convert characters, contacts, contact categories, the DM profile and the players list, with the image crop dialog and the six approved twin merges, to the foundation, keeping every field and action and fixing the editor keyboard trap and the dialog findings.
**Requirements**: TBD
**Depends on:** Phase 94 and Phase 95 (Phase 95 fixes the Markdown editor keyboard trap this phase verifies on six pages)
**Plans:** 0 plans

**Scope:**

- Views: 13 rows, 26 files: `Characters/*` (8), `Contacts/*` (8), `ContactCategoryManagement/*` (4), `DungeonMaster/{Profile,EditProfile}` (4), `Players/Index` (2); `wwwroot/js/image-crop.js` and its dialog markup. The character select modal (Q-135) goes with Phase 95, and the Markdown editor is fixed in Phase 95 and verified here on six pages
- Stylesheets: `characters.css`, `characters.mobile.css`, `character-detail.mobile.css`, `character-form.mobile.css`, `contacts.css`, `contacts.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-profile.css`, `dm-profile.mobile.css`, `dm-editprofile.mobile.css`, `players.mobile.css`, `image-crop.css` (13; `dm-create.mobile.css` and `dm-manage.mobile.css` go to Phase 95)
- Moves into `@section Styles`: `characters.css`, `contacts.css`, `dm-profile.css` and `image-crop.css` leave the global list of `_Layout.cshtml`.
- Counts after the foundation: 26 view files; 13 stylesheets: **70 hex, 243 `rgb()`, 88 `!important`**; in the views **49 inline `style=`, 0 `<style>`, 22 inline `<script>`, 11 inline event handlers**; 8 utility-colour fights; rendered contrast **97 failing rows on 32 elements on 13 pages** (an amber Edit Character button on the phone at 1.68:1, form help text at 2.99:1, the phone required asterisk at 3.36:1, `row-nav-link` names at 4.34:1); overflow of `ContactCategoryManagement/Manage` at 320 desktop; 3 test files read its sheets (`MobileViewsTests` 14 references, `ContactCategoryContrastGuardTests` 5).
- Findings owned: S1 6 and S2 13 of 55 findings (S3 25, S4 11); S1: crop dialog and character dialog text, two amber mobile buttons, a dark category label, the editor keyboard trap
- Deferred items landing here: WINDOWS 12 for Characters Create and Edit and Contacts Create, Edit and Details (phone required asterisk, 7 rows at 3.36:1); the crop dialog and character dialog findings; the 55 findings of 94-AUDIT-characters.md (S1 6, S2 13).

**Guard tests to rewrite to the same intent (none deleted):** `MobileViewsTests` facts for Characters (near line 767), DM EditProfile (`GetMobilePage_DmEditProfile_...`), Contacts; `ContactCategoryContrastGuardTests` (the `contacts.mobile.css` and `contact-form.mobile.css` facts and the two rendered-HTML facts, the three facts that pinned `modern-card.css`, re-pinned when the foundation deleted it and rewritten again as the contact sheets move), `ContactsTagsFormMarkupTests`, `ContactsTagsDesktopMarkupTests`, `ContactDetailsCategoryTests`, `ContactCategoryManagementControllerIntegrationTests`

**Inherited decisions:**

- Operator answers: 5 "add it to the other view" answers (Q-50 to Q-55, 6 questions, 1 deliberate), 6 approved merges (Q-97 to Q-102), 3 approved relayouts (Q-117 to Q-119).
- Open questions inherited: twin questions Q-50 to Q-55, merges Q-97 to Q-102 (six pairs), relayouts Q-117 to Q-119, and the crop dialog decision
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved merge: M-characters-1 (Q-97) Characters/Details: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved merge: M-characters-2 (Q-98) Contacts/Details: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved merge: M-characters-3 (Q-99) Contacts/Create: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved merge: M-characters-4 (Q-100) Contacts/Edit: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved merge: M-characters-5 (Q-101) ContactCategoryManagement/Edit: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved merge: M-characters-6 (Q-102) DungeonMaster/EditProfile: approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - ContactCategoryManagement/Manage: Q-117 (R-1 (characters)): ContactCategoryManagement/Manage (desktop): wrap the category table in a responsive container so it scrolls inside its card at narrow widths; columns, buttons and order unchanged. BEFORE desktop `$QBR/before/desktop/ContactCategoryManagement-Manage.png`, mobile `$QBR/before/mobile/ContactCategoryManagement-Manage.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Characters/Create: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/Characters-Create.png`, mobile `$QBR/before/mobile/Characters-Create.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Characters/Edit: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/Characters-Edit.png`, mobile `$QBR/before/mobile/Characters-Edit.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Contacts/Create: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/Contacts-Create.png`, mobile `$QBR/before/mobile/Contacts-Create.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Contacts/Edit: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/Contacts-Edit.png`, mobile `$QBR/before/mobile/Contacts-Edit.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - ContactCategoryManagement/Edit: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/ContactCategoryManagement-Edit.png`, mobile `$QBR/before/mobile/ContactCategoryManagement-Edit.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - DungeonMaster/EditProfile: Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. BEFORE desktop `$QBR/before/desktop/DungeonMaster-EditProfile.png`, mobile `$QBR/before/mobile/DungeonMaster-EditProfile.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Contacts/Create: Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order. BEFORE desktop `$QBR/before/desktop/Contacts-Create.png`, mobile `$QBR/before/mobile/Contacts-Create.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Contacts/Edit: Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order. BEFORE desktop `$QBR/before/desktop/Contacts-Edit.png`, mobile `$QBR/before/mobile/Contacts-Edit.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Characters/Create: Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order. BEFORE desktop `$QBR/before/desktop/Characters-Create.png`, mobile `$QBR/before/mobile/Characters-Create.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Characters/Edit: Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order. BEFORE desktop `$QBR/before/desktop/Characters-Edit.png`, mobile `$QBR/before/mobile/Characters-Edit.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: the crop dialog meets 2.5.7 per the answer to Q-132; the one remaining infinite animation (`characters.css:203`) is capped; the editor keyboard trap fix is verified on all six pages

**Constraints:**

- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:

- [ ] TBD (run /gsd-plan-phase to break down)

### Phase 99: Shop and Shop management UI conversion

**Goal:** Convert the shop and its management pages after the Shop theme is decided, fixing the functional breaks the conversion touches. Convert the shop and shop management pages to the foundation in the navy shop theme, relocating the floating merchant button and fixing the hidden Return and Sell buttons, the dead Preview and View Details controls and the sell confirmation script.
**Requirements**: TBD
**Depends on:** Phase 94 and the separate Shop item creation fix (a bug fix off `main`, outside this phase)
**Plans:** 0 plans

**Scope:**

- Views: 10 view files plus the partial `Shared/_ShopItemDetailsContent`: `Shop/{Index,Details}` and `ShopManagement/{Index,Create,Edit}`, each with a twin (11 with the partial)
- Stylesheets: `shop.css`, `shop.mobile.css`, `shop-details.mobile.css`, `shop-management-index.mobile.css`, `shop-management-create.mobile.css`, `shop-management-edit.mobile.css`, plus the Shop ranges of legacy-pages.css (line ranges in the post-foundation numbers below), which the phase removes, and the shop.css line in `_Layout.cshtml`
- Moves into `@section Styles`: `shop.css` leaves the global list of `_Layout.cshtml`; the Shop theme goes on `<html data-theme="shop">`, never through a body-level custom property.
- Counts after the foundation: 11 view files; 6 stylesheets plus the Shop ranges of `legacy-pages.css` (lines 39-298 and 792-1184): **189 hex, 209 `rgb()`, 152 `!important`**; in the views **15 inline `style=`, 4 `<style>` blocks, 9 inline `<script>`, 40 inline event handlers**; 14 utility-colour fights (7 in `legacy-pages.css`, 6 in `shop.css`, 1 in `shop.mobile.css`); rendered contrast **59 failing rows on 20 elements on 11 pages** (legendary and rare rarity labels at 1.63 and 2.15:1, the Purchase button at 2.13:1, outline buttons at 1.70 and 1.88:1); no overflow flag; 1 test file reads its sheets (`MobileViewsTests`, 10 references).
- Findings owned: S1 10 and S2 19 of 65 findings (S3 29, S4 7); S1 includes the creation failure A-shop-010, which is handed off, not fixed here
- Deferred items landing here: G1-O2 (the floating merchant button needs a new place, a required fix); the three shop alerts still on literal colours with important flags (`#denyModal .alert-warning` and `#addCharacterModal .alert-info` in `shop.css`, and the help and small text of the management edit card in `shop-management-edit.mobile.css`), to move to the foundation's alert variables; the `#denyModal` and `#addCharacterModal` required-mark rules; WINDOWS 22 (the board photo token: the Shop theme must replace `--qb-board-image` through `data-theme`, keeping the photo behind `--qb-board-photo`, which the foundation already does); WINDOWS 12 for Shop management Create and Edit (2 rows); the four functional breaks (Q-137); the 65 shop findings of 94-AUDIT-shop.md (S1 10, S2 19). The Shop item creation bug is a separate fix, not part of this phase (see below).

**Guard tests to rewrite to the same intent (none deleted):** `MobileViewsTests` Shop and ShopManagement entries (the purchase-panel omission at lines 716-733 and the management mobile layout at 1123), `BoardPageStaticGuardTests` (controller file list), `BoardPageGateMatrixTests`, the Shop controller integration tests

**Inherited decisions:**

- Operator answers: 11 "add it to the other view" answers (Q-56 to Q-75, 20 questions, 9 deliberate), no merges, 2 approved relayouts (Q-120, Q-121).
- Open questions inherited: twin questions Q-56 to Q-75, no merges (none qualifies), relayouts Q-120 to Q-121, the theme decision and the functional-breaks owner
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - Shop/Index: Q-120 (R-1 (shop)): Shop/Index (desktop): move the purchase history from a fixed 280px side panel to an in-flow section under the item grid (a reorder of two blocks; same rows, same Return and Sell forms), so it is reachable at every width. BEFORE desktop `$QBR/before/desktop/Shop-Index.png`, mobile `$QBR/before/mobile/Shop-Index.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a-1440.png`, `$QBR/mockups/direction-a/png/shop-a-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2).
  - Shop/Index (empty-category): Q-120 (R-1 (shop)): Shop/Index (desktop): move the purchase history from a fixed 280px side panel to an in-flow section under the item grid (a reorder of two blocks; same rows, same Return and Sell forms), so it is reachable at every width. BEFORE desktop `$QBR/before/desktop/Shop-Index--empty-category.png`, mobile `$QBR/before/mobile/Shop-Index--empty-category.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a--empty-1440.png`, `$QBR/mockups/direction-a/png/shop-a--empty-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2).
  - Shop/Index: Q-121 (R-2 (shop)): Shop/Index (desktop): one filter card containing the rarity checkboxes, sort select, search input and the Apply Filters and Search buttons, instead of two stacked bars tied by a form attribute. BEFORE desktop `$QBR/before/desktop/Shop-Index.png`, mobile `$QBR/before/mobile/Shop-Index.png`. AFTER: Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a-1440.png`, `$QBR/mockups/direction-a/png/shop-a-390.png`. Answer: approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2).

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: ten infinite animation declarations (not six; four are dead copies in the Create and Edit `<style>` blocks) are removed or capped; the four views with `<style>` lose it; Return and Sell are verified with a purchase made through the Purchase form on the audit board; Create works first (separate fix off `main`)

**Constraints:**

- The Shop item creation failure (the missing board on a new item) is fixed separately before this phase; its after-capture of Create needs it. It is not part of this phase.
- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:

- [ ] TBD (run /gsd-plan-phase to break down)

### Phase 100: Admin and Platform UI conversion

**Goal:** Convert the admin pages and the Platform area on top of the foundation's Platform layouts. Convert the admin and Platform pages to the foundation with their approved relayouts and readable badges, tables and mailto links, keeping the three Platform Group twins.
**Requirements**: TBD
**Depends on:** Phase 94 (the foundation's Platform layouts, which this phase verifies)
**Plans:** 0 plans

**Scope:**

- Views: 25 view files: `Admin/*` (11: five twin pairs plus `EmailStats`), `Platform/Group/*` (10), `Platform/Users/Index` (2), `Platform/LinkedBoards/Index` (2). The two Platform layouts are Foundation work that this phase verifies (A-admin-008, A-admin-014, A-shell-037, A-shell-038)
- Stylesheets: `admin-form.mobile.css`, `admin-quests.mobile.css`, `admin-users.mobile.css`, `platform-group.mobile.css`, `platform-users.mobile.css`, `platform-linked-boards.mobile.css`
- Moves into `@section Styles`: nothing leaves a desktop layout's global list (the Platform layouts link no page sheet); the phone sheets are linked from their views through the section the Platform layouts already render.
- Counts after the foundation: 25 view files; 6 stylesheets: **6 hex, 57 `rgb()`, 28 `!important`**; in the views **1 inline `style=`, 0 `<style>`, 4 inline `<script>`, 12 inline event handlers**; 5 utility-colour fights (`platform-group.mobile` 3, `platform-users.mobile` 2); rendered contrast **254 failing rows on 16 elements on 13 pages** (the outline Reset Password button at 1.21:1, Platform phone meta text and dates at 2.35:1, 43 mailto links at 3.11:1); overflow of `Platform/Group/Index` at 320 desktop; 4 test files read its sheets.
- Findings owned: S1 8 and S2 14 of 44 findings (S3 16, S4 6); S1: badge and table-cell readability, mailto links on Platform phones, the dark modal, the outline Reset Password button, the Platform bar at 320px
- Deferred items landing here: WINDOWS 24 (43 `a.email-link` links 22px tall on the phone `/Platform/Users`); the `Platform/Group/Index` 320 overflow; the 44 findings of 94-AUDIT-admin.md (S1 8, S2 14).

**Guard tests to rewrite to the same intent (none deleted):** `PlatformAreaLayoutTests` (the Platform layout loads the card styles), `LinkedBoardsPlatformIntegrationTests` (`platform-linked-boards.mobile.css`, `platform-group-card-mobile`), `BoardPageStaticGuardTests` (reads `platform-group.mobile.css`), the Admin and Platform entries of `MobileViewsTests`

**Inherited decisions:**

- Operator answers: 5 "add it to the other view" answers (Q-85 to Q-94, 10 questions, 5 deliberate), no merges to do (Q-108 to Q-110 kept the twins), 3 approved relayouts (Q-124 to Q-126).
- Open questions inherited: twin questions Q-85 to Q-94, merges Q-108 to Q-110 (Platform Group Create, Delete, Edit), relayouts Q-124 to Q-126
- Signed-off direction: Direction A (refined glass) with the operator's stated exceptions; the Shop uses variant (a), the navy theme, on `<html data-theme="shop">`.
- Every relayout was approved in principle at the first sign-off (2026-11-01) and is re-confirmed with its AFTER in this phase: no page is re-laid out until its before/after sign-off in this phase, mocked pages included.
- Approved merge: M-admin-1 (Q-108) Platform Group/Create: kept at G1 (2026-11-01): the twin stays; costly to reverse
- Approved merge: M-admin-2 (Q-109) Platform Group/Delete: kept at G1 (2026-11-01): the twin stays; costly to reverse
- Approved merge: M-admin-3 (Q-110) Platform Group/Edit: kept at G1 (2026-11-01): the twin stays; costly to reverse
- Approved relayouts (`$QBR` is `~/qb-ui-review/phase-94`):
  - Admin/CreateUser: Q-124 (R-admin-1): Admin/CreateUser and Admin/EditUser (desktop): merge the bare-board duplicate h1 and the card h2 into one page title in the card header; no field or action moves. BEFORE desktop `$QBR/before/desktop/Admin-CreateUser.png`, mobile `$QBR/before/mobile/Admin-CreateUser.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Admin/EditUser: Q-124 (R-admin-1): Admin/CreateUser and Admin/EditUser (desktop): merge the bare-board duplicate h1 and the card h2 into one page title in the card header; no field or action moves. BEFORE desktop `$QBR/before/desktop/Admin-EditUser.png`, mobile `$QBR/before/mobile/Admin-EditUser.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Group/Members: Q-125 (R-admin-2): Platform Group/Members (desktop): stack "Current Members" and "Available Users" full width instead of two half-width columns; all columns, forms and buttons stay. BEFORE desktop `$QBR/before/desktop/Platform-Group-Members.png`, mobile `$QBR/before/mobile/Platform-Group-Members.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.
  - Admin/Users: Q-126 (R-admin-3): Admin/Users (desktop): stack the Email under the Name in one cell so the Actions cell gets the freed width; every column's content and every button stays. BEFORE desktop `$QBR/before/desktop/Admin-Users.png`, mobile `$QBR/before/mobile/Admin-Users.png`. AFTER: after: not yet drawn — drawn in its area phase before that page is re-laid out. Answer: approved in principle — re-confirmed with its AFTER in the area phase.

**Definition of converted:**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules.
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered; approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent.
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads.

Plus, specific to this phase: a configured provider key (or a seeded stub) so `Admin/EmailStats` tile contrast is measured, not read; the Campaign status mapping (A-admin-016) is answered with the Quests owner's knowledge before it is fixed; the stale note about `_Layout.Platform.Mobile` being dead code is already corrected (PROJECT.md says the Platform mobile layout is live)

**Constraints:**

- No user-facing flow is removed or broken; every page stays usable at every merge.
- Mobile checks run under a mobile User-Agent with touch.
- Dates render through `Html.LocalTime` (real instants) and `Html.WallClock` (wall-clock values) only; nothing writes to `Events` or `Quests`, and the calendar feed files are untouched.
- Before and after screenshots are kept outside the repository.
- No planning references (requirement, phase or plan ids, planning document names) in CSS or source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:

- [ ] TBD (run /gsd-plan-phase to break down)

### Phase 101: Email templates restyle

**Goal:** Give the ten email components the same brand look as the converted web pages, within what mail clients support, with every message's content and trigger unchanged. Restyle the ten email templates to the web pages' brand look with inline styles only and a real test send of each, leaving every message's content and trigger unchanged.
**Requirements**: TBD
**Depends on:** Phase 94 (the final tokens; it is independent of the web phases and can run at any point after them)
**Plans:** 0 plans

**Scope:**

- Components: all 10 `.razor` components under `QuestBoard.Service/Components/Emails/`: `AddedToGroup`, `ChangeEmailConfirm`, `ForgotPassword`, `QuestDateChanged`, `QuestFinalized`, `QuestMoved`, `SessionReminder`, `WaitlistPromoted`, `Welcome` and `_EmailLayout.razor`, which carries the shared shell and today's remote Font Awesome and Google Fonts links and a `<style>` block.
- Stylesheets: none (email styling is inline); the shell's tokens are translated to literal values once, in `_EmailLayout.razor`.
- Counts after the foundation: 10 components; **192 inline `style=` attributes and 134 hex literals** (mail clients need both), 1 `<style>` block and 2 remote stylesheet links (Font Awesome 6.4.0 from cdnjs, Cinzel from Google Fonts) in `_EmailLayout.razor`; no CSS custom properties, no scripts; unchanged by the foundation (the email diff against the baseline is empty).
- Findings owned: none from this audit (the email templates are not web views and were not part of the manifest); the phase starts with its own inventory of the ten components.

**Guard tests to rewrite to the same intent (none deleted):** `Emails/QuestFinalizedMarkdownRenderTests`, `Emails/WaitlistPromotedMarkdownRenderTests`, `Emails/SessionReminderMarkdownRenderTests` (the markdown `<div>` wrapper), `UnitTests/Services/SessionReminderJobTests`, and the email file references in `Tests/BoardMoveStaticGuardTests`.

**Inherited decisions:**

- No relayouts and no merges apply to email. Content, subjects, send triggers and recipients stay exactly as they are.
- The remote fonts in `_EmailLayout.razor` (Font Awesome 6.4.0 from cdnjs, Cinzel from Google Fonts) are decided inside the phase with the operator: keep web fonts as progressive enhancement behind safe fallbacks, or drop them.
- The test-send inbox is the operator's; the mail clients the group uses are named in the phase.

**Definition of converted:** the ten points above read for email: points 1 to 3 become "inline styles only, no custom properties, no scripts" and point 5 is checked on the rendered preview and the received message; points 6 and 7 become the preview plus the real test send of each template.

**Constraints:**

- Inline styles are required; no CSS custom properties (mail clients do not resolve `var()`); limited mail-client CSS (no layers, no `:where()`, no `backdrop-filter`; table or div layouts that survive the common clients).
- Markdown-rendered sections keep their `<div>` wrapper; the `@Html.Markdown(...)` output is untouched.
- Content, subjects, triggers and recipients unchanged; `Html.LocalTime` and `Html.WallClock` markup untouched where used.
- Verification: a rendered preview screenshot of every template plus a real test send of each template to the operator's inbox (the development setting that suppresses sending is switched off for the test send only), checked in the mail clients the group uses; preview and received message compared per template.
- Pages-style constraints carried over: no user-facing flow removed; screenshots kept outside the repository; no planning references in source comments.
- Release as usual through the existing draft-release `deploy` approval gate, with deployment timing left to the operator; nothing new is built for release. Caveat: `docker-publish.yml` publishes the GHCR image on every tag push regardless of the draft-release approval, so holding a release holds the production deployment only, not the public container image; self-hosters pulling `latest` receive each tagged phase immediately.

Plans:

- [ ] TBD (run /gsd-plan-phase to break down)

### Phase 102: Board deletion

**Goal:** A super admin can delete a board that no longer has any members, without a server error. Everything that belongs only to that board goes with it, and anything that must outlive the board (users, and whatever they own across boards) is unlinked instead of deleted.
**Requirements**: TBD
**Depends on:** Nothing in this milestone (independent bug fix; it can run at any point)
**Plans:** 10/10 plans complete

**Scope:**

- The bug: the Platform board Delete posts to `GroupController.DeleteConfirmed`, which removes only the board row through `groupService.RemoveAsync`. Memberships (`UserGroups`) cascade, but quests, characters, events, event series, contacts, contact categories, contact tags and shop items reference the board with no delete rule (`NoAction`), so SQL Server rejects the delete and the user sees a 500.
- Keep the existing guard: the Delete button only appears, and the delete is only accepted, once every member has been removed from the board.
- Map every entity that hangs off a board, directly or through another row (signups, date votes, purchases, calendar feed rows, linked-board-set membership, follow-up quest chains, home-page and page selections, and so on), and decide for each whether it is deleted with the board or only unlinked.
- Delete in one transaction, behind a confirmation page that lists what will be removed. Any failure shows a friendly error, never a 500.
- Writes to quests and events go through the change tracker, so the calendar feed revision guard holds.
- Platform Users page (`/platform/Users`): add a column showing how many boards each user is a member of, so a super admin can see who is still linked where (for example while emptying a board before deleting it).

Plans:

**Wave 1**

- [x] 102-01-PLAN.md — Tracer: typed-name delete POST through controller, service and repository in one save, for a board holding quests, proven over HTTP
- [x] 102-02-PLAN.md — Super-admin stale active-board recovery: shared session-clear helper and a board-exists check in the session middleware
- [x] 102-03-PLAN.md — Boards column on /platform/Users, desktop and phone

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 102-04-PLAN.md — Delete every board-owned table and unlink cross-board references, with per-kind counts and a foreign-key integrity sweep
- [x] 102-05-PLAN.md — Guard tests: foreign-key model pin, service typed-name rules, SQL Server delete and rollback test (skips without a server)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 102-06-PLAN.md — Deletion preview built from the delete's own query helpers

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 102-07-PLAN.md — Confirmation page on both twins through one shared partial, with refusal markup

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 102-08-PLAN.md — Page sheets, typed-name gate script and static guard
- [x] 102-09-PLAN.md — POST outcomes, own-session clear and audit log line

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 102-10-PLAN.md — Full suite, required SQL Server run, and manual check on the real database (checkpoint)

## Backlog

Unsequenced ideas parked outside the phase sequence. Promote with `/gsd-review-backlog`.

### Phase 999.1: Voting from a Phone Calendar Entry (BACKLOG)

**Goal:** [Captured for future planning]
**Requirements:** TBD
**Plans:** 0 plans

**Origin:** raised by the operator on 2026-09-18, after Phases 84 and 85 shipped and were tested on production — "would it be possible to vote in the calendar item on my phone, and will it then update my vote in the quest board?" Parked the same day, once the feasibility question was answered.

**Feasibility verdict — the reason this is parked rather than planned.** It cannot be done through the subscription Phase 84 shipped, and no amount of design work changes that. A subscribed `.ics` URL is a one-way HTTP GET: the client polls, the server answers, and there is no return channel in the transport. Apple Calendar, Google Calendar's *From URL* calendars and Outlook's Internet Calendar Subscriptions all render a subscribed calendar read-only and run no scheduling against it, so emitting `ORGANIZER`, `ATTENDEE` and `RSVP=TRUE` would add bytes and produce no buttons. This confirms rather than contradicts Phase 84's own scope note ("a one-way subscription feed, not two-way sync … nothing a reader does in their calendar app ever writes back") and the writer's standing comment on why it emits no `METHOD` (`QuestBoard.Domain/Services/CalendarFeedWriter.cs`).

**The two routes that could work, and what each costs:**

- **iMIP — real email invitations.** The only mechanism that puts genuine Accept / Maybe / Decline buttons on a phone. The server mails a `METHOD:REQUEST` VEVENT from an `ORGANIZER` address it controls, with the reader as `ATTENDEE;RSVP=TRUE`; the client mails back a `METHOD:REPLY` carrying `PARTSTAT`. The data model fits unusually well — `ACCEPTED` / `TENTATIVE` / `DECLINED` is exactly `VoteType.Yes` / `Maybe` / `No`. The cost is not the outbound half. It is that **this codebase has no inbound mail of any kind**: `EmailService` is outbound `System.Net.Mail` SMTP only, and there is no MimeKit, no IMAP, no provider webhook anywhere in the solution. A real phase would have to add a mailbox plus polling or an inbound webhook, MIME parsing, iCalendar `REPLY` parsing, and `UID`-to-entity plus attendee-to-user matching. It would also have to answer two things that are not details: an inbound `From` header is forgeable, so a reply must carry its own secret (reply-address sub-addressing) or pass DKIM/SPF before it is allowed to change anyone's answer; and Gmail and Outlook auto-add invitations to the primary calendar, which would sit beside the identical session already arriving through the Phase 84 feed, because clients do not merge entries across calendars.
- **Deep-link tap-through.** Reinstate `DESCRIPTION` (the reliable carrier; the `URL:` property is surfaced inconsistently, Google especially) with a token-authenticated link to a small Yes/Maybe/No page. Three taps — entry, link, answer — and it works in every client today with no inbound mail. Rejected for now on two grounds the operator weighed: it is not actually voting *in* the calendar item, only a shortcut back to the board; and it escalates what a leaked feed address costs, turning a read-only schedule disclosure into the ability to change someone's answers. Note it also reverses Phase 84 D-10, which dropped `DESCRIPTION` deliberately.

**Scope correction that survives the parking:** quest *date* voting is out of reach on any route. The feed carries finalized quests only — Phase 85 locked candidate dates out on the grounds that a phone should not fill with dates that will mostly not happen — so there is nothing in the feed for a date vote to attach to. Event availability is the only thing a voting story can reach, and it is a natural fit: the feed already renders the answer outbound as the `(maybe)` / `(declined)` title suffix, so this would close a loop that is currently half-built.

**Left open deliberately:** whether a decline should release a finalized quest seat and promote the waitlist. The operator chose to settle that in a discuss pass rather than now, so it is not decided here.

**Revive this if:** manually opening the board to answer proves annoying enough in production use to justify the build, or inbound mail arrives in the project for some other reason — the iMIP route is a far smaller phase once a parsed inbound mailbox already exists.

Plans:

- [ ] TBD (promote with /gsd-review-backlog when ready)
