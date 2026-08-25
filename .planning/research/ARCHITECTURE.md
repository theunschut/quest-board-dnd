# Architecture Research

**Domain:** ASP.NET Core 10 MVC quest board — v9.0 "Rolling Improvements" (subsequent milestone, two small ad-hoc items)
**Researched:** 2026-08-25
**Confidence:** HIGH — every claim below was verified by opening the actual source file at the cited path/line, not inferred from naming conventions.

This is not a greenfield-domain research doc — it is an integration analysis for two small changes against an established codebase. The generic "Standard Architecture / Scaling Considerations" template sections are omitted where they don't apply; the existing three-layer architecture (`Service → Domain → Repository`) is **not** being changed by either item.

---

## Item 1 — Change character on an existing quest signup

### Verdict: Service-layer only

**No Domain or Repository changes are needed.** Confirmed end-to-end:

- `PlayerSignupService.UpdateSignupCharacterAsync(int playerSignupId, int? characterId, ...)` (`QuestBoard.Domain/Services/PlayerSignupService.cs:36-46`) already accepts a nullable `characterId`, loads the signup, sets `playerSignup.CharacterId = characterId;` (line 44 — no coercion, `null` assigns cleanly), and calls `repository.UpdateAsync`.
- `PlayerSignupRepository` **overrides** the generic AutoMapper-based `BaseRepository.UpdateAsync` (`QuestBoard.Repository/PlayerSignupRepository.cs:112-130`) with an explicit scalar copy: `entity.CharacterId = model.CharacterId;` — this is a deliberate override (the base `BaseRepository.UpdateAsync` at `QuestBoard.Repository/BaseRepository.cs:63-69` would otherwise AutoMap-overwrite `DateVotes` too aggressively). It assigns `int? → int?` directly; nothing here coerces `null` to `0` or throws.
- `PlayerSignupEntity.CharacterId` (`QuestBoard.Repository/Entities/PlayerSignupEntity.cs:31`) is `int?` with `[ForeignKey(nameof(CharacterId))]` — the DB column is already nullable (set by an existing migration; no new migration required).
- `QuestController.UpdateSignupCharacter(int questId, int? characterId)` (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:520-548`, verified) already:
  - Is `[HttpPost] [ValidateAntiForgeryToken] [Authorize]`.
  - Loads the quest, resolves the caller via `userService.GetUserAsync(User)`, finds *that user's own* signup (`quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)`) — never trusts a client-supplied signup ID.
  - Validates the character **only when `characterId.HasValue`** (lines 538-545): must belong to the caller (`character.OwnerId != user.Id`) and be `CharacterStatus.Active`. When `characterId` is `null`, this whole validation block is skipped — clearing is already a supported code path in the controller today.
  - Calls `playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId)` and redirects to `Details`.

**Conclusion: the entire nullable-characterId plumbing from controller → domain service → repository → entity → DB is already correct and already handles clearing.** The only gap is in the **View layer**: no UI element currently posts `characterId=null` (see below), and no UI element lets a user change an *already-set* character at all.

### Where a null `characterId` could be silently coerced — and where it is NOT

The only place a coercion risk exists is client-side: the modal's `<select name="characterId" ... required>` (`Details.cshtml:840`) has an HTML5 `required` attribute, which blocks the browser from submitting an empty value at all. This is a **client-side UX blocker, not a server-side coercion** — if bypassed (e.g., a crafted request with `characterId=` empty string), ASP.NET Core's default model binder converts an empty string to `null` for a `int?` parameter without error (standard MVC behavior), so the server path was already safe. Nowhere in the verified chain (controller → `UpdateSignupCharacterAsync` → `PlayerSignupRepository.UpdateAsync` → `PlayerSignupEntity.CharacterId`) does `null` get coerced to `0` or an exception.

**Clean way to support "-- No character --":** remove `required` from the `<select>`, add a first `<option value="">-- No character --</option>` (the code already has `<option value="">-- Select a character --</option>` at line 841 for the "add" case — reusable/renameable per context), and let empty-string → `int?` model binding do the rest. No controller change needed.

### Files: modified vs new (exhaustive)

**MODIFIED**

| File | Change |
|---|---|
| `QuestBoard.Service/Views/Quest/Details.cshtml` | In the `Character != null` branches (participants table ~line 116-129, waitlist table ~line 232-244): add a "Change character" trigger button next to the character name (icon + `data-bs-toggle="modal" data-bs-target="#addCharacterModal" data-character-id="@participant.Character.Id"`), gated by the existing `isCurrentUser` check (mirrors the existing gate at line 134/250). Replace the inline `#addCharacterModal` block (lines ~819-863) with `@await Html.PartialAsync("_CharacterSelectModal", Model)`. |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` | Add the same character-name-plus-button UI to the two participant-row blocks (~line 215, ~243) — today these are plain `<small>@(participant.Character?.Name ?? "No character")</small>` with **no** affordance at all (add or change). Add the same trigger-button markup (adapted to the mobile row layout), gated on `isCurrentUser` (already computed at lines 200/232). Add `@await Html.PartialAsync("_CharacterSelectModal", Model)` once, near the bottom of the page (mobile already has a `@section Scripts` block at line 389 — the partial call itself goes in the body, not the script section). |

