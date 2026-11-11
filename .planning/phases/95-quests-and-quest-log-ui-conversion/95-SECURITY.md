---
phase: 95
slug: quests-and-quest-log-ui-conversion
# open, not verified: T-95-01 (medium) is open but below the block_on=high threshold, so it does not block ship.
# Flip to `verified` once T-95-01 is closed (rotate the review passwords) or the operator signs an accepted-risk entry.
status: open
# threats_open = count of OPEN threats at or above workflow.security_block_on (high). Blocking gate.
threats_open: 0
threats_total: 45
asvs_level: 1
created: 2026-11-11
---

# Phase 95 - Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Audited against baseline 7194364c (`git diff 7194364c..HEAD`), HEAD 6fc59416. Implementation files read-only.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser to app (same-origin fetch and forms) | Quest Details and Manage modules and the quest forms post with the antiforgery token read from a Razor-encoded meta element or hidden field | antiforgery token, signup ids, votes, quest and player ids |
| Razor views to the page (encoding) | Quest titles, DM and player names, character labels and recap text reach HTML and `data-*` attributes | user-authored text |
| Server authorisation (unchanged by this phase) | `DungeonMasterOnly`, `AdminOnly`, `SuperAdminOnly` policies, `IsAuthorized`, `CanManage`, `CanEditRecap`; no change under `QuestBoard.Service/Controllers`, `Authorization`, `Domain`, `Repository`, `HtmlHelperExtensions.cs` | role decisions, quest ownership |
| Static assets and cascade | Self-hosted `wwwroot/lib` EasyMDE, per-view CSS, WebP posters; no CDN | stylesheets, images, scripts |
| Review tooling (outside the repo) | `~/qb-ui-review/phase-95`: credentials.json, session states, captures, mockups, baseline-src, Playwright | review-account passwords, session cookies, screenshots of fantasy fixtures |
| Release pipeline | tag push triggers release.yml and docker-publish.yml (GHCR); owned by the operator | container image |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|----------|-----------|----------|-------------|-----------------------|--------|
| T-95-01 | Information disclosure | credentials.json, state/*.json | medium | mitigate | HELD: `~/qb-ui-review/phase-95/credentials.json` mode 600 in a mode-700 parent; all 14 `state/**/*.json` mode 600; 7 emails are `@example.invalid`; no password in any tracked or untracked repo file, in the history of any ref (`git log --all -S`, 0 hits), or elsewhere in the scratch tree; `git grep qb-audit-` finds nothing outside `.planning/` and nothing in the three source trees. NOT HELD: "never echoed to stdout": the 95-02 executor printed credentials.json once (95-02-SUMMARY.md:99); all 7 passwords are in that executor's session transcript (`~/.claude/projects/-mnt-Data-repos-quest-board-dnd/45709884-0628-492a-b7c3-516d7a288ef7/subagents/agent-a652aef1e20bb7492.jsonl`, mode 600); the 4 reused Phase 94 passwords were already in a Phase 94 plan-13 executor transcript (`.../5c828c2b-ba76-42ba-8d88-7f6f8be1ed42/subagents/agent-aa2981bd9a08cdc2b.jsonl`, mode 600). Bounded: same user and mode as credentials.json, app bound to loopback only (`ss`: 127.0.0.1 and [::1] on 8000 and 8001), throwaway accounts. To close: rotate the 7 passwords (or retire the accounts) and rewrite credentials.json at mode 600, or operator signs an accepted-risk entry. | open - below high threshold (non-blocking) |
| T-95-02 | Elevation of privilege | Review Admin and SuperAdmin accounts | medium | mitigate | 95-01-SUMMARY.md:41 operator sign-off to reuse the Phase 94 accounts; :49-62 only player2, player3 and dm2 were created (Player, Player, Dungeon Master); credentials.json roles = superadmin, admin, player, dm, player2, player3, dm2; `roles-check.json` 4/4 gates pass; `appsettings.json` unchanged vs baseline. Review DB target (user-secrets) not independently inspected (read denied); rests on the CLAUDE.md local-DB contract and 95-01-SUMMARY.md:41 ("1433 reachable") | closed |
| T-95-03 | Information disclosure | screenshots of real member data | medium | mitigate | `manifest.json` (51 rows) addresses only the Audit Board, the Campaign review board and the empty review board; no platform or Admin > Users page is captured; 1548 PNGs stay under `~/qb-ui-review/phase-95/{before,after}`; spot check of the group-picker, Admin-quests and calendar captures shows fantasy fixtures only | closed |
| T-95-04 | Tampering | fixture creation | low | mitigate | `~/qb-ui-review/phase-95/tools` has no SQL (grep for sqlcmd/INSERT/UPDATE/DELETE: none); `fixtures.mjs` drives form pages with `page.fill`/`click` (antiforgery posted by the forms); `fixtures.json` lists 39 keys, 20 with `created: true`; 95-02-SUMMARY.md: second fixtures run created 0 (title lookup) | closed |
| T-95-05 | Information disclosure | baseline-src, before/ captures | low | accept | AR-01. Rationale re-checked: `BASELINE_SHA` file present, baseline-src is a `git archive` of 7194364c; text samples capped at 40 characters (`tools/probes.mjs:87`) and omitted for move-quest, move-event, fx-admin-quests, fx-group-picker (`tools/manifest.mjs:122,124`, `noSamples`). Caveat: the plan's premise "SQL login never in a committed file" is inaccurate; `QuestBoard.Service/appsettings.json:17` holds a vestigial SQL login (pre-existing, unchanged, documented in CLAUDE.md), and baseline-src copies only what the repo already holds | closed (accepted) |
| T-95-06 | Information disclosure | mockups and Gate 1 sheet | low | accept | AR-02. Gate 1 package is scratch static HTML and PNGs of fantasy text; `feature/phase-95-gate1-mockups` is no longer on origin (`ls-remote` shows only the gate2 branch); the Gate 2 `index.html` contains no http(s) URLs | closed (accepted) |
| T-95-07 | Repudiation | Gate 1 answers | low | mitigate | `~/qb-ui-review/phase-95/gate1/answers.json` (status `gate1-answered`, `mockupVersion` "v1 posters with the v2 option-C text treatment", every item answered) and 95-03-SUMMARY.md:43 "Gate 1 answers" | closed |
| T-95-08 | Information disclosure | antiforgery token in a meta element | low | mitigate | `content="@tokens.RequestToken"` (Razor-encoded) at `Views/Quest/Details.cshtml:976`, `Details.Mobile.cshtml:700`, `Manage.cshtml:825`, `Manage.Mobile.cshtml:509`, `_QuestFormScripts.cshtml:15`; token from `Antiforgery.GetAndStoreTokens` (`Details.cshtml:14`); baseline wrote the same token into inline script strings (`Details.cshtml` baseline :983,:1004); field and header names unchanged (`quest-details.js:99,119`, `quest-manage.js:19,42`, `markdown-editor.js:50`) | closed |
| T-95-09 | Tampering | submit guard scope | medium | mitigate | `wwwroot/js/quest-form.js:172` `document.querySelector('form[action*="/Quest/"]')`, identical to baseline `_QuestFormScripts.cshtml:16`; Logout form and foreign forms are not matched. The plan's foreign-form submit probe is not recorded (95-04-SUMMARY.md:56 records the Create alert check, Contacts pages loading cleanly, selector unchanged); the control itself is intact | closed |
| T-95-10 | Tampering | editor accessible name | low | mitigate | `Views/Shared/_MarkdownEditor.cshtml:19` `data-editor-label="@Model.Label"` (Razor-encoded); `markdown-editor.js:10-11,93` reads `dataset.editorLabel` and passes it to CodeMirror `screenReaderLabel`; no label write via innerHTML | closed |
| T-95-11 | Tampering | confirm and instruction text | low | mitigate | `quest-details.js` and `quest-manage.js` contain no `innerHTML`/`outerHTML`/`insertAdjacentHTML`/`document.write` (grep); text via `createTextNode`/`textContent` (`quest-details.js:45,48,199,209`, `quest-manage.js:128`); `confirm()` text from `data-confirm-message` | closed |
| T-95-12 | Tampering | hidden Spectator DateVotes inputs | low | mitigate | `quest-details.js:35` only sets `voteSection.hidden`, so inputs still post; hidden ids at `Details.cshtml:603-620` and `Details.Mobile.cshtml:244,323`; `QuestDetailsSignupFormTests.cs:172-212` pins the Spectator post on desktop and phone | closed |
| T-95-13 | Spoofing | revoke and change-vote fetches | medium | mitigate | `quest-details.js:99-103,119-124` send `__RequestVerificationToken` from the meta to the same endpoints and verbs as baseline; server `QuestController.cs:660-663` (ChangeVote) and `:701-704` (RevokeSignup) carry `[ValidateAntiForgeryToken]` + `[Authorize]`; Controllers diff vs baseline is empty | closed |
| T-95-14 | Tampering | character-select modal data attributes | low | accept | AR-03. Rationale re-checked: modal markup and form unchanged (`_CharacterSelectModal.cshtml:33-39` posts `UpdateSignupCharacter`); the server resolves ownership via `ResolveCharacterAssignmentAsync(characterId, user.Id)` (`QuestController.cs` UpdateSignupCharacter) | closed (accepted) |
| T-95-15 | Tampering | vote radio names and values | medium | mitigate | `Views/Shared/_Calendar.cshtml:135,140,145,169,174` radios `name="DateVotes[..].Vote"` values 2/1/0 and `checked` logic untouched (diff shows only label and group markup changed; `_Calendar.Mobile.cshtml` likewise); `QuestDetailsSignupFormTests.cs:217-242` asserts names `DateVotes[` and values 2,1,0 | closed |
| T-95-16 | Denial of service | shared calendar.css edits | low | mitigate | `wwwroot/css/calendar.css` hunks at 332-350 (`.vote-buttons .btn`), 640-660 (`.details-page`, `.quest-details-page`), 675-760 (`.quest-event.current-quest`) are Details-scoped; `fx-calendar` swept (95-06-SUMMARY.md:80; later mobile fx-calendar differences traced to added fixtures, 95-17-SUMMARY.md) | closed |
| T-95-17 | Elevation of privilege | DM Controls and Manage Quest visibility | medium | mitigate | `Views/Quest/Manage.cshtml:60` and `Manage.Mobile.cshtml:59` `IsAuthorized` gate (set `QuestController.cs:978` = quest DM or admin); Manage action `[Authorize(Policy="DungeonMasterOnly")]` `QuestController.cs:959-960`; `Details.cshtml:765` and `Details.Mobile.cshtml:16,635` `ViewBag.CanManage` (same as baseline); `moveControl.CanMove` at `Manage.cshtml:695`, `Manage.Mobile.cshtml:488`; `QuestManageDmControlsTests` and `BoardMoveControlTests` changed markup selectors only, no auth assertion removed | closed |
| T-95-18 | Tampering | avatar fallback | low | mitigate | `wwwroot/js/avatar-fallback.js:5-9` toggles `hidden` only; no markup built from data | closed |
| T-95-19 | Information disclosure | phone Current Signups list | low | accept | AR-04. Rationale re-checked: desktop `Details.cshtml:896-899` shows Current Signups ungated by role under `PlayerSignups.Any()`; phone `Details.Mobile.cshtml:576-577` uses the same condition | closed (accepted) |
| T-95-20 | Elevation of privilege | poster link target | medium | mitigate | `Views/Quest/Index.cshtml:38-40,66` Manage only when `currentUserId == quest.DungeonMaster?.Id`, else Details (same rule as baseline `Index.cshtml` :83,:115; inline onclick removed); `RowNavigationAccessibilityTests.cs:111-174` pins both targets; Manage authorises server-side (`QuestController.cs:959-960,978`) | closed |
| T-95-21 | Tampering | user text in poster markup | low | mitigate | `Index.cshtml:56-66` title and DM name through Razor encoding; `pick.Poster.CssClass` and `pick.Seal` come from the constant catalog (`QuestPosterCatalog.cs:38-43,50`), seal also `Uri.EscapeDataString`; only `Html.Raw(string.Empty)` at `Details.cshtml:873`, none elsewhere in Quest, QuestLog, Move views | closed |
| T-95-22 | Denial of service | artwork weight | low | mitigate | `wwwroot/css/quests.css:211-212,226-227,241-242,256-257,271-272,286-287` `image-set` with WebP and PNG fallback; WebP derivatives 45-127 KB vs PNG 0.49-1.9 MB (`Poster1.webp` 127138 B vs `Poster1.png` 1878817 B); seals have WebP too | closed |
| T-95-23 | Elevation of privilege | phone card link target | low | mitigate | `Views/Quest/Index.Mobile.cshtml:36-38,82` same navUrl rule; baseline inline `onclick="window.location.href='@navUrl'"` (baseline :76) removed; `RowNavigationAccessibilityTests.cs:178-210` | closed |
| T-95-24 | Tampering | quest title in card markup | low | mitigate | Razor-encoded `@quest.Title` (`Index.Mobile.cshtml:82`); zero inline handlers and zero inline `<script>` in all Quest, QuestLog, Move views (grep; baseline had 6/11/5/10 handlers in Details/Manage desktop/phone); `QuestsConversionGuardTests.cs:154-157,234-244` guard it | closed |
| T-95-25 | Tampering | proposed-date rows built in the browser | low | mitigate | `wwwroot/js/quest-form.js:55-91` `buildDateRow` uses `createElement` and property assignment; names from `fieldPrefix` (`:13-16`); no markup string, no handler attribute | closed |
| T-95-26 | Elevation of privilege | Campaign and finalized field gating | medium | mitigate | Razor `@if` condition sets in `Edit.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml`, `CreateFollowUp.cshtml` identical to baseline (set diff empty both ways); the only twin-only condition (Edit.Mobile `HasExistingSignups` notice) is kept at `Edit.cshtml:82`; `QuestCampaignUiParityTests` diff changes only the delete-control assertion; controller unchanged | closed |
| T-95-27 | Tampering | date rows built in the browser | low | mitigate | no inline script in `Create*.cshtml`, `Edit.cshtml`, `CreateFollowUp.cshtml` (grep); rows built by the shared module `quest-form.js:55-91`; submit guard `:172-197`; server validation untouched | closed |
| T-95-28 | Elevation of privilege | follow-up creation for another DM's quest | low | accept | AR-05. Rationale re-checked: `QuestController.cs:994-1010` (GET) and `:1047-1070` (POST) enforce `DungeonMasterOnly` plus quest-owner or admin (`Forbid`); `CreateFollowUp.cshtml:30-32` keeps `asp-route-id` and hidden `OriginalQuestId`, same as baseline :20-22 | closed (accepted) |
| T-95-29 | Tampering | player name interpolated into a script string | medium | mitigate | baseline `onclick="removePlayerSignup(@id, '@player.Player.Name')"` (baseline `Manage.cshtml` :299,:340,:370,:523) is gone; now `data-player-name="@..."` (`Manage.cshtml:276,317,347,505`, `Manage.Mobile.cshtml:171,366,397,420`) read via `dataset.playerName` as text (`quest-manage.js:34-35`); no inline handlers remain | closed |
| T-95-30 | Tampering | availability status written with innerHTML | low | mitigate | `quest-manage.js:126-129` builds a span with `textContent` and `replaceChildren`; `QuestsConversionGuardTests.cs:254-259` `QuestManageModule_WritesNoMarkupFromStrings`; grep finds no innerHTML | closed |
| T-95-31 | Spoofing | delete and remove fetches | medium | mitigate | `quest-manage.js:16-20` (DELETE `/Quest/Delete/{id}`) and `:39-43` (DELETE `/Quest/RemovePlayerSignup/{id}`) send header `RequestVerificationToken` from the meta (`:6-9`), as baseline; server `QuestController.cs:286-289` (`DungeonMasterOnly`) and `:729-732` (`AdminOnly`) both `[ValidateAntiForgeryToken]`; Controllers diff empty | closed |
| T-95-32 | Elevation of privilege | admin Remove on the phone finalized rows | medium | mitigate | `Manage.Mobile.cshtml:14,169` `isAdmin = (bool)ViewBag.IsAdmin` gates the new Remove, as the desktop `ViewBag.IsAdmin` checks (`Manage.cshtml:274,315,345,418,502`); `RemovePlayerSignup` `[Authorize(Policy="AdminOnly")]` `QuestController.cs:731`; `MobileViewsTests.cs:497-499` (Admin sees Remove) and `:520` (DM does not) | closed |
| T-95-33 | Information disclosure | Quick Stats on the phone | low | accept | AR-06. Rationale re-checked: whole Manage body is inside the `IsAuthorized` else branch (`Manage.Mobile.cshtml:59-69`), Manage action `DungeonMasterOnly` (`QuestController.cs:959`); same figures as desktop `Manage.cshtml:797` | closed (accepted) |
| T-95-34 | Elevation of privilege | merged Details Quick Actions | medium | mitigate | `Views/QuestLog/Details.cshtml:138` `ViewBag.CanManageQuest` and `:144` `ViewBag.CanEditRecap` (baseline :167,:139); set by `QuestLogController.cs:70-71` (unchanged); `QuestLogController` `[Authorize]` (:11), EditRecap POST `[ValidateAntiForgeryToken]` (:109-110) with auth and completed-quest checks; `QuestLogControllerIntegrationTests.cs:226-330` (recap link, 200 for Player and Admin, non-owner Admin POST) unchanged | closed |
| T-95-35 | Tampering | recap markdown output | low | accept | AR-07. Rationale re-checked: `git diff --quiet 7194364c HEAD -- QuestBoard.Service/Extensions/HtmlHelperExtensions.cs QuestBoard.Domain QuestBoard.Repository QuestBoard.Service/Controllers QuestBoard.Service/Authorization QuestBoard.Service/Program.cs` exits 0 | closed (accepted) |
| T-95-36 | Elevation of privilege | Move page access | medium | mitigate | `Controllers/MoveController.cs:13` `[Authorize(Policy="SuperAdminOnly")]` (unchanged), POST actions `[ValidateAntiForgeryToken]` (:48,:110); `Views/Move/Index.cshtml:266-269` keeps the asp-tag-helper post form with hidden `id`, `targetBoardId`, `stamp` (baseline :264-267); `BoardMoveAuthorizationTests.cs:64-67,105-111` | closed |
| T-95-37 | Repudiation | weakening the closed ambient-clock list | low | mitigate | `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` commit 2fb483ab removes only `Views/Move/Index.Mobile.cshtml`, in the commit that deletes that file | closed |
| T-95-38 | Tampering | EasyMDE stylesheet source | low | mitigate | `wwwroot/css/easymde-vendor.css:5` `@import url("../lib/easymde-2.21.0/easymde.min.css") layer(vendor)`; committed `wwwroot/lib/easymde-2.21.0/*`; `UiFoundationViewLibraryGuardTests.cs:73-91` keeps the lib-path and no-CDN assertions (`JsDelivrHost`, :40); no CDN reference in touched views or CSS | closed |
| T-95-39 | Denial of service | foreign pages losing styling | medium | mitigate | foreign edits are 4-line `@section Styles` links (Characters, Contacts, Series, Shop, Events, DungeonMaster views) and one-selector `:not(:where(.EasyMDEContainer *))` narrowings (`contacts.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `character-form.mobile.css`, `dm-editprofile.mobile.css`, `shop-management-*.mobile.css`); sweeps in 95-16-SUMMARY.md:80-90. Recorded deviation: four new foreign rows used HEAD on 8000 as BEFORE, not the frozen 8001 baseline (95-16-SUMMARY.md deviation 5) | closed |
| T-95-40 | Tampering | Markdown output rendering | low | accept | AR-08. Rationale re-checked: same unchanged-backend diff as T-95-35; only `markdown-content.css` changed | closed (accepted) |
| T-95-41 | Denial of service | removing global sheets and legacy rules | medium | mitigate | no view or JS references the removed `.fantasy-quest-card`, `.custom-radio-group`, `.vote-btn`, `.quest-status-*`, `.quest-signups` (grep; `.custom-radio-label` survives only inside a never-matching selector in `site.js:121`; `quest-card-*` hits are different classes); `legacy-pages.css` holds none of them; `quests.css` no longer linked from `_Layout.cshtml`; 95-17-SUMMARY.md: selector scan plus same-data two-build sweep, 201 of 214 captures identical, 13 differ only by port in a 404 URL | closed |
| T-95-42 | Repudiation | closed ambient-clock list edits | low | mitigate | `AmbientClockSeamTests.cs` commit 6210ace3 removes exactly the two `_QuestCard.cshtml` entries, in the commit that deletes `_QuestCard.cshtml` and `_QuestSection.cshtml` | closed |
| T-95-43 | Information disclosure | Gate 2 sheet and captures | low | accept | AR-09. Rationale re-checked: fantasy fixtures only; text samples capped. Divergence: the package is also on public branch `origin/feature/phase-95-gate2-review` (95-18-SUMMARY.md:45); spot check shows only `@example.invalid` review emails, no passwords, tokens or URLs; recommend deleting the branch after review | closed (accepted) |
| T-95-44 | Information disclosure | release publishing the GHCR image on tag push | medium | transfer | Release handoff and GHCR caveat stated at `95-18-SUMMARY.md:136-138` (operator owns timing and tag; `docker-publish.yml` publishes on every `v*.*.*` tag regardless of the approval gate); no tag created (newest tag v5.7.1, 2026-10-22; none since 7194364c); only a review branch was pushed, which neither `release.yml` nor `docker-publish.yml` triggers on | closed (transferred) |
| T-95-SC | Tampering | npm `playwright` 1.64.1 (Phase 94 scratch install) | high | mitigate | `~/qb-ui-review/phase-95/tools/package.json` pins `playwright 1.64.1`; `node_modules` is a symlink to the Phase 94 install, whose `playwright` and `playwright-core` are 1.64.1 and untouched since 2026-11-01 (before Phase 95); reuse approved at 95-01-SUMMARY.md:41,68; no `package*.json`, `.csproj`, lockfile, Dockerfile or workflow changed in `git diff 7194364c..HEAD` | closed |

*Status: open · closed · open - below high threshold (non-blocking)*
*Severity: critical > high > medium > low - only open threats at or above workflow.security_block_on (high) count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-95-05 | baseline-src is a `git archive` of committed files only; captures hold fantasy fixtures; text samples capped at 40 chars and omitted for user and board listing pages. Note: committed `appsettings.json:17` holds a vestigial SQL login (pre-existing, already public in the repo) | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-02 | T-95-06 | Mockups and Gate 1 sheet are fantasy text in scratch, local-only references | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-03 | T-95-14 | Quest and character ids were already in data attributes; the server authorises character ownership on every change | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-04 | T-95-19 | Phone Current Signups shows the same list the desktop shows the same viewers | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-05 | T-95-28 | Follow-up creation is authorised server-side (DM or admin), unchanged | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-06 | T-95-33 | Phone Quick Stats shows the figures the desktop shows the same DM; Manage is DM or admin only server-side | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-07 | T-95-35 | `Html.Markdown` and its sanitising untouched (backend diff empty) | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-08 | T-95-40 | Same as AR-07; only the stylesheet changed | Phase 95 plan register, carried forward at audit | 2026-11-11 |
| AR-09 | T-95-43 | Fantasy fixtures on review boards only; the Gate 2 package is also on public branch `feature/phase-95-gate2-review` (review emails only, no secrets); operator to delete the branch after review | Phase 95 plan register, carried forward at audit | 2026-11-11 |

*Not an accepted risk (pending, not in effect): T-95-01. If the operator chooses acceptance rather than rotation, add an AR-10 row naming the operator and the date.*

---

## Transfers

| Threat Ref | Transferred To | Documentation |
|------------|----------------|---------------|
| T-95-44 | Operator (release timing, tag, deploy approval gate) | `.planning/phases/95-quests-and-quest-log-ui-conversion/95-18-SUMMARY.md:136-138` (Release handoff; GHCR publishes on any tag push regardless of the approval gate) |

---

## Unregistered Flags

- 95-02 printed credentials.json once but listed no threat flag; it maps to T-95-01.
- Gate 2 package is on a public GitHub branch while T-95-43 says "scratch folder"; content spot-checked, no secrets; see AR-09.
- Pre-existing, outside the phase diff: committed `QuestBoard.Service/appsettings.json:17` holds a vestigial SQL `User Id`/`Password`; the operator may want to confirm it is dead.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-11 | 45 | 44 | 1 (0 blocking; T-95-01, medium, non-blocking) | gsd-security-auditor (Claude Sonnet 5.5) |

Method (ASVS L1, presence checked in the cited file or tree): `git diff 7194364c..HEAD` read against each mitigation; Controllers, Authorization, Domain, Repository, Program.cs and HtmlHelperExtensions.cs confirmed unchanged; JS modules grepped for `innerHTML`/`outerHTML`/`insertAdjacentHTML`/`document.write`/`eval`; views grepped for inline scripts, inline handlers and `Html.Raw`; scratch tree stat-checked and byte-searched (passwords never printed); Claude session transcripts searched for plaintext copies (counts and paths only). Test suite not re-run (it would rebuild the running review app); recorded results at 95-18-SUMMARY.md:123.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-01 to AR-09)
- [x] `threats_open: 0` confirmed (blocking gate, block_on high)
- [ ] `status: verified` set in frontmatter (pending: close T-95-01 by rotating the review passwords, or operator signs an accepted-risk entry)

**Approval:** pending
