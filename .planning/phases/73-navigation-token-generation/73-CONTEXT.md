# Phase 73: Navigation + Token Generation - Context

**Gathered:** 2026-07-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Every Omphalos entry point in the UI generates a signed, time-limited redirect token through one shared service (`IIntegrationTokenService`, new in this phase) and lands the user in Omphalos — no surface ever links to Omphalos's raw base URL unsigned.

Two entry points, both DM-only:
1. **Quest Details page** — a "Launch Omphalos" button in the existing DM Controls card, carrying full quest context (questId/questTitle/questDate) in the token.
2. **DM navbar link** — not tied to any specific quest; carries only the user's identity in the token and lands on Omphalos's index/dashboard.

This phase builds `IIntegrationTokenService` (generation only — signing, encoding, expiry), the `QuestController.LaunchOmphalos(int id)` action (TOKEN-05), the navbar action/entry point, and the UI wiring for both. It does not implement the Omphalos-side SSO endpoint or session linking (Phase 74) — it only produces the signed URL and redirects to it.

</domain>

<decisions>
## Implementation Decisions

### Quest-page entry point
- **D-01:** The "Launch Omphalos" button lives only on the Quest Details page (`Views/Quest/Details.cshtml`), inside the existing DM Controls card, alongside "Manage Quest" — not duplicated onto the quest list page.
- **D-02:** The button is visible for a quest in **any** state, not gated on the quest being finalized/having a confirmed date. `questDate` in the token payload may be null/unset for quests without a confirmed date yet.
- **D-03:** Clicking the button launches immediately (plain link, same UX as the existing "Manage Quest" link) — no confirmation dialog before leaving to Omphalos.

