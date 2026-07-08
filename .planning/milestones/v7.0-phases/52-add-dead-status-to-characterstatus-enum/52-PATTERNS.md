# Phase 52: Add Dead status to CharacterStatus enum - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 7 (1 enum + 4 Razor views + 2 CSS files)
**Analogs found:** 7 / 7 (all are self-analogous — this phase edits existing files by extending an existing binary pattern to a third value, so each file's own current code is its own best analog)

This phase has an unusual pattern-mapping shape: there are **no new files**, only edits to existing files that already contain the exact two-way pattern (Active/Retired) that needs to become three-way (Active/Retired/Dead). Consequently "closest analog" for each file is **the adjacent existing branch/class within that same file** rather than a different file elsewhere in the codebase. Two files (`GuildMembersController.cs`, `CharacterRepository.cs`) are confirmed **zero-change** touchpoints — included here only so the planner/verifier doesn't mistake them for missed work.

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|----------------|------|-----------|-----------------|---------------|
| `QuestBoard.Domain/Enums/CharacterStatus.cs` | model (enum) | CRUD (data definition) | itself — additive member on existing 2-value enum | exact |
| `QuestBoard.Service/Views/GuildMembers/Details.cshtml` | component (Razor view) | request-response | own existing Retired-vs-Active if/else block (lines 31-42) + own existing toggle-button form (lines 64-78) | exact |
| `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` | component (Razor view) | request-response | own existing Retired-vs-Active if/else block (lines 28-35) + own existing toggle-button form (lines 100-114) | exact |
| `QuestBoard.Service/Views/GuildMembers/Index.cshtml` | component (Razor view) | request-response | own existing single-condition Retired `if` (lines 46-51, 115-120) + card class ternary (lines 32, 101) | exact |
| `QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml` | component (Razor view) | request-response | own existing inline class-string ternary (lines 21, 66) + single-condition Retired badge (lines 38-41, 80-83) | exact |
| `QuestBoard.Service/wwwroot/css/guild-members.css` | config (CSS) | — | own existing `.character-retired` / `.retired-badge` rule blocks (lines 85-93, 149-162) | exact |
| `QuestBoard.Service/wwwroot/css/guild-members.mobile.css` | config (CSS) | — | own existing `.guild-member-row.retired` rule (lines 39-41) | exact |

**Confirmed zero-change (do not edit, listed for completeness):**

| File | Reason |
|------|--------|
| `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` | `ToggleRetirement` (lines 274-297) is a binary flip left as-is per D-03 — fix is view-side only (hide the button). `Index` sort (lines 34-42) uses `Status == Active` equality, already correct two-bucket semantics per D-05. |
| `QuestBoard.Repository/CharacterRepository.cs` | Two `OrderByDescending(c => c.Status == 0) // 0 = Active` sites (lines 18, 34) — same two-bucket semantics, need no change per D-05. |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` | Signup eligibility filters use `Status == Active` equality (not `!= Retired` inequality) — Dead auto-excluded with zero code change. |
| `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` | `CharacterStatusValues()` iterates `Enum.GetValues<CharacterStatus>()` dynamically — automatically covers `Dead` with zero test changes. |

## Pattern Assignments

### `QuestBoard.Domain/Enums/CharacterStatus.cs` (model, CRUD)

**Current full file (7 lines):**
```csharp
namespace QuestBoard.Domain.Enums;

public enum CharacterStatus
{
    Active = 0,
    Retired = 1
}
```

**Pattern to apply:** Add `Dead = 2` as a third member. No other code in this file changes. This is a pure additive enum extension — confirmed no `[Column(TypeName)]`, `HasConversion`, or check constraint exists anywhere in the mapping chain, so no migration is needed.

```csharp
namespace QuestBoard.Domain.Enums;

public enum CharacterStatus
{
    Active = 0,
    Retired = 1,
    Dead = 2
}
```

---

### `QuestBoard.Service/Views/GuildMembers/Details.cshtml` (component, request-response)

**Analog:** own existing if/else block + own existing toggle form (this file already has the two-way pattern to extend)

**Current badge pattern** (lines 30-49):
```csharp
<div class="mb-2">
    @if (Model.Status == CharacterStatus.Retired)
    {
        <span class="badge bg-secondary fs-6">
            <i class="fas fa-moon me-1"></i>Retired
        </span>
    }
    else
    {
        <span class="badge bg-success fs-6">
            <i class="fas fa-check-circle me-1"></i>Active
        </span>
    }
    @if (Model.Role == CharacterRole.Main)
    {
        <span class="badge bg-warning fs-6">
            <i class="fas fa-star me-1"></i>Main Character
        </span>
    }
</div>
```

**Pattern to apply:** True if/else here (unlike Index.cshtml) — extend to `if / else if / else` three-way chain, Dead branch first:
```csharp
@if (Model.Status == CharacterStatus.Dead)
{
    <span class="badge bg-dark fs-6">
        <i class="fas fa-skull me-1"></i>Dead
    </span>
}
else if (Model.Status == CharacterStatus.Retired)
{
    <span class="badge bg-secondary fs-6">
        <i class="fas fa-moon me-1"></i>Retired
    </span>
}
else
{
    <span class="badge bg-success fs-6">
        <i class="fas fa-check-circle me-1"></i>Active
    </span>
}
```

**Current toggle-button form pattern** (lines 64-78, inside the `@if (isOwner)` Actions card):
```csharp
<form asp-action="ToggleRetirement" method="post" class="mb-2">
    <input type="hidden" name="id" value="@Model.Id" />
    @if (isRetired)
    {
        <button type="submit" class="btn btn-success w-100">
            <i class="fas fa-undo me-2"></i>Reactivate Character
        </button>
    }
    else
    {
        <button type="submit" class="btn btn-secondary w-100">
            <i class="fas fa-moon me-2"></i>Retire Character
        </button>
    }
</form>
```

**Pattern to apply (D-03):** Wrap the entire form in a guard so it does not render when `Status == Dead`. Do not touch the `isRetired`/Active↔Retired flip logic inside — it stays exactly as-is:
```csharp
@if (Model.Status != CharacterStatus.Dead)
{
    <form asp-action="ToggleRetirement" method="post" class="mb-2">
        <input type="hidden" name="id" value="@Model.Id" />
        @if (isRetired)
        {
            <button type="submit" class="btn btn-success w-100">
                <i class="fas fa-undo me-2"></i>Reactivate Character
            </button>
        }
        else
        {
            <button type="submit" class="btn btn-secondary w-100">
                <i class="fas fa-moon me-2"></i>Retire Character
            </button>
        }
    </form>
}
```
The `Delete` form directly below (lines 79-84) is unaffected and stays outside this guard — Delete remains available for Dead characters.

**Local variable declaration** (line 7, unaffected — `isRetired` stays a simple equality check, still valid for the Retired branch of the badge/button logic):
```csharp
var isRetired = Model.Status == CharacterStatus.Retired;
```

---

### `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` (component, request-response)

**Analog:** own existing if/else block + own existing toggle form (mirrors Details.cshtml exactly, mobile markup variant)

**Current badge pattern** (lines 28-35):
```csharp
@if (isRetired)
{
    <span class="badge bg-secondary me-1 mb-1"><i class="fas fa-moon me-1"></i>Retired</span>
}
else
{
    <span class="badge bg-success me-1 mb-1"><i class="fas fa-check-circle me-1"></i>Active</span>
}
```

**Pattern to apply:** Same three-way chain shape as Details.cshtml, but note this file uses the pre-computed `isRetired` bool rather than inline `Model.Status ==` — use `Model.Status == CharacterStatus.Dead` for the new branch (no precomputed `isDead` var exists, follow existing style of computing `isRetired` at top or inline check directly — inline is simplest since this is a single new branch):
```csharp
@if (Model.Status == CharacterStatus.Dead)
{
    <span class="badge bg-dark me-1 mb-1"><i class="fas fa-skull me-1"></i>Dead</span>
}
else if (isRetired)
{
    <span class="badge bg-secondary me-1 mb-1"><i class="fas fa-moon me-1"></i>Retired</span>
}
else
{
    <span class="badge bg-success me-1 mb-1"><i class="fas fa-check-circle me-1"></i>Active</span>
}
```

**Current toggle-button form pattern** (lines 100-114, inside `@if (isOwner)` Owner Actions card):
```csharp
<form asp-action="ToggleRetirement" method="post" class="mb-2">
    <input type="hidden" name="id" value="@Model.Id" />
    @if (isRetired)
    {
        <button type="submit" class="btn btn-success w-100">
            <i class="fas fa-undo me-2"></i>Reactivate Character
        </button>
    }
    else
    {
        <button type="submit" class="btn btn-secondary w-100">
            <i class="fas fa-moon me-2"></i>Retire Character
        </button>
    }
</form>
```

**Pattern to apply (D-03):** Identical guard pattern as Details.cshtml — wrap the form in `@if (Model.Status != CharacterStatus.Dead) { ... }`. Delete form (lines 115-120) stays unguarded/unaffected.

---

### `QuestBoard.Service/Views/GuildMembers/Index.cshtml` (component, request-response)

**Analog:** own existing single-condition `if` (no else) — **this file's pattern is structurally different from Details.cshtml.** Active characters currently render zero badge here.

**Current badge pattern, MyCharacters section** (lines 46-51):
```csharp
@if (character.Status == CharacterStatus.Retired)
{
    <div class="retired-badge">
        <i class="fas fa-moon"></i> Retired
    </div>
}
```

**Current badge pattern, OtherCharacters section** (lines 115-120) — identical shape, no Main-badge sibling in this section:
```csharp
@if (character.Status == CharacterStatus.Retired)
{
    <div class="retired-badge">
        <i class="fas fa-moon"></i> Retired
    </div>
}
```

**Pattern to apply (both sections — do NOT convert to if/else, add a second independent `if`):**
```csharp
@if (character.Status == CharacterStatus.Dead)
{
    <div class="dead-badge">
        <i class="fas fa-skull"></i> Dead
    </div>
}
@if (character.Status == CharacterStatus.Retired)
{
    <div class="retired-badge">
        <i class="fas fa-moon"></i> Retired
    </div>
}
```
**Critical constraint:** Do not merge into `if/else if` — Active must continue to render no badge at all. Converting to if/else-if would require an explicit `else` producing nothing, which is fragile; keep as two independent sibling `if` blocks exactly as the Retired/Main pattern already demonstrates at lines 46-57.

**Current card-class ternary, MyCharacters section** (line 32):
```csharp
<div class="character-card @(character.Status == CharacterStatus.Retired ? "character-retired" : "")">
```

**Current card-class ternary, OtherCharacters section** (line 101) — identical:
```csharp
<div class="character-card @(character.Status == CharacterStatus.Retired ? "character-retired" : "")">
```

**Pattern to apply (both sections):** Extend the single ternary to a nested ternary, Dead checked first:
```csharp
<div class="character-card @(character.Status == CharacterStatus.Dead ? "character-dead" : character.Status == CharacterStatus.Retired ? "character-retired" : "")">
```

---

### `QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml` (component, request-response)

**Analog:** own existing inline class-string ternary + own existing single-condition badge `if`

**Current row-class ternary, MyCharacters section** (line 21):
```csharp
<div class="guild-member-row d-flex align-items-center@(character.Status == CharacterStatus.Retired ? " retired" : "")" onclick="window.location.href='@Url.Action("Details", new { id = character.Id })'">
```

**Current row-class ternary, OtherCharacters section** (line 66) — identical:
```csharp
<div class="guild-member-row d-flex align-items-center@(character.Status == CharacterStatus.Retired ? " retired" : "")" onclick="window.location.href='@Url.Action("Details", new { id = character.Id })'">
```

**Pattern to apply (both sections):** Nested ternary appending a space-prefixed modifier class, mirroring the naming precedent already set by `.retired` on this file's `.guild-member-row`:
```csharp
<div class="guild-member-row d-flex align-items-center@(character.Status == CharacterStatus.Dead ? " dead" : character.Status == CharacterStatus.Retired ? " retired" : "")" onclick="window.location.href='@Url.Action("Details", new { id = character.Id })'">
```

**Current badge pattern, MyCharacters section** (lines 34-41, alongside the Main badge):
```csharp
<div>
    @if (character.Role == CharacterRole.Main)
    {
        <span class="badge bg-warning text-dark"><i class="fas fa-star me-1"></i>Main</span>
    }
    @if (character.Status == CharacterStatus.Retired)
    {
        <span class="badge bg-secondary">Retired</span>
    }
</div>
```

**Current badge pattern, OtherCharacters section** (lines 79-84, no Main-badge sibling here):
```csharp
<div>
    @if (character.Status == CharacterStatus.Retired)
    {
        <span class="badge bg-secondary">Retired</span>
    }
</div>
```

**Pattern to apply (both sections — again, independent sibling `if`, not if/else):**
```csharp
@if (character.Status == CharacterStatus.Dead)
{
    <span class="badge bg-dark">Dead</span>
}
@if (character.Status == CharacterStatus.Retired)
{
    <span class="badge bg-secondary">Retired</span>
}
```

---

### `QuestBoard.Service/wwwroot/css/guild-members.css` (config, desktop)

**Analog:** own existing `.character-retired` card-state rule + `.retired-badge` absolute-positioned badge rule

**Current `.character-retired` rule** (lines 85-93):
```css
.guild-members-page .character-card.character-retired {
    opacity: 0.7;
    border-color: rgba(108, 117, 125, 0.5);
}

.guild-members-page .character-card.character-retired:hover {
    border-color: rgba(108, 117, 125, 0.8);
    box-shadow: 0 12px 35px rgba(108, 117, 125, 0.3);
}
```

**Current `.retired-badge` rule** (lines 149-162):
```css
.guild-members-page .retired-badge {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    background: rgba(108, 117, 125, 0.95);
    color: white;
    padding: 0.25rem 0.75rem;
    border-radius: 20px;
    font-size: 0.75rem;
    font-weight: 600;
    backdrop-filter: blur(5px);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
    z-index: 10;
}
```

**Pattern to apply:** Add fresh, parallel `.character-dead` and `.dead-badge` rule blocks (per RESEARCH.md's Open Question 1 recommendation — write fresh rather than composing/extending `.character-retired`). Insert immediately after the `.character-retired` block (after line 93) and after the `.retired-badge` block (after line 162) respectively, to keep Retired/Dead rule pairs visually grouped in the stylesheet:
```css
.guild-members-page .character-card.character-dead {
    opacity: 0.5;
    border-color: rgba(33, 37, 41, 0.6);
    filter: grayscale(60%);
}

.guild-members-page .character-card.character-dead:hover {
    border-color: rgba(33, 37, 41, 0.9);
    box-shadow: 0 12px 35px rgba(33, 37, 41, 0.4);
}
```
```css
.guild-members-page .dead-badge {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    background: rgba(33, 37, 41, 0.95);
    color: white;
    padding: 0.25rem 0.75rem;
    border-radius: 20px;
    font-size: 0.75rem;
    font-weight: 600;
    backdrop-filter: blur(5px);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
    z-index: 10;
}
```
Exact opacity/grayscale/color values are implementation discretion (CONTEXT.md) — the above values satisfy "visually distinguishable from Retired at a glance" (darker/near-black tone + grayscale filter vs. Retired's mid-gray, no filter).

---

### `QuestBoard.Service/wwwroot/css/guild-members.mobile.css` (config, mobile)

**Analog:** own existing `.guild-member-row.retired` modifier rule

**Current rule** (lines 39-41):
```css
.guild-member-row.retired {
    opacity: 0.7;
}
```

**Pattern to apply:** Add a parallel `.guild-member-row.dead` modifier immediately after (naming convention matches the `" dead"` class-string literal used in the Index.Mobile.cshtml ternary above — this file already has bare `.retired`/`.dead` modifier names, not `character-retired`/`character-dead` like the desktop stylesheet, since the two files independently established their own naming convention):
```css
.guild-member-row.retired {
    opacity: 0.7;
}

.guild-member-row.dead {
    opacity: 0.5;
    filter: grayscale(60%);
}
```

---

## Shared Patterns

### Enum-driven dropdown (no view change needed)
**Source:** `QuestBoard.Service/Views/GuildMembers/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` (all four confirmed identical, not read in full since RESEARCH.md already verified this and no markup edit is needed)
```html
<select asp-for="Status" asp-items="Html.GetEnumSelectList<CharacterStatus>()" class="form-select"></select>
```
**Apply to:** Nothing — these four files need zero edits. `Html.GetEnumSelectList<CharacterStatus>()` auto-populates `Dead` the moment the enum member exists. Listed here only so the planner doesn't schedule unnecessary edits to these files.

### Badge visual convention (Bootstrap `bg-*` + Font Awesome icon)
**Source:** every badge in Details.cshtml/Index.cshtml (see excerpts above) — established convention is `badge bg-{semantic-color} + <i class="fas fa-{icon}">`
**Apply to:** All 4 view files' new Dead badge markup — use `bg-dark` + `fa-skull` consistently (D-04), matching the existing `bg-secondary`+`fa-moon` (Retired) / `bg-success`+`fa-check-circle` (Active) / `bg-warning`+`fa-star` (Main) convention.

### CSS naming convention split by desktop/mobile stylesheet
**Source:** `guild-members.css` uses `.character-retired` (compound class on `.character-card`); `guild-members.mobile.css` uses bare `.retired` (compound class on `.guild-member-row`)
**Apply to:** New Dead classes must follow each file's own existing naming convention rather than a single global name — `.character-dead` in `guild-members.css`, `.dead` in `guild-members.mobile.css`. Do not introduce a third, different naming scheme.

### Toggle-button visibility gating (view-only fix, no controller change)
**Source:** `Details.cshtml` lines 64-78, `Details.Mobile.cshtml` lines 100-114 — both wrap the `ToggleRetirement` form
**Apply to:** Both files — wrap the existing form in `@if (Model.Status != CharacterStatus.Dead) { ... }`. The POST action `GuildMembersController.ToggleRetirement` (lines 274-297) requires **no code change**; it is a closed loop that is simply never invoked for Dead characters once the button doesn't render. Do not add server-side rejection logic to the action — CONTEXT.md D-03 scopes this as a view-only fix.

### "No else" badge sites vs. true if/else badge sites
**Source:** Confirmed via direct read — `Details.cshtml`/`Details.Mobile.cshtml` use true if/else (Active gets an explicit badge); `Index.cshtml`/`Index.Mobile.cshtml` use single-condition `if` with no else (Active gets no badge)
**Apply to:** This distinction governs which pattern shape (if/else-if/else vs. two independent sibling `if`s) each file needs. Do not homogenize all 4 files to the same shape — that would either introduce a new "Active" badge in the Index views (scope violation) or silently lose the Active-badge display in Details views.

## No Analog Found

None. Every file in scope already contains the exact binary pattern being extended to three-way; there was no need to search elsewhere in the codebase for a donor pattern.

## Metadata

**Analog search scope:** `QuestBoard.Domain/Enums/`, `QuestBoard.Service/Views/GuildMembers/`, `QuestBoard.Service/wwwroot/css/`, `QuestBoard.Service/Controllers/Characters/`, `QuestBoard.Repository/CharacterRepository.cs`, `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` — plus a full-codebase grep for `CharacterStatus\.(Active|Retired)` to confirm no additional touchpoint exists beyond what CONTEXT.md/RESEARCH.md already identified.
**Files scanned:** 10 read in full + 1 codebase-wide grep (16 total hits, all accounted for: 4 view files being edited, 3 confirmed zero-change source files, remainder are `.planning/` docs)
**Pattern extraction date:** 2026-07-06
