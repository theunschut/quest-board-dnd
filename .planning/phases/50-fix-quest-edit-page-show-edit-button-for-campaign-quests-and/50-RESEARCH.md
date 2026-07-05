# Phase 50: Fix quest edit page: show edit button for campaign quests and align field visibility with create page - Research

**Researched:** 2026-07-05
**Domain:** ASP.NET Core MVC Razor views — conditional markup / board-type UI parity bug fix
**Confidence:** HIGH

## Summary

This is a narrow, mechanical UI-parity bug fix confined entirely to Razor view markup in four files (`Manage.cshtml`, `Manage.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`) plus a one-line controller change (`QuestController.Edit` GET). CONTEXT.md already pins every decision (D-01 through D-06) with exact file/line references. This research verified every cited line number and code shape against the current codebase (as of 2026-07-05) — all citations are accurate, with only a one-line offset on one reference (see Verification Findings). No new architecture, library, or pattern is introduced; every piece of markup being added is a **verbatim copy** of markup that already exists elsewhere in the same files.

The two bugs share one root cause: the Campaign board-type branch of the DM-facing Quest views was built as a stripped-down, Campaign-specific variant that never got the same "hide OneShot-only fields" and "full action set" treatment that OneShot quests received. `Create.cshtml`/`Create.Mobile.cshtml` already do this correctly (`@if (boardType != BoardType.Campaign)` wraps Challenge Rating, Total Player Count, DM-Session-Only checkbox, and Proposed Dates). `Edit.cshtml`/`Edit.Mobile.cshtml` never received this treatment — they don't even receive `ViewBag.BoardType` from the `Edit` GET action, so there is no `boardType` variable available to branch on yet. Similarly, `Manage.cshtml`/`Manage.Mobile.cshtml`'s Campaign action row (`@if (boardType == BoardType.Campaign)`) only has Close/Reopen + Refresh Data, while the OneShot unfinalized-quest row nearby has Finalize + Edit + Delete + Refresh Data.

**Primary recommendation:** Implement as a single small plan (or 2 tightly-scoped plans — one for Manage.cshtml/Manage.Mobile.cshtml button additions, one for Edit.cshtml/Edit.Mobile.cshtml + controller conditional) since all four view edits are independent copy-paste-and-adapt operations with no shared state or sequencing dependency between the Manage-page fix and the Edit-page fix. The `ViewBag.BoardType` controller change (D-05) must land before or together with the Edit view conditional changes, since the view `@if` block depends on it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Board-type-conditional field visibility (Edit page) | Frontend Server (Razor view) | API/Backend (`ViewBag.BoardType` supplied by controller GET action) | Rendering decision belongs in the view; the value it branches on is resolved server-side from the active group (never client input) — same split already used by `Create.cshtml` |
| Campaign action-row buttons (Manage page) | Frontend Server (Razor view) | API/Backend (existing `Edit`/`Delete`/`Close`/`Reopen` actions + their independent authorization/board-type guards) | Adding a link/button is pure view markup; the underlying actions being linked to already exist and already self-defend server-side (Close/Reopen reject non-Campaign at controller level, Delete/Edit already enforce `DungeonMasterOnly` + ownership) — this phase adds no new authorization surface |
| `ViewBag.BoardType` resolution | API/Backend (`QuestController.GetActiveBoardTypeAsync`) | — | Single existing source of truth already used by `Index`, `Create` GET, `Create` POST, `Edit` POST, `Close`, `Reopen`; `Edit` GET is the only action in this controller missing the call |
| Client-side delete confirmation | Browser/Client (existing `deleteQuest(id)` JS) | — | Already implemented and shared across both OneShot and (after this phase) Campaign delete buttons; no new JS needed |

## Standard Stack

No new libraries, packages, or dependencies. This phase uses only:
- ASP.NET Core 10 MVC Razor view engine (existing)
- Bootstrap 5 (existing, `.btn`, `.d-flex`, etc. — per UI-SPEC.md)
- Font Awesome (existing, `fas fa-edit`, `fas fa-trash`)

**Installation:** None required — all changes are edits to existing `.cshtml` files and one existing controller method.

## Package Legitimacy Audit

**Not applicable.** This phase installs no external packages (no `npm install`, `dotnet add package`, or equivalent). Skipping the Package Legitimacy Gate per its trigger condition ("Every phase that installs external packages").

## Verification Findings (codebase state vs. CONTEXT.md citations)

All CONTEXT.md file/line citations were re-verified against the current working tree (branch `milestone/v7-backlog-cleanup`, as of this research). Result: **accurate**, with one line-offset note.

