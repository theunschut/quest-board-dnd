# Milestone v9.0: Rolling Improvements

**Status:** 🚧 IN PROGRESS
**Phases:** 72–81 so far (open-ended)
**Working branch:** `milestone/v9-rolling-improvements`

## Overview

A rolling bucket milestone for small, ad-hoc features and bug fixes. Unlike v1.0–v8.0, this milestone has no fixed end-state and no unifying theme — phases are appended as work arrives, and the milestone closes when the operator decides to cut it. Phase numbering continues from v8.0 (which ended at Phase 71).

The opening scope was three items. Two are small and independent: closing a UX gap where a player cannot change the character on a quest they have already signed up for, and resolving five stale HIGH GitHub security alerts left behind by the v5.0 EuphoriaInn→QuestBoard rename. The third is substantial — Calendar Events, spanning four phases, which adds dated informational entries to the calendar with per-event player availability and an optional recurrence model.

Appended 2026-08-26: **Link Previews**, spanning two phases, so that a quest, character, or contact link pasted into Discord or Slack renders a rich preview card instead of a bare URL. The cards are gated behind explicitly-minted signed share links rather than being public, because a board is private and external unfurl caches are permanent.

Appended 2026-08-27: **NPC Contact Organisation**, spanning two phases, from a board user's request — categories first, so a long flat Contacts list can be broken into named headings, then free-form tags with a filter on top. The two are split because the requester staged them that way and because they are different data models: a category is a single grouping a contact sits under, a tag is a many-to-many label it carries. Phase 81 can sit unplanned indefinitely without blocking 80.

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
**Plans**: 0/8 plans complete

Plans:
**Wave 1**

- [ ] 81-01-PLAN.md — Mint the `CONTACTTAG-*` requirement family into REQUIREMENTS.md and the roadmap coverage table, and complete the phase validation contract (wave 1)
- [ ] 81-02-PLAN.md — Data foundation: the board-scoped `ContactTag` entity, the app's first many-to-many join, its fail-closed query filter, a collation-backed unique index, the migration, the domain model, the entity mapping, the test seed helper, and cross-board filter coverage (wave 1)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 81-03-PLAN.md — Repository and service tag persistence: split-queried tag loading, upsert-by-name reconciliation through a board-filtered query, orphan pruning on save and delete, comma-list parsing, and unit tests asserting against the database (wave 2)

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 81-04-PLAN.md — Index read path: tag view models, repeated-tag-id query-string binding, vocabulary derived from the visible-but-unfiltered set, in-memory union filtering after the visibility gate, the Show Hidden round trip, and filter-semantics integration tests (wave 3)

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 81-05-PLAN.md — Contact write path: viewer-scoped suggestion lists on both form GETs, comma-separated tag persistence on both POSTs, name-length validation, and no-script write-path integration tests (wave 4)

**Wave 5** *(blocked on Wave 4 completion)*

- [ ] 81-06-PLAN.md — Tag entry widget on all four create and edit views: a re-verified SRI-pinned CDN library, a thin local init module, scoped theme overrides on both platforms, and form markup tests under both user agents (wave 5)

**Wave 6** *(blocked on Wave 5 completion)*

- [ ] 81-07-PLAN.md — Desktop display: the index filter row and its disabled state, chips on cards, the details tag line, the two-branch empty state, and markup tests for audience, vocabulary scoping, and escaping (wave 6)
- [ ] 81-08-PLAN.md — Mobile display: the filter trigger and bottom drawer, chips on rows, the mobile details tag line, the two-branch empty state, and markup tests driven by a real mobile user agent (wave 6)

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

**Coverage:** 99/99 requirements mapped ✓ · 0 unmapped · 0 orphaned phases

## Research Flags

Phases 72 and 73 needed no research step — both were researched to implementation-ready depth with verified file paths and line numbers. See `.planning/research/SUMMARY.md`.

**Phase 78 needs a research step.** Open Graph and Twitter Card behaviour is defined by each consumer, not by a spec: Discord, Slack, iMessage, and WhatsApp differ on description length, image aspect ratio and size limits, redirect handling, and cache invalidation. Getting those wrong produces a silently absent card rather than an error, so the limits must be established before planning rather than discovered by trial. Phase 79 inherits the findings and needs no separate research pass.

**Phases 80 and 81 need no external research.** Both are ordinary EF Core schema-plus-CRUD work against a feature that already exists in this codebase; what they need is a discuss-phase pass on the modelling questions noted under each phase, not a web search.

**Phase 82 needs no external research.** The two safe cross-group read mechanisms it must choose between already exist in this codebase and are named in its scope notes; what it needs is a discuss-phase pass, not a web search.

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

---
*Roadmap created: 2026-08-25*
