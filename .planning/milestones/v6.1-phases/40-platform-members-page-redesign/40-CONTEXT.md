# Phase 40: Platform Members Page Redesign - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

The Platform (SuperAdmin) group Members page (`GroupController.Members`, `Areas/Platform/Views/Group/Members.cshtml` + `.Mobile.cshtml`) is redesigned from a single-column members table plus a plain `<select>` "Add Member" dropdown into a two-column layout: current members on the left (unchanged), a searchable/filterable table of non-member platform users on the right with per-row "Add" actions, plus a "Create New User" entry point in that same right column that creates or adds a user scoped to the group being managed — reusing Phase 39's shared `IUserService.CreateOrAddToGroupAsync` method, without requiring a session-level active group and without leaving the Members page.

This phase touches only `GroupController` (Platform area), its views, and any new supporting ViewModels/repository query needed for the searchable non-member list. It does not touch the group-admin `AdminController.Users()`/`CreateUser` flow (Phase 38/39 territory) beyond reusing its shared method and flash-message conventions.

</domain>

<decisions>
## Implementation Decisions

### Non-member search & table (MEMBERS-02)
- **D-01:** Search is a server round-trip — a GET request with a `search` query parameter that reloads the Members page with the non-member table filtered, mirroring `Views/Shop/Index.cshtml`'s existing filter-row pattern (`shop-filter-form`, `BuildTabUrl`-style query-string construction). Not a client-side JS instant filter — user explicitly chose the round-trip approach over the client-side option for consistency with the established pattern.
- **D-02:** Each non-member row shows Name + Email only — matches the information already present in today's dropdown option text (`@u.Name (@u.Email)`). No new "other group memberships" column; no new repository query needed for this.

### Add-to-group interaction (MEMBERS-01, MEMBERS-03 supporting)
- **D-03:** Each non-member row gets an inline role dropdown (defaulting to `GroupRole.Player`) and a small "Add" button — one action per row, replacing today's separate "select user, pick role, submit" form below the table entirely. The standalone `AddMember` form section is removed from the view.
- **D-04:** Submitting "Add" for a row redirects back to `Members` preserving the current `search` query string (not resetting to the unfiltered list) — the SuperAdmin's filter stays intact after adding someone, since D-01 makes search a page-reloading GET.

### Create New User entry point (MEMBERS-03)
- **D-05:** Presented as a Bootstrap modal triggered by a button in the right column — mirrors the existing modal-with-form-post pattern already used in `Views/ShopManagement/Index.cshtml` (`#bulkActionsModal`, `#denyModal`: `modal fade` → `modal-dialog` → `modal-content` containing a `<form method="post">` with `@Html.AntiForgeryToken()`). Not an inline expandable panel — no precedent for that pattern in this codebase, and the modal approach satisfies the Phase 40 goal's "without leaving the page" requirement identically.
- **D-06:** The modal's form fields mirror `Views/Admin/CreateUser.cshtml` exactly (Email, Name, GroupRole dropdown) but posts to a new Platform-area action that takes `groupId` from the route (the `Members` page's `id`), never from `IActiveGroupContext` — per the STATE.md risk flag (this exact session/route confusion has caused two prior incidents: Phase 30, Phase 34.3). **Never inject `IActiveGroupContext` into `GroupController`.**
- **D-07:** The new Platform create-user action reuses `IUserService.CreateOrAddToGroupAsync` (Phase 39) and reuses its exact flash-message wording verbatim for all four outcomes, applying the same `RedirectWithSuccess`/`RedirectWithWarning` helper pattern `AdminController.CreateUser` already uses:
  - New account: `"Account created for {Name}. A welcome email with a set-password link has been sent."`
  - Added to group (both `AddedToGroup` and `AddedToGroupStrandedAccount` outcomes): `"{Name} has been added to the group as {Role}. A notification email has been sent."`
  - Already a member: `"{Name} is already a member of this group."` via `RedirectWithWarning`
  - No SuperAdmin-specific copy variant — user confirmed the existing wording works regardless of which screen triggered creation, consistent with Phase 39's original design intent (identical behavior "applied consistently... in both the group-admin `AdminController.CreateUser` and the new platform create-user entry point").