| CONTEXT.md citation | Verified? | Actual state |
|---|---|---|
| `Manage.cshtml` OneShot unfinalized action row, "Edit Quest"/"Delete" markup at lines 341-353 | ✓ Confirmed | Exact markup at lines 341-353: `btn-primary`/`fa-edit`/"Edit Quest" (344-346), `btn-danger`/`fa-trash`/"Delete" with `onclick="deleteQuest(@Model.Id)"` (347-349) |
| `Manage.cshtml` Campaign action row at lines 524-547 | ✓ Confirmed | `@if (boardType == BoardType.Campaign)` block spans lines 524-547 exactly as described — Close/Reopen forms + Refresh Data only, no Edit/Delete |
| `Manage.cshtml` `deleteQuest(id)` JS function at lines 676-692 | ✓ Confirmed (off by 1) | Function starts at line **677**, ends at line 692 (CONTEXT.md said "676-692" — negligible, same block) |
| `Manage.Mobile.cshtml` OneShot row at lines 351-366 (351-361 button markup specifically) | ✓ Confirmed | `btn-secondary flex-fill`/"Edit Quest" at 355-358 (note: **mobile Edit Quest button already uses `btn-secondary`, not `btn-primary`** — see Pitfall 1 below), `btn-danger w-100`/"Delete Quest" at 359-361 |
| `Manage.Mobile.cshtml` Campaign action row at lines 372-396 | ✓ Confirmed | `@if (boardType == BoardType.Campaign)` block spans 372-396 exactly — Close/Reopen + Refresh Data only |
| `Manage.Mobile.cshtml` `deleteQuest(id)` JS at lines 402-418 | ✓ Confirmed | Matches (block starts 403, ends 418 — same one-line offset pattern as desktop, immaterial) |
| `Edit.cshtml` four unconditional fields at lines 34-92 | ✓ Confirmed | Challenge Rating (34-39), Total Player Count (42-46), DM-Session-Only checkbox (48-56), Proposed Dates block including `HasExistingSignups` warning banner (58-92) — all unconditional, no `boardType` variable declared anywhere in the file |
| `Edit.Mobile.cshtml` four unconditional fields at lines 48-94 | ✓ Confirmed | Challenge Rating (48-52), Total Player Count (54-58), DM-Session-Only checkbox (60-68), Proposed Dates block (70-94) — no `HasExistingSignups`-inside-Proposed-Dates banner in mobile (the warning banner is rendered separately, outside the form, at lines 14-21 — see Pitfall 2 below) |
| `QuestController.Edit` GET lacks `ViewBag.BoardType`, POST resolves `boardType` internally at line 237 | ✓ Confirmed | `Edit` GET is lines 140-183, no `ViewBag`/`boardType` reference anywhere in it. `Edit` POST resolves `var boardType = await GetActiveBoardTypeAsync(token);` at line 237, sanitizes 4 fields at lines 238-244 |
| `Create` GET sets `ViewBag.BoardType` at line 77 | ✓ Confirmed | `ViewBag.BoardType = await GetActiveBoardTypeAsync(token);` at line 77, inside `Create` GET (lines 68-79) |
| `Create.cshtml` conditional at lines 34-83 | ✓ Confirmed | `@if (boardType != BoardType.Campaign)` spans lines 34-83, wrapping exactly the 4 field groups |
| `Create.Mobile.cshtml` conditional at lines 44-99 | ✓ Confirmed | `@if (boardType != BoardType.Campaign)` spans lines 44-99 |
| Close/Reopen board-type guards at QuestController.cs:710-714/749-753 | ✓ Confirmed (off by ~5-8 lines) | Actual guard clauses: `Close` at lines 717-723 (`if (boardType != BoardType.Campaign) return BadRequest(...)`), `Reopen` at lines 756-762 — same mechanism, CONTEXT.md's line numbers are close but not exact; content and behavior match exactly |

**Net finding:** No citation is materially wrong. Line numbers drift by 0-8 lines in a few spots (normal churn since CONTEXT.md was written earlier the same day), but every code shape, block boundary, and pattern described in CONTEXT.md is present and correct in the current tree. The planner can trust CONTEXT.md's guidance; executors should locate blocks by the described Razor `@if` conditions / button text rather than by line number alone, since exact numbers may drift again between planning and execution.

## Architecture Patterns

### System Architecture Diagram

