# Feature Research

**Domain:** Admin/user-management UX for a small internal multi-tenant (group-scoped) SaaS-style app
**Researched:** 2026-07-03
**Confidence:** MEDIUM (codebase analysis HIGH; external pattern corroboration LOW — see Sources)

*Supersedes the v5.0 multi-tenancy research previously in this file — that milestone shipped (Phases 29–34.3, see PROJECT.md). This is fresh research for the v6.1 Bugfixes milestone.*

## Context Recap

Three bugfixes for v6.1, all touching the existing admin/user-management surface:

1. `AdminController.Users()` (group-admin, `/Admin/Users`) — currently calls `userService.GetAllAsync()` with **no group filter**, leaking every platform user (all groups) into a single group's admin view.
2. `Areas/Platform/Controllers/GroupController.Members()` (SuperAdmin, `/platform/Group/Members/{id}`) — "Add Member" is a plain `<select>` dropdown built from `allUsers.Where(u => !memberUserIds.Contains(u.Id))`; needs to become a searchable/filterable table, plus a group-scoped "create new user" entry point.
3. `IdentityService.CreateUserAsync` — hard-fails via `UserManager.CreateAsync` uniqueness constraint (`UserName = email`) when the email already exists anywhere on the platform; needs to instead detect the collision and auto-add the existing user to the target group with the selected role, sending a distinct notification email.

Existing infra confirmed by reading the code: `IActiveGroupContext.ActiveGroupId`, `GroupRole` enum (Admin/DungeonMaster/Player) with `userService.GetGroupRoleByIdAsync`/`SetGroupRoleAsync`/repository `AddMemberAsync`, Hangfire `IBackgroundJobClient` + `IEmailService`/`IEmailRenderService` job pattern (see `WelcomeEmailJob`), no existing client-side search/filter pattern anywhere in the codebase (this is genuinely new for the app), `[Bind(Prefix = "AddMember")]` nested-viewmodel convention on `GroupController`.

## Feature Landscape

### Table Stakes (Users Expect These)

