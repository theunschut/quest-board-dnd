---
phase: 96-account-and-pickers-ui-conversion
verified: 2026-11-12T17:30:00Z
status: passed
score: 15/16 must-haves verified
behavior_unverified: 1
overrides_applied: 0
behavior_unverified_items:
  - truth: "The Delete confirmation dialog takes focus on open, traps it, returns it to the Delete button on close and closes on Escape (D-11); Copy's manual fallback inside the QR dialog stays inside the focus trap"
    test: "On the Profile with one subscription, open Delete from the keyboard, Tab through the dialog, press Escape, then repeat with Keep and with the real Delete. In the QR dialog with the clipboard refused (plain http), press Copy."
    expected: "Focus lands on Keep Subscription, stays inside the dialog, returns to the row's Delete button on every close path; the QR fallback reveals and selects a field inside the dialog and nothing throws."
    why_human: "The invariant is focus order across open, close and Escape, and no repo test runs a browser. The static guards pin the hooks and the script text, not the focus behaviour. The only behavioural evidence is the scratch dialog-probe (ok under both User-Agents), which lives outside the repository and predates the WR-02 fix to the QR fallback."
human_verification:
  - test: "G2-18 item 1: on a real phone, sign in, choose a board on the picker (keyboard or switch access if available), open Profile, copy an address, open and dismiss the QR, Rename and Delete dialogs, open Edit, Change Password, Forgot Password, AccessDenied signed in and out, and the signed-out landing page"
    expected: "Phone layout is served on every page except the picker (own layout); every control is at least 44px; nothing scrolls sideways at 390; dialogs return focus to their opener"
    why_human: "Real device, touch and real User-Agent; the sweep used an emulated Pixel 7 profile"
  - test: "G2-18 item 2: with a screen reader, submit an empty Log in and press Copy on a Profile row"
    expected: "The refusal line (role alert) and the Copied! status are announced; a live region becomes a gap only if either stays silent"
    why_human: "Screen-reader announcement cannot be observed programmatically"
  - test: "G2-18 item 4: QR dialog with the clipboard-denied Copy path"
    expected: "The revealed field sits inside the dialog's focus trap; nothing throws"
    why_human: "Needs a non-secure context in a real browser; the WR-02 fix landed after the AFTER sweep"
  - test: "G2-18 item 5: a real Edit with a changed email"
    expected: "The email-change confirmation round trip works"
    why_human: "Needs a mailbox the tester can read"
  - test: "G2-18 item 6: open a malformed (not spent) SetPassword link"
    expected: "The persistent 'Password reset failed' toast appears"
    why_human: "Only the spent-token path was exercised with a mailed link (96-11); the malformed-token catch path is verified by source only"
  - test: "G2-18 item 7: Log in with a wrong password at 320 and 390 with an identity error present"
    expected: "Long validation messages wrap without sideways scroll"
    why_human: "Visual check of wrapping at the narrowest widths"
  - test: "Re-look at AccessDenied signed out (phone) and the desktop Edit page after the post-Gate-2 review fixes"
    expected: "The in-sentence log in link wraps with its sentence and has a 44px tap area (WR-01); the 'Want to change your password?' line reads at 4.5:1 (WR-05)"
    why_human: "Both CSS changes (bfbd1eb5, 3300fd5b) landed after the operator's Gate 2 approval and after the AFTER sweep; the review reports clean sweeps but that evidence is not in after/final"
gaps: []
deferred: []
---

# Phase 96: Account and pickers UI conversion Verification Report

**Phase Goal:** Convert the sign-in, profile and board-picker pages (picker first, because it is the one S1 that stops a keyboard user entering the app) to the foundation and merge the five approved twins, so a keyboard user can enter the app and every account page reads at 4.5:1.
**Verified:** 2026-11-12
**Status:** human_needed
**Re-verification:** No, initial verification
**HEAD:** 4c10314f

## Goal Achievement