**NEW**

| File | Purpose |
|---|---|
| `QuestBoard.Service/Views/Quest/_CharacterSelectModal.cshtml` | `@model PlayerSignup` shared partial containing the modal markup (title/button text can stay static, e.g. "Manage Character" for both add/change) plus a self-contained `show.bs.modal` script (see below). Reads `ViewBag.UserCharacters` — ambient and automatically available inside the partial (Razor's 2-arg `Html.PartialAsync(name, model)` overload passes the current `ViewData` through unchanged, so no extra parameter plumbing is needed). Dropdown gets a `<option value="">-- No character --</option>` first entry (no `required` attribute) so clearing is a normal form submission. |

No controller, Domain, or Repository files are modified or added for Item 1. This is confirmed Service-only (views + one new partial); the `[HttpPost]` action, its validation, and the full data path already support everything the UI needs to expose.

### Why a shared partial, not duplicated markup — argued, not asserted

Three independent, verified pieces of evidence support extracting a shared partial rather than triplicating the modal (once per: desktop-participants-context, desktop-waitlist-context, mobile):

1. **This project has a documented, self-inflicted cost from exactly this kind of near-duplication.** `.planning/PROJECT.md` (line 153) records: *"`Characters/Edit.cshtml`'s 'Add Another Class' script is missing the empty-`Classes`-list `classIndex` guard the other 3 near-duplicate write-form copies (`Create.cshtml`, `Create.Mobile.cshtml`, `Edit.Mobile.cshtml`) all have"* — a real bug from four hand-copied blocks drifting apart, still unfixed as of this research. The `Details.cshtml` participants-table and waitlist-table character cells are already a byte-for-byte-near-duplicate pair (verified: lines 116-144 and 232-260 are structurally identical) with *zero* shared partial today — adding "change character" logic as a third hand-copy (desktop x2 inline + mobile) reproduces the exact failure mode this project has already been burned by once.
2. **The codebase already has the precedent for sharing markup literally across the desktop/mobile boundary via `Html.PartialAsync`, invoked with the same `PlayerSignup` model both files already share.** `_Calendar.cshtml` (`QuestBoard.Service/Views/Shared/_Calendar.cshtml`) is called identically from both `Details.cshtml` (3 call sites) and `Details.Mobile.cshtml` (2 call sites) — proving partials are the established mechanism for exactly this kind of desktop+mobile parity, not something to be introduced here.
3. **The dynamic-modal-content JS pattern this needs (`show.bs.modal` + `event.relatedTarget` reading a `data-*` attribute) is already an established, repeated idiom in this codebase** — `Shop/Index.cshtml:455`, `Shop/Index.Mobile.cshtml:262`, `ShopManagement/Index.cshtml:505`, `ShopManagement/Index.Mobile.cshtml:216` all use it. `ShopManagement/Index.cshtml`'s deny-modal variant (lines 501-517) is the closest structural match: it reads `data-item-id`/`data-item-name` off `event.relatedTarget` and rewrites the form's `action`/hidden field values before the modal opens — this is the exact shape needed here (`data-character-id` → set `#characterSelect`'s value).

**Recommendation: extract `_CharacterSelectModal.cshtml` into `Views/Quest/`** (not `Views/Shared/` — `_Calendar.cshtml` sits in `Shared` because it's genuinely cross-feature, used by both the standalone `Calendar` feature and `Quest`; this new partial is Quest-only, so it belongs alongside the existing Quest-scoped partials `_QuestCard.cshtml` and `_QuestSection.cshtml`, matching the codebase's own "single-feature partials live with their feature" convention). One partial, called identically from both `Details.cshtml` and `Details.Mobile.cshtml` with `@model PlayerSignup` (both files already share this exact model type, so no ViewModel change is needed to pass it).

### How the mobile variant shares the modal without a third copy

Put the `show.bs.modal` JavaScript **inside the partial itself** as a plain inline `<script>` tag (not inside a `@section Scripts` block). This sidesteps a real, verified inconsistency between the two host files: `Details.cshtml` puts its existing modal-adjacent script directly in the page body (line 865 onward, no `@section Scripts` wrapper), while `Details.Mobile.cshtml` does use `@section Scripts { ... }` (starting line 389). Since the modal's `id="addCharacterModal"` is unique per rendered page regardless of which file included the partial, a self-contained `<script>` block inside `_CharacterSelectModal.cshtml` works correctly wherever the partial is rendered, without needing to reconcile the two files' differing script-placement conventions or duplicate the JS a third time.

The script itself (new logic, following the `ShopManagement/Index.cshtml` `denyModal` precedent exactly):
```javascript
document.getElementById('addCharacterModal').addEventListener('show.bs.modal', function (event) {
    const button = event.relatedTarget;
    const characterId = button?.getAttribute('data-character-id') || '';
    document.getElementById('characterSelect').value = characterId;
});
```

Each trigger button (in both `Character != null` and `Character == null` branches, in both tables, on both desktop and mobile) sets `data-bs-toggle="modal" data-bs-target="#addCharacterModal" data-character-id="@(participant.Character?.Id.ToString() ?? "")"` — the empty case naturally resets the dropdown to "-- No character --" or the default placeholder, matching today's "Add" behavior with no special-casing needed.

### Data flow (verified, end to end)

```
Desktop or Mobile Details.cshtml
  trigger button (data-character-id=<id or empty>)
        │  show.bs.modal event
        ▼
_CharacterSelectModal.cshtml inline <script>
  sets #characterSelect.value from event.relatedTarget's data-character-id
        │  user submits <form asp-action="UpdateSignupCharacter" method="post">
        ▼  POST /Quest/UpdateSignupCharacter  { questId, characterId: int|"" }
QuestController.UpdateSignupCharacter(int questId, int? characterId)
  - ASP.NET model binding: "" → null  (no coercion risk — verified default MVC behavior)
  - loads quest, resolves caller, finds caller's OWN PlayerSignup (never trusts a signup id)
  - if characterId.HasValue: validates owner + CharacterStatus.Active, else BadRequest
  - if null: validation skipped entirely (pre-existing code path, already correct)
        ▼
playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId)
  QuestBoard.Domain/Services/PlayerSignupService.cs:36
  - loads PlayerSignup by id, sets playerSignup.CharacterId = characterId (null-safe)
        ▼
repository.UpdateAsync(playerSignup)
  QuestBoard.Repository/PlayerSignupRepository.cs:112 (override, not the generic base)
  - entity.CharacterId = model.CharacterId  (direct scalar assign, no AutoMapper here)
  - DbContext.SaveChangesAsync()
        ▼
RedirectToAction("Details", new { id = questId })
        ▼
QuestController.Details(int id) GET  (QuestController.cs:307-373)
  - re-fetches quest with signups (now-updated Character reflected via nav property)
  - re-populates ViewBag.UserCharacters (Active characters only, line 320)
  - renders Details.cshtml or Details.Mobile.cshtml (view-location expander picks by IsMobile)
```

No point in this chain silently coerces or loses a `null` `characterId` — verified by reading every hop, not assumed.

### `ViewBag.UserCharacters` availability

Populated once, in `QuestController.Details(int id, ...)` GET (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:317-320`):
```csharp
var allCharacters = await characterService.GetCharactersByOwnerIdAsync(currentUser.Id, token);
userCharacters = allCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
...
ViewBag.UserCharacters = userCharacters ?? new List<Character>();
```
This is a **single controller action** shared by both the desktop and mobile Details pages (view selection happens purely at the view-resolution layer via `MobileViewLocationExpander` + root `_ViewStart.cshtml`'s `IsMobile` branch — there is no separate `DetailsMobile` action). So yes: `ViewBag.UserCharacters` is already available identically on both render paths, already filtered to `CharacterStatus.Active` only (matching the server-side validation in `UpdateSignupCharacter`, so the dropdown never offers a character the POST would reject).

---

## Item 2 — Stale Dependabot alerts on the deleted `EuphoriaInn.Domain.csproj` manifest

Keep short per the question's scope — this touches no application architecture.

**Verified:**
- Commit `a477ab9` ("refactor: rename EuphoriaInn -> QuestBoard") deleted `EuphoriaInn.Domain.csproj` (and its 4 sibling `.csproj` files) from tracked source, confirmed via `git show --stat a477ab9`.
- The `EuphoriaInn.Domain/`, `EuphoriaInn.Repository/`, `EuphoriaInn.Service/`, `EuphoriaInn.UnitTests/`, `EuphoriaInn.IntegrationTests/` directories still physically present in the working tree contain **only** `bin/` and `obj/` build-artifact subfolders (verified via `find`) — no `.csproj`, no source files. `git ls-files` returns nothing for any of them; they are not tracked, are covered by the standard `[Bb]in/`/`[Oo]bj/` rules already in `.gitignore`, and were never added deliberately (they're stale local build output from before the rename, sitting untouched on this machine). **They have zero bearing on the Dependabot alerts** — Dependabot/GitHub's dependency graph reads tracked manifests from the repository on GitHub, not local untracked build artifacts.
- `QuestBoard.slnx` contains no reference to any `EuphoriaInn.*` project (confirmed — zero grep matches).
- **There is no `.github/dependabot.yml` in this repository at all** (confirmed — `find .github -type f` lists only `ISSUE_TEMPLATE/*.md` and the 3 workflow YAMLs; no dependabot config). Dependabot *alerts* (as opposed to Dependabot *version-update PRs*, which do require a `dependabot.yml`) are generated from GitHub's native Dependency Graph feature against whatever manifests exist in the repo's history/current tree — this needs no config file to function, and adding one would not affect alert staleness.

**Conclusion:** this is purely a **GitHub-side stale-alert cleanup**, not a repo change. The 5 HIGH alerts reference a manifest path that no longer exists on `main`; GitHub's dependency graph should auto-close alerts tied to a removed manifest, but in practice this doesn't always happen promptly/automatically, especially if the alerts were opened before the manifest was removed. The correct resolution path is to open each alert in the repo's **Security → Dependabot alerts** tab on GitHub.com and manually **Dismiss** with reason "No longer used" / "Vulnerable dependency removed" (whichever GitHub's UI offers for this repo) — no code, config, `.gitignore`, or `dependabot.yml` change is warranted or would have any effect. Optionally, as unrelated hygiene (not required to close the alerts): the 5 untracked `EuphoriaInn.*/bin,obj` directories could be deleted locally to declutter the working tree, since they're dead build output with no source behind them — but this has no bearing on the Dependabot alerts themselves and is a one-line `rm -rf` a developer can do at their own discretion, not a phase deliverable.

---

## Recommended build order

**Phase A — Item 2 first (Dependabot alert dismissal).** Zero code risk, zero dependency on anything else, and it's pure GitHub UI interaction (no `git` commit even required). Doing it first clears it off the milestone's open list immediately and has no ordering constraint with Item 1 — sequencing it first is purely a "bank the easy win, unblock nothing" call, not a technical dependency.

**Phase B — Item 1, single phase, ordered internally as:**
1. New partial `_CharacterSelectModal.cshtml` (self-contained: modal markup + `show.bs.modal` script) — build and unit-verify it renders correctly with a stub model before wiring callers, since both host views depend on it existing first.
2. `Details.cshtml` — replace inline modal with the partial call, add "Change" trigger buttons to the `Character != null` branches (both tables).
3. `Details.Mobile.cshtml` — add the partial call, add matching trigger buttons/character display to the two participant-row blocks (this file currently has **no** character add/change affordance at all, so this is new UI, not a parity port of existing desktop-only UI — slightly more design latitude, but must match the desktop's data-flow contract exactly since both post to the same `UpdateSignupCharacter` action).

**Why one phase, not split desktop/mobile across two phases:** this project's own retrospective notes (`.planning/PROJECT.md`, Phase 43/54 lesson, referenced directly in the v7.0 history: *"mobile-button-class mismatch... UI-SPEC review separately caught"* and *"mobile parity enforced by pairing desktop+mobile view edits into single tasks, per the Phase 43/54 lesson"*) establish a standing project convention that desktop+mobile edits for the same feature should land as **paired tasks within one phase**, specifically because splitting them across phases previously caused parity drift (Phase 54's mobile-only bug fix, Phase 43's Mobile-only fixes for #115/#116). Item 1 should follow that same paired-task discipline: one phase, with the partial as its own task/wave (dependency for both view edits), then desktop and mobile view edits as sibling tasks in the same phase — not two separate phases.

**No Domain/Repository phase is needed for Item 1** — reiterated because it changes the usual phase shape for this project (most feature phases here touch all three layers): this one is Service/Views-only, so there's no "backend phase then frontend phase" split to plan for.

---

## Sources

All findings verified by direct file reads in this working tree (`C:\Repos\quest-board`), not inferred:

- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (lines 307-373 `Details` GET, 520-548 `UpdateSignupCharacter`)
- `QuestBoard.Service/Views/Quest/Details.cshtml` (lines 1-40, 95-284, 815-889)
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (lines 1-20, 195-264)
- `QuestBoard.Service/Views/Quest/_QuestCard.cshtml`, `Views/Shared/_Calendar.cshtml` (partial-sharing precedent)
- `QuestBoard.Service/Views/Shop/Index.cshtml`, `Views/Shop/Index.Mobile.cshtml`, `Views/ShopManagement/Index.cshtml`, `Views/ShopManagement/Index.Mobile.cshtml` (`show.bs.modal` precedent)
- `QuestBoard.Service/Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml` (ambient usings, mobile view selection)
- `QuestBoard.Domain/Services/PlayerSignupService.cs` (lines 36-46)
- `QuestBoard.Domain/Interfaces/IPlayerSignupService.cs`
- `QuestBoard.Repository/PlayerSignupRepository.cs` (lines 112-130, `UpdateAsync` override)
- `QuestBoard.Repository/BaseRepository.cs` (lines 63-69, generic `UpdateAsync` being overridden)
- `QuestBoard.Repository/Entities/PlayerSignupEntity.cs` (line 31, `CharacterId` FK)
- `QuestBoard.Domain/Enums/CharacterStatus.cs`
- `.planning/PROJECT.md` (Known issues section, line 153 — `Characters/Edit.cshtml` guard drift; Phase 43/54/54 mobile-parity history)
- `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONVENTIONS.md` (baseline layer/convention confirmation)
- `git show --stat a477ab9`, `git ls-files`, `find EuphoriaInn.*`, `.gitignore`, `.github/` directory listing, `QuestBoard.slnx` (Item 2 verification)

---
*Architecture research for: D&D Quest Board v9.0 "Rolling Improvements"*
*Researched: 2026-08-25*