```
Browser (GET /Quest/Manage/{id})
        |
        v
QuestController.Manage (existing, unchanged)
        | resolves ViewBag.BoardType via GetActiveBoardTypeAsync()  [already correct]
        v
Manage.cshtml / Manage.Mobile.cshtml
        | @if (boardType == BoardType.Campaign) { ... }
        |   -- BEFORE: Close/Reopen + Refresh Data only
        |   -- AFTER:  Edit Quest -> Close/Reopen -> Delete Quest -> Refresh Data
        v
Rendered HTML includes new <a href="@Url.Action("Edit", ...)"> and
new <a onclick="deleteQuest(@Model.Id)"> pointing at EXISTING controller actions
        |
        +---> GET /Quest/Edit/{id}  (existing action, now also reachable from Campaign Manage row)
        |           |
        |           v
        |     QuestController.Edit (GET) -- ADD ViewBag.BoardType = await GetActiveBoardTypeAsync(token)
        |           |
        |           v
        |     Edit.cshtml / Edit.Mobile.cshtml
        |           | var boardType = (BoardType)ViewBag.BoardType;  [NEW]
        |           | @if (boardType != BoardType.Campaign) { CR, PlayerCount, DMSession, ProposedDates }  [NEW conditional]
        |           v
        |     Rendered form hides the 4 fields for Campaign quests, matching Create.cshtml
        |
        +---> DELETE /Quest/Delete/{id}  (existing action, already has DungeonMasterOnly + ownership checks;
                    no board-type restriction -- was already reachable via _QuestCard.cshtml on Index page)
```

### Recommended Project Structure

No new files or folders. All changes are in-place edits to:
```
QuestBoard.Service/
├── Controllers/QuestBoard/
│   └── QuestController.cs        # Edit GET: add one ViewBag.BoardType line (D-05)
└── Views/Quest/
    ├── Manage.cshtml              # Campaign action row: add Edit Quest + Delete Quest (D-01/D-02/D-03)
    ├── Manage.Mobile.cshtml       # same, mobile variant
    ├── Edit.cshtml                # wrap 4 fields in @if (boardType != BoardType.Campaign) (D-04)
    └── Edit.Mobile.cshtml         # same, mobile variant
```

### Pattern 1: Board-type conditional field visibility (Razor `@if`, no partial/ViewComponent)

**What:** A plain `@if (boardType != BoardType.Campaign) { ... }` block wraps DOM that should only render for non-Campaign (i.e., OneShot) board types.
**When to use:** Any DM-facing quest form field that only makes sense for one-off scheduled quests (challenge rating, player count, DM-only toggle, proposed dates) and not for ongoing campaign quests (no per-quest scheduling/signup).
**Example (from `Create.cshtml`, to mirror verbatim in `Edit.cshtml`):**
```razor
@* Source: QuestBoard.Service/Views/Quest/Create.cshtml lines 34-83 (existing, verified) *@
@if (boardType != BoardType.Campaign)
{
    <div class="mb-3">
        <label asp-for="ChallengeRating" class="form-label">Challenge Rating <span class="text-danger">*</span></label>
        <input asp-for="ChallengeRating" type="number" class="form-control" min="1" max="20" step="1" placeholder="Enter level (e.g., 1, 5, 10, 15)" />
        <div class="form-text text-muted">Enter the recommended player level for this quest (1-20). This helps players understand if the quest is appropriate for their character level.</div>
        <span asp-validation-for="ChallengeRating" class="text-danger"></span>
    </div>
    @* ... TotalPlayerCount, DungeonMasterSession checkbox, Proposed Dates block ... *@
}
```
Note the property binding difference: `Create.cshtml` binds directly to `Model.X` (its ViewModel is the quest itself: `@model QuestViewModel`), while `Edit.cshtml`'s model is `EditQuestViewModel` with the quest nested at `Model.Quest` — so `Edit.cshtml`'s existing `asp-for="Quest.ChallengeRating"` etc. must be preserved as-is; only the wrapping `@if` is new, no `asp-for` targets change.

### Pattern 2: Board-type conditional action buttons (Razor `@if`, existing action links)

**What:** A plain `@if (boardType == BoardType.Campaign) { ... }` block in `Manage.cshtml`/`Manage.Mobile.cshtml` renders a different action-button set for Campaign vs. OneShot quests.
**When to use:** DM action rows on the Manage page where available actions differ by board type (Campaign has no Finalize concept; OneShot has no Close/Reopen concept).
**Example (existing OneShot markup being copied into the Campaign branch, from `Manage.cshtml` lines 344-349):**
```razor
@* Source: QuestBoard.Service/Views/Quest/Manage.cshtml lines 344-349 (existing, verified) *@
<a href="@Url.Action("Edit", "Quest", new { id = Model.Id })" class="btn btn-primary ms-2">
    <i class="fas fa-edit me-1"></i>Edit Quest
</a>
<a href="#" class="btn btn-danger ms-2" onclick="deleteQuest(@Model.Id)">
    <i class="fas fa-trash me-1"></i>Delete
</a>
```

