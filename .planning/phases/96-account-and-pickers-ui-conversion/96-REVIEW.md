---
phase: 96-account-and-pickers-ui-conversion
reviewed: 2026-11-12T00:00:00Z
depth: standard
files_reviewed: 25
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs
  - QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  - QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  - QuestBoard.Service/Views/Account/ChangePassword.cshtml
  - QuestBoard.Service/Views/Account/Edit.cshtml
  - QuestBoard.Service/Views/Account/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Account/ForgotPassword.cshtml
  - QuestBoard.Service/Views/Account/Login.cshtml
  - QuestBoard.Service/Views/Account/Profile.cshtml
  - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/Account/SetPassword.cshtml
  - QuestBoard.Service/Views/GroupPicker/Index.cshtml
  - QuestBoard.Service/Views/Home/Index.cshtml
  - QuestBoard.Service/Views/Shared/AccessDenied.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
  - QuestBoard.Service/wwwroot/css/account.css
  - QuestBoard.Service/wwwroot/css/group-picker.css
  - QuestBoard.Service/wwwroot/css/legacy-pages.css
  - QuestBoard.Service/wwwroot/css/shell.mobile.css
  - QuestBoard.Service/wwwroot/js/profile-subscriptions.js
findings:
  critical: 0
  warning: 5
  info: 5
  total: 10
status: issues_found
---

# Phase 96: Code Review Report

**Reviewed:** 2026-11-12
**Depth:** standard
**Files Reviewed:** 25
**Status:** issues_found

## Summary

The converted views are sound against their controllers. Antiforgery, hidden inputs, `returnUrl` and SetPassword's `userId` and token are all intact:

- Login echoes `returnUrl` through `asp-route-returnurl`. The controller passes it to the picker, and both picker actions only follow it through `Url.IsLocalUrl`.
- SetPassword keeps `UserId` and `Token` as hidden `asp-for` inputs and renders the token nowhere else.
- Every POST form carries a token. The tag helper already adds one, so the explicit `@Html.AntiForgeryToken()` is redundant (IN-01).
- The script writes the subscription name with `textContent` only. No `innerHTML` is built from `dataset`.
- No view carries an inline script, style or handler. `type="module"` is deferred, so `window.bootstrap` from the classic bundle is present when it runs.
- No GSD IDs were found in the changed source, comments, test names or strings.

No blockers. The findings are:

- one real behavioural defect in the phone CSS (WR-01);
- a Copy fallback that cannot work inside the QR dialog, carried over unchanged from the old script (WR-02);
- three test-quality gaps, where guards can pass vacuously or leave the script/view contract unpinned (WR-03, WR-04, WR-05).

## Warnings

### WR-01: `.mobile-layout .account-link` turns the in-sentence "log in" link into a 44px inline-flex box

**File:** `QuestBoard.Service/wwwroot/css/account.css:133-137` (consumer: `QuestBoard.Service/Views/Shared/AccessDenied.cshtml:39`)
**Issue:** The rule sets `display: inline-flex; min-height: var(--qb-hit-min)` on every `.account-link` under the phone layout. It was written for the stand-alone "Forgot password?" link on Login. AccessDenied also puts `account-link` on the "log in" anchor inside the sentence "Please log in to access this page."

On a phone the anchor becomes an atomic inline-flex box at least 44px tall in the middle of running text. That inflates the line height. It also stops the link text from wrapping with the surrounding words: if the link falls near a line end, the whole box jumps to the next line. This is a visible layout regression on the phone AccessDenied page.

**Fix:** Do not give the inline link the stand-alone treatment. Either drop `account-link` from the AccessDenied anchor (`alert-link` is enough), or scope the rule to the block case:
```css
.mobile-layout .mb-3 > .account-link { display: inline-flex; align-items: center; min-height: var(--qb-hit-min); }
```
or put the tap-target rule on a dedicated `.account-link-block` class used only by Login.

### WR-02: Copy's manual fallback cannot work from the QR dialog

**File:** `QuestBoard.Service/wwwroot/js/profile-subscriptions.js:40-52` (triggered from `Profile.cshtml:233` and `Profile.Mobile.cshtml:203`)
**Issue:** Each QR dialog has its own Copy button, and it targets the hidden address input in the page behind the dialog. When the clipboard API is missing or denied (plain-http self-hosts), `showManualFallback` removes `visually-hidden` from that input and calls `field.focus()` and `field.select()`.

Bootstrap's modal focus trap redirects focus back into the dialog. The input stays unfocused behind the backdrop, so Ctrl+C copies nothing and the revealed field is covered. The button reads "Press Ctrl+C" and the live region announces it, but nothing is selected.

The behaviour was inherited from the old inline script, but this phase moved the script into a module and added the status announcement, so it now actively announces a copy path that does not exist from the dialog.

**Fix:** In the fallback, detect that the field is outside the open dialog (`button.closest('.modal')`) and either render a second read-only input inside the dialog body as the selectable address, or close the dialog first and reveal the field once `hidden.bs.modal` fires.

