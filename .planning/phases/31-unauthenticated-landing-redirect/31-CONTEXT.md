# Phase 31: Unauthenticated Landing Redirect - Context

**Gathered:** 2026-06-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Lock down all group-scoped pages so unauthenticated visitors are redirected to `/Account/Login` rather than seeing empty or broken content. Introduce a simple public landing page at `/` and move the quest board to `QuestController.Index` at `/quests`. Add session-recovery middleware so authenticated users with an expired group session are redirected to `/groups/pick` (auto-resolved for single-group users) instead of getting a 403.

This phase does NOT include: per-group email configuration, cross-group quest browsing, group invitation flows, or any Platform area changes.

</domain>

<decisions>
## Implementation Decisions

### Route Lockdown

- **D-01:** Add `[Authorize]` to `HomeController`, `CalendarController`, and `QuestLogController` at the class level. Matches the existing pattern used by `ShopController` and `GuildMembersController`. No global fallback policy.

- **D-02:** Remove `[AllowAnonymous]` from the DM profile actions in `DungeonMasterController`. DMs are group-bound members — their profiles are group-private. The class-level `[Authorize]` already applies once the exemptions are removed.

- **D-03:** Only two categories of routes stay publicly accessible (no login required):
  1. Auth routes: `/Account/Login`, `/Account/Logout`
  2. Error/infrastructure pages (e.g. `/Home/Error` if it exists)
  All quest board, calendar, quest log, shop, guild, and DM profile routes require authentication.

### Root URL (/) Redesign

- **D-04:** `HomeController.Index` becomes a simple public landing page — app name/tagline and a prominent "Log in" button. No group-scoped data. No `[Authorize]` on HomeController (the landing page is intentionally public). The user said "it's just the home page — can always redesign later."

- **D-05:** The quest board (quest list with signup status, currently in `HomeController.Index`) moves to a new `QuestController.Index` action at route `/quests`. The logic from `HomeController.Index` (including the `IQuestService.GetQuestsWithSignupsForRoleAsync` call and current-user detection) moves verbatim. `QuestController` already has `[Authorize]` on individual actions — confirm class-level or add it to the new Index action.

- **D-06:** Existing `Views/Home/Index.cshtml` and `Views/Home/Index.Mobile.cshtml` move to `Views/Quest/Index.cshtml` and `Views/Quest/Index.Mobile.cshtml`. New `Views/Home/Index.cshtml` and `Views/Home/Index.Mobile.cshtml` are created for the landing page. Both landing variants are required — consistent with project-wide mobile view parity.

- **D-07:** `GroupPickerController.SelectGroup` POST changes its fallback redirect from `Redirect(returnUrl ?? Home/Index)` to `Redirect(returnUrl ?? "/quests")`. All other returnUrl logic is preserved — deep links continue to work.

- **D-08:** Any nav links or `RedirectToAction("Index", "Home")` calls elsewhere in the codebase that pointed to the quest board must be updated to point to `("/quests")` or `RedirectToAction("Index", "Quest")`.

### Expired Session Recovery Middleware

- **D-09:** Add a middleware component after `UseAuthentication` (and after `UseSession`) in `Program.cs` that detects: authenticated user + no `ActiveGroupId` in `ISession` + request path is not an exempt route → redirect to `/groups/pick`.

- **D-10:** Exempt routes for the session recovery middleware (must not trigger redirect):
  - `/groups/pick` (the destination)
  - `/Account/Login`, `/Account/Logout`
  - `/platform/*` (SuperAdmin area — SuperAdmin has null ActiveGroupId by design)
  - Error/infrastructure routes (e.g. `/Home/Error`)
  - Static files and favicon are already handled before middleware by `UseStaticFiles`

- **D-11:** Behavior by user type on redirect to `/groups/pick`:
  - **Single-group user:** `GroupPickerController.Index` auto-selects the group and redirects to `/quests`. Seamless recovery — user never sees the picker.
  - **Multi-group user:** Sees the group-picker card grid, re-selects their group.
  - **SuperAdmin:** Lands on the picker with all groups + "Go to Platform →" button. This is correct — SuperAdmin intentionally has null `ActiveGroupId`.

### Claude's Discretion