### Anti-Patterns to Avoid
- **Introducing a new partial view or ViewComponent for the conditional blocks:** The established codebase convention (both `Create.cshtml` and `Manage.cshtml`) is a plain inline `@if`, never extraction into a partial. Do not refactor this phase into a partial — it expands scope and diverges from CLAUDE.md's "no unrequested architecture changes" spirit for a bug-fix phase.
- **Making the Edit page's hidden fields read-only/disabled instead of fully hidden:** D-04 explicitly requires full hide, matching Create exactly — no `disabled` attribute variant.
- **Reusing `btn-primary` uniformly for the mobile Edit Quest button:** see Pitfall 1 below — the mobile OneShot row already uses `btn-secondary` for its Edit Quest button, not `btn-primary`. UI-SPEC.md's own example markup for the mobile Campaign row says `btn-primary flex-fill`, which is an inconsistency with the actual current mobile OneShot pattern it claims to mirror — flag this for the planner (see Pitfall 1).
- **Skipping the `ViewBag.BoardType` controller change:** the view conditional cannot function without it; `boardType` would throw a null-reference/invalid-cast exception on `(BoardType)ViewBag.BoardType` if `ViewBag.BoardType` is never set. This makes D-05 a hard prerequisite, not an optional nice-to-have.

## Don't Hand-Roll

Not applicable to this phase in the traditional sense (no generic engineering problem like "auth" or "date parsing" is being solved). The equivalent guidance here:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Confirm-before-delete UX | New confirmation modal/dialog | Existing `deleteQuest(id)` JS function (browser `confirm()`) | Already implemented, tested-in-practice, and explicitly required verbatim reuse by D-03/UI-SPEC.md — zero new JS |
| Board-type resolution | New helper/service method, or trusting a posted/query-string value | Existing `QuestController.GetActiveBoardTypeAsync()` | Single source of truth already used by 6 other actions in the same controller; trusting client input here would reintroduce the exact class of bug Create/Edit POST already guard against |

**Key insight:** Every "building block" this phase needs already exists in the same two files it's editing. The work is entirely relocation/duplication of existing verified markup, not creation of new patterns.

## Common Pitfalls

### Pitfall 1: UI-SPEC.md's mobile "Edit Quest" button class (`btn-primary`) does not match the existing mobile OneShot pattern it claims to mirror (`btn-secondary`)
**What goes wrong:** UI-SPEC.md's Interaction Contract section shows the mobile Campaign action row's "Edit Quest" button as `class="btn btn-primary flex-fill"`. The actual existing mobile OneShot "Edit Quest" button (`Manage.Mobile.cshtml` lines 355-358, the pattern being mirrored) uses `class="btn btn-secondary flex-fill"` — not `btn-primary`. Desktop's OneShot Edit Quest button IS `btn-primary` (`Manage.cshtml` line 344), so the spec's example may have been transcribed from the desktop pattern rather than the mobile one it was labeled as sourcing from.
**Why it happens:** Desktop and mobile OneShot action rows use different button-color conventions for the same semantic action (desktop: primary/blue for Edit; mobile: secondary/gray for Edit, with Finalize taking primary/blue instead). UI-SPEC.md's own "verbatim reuse" framing assumed the two rows use identical colors, which they don't.
**How to avoid:** The planner must decide (or flag as a checkpoint question) whether the new Campaign mobile Edit Quest button should use `btn-secondary` (byte-for-byte matching the actual current mobile OneShot Edit Quest button, honoring UI-SPEC.md's stated intent of "verbatim reuse of existing mobile pattern") or `btn-primary` (following UI-SPEC.md's literal example code, which mismatches its own cited source). **Recommendation: use `btn-secondary flex-fill`** to genuinely match the existing mobile convention — this is a one-line class discrepancy, not a design decision requiring user input, since UI-SPEC.md's stated *intent* ("verbatim reuse... existing mobile pattern") is unambiguous and the literal example snippet is simply a transcription slip.
**Warning signs:** If the executor copies UI-SPEC.md's code block literally without cross-checking against the actual `Manage.Mobile.cshtml` OneShot row, the new Campaign mobile button will look inconsistent with the adjacent Finalize/Delete buttons in the same row (Finalize is `btn-primary`, Edit would become `btn-primary` too, when it should be visually distinct as `btn-secondary`).

