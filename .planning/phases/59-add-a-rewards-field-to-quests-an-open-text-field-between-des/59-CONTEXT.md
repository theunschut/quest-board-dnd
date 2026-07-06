# Phase 59: Add a rewards field to quests - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a `Rewards` field to Quest — an optional, unbounded freeform text field. It sits between Description and Challenge Rating on the Quest Create/Edit forms (desktop + mobile), and renders as its own boxed callout below the Description on the Quest Details page (desktop + mobile). Also extends to the completed-quest QuestLog/Details page (read-only) and the separate Follow-Up Quest creation form (blank, not pre-filled). Does not touch the quest board list card.

</domain>

<decisions>
## Implementation Decisions

### Required vs. optional, and board-type scope
- **D-01:** `Rewards` is optional — nullable string, no `[Required]` attribute — mirroring `Recap`'s pattern rather than `Description`'s required one. A DM can create/save a quest without specifying rewards.
- **D-02:** Shows for **both** board types (OneShot and Campaign), not gated by the `@if (boardType != BoardType.Campaign)` conditional that hides Challenge Rating/Total Player Count/Dungeon Master Session/Proposed Dates. In the markup, place the Rewards field immediately after Description and before that conditional block — this naturally satisfies "between Description and Challenge Rating" for OneShot boards, while Campaign boards just get Description → Rewards with no CR to sandwich against. **Do not** add Rewards to the Campaign sanitization block in `QuestController.Create`/`Edit` (the `if (boardType == BoardType.Campaign) { viewModel.ChallengeRating = 1; ... }` reset logic) — Rewards must survive untouched for Campaign quests.
- **D-03:** No character limit — unbounded text, matching `Description`'s own convention (no `[StringLength]` anywhere in the ViewModel/domain/entity stack for Description; Rewards should follow the same shape).

### Where it appears
- **D-04:** Add the Rewards field/block to:
  - `Quest/Create.cshtml` + `Create.Mobile.cshtml` — form field, between Description and the CR conditional block
  - `Quest/Edit.cshtml` + `Edit.Mobile.cshtml` — same position
  - `Quest/Details.cshtml` + `Details.Mobile.cshtml` — new boxed block below Description
  - `QuestLog/Details.cshtml` (completed-quest recap page) — read-only, same box treatment, placed near the existing "Original Quest Description" box
  - `CreateFollowUp.cshtml` (Follow-Up Quest form, `FollowUpQuestViewModel`) — new field added in the same Title→Description→CR order, but **left blank** (not pre-filled from the original quest) — unlike Title/Description/ChallengeRating/TotalPlayerCount, which the `CreateFollowUp` GET action does copy forward. Rationale: a follow-up ("Part 2") session's reward is a new thing to decide, not a repeat of the last one.
- **D-05:** Does **not** appear on `_QuestCard.cshtml` (the quest board list card, `Quest/Index.cshtml`) — no board-list indicator, icon, or snippet. Considered and explicitly declined during discussion, not deferred to a future phase.

### Details-page block style
- **D-06:** Reuse the exact existing boxed-callout CSS pattern already established for `.quest-description-box`/`.recap-display-box` (`QuestBoard.Service/wwwroot/css/quests.css:813-834` — `rgba(0,0,0,0.4)` background, 1px gold-tinted border, 8px border-radius, `white-space: pre-wrap`) rather than inventing a new visual treatment. Heading: **"Rewards"** with a `fa-coins` icon, matching the app's existing gold/warning accent color used elsewhere (CR badge, DM crown icon).

### Empty state
- **D-07:** When Rewards is null/empty/whitespace, **hide the block entirely** — no "no rewards" placeholder message — on both `Quest/Details.cshtml` and `QuestLog/Details.cshtml`. Mirrors the existing mobile Description-block convention in `Quest/Details.Mobile.cshtml:97` (`@if (!string.IsNullOrWhiteSpace(Model.Quest?.Description))`).

