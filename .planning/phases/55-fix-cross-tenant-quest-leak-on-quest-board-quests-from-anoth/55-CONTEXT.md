# Phase 55: Fix cross-tenant quest leak on quest board - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Close the confirmed root cause of a SuperAdmin seeing all groups' quest-board data merged together with no indication it wasn't a single tenant's board, plus a separately-discovered, related authorization gap in group selection. Two confirmed issues, one theme (a caller's authorization must be validated at the moment data is served, not assumed from a prior state):

1. **`GroupSessionMiddleware` exempts SuperAdmin from the "must have an active group" gate everywhere**, not just in genuinely group-agnostic areas (Platform, GroupPicker, Account, Error). This means a SuperAdmin with a null `ActiveGroupId` (e.g. a fresh session on a new device/browser, since session data — including `ActiveGroupId` — is per-device and doesn't sync) can reach `/quests` and every other group-scoped board. On those pages, `QuestEntity`/`ShopItemEntity`/`ProposedDateEntity`/`PlayerDateVoteEntity`/`PlayerSignupEntity`'s shared EF Core global query filter shape — `ActiveGroupId == null || e.GroupId == ActiveGroupId` — evaluates true for every row when `ActiveGroupId` is null, so the SuperAdmin sees every group's data merged into one unlabeled list. This is what the user experienced and reported as "tenant 2 bled into tenant 1."
2. **`GroupPickerController.SelectGroup`** validates that a posted `groupId` exists, but never validates that the authenticated caller is actually a member of that group — found during investigation, confirmed via code read, not the cause of this specific incident but the same root-cause pattern (authorization checks validating the caller's role/authentication but not the specific target) that Phase 49 already fixed twice elsewhere in this codebase.

</domain>

<decisions>
## Implementation Decisions

### Root cause — investigated and ruled out first
- **D-00 [informational]:** The user's original hypothesis (session/`AspNetSessionState` row expiration causing a regular user to see another tenant's data) was investigated and ruled out as the mechanism. `GroupSessionMiddleware` already intercepts every request for a non-SuperAdmin user with a null `ActiveGroupId` — GET/HEAD redirects to `/groups/pick`, POST/PUT/PATCH/DELETE return 409 Conflict. There is no code path today where a regular (non-SuperAdmin) user reaches a controller action with a null `ActiveGroupId`. The actual incident required a SuperAdmin account (confirmed with the user) landing on a new device with no `ActiveGroupId` ever set, which is the one role the middleware explicitly bypasses.

### Fix 1 — SuperAdmin must pick a group before seeing any group-scoped board
- **D-01:** Extend `GroupSessionMiddleware`'s "must have an active group" gate to also apply to SuperAdmin, for **every group-scoped route** (broad fix — not just `/quests`). User's explicit framing: *"a super admin can access everything, but should do so as a normal user... the content should be displayed as if a normal user views the site... this way there cannot be any confusion about what a user sees vs what a super admin sees."* SuperAdmin gets redirected to `/groups/pick` exactly like every other role. Confirmed via grep that no production feature depends on the current "null ActiveGroupId = see everything" behavior — the only real production `IgnoreQueryFilters()` call site is the unrelated Hangfire `DailyReminderJob` cross-group sweep (`QuestRepository.GetQuestsForTomorrowAllGroupsAsync`), which is unaffected by this change.
- **D-02 (kept exempt):** `GroupSessionMiddleware`'s existing exempt-path-prefixes (`/platform`, `/Error`) and the GroupPicker/Account paths stay exempt — SuperAdmin's cross-group platform management (adding/removing groups, managing members) must continue to work without first picking a specific group. Only genuinely group-scoped board pages (quest board, shop, guild members, quest log, calendar, etc.) get the new gate.

### Fix 2 — Filter hardening (defense-in-depth, on top of Fix 1)
- **D-03:** Harden `QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`, `PlayerDateVoteEntity`, and `PlayerSignupEntity`'s `HasQueryFilter` in `QuestBoardContext.cs` to drop the `ActiveGroupId == null ||` escape hatch entirely — matching `CharacterEntity`'s existing fail-closed shape from Phase 49 (D-03 there: `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`, no null-passthrough). User confirmed this on top of the middleware fix: *"matches this codebase's own established lesson (Phase 49) that relying on one layer alone has repeatedly proven fragile."* If a future code change ever bypasses the middleware gate, the filter itself must show zero rows for a null `ActiveGroupId`, not every group's rows.
  - Confirmed safe: `DailyReminderJob`'s cross-group sweep uses `.IgnoreQueryFilters()`, which bypasses `HasQueryFilter` entirely regardless of the filter's predicate shape — unaffected by this change.

### Fix 3 — GroupPickerController.SelectGroup membership check (folded in from investigation)
- **D-04:** Add a membership check to `SelectGroup` — verify the authenticated caller is actually a member of the posted `groupId` (e.g. via the existing `IUserService.GetGroupRoleByIdAsync(userId, groupId)` primitive already established in Phase 49 for the identical purpose) before setting it as the session's `ActiveGroupId`. Currently it only checks the group exists (`GetByIdAsync(groupId)` → `NotFound()` if null), not membership.
- **D-05:** When `SelectGroup` is posted with a `groupId` the caller isn't a member of, return **404 Not Found** — matching this project's established cross-tenant-response convention (Phase 49 D-04/D-09/D-13: hide existence rather than confirm it with 403).

### Fix 4 — Defense-in-depth: stale membership re-validation
- **D-06:** Add periodic re-validation that an already-active session's `ActiveGroupId` membership is still current — closes the residual gap where a user is removed from a group by an admin mid-session but keeps access until their session naturally re-selects a group (their `ActiveGroupId` stays non-null and matches a real group, so D-03's hardened filter alone doesn't catch this — it only checks "does this ID match," not "is this still a valid membership"). Locked as in-scope per the user's explicit "as if a normal user, no confusion" philosophy — this is the same class of correctness gap as D-01/D-03, just on the time axis instead of the initial-access axis.
  - **Mechanism is Claude's Discretion, to be resolved during research:** investigate piggybacking on the existing `SecurityStampValidatorOptions.ValidationInterval` (already shortened to 5 minutes app-wide in Phase 41, specifically to force fast re-validation of a revoked/disabled account — the same "close a stale-session privilege gap quickly" problem, already solved once in this codebase) versus a bespoke periodic check in `GroupSessionMiddleware`/`ActiveGroupContextService`. Prefer reusing the existing mechanism over inventing a second one, consistent with this project's stated preference (see Phase 49 D-11's "document + test over introducing a second, redundant mechanism").

### Claude's Discretion
- Exact hook point and code shape for D-06's periodic re-validation (see above).
- Whether `GroupPickerController.Index`'s own group-listing logic already correctly scopes a regular user's selectable list to their own memberships (it should, given D-04 makes any mismatch non-exploitable regardless) — verify during research, not a blocking decision either way.
- Whether any additional group-scoped controllers beyond Quest/Shop/vote/PlayerSignup need identical filter treatment — research should grep for any other entity sharing the `ActiveGroupId == null ||` shape and apply D-03's treatment uniformly if found.

### Investigated and ruled out — alternate theories
- **D-07 [informational]:** Two alternate theories were raised during investigation and explicitly not pursued, since the user's own account (SuperAdmin, new device, no group ever picked) fully explains the reported symptom without them: (1) reverse-proxy/CDN response caching serving one client's session cookie to another — no `ResponseCaching`/`OutputCache` middleware exists in `Program.cs`, and this app's own Traefik reverse-proxy config is outside this codebase's visibility; (2) session fixation on login (no confirmed `AccountController` login-flow session-ID rotation check was performed). Neither is in scope for this phase. If a similar leak is ever reported from a **non-SuperAdmin** account, re-open these two leads — they would rule back in.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The confirmed root cause (SuperAdmin escape hatch)
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — SuperAdmin bypass check (~line 65) happens before the null-`ActiveGroupId` gate (~lines 78-95); this is the exemption to narrow per D-01/D-02. Exempt-path-prefixes list (~lines 41-48: `/platform`, `/Error`) stays as-is.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — filter definitions to hardened per D-03: `QuestEntity` (~lines 251-254), `ShopItemEntity` (~lines 256-259), `ProposedDateEntity` (~lines 267-269), `PlayerDateVoteEntity` (~lines 275-277), `PlayerSignupEntity` (~lines 284-286). All currently share the `activeGroupContext.ActiveGroupId == null || e.GroupId == activeGroupContext.ActiveGroupId` shape.
- `QuestBoard.Repository/Entities/CharacterEntity.cs` filter (Phase 49 D-03, same file, `QuestBoardContext.cs`) — the exact fail-closed shape to replicate: `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`.
- `.planning/codebase/CONCERNS.md` lines 288-292, 307-310 — the previously-documented rationale for "SuperAdmin sees all on /quests" being treated as intentional; this phase's decisions (D-01, D-03) supersede that rationale. Update/correct this doc during implementation so it doesn't mislead a future reader.
- `.planning/phases/49-fix-guild-members-page-missing-group-tenant-filtering/49-CONTEXT.md` — direct precedent for D-03's filter shape, D-04/D-05's 404 convention, and D-04's membership-check primitive.

### Fix 3 — SelectGroup membership check
- `QuestBoard.Service/Controllers/.../GroupPickerController.cs` — `SelectGroup` (POST) action to add the membership check to; `Index` (GET) to verify (Claude's Discretion item above) already scopes its listed groups to the caller's own memberships for non-SuperAdmin callers.
- `QuestBoard.Domain/Interfaces/IUserService.cs` — `GetGroupRoleByIdAsync(int userId, int groupId)` (returns null if not a member) — the existing primitive to reuse, per Phase 49 D-07's identical usage on `DungeonMasterController`.

### Fix 4 — periodic re-validation precedent
- `PROJECT.md` Key Decisions table — `SecurityStampValidatorOptions.ValidationInterval` shortened to 5 minutes app-wide (Phase 41), the existing "force fast re-validation of a revoked/stale session state" mechanism to investigate reusing for D-06.

No external ADRs/specs beyond the codebase references above — requirements are fully captured in the decisions section.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IUserService.GetGroupRoleByIdAsync(int userId, int groupId)` — already DI-registered and used identically in Phase 49 for target-membership checks; reuse directly for D-04, no new plumbing needed.
- `CharacterEntity`'s fail-closed `HasQueryFilter` shape (Phase 49 D-03) — copy-paste template for D-03's five entity filter changes.
- `SecurityStampValidatorOptions.ValidationInterval` (Phase 41) — existing periodic re-validation mechanism to investigate extending for D-06 rather than building a new one.

### Established Patterns
- Cross-tenant access attempts return 404 Not Found, never 403 Forbidden — consistent convention across Phase 49 (D-04, D-09, D-13) and this phase's D-05.
- "Reached only through an already-filtered navigation" / null-passthrough filter assumptions have twice now been found fragile in this codebase (Phase 49's D-10/D-12 investigation) — D-03 applies the same "verify empirically, prefer fail-closed schema-level filters over incidental protection" lesson to a third case (the SuperAdmin escape hatch itself, which is a deliberate null-passthrough, not an accident, but carries the identical fail-open risk).

### Integration Points
- `GroupSessionMiddleware`'s SuperAdmin-bypass-before-null-check ordering must be restructured so the SuperAdmin check no longer short-circuits past the group-required gate for board routes, while the `/platform`/`/Error`/GroupPicker/Account exemptions continue to short-circuit correctly for SuperAdmin's actual group-agnostic workflows.
- `DailyReminderJob` → `QuestRepository.GetQuestsForTomorrowAllGroupsAsync()` — the one legitimate production `IgnoreQueryFilters()` call site; confirmed unaffected by D-03's filter hardening since `IgnoreQueryFilters()` bypasses `HasQueryFilter` regardless of the filter's predicate shape.

</code_context>

<specifics>
## Specific Ideas

User's original report: "quests from tenant 2 bleed into the questboard from tenant 1... this should not happen!! never should a quest from another tenant be seen at the currently viewed tenant," with a hypothesis that `AspNetSessionState` expiration was the cause.

Investigation ruled out the session-expiration-for-a-regular-user theory (middleware already blocks that path) and instead traced the actual mechanism through the user's own follow-up account: they opened the app on a different computer a day later, using a **SuperAdmin** account, and saw "all quests on the same page" — the signature of the `ActiveGroupId == null` escape hatch on the Quest/ShopItem/vote filters, reachable only because `GroupSessionMiddleware` deliberately exempts SuperAdmin from the group-required gate everywhere, not just in group-agnostic areas.

The user's closing statement set the governing design principle for this phase: *"a super admin can access everything, but should do so as a normal user... the content should be displayed as if a normal user views the site... this way there cannot be any confusion about what a user sees vs what a super admin sees."* This single principle drove D-01 (broad middleware fix, not just `/quests`), D-03 (fail-closed filter hardening), and D-06 (closing the mid-session membership-revocation gap) — all three are different facets of "SuperAdmin's board-viewing experience must be structurally identical to a normal user's, not merely visually similar."

</specifics>

<deferred>
## Deferred Ideas

None — all four fixes (D-01/D-02 middleware, D-03 filter hardening, D-04/D-05 SelectGroup membership check, D-06 periodic re-validation) are in scope for this phase, following the same "fold in adjacent confirmed issues" precedent Phase 49 established. The two alternate root-cause theories (reverse-proxy cookie caching, session fixation on login) were investigated, not confirmed as relevant here, and explicitly not pursued — see D-07.

</deferred>

---

*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Context gathered: 2026-07-06*
