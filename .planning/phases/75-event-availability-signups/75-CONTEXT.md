# Phase 75: Event Availability Signups - Context

**Gathered:** 2026-08-27
**Status:** Ready for planning

<domain>
## Phase Boundary

A player can record their availability for a calendar event as Yes, Maybe, or No, with the default determined by the board's immutable `BoardType`: **One-Shot boards are opt-in** (no signup row exists until a player creates one) and **Campaign boards are opt-out** (every member holds a Yes row from the moment the event exists, and opting out means changing that answer to No, not deleting it). Membership changes keep Campaign auto-signups in sync. A player can change their own answer at any time and nobody else's. No board can read or write another board's availability, proven by a two-group integration test.

The schema already exists — Phase 74 D-02 created `EventSignups` in migration `20260826134133_AddCalendarEventsFeature`. This phase is intended to be **pure code**: domain model, repository, service, controller actions, and view changes. Every decision below was taken to keep it that way (see D-10).

Not in this phase: the cross-event availability grid and its untouched-vs-real rendering (Phase 77, EVTVIEW-01…04), recurring series and occurrence generation (Phase 76), any change to quest signups or date votes, any change to `_Calendar.cshtml` or either calendar page.

</domain>

<decisions>
## Implementation Decisions

### Where availability is answered and seen

- **D-01: `Views/Events/Details.cshtml` is the only availability surface.** Three buttons — Yes / Maybe / No — following the established `changeVote()` idiom exactly: `fetch` POST carrying `__RequestVerificationToken`, then `location.reload()` on success and an `alert()` on failure (`Views/Quest/Details.cshtml:733` for the markup, `:966` for the script). `Events/Details.cshtml` has **no `.Mobile` variant**, so a single view serves desktop and mobile — no platform-divergence risk to manage this phase.

- **D-02: The page shows a named roster of who answered what, visible to every board member.** Not DM-gated: the roster renders for everyone, the same way any signed-up player sees other participants' votes on `Views/Quest/Details.cshtml:202–224`. Players self-coordinate without pulling the DM in.

  **Consequence for Phase 77:** this effectively pre-answers that phase's open "DM-only or all members?" question in the *all members* direction. Phase 77 should follow it unless there is a reason to diverge.

- **D-03: On a One-Shot board the roster lists only members who actually have a row.** No "hasn't answered yet" group, no count of the silent, and therefore no second query for board membership on that path. **Accepted cost:** on a One-Shot board you cannot tell "nobody else has looked" from "this is a small board".

- **D-04: The roster shows plain Yes / Maybe / No and does *not* mark an untouched Campaign default.** That distinction is Phase 77's job (EVTVIEW-02). **Accepted cost:** until Phase 77 ships, a fresh Campaign event's roster reads as unanimous agreement that nobody actually gave. This is the ROADMAP's named "Yes by default read as a real answer" risk, knowingly left in presentation — which is exactly where the ROADMAP says the fix belongs.

- **D-05: Nothing about availability appears on the calendar.** No marker on the desktop chip, no marker on the mobile agenda entry, no count. `Views/Shared/_Calendar.cshtml` and `Views/Calendar/Index.Mobile.cshtml` are **untouched this phase**, which keeps the five out-of-scope `Quest/Details(.Mobile).cshtml` call sites that Phase 74 D-09 protects entirely outside this phase's blast radius. The chip already links to the details view, so no information is unreachable.

### One-Shot opt-in lifecycle

- **D-06: One click on Yes / Maybe / No creates the row with that answer.** Signing up and answering are a single gesture. No separate "Sign Up for This Event" step — an event signup carries no role, no character, and no seat, so the two-step quest flow has nothing to model here.

- **D-07: A Withdraw action deletes the row, returning the player to not-answered.** This preserves EVTAVAIL-01's genuine third state rather than collapsing it into "No". Follows the `revokeSignup()` idiom at `Views/Quest/Details.cshtml:726`. It is the one write path in this phase that removes a row, so it needs its own ownership check and its own test.

