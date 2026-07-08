---
phase: 55
slug: fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
status: verified
threats_open: 0
asvs_level: 1
created: 2026-07-06
---

# SECURITY.md — Phase 55: Fix cross-tenant quest leak on quest board (quests from another group)

**Audit date:** 2026-07-06
**ASVS Level:** 1
**Block on:** high
**Threat register source:** `55-01-PLAN.md`, `55-02-PLAN.md`, `55-03-PLAN.md`, `55-04-PLAN.md` `<threat_model>` blocks (register authored at plan time — all 4 plans had parseable threat models). No `## Threat Flags` entries in any of the 4 SUMMARY.md files — no unregistered attack surface reported by the executor.

## Result: SECURED — 16/16 threats closed, 0 open

---

## Threat Verification

| Threat ID | Category | Disposition | Evidence |
|-----------|----------|-------------|----------|
| T-55-01 | Information Disclosure | mitigate | `QuestBoard.Repository/Entities/QuestBoardContext.cs:253-328` — all 7 group-scoped entities (`QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`, `PlayerDateVoteEntity`, `PlayerSignupEntity`, `ReminderLogEntity`, `UserTransactionEntity`) use `activeGroupContext.ActiveGroupId != null && <path>.GroupId == activeGroupContext.ActiveGroupId`. Grep confirms exactly 10 occurrences of `ActiveGroupId != null &&` (7 hardened + 3 pre-existing CharacterEntity family) and 0 occurrences of the fail-open `ActiveGroupId == null \|\|` shape. Regression proof: `QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs` — 7 zero-rows-on-null assertions + 1 positive companion test, all passing (13/13 in the filtered unit run including Plan 04 tests). Additional integration coverage: `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs::GroupFilter_NullGroupIdShowsNoGroups`, `GroupFilter_NonExistentGroup_ReturnsEmpty` |
| T-55-02 | Elevation of Privilege | mitigate | Same evidence as T-55-01 — the filter hardening is independent of the middleware gate, confirmed by the filter tests instantiating `QuestBoardContext` directly (no HTTP pipeline/middleware involved), proving the data layer alone fails closed |
| T-55-03 | Tampering | mitigate | `QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs` exists, compiles, and asserts zero rows for a null-`ActiveGroupId` context for all 7 entities (`QuestEntity_NullActiveGroupId_ReturnsZeroRows`, `ShopItemEntity_...`, `ProposedDateEntity_...`, `PlayerDateVoteEntity_...`, `PlayerSignupEntity_...`, `ReminderLogEntity_...`, `UserTransactionEntity_...`) — runs as part of the standard unit test suite, so a future regression reintroducing the fail-open shape breaks CI |
| T-55-04 | Elevation of Privilege / Information Disclosure | mitigate | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:77-107` — no `IsInRole("SuperAdmin")` blanket-bypass branch exists (the only `IsInRole("SuperAdmin")` reference left in the file, line 115, is the Plan-04 re-validation exclusion, not a gate bypass). Guard order confirmed: anonymous check (79-83) → exempt-path check (85-89) → null-`ActiveGroupId` gate applying to all roles (91-107). Regression proof: `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs::SuperAdmin_NoActiveGroup_RedirectsToGroupPick` and `SuperAdmin_NoActiveGroup_ProtectedAreaRedirectsToGroupPick` ([Theory], 3 routes) both pass |
| T-55-05 | Tampering | mitigate | `grep -c 'NotRedirectedByMiddleware'` on `GroupSessionMiddlewareIntegrationTests.cs` returns 0 — the old contradictory test is gone, replaced by `SuperAdmin_NoActiveGroup_RedirectsToGroupPick` (asserts `location.Should().Contain("/groups/pick")`). Full filtered integration run: 25/25 passing including this test |
| T-55-06 | Repudiation / documentation drift | mitigate | `.planning/codebase/CONCERNS.md:288-292` — entry now titled "SuperAdmin-specific authorization bypass scenarios (corrected — Phase 55)", explicitly marks the old "SuperAdmin sees all groups" expectation as superseded/the confirmed root cause, and documents the corrected fail-closed behavior. "Status: Closed" |
| T-55-07 | Tampering / Elevation of Privilege | mitigate | `QuestBoard.Service/Controllers/GroupPickerController.cs:44-61` — `SelectGroup` calls `userService.GetGroupRoleByIdAsync(userId, groupId)` for non-SuperAdmin callers and returns `NotFound()` when `role == null`, before any session write. Constructor now `GroupPickerController(IGroupService groupService, IUserService userService)`. Regression proof: `GroupPickerControllerIntegrationTests.cs::SelectGroup_WhenNotAMember_ShouldReturnNotFound` (seeds a group with no `UserGroupEntity` row, asserts 404) passes |
| T-55-08 | Information Disclosure | mitigate | Same code location (`GroupPickerController.cs:54`) — the non-membership branch returns `NotFound()` (404), never `Forbid()`/403, matching the existing exists-vs-doesn't-exist check's own `NotFound()` return one line above (line 47), so a probe cannot distinguish the two cases. Test assertion confirms `HttpStatusCode.NotFound` exactly |
| T-55-09 | Elevation of Privilege | accept | Entry present below in Accepted Risks Log. Code confirms the intended bypass is real, not a gap: `GroupPickerController.cs:49-55` — `isSuperAdmin` is checked first and the membership block is skipped entirely for SuperAdmin, consistent with `Index`'s own `isSuperAdmin ? GetAllWithMemberCountAsync() : GetGroupsForUserAsync(userId)` any-group listing (line 22-24) |
| T-55-10 | Elevation of Privilege | mitigate | `GroupSessionMiddleware.cs:109-142` — interval-gated re-validation block reads `SessionKeys.ActiveGroupValidatedAtUtc`, and when stale/missing calls `GetGroupRoleByIdAsync`; on `role == null` clears `ActiveGroupId`/`ActiveGroupName`/`ActiveGroupValidatedAtUtc` from session and reuses the same GET/HEAD-redirect vs POST-409 branch. Regression proof: `QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs::RemovedMember_StaleTimestamp_Get_RedirectsToGroupPickAndClearsSession` and `RemovedMember_StaleTimestamp_Post_ReturnsConflict` both pass (5/5 in this file) |
| T-55-11 | Tampering | mitigate | `GroupSessionMiddleware.cs:109-113` — `needsRevalidation` is `true` when `validatedAtRaw == null` OR `DateTime.TryParse(...)` fails OR the elapsed time exceeds the interval — an OR chain that fails toward re-checking, never toward skipping. No code path sets `needsRevalidation = false` on a parse failure. Test proof: the revalidation suite has no test asserting a corrupted timestamp skips the check (by construction, `FakeSession` only ever seeds a valid ISO-8601 string or null — the parse-failure branch is exercised by the OR's logical structure, confirmed by code read, not a dedicated corrupted-string unit test — see Note below) |
| T-55-12 | Denial of Service | accept | Entry present below in Accepted Risks Log. Code confirms the interval gate is real: `GroupSessionMiddleware.cs:75` `MembershipRevalidationInterval = TimeSpan.FromMinutes(5)`, and `FreshTimestamp_WithinInterval_InvokesNextWithoutMembershipCheck` proves `GetGroupRoleByIdAsync` is NOT called within the window (`DidNotReceive()` assertion) |
| T-55-SC (Plan 01) | Tampering | accept | Entry present below in Accepted Risks Log. Verified: `git diff main..HEAD --stat -- "*.csproj"` returns no output — no `.csproj` changes on this branch versus `main` |
| T-55-SC (Plan 02) | Tampering | accept | Same verification as above — no package manifest changes |
| T-55-SC (Plan 03) | Tampering | accept | Same verification as above — no package manifest changes |
| T-55-SC (Plan 04) | Tampering | accept | Same verification as above — no package manifest changes |

**Closed: 16/16** (10 mitigate + 6 accept, all verified — 0 open)

### Note on T-55-11 verification depth

The mitigation code is unambiguous by inspection (fail-toward-recheck OR chain, no path sets `needsRevalidation = false` on parse failure), and this satisfies "mitigation present in code." However, the regression suite does not include a dedicated `[Fact]` seeding a syntactically-corrupted (non-parseable) `ActiveGroupValidatedAtUtc` string and asserting re-validation is triggered — only the `null` case and valid-but-stale/valid-but-fresh cases are covered. This is a test-coverage thinness, not an open mitigation gap (the code path exists and is logically sound), so it does not block SECURED status, but is flagged for the next test-hardening pass.

---

## Unregistered Flags

None. No `## Threat Flags` section was present in any of the 4 SUMMARY.md files (`55-01-SUMMARY.md`, `55-02-SUMMARY.md`, `55-03-SUMMARY.md`, `55-04-SUMMARY.md`) — the executor did not report any new attack surface discovered during implementation.

