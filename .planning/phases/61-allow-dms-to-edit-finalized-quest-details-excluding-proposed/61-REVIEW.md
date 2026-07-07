---
phase: 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed
reviewed: 2026-07-07T09:18:48Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/QuestFinalizedEditTests.cs
  - QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Views/Quest/Edit.cshtml
  - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 61: Code Review Report

**Reviewed:** 2026-07-07T09:18:48Z
**Verified/updated:** 2026-07-07 (orchestrator follow-up — see "Verification Update" below)
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found (1 warning fixed, 1 critical reclassified as info and then fixed after live verification, 1 info note unchanged)

## Summary

This phase relaxes the server-side block on editing finalized quests, adds a Total Player Count floor guard (D-01), skips `ProposedDates`/`FinalizedDate` mutation for finalized quests (D-04), and adds an "Edit Quest" entry point to `Manage.cshtml`/`Manage.Mobile.cshtml`. The controller-level and server-side changes are sound: the `IsFinalized` guard removal, the `updateProposedDates: !existingQuest.IsFinalized` wiring, and the player-count floor validation all match the documented decisions (D-01 through D-04), and the repository call path confirmed correct — passing `updateProposedDates: false` causes the repository to skip touching `ProposedDates` entirely, so the roster and `FinalizedDate` are provably preserved (matches the `PersistsWithoutWipingRoster` test).

However, the client-side submit-blocking script (`_QuestFormScripts.cshtml`, shared by both Edit views and unmodified by this phase) unconditionally requires a `datetime-local` input inside `#proposed-dates` before allowing form submission. Because the Proposed Dates block — and its container div — no longer renders at all when `Model.IsFinalized` is true, **every real browser submission of a finalized-quest Edit form will be silently blocked** by this pre-existing script, even though the server now accepts the request. This defeats the actual deliverable of the phase for real users; only the integration tests (which POST directly via `HttpClient`, bypassing the browser DOM/JS entirely) pass. This is the most severe issue found and should block sign-off until addressed, even though the offending line itself sits outside the file list touched by this phase's diff — it was previously unreachable for finalized quests (hard 400 BadRequest) and is now reachable and broken.

Two lower-severity issues were also found: the new Total Player Count floor guard runs before Campaign-quest sanitization, so a finalized quest that is (abnormally) on a Campaign board would be floor-guarded against a stale unsanitized value; and the `EditQuestViewModel.CanEditProposedDates` field is now fully vestigial when `IsFinalized` did the real gating job in the views (pre-existing dead code, worth a cleanup note but not introduced by this phase).

## Verification Update (orchestrator follow-up)

CR-01 below (as originally filed) claimed the finalized-quest Edit form can never be submitted from a real browser. This was investigated directly (source inspection of `_Layout.cshtml`/`_Layout.Mobile.cshtml` plus a DOM test of the exact selector via a live preview browser) and **the premise does not hold**: `_QuestFormScripts.cshtml`'s `document.querySelector('form')` (first-match, not scoped to the Quest form) actually binds to the navbar's **Logout form** — `_Layout.cshtml:187` and `_Layout.Mobile.cshtml:158` both render a `<form asp-controller="Account" asp-action="Logout">` *before* `@RenderBody()` (line 211 / 179 respectively), and that Logout form is present on every authenticated page, including Quest Edit. `querySelector('form')` returns the first form in document order, so it selects the Logout form, not the Quest Edit/Create form. Confirmed empirically: a DOM fragment reproducing the finalized-Edit markup (`#proposed-dates` present with only hidden/readonly-text date inputs, matching the non-finalized case too) returns 0 matches for `input[type="datetime-local"]`, same as the finalized case — yet the actual Quest form submission was never gated by this script in either case, because the listener was never attached to it.

**Reclassified as IN-02** (below) rather than a blocking critical: this is a real, pre-existing bug in `_QuestFormScripts.cshtml`/`_Layout.cshtml` (unmodified by this phase, and already latent before Phase 61 for the identical reason on non-finalized Edit and Create pages), not something Phase 61 introduced or something that blocks this phase's actual deliverable. It has been flagged as a separate follow-up task rather than fixed here, to avoid scope creep into files this phase doesn't own.

WR-01 (Total Player Count guard vs. Campaign board) **has been fixed** — see the "Fix Applied" note under WR-01.

## Critical Issues

~~### CR-01: Finalized-quest Edit form can never be submitted from a real browser — client-side validation always blocks it~~

**Reclassified — see "Verification Update" above and IN-02 below. Retained verbatim for audit trail; do not treat as an open blocker.**

### CR-01 (original filing, superseded): Finalized-quest Edit form can never be submitted from a real browser — client-side validation always blocks it

**File:** `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml:14` (root cause), exposed by `QuestBoard.Service/Views/Quest/Edit.cshtml:68-106` and `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml:80-107`

