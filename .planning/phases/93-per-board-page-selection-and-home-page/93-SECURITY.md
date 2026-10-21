---
phase: "93"
slug: "per-board-page-selection-and-home-page"
status: secured
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
created: "2026-10-21"
---

# Phase 93 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Declared In |
|----------|-------------|-------------|
| browser → any gated controller action | Typed URLs, bookmarks, emailed links and crafted POSTs to a switched-off page cross here | 93-01 |
| session → active board | The active board id selects which board's settings apply; it is never taken from the request | 93-01 |
| filter → redirect | The refusal builds a redirect; its target must never come from request data | 93-01 |
| browser → 7 controllers | Typed URLs and crafted requests on any verb reach actions whose menu item is hidden | 93-02 |
| resolver → layouts and Agenda | Menu and button rendering must agree with the server gate | 93-03 |
| Platform form → settings service | Posted page keys, switch values and home page are untrusted, even from a super admin's browser | 93-04 |
| settings service → another board's rows | Counts read a board other than the active one | 93-04 |
| linked set read → another board's settings | The overlay reads which pages other boards offer | 93-05 |
| rendered link → cross-board deep link | A followed link switches the session board before the gate decides | 93-05 |
| super admin browser → GroupController.Edit POST | Page keys, switch values, home page and the board id are untrusted form data | 93-06 |
| controller → Groups row | The save must not overwrite the link column or race silently | 93-06 |
| browser → QuestController signup POSTs | `CharacterId`/`characterId` form fields are untrusted and may come from a stale or crafted form | 93-07 |
| controller → event and series reads | What the calendar loads decides what links can render | 93-08 |
| redirect-after-save → gate | A redirect target must not itself be refused | 93-08 |
| browser → MoveController | `targetBoardId` and `stamp` are untrusted; a crafted target could name an off board | 93-09 |
| Platform form → Quests switch | A super admin's post can now switch the core page off | 93-10 |
| settings service → another board's quests | The live count reads a board other than the active one | 93-10 |
| old links (emails, bookmarks, deep links) → quest pages | Links created before the switch keep arriving | 93-10 |
| source tree → shipped code | Comments and string literals ship with the build | 93-11 |
| DI container → resolver | A registration cycle would only surface at runtime | 93-11 |
| browser → 6 controllers | Typed URLs and crafted requests on any verb reach actions whose menu item is hidden | 93-12 |
| reflection test → assembly | The closed list is the build-time guard that a future controller is classified | 93-12 |
| query string → GroupPickerController | `returnUrl` is untrusted input | 93-13 |
| resolver → HomeController | The home redirect must never point at a refused page | 93-13 |
| policy code → every consumer | A silent rule regression would widen what the gate, menus and Platform form allow | 93-14 |
| mapping → Groups row | A mapping regression could overwrite the link column on a settings save | 93-14 |
| Groups row → super admin board list | Each board's stored home is shown on the Platform list | 93-15 |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-93-01 | Elevation of privilege | BoardPageGateFilter | high | mitigate | Global resource filter refuses every verb on a page that is off or type-disallowed (D-01, D-06); runs after authorization so it never weakens a policy; Campaign and SuperAdmin refusals tested here; plan 12 adds the reflection coverage test that fails the build when an action is unclassified | closed |
| T-93-02 | Spoofing (open redirect) | BoardPageGateFilter | medium | mitigate | Redirect target is built from `BoardPageRoutes` route names only, never from query or form data; the existing `Url.IsLocalUrl` return-URL handling is untouched | closed |
| T-93-03 | Denial of service (redirect loop) | BoardPageRoutes.HomeRoute | medium | mitigate | `EffectiveHome` is available by construction (pinned by the 2 x 512 x 9 invariant test in plan 14); a null home falls back to ungated Account/Profile; the gate passes through when no board resolves (tested here) | closed |
| T-93-04 | Tampering | EntityProfile Group map | medium | mitigate | The `LinkedBoardSetId`/`LinkedBoardSet` Ignore() lines stay, so a settings save cannot overwrite the link or defeat its concurrency token; the existing Edit POST suites run here, and `BoardPageSettingsMappingTests` (plan 14) pins it | closed |
| T-93-05 | Information disclosure | BoardPageResolver | low | accept | The decision depends only on the active board's own settings and type, never on whether an entity id exists, so a refusal reveals nothing about other boards | closed |
| T-93-06 | Tampering (stale authorization state) | BoardPageResolver | low | mitigate | Read fresh per request (scoped memo keyed on the active group id), never cached in session or claims, so a super admin's change applies on the next request | closed |
| T-93-45 | Elevation of privilege | Calendar, Events, Series, Quest, QuestLog, Admin quest and Move actions | high | mitigate | Class or action `[BoardPage]` read by the global resource filter refuses every verb before model binding; proven per page in `BoardPageGateMatrixTests`; plan 12's coverage test pins each attribute | closed |
| T-93-10 | Tampering | UpdateSignupCharacter with Characters off | medium | mitigate | Action carries both Quests and Characters; filter requires all; proven with CharacterId unchanged | closed |
| T-93-12 | Information disclosure | refusal on another board's id | low | accept | The decision uses only the active board's settings; an existing or missing id gets the same answer | closed |
| T-93-14 | Elevation of privilege | hidden menu items and buttons | low | mitigate | Menus are presentation only; the gate (plans 01, 02 and 12) refuses the same pages on the server, and both read one resolver so they cannot disagree | closed |
| T-93-15 | Information disclosure | unresolved board menus | low | accept | The unresolved state renders exactly what it rendered before; the gate passes through and the board query filters fail closed | closed |
| T-93-16 | Tampering (mass assignment) | BoardPageSettingsService | high | mitigate | Type-disallowed, non-switchable and dependency-locked switches are ignored; undefined enum values ignored; absent pages unchanged; home must be a candidate available in the result; every rule unit-tested | closed |
| T-93-17 | Information disclosure | CountUpcomingForBoardAsync, CountActiveSeriesForBoardAsync | medium | mitigate | Return scalars only, scoped by explicit `GroupId` inside the already-allowlisted `EventRepository.cs`; the seam allowlist stays unchanged | closed |
| T-93-18 | Tampering (integrity of the feed promise) | Events switch | medium | mitigate | On-to-off refused while upcoming (cancelled included) events or running series exist, judged by `IBoardClock`; feed files untouched | closed |
| T-93-19 | Tampering (race) | count then save | low | accept | Super-admin-only settings action; a create between count and save is refused afterwards by the gate; no transaction, recorded choice 5 | closed |
| T-93-20 | Information disclosure | LinkedBoard.DisabledPages | low | accept | The mask only drives whether a link renders; it is never shown, and it is read only for boards already in the viewer's linked set | closed |
| T-93-21 | Denial of service (dead end) | linked entry links | low | mitigate | Entries from a board with the page off render inert, so no link leads into a cross-board switch that ends in a refusal; tested on both user agents | closed |
| T-93-22 | Tampering | entry visibility rules | low | mitigate | Membership and DM-only visibility rules are unchanged; `CanOpen` only narrows the link, never widens what is shown | closed |
| T-93-23 | Elevation of privilege | GroupController | high | mitigate | Class-level `SuperAdminOnly` policy unchanged; no new action | closed |
| T-93-24 | Tampering (mass assignment) | GroupEditViewModel | high | mitigate | `BoardType`, labels, lock flags and home choices are `[BindNever]` and recomputed; posted rows go through `BoardPageSettingsService` (type ceiling, dependency lock, absent = unchanged, home validated); proven by the Campaign tamper and Id+Name-only tests | closed |
| T-93-25 | Tampering (CSRF) | Edit POST | high | mitigate | `[ValidateAntiForgeryToken]` kept; `AntiForgeryTokenCoverageTests` | closed |
| T-93-26 | Tampering (lost update) | groupService.UpdateAsync | medium | mitigate | `LinkedBoardSetId` stays ignored in the map and remains the concurrency token; `DbUpdateConcurrencyException` becomes a form error (unit-tested), never a 500 — closed by stale-form check in `GroupController.Edit` (commit 1ce594cd) | closed |
| T-93-27 | Information disclosure | refusal messages | low | accept | Messages give only counts of the edited board's own events, and only to a super admin | closed |
| T-93-28 | Tampering | Details POST, JoinFinalizedQuest | medium | mitigate | `ResolveCharacterAssignmentAsync` returns NotAssignable while Characters is not served; callers refuse through "Invalid character selection."; proven by HTTP tests asserting no signup carries the posted CharacterId — closed by building the signup server-side in `QuestController.Details` POST (commit c36d9e19) | closed |
| T-93-29 | Tampering | UpdateSignupCharacter | medium | mitigate | Gate requires Characters (plan 02); proven here end to end with CharacterId unchanged | closed |
| T-93-30 | Information disclosure | hidden pickers | low | accept | Hiding the pickers reveals nothing new; the viewer's own character list is still loaded by the controller as before but not rendered | closed |
| T-93-31 | Information disclosure | CalendarController with Events off | low | mitigate | Events and runway series are never read when Events is not served, so nothing can leak through a view; tested | closed |
| T-93-32 | Denial of service (dead end) | EventsController/SeriesController redirects | low | mitigate | Redirects go to the Events list when Calendar is not served; tested for create, edit, delete, series delete and detach | closed |
| T-93-33 | Spoofing (open redirect) | RedirectAfterSaveAsync | low | mitigate | Targets are fixed route names; no request data is used | closed |
| T-93-34 | Tampering | ConfirmQuest/ConfirmEvent with an off target | high | mitigate | The move services refuse `TargetPageOff` from a fresh linked-set read before any write; unit tests assert ApplyAsync is never called; HTTP tests assert GroupId unchanged | closed |
| T-93-35 | Elevation of privilege | MoveController | high | mitigate | Class-level `SuperAdminOnly` and the plan 02 page gates are unchanged | closed |
| T-93-36 | Denial of service (unhandled state) | MoveController GET | low | mitigate | Explicit `TargetPageOff` case on both GETs; tested | closed |
| T-93-37 | Tampering (availability of the core loop) | Quests switch | high | mitigate | Refused while any live quest exists (D-20) and while the quest list is the home (D-13); default on; super-admin only; proven by unit and HTTP tests | closed |
| T-93-38 | Information disclosure | CountLiveForBoardAsync | medium | mitigate | Scalar count scoped by explicit `GroupId` inside the already-allowlisted `QuestRepository.cs`; seam allowlist unchanged | closed |
| T-93-39 | Elevation of privilege | quest routes after the switch | high | mitigate | Class and action `[BoardPage(BoardPage.Quests)]` from plan 02 refuse every verb; this plan's HTTP tests walk list, details, create, manage, admin and move | closed |
| T-93-40 | Denial of service (dead end) | old quest links | low | mitigate | Every old link redirects to the board's home with the toast (D-05); linked calendars render inert; DM history is plain text | closed |
| T-93-41 | Tampering (feed integrity) | guarded feed files | medium | mitigate | No change to the three guarded files, checked by `git diff` against c35512f3 in this task's verify | closed |
| T-93-42 | Denial of service | production DI graph | medium | mitigate | `BoardPageDependencyInjectionTests` resolves the page resolver through the real board-type resolver | closed |
| T-93-43 | Repudiation / maintainability | planning references in source | low | mitigate | `BoardPageStaticGuardTests` fails on any planning reference in the phase's files | closed |
| T-93-44 | Elevation of privilege | end-to-end gate | high | mitigate | The walk proves form, rules, storage, gate, menus and home agree on both layouts, driven only through the super-admin Platform form | closed |
| T-93-07 | Elevation of privilege | ShopManagementController/PlayersController/DungeonMasterController on Campaign | high | mitigate | Class or action `[BoardPage]` plus the policy's type ceiling closes today's typed-URL gap; Campaign closure proven per page in `BoardPageGateMatrixTests` | closed |
| T-93-08 | Elevation of privilege | an unannotated future action | high | mitigate | `BoardPageCoverageTests` fails the build for any unclassified action and pins each controller's page key | closed |
| T-93-09 | Tampering | crafted POST to a switched-off page | high | mitigate | Resource filter refuses before model binding on every verb; proven with POST /ShopManagement/Create writing nothing | closed |
| T-93-11 | Denial of service | picture endpoints used by quest pages | low | mitigate | `[BoardPageExempt]` on the four picture endpoints keeps portraits loading (D-07); proven by HTTP facts with the page off | closed |
| T-93-13 | Spoofing (open redirect) | GroupPickerController.RedirectToLocal | medium | mitigate | The existing `Url.IsLocalUrl` check is kept unchanged; the new fallback is a route name (`Home/Index`), never request data; tested with a local return URL | closed |
| T-93-49 | Denial of service (redirect loop) | HomeController.Index | medium | mitigate | The target comes from `BoardPageRoutes.HomeRoute`, which is an available home candidate or ungated Account/Profile; the switched-off and type-disallowed fallbacks are tested | closed |
| T-93-47 | Elevation of privilege (regression) | BoardPagePolicy | medium | mitigate | All-masks Campaign ceiling theory and the 2 x 512 x 9 EffectiveHome invariant go red on any change that widens availability or points the home at a refused page (backs T-93-03) | closed |
| T-93-48 | Tampering (regression) | EntityProfile Group map | medium | mitigate | `BoardPageSettingsMappingTests` asserts `LinkedBoardSetId` survives `UpdateAsync` (backs T-93-04) | closed |
| T-93-46 | Information disclosure | Platform board-list badge | low | accept | The badge shows only a board's own home page label, on a page already restricted to super admins by the class-level `SuperAdminOnly` policy | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-93-01 | T-93-05 | The decision depends only on the active board's own settings and type, never on whether an entity id exists, so a refusal reveals nothing about other boards | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-02 | T-93-12 | The decision uses only the active board's settings; an existing or missing id gets the same answer | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-03 | T-93-15 | The unresolved state renders exactly what it rendered before; the gate passes through and the board query filters fail closed | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-04 | T-93-19 | Super-admin-only settings action; a create between count and save is refused afterwards by the gate; no transaction, recorded choice 5 | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-05 | T-93-20 | The mask only drives whether a link renders; it is never shown, and it is read only for boards already in the viewer's linked set | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-06 | T-93-27 | Messages give only counts of the edited board's own events, and only to a super admin | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-07 | T-93-30 | Hiding the pickers reveals nothing new; the viewer's own character list is still loaded by the controller as before but not rendered | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |
| AR-93-08 | T-93-46 | The badge shows only a board's own home page label, on a page already restricted to super admins by the class-level `SuperAdminOnly` policy | operator (plan-time accept disposition; rationale re-confirmed by gsd-security-auditor) | 2026-10-21 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-21 | 49 | 47 | 2 (medium, non-blocking: T-93-26, T-93-28) | gsd-security-auditor (opus), audited at a276cf5b |
| 2026-10-21 | 49 | 49 | 0 | orchestrator re-verification after fixes 1ce594cd (T-93-26) and c36d9e19 (T-93-28) |

### Notes

- **T-93-26** (lost update on Platform Edit): the auditor found `DisabledPages`/`HomePage` had no concurrency cover (same root cause as code review WR-01). The form now posts the mask and home it was rendered from, and `GroupController.Edit` refuses a stale post with the existing concurrency message before any count or save (`GroupController.cs` `formIsStale`; `GroupControllerEditConcurrencyTests`). The count-then-save window (T-93-19) stays an accepted residual.
- **T-93-28** (character smuggled through the signup POST): the Details POST bound `PlayerSignup` wholesale, so a nested `Character.*` payload skipped the Characters-off guard and inserted a character. The same binding also let a player post `IsSelected`, `SignupTime`, `LastVoteChangeTime` and votes for another quest's dates (pre-existing, outside the register). The action now builds the signup from `CharacterId` and this quest's date votes only; `QuestSignupOverpostingTests` (4 cases) failed against the old action and passes now.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
