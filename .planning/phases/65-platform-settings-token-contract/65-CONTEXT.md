# Phase 65: Platform Settings + Token Contract - Context

**Gathered:** 2026-07-02
**Status:** Ready for planning
**Re-validated:** 2026-07-08 — after the v7.0 merge into this branch. All decisions (D-01–D-06) confirmed unchanged against current `PROJECT.md`/`REQUIREMENTS.md`; `code_context` below updated with pointers that shifted or improved because of code the merge brought in.

<domain>
## Phase Boundary

SuperAdmin-only Platform Settings page for the Omphalos integration (URL, shared secret, enabled toggle), backed by a single-row `IntegrationSettingEntity` and an EF Core migration — plus the written HMAC token-format contract that Phases 66 (Quest Board) and 67 (Omphalos) will both implement against. This phase writes the contract down; it does not implement token generation or the SSO endpoint themselves (those are Phase 66/67).

</domain>

<decisions>
## Implementation Decisions

### Token Contract Format
- **D-01:** The canonical signed message includes an explicit unique token ID (nonce/jti), not just the payload fields already required by REQUIREMENTS.md (TOKEN-01/03/04/06). Phase 67's replay-protection (SSO-04) keys its used-token tracking on this nonce, not a hash of the full token.
- **D-02 (Claude's discretion):** Canonical message structure is JSON, HMAC-SHA256 signed over the raw JSON bytes, then Base64URL-encoded (payload + signature) for the URL — reusing Quest Board's existing `WebEncoders.Base64UrlEncode` convention already used for password-reset/email-change tokens. Chosen specifically to avoid the delimiter field-shifting collision research flagged as the single highest-risk mistake (e.g., a quest title containing a delimiter character shifting every subsequent field in a fixed-order concatenated string). Full field list: `nonce`, `userId` (Quest Board `UserEntity.Id`), `questId`, `questTitle`, `questDate`, `expiry`.

### Shared Secret Input UX
- **D-03:** The settings page has a **"Generate Secret" button** — server generates a cryptographically random value, shown once in the masked field. SuperAdmin manually copies it into Omphalos's `.env` (e.g. `Sso:Secret`, distinct from Omphalos's own `Jwt:Secret`). No automatic sync between the two apps.
- **D-04 [informational]:** No REST API or other server-to-server channel between Quest Board and Omphalos for syncing this secret (or anything else) is in scope. This was raised by the user during discussion and redirected — see Deferred Ideas. Nothing to build for this decision — it is satisfied by the absence of such a channel, not by a plan task.

### Settings Page Naming & Placement
- **D-05:** The page is a generic **"Integrations"** page under `/platform` (not "Omphalos Integration" by name) — framed as a settings category that could hold future integrations, even though only Omphalos exists today. Exact route, icon, and copy are left to planning/implementation, following the existing `Areas/Platform/Controllers/GroupController.cs` pattern (own nav item, own controller/view under the Platform area).

### Cross-Repo Contract Delivery
- **D-06:** The written token contract stays in Quest Board's `.planning/` (this phase's context/a dedicated contract doc) — it is **not** committed as a new file into the Omphalos repo. Phase 67's PR description references it directly. Omphalos has no `.planning/`/docs convention today (confirmed: only `README.md`/`CLAUDE.md` at its repo root), so introducing one just for this contract was decided against in favor of pointing to the existing doc from the PR.

### Claude's Discretion
- Exact canonical-message serialization details beyond the field list and JSON+Base64URL approach (D-02) — e.g. exact JSON property casing, exact nonce generation method (`RandomNumberGenerator`/GUID) — left to research/planning.
- Exact route path, icon (FontAwesome, per CLAUDE.md UI guidelines), and page copy for the "Integrations" page (D-05) — left to planning/UI phase.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` — SETT-01 through SETT-08 (locked requirements for this phase); also TOKEN-01/02/03/04/06 (token contract fields this phase must write down, even though the code implementing them lands in Phase 66)
- `.planning/ROADMAP.md` — Phase 65 section (goal, success criteria, dependencies)
- `.planning/PROJECT.md` — Key Decisions table: instance-wide settings (not per-group), HMAC-signed token behind swappable `IIntegrationTokenService`/`SsoService` seam (not OAuth2), identity key = `UserEntity.Id`, replay protection in scope

### Research
- `.planning/research/SUMMARY.md` — full milestone research: recommended stack (BCL `HMACSHA256`, `WebEncoders.Base64UrlEncode`, no new packages), architecture approach (`GroupEntity`-shape for `IntegrationSettingEntity`), identity-matching resolution, critical pitfalls (HMAC canonical-message format, secret blank-overwrite guard, cross-repo PR friction)

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
- `SuperAdminOnly` policy (`policy.RequireRole("SuperAdmin")`) — apply directly to the new Integrations controller (SETT-07); see `UsersController.cs`/`GroupController.cs` for usage
- Modern card UI pattern (`modern-card`/`modern-card-header`/`modern-card-body`, per root `CLAUDE.md`) — apply to the new Integrations page

### Integration Points
- There is no shared per-page nav list in `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` (desktop) / `_Layout.Platform.Mobile.cshtml` (mobile) — confirmed by inspecting both after the merge. Cross-linking between Platform pages is done via a header button on the source page instead: `UsersController`'s addition wired in a `<a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">` button on `Group/Index.cshtml` and `Index.Mobile.cshtml`. Follow the same pattern for the new Integrations page — add a header button to `Group/Index.cshtml`/`Index.Mobile.cshtml`, not a shared-layout nav item.
- New `IntegrationSettingEntity` + repository + domain service, single-row table — no existing entity to extend

</code_context>

<specifics>
## Specific Ideas

- Nonce/jti field added to the token contract specifically so Phase 67's replay-protection has a purpose-built key to track, rather than hashing the whole token.
- "Generate Secret" button UX explicitly chosen over both plain manual paste and an auto-sync API, after discussing the security tradeoffs of secret bootstrapping between two apps that don't otherwise call each other.

</specifics>

<deferred>
## Deferred Ideas

- **Server-to-server API between Quest Board and Omphalos** (raised during the Shared Secret UX discussion, as a way to auto-sync the shared secret or otherwise let the two apps talk to each other) — this is already covered by requirement `BIDI-01` ("Foundation laid for a future bidirectional API — no implementation now") and the milestone's Key Decisions log (`.planning/PROJECT.md`). Not a new deferred item requiring roadmap action; redirected during discussion to the existing scope decision. If it resurfaces, it belongs in the same future milestone as `BIDI-01`/`IDP-01`, not this one.

</deferred>

---

*Phase: 65-platform-settings-token-contract*
*Context gathered: 2026-07-02*
*Re-validated post-v7.0-merge: 2026-07-08*
