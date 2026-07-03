# Project Research Summary

**Project:** D&D Quest Board — Milestone v6.1 (Bugfixes)
**Domain:** Admin/user-management bugfixes in an existing ASP.NET Core 10 MVC + EF Core + Identity multi-tenant app
**Researched:** 2026-07-03
**Confidence:** HIGH

## Executive Summary

This milestone fixes three real bugs in the existing admin/user-management surface, not new product features: (1) `AdminController.Users()` leaks every platform user into a group admin's view because `UserEntity` deliberately has no EF Core Global Query Filter (it would break Identity); (2) the Platform "add member" screen is a raw `<select>` dropdown with no group-scoped "create new user" entry point; (3) `IdentityService.CreateUserAsync` hard-fails with a raw duplicate-username error whenever an admin types an email that already exists anywhere on the platform, instead of the friendlier "add the existing user to this group" behavior. All three fixes are additive extensions of seams the codebase already has — `IGroupService.GetMembersAsync`, `IIdentityService.GetIdByEmailAsync`, `UserRepository`'s explicit-junction `.Any()` join pattern, and the existing Hangfire/`IEmailRenderService` email-job pattern — so this is a zero-new-package, zero-new-migration, mostly-reordering-existing-calls milestone.

The recommended approach is to build one new shared Domain-layer method, `IUserService.CreateOrAddToGroupAsync(email, name, groupId, role)`, once, and have both `AdminController.CreateUser` and the new Platform `GroupController.CreateMember` action call it — never duplicate the collision-branching logic per controller, which is exactly the class of tech debt this codebase's own history (`GetActiveBoardTypeAsync` implemented 3x) already warns against. The searchable Members table should be plain server-side query-string filtering (mirroring `ShopController`'s existing pattern) or a vanilla-JS client-side filter — no grid library, no AJAX, no jQuery dependency, all consistent with an app that has neither today.

The dominant risk is not technical difficulty but silent correctness/security failures that are easy to miss in manual testing: (a) a subtly wrong group-scoping join leaking PII across tenants, (b) the new Platform create-user action reaching for session `IActiveGroupContext.ActiveGroupId` instead of the route `groupId` (this exact session/route confusion bug class has already bitten this codebase twice — Phase 30's `?? 1` fallback and Phase 34.3's ownership bypass), and (c) the collision-auto-add path silently granting an existing, uninvolved user an elevated role in a group they never asked to join, with no consent step beyond a notification email. All three are flagged as requiring explicit regression tests / human-verify checkpoints, not just code review.

## Key Findings

### Recommended Stack

No new NuGet packages, npm packages, CDN scripts, or EF migrations are required. Everything is achievable with `Microsoft.AspNetCore.Identity` (`UserManager.FindByEmailAsync`/`GetIdByEmailAsync`, already wired), `EntityFrameworkCore` (the existing explicit-junction `.Any()` subquery pattern from `UserRepository.GetAllDungeonMasters`/`GetAllPlayers`), and Bootstrap 5 (existing `modern-card`/table styling, same as the Shop pages). The searchable table should copy `ShopController`'s server-side query-string pattern rather than introducing DataTables.net, Select2, Tom Select, or any client-side grid library — none of which exist anywhere in this codebase today.

**Core technologies:**
- `UserManager<UserEntity>.FindByEmailAsync` (via existing `IdentityService.GetIdByEmailAsync`) — email-collision pre-check before `CreateAsync`, avoids brittle `IdentityResult.Errors` code-matching
- EF Core explicit-junction `.Any()` subquery (`DbContext.UserGroups.Any(ug => ...)`) — group-scoped user queries, mirrors `GetAllDungeonMasters`/`GetAllPlayers` exactly, translates to efficient WHERE EXISTS
- Bootstrap 5 + server-side query-string filtering — searchable Members table, same paradigm as `ShopController`, zero new JS dependencies

### Expected Features

