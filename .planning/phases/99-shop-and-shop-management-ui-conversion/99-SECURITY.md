---
phase: 99
slug: shop-and-shop-management-ui-conversion
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
created: 2026-11-17
---

# Phase 99 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register authored at plan time (23 PLAN `<threat_model>` blocks: 85 rows, 81 unique IDs; T-99-SC repeats in plans 01, 02, 03, 05, 06). Verified by gsd-security-auditor at ASVS L1 against HEAD 396b1067.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Review tooling → operator machine / mail relay / disk | Scratch Playwright harness, forced email suppression, credentials, the migrator connection string and the baseline copy under ~/qb-ui-review/phase-99 | Fixture credentials and migrator.env (mode 600), captures; no mail leaves (SMTP 127.0.0.1:1025) |
| Harness / baseline app → operator database | Fixtures, probes and click-throughs on a DB that also holds real member data; the migrator suite creates and drops scratch databases | p99 fixture rows and the p99 player's purchases only, through the app's forms |
| Scratch package → operator desktop / publication | Gate 1 and Gate 2 sheets opened locally from disk; a tag push publishes the container image regardless of the draft-release approval | Fixture data in JPEG frames, index, README, answers |
| Operator answers → later plans / release | gate1/gate2 answers.json and contrast-exceptions.json become the build contract | Decisions with provenance |
| Server enum values → rendered text | Rarity, status and type words and chips come from one server mapping, not from input | Constant words, Razor-encoded |
| DM-authored text → buyer's page / dialogs | Item names in the Sell confirmation, the Sell to Shop form and the Deny dialog; Markdown descriptions and images in the details body | Razor-encoded attributes read through `dataset`; sanitised Markdown; no `Html.Raw` |
| Reference link → external site | The D&D Beyond link opens a third-party page | URL with `rel="noopener noreferrer"` |
| Browser → Shop POST actions | Purchase, Return/Sell and Sell to Shop change the buyer's ledger and the item's stock; `quantity` is untrusted (`min="1"` is only a hint) | id, quantity + antiforgery token |
| Dungeon Master browser → ShopManagement POST actions | Create, Edit, Publish, Deny, Archive, Reopen and Delete; the review rule (no self-approval) must hold against the role's own hand-made posts | Item fields, denial reason + antiforgery token |
| Browser → GET /Shop/Details/{id} (page and `isModal` fragment) | `id` is untrusted and sequential; the viewer's role is per board, read from the active board by the DungeonMasterOnly handler | Item details or the shared 404 |
| Deep link → board switch | A top-level GET may repoint the session to another board the viewer belongs to before authorization runs | Board id only (known debt) |
| Server fragment → dialog | Same-origin, server-rendered details fragment inserted into the page | HTML parsed into an inert document; scripts dropped |
| Query string → filter card | Rarity, sort, search and page reflected into the form | Razor-encoded `value` |
| Module → page forms / /markdown/preview | Scripts that bind submit handlers; an authenticated POST that renders Markdown | Verification-token header; text written with `textContent` |
| Domain model → tracked entity | What a mapped model may overwrite in the change tracker (`BaseRepository.UpdateAsync`) | Own columns and foreign keys only |
| Board clock → availability decision | The wall-clock window is judged in the board's zone, never against a host or UTC clock | `IBoardClock.Now` |
| Global stylesheet → every desktop page | Removing a globally linked sheet can change pages outside the area | CSS scope only |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation / evidence | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-99-SC | Tampering | npm packages (×5 rows) | high | mitigate | `tools/node_modules` symlinked to the Phase 94 install, Playwright 1.64.1; no `package-lock.json` in tools/ | closed |
| T-99-01 | Info disclosure | email to real addresses | high | mitigate | run-app.mjs:9-11,32-42 forces SuppressSending + SMTP 127.0.0.1:1025 and refuses overrides; :111 checks /proc environ | closed |
| T-99-02 | Info disclosure | credentials.json, migrator.env, state/*.json | medium | mitigate | mode 600 on every file; secret scan of .planning, Service, IntegrationTests and scratch records: 0 hits; none tracked in git | closed |
| T-99-03 | Info disclosure | feed addresses in capture records | high | mitigate | /feeds/calendar/ aborted in audit-shoot.mjs:73, fixtures.mjs:435, shop-behaviour.mjs:38, roles-check.mjs:12; no feed address in any record; shop views and layouts never request the feed (see residual note) | closed |
| T-99-04 | DoS | operator database | medium | mitigate | run-app.mjs:4-8 never stops a listener it did not start; 99-01 Task 1 operator checkpoint on 1433 | closed |
| T-99-05 | Info disclosure | welcome emails from Admin > Create user | high | mitigate | same forced suppression; register99.mjs:23-25 accounts are @example.invalid | closed |
| T-99-06 | Tampering | fixture writes on real-data DB | medium | mitigate | app forms only, no SQL (99-02-SUMMARY:34); every row in fixtures.json; fixtures.mjs:21-22 | closed |
| T-99-07 | Tampering | purchase workaround | high | mitigate | fixtures.mjs:21-22,350,363 throws STOP-OPERATOR on a refusal; no SQL or ledger-write tool in scratch tools/ or logs/ | closed |
| T-99-08 | Info disclosure | credentials.json | medium | mitigate | mode 600, never printed; secret scan 0 hits | closed |
| T-99-09 | Info disclosure | baseline-src, before/ captures | low | accept | `git archive` copies committed files only; fantasy fixtures | closed (accepted) |
| T-99-10 | Tampering | behaviour probes writing to the DB | medium | mitigate | shop-behaviour.mjs:37-39 aborts every POST; sweep.mjs:134-135 answers POSTs with 204 | closed |
| T-99-11 | Tampering | baseline app migrating the DB | low | mitigate | run-app.mjs:4-11 refuses a port another tree serves; forced suppression on 8321 | closed |
| T-99-12 | Tampering | Return and Sell row forms (CSRF) | high | mitigate | _ShopPurchaseHistory.cshtml:64,77 asp-controller/asp-action; ShopController.cs:136 [ValidateAntiForgeryToken]; ShopConversionGuardTests.cs:711; HistoryRowForm_CarriesAnAntiforgeryTokenAndPostsToARedirect | closed |
| T-99-13 | Tampering / EoP | item name in an inline script string | high | mitigate | _ShopItemDetailsContent.cshtml:196 Razor-encoded `data-item-name`; shop-item-sell.js:34 reads `dataset`; 0 inline `on*=` in shop views; ShopRenderTests.cs:24,174 | closed |
| T-99-14 | Tampering | markup built from strings in the module | medium | mitigate | shop-item-sell.js:24 textContent; ShopModules_WriteNoMarkupFromStrings (ShopConversionGuardTests.cs:81-91,816); no innerHTML/outerHTML/insertAdjacentHTML/document.write in any shop module | closed |
| T-99-15 | Spoofing | theme attribute | low | accept | constant from the view; _Layout.cshtml:6 and _Layout.Mobile.cshtml:6 HtmlEncode it | closed (accepted) |
| T-99-16 | Info disclosure | mockup content (99-05) | low | mitigate | fantasy fixture text only; no mockup, gate or png file in git ls-files or phase history | closed |
| T-99-17 | Info disclosure | mockup content (99-06) | low | mitigate | as T-99-16 | closed |
| T-99-18 | Info disclosure | Gate 1 package content | medium | mitigate | gate-hygiene.py:20-26,44-47; re-run on gate1: hygiene ok (176 jpeg) | closed |
| T-99-19 | Repudiation | Gate 1 answers | low | mitigate | gate1/answers.json items carry `source: operator` and sheet versions | closed |
| T-99-20 | Tampering | unapproved copy change / defaulted no-default item | medium | mitigate | items 1, 2, 3 and 16 answered by the operator; 99-07-SUMMARY:92 | closed |
| T-99-21 | Tampering | chip partial rendering item data | low | mitigate | _ShopRarityChip.cshtml:2-3, _ShopTypeChip.cshtml:2-3 take an enum only | closed |
| T-99-22 | Repudiation | status shown differently on the two layouts | low | mitigate | ShopItemDisplay.cs:35-42; _ShopStatusBadge at all six sites; ShopViews_UseTheSharedStatusMapping | closed |
| T-99-23 | Tampering | Markdown description | medium | mitigate | _ShopItemDetailsContent.cshtml:122 Html.Markdown; HtmlHelperExtensions.cs, MarkdownService.cs, MarkdownController.cs unchanged since 59df0cf8 | closed |
| T-99-24 | Spoofing | reverse tabnabbing via reference link | medium | mitigate | _ShopItemDetailsContent.cshtml:129 rel="noopener noreferrer"; ShopRenderTests.cs:1114 | closed |
| T-99-25 | Info disclosure | Markdown image tracking pixel | low | accept | allowed tag set unchanged; deferred-items.md, Gate 2 `df:defer` | closed (accepted) |
| T-99-26 | Info disclosure | member opening a Draft item's Details by id | low | accept | superseded: fixed by 99-22 (T-99-64, commit fd6e799e) | closed (superseded) |
| T-99-27 | DoS | item script intercepting the Logout form | medium | mitigate | shop-item-form.js:112 binds only `form[data-shop-item-form]`; keyboard-guard fact | closed |
| T-99-28 | Tampering | client-side validation as the only check | low | accept | server validation attributes unchanged and authoritative | closed (accepted) |
| T-99-29 | Tampering | CSRF on Edit | medium | mitigate | Edit.cshtml:18, Edit.Mobile.cshtml:18 asp-action; ShopManagementController.cs:120; token scanner | closed |
| T-99-30 | Tampering | hint text built from strings | low | mitigate | shop-item-form.js:23-34,60 createElement + textContent | closed |
| T-99-31 | Tampering | typed values or server HTML in the preview | high | mitigate | shop-item-preview.js:54-60 inert DOMParser, text only; :89-109,133,144 textContent | closed |
| T-99-32 | Tampering | CSRF on /markdown/preview | low | mitigate | shop-item-preview.js:74 RequestVerificationToken header; MarkdownController.cs:12,19 [Authorize] + [ValidateAntiForgeryToken], unchanged; token meta from _QuestFormScripts.cshtml:15 via Create.cshtml:173 | closed |
| T-99-33 | Tampering | accidental save from the preview dialog | medium | mitigate | Create.cshtml:145-158 template holds no form or link; Purchase is an aria-hidden span | closed |
| T-99-34 | Tampering | CSRF on the management and Deny forms | high | mitigate | every POST form tokenised (Index 76/156/165/180/255/264/273, Index.Mobile 67/76/93/102, _ShopManagementDialogs.cshtml:36-37); every action [ValidateAntiForgeryToken]; ShopPostForms_EachCarryAnAntiforgeryToken | closed |
| T-99-35 | Tampering / EoP | item name in the Deny dialog | high | mitigate | Index.cshtml:84, Index.Mobile.cshtml:87 Razor-encoded `data-deny-name`; shop-management.js:25 textContent | closed |
| T-99-36 | Tampering | Deny form action taken from the page | medium | mitigate | server-rendered `data-deny-url`; ShopManagementController.cs:13 policy, :249 ownership, :255 status | closed |
| T-99-37 | EoP | who sees Publish, Deny and Delete | medium | mitigate | controller checks ShopManagementController.cs:191,249,300-308; ShopRenderTests.cs:519,571,699 | closed |
| T-99-38 | Tampering | inserting the fetched fragment | medium | mitigate | Index.cshtml:195,202 server-built `data-item-url`; shop-item-modal.js:50-54 inert parse, scripts dropped, nodes moved | closed |
| T-99-39 | Tampering | reflected search text (desktop) | medium | mitigate | Index.cshtml:155 Razor-encoded value; no Html.Raw in shop views | closed |
| T-99-40 | DoS | stale or failed fetches | low | mitigate | shop-item-modal.js:17,24-25,46,57-60 request counter; failure clears aria-busy | closed |
| T-99-41 | Tampering | CSRF on Return and Sell | high | mitigate | as T-99-12 | closed |
| T-99-42 | Tampering / EoP | item name in the Sell confirmation | high | mitigate | _ShopPurchaseHistory.cshtml:76-77 encoded `data-confirm-message`; confirm-submit.js:44-46 plain text; ShopRenderTests.cs:2032 | closed |
| T-99-43 | Tampering | toast built from strings | medium | mitigate | shop-merchant.js:115,123 template clone + textContent; templates at Index.cshtml:323-354 | closed |
| T-99-44 | EoP | selling another member's purchase | low | accept | ownership check present at ShopService.cs:137, unchanged | closed (accepted) |
| T-99-45 | Tampering | CSRF on the phone Return and Sell forms | high | mitigate | Index.Mobile.cshtml:178 renders the shared partial; ShopIndex_Phone_…ReturnFormWithATokenAndAThirtyHourOneASellThatAsks | closed |
| T-99-46 | Tampering | reflected search text (phone) | medium | mitigate | Index.Mobile.cshtml:250 encoded value; ShopIndex_Phone_ReflectsTheSearchTextAsAnEncodedValueOnly | closed |
| T-99-47 | DoS | non-shop page losing styling | medium | mitigate | after/99-18/outside-shop.json: 7 pages, 0 differences each | closed |
| T-99-48 | Repudiation | a guard silently deleted | medium | mitigate | re-checked at HEAD: 2725 baseline methods, 1 missing, recorded in tests-renamed.md; no test file deleted | closed |
| T-99-49 | Tampering | unintended change outside the area | medium | mitigate | git diff 59df0cf8..0c1cea5b over controllers, domain, repository, helpers, AmbientClockSeamTests, appsettings and tokens is empty | closed |
| T-99-50 | Info disclosure | migrator connection string | high | mitigate | migrator.env mode 600; secret scan 0 hits | closed |
| T-99-51 | Tampering | click-through writes on real data | low | mitigate | p99 player's own purchases on P99 Audit Shop through the forms (99-19-SUMMARY:41,138) | closed |
| T-99-52 | Tampering | backdating to reach the Sell state | high | mitigate | eligible by the calendar since 2026-11-17T11:17:54Z (99-19-SUMMARY:41); no SQL, faketime or clock tool in scratch | closed |
| T-99-53 | Info disclosure | Gate 2 package content | medium | mitigate | gate-hygiene.py re-run on gate2: hygiene ok (375 jpeg) | closed |
| T-99-54 | Tampering | unreviewed release or image publication | high | mitigate | no tag at HEAD or containing 59df0cf8; no phase-99 PR; docker-publish.yml and release.yml trigger on v* tags only, dotnet.yml on main only; milestone-branch push follows the standing rule and fires no workflow | closed |
| T-99-55 | Repudiation | Gate 2 answers | low | mitigate | gate2/answers.json carries a source on every answer, versions [gate2-v1] | closed |
| T-99-56 | Repudiation | legendary colour drifting from the answer | low | mitigate | shop-pages.css:40-42; ShopConversionGuardTests.cs:326 pins amber; ui-guidelines gold list unchanged | closed |
| T-99-57 | Tampering | partials rendering item data | low | mitigate | _ShopStatusBadge.cshtml:2 enum, _ShopManagementStockBadge.cshtml:1 int | closed |
| T-99-58 | DoS | item script intercepting Logout on Create | medium | mitigate | shop-item-form.js:112; Create.cshtml:18, Create.Mobile.cshtml:18 | closed |
| T-99-59 | Tampering | CSRF on Create and the phone forms | medium | mitigate | asp-action on Create, Create.Mobile, Edit, Edit.Mobile; ShopManagementController.cs:56 | closed |
| T-99-60 | Tampering | crafted zero or negative quantity | high | mitigate | ShopService.cs:77-83 called first at :88,134,194; ShopServiceTests.cs:81-109; Post_QuantityBelowOne_IsRefusedWithTheErrorAndChangesNothing | closed |
| T-99-61 | Tampering | stock-convention regression | medium | mitigate | ShopService.cs:96-113,167-172,212-217; ShopServiceTests.cs:120,149,190 | closed |
| T-99-62 | DoS | zero- or negative-quantity rows already recorded | low | accept | no data change; ShopService.cs:146 refuses before the division at :164 | closed (accepted) |
| T-99-63 | Info disclosure | refusal message shown to the member | low | accept | names the rule only, shown to the requester only | closed (accepted) |
| T-99-64 | Info disclosure | member reads a Draft or Denied item by id | high | mitigate | ShopController.cs:82-85,99-103, ahead of the isModal branch at :90; ShopControllerIntegrationTests.cs:385 | closed |
| T-99-65 | Info disclosure | existence oracle | medium | mitigate | the same NotFound() at ShopController.cs:84; Details_RefusedItem_AnswersExactlyLikeAMissingId (:489) | closed |
| T-99-66 | EoP (regression) | reviewers refused or role-name bypass | medium | mitigate | ShopController.cs:102-103 policy only; no IsInRole or role literal in the file; facts at :404,:450,:468 | closed |
| T-99-67 | Info disclosure | deep link to a Draft on another board switches the board | low | accept | deferred-items.md deep-link row, known debt | closed (accepted) |
| T-99-68 | Info disclosure | window not enforced by id; Archive accepting any status | low | accept | superseded: fixed by 99-23 (T-99-72/73/74; c53e7e59, b6c80468, fd84daad); residual is T-99-77 | closed (superseded) |
| T-99-69 | DoS | hand-made Purchase/Sell to Shop on a Draft lands on a 404 | low | accept | reachable only by a hand-made post; service refusal unchanged | closed (accepted) |
| T-99-70 | DoS | limited-stock Return and Sell refused | high | mitigate | EntityProfile.cs:82-84,95-97 ignore navigations; ShopFormPostEffectTests.cs:138 no longer skipped | closed |
| T-99-71 | Tampering | mapped model overwrites tracked rows | medium | mitigate | same map change; ShopServiceStockWriteTests.cs:180-226 | closed |
| T-99-72 | EoP | self-publish through Archive then Reopen | medium | mitigate | ShopManagementController.cs:218-224, the only ArchiveItemAsync caller; facts :290,:309 | closed |
| T-99-73 | Tampering | buying or selling outside the window | medium | mitigate | ShopService.cs:91,197 IsOnSale(item, boardClock.Now); ShopAvailabilityWindowTests.cs:265 | closed |
| T-99-74 | Info disclosure | scheduled item read by id | medium | mitigate | ShopController.cs:99-100 `!HasOpened`; parity fact ShopAvailabilityWindowTests.cs:165-186 | closed |
| T-99-75 | EoP (regression) | reviewers lose the scheduled item | medium | mitigate | single policy call ShopController.cs:103; ShopAvailabilityWindowTests.cs:218 | closed |
| T-99-76 | Tampering | window judged against UTC or the host clock | low | mitigate | ShopItemAvailability.cs takes boardNow; ShopRepository.cs:122-126; AmbientClockSeamTests.cs:19,28,35,43,342 | closed |
| T-99-77 | EoP | Archived-from-Draft rows made before the fix reopen | low | accept | deferred-items.md Archive row, "Residual, accepted" | closed (accepted) |
| T-99-78 | Info disclosure | deep link to a scheduled item switches the board | low | accept | deferred-items.md deep-link row, known debt | closed (accepted) |
| T-99-79 | Repudiation | Return slip posted after 24h settled as a sell | low | accept | deferred-items.md 24-hour row; 99-REVIEW-FIX.md:55-57 | closed (accepted) |
| T-99-80 | Info disclosure | EF tracking error shown as the toast | low | accept | only known trigger fixed (T-99-70); deferred-items.md EF-error row | closed (accepted) |
| T-99-81 | EoP | Edit publishes a Draft or Denied item edited to Common or Uncommon, which a second edit raises to Rare or above while on sale | medium | mitigate | ShopItemReview.cs:39-44 `StatusAfterEdit` never answers Published for a Draft or Denied item; ShopManagementController.cs:152-163 takes the status from it; ShopItemReviewTests.cs:56 `StatusAfterEdit_NeverPublishesAndSendsContentThatNeedsReviewBackToReview`; ShopManagementControllerIntegrationTests.cs:432 `Edit_Post_ByTheItemsCreator_LeavesTheStatusTheReviewRuleGives`, :486 `Edit_Post_OnAnotherKeepersItem_IsSavedButNeverPublishedByTheEdit`, :515 `EditThenPublish_ByTheItemsCreator_NeverPutsAnUnreviewedItemOnSale`; bite check 1 failed 4 unit and 10 integration cases plus the 3 draft chains; opened at planning by 99-24; closed by 14ce7ed9 | closed |
| T-99-82 | EoP | an edit leaves a Published item on sale, or an Archived item one Reopen from sale, with changed content at Rare or above nobody reviewed | medium | mitigate | ShopItemReview.cs:41-42 sends Published and Archived items edited at a rarity that needs review to Draft; ShopManagementController.cs:163; the same three integration theories (stored-Common upgrade rows included) and the unit rows; bite check 2 failed 6 unit and 17 integration cases; opened at planning by 99-24; closed by 14ce7ed9 | closed |
| T-99-83 | Tampering | a Dungeon Master who is not an Admin deletes another keeper's Draft or Denied item | medium | mitigate | ShopManagementController.cs:300-316 loads the current user and refuses another keeper's item with "You can only delete your own items." before the status check; ShopManagementControllerIntegrationTests.cs:576 `Delete_Post_FollowsWhoCreatedTheItemAndWhetherItHasBeenOnSale` (24 cases); bite check 1 (creator check removed) failed the Dungeon Master's 4 other-keeper rows; opened at planning by 99-24; closed by 014994d2 | closed |
| T-99-84 | EoP (regression) | Admins or SuperAdmins lose Delete, or a role-name shortcut replaces the policy | medium | mitigate | ShopManagementController.cs:297 the single `AuthorizeAsync(User, "AdminOnly")` call, `IsInRole` count 0 in the controller; Admin and SuperAdmin rows of `Delete_Post_FollowsWhoCreatedTheItemAndWhetherItHasBeenOnSale`; bite check 2 (policy forced to fail) failed the 12 Admin and SuperAdmin rows on other keepers' items and their own Published and Archived items; opened at planning by 99-24; closed by 014994d2 | closed |
| T-99-85 | Repudiation | the Edit toast says "Item updated successfully!" when the item went off sale or stayed unpublished | low | mitigate | ShopManagementController.cs:165-171 chooses the toast from the before and after statuses; every row of `Edit_Post_ByTheItemsCreator_LeavesTheStatusTheReviewRuleGives` asserts its toast and, for a Draft result, the absence of the success sentence; opened at planning by 99-24; closed by 14ce7ed9 | closed |
| T-99-86 | DoS (regression) | a list offers a Delete the server refuses, or loses an Edit or Delete control the server accepts | low | mitigate | Index.cshtml:271 Delete for an Admin only in the other-items table; Index.Mobile.cshtml:101 Delete for an Admin on non-draft rows, otherwise only an own Denied row; ShopRenderTests.cs:736 `ShopManagementList_BothLayouts_OfferDeleteOnlyWhereTheServerAcceptsIt`, :764 `ShopManagementList_BothLayouts_EveryEditLinkOpensForTheViewer` (6 cases each, passed before and after the view change); bite check (Delete on every row) failed all 6 Delete cases; opened at planning by 99-24; closed by 050167a1 | closed |
| T-99-87 | EoP | a Dungeon Master edits another keeper's item at Rare or above and then publishes it, approving their own change | medium | accept | any Dungeon Master may still edit any item and Publish refuses only the creator; closing it needs a record of who sent the item to review (new column and migration) or taking Edit off other keepers' items; AR-99-16; deferred-items.md row; unticked Sign-Off line; opened at planning by 99-24; closed by 14ce7ed9 (own-item route), remainder accepted | closed (accepted) |
| T-99-88 | EoP | deleting one's own Draft and creating it again at Common or Uncommon puts it on sale at once | low | accept | ShopItemReview.cs:27-28 `StatusOnCreate` unchanged in effect; only review-free content reaches the shop this way; AR-99-17; deferred-items.md Create/Edit row; opened at planning by 99-24; closed by 14ce7ed9 (Create on the shared rule) | closed (accepted) |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-99-01 | T-99-09 | Baseline copy and BEFORE captures contain only committed files and fantasy fixtures; the connection string stays in user-secrets | Plan 99-03 (accept disposition) | 2026-11-16 |
| AR-99-02 | T-99-15 | Theme attribute is a constant set by the view, never input, and HTML-encoded by both layouts | Plan 99-04 (accept disposition) | 2026-11-16 |
| AR-99-03 | T-99-25 | Markdown images in item descriptions can act as a tracking pixel; an allow-list or proxy is a site-wide content-policy decision | Plan 99-10; operator at Gate 2 (`df:defer`) | 2026-11-17 |
| AR-99-04 | T-99-26 | Draft Details by id; superseded by the 99-22 fix | Plan 99-10; resolved by 99-22 | 2026-11-17 |
| AR-99-05 | T-99-28 | Server validation attributes stay authoritative; no client check, per the Gate 1 answer | Plan 99-11 (accept disposition) | 2026-11-16 |
| AR-99-06 | T-99-44 | The service already checks transaction ownership; the phase does not change it | Plan 99-16 (accept disposition) | 2026-11-16 |
| AR-99-07 | T-99-62 | Old zero- or negative-quantity rows are not migrated; the remaining-quantity refusal runs before the division | Plan 99-21 (accept disposition) | 2026-11-16 |
| AR-99-08 | T-99-63 | The refusal message names only the rule and goes only to the requester | Plan 99-21 (accept disposition) | 2026-11-16 |
| AR-99-09 | T-99-67, T-99-78 | A deep link to a Draft or scheduled item on another of the member's boards switches the board before the 404; the resolver never consults roles or visibility by design | Plans 99-22, 99-23; operator decision "Fix + 2 gaps" | 2026-11-17 |
| AR-99-10 | T-99-68 | Window and Archive bypass by id; superseded by the 99-23 fixes | Plan 99-22; resolved by 99-23 | 2026-11-17 |
| AR-99-11 | T-99-69 | A hand-made Purchase or Sell to Shop on a Draft redirects to a 404; members never see Drafts in the list | Plan 99-22 (accept disposition) | 2026-11-17 |
| AR-99-12 | T-99-77 | An Archived row made from a Draft or Denied item before the fix still reopens; indistinguishable without a publication record | Plan 99-23 (accept disposition) | 2026-11-17 |
| AR-99-13 | T-99-79 | A Return slip posted just after the 24-hour window settles as a half-price sell reported as "sold" | Plan 99-23; operator decision "Fix + 2 gaps" | 2026-11-17 |
| AR-99-14 | T-99-80 | EF change-tracking errors share the refusal exception type and can reach the toast; the only known trigger is fixed | Plan 99-23; operator (known debt) | 2026-11-17 |
| AR-99-15 | WINDOWS 33 | Shop forms do not opt in to the submit lock, so a double click can post twice; baseline behaviour kept for this release | Operator at Gate 2 (`d1:accept`, decision-99-no-submit-lock); `.planning/WINDOWS.md:50` status `waived` | 2026-11-17 |
| AR-99-16 | T-99-87 | A Dungeon Master can publish their own change to another keeper's item; closing it needs a record of who sent the item to review (a new column and migration) or taking Edit off other keepers' items | Plan 99-24 (accept disposition); flagged to the operator | 2026-11-17 |
| AR-99-17 | T-99-88 | Deleting one's own Draft and creating it again at Common or Uncommon puts review-free content on sale, as Create always allowed | Plan 99-24 (accept disposition) | 2026-11-17 |

Deferred by operator decision, not security acceptances: dialog dates keep the server wording until reload and the stale `QuestsConversionGuardTests` message (Gate 2 `df:defer`), and the date-only availability line (known debt). All three are recorded in `deferred-items.md`. Gate 2 accessibility exceptions (e1, e3) and acknowledgement (e2) are recorded in `~/qb-ui-review/phase-99/contrast-exceptions.json`.

---

## Residual notes (non-blocking)

- **T-99-03:** seven phase-authored ad hoc probes (focus-details-99, history-focus-99, preview-focus-99, overflow-sweep-99, shop-item-form-probe, register99, mockup-shoot-99) create contexts without the feed abort. No record contains a feed address, and the shop views and layouts never request `/feeds/calendar/`. A shared context factory would remove the per-script dependence before these probes are reused on non-shop pages.
- **Resolved by 99-24 (operator decision "Fix first"):** **Recommended follow-up threat entry — review bypass through Edit (pre-existing, medium impact):** `ShopManagementController.Edit` (`ShopManagementController.cs:101-178`) has no ownership check. A Draft edited to Common or Uncommon is auto-published, and a second edit back to Rare or Legendary keeps it Published; editing a Published item never sends it back to review. So any Dungeon Master can put a high-rarity item on sale, their own or another's, without a second Dungeon Master's review, the same impact T-99-72 closed for Archive then Reopen. A non-admin Dungeon Master can also delete another Dungeon Master's Draft or Denied item (`:300-308`). This predates the phase: the controller's diff since 59df0cf8 is only the Archive check and the Edit status reload. Register it in a follow-up phase. Resolution: an edit never publishes and an edit at Rare or above sends a Published or Archived item back to Draft (Create and Edit share `ShopItemReview`), and a Dungeon Master who is not an Admin deletes only their own Draft or Denied item while Admins and SuperAdmins delete any item through the AdminOnly policy; fix commits 14ce7ed9, 014994d2 and 050167a1, registered as T-99-81 to T-99-86, with the residual T-99-87 (a Dungeon Master publishing their own edit of another keeper's item) accepted pending the operator's choice.
- **Sell to Shop needs no purchase and has no upper bound on quantity** (`ShopService.cs:192-217`): the payout `(Price/2)*quantity` is unbounded by design, and gold is not balanced in the app, so it only produces a large recorded number. The int-overflow part was fixed by WR-01 (69207a4a) at `:202-207`.
- **Details dialog:** `shop-item-modal.js:50-54` drops `<script>` elements, but moved nodes keep any `on*` attributes. Safety rests on the server-side Markdown sanitiser and the no-inline-handler view guard, both in place.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-17 | 81 | 81 | 0 | gsd-security-auditor (ASVS L1), recorded by the auditor at the orchestrator's request |

## Security Audit 2026-11-17

| Metric | Count |
|--------|-------|
| Threats found | 81 (66 mitigate, 15 accept, 0 transfer; 19 high, 33 medium, 29 low) |
| Closed | 81 |
| Open | 0 |
| Open at or above high | 0 |

Tests re-run by the auditor: unit (filter `Shop|AmbientClockSeamTests`) 150 passed, 0 failed; integration (filter `Shop|KeyboardScriptGuardTests|MobileViewsTests`) 452 passed, 0 failed, 2 skipped (`ShopItemSqlServerTests`, gated on the SQL Server environment flag).

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter
- [x] Edit review bypass and cross-keeper Delete registered as T-99-81 to T-99-88 and closed by 99-24 (operator decision "Fix first")
- [ ] Operator's choice on T-99-87 (a Dungeon Master publishing their own edit of another keeper's item): accept, or a follow-up that records who sent an item to review

**Approval:** verified 2026-11-17
