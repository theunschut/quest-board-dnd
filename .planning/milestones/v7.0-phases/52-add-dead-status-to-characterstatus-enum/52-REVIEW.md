---
phase: 52-add-dead-status-to-characterstatus-enum
reviewed: 2026-07-06T10:01:37Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - QuestBoard.Domain/Enums/CharacterStatus.cs
  - QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml
  - QuestBoard.Service/Views/GuildMembers/Details.cshtml
  - QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml
  - QuestBoard.Service/Views/GuildMembers/Index.cshtml
  - QuestBoard.Service/wwwroot/css/guild-members.css
  - QuestBoard.Service/wwwroot/css/guild-members.mobile.css
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
status: issues_found
---

# Phase 52: Code Review Report

**Reviewed:** 2026-07-06T10:01:37Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

This phase adds a `Dead = 2` member to `CharacterStatus` and wires it through the Guild Members Details/Index views (desktop + mobile) and their CSS. I read all 7 listed files, cross-referenced the phase's own PLAN/UI-SPEC to confirm scope, inspected `GuildMembersController.cs` (the confirmed zero-change touchpoint) to verify the accepted-risk claim in the threat model, checked `EntityProfileEnumCastTests.cs` to confirm the dynamic-enum test genuinely covers the new value with no test changes needed, and ran a full `dotnet build` (0 errors, 0 warnings). The diff against the base commit matches the plan's `<action>` blocks essentially verbatim — badge branches, ternary nesting, CSS rule placement, and the `ToggleRetirement` form guard are all present exactly as specified.

This is a narrowly-scoped, well-executed view-only change. I found one legitimate defense-in-depth gap (the toggle-button hide is UI-only, with no matching server-side guard) and one minor consistency nit in badge-check style between the two Details views. Neither blocks shipping, but the warning is worth a follow-up decision since it's a genuine, exploitable state inconsistency (a "dead" character can be silently revived to Active by a raw POST), even though the phase's own threat model consciously accepted it.

## Warnings

### WR-01: `ToggleRetirement` has no server-side guard against reviving a Dead character

**File:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:276-297` (not in the file list for this phase, but is the action the reviewed views POST to)
**Issue:** The Details/Details.Mobile views now hide the "Retire/Reactivate" `<form asp-action="ToggleRetirement">` block whenever `Model.Status == CharacterStatus.Dead`, which stops the button from appearing in the rendered page. However, `ToggleRetirement` itself performs an unconditional binary flip:
```csharp
character.Status = character.Status == CharacterStatus.Active
    ? CharacterStatus.Retired
    : CharacterStatus.Active;
```
Because `Dead` is neither `Active` nor explicitly excluded, a `POST /GuildMembers/ToggleRetirement` with a Dead character's `id` (trivial to construct — same origin, same auth, just missing the now-hidden form) flips the character straight from `Dead` to `Active`, silently "reviving" it and bypassing the entire point of this phase (a permanent, distinct dead state). The phase's own threat model (`52-01-PLAN.md`, T-52-02) explicitly accepts this as "no security impact... status is not an access-control field," which is true for quest-eligibility purposes, but it does undermine the feature's own stated intent ("Let DMs and players mark a character as permanently deceased") — the "permanent" framing is only a UI convention, not an enforced invariant, and any user who owns the character (or knows/guesses the id via dev tools) can undo a Dead marking with one crafted request. This is a judgment call the team already made consciously, so it is flagged as a warning rather than a blocker, but it should be tracked as intentional tech debt rather than silently accepted.
**Fix:** Add a same-guard check in the controller action so the server enforces what the view merely hides:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ToggleRetirement(int id, CancellationToken token = default)
{
    var character = await characterService.GetCharacterWithDetailsAsync(id, token);
    if (character == null)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null || character.OwnerId != currentUser.Id)
    {
        return Forbid();
    }

    if (character.Status == CharacterStatus.Dead)
    {
        return BadRequest("Dead characters cannot be retired or reactivated.");
    }

    character.Status = character.Status == CharacterStatus.Active
        ? CharacterStatus.Retired
        : CharacterStatus.Active;

    await characterService.UpdateAsync(character, token);

    return RedirectToAction(nameof(Details), new { id });
}
```

## Info

### IN-01: Badge-condition style diverges between Details.cshtml and Details.Mobile.cshtml for the Retired branch

**File:** `QuestBoard.Service/Views/GuildMembers/Details.cshtml:37`, `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml:32`
**Issue:** The new Dead branch is written consistently as inline `Model.Status == CharacterStatus.Dead` in both files, but the surviving Retired branch differs: desktop uses inline `Model.Status == CharacterStatus.Retired` while mobile uses the precomputed `isRetired` local. This divergence was carried forward intentionally from the pre-existing code (the plan explicitly says "keep `isRetired` for the Retired branch" on mobile), so it is not a regression introduced by this phase — but it does mean the two badge blocks are not literally structurally identical, which could confuse a future maintainer diffing the two files side by side expecting parity.
**Fix:** Optional cleanup for a later pass: normalize both files to use either the inline comparison or a precomputed bool consistently. Not required for this phase; purely a maintainability nit.

---

_Reviewed: 2026-07-06T10:01:37Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