The goal is achieved in the codebase. The picker is now a list of native submit buttons, so a keyboard user reaches and activates every board. Every account page is on the foundation with zero literals, zero `!important` and no inline code. The five merges are done. All that remains is the human UAT list the operator accepted at Gate 2.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A keyboard user can enter the app: every board on the picker is reachable by Tab and activated by Enter/Space (A-account-031, S1) | VERIFIED | `Views/GroupPicker/Index.cshtml`: each board is `<li><form asp-action="SelectGroup" method="post">` holding the antiforgery token, hidden `groupId` and `returnUrl`, and `<button type="submit" class="group-picker-row">`. No `div`, no `onclick`, no inline style. `group-picker.css` `@layer pages`, `:focus-visible` row rule with the foundation ring. `AccountConversionGuardTests.Picker_RowsAreNativeSubmitButtonsInTheSelectGroupForm` passes. Scratch `keyboard-picker.mjs` ok under desktop and Pixel 7 (96-12). |
| 2 | Every account page reads at 4.5:1 (S1 A-account-019 and 12 failing rows on 7 elements) | VERIFIED | AFTER `after/final/report.json` (118 records, all 200): contrast failures 0 desktop / 0 phone against BEFORE 171 / 272 (stack mode); every page row 0. Meta text 6.09:1. No hex, `rgb()` or `!important` remain in the account sheets; text uses `--qb-fg-muted`. Measured by the scratch harness, not re-rendered here. |
| 3 | The five approved merges are done: Login, ForgotPassword, SetPassword, ChangePassword, GroupPicker each one view for both User-Agents | VERIFIED | No `.Mobile.cshtml` exists for any of them (`ls Views/Account Views/GroupPicker`). Each view is one file with `@section Styles` and a single `h1`. `AccountConversionGuardTests.RetiredFiles_AreGone` passes. |
| 4 | Edit and Profile stay twins by deliberate answers (Q-82, Q-83); Q-76 to Q-81 titles and Q-84 building-key wording are aligned | VERIFIED | `Edit.cshtml`/`Edit.Mobile.cshtml` and `Profile.cshtml`/`Profile.Mobile.cshtml` both exist. Profile says "Administrator" (desktop) and "Admin" (phone). "Has Building Key"/"No Building Key" on both twins. Both Edit twins carry `ViewData["Title"] = "Edit Profile"`, both Profile twins "Profile". |
| 5 | DoC-1: zero inline `style=`, `<style>`, inline `<script>` and inline event handlers in the area's views | VERIFIED | `grep -E 'style\s*=\|<style\|<script\|\son[a-z]+\s*='` over the 11 area views returns only the two `<script type="module" src=...>` file includes. Guard `ConvertedViews_CarryNoInlineStyleScriptOrHandler` passes. |
| 6 | DoC-2: zero hex/`rgb()` and zero `!important` in the area's stylesheets | VERIFIED | grep over `account.css`, `group-picker.css`, `home.mobile.css` (shared with Phase 95) returns nothing. `account.mobile.css` is deleted and no file references it. The Profile block is gone from `legacy-pages.css` (no profile or calendar-subscription match). Counts in the 96-12 summary: hex 4 to 0, rgb 14 to 0, important 11 to 0. |
| 7 | DoC-3: the area's stylesheet loads through `@section Styles`, not the global list | VERIFIED | Every area view links `account.css` or `group-picker.css` in `@section Styles`. Neither appears in `_Layout.cshtml` or `_Layout.GroupPicker.cshtml` (only `legacy-pages.css` is global there). Guard `AreaViews_LinkTheirSheetThroughTheStylesSection` passes. |
| 8 | DoC-4: every twin aligned, every content difference answered, approved merges done | VERIFIED | See truths 3 and 4. Home merged on the operator's explicit Gate 1 yes (item 9 "yes", `gate1/answers.json`); `Home/Index.Mobile.cshtml` is gone. Button mapping aligned: Save Changes `btn-primary`, Change Password `btn-secondary` on both Edit twins. |
| 9 | DoC-5: accessibility bar A-1 to A-11 verified on each page, desktop and mobile | VERIFIED | `after/final/summary.md`: tables per page and layout, 0 failures over 50 page-and-layout combinations; A-9 n/a with stated reason. Skip link first, one `main`, one `h1`, 0 unnamed icon controls, autocomplete tokens present (`autocomplete="username"`/`current-password` seen in Login; guard `CredentialViews_CarryTheirAutocompleteTokensAndOneHeading` passes). Evidence is scratch-harness output; see human item on the post-gate CSS changes. |
| 10 | DoC-6: BEFORE and AFTER screenshots stored outside the repository; relaid-out pages signed off | VERIFIED | `~/qb-ui-review/phase-96/{before,after,gate1,gate2}`; Gate 2 sheet has 94 `data-pair` ids. Gate 1 approved (picker flat rows, Profile intrinsic reflow, Home merge) and Gate 2 approved with no rejected pairs, answers in both `answers.json`. Caveat below on how the Gate 2 approval reached the executor. |
| 11 | DoC-7: mobile checks listed in the UAT and run under a mobile User-Agent | VERIFIED | Every manifest row ran under both User-Agents with `expectMobileLayout` matched (96-12). The UAT list is G2-18 in `96-12-SUMMARY.md` and is carried here as human verification. SetPassword was checked with a freshly mailed token under both User-Agents (96-11, local SMTP sink; `rendered`, `submitted`, `signedIn`, `reuseRefused` all true). |
| 12 | DoC-8: guard tests rewritten to the same intent, none deleted | VERIFIED | `MobileViewsTests` (Login, GroupPicker, Edit, Profile facts, +328/-lines), `ProfileCalendarSubscriptionTests`, `CalendarSubscriptionStaticGuardTests`, `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests` all exist; new `AccountConversionGuardTests` (17 facts). The only test files ever deleted in the repo are the unrelated shop-seed tests (commit 894e5523, not this phase). Unit 1389 and integration 1894 pass at HEAD (re-run here). |
| 13 | DoC-9: `Html.LocalTime`/`Html.WallClock` untouched; nothing writes to `Events` or `Quests` | VERIFIED | Diff since the first area commit changes no non-test `.cs` file. The only `LocalTime` lines in the diff are re-indentation of the same two Profile calls (Created, Last fetched). The review fixture `event.single` was reused, so no new Events row was written. |
| 14 | DoC-10: at merge, every page still works and reads | VERIFIED | Full suite green at HEAD (1389 + 1894, SQL Server required). AFTER sweep: all 118 records HTTP 200, 0 console or page errors. SetPassword end to end verified with a mailed token. |
| 15 | Phase extras: foundation items delivered first; AFTER adds Profile with one subscription, AccessDenied signed out, Login validation state; AccessDenied overflow fix reused on `Events/Details` | VERIFIED | Foundation tokens in use (`--qb-fg-muted`, `--qb-hit-min`). AFTER rows `profile-one(-qr,-rename,-delete)`, `accessdenied-out`, `login-errors/invalid` exist. Overflow: AccessDenied 394 to 390 at 390 and 416 to 412 at 412; `Events/Details` 394 to 390 and 416 to 412 (the fix is the shared `shell.mobile.css` rule, +9 lines). |
| 16 | The Delete dialog takes focus, traps it, returns it to the opener and closes on Escape; the no-script path still posts (D-11) | PRESENT_BEHAVIOR_UNVERIFIED | Present and wired: `profile-subscriptions.js` (179 lines) opens `Modal.getOrCreateInstance(deleteDialog).show(opener)`, focuses `[data-subscription-name-target]`'s Keep button on `shown`, restores focus on `hidden`; no `confirm()` anywhere. Delete is a real submit button in its own form, so it posts without script. Guards pin the hooks and the script text. No repo test runs a browser; the scratch `dialog-probe.mjs` reported ok but is outside the repository and predates the WR-02 change. Routed to human verification. |

