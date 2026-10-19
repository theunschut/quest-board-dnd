---
phase: 92
slug: move-a-quest-or-event-to-a-linked-board
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-19
---

# Phase 92 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser → MoveController GET/POST | Untrusted `id`, `targetBoardId` (query/form) and `stamp` cross here, for quests and events | ids, posted change-detector stamp |
| session → active board | The active board identifies the source only; it never names the target | board id |
| target board membership (UserGroups) → move plan | Who stays and who goes is decided by membership read at request time | membership, signups, availability rows |
| Identity roles → DM rule | A SuperAdmin DM bypasses the board-role requirement | role claims |
| board clock → eligibility | "Today" is the board's local date; the container clock is UTC | board-local date |
| service → QuestBoardContext | The write crosses board filters; the unit of work must be atomic | Quests, Events, signups, votes, availability |
| stored user text → Move page / toast / email HTML | Titles, player names, board names rendered for the super admin, in the toast (via TempData) and in outbound email | user-entered text |
| move → outbound mail (Hangfire) | Addresses and names leave the system; serialized job arguments are rehydrated later | email addresses, names |
| role → Details markup | Whether the move control renders depends on the viewer's Identity role | role |
| server log → operators | The audit line is read by whoever reads the logs | ids and counts only |
| future edits → move code | Later changes could quietly break the single save or the clock seam | source |

---

## Threat Register