### WR-03: `ProfileScript_FillsTheDialogNameAsTextOnly` can pass while the guarantee is broken

**File:** `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs:221-235`
**Issue:** The test is meant to pin that the member-supplied name reaches the dialog only as text. It does not.

- `script.Should().Contain(".textContent = ")` is already satisfied by the unrelated `status.textContent = message` in `announce()` and by `submitButton.textContent = 'Adding…'`. Deleting the dialog's `textContent` line, or replacing it with `innerHTML`, still passes this assertion.
- The `innerHTML` check is per line and only fires when `dataset` is on the same line. This passes:
  ```js
  const name = trigger.dataset.subscriptionName;
  target.innerHTML = name;
  ```
  It also misses `outerHTML`, `document.write`, `Range.createContextualFragment`, and anything split across lines.

**Fix:** Pin the actual statement and forbid sinks for the whole script:
```csharp
Regex.IsMatch(script,
    @"querySelector\('\[data-subscription-name-target\]'\)\.textContent\s*=\s*trigger\.dataset\.subscriptionName")
    .Should().BeTrue("the dialog's name slot is filled with textContent from the trigger's dataset");
script.Should().NotMatchRegex(@"\.(?:innerHTML|outerHTML)\s*=\s*(?!restHtml\b|iconOnly\b|'<)");
```
Alternatively, allow `innerHTML` only on the two literal-string restore and success lines and fail on every other occurrence.

### WR-04: The view-to-script contract for the Delete dialog is not pinned, and a missing dialog silently removes the confirmation

**File:** `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs:237-275`; `QuestBoard.Service/wwwroot/js/profile-subscriptions.js:119-133, 135-152`
**Issue:** The script depends on hooks in the dialog markup: `#deleteSubscriptionModal`, `[data-subscription-name-target]`, `input[name="id"]` inside the dialog's revoke form, and `[data-keep-subscription]`. No test asserts any of them in either view. The guard that comes closest (`ProfileDelete_OnlyTheScriptOpensTheDialog`) only checks that the script text contains `getOrCreateInstance(`, `preventDefault()` and `hidden.bs.modal`. Those strings also appear in comments, so the assertion is a substring match and not a behaviour check.

The failure modes are asymmetric:
- If the dialog id or the whole dialog block goes missing from one twin, `if (!deleteDialog || !Modal) return;` lets the row form post immediately. The destructive confirmation silently disappears, while `DeleteConfirmText_IsByteIdenticalBetweenLayouts` is the only thing that would notice (it fails only when the message paragraph is also gone).
- If the `input[name="id"]` or the name target is dropped, `querySelector(...)` returns `null` and the `show.bs.modal` handler throws. The dialog still opens with an empty id.

**Fix:** Add a per-view theory that asserts the dialog hooks, and tighten the script checks to code rather than comments:
```csharp
markup.Should().Contain("id=\"deleteSubscriptionModal\"");
markup.Should().Contain("data-subscription-name-target");
markup.Should().Contain("data-keep-subscription");
Regex.IsMatch(markup, "id=\"deleteSubscriptionModal\"[\\s\\S]*?asp-action=\"RevokeCalendarSubscription\"[\\s\\S]*?@Html\\.AntiForgeryToken\\(\\)[\\s\\S]*?name=\"id\"")
    .Should().BeTrue("the dialog's own revoke form carries a token and the id field the script fills");
```
In the script, guard against a missing node (`const idField = deleteDialog.querySelector('input[name="id"]'); if (idField) idField.value = ...`) so a markup drift cannot throw inside the handler.

### WR-05: `Edit.Mobile.cshtml` is now a one-line fork of `Edit.cshtml`, and that line breaks the card colour rule

**File:** `QuestBoard.Service/Views/Account/Edit.Mobile.cshtml:52-58` vs `QuestBoard.Service/Views/Account/Edit.cshtml:52-59`
**Issue:** `diff Edit.cshtml Edit.Mobile.cshtml` shows a single line: `<p class="text-muted">Want to change your password?</p>`. A 76-line twin kept for one sentence invites drift. `MobileViewsTests` already shows the cost, since it needs a dedicated test to pin the absence of the sentence on phones.

The sentence itself uses `text-muted`, a Bootstrap `!important` colour utility on a paragraph inside a card. `ui-guidelines.md` forbids this: "Do not set a text colour on paragraphs ... inside a card". It is also the case the foundation notes warn about, because the layered `text-purple`/muted handling does not own Bootstrap's important utilities.

**Fix:** Either drop the sentence from the desktop view and delete the twin (the heading and the button already say what it does), or keep one view and hide the sentence on phones with a class in `account.css`, for example `<p class="password-prompt">`, displayed only outside `.mobile-layout`. In both cases drop `text-muted`. If the twin stays, reduce it to what genuinely differs.

## Info

### IN-01: Duplicate antiforgery inputs on every POST form