**Score:** 15/16 truths verified (1 present, behaviour unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Views/GroupPicker/Index.cshtml` | One merged view, flat rows as submit buttons | VERIFIED | 56 lines, wired to `SelectGroup`, member count reads "1 member"/"N members" |
| `wwwroot/css/group-picker.css` | Layered row styles, focus lift | VERIFIED | 104 lines, `@layer pages`, no literals |
| `wwwroot/css/account.css` | Account sheet absorbing the Profile block and the phone sheet | VERIFIED | 167 lines, linked by 10 views |
| `wwwroot/js/profile-subscriptions.js` | One ES module for both Profile twins | VERIFIED | 179 lines, loaded as `type="module"` by both twins |
| `Views/Account/{Login,ForgotPassword,SetPassword,ChangePassword}.cshtml` | One view each | VERIFIED | `.Mobile` twins deleted |
| `Views/Account/{Edit,Profile}{,.Mobile}.cshtml` | Four twin files | VERIFIED | |
| `Views/Home/Index.cshtml`, `Views/Shared/AccessDenied.cshtml` | Converted | VERIFIED | One `h1` each, no inline code |
| `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs` | Area guard | VERIFIED | 17 facts, passing |
| `account.mobile.css`, `*.Mobile.cshtml` of merged views | Retired | VERIFIED (gone) | No references remain |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Picker row button | `GroupPickerController.SelectGroup` | form post with token, `groupId`, `returnUrl` | WIRED | `GroupPickerControllerIntegrationTests` and `CrossBoardPickerSkipTests` green |
| Profile views | `profile-subscriptions.js` | `@section Scripts` module | WIRED | both twins |
| Profile Delete button | revoke POST | native form submit; script upgrades to dialog | WIRED | `ProfileDelete_StillPostsWithoutScript` passes for both twins |
| Dialog hooks | script lookups | `#deleteSubscriptionModal`, `[data-subscription-name-target]`, `[data-keep-subscription]`, `input[name="id"]` | WIRED | pinned per view by `ProfileDeleteDialog_CarriesEveryHookTheScriptReads` |
| Area views | `account.css` / `group-picker.css` | `@section Styles` | WIRED | |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| Picker rows | `Model.Groups`, `MemberCount` | `GroupPickerController` view model from the board service | Yes (member counts "10/2/1" seen live in 96-12) | FLOWING |
| Profile rows | `subscription.Name`, `CreatedAt`, `LastFetchedAt`, `WebcalAddress` | `AccountController` subscription service | Yes (AFTER frames with one, three and ten rows) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Unit suite | `dotnet test QuestBoard.UnitTests` (SQL env set) | 1389 passed, 0 failed | PASS |
| Integration suite | `dotnet test QuestBoard.IntegrationTests` (SQL env set) | 1894 passed, 0 failed | PASS |
| No inline code in area views | grep for style, `<style>`, `<script>`, `on*=` | only the two module includes | PASS |
| No literals in area sheets | grep hex, `rgb`, `!important` | none | PASS |
| No `confirm()` | grep | none | PASS |
| No planning ids in area source | broad grep over views, sheets, script | none | PASS |

### Probe Execution

No `probe-*.sh` is declared by this phase. SKIPPED. The scratch browser probes named in 96-12 (`keyboard-picker`, `dialog-probe`, `copy-probe`) live outside the repository and were not re-run here.

### Requirements Coverage

The PLAN frontmatter IDs (D-01 to D-11, DoC-1 to DoC-10, X-1 to X-4) are phase decision and Definition-of-converted IDs. None is in `.planning/REQUIREMENTS.md`, and the roadmap lists the phase's requirements as "TBD". They are accounted for against the plans and summaries below. No REQUIREMENTS.md entry maps to Phase 96, so there are no orphaned requirements.

| ID | Source | Meaning | Status | Evidence |
|----|--------|---------|--------|----------|
| D-01 | 96-03, 04, 05, 06 | Hybrid sign-off: mockups approved before build | SATISFIED | Gate 1 approved picker and Profile mockups before 96-05/06 built them |
| D-02 | 96-03, 04, 12 | Two operator gates | SATISFIED | `gate1/answers.json`, `gate2/answers.json` |
| D-03 | 96-04, 12 | Rejected AFTER iterated until approved | SATISFIED | Nothing rejected; Gate 1 redraw loop recorded in 96-04 |
| D-04 | 96-01, 02 | Re-capture BEFORE from the current branch | SATISFIED | `before/`, `before-stack/`, `foundation-src` |
| D-05 | 96-01, 02 | Operator checkpoint and fixtures through app forms | SATISFIED | 96-01/02 summaries, `fixtures.json` |
| D-06 | 96-05 to 12 | Slices on the milestone branch, one release at the end | SATISFIED | 43 phase commits on `milestone/v9-rolling-improvements`; no tag created |
| D-07 | 96-03, 04, 10 | Home merge only on explicit yes | SATISFIED | Gate 1 item 9 "yes"; merged in 96-10 |
| D-08 | 96-03, 10 | Unified Home look | SATISFIED | Centred glass card, `h1` title, body-contrast sentence, Log in button |
| D-09 | 96-11 | Mailed SetPassword link | SATISFIED | Verified with a real token via a local SMTP sink (operator approved the substitution) |
| D-10 | 96-04, 07 | Empty-state clause reworded | SATISFIED | "The address you create acts like a password" on both twins |
| D-11 | 96-04, 07 | Opaque modal replaces `confirm()` | SATISFIED in code; focus behaviour is truth 16 (human) | |
| X-1 | 96-05 | Foundation items present | SATISFIED | Tokens and `.modern-card--interactive` in use |
| X-2 | 96-12 | AFTER extras | SATISFIED | See truth 15 |
| X-3 | 96-11 | SetPassword with mailed token | SATISFIED | See truth 11 |
| X-4 | 96-08, 09, 12 | AccessDenied overflow fix reusable | SATISFIED | See truth 15 |
| DoC-1 to DoC-10 | various | Definition of converted | SATISFIED | Truths 5 to 14 |

Note: 96-04-SUMMARY lists `requirements-completed` without D-10 and D-11 although its PLAN declares them. Both are completed by 96-07, so nothing is missing.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs` | `AreaFiles_CarryNoPlanningReference` | Pattern list is narrower than the project rule (review IN-04, left open) | Info | A broad manual grep over the area's views, sheets and script found no planning id, so no leak exists today |
| area views | various | Duplicate antiforgery inputs (IN-01), repeated validation summary (IN-02, accepted at G2-12), test names (IN-03), `Random.Shared` group ids in `MobileViewsTests` (IN-05) | Info | Review items left open on purpose; none affects the goal |

No `TBD`, `FIXME`, `XXX`, `TODO`, `HACK` or placeholder marker in the area's files. No blocker anti-pattern.

### Human Verification Required

See the frontmatter `human_verification` list. Seven items: the G2-18 UAT checks 1, 2, 4, 5, 6 and 7 (real device, screen reader, clipboard-denied QR, email-change round trip, malformed SetPassword link, long messages at 320 and 390), plus a re-look at AccessDenied (signed out, phone) and desktop Edit because their CSS changed after the Gate 2 sweep. Item 3 of G2-18 is the expected-results statement and is folded into item 1.

### Findings that are not gaps

- **Gate 2 approval path (WARNING, recorded, not blocking).** The approval ("yes looks good, approved") was typed into the orchestrating session and relayed to the executor, which first declined to treat it as the operator's own; the orchestrator then took responsibility. Items G2-01 to G2-18 were recorded at their recommendations because the operator gave no item answers, and G2-11 was changed by the orchestrator's decision. This verification relies on the orchestrator's statement that Gate 1 and Gate 2 were approved by the operator, as the task instructions say.
- **Post-gate code changes (WARNING).** Six small changes landed after Gate 2 and after the AFTER sweep (bfbd1eb5 to d9ec25fc): inline link padding on AccessDenied, `account-prompt` on Edit, the QR Copy fallback, a QR button label, a removed HTML comment. The review reports clean follow-up sweeps for them, but that evidence is not in `after/final`. Hence the re-look item above.
- **G2-17 (known, foundation-owned).** The picker's fixed bar wraps at 320 desktop and covers about 10px of the first element. It is identical in BEFORE and clean at 390; logged as a foundation follow-up and not part of this phase's definition of converted.
- **Evidence provenance.** Contrast, overflow, focus-ring and target-size results come from the scratch harness in `~/qb-ui-review/phase-96/`. I checked that the records exist and agree with the code (the fixes they cite are present), but did not re-render the pages.

### Gaps Summary

No gaps. Every roadmap contract item and the Definition of converted hold in the code at HEAD 4c10314f, and both test projects pass with SQL Server. The one behaviour-dependent truth, the Delete dialog's focus handling, has present and wired code but no repository test that exercises it, so it is carried with the real-device and screen-reader UAT items as `human_needed`.

---

_Verified: 2026-11-12_
_Verifier: Claude (gsd-verifier)_
