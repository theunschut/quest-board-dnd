---
phase: 40-platform-members-page-redesign
audited: 2026-07-04
asvs_level: default (not configured by phase — standard baseline applied)
block_on: default (open threats block; unregistered flags are advisory)
threats_total: 12
threats_closed: 12
threats_open: 0
unregistered_flags: 1
status: SECURED
---

# Phase 40: Security Audit — Platform Members Page Redesign

Independent verification of every threat declared across `40-01-PLAN.md`, `40-02-PLAN.md`, and `40-03-PLAN.md`'s `<threat_model>` blocks, plus the orchestrator-added T-40-11 (new attack surface introduced by a mid-checkpoint scope addition in Plan 03). Every mitigation claim below was verified against the actual implementation files (grep + direct read) — code review commentary and SUMMARY.md self-reports were treated as leads, not evidence.

## Threat Verification

| Threat ID | Category | Disposition | Verification Method | Evidence | Result |
|-----------|----------|-------------|---------------------|----------|--------|
| T-40-01 | Elevation of Privilege | mitigate | grep for `activeGroupContext` inside `GetAvailableUsers` method body | `QuestBoard.Repository/UserRepository.cs:62-75` — `GetAvailableUsers(int groupId, string? search, CancellationToken token)` takes `groupId` as a plain parameter; method body contains zero reference to `activeGroupContext` (the field exists on the class for `GetAllDungeonMasters`/`GetAllPlayers`, lines 20-48, but is never read here) | CLOSED |
| T-40-02 | Tampering (SQL injection) | accept | grep for raw SQL / string concatenation in the search predicate | `QuestBoard.Repository/UserRepository.cs:70` — `u.Name.Contains(search) \|\| (u.Email != null && u.Email.Contains(search))`, a plain EF Core LINQ predicate; no `FromSqlRaw`, `ExecuteSqlRaw`, or string interpolation anywhere in the query | CLOSED |
| T-40-03 | Information Disclosure | accept | grep for class-level authorization policy on `GroupController` | `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:18` — `[Authorize(Policy = "SuperAdminOnly")]` at class level, applies to all actions including `Members` | CLOSED |
| T-40-04 | Elevation of Privilege | mitigate | grep for `IActiveGroupContext`/`activeGroupContext` in full file; confirm `CreateMember` uses route `id` | `GroupController.cs` — full-file grep for `IActiveGroupContext\|activeGroupContext` returns **zero matches**; `CreateMember(int id, CreateMemberViewModel model, ...)` (line 167) passes `id` directly into `CreateOrAddToGroupAsync(model.Email, model.Name, id, model.GroupRole)` (line 187) | CLOSED |
| T-40-05 | Tampering/Spoofing (CSRF) | mitigate | grep for `[ValidateAntiForgeryToken]` above `AddMember`/`CreateMember` | `GroupController.cs:144` (`AddMember`) and `:166` (`CreateMember`) both carry `[ValidateAntiForgeryToken]` immediately above the action | CLOSED |
| T-40-06 | Elevation of Privilege | accept | grep for class-level `[Authorize(Policy = "SuperAdminOnly")]`; confirm no per-group ownership check exists (by design) | `GroupController.cs:18` class-level attribute confirmed; no per-group ownership guard present anywhere in the controller, consistent with the accepted-risk disposition | CLOSED |
| T-40-07 | Input Validation | mitigate | grep for `[Required]`/`[EmailAddress]`/`[StringLength]` on `CreateMemberViewModel`; confirm `ModelState.IsValid` gate | `QuestBoard.Service/ViewModels/PlatformViewModels/CreateMemberViewModel.cs:8-16` — `[Required][EmailAddress]` on `Email`, `[Required][StringLength(100)]` on `Name`; `GroupController.cs:172` — `if (!ModelState.IsValid)` re-renders the view before `CreateOrAddToGroupAsync` is called | CLOSED |
| T-40-08 | Tampering (XSS) | mitigate | grep for `Html.Raw` in both views; confirm `@Model.SearchQuery` uses plain Razor expression | `Members.cshtml:140` (`value="@Model.SearchQuery"`) and `:200` (`No users match "@Model.SearchQuery"`); zero `Html.Raw` matches in `QuestBoard.Service/Areas/Platform/Views/Group/` (both files) | CLOSED |
| T-40-09 | Tampering/Spoofing (CSRF) | mitigate | grep for `asp-antiforgery`/`@Html.AntiForgeryToken()` on per-row Add form and modal form | 4 occurrences of `asp-antiforgery="true"` across `Members.cshtml`/`Members.Mobile.cshtml` (per-row Add + Remove forms); `@Html.AntiForgeryToken()` present in both `#createMemberModal` blocks (`Members.cshtml:216`, `Members.Mobile.cshtml:186`); both post to `[ValidateAntiForgeryToken]`-guarded actions (T-40-05 evidence) | CLOSED |
| T-40-10 | Elevation of Privilege | mitigate | grep for `asp-route-id="@Model.Group.Id"` on modal form; confirm route-sourced `groupId` in controller; confirm blocking checkpoint was executed and approved | Modal form targets `asp-route-id="@Model.Group.Id"` (`Members.cshtml:215`, `Members.Mobile.cshtml:185`); `CreateMember` sources `id` from route (T-40-04 evidence); `40-03-SUMMARY.md` documents the blocking human-verify checkpoint was executed across 3 round-trips and approved ("yes, looks good"), and an integration test `CreateMember_PostedToSecondGroup_ShouldScopeMembershipToRouteGroupId` independently proves route-scoping to a second group | CLOSED |
| T-40-11 | Tampering (XSS) — orchestrator-added, new field from mid-checkpoint scope expansion | mitigate (claimed — independently re-verified, not assumed by analogy to T-40-08) | grep for `Html.Raw` near `@Model.MemberSearchQuery`; confirm plain Razor expression | `Members.cshtml:56` (`value="@Model.MemberSearchQuery"`) and `:127` (`No members match "@Model.MemberSearchQuery"`); same file already confirmed zero `Html.Raw` matches (T-40-08 evidence covers the whole file) — verified directly against this specific field, not inferred from T-40-08's pattern | CLOSED |
| T-40-SC | Tampering (supply chain) | mitigate | check `tech-stack.added` front-matter in all 3 SUMMARY.md files | `40-01-SUMMARY.md`, `40-02-SUMMARY.md`, `40-03-SUMMARY.md` — all three declare `tech-stack: added: []`; no `npm install`/`dotnet add package` commands appear in any task action | CLOSED |

