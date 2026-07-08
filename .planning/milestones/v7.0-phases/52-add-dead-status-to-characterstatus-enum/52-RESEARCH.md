# Phase 52: Add Dead status to CharacterStatus enum - Research

**Researched:** 2026-07-06
**Domain:** ASP.NET Core 10 MVC enum extension (additive C# enum value + Razor view branch updates)
**Confidence:** HIGH

## Summary

This phase adds a third value (`Dead = 2`) to the existing `CharacterStatus` enum (`Active = 0`, `Retired = 1`). The change is purely additive at the data layer — `CharacterEntity.Status` is a plain `int` column with no check constraint, `[Column(TypeName)]`, or enum-backed SQL type, so **no EF Core migration is required**. This claim from CONTEXT.md is confirmed against current source.

The bulk of the work is in Razor views and controller/CSS touchpoints that currently branch on status with **binary** (two-way) logic that will now see a third value flow through it. Re-verification against current source found the touchpoints in CONTEXT.md are accurate, but surfaced two nuances CONTEXT.md did not fully capture:

1. The "binary if/else" framing in CONTEXT.md's Established Patterns section is only fully accurate for `Details.cshtml`/`Details.Mobile.cshtml` (true if/else: Retired-badge vs. Active-badge, no bare "any non-Retired-is-Active" gap). In `Index.cshtml` and `Index.Mobile.cshtml`, the badge markup is a single **conditional `if` with no `else`** — Active characters get *no* badge at all today. Adding Dead here means adding a **second independent `if`**, not converting a two-branch `if/else` into a three-branch one. The distinction matters for how the planner scopes the diff.
2. `CharacterRepository.cs` (lines 18 and 34) has two additional SQL-level `OrderByDescending(c => c.Status == 0)` sorts feeding into `GuildMembersController.Index`'s already-known in-memory re-sort. These weren't called out in CONTEXT.md's Integration Points list. They require zero code changes (same two-bucket "Active vs. not-Active" semantics as D-05), but the planner/verifier should know they exist so a future contributor doesn't mistake them for missed touchpoints.

No other switch/branch/equality call sites on `CharacterStatus` exist anywhere else in the codebase — confirmed via a full-codebase grep across `*.cs` files (see Common Pitfalls and Architectural Responsibility Map below for the complete site list).

**Primary recommendation:** Add `Dead = 2` to the enum, then touch exactly 6 code files (4 Razor views + `GuildMembersController.cs`'s toggle-button-hiding is actually view-side only + 2 CSS files) plus 0 test files (the enum-cast unit test iterates dynamically). No migration, no ViewModel change, no dropdown markup change.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Enum value definition | Domain | — | `CharacterStatus` lives in `QuestBoard.Domain/Enums/`; single source of truth consumed by Repository (int cast) and Service (view binding) |
| Status persistence (int column) | Repository | — | `CharacterEntity.Status` is `int`; AutoMapper casts at the Entity↔Domain boundary; no schema change needed for additive enum values |
| Status selection (Create/Edit forms) | Frontend Server (Razor view) | — | `Html.GetEnumSelectList<CharacterStatus>()` auto-populates; zero markup change |
| Status badge rendering (Details/Index) | Frontend Server (Razor view) | — | Conditional badge markup per view; needs a third branch/second `if` per view |
| Toggle-button visibility gating | Frontend Server (Razor view) | API/Backend (unchanged) | D-03 requires hiding the button in the view when `Status == Dead`; the `ToggleRetirement` POST action itself needs no change since the button that calls it simply won't render |
| List/grid sort ordering | API/Backend (LINQ, in-memory + SQL) | — | Two sort sites: `CharacterRepository` (SQL `OrderByDescending`) and `GuildMembersController.Index` (in-memory `ThenByDescending`) — both already two-bucket (Active vs. not), need zero change per D-05 |
| Quest signup eligibility | API/Backend | — | `QuestController` filters via `Status == Active` equality (not `!= Retired` inequality) — Dead auto-excluded, zero change needed |
| Visual styling (badge color/icon, card dimming) | CDN/Static (CSS) | — | New `.character-dead` class + badge color/icon change in `guild-members.css`; new `.retired`-analog class in `guild-members.mobile.css` |

## Standard Stack

No new packages, libraries, or dependencies are introduced by this phase. This is a pure code-and-markup change within the existing stack.

### Core (unchanged, confirmed in place)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC | 10.0.9 [VERIFIED: .planning/codebase/STACK.md] | Razor views, `Html.GetEnumSelectList` tag helper | Already in use throughout Views/GuildMembers |
| Entity Framework Core | 10.0.9 [VERIFIED: .planning/codebase/STACK.md] | ORM; `CharacterEntity.Status` int column mapping | Already in use; confirms no migration path needed for enum-only changes |
| AutoMapper | 16.1.1 [VERIFIED: .planning/codebase/STACK.md] | `(int)`/`(CharacterStatus)` cast at Entity↔Domain boundary in `EntityProfile.cs` | Already handles the cast pattern that Dead will flow through unchanged |
| Bootstrap | 5.3.0 (CDN) [VERIFIED: `_Layout.cshtml:12`] | `bg-dark` badge class for the Dead badge (D-04) | Already the badge-color convention (`bg-secondary`, `bg-success`, `bg-warning` in use) |
| Font Awesome | 6.4.0 Free (CDN) [VERIFIED: `_Layout.cshtml:13`; fontawesome.com/icons/skull] | `fa-skull` icon for the Dead badge (D-04) | `fas fa-skull` is part of the Free Solid icon set, available since FA5, present in 6.4.0 |

### Package Legitimacy Audit

**Not applicable** — this phase installs no external packages (no `npm install` / `dotnet add package` / `pip install`). Skipping the Package Legitimacy Gate protocol; it only applies to phases that add new dependencies.

## Architecture Patterns

### System Architecture Diagram

```
[Create/Edit form]                    [Details/Index views]
  Html.GetEnumSelectList<CharacterStatus>()   badge rendering (per-view conditional)
        │                                            │
        ▼                                            ▼
  CharacterViewModel.Status (enum)          CharacterViewModel.Status (enum)
        │                                            │
        ▼                                            │
  POST Create/Edit ──► GuildMembersController        │
        │                                            │
        ▼                                            │
  AutoMapper (int)cast ──► CharacterEntity.Status(int)│
        │                                            │
        ▼                                            │
  EF Core SaveChanges (no migration — plain int col)  │
        │                                            │
        ▼                                            │
  CharacterRepository query (OrderByDescending Status==0)
        │                                            │
        ▼                                            │
  AutoMapper (CharacterStatus)cast ──► Character (domain model)
        │                                            │
        ▼                                            ▼
  GuildMembersController.Index in-memory re-sort ──► View(viewModel)
        (ThenByDescending Status==Active)             │
                                                       ▼
                                          Razor badge/toggle-button rendering
                                          (this phase's edit surface)
```

Data flow for the one path that changes behavior (ToggleRetirement) is a closed loop: Details view POSTs to `ToggleRetirement` → controller flips Active↔Retired (unchanged code) → redirects back to Details → view re-renders. This phase's fix is entirely upstream of the POST: the button is hidden in the view when `Status == Dead`, so the unchanged binary-flip action method is simply never invoked for Dead characters.

### Recommended Project Structure

No new files or folders. All changes are edits to existing files:

```
QuestBoard.Domain/Enums/CharacterStatus.cs          # add Dead = 2
QuestBoard.Service/Views/GuildMembers/
├── Details.cshtml                                   # 3rd badge branch + hide toggle button when Dead
├── Details.Mobile.cshtml                             # 3rd badge branch + hide toggle button when Dead
├── Index.cshtml                                       # 2nd independent badge `if` + character-dead class
└── Index.Mobile.cshtml                                # 2nd independent badge `if` + dead-analog row class
QuestBoard.Service/wwwroot/css/
├── guild-members.css                                  # .character-dead card class + dead badge color (if not solely inline bg-dark)
└── guild-members.mobile.css                           # dead-analog row class (parallel to .guild-member-row.retired)
```

### Pattern 1: Additive enum value with dynamic-iteration test coverage
**What:** `CharacterStatusValues()` in `EntityProfileEnumCastTests.cs` calls `Enum.GetValues<CharacterStatus>()` and generates one `TheoryData` entry per defined value — it does not hardcode `Active`/`Retired`.
**When to use:** Any time a new enum member is added to an enum already covered by this dynamic-iteration pattern.
**Example:**
```csharp
// Source: QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs:80-88
private static TheoryData<TEnum> ToTheoryData<TEnum>() where TEnum : struct, Enum
{
    var data = new TheoryData<TEnum>();
    foreach (var value in Enum.GetValues<TEnum>())
    {
        data.Add(value);
    }
    return data;
}
```
Adding `Dead = 2` requires zero changes to this test file — the new value is picked up automatically and its int-cast round-trip is verified for free.

### Pattern 2: `Html.GetEnumSelectList<T>()` for enum-backed dropdowns
**What:** Auto-generates `<option>` elements from all defined enum values using the raw member name as display text (no `[Display(Name=...)]` present on `CharacterStatus`, so `Dead` will render as literal text "Dead").
**When to use:** Already in use in `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` — confirmed identical across all four files.
**Example:**
```html
<!-- Source: QuestBoard.Service/Views/GuildMembers/Edit.cshtml:58 (identical in Create.cshtml, both Mobile variants) -->
<select asp-for="Status" asp-items="Html.GetEnumSelectList<CharacterStatus>()" class="form-select"></select>
```
[CITED: gunnarpeipman.com/aspnet-core-enum-to-select-list — confirms default behavior displays enum member names as-is when no `[Display]` attribute is present]

### Anti-Patterns to Avoid
- **Converting the toggle button to tri-state/cyclical:** D-03 explicitly rejects this. The button stays a simple Active↔Retired flip; it is only ever hidden (not relabeled or cycled) when `Status == Dead`.
- **Introducing a three-tier sort comparator:** D-05 explicitly rejects this. Do not add `ThenByDescending(c => c.Status == CharacterStatus.Retired)` as a second tier — Dead and Retired stay in the same "not Active" bucket, sorted alphabetically by name within that bucket.
- **Assuming Index.cshtml/Index.Mobile.cshtml badge logic is if/else:** It is not — Active characters currently render zero badges in the grid/list views (only Retired and Main get badges). Adding Dead means adding a second independent conditional `if (Status == Dead) { ... }`, not restructuring an existing if/else into if/else-if/else.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Enum-to-dropdown population | Manual `<option>` list with `(int)CharacterStatus.Dead` hardcoded values | `Html.GetEnumSelectList<CharacterStatus>()` (already in place) | Already generic; adding the enum value is sufficient, no markup touch needed |
| Enum int round-trip test coverage | A new hardcoded unit test asserting `((int)CharacterStatus.Dead) == 2` | Rely on the existing dynamic `Enum.GetValues<TEnum>()` iteration in `EntityProfileEnumCastTests.cs` | Already covers every defined enum value with zero new test code |

**Key insight:** This phase's "don't hand-roll" opportunities are less about avoiding third-party libraries and more about *not touching code that already generalizes correctly*. The two biggest traps are (1) writing a new dropdown/test when the existing generic mechanism already covers the new value, and (2) writing a migration when none is needed.

## Common Pitfalls

### Pitfall 1: Missing the Index.cshtml / Index.Mobile.cshtml "no else" badge gap
**What goes wrong:** A developer assumes all 4 status-branch view files use if/else (per a surface reading of CONTEXT.md's "binary if/else" phrasing) and mechanically converts each `if (Retired) {...} else {...}` into a three-branch chain. In Index.cshtml/Index.Mobile.cshtml there is no `else` — Active characters get no badge. Blindly adding an `else` branch here would newly display an "Active" badge on every active character in the grid, a scope change not requested by CONTEXT.md.
**Why it happens:** CONTEXT.md's summary phrase "binary if/else in 4 view files" is accurate for Details/Details.Mobile but imprecise for Index/Index.Mobile, which are single-condition `if` blocks with no `else`.
**How to avoid:** Read each of the 4 files individually before editing. Details.cshtml/Details.Mobile.cshtml: convert `if (Retired) {...} else {...}` to `if (Dead) {...} else if (Retired) {...} else {...}`. Index.cshtml/Index.Mobile.cshtml: add a **second, independent** `if (Status == Dead) { ... }` block alongside the existing `if (Status == Retired) { ... }` block — do not merge them into an if/else-if chain unless you also verify it doesn't change Active's no-badge behavior.
**Warning signs:** If the diff for Index.cshtml introduces a badge that renders for `Status == Active`, that's a scope violation of the phase boundary ("no new capability", CONTEXT.md domain statement).

### Pitfall 2: Overlooking `CharacterRepository.cs` sort sites during pitfall/regression review
**What goes wrong:** A verifier greps only for `CharacterStatus.Active`/`CharacterStatus.Retired` symbol usage (as CONTEXT.md's Integration Points table does) and misses the two `Status == 0` magic-number comparisons in `CharacterRepository.cs` lines 18 and 34, which use the same "0 = Active" two-bucket logic but without the enum symbol.
**Why it happens:** These sort clauses predate a stricter enum-usage convention and use raw `0`/`1` int literals with a comment instead of the enum member — they don't show up in a `CharacterStatus.` grep.
**How to avoid:** When auditing "does this sort need a third tier," search for `Status == 0`, `Status == 1`, and `c.Status ==` patterns in addition to `CharacterStatus.` symbol references. Confirmed in this research: both sites use the same "Active vs. not" two-bucket comparison as the controller-level sort, so per D-05 they need **no change** — but a plan/verification step should explicitly state this rather than silently skip them.
**Warning signs:** If a future contributor "fixes" these lines to reference the enum symbol as part of unrelated cleanup, verify the boolean bucket semantics (`== 0` / Active-first-descending) are preserved exactly, since these are SQL-translated LINQ expressions (EF Core translates `c.Status == 0` correctly, but ensure any refactor keeps the SQL-translatable shape).

### Pitfall 3: Assuming an EF Core migration is required for enum changes in general
**What goes wrong:** A developer reflexively runs `dotnet ef migrations add` out of habit whenever a C# enum changes, without checking whether the column is enum-typed at the database level.
**Why it happens:** In many EF Core setups, enums can be mapped via `HasConversion` to string columns or via a custom converter with a check constraint, in which case adding a new enum member is not always risk-free without migration review.
**How to avoid:** Confirmed for this specific case: `CharacterEntity.Status` is `public int Status { get; set; } = 0;` — a bare `int` with a comment (`// CharacterStatus enum stored as int`), no `[Column(TypeName)]`, no EF `HasConversion`, no CHECK constraint found in the migration history (`QuestBoard.Repository/Migrations/` — none of the Character-related migrations, including `20260705183646_AddGroupIdToCharacters.cs`, touch a `Status` column or add a constraint on it). Adding `Dead = 2` is purely additive against an already-generic int column. **No migration needed** — this CONTEXT.md claim is verified accurate.
**Warning signs:** If `dotnet ef migrations add` is run for this phase and produces a migration with actual `AlterColumn`/`AddCheckConstraint` operations (not an empty no-op migration), something about this analysis was wrong — stop and investigate before applying.

## Code Examples

### Details.cshtml / Details.Mobile.cshtml — extending if/else to 3-way branch (D-04)
```csharp
// Current (Details.cshtml:31-42, Details.Mobile.cshtml:28-35) — badge rendering
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

// Recommended shape after this phase (D-04: dark badge + fa-skull for Dead)
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

### Details.cshtml / Details.Mobile.cshtml — hiding the toggle button when Dead (D-03)
```csharp
// Current computed flag (Details.cshtml:7, Details.Mobile.cshtml:7)
var isRetired = Model.Status == CharacterStatus.Retired;

// Recommended: gate the entire <form asp-action="ToggleRetirement"> block
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
This requires no change to `GuildMembersController.ToggleRetirement` (`QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:276-297`) — the binary flip logic stays exactly as-is; it's simply never triggered for Dead characters because the button won't render.

### Index.cshtml — adding an independent second `if` (not else-if) for Dead badge (D-04)
```csharp
// Current (Index.cshtml:46-51) — single conditional, no else, Active gets no badge
@if (character.Status == CharacterStatus.Retired)
{
    <div class="retired-badge">
        <i class="fas fa-moon"></i> Retired
    </div>
}

// Recommended: second, independent conditional alongside it (order matters for visual stacking with main-badge)
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
Also update the card class expression at Index.cshtml:32/101 from a single ternary to a value that supports the new state:
```csharp
// Current
<div class="character-card @(character.Status == CharacterStatus.Retired ? "character-retired" : "")">

// Recommended
<div class="character-card @(character.Status == CharacterStatus.Dead ? "character-dead" : character.Status == CharacterStatus.Retired ? "character-retired" : "")">
```

### guild-members.css — new `.character-dead` class mirroring `.character-retired` (D-04)
```css
/* Source: QuestBoard.Service/wwwroot/css/guild-members.css:85-93 (existing pattern to mirror) */
.guild-members-page .character-card.character-retired {
    opacity: 0.7;
    border-color: rgba(108, 117, 125, 0.5);
}

.guild-members-page .character-card.character-retired:hover {
    border-color: rgba(108, 117, 125, 0.8);
    box-shadow: 0 12px 35px rgba(108, 117, 125, 0.3);
}

/* New class for Dead — must be visually distinct from Retired (D-04 hard requirement) */
.guild-members-page .character-card.character-dead {
    opacity: 0.5;
    border-color: rgba(33, 37, 41, 0.6);
    filter: grayscale(60%);
}

.guild-members-page .character-card.character-dead:hover {
    border-color: rgba(33, 37, 41, 0.9);
    box-shadow: 0 12px 35px rgba(33, 37, 41, 0.4);
}

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
Exact opacity/grayscale intensity is left to implementation discretion per CONTEXT.md — the values above are a starting point that satisfies "visually distinguishable at a glance" without needing user re-confirmation.

## State of the Art

Not applicable — this is a small additive change to an existing, actively-maintained internal enum with no external ecosystem "state of the art" to track. No deprecated APIs are involved.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | Exact CSS opacity/grayscale/color values proposed for `.character-dead` and `.dead-badge` in Code Examples | Code Examples | Low — CONTEXT.md explicitly leaves exact intensity to implementation discretion; any visually-distinct-from-Retired value satisfies the requirement, so this is a starting suggestion, not a locked value |

**All other claims in this research were verified against current source code via direct file reads and grep, or confirmed via WebSearch cross-referenced with official-style documentation (Font Awesome icon library, ASP.NET Core enum-to-select-list behavior).** No user confirmation is needed for the core technical claims (no migration needed, touchpoint list, enum-cast test coverage) — these are code facts, not judgment calls.

## Open Questions (RESOLVED)

1. **Should `.character-dead` reuse `.character-retired`'s CSS structure or be written fresh? (RESOLVED)**
   - What we know: CONTEXT.md explicitly marks this as Claude's Discretion — "Whether `character-dead` reuses the existing `.character-retired` selector's structure/specificity or is written fresh."
   - What's unclear: No further constraint given.
   - Recommendation: Write a fresh, parallel `.character-dead` rule block (as shown in Code Examples) rather than trying to compose/extend `.character-retired` via multiple classes on the same element — this avoids specificity conflicts if a character is ever in an ambiguous transitional state during a template refactor, and keeps the two states independently tunable.
   - **RESOLVED:** 52-01-PLAN.md implements this recommendation verbatim — a fresh `.character-dead` rule block in `guild-members.css`, independently tunable from `.character-retired`.

2. **Does `Index.Mobile.cshtml` need a `.guild-member-row.dead` CSS class parallel to `.guild-member-row.retired`? (RESOLVED)**
   - What we know: `guild-members.mobile.css:39` has `.guild-member-row.retired { opacity: 0.7; }`. The Razor markup builds the class string inline: `class="guild-member-row d-flex align-items-center@(character.Status == CharacterStatus.Retired ? " retired" : "")"`.
   - What's unclear: CONTEXT.md's D-04 says the `character-dead` CSS class is specifically for "Card/row class: new `character-dead` CSS class (parallel to the existing `character-retired` class)" but only names `character-retired`/`character-dead` (the desktop Index.cshtml naming), not the mobile-specific `.guild-member-row.retired` naming convention.
   - Recommendation: Follow the same naming convention already established per-file rather than forcing a single global class name — i.e., add `.guild-member-row.dead` in `guild-members.mobile.css` (parallel to the existing `.retired` modifier class there), while using `.character-dead` in `guild-members.css` (parallel to `.character-retired` there). This is the naming pattern precedent already set by how Retired is styled differently per view file today.
   - **RESOLVED:** 52-01-PLAN.md implements this recommendation verbatim — `.guild-member-row.dead` in `guild-members.mobile.css`, `.character-dead` in `guild-members.css`.

## Environment Availability

Skipped — this phase has no external tool/service/runtime dependencies beyond the existing project stack (no new package installs, no new external services). All required tooling (`.NET 10 SDK`, EF Core CLI, SQL Server) is already verified present per `.planning/codebase/STACK.md` and prior phases' successful migrations.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 3.2.2 [VERIFIED: .planning/codebase/STACK.md] |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (standard xUnit test project, no custom config file) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~EntityProfileEnumCastTests"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

This phase has no mapped requirement IDs (unmapped backlog item per phase description). The following table maps CONTEXT.md's locked decisions (D-01 through D-05) to verification approach instead:

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01/D-02 | `Dead` selectable in Create/Edit dropdowns | manual/smoke (existing `Html.GetEnumSelectList` mechanism, no new test needed) | — | N/A — no markup change means no new automated coverage needed beyond existing enum-cast test |
| Enum cast integrity | `(int)Dead` round-trips through AutoMapper cast | unit (automatic) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterStatus_CastRoundTrips"` | Yes — `EntityProfileEnumCastTests.cs` already covers this dynamically |
| D-03 | Toggle button hidden when `Status == Dead` | manual/smoke — no existing controller/view test harness for this Razor conditional found in the codebase | Manual verification via browser: view a Dead character's Details page, confirm no Retire/Reactivate button renders | ❌ Wave 0 — no integration test infrastructure exists for Razor view conditional rendering in this project |
| D-04 | Dead badge renders with `bg-dark`/`fa-skull` in all 4 view files, `character-dead` class visually distinct | manual/smoke | Manual verification via browser across Details, Details.Mobile, Index, Index.Mobile | ❌ Wave 0 — same as above, no view-rendering test infrastructure |
| D-05 | Sort order unchanged (Dead falls in "not Active" bucket) | unit — confirmed no existing test asserts sort order | Not applicable — no test to run or extend | Confirmed absent — grepped `CharacterRepositoryTests.cs` (which exists, covers group-scoping only) for `Status`/`sort`-related assertions: none found. No test currently asserts sort ordering, so D-05's "no code change" claim carries zero regression risk against existing coverage. Not a Wave 0 gap requiring new tests — D-05 explicitly declines a new sort tier, so there is no new behavior to test. |
| Quest signup exclusion | Dead characters excluded from quest signup eligibility | unit — confirmed no existing test asserts this filter | Not applicable — no test to run or extend | Confirmed absent — grepped both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests` for `CharacterStatus.Active`/`Status.*Active`/`Retired` patterns tied to `QuestController`: no matches. No existing test covers the `Status == Active` signup-eligibility filter, so there is nothing to break and no automated regression check to add for this phase specifically (CONTEXT.md confirms zero code change here). |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterStatus_CastRoundTrips"` (fast, verifies the enum addition doesn't break the cast contract)
- **Per wave merge:** `dotnet test` (full suite — this is a small phase, likely a single wave)
- **Phase gate:** Full suite green before `/gsd:verify-work`; manual browser verification of D-03/D-04 (button hiding, badge rendering) since no automated Razor-view-rendering test harness exists in this codebase

### Wave 0 Gaps
- No automated test exists for "toggle button hidden when Dead" (D-03) — this is a Razor conditional with no existing MVC integration test pattern found for view-level conditional rendering in `QuestBoard.UnitTests`. Recommend manual/smoke verification only; do not build new integration test infrastructure for a single small phase unless the planner determines otherwise.
- No automated test exists for badge rendering (D-04) for the same reason as above — manual/smoke verification recommended, no new test infrastructure needed for this phase's scope.
- Sort order (D-05) and quest-signup exclusion have no existing automated coverage, but since both are explicitly "no code change" decisions in CONTEXT.md, this is not a gap this phase needs to fill — confirmed via grep, not merely assumed.

## Security Domain

Not applicable for this phase. `security_enforcement` was not found configured in `.planning/config.json` as explicitly `false`, but this phase introduces no new input surface, authentication/session logic, cryptography, or access-control change — it extends an existing enum value flowing through an already-authorized, already-validated form field (`Status` is bound via `[Authorize]`-gated `GuildMembersController` actions that already enforce ownership checks (`character.OwnerId != currentUser.Id`) unrelated to this phase). No new ASVS categories are triggered.

## Sources

### Primary (HIGH confidence — direct codebase verification)
- `QuestBoard.Domain/Enums/CharacterStatus.cs` — confirmed current enum state (`Active = 0, Retired = 1`)
- `QuestBoard.Repository/Entities/CharacterEntity.cs:23` — confirmed `Status` is plain `int`, no schema constraint
- `QuestBoard.Repository/Automapper/EntityProfile.cs:88-103` — confirmed `(int)`/`(CharacterStatus)` cast pattern at Entity↔Domain boundary
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` (full file, lines 1-100, 260-297) — confirmed Index sort, Create default, ToggleRetirement binary flip
- `QuestBoard.Repository/CharacterRepository.cs` (full file) — discovered additional `Status == 0` sort sites not in CONTEXT.md
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (lines 322, 402, 455, 545) — confirmed equality-based `Status == Active` filtering, Dead auto-excluded
- `QuestBoard.Service/Views/GuildMembers/Details.cshtml`, `Details.Mobile.cshtml`, `Index.cshtml`, `Index.Mobile.cshtml` — confirmed exact badge-rendering shape per file, found the if/no-else distinction in Index views
- `QuestBoard.Service/Views/GuildMembers/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` — confirmed identical `Html.GetEnumSelectList<CharacterStatus>()` usage, no markup change needed
- `QuestBoard.Service/wwwroot/css/guild-members.css`, `guild-members.mobile.css` — confirmed exact `.character-retired`/`.retired-badge`/`.guild-member-row.retired` CSS to mirror
- `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` — confirmed dynamic `Enum.GetValues<TEnum>()` test iteration covers new enum values automatically
- `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — confirmed exists (covers group-scoping only), no sort-order assertions found
- `QuestBoard.Repository/Migrations/` (directory listing) — confirmed no prior migration touches a `Status` column or constraint on `CharacterEntity`
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:12-13` — confirmed Bootstrap 5.3.0 and Font Awesome 6.4.0 CDN versions in use

### Secondary (MEDIUM confidence — WebSearch cross-referenced)
- [Font Awesome — Skull icon](https://fontawesome.com/icons/skull) — confirms `fas fa-skull` is part of Free Solid style
- [Gunnar Peipman — Displaying enum as select list in ASP.NET Core](https://gunnarpeipman.com/aspnet-core-enum-to-select-list/) — confirms `Html.GetEnumSelectList<T>()` default behavior (raw member name when no `[Display]` attribute)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; existing stack versions confirmed via STACK.md and direct `_Layout.cshtml` inspection
- Architecture: HIGH — every touchpoint confirmed via direct source read, including two sites (`CharacterRepository.cs`) not covered in CONTEXT.md
- Pitfalls: HIGH — the if/no-else distinction and the CharacterRepository sort sites were discovered via direct source inspection, not inference

**Research date:** 2026-07-06
**Valid until:** 30 days (stable internal codebase change, no external dependency drift risk)
