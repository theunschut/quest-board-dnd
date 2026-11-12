---
phase: 96-account-and-pickers-ui-conversion
plan: 07
subsystem: ui
tags: [profile, calendar-subscription, modal, es-module, a11y, guard-tests, tracer]
requires:
  - phase: 96-06
    provides: both Profile twins on account.css and the foundation card, the inline script still in place
  - phase: 96-04
    provides: Gate 1 answers (delete without script kept, icon Copy fallback, default row-button mapping)
provides:
  - profile-subscriptions.js, the one module both Profile twins load for Copy, Add, Rename and Delete
  - the Delete dialog on both twins, opened only by the module, with the browser prompt gone
  - accessible names on the row actions, the approved button mapping, the reworded empty-state clause
  - both Profile views in the inline-code guard, plus facts pinning the no-script post and the module-only opening
affects: [96-08, 96-09, 96-10, 96-12]
tech-stack:
  added: []
  patterns: [dialog opened through the Modal API from the form's own submit listener, captured-markup restore for a state-swapping button, one status element for a transient result]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/profile-subscriptions.js
  modified:
    - QuestBoard.Service/Views/Account/Profile.cshtml
    - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs
    - QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs
key-decisions:
  - "The Delete button stays a real submit button in its own per-row form with no Bootstrap modal attribute; only the module opens the dialog, in the submit listener that cancels the post"
  - "Copy restores each button's own captured innerHTML and className, so the phone's icon-only Copy stays icon-only through success and the fallback"
  - "The status element is cleared when the button restores, so a second copy changes the text again and is announced again"
requirements-completed: [D-06, D-10, D-11, DoC-1, DoC-4, DoC-5, DoC-8, DoC-10]
duration: ~1.7 h
completed: 2026-11-12
status: complete
actuals:
  tokens: 26000
  tasks: 2
  commits: 2
---

# Phase 96 Plan 07: Profile script, dialogs and row buttons (second half) Summary

Deleting a subscription now opens an accessible dialog on both Profile layouts instead of the browser prompt, from one shared module, while a row's Delete still posts the revoke with JavaScript off or the module missing. The empty-state clause no longer points at an address that is not there, every row action has a name that starts with its visible word, and the Profile page reads at 4.5:1 or better in every state and dialog on both User-Agents.

## What was built

- **`profile-subscriptions.js`.** A module that imports nothing, with a plain-language header saying both layouts load it. It holds:
  - Copy: clipboard write, success state, and the clipboard-denied fallback that reveals, focuses and selects the field. Each button's `innerHTML` and `className` are captured once and restored after two seconds (the old helper's unused parameters are gone). The phone's icon-only Copy stays icon-only. A pending restore is cleared if the button is clicked again.
  - Add: the disable-and-`Adding…` courtesy.
  - Rename: the dialog fill from `event.relatedTarget.dataset` and the focus on `shown.bs.modal`.
  - Delete: a `submit` listener on each `.calendar-subscription-delete-form` that, only when `#deleteSubscriptionModal` exists and `window.bootstrap.Modal` is defined, calls `preventDefault()`, keeps the opener (`event.submitter` or the form's submit button) and calls `getOrCreateInstance(dialog).show(opener)`. Otherwise it does nothing and the form posts. `show.bs.modal` fills the hidden `id` and the name span with `textContent`; `shown.bs.modal` focuses Keep Subscription; `hidden.bs.modal` focuses the kept opener while it is still connected.
  - Every lookup is a no-op when its element is absent.
- **Both twins.** The inline script is gone and `@section Scripts` loads the module with `type="module"` and `asp-append-version`. Each row's revoke form is unchanged (antiforgery token, hidden id, a real submit button); the button gained `data-subscription-id` and `data-subscription-name`, no Bootstrap modal toggle or target, and its `aria-label`.
- **Delete dialog** (`#deleteSubscriptionModal`, one per page, outside the card, under the any-subscriptions condition): `h2.modal-title` Delete Subscription, the close button, a `<p id="deleteSubscriptionMessage">` carrying today's sentence verbatim with the name in a `data-subscription-name-target` span, then Keep Subscription (`btn-secondary`, dismiss, `data-keep-subscription`) and the dialog's own revoke form (antiforgery token, hidden `id`, Delete Subscription `btn-danger` with a decorative `fa-trash`). That form does not carry the per-row class, so the module never cancels its submit.
- **Task 2 on both twins.**
  - Empty-state clause: only `The address below acts like a password` became `The address you create acts like a password`.
  - The hidden address input has `tabindex="-1"`.
  - Row mapping: Copy, Open in Calendar App and Show QR Code are `btn-secondary btn-sm` (the QR dialog's Copy and Open too); Rename stays `btn-warning`; Delete stays `btn-danger`.
  - Row `aria-label`s: `Copy address for NAME` (desktop and the phone's icon-only Copy, plus the phone QR dialog's icon-only Copy), `Rename NAME`, `Delete NAME`. The phone's icon-only Show QR Code keeps its label.
  - Dialog titles are `h2.modal-title`; the Rename dialog reads Keep Current Name (`btn-secondary`) and Save Name (`btn-primary`).
  - One visually hidden `role="status"` element (`data-copy-status`) per page, filled with `Copied!` or `Press Ctrl+C to copy`.

## Guard facts

- **`CalendarSubscriptionStaticGuardTests`:**
  - `Copied!` and `Press Ctrl+C` left `CopyContractStrings`; new fact `ScriptContractStrings_LiveInTheSharedScript` holds them on the script and requires both views to load it.
  - `DeleteConfirmText_IsByteIdenticalBetweenLayouts` now extracts the inner text of `<p id="deleteSubscriptionMessage">` from each view (single-line mode), requires both non-empty, equal and containing the two pinned halves, and requires no `confirm(` in either view.
  - `NeitherLayout_MakesAForbiddenClaim` also scans the script.
  - The empty-state sentence is held in one constant (`EmptyStateSentence`), and the `CopyContractStrings` entry uses it.
- **`ProfileCalendarSubscriptionTests`:** the empty-state test uses one `EmptyStateSentence` constant; the truncation test's because-message names `account.css`. No test was deleted or weakened.
- **`AccountConversionGuardTests`:**
  - Both Profile views joined `ConvertedViews` (so the inline style, script and handler scan covers them), and the script joined the planning-reference scan.
  - New facts: `ProfileViews_LoadTheSharedScriptAsAModule`, `ProfileScript_FillsTheDialogNameAsTextOnly`, `ProfileDelete_StillPostsWithoutScript`, `ProfileDelete_OnlyTheScriptOpensTheDialog`.
  - One extra fact: `ProfileRows_NameTheirActionsAndKeepTheHiddenAddressOutOfTheTabOrder` (the order-independent `tabindex="-1"` check on the address input, the three row labels, the dialog labels, the status element, and no `h5` titles).
  - The tests marked Theory run over both twins.

## Probe results (both User-Agents, real app on 8000)

`dialog-probe.mjs` printed `ok` twice (after Task 1 and again on the final code). Per User-Agent (desktop 1440, Pixel 7 with touch):

- **A, keyboard on the multi account:**
  - Tab reached the first row's Delete and Enter opened the dialog.
  - Focus started on Keep Subscription, the name span and the hidden id matched the row, and 20 Tab presses never left the dialog.
  - Escape, Keep Subscription and the close button each closed it with focus back on that Delete.
  - No revoke request was sent from the first Enter through the last close, and the subscription count was unchanged.
- **B, a real delete:** a temporary subscription was added, its Delete opened from the keyboard, Delete Subscription confirmed from the keyboard, the `Subscription deleted.` toast appeared and the row count returned to its fixture value.
- **C, no script (`javaScriptEnabled: false`):** a temporary subscription was added and its Delete activated. The revoke POST was sent, the response was the Profile page, the row count was back to its fixture value and no dialog was visible. **A row's Delete still posts without JavaScript.**
- **D, script blocked with Bootstrap loaded:** the module request was aborted and Bootstrap's Modal was present. Enter on the Delete sent exactly one plain revoke post, a MutationObserver watching every `.modal` for the `show` class reported no dialog at any point, and the row count returned to its fixture value. **A plain post and no dialog.**

`copy-probe.mjs` printed `ok`: with clipboard permission the button turns `btn-success`, the status reads `Copied!`, an icon-only button stays icon-only with its `aria-label` unchanged, and after 2.5 s `innerHTML` and `className` equal their rest values and the status is cleared. With `writeText` rejected, the desktop button reads `Press Ctrl+C`, the phone's icon-only Copy keeps its icon (Gate 1 "icon"), the field is revealed and focused, the status reads `Press Ctrl+C to copy`, and the button restores. No screenshot was taken in that state, and the address is never printed.

## Sweep and readability-compare (`after/96-07`, stack mode)

`sweep.mjs --stack` over `profile-empty`, `profile-one`, `profile-three`, `profile-ten`, `profile-one-qr`, `profile-one-rename` and `profile-one-delete`: 37 captures, 0 non-200.

- **readability-compare with `--zero-contrast` on all seven rows and the empty exceptions file: `failingRows: 0`, `residualContrast` 0, exit 0, `ok`.** Zero contrast failures on every Profile state and on all three dialogs under both User-Agents. `contrast-exceptions.json` holds no entries, and none belonged to this slice.
- Overflow is false and `smallTargets` is empty on every record, including the delete row at 320 and 390.
- **allow.json** (`after/96-07/allow.json`, generated by a scratch script that aligns BEFORE and AFTER controls): the relabelled controls (`Copy` to `Copy address for NAME`, `Rename` to `Rename NAME`, `Delete` to `Delete NAME`, `Cancel` to `Keep Current Name`, `Save` to `Save Name`), the desktop `main` landmark's text, the hidden address inputs (hidden on the phone since the previous plan), and the positional button keys that moved when the new Delete dialog added three buttons. One positional key, `button#4` in the rename row, shifted on desktop but not on the phone, so it is listed as removed there rather than renamed (the allow file is per row, not per User-Agent). The delete row is AFTER-only by design.
- **diff.py** against the approved mockup, desktop 1440 delete dialog: 6.0% differing, all of it the fixture's real name and dates (the dates line wraps onto a second line in the mockup, shifting the rows below by about 20px), the row-button text, and the focus ring on Keep Subscription, which is focused in the capture. The dialog's size, spacing, title, body, buttons and colours match. The overlay is in `after/96-07/diff/`. On the phone, the two footer buttons wrap onto two lines at 412px (Keep above Delete Subscription); this is the footer's natural wrap, not a defect.

## Wording shipped for Gate 2

Empty-state sentence: "Get this board's schedule onto your phone's own calendar app. The address you create acts like a password — anyone who has it can see your events, dates, and times, and it never expires on its own. Press Add Subscription to create one; nothing is generated until you do."

Delete dialog message: `Delete "NAME"? Any device using this address will silently stop receiving updates the next time it checks — this cannot be undone. You can always create a new subscription afterward.` The Rename dialog buttons read Keep Current Name and Save Name; Copy status messages are `Copied!` and `Press Ctrl+C to copy`.

## Deviations from Plan

### Environment substitutions (operator-approved, see ENVIRONMENT.md)

Port 8000 for 8096, `python3` for `python`, bundled Chromium and the `qb-audit-*` accounts. The scratch sweep switch `--stack` was used on the AFTER sweep.

### Auto-fixed issues

None needed.

### Plan wording and process

- **Tracer gate.** `workflow.auto_advance` and `_auto_chain_active` are unset, which strictly makes the run interactive. As in the two previous slices, the orchestrator ran this plan as an autonomous sequential execution, so the gate was met by running the tracer's own verify end to end before Task 2: the pinned tests (222 passed) and the dialog probe (`ok` under both User-Agents, including the no-script and script-blocked passes). The probe was run again on the final code.
- **`Adding…` is set with `textContent`**, not `innerHTML`: the label replaces the icon exactly as before, with no markup written.
- **Extra guard fact.** `ProfileRows_NameTheirActionsAndKeepTheHiddenAddressOutOfTheTabOrder` was added beyond the plan's list, because the acceptance criteria for Task 2 were otherwise only checked by the probes.
- **The phone QR dialog's icon-only Copy** also takes the longer `aria-label`, so every icon-only Copy on the page carries the subscription's name.
- **ROADMAP.md.** The working-tree edit that was uncommitted at the start of the run was committed by its owner (cfd45638) in the meantime, so the progress update below is a normal commit.

## Findings closed

A-account-022 (the phone's icon-only Copy returns to its own icon-only markup after one use, and the result is announced), A-account-023 (the Profile script lives in one module, no inline script on either twin), A-account-024 (the hidden address field is out of the tab order while the fallback still focuses it explicitly), A-account-025 (the Profile dialogs sit on the foundation's overlay surface with `h2` titles; the phone sweep reports no target under 44px with a dialog open) and A-account-014 (advanced as the plan scoped it: the row actions carry accessible names that start with the visible word and name the subscription, with the Gate 1 role mapping applied). The browser confirm on Delete is replaced by the keyboard-safe dialog (D-11).

## Known Stubs

None.

## Threat Flags

None new. T-96-17: the name only ever becomes text (`textContent` on the name span, `ProfileScript_FillsTheDialogNameAsTextOnly` fails on any `innerHTML` or `insertAdjacentHTML` line that reads a dataset value). T-96-18: the dialog's form and each row's kept form carry `@Html.AntiForgeryToken()` (`ProfileDelete_StillPostsWithoutScript`); `Mutations_AreRejectedWithoutAnAntiforgeryToken` stays green. T-96-19: the controller's ownership check is untouched and `RevokeSubscription_DoesNothing_ForAnotherMembersSubscription` stays green. T-96-34: the row's Delete carries no Bootstrap modal attribute and the dialog is opened only inside the listener that cancels the post; `ProfileDelete_OnlyTheScriptOpensTheDialog` pins it and the script-blocked probe pass confirmed that with the module missing there is a plain post and no dialog. T-96-20: the hidden address field is out of the tab order, the address is in no data attribute or the status element, and the copy probe took no screenshot of the revealed state.

## Verification results

- `dotnet build`: 0 errors.
- Full `dotnet test` with the SQL-backed tests required: unit 1389 passed, integration 1881 passed, 0 failed.
- App on 8000 restarted on the final code and left running.

## Self-Check: PASSED

- Files present: `profile-subscriptions.js`, both Profile views, the three edited test files.
- Commits 1381887f (Task 1) and f0a4dbc4 (Task 2) exist on `milestone/v9-rolling-improvements`.
- `dialog-probe.mjs` and `copy-probe.mjs` printed `ok`; `readability-compare` printed `ok`.
