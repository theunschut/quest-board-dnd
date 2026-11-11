---
phase: 102
slug: board-deletion
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-11-11
---

# Phase 102 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser → Platform `GroupController` (Delete GET/POST) | Super admin requests the deletion preview and posts the typed board name | Board id, typed name, antiforgery token; preview of board content names |
| Browser → Platform `UsersController` | Super admin reads users with their board memberships | User ↔ board membership names |
| Session → `GroupSessionMiddleware` | Active-board session keys trusted only after revalidation | Active board id/name, revalidation timestamp |
| Repository → SQL Server | One-transaction delete across every board-owned table, plus cross-board unlinks | All rows of one board; foreign keys on other boards' rows |
| Test harness → local SQL Server | Gated tests create and drop Guid-named scratch databases | Connection string from the environment (never printed) |

---

## Threat Register

Deduplicated across the ten plans (an ID repeated in several plans is one threat with the union of its mitigations).
Evidence is file:line at HEAD `2f780f16`, after the code review fixes.

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-102-01 | Tampering | `GroupRepository` scoped / reach-in helpers | high | mitigate | All 21 `IgnoreQueryFilters()` re-scoped by explicit `groupId` (`GroupRepository.cs:139-226`); reach-ins only null a pointer (246-256); `Delete_LeavesAnotherBoardUnchanged(true/false)` | closed |
| T-102-02 | Tampering | `DeleteConfirmed` POST (CSRF) | high | mitigate | `[ValidateAntiForgeryToken]` (`GroupController.cs:226-228`); `AntiForgeryTokenCoverageTests` reflection sweep | closed |
| T-102-03 | Tampering | delete of the wrong board | medium | mitigate | Ordinal compare of edge-trimmed names on both sides (`GroupService.cs:57-58`, `BoardNameText.cs`); member recheck before staging and again inside the serializable transaction (`GroupRepository.cs:239-240, 300-310`) | closed |
| T-102-04 | Denial of Service | partial delete | medium | mitigate | Single `SaveChangesAsync` (`GroupRepository.cs:312`) in a serializable transaction on relational providers; tracker cleared + rethrow on failure; FK-integrity sweep; real-server rollback fact | closed |
| T-102-05 | Information Disclosure | exception text on failed delete | low | mitigate | Logged server-side, fixed friendly sentence, redirect (`GroupController.cs:231-240`) | closed |
| T-102-06 | Elevation of Privilege | non-super-admin reaching Delete | high | mitigate | Class-level `SuperAdminOnly` (`GroupController.cs:22`); no new route | closed |
| T-102-07 | Repudiation | one-way delete with no record | low | accept | Interim only; superseded by T-102-25 | closed |
| T-102-08 | Denial of Service | super-admin session naming a deleted board | medium | mitigate | Board-exists check, clear + gate (redirect on GET, 409 otherwise) (`GroupSessionMiddleware.cs:157-196`); every write revalidated | closed |
| T-102-09 | Tampering | partial session clear | low | mitigate | One helper removes all three keys (`ActiveBoardSessionExtensions.cs:17-22`) | closed |
| T-102-10 | Spoofing | open redirect via `returnUrl` | low | accept | Own local path + query, `Uri.EscapeDataString`, picker checks `Url.IsLocalUrl` (unchanged) | closed |
| T-102-11 | Elevation of Privilege | super admin membership-gated by mistake | medium | mitigate | Super admins excluded from the membership arm (`GroupSessionMiddleware.cs:139`); `SuperAdmin_StaleTimestamp_InvokesNextWithoutMembershipCheck` | closed |
| T-102-12 | Tampering | board names on Users page (stored XSS) | medium | mitigate | Razor encoding only (`Users/Index.cshtml:53-54`, `Index.Mobile.cshtml:65`); encoding fact | closed |
| T-102-13 | Information Disclosure | membership list | low | mitigate | `UsersController` stays `SuperAdminOnly` | closed |
| T-102-14 | Denial of Service | per-user query loop | low | mitigate | One projection query before the loop (`UsersController.cs:16`, `UserRepository.cs:78-92`) | closed |
| T-102-15 | Tampering | gated test touching the app database | high | mitigate | `InitialCatalog` forced to master; Guid-named `ScratchDatabase` dropped on dispose (`MigratorSqlServer.cs:44-49, 161-185`) | closed |
| T-102-16 | Tampering | new table referencing Groups unnoticed | medium | mitigate | `BoardOwnedForeignKeysTests` pins the full FK set | closed |
| T-102-17 | Information Disclosure | connection string in test output | low | accept | `MigratorSqlServer` unchanged; new tests print no connection string | closed |
| T-102-18 | Tampering | names in `_BoardDeleteBody.cshtml` (stored XSS) | high | mitigate | No raw output; string view-model fields; `Get_EncodesUserAuthoredNames`, static guard | closed |
| T-102-19 | Information Disclosure | preview reachable by non-super-admin | high | mitigate | Preview only from `SuperAdminOnly` actions (`GroupController.cs:222, 271`) | closed |
| T-102-20 | Tampering | preview scoped to the wrong board | medium | mitigate | Preview reuses the delete's scoped helpers (`GroupRepository.cs:350-377`); `Preview_DoesNotDependOnTheActiveBoard` | closed |
| T-102-21 | Denial of Service | loading blobs / whole entities | low | mitigate | Projections and `CountAsync` only | closed |
| T-102-22 | Tampering | bypassing the client gate | medium | mitigate | Server check authoritative; HTTP facts post wrong case and no name, both refused | closed |
| T-102-23 | Tampering | DOM XSS via the board name in the module | medium | mitigate | Encoded data attribute; text nodes only (`platform-board-delete.js:44-47`); sink guard | closed |
| T-102-24 | Information Disclosure | echoing the typed text | low | mitigate | `confirmName` input renders no `value` (`_BoardDeleteBody.cshtml:96-100`); HTTP fact | closed |
| T-102-25 | Repudiation | delete with no audit trail | medium | mitigate | One Information log line: actor, board id, name, per-kind counts (`GroupController.cs:255-257, 288-291`) | closed |
| T-102-26 | Denial of Service | actor's session naming the deleted board | medium | mitigate | `ClearActiveBoardIfAsync(id)` in its own try/catch; middleware covers other sessions on every write | closed |
| T-102-27 | Tampering | CSRF on the (re-rendered) confirm form | high | mitigate | `asp-action` tag-helper form renders the token on every render; attribute unchanged | closed |
| T-102-28 | Tampering | UAT / gated test deleting real member content | high | mitigate | Gated runs touch only master and `QuestBoardMigratorProbe_*` scratch databases; normal suites use InMemory; UAT limited to two "UAT …" boards | closed |
| T-102-29 | Information Disclosure | SQL credentials exposed | low | accept | Credentials passed from user-secrets via an environment variable, never printed; absent from every committed file on every ref | closed |
| T-102-30 | Denial of Service | image blobs in the tracker | low | mitigate | Image rows never loaded; database cascade removes them (pinned and proven on SQL Server) | closed |
| T-102-31 | Information Disclosure | other boards' content in the preview | medium | mitigate | Reach-ins expose only follow-up title + board name and two counts | closed |
| T-102-32 | Tampering | inline script/handlers reintroduced | low | mitigate | `DeleteViews_CarryNoInlineStyleScriptOrHandler` | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-102-01 | T-102-07 | Interim lack of a delete record during the tracer; superseded by the audit log line (T-102-25), so no residual remains | Plan threat model (102-01) | 2026-11-11 |
| AR-102-02 | T-102-10 | Return URL is the request's own local path and query, escaped; the picker validates `Url.IsLocalUrl` and is unchanged | Plan threat model (102-02) | 2026-11-11 |
| AR-102-03 | T-102-17 | Existing `MigratorSqlServer` handling reused unchanged; the string goes only to SqlClient and is never printed by the new tests | Plan threat model (102-05) | 2026-11-11 |
| AR-102-04 | T-102-29 | The gated run reads the connection from the environment; the run used user-secrets via an env var, never echoed; verified absent from the repository history | Plan threat model (102-10) | 2026-11-11 |

*Accepted risks do not resurface in future audit runs.*

---

## Residual Notes (informational, nothing open)

- T-102-01 / review IN-01: the chained-transaction clause could remove a return whose own shop item is on another board; unreachable today because returns copy `ShopItemId` from the original.
- T-102-03: a pre-existing board whose stored name is only edge whitespace would match an empty typed name; the route id still pins which board is deleted, and new names like that are now refused.
- T-102-05 / review IN-05: the name-mismatch re-render sits outside the try/catch; a database failure there reaches the generic error page, not exception text, with nothing changed.
- T-102-02 / review IN-04: anti-forgery on the delete POST is pinned only by the reflection sweep (the test factory's antiforgery decorator always passes).
- T-102-23: the static sink guard catches `.innerHTML =` but not `+=`; the module has no sink today.
- T-102-08: a GET inside the 5-minute window on a deleted board shows an empty board; writes are stopped at once.
- T-102-28: the gated SQL Server tests were run by the executor and orchestrator against the local server (scratch databases only), departing from the plan's "the executor never touches the database". Orphaned `QuestBoardMigratorProbe_*` databases could not be ruled out (no `sqlcmd`); they would hold only test data.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-11 | 32 | 32 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-11