Independent observation made during this audit (not a new threat — informational only): Plan 04 and Plan 02's summaries both document deviation fixes to test infrastructure (`AuthenticationHelper.CreateAuthenticatedClientWithUserAsync` now seeds `UserGroups` by default; `TenantIsolationTests.GroupFilter_NonExistentGroup_ReturnsEmpty` switched to a SuperAdmin client). These are test-only changes surfaced by the new correctness checks, not production attack surface, and are already covered by T-55-10/T-55-11's evidence trail above.

---

## Accepted Risks Log

### T-55-09 — SuperAdmin bypasses `GroupPickerController.SelectGroup` membership check
**Category:** Elevation of Privilege
**Disposition:** Accept
**Rationale (from 55-03-PLAN.md):** SuperAdmin legitimately manages all groups across the platform. The membership check is intentionally bypassed for SuperAdmin in `SelectGroup`, matching the pre-existing pattern in `Index` (`GetAllWithMemberCountAsync()` lists every group for SuperAdmin, `GetGroupsForUserAsync(userId)` for everyone else). This is a documented, intentional widening of scope for a single trusted platform-admin role, not a gap.
**Verified in code:** `GroupPickerController.cs:49-55`.

### T-55-12 — Membership re-validation DB round-trip under load
**Category:** Denial of Service
**Disposition:** Accept
**Rationale (from 55-04-PLAN.md):** The re-check is interval-gated to once per 5 minutes per active user (`MembershipRevalidationInterval = TimeSpan.FromMinutes(5)`), not per-request. This is the same tradeoff already accepted elsewhere in the app for `SecurityStampValidatorOptions.ValidationInterval`. At this application's scale (self-hosted, single-tenant-group-per-session, small user base per PROJECT.md), the additional DB round-trip once per 5 minutes per active session is negligible load.
**Verified in code:** `GroupSessionMiddleware.cs:75` (interval constant) and `FreshTimestamp_WithinInterval_InvokesNextWithoutMembershipCheck` unit test (proves no DB call within the window).