**Issue:** The shared submit-handler script queries `document.querySelectorAll('#proposed-dates input[type="datetime-local"]')` on every form submit and calls `e.preventDefault()` if no matching input has a value:

```js
form.addEventListener('submit', function(e) {
    const dateInputs = document.querySelectorAll('#proposed-dates input[type="datetime-local"]');
    let hasValidDate = false;
    dateInputs.forEach(function(input) {
        if (input.value) { hasValidDate = true; }
    });
    if (!hasValidDate) {
        e.preventDefault();
        alert('Please provide at least one proposed date and time.');
        return false;
    }
    ...
});
```

Both `Edit.cshtml` and `Edit.Mobile.cshtml` now wrap the entire `#proposed-dates` div in `@if (!Model.IsFinalized) { ... }`. For a finalized quest, this div (and every input inside it) is absent from the rendered DOM. `dateInputs` is therefore always an empty `NodeList`, `hasValidDate` stays `false`, and the submit handler unconditionally calls `e.preventDefault()` and shows the alert — for every finalized-quest edit, regardless of what the DM changed.

Before this phase, `Edit` GET/POST hard-blocked finalized quests with a 400 before this script was ever reachable in that state, so the bug was latent (the only other place `#proposed-dates` is absent is Campaign board quests, which is a separate pre-existing gap). This phase is the first to route a real, supported user flow through this code path with the block missing, so the finalized-quest Edit page now renders successfully (satisfying the GET-side tests) but is unusable via the UI for its POST — the actual feature this phase exists to deliver. The included integration tests do not catch this because they construct and POST the form payload directly via `HttpClient`, never executing the page's JavaScript.

**Fix:** Scope the "at least one date" guard to only apply when the Proposed Dates section is actually rendered, e.g. by checking for the container's existence:

```js
form.addEventListener('submit', function(e) {
    const container = document.getElementById('proposed-dates');
    if (container) {
        const dateInputs = container.querySelectorAll('input[type="datetime-local"], input[type="hidden"][name*="ProposedDates"]');
        let hasValidDate = false;
        dateInputs.forEach(function(input) {
            if (input.value) { hasValidDate = true; }
        });
        if (!hasValidDate) {
            e.preventDefault();
            alert('Please provide at least one proposed date and time.');
            return false;
        }
    }

    const submitButton = document.querySelector('button[type="submit"]');
    ...
});
```

Note the selector also needs to account for existing dates being rendered as `input[type="hidden"]` (not `datetime-local`) in `Edit.cshtml`/`Edit.Mobile.cshtml` — the current check only recognizes `datetime-local`, so even a non-finalized Edit page with only pre-existing (unedited) dates and no newly-added ones would incorrectly report `hasValidDate: false`. Recommend testing this fix against: (a) a finalized quest edit, (b) a non-finalized quest edit with only original hidden-input dates, (c) a non-finalized quest edit with a newly added `datetime-local` date, and (d) the Create/CreateFollowUp forms (unaffected, still all `datetime-local`).

## Warnings

### WR-01: Total Player Count floor guard runs before Campaign-quest sanitization, using an unsanitized value

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:222-231`

**Issue:** The new D-01 floor guard:

```csharp
if (existingQuest.IsFinalized)
{
    var selectedPlayerCount = existingQuest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player);
    if (viewModel.Quest.TotalPlayerCount < selectedPlayerCount)
    {
        ModelState.AddModelError("Quest.TotalPlayerCount", ...);
    }
}
```

runs at line 222, before the Campaign-quest sanitization block at line 249 (`if (boardType == BoardType.Campaign) { viewModel.Quest.TotalPlayerCount = 0; ... }`). `TotalPlayerCount` is hidden from the Edit form when `boardType == BoardType.Campaign` (see `Edit.cshtml:42`), so the browser never submits it and it model-binds to its C# default of `0`. If a quest is ever both `IsFinalized == true` and on a Campaign board (an inconsistent-but-not-impossible state — e.g. legacy data, or a group's board type changed after a quest was finalized under the old OneShot type), this guard would always fail for any quest with `selectedPlayerCount > 0`, since `0 < selectedPlayerCount` is always true, permanently blocking the edit with a validation error the DM has no way to satisfy (the field that would need raising isn't even shown).

Per `61-CONTEXT.md`, "Campaign-board quests never use `IsFinalized`/`Finalize`" under normal operation, so this is a defensive/data-integrity edge case rather than a reachable bug through the current UI — but the guard as written has no defense against it.

**Fix:** Scope the floor guard to only run for OneShot-board quests, mirroring the same defensive posture already used elsewhere in this method for board-type-conditional fields:

```csharp
var boardType = await GetActiveBoardTypeAsync(token); // hoist above the guard

