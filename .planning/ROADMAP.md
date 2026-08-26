# Milestone v9.0: Rolling Improvements

**Status:** 🚧 IN PROGRESS
**Phases:** 72–79 so far (open-ended)
**Working branch:** `milestone/v9-rolling-improvements`

## Overview

A rolling bucket milestone for small, ad-hoc features and bug fixes. Unlike v1.0–v8.0, this milestone has no fixed end-state and no unifying theme — phases are appended as work arrives, and the milestone closes when the operator decides to cut it. Phase numbering continues from v8.0 (which ended at Phase 71).

The opening scope was three items. Two are small and independent: closing a UX gap where a player cannot change the character on a quest they have already signed up for, and resolving five stale HIGH GitHub security alerts left behind by the v5.0 EuphoriaInn→QuestBoard rename. The third is substantial — Calendar Events, spanning four phases, which adds dated informational entries to the calendar with per-event player availability and an optional recurrence model.

Appended 2026-08-26: **Link Previews**, spanning two phases, so that a quest, character, or contact link pasted into Discord or Slack renders a rich preview card instead of a bare URL. The cards are gated behind explicitly-minted signed share links rather than being public, because a board is private and external unfurl caches are permanent.

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
**Plans**: TBD (run `/gsd-plan-phase 74`)

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
**Plans**: TBD (run `/gsd-plan-phase 75`)

**Success criteria:**

1. On a One-Shot board, no signup exists for an event until a player creates one, and they can record Yes, Maybe, or No.
2. On a Campaign board, every member has a signup on each event with availability Yes from the moment the event exists, and opting out flips their own answer to No rather than deleting the signup.
3. A player can change their own availability at any time, and cannot change anyone else's.
4. A member joining a Campaign board is auto-signed-up to future events; a member leaving keeps their past answers while their future auto-signups are removed.
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
**Plans**: TBD (run `/gsd-plan-phase 76`)

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

**Risks this phase must actively avoid:**

- **The stale `ActiveGroupContextService` doc comment.** It claims a null `ActiveGroupId` means "see all"; the Phase 55 filters are fail-closed and return **zero** rows. The job runs outside `GroupSessionMiddleware`, so it must call `SetGroupId()` per group and iterate — never `IgnoreQueryFilters()`.
- **The job silently stopping.** The calendar quietly runs dry at the horizon with no error anyone sees — the failure mode the rolling window trades for never needing manual extension. Surface a horizon check somewhere a human actually looks.
- **Retry re-running from scratch.** A global `AutomaticRetryAttribute` is already registered app-wide, so a partially-failed run re-executes fully. Idempotency is a hard requirement, not a nicety.

### Phase 77: Availability Overview Page

**Goal**: A DM can see, in one place, who is available for which upcoming events — and tell a real answer apart from an untouched default.
**Depends on**: Phase 75 (and most valuable once Phase 76 populates it with recurring sessions)
**Requirements**: EVTVIEW-01, EVTVIEW-02, EVTVIEW-03, EVTVIEW-04
**Plans**: TBD (run `/gsd-plan-phase 77`)

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
**Plans**: TBD (run `/gsd-plan-phase 78`)

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

## Phase Ordering Rationale

- Phases 72 and 73 share no code, no files, and no data — either order works, and neither blocks the other.
- 72 is sequenced first because it is the operator's driving request and carries the user-visible value; 73 is maintenance. Research suggested the reverse (bank the zero-risk win first, and start the rate-limited graph refresh early), so flipping them costs nothing if preferred.
- Within Phase 72, the shared partial must exist before either host view can call it.
- The events feature (74–77) is a strict dependency chain: schema → signups → recurrence → overview. Each phase ships a usable increment rather than scaffolding.
- Recurrence (76) is sequenced *after* signups (75) rather than before, because materialized occurrences must carry availability from the moment they exist.
- **If scope needs cutting mid-milestone**, stopping after 75 leaves a complete, usable non-recurring events feature. Recurrence and the overview grid are separable value-adds, not blocking dependencies.
- Phases 78 and 79 (link previews) were appended on 2026-08-26 and are independent of the events chain — they touch no event, signup, or calendar code, so they can run before, after, or alongside 74–77.
- 78 must precede 79: it owns the signing scheme, the meta partial, the absolute-URL helper, and the Markdown-to-plain-text summarizer that 79 consumes. It also ships usable value on its own (quest cards), so stopping after 78 leaves a complete feature.
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

**Coverage:** 50/50 requirements mapped ✓ · 0 unmapped · 0 orphaned phases

## Research Flags

Phases 72 and 73 needed no research step — both were researched to implementation-ready depth with verified file paths and line numbers. See `.planning/research/SUMMARY.md`.

**Phase 78 needs a research step.** Open Graph and Twitter Card behaviour is defined by each consumer, not by a spec: Discord, Slack, iMessage, and WhatsApp differ on description length, image aspect ratio and size limits, redirect handling, and cache invalidation. Getting those wrong produces a silently absent card rather than an error, so the limits must be established before planning rather than discovered by trial. Phase 79 inherits the findings and needs no separate research pass.

---
*Roadmap created: 2026-08-25*