### DM navbar entry point
- **D-04:** The navbar link generates a **signed token containing only the user's identity** (`UserEntity.Id`, per TOKEN-04) — `questId`/`questTitle`/`questDate` are left null. It is NOT a raw unsigned link to Omphalos's base URL (that would violate the phase goal and TOKEN-04's identity requirement). It lands the DM on Omphalos's index/dashboard rather than a specific game session.
- **D-05 (resolved after initial ambiguity):** The user's first instinct was "just redirect to Omphalos's index page, no payload needed" for the navbar link. This was clarified during discussion: even the quest-less navbar launch must still be signed and carry the user's identity, both because the phase goal explicitly forbids any unsigned raw link to Omphalos and because TOKEN-04 requires the identity claim always be `UserEntity.Id`. The user confirmed the signed, user-only token approach once this was explained.
- Implication for `IIntegrationTokenService`: it needs to support generating a token where quest fields are optional/nullable — not just an overload hardcoded to always require a quest. Left to planning/research to decide the exact method shape (e.g., one method with nullable quest params vs. two methods).

### Disabled / misconfigured integration UX
- **D-06:** When Omphalos integration is disabled or unconfigured for the DM's active group (per `IPlatformSettingService.GetResolvedAsync(groupId)` → `OmphalosSettings.IsEnabled`/`HasSecret`), both entry points (quest-page button and navbar link) are **hidden entirely** — not shown disabled/grayed with a tooltip.
- **D-07 (TOKEN-05's "graceful response"):** If `LaunchOmphalos` (or the navbar action) is hit directly while disabled/misconfigured — defense in depth, since hiding the button doesn't stop a guessed URL — the action redirects back with a flash/TempData message (e.g., "Omphalos integration is not enabled for this group") rather than throwing or returning a raw error.
  - For the quest-specific action: redirect back to that quest's Details page.
  - For the navbar (quest-less) action: redirect to the Quest Board home/dashboard, since there's no quest page to return to.

### Redirect / token-generation failure handling
- **D-08:** If token generation fails unexpectedly at click-time (e.g., secret missing at the moment of signing — a different case from "integration disabled," which is a normal/expected state) — use the **same graceful fallback** as the disabled case (D-07): redirect back with a flash message. One consistent failure path for both "expected disabled" and "unexpected failure," rather than a separate distinct error page.

### Claude's Discretion
- Exact `IIntegrationTokenService` method signature(s) — whether quest-less generation is a nullable-params overload or a separate method — left to research/planning.
- Exact nonce generation mechanism, JSON casing, and other low-level serialization details not already fixed by Phase 72's D-02 (JSON + HMAC-SHA256 + Base64URL, reusing `WebEncoders.Base64UrlEncode`).
- Exact wording of flash/TempData messages for the disabled/failure cases (D-06/D-07/D-08).
- Icon and exact button copy/placement details within the DM Controls card and the navbar dropdown, following existing FontAwesome + `me-2` conventions.
- Where in `QuestController` vs. a new controller the navbar (quest-less) action lives — planner's call, consistent with how other DM-only actions are organized.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` — TOKEN-01, TOKEN-03, TOKEN-04, TOKEN-05, TOKEN-06 (locked requirements this phase implements; TOKEN-02 was Phase 72's contract-writing, already complete)
- `.planning/ROADMAP.md` — Phase 73 section (goal, dependencies on Phase 72)
- `.planning/PROJECT.md` — Key Decisions table (HMAC-signed token behind swappable `IIntegrationTokenService`/`SsoService` seam, identity key = `UserEntity.Id`)

### Token Contract (Phase 72 — locked, do not re-decide)
- `.planning/phases/72-platform-settings-token-contract/72-CONTEXT.md` — D-01/D-02: canonical message is JSON (`nonce`, `userId`, `questId`, `questTitle`, `questDate`, `expiry`), HMAC-SHA256 signed, Base64URL-encoded via `WebEncoders.Base64UrlEncode`. This phase's token-less-quest variant (D-04/D-05 above) must fit within this same contract shape with `questId`/`questTitle`/`questDate` nullable.
- `.planning/phases/72-platform-settings-token-contract/72-RESEARCH.md` — stack recommendation (BCL `HMACSHA256`, `WebEncoders.Base64UrlEncode`, no new packages) still valid; settings-architecture section is superseded by the actual Phase 72 implementation (see code_context below).

### Settings Resolution (Phase 72 — implemented, this phase consumes it)
- `QuestBoard.Domain/Interfaces/IPlatformSettingService.cs` — `GetResolvedAsync(int? groupId, CancellationToken)` returns the cascade-resolved `OmphalosSettings` (group override → instance default). This is the service Phase 73's token generator and disabled-check logic call.
- `QuestBoard.Domain/Models/OmphalosSettings.cs` — `Url`, `SharedSecret` (plaintext, never render to browser), `IsEnabled`, `HasSecret`.
- `QuestBoard.Domain/Constants/PlatformSettingKeys.cs` — key constants (`Omphalos.Url`, `Omphalos.SharedSecret`, `Omphalos.Enabled`).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/Views/Quest/Details.cshtml` (~line 628-639) — the DM Controls card with the "Manage Quest" link:
  ```html
  <a href="@Url.Action("Manage", "Quest", new { id = Model.Quest?.Id })" class="btn btn-primary w-100">
      <i class="fas fa-cog me-2"></i>Manage Quest
  </a>
  ```
  Add the "Launch Omphalos" button as a sibling `<a>` in this same card, conditionally rendered per D-06 (hidden when disabled).
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` / `_Layout.Mobile.cshtml` (~lines 95-120) — DM-only dropdown gated on `AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")`, containing `asp-controller`/`asp-action` links with FontAwesome icons. Add the navbar Omphalos link here, also conditionally rendered per D-06.
- `WebEncoders.Base64UrlEncode`/`Decode` (`Microsoft.AspNetCore.WebUtilities`) — already used throughout `AdminController.cs` for password-reset/email-change tokens; reuse for this token's encoding per Phase 72's D-02.
- `IPlatformSettingService.GetResolvedAsync` — call this to resolve `OmphalosSettings` for the active group, both to build the token (URL + secret) and to decide whether to hide the entry points (D-06).

### Established Patterns
- `[Authorize(Policy = "DungeonMasterOnly")]` — the standard DM-only action gate used across `QuestController` (e.g. `Finalize`, `Open`, `Close`, `Manage` at line 857); apply to `LaunchOmphalos` per TOKEN-05.
- Only one existing plain external `Redirect()` call in the codebase: `GroupPickerController.cs:67`. No established pattern for signed external redirects — new territory. Construct/validate the target URL carefully (open-redirect risk is mitigated here because the base URL always comes from `OmphalosSettings.Url`, a value the SuperAdmin/Group Admin configured — never from user input).
- Flash/TempData message pattern for redirect-with-error (used across existing controllers) — reuse for D-07/D-08's graceful fallback rather than introducing a new error-page pattern.

### Integration Points
- `QuestController` (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`) — add `LaunchOmphalos(int id)` here, following the existing action conventions (constructor-injected services, no `[Route]` prefix).
- New `IIntegrationTokenService` (Domain layer) — does not exist yet; this phase creates it. Needs to accept nullable quest context per D-04/D-05.
- The navbar (quest-less) action needs a home — either a new action on `QuestController` or a new lightweight controller; left to planning (see Claude's Discretion above).

</code_context>

<specifics>
## Specific Ideas

- The navbar link's scope initially seemed like it could just be an unsigned "quick link" to Omphalos, but discussion surfaced that this would violate both the phase's own goal statement and TOKEN-04 — worth flagging to the researcher/planner since it's an easy trap to fall into when only reading the ROADMAP.md one-liner ("DM navbar link") without the full reasoning captured here.
- "Hide entirely" for disabled state (D-06) was a clear, uncontested choice — no back-and-forth, matches the general pattern of features being invisible until an admin enables them elsewhere in this app.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 73-navigation-token-generation*
*Context gathered: 2026-07-13*