### Mobile layout
- **D-08:** `Members.Mobile.cshtml` stacks the two columns vertically — current members list first (unchanged from today), then the search box + non-member cards, then the Create New User trigger below — no tab/toggle switch between the two lists (no precedent for tabs in this codebase's mobile views).
- **D-09 (Claude's discretion):** Whether the Create New User entry point stays a modal on mobile (Bootstrap modals already render responsively elsewhere, e.g. `ShopManagement.Mobile.cshtml` reuses the same deny/bulk-actions modals) or becomes a separate full-page form matching the desktop/mobile split pattern seen elsewhere (`Manage.cshtml` vs `Manage.Mobile.cshtml`) — user explicitly deferred this choice to Claude's judgment during planning/implementation.

### Claude's Discretion
- Exact non-member repository query/method name for the group-scoped, search-filtered "users not in this group" list (new method on `IUserRepository`/`IUserService`, e.g. `GetAvailableUsersAsync(groupId, search)`) — implementation detail, not discussed.
- Exact new Platform action name/route for the create-user entry point (e.g. `GroupController.CreateMember` or similar) — implementation detail. Must accept `groupId` from the route per D-06.
- Whether the per-row inline "Add" control needs its own antiforgery-protected mini-form per row, or a single delegated form/JS submit — implementation detail.
- D-09 above (mobile Create New User presentation: modal vs. full page).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — MEMBERS-01, MEMBERS-02, MEMBERS-03 (the three requirements this phase satisfies)
- `.planning/ROADMAP.md` §"Phase 40: Platform Members Page Redesign" — goal, dependency on Phase 39, and the four locked success criteria
- `.planning/PROJECT.md` §"Active" — mirrors the milestone-level framing of this requirement

### Project state / risk flags
- `.planning/STATE.md` §"Risk Flags for Planning (from research)" — Phase 40 entry: "new Platform `CreateMember`/create-user action must source `groupId` strictly from the route, never from session `IActiveGroupContext`... Never inject `IActiveGroupContext` into `GroupController`" — this is a hard constraint (D-06), not a suggestion
- `.planning/STATE.md` §"Accumulated Context > Decisions" — "MEMBERS-01 was refined after research: the Platform Members page uses a two-column layout (current members left, searchable available-users + Add User + Create New User right), not a single-column redesign"

### Phase 39 dependency (shared method this phase reuses)
- `.planning/phases/39-shared-collision-aware-user-creation-email/39-CONTEXT.md` — full decision record for `CreateOrAddToGroupAsync`'s four outcomes, the `AddedToGroup`/stranded-account email variants, and the locked flash-message wording (D-09 in that file) this phase reuses verbatim (D-07 above)
- `.planning/phases/39-shared-collision-aware-user-creation-email/39-CONTEXT.md` §Specifics — "`groupId` is a plain parameter (not session-derived), the call shape Phase 40's platform entry point will reuse"

### UI/UX conventions
- `CLAUDE.md` §"UI/UX Design Guidelines" — modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`), filled colored buttons, FontAwesome icons with `me-2`, `d-flex justify-content-between` button layout — applies to the redesigned Members view
- `.planning/codebase/CONVENTIONS.md` §"UI/UX Design" — same modern-card conventions, restated with codebase-verified examples

No other external specs/ADRs apply — requirements fully captured in Decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Areas/Platform/Controllers/GroupController.cs:113-161` (`Members`, `AddMember`, `RemoveMember` actions) — the controller this phase modifies; `Members` currently does `allUsers.Where(u => !memberUserIds.Contains(u.Id))` in-memory with no search — needs a group-scoped, search-filtered query instead
- `Views/Shop/Index.cshtml:205-280` — server round-trip filter-row pattern to mirror for D-01 (`BuildTabUrl` query-string builder, `<form method="get">`, search input tied to the filter form via `form="shop-filter-form"`)
- `Views/ShopManagement/Index.cshtml:414-476` (`#bulkActionsModal`, `#denyModal`) — Bootstrap modal-with-form-post pattern to mirror for D-05 (`modal fade` → `modal-dialog` → `modal-content` → `<form method="post">` with `@Html.AntiForgeryToken()`)
- `Views/Admin/CreateUser.cshtml` — form field layout (Email, Name, GroupRole `asp-items="Html.GetEnumSelectList<GroupRole>()"`) to mirror inside the new modal (D-06)
- `Controllers/Admin/AdminController.cs:113-190` (`CreateUser` POST) — the exact `CreateOrAddToGroupAsync` call shape, outcome switch, and flash-message strings to reuse verbatim in the new Platform action (D-07)
- `Extensions/ControllerExtensions.cs` — `RedirectWithSuccess`/`RedirectWithWarning`/`RedirectWithError` helpers already exist (added in Phase 39); reuse directly, no new helper needed
- `ViewModels/PlatformViewModels/GroupMembersViewModel.cs`, `AddMemberViewModel.cs` — current ViewModels; `AvailableUsers` (`IList<User>`) and `AddMember` (single-user form) will need reshaping for the per-row inline Add pattern (D-03) and search (D-01)

### Established Patterns
- `IUserService.CreateOrAddToGroupAsync(string email, string name, int groupId, GroupRole role, CancellationToken token = default)` (`QuestBoard.Domain/Services/UserService.cs:155`) — confirmed signature takes `groupId` as a plain parameter, not session-derived; directly reusable from `GroupController` with the route's `id`
- `UserEntity` has no EF Core Global Query Filter (`QuestBoardContext.cs`) — any new "users not in group X, matching search" query must be an explicit manual join/filter, same as Phase 38's `GetAllGroupMembersAsync` precedent
- `[Authorize(Policy = "SuperAdminOnly")]` already class-level on `GroupController` — no new authorization work needed
- `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` + `MobileDetectionMiddleware` — confirms Platform area already has its own mobile view split (`Members.Mobile.cshtml` exists today), so this phase's mobile work is a redesign of an existing mobile view, not new mobile infrastructure

### Integration Points
- `Areas/Platform/Controllers/GroupController.cs` — `Members` GET (add search param + new query), `AddMember` POST (adapt to per-row inline submission, D-03/D-04), new `CreateMember`-style POST action (D-06/D-07)
- `Areas/Platform/Views/Group/Members.cshtml` + `Members.Mobile.cshtml` — full two-column (desktop) / stacked (mobile) redesign
- `QuestBoard.Domain/Interfaces/IUserService.cs` / `IUserRepository.cs` — likely needs a new group-scoped + search-filtered "available users" query method (naming left to Claude's discretion)

</code_context>

<specifics>
## Specific Ideas

- Search behaves like Shop's existing filter row: GET + query string, page reload, not instant/client-side.
- Add-to-group becomes one click per row (inline role dropdown + Add button) instead of today's separate select-user-then-submit form.
- Adding a user preserves whatever search term was active — no losing your filter after an Add.
- Create New User opens in a Bootstrap modal (matching ShopManagement's existing modal pattern), not a separate page, and not an inline expandable section.
- Flash messages for the new create-user action are byte-for-byte the same as Phase 39's `AdminController.CreateUser` wording — no Platform-specific variant.
- Mobile: simple vertical stack (Members → Search/Add Users → Create New User), no tabs.

</specifics>

<deferred>
## Deferred Ideas

None raised during discussion — all four selected gray areas stayed within this phase's scope.

### Reviewed Todos (not folded)
None — `todo.match-phase` returned zero matches for Phase 40.

</deferred>

---

*Phase: 40-platform-members-page-redesign*
*Context gathered: 2026-07-04*