- **D-08: Withdraw exists on One-Shot boards only, and the restriction is enforced server-side — not merely hidden in markup.** Follow the `QuestController` Close/Reopen precedent (`Controllers/QuestBoard/QuestController.cs:762`), which re-resolves the board type server-side and returns `BadRequest` when it is wrong, explicitly refusing to trust client-rendered button visibility. On a Campaign board, opting out is changing your own answer to No (EVTAVAIL-02) — a delete there would both contradict the requirement and be undone by the next auto-signup pass.

- **D-09 (locked by EVTAVAIL-03): a player changes only their own answer.** No DM override, no editing anyone else's availability, on any board type. Every write path takes the acting user from `User`, never from the request body.

### Recording a real answer vs an untouched default

- **D-10: Every player-initiated write stamps `UpdatedAt` — including the write that creates the row. Auto-signup passes never stamp it.** So `UpdatedAt != null` means "a human deliberately set this", uniformly on both board types, with no schema change and no board-type-dependent rule for a later phase to get wrong.

  This was chosen over an explicit `IsExplicit` column on **failure-mode** grounds, not simplicity: with one field, the answer write *is* the flag write, so they cannot diverge. With two fields, forgetting the flag still saves the answer correctly, so the bug is silent and only surfaces as a wrong colour in Phase 77, weeks later, in a different phase.

- **D-11: Surface it as a named property (e.g. `HasAnswered`) on the domain model.** No consumer — including Phase 77 — should read the raw timestamp for this purpose. This is what neutralises the one real cost of D-10, that `UpdatedAt` on a never-updated row is a semantic overload.

- **D-12: Rewrite the entity comment.** `EventSignupEntity.UpdatedAt` currently reads *"A null value means the answer has never been changed since it was created."* That stops being accurate under D-10 and must be replaced with what the field actually means: null = no human has ever set this answer.

- **D-13: Phase 76 inherits this as a discipline rule.** The occurrence generator's auto-signup fan-out must not stamp `UpdatedAt`. Call it out in that phase's context so it is not rediscovered.

### Campaign auto-signup — who and when

- **D-14: Every member gets a row regardless of role — DMs and Admins included.** Matches `UserRepository.GetAllGroupMembers`, whose own comment says membership is *"any UserGroups row for the group, regardless of role"*, and matches EVTAVAIL-02's "every board member" with no role carve-out. On a Campaign board the DM is running the session and their availability matters as much as anyone's — and because Campaign boards have no opt-in path (D-08), a `GroupRole.Player` filter would lock DMs out of the feature entirely rather than merely omitting them.

- **D-15: Rows are written at event-create time, in the same unit of work as the event.** Not materialized lazily on read. This is the literal reading of EVTAVAIL-02's "from the moment the event exists", and it keeps Phase 77's grid a straight join with no write side-effect on a read path. **Consequence:** event creation now depends on the member list, and Phase 76's generator must perform the same fan-out per materialized occurrence.

- **D-16: The fan-out runs regardless of the event's date**, including the past-dated events Phase 74 D-19 allows. One rule, no date comparison at create time, inherited unchanged by Phase 76. **Accepted cost:** backfilling a session that already happened marks everyone available for it — meaningless but harmless, and Phase 77 only shows upcoming events.

- **D-17: The joining-member backfill boundary is `Date >= today`** — today's event **included**. Use `DateOnly.FromDateTime(DateTime.Today)` with no time-of-day comparison, which is precisely the bug class Phase 74 D-01 chose `DateOnly` to make structurally impossible. Excluding today would leave someone who joins in the morning with no row for tonight's session and, on a Campaign board, no opt-in path to create one.

- **D-18: Hook the backfill at `GroupService.AddMemberAsync` — a verified single chokepoint.** Both entry points funnel through it: `Areas/Platform/Controllers/GroupController.cs` (direct add) and `UserService.CreateOrAddToGroupAsync`, which calls `groupService.AddMemberAsync` at `QuestBoard.Domain/Services/UserService.cs:178`. There is no second call site to remember. Hooking the Domain service (not the repository) also keeps the Service → Domain → Repository direction intact.