Features that are the minimum correct/expected behavior — missing these is a bug, not a design choice (this maps directly to the three PROJECT.md requirements).

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Group-scoped user list (filter `Users()` by `ActiveGroupId`) | A group admin managing "their" users must never see or accidentally act on another group's members — this is a tenant-isolation bug today, not a missing nice-to-have | LOW | Swap `userService.GetAllAsync()` for a group-scoped repository query (e.g. `GetUsersInGroupAsync(groupId)`, joining on the `UserGroup`/membership table used by `GetGroupRoleByIdAsync`). No new UI needed — same `Users.cshtml` view, filtered dataset. Directly parallels the WorkOS/Filament pattern of scoping the query, not the view. |
| Live substring filter on the Members/"add existing user" table (name + email, case-insensitive, as-you-type) | Table-stakes UX for any list-with-a-search-box at this scale; users expect typing to narrow results with no page reload | LOW | Vanilla JS `input` event + `textContent.toLowerCase().includes()` row toggle. No backend round-trip, no library. Matches every vanilla-JS table-filter reference found (W3Schools pattern, GeeksforGeeks, dev.to) — this is the de facto standard approach, not an opinion. |
| Keep full member/user table visible (no pagination) at ~17 users, N groups | Pagination below ~50-100 rows adds clicks without solving a real problem; a scrollable/filterable single table is faster to scan | LOW | Explicit anti-over-engineering guardrail — see Anti-Features. |
| "Create new user" entry point reachable from the Platform Members page, scoped to that group | SuperAdmin currently must leave the Members page, use group-admin's `AdminController.CreateUser` (which requires an *active* group context session, not applicable to a SuperAdmin browsing arbitrary groups by ID), then navigate back — today there's no group-scoped create-user path in the Platform area at all | MEDIUM | Needs a `Platform/Group/CreateUser?groupId=X` (or similar) action mirroring `AdminController.CreateUser`'s body but parameterized by an explicit `groupId` route/query value instead of `IActiveGroupContext.ActiveGroupId` (SuperAdmin has no "active group" session concept the way a group admin does). This is the one piece of real new server logic in this list — everything else is filtering an existing query or adding client JS. |
| Existing-email collision auto-adds to group + sends a *distinct* notification email (not the Welcome email) | An admin typing a real teammate's email into "Create User" is a completely ordinary action ("oh, they're already on the platform from another group") — hard-failing with a raw duplicate-username error is a bug that surprises the admin and blocks a legitimate operation | MEDIUM | Requires: (1) pre-check via `identityService.GetIdByEmailAsync(email)` *before* calling `CreateUserAsync`, short-circuiting to `SetGroupRoleAsync`/`AddMemberAsync` on collision; (2) a second email template + Hangfire job (e.g. `AddedToGroupEmailJob`) distinct from `WelcomeEmailJob`, since the recipient already has a password and should not get a "set your password" link; (3) apply identically in both `AdminController.CreateUser` and the new Platform create-user action — shared logic belongs in `UserService`/a new domain-level method, not duplicated per controller. |
| If the existing user is *already a member of this group*, surface a clear "already a member" message rather than a duplicate-add attempt | `GroupService.AddMemberAsync`/repository already throws `InvalidOperationException` for exactly this case (see `GroupController.AddMember`'s existing catch block) | LOW | Reuse the existing `InvalidOperationException` catch pattern already proven in `AddMember` — same message style ("This user is already a member of the group."), no new mechanism required. |

### Differentiators (Nice, Not Required for This Milestone)

Not required to close the three PROJECT.md gaps, but worth flagging since they sit directly adjacent and a reviewer may ask "why didn't you also...".

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Cross-group visibility for SuperAdmin on the group-admin `Users()` page (e.g. a "viewing as Group X" banner or group switcher) | Clarifies scope when a SuperAdmin browses a specific group's user list rather than the platform-wide view they're used to | LOW | Not required — the fix is a query-level scope change; a banner is polish. Consider only if human verification finds the scoped list confusing without context. |
| Debounced input on the live filter | Prevents excessive re-renders on very large tables | LOW (but unnecessary) | At ~17 users / dozens of rows max, a synchronous filter is imperceptibly fast — debounce is solving a problem that doesn't exist yet at this row count. Skip. |
| Highlighting matched substrings in filtered rows | Slightly nicer scan experience | LOW | Cosmetic; skip unless trivially cheap to add alongside the base filter — not worth a separate task. |
| Combined "invite or add" single form (radio toggle between "new user" / "existing user" before submit, instead of one form that auto-detects) | Some SaaS tools (implied by GitHub/Slack's explicit-consent model) make the new-vs-existing choice explicit upfront rather than resolving it server-side on submit | MEDIUM | Deliberately **not** recommended here — see Anti-Features: GitHub/Slack's explicit-invite-and-accept flow exists because they're inviting a *stranger* across trust boundaries; this app's admin is directly adding a known teammate to a group they already administer, so the "silent auto-add" the milestone spec already calls for is the right level of friction, not the interstitial. |

### Anti-Features (Commonly Considered, Wrong Fit Here)

| Feature | Why It Seems Appealing | Why Problematic at This Scale | Alternative |
|---------|------------------------|-------------------------------|-------------|
| Full JS grid/DataTables library (DataTables.net, ag-grid, Tabulator) for the searchable Members table | "Search" + "table" pattern-matches to "add a grid library" | Adds a new client dependency, a new CSS theme to reconcile with the existing `modern-card` Bootstrap styling, and server-side paging/sorting machinery this app will never need at ~17 users across a handful of groups — pure complexity tax with no user-visible benefit over vanilla JS | Vanilla JS `input`-event substring filter over the existing Bootstrap `table-striped table-hover` markup (same pattern already used elsewhere in the app for tables, just add the script) |
| Server-side search/filter API endpoint (AJAX call per keystroke) | "Feels more scalable" / mirrors patterns from larger apps | Unnecessary round-trip latency and a new endpoint to secure/test, for a dataset small enough to ship to the client in the initial page render already (the Members view already loads `AvailableUsers` server-side today) | Keep filtering 100% client-side against the already-rendered/already-fetched row set |
| Confirming interstitial ("This email already has an account — send an invite instead?") before auto-adding on collision | Mirrors GitHub/Slack's explicit-consent invite model, feels "safer" | GitHub/Slack are inviting a *stranger* into a workspace/org across a trust boundary the invitee controls (they must accept). Here, a **group Admin/SuperAdmin who already has authority over the target group** is adding a known person — the milestone's own spec calls for silent auto-add + notification, which matches the lower-friction "org admin directly assigns access" pattern (e.g. Google Workspace admin adding an existing user to a new OU, or a Microsoft Entra ID admin assigning group membership) rather than the peer-to-peer invite pattern. An extra confirmation click here is friction without a corresponding security benefit, since the actor already had permission to add *any* user (new or existing) to the group | Silent auto-add + role assignment + a clearly-worded "you've been added to [Group]" notification email (distinct copy from Welcome) so the *added user* — not the admin — gets the confirmation signal, after the fact |
| Pagination on the group-scoped Users list or Members "add existing user" table | "Best practice for tables" in the abstract | At ~17 total platform users split across a handful of groups, any single group's list is a handful of rows; pagination adds a control and a click for zero scanability benefit and actively hides rows during search-as-you-filter (paginated + client-filtered is a well-known bad combo — filtering should reveal rows a pagination boundary would otherwise hide) | One flat, filterable table; revisit only if user count grows by an order of magnitude |
| Building a new generic "cross-group user picker" component/service abstraction | Anticipates future reuse | Two of the three fixes only need a straightforward query filter and a client-side script; premature abstraction for a reuse case that doesn't exist yet | Ship the group-scoped query and the vanilla-JS filter inline where needed; extract a shared helper only if a third call site appears |
| Two separate email templates diverging significantly in tone/branding from `Welcome.razor` | Feels like it deserves distinct visual treatment | Adds template-maintenance surface for a one-year-old email system that already has 6 job/template pairs (`WelcomeEmailJob`, `ForgotPasswordEmailJob`, `ChangeEmailConfirmationJob`, `QuestFinalizedEmailJob`, `QuestDateChangedEmailJob`, `DailyReminderJob`/`SessionReminderJob`) — a 7th wildly different design is inconsistent and more to keep in sync | Copy the existing `Welcome.razor` component structure (same `IEmailRenderService.RenderAsync<T>` + `Dictionary<string,object?>` parameter pattern), swap subject/body copy and drop the `CallbackUrl`/set-password CTA since the recipient already has a password |

## Feature Dependencies

```
[Fix 1: Group-scoped Users() list]
    └──independent── no dependency on Fix 2 or Fix 3; pure query-filter change

[Fix 2: Searchable Members table + group-scoped Create User entry point]
    └──requires──> existing GroupRole/AddMemberAsync infra (already built, Phase 29/30)
    └──shares-logic-with──> Fix 3 (the new Platform "create user" action must apply the
                              same collision-handling as AdminController.CreateUser)

[Fix 3: Existing-email collision → auto-add + notification email]
    └──requires──> IIdentityService.GetIdByEmailAsync (already exists — used today in
                    AdminController.CreateUser's success path, needs to move earlier to
                    a pre-check)
    └──requires──> GroupService.AddMemberAsync (already exists, already throws
                    InvalidOperationException on duplicate membership — reuse, don't rebuild)
    └──requires──> new AddedToGroupEmailJob + email template (net-new, mirrors
                    WelcomeEmailJob's IServiceScopeFactory + IEmailRenderService pattern)
    └──consumed-by──> AdminController.CreateUser (existing, needs the pre-check added)
    └──consumed-by──> Platform/Group's new create-user action (Fix 2's entry point)
```

### Dependency Notes

- **Fix 1 is fully independent** — it's a one-line-of-intent change (add a group filter to the query) with no coupling to the other two fixes. Safe to sequence first or in parallel.
- **Fix 2's "create new user" entry point should be built to call the same collision-aware creation logic as Fix 3**, not a copy-pasted duplicate of `AdminController.CreateUser`'s current (collision-unaware) body — otherwise Fix 2 ships with the same bug Fix 3 is fixing, just in a second location. This means Fix 3's collision-handling logic should land in a shared service method (e.g. `UserService.CreateOrAddToGroupAsync(email, name, groupId, role)`) that both `AdminController.CreateUser` and the new Platform action call, rather than being written twice.
- **Fix 3's notification email is net-new infrastructure** (new Hangfire job + new Razor email component) but follows an established, low-risk pattern already proven 6 times in this codebase — low complexity in practice despite being "new code," because it's copying a template, not inventing an approach.
- **No conflicts** between any of the three fixes — they touch different controllers/views and can be planned as parallel or sequential phases without ordering constraints beyond the shared-logic note above.

## MVP Definition

All three fixes are individually small and already fully scoped by PROJECT.md — there isn't a meaningful "MVP subset" to carve out within any single fix. The MVP framing that matters here is **what NOT to add** while doing them.

### Ship With This Milestone

- [ ] Group-scoped query filter on `AdminController.Users()` — closes the tenant-leak bug
- [ ] Vanilla-JS live filter (name + email, case-insensitive substring, `input` event) on the Platform Members "add existing user" table
- [ ] Group-scoped "Create New User" entry point in the Platform area, calling the same shared creation logic as the group-admin flow
- [ ] Shared collision-aware user-creation method: existing email → auto-add to group + role + `AddedToGroupEmailJob` notification; new email → existing `CreateAsync` + `WelcomeEmailJob` path (unchanged)
- [ ] "Already a member" message reusing the existing `InvalidOperationException` catch pattern when the colliding email is already in the target group

### Explicitly Not This Milestone

- [ ] Any JS grid library — vanilla JS is sufficient and consistent with the rest of the app's stack (no existing client-side framework beyond plain Bootstrap + inline scripts, per `Users.cshtml`'s existing `deleteUser` script)
- [ ] Pagination anywhere in this flow — dataset too small to justify it
- [ ] Server-side/AJAX search — data is small enough to filter client-side against the already-rendered page
- [ ] A confirming interstitial before auto-add-on-collision — the milestone spec (and the admin-assigns-access precedent, not the peer-invite precedent) calls for silent auto-add + after-the-fact email
- [ ] Cross-group visibility banner/switcher on the group-admin `Users()` page — polish, not a gap-fix; revisit only if verification surfaces confusion

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Group-scoped `Users()` filter | HIGH (fixes a real data-leak/confusion bug) | LOW | P1 |
| Live filter on Members table | MEDIUM (usability improvement over a dropdown) | LOW | P1 |
| Platform-scoped "Create New User" entry point | HIGH (closes a real workflow gap for SuperAdmins) | MEDIUM | P1 |
| Collision → auto-add + notification email | HIGH (removes a hard-fail on an ordinary action) | MEDIUM | P1 |
| Shared creation-logic extraction (avoid duplicating collision handling) | MEDIUM (prevents Fix 2 from reintroducing Fix 3's bug) | LOW (refactor, not new logic) | P1 |
| Cross-group visibility banner | LOW | LOW | P3 |
| Match highlighting in filtered rows | LOW | LOW | P3 |
| JS grid library | NEGATIVE (adds risk/maintenance without benefit) | HIGH | Do not build |
| Confirming interstitial on collision | NEGATIVE (contradicts spec, adds friction) | MEDIUM | Do not build |

## Comparable-System Analysis

| Concern | GitHub Org Invite | Slack Workspace Invite | This App's Recommended Approach |
|---------|--------------------|-------------------------|----------------------------------|
| Existing email, adding to new org/workspace | Sends invite email; recipient must click "Accept" — explicit consent required | Blocks/flags duplicate accounts; requires the invitee to actively join or admin to resolve manually | **Silent auto-add** — no accept step, because the actor is an Admin/SuperAdmin who already has authority over the target group, not a stranger inviting across a trust boundary |
| Notification to the added user | Invite email with an actionable link (join now) | N/A (errors out rather than silently joining) | Distinct "you've been added to [Group]" email — informational, not actionable (no set-password CTA since they already have a password) |
| User list scoping | Org members list is inherently org-scoped (you can't see another org's members without being a member) | Workspace member list is workspace-scoped | Group-scoped query filter, matching the same principle — this app's bug is that it currently does NOT do what GitHub/Slack do by default |
| Add-existing-user UX | Autocomplete-style username/email search box, not a raw dropdown | Autocomplete-style email entry | Live-filtered table (functionally equivalent search-as-you-type experience, adapted to this app's existing server-rendered-then-filtered table pattern rather than an autocomplete widget, since the full list is already small enough to render up front) |

**Why the difference is correct, not a shortcut:** GitHub/Slack's explicit-consent model exists because *anyone* can attempt to invite *anyone else's* email — the friction protects the invitee from unwanted org membership. In this app, only a group Admin or platform SuperAdmin (both already vetted, both already having unilateral authority to add *any* user, new or existing, to the group) can trigger this flow at all. The authority check already happened at the `[Authorize(Policy = "AdminOnly"/"SuperAdminOnly")]` layer; adding a second consent gate for the *target* user doesn't add security, it adds friction to a routine internal-tool operation the milestone spec already decided against.

## Sources

- Direct codebase reads: `AdminController.cs`, `Areas/Platform/Controllers/GroupController.cs`, `Members.cshtml`, `Users.cshtml`, `UserService.cs`, `GroupService.cs`, `IdentityService.cs`, `WelcomeEmailJob.cs` — HIGH confidence, ground truth for current behavior and existing infra
- [Multi-tenancy — Filament docs](https://filamentphp.com/docs/3.x/panels/tenancy) — LOW confidence (single-pass web search, unverified) — tenant query-scoping pattern
- [How to design an RBAC model for multi-tenant SaaS — WorkOS](https://workos.com/blog/how-to-design-multi-tenant-rbac-saas) — LOW confidence — per-org role scoping principle
- [Inviting users to join your organization — GitHub Docs](https://docs.github.com/en/organizations/managing-membership-in-your-organization/inviting-users-to-join-your-organization) — LOW confidence (single-pass) but describes a well-known, widely-documented product behavior — explicit-consent invite semantics
- [Manage how people join your workspace — Slack](https://slack.com/help/articles/115004856503-Manage-how-people-join-your-workspace) — LOW confidence — duplicate-account handling behavior
- [How To Create a Filter/Search Table — W3Schools](https://www.w3schools.com/howto/howto_js_filter_table.asp) — LOW confidence but representative of the canonical, widely-repeated vanilla-JS table-filter pattern (corroborated independently by GeeksforGeeks, dev.to, and daily-dev-tips.com results in the same search pass)
- [How to Perform Real Time Search and Filter on HTML table — GeeksforGeeks](https://www.geeksforgeeks.org/html/how-to-perform-a-real-time-search-and-filter-on-a-html-table/) — LOW confidence, corroborating source for the same pattern

**Confidence caveat:** All external (non-codebase) findings above come from unverified single-pass web search (classified LOW by this project's confidence tooling). They are used here only to confirm well-established, low-controversy conventions (vanilla-JS table filtering, tenant query scoping, invite-vs-auto-add semantics) that are independently corroborated by multiple sources in the same search pass and align with this app's own existing architecture — not as authoritative citations for a novel or contested claim. Treat the *codebase-grounded* recommendations (what to change in `AdminController`, `GroupController`, `UserService`, the email-job pattern) as the HIGH-confidence core of this document.

---
*Feature research for: admin user-management bugfixes (v6.1)*
*Researched: 2026-07-03*