if (existingQuest.IsFinalized && boardType != BoardType.Campaign)
{
    var selectedPlayerCount = existingQuest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player);
    if (viewModel.Quest.TotalPlayerCount < selectedPlayerCount)
    {
        ModelState.AddModelError("Quest.TotalPlayerCount", ...);
    }
}
```

**Fix Applied (2026-07-07, commit `70aaa2a`):** `boardType` was hoisted above the guard and `&& boardType != BoardType.Campaign` added, exactly as suggested. Full test suite re-run after the change: 191/191 unit tests, 361/361 integration tests, 0 build errors/warnings.

### WR-02: `EditQuestViewModel.CanEditProposedDates` is now fully vestigial

**File:** `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs:10`, set at `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:176,215`

**Issue:** `CanEditProposedDates` is hardcoded to `true` in both the GET and POST actions (`var canEditProposedDates = true;`) and is not referenced anywhere in `Edit.cshtml` or `Edit.Mobile.cshtml` — the views now gate the Proposed Dates section purely on the new `Model.IsFinalized` flag. This property was already trending toward dead weight before this phase (pre-existing), but this phase's introduction of `IsFinalized` as the real gating signal makes `CanEditProposedDates` redundant rather than just unused — it's computed, threaded through the view model, but has zero effect on rendering or behavior.

**Fix:** Not required for this phase's scope, but flagging for a future cleanup pass: remove `CanEditProposedDates` from `EditQuestViewModel` and the two `var canEditProposedDates = true;` locals in `QuestController`, unless another consumer depends on it (checked: `QuestBoard.UnitTests/ViewModels/CreateQuestViewModelTests.cs` tests it directly, so removal would need to update that test file too — out of scope here, noted for awareness only).

## Info

### IN-01: `Edit.cshtml`/`Edit.Mobile.cshtml` "Quest Editing Tips" sidebar duplicates HTML instead of sharing a single finalized/non-finalized message

**File:** `QuestBoard.Service/Views/Quest/Edit.cshtml:130-159`

**Issue:** The tips sidebar now branches into two nearly-parallel `<ul class="list-unstyled">` blocks (finalized vs. non-finalized), each with hand-duplicated `<i class="fas fa-check text-warning me-2"></i>` markup per list item. This is a minor readability/maintainability nit — not a functional defect — since the two branches are mutually exclusive and simple.

**Fix:** No action required; noting only because a future edit to the icon/styling would need to be applied in two places instead of one. Could be flattened with a single loop over a `List<string>` of tip strings if this pattern grows further.

### IN-02 (fixed, commit `88c9a8f`): `_QuestFormScripts.cshtml`'s submit-validation script binds to the navbar's Logout form instead of the Quest form (pre-existing, out of phase scope)

**File:** `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml:10` (`document.querySelector('form')`), root-caused via `QuestBoard.Service/Views/Shared/_Layout.cshtml:187,211` and `_Layout.Mobile.cshtml:158,179`

**Issue:** `document.querySelector('form')` returns the first `<form>` in document order. Both the desktop and mobile layouts render an authenticated-user Logout form in the navbar *before* `@RenderBody()`, so on any Quest Create/Edit/CreateFollowUp page the script's submit listener attaches to the Logout form, not the Quest form. Practical effect: the "at least one proposed date" client-side check has never actually gated the real Quest form (server-side `ModelState` validation on Create already enforces this independently, so the feature still works today) — but it does mean clicking **Logout** from a Quest Create/Edit page can incorrectly trigger `e.preventDefault()` plus the "Please provide at least one proposed date and time" alert, silently blocking logout, whenever `#proposed-dates` has no filled `datetime-local` input (e.g. a finalized-quest Edit page where the container doesn't render at all, or a Create page before any date is added).

This is **not introduced by Phase 61** — the misattached `querySelector('form')` and the layout's form ordering both predate this phase and are unmodified by it. It was investigated only because it was the root cause behind the CR-01 filing above. Initially flagged as a separate follow-up task (outside this phase's file scope), then fixed directly in the same session at the user's request rather than left pending — see commit `88c9a8f`.

**Fix applied:** `_QuestFormScripts.cshtml` now selects `document.querySelector('form[action*="/Quest/"]')` instead of the bare `document.querySelector('form')`. Fixing that exposed a second, previously-masked bug: the date-input check only counted `input[type="datetime-local"]`, missing the `input[type="hidden"][name*="ProposedDates"]` inputs `Edit.cshtml` uses for already-existing dates — once the listener attached to the real form, that would have newly blocked submission of any non-finalized edit that didn't add a brand-new date. Fixed to count both input types, and to skip the requirement entirely when `#proposed-dates` isn't rendered at all (finalized quests, Campaign board). Verified via DOM eval against all four scenarios (Create/empty, non-finalized edit with only pre-existing dates, non-finalized edit with a newly-added date, finalized edit with no dates section) plus a full `dotnet test` re-run (191 unit + 361 integration, all passing).

---

_Reviewed: 2026-07-07T09:18:48Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