- **D-19: Membership and backfill are atomic — both or neither.** A failed backfill rolls back the join and the DM sees an error and retries. No half-synced state can exist, so Phase 77's grid can trust that "is a member" implies "has rows". Chosen over a best-effort join plus a repair button because the failure here is rare and *attended* — a loud, self-correcting error beats a silent gap that only a button nobody knows about can fix.

  **Note for the planner:** `GroupRepository.AddMemberAsync` currently calls `SaveChangesAsync` itself and catches `DbUpdateException` for the concurrent-add race. Achieving atomicity means either moving the fan-out into that same unit of work or wrapping both in an explicit transaction — a real, if modest, change to an existing method. Do not break its existing race handling.

### Leaving the board

- **D-20: Leaving deletes every event signup row that member holds on that board — past and future, auto-created and deliberate.** No date boundary, no touched-versus-untouched branch.

- **D-21: This amends EVTAVAIL-04, which must be updated.** The requirement currently reads *"a member who leaves keeps their past answers while their future auto-signups are removed."* Under D-20 the first clause is false. `.planning/REQUIREMENTS.md:44` needs rewording so the requirement and the code do not silently diverge — this is a roadmap/requirements action, not something to change quietly during implementation.

- **D-22: Accepted inconsistency with the rest of the app, chosen knowingly.** `GroupRepository.RemoveMemberAsync` (`QuestBoard.Repository/GroupRepository.cs:78`) deletes exactly one row — the `UserGroups` membership — and nothing else. So today a departing member keeps their `PlayerSignupEntity` rows, their `PlayerDateVoteEntity` votes, their `CharacterEntity` records (which carry their own `GroupId` and stay visible on the board), and their gold and `UserTransactionEntity` history. Their account survives too — `AdminController.DeleteUser` is membership-removal only. Event availability therefore becomes the **only** thing erased on leave.

  The rationale: a quest signup records something that happened; a Campaign Yes row is mostly an untouched default recording nothing. **Accepted cost:** with past events staying visible, a past event's roster silently loses everyone who has since left, so "who actually came on that date" stops being answerable. A remove-and-re-add also loses the member's deliberate answers and resets them to the Yes default.

- **D-23: Hook the cleanup at `GroupService.RemoveMemberAsync` — also a verified single chokepoint.** Both callers go through it: `Areas/Platform/Controllers/GroupController.cs:336` and `Controllers/Admin/AdminController.cs:360`.

- **D-24: The Platform Remove Member control gains a confirmation naming what is lost.** `GroupController.RemoveMember` today removes with no confirmation at all. This phase is what makes the action destructive, so the warning belongs here even though the page sits outside the event feature.

### Deleting an event that has answers

- **D-25: The existing native `confirm()` gains a count of the signup rows that will be destroyed** — e.g. *"Delete this event? N people have signed up and their availability will be lost."* This closes Phase 74 D-17, which explicitly deferred the question to this phase. The dialog stays a native `confirm()`: it is the app's delete idiom throughout (`revokeSignup`, the event delete itself), and a Bootstrap modal or type-to-confirm would be a new pattern on one page.

- **D-26: The count is of *all* signup rows, not only real answers.** **Explicitly chosen with the cost stated: on a Campaign board every member holds a row, so the dialog always reports the full member count and always fires at maximum volume, including on a freshly created event where nothing of value would be lost.** Do not "correct" this to count `HasAnswered` rows — it is a deliberate decision, not an oversight.

- **D-27: No DB-side work is needed for the delete itself.** `FK_EventSignups_Events_EventId` is already `ReferentialAction.Cascade` in the shipped migration, so removing an event removes its signups.

### Tenant scoping, write safety, and testing

- **D-28: Defence in both layers, restated from Phase 74 D-21 because this phase adds player-driven writes.** `EventSignupEntity`'s query filter scopes reads through its required `Event` navigation and is fail-closed. That constrains **reads only** — every signup write must additionally verify the target event belongs to the active board *and* that the acting user is the signup's owner. This app has shipped two real cross-tenant leaks (Phases 49/55) and Phase 72 D-13 found a third live gap during discussion.

