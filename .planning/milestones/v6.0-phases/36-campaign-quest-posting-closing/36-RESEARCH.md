# Phase 36: Campaign Quest Posting & Closing - Research

**Researched:** 2026-07-03
**Domain:** ASP.NET Core 10 MVC / EF Core — conditional business logic and view rendering gated by a per-group enum (`BoardType`)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Close/Reopen control**
- **D-01:** Location and authorization follow the existing `Finalize`/`Open` precedent exactly — DungeonMasterOnly-gated actions on `Manage.cshtml`, restricted to the quest's owning DM or a group Admin (`IsQuestOwner(currentUser, quest.DungeonMaster) || role == GroupRole.Admin`, mirroring `QuestController.Finalize`/`Open`). This was not treated as an open question — the user confirmed there's no reason to deviate from precedent here.
- **D-02 (Claude's discretion):** After closing, the page should stay on `Manage` (re-rendering with a Reopen button in place of Close) rather than redirecting to `Details` — reasoning: Close/Reopen is a reversible toggle like the existing `Open` action (which stays on Manage), not a one-time commit like `Finalize` (which redirects to Details).
- **D-03 (Claude's discretion):** Close/Reopen should be a single-click POST with no confirmation dialog — matches the existing Finalize/Open pattern exactly; no confirmation dialogs exist anywhere else in the app today.

**Campaign quest board display**
- **D-04:** Drop the Challenge Rating badge entirely for campaign quests — on the quest board card, Details, and Manage pages. CR was a signup-decision aid ("should I sign up for this difficulty?"); campaign quests have no signup decision.
- **D-05:** Remove the signup-count line entirely from the campaign quest board card (replaces "Adventurers signed up: N" / "Selected Adventurers: N", both driven by `PlayerSignups` which campaign quests won't have). Let the `.quest-description` div expand to fill the freed vertical space rather than substituting different content into that slot.
- **D-06 (resolved by UI-SPEC):** The wax-seal treatment on the board card — resolved in 36-UI-SPEC.md: reuse the exact same wax-seal mechanic, relabeled Open/Closed. No new visual, no new CSS. See UI-SPEC's "Key Design Decision: Campaign Wax-Seal Treatment" for the full implementation contract.

**Quest Log integration**
- **D-07:** Apply the same simplification to the Quest Log card as the board card — drop the CR badge and the "Adventurers: N" (`selectedPlayers.Count`) line for campaign-closed quest entries.
- **D-08:** The Session Recap field (DM's free-text post-session notes, `Quest.Recap`) applies to campaign quests exactly as it does to one-shot — DM can add/edit it on a closed campaign quest via the same `UpdateRecap` action/UI.
- **Note:** Mixed-vs-separate Quest Log sections was raised as a question and withdrawn as moot — `BoardType` is set per-group and immutable (Phase 35), so a single group's Quest Log can only ever contain one kind of entry (all one-shot-finalized, or all campaign-closed). There is no scenario where the two interleave within one group's view.

**Campaign create-form fields**
- **D-09:** Drop the Challenge Rating field from the campaign Create form entirely (not just hidden from display) — value defaults under the hood, not DM-selectable for campaign quests.
- **D-10:** Drop `TotalPlayerCount` from the campaign Create form entirely — no signup cap needed since there's no per-quest signup to cap.
- **D-11:** Drop the `DungeonMasterSession` checkbox/concept from campaign quests entirely — it doesn't map to campaign mode (campaign quests are all real party quests; the "DM-only planning session excluded from Quest Log" concept was one-shot-specific).

### Claude's Discretion
- Post-close redirect target (D-02) and confirmation-step presence (D-03) — both explicitly deferred to Claude by the user (resolved above).
- Exact mechanism for how `ProposedDates`' required-field validation on `QuestViewModel` relaxes for campaign quests (conditional validation, separate ViewModel, etc.) — not discussed in CONTEXT.md; this research resolves it (see Summary + Architecture Patterns, Pattern in `QuestViewModel`/Create action section below).
- Exact mechanism for how Close/Reopen state is modeled at the entity level (reusing `IsFinalized`/`FinalizedDate`, or new `IsClosed`/`ClosedDate` fields) — not discussed in CONTEXT.md; PROJECT.md already locks that `CloseQuestAsync`/`ReopenQuestAsync` are additive and separate from `FinalizeQuestAsync`/`OpenQuestAsync` at the service-method level. This research resolves the entity/schema shape (see Summary finding #1).

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope (per 36-CONTEXT.md `<deferred>`).

Additionally, from `.planning/REQUIREMENTS.md`'s Out of Scope table (applies to this phase):
- Changing board type after group creation (locked at creation, Phase 35)
- Per-quest party tagging for campaign quests (roster is fixed at group level, no per-quest signup UI)
- Rewards/gold flow tied to closing a campaign quest (closing is a simple status toggle only)
- Navigation visibility and Email Stats access control (NAV-0x, ACCESS-01 — deferred to Phase 37)
- Separate Area/controller stack for campaign quests (confirmed unnecessary; shared `QuestController`/`QuestService` gated by `BoardType`)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CQUEST-01 | DM can post a quest for a campaign group without selecting proposed dates | Summary finding #2 + Architecture Patterns (Create POST conditional validation); Pitfall 4 (default values for stripped fields) |
| CQUEST-02 | Campaign quest pages show no per-quest signup or date-voting UI — the party is the group's fixed roster | Architecture Patterns Pattern 3 (`ViewBag.BoardType` threading into `Details`/`Manage` views); UI-SPEC (already locks the exact markup removals) |
| CQUEST-03 | DM can close a campaign quest (simple status toggle), hiding it from the active quest board | Summary finding #1 (entity design); Architecture Patterns Pattern 1 & 2 (`CloseQuestAsync`, `Close` action); Pitfall 1 (all board-filter sites that must learn `IsClosed`) |
| CQUEST-04 | DM can reopen a closed campaign quest | Same as CQUEST-03, mirrored (`ReopenQuestAsync`, `Reopen` action) |
| CQUEST-05 | Party can browse closed campaign quests in the Quest Log immediately after closing (no next-day wait like one-shot finalization) | Pitfall 2 & 3 (`QuestLogController` guards and `GetCompletedQuestsAsync` OR-branch) |
| CQUEST-06 | No quest-related emails are sent for campaign-group quests (no posted/reminder/finalized notifications) | Summary finding #1 (structural exclusion — `CloseQuestAsync`/`ReopenQuestAsync` never call `IQuestEmailDispatcher`, never touch `FinalizedDate`); confirmed no "posted" email exists at all today (Sources — `IQuestEmailDispatcher`, `HangfireQuestEmailDispatcher`, `NullQuestEmailDispatcher`, `Jobs/` directory listing) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

These directives apply to this phase and constrain the plan:

- **Branching:** Work must occur on the existing feature/milestone branch (`milestone/v6-board-types`, confirmed current branch) — never commit directly to `main`.
- **EF Core migrations:** Must live only in `QuestBoard.Repository` — never add EF packages to `QuestBoard.Service`. Migrations auto-apply on startup via `context.Database.Migrate()`; no manual `dotnet ef database update` needed in dev. Use `dotnet ef migrations add MigrationName --project ../QuestBoard.Repository` (run from `QuestBoard.Service/`).
- **Three-layer architecture:** Service → Domain → Repository, strict one-way dependency. New `CloseQuestAsync`/`ReopenQuestAsync` must be added at all three layers (`IQuestRepository`/`QuestRepository`, `IQuestService`/`QuestService`, `QuestController`), matching the existing `FinalizeQuestAsync`/`OpenQuestAsync` layering exactly.
- **AutoMapper boundaries:** Entity ↔ DomainModel mapping lives in `QuestBoard.Repository/Automapper/EntityProfile.cs`; DomainModel ↔ ViewModel mapping lives in `QuestBoard.Service/Automapper/ViewModelProfile.cs`. New `IsClosed`/`ClosedDate` fields need default (implicit, same-name) mapping in both — no custom `ForMember` needed since names match exactly, consistent with how `IsFinalized`/`FinalizedDate` require no explicit `ForMember` entries today.
- **Authorization policies:** `"DungeonMasterOnly"` (DungeonMaster or Admin role) and `"AdminOnly"` (Admin role only) are the only two policies — new `Close`/`Reopen` actions use `"DungeonMasterOnly"` per D-01.
- **Code comments:** Never embed GSD planning/tracking references (`CQUEST-01`, `Phase 36`, etc.) in source code comments/XML docs/string literals — write plain-language comments that stay true independent of phase. This RESEARCH.md itself may reference requirement IDs freely (planning doc), but the plan's generated code must not.
- **UI/UX guidelines:** No new views are created by this phase (confirmed by 36-UI-SPEC.md — "zero new views/pages"), so the `modern-card`/`modern-card-header`/`modern-card-body` pattern requirement does not apply to new markup; existing markup in `Manage.cshtml` already uses this pattern and must not regress it when Close/Reopen buttons are added.
- **Windows dev environment:** Use CRLF line endings and Windows-style paths when creating/editing files; SQL Server runs on the Windows host (`localhost` connection string for local dev, `sqlserver` service name in Docker — not applicable to this phase's changes directly, but the migration will run against the local SQL Server instance during `dotnet build`/`dotnet run`).

## Summary

This phase adds a second lifecycle (Close/Reopen) alongside the existing one-shot lifecycle (Finalize/Open) on the same `QuestEntity`/`QuestController`/`QuestService` stack, gated entirely by `BoardType` read from the quest's owning `Group`. The two open questions from CONTEXT.md have clean, low-risk answers grounded directly in existing code:

1. **Entity design:** add two new nullable/bool fields to `QuestEntity` — `bool IsClosed` and `DateTime? ClosedDate` — additive and structurally parallel to `IsFinalized`/`FinalizedDate`, not a reuse/overload of them. This keeps `FinalizedDate` untouched, which means `DailyReminderJob.GetQuestsForTomorrowAllGroupsAsync` (keyed off `FinalizedDate`) and `QuestFinalizedEmailJob`'s trigger (`FinalizeQuestAsync`'s call to `dispatcher.EnqueueFinalizedEmail`) **already** exclude every campaign quest with zero code changes, because a campaign quest's `CloseQuestAsync` will never touch `IsFinalized`/`FinalizedDate` at all. This is the single most important research finding for CQUEST-06.

2. **`ProposedDates` validation relaxation:** do not use a custom `IValidatableObject`/conditional `ValidationAttribute` on the shared `QuestViewModel`. Instead, add a `BoardType BoardType` property to `QuestViewModel` (populated server-side from the active group before validation, never bound from the posted form), and validate/branch explicitly in the `Create` POST action before mapping — mirroring the existing pattern where `Edit`/`Create` actions already do hand-written guard checks (`if (existingQuest.IsFinalized) return BadRequest(...)`) rather than relying purely on data-annotation validation. Relax `[Required, MinLength(1)]` on `ProposedDates` to a plain default (`[]`) and enforce "at least one date, required only for OneShot" in the controller action, matching the existing `CreateFollowUp` POST action's exact style (`if (!ModelState.IsValid || viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0)` with a manually added `ModelState.AddModelError`). This avoids introducing a new validation abstraction into a codebase that has never used one.

Both `Quest` (domain model) and the view models passed to `Index.cshtml`/`Manage.cshtml`/`Details.cshtml` currently carry **no `BoardType`/`Group` data at all** — this is new plumbing this phase must add, not something to discover as already wired up. The cleanest path is a `ViewBag.BoardType` (or a lightweight wrapper view model) populated once per controller action from `IGroupService.GetByIdAsync(quest.GroupId)`, rather than threading `BoardType` onto the `Quest` domain model itself (which would require AutoMapper changes and touches many more call sites for zero benefit, since every current view already receives quest(s) already scoped to one active group via the existing `HasQueryFilter` tenant isolation).

**Primary recommendation:** Add `IsClosed`/`ClosedDate` to `QuestEntity` as new additive columns (EF Core migration, `defaultValue: false`/`null`), add `CloseQuestAsync`/`ReopenQuestAsync` to `IQuestRepository`/`QuestRepository`/`IQuestService`/`QuestService` mirroring `OpenQuestAsync`'s exact shape (no email dispatch calls — campaign has none), add `Close`/`Reopen` actions to `QuestController` mirroring `Finalize`/`Open` line-for-line, and thread `BoardType` into views via `ViewBag.BoardType` populated from `IGroupService.GetByIdAsync(quest.GroupId)` in each relevant controller action.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| BoardType-conditional Create form validation | API / Backend (Service layer, `QuestController`) | Browser (Razor conditional rendering) | Validation must be server-authoritative; the view only needs to *hide* fields, the controller must *not require* them |
| Close/Reopen state transition | API / Backend (`QuestService`/`QuestRepository`) | Database | Simple flag+date mutation, identical shape to existing `FinalizeQuestAsync`/`OpenQuestAsync` |
| Quest board / Quest Log visibility rules | API / Backend (`QuestService` query methods) | Database (new index) | The "what counts as active/closed/completed" filter logic already lives in `QuestService`/`QuestRepository`, not in views |
| BoardType-conditional view rendering (CR badge, signup section, wax seal) | Frontend Server (SSR / Razor) | — | Purely presentational; no business logic, per UI-SPEC |
| Email suppression for campaign quests | API / Backend (by construction — `CloseQuestAsync` never calls `IQuestEmailDispatcher`) | — | No guard needed; suppression is structural, not conditional, per Summary finding #1 |
| BoardType lookup for a given quest | API / Backend (`IGroupService.GetByIdAsync`) | — | `Quest` domain model has no `Group` navigation; must be fetched separately per request |

## Standard Stack

No new external packages are introduced by this phase. All work uses the existing in-repo stack:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| EF Core | (project-pinned, .NET 10) | New migration for `IsClosed`/`ClosedDate` columns + index | Existing ORM, migrations auto-apply on startup per CLAUDE.md |
| AutoMapper | (project-pinned) | `Quest ↔ QuestEntity`, `QuestViewModel ↔ Quest` mapping updates | Existing mapping boundary per CLAUDE.md architecture |
| ASP.NET Core MVC | 10 | Controller actions, Razor views | No framework changes permitted (PROJECT.md constraint) |

### Supporting
None — no new supporting libraries needed.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `IsClosed`/`ClosedDate` fields | Reuse `IsFinalized`/`FinalizedDate` for campaign Close too | Rejected — would make `DailyReminderJob` and `QuestFinalizedEmailJob` pick up campaign quests, actively breaking CQUEST-06 (Summary finding #1). PROJECT.md already locks `CloseQuestAsync`/`ReopenQuestAsync` as additive/separate at the service level; separate entity fields is the only design consistent with that lock. |
| Controller-level validation relaxation | Custom `IValidatableObject` on `QuestViewModel` | Rejected — codebase has zero precedent for conditional/custom validation attributes; every existing "business rule beyond data shape" check (finalized-quest edit guard, follow-up date requirement) is hand-written in the controller action, not a validation attribute. Introducing a new validation abstraction for one field is inconsistent with `CONVENTIONS.md`. |
| Controller-level validation relaxation | Separate `CampaignQuestViewModel` + separate Create view | Rejected — CONTEXT.md's domain boundary explicitly says "reuses the existing `QuestController`/`QuestService`/views... no new controller or Area"; a second ViewModel is compatible with that boundary in principle, but doubles the AutoMapper surface and the `Create.cshtml`/`EditQuestViewModel` reuse (Edit nests `QuestViewModel` inside `EditQuestViewModel`) for a single-field relaxation. Conditional logic on the existing `QuestViewModel` is simpler and keeps one Create action. |
| `ViewBag.BoardType` per action | Add `BoardType` to `Quest` domain model + AutoMapper | Rejected — would require every `CreateMap<QuestEntity, Quest>()` call path to `.Include(q => q.Group)` (adds a join to every quest query, including the hot `Index`/board list path), and touches `EntityProfile.cs` mapping for a value that's needed only for view rendering, not domain logic. `ViewBag` (already used pervasively in this controller for view-only flags like `IsAuthorized`/`IsAdmin`) is the lower-blast-radius choice and matches existing convention. |

**Installation:** No new packages — nothing to install.

**Version verification:** Not applicable — no new package versions to verify. Existing project TFM confirmed via `QuestBoard.UnitTests.csproj`/`QuestBoard.IntegrationTests.csproj`: `net10.0`. `[VERIFIED: local file read]`

## Package Legitimacy Audit

Not applicable — this phase installs no external packages (pure application code + one EF Core migration using already-referenced EF Core packages).

## Architecture Patterns

### System Architecture Diagram

```
DM submits Create form (Campaign group)
        │
        ▼
QuestController.Create (POST)
  ├─ Read BoardType from ViewBag/service (IGroupService.GetByIdAsync(activeGroupId))
  ├─ If Campaign: skip ProposedDates.Count==0 rejection, ignore CR/TotalPlayerCount/DMSession inputs
  ├─ AutoMapper: QuestViewModel → Quest (ProposedDates empty list for Campaign)
  └─ QuestService.AddAsync → QuestRepository.AddAsync → DB
        │
        ▼
Quest appears on board (QuestController.Index)
  └─ GetQuestsWithSignupsForRoleAsync — filter must treat "active" as:
       OneShot:  !IsFinalized || (IsFinalized && FinalizedDate > oneDayAgo)
       Campaign: !IsClosed   (no date-based grace window — closes are immediate per CQUEST-03/04)

DM clicks "Close Quest" on Manage.cshtml
        │
        ▼
QuestController.Close (POST, DungeonMasterOnly, IsQuestOwner-or-Admin)
  └─ QuestService.CloseQuestAsync → QuestRepository.CloseQuestAsync
       sets IsClosed = true, ClosedDate = DateTime.UtcNow
       (does NOT touch IsFinalized/FinalizedDate — structurally cannot trigger
        DailyReminderJob or QuestFinalizedEmailJob, which both key off FinalizedDate)
        │
        ▼
Quest Log (QuestLogController.Index → QuestService.GetCompletedQuestsAsync)
  └─ Must OR-branch: (IsFinalized && FinalizedDate <= yesterday && !DungeonMasterSession)
                      OR (IsClosed)   [campaign: immediate, no next-day wait — CQUEST-05]

DM clicks "Reopen Quest" on Manage.cshtml
        │
        ▼
QuestController.Reopen (POST, same authz) → QuestService.ReopenQuestAsync
  └─ QuestRepository.ReopenQuestAsync: IsClosed = false, ClosedDate = null
       Quest reappears on active board immediately (query filter re-evaluated on next Index load)
```

### Recommended Project Structure

No new files/folders beyond the standard per-feature additions to existing files:
```
QuestBoard.Repository/
├── Entities/QuestEntity.cs           # + IsClosed, ClosedDate
├── Migrations/                        # + new migration: AddQuestCloseFields (or similar)
└── QuestRepository.cs                 # + CloseQuestAsync, ReopenQuestAsync

QuestBoard.Domain/
├── Models/QuestBoard/Quest.cs         # + IsClosed, ClosedDate
├── Interfaces/IQuestRepository.cs     # + CloseQuestAsync, ReopenQuestAsync signatures
├── Interfaces/IQuestService.cs        # + CloseQuestAsync, ReopenQuestAsync signatures
└── Services/QuestService.cs           # + CloseQuestAsync, ReopenQuestAsync (thin passthrough, like OpenQuestAsync)

QuestBoard.Service/
├── Controllers/QuestBoard/QuestController.cs   # + Close, Reopen actions; Create conditional validation
├── ViewModels/QuestViewModels/QuestViewModel.cs # BoardType property (server-set, not [Required] ProposedDates)
├── Views/Quest/{Index,Details,Manage}.cshtml (+.Mobile)  # BoardType-conditional rendering
├── Views/Quest/Create.cshtml                    # conditional field visibility
└── Views/QuestLog/{Index,Details}.cshtml         # BoardType-conditional card simplification
```

### Pattern 1: Additive lifecycle fields, mirrored service/repository methods
**What:** `CloseQuestAsync`/`ReopenQuestAsync` implemented as a near-identical copy of `OpenQuestAsync`'s shape (load entity, flip flag+date, save) rather than trying to generalize `FinalizeQuestAsync`/`OpenQuestAsync` into a shared "lifecycle" abstraction.
**When to use:** When PROJECT.md has already locked the two lifecycles as additive/separate (as it has here) — do not attempt to unify them into one parameterized method now; that's a larger refactor with no requirement asking for it.
**Example:**
```csharp
// Source: existing QuestRepository.cs:140-157 (OpenQuestAsync) — pattern to mirror
/// <inheritdoc/>
public async Task CloseQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsClosed = true;
    entity.ClosedDate = DateTime.UtcNow;

    await DbContext.SaveChangesAsync(token);
}

/// <inheritdoc/>
public async Task ReopenQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsClosed = false;
    entity.ClosedDate = null;

    await DbContext.SaveChangesAsync(token);
}
```
Note: unlike `OpenQuestAsync`, `CloseQuestAsync`/`ReopenQuestAsync` do NOT need `.Include(q => q.PlayerSignups)` — campaign quests never populate `PlayerSignups`, so there is no signup-deselection side effect to perform (consistent with D-03's "no destructive side effect" reasoning in the UI-SPEC).

### Pattern 2: Controller action mirrors Finalize/Open exactly, including authorization
**What:** New `Close`/`Reopen` actions copy `Finalize`/`Open`'s authorization and not-found guard structure verbatim.
**When to use:** D-01 explicitly locks this — no deviation.
**Example:**
```csharp
// Source: existing QuestController.cs:637-666 (Open action) — pattern to mirror exactly
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Close(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || quest.IsClosed) return NotFound();

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) return Challenge();

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();

    await questService.CloseQuestAsync(id);
    return RedirectToAction("Manage", new { id });   // D-02: stays on Manage, not Details
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Reopen(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || !quest.IsClosed) return NotFound();

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) return Challenge();

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();

    await questService.ReopenQuestAsync(id);
    return RedirectToAction("Manage", new { id });
}
```
Note the integration test `QuestFinalizeTests.FinalizeAction_BodyIsTwentyLinesOrFewer` is a *regression guard specific to `Finalize`* (CTRL-01) — it does not automatically apply to `Close`/`Reopen`, but both new actions naturally come in under 20 lines following this pattern, so no test changes are needed there. Verify during planning whether a parallel guard test is expected for the new actions (not required by any CQUEST-0x requirement, but consistent with the codebase's existing quality bar).

### Pattern 3: BoardType lookup via ViewBag, not domain model
**What:** Fetch `BoardType` once per controller action via `IGroupService.GetByIdAsync(quest.GroupId)` and expose as `ViewBag.BoardType`, following the existing `ViewBag.IsAuthorized`/`ViewBag.IsAdmin`/`ViewBag.CanManage` convention already used throughout `QuestController`.
**When to use:** Any action that renders a view needing board-type-conditional sections (`Index`, `Details`, `Manage`, `Create`) — and `QuestLogController.Index`/`Details`.
**Example:**
```csharp
// New helper, colocated with GetEffectiveRoleAsync in QuestController
private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
{
    var groupId = activeGroupContext.RequireActiveGroupId();
    var group = await groupService.GetByIdAsync(groupId, token);
    return group?.BoardType ?? BoardType.OneShot;
}
```
Since `BoardType` is immutable per group (Phase 35, BOARD-02) and every quest in a request is already scoped to exactly one active group (tenant isolation query filter), a single lookup per action is correct and sufficient — no need to resolve BoardType per-quest in a loop on `Index` (all quests on one board share the same group's BoardType).

**Caveat — `DailyReminderJob`/`GetQuestsForTomorrowAllGroupsAsync` is explicitly cross-group** (`IgnoreQueryFilters()`), so `IActiveGroupContext.ActiveGroupId` is not meaningful there; this doesn't matter because that job only ever touches `FinalizedDate`-keyed one-shot quests by construction (see Summary finding #1) — no `BoardType` check is needed inside the job itself.

### Anti-Patterns to Avoid
- **Adding a `BoardType` guard clause inside `DailyReminderJob` or `QuestFinalizedEmailJob`:** Unnecessary — the entity design (separate `IsClosed`/`ClosedDate` fields never touched by Close/Reopen) already makes these jobs structurally blind to campaign quests. Adding a redundant guard is not wrong, but research found no functional need for it; if the planner adds one anyway for defensive clarity, it should be a one-line comment, not new branching logic.
- **Making `ProposedDates` nullable and relying on `[Required]` alone:** `[MinLength(1)]` on an empty (not null) `IList<DateTime>` still fires today; simply removing `[Required]` is insufficient — the `MinLength(1)` attribute must also be removed/relaxed, and campaign quests must post an explicitly empty list, which AutoMapper's existing `CreateMap<QuestViewModel, Quest>()` already handles correctly (`ProposedDates` maps via `.MapFrom(src => src.ProposedDates)`, and an empty `IList<DateTime>` maps to an empty `IList<ProposedDate>` with no special-casing needed).
- **Introducing a `BoardType.Campaign` check inside `QuestBoardContext`'s `HasQueryFilter`:** Out of scope — the existing group-isolation filter (`e.GroupId == activeGroupContext.ActiveGroupId`) already scopes every quest correctly; `BoardType` is a display/behavior concern, not a tenant-isolation concern, and does not belong in the EF Core query filter.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Board-type dispatch in C# | if/else chains scattered across services | `switch` expressions on `BoardType`, per PROJECT.md's `ShopService.CalculateItemPriceAsync`/`ItemRarity` precedent | Already the locked project convention (STATE.md: "v6.0 planning: `BoardType` dispatch uses C# switch expressions... not if/else chains") |
| Conditional required-field validation | Custom `ValidationAttribute`/`IValidatableObject` | Controller-level guard clause before `ModelState.IsValid` check, mirroring `CreateFollowUp` POST's existing manual `ModelState.AddModelError` pattern | Zero existing precedent for custom validation attributes in this codebase; consistency > cleverness |
| "Is this quest visible on the active board" filter logic | New service, or duplicating filter logic in the view | Extend the existing `GetQuestsWithSignupsForRoleAsync`/`GetQuestsWithSignupsAsync` predicates in `QuestRepository` with an `IsClosed`-aware branch | This logic already lives in exactly one place (`QuestRepository`); views must never re-derive "is this active" from raw fields |

**Key insight:** This phase's entire complexity is "does field X exist / does branch Y fire for this BoardType" — there is no genuinely novel technical problem being solved (no new persistence pattern, no new UI framework, no new email/job infrastructure). The risk is entirely in *finding every place* the one-shot lifecycle assumptions are baked in, not in designing anything new. Treat this as a systematic audit-and-extend task, not a feature-design task.

## Common Pitfalls

### Pitfall 1: Missing one of the several places `IsFinalized`/`FinalizedDate` gates "is this quest active/visible"
**What goes wrong:** The active-board filter exists in at least 3 separate places that must all learn about `IsClosed`: `QuestRepository.GetQuestsWithSignupsAsync` (line ~58-66), `QuestRepository.GetQuestsWithSignupsForRoleAsync` (line ~69-78), and `QuestController.Index`'s Mobile view's inline status-badge logic (`Index.Mobile.cshtml` lines 39-57, which independently recomputes status text rather than reusing a shared helper). Missing any one means campaign quests either never disappear when closed, or one-shot quests regress.
**Why it happens:** No shared "is this quest active" predicate exists as a single method — the boolean logic is duplicated inline in three places (two repository LINQ predicates, one Razor view).
**How to avoid:** Grep for `IsFinalized` and `FinalizedDate` across `QuestBoard.Repository`, `QuestBoard.Domain`, and `QuestBoard.Service/Views` before considering the phase done; every hit is a candidate site that may need a parallel `IsClosed`/campaign branch. Confirmed hit locations in this research: `QuestService.cs` (`GetCompletedQuestsAsync`), `QuestRepository.cs` (`GetQuestsWithSignupsAsync`, `GetQuestsWithSignupsForRoleAsync`, `FinalizeQuestAsync`, `OpenQuestAsync`, `GetFinalizedQuestsForDateAsync`, `GetQuestsForTomorrowAllGroupsAsync`), `QuestController.cs` (`Edit` GET/POST guards, `Details` POST guard, `JoinFinalizedQuest`, `ChangeVoteToYes`, `Finalize`, `Open`, `SendReminder`, `CreateFollowUp`), `QuestLogController.cs` (`Index`'s implicit reliance on `GetCompletedQuestsAsync`, `Details`, `UpdateRecap`), and views `Index.cshtml`, `Index.Mobile.cshtml`, `Manage.cshtml`, `Manage.Mobile.cshtml`, `Details.cshtml`. Not all of these need changes (e.g. `JoinFinalizedQuest`/`ChangeVoteToYes`/signup-related actions are legitimately one-shot-only and campaign quests will simply never reach them since they have no `PlayerSignups` UI) — but each must be *considered*, not assumed safe.
**Warning signs:** A closed campaign quest still appears on `/quests`; a reopened campaign quest doesn't reappear; `QuestLogController.Details`'s guard (`if (!quest.IsFinalized || ...)`) 404s a valid closed campaign quest because it never learned about `IsClosed`.

### Pitfall 2: `QuestLogController.Details`/`UpdateRecap` guards will 404 or reject every campaign quest unless updated
**What goes wrong:** `QuestLogController.Details` (line 41) and `UpdateRecap` (line 83) both hard-code `if (!quest.IsFinalized || !quest.FinalizedDate.HasValue || quest.FinalizedDate.Value.Date > DateTime.UtcNow.AddDays(-1).Date || quest.DungeonMasterSession) return NotFound()/BadRequest()`. A closed campaign quest has `IsFinalized == false` always, so this guard will incorrectly reject it.
**Why it happens:** These guards were written when only one lifecycle existed; they conflate "is this a valid Quest Log entry" with "is this specifically a finalized-and-aged one-shot quest."
**How to avoid:** Both guards need an explicit OR-branch: `(quest.IsFinalized && quest.FinalizedDate.HasValue && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date && !quest.DungeonMasterSession) || quest.IsClosed`. D-08 (Recap applies to campaign quests exactly as one-shot) makes this a hard requirement, not an edge case.
**Warning signs:** DM can close a campaign quest, sees it "in the Quest Log right away" per Success Criterion 5 on the Index list, but clicking into it 404s.

### Pitfall 3: `GetCompletedQuestsAsync`'s single `.Where()` clause needs an OR, not an AND, for campaign
**What goes wrong:** Naively adding `&& !quest.IsClosed` or similar to the existing one-shot predicate would suppress campaign quests entirely rather than including them without the next-day wait.
**Why it happens:** The existing predicate is a single AND-chain (`IsFinalized && FinalizedDate <= yesterday && !DungeonMasterSession`); the natural instinct when "fixing" it for a new case is to keep extending the same AND-chain, which is wrong here because campaign membership is a parallel, not overlapping, condition.
**How to avoid:** Structure as `(oneShotCondition) || (quest.IsClosed)`, confirmed against CQUEST-05 ("Party can browse closed campaign quests in the Quest Log immediately after closing — no next-day wait like one-shot finalization"). Since `BoardType` is immutable per group and a quest can only be OneShot or Campaign, `IsClosed` will never be true for a genuine one-shot quest (nothing in the one-shot flow ever sets it), so the OR is safe without an explicit `BoardType` check — but adding one for defensive clarity is reasonable.
**Warning signs:** Closed campaign quests never appear in the Quest Log at all (over-restrictive OR/AND mixup), or they appear immediately but ALSO one-shot quests lose their next-day wait (under-restrictive change to the wrong branch).

### Pitfall 4: `DungeonMasterSession` and `TotalPlayerCount` still have non-nullable/default-value constraints on `QuestEntity`
**What goes wrong:** `QuestEntity.ChallengeRating` is `[Required] public int ChallengeRating { get; set; } = 1;` and `TotalPlayerCount`/`DungeonMasterSession` have no `[Required]` but do have implicit defaults. D-09/D-10/D-11 drop these fields from the campaign **Create form**, but the entity-level columns are NOT nullable and cannot be dropped from the schema (one-shot quests still need them) — the fields must still be populated with sensible defaults server-side when a campaign quest is created (e.g. `ChallengeRating = 1`, `TotalPlayerCount = 0`, `DungeonMasterSession = false`), not left at whatever the un-bound `QuestViewModel` default produces.
**Why it happens:** Dropping a field from a Create *form* does not automatically mean the corresponding ViewModel property stops being sent/bound — if the Razor form input is removed but the ASP.NET Core model binder still expects the property, it silently binds to the ViewModel's C# default (`ChallengeRating = 1` per the existing `[Range(1,20)]`/default initializer, `TotalPlayerCount = 6` per its default initializer) rather than throwing. That is actually the *safe* outcome here (no validation error), but the planner must confirm this default-value behavior is what's wanted (e.g. TotalPlayerCount defaulting to 6 for a campaign quest that will never display it is harmless but should be verified as intentional, not accidental).
**How to avoid:** Explicitly set safe defaults for `ChallengeRating`/`TotalPlayerCount`/`DungeonMasterSession` in the `Create` POST action when `BoardType == Campaign`, before mapping, rather than relying on whatever the ViewModel's field-initializer default happens to be. Document the chosen default values in the plan.
**Warning signs:** Campaign quest detail/manage pages inadvertently render a CR badge showing "CR 1" somewhere not yet cleaned up by the D-04 removal work, revealing that removal was view-only and the underlying data is stale/misleading if ever exposed via a future feature.

### Pitfall 5: EF Core migration must not silently break the existing `IX_Quests_IsFinalized_FinalizedDate` index or one-shot behavior
**What goes wrong:** A new migration adding `IsClosed`/`ClosedDate` columns is low-risk in isolation, but if the planner also adds a **new** index (e.g. `IX_Quests_IsClosed_ClosedDate`, mirroring the existing `AddQuestFinalizedDateIndex` migration precedent for query performance on `GetQuestsWithSignupsForRoleAsync`), it must use `AddColumn`+`CreateIndex` as two independent migration operations exactly like the two most recent precedent migrations (`AddBoardTypeToGroup`, `AddQuestFinalizedDateIndex`), not combined in a way that risks a partial-apply state if the migration fails mid-way in production (Down() must cleanly reverse both).
**Why it happens:** Combining a column add and an index create in one migration is normal EF Core practice, but the `Down()` method must drop the index before dropping the column (reverse order), or SQL Server will reject the column drop (index still references it).
**How to avoid:** Follow the two existing migrations as templates exactly — `AddQuestFinalizedDateIndex.cs`'s pattern for the index; `AddBoardTypeToGroup.cs`'s pattern for the column add with `defaultValue`. If both are needed in one migration, order `Up()` as AddColumn → CreateIndex, and `Down()` as DropIndex → DropColumn.
**Warning signs:** `dotnet ef migrations add` succeeds but `dotnet ef migrations remove` (or a manual `Down()` review) fails; startup `context.Database.Migrate()` throws in a fresh environment.

## Code Examples

### Existing Finalize/Open action pair (verified in codebase, the direct precedent)
```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:609-666
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Open(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || !quest.IsFinalized) return NotFound();

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) return Challenge();

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();

    await questService.OpenQuestAsync(id);
    return RedirectToAction("Manage", new { id });
}
```

### Existing repository Open pattern (the direct precedent for Close/Reopen)
```csharp
// Source: QuestBoard.Repository/QuestRepository.cs:140-157
public async Task OpenQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .Include(q => q.PlayerSignups)
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsFinalized = false;
    entity.FinalizedDate = null;

    foreach (var playerSignup in entity.PlayerSignups)
    {
        playerSignup.IsSelected = false;
    }

    await DbContext.SaveChangesAsync(token);
}
```

### Existing migration precedent for additive int/bool columns with default
```csharp
// Source: QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "BoardType",
        table: "Groups",
        type: "int",
        nullable: false,
        defaultValue: 0);
}
```

### Existing index precedent for a status+date pair
```csharp
// Source: QuestBoard.Repository/Migrations/20260702081517_AddQuestFinalizedDateIndex.cs
migrationBuilder.CreateIndex(
    name: "IX_Quests_IsFinalized_FinalizedDate",
    table: "Quests",
    columns: new[] { "IsFinalized", "FinalizedDate" });
```

## State of the Art

Not applicable in the usual "library moved on" sense — this is a from-scratch internal feature with no external ecosystem to track. The only "old vs. new" axis is within this codebase: the one-shot lifecycle (`IsFinalized`/`FinalizedDate`, existing since early phases) is being joined by a second, parallel lifecycle (`IsClosed`/`ClosedDate`, new in this phase) rather than the old one being replaced or generalized.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IGroupService.GetByIdAsync(groupId)` (inherited from `IBaseService<Group>`) is the correct/sufficient way to fetch a group's `BoardType` for `ViewBag` population — no dedicated lightweight "GetBoardTypeAsync" method needed | Architecture Patterns, Pattern 3 | Low — `GetByIdAsync` is a simple, already-used, un-cached single-entity lookup (`BaseRepository.GetByIdAsync`, confirmed present); worst case is one extra trivial query per request, not a correctness issue |
| A2 | No shared "is this quest currently active on the board" helper method should be extracted as part of this phase (Pitfall 1's fix is applied at each of the 3 duplicated sites independently) | Common Pitfalls, Pitfall 1 | Low-Medium — if the planner instead extracts a shared predicate, that's a reasonable/arguably better design, but it's a larger refactor not requested by CQUEST-0x; flagging so the planner makes this an explicit choice rather than an accidental scope expansion |
| A3 | Setting `ChallengeRating = 1`, `TotalPlayerCount = 0`, `DungeonMasterSession = false` as the campaign-quest defaults (Pitfall 4) is acceptable — no specific default values were discussed in CONTEXT.md | Common Pitfalls, Pitfall 4 | Low — these fields are confirmed unused/hidden everywhere in campaign UI per D-04/D-05/D-09/D-10/D-11; any sane default is functionally invisible, but the planner/user should confirm rather than have the executor invent values silently |

**If this table is empty:** N/A — see entries above. All three are low-risk implementation-detail assumptions, not requirements-level assumptions; nothing here needs to block planning, but A2 in particular should be an explicit planner decision (extract helper vs. duplicate the check 3x) rather than silently defaulting to duplication.

## Open Questions

1. **Exact migration name/timestamp for the new `IsClosed`/`ClosedDate` columns**
   - What we know: Precedent naming is `Add{Feature}To{Table}` (`AddBoardTypeToGroup`) or `Add{Feature}` (`AddQuestFinalizedDateIndex`); timestamp must be later than `20260703113120` (the most recent migration).
   - What's unclear: Whether to combine the column-add and a potential new index into one migration or two — Pitfall 5 covers the mechanics either way.
   - Recommendation: Single migration, e.g. `AddQuestCloseFields`, adding both `IsClosed` (bool, default false) and `ClosedDate` (nullable datetime, default null) to `Quests`, optionally with a companion index `IX_Quests_IsClosed_ClosedDate` if the planner determines query performance on the board-list filter warrants it (the existing `AddQuestFinalizedDateIndex` migration's own rationale — check its PATTERNS.md/commit message if available for why it was added — should inform whether a matching index is needed here now vs. deferred until it's actually slow).

2. **Whether `ChallengeRating`/`TotalPlayerCount`/`DungeonMasterSession` should remain bindable-but-hidden on `QuestViewModel` for campaign Create, or be actively stripped/defaulted server-side**
   - What we know: D-09/D-10/D-11 require these fields absent from the campaign Create *form* (view-level). CONTEXT.md doesn't specify whether a malicious/tampered POST including these fields for a campaign quest should be silently ignored (defaulted server-side, ignoring whatever was posted) or rejected.
   - What's unclear: No explicit "tamper handling" decision was made for Create the way Phase 35's D-06 explicitly decided this for group Edit tampering.
   - Recommendation: Apply the same philosophy as Phase 35 D-06 (silent server-side override, no error) for consistency — if `BoardType == Campaign`, the controller sets `viewModel.ChallengeRating`/`TotalPlayerCount`/`DungeonMasterSession` to fixed defaults immediately after determining BoardType and before `ModelState.IsValid` is checked, regardless of what was posted. This is a natural extension of Pitfall 4's fix and needs no new user decision, but the planner should state this explicitly as a task rather than leave it implicit.

## Environment Availability

Skipped — this phase has no new external dependencies. It uses only the already-installed .NET 10 SDK, EF Core, and SQL Server (confirmed running per repo's established local dev setup) — no new tools, services, or runtimes are introduced.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 (unit + integration), FluentAssertions 8.10.0, NSubstitute 5.3.0 (mocking) `[VERIFIED: local .csproj read]` |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests` |
| Full suite command | `dotnet test` (runs both Unit and Integration test projects) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CQUEST-01 | Campaign quest Create succeeds with no proposed dates | integration | `dotnet test --filter "FullyQualifiedName~QuestControllerIntegrationTests_Comprehensive"` (extend existing file) | ✅ existing file, ❌ new test case — Wave 0 |
| CQUEST-02 | Campaign quest Details/Manage show no signup/date-voting section | unit/integration | New Razor-rendering assertion or controller ViewBag/model assertion | ❌ Wave 0 |
| CQUEST-03 | Close hides quest from active board immediately | unit | `QuestServiceTests` — new `CloseQuestAsync_...` tests, mirroring `FinalizeQuestAsync_...` style at lines 52-93 | ❌ Wave 0 (extend `QuestServiceTests.cs`) |
| CQUEST-04 | Reopen restores quest to active board | unit | Same file, `ReopenQuestAsync_...` tests | ❌ Wave 0 |
| CQUEST-05 | Closed campaign quest appears in Quest Log immediately (no next-day wait) | unit | `QuestServiceTests` — new test on `GetCompletedQuestsAsync` covering the new OR-branch | ❌ Wave 0 |
| CQUEST-06 | No email sent for post/close/reopen in campaign group | unit | Assert `IQuestEmailDispatcher` (NSubstitute mock) receives **zero** calls after `CloseQuestAsync`/`ReopenQuestAsync`/`AddAsync` for a campaign quest — mirrors existing `FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails` pattern at line 56 | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"`
- **Per wave merge:** `dotnet test` (full suite — unit + integration, ~191+ tests per PROJECT.md's "191 tests green" note)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — extend with `CloseQuestAsync`/`ReopenQuestAsync`/updated `GetCompletedQuestsAsync` test cases (file exists, needs new `[Fact]`/`[Theory]` methods)
- [ ] `QuestBoard.IntegrationTests/Controllers/` — new or extended test file for `Close`/`Reopen` action authorization + happy-path (mirror `QuestFinalizeTests.cs` structure, including whether a body-length regression guard is warranted for the new actions per this research's Pattern 2 note)
- [ ] `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestQuestAsync` needs new optional parameters (`isClosed`, `closedDate`, `boardType`/`groupId` pointing at a Campaign-type test group) to support seeding campaign-quest test fixtures
- [ ] A test `GroupEntity` seed with `BoardType = Campaign` needs to exist somewhere in the integration test fixture setup (check `GroupManagementIntegrationTests.cs`/`TenantIsolationTests.cs` for existing group-seeding helpers to extend, since none currently seed a Campaign-type group)
- No new framework install needed — xunit.v3/FluentAssertions/NSubstitute already present and used identically for this kind of service-method + controller-action testing throughout the codebase.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Not touched by this phase — reuses existing ASP.NET Core Identity auth unchanged |
| V3 Session Management | no | Not touched — `IActiveGroupContext`/`GroupSessionMiddleware` unchanged by this phase |
| V4 Access Control | yes | `[Authorize(Policy = "DungeonMasterOnly")]` + `IsQuestOwner(currentUser, quest.DungeonMaster) \|\| role == GroupRole.Admin` — identical pattern to existing `Finalize`/`Open`, confirmed correct precedent (Phase 34.3 fixed a `User.Name`-based ownership bug; `Close`/`Reopen` must use the already-fixed `User.Id`-based `IsQuestOwner` helper, not reintroduce name-based comparison) |
| V5 Input Validation | yes | `[ValidateAntiForgeryToken]` on all new POST actions (existing convention, non-negotiable per every other mutating action in this controller); server-side `BoardType` re-validation before trusting any Create-form field relaxation (never trust client-submitted `BoardType` — always read it from the active group server-side, per Pattern 3) |
| V6 Cryptography | no | Not applicable — no new secrets, tokens, or crypto operations |

### Known Threat Patterns for ASP.NET Core MVC + EF Core

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Tampered `BoardType` submitted on Create POST to bypass campaign field-stripping (e.g. a Campaign-group DM POSTs `ChallengeRating`/`ProposedDates` anyway) | Tampering | Never bind `BoardType` from the posted `QuestViewModel` — always resolve it server-side from `activeGroupContext.RequireActiveGroupId()` → `IGroupService.GetByIdAsync`, exactly as Phase 35's D-06 already established for `GroupEditViewModel.BoardType` (`[BindNever]`-equivalent: don't add a bindable `BoardType` property to `QuestViewModel` at all — compute it, don't accept it) |
| CSRF on new `Close`/`Reopen` POST endpoints | Tampering / Spoofing | `[ValidateAntiForgeryToken]` — mandatory, matches every other mutating action in this controller; do not omit |
| Broken access control — non-owner/non-admin DM closes another DM's campaign quest | Elevation of Privilege | Reuse `IsQuestOwner(currentUser, quest.DungeonMaster) \|\| role == GroupRole.Admin` exactly; do not write a new/parallel ownership check that could diverge from the Phase 34.3-fixed `Id`-based comparison |
| Cross-tenant quest close (DM in Group A closes a quest belonging to Group B by guessing/incrementing the quest id) | Elevation of Privilege / Tampering | Already mitigated structurally by the existing `HasQueryFilter` on `QuestEntity` (`e.GroupId == activeGroupContext.ActiveGroupId`) — `GetQuestWithDetailsAsync(id)` will return `null` for a quest outside the caller's active group, causing the existing `NotFound()` guard to fire before authorization is even checked. No new code needed, but confirm the new `Close`/`Reopen` actions call the same filtered `GetQuestWithDetailsAsync` (not an `IgnoreQueryFilters()` variant) — they should, by mirroring `Open`/`Finalize` exactly. |

## Sources

### Primary (HIGH confidence)
- Direct codebase reads (`[VERIFIED: local file read]` for all code claims): `QuestBoard.Repository/Entities/QuestEntity.cs`, `QuestBoard.Repository/Entities/GroupEntity.cs`, `QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.Domain/Services/QuestService.cs`, `QuestBoard.Domain/Interfaces/{IQuestService,IQuestRepository,IGroupService,IActiveGroupContext}.cs`, `QuestBoard.Domain/Models/{Group,QuestBoard/Quest}.cs`, `QuestBoard.Domain/Enums/BoardType.cs`, `QuestBoard.Domain/Extensions/ActiveGroupContextExtensions.cs`, `QuestBoard.Service/Controllers/QuestBoard/{QuestController,QuestLogController}.cs`, `QuestBoard.Service/ViewModels/QuestViewModels/{QuestViewModel,EditQuestViewModel}.cs`, `QuestBoard.Service/Jobs/DailyReminderJob.cs`, `QuestBoard.Service/Services/{HangfireQuestEmailDispatcher,NullQuestEmailDispatcher}.cs`, `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs`, `QuestBoard.Repository/Automapper/EntityProfile.cs`, `QuestBoard.Service/Automapper/ViewModelProfile.cs`, `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`, `QuestBoard.Repository/Migrations/{20260703113120_AddBoardTypeToGroup,20260702081517_AddQuestFinalizedDateIndex}.cs`, `QuestBoard.Service/Views/Quest/{Index,Index.Mobile,Manage,Manage.Mobile,Create,Details}.cshtml`, `QuestBoard.Service/Views/QuestLog/Index.cshtml`, `QuestBoard.Service/wwwroot/css/quests.css`, `QuestBoard.UnitTests/Services/QuestServiceTests.cs`, `QuestBoard.IntegrationTests/Controllers/QuestFinalizeTests.cs`, `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`, `QuestBoard.Repository/BaseRepository.cs`, `.csproj` files for test-framework versions.
- Prior-phase and project planning docs (`[CITED: project docs]`): `.planning/phases/36-campaign-quest-posting-closing/36-CONTEXT.md`, `.planning/phases/36-campaign-quest-posting-closing/36-UI-SPEC.md`, `.planning/phases/35-board-type-configuration/35-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/PROJECT.md`, `.planning/config.json`, `CLAUDE.md`.

### Secondary (MEDIUM confidence)
None used — this phase required no external framework/library research; all findings are grounded in direct codebase inspection.

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all findings are direct reads of already-pinned project dependencies
- Architecture: HIGH — every pattern cited is copied from an existing, working precedent in the same codebase (Finalize/Open, BoardType migration, index migration)
- Pitfalls: HIGH — all five pitfalls are grounded in specific line-level code reads (not speculative), cross-referenced against the exact requirement (CQUEST-0x) each would violate

**Research date:** 2026-07-03
**Valid until:** No expiry driver — this is internal codebase research, not tracking an external library's release cadence. Valid until the underlying files change (i.e., effectively until this phase is executed).