### Pitfall 2: `Edit.Mobile.cshtml`'s `HasExistingSignups` warning banner is OUTSIDE the field being conditionally hidden, unlike `Edit.cshtml`
**What goes wrong:** In `Edit.cshtml`, the `HasExistingSignups` warning `<div class="alert alert-warning">` (lines 62-66) is nested *inside* the Proposed Dates `<div class="mb-3">` block (lines 58-92) that D-04 says to wrap entirely in the Campaign conditional. In `Edit.Mobile.cshtml`, the equivalent warning banner (lines 14-21) is rendered *before* the card wrapper even begins — completely outside the `<form>`, outside any per-field `mb-3` div, and not adjacent to the mobile Proposed Dates block at all (which starts at line 70).
**Why it happens:** The mobile view was structured with a page-level "heads up" banner pattern (shown once near the top of the page) rather than a field-adjacent banner like desktop. This is a genuine structural difference between the two views, not a copy-paste error.
**How to avoid:** D-04/CONTEXT.md's claim that Edit.Mobile.cshtml has "no `HasExistingSignups` banner variant differences to worry about — same structure" is **not accurate** upon verification — the banner exists in both, but at different structural locations. The planner should treat this as: hide the mobile Proposed Dates block (lines 70-94) using the same `@if (boardType != BoardType.Campaign)` wrapper, but the top-of-page `HasExistingSignups` banner (lines 14-21) should also be gated by the same condition, since a Campaign quest can never have `HasExistingSignups` be meaningfully true for date-related reasons once dates are hidden — however, `HasExistingSignups` reflects *any* player signup, not specifically date-related ones, so hiding it entirely could hide a legitimately relevant warning about non-date changes for Campaign quests. **Recommendation:** Leave the mobile top-of-page banner as-is (unconditional) since its copy ("Removing or significantly changing dates...") only makes sense when dates exist, but Campaign quests will never trigger `HasExistingSignups=true` through date changes since they have no dates to change — evaluate whether `HasExistingSignups` can ever be true for a Campaign quest at all (likely yes, via non-date signups) and decide whether the banner's date-specific copy is misleading in that case. This is a genuine edge case the plan should explicitly address rather than silently port over.
**Warning signs:** If the executor treats "wrap the same 4 fields" as a purely mechanical line-range copy without reading the surrounding structure of each mobile/desktop file independently, this banner-placement difference will be missed.

### Pitfall 3: `(BoardType)ViewBag.BoardType` cast will throw at runtime if `ViewBag.BoardType` is not set for every code path that returns the `Edit` view
**What goes wrong:** `QuestController.Edit` GET has two early-return paths before reaching the `return View(...)` at the end: `NotFound()` (quest null) and `Challenge()` (user null) and `Forbid()` (not owner/admin) and `BadRequest(...)` (finalized quest) — none of these render the `Edit` view, so they're safe. But if a future edit accidentally adds a new `return View(viewModel)` path (e.g., in a validation-failure branch) without also setting `ViewBag.BoardType` first, the view will throw `InvalidCastException` (casting `null` to `BoardType`) rather than degrading gracefully.
**Why it happens:** `ViewBag` is untyped; the compiler cannot catch a missing assignment. The `Edit` POST action already has this exact hazard today (line 231: `return View(viewModel);` inside `if (!ModelState.IsValid)` — but Edit POST returning the Edit view on failure would hit `Edit.cshtml`, which after this phase requires `ViewBag.BoardType`).
**How to avoid:** **Critical: the `Edit` POST action's `ModelState.IsValid` failure path (line 227-232) also returns `View(viewModel)` — i.e., re-renders `Edit.cshtml` — and currently does NOT set `ViewBag.BoardType` anywhere in the POST action.** This is a real gap the plan must address, not just the GET action. Compare to `Create` POST, which explicitly sets `ViewBag.BoardType = boardType;` before its own `return View(viewModel);` on validation failure (line 115). The Edit POST action must receive the same treatment: set `ViewBag.BoardType = boardType;` before the `if (!ModelState.IsValid) { ...; return View(viewModel); }` branch (or compute `boardType` earlier in the method, before the validation check, and assign to ViewBag right before returning). **This is a necessary corollary to D-05 that CONTEXT.md did not explicitly call out** — D-05 only mentions the `Edit` GET action needs `ViewBag.BoardType`; it says "The `Edit` POST action already resolves `boardType` internally... no change needed there" — but that resolution happens at line 237, which is *after* the validation-failure early return at line 227-232, so the POST action's failure path has no `boardType` in scope yet at that point.
**Warning signs:** A test that submits an invalid Edit form (e.g., empty Title) for a Campaign quest would throw an unhandled exception instead of re-rendering the form with validation errors. This is a testable, concrete regression risk — see Validation Architecture below.

## Code Examples

### Desktop Campaign action row — target end state (Manage.cshtml)
```razor
@* Extends the existing block at Manage.cshtml lines 524-547 *@
@if (boardType == BoardType.Campaign)
{
    <div class="d-flex justify-content-between align-items-center">
        <div class="d-flex gap-2">
            <a href="@Url.Action("Edit", "Quest", new { id = Model.Id })" class="btn btn-primary">
                <i class="fas fa-edit me-1"></i>Edit Quest
            </a>
            @if (!Model.IsClosed)
            {
                <form asp-action="Close" method="post" style="display: inline;">
                    <input type="hidden" name="id" value="@Model.Id" />
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-secondary">Close Quest</button>
                </form>
            }
            else
            {
                <form asp-action="Reopen" method="post" style="display: inline;">
                    <input type="hidden" name="id" value="@Model.Id" />
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-warning">Reopen Quest</button>
                </form>
            }
            <a href="#" class="btn btn-danger" onclick="deleteQuest(@Model.Id)">
                <i class="fas fa-trash me-1"></i>Delete
            </a>
        </div>
        <button type="button" class="btn btn-secondary" onclick="window.location.reload()">Refresh Data</button>
    </div>
}
```
Note: existing `d-flex gap-2` wrapper already provides consistent spacing between siblings — the UI-SPEC.md's `ms-2` recommendation on the Delete link is redundant given the parent already uses `gap-2` (Bootstrap 5's flex-gap utility), but including `ms-2` anyway causes no visual harm (just a very slightly wider gap before that one element) and matches OneShot's row, which does use `ms-2` throughout despite ALSO not having `gap-2` on its wrapper (`Manage.cshtml` line 341-342 uses plain `d-flex justify-content-between`, no `gap-2`). **Match whichever the current Campaign wrapper already uses** (`gap-2`, confirmed at line 527) — omit redundant `ms-2` for consistency within this specific block, OR keep UI-SPEC.md's `ms-2` for maximum literal-spec compliance. Either renders correctly; this is a cosmetic, non-blocking detail.

