# Phase 72: Platform Settings + Token Contract - Context

**Gathered:** 2026-07-02
**Status:** Ready for planning
**Re-validated:** 2026-07-08 — after the v7.0 merge into this branch. All decisions (D-01–D-06) confirmed unchanged against current `PROJECT.md`/`REQUIREMENTS.md`; `code_context` below updated with pointers that shifted or improved because of code the merge brought in.
**Revised:** 2026-07-11 — settings storage redesigned from a fixed-column singleton row to a generic key-value table with per-group override support (D-07–D-10). Supersedes SETT-06/07/08 as originally worded; REQUIREMENTS.md and PROJECT.md updated to match. **This changes the schema/service work planned in 72-01 and 72-02 — those plans need regenerating**, and 72-UI-SPEC.md (instance-wide page only) needs a pass to add the new group-override page.

<domain>
## Phase Boundary

Two settings surfaces for the Omphalos integration, both backed by a shared generic key-value `PlatformSettingEntity` and an EF Core migration:
1. SuperAdmin-only instance-wide default (URL, shared secret, enabled toggle) under `/platform` — the original scope.
2. **New:** Group Admin-only per-group override (same three fields), living alongside the existing group-scoped `AdminController` surface. A group with no override falls back to the instance-wide default.

Plus the written HMAC token-format contract that Phases 73 (Quest Board) and 74 (Omphalos) will both implement against. This phase writes the contract down; it does not implement token generation or the SSO endpoint themselves (those are Phase 73/74). Phase 73's token generator will need to resolve settings by the quest's own `GroupId` using the cascade (group override → instance default) — noted here so Phase 73's discussion doesn't have to rediscover it.

</domain>

<decisions>
## Implementation Decisions

### Token Contract Format
- **D-01:** The canonical signed message includes an explicit unique token ID (nonce/jti), not just the payload fields already required by REQUIREMENTS.md (TOKEN-01/03/04/06). Phase 74's replay-protection (SSO-04) keys its used-token tracking on this nonce, not a hash of the full token.
- **D-02 (Claude's discretion):** Canonical message structure is JSON, HMAC-SHA256 signed over the raw JSON bytes, then Base64URL-encoded (payload + signature) for the URL — reusing Quest Board's existing `WebEncoders.Base64UrlEncode` convention already used for password-reset/email-change tokens. Chosen specifically to avoid the delimiter field-shifting collision research flagged as the single highest-risk mistake (e.g., a quest title containing a delimiter character shifting every subsequent field in a fixed-order concatenated string). Full field list: `nonce`, `userId` (Quest Board `UserEntity.Id`), `questId`, `questTitle`, `questDate`, `expiry`.

### Shared Secret Input UX
- **D-03:** The settings page has a **"Generate Secret" button** — server generates a cryptographically random value, shown once in the masked field. SuperAdmin manually copies it into Omphalos's `.env` (e.g. `Sso:Secret`, distinct from Omphalos's own `Jwt:Secret`). No automatic sync between the two apps.
- **D-04 [informational]:** No REST API or other server-to-server channel between Quest Board and Omphalos for syncing this secret (or anything else) is in scope. This was raised by the user during discussion and redirected — see Deferred Ideas. Nothing to build for this decision — it is satisfied by the absence of such a channel, not by a plan task.

### Settings Page Naming & Placement
- **D-05:** The page is a generic **"Integrations"** page under `/platform` (not "Omphalos Integration" by name) — framed as a settings category that could hold future integrations, even though only Omphalos exists today. Exact route, icon, and copy are left to planning/implementation, following the existing `Areas/Platform/Controllers/GroupController.cs` pattern (own nav item, own controller/view under the Platform area).

### Cross-Repo Contract Delivery
- **D-06:** The written token contract stays in Quest Board's `.planning/` (this phase's context/a dedicated contract doc) — it is **not** committed as a new file into the Omphalos repo. Phase 74's PR description references it directly. Omphalos has no `.planning/`/docs convention today (confirmed: only `README.md`/`CLAUDE.md` at its repo root), so introducing one just for this contract was decided against in favor of pointing to the existing doc from the PR.