**Must have (table stakes — closes the three PROJECT.md gaps):**
- Group-scoped `AdminController.Users()` — filter by `ActiveGroupId`, closing a real tenant-isolation/PII-leak bug
- Searchable/filterable table replacing the raw select dropdown on the Platform Members "add existing user" screen (name + email substring match)
- Group-scoped "Create New User" entry point in the Platform area, parameterized by route `groupId` (SuperAdmin has no session `ActiveGroupId`)
- Shared collision-aware creation logic: existing email → auto-add to group + role + distinct "added to group" notification email; new email → unchanged `CreateAsync` + `WelcomeEmailJob` path
- "Already a member of this group" friendly no-op message (reusing `GroupRepository.AddMemberAsync`'s existing `InvalidOperationException`), not a duplicate-membership error

**Should have (adjacent polish, not required):**
- Clear, visibly distinct success messaging for "created new account" vs. "added existing user to group"
- No-results state on the searchable table, ideally linking to the create-user entry point

**Defer / do not build:**
- Any JS grid/DataTables library, AJAX search endpoint, or Select2/Choices.js-style enhanced dropdown
- Pagination anywhere in this flow
- A confirming interstitial before auto-add-on-collision — the milestone spec deliberately calls for silent auto-add + after-the-fact notification
- Cross-group visibility banner/switcher on the group-admin `Users()` page — polish, revisit only if verification surfaces confusion

### Architecture Approach

All three fixes are additive extensions of existing seams — no new layer, dispatcher, or cross-cutting abstraction needed. `IGroupService.GetMembersAsync(groupId)` already does the exact join Feature 1 needs (and also eliminates the current N+1 `GetGroupRoleByIdAsync` loop as a side benefit) — use it instead of inventing a new `IUserService.GetByGroupIdAsync`, which would duplicate an existing method. The one genuinely new piece of business logic is `IUserService.CreateOrAddToGroupAsync`, which must live in the Domain layer (not either controller) since both `AdminController` and `Platform/GroupController` need identical branching and Domain is the only layer both already depend on.

**Major components:**
1. `IGroupService.GetMembersAsync` (existing, reused) — group-scoped user+role query for `AdminController.Users()`, replacing unfiltered `GetAllAsync()` + N+1 loop
2. `IUserService.CreateOrAddToGroupAsync` (new, Domain layer) — shared create-or-add branching logic; both `AdminController.CreateUser` and the new `Platform/GroupController.CreateMember` call it, branching only on the result to pick which email job to enqueue
3. `GroupMembershipAddedEmailJob` + `AddedToGroup.razor` (new, Service layer) — dedicated email/job pair mirroring `ChangeEmailConfirmationJob`'s direct-`IBackgroundJobClient`-enqueue pattern (no dispatcher abstraction)
4. `Platform/GroupController` constructor (modified) — add `IIdentityService` and `IBackgroundJobClient`, mirroring `AdminController`'s existing shape; route `groupId` (`id`) is the only source of group context, `IActiveGroupContext` must never be injected here

Suggested build order: (1) `CreateOrAddToGroupAsync` built and unit-tested in isolation first, (2) new email job/template built in parallel, (3) Feature 1's `Users()` rescoping (fully independent, smallest blast radius), (4) rewire `AdminController.CreateUser` onto the shared method and verify against its existing coverage, (5) build the new Platform `CreateMember` action + searchable-table view last, since it depends on the shared method being proven correct in step 4.

### Critical Pitfalls

1. **Group-scoping join subtly wrong / omitted** — `UserEntity` has no Global Query Filter (breaks Identity if added), so every group-scoped user query must be a manual explicit join; mirror `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` exactly and add a regression test seeding two groups with disjoint users asserting zero cross-group leakage.
2. **Platform create-user action reads session ActiveGroupId instead of route groupId** — the natural copy-paste-from-`AdminController` mistake; this exact session/route confusion bug class has already caused two prior incidents (Phase 30, Phase 34.3). Structural guardrail: never inject `IActiveGroupContext` into `GroupController` at all.
3. **Silent privilege escalation on email collision** — auto-adding an existing user to a group with an admin-selected role, with no consent step, can grant Admin/DM access to someone with no relationship to that group. Must distinguish and test all three cases (new email; existing email not yet a member; existing email already a member).
4. **Email collision check bypasses UserManager's normalized lookup** — always resolve via `IIdentityService.GetIdByEmailAsync`/`FindByEmailAsync`; never write a new raw `Email == ...` EF query.
5. **Wrong email variant sent** — extending `WelcomeEmailJob`'s `isNewAccount` bool with a third mode risks accidentally including a SetPassword/CallbackUrl token in a notification meant for a user who already has a password. Build a dedicated `GroupMembershipAddedEmailJob`/`AddedToGroup.razor` with no callback-URL parameter at all.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Group-Scoped User List
**Rationale:** Fully independent of the other two fixes, smallest blast radius, pure read-path swap — safest to build/ship first.
**Delivers:** `AdminController.Users()` scoped to `ActiveGroupId` via `IGroupService.GetMembersAsync`, eliminating both the PII leak and the existing N+1 `GetGroupRoleByIdAsync` loop.
**Addresses:** Group-scoped user list (table stakes)
**Avoids:** Pitfall 1 (unfiltered `GetAllAsync()` leak) — ship with a cross-group-isolation regression test.

### Phase 2: Shared Collision-Aware User Creation (Domain Logic + Email)
**Rationale:** This is the shared seam both controllers will depend on; build and unit-test it before either controller consumes it.
**Delivers:** `IUserService.CreateOrAddToGroupAsync`, the new `GroupMembershipAddedEmailJob` + `AddedToGroup.razor` email component, and `AdminController.CreateUser` rewired onto the shared method.
**Addresses:** Existing-email-collision auto-add-to-group + distinct notification email (table stakes)
**Uses:** `IIdentityService.GetIdByEmailAsync` (pre-check reuse), `IUserService.SetGroupRoleAsync` (upsert semantics), `GroupRepository.AddMemberAsync`'s existing `InvalidOperationException` pattern
**Avoids:** Pitfall 3 (silent privilege escalation — needs explicit UAT/human-verify), Pitfall 4 (email lookup bypassing UserManager), Pitfall 5 (wrong email variant / stray password-reset token)

### Phase 3: Platform Searchable Members Table + Group-Scoped Create-User Entry Point
**Rationale:** Built last because it depends on Phase 2's shared method being proven correct via Phase 2's real-world usage in `AdminController`.
**Delivers:** Replaces the select dropdown with a server-side or vanilla-JS filtered table; adds `Platform/GroupController.CreateMember` (GET+POST), calling the same `CreateOrAddToGroupAsync` from Phase 2 with `groupId` sourced strictly from the route.
**Addresses:** Searchable/filterable Members table + create-user entry point (table stakes)
**Avoids:** Pitfall 2 (session ActiveGroupId vs. route groupId confusion — the highest-recurrence bug class in this codebase's history).

### Phase Ordering Rationale

- Phase 1 first: zero dependency on the other two fixes, lowest-risk place to prove the pattern works.
- Phase 2 before Phase 3: the collision-handling logic must be built and validated once against an existing, already-tested caller before a second caller is added — this directly avoids Pitfall 3's "duplicated logic diverges" failure mode, which this codebase has already paid down once before (`GetActiveBoardTypeAsync` 3x duplication).
- Phase 3 last: its new controller action is the one place most likely to reintroduce the session/route confusion bug (Pitfall 2), so it benefits from being built only after the shared creation logic is stable.

### Research Flags

Phases likely needing deeper research during planning:
- None expected to need --research-phase — all three phases are grounded in HIGH-confidence direct codebase reads with concrete existing patterns to copy.

Phases with standard patterns (skip research-phase):
- **Phase 1:** Direct copy of `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` pattern; well understood.
- **Phase 2:** Direct copy of `ChangeEmailConfirmationJob`/`WelcomeEmailJob`'s Hangfire job pattern and `GroupController.AddMember`'s `InvalidOperationException` handling; well understood.
- **Phase 3:** Direct copy of `ShopController`'s server-side query-string filter pattern and `AdminController`'s constructor/action shape; well understood.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified against official Microsoft Identity/EF Core docs plus direct codebase inspection; zero new dependencies needed |
| Features | MEDIUM | Codebase analysis is HIGH confidence; external comparable-system corroboration (GitHub/Slack invite semantics, vanilla-JS filter patterns) is LOW confidence single-pass web search |
| Architecture | HIGH | All findings grounded in direct reads of actual interfaces/controllers in this repo, cross-checked against PROJECT.md's Key Decisions and Known Issues log |
| Pitfalls | HIGH | Grounded directly in this codebase's source and its own documented history of the same bug class recurring across Phases 30, 34.3, 37 |

**Overall confidence:** HIGH

### Gaps to Address

- **Role-dropdown behavior on collision (Pitfall 3):** Whether the role dropdown should default/lock to Player when an email collision is detected, vs. trusting whatever the admin pre-selected, needs an explicit product decision — not resolved by research. Should be settled during Phase 2 planning/discussion, documented as a Key Decision.
- **Presentation choice for the searchable table (server-side query-string vs. vanilla-JS client-side filter):** STACK.md recommends server-side (mirrors Shop, scales better); FEATURES.md's table-stakes section leans vanilla-JS client-side (simpler, sufficient at 17 users). Resolve during Phase 3 planning.
- **Whether CreateGroupMemberViewModel should be a new type or a reused CreateUserViewModel:** an open choice depending on whether the two create-user views can share a partial — a Phase 3 implementation detail, not a blocking gap.

## Sources

### Primary (HIGH confidence)
- Direct codebase reads: AdminController.cs, Areas/Platform/Controllers/GroupController.cs, IdentityService.cs, UserRepository.cs, GroupRepository.cs, UserService.cs, GroupService.cs, QuestBoardContext.cs, WelcomeEmailJob.cs, ChangeEmailConfirmationJob.cs, Welcome.razor, ShopController.cs/ShopRepository.cs, various ViewModels
- .planning/PROJECT.md — v6.1 milestone scope, Key Decisions log, Known Issues
- UserManager FindByEmailAsync — Microsoft Learn official API docs
- Introduction to Identity on ASP.NET Core — Microsoft Learn official docs (ASP.NET Core 10)
- dotnet/aspnetcore UserManager.cs source — GitHub primary source
- Many-to-many relationships — EF Core, Microsoft Learn official docs

### Secondary (MEDIUM confidence)
- dotnet/efcore#21082 — Join with query filter causes nested subquery and poor query performance — informs "avoid Include() across Global Query Filters" caution

### Tertiary (LOW confidence)
- Filament docs — Multi-tenancy query-scoping principle, unverified single-pass search
- WorkOS blog — RBAC model for multi-tenant SaaS, per-org role scoping principle
- GitHub Docs — Inviting users to join your organization, explicit-consent invite semantics comparison
- Slack Help — Manage how people join your workspace, duplicate-account handling comparison
- W3Schools — How To Create a Filter/Search Table, vanilla-JS table-filter pattern, corroborated by GeeksforGeeks/dev.to

---
*Research completed: 2026-07-03*
*Ready for roadmap: yes*