### Edit GET controller change (D-05)
```csharp
// QuestBoard.Service/Controllers/QuestBoard/QuestController.cs, Edit GET action (currently lines 140-183)
[HttpGet]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Edit(int id, CancellationToken token = default)
{
    var quest = await questService.GetQuestWithDetailsAsync(id, token);
    if (quest == null) { return NotFound(); }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) { return Challenge(); }

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) { return Forbid(); }

    if (quest.IsFinalized)
    {
        return BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.");
    }

    ViewBag.BoardType = await GetActiveBoardTypeAsync(token); // NEW — mirrors Create GET line 77

    var dms = await userService.GetAllDungeonMastersAsync(token);
    var questViewModel = mapper.Map<QuestViewModel>(quest);
    // ... unchanged rest of method
}
```

### Edit POST controller change (corollary to D-05, see Pitfall 3)
```csharp
// Edit POST action — boardType must be resolved BEFORE the ModelState.IsValid check,
// and ViewBag.BoardType must be set before any return View(viewModel) path.
var boardType = await GetActiveBoardTypeAsync(token); // move earlier than current line 237

if (!ModelState.IsValid)
{
    var dms = await userService.GetAllDungeonMastersAsync(token);
    viewModel.DungeonMasters = dms;
    ViewBag.BoardType = boardType; // NEW — required or Edit.cshtml throws on invalid-cast
    return View(viewModel);
}

if (boardType == BoardType.Campaign)
{
    viewModel.Quest.ChallengeRating = 1;
    viewModel.Quest.TotalPlayerCount = 0;
    viewModel.Quest.DungeonMasterSession = false;
    viewModel.Quest.ProposedDates = [];
}
// ... unchanged rest of method
```

## State of the Art

