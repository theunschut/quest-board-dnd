---
phase: 91
slug: linked-boards-with-a-shared-calendar
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on (high) severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-17
---

# Phase 91 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register source: the `<threat_model>` blocks of `91-01-PLAN.md` to `91-10-PLAN.md` (authored at plan time).
> Audited at commit `06f49b71` (milestone/v9-rolling-improvements), after the review fixes in `a191b32c`.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser → `LinkedBoardsController` (desktop and mobile) | Posted set ids, board ids, names and board type are untrusted, even from a super admin's own form; the mobile view adds no endpoint | Set ids, board ids, set names, board type |
| `LinkedBoardsController` → `LinkedBoardSetService` | The service is the only place link rules are enforced; the picker is a convenience | Set / board ids, names |
| stored names → Razor and `confirm()` handlers | Board and set names are stored text rendered into HTML and inside JavaScript confirm prompts | Board names, set names |
| `LinkedBoardSetRepository` → `Groups` / `LinkedBoardSets` | Unfiltered tables; the repository must not reach tenant entities or bypass the ambient filter | Board id, name, type, set id, viewer role |
| Group edit path → `GroupEntity` | A board rename maps a domain Group onto the tracked row and must not touch the link columns | Board name |
| viewer → `CalendarController` / `QuestController.Details` | The viewer's role on the active board decides which quests are disclosed | Quest titles, DM-only flag |
| active board A (session) → linked reads of B | The only place another board's rows are read on a calendar; the tenant filter is bypassed (`IgnoreQueryFilters`) | Another board's quest and event titles, times, DM name |
| viewer → linked-board lookup and chips | Which boards count as linked, and the viewer's membership on each, decide what the overlay shows and whether a link renders | Board ids, membership role |
| A's calendar and Details pages → B's data | Rendered linked content must stay within the narrow projection | Title, time, board name, DM name |
| linked chip / row link → `CrossBoardDeepLinkMiddleware` | A rendered link must only exist where the board switch will succeed | Detail-page URL with integer id |
| linked markup → Details vote form | Linked lines sit inside the form that posts `DateVotes` | Form controls and field names |
| InMemory test harness → production SQL Server | Query translation and FK behaviour are only provable on the real provider | Schema, query shape |

---

## Threat Register

42 threats: 37 mitigate, 5 accept, 0 transfer. Verification depth is ASVS L1 (code, test and git-diff evidence); no open threat of any severity.