41 register rows across plans 01–09; T-92-02 recurs in plans 01, 03, 04 and 06 and T-92-08 in plans 03 and 04, giving 37 unique threat IDs.

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-92-01 | Elevation of privilege | MoveController | high | mitigate | Class-level `[Authorize(Policy = "SuperAdminOnly")]` (MoveController.cs:12; Program.cs:90-91); BoardMoveAuthorizationTests | closed |
| T-92-02 | Tampering | `targetBoardId` (POST, GET, events) | high | mitigate | Linked set re-read on every preview/move (QuestMoveService.cs:195-222, EventMoveService.cs:113-140); source excluded (LinkedBoardSetRepository.cs:267); target-outside-set tests | closed |
| T-92-03 | Information disclosure / Tampering | posted id of another board | medium | mitigate | Loads run under the source board filter; NotFound → `NoLongerOnThisBoard` redirect with a kind-only toast (MoveController.cs:131-135), identical for missing / other-board / just-moved ids; same-answer tests | closed |
| T-92-04 | Tampering (integrity) | Quest/EventMoveRepository | high | mitigate | Validate before mutate, GroupId last, one SaveChangesAsync, ChangeTracker.Clear on failure; per-signup and membership recheck (QuestMoveRepository.cs:34-131); BoardMoveRepositorySingleSaveTests | closed |
| T-92-05 | Tampering | feed revision | medium | mitigate | Tracked GroupId assignment only; FeedRevisionStamper watches GroupId; FeedRevisionWriteSeamTests; feed files untouched | closed |
| T-92-06 | Tampering (CSRF) | ConfirmQuest / ConfirmEvent | high | mitigate | `[ValidateAntiForgeryToken]` (MoveController.cs:42, :97); AntiForgeryTokenCoverageTests | closed |
| T-92-07 | Repudiation | move audit | low | mitigate | One structured log line with actor, entry, board ids and counts (QuestMoveService.cs:71-75, EventMoveService.cs:70-73) | closed |
| T-92-08 | Tampering | stale or forged stamp | medium | mitigate | MoveAsync rebuilds the preview before the ordinal stamp compare; mismatch re-renders (200); repository re-validates; stale-stamp tests | closed |
| T-92-09 | Tampering | eligibility bypass by direct POST | medium | mitigate | MoveEligibility with `boardClock.Today` on every build (QuestMoveService.cs:187, EventMoveService.cs:105); closed/played/series tests | closed |
| T-92-10 | Spoofing / abuse | mail recipients | medium | mitigate | One recipient per job (HangfireQuestEmailDispatcher.cs:67-99); confirmed-address gate (QuestMoveService.cs:100); BoardMoveMailEnqueueTests | closed |
| T-92-11 | Information disclosure | Removed email names the target board | low | accept | See Accepted Risks Log AR-92-01 | closed |
| T-92-12 | Tampering | duplicate send on Hangfire retry | low | mitigate | One SendAsync per job; caller de-duplicates by user id; BoardMoveMailJobTests | closed |
| T-92-13 | Information disclosure | Email Preview actions | low | accept | See Accepted Risks Log AR-92-02 | closed |
| T-92-14 | Tampering (HTML injection) | email templates | medium | mitigate | No MarkupString in QuestMoved.razor; `@MovedToBoardName` encoded; QuestMovedRenderTests.TitleWithMarkup_IsHtmlEncoded | closed |
| T-92-15 | Elevation of privilege | GET Move page | high | mitigate | Class-level policy covers GET; no AllowAnonymous; authorization and anonymous tests | closed |
| T-92-16 | Tampering (XSS) | Views/Move/Index and Index.Mobile | medium | mitigate | No Html.Raw / MarkupString / HtmlString; encoded-title and view-hygiene tests | closed |
| T-92-17 | Information disclosure | Move URL followed from another board | low | mitigate | No Move route in CrossBoardLinkRegistry (guard test); other-board ids answer like missing ids | closed |
| T-92-18 | Tampering (open redirect) | MovedEntryUrl | low | mitigate | Server-built relative `Url.Action` (MoveController.cs:152); `Url.IsLocalUrl` gate (_Toasts.cshtml:118) | closed |
| T-92-19 | Tampering / Elevation | DM rule | high | mitigate | GetGroupRoleAsync + IsSuperAdminAsync on every GET/POST (QuestMoveService.cs:228-235); POST binds only id, targetBoardId, stamp; DM-rule HTTP tests | closed |
| T-92-20 | Information disclosure | non-member signups | medium | mitigate | Split by target membership, dropped signups and votes removed before the save; membership re-read in the repository | closed |
| T-92-21 | Tampering | follow-up chain | medium | mitigate | Both chain ends nulled before the single save; chain tests | closed |
| T-92-22 | Spoofing / abuse | move mail | medium | mitigate | `TryClaim`: confirmed non-empty address, actor excluded, de-duplicated (QuestMoveService.cs:98-106); mail gate tests | closed |
| T-92-23 | Tampering | waitlist promotion | medium | mitigate | Promotions chosen before the save and rechecked in the repository; BoardMoveWaitlistParityTests | closed |
| T-92-24 | Denial of service | false failure on enqueue | low | mitigate | Per-enqueue try/catch, enqueue only after Moved (QuestMoveService.cs:69-118); dispatcher-throws test | closed |
| T-92-25 | Tampering | moving a series occurrence | medium | mitigate | MoveEligibility refuses `SeriesId != null` on every GET/POST; series tests | closed |
| T-92-26 | Information disclosure | non-member answers | medium | mitigate | Answers split by membership, non-member rows removed before the save, membership rechecked | closed |
| T-92-27 | Elevation of privilege | Event GET and POST | high | mitigate | Class-level policy covers Event and ConfirmEvent; board Admin/DM refusal test | closed |
| T-92-28 | Tampering | duplicate automatic rows | low | mitigate | Adds only for members with no row; repository returns Changed if a row appeared; unique (EventId, UserId) index | closed |
| T-92-29 | Tampering (XSS) | consequence rows and names list | medium | mitigate | No Html.Raw / script / inline style in Move views; markup-name test | closed |
| T-92-30 | Information disclosure | removed people's names on the page | low | accept | See Accepted Risks Log AR-92-03 | closed |
| T-92-31 | Elevation of privilege | move control on Details | medium | mitigate | ViewBag.MoveControl only for `User.IsInRole("SuperAdmin")` (QuestController.cs:356, EventsController.cs:78); control tests | closed |
| T-92-32 | Tampering (XSS) | Moved toast | medium | mitigate | Razor-encoded TempData, no Html.Raw; encoded-title toast test | closed |
| T-92-33 | Tampering (open redirect) | toast link | low | mitigate | As T-92-18 | closed |
| T-92-34 | Information disclosure | linked set shown on Details | low | accept | See Accepted Risks Log AR-92-04 | closed |
| T-92-35 | Tampering | later regressions | low | mitigate | BoardMoveRepositorySingleSaveTests, AmbientClockSeamTests, BoardMoveStaticGuardTests | closed |
| T-92-36 | Information disclosure | move log line | low | mitigate | End-to-end audit tests assert no seeded name or email is logged | closed |
| T-92-37 | Repudiation | super admin moves | low | mitigate | End-to-end tests assert exactly one audit record with the true counts | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-92-01 | T-92-11 | A removed player is a member of a board in the same linked set and already sees that board's name on their calendar, so naming it in the Removed email discloses nothing new. | Plan 92-02 threat model (operator-approved plan) | 2026-10-19 |
| AR-92-02 | T-92-13 | The Email Preview controller is AdminOnly and the new actions render fixed sample data only. | Plan 92-02 threat model (operator-approved plan) | 2026-10-19 |
| AR-92-03 | T-92-30 | Only a SuperAdmin reaches the Move page; the names shown are exactly the people the move would remove, which the page must show so the super admin can decide. | Plan 92-07 threat model (operator-approved plan) | 2026-10-19 |
| AR-92-04 | T-92-34 | The control renders only for a SuperAdmin, who already sees every board on the Platform pages. | Plan 92-08 threat model (operator-approved plan) | 2026-10-19 |

*Accepted risks do not resurface in future audit runs.*

### Residual notes (informational, not counted in threats_open)

- **Insert between read and save.** A signup or availability row inserted after the repository's read and before `SaveChangesAsync` is not caught without a serializable transaction; such a row could reach the target board unchecked for membership. Touches T-92-20 / T-92-26; the declared mitigations hold. Window is milliseconds inside a super-admin-only action.
- **Actor id 0 (review IN-02).** If the acting SuperAdmin principal has no user row, `actor.Id` resolves to 0, weakening the actor exclusion (T-92-22) and the actor id in the audit line (T-92-37). Requires a valid SuperAdmin principal with no user row.
- **DM rule not re-checked in the repository.** T-92-19 is recomputed in the service on every GET/POST but not re-validated inside `ApplyAsync`.
- **Pre-existing MarkupString.** `WaitlistPromoted.razor` renders the quest description as markup (sanitized by MarkdownService: Markdig `DisableHtml` + HtmlSanitizer). Not introduced by this phase; this phase only added the encoded `@MovedToBoardName`.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-19 | 37 (41 rows) | 37 | 0 | gsd-security-auditor (opus), after review fixes b44d2379 and 06751a48; targeted tests 281 unit + 191 integration passed |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-19