**Result: 12/12 threats CLOSED. 0 OPEN.**

## Unregistered Flags

### UF-01: `GetMembersAsync` "Current Members" search — new EF `.Contains()` attack surface with no SQL-injection-category threat entry

**Introduced by:** `40-03-SUMMARY.md`, checkpoint-driven scope addition #2 ("Server-side search on Current Members"), commits `fcc820f`/`ad84a76`. Not present in `40-03-PLAN.md`'s original `<threat_model>` — the orchestrator only added T-40-11 to cover the XSS angle of the new `MemberSearchQuery` field.

**Gap:** `GroupRepository.GetMembersAsync(int groupId, string? search, ...)` (`QuestBoard.Repository/GroupRepository.cs:88-101`) introduces a second, independent user-controlled EF `.Contains()` predicate (`ug.User!.Name.Contains(search) || (ug.User!.Email != null && ug.User!.Email.Contains(search))`) analogous to the one covered by T-40-02 (accept disposition, "parameterized via EF Core, no raw SQL — no additional mitigation needed"). This new query path was never given its own STRIDE entry for the Tampering/SQL-injection category — T-40-11 only registers the XSS reflection risk for `MemberSearchQuery`, not the injection risk for the query itself.

**Verification performed anyway (informational, not a blocker):** Read `GroupRepository.cs:94-97` directly — the `memberSearch` predicate is a plain EF Core LINQ `.Where()` clause with no raw SQL, string concatenation, or `FromSqlRaw`/`ExecuteSqlRaw` calls, structurally identical to the T-40-02-accepted pattern. No injection vector found. This is logged as a documentation/threat-register gap, not a code defect — the phase's own code review (`40-REVIEW.md`, WR-02) independently flagged the duplicated predicate logic between `GroupRepository` and `UserRepository` as a maintainability concern.

**Recommendation:** Retroactively add a threat entry (or an addendum to T-40-02) covering `GetMembersAsync`'s search predicate for future audits, so the next phase that touches this query has an explicit disposition to verify against rather than relying on analogy to a sibling method's accepted risk.

## Accepted Risks Log

The following threats carry an `accept` disposition per the phase's threat model — recorded here as the permanent accepted-risk log this SECURITY.md maintains going forward:

- **T-40-02** — SQL injection risk on the `search`/`GetAvailableUsers` predicate is accepted because EF Core parameterizes all LINQ `.Where()` clauses; no raw SQL is used anywhere in this phase's query code.
- **T-40-03** — The available-users query returning all platform non-members (rather than some narrower slice) is accepted by design: the Platform Members page is `SuperAdminOnly`, and SuperAdmins are trusted to see and manage all platform users.
- **T-40-06** — Crafted POSTs to `CreateMember`/`AddMember` for an arbitrary `groupId` (any group a SuperAdmin didn't "open" first) are accepted by design: `GroupController` is `SuperAdminOnly` at the class level, and SuperAdmins are trusted to manage any group on this platform surface with no per-group ownership check.

## Notes on Verification Rigor

- Every `mitigate` disposition was checked with a direct grep/read against the cited implementation file — not accepted from `40-REVIEW.md` or SUMMARY.md self-reports, though those documents' independent conclusions were consistent with what this audit found in the code.
- T-40-11 was explicitly re-verified against `MemberSearchQuery`'s own call sites rather than assumed to be covered "by analogy" to T-40-08, per this audit's adversarial-stance instructions.
- The `IActiveGroupContext` full-file grep (T-40-04) was re-run directly in this audit (zero matches) rather than trusting the `40-02-SUMMARY.md`/`40-03-SUMMARY.md` self-reported grep counts.
- `IUserRepository`'s constructor still injects `IActiveGroupContext` (used by `GetAllDungeonMasters`/`GetAllPlayers`, unrelated pre-existing methods) — this does not affect `GetAvailableUsers`, which never reads it. Confirmed by direct method-body read, not inferred from the class-level constructor signature.

---
*Audited: 2026-07-04*
*Auditor: gsd-security-auditor*
