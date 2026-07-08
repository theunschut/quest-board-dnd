---
phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v
reviewed: 2026-07-06T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - QuestBoard.Service/ViewModels/QuestLogViewModels/EditRecapViewModel.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
  - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs
  - QuestBoard.Service/Views/QuestLog/EditRecap.cshtml
  - QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: issues_found
---

# Phase 53: Code Review Report

**Reviewed:** 2026-07-06T00:00:00Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Reviewed the new dedicated `EditRecap` GET/POST action pair, its view model, the desktop/mobile edit views, the desktop/mobile `Details` views (which now link out to the new edit page instead of hosting an inline form), and the integration test suite. Authorization is correctly layered (policy + in-action DM/Admin ownership check, mirroring the pattern already established by `UpdateRecap`), CSRF protection is present on both POST actions via the form tag helper + `[ValidateAntiForgeryToken]`, and Razor's default HTML-encoding neutralizes XSS risk on both the recap display and the textarea echo. No critical/security-blocking issues were found.

The main findings are quality/maintainability issues: the old `UpdateRecap` action is now fully dead code with zero callers anywhere in the codebase (superseded by the new `EditRecap` POST, which is a byte-for-byte duplicate of its body), the completed-quest-guard logic is now triplicated across three action methods with no shared helper, and the mobile recap display box is missing `white-space: pre-wrap`, so multi-line recaps written through the new textarea editor will render as a single collapsed line on the mobile Details page while rendering correctly on desktop.

## Warnings

### WR-01: `UpdateRecap` action is dead code, fully superseded by `EditRecap`

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:77-117`
**Issue:** The `UpdateRecap` POST action is byte-for-byte identical in logic to the new `EditRecap` POST action (`QuestLogController.cs:158-198`): same completed-quest guard, same DM/Admin ownership check, same `UpdateQuestRecapAsync` call, same redirect. Since the `Details` view no longer renders an inline form (confirmed: no `.cshtml` file references `UpdateRecap`, and no JS/AJAX call references it either — the only remaining references are its own regression tests), this action has zero production callers. Leaving it in place means two independent authorization/business-logic code paths must be kept in sync forever, and a future contributor may not realize `UpdateRecap` is unreachable from the UI.
**Fix:** Remove the `UpdateRecap` action and its dedicated test cases (`UpdateRecap_NonOwnerAdmin_IsNotForbidden`, `UpdateRecap_Player_IsForbiddenOrRedirected`) now that `EditRecap` covers the same contract, or if the endpoint must be kept for backward-compatibility (e.g. an external caller), add a comment documenting why and consider having it delegate to a shared private method instead of duplicating the full body.

### WR-02: Completed-quest guard duplicated a third time with no shared helper

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:44-46, 91-93, 132-134, 172-174`
**Issue:** The exact same 3-line "is this quest completed" expression (`isCompletedOneShot` + `!isCompletedOneShot && !quest.IsClosed`) is now copy-pasted four times in this controller (`Details`, `UpdateRecap`, `EditRecap` GET, `EditRecap` POST) — up from three before this phase added a fourth. There is no shared helper anywhere in `QuestBoard.Domain` (confirmed via search) that centralizes this rule. Any future change to "what counts as a completed quest" (e.g., adjusting the 1-day grace window) requires editing four call sites and risks partial updates that silently desync GET/POST behavior.
**Fix:** Extract a private helper on the controller (or a domain-level method on `Quest`/`IQuestService`), e.g.:
```csharp
private static bool IsQuestLogEligible(Quest quest) =>
    (quest.IsFinalized && quest.FinalizedDate.HasValue
        && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
        && !quest.DungeonMasterSession)
    || quest.IsClosed;
```
and call `if (!IsQuestLogEligible(quest)) return NotFound();` (or `BadRequest` where applicable) from all four sites.

### WR-03: Mobile recap display box collapses newlines from the new multi-line editor

**File:** `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml:97-99`
**Issue:** `Details.Mobile.cshtml` renders `@Model.Quest.Recap` inside a `<div class="recap-display-box">`. The desktop equivalent (`quests.css:825-834`) defines `.recap-display-box` with `white-space: pre-wrap`, so line breaks entered via the new `<textarea rows="10">` in `EditRecap.cshtml` render correctly. The mobile stylesheet's same-named class (`quest-log-detail.mobile.css:59-64`) has no `white-space` rule at all, so on mobile every recap saved through the new multi-line `<textarea rows="6">` in `EditRecap.Mobile.cshtml` will display as one run-on line, losing all paragraph breaks the DM wrote. This is a direct regression risk introduced by this phase: before the dedicated editor existed, recap content had no established multi-line authoring path exercising this box.
**Fix:** Add `white-space: pre-wrap;` to `.recap-display-box` in `QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css` (note: this file was not in the reviewed file list, but is the root cause and must be touched to fix WR-03):
```css
.recap-display-box {
    background-color: rgba(255, 255, 255, 0.9);
    color: #1a1a1a;
    border-radius: 8px;
    padding: 12px;
    white-space: pre-wrap;
}
```

## Info

### IN-01: `asp-validation-summary` renders with no validation to summarize

**File:** `QuestBoard.Service/Views/QuestLog/EditRecap.cshtml:17`, `QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml:21`
**Issue:** Both edit views include `<div asp-validation-summary="All" class="text-danger mb-3"></div>`, but `EditRecapViewModel` has no data annotations (`Recap` is just `string?`) and the controller never checks `ModelState.IsValid`. The validation summary tag helper is therefore permanently inert markup that implies input validation exists when it does not.
**Fix:** Either remove the validation-summary element since there is nothing to validate, or add real validation (e.g., a reasonable `[StringLength]` cap on `Recap` matching the intended UX, with the corresponding `ModelState.IsValid` check in the POST action) if validation was actually intended.

### IN-02: Integration tests only cover the non-owner-Admin path for `EditRecap`

**File:** `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs:340-437`
**Issue:** All three new `EditRecap` tests (`EditRecap_Player_IsForbidden`, `EditRecap_NonOwnerAdmin_ReturnsOk`, `EditRecap_Post_NonOwnerAdmin_RedirectsToDetails`) exercise the Admin-not-DM path. There is no test where the quest's actual owning DM (non-Admin) hits `EditRecap` GET/POST, no test asserting `EditRecap` returns `NotFound`/`BadRequest` for a quest that isn't completed yet (the guard duplicated in WR-02), and no test asserting the recap text actually persists and is visible on the subsequent `Details` page (the redirect-target assertions only check the HTTP status code of the redirect, not the saved content).
**Fix:** Add a case authenticating as the quest's own DM (not Admin) to confirm `isQuestDm` grants access independently of `isAdmin`; add a case posting to `EditRecap` for a quest where `IsFinalized`/`IsClosed` are both false and assert `BadRequest`; and extend `EditRecap_Post_NonOwnerAdmin_RedirectsToDetails` to follow the redirect (or query the DB) and assert the new recap text is actually stored.

---

_Reviewed: 2026-07-06T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