### Settings Storage Schema (supersedes original SETT-06/07/08 wording)
- **D-07:** Settings are persisted in a **generic key-value table** — `PlatformSettingEntity { Id, Key (string), Value (string), GroupId (int?, nullable FK → GroupEntity) }` — not the originally-researched fixed-column singleton row (`IntegrationSettingEntity { OmphalosUrl, OmphalosSharedSecret, IsEnabled }`). Rejected explicitly for being schema-rigid: every future setting or integration would need a new column + migration. This is a new pattern in this codebase (no prior key-value settings precedent existed before this decision — confirmed via codebase scout).
- **D-08:** `GroupId = null` is the **instance-wide default** row(s); `GroupId = <group>` is that **group's override**. Lookup is a cascade: check for a group-specific row first, fall back to the instance-wide default (`GroupId = null`) when no override exists for that key/group. This single mechanism naturally covers all three settings (`OmphalosUrl`, `OmphalosSharedSecret`, `IsEnabled`) without a separate per-field design — a group can override just the URL while still inheriting the instance-wide default's enabled flag, for example, though the UI (D-09) is expected to treat the three as one unit per scope rather than exposing field-by-field overrides.
- **D-09:** Two separate settings pages, not one shared form:
  1. Instance-wide default — SuperAdmin-only, under `/platform` (unchanged placement/naming from D-05 above).
  2. **New:** Group override — **Group Admin only** (`GroupRole.Admin`; explicitly **not** DungeonMaster), placed alongside the existing group-scoped `AdminController` surface (`QuestBoard.Service/Controllers/Admin/AdminController.cs`), not under `/platform`. Each page independently manages its own row(s) via the same underlying `Key`/`Value`/`GroupId` cascade.
- **D-10 [informational]:** Duplicate values across groups are fine and expected — e.g. two groups both pointing at the same shared Omphalos instance with the same secret is a valid, unremarkable configuration. No uniqueness constraint on `Value`; only `(Key, GroupId)` needs to be unique (one row per setting per scope).

### Shared Secret Input UX (applies to both pages from D-09)
- **D-03/D-09 interaction:** The "Generate Secret" button (D-03) applies independently on both the instance-wide and group-override pages — each generates and persists a value scoped to its own row(s) (`GroupId = null` vs `GroupId = <group>`), with no cross-influence between them.

### Claude's Discretion
- Exact canonical-message serialization details beyond the field list and JSON+Base64URL approach (D-02) — e.g. exact JSON property casing, exact nonce generation method (`RandomNumberGenerator`/GUID) — left to research/planning.
- Exact route path, icon (FontAwesome, per CLAUDE.md UI guidelines), and page copy for the "Integrations" page (D-05) — left to planning/UI phase.
- Exact `PlatformSettingEntity` uniqueness enforcement mechanism (filtered unique index vs. app-level check) for the `(Key, GroupId)` constraint, and the exact repository/service method shape for the cascade lookup (D-08) — left to research/planning.
- Whether the group-override page's route lives under the existing `AdminController` itself (new action) or a new sibling controller in `Controllers/Admin/` — left to planning, following whichever keeps `AdminController` from growing unreasonably large (planner's call, consistent with how `UsersController` was split out from `GroupController` in the Platform area for the same reason).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` — SETT-01 through SETT-10 (locked requirements for this phase, **SETT-06/07/08 revised and SETT-09/10 added 2026-07-11** — read the current file, not the original research's assumptions); also TOKEN-01/02/03/04/06 (token contract fields this phase must write down, even though the code implementing them lands in Phase 73)
- `.planning/ROADMAP.md` — Phase 72 section (goal, success criteria, dependencies)
- `.planning/PROJECT.md` — Key Decisions table: settings now support instance-wide default + per-group override (**revised 2026-07-11**, was "not per-group"), HMAC-signed token behind swappable `IIntegrationTokenService`/`SsoService` seam (not OAuth2), identity key = `UserEntity.Id`, replay protection in scope

### Research (⚠ predates D-07–D-10 — architecture section is superseded)
- `.planning/research/SUMMARY.md` — full milestone research: recommended stack (BCL `HMACSHA256`, `WebEncoders.Base64UrlEncode`, no new packages) is still valid; **the settings-architecture recommendation (`GroupEntity`-shape singleton-row `IntegrationSettingEntity`) is superseded by D-07/D-08 — do not follow it for the entity/repository/service shape.** Identity-matching resolution and cross-repo pitfalls sections are unaffected and still apply.

### Cross-Repo Context (Omphalos)
- `C:\Repos\omphalos\CLAUDE.md` — Omphalos architecture/conventions reference; confirms no `.planning/`/docs directory exists there today (informs D-06)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` — added by the v7.0 merge (2026-07-08); a lean SuperAdmin-only Platform-area controller (~2K, no member-management CRUD) that's a closer-scoped template for the new Integrations controller than `GroupController.cs`
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — still valid for the general Platform-area/`SuperAdminOnly` shape, but prefer `UsersController.cs` as the template given its closer scope
- `WebEncoders.Base64UrlEncode`/`Decode` (`Microsoft.AspNetCore.WebUtilities`) — used throughout `AdminController.cs` (password-reset and email-change-confirmation flows); reuse verbatim for the token contract's encoding (D-02)