Not applicable — this is an internal bug fix in an existing bespoke codebase, not a library/framework integration where "current best practice" evolves externally.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Mobile "Edit Quest" button in the new Campaign row should use `btn-secondary` (matching mobile OneShot) rather than `btn-primary` (as literally written in UI-SPEC.md's example) | Pitfall 1 | Low — cosmetic only; if wrong, produces a slightly-too-blue button that still functions correctly. Easy to spot and fix in review/UAT. |
| A2 | `HasExistingSignups` can be true for a Campaign quest via non-date-related signups (e.g., players signing up to an ongoing campaign quest without a date-vote step) | Pitfall 2 | Medium — if this assumption is wrong (i.e., Campaign quests never produce signups that set `HasExistingSignups=true`), then the mobile top-of-page banner's unconditional visibility is harmless dead code for Campaign quests, not a misleading-copy bug. Either way the impact is cosmetic (a warning banner mentioning "dates" appearing on a page with no visible dates), not functional. Recommend the planner confirm via a quick domain-model check (does `PlayerSignups` populate for Campaign quests?) rather than treating this as blocking. |
| A3 | The `Edit` POST validation-failure path's missing `ViewBag.BoardType` (Pitfall 3) is in-scope for this phase, even though CONTEXT.md's D-05 only explicitly names the GET action | Pitfall 3 / D-05 | High if not fixed — an invalid Edit form submission for ANY quest (not just Campaign) would throw an unhandled `InvalidCastException` once `Edit.cshtml` starts reading `ViewBag.BoardType` unconditionally at the top of the file (mirroring `Create.cshtml`'s `var boardType = (BoardType)ViewBag.BoardType;` at file-top, which runs unconditionally on every render, including the validation-failure re-render). This is a genuine regression risk introduced by this phase's own change (D-04/D-05), not a pre-existing bug — it must be fixed in the same phase or the phase introduces a new crash. |

**If this table is empty:** N/A — see above, 3 assumptions logged, all recommend a fix or confirmation rather than blocking planning.

## Open Questions

1. **Should the plan explicitly include fixing the Edit POST validation-failure `ViewBag.BoardType` gap (Pitfall 3 / A3)?**
   - What we know: D-05 in CONTEXT.md only mentions the GET action. The POST action's existing validation-failure path returns `View(viewModel)` without setting `ViewBag.BoardType`, and after D-04's view change lands, `Edit.cshtml` will unconditionally read `(BoardType)ViewBag.BoardType` at the top of the file (same pattern as `Create.cshtml` line 7) — this WILL throw if unset.
   - What's unclear: Whether CONTEXT.md's author considered this and decided it's out of scope, or simply didn't trace the POST action's early-return path far enough.
   - Recommendation: Treat as in-scope. It is a direct, mechanical consequence of implementing D-04 correctly — not scope creep, but a completeness requirement of D-04/D-05 themselves. Flag for the planner to include a task for it; do not silently skip.

2. **Desktop `Manage.cshtml` new Delete button position: `ms-2` vs relying on parent's `gap-2`?**
   - What we know: The Campaign action row wrapper already uses `d-flex gap-2` (unlike the OneShot row's plain `d-flex justify-content-between` with per-child `ms-2`). UI-SPEC.md's example snippet adds `ms-2` to the new Delete link.
   - What's unclear: Whether to follow UI-SPEC.md literally (add redundant `ms-2`) or adapt to the existing wrapper's `gap-2` convention (omit it).
   - Recommendation: Either is visually correct (redundant `ms-2` on top of `gap-2` just adds ~8px extra — imperceptible). Not worth a checkpoint; executor discretion, lean toward matching the file's existing wrapper convention (omit `ms-2` since `gap-2` already handles spacing) for cleaner markup.

## Environment Availability

Not applicable — this phase has no external tool/service/runtime dependencies beyond the existing dotnet SDK and the project's own test infrastructure, both already confirmed present and in use throughout the repository (see Validation Architecture below).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (via `TestContext.Current.CancellationToken` usage) + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) + FluentAssertions (`.Should()`) |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`, base fixture at `QuestBoard.IntegrationTests/Helpers/WebApplicationFactoryBase.cs` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController"` |
| Full suite command | `dotnet test` (runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |

The codebase already has a directly-applicable pattern for this phase: raw-HTML-string integration assertions (`content.Should().Contain("Edit DM Profile")` in `DungeonMasterControllerIntegrationTests.cs`; `content.Should().NotContain(...)` in `GroupManagementIntegrationTests.cs`) and a `TestDataHelper.SeedCampaignGroupAsync(services, groupId)` helper already used by `QuestCloseTests.cs` to set up a Campaign-board-type group for controller-level tests. Mobile-view rendering is triggerable in integration tests via a `User-Agent` header on the `HttpClient` request (`MobileViewsTests.cs` pattern: `request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent)`).

This means **all four view changes in this phase are directly source-verifiable without a live browser** — every acceptance criterion in this phase (a button's presence/absence, a field's presence/absence, in rendered HTML) can be asserted via `HttpClient.GetAsync` + `content.Should().Contain(...)`/`NotContain(...)`, using the existing `SeedCampaignGroupAsync` and a to-be-added (or already-present, verify during planning) `SeedOneShotGroupAsync`-equivalent helper for the OneShot-side regression checks (confirming OneShot Manage/Edit pages are unaffected).

### Phase Requirements → Test Map

No formal REQ-IDs exist for this ad-hoc phase (per phase description: "TBD — no formal REQ-IDs"). Mapping instead to CONTEXT.md's D-01 through D-06 decisions:

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01/D-02 | Campaign Manage page renders an "Edit Quest" link before Close/Reopen | integration | `dotnet test --filter "FullyQualifiedName~QuestManage"` asserting `content.Should().Contain("Edit Quest")` on a Campaign-board GET `/Quest/Manage/{id}` response | ❌ Wave 0 — new test file needed |
| D-03 | Campaign Manage page renders a "Delete"/"Delete Quest" link after Close/Reopen, wired to `deleteQuest(id)` | integration | same test class, assert `content.Should().Contain("deleteQuest(")` and `Contain("Delete")` | ❌ Wave 0 |
| D-04 | Edit page hides Challenge Rating, Total Player Count, DM-Session checkbox, Proposed Dates for Campaign quests; shows them for OneShot | integration | GET `/Quest/Edit/{id}` for a Campaign quest: assert `NotContain("Challenge Rating")`/`NotContain("Total Player Count")`/`NotContain("Dungeon Master Session Only")`/`NotContain("Proposed Dates")`; GET for a OneShot quest: assert `Contain(...)` for all four (regression check) | ❌ Wave 0 |
| D-05 | `Edit` GET sets `ViewBag.BoardType`; `Edit` POST validation-failure path also sets it (Pitfall 3 fix) | integration | (a) GET Edit for Campaign quest does not throw / returns 200; (b) POST Edit with invalid ModelState (e.g., empty Title) for a Campaign quest does not throw / returns 200 with validation errors rendered — this second case is the regression test for Pitfall 3/A3 | ❌ Wave 0 — this is the most important new test in the phase, since it guards against an unhandled exception, not just a visual mismatch |
| D-06 | Sidebar tips unchanged (explicitly not fixed) | none — no test needed | n/a — explicit non-change | n/a |
| Mobile parity (all of the above) | Same assertions repeated with `MobileUserAgent` header per `MobileViewsTests.cs` pattern | integration | Same test class, mobile-user-agent variant of each case above | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~QuestManage|FullyQualifiedName~QuestEdit"`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] New integration test file (or extend `QuestCloseTests.cs` / `QuestControllerIntegrationTests_Comprehensive.cs`) covering: Campaign Manage page shows Edit+Delete; Edit page hides/shows the 4 fields correctly by board type; Edit POST validation-failure path doesn't throw for Campaign quests (Pitfall 3 regression guard) — covers D-01 through D-05
- [ ] Confirm whether a `SeedOneShotGroupAsync` (or equivalent existing OneShot-default-group fixture) already exists in `TestDataHelper.cs` for the regression-check side of D-04's test map row — if the default seeded group is already OneShot-type, no new helper is needed; verify during Wave 0 rather than assuming
- [ ] Mobile-user-agent variants of the above tests, following the `MobileViewsTests.cs` `GetWithUserAgentAsync` helper pattern

*(No framework install needed — xUnit v3 + WebApplicationFactory + FluentAssertions are already fully wired and in active use across 60+ existing test files.)*

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — `[Authorize(Policy = "DungeonMasterOnly")]` already present on all touched actions, not modified by this phase |
| V3 Session Management | No | Not touched |
| V4 Access Control | Yes (verification only, no new surface) | The new Manage-page buttons link to `Edit` (GET, `DungeonMasterOnly` + ownership check) and `Delete` (DELETE, `DungeonMasterOnly` + ownership check) — both already enforce authorization independently of UI visibility. This phase's UI-only change does not weaken or bypass any existing check; verify during code review that no new action method is added without equivalent guards (none are — see Architecture Patterns) |
| V5 Input Validation | No | No new input surface — `ViewBag.BoardType` is server-resolved from the active group context, never from request data (same pattern already audited/trusted in `Create`/`Close`/`Reopen`) |
| V6 Cryptography | No | Not touched |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Client-rendered button visibility mistaken for authorization | Elevation of Privilege | Already mitigated pre-existing: `Close`/`Reopen` reject non-Campaign quests server-side (lines 717-723/756-762); `Edit`/`Delete` already enforce `DungeonMasterOnly` + `IsQuestOwner`/Admin check regardless of which page linked to them. This phase adds links to already-guarded endpoints — confirm during code review that no guard was weakened, only new UI added. |
| Trusting `ViewBag.BoardType`-like state from client input | Tampering | Not applicable here — `ViewBag.BoardType` is always assigned from `GetActiveBoardTypeAsync()`, a server-side call resolving from the active group; this phase does not introduce any new place where board type could be read from request data |

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Service/Views/Quest/{Manage,Manage.Mobile,Edit,Edit.Mobile,Create,Create.Mobile}.cshtml`) — all line numbers and code shapes verified against current working tree, 2026-07-05
- `QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs`, `QuestControllerIntegrationTests_Comprehensive.cs`, `GroupManagementIntegrationTests.cs`, `DungeonMasterControllerIntegrationTests.cs`, `Mobile/MobileViewsTests.cs`, `Helpers/TestDataHelper.cs` — existing test patterns directly reused for Validation Architecture section
- `.planning/phases/50-.../50-CONTEXT.md` and `50-UI-SPEC.md` — user-approved decisions and design contract (this phase's binding scope)
- `.planning/config.json` — confirmed `nyquist_validation: true`

### Secondary (MEDIUM confidence)
- None used — this phase required no external documentation lookup (pure internal codebase analysis, no third-party library involved)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new stack, all existing ASP.NET Core/Bootstrap/FontAwesome already in use
- Architecture: HIGH — directly verified against current codebase, all patterns are copy-paste of existing verified code
- Pitfalls: HIGH — Pitfall 3 (ViewBag.BoardType gap in Edit POST) was discovered via direct code trace, not speculation; Pitfalls 1-2 were discovered via direct line-by-line comparison of UI-SPEC.md's claims against actual file contents

**Research date:** 2026-07-05
**Valid until:** Line-number citations may drift with any further commits to these files before execution; re-verify block locations by content (Razor `@if` conditions, button text) rather than trusting exact line numbers if more than a few days elapse before execution. Code shapes and patterns described are stable (30+ days) since they reflect established codebase conventions, not external library versions.