- Exact name for the new middleware class or inline lambda in Program.cs (e.g. `GroupSessionMiddleware` or inline `app.Use(...)`)
- Whether `QuestController.Index` gets a class-level `[Authorize]` added or just an action-level `[Authorize]` on the new Index action (depends on existing controller structure — planner should check)
- Visual design of the public landing page at `/` — simple card with app name and login button; exact copy and styling at planner's discretion

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior Phase Decisions (locked — do not re-litigate)
- `.planning/phases/30-group-ux-admin-user-creation/30-CONTEXT.md` — D-02: `GroupPickerController` routes and auto-pick logic; D-03: `SessionKeys.ActiveGroupId`; D-04: null ActiveGroupId → `context.Fail()` in auth handlers (still applies for policy violations by authenticated users — the middleware handles the session-expiry case separately)
- `.planning/phases/28-tenant-isolation/28-CONTEXT.md` — D-02: `ActiveGroupContextService` reads `SessionKeys.ActiveGroupId` from `ISession`; returns `null` for SuperAdmin and unauthenticated users

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Group UX (UX-01–UX-05) — login routing, group picker, session persistence, nav requirements
- `.planning/ROADMAP.md` §Phase 31 — phase goal, dependency on Phase 30

### Key Files to Read Before Planning
- `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` — quest board logic to migrate to QuestController
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — target controller; verify no existing Index action conflicts
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — update fallback redirect (D-07)
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` — remove `[AllowAnonymous]` overrides (D-02)
- `QuestBoard.Service/Program.cs` — middleware insertion point (after `UseAuthentication`, after `UseSession`)
- `QuestBoard.Service/Views/Home/Index.cshtml` — current quest board view to migrate
- `QuestBoard.Service/Views/Home/Index.Mobile.cshtml` — current quest board mobile view to migrate

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SessionKeys.ActiveGroupId` — already defined at `QuestBoard.Service/Constants/SessionKeys.cs`; middleware reads this key from `HttpContext.Session.GetInt32(SessionKeys.ActiveGroupId)`
- `GroupPickerController.Index` — auto-picks for single-group users (Phase 30 D-02); no changes needed for session-recovery redirect to work
- `modern-card` CSS classes — available for the simple landing page design at `/`
- `IQuestService.GetQuestsWithSignupsForRoleAsync` — the existing method used in `HomeController.Index`; moves verbatim to `QuestController.Index`

### Established Patterns
- `[Authorize]` at controller class level — existing pattern in `ShopController`, `GuildMembersController`, `DungeonMasterController`; add to `HomeController`, `CalendarController`, `QuestLogController`
- Middleware in `Program.cs` — existing pattern: Hangfire path guard is an inline `app.Use(...)` lambda; session-recovery can follow the same pattern or be a named class
- Mobile view parity — every view has a `.Mobile.cshtml` variant; new `Views/Home/Index.Mobile.cshtml` and migrated `Views/Quest/Index.Mobile.cshtml` required

### Integration Points
- `Program.cs` middleware pipeline — insert session-recovery middleware AFTER `app.UseSession()` and `app.UseAuthentication()` and BEFORE `app.UseAuthorization()`
- `GroupPickerController.SelectGroup` POST — change `nameof(Home)` redirect to point at `QuestController.Index`
- Any `RedirectToAction("Index", "Home")` or `Url.Action("Index", "Home")` references in the codebase — must be updated to `/quests`; planner should grep for these

### Known Landmines
- `QuestController` does not currently have an `Index` action — adding one is new territory; confirm route `/quests` maps correctly under the default MVC route `{controller}/{action}/{id?}` (it should: controller=Quest, action=Index)
- The landing page `HomeController.Index` is intentionally NOT `[Authorize]` — do not add it; the whole point is a public entry point
- Session-recovery middleware must NOT fire for SuperAdmin users even though `ActiveGroupId` is null for them. Exempt: check `User.IsInRole("SuperAdmin")` alongside the path exemptions (D-10)
- Static files are served before MVC middleware — `UseStaticFiles` already runs before the middleware, so assets are unaffected

</code_context>

<specifics>
## Specific Ideas

- **Landing page at `/`:** Simple and clean — user explicitly said "it's just the home page, can always redesign later." Welcome card with the app name and a "Log in" button. Bootstrap handles mobile responsiveness but a `Mobile.cshtml` variant is required per project convention.
- **Quest board route:** User confirmed `/quests` as the new home for the quest board after migrating from `HomeController.Index`.
- **Session-recovery seamlessness for single-group users:** User explicitly verified that `GroupPickerController.Index` auto-picks for single-group users → seamless re-entry to `/quests` without seeing the picker page.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 31-unauthenticated-landing-redirect*
*Context gathered: 2026-06-30*