- **D-29: A dedicated two-group integration test is not optional (EVTAVAIL-05).** `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to `ActiveGroupId = 1`, so the standard integration test is **structurally blind** to this bug class. Follow `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`: seed Group 2 via `factory.Database.CreateContext()`, flip `factory.TestGroupContext.ActiveGroupId = 1`, assert both read *and* write are refused, and reset to `1` in `DisposeAsync`.

- **D-30: Signup writes use narrow scalar-update repository methods**, mirroring `PlayerSignupRepository.ChangeVoteAsync` (`QuestBoard.Repository/PlayerSignupRepository.cs:43`). The generic `BaseRepository.UpdateAsync` is off-limits — the existing override exists precisely because AutoMapper overwrites navigation collections too aggressively. Note that `EventEntity` currently has **no** `Signups` navigation collection; if the plan adds one, this constraint becomes load-bearing rather than precautionary.

### Claude's Discretion

Not discussed — planner decides:

- **Whether a past event still accepts new or changed answers.** Raised during discussion, never settled. Phase 74 D-19 allows past-dated events to exist, so the case is reachable. Either behaviour is defensible; pick one and test it.
- Roster ordering (alphabetical, by answer, by answer time) and the empty-state copy when nobody has answered on a One-Shot board.
- Whether an availability change produces a toast, an email, or nothing. Nothing is the safe default — no EVTAVAIL requirement asks for a notification, and `_Toasts.cshtml` is already wired everywhere if one is wanted.
- Whether the join-time backfill runs inline or as a Hangfire job. Inline is the expected answer at this scale (members × future events is tens of rows), and D-19's atomicity requirement argues strongly for inline; a `GroupMembershipAddedEmailJob` precedent exists if there is a reason to reconsider.
- Exact wording of the two confirmation dialogs (D-25, D-24) and any toast messages.
- Domain model / repository / service / controller naming and file placement, and the Entity ↔ DomainModel ↔ ViewModel AutoMapper profile entries.
- Whether the roster renders inline in `Events/Details.cshtml` or as a partial.
- Whether `EventEntity` gains a `Signups` navigation collection, and the query shape used to load the roster without an N+1.
- Test structure beyond the mandated two-group isolation test (D-29) and the board-type enforcement test implied by D-08.
- `FK_EventSignups_AspNetUsers_UserId` has no cascade in the shipped migration. This is a non-issue today because the app never hard-deletes user accounts (`AdminController.DeleteUser` removes membership only), but worth knowing before writing any user-deletion code.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — the Phase 75 entry (goal, 5 success criteria, scope notes, and the 2 named risks). Also read the **Phase 76** entry, which inherits D-13 and D-15, and the **Phase 77** entry, whose EVTVIEW-02 depends entirely on D-10/D-11 and whose open "DM-only or all members?" question is pre-answered by D-02.
- `.planning/REQUIREMENTS.md:41–45` — EVTAVAIL-01 … EVTAVAIL-05 in full. **Note D-21: line 44 (EVTAVAIL-04) needs amending** — its "keeps their past answers" clause is superseded by D-20.
- `.planning/phases/74-event-schema-crud-and-calendar-display/74-CONTEXT.md` — the direct dependency. D-01 (`DateOnly`/`TimeOnly?`), D-02 (all three tables shipped in one migration, so Phase 75 is meant to be pure code), D-04 (tenant scoping shape), D-09 (the five protected `_Calendar.cshtml` call sites), D-10 (details view is the one event surface), **D-17 (the delete-confirmation question explicitly deferred to this phase → D-25/D-26)**, D-19 (past dates allowed → D-16).

### Project conventions
- `CLAUDE.md` — EF packages belong only in `QuestBoard.Repository`; the `modern-card` / `modern-card-header` / `modern-card-body` view pattern; **no GSD references in source comments** (applies to every comment written this phase, including the D-12 rewrite); migrations auto-apply on startup.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository one-way dependency and the two AutoMapper boundaries. Relevant to D-18/D-23, which hook the Domain service rather than the repository.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns.
- `.planning/codebase/TESTING.md` — integration vs unit test placement.

### Code the phase must read before changing
- `QuestBoard.Repository/Entities/EventSignupEntity.cs` — the shipped entity. `Availability` is a plain `int` with `[Range(0,2)]` mapping to `VoteType`; `UpdatedAt` carries the comment D-12 must rewrite.
- `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` — the shipped schema: unique `IX_EventSignups_EventId_UserId`, `IX_EventSignups_UserId`, cascade delete from `Events`, no cascade from `AspNetUsers`.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — the fail-closed global query filter block, including the "do not capture `ActiveGroupId` into a local var" warning.
- `QuestBoard.Repository/PlayerSignupRepository.cs:43` — `ChangeVoteAsync`, the narrow scalar-update precedent D-30 mandates.
- `QuestBoard.Domain/Enums/VoteType.cs` — `{ No = 0, Maybe = 1, Yes = 2 }`, reused as-is.
- `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs` — how the active board type is resolved server-side (needed by D-08 and D-15).
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — where the availability actions land; already carries the D-15-relevant `ActiveGroupId`-is-null SuperAdmin handling and the `SeriesIsOnActiveBoardAsync` second-layer check to mirror.
- `QuestBoard.Service/Views/Events/Details.cshtml` — the single availability surface (D-01). No `.Mobile` variant exists.
- `QuestBoard.Service/Views/Quest/Details.cshtml:202–224, :726, :733, :966` — the roster-rendering idiom (D-02), `revokeSignup()` (D-07), the three vote buttons and the `changeVote()` fetch script (D-01).
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:762` — the server-side board-type enforcement precedent D-08 follows.
- `QuestBoard.Domain/Services/GroupService.cs:24, :28` — the two chokepoints D-18 and D-23 hook.
- `QuestBoard.Repository/GroupRepository.cs:49, :78` — `AddMemberAsync` (its own `SaveChangesAsync` and `DbUpdateException` race handling matter to D-19) and `RemoveMemberAsync`.
- `QuestBoard.Domain/Services/UserService.cs:178` — proof that the invite flow routes through `GroupService.AddMemberAsync`.
- `QuestBoard.Repository/UserRepository.cs:51` — `GetAllGroupMembers`, the member list D-14 uses.
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:334` and `QuestBoard.Service/Controllers/Admin/AdminController.cs:343` — the two removal entry points; D-24 changes the first.
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — the D-29 precedent and why the default harness is blind without it.

### Do not touch
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml`
- `QuestBoard.Service/Views/Calendar/Index.cshtml`
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml`

  D-05 keeps all three out of this phase entirely, which in turn keeps the five protected `Quest/Details(.Mobile).cshtml` call sites out of the blast radius.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`VoteType { No, Maybe, Yes }`** — reused verbatim for `Availability`; the entity already stores it as `int` with `[Range(0,2)]`.
- **The `changeVote()` idiom** (`Quest/Details.cshtml:733` + `:966`) — three buttons, `fetch` POST with `__RequestVerificationToken`, `location.reload()` on success. Copy the shape rather than inventing one.
- **`revokeSignup()`** (`Quest/Details.cshtml:726`) — the delete-my-own-row pattern D-07 follows, including its `confirm()`.
- **`PlayerSignupRepository.ChangeVoteAsync`** — the narrow scalar-update template D-30 mandates.
- **`TenantIsolationTests.cs`** — a working two-group isolation test to copy structurally.
- **`IBoardTypeResolver`** — server-side board-type resolution, already injected in `AdminController` and used throughout `QuestController`.
- **`_Toasts.cshtml`** — wired into all five layouts; `TempData["Success"]` needs no view changes if a toast is wanted.

### Established Patterns
- **Membership mutation has exactly two chokepoints**, both in `GroupService`: `AddMemberAsync` and `RemoveMemberAsync`. Verified — the invite flow (`UserService.CreateOrAddToGroupAsync:178`), the Platform group page, and `AdminController.DeleteUser` all route through them. There is no third path.
- **`BoardType` is effectively immutable.** `GroupController.Edit` (POST) writes only `Name`; the `BoardType` on `GroupEditViewModel` is round-tripped and discarded. So no One-Shot → Campaign backfill problem exists, and D-15's create-time fan-out cannot be invalidated by a later board-type change.
- **"Member" means any role.** `GetAllGroupMembers` is explicitly role-agnostic, with a comment saying so; `GetAllGroupPlayers` is the `GroupRole.Player`-filtered variant. D-14 uses the former.
- **Never trust client-rendered visibility for a board-type rule.** `QuestController.Close`/`Reopen` re-resolve the board type server-side and `BadRequest` when wrong, with a comment saying exactly that. D-08 follows it.
- **Removing a member removes one row and nothing else.** `RemoveMemberAsync` deletes the `UserGroups` row only — no signup, vote, character, or transaction cleanup anywhere in the codebase. D-20 deliberately breaks this pattern for event signups; D-22 records that as a knowing choice.
- **`EventEntity` has no `Signups` navigation collection today.** Whether to add one is a planner decision; if added, D-30's constraint becomes load-bearing.

### Integration Points
- `QuestBoard.Domain/Services/GroupService.cs` — `AddMemberAsync` gains the backfill (D-18, atomic per D-19), `RemoveMemberAsync` gains the cleanup (D-23).
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — new availability actions (set / withdraw), plus the create path gaining the Campaign fan-out (D-15).
- `QuestBoard.Service/Views/Events/Details.cshtml` — buttons, roster, and the D-25 delete-dialog count.
- `QuestBoard.Service/Areas/Platform/.../GroupController.cs` and its Members view — the D-24 removal confirmation.
- New `EventSignup` domain model, repository, and service following Service → Domain → Repository, with both AutoMapper profiles extended. Nothing exists above the entity today.

</code_context>

<specifics>
## Specific Ideas

- **"A quest signup records something that happened; a Campaign Yes row is mostly an untouched default recording nothing."** This is the reasoning that makes D-20 (delete everything on leave) acceptable despite D-22's inconsistency with how quests, votes, and characters are preserved.
- **The one-field-cannot-diverge argument** is what decided D-10 over an explicit column. It is a failure-mode argument, not a simplicity argument, and the planner should not re-open it on "explicitness" grounds without engaging with that.
- **"A loud, self-correcting error beats a silent gap."** The reasoning behind D-19's atomicity, and the reason a repair button was rejected here and pushed toward Phase 76 where drift is unattended and likely.

</specifics>

<deferred>
## Deferred Ideas

- **An idempotent "sync availability" pass** — insert any missing rows for all members × all future events, safe to run repeatedly thanks to the unique `(EventId, UserId)` index. Rejected here because D-19's atomicity removes the failure it would repair, and a repair button only helps if someone knows to press it. **Aimed at Phase 76**, where the occurrence generator runs unattended on a schedule and the ROADMAP already names "the job silently stopping" and "retry re-running from scratch" as risks — there, an idempotent top-up is load-bearing rather than defensive.
- **Automatically purging past events** — raised during discussion and **considered and declined**, recorded here so it is not re-litigated. It would reverse shipped Phase 74 D-19, require a low-water mark on `EventSeriesEntity` so Phase 76's slot-index idempotency does not resurrect purged occurrences, and introduce unattended irreversible deletion. The cheaper alternative — hiding past events from the surfaces a DM looks at rather than deleting them — was also offered and declined. Past events stay exactly as they are today.
- **Distinguishing an untouched default from a real answer in the UI** — the data is recorded this phase (D-10/D-11) but rendered in Phase 77 (EVTVIEW-02). D-04 is what leaves the gap open in the meantime.
- **A per-event availability count** — Phase 77 owns it (EVTVIEW-03). Deliberately not added to the event details page or the calendar chip (D-04, D-05).
- **Marking availability on the calendar chip or mobile agenda entry** — rejected by D-05 to keep `_Calendar.cshtml` untouched. If it is ever wanted, Phase 74 D-09's default-empty-collection trick is the pattern that would keep the five protected call sites safe.
- **Guarding against a remove-and-re-add losing deliberate answers** — an accepted cost of D-20/D-22, not a bug to fix in this phase.

</deferred>

---

*Phase: 75-Event Availability Signups*
*Context gathered: 2026-08-27*
