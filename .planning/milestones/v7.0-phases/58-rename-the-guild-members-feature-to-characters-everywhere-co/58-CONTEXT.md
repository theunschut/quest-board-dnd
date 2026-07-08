# Phase 58: Rename the Guild Members feature to Characters everywhere - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Rename the "Guild Members" character-roster feature to "Characters" throughout the Service project — controller, route, views, ViewModels, CSS, integration tests, and all user-facing copy/nav text — so the terminology matches what the Domain/Repository layers already call it (`CharacterEntity`, `CharacterService`, `ICharacterRepository` are all already "Character"-named; only the Service-project presentation layer still says "Guild Members"). Zero behavior change — this is a naming/terminology phase, not a feature change.

Folded in during discussion: a related naming collision on an unrelated feature (`PlayersController`'s misnamed `GuildMembersIndexViewModel`) gets fixed in the same phase since it's a stray "GuildMembers"-named file this rename would otherwise leave behind.

Not in scope: no other D&D-flavored terminology (Quest, DM, Session, etc.) is touched — this phase is scoped strictly to the "Guild Members" → "Characters" rename per the ROADMAP phase title.

</domain>

<decisions>
## Implementation Decisions

### Rename depth
- **D-01:** Full structural rename, not just UI copy. Rename:
  - `GuildMembersController.cs` → `CharactersController.cs` (stays in `Controllers/Characters/` folder, which is already correctly named) — default MVC route changes from `/GuildMembers/*` to `/Characters/*`
  - `Views/GuildMembers/` folder → `Views/Characters/` (all 8 files: `Index`/`Details`/`Edit`/`Create` × desktop/mobile) — required for MVC's default view-location convention to keep resolving views once the controller is renamed
  - `ViewModels/GuildMembersViewModels/` folder — merge its actually-in-use content into/alongside the already-correctly-named `ViewModels/CharacterViewModels/` (which already holds `CharacterViewModel.cs` and `CharactersIndexViewModel.cs`, both used by this controller today)
  - CSS files `guild-members.css` / `guild-members.mobile.css` → `characters.css` / `characters.mobile.css`, including their `<link>` references in `Views/Shared/_Layout.cshtml`, `Views/Characters/Index.Mobile.cshtml`, and any other `@section Styles` references
  - CSS class names inside those files (`.guild-members-page`, `.guild-section-card`, `.guild-section-heading`, `.guild-member-row`, `.guild-member-thumbnail`, `.guild-member-placeholder`, `.guild-member-name`, `.guild-member-class`, `.guild-member-owner`, `.guild-empty-state`, `.guild-roster` etc.) → `character-*` equivalents, and all class references in the renamed view files updated to match
  - Integration test file `GuildMembersControllerIntegrationTests.cs` → `CharactersControllerIntegrationTests.cs` (class name too)
  - All user-facing copy: nav link text "Guild Members" → "Characters" (`_Layout.cshtml:131-133`, `_Layout.Mobile.cshtml:113-115`), page titles/headings ("Guild Members" → "Characters", "Guild Roster" → keep or rename per Claude's discretion — see below), "Back to Guild Members" links, empty-state copy ("join the guild" / "the guild awaits other adventurers" — reword to drop "guild" framing)
  - Global `@using QuestBoard.Service.ViewModels.GuildMembersViewModels` in `Views/_ViewImports.cshtml:9` updated to the new namespace
  - Every `@Url.Action("GetProfilePicture", "GuildMembers", ...)` cross-reference updated to `"Characters"` — found in `Views/QuestLog/Details.cshtml:73`, `Views/QuestLog/Details.Mobile.cshtml:69`, `Views/Quest/Details.cshtml:111,227`, `Views/Quest/Manage.cshtml:410`
  - Rationale: finishes the job the Domain layer already did (Entity/Service/Repository are all "Character"-named since inception); leaves no split naming for a future reader to trip over.

### Players/DM roster naming collision (folded in)
- **D-02:** `ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` is NOT used by the character roster controller — it's used by the unrelated `PlayersController.Index` (lists `DungeonMasters` + `Players`, i.e. users — a completely different "Players" nav page, model shape `{ IEnumerable<User> DungeonMasters, IEnumerable<User> Players }`). Rename it too: move out of the `GuildMembersViewModels` folder into a `ViewModels/PlayersViewModels/` folder (matching the codebase's `ViewModels/[Feature]ViewModels/` convention, since it's the `Players` feature) and rename the class to something like `PlayersIndexViewModel`. Also fix its "guild registry" UI copy in `Views/Players/Index.cshtml:45` and `Views/Players/Index.Mobile.cshtml:35` (currently: "The guild registry is currently empty. Brave souls may register as new quest leaders.") — reword to drop the "guild registry" framing. Exact new class name and copy wording are Claude's discretion.
  - Rationale: this file was never really part of the Guild Members/Characters feature — it's a stray "GuildMembers"-named artifact for an unrelated page. Leaving it after this rename would mean a `GuildMembersIndexViewModel` still exists in the codebase, which defeats the purpose of the rename and would confuse the next person who greps for "GuildMembers" expecting zero hits.
  - Users already see "Guild Members" and "Players" as two distinct nav items today (`_Layout.cshtml:131` vs `:138`) — this is purely an internal/code-level naming fix, not a user-facing behavior or copy change to the Players page's actual content (only the "guild registry" phrase in its empty-state copy).

### Old URL / route compatibility
- **D-03:** Clean break — no redirect or route alias from old `/GuildMembers/*` paths to the new `/Characters/*` paths. This is a ~17-member trusted-group app where navigation is primarily nav-bar-driven, not deep-bookmark-driven; a stale bookmark to `/GuildMembers/Details/5` will 404 and the user re-navigates via the (already-renamed) nav link. No `[Route("GuildMembers/{action}")]` alias, no redirect middleware.

### Claude's Discretion
- Exact new class name for `GuildMembersIndexViewModel` → e.g. `PlayersIndexViewModel` (D-02) — any clear, convention-following name works.
- Exact reworded copy for "guild registry" (Players page empty state) and "the guild awaits other adventurers" / "join the guild" (Characters page empty states) — should read naturally without "guild" framing, matching this app's existing tone elsewhere.
- Whether "Guild Roster" (the second section heading on the Characters Index page, listing OTHER players' characters vs. "My Characters") gets renamed to something like "Character Roster" or a different label entirely — keep the two-section distinction (own characters vs. everyone else's) clear either way.
- Exact new CSS class names (`character-member-row` vs `character-row` vs other reasonable naming) — just needs to be internally consistent across the renamed CSS files and their view references.
- Whether the `ViewModels/GuildMembersViewModels/` folder is deleted entirely after D-01/D-02 move its contents out (it should end up empty and removable).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

No external ADRs/specs — this is an ad-hoc backlog phase (no REQUIREMENTS.md mapping), same pattern as Phases 47-51, 55-56. All scope is captured in the decisions above and the file-level pointers below.

### Files to rename (D-01 — character roster feature)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` → `CharactersController.cs`
- `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` → `CharactersControllerIntegrationTests.cs`
- `QuestBoard.Service/Views/GuildMembers/` (8 files: `Index.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml`) → `Views/Characters/`
- `QuestBoard.Service/wwwroot/css/guild-members.css` → `characters.css`
- `QuestBoard.Service/wwwroot/css/guild-members.mobile.css` → `characters.mobile.css`
- `QuestBoard.Service/ViewModels/GuildMembersViewModels/` — content already used by the controller (none directly — see D-02, the only file in this folder is the one being moved to Players)

### Already correctly named — do NOT rename (already "Character")
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharactersIndexViewModel.cs` (this is what `GuildMembersController`/`CharactersController` actually uses today for its Index action — confirmed via `GuildMembersController.cs:31`)
- `QuestBoard.Domain/Models/Character.cs`, `ICharacterService`, `CharacterService.cs`, `ICharacterRepository`, `CharacterRepository.cs`
- `QuestBoard.Repository/Entities/CharacterEntity.cs`, `CharacterImageEntity.cs`, `CharacterClassEntity.cs`

### Cross-references to update (Url.Action controller-name strings)
- `QuestBoard.Service/Views/QuestLog/Details.cshtml:73` — `@Url.Action("GetProfilePicture", "GuildMembers", ...)`
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml:69` — same
- `QuestBoard.Service/Views/Quest/Details.cshtml:111` and `:227` — same
- `QuestBoard.Service/Views/Quest/Manage.cshtml:410` — same

### Nav links
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:131-133` — `asp-controller="GuildMembers"` + "Guild Members" text
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:113-115` — same
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:19` — `<link rel="stylesheet" href="~/css/guild-members.css" .../>`

### Global using
- `QuestBoard.Service/Views/_ViewImports.cshtml:9` — `@using QuestBoard.Service.ViewModels.GuildMembersViewModels`

### Players/DM roster collision (D-02)
- `QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs` — constructs `GuildMembersIndexViewModel` (line 14), needs updated type reference after rename
- `QuestBoard.Service/ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` — the file to rename/relocate; shape is `{ IEnumerable<User> DungeonMasters, IEnumerable<User> Players }`, has nothing to do with `CharacterEntity`
- `QuestBoard.Service/Views/Players/Index.cshtml:2-3,45` — `@using ...GuildMembersViewModels`, `@model GuildMembersIndexViewModel`, "guild registry" copy
- `QuestBoard.Service/Views/Players/Index.Mobile.cshtml:2-3,35` — same

### Future phase awareness (informational, not actionable now)
- `.planning/REQUIREMENTS.md` line 30 — `IMAGE-04`: "Guild-member list page displays the cropped image..." — this requirement belongs to the not-yet-started Phase 46 (Client-Side Crop UI). Once this rename phase ships, that requirement text refers to the (renamed) Characters list page. No action needed now — just don't be confused by the stale wording if/when Phase 46 research reads REQUIREMENTS.md.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None new — this phase renames existing files/classes in place; no new components needed.

### Established Patterns
- `ViewModels/[Feature]ViewModels/` folder-per-feature convention (per `.planning/codebase/CONVENTIONS.md`) — D-02's new `PlayersViewModels/` folder follows this exactly.
- MVC default view-location convention (`Views/{Controller}/{Action}.cshtml`) — the reason `Views/GuildMembers/` MUST become `Views/Characters/` once the controller class is renamed; there is no explicit `[Route]`/view-name override in `GuildMembersController.cs` to preserve the old folder name.

### Integration Points
- `Program.cs` has no explicit route registration referencing "GuildMembers" (confirmed via grep) — default attribute/conventional MVC routing is all that's in play, so renaming the controller class is sufficient to move the route; no `Program.cs` changes needed.
- CSS class usage for the `guild-*` names is fully self-contained within `guild-members.css`/`guild-members.mobile.css` and the `Views/GuildMembers/*.cshtml` files that reference those classes (confirmed via grep — no other CSS file references `guild-` classes) — the rename is a closed, traceable set of files.

</code_context>

<specifics>
## Specific Ideas

User's own words: "In addition to this phase, I want to rename all related to 'Guild Members' to Characters. This should be more universal for all tenants" (said in the same message that requested the NPC directory feature — Phase 57 — which explicitly referred to "the guild members (characters)").

Discussion surfaced a scouting find the user hadn't mentioned: the `PlayersController`/`GuildMembersIndexViewModel` naming collision. User confirmed folding the fix into this phase rather than treating it as a separate, unrelated concern.

</specifics>

<deferred>
## Deferred Ideas

- Renaming other D&D-flavored terminology (Quest, DM, Session, Guild in other contexts) — explicitly out of scope; this phase is the "Guild Members" → "Characters" rename only, per the ROADMAP phase title.
- A redirect/alias for old `/GuildMembers` URLs — explicitly decided against (D-03, clean break).

</deferred>

---

*Phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co*
*Context gathered: 2026-07-06*
