---
phase: 96
slug: account-and-pickers-ui-conversion
# verified: every register entry closed at audit (34 mitigated, 3 accepted from the plan registers, 1 transferred); no operator decision outstanding.
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on (high). Blocking gate.
threats_open: 0
threats_total: 38
asvs_level: 1
created: 2026-11-12
---

# Phase 96 - Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Audited against dbdee86d, the commit before the first area change (`git diff dbdee86d..HEAD`; first area commit f29749a2). HEAD is cbd9a7b1. The frozen review baseline is e84ca9e0 (`~/qb-ui-review/phase-96/baseline-src/BASELINE_SHA`). Implementation files were read-only.
> Register: 12 plan `<threat_model>` blocks, 38 entries and 35 distinct ids. Only `T-96-SC` repeats (plans 96-01 to 96-04, same supply-chain threat, a different mitigation statement in each), so it is keyed by plan. Numeric ids 01 to 34 do not collide. Plan map: 96-01 SC, 01-04; 96-02 SC, 05-07; 96-03 SC, 08-09; 96-04 SC, 10-11; 96-05 12-14; 96-06 15-16; 96-07 17-20, 34; 96-08 21-24; 96-09 25-26; 96-10 27; 96-11 28-30; 96-12 31-33.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser to SelectGroup POST | Each picker row is a submit button in its own form with the antiforgery token, the hidden `groupId` and the hidden `returnUrl` | board id, return URL, antiforgery token |
| Non-member and nonexistent-board picker responses | Two bodies an attacker could compare to learn board membership | rendered picker markup |
| Browser to the credential and Edit POSTs | Login, ForgotPassword, SetPassword, ChangePassword and Edit forms go through the form tag helper | credentials, reset token, user id, profile fields, email-change request |
| Password managers to form fields | Autocomplete tokens tell the client what each field is | field purpose |
| Server-rendered Profile to browser and clipboard | The never-expiring calendar subscription address is in a hidden input and the webcal link, and Copy writes it to the clipboard | bearer credential |
| Member-supplied subscription name to the Delete dialog | `profile-subscriptions.js` writes the name from a Razor-encoded data attribute | user-authored text |
| Browser to the add, rename and revoke POSTs | The Delete dialog's form id is filled by script; each row also keeps its own no-script form | subscription id, antiforgery token |
| Public pages (Home landing, AccessDenied) | Shown to signed-out visitors | static copy |
| Server authorisation (unchanged by this phase) | `git diff --quiet dbdee86d HEAD` exits 0 for `QuestBoard.Service/Controllers`, `Authorization`, `Extensions`, `ViewModels`, `Program.cs`, `appsettings.json`, `QuestBoard.Domain`, `QuestBoard.Repository`, Dockerfile, docker-compose.yml and `.github`. No `.csproj` or package file changed | role decisions, ownership checks |
| Review tooling (outside the repository) | `~/qb-ui-review/phase-96`: credentials.json, session states, captures, mockups, baseline-src, foundation-src, and Playwright through a symlink to the Phase 94 install | review-account passwords, session cookies, screenshots of review fixtures |
| Local app to SMTP (96-11) | The mailed SetPassword check ran against a local sink on 127.0.0.1:2525. The committed relay is 192.168.6.13:25 | one reset mail |
| Review sheets to operator | Gate 1 and Gate 2 sheets | mockups and captures |
| Release pipeline | A tag push triggers docker-publish.yml (GHCR). The operator owns this | container image |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|----------|-----------|----------|-------------|-----------------------|--------|
| 96-01/T-96-SC | Tampering | npm `playwright-core` | high | mitigate | 96-01-SUMMARY.md:41: the operator approved Part A at the blocking-human Task 1 checkpoint, reusing the Phase 94/95 playwright@1.64.1. `tools/package.json` pins `"playwright": "1.64.1"`. `tools/node_modules` is a symlink to `~/qb-ui-review/phase-94/tools/node_modules`; its playwright and playwright-core are 1.64.1, installed 2026-11-01. There is no lockfile or other node_modules in the phase-96 tree. No browser was downloaded: the `~/.cache/ms-playwright` builds date from 2026-10-26. No package file in the repository; `git grep -i playwright` outside `.planning` finds nothing. The Edge-to-bundled-Chromium switch was operator-approved (`ENVIRONMENT.md:1,6`: "No channel 'msedge', no Edge, and no npm install") | closed |
| 96-02/T-96-SC | Tampering | npm `playwright-core` | high | mitigate | Reused as installed. The only node_modules is the symlink above; no new package, lockfile or install appeared in 96-02 (96-02-SUMMARY.md:16 `added: []`) | closed |
| 96-03/T-96-SC | Tampering | npm `playwright-core` | high | mitigate | Same as 96-02. 96-03-SUMMARY.md:16 `added: []`; the mockups use copied foundation assets, not packages (96-03-SUMMARY.md:91) | closed |
| 96-04/T-96-SC | Tampering | npm `playwright-core` | high | mitigate | Same as 96-02. No package change in 96-04, and no `package*.json`, `.csproj` or lockfile changed in `git diff dbdee86d..HEAD` | closed |
| T-96-01 | Information disclosure | credentials.json, state/*.json | medium | mitigate | `~/qb-ui-review/phase-96/credentials.json` is mode 600. `~/qb-ui-review` is 700 and `~` is 750, so no other local user can reach it. All 15 `state/{8000,8001}/*.json` files are mode 600. All 9 accounts are `qb-audit-*@example.invalid`. Contents were never printed; searches report counts and paths only. No password is in the repository working tree or the scratch tree outside credentials.json (0 files), and none is in git history (`git log --all -S`, 0 commits for each of the 9). `git grep qb-audit-\|qb96-` matches nothing outside `.planning`. Claude transcripts: the passwords for multi, noboard and the rotated player3 (96-11) appear in no transcript, so phase 96 echoed none. 6 reused Phase 95 passwords are still in two Phase 95 executor transcripts; that predates this phase and is covered by 95 AR-10 | closed |
| T-96-02 | Elevation of privilege | Review Admin and SuperAdmin accounts | medium | mitigate | Operator sign-off at 96-01-SUMMARY.md:41. The privileged accounts were reused from Phase 95; only `multi` and `noboard` were created, both as Players (`tools/register.mjs:25-26`). They were made through the review admin's session (`register.mjs:30`, `stateFor('admin')` is qb-audit-admin), not the operator's login. credentials.json holds only the 9 qb-audit roles. `roles-check.json` passes 4/4. The apps on 8000 and 8001 listen on 127.0.0.1 and [::1] only, and their database connections go to 127.0.0.1:1433 (`ss`). `appsettings.json` is unchanged | closed |
| T-96-03 | Information disclosure | reset tokens and calendar addresses in reports | high | mitigate | `tools/audit-shoot.mjs:43-44` `redact` replaces `token=` values with REDACTED. It is applied to url, finalUrl, requests, failed requests, console and page errors (:88-93, :99, :130). `:73` aborts `/feeds/calendar/`; `:86-87` record `{ dialog: true, type }` only. The redaction check is at 96-01-SUMMARY.md:46. The real-token run used `setpassword-verify.mjs:16,55,130`, which imports `redact`. Scan of every json, md, html, log and eml file in the scratch tree (values classed, never printed): 76 `token=render-only` placeholders, 5 `token=REDACTED`, 0 other values. A search of the scratch tree, repository and transcripts for unredacted SetPassword tokens finds 0 files. Caveat: `sweep.mjs`, which produced the BEFORE and AFTER records, imports neither `redact` nor the abort (`sweep.mjs:16,159-163,181`). Only the `render-only` placeholder ever went through it, so nothing leaked; see Unregistered Flags | closed |
| T-96-04 | Information disclosure | screenshots of real member data | medium | mitigate | A dedicated review board (Audit Board, id 7) and fantasy fixture boards (96-02-SUMMARY.md:45,51-60). The super admin picker lists real board names. The row is `noSamples` (no name in any report; 96-02-SUMMARY.md:110), its captures stay in scratch, and Gate 2 blurs it (6 of 6 BEFORE/AFTER super admin images carry a blur job in `gate2/jobs.json`; AFTER 1440 spot-checked). The one-time Gate 1 branch push carried no super admin capture (see Unregistered Flags) | closed |
| T-96-05 | Tampering | fixture creation | low | mitigate | `tools/*.mjs` and `*.py` contain no SQL (grep for sqlcmd, INSERT, UPDATE ... SET, DELETE FROM, mssql: none). `fixtures.mjs` drives the app's forms (`page.fill` and `click`, lookup before create at :50), and `rec` keeps `created: true` across re-runs (:19-24). `fixtures.json`: 8 keys `created: true`, and `event.single` reused (`created: false`, `reusedFrom: phase-94`). A second run created 0 (96-02-SUMMARY.md:45) | closed |
| T-96-06 | Information disclosure | calendar subscription addresses of fixture accounts | medium | mitigate | The control key holds role and accessible name only, never an href or value (`tools/probes.mjs:282-290`). Fixture rows were counted by delete form and names were read from the Rename trigger (96-02-SUMMARY.md:62). Grep gate: no real `/feeds/calendar/<id>` or `webcal://` in any scratch json, md, html or log; the only hits are the `example.invalid` mockups. All 14 fixture subscriptions were revoked (`logs/revoke-fixtures.log`: player 1, multi 3, dm 10, 0 left; 96-12-SUMMARY.md:139-141). Caveat: the feed-request abort exists only in `audit-shoot.mjs:73`, not in `sweep.mjs` or `fixlib.mjs`. No manifest page requests a feed (the QR is inline SVG, and the BEFORE Profile capture reads "Never fetched yet"), so nothing was recorded. 96-02-SUMMARY.md:122 "aborted by the harness" overstates this | closed |
| T-96-07 | Information disclosure | baseline-src, before/ captures | low | accept | AR-01. Rationale re-checked: `baseline-src/BASELINE_SHA` = e84ca9e0 (git archive); text samples are capped at 40 characters (`tools/probes.mjs:119`); captures show review fixtures. Caveat: the premise "no secrets" is inexact, because committed `QuestBoard.Service/appsettings.json:17` holds a vestigial SQL `User Id`/`Password` beside `Trusted_Connection=true`. That is pre-existing, unchanged and already public | closed (accepted) |
| T-96-08 | Information disclosure | mockups and the Gate 1 sheet | low | accept | AR-02. The mockups use fantasy names (96-03-SUMMARY.md:113), and the sheets reference no external host (`gate1/index.html`, `gate1-review/index.html`, `home-merge.html`: 0 http(s) URLs). Caveat: the package was pushed once to the public branch `feature/phase-96-gate1-review` and then deleted (96-04-SUMMARY.md:94); `git ls-remote --heads origin` now shows no phase-96 or review branch | closed (accepted) |
| T-96-09 | Tampering | style injection in overflow-compare.mjs | low | accept | AR-03. `tools/overflow-compare.mjs:69-71` uses `page.addStyleTag` only, and its one write is its own JSON output (:99). Nothing posts to the app | closed (accepted) |
| T-96-10 | Information disclosure | Profile mockup | medium | mitigate | The `mockups/v1/profile/index.html` links are `https://example.invalid` and `webcal://example.invalid` only. The address value is `https://example.invalid/feeds/calendar/EXAMPLE-NOT-A-REAL-ADDRESS.ics`. The QR area is an SVG reading "QR placeholder" (:335-342). The `gate1-review` copy is the same | closed |
| T-96-11 | Repudiation | Gate 1 answers | low | mitigate | `gate1/answers.json`: `source` records the operator's own AskUserQuestion answer ("Approve all, 9:yes"); 16 items; `picker.version` and `profile.version` "v1". The same record is at 96-04-SUMMARY.md:25-46 | closed |
| T-96-12 | Tampering | SelectGroup POST (cross-site request) | high | mitigate | `Views/GroupPicker/Index.cshtml:44-45` `<form asp-action="SelectGroup" method="post">` with `@Html.AntiForgeryToken()`. The button is `type="submit"` (:48). `GroupPickerController.cs:75-77` has `[HttpPost][ValidateAntiForgeryToken]` and is unchanged (Controllers diff empty). Guard `AccountConversionGuardTests.cs:173-183` passes | closed |
| T-96-13 | Information disclosure | picker markup as a membership oracle | high | mitigate | The hidden `groupId` and `returnUrl` lines (`Index.cshtml:46-47`) are byte-identical to baseline `Index.cshtml:42-43` and `Index.Mobile.cshtml:39-40`. `returnUrl` appears nowhere else in the view or `_Layout.GroupPicker.cshtml`. The guard pins one `name="returnUrl"` and no `formaction` or `data-return-url` (`AccountConversionGuardTests.cs:180-182`). `CrossBoardPickerSkipTests.cs:150` `NonMemberReturnUrl_AndNonexistentReturnUrl_ProduceIndistinguishablePickerResponses` is unchanged and passes (re-run at audit) | closed |
| T-96-14 | Tampering | open redirect via returnUrl | medium | mitigate | The hidden input value is unchanged (`Index.cshtml:47`). `GroupPickerController.cs:51` (Index) and `:96` (`RedirectToLocal`) still use `Url.IsLocalUrl`. Login keeps `asp-route-returnurl` (`Login.cshtml:22`) and redirects only to the picker (`AccountController.cs:146`) | closed |
| T-96-15 | Information disclosure | subscription address made visible by a layout change | high | mitigate | The address input keeps `visually-hidden` on both twins (`Profile.cshtml:100`, `Profile.Mobile.cshtml:82`) and is rendered only as an input value and the pre-existing webcal href (baseline also has 2 hrefs). `account.css:48-60` `.calendar-subscription-address` has no `!important`, so Bootstrap's `.visually-hidden` (`width:1px!important ... position:absolute!important`, `app.css:19` layer vendor) still wins until the fallback removes the class (`profile-subscriptions.js:63`). The WR-02 dialog copy is created only on that path (:42-55). 96-06-SUMMARY.md:99: the phone input is now reported hidden and the sweep screenshots show no address (spot-checked BEFORE Profile) | closed |
| T-96-16 | Tampering | rename, add and revoke POSTs inside new wrappers | medium | mitigate | Every Profile POST form keeps its token: desktop :113-114 (row revoke), :133-134 (add), :192-193 (rename); phone :96-97, :110-111, :165-166. The `asp-action` sets match baseline, plus the new dialog revoke form. `ProfileCalendarSubscriptionTests.cs:495` `Mutations_AreRejectedWithoutAnAntiforgeryToken` and `:442` `RevokeSubscription_DoesNothing_ForAnotherMembersSubscription` pass (re-run). The test diff only changes the empty-state constant and a because-message | closed |
| T-96-17 | Tampering / Elevation of privilege | stored XSS through the subscription name in the Delete dialog | high | mitigate | `data-subscription-name="@subscription.Name"` is Razor-encoded (`Profile.cshtml:116`, `Profile.Mobile.cshtml:99`). `profile-subscriptions.js:162` assigns `.textContent = trigger.dataset.subscriptionName`. Rename uses input `.value` (:123-124). The only `innerHTML` writes are the button's own captured markup and literals (:25,34,87-89). The WR-02 copy is built with `createElement`, the `.value` property and `insertAdjacentElement` (:45-52). Guard `AccountConversionGuardTests.cs:234-255` strips comments, pins the textContent statement, bans `insertAdjacentHTML`, `document.write` and `createContextualFragment`, and allows only literal markup assignments. It passes (re-run) | closed |
| T-96-18 | Tampering | cross-site request on the shared revoke form | high | mitigate | The dialog's form carries its own token (`Profile.cshtml:177-178`, `Profile.Mobile.cshtml:150-151`), and each row form keeps one (:114, :97). Pinned by `ProfileDelete_StillPostsWithoutScript` (`AccountConversionGuardTests.cs:260-281`) and `ProfileDeleteDialog_CarriesEveryHookTheScriptReads` (:300-328). `AccountController.cs:411-414` Revoke has `[HttpPost][Authorize][ValidateAntiForgeryToken]`. `Mutations_AreRejectedWithoutAnAntiforgeryToken` passes | closed |
| T-96-19 | Tampering | tampered hidden id revoking another member's subscription | high | mitigate | `AccountController.cs:416,423` `RevokeAsync(id, user.Id, token)`; ownership is decided by the service against the signed-in user and is unchanged (Controllers diff empty). `ProfileCalendarSubscriptionTests.cs:442` passes (re-run) | closed |
| T-96-20 | Information disclosure | bearer address in the tab order or in markup attributes | medium | mitigate | `tabindex="-1"` on the hidden input (`Profile.cshtml:100`, `Profile.Mobile.cshtml:82`), pinned at `AccountConversionGuardTests.cs:337-338`. The address appears only in `value=` and `href=` (grep: no `data-*` attribute carries `HttpsAddress` or `WebcalAddress`). `data-address-target` holds a selector. `announce()` writes only the literal "Copied!", "Press Ctrl+C to copy" and "" (`profile-subscriptions.js:12-16,69,90`). The dialog copy's `data-dialog-address` attribute is empty and its value is a property (:48,51). `tools/copy-probe.mjs:7` takes no screenshot and has no `screenshot` or `readText` call | closed |
| T-96-21 | Tampering | cross-site request on the four credential forms | high | mitigate | Form tag helper with `method="post"` and no `asp-antiforgery` override: `Login.cshtml:22`, `ForgotPassword.cshtml:22`, `SetPassword.cshtml:22`, `ChangePassword.cshtml:23`. `AccountController.cs:50-51,96-97,134-135,327-328` keep `[ValidateAntiForgeryToken]`. The token-extracting flows in `AccountControllerIntegrationTests` (e.g. :244, :299, :351) pass (re-run) | closed |
| T-96-22 | Information disclosure | account enumeration via Login or ForgotPassword messages | medium | mitigate | The view diffs add no message copy; the summary keeps rendering controller ModelState. The controller is unchanged ("Invalid login attempt." at `AccountController.cs:167`; the forgot path at :53-). `ForgotPassword_Post_KnownAndUnknownEmail_ShouldReturnSameGenericMessage` (`AccountControllerIntegrationTests.cs:287`) passes | closed |
| T-96-23 | Information disclosure | SetPassword token handling | high | mitigate | `SetPassword.cshtml:31-32` `<input type="hidden" asp-for="UserId|Token" />`. `AccountConversionGuardTests.cs:446-454` pins exactly one token reference, and it passes. No script on the view. Controllers unchanged, so no logging was added. The real mailed token was exercised only in 96-11 | closed |
| T-96-24 | Spoofing | weakened credential entry | low | mitigate | Autocomplete tokens: `Login.cshtml:33,39` username and current-password; `ForgotPassword.cshtml:33` email; `SetPassword.cshtml:36,42` new-password; `ChangePassword.cshtml:34,40,46`. Pinned at `AccountConversionGuardTests.cs:350-361`. The area views contain no `paste`, `onpaste` or `autocomplete="off"`, and no inline script or handler (grep; `ConvertedViews_CarryNoInlineStyleScriptOrHandler`) | closed |
| T-96-25 | Tampering | Edit form (cross-site request, over-posting) | high | mitigate | `Edit.cshtml:22` and `Edit.Mobile.cshtml:22` form tag helper; hidden `Id` at :31 in both. `ViewModels` diff is empty. `AccountController.cs:250-253` has `[HttpPost][ValidateAntiForgeryToken][Authorize]`, and the action loads the user from `GetUserAsync(User)` (:257), not from the posted Id | closed |
| T-96-26 | Information disclosure | AccessDenied revealing roles or policies | low | mitigate | `Views/Shared/AccessDenied.cshtml` is static: no model, and only the `IsAuthenticated` branch. The text diff vs baseline only moves `@ViewData["Title"]` and markup. `AccountControllerIntegrationTests.cs:27-37` asserts no `Dungeon Master`; it is unchanged and passes | closed |
| T-96-27 | Information disclosure | signed-out landing showing board data | low | mitigate | `Views/Home/Index.cshtml` is static (no model). `HomeControllerIntegrationTests.cs:58-77` `Index_WithQuests_ShouldNotDisplayQuestList` still asserts the seeded quest titles are absent, and passes (re-run) | closed |
| T-96-28 | Information disclosure | reset link and token | high | mitigate | `~/qb-ui-review/phase-96/secrets/` is mode 700 and empty, and no `.eml` remains. `after/96-11/report.json` URLs are redacted (`setpassword-verify.mjs:55,130,170`). `logs/smtp-sink.log` records subjects only. No unredacted token is in the scratch tree, transcripts or repository (scan: 0 files), and git history holds no token file (repository unchanged by 96-11) | closed |
| T-96-29 | Elevation of privilege | token used to take over an account | medium | mitigate | The check ran on review account qb-audit-player3 (`@example.invalid`; account chosen by the coordinator, the sink approved by the operator; 96-11-SUMMARY.md:22-23,56-63). The mail never left the machine. `after/96-11/report.json` summary: `reuseRefused: true` (`invalid-token-summary-on-form`), and the second password does not sign in | closed |
| T-96-30 | Repudiation | sending left enabled after the check | low | mitigate | Both running apps (pids for 8000 and 8001) run in `ASPNETCORE_ENVIRONMENT=Development` with no `EmailSettings__*` variable (`/proc/<pid>/environ`, names only). `appsettings.Development.json` sets `SuppressSending: true`. No scratch tool, log or config holds the override, and nothing listens on 2525. Deviation: the restart ran as a separate step, not inside the script (96-11-SUMMARY.md:78) | closed |
| T-96-31 | Information disclosure | Gate 2 sheet and captures | medium | mitigate | `gate2/index.html` has 0 `token=`, no feed id and no external host. The QR captures are rendered at 760, 400 and 320 px (`gate2/jobs.json`); real board names are blurred (`tools/gate2-sheet.mjs:5`, 6 blur jobs, image checked). All 14 fixture subscriptions were revoked (`logs/revoke-fixtures.log`). No Gate 2 branch is on origin | closed |
| T-96-32 | Information disclosure | release publishing the GHCR image on tag push | medium | transfer | Transferred to the operator: 96-12-SUMMARY.md:158-160 and the `gate2/index.html` release handoff ("docker-publish.yml publishes the GHCR image on every tag push regardless of that approval ... This work creates no tag and pushes nothing"). `git tag --contains f29749a2` is empty | closed (transferred) |
| T-96-33 | Repudiation | Gate 2 decision and exception approvals | low | mitigate | 96-12-SUMMARY.md:108-133 and `gate2/answers.json`: the verbatim quote "yes looks good, approved", `rejectedPairs: []`, `exceptionsApproved: []`, and G2-01 to G2-18 answers. The record states that the approval was relayed by the orchestrating session, which took responsibility (also flagged in 96-VERIFICATION.md:160) | closed |
| T-96-34 | Tampering | revoke sent while the Delete confirmation is on screen | high | mitigate | The row Delete buttons carry no `data-bs-toggle` or `data-bs-target` (`Profile.cshtml:116`, `Profile.Mobile.cshtml:99`), and no view has `data-bs-target="#deleteSubscriptionModal"` (0 in both). `profile-subscriptions.js:141-151` opens the dialog only in the submit listener, after `preventDefault()`. Pinned by `AccountConversionGuardTests.cs:260-295` (passes). The `logs/final-dialog-probe.log` pass D (script blocked) is `ok` on both User-Agents. The trade-off is stated in Gate 1 item 14 "kept" (`gate1/answers.json`) | closed |

*Status: open · closed · open - below high threshold (non-blocking)*
*Severity: critical > high > medium > low - only open threats at or above workflow.security_block_on (high) count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-96-07 | baseline-src is a `git archive` of committed files only (e84ca9e0). Captures hold review fixtures, and text samples are capped at 40 characters. Note: committed `appsettings.json:17` holds a vestigial SQL login (pre-existing, already public) | Phase 96 plan register (96-02), carried forward at audit | 2026-11-12 |
| AR-02 | T-96-08 | Mockups and the Gate 1 sheet are fantasy text with local-only references. The package was pushed once to the public `feature/phase-96-gate1-review` and deleted; its contents (mockups, plus 9 BEFORE captures of review accounts and fixture boards with the email blurred) carry no token, address or real member data | Phase 96 plan register (96-03), carried forward at audit | 2026-11-12 |
| AR-03 | T-96-09 | `addStyleTag` affects only the headless page; nothing is written to the app, database or repository | Phase 96 plan register (96-03), carried forward at audit | 2026-11-12 |

*Accepted risks do not resurface in future audit runs.*

---

## Transfers

| Threat Ref | Transferred To | Documentation |
|------------|----------------|---------------|
| T-96-32 | Operator (release timing, tag, deploy approval gate) | `.planning/phases/96-account-and-pickers-ui-conversion/96-12-SUMMARY.md:158-160` (release handoff); `~/qb-ui-review/phase-96/gate2/index.html` release note |

---

## Unregistered Flags

The SUMMARY `## Threat Flags` sections all read "None" or "None new". 96-04-SUMMARY has no such section. Every flag the executors cited maps to a registered id. The items below were found at audit; none is a new threat.

- **Observation (96-11 incident, recorded as instructed, not a new threat).** The first email-enabled start used the committed LAN relay (192.168.6.13:25) with `SuppressSending=false` for about one minute. No HTTP requests were made, and the log had 0 "sent" and 0 "skipping send" lines (96-11-SUMMARY.md:84-86). A leftover Hangfire job cannot be ruled out from the log alone. The rerun used a local sink on 127.0.0.1:2525, and `logs/smtp-sink.log` shows only the self-test and the one reset mail. At audit, neither running app has an `EmailSettings__` override and nothing connects to port 25. The operator acknowledged this at G2-15. The 96-11 Threat Flags line reads "None" although the body describes the incident.
- **Tooling gap (maps to T-96-03, T-96-06).** `tools/sweep.mjs`, which produced the 114 BEFORE and 118 AFTER records, does not import `redact` and does not abort `/feeds/calendar/`. `finalUrl` and `failedRequests` are recorded raw (`sweep.mjs:162-163,181`). Only the `render-only` placeholder ever went through it, and no address or real token is in any record. Wire both into `sweep.mjs` before a later phase reuses it with live tokens.
- **Gate 1 package on a public branch (maps to T-96-08, T-96-04).** See AR-02. The branch no longer exists on origin.
- **Post-Gate-2 code changes (bfbd1eb5 to d9ec25fc).** WR-02 adds a dialog-owned read-only address copy, created only on the clipboard-denied path; it is consistent with T-96-15 and T-96-20. WR-03 and WR-04 tighten the T-96-17 and T-96-34 guards. These landed after the AFTER sweep (96-VERIFICATION.md:161).
- **Pre-existing, outside the phase diff.** 6 of the 9 current review passwords are still in two Phase 95 executor transcripts (mode 600), covered by 95 AR-10. `QuestBoard.Service/appsettings.json:17` holds the vestigial SQL login. `~/qb-ui-review/phase-96` itself is mode 775, but its parent is 700.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-12 | 38 | 38 | 0 | gsd-security-auditor (Claude Opus 5.5) |

Method (ASVS L1: each mitigation's presence checked in the cited file or tree):
- **Diff.** Read `git diff dbdee86d..HEAD` against each mitigation. The backend, layouts other than the picker's, ViewModels, appsettings, Docker and workflow files are confirmed unchanged.
- **Views and script.** The area views and `profile-subscriptions.js` were grepped for markup sinks, inline code, paste blocking, antiforgery tokens, modal attributes and address renderings.
- **Scratch tree.** Modes were stat-checked. Passwords, reset tokens and feed addresses were byte-searched in the scratch tree, the repository, git history and the Claude transcripts; only counts, value classes and paths were printed, never a secret.
- **Running apps.** Their environment-variable names and sockets were checked.
- **Tests.** `dotnet test QuestBoard.IntegrationTests --no-build` was run on the security-relevant classes (AccountConversionGuardTests, CrossBoardPickerSkipTests, ProfileCalendarSubscriptionTests, AccountControllerIntegrationTests, HomeControllerIntegrationTests, GroupPickerControllerIntegrationTests, CalendarSubscriptionStaticGuardTests, AccountPagesRenderTests): 151 passed, 0 failed. `--no-build` left the running review app's build untouched.
- **Not re-run.** The scratch browser probes, which live outside the repository; their logs were read (`logs/final-*-probe.log`: `ok`).

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-01 to AR-03)
- [x] `threats_open: 0` confirmed (blocking gate, block_on high)
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-12