### Claude's Discretion
- Exact placeholder copy for the Rewards textarea (e.g. "Describe the loot, gold, or other rewards for this quest...") — should read naturally, consistent with Description's existing placeholder tone.
- Exact CSS class name for the new box on `QuestLog/Details.cshtml` (reuse `.quest-description-box` directly vs. a new same-shaped class) — planner's call, just needs the same visual output as D-06.
- Exact placement order of the new Rewards box on `QuestLog/Details.cshtml` relative to the existing "Original Quest Description" / "Adventurers" / "Session Recap" sections — reasonable default is directly after the Description box, before Adventurers.
- Whether the new `Rewards` column is added via its own EF Core migration or bundled — should be its own dedicated migration, nullable string, same shape as `Recap`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

No external ADRs/specs — this is an ad-hoc backlog phase (no REQUIREMENTS.md mapping), same pattern as Phases 47-58. All scope is captured in the decisions above and the file-level pointers below.

### Model stack (add `Rewards` property, same name, all three layers)
- `QuestBoard.Repository/Entities/QuestEntity.cs` — add `public string? Rewards { get; set; }` (mirrors `Recap`'s exact shape: nullable, no attributes)
- `QuestBoard.Domain/Models/QuestBoard/Quest.cs` — add matching `Rewards` property
- `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` — add matching `Rewards` property (no `[Required]`, no `[StringLength]`, per D-01/D-03)
- `QuestBoard.Service/ViewModels/QuestViewModels/FollowUpQuestViewModel.cs` — add matching `Rewards` property (per D-04)

### AutoMapper (convention-based — confirmed no `ForMember` needed)
- `QuestBoard.Repository/Automapper/EntityProfile.cs` — `CreateMap<QuestEntity, Quest>()` / `CreateMap<Quest, QuestEntity>()` (lines ~18-30) map same-named properties by convention; `Rewards` will auto-map once added to both sides, same as `Description`/`ChallengeRating` today.
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` — `CreateMap<QuestViewModel, Quest>()` / `CreateMap<Quest, QuestViewModel>()` (lines ~16-29) — same convention-based behavior confirmed.

### Views — form fields (Create/Edit, desktop + mobile)
- `QuestBoard.Service/Views/Quest/Create.cshtml:28-32` (Description field) — insert Rewards field immediately after, before the `@if (boardType != BoardType.Campaign)` block at line 34
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml:35-42` — same position
- `QuestBoard.Service/Views/Quest/Edit.cshtml:30-34` (`Quest.Description`) — same treatment, `asp-for="Quest.Rewards"`
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml:44-48` — same
- `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml:34-38` (Description field) — insert Rewards field after, left blank per D-04 (do NOT add to the GET action's pre-fill object at `QuestController.cs:906-917`)

### Views — display blocks (Details, desktop + mobile)
- `QuestBoard.Service/Views/Quest/Details.cshtml:31` — `<p class="lead" style="white-space: pre-wrap;">@Model.Quest?.Description</p>` — insert new boxed Rewards block immediately after, per D-06/D-07
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:97-103` — existing `@if (!string.IsNullOrWhiteSpace(Model.Quest?.Description))` conditional-render pattern for `.quest-description-mobile` — mirror this exact pattern for Rewards (own conditional, own class)
- `QuestBoard.Service/Views/QuestLog/Details.cshtml:44-50` — existing "Original Quest Description" box (`.quest-description-box`) — insert a same-styled Rewards box nearby, read-only, per D-04/D-06/D-07

### CSS — box pattern to mirror
- `QuestBoard.Service/wwwroot/css/quests.css:813-834` — `.quest-description-box` / `.recap-display-box` (identical rule sets) — the exact visual pattern D-06 locks in for the new Rewards box

### Controller / service — explicit-parameter methods (NOT pure AutoMapper passthroughs)
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:253-263` — `Edit` POST calls `questService.UpdateQuestPropertiesWithNotificationsAsync(id, viewModel.Quest.Title, viewModel.Quest.Description, viewModel.Quest.ChallengeRating, ...)` with explicit positional arguments — needs a new `Rewards` parameter threaded through this call site
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:984-992` — `CreateFollowUp` POST calls `questService.CreateFollowUpQuestWithDetailsAsync(id, viewModel.Title, viewModel.Description, viewModel.ChallengeRating, ...)` — same, needs a new parameter (passed as `viewModel.Rewards`, which per D-04 the DM fills in on this form, blank by default)
- `QuestBoard.Domain/Services/QuestService.cs` + `QuestBoard.Domain/Interfaces/IQuestService.cs` — `UpdateQuestPropertiesWithNotificationsAsync` and `CreateFollowUpQuestWithDetailsAsync` signatures both need the new `Rewards` parameter added
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:84-136` (`Create` POST) and `:245-251` (`Edit`'s Campaign-sanitization block) — confirm Rewards is NOT added to the Campaign-quest field-reset logic (per D-02)

### Migration
- New EF Core migration required: nullable `Rewards` string column on the `Quests` table (same shape as the existing `Recap` column — see `QuestBoard.Repository/Migrations/20260127153158_AddRecapToQuest.Designer.cs` for the precedent to mirror)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `.quest-description-box` / `.recap-display-box` CSS classes (`quests.css:813-834`) — reuse directly for the new Rewards box (D-06)
- `Quest/Details.Mobile.cshtml`'s `@if (!string.IsNullOrWhiteSpace(...))` conditional-render pattern around `.quest-description-mobile` — mirror for Rewards' hide-when-empty behavior (D-07)

### Established Patterns
- AutoMapper convention-based mapping — confirmed both `EntityProfile` (Entity↔Domain) and `ViewModelProfile` (Domain↔ViewModel) map `Description`/`ChallengeRating` with zero `ForMember` calls; a same-named `Rewards` property added to all three model classes will auto-map with no profile changes needed.
- Campaign-quest field-sanitization pattern (`QuestController.Create`/`Edit`, `if (boardType == BoardType.Campaign) { viewModel.ChallengeRating = 1; ... }`) — an allowlist of what gets zeroed out for Campaign quests. Rewards must stay off this list per D-02.
- Explicit-parameter service methods — unlike Create (which does `mapper.Map<Quest>(viewModel)` directly), `Edit` and `CreateFollowUp` route through service methods with individually-named parameters rather than passing the whole ViewModel/domain object. This is the one place a new field requires touching a method signature, not just a model class.

### Integration Points
- 8 view files touched total: `Create.cshtml`/`Create.Mobile.cshtml`, `Edit.cshtml`/`Edit.Mobile.cshtml` (form field); `Details.cshtml`/`Details.Mobile.cshtml` (display block); `QuestLog/Details.cshtml` (display block); `CreateFollowUp.cshtml` (form field)
- One new EF Core migration (Rewards column on Quests table)
- Two service method signatures gain a parameter (`UpdateQuestPropertiesWithNotificationsAsync`, `CreateFollowUpQuestWithDetailsAsync`) plus their `IQuestService` interface declarations

</code_context>

<specifics>
## Specific Ideas

User's own words: "I want to add a rewards section to all quests. This should be an open text field in between the Description and the Challenge Rating on the quest create or edit page. As for the Quest Details page, it should be located in some sort of block, just below the description."

</specifics>

<deferred>
## Deferred Ideas

- A small rewards indicator/icon on the quest board list card (`_QuestCard.cshtml`) — considered and explicitly declined during discussion (D-05), not deferred to a specific future phase, just decided against for now.

### Reviewed Todos (not folded)
None — `gsd_run query todo.match-phase 59` returned zero matches.

</deferred>

---

*Phase: 59-add-a-rewards-field-to-quests-an-open-text-field-between-des*
*Context gathered: 2026-07-06*