### T-55-SC — No package installs (all 4 plans)
**Category:** Tampering (supply chain)
**Disposition:** Accept
**Rationale:** Each plan's RESEARCH.md Package Legitimacy Audit recorded zero packages evaluated because no new dependency was introduced — all APIs used ship in already-referenced packages (EF Core 10.0.9, ASP.NET Core shared framework, existing `IUserService`/BCL primitives).
**Verified:** `git diff main..HEAD --stat -- "*.csproj"` shows no `.csproj` changes on this branch relative to `main`.

---

## Verification Commands Run

```
dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~QuestBoardContextFilterTests|FullyQualifiedName~GroupSessionMiddlewareRevalidationTests"
  -> Passed: 13, Failed: 0

dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~GroupSessionMiddlewareIntegrationTests|FullyQualifiedName~GroupPickerControllerIntegrationTests|FullyQualifiedName~TenantIsolationTests"
  -> Passed: 25, Failed: 0

grep -c 'ActiveGroupId != null &&' QuestBoard.Repository/Entities/QuestBoardContext.cs -> 10
grep -c 'ActiveGroupId == null ||' QuestBoard.Repository/Entities/QuestBoardContext.cs -> 0
grep -c 'IsInRole("SuperAdmin")' QuestBoard.Service/Middleware/GroupSessionMiddleware.cs -> 1 (re-validation exclusion only, not a gate bypass)

git log --oneline --all | grep -E "8524d34|f7d270f|fc30bfd|6fcd099|3ccc2f4|9e870a6|dcf51ea|b892e63|5424a7e|f23d309|11b774e"
  -> all 11 claimed commits present

git diff main..HEAD --stat -- "*.csproj" -> (no output; no package changes)
```

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-07-06 (via gsd-security-auditor SECURED)

---
*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Audited: 2026-07-06*