**File:** `QuestBoard.Service/Views/GroupPicker/Index.cshtml:45`; `Profile.cshtml:117,137,181,196`; `Profile.Mobile.cshtml:97,111,151,166`
**Issue:** `<form asp-action=... method="post">` already makes the form tag helper emit a `__RequestVerificationToken` field. The explicit `@Html.AntiForgeryToken()` adds a second identical one. This is harmless, but the picker renders it per board and the Profile renders it per row.
**Fix:** Delete the explicit `@Html.AntiForgeryToken()` calls. The guard `body.Should().Contain("@Html.AntiForgeryToken()")` in `ProfileDelete_StillPostsWithoutScript` would then assert the tag-helper form instead.

### IN-02: `asp-validation-summary="All"` repeats every field error

**File:** `Login.cshtml:27`, `ForgotPassword.cshtml:27`, `ChangePassword.cshtml:28`, `SetPassword.cshtml:27`
**Issue:** Each form shows the summary of all errors and also renders `asp-validation-for` spans under each field. Every field error therefore appears twice, and a screen reader hears it in the `role="alert"` block and again at the field. `Edit.cshtml` correctly uses `ModelOnly` and gates the wrapper on a model-level entry.
**Fix:** Use `ModelOnly` and the same gating as Edit on Login, ForgotPassword and ChangePassword. Keep `All` on SetPassword. Its `UserId` and `Token` fields have no visible span, so a missing or bad link would otherwise show no explanation.

### IN-03: Test names no longer describe what the tests assert

**File:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs:683, 700, 1014`
**Issue:** `MobileAccountLogin_MobileUserAgent_RendersGlassCardForm`, `MobileAccountLogin_DesktopUserAgent_DoesNotRenderGlassCard`, `MobileAccountEdit_MobileUserAgent_RendersGlassCardForm` and `MobileAccountProfile_MobileUserAgent_RendersGlassCardLayout` now check for the shared card, the shared sheet and the page title, and no glass card. The leftover names mislead anyone reading a failure.
**Fix:** Rename them to match their summaries, for example `MobileAccountLogin_MobileUserAgent_ServesTheSharedViewInThePhoneShell`.

Separately, the same file still carries older "Test starts RED", "Plan 03" and "when Plan 02 lands" comments at lines 1044-1045, 1067, 1093, 1194, 1358, 1384, 1419, 1446 and 1481. None were added by this phase, but they are planning references in source and a good candidate for a cleanup pass.

### IN-04: `AreaFiles_CarryNoPlanningReference` is narrower than the project rule and skips missing files silently

**File:** `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs:76-86, 173-184`
**Issue:**
- The patterns cover only D-01..21, Q-01..19, `DoC-n`, `A-account-n`, "Phase 96", "96-01..12" and a few document names. CLAUDE.md bans every requirement, phase, plan and review-finding ID, such as `WR-03`, `31-REVIEW` or `TENANT-03`. A reference to any other phase or finding slips through.
- `if (!File.Exists(...)) continue;` makes the scan vacuous for a path that has moved or been renamed. A deleted `profile-subscriptions.js` would be skipped here and only caught elsewhere.
**Fix:** Add generic shapes, for example `\b(?:WR|CR|IN|BL)-\d{2}\b`, `\b\d{2,3}-\d{2}\b(?=[^\w])` limited to comment lines, and `\bPhase\s+\d+\b`. Replace `continue` with `File.Exists(...).Should().BeTrue(...)`.

### IN-05: Smaller leftovers

- **`wwwroot/js/profile-subscriptions.js:83-92`:** The Add button is disabled and relabelled on submit with no `pageshow` reset. When the browser restores the Profile from the back-forward cache, the button stays disabled showing "Adding…". The old inline script had the same issue. A `pageshow` listener that restores the button fixes it.
- **`Views/Account/Profile.cshtml:13-15`:** The HTML comment ("Widened from col-md-6 ... Accepted at design time as something that will dominate this page") is shipped to every client and is design-process commentary. Make it a Razor comment, `@* ... *@`, or drop it. `Profile.cshtml:39` also writes a bare `&` where the phone twin uses `&amp;`.
- **`Views/Account/Profile.cshtml:166-245`:** The dialogs block inside `@if` is indented one level too deep relative to the surrounding markup.
- **`Views/Account/Profile.Mobile.cshtml:90`:** The icon-only QR button is labelled just "Show QR Code" on every row, while the Copy, Rename and Delete buttons carry the subscription name. With several subscriptions a screen reader hears identical labels. Use `aria-label="Show QR code for @subscription.Name"` (the desktop button's visible text has the same ambiguity).
- **`wwwroot/css/account.css:118`:** `.mobile-layout .section-heading` is the only rule for that class, so on desktop the class is an unstyled hook. Either style it or drop it from the desktop views.
- **`MobileViewsTests.cs:848, 931-932`:** Group ids drawn from `Random.Shared.Next(100_000, 999_999)` against a shared in-memory fixture have a small chance of colliding across tests, which would fail with a duplicate-key error. Use a monotonically increasing counter or `Interlocked.Increment`.

---

_Reviewed: 2026-11-12_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