### Established Patterns
- Blank-preserves-existing-value guard relevant to SETT-04: the closest current analog is `ContactsController.cs:238` / `CharactersController.cs:308` ("otherwise remains unchanged" — blank upload input keeps the prior value), both added by the v6.0/v7.0 merge. `AdminController.EditUser` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:224`, line shifted from the original :167 reference) handles email-change logic, not a masked-field blank-preserve — it is **not** the pattern to copy for SETT-04.
- `SuperAdminOnly` policy (`policy.RequireRole("SuperAdmin")`) — apply directly to the instance-wide Integrations controller (SETT-07); see `UsersController.cs`/`GroupController.cs` for usage
- Group-scoped `Admin` authorization for the new override page (D-09/SETT-09/SETT-10) — `AdminController.cs` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:21`) is `[Authorize(Policy = "AdminOnly")]` and separately resolves per-group role via `IActiveGroupContext.ActiveGroupId` + `userService.GetGroupRoleByIdAsync(userId, groupId)` (see `AdminController.cs:26-34`, the `Users` action). The new group-override action needs the equivalent `GroupRole.Admin` check, not just the platform-wide `AdminOnly` policy alone — confirm the exact existing check used to gate write actions (not just read/list) in `AdminController` during research/planning.
- Modern card UI pattern (`modern-card`/`modern-card-header`/`modern-card-body`, per root `CLAUDE.md`) — apply to both the instance-wide and group-override pages.

### Integration Points
- There is no shared per-page nav list in `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` (desktop) / `_Layout.Platform.Mobile.cshtml` (mobile) — confirmed by inspecting both after the merge. Cross-linking between Platform pages is done via a header button on the source page instead: `UsersController`'s addition wired in a `<a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">` button on `Group/Index.cshtml` and `Index.Mobile.cshtml`. Follow the same pattern for the instance-wide Integrations page — add a header button to `Group/Index.cshtml`/`Index.Mobile.cshtml`, not a shared-layout nav item.
- New `PlatformSettingEntity` + repository + domain service — generic key-value shape (D-07/D-08), no existing entity to extend. The cascade lookup (group override → instance default) is new logic with no direct precedent in this codebase; closest conceptual analog is the tenant `HasQueryFilter` pattern (`QuestBoardContext.cs:280-373`) in that both resolve "what applies to the active group," but the mechanism is different (application-level fallback query, not a DB query filter) since `GroupId = null` rows must remain visible regardless of active group.
- The group-override page needs its own entry point in the group's admin UI — no existing "group settings" page/nav item exists yet to extend; planner should decide where it's linked from within `AdminController`'s existing views.

</code_context>

<specifics>
## Specific Ideas

- Nonce/jti field added to the token contract specifically so Phase 74's replay-protection has a purpose-built key to track, rather than hashing the whole token.
- "Generate Secret" button UX explicitly chosen over both plain manual paste and an auto-sync API, after discussing the security tradeoffs of secret bootstrapping between two apps that don't otherwise call each other.
- User explicitly rejected the researched fixed-column singleton-row design ("not a fan of a single row with columns as settings... not future proof and a bit of an ugly design") in favor of a generic key-value table — this was the trigger for the entire D-07–D-10 redesign. Worth noting `PROJECT.md`'s original milestone wording had actually said "key-value store" from the start; the fixed-column design was a divergence introduced during research/planning that wasn't flagged as a deviation at the time.
- Per-group override scenario clarified through discussion: user wants "if two groups point to the same Omphalos instance, it should still work" with "a single shared secret" being fine — i.e., no uniqueness constraint needed on secret values across groups (D-10), and Omphalos itself never needs multi-tenant awareness since each deployment only verifies its own configured secret regardless of how many Quest Board groups happen to point at it.

</specifics>

<deferred>
## Deferred Ideas

- **Server-to-server API between Quest Board and Omphalos** (raised during the Shared Secret UX discussion, as a way to auto-sync the shared secret or otherwise let the two apps talk to each other) — this is already covered by requirement `BIDI-01` ("Foundation laid for a future bidirectional API — no implementation now") and the milestone's Key Decisions log (`.planning/PROJECT.md`). Not a new deferred item requiring roadmap action; redirected during discussion to the existing scope decision. If it resurfaces, it belongs in the same future milestone as `BIDI-01`/`IDP-01`, not this one.

</deferred>

---

*Phase: 72-platform-settings-token-contract*
*Context gathered: 2026-07-02*
*Re-validated post-v7.0-merge: 2026-07-08*
*Settings storage redesigned: 2026-07-11*
