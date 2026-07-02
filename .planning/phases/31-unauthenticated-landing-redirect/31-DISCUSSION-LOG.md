# Phase 31: Unauthenticated Landing Redirect - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-30
**Phase:** 31-unauthenticated-landing-redirect
**Areas discussed:** Route lockdown approach, Root URL (/) behavior, Expired session UX

---

## Route Lockdown Approach

| Option | Description | Selected |
|--------|-------------|----------|
| Targeted [Authorize] on 3 controllers | Add [Authorize] to HomeController, CalendarController, QuestLogController. Matches existing ShopController/GuildMembersController pattern. | ✓ |
| Global fallback auth policy | SetFallbackPolicy(RequireAuthenticatedUser()) in Program.cs. More future-proof but more invasive. | |

**User's choice:** Targeted [Authorize] on 3 controllers

---

| Option | Description | Selected |
|--------|-------------|----------|
| Require login (lock QuestLog) | Quest log is group history — group-private data. | ✓ |
| Keep public (read-only history) | Past quest recaps are non-sensitive lore — could stay public. | |

**User's choice:** Lock it — "the quest log is group-specific"

---

| Option | Description | Selected |
|--------|-------------|----------|
| Just DM profiles + Login/Logout + error pages as public | DungeonMasterController [AllowAnonymous] stays. | |
| Lock everything else | DMs are group-bound — their profiles should also require login. | ✓ |

**User's choice:** "DungeonMaster controller should also be locked, as the dungeon masters are also group bound? so only login/logout and error pages are public"

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — remove [AllowAnonymous], profiles require login | Class-level [Authorize] already applies; just remove the exemptions. | ✓ |
| Keep DM profiles public | Profile pages are read-only presentation — harmless to outside visitors. | |

**User's choice:** Yes — remove [AllowAnonymous], profiles require login

---

## Root URL (/) Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Let Identity handle redirect automatically | [Authorize] on HomeController → Identity redirects to /Account/Login. | |
| Move quest board to QuestController, redesign / as landing page | Make HomeController a proper welcome page; quest board moves to /quests. | ✓ |

**User's choice:** "What about, moving the contents of the homeController (which is just the index method) to the QuestController? And redesign the homeController (/) to be an actual welcoming page for the questboard?"
**Notes:** User proactively suggested this redesign — it was not offered as an option, it emerged from the discussion.

---

| Option | Description | Selected |
|--------|-------------|----------|
| Simple welcome + Login button | Name/tagline, prominent Log in button. Can redesign later. | ✓ |
| Richer marketing page | Feature highlights, illustrations. More work. | |
| Just redirect to /Account/Login | / immediately redirects unauthenticated users. No dedicated landing. | |

**User's choice:** Simple welcome + Login button — "Let's keep it simple for now, I can always redesign it later. It's just the home page"

---

| Option | Description | Selected |
|--------|-------------|----------|
| /quests or QuestController.Index | Move quest list to QuestController; semantically correct. | ✓ |
| /home/board | Keep quest board on HomeController under a different action. | |

**User's choice:** /quests or QuestController.Index

---

| Option | Description | Selected |
|--------|-------------|----------|
| /quests as direct fallback | GroupPicker always redirects to /quests. | |
| returnUrl or /quests as fallback | Preserve returnUrl for deep links; /quests as default fallback. | ✓ |

**User's choice:** returnUrl or /quests as fallback

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — create Home/Index.Mobile.cshtml for landing | Consistent with project-wide mobile view parity. | ✓ |
| No — landing page is simple enough without mobile variant | Bootstrap handles it at any width. | |

**User's choice:** Yes, create Mobile variant

---

## Expired Session UX

| Option | Description | Selected |
|--------|-------------|----------|
| Redirect to /groups/pick (include the deferred item) | Middleware after UseAuthentication: authenticated + no ActiveGroupId → redirect to picker. | ✓ |
| Keep 403 (Phase 30 deferred, stay narrow) | Phase 31 stays minimal; 403 is correct behavior for expired session. | |

**User's initial response:** "I'm not sure? I'm currently thinking the Redirect is the right option, but I really don't know"

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add middleware — clean enough | Transparent session recovery for single-group users; picker for multi-group. | ✓ |
| No, keep 403 for now | Middleware adds complexity and edge cases. | |

**User's question before deciding:** "Does that also mean, a user with a single group is automatically redirected back to the /quest/index? as this user only has one group?"
**Notes:** After confirmation that GroupPickerController.Index auto-picks for single-group users (Phase 30 D-02), user chose to include the middleware.

---

## Claude's Discretion

- Exact name/style for the session-recovery middleware (named class vs. inline lambda)
- Whether QuestController.Index gets class-level or action-level [Authorize]
- Visual design of the public landing page at / — simple card, exact copy and styling

## Deferred Ideas

None — discussion stayed within phase scope.