| Threat ID | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|----------|-----------|----------|-------------|------------------------|--------|
| T-91-01 | Elevation of privilege | LinkedBoardsController (all actions) | high | mitigate | Class-level `[Authorize(Policy = "SuperAdminOnly")]` at `LinkedBoardsController.cs:11`; the policy is `RequireRole("SuperAdmin")` at `Program.cs:90-91`. Deny matrix `Page_WhenNotSuperAdmin_DeniesGetAndPostAndLeavesTheBoardUnchanged` (Admin, DungeonMaster, Player; GET plus Create/AddBoard/Delete POSTs; asserts nothing changed) | closed |
| T-91-02 | Tampering | LinkedBoardSetService.AddBoardAsync | high | mitigate | Service refuses a board whose type differs from the set's stored type (`LinkedBoardSetService.cs:55`) and a board already in a set (`:61`); repository re-checks at write (`LinkedBoardSetRepository.cs:89`) and `Groups.LinkedBoardSetId` is a concurrency token (`QuestBoardContext.cs:321`). Hand-posted-id facts `AddBoard_WhenCampaignBoardPostedIntoOneShotSet_*`, `AddBoard_WhenOneShotBoardPostedIntoEmptyCampaignSet_*`, `AddBoard_WhenBoardIsAlreadyInAnotherSet_*` | closed |
| T-91-03 | Tampering (IDOR) | RemoveBoard / Rename / Delete posts | medium | mitigate | Set and board existence re-checked (`LinkedBoardSetService.cs:50`, `:86`, `:110`, `:132`); `RemoveBoard` requires `board.LinkedBoardSetId == setId` else `NotInSet` (`:115`, repo `LinkedBoardSetRepository.cs:121`); SuperAdmin-only. Facts `RemoveBoard_WhenBoardIsInADifferentSet_IsRefusedAndWritesNothing`, `AddBoard_WhenSetOrBoardIsGone_*` | closed |
| T-91-04 | Tampering (CSRF) | All POST actions | medium | mitigate | `[ValidateAntiForgeryToken]` on all five POSTs (`LinkedBoardsController.cs:28,45,54,63,72`); reflection sweep `AntiForgeryTokenCoverageTests.AllHttpPostActions_CarryValidateAntiForgeryToken` passes | closed |
| T-91-05 | Tampering (stored XSS) | Index.cshtml names in HTML and confirm() | medium | mitigate | Names output with `@set.Name` / `@board.Name` (Razor-encoded); confirm text built with `Json.Serialize` (`Index.cshtml:64-65,123-124`) then HTML-encoded as the `onsubmit` attribute value (`:128,164`); no `Html.Raw` in any linked view (grep clean). Fact `ConfirmPrompts_EscapeQuotesAndApostrophesInNamesForJavaScript` (O'Brien); operator also used a name with quotes and an apostrophe on SQL Server | closed |
| T-91-06 | Denial of service / integrity | Set delete and board delete | low | mitigate | FK `ON DELETE SET NULL` (`QuestBoardContext.cs:308-314`; migration `20261016061911_AddLinkedBoardSets.cs:47-52`); operator confirmed `FK_Groups_LinkedBoardSets_LinkedBoardSetId` has delete action `SET_NULL` on SQL Server (91-10-SUMMARY). Facts `DeleteSet_UnlinksEveryBoardAndRemovesTheSetWithoutDeletingBoards`, `DeletingALinkedBoard_LeavesTheSetAndItsOtherBoardsIntact` | closed |
| T-91-07 | Repudiation | Concurrent super-admin edits | low | accept | Accepted (see Accepted Risks Log). Residual risk is now smaller than accepted: the review fix added a concurrency token on `LinkedBoardSetId` and conflict handling (`LinkedBoardSetRepository.cs:96-104,128-135,195-200`) | closed |
| T-91-08 | Information disclosure | QuestRepository.GetQuestsForCalendarAsync | medium | mitigate | Query keeps only quests that are not DM-only unless the viewer is admin or DM (`QuestRepository.cs:53`); tier comes from the fresh role lookup (`CalendarController.cs:52,97-109`, `QuestController.cs:348-354`). Facts `CalendarDungeonMasterSessionTests` (player hidden, DM shown, desktop and mobile) | closed |
| T-91-09 | Information disclosure | QuestController.Details own-quest exemption | low | accept | Accepted (see Accepted Risks Log). Verified only the page's own quest is re-added (`QuestController.cs:357-362`) | closed |
| T-91-10 | Spoofing | Role source | low | mitigate | Role from `IUserService.GetEffectiveGroupRoleAsync` → `UserGroups` per request (`UserService.cs:61-68`; callers `CalendarController.cs:105-108`, `QuestController.cs:1098-1101`); active board id from server session (`ActiveGroupContextService.cs:20-23`); no role read from session or posted data | closed |
| T-91-11 | Information disclosure | QuestRepository / EventRepository linked reads | high | mitigate | Predicate pinned to `linkedGroupIds` (`QuestRepository.cs:293`, `EventRepository.cs:204`); ids come only from `GetLinkedBoardsForViewerAsync` in the same request (`LinkedCalendarService.cs:34-41`); XML contract states never from request input (`IQuestRepository.cs:121-122`, `IEventRepository.cs:96-97`); empty set short-circuits (`QuestRepository.cs:281`, `EventRepository.cs:195`). Exactly two new `IgnoreQueryFilters` calls, both in already-allowlisted files (`CrossBoardIgnoreQueryFiltersSeamTests` passes). Facts `GetLinkedQuestsForCalendar_PinnedToBoardB_*`, `GetLinkedEventsForCalendar_PinnedToBoardB_*`, `Calendar_BoardLinkedToAnother_ShowsTheLinkedBoardsEntryAndNeverTheOutsideBoards` | closed |
| T-91-12 | Information disclosure | Linked quest projection | high | mitigate | Flat `Select` with no `Include` (`QuestRepository.cs:297-311`) into `LinkedQuestEntry` (title, DM name, flags, dates only). `LinkedBoardStaticGuardTests` reflection guards on all linked model types; fact `Calendar_LinkedQuestWithSignupsAndVotes_NeverShowsAnotherBoardsRosterOrPollAnswers` | closed |
| T-91-13 | Information disclosure | DM-only linked sessions | medium | mitigate | `LinkedCalendarService.cs:104-108`: DM-only entry kept only for a SuperAdmin or a DungeonMaster/Admin of the owning board, role read fresh from `UserGroups` (`LinkedBoardSetRepository.cs:270-278`). Matrix facts `Calendar_LinkedDmOnlySession_*`, `Agenda_LinkedDmOnlySession_*` | closed |
| T-91-14 | Information disclosure | Links to non-member boards | low | accept | Accepted (see Accepted Risks Log). Verified no link or handler is rendered for non-members, so no 404 probe surface (`_LinkedDayEntries.cshtml:22,46`, `Index.Mobile.cshtml:143,166`) | closed |
| T-91-15 | Tampering | Vote form on Quest Details | medium | mitigate | Linked content only through `_LinkedDayEntries` / `_LinkedAlsoThatDay` with their own model and no form controls; `LinkedBoardStaticGuardTests.LinkedPartial_ContainsNoFormControlAnswerMarkerIdentityAttributeOrRawOutput` bans `<input/<form/<button/<label/<select`, `DateVotes`, `id=`, `for=`, `Html.Raw`; placement facts for desktop and mobile | closed |
| T-91-16 | Tampering | Feed revision integrity | medium | mitigate | Both linked reads are `AsNoTracking` projections with no write path (`QuestRepository.cs:296`, `EventRepository.cs:208`); `FeedRevisionWriteSeamTests` unchanged and passing; feed files unchanged (see T-91-38) | closed |
| T-91-17 | Spoofing / Elevation | Stale linked set or membership | low | mitigate | Set and membership read per request, never cached (`LinkedCalendarService.cs:31-34`). Facts `Calendar_AfterABoardIsRemovedFromTheSet_NextRequestShowsNothingFromIt`, `LinkingThroughThePlatformShowsTheOtherBoardOnAllFourSurfacesAndUnlinkingRemovesItAtOnce` | closed |
| T-91-18 | Information disclosure | Set name to players | low | mitigate | `LinkedBoard`, `LinkedCalendarData`, `ViewerLinkedBoards` and the repository projection carry board names only; no `SetName` in any calendar model or view (grep clean). Fact `Legend_NeverShowsTheNameOfTheSet` | closed |
| T-91-19 | Denial of service | Linked read size | low | mitigate | Reads bounded to the shown month (`CalendarController.cs:75-82`; window predicates `QuestRepository.cs:293-294`, `EventRepository.cs:204`) plus empty-set short-circuit. Edge facts `GetLinkedEventsForCalendar_EventAtUpperBoundExcludedAndAtLowerBoundIncluded`, `GetLinkedQuestsForCalendar_ProposedDatesAtWindowEdges_*` | closed |
| T-91-20 | Tampering (stored XSS) | Linked titles, board and DM names in chips/tooltips | medium | mitigate | All values Razor-encoded in `_LinkedDayEntries.cshtml` (including `title="@tooltip"`), calendar legend and note; no `Html.Raw` in any linked or calendar view (grep clean; static guard bans it in both partials) | closed |
| T-91-21 | Tampering (stored XSS) | Index.Mobile.cshtml confirm() and names | medium | mitigate | `Json.Serialize` for confirm text (`Index.Mobile.cshtml:59,106`), Razor encoding elsewhere, no `Html.Raw`. Fact `MobileConfirmPrompts_EscapeQuotesAndApostrophesInNamesForJavaScript` | closed |
| T-91-22 | Elevation of privilege | Mobile page | high | mitigate | Mobile view is served by the same `SuperAdminOnly` controller; no new action (`LinkedBoardsController.cs`); deny matrix from T-91-01 covers every action | closed |
| T-91-23 | Tampering (CSRF) | Mobile POST forms | medium | mitigate | Mobile forms use the form tag helper with `method="post"` and no `asp-antiforgery="false"` (`Index.Mobile.cshtml:23,82,109,120,138`), so tokens are emitted; actions keep `[ValidateAntiForgeryToken]` (T-91-04) | closed |
| T-91-24 | Information disclosure | Linked chips on A's page | high | mitigate | Narrow projection only (T-91-12). Facts `Calendar_LinkedQuestWithSignupsAndVotes_NeverShowsAnotherBoardsRosterOrPollAnswers`, `Calendar_LinkedQuestChip_CarriesTheDmNameAndNoQuestDataAttributes` | closed |
| T-91-25 | Information disclosure | Links to B for non-members | low | mitigate | Link rendered only when `ViewerIsMember` (a `UserGroups` row; no SuperAdmin branch, `LinkedBoard.cs`); facts `Calendar_NonMemberOfTheLinkedBoard_SeesTheEntryAsPlainTextWithNoLink`, `NonMemberOfTheLinkedBoard_SeesTheEntryWithNoLinkAndTheSameUrlAnswersNotFound`, `SuperAdminWithoutAMembershipOnTheLinkedBoard_IsGivenNoLinkToItsQuest` | closed |
| T-91-26 | Tampering (CSS-only UI) | calendar.css | low | accept | Accepted (see Accepted Risks Log). Re-verified additions only: `git diff --numstat c4abdf84..HEAD` on `wwwroot/css` shows 142, 71, 33 and 33 added lines and 0 deleted | closed |
| T-91-27 | Information disclosure | Legend | low | mitigate | Legend lists board names only (`Index.cshtml:96-111` in `Views/Calendar`); fact `Legend_NeverShowsTheNameOfTheSet` | closed |
| T-91-28 | Tampering (stored XSS) | Linked titles/names in Index.Mobile.cshtml and the onclick URL | medium | mitigate | Razor encoding; `onclick` URL built by `Url.Action("Details", ..., new { id })` with an integer id only (`Views/Calendar/Index.Mobile.cshtml:131,156,145,168`); no title or name inside script; no `Html.Raw` added (diff scan clean) | closed |
| T-91-29 | Information disclosure | Mobile linked rows | medium | mitigate | Rows render only the linked day entries from the service (DM-only already removed, `LinkedCalendarService.cs:104-108`). Facts `Agenda_LinkedDmOnlySession_IsHiddenFromAPlayerAndItsOnlyDayHasNoSection`, `Agenda_LinkedDmOnlySession_IsShownToADungeonMasterOfTheOwningBoard` | closed |
| T-91-30 | Information disclosure | Links to non-member boards | low | mitigate | No `onclick` and no anchor unless `ViewerIsMember` (`Views/Calendar/Index.Mobile.cshtml:143-151,166-174`). Facts `Agenda_NonMemberOfTheLinkedBoard_SeesPlainRowsWithNoNavigation`, `Agenda_SuperAdminWithoutMembershipOnTheLinkedBoard_SeesPlainRows` | closed |
| T-91-31 | Tampering | Details vote form (desktop and mobile) | high | mitigate | Linked content only via form-free partials with their own model (`_Calendar.cshtml:243`, `_Calendar.Mobile.cshtml:32`); static guard bans form controls, `id`/`for`, vote markers; facts `Details_Desktop_LinkingAddsNoVoteInputToThePlayersForm`, `DetailsMobile_LinesCarryNoFormControlIdOrForAttributeAndAddNoVoteInput` | closed |
| T-91-32 | Information disclosure | Linked entries on Details | high | mitigate | Same service, re-check and DM-only filter as `/calendar` (`QuestController.cs:387-402` → `LinkedCalendarService`). Facts assert the outside board's quest and name are absent and DM-only sessions hidden (`LinkedBoardQuestDetailsTests.cs:214-215`, `Details_Desktop_LinkedDmOnlySession*`, `DetailsMobile_LinkedDmOnlySession*`) | closed |
| T-91-33 | Information disclosure | Own events on Details | low | mitigate | Month models set only `Quests`, never `Events` (`QuestController.cs:369-374`). Fact `Details_Desktop_KeepsTheBoardsOwnEventOffThePageWhileLinkedEventsShow` | closed |
| T-91-34 | Tampering (stored XSS) | Also-that-day lines | medium | mitigate | Razor encoding throughout `_LinkedAlsoThatDay.cshtml`; no `Html.Raw` (static guard) | closed |
| T-91-35 | Denial of service | Details linked read | low | mitigate | One windowed read for all shown months, skipped when the quest has no proposed dates (`QuestController.cs:380-402`); empty for no board or no viewer (`LinkedCalendarService.cs:26-29`) | closed |
| T-91-36 | Denial of service | Windowed linked reads on SQL Server | medium | mitigate | Operator ran the migration and every linked read on real SQL Server 2022 (`mssql-dev`) on 2026-10-16: no translation or server errors, fallback not needed (91-10-SUMMARY, Operator verification). Reads remain pinned and re-checked | closed |
| T-91-37 | Information disclosure | End-to-end set lifecycle | high | mitigate | `LinkedBoardEndToEndTests` drives Platform POSTs for link and unlink, asserts the outside board never shows and the removed board is gone on the next request; operator confirmed unlink in the browser | closed |
| T-91-38 | Tampering | Feed and out-of-scope surfaces | medium | mitigate | `git diff --stat c4abdf84` over `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`, `AgendaController.cs`, `Views/Agenda`, `EventsController.cs`, `Views/Events`, `EventSeriesService.cs` re-run at `06f49b71`: empty. Seam tests in `QuestBoard.UnitTests/Architecture` also unchanged | closed |
| T-91-39 | Tampering (integrity) | EntityProfile `Group → GroupEntity` map | medium | mitigate | `Ignore()` on `LinkedBoardSetId` and `LinkedBoardSet` (`EntityProfile.cs:218-219`). Facts `RenamingALinkedBoardThroughTheGroupRepository_KeepsItsLink`, `RenamingALinkedBoard_KeepsItInItsSet` | closed |
| T-91-40 | Information disclosure | LinkedCalendarService re-check | high | mitigate | Ids only from the per-request Groups lookup (`LinkedCalendarService.cs:34-41`); no linked boards means no bypass read (`:35-38`); rows outside `linkedIds` dropped and logged (`:50-56`, `:92-98`). Unit facts `GetForViewer_RowFromOutsideTheLinkedSet_IsDroppedAndLoggedAsAnError`, `GetForViewer_NoOtherBoardInTheSet_ReturnsEmptyWithoutAnyCrossBoardRead`; outside-board absence facts on the page | closed |
| T-91-41 | Information disclosure | LinkedBoardSetRepository reads | low | mitigate | Reads project only id, name, type, set id and the viewer's membership role (`LinkedBoardSetRepository.cs:17-31,245-293`); the file has no `IgnoreQueryFilters`; `Groups`, `LinkedBoardSets` and `UserGroups` carry no query filter, so nothing is bypassed; `CrossBoardIgnoreQueryFiltersSeamTests` unchanged and passing | closed |
| T-91-SC | Tampering | npm/pip/cargo installs | low | accept | Accepted (see Accepted Risks Log). Re-verified: no `*.csproj`, `dotnet-tools.json`, lock or package manifest changed between `c4abdf84` and `06f49b71` | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `workflow.security_block_on` (high) count toward `threats_open`*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-91-01 | T-91-07 | Concurrent super-admin edits end as last write wins on a single column. The page is super-admin-only with a handful of sets. Reduced further by the concurrency token added in the review fix. | Phase 91 plan 03 (disposition: accept); confirmed in this audit | 2026-10-17 |
| AR-91-02 | T-91-09 | Quest Details re-adds only the page's own quest to its calendar even when the viewer's tier hides it elsewhere. The page is already reachable by URL today, so nothing new is disclosed. Deferred fix, out of this phase. | Phase 91 plan 02 (disposition: accept); confirmed in this audit | 2026-10-17 |
| AR-91-03 | T-91-14 | Non-members see another board's entry title, time, board name and DM name as plain text, by design. No link is rendered, so no 404 probe surface is added. | Phase 91 plan 05 (disposition: accept); confirmed in this audit | 2026-10-17 |
| AR-91-04 | T-91-26 | `calendar.css` changes are purely presentational additions; existing selectors are unchanged (0 deleted lines). | Phase 91 plan 07 (disposition: accept); confirmed in this audit | 2026-10-17 |
| AR-91-05 | T-91-SC | No package is installed in plan 01; `dotnet-ef` is the existing pinned local tool. No dependency manifest changed in the phase. | Phase 91 plan 01 (disposition: accept); confirmed in this audit | 2026-10-17 |

*Accepted risks do not resurface in future audit runs.*

---

## Audit Notes (non-blocking, not part of the register)

- **No unregistered flags.** The `## Threat Flags` section of all ten `91-*-SUMMARY.md` files reports none beyond the plan register.
- **Re-check is a consistency check.** `LinkedCalendarService` re-filters on the same `linkedIds` the query used, so it catches a dropped predicate or a bad containment translation, not a wrong set. The load-bearing controls are the server-side id source and the pinned predicate, both verified above (code comments say the same).
- **Pre-existing dependency advisory, not from this phase.** The build of the audited commit prints NU1903 / NU1902 for `System.Security.Cryptography.Pkcs 9.0.4` (transitive). No package manifest changed in phase 91, so this predates it. It is outside this phase's scope and worth a separate dependency review.

---

## Security Audit 2026-10-17

| Metric | Count |
|--------|-------|
| Threats found | 42 |
| Closed | 42 |
| Open | 0 |

Method: every plan `<threat_model>` register read and checked against the code at `06f49b71` (ASVS L1, file and line evidence above). Register authored at plan time, so the short-circuit rule applied and the auditor agent was not spawned. As an independent confirmation, the security-relevant suites were run against a clean `git archive` export of `HEAD` in an isolated directory (no shared build output, no database): `Linked*` unit and integration tests, `CrossBoardIgnoreQueryFiltersSeamTests`, `FeedRevisionWriteSeamTests`, `AntiForgeryTokenCoverageTests`, `PlatformAreaIntegrationTests`, `CalendarDungeonMasterSessionTests`, `CrossBoardAuthorizationBoundaryTests`, `CrossBoardOracleParityTests`: 119 of 119 unit and 156 of 156 integration tests passed, 0 failed. No implementation or source file was modified.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-17 | 42 | 42 | 0 | Claude (gsd-secure-phase) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-17
