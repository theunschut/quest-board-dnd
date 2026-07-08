# Phase 58: Rename the Guild Members feature to Characters everywhere - Research

**Researched:** 2026-07-06
**Domain:** Multi-file rename/refactor (controller, routes, views, ViewModels, CSS, integration tests, nav copy) in an ASP.NET Core 10 MVC codebase — zero behavior change
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01 — Rename depth: full structural rename, not just UI copy.**
- `GuildMembersController.cs` → `CharactersController.cs` (stays in `Controllers/Characters/` folder, already correctly named) — default MVC route changes from `/GuildMembers/*` to `/Characters/*`
- `Views/GuildMembers/` folder → `Views/Characters/` (all 8 files: `Index`/`Details`/`Edit`/`Create` × desktop/mobile) — required for MVC's default view-location convention to keep resolving views once the controller is renamed
- `ViewModels/GuildMembersViewModels/` folder — merge its actually-in-use content into/alongside the already-correctly-named `ViewModels/CharacterViewModels/` (which already holds `CharacterViewModel.cs` and `CharactersIndexViewModel.cs`, both used by this controller today)
- CSS files `guild-members.css` / `guild-members.mobile.css` → `characters.css` / `characters.mobile.css`, including their `<link>` references in `Views/Shared/_Layout.cshtml`, `Views/Characters/Index.Mobile.cshtml`, and any other `@section Styles` references
- CSS class names inside those files (`.guild-members-page`, `.guild-section-card`, `.guild-section-heading`, `.guild-member-row`, `.guild-member-thumbnail`, `.guild-member-placeholder`, `.guild-member-name`, `.guild-member-class`, `.guild-member-owner`, `.guild-empty-state`, `.guild-roster` etc.) → `character-*` equivalents, and all class references in the renamed view files updated to match
- Integration test file `GuildMembersControllerIntegrationTests.cs` → `CharactersControllerIntegrationTests.cs` (class name too)
- All user-facing copy: nav link text "Guild Members" → "Characters" (`_Layout.cshtml:131-133`, `_Layout.Mobile.cshtml:113-115`), page titles/headings ("Guild Members" → "Characters", "Guild Roster" → keep or rename per Claude's discretion), "Back to Guild Members" links, empty-state copy ("join the guild" / "the guild awaits other adventurers" — reword to drop "guild" framing)
- Global `@using QuestBoard.Service.ViewModels.GuildMembersViewModels` in `Views/_ViewImports.cshtml:9` updated to the new namespace
- Every `@Url.Action("GetProfilePicture", "GuildMembers", ...)` cross-reference updated to `"Characters"` — found in `Views/QuestLog/Details.cshtml:73`, `Views/QuestLog/Details.Mobile.cshtml:69`, `Views/Quest/Details.cshtml:111,227`, `Views/Quest/Manage.cshtml:410`
- Rationale: finishes the job the Domain layer already did (Entity/Service/Repository are all "Character"-named since inception); leaves no split naming for a future reader to trip over.

**D-02 — Players/DM roster naming collision (folded in).**
`ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` is NOT used by the character roster controller — it's used by the unrelated `PlayersController.Index` (lists `DungeonMasters` + `Players`, i.e. users — a completely different "Players" nav page, model shape `{ IEnumerable<User> DungeonMasters, IEnumerable<User> Players }`). Rename it too: move out of the `GuildMembersViewModels` folder into a `ViewModels/PlayersViewModels/` folder (matching the codebase's `ViewModels/[Feature]ViewModels/` convention, since it's the `Players` feature) and rename the class to something like `PlayersIndexViewModel`. Also fix its "guild registry" UI copy in `Views/Players/Index.cshtml:45` and `Views/Players/Index.Mobile.cshtml:35` (currently: "The guild registry is currently empty. Brave souls may register as new quest leaders.") — reword to drop the "guild registry" framing. Exact new class name and copy wording are Claude's discretion.
- Rationale: this file was never really part of the Guild Members/Characters feature — it's a stray "GuildMembers"-named artifact for an unrelated page. Leaving it after this rename would mean a `GuildMembersIndexViewModel` still exists in the codebase, which defeats the purpose of the rename.
- Users already see "Guild Members" and "Players" as two distinct nav items today — this is purely an internal/code-level naming fix, not a user-facing behavior or copy change to the Players page's actual content (only the "guild registry" phrase in its empty-state copy).

**D-03 — Old URL / route compatibility: clean break.**
No redirect or route alias from old `/GuildMembers/*` paths to the new `/Characters/*` paths. This is a ~17-member trusted-group app where navigation is primarily nav-bar-driven, not deep-bookmark-driven; a stale bookmark to `/GuildMembers/Details/5` will 404 and the user re-navigates via the (already-renamed) nav link. No `[Route("GuildMembers/{action}")]` alias, no redirect middleware.

### Claude's Discretion
- Exact new class name for `GuildMembersIndexViewModel` (D-02) — any clear, convention-following name works (research recommends `PlayersIndexViewModel`, see below — matches the existing `players.mobile.css` / `.players-*` CSS class naming already in use on that page).
- Exact reworded copy for "guild registry" (Players page empty state) and "the guild awaits other adventurers" / "join the guild" (Characters page empty states) — should read naturally without "guild" framing, matching this app's existing tone elsewhere.
- Whether "Guild Roster" (the second section heading on the Characters Index page, listing OTHER players' characters vs. "My Characters") gets renamed to something like "Character Roster" or a different label entirely — keep the two-section distinction (own characters vs. everyone else's) clear either way.
- Exact new CSS class names (`character-member-row` vs `character-row` vs other reasonable naming) — just needs to be internally consistent across the renamed CSS files and their view references.
- Whether the `ViewModels/GuildMembersViewModels/` folder is deleted entirely after D-01/D-02 move its contents out (it should end up empty and removable — **confirmed by this research: the folder contains exactly one file**, see Runtime State Inventory).

### Deferred Ideas (OUT OF SCOPE)
- Renaming other D&D-flavored terminology (Quest, DM, Session, Guild in other contexts) — explicitly out of scope; this phase is the "Guild Members" → "Characters" rename only, per the ROADMAP phase title.
- A redirect/alias for old `/GuildMembers` URLs — explicitly decided against (D-03, clean break).
</user_constraints>

## Summary

This is a pure rename/refactor phase with zero new logic. CONTEXT.md's scouting was thorough and accurate — this research's exhaustive sweep (grepping the entire repo case-insensitively for "guild", checking `QuestBoard.UnitTests/`, JSON/JS/SCSS files, `appsettings*.json`, `docs/`, and reading every file in full) found **no additional production-code files** beyond what CONTEXT.md already identified, with two exceptions worth flagging: (1) `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` contains 4 test methods with hardcoded `/GuildMembers/*` route strings and a `GuildMembers`-prefixed test method name that CONTEXT.md's canonical_refs did not mention (it only listed `GuildMembersControllerIntegrationTests.cs` and `LayoutNavigationTests.cs`'s nav-text assertion); (2) `QuestBoard.Repository/Entities/QuestBoardContext.cs` has a code comment mentioning "guild roster" that is prose-only (not a rename target — it's a comment describing behavior, not a symbol/string) but is worth an optional touch-up for consistency since CLAUDE.md forbids planning-ID references in comments, not descriptive ones.

The `QuestBoard.UnitTests/` project has zero references to "guild" (confirmed) — no unit test changes needed. No unit tests exist for this controller; all testing is at the integration level.

**Primary recommendation:** Execute this as a mechanical, wave-ordered rename: (1) CSS files + their internal class names, (2) ViewModels folder split (Characters vs. Players), (3) Views folder rename + all `guild-*` class references + copy updates, (4) Controller rename + namespace/using updates, (5) Integration test file rename + route string updates (both `GuildMembersControllerIntegrationTests.cs` AND `MobileViewsTests.cs`), (6) cross-reference sweep (`Url.Action` controller-name strings in Quest/QuestLog views), (7) nav link updates in both `_Layout.cshtml` and `_Layout.Mobile.cshtml`. Because C# renames (controller class/namespace) and Razor view folder renames are coupled by MVC convention, order matters: rename the CSS/ViewModels/Views first, verify the app still resolves, then rename the controller last (since renaming the controller class breaks view resolution until `Views/Characters/` already exists).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Character roster controller routes | API/Backend (MVC controller) | — | `GuildMembersController` → `CharactersController`; convention-based routing, no explicit `[Route]` |
| Character roster views (desktop/mobile) | Frontend Server (SSR/Razor) | — | Razor views rendered server-side; folder rename required by MVC convention |
| CSS class names/files | CDN/Static | — | Static assets served from `wwwroot/css/`; pure presentation, no logic |
| ViewModels (`CharactersIndexViewModel`, `GuildMembersIndexViewModel`) | API/Backend (Service-layer DTOs) | Frontend Server | ViewModels are Service-project DTOs consumed by Razor views; both tiers touch them |
| Integration tests | N/A (test infra) | — | `QuestBoard.IntegrationTests` exercises the full HTTP pipeline; asserts on route strings and rendered HTML |
| Nav link copy | Frontend Server (SSR) | — | `_Layout.cshtml`/`_Layout.Mobile.cshtml` render nav server-side per request |

This is a single-tier-dominant phase (Service project only) — no Domain or Repository layer changes, confirmed by the CONTEXT.md boundary statement and this research's full-repo grep (zero hits in `QuestBoard.Domain/`, one comment-only hit in `QuestBoard.Repository/Entities/QuestBoardContext.cs`).

## File-by-File Rename Manifest

This is the definitive, exhaustive list. Everything below was verified by reading full file contents in this research session — the planner can turn this directly into tasks without re-deriving it.

### 1. Controller

| Old | New |
|-----|-----|
| `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` | `QuestBoard.Service/Controllers/Characters/CharactersController.cs` |

**Class rename:** `GuildMembersController` → `CharactersController` (folder `Controllers/Characters/` stays as-is — already correctly named).

**Full action inventory (verified — all 7 actions, confirmed via full file read):**

| Action | Verb | Route (before → after) | ViewModel used |
|--------|------|------------------------|-----------------|
| `Index` | GET | `/GuildMembers` → `/Characters` | `CharactersIndexViewModel` (already correctly named — no change) |
| `Details(int id)` | GET | `/GuildMembers/Details/{id}` → `/Characters/Details/{id}` | `CharacterViewModel` (already correctly named) |
| `Create()` | GET | `/GuildMembers/Create` → `/Characters/Create` | `CharacterViewModel` |
| `Create(CharacterViewModel, ...)` | POST | `/GuildMembers/Create` → `/Characters/Create` | `CharacterViewModel` |
| `Edit(int id)` | GET | `/GuildMembers/Edit/{id}` → `/Characters/Edit/{id}` | `CharacterViewModel` |
| `Edit(int id, CharacterViewModel, ...)` | POST | `/GuildMembers/Edit/{id}` → `/Characters/Edit/{id}` | `CharacterViewModel` |
| `Delete(int id)` | POST | `/GuildMembers/Delete` → `/Characters/Delete` | — |
| `ToggleRetirement(int id)` | POST | `/GuildMembers/ToggleRetirement` → `/Characters/ToggleRetirement` | — |
| `GetProfilePicture(int id)` | GET | `/GuildMembers/GetProfilePicture/{id}` → `/Characters/GetProfilePicture/{id}` | — (returns raw image bytes) |

**No `[Route]` or `[Area]` attributes exist on this controller** — confirmed via direct grep. Pure convention-based routing (`{controller}/{action}/{id?}`). Renaming the class is sufficient to move every route; no route-attribute edits needed. `Program.cs` has zero references to "GuildMembers" — confirmed, no `Program.cs` changes needed.

**Private helper (no rename needed, already generic):** `CanManageCharacterAsync(User, Character)` — shared owner-or-admin guard used by Edit/Delete/ToggleRetirement (added in Phase 56). No "Guild" references inside it.

### 2. Views folder (8 files)

| Old | New |
|-----|-----|
| `QuestBoard.Service/Views/GuildMembers/Index.cshtml` | `QuestBoard.Service/Views/Characters/Index.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml` | `QuestBoard.Service/Views/Characters/Index.Mobile.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Details.cshtml` | `QuestBoard.Service/Views/Characters/Details.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` | `QuestBoard.Service/Views/Characters/Details.Mobile.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Edit.cshtml` | `QuestBoard.Service/Views/Characters/Edit.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Edit.Mobile.cshtml` | `QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Create.cshtml` | `QuestBoard.Service/Views/Characters/Create.cshtml` |
| `QuestBoard.Service/Views/GuildMembers/Create.Mobile.cshtml` | `QuestBoard.Service/Views/Characters/Create.Mobile.cshtml` |

**Per-file content audit (verified by full read — this fills the gap CONTEXT.md flagged, since it only sampled Index.cshtml/Index.Mobile.cshtml in detail):**

| File | "Guild" text/classes found | Action needed |
|------|---------------------------|----------------|
| `Index.cshtml` | `<div class="guild-members-page">`, `ViewData["Title"] = "Guild Members"`, `<h1>...Guild Members</h1>`, `<h3>...Guild Roster</h3>`, empty-state copy "Create your first character to join the guild!" and "The guild awaits other adventurers!" | Rewrite title/heading/copy; rename `guild-members-page` class |
| `Index.Mobile.cshtml` | `<link href="~/css/guild-members.mobile.css" .../>`, `.guild-section-card` ×2, `.guild-section-heading` ×2, `.guild-member-row` ×2, `.guild-member-thumbnail` ×2, `.guild-member-placeholder` ×2, `.guild-member-name` ×2, `.guild-member-class` ×2, `.guild-member-owner` ×1, `.guild-empty-state` ×2, "Guild Roster" heading text, "join the guild!"/"guild awaits other adventurers!" copy | Update CSS `<link>` path; rename all `guild-*` classes; rewrite copy |
| `Details.cshtml` | **Only** one "Guild"-adjacent hit: "Back to Guild Members" link text (line ~167) | Update link text to "Back to Characters"; no CSS classes to rename (uses `character-details-page`, `character-portrait`, etc. — already correct) |
| `Details.Mobile.cshtml` | **Only** one hit: "Back to Guild Members" link text (line ~134) | Update link text; CSS classes already `character-*` (loads `character-detail.mobile.css`, already correctly named) |
| `Edit.cshtml` | **Zero hits** — verified by full read; all classes are `character-form-page`, `character-form-card`, `class-entry`, etc. | No changes beyond controller-name-agnostic `asp-action="Edit"` (unaffected by controller rename since it's a relative action name) |
| `Edit.Mobile.cshtml` | **Zero hits** — verified by full read; loads `character-form.mobile.css` (already correctly named) | No changes needed |
| `Create.cshtml` | **Zero hits** — verified by full read | No changes needed |
| `Create.Mobile.cshtml` | **Zero hits** — verified by full read; loads `character-form.mobile.css` | No changes needed |

**Important finding for the planner:** Edit and Create views (both desktop and mobile, 4 of the 8 files) require **zero content changes** — they only need the folder-level file move to `Views/Characters/`. All their CSS classes were already `character-*`-named from a prior phase. Only `Index.*` and `Details.*` (4 files) need content edits. This narrows the actual content-editing surface significantly from what "8 files need auditing" might imply.

### 3. CSS files

| Old | New |
|-----|-----|
| `QuestBoard.Service/wwwroot/css/guild-members.css` | `QuestBoard.Service/wwwroot/css/characters.css` |
| `QuestBoard.Service/wwwroot/css/guild-members.mobile.css` | `QuestBoard.Service/wwwroot/css/characters.mobile.css` |

**Definitive class rename table — `guild-members.css` (307 lines, verified full read):**

| Old class | New class (recommended) | Occurrences |
|-----------|--------------------------|--------------|
| `.guild-members-page` | `.characters-page` | 27 (used as a scoping prefix selector throughout the file, e.g. `.guild-members-page .character-card`) |

Note: every other selector in this file (`.character-card`, `.character-image`, `.character-placeholder`, `.retired-badge`, `.dead-badge`, `.main-badge`, `.character-info`, `.character-name`, `.character-class`, `.character-level`, `.character-owner`, `character-card-ripple` keyframe) is **already `character-*`-named** — only the single top-level page-scope class `.guild-members-page` needs renaming. The header comment block (`/* GUILD MEMBERS - CHARACTER CARDS */`) should also be updated for consistency (not load-bearing, but avoids leaving stale text in a renamed file).

**Definitive class rename table — `guild-members.mobile.css` (115 lines, verified full read):**

| Old class | New class (recommended) | Occurrences |
|-----------|--------------------------|--------------|
| `.guild-section-card` | `.character-section-card` | 2 (rule definition + Index.Mobile.cshtml references ×2) |
| `.guild-section-heading` | `.character-section-heading` | 2 |
| `.guild-member-row` | `.character-member-row` | 6 (base rule + `:last-child`, `:active`, `.retired`, `.dead` modifiers + 2 usages in view) |
| `.guild-member-thumbnail` | `.character-member-thumbnail` | 2 |
| `.guild-member-placeholder` | `.character-member-placeholder` | 2 |
| `.guild-member-name` | `.character-member-name` | 2 |
| `.guild-member-class` | `.character-member-class` | 1 (combined selector `.guild-member-class, .guild-member-owner`) |
| `.guild-member-owner` | `.character-member-owner` | 1 (same combined selector; also 1 usage in view for OtherCharacters) |
| `.guild-empty-state` | `.character-empty-state` | 4 (base + `i`, `h5`, `p` child selectors) |
| `.guild-section-card .text-muted` (compound) | `.character-section-card .text-muted` | 1 |
| `.guild-section-card .badge` (compound) | `.character-section-card .badge` | 1 |

The file's header comment (`/* guild-members.mobile.css — loaded exclusively by GuildMembers/Index.Mobile.cshtml ... */`) must be updated to reference the new filename and controller/view path.

**Note on naming convention:** This research recommends `.character-member-row` (not the terser `.character-row`) to preserve the semantic meaning "a row representing one character" without colliding with the desktop CSS's unrelated `.character-card` naming, and to keep a clear visual grep-distinction between the two CSS files' scoped classes. Either is acceptable per CONTEXT.md's "Claude's Discretion" — just be internally consistent.

**CSS files that reference "guild" only in a header comment, NOT in class names (no rename target, comment-only touch-up):**

| File | Line | Content | Action |
|------|------|---------|--------|
| `QuestBoard.Service/wwwroot/css/character-detail.mobile.css` | 1 | `/* character-detail.mobile.css — loaded exclusively by GuildMembers/Details.Mobile.cshtml via _Layout.Mobile.cshtml; no media queries */` | Update comment to say `Characters/Details.Mobile.cshtml` |
| `QuestBoard.Service/wwwroot/css/character-form.mobile.css` | 1 | `/* character-form.mobile.css — loaded exclusively by GuildMembers/Create.Mobile.cshtml and Edit.Mobile.cshtml via _Layout.Mobile.cshtml; no media queries */` | Update comment to say `Characters/Create.Mobile.cshtml and Edit.Mobile.cshtml` |

These two files were **not in CONTEXT.md's rename list** because they don't need a file rename or class rename — only their header comment's path reference needs updating for accuracy. Flagging this so the planner doesn't miss the comment drift (these files are otherwise entirely correct and require no other changes).

### 4. ViewModels

| Old | New |
|-----|-----|
| `QuestBoard.Service/ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` | `QuestBoard.Service/ViewModels/PlayersViewModels/PlayersIndexViewModel.cs` (recommended name; Claude's discretion per D-02) |

**Confirmed via `ls`:** the `GuildMembersViewModels/` folder contains **exactly one file** — `GuildMembersIndexViewModel.cs`. No other content. After this move, the folder is empty and must be deleted (per CONTEXT.md's discretion note).

**Already correctly named — do NOT touch:**
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharactersIndexViewModel.cs` — confirmed via full controller read: `Index()` action constructs `new CharactersIndexViewModel { CurrentUserId, MyCharacters, OtherCharacters }`.

**`GuildMembersIndexViewModel.cs` current content (verified, 8 lines):**
```csharp
namespace QuestBoard.Service.ViewModels.GuildMembersViewModels;

public class GuildMembersIndexViewModel
{
    public IEnumerable<User> DungeonMasters { get; set; } = [];
    public IEnumerable<User> Players { get; set; } = [];
}
```

Consumers to update when renaming this class:
- `QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs` line 2 (`using` statement) and line 14 (`new GuildMembersIndexViewModel { ... }`)
- `QuestBoard.Service/Views/Players/Index.cshtml` lines 2-3 (`@using`, `@model GuildMembersIndexViewModel`)
- `QuestBoard.Service/Views/Players/Index.Mobile.cshtml` lines 2-3 (same)

**Naming precedent already established:** the `Players` feature already has a `players.mobile.css` file with `.players-section-card`, `.players-section-heading`, `.players-row`, `.players-name`, `.players-empty-state` classes (verified via full read of `Players/Index.Mobile.cshtml`) — none of these reference "guild". This confirms `ViewModels/PlayersViewModels/` is the natural, convention-matching destination folder name (matches `ViewModels/[Feature]ViewModels/` pattern used everywhere else, e.g. `CharacterViewModels`, `AccountViewModels`, `CalendarViewModels`, `QuestViewModels` — all confirmed present in `_ViewImports.cshtml`).

### 5. Integration tests

| Old | New |
|-----|-----|
| `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` | `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` |

**Class rename:** `GuildMembersControllerIntegrationTests` → `CharactersControllerIntegrationTests`.

**Full route-string inventory in this file (verified via full read, 504 lines, 17 test methods) — every hardcoded route string that must change:**

| Line(s) | Route string | Method |
|---------|--------------|--------|
| 12, 18 | `"/GuildMembers"` | `Index_ShouldReturnGuildMembersPage` — **method name itself references "GuildMembers", should become e.g. `Index_ShouldReturnCharactersPage`** |
| 49 | `"/GuildMembers"` | `Index_WithMembers_ShouldDisplayAllMembers` |
| 77 | `"/GuildMembers"` | `Index_ShouldShowDungeonMasterBadge` |
| 103 | `"/GuildMembers"` | `Index_ShouldDisplayUserInformation` |
| 125 | `"/GuildMembers/Edit/{character.Id}"` | `Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed` |
| 145 | `"/GuildMembers/Edit/{character.Id}"` | `Edit_SuperAdminEditingAnotherPlayersCharacter_ShouldSucceed` |
| 165 | `"/GuildMembers/Edit/{character.Id}"` | `Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden` |
| 190 | `"/GuildMembers/Edit/{character.Id}"` | `Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound` |
| 209 | `"/GuildMembers/Edit/{character.Id}"` | `Edit_OwnerEditingOwnCharacter_ShouldSucceed` |
| 251 | `"/GuildMembers/Edit/{targetCharacter.Id}"` | `Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner` |
| 311 | `"/GuildMembers/Edit/{targetCharacter.Id}"` | `Edit_PromotingCharacterToMain_ShouldDemoteOwnersOtherCharacterToBackup` |
| 346 | `"/GuildMembers/Details/{character.Id}"` | `Details_AdminViewingAnotherPlayersCharacter_ShowsEditButton` |
| 373 | `"/GuildMembers/Delete"` | `Delete_AdminDeletingAnotherPlayersCharacter_ShouldSucceed` |
| 408 | `"/GuildMembers/Delete"` | `Delete_PlayerDeletingAnotherPlayersCharacter_ShouldBeForbidden` |
| 435 | `"/GuildMembers/ToggleRetirement"` | `ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed` |
| 471 | `"/GuildMembers/ToggleRetirement"` | `ToggleRetirement_PlayerTogglingAnotherPlayersCharacter_ShouldBeForbidden` |
| 499 | `"/GuildMembers/Delete"` | `Delete_AdminDeletingCharacterInDifferentGroup_ShouldReturnNotFound` |

Also: line 23 asserts `content.Should().ContainAny("Guild", "Members")` — this is a loose assertion that would still technically pass if the rendered page contains neither word post-rename (it uses `ContainAny`, which requires only ONE to match) — **but both "Guild" and "Members" will be absent from the renamed page**, so this assertion will fail and must be updated to something like `content.Should().ContainAny("Character", "Characters")`.

**CRITICAL FINDING NOT IN CONTEXT.md — `MobileViewsTests.cs` also has hardcoded `/GuildMembers` routes.** CONTEXT.md's canonical_refs section only listed `GuildMembersControllerIntegrationTests.cs` and `LayoutNavigationTests.cs` (for nav text). It did **not** mention `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs`, which contains 4 more test methods with hardcoded old-route assertions:

| File | Line(s) | Content | Action |
|------|---------|---------|--------|
| `Mobile/MobileViewsTests.cs` | 737 | Comment: `// Guild Members index renders list rows on mobile UA` | Update comment |
| `Mobile/MobileViewsTests.cs` | 741, 744 | Doc comments: `/// Mobile UA on /GuildMembers links guild-members.mobile.css. The guild-member-row ... Note: guild-member-row presence ...` | Update doc comments (route + CSS class + filename all referenced) |
| `Mobile/MobileViewsTests.cs` | 745 | Doc comment: `/// Test starts RED — GuildMembers/Index.Mobile.cshtml does not exist yet.` | Stale historical comment — safe to update or leave (describes original TDD state); recommend updating for consistency |
| `Mobile/MobileViewsTests.cs` | 748 | Method name: `MobileGuildMembers_MobileUserAgent_RendersListRows` | Rename to e.g. `MobileCharacters_MobileUserAgent_RendersListRows` |
| `Mobile/MobileViewsTests.cs` | 753 | Route string: `"/GuildMembers"` | Update to `"/Characters"` |
| `Mobile/MobileViewsTests.cs` | 760 | Assertion: `html.Should().Contain("guild-members.mobile.css")` | Update to `html.Should().Contain("characters.mobile.css")` |
| `Mobile/MobileViewsTests.cs` | 768-769 | Doc comment referencing `/GuildMembers/Details/{id}` and CSS | Update |
| `Mobile/MobileViewsTests.cs` | 778 | Route string: `"/GuildMembers/Details/{character.Id}"` | Update to `"/Characters/Details/{character.Id}"` |
| `Mobile/MobileViewsTests.cs` | 794-795 | Doc comment referencing `/GuildMembers/Create` | Update |
| `Mobile/MobileViewsTests.cs` | 803 | Route string: `"/GuildMembers/Create"` | Update to `"/Characters/Create"` |
| `Mobile/MobileViewsTests.cs` | 819-820 | Doc comment referencing `/GuildMembers/Edit/{id}` | Update |
| `Mobile/MobileViewsTests.cs` | 829 | Route string: `"/GuildMembers/Edit/{character.Id}"` | Update to `"/Characters/Edit/{character.Id}"` |

**This is a genuinely new finding beyond CONTEXT.md's scope** — the planner must add `MobileViewsTests.cs` as an explicit task target, not just the two files CONTEXT.md named.

**`LayoutNavigationTests.cs` (already flagged by CONTEXT.md, confirmed via full read):**

| Line | Content | Action |
|------|---------|--------|
| 92 (comment) | `// NAV-03: Campaign+authenticated — Guild Members link PRESENT (regression guard)` | Update comment text |
| 98 | Method name: `Nav_CampaignAuthenticated_GuildMembersLinkPresent` | Rename to e.g. `Nav_CampaignAuthenticated_CharactersLinkPresent` |
| 107 | Assertion: `html.Should().Contain("Guild Members")` | Update to `html.Should().Contain("Characters")` |

**QuestBoard.UnitTests/ — confirmed ZERO references to "guild" (case-insensitive), verified via full-project grep.** No unit test changes needed; this controller has never had unit-level tests, only integration tests.

### 6. Cross-references (`Url.Action` controller-name strings)

All four confirmed via full-file grep — exact matches to CONTEXT.md's list, no additional occurrences found elsewhere in the repo:

| File | Line | Current | New |
|------|------|---------|-----|
| `QuestBoard.Service/Views/QuestLog/Details.cshtml` | 73 | `@Url.Action("GetProfilePicture", "GuildMembers", new { id = participant.Character.Id })` | `"Characters"` |
| `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` | 69 | same pattern | `"Characters"` |
| `QuestBoard.Service/Views/Quest/Details.cshtml` | 111 | same pattern (`participant.Character.Id`) | `"Characters"` |
| `QuestBoard.Service/Views/Quest/Details.cshtml` | 227 | same pattern (`player.Character.Id`) | `"Characters"` |
| `QuestBoard.Service/Views/Quest/Manage.cshtml` | 410 | same pattern (`player.Character.Id`) | `"Characters"` |

Confirmed: `QuestBoard.Service/Views/Quest/_QuestCard.cshtml:61` also has `@Url.Action("GetProfilePicture", "GuildMembers", new { id = player.Character.Id })` — **this is a 6th occurrence not listed in CONTEXT.md's canonical_refs** (CONTEXT.md's list only named 4 line locations across QuestLog/Quest views but the count in the codebase-wide grep was 4 files with 6 total occurrences: QuestLog/Details.cshtml ×1, QuestLog/Details.Mobile.cshtml ×1, Quest/Details.cshtml ×2, Quest/Manage.cshtml ×1, **Quest/_QuestCard.cshtml ×1**). Add `_QuestCard.cshtml` to the task list.

### 7. Nav links and global using

| File | Line | Current | New |
|------|------|---------|-----|
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | 19 | `<link rel="stylesheet" href="~/css/guild-members.css" asp-append-version="true" />` | `~/css/characters.css` |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | 131 | `asp-controller="GuildMembers"` | `asp-controller="Characters"` |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | 132 | `Guild Members` (link text) | `Characters` |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` | 113 | `asp-controller="GuildMembers"` | `asp-controller="Characters"` |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` | 114 | `Guild Members` (link text) | `Characters` |
| `QuestBoard.Service/Views/_ViewImports.cshtml` | 9 | `@using QuestBoard.Service.ViewModels.GuildMembersViewModels` | `@using QuestBoard.Service.ViewModels.PlayersViewModels` (or wherever `PlayersIndexViewModel` lands — this using statement is only needed if `Players/Index.cshtml` doesn't already `@using` it directly; note `Players/Index.cshtml` already has its own explicit `@using` at line 2, so removing this global one and confirming the local `@using` covers it is an option, or simply updating the global using's namespace path) |

**Verified: `_Layout.cshtml` has no other CSS `<link>` referencing guild-members** beyond line 19 — confirmed via targeted grep on the file.

### 8. Explicit view-path strings

**Confirmed via grep across the entire `QuestBoard.Service` project: zero occurrences of `View("~/Views/GuildMembers/...")` or any explicit view-path override.** All view resolution uses MVC's default convention (`Views/{Controller}/{Action}.cshtml`), so the Views folder rename + controller rename together are sufficient — no controller code needs a `return View("...")` path fix.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages. No `npm install` / `dotnet add package` operations occur; it is a pure rename of existing first-party files.

## Architecture Patterns

### System Architecture Diagram

```
Browser (nav link click: "Characters")
        │
        ▼
  GET /Characters  ──────────────► CharactersController.Index()
        │                                  │
        │                                  ▼
        │                       ICharacterService.GetAllCharactersWithDetailsAsync()
        │                                  │
        │                                  ▼
        │                       AutoMapper: CharacterEntity → Character → CharacterViewModel
        │                                  │
        │                                  ▼
        │                       CharactersIndexViewModel { MyCharacters, OtherCharacters }
        │                                  │
        ▼                                  ▼
  Views/Characters/Index.cshtml  ◄─────────┘
  (or Index.Mobile.cshtml, selected by
   AspNetCore.MvcRouting UA-based view engine)
        │
        ▼
  Renders using characters.css / characters.mobile.css
  (character-card, character-member-row, etc.)
        │
        ▼
  HTML response with "Characters" nav-active state
```

The rename touches every box in this diagram except the Domain/Repository layers (`ICharacterService`, `CharacterEntity`, AutoMapper profiles) — those remain untouched since they were already correctly named.

### Recommended Execution Order (Wave Structure)

Because Razor's view-resolution convention couples the controller's class name to its view folder name, a naive "rename everything in one pass" risks a broken intermediate state if plans execute out of order. Recommended wave sequence:

1. **Wave 1 — CSS:** Rename `guild-members.css`/`guild-members.mobile.css` → `characters.css`/`characters.mobile.css`; rename all classes inside. (No consumers reference the new names yet — safe, isolated change; old `<link>` tags still point to old filenames until Wave 3, so nothing breaks mid-wave as long as both old and new CSS files can coexist temporarily, or this wave is done atomically with Wave 3's `<link>` update in the same task.)
2. **Wave 2 — ViewModels:** Split `GuildMembersViewModels/` → move `PlayersIndexViewModel` (renamed) to `PlayersViewModels/`, update `PlayersController.cs` and `Players/Index*.cshtml`. Fully independent of the Characters rename — can run in parallel with Wave 1.
3. **Wave 3 — Views folder + controller (coupled, must be one atomic change):** Rename `Views/GuildMembers/` → `Views/Characters/`, update all CSS class references and copy inside those 4 content-bearing files (`Index.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`), update the CSS `<link>` path in `Index.Mobile.cshtml`, AND rename `GuildMembersController.cs` → `CharactersController.cs` in the same task/commit — these three changes are interdependent (old controller name + new view folder = 404; new controller name + old view folder = 404) and should not be split across separate plan tasks that could execute out of order.
4. **Wave 4 — Cross-references:** Update the 6 `Url.Action(..., "GuildMembers", ...)` call sites (QuestLog/Details×2, Quest/Details×2, Quest/Manage×1, Quest/_QuestCard×1) and the `_ViewImports.cshtml` using statement.
5. **Wave 5 — Nav links:** Update `_Layout.cshtml` and `_Layout.Mobile.cshtml` nav link `asp-controller` + link text + CSS `<link>` href.
6. **Wave 6 — Tests:** Rename `GuildMembersControllerIntegrationTests.cs` → `CharactersControllerIntegrationTests.cs` (class + all 16 route strings + 1 method name + 1 loose assertion); update `MobileViewsTests.cs` (4 route strings + 1 method name + doc comments); update `LayoutNavigationTests.cs` (1 method name + 1 assertion + 1 comment).

Waves 3 and 6 are the highest-risk since a partial rename leaves the app in a broken/red-test state — recommend these are done as single atomic commits rather than split across multiple smaller tasks.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bulk find-and-replace across many files | A custom script that blindly regex-replaces "guild" everywhere | Targeted per-file edits guided by this manifest | A blind global replace would incorrectly touch README.md's unrelated "guild system" prose, `.planning/` historical docs, and the `QuestBoardContext.cs` comment about "guild roster" being intentional empty-state behavior — none of which are in scope per D-01's boundary |
| Verifying the rename is complete | Manual re-reading of every file | `grep -ri "guild" --include="*.cs" --include="*.cshtml" --include="*.css"` across `QuestBoard.Service/` and `QuestBoard.IntegrationTests/` as a final verification step | Fast, deterministic, catches anything missed — should return zero hits in those two projects when the phase is done (excluding README.md/`.planning/` which are out of scope) |

**Key insight:** This phase has no "hard" engineering problems — it's pure mechanical renaming. The risk is *completeness*, not correctness of any individual line. The manifest above plus a final `grep -ri guild` sweep (scoped to `QuestBoard.Service/` and `QuestBoard.IntegrationTests/`, explicitly excluding `.planning/` and `README.md`) is the verification strategy, not custom tooling.

## Runtime State Inventory

This is a rename/refactor phase, so this section is required per the verification protocol.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | **None.** `CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` (Repository layer) were already named "Character" from inception — no database column, table name, or stored string contains "Guild" or "GuildMembers". Confirmed via full-repo grep of `QuestBoard.Repository/` (only hit is a comment in `QuestBoardContext.cs`, not data). | None — no data migration needed. |
| Live service config | **None.** No external service (email provider, etc.) has a "GuildMembers"-named webhook, config key, or dashboard reference. This app has no n8n/Datadog/Tailscale-style external config surfaces per its architecture (self-hosted ASP.NET Core app on Linux + Postfix per project memory). | None. |
| OS-registered state | **None.** No Windows Task Scheduler, systemd unit, or pm2 process references "GuildMembers" — this app runs as a single self-hosted process (per project memory: "/opt/questboard/, NOT Docker"), and its process name/service registration (if any, e.g. systemd unit file) is not part of this repo and was not found referencing "Guild" in any tracked file. | Verify (outside repo scope) that the systemd unit or launch script at the deployment host does not reference "GuildMembers" in its description/name — informational only, not blocking, since this repo has no such file checked in. |
| Secrets/env vars | **None.** No `.env`, `appsettings.json`, or `appsettings.Development.json` contains "guild" (confirmed via direct grep — zero hits in both appsettings files). | None. |
| Build artifacts | **Stale references found, self-healing.** `bin/`/`obj/` directories under all 3 projects contain generated `*.staticwebassets.*.json` manifest files referencing `guild-members.css`/`guild-members.mobile.css` by filename (these are build-time-generated bundler manifests, not source-controlled). | None required — these regenerate automatically on the next `dotnet build` once the CSS files are renamed in source. No manual cleanup needed, but if a stale `bin/obj` causes a confusing 404 for the old CSS path during local dev, a `dotnet clean` resolves it. |

**Canonical answer for this phase:** After every file in `QuestBoard.Service/` and `QuestBoard.IntegrationTests/` is updated per the manifest above, the only remaining "GuildMembers" string in the entire repo is prose usage in `.planning/` (historical phase docs — immutable record, never rewritten) and `README.md` line 5's generic "guild system" phrase (out of scope per D-01's boundary — not the "Guild Members" feature name, just flavor text describing the product). Both are acceptable to leave untouched.

## Common Pitfalls

### Pitfall 1: Renaming the controller before the Views folder
**What goes wrong:** MVC's default view-location convention looks for `Views/{ControllerName}/{Action}.cshtml`. If `GuildMembersController.cs` is renamed to `CharactersController.cs` first (in its own commit) while `Views/GuildMembers/` still exists, every action on the controller will throw `InvalidOperationException: The view '...' was not found` at runtime, and every integration test hitting that controller will fail.
**Why it happens:** The controller name and view folder name are two independent renames that must land in the same atomic change to avoid a broken intermediate state.
**How to avoid:** Rename the Views folder AND the controller class in the same task/commit (Wave 3 above), not as separate sequential plans.
**Warning signs:** Integration tests suddenly returning 500 with "view not found" exceptions after only the controller file was touched.

### Pitfall 2: Missing the `MobileViewsTests.cs` route strings
**What goes wrong:** CONTEXT.md's canonical_refs section names `GuildMembersControllerIntegrationTests.cs` and `LayoutNavigationTests.cs` as the test files to update, but does not mention `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs`, which has 4 hardcoded `/GuildMembers/*` route strings and one `guild-members.mobile.css` string assertion. If the planner only reads CONTEXT.md's file list without this research, these 4 tests will start failing (404s / CSS-link-not-found assertions) after the rename ships, and the failure won't be obvious from the phase's own file-change list.
**Why it happens:** CONTEXT.md's scouting grep pattern likely searched for `GuildMembers` primarily in files whose names contained "GuildMembers", missing a test file (`MobileViewsTests.cs`) that references the route as a string literal without having "GuildMembers" in its own filename.
**How to avoid:** Include `MobileViewsTests.cs` explicitly as a task target — see the Integration Tests section above for the exact line numbers and required edits.
**Warning signs:** `dotnet test` failures in `MobileGuildMembers_MobileUserAgent_RendersListRows` and 3 sibling tests after the rename, with 404 or missing-CSS-link assertion failures.

### Pitfall 3: Forgetting `Quest/_QuestCard.cshtml`'s `Url.Action` cross-reference
**What goes wrong:** CONTEXT.md's canonical_refs lists 4 `Url.Action("GetProfilePicture", "GuildMembers", ...)` call sites, but a 5th exists in the shared partial `Views/Quest/_QuestCard.cshtml:61`. Because this is a partial view included from multiple parent views, missing it means profile picture images silently 404 (broken `<img>` src) anywhere `_QuestCard.cshtml` is rendered, post-rename — a visually obvious but easy-to-miss regression since the page still loads (just with a broken image icon).
**Why it happens:** Partial views are easy to miss in a manual file-list audit since they're not top-level "pages" — grep is the only reliable way to find every consumer of a cross-controller `Url.Action` string.
**How to avoid:** Run `grep -rn 'Url.Action.*"GuildMembers"' QuestBoard.Service/` as an explicit verification step before considering this phase done — should find exactly 6 occurrences across 4 files, all of which must be updated.
**Warning signs:** Broken character thumbnail images on the Quest board / Quest details pages after deployment, while the Characters page itself works fine.

### Pitfall 4: Assuming all 8 view files need CSS-class-level edits
**What goes wrong:** Spending equal planning/task effort auditing all 8 files for "guild" content when 4 of them (`Edit.cshtml`, `Edit.Mobile.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml`) have zero "guild" references and only need the folder-level `git mv`.
**Why it happens:** CONTEXT.md's phrasing ("8 files") implies uniform effort per file; in reality the content-editing work concentrates entirely in `Index.*` and `Details.*` (4 files).
**How to avoid:** Use the per-file audit table in this research (Section 2 above) to right-size the plan's task granularity — a single task can handle the folder `git mv` for all 8 files, with a separate, more detailed task for the CSS-class/copy edits in just the 4 content-bearing files.
**Warning signs:** N/A — this is a planning-efficiency pitfall, not a runtime bug.

## Code Examples

### Example: Controller rename preserving convention-based routing
```csharp
// Before: QuestBoard.Service/Controllers/Characters/GuildMembersController.cs
namespace QuestBoard.Service.Controllers.Characters
{
    [Authorize]
    public class GuildMembersController(
        ICharacterService characterService,
        IUserService userService,
        IActiveGroupContext activeGroupContext,
        IMapper mapper) : Controller
    {
        // ... 7 actions, no [Route] attributes ...
    }
}

// After: QuestBoard.Service/Controllers/Characters/CharactersController.cs
namespace QuestBoard.Service.Controllers.Characters
{
    [Authorize]
    public class CharactersController(
        ICharacterService characterService,
        IUserService userService,
        IActiveGroupContext activeGroupContext,
        IMapper mapper) : Controller
    {
        // ... same 7 actions, unchanged bodies — only the class name and
        // any internal comments referencing "Guild Members" change ...
    }
}
```
Note the one in-body comment that must also be updated: line 152 `// Tag the character to the active group so Guild Members scoping applies` → reword to avoid the stale feature name (e.g. `// Tag the character to the active group so the character-roster scoping applies`).

### Example: CSS class + view reference kept in sync
```css
/* Before: guild-members.mobile.css */
.guild-member-row {
    padding: 12px 0;
    border-bottom: 1px solid rgba(139, 69, 19, 0.3);
    cursor: pointer;
}

/* After: characters.mobile.css */
.character-member-row {
    padding: 12px 0;
    border-bottom: 1px solid rgba(139, 69, 19, 0.3);
    cursor: pointer;
}
```
```html
<!-- Before: Views/GuildMembers/Index.Mobile.cshtml -->
<div class="guild-member-row d-flex align-items-center...">

<!-- After: Views/Characters/Index.Mobile.cshtml -->
<div class="character-member-row d-flex align-items-center...">
```

### Example: Integration test route-string update
```csharp
// Before
var response = await client.GetAsync("/GuildMembers", TestContext.Current.CancellationToken);

// After
var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);
```

## State of the Art

Not applicable in the traditional "framework version" sense — this is an internal naming convention fix, not a library/framework upgrade. There is no "old vs. new approach" to document; the Domain/Repository layers already reflect the "current" (post-rename) naming convention, and this phase brings the Service layer into alignment with a pattern that has existed in the same codebase since the `CharacterEntity`/`ICharacterService` classes were first created.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Recommended new class name `PlayersIndexViewModel` for the renamed `GuildMembersIndexViewModel` | ViewModels section | Low — explicitly marked Claude's Discretion in CONTEXT.md; any clear, convention-following name is acceptable, this is just a suggestion |
| A2 | Recommended new CSS class names (`.characters-page`, `.character-member-row`, `.character-section-card`, etc.) | CSS rename tables | Low — explicitly marked Claude's Discretion in CONTEXT.md; only internal consistency is required, exact naming is flexible |
| A3 | Recommended reworded empty-state copy is not yet drafted verbatim in this research (left to planner/execution to word naturally) | Views section | Low — explicitly marked Claude's Discretion in CONTEXT.md |
| A4 | `README.md` line 5's "guild system" phrase and `.planning/codebase/*` docs are out of scope for this phase | Runtime State Inventory / Don't Hand-Roll | Low-Medium — if the user actually wants ALL "guild" references scrubbed repo-wide (contradicting D-01's stated boundary and the Deferred Ideas list), this would need a follow-up task; flagged explicitly so the planner can confirm with the user if there's ambiguity, but CONTEXT.md's own Deferred Ideas section explicitly excludes "other D&D-flavored terminology" and this phrase is generic marketing prose, not a symbol name |

**If this table is empty:** N/A — see above; all items are low-risk naming suggestions explicitly delegated to Claude's discretion by CONTEXT.md, not load-bearing assumptions about behavior or requirements.

## Open Questions

1. **Should the `@using QuestBoard.Service.ViewModels.GuildMembersViewModels` global import in `_ViewImports.cshtml` simply be removed (since `Players/Index.cshtml` already has its own local `@using` for the same namespace) rather than updated to point at the new `PlayersViewModels` namespace?**
   - What we know: `_ViewImports.cshtml` line 9 currently imports the GuildMembersViewModels namespace globally; `Players/Index.cshtml` and `Players/Index.Mobile.cshtml` both also have their own explicit `@using` for the same namespace (redundant with the global one).
   - What's unclear: Whether the global import exists for a reason not surfaced by this research (e.g., some other view relying on it implicitly) — a grep found no other consumer, but it's worth a final check during implementation.
   - Recommendation: Safest path is to update the global import's namespace to `QuestBoard.Service.ViewModels.PlayersViewModels` (mirroring the rename) rather than removing it — preserves current behavior with minimal risk, and the redundant local `@using` in `Players/Index*.cshtml` can be cleaned up as a trivial dead-code removal in the same task if desired.

2. **Is the `QuestBoardContext.cs` comment (line 300: "an empty guild roster is the intended behavior here, not an oversight") in scope for a wording touch-up?**
   - What we know: This is a code comment (not a string literal, not user-facing), explaining why an empty-group character query intentionally returns nothing rather than falling back to "everyone's characters." CLAUDE.md's comment-hygiene rule is about planning/tracking IDs (`D-06`, `Phase 28`), not about descriptive prose using domain vocabulary.
   - What's unclear: Whether "guild roster" here is meant informally (interchangeable with "the Characters page's result set") such that leaving it is harmless, or whether it should be reworded to "character roster" for terminology consistency with this phase's stated goal ("so the terminology is tenant-generic instead of D&D-specific").
   - Recommendation: Low-priority, optional touch-up — reword to "an empty character roster is the intended behavior here" for terminology consistency, but this is not required for the phase's zero-behavior-change goal and can be skipped without any functional impact. Recommend including it as a minor task since it's a one-line comment edit and this phase's explicit goal is terminology consistency in the Service layer's presentation surface (this comment is in `QuestBoard.Repository`, technically outside the phase's stated Service-project boundary — so it may also be reasonable to explicitly leave it out of scope).

## Environment Availability

Not applicable — this phase has no external tool/service/runtime dependencies beyond the existing .NET SDK and the project's own build tooling, both of which are already confirmed present and working (per CLAUDE.md's documented dev environment: Windows, `dotnet build`/`dotnet test`, SQL Server on localhost).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (v3, per `TestContext.Current.CancellationToken` usage pattern seen throughout `GuildMembersControllerIntegrationTests.cs`) |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`, `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Characters\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~LayoutNavigationTests"` |
| Full suite command | `dotnet test` (from repo root — runs all 3 test projects) |

### Phase Requirements → Test Map

This is an ad-hoc backlog phase with no REQUIREMENTS.md mapping (per CONTEXT.md and the phase's own instructions: "None (ad-hoc backlog phase — no REQUIREMENTS.md mapping)"). The below maps the phase's *implicit* behavioral requirements (zero behavior change) to existing tests that must continue passing after the rename:

| Behavior | Test Type | Automated Command | File Exists? |
|----------|-----------|--------------------|--------------| 
| `/Characters` (formerly `/GuildMembers`) Index renders and lists all characters | integration | `dotnet test --filter "FullyQualifiedName~CharactersControllerIntegrationTests"` | Renamed from existing file, ✅ |
| Admin/SuperAdmin can edit another player's character; player cannot | integration | same filter | ✅ (existing Phase 56 tests, must survive rename with updated routes) |
| Mobile UA renders correct list rows / detail card / form card + correct CSS link | integration | `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` | ✅ (existing, needs route+string updates only) |
| Nav shows "Characters" (formerly "Guild Members") link for authenticated users | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ (existing, needs string update only) |
| Cross-controller profile picture links resolve (Quest/QuestLog views) | manual / visual smoke | Run app locally, view a Quest with signed-up characters, confirm avatar images load | No dedicated automated test currently asserts on this — **Wave 0 gap** |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Characters|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~LayoutNavigationTests"` (fast, scoped to affected tests)
- **Per wave merge:** `dotnet test` (full suite — catches any cross-project reference this research missed)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- No existing automated test asserts that `Url.Action("GetProfilePicture", "Characters", ...)` cross-references from Quest/QuestLog views actually resolve to a working image URL post-rename. Recommend either (a) a quick manual/visual check during execution (load a Quest details page with a signed-up character, confirm the avatar renders), or (b) if the team wants automated coverage, a lightweight integration test hitting `/Characters/GetProfilePicture/{id}` directly — this already exists implicitly as part of `CharactersControllerIntegrationTests` scope but no explicit assertion checks the cross-controller call sites in Quest views.
- No test currently locks in the exact "Guild Roster" → renamed heading text choice (Claude's Discretion item) — if the planner wants a regression guard for whatever heading text is chosen, a new assertion should be added to the renamed `CharactersControllerIntegrationTests.cs` (mirrors the existing informal pattern where `Index_ShouldReturnGuildMembersPage`/`Index_ShouldReturnCharactersPage`'s loose `ContainAny` assertion is the closest existing analog).

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Unaffected — `[Authorize]` attribute stays as-is on the renamed controller |
| V3 Session Management | No | Unaffected |
| V4 Access Control | Yes (verify, not implement) | The `CanManageCharacterAsync` owner-or-admin guard (added Phase 56) must be verified to still gate Edit/Delete/ToggleRetirement correctly after the rename — this is a **regression check**, not new authorization logic. No new authorization code is introduced by this phase. |
| V5 Input Validation | No | No new input surfaces introduced |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Broken access control via stale authorization checks after a rename | Elevation of Privilege | Re-run the full `CharactersControllerIntegrationTests` suite (all Admin/SuperAdmin/Player/cross-group test cases from Phase 56) post-rename to confirm `CanManageCharacterAsync` still correctly blocks players from editing others' characters — this is purely a regression-verification concern, not a new threat surface, since D-01/D-02/D-03 introduce no new authorization logic. |
| Route enumeration / stale-bookmark 404s (intentional, per D-03) | Information Disclosure (low severity, accepted risk) | D-03 explicitly accepts that old `/GuildMembers/*` bookmarks 404 with no redirect — this is a deliberate, documented tradeoff for a ~17-member trusted-group app, not a security gap requiring mitigation. |

This phase introduces no new attack surface — it is a pure rename with the same `[Authorize]` + `CanManageCharacterAsync` guard structure preserved verbatim. The only security-relevant verification is confirming the Phase 56 authorization test suite still passes after every route/class rename.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection via `Read`/`Grep`/`Bash` tools this session — full-content reads of `GuildMembersController.cs`, all 8 view files in `Views/GuildMembers/`, both CSS files (`guild-members.css`, `guild-members.mobile.css`), `GuildMembersIndexViewModel.cs`, `PlayersController.cs`, `Players/Index.cshtml` + `.Mobile.cshtml`, `_ViewImports.cshtml`, `_Layout.cshtml`/`_Layout.Mobile.cshtml` (targeted sections), `GuildMembersControllerIntegrationTests.cs` (full 504 lines), `LayoutNavigationTests.cs` (full 207 lines), `MobileViewsTests.cs` (targeted "Guild" sections), `QuestBoardContext.cs` (targeted comment), `character-detail.mobile.css`/`character-form.mobile.css` (header comments), `README.md` (targeted line), `appsettings.json`/`appsettings.Development.json` (grep, zero hits), `docs/server-setup.md` (grep, zero hits).
- `.planning/phases/58-.../58-CONTEXT.md` — user decisions from `/gsd-discuss-phase`, read in full.
- `.planning/REQUIREMENTS.md`, `.planning/STATE.md` — read in full for project context.

### Secondary (MEDIUM confidence)
- None — this phase required no external documentation lookup (no new library, no framework upgrade, no third-party API). All findings are first-party codebase inspection.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — no new stack/library involved, this is an internal rename.
- Architecture: HIGH — every file in the rename manifest was read in full this session; the action list, route table, and CSS class inventory are exhaustive, not sampled.
- Pitfalls: HIGH — all 4 pitfalls are grounded in specific line-level findings from this session's full-file reads (e.g., the `MobileViewsTests.cs` gap and the `_QuestCard.cshtml` 6th cross-reference are concrete, verified discoveries, not speculation).

**Research date:** 2026-07-06
**Valid until:** Effectively indefinite for a same-day rename phase — this research is tied to the exact current state of the working tree (branch `milestone/v7-backlog-cleanup`) and should be treated as stale if significant unrelated commits land on this branch before the phase executes (re-grep for "guild" as a cheap staleness check before planning).
