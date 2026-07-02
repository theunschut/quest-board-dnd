# Security Audit — Phase 31: Unauthenticated Landing Redirect

**Audit date:** 2026-07-01
**ASVS Level:** 1
**Block-on policy:** critical
**Threats verified:** 11/11 (10 unique STRIDE threats + T-31-SC repeated across plans, counted once)
**Threats closed:** 11/11
**Threats open:** 0

This is the first security audit for this repository (no prior SECURITY.md existed). Verification was performed against the CURRENT state of the code on `milestone/v5-multi-tenancy`, not against plan-time descriptions, per the `<post_plan_changes>` instructions supplied with this audit (post-merge route-attribute fix in commit `997d27f`, and the CR-fix pass in commits `12b59b6`, `0ffe001`, `c2ebcb2`, `71626a7`, `a1c5d2e`).

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|--------------|--------|----------|
| T-31-01 | Information Disclosure | mitigate | CLOSED | `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs:8` and `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:8` — bare class-level `[Authorize]`. Confirmed by build + integration test `AuthenticatedUser_NoActiveGroup_ProtectedAreaRedirectsToGroupPick` (`GroupSessionMiddlewareIntegrationTests.cs:135`) which exercises `/Calendar` and `/QuestLog` unauthenticated-boundary behavior end-to-end. |
| T-31-02 | Information Disclosure | mitigate | CLOSED | `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:9` class-level `[Authorize]`; `grep -c "AllowAnonymous"` on the file returns `0` (both prior `[AllowAnonymous]` attributes removed from `Profile` and `GetDMProfilePicture`). |
| T-31-03 | Information Disclosure | mitigate | CLOSED | `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` — `HomeController : Controller` with no constructor dependencies; `Index()` returns `View()` (anonymous branch) with zero service calls and no group-scoped data. Note: post-plan the action gained an authenticated-user branch (`RedirectToAction("Index", "Quest")`) — this does not reintroduce group data into the public/anonymous render path, since the redirect carries no payload and the anonymous branch is unchanged. |
| T-31-04 | Information Disclosure | mitigate | CLOSED | `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:24-25` — `[Route("quests")]` + `[Authorize]` action-level on `Index`. Group filtering still enforced by Phase 28's EF Core global query filters (unchanged in this phase). |
| T-31-05 | Spoofing | accept | CLOSED (logged below) | `QuestBoard.Service/Views/Shared/_Layout.cshtml:24-31` — navbar brand is conditional: authenticated users get `asp-controller="Quest" asp-action="Index"`, anonymous users get `asp-controller="Home" asp-action="Index"`. Anonymous branch targets the public landing page only; no sensitive route is exposed to anonymous users via this link. |
| T-31-06 | Spoofing (open redirect) | mitigate | CLOSED | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:90-91` — redirect target is the hardcoded literal `/groups/pick`; the appended `returnUrl` query value is built from the CURRENT request's own `Path + QueryString` (line 90), never from user-supplied redirect-parameter input. Downstream, `QuestBoard.Service/Controllers/GroupPickerController.cs:53-63` (`RedirectToLocal`) validates `Url.IsLocalUrl(returnUrl)` before calling `Redirect(returnUrl)`, falling back to `RedirectToAction("Index", "Quest")` for any non-local value. **Explicitly verified per audit instructions: the WR-01 returnUrl addition does NOT reopen the open-redirect threat** — the value only ever originates from the server's own request URI, and the consuming controller independently re-validates locality before use (defense in depth). |
| T-31-07 | Elevation of Privilege / DoS (redirect loop) | mitigate | CLOSED | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:65-69` — `IsInRole("SuperAdmin")` early-out runs before the exempt-path check and before the group-null check. Exempt-path list (`ExemptPathPrefixes`, lines 41-48) is derived via `ControllerNameOf<TController>()` reflection off `GroupPickerController` and `AccountController` (lines 44-45) rather than hardcoded strings. **Explicitly verified per audit instructions**: `ControllerNameOf<GroupPickerController>()` strips the "Controller" suffix from the literal class name `GroupPickerController` → `"GroupPicker"` → `/GroupPicker`; `ControllerNameOf<AccountController>()` on the class declared in `QuestBoard.Service/Controllers/Admin/AccountController.cs:14` → `"Account"` → `/Account`. This produces the identical effective exempt set as before (`/groups/pick`, `/GroupPicker`, `/Account`, `/platform`, `/Error`). Confirmed further by the passing `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` and `GroupPickPath_NoActiveGroup_NotLooped` tests. |
| T-31-08 | Information Disclosure | mitigate | CLOSED | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:77-93` — on null `ActiveGroupId`, the middleware short-circuits with a redirect/409 before calling `next(context)`, so the group-scoped controller action never executes. |
| T-31-09 | Information Disclosure (regression) | mitigate | CLOSED | `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`, `CalendarControllerIntegrationTests.cs`, `QuestLogControllerIntegrationTests.cs`, `QuestControllerIntegrationTests_Comprehensive.cs`. Re-ran filtered suite at audit time: `Passed! - Failed: 0, Passed: 51, Total: 51`. Full suite: `Passed! - Failed: 0, Passed: 188, Total: 188`. |
| T-31-10 | DoS (redirect loop regression) | mitigate | CLOSED | `GroupSessionMiddlewareIntegrationTests.cs` — `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` and `GroupPickPath_NoActiveGroup_NotLooped` both pass in the current suite run. |
| T-31-SC | Tampering (supply chain) | accept | CLOSED (logged below) | Verified via `git show --stat` on all Phase 31 task commits (`3142edc`, `58f3e47`, `0705286`, `3963ec9`, `7b0ac54`, `7b8c766`, `ac7fda4`, `ef018a5`, `b8da010`, `e504996`, `abd6be3`, `2dfa228`, `63ae9c4`): zero `.csproj` or package-manifest files touched. |

## Post-Plan Changes Verified

Two changes were made after the original plans were executed, and both were independently re-verified against current code rather than accepted on description alone:

1. **Commit `997d27f`** (routing regression fix, documented in `31-03-SUMMARY.md` Post-Merge Amendment): `GroupPickerController.Index` carries both `[Route("groups/pick")]` and `[Route("[controller]/[action]")]` (confirmed at `GroupPickerController.cs:15-16`). This restores the conventional `/GroupPicker/Index` route without altering any auth/redirect security property — the controller remains `[Authorize]`-gated and the additional route is not a new anonymous entry point.
2. **Code-review fix pass (`12b59b6`, `0ffe001`, `c2ebcb2`, `71626a7`, `a1c5d2e`)**: verified above under T-31-06 (returnUrl mechanism does not reopen open redirect) and T-31-07 (reflection-derived exempt list produces the same effective set). Both hold.

## Unregistered Flags

None. No `## Threat Flags` section was present in any of `31-01-SUMMARY.md`, `31-02-SUMMARY.md`, `31-03-SUMMARY.md`, or `31-04-SUMMARY.md` — there is no executor-flagged new attack surface to reconcile against the threat register for this phase.

## Accepted Risks Log

| Threat ID | Description | Rationale | Accepted by |
|-----------|-------------|------------|--------------|
| T-31-05 | Anonymous users see a navbar brand link that conditionally resolves based on auth state (client-side branch in Razor, server-rendered) | The anonymous branch always resolves to the public landing page (`Home/Index`) — no sensitive or group-scoped route is exposed. Verified in code at `_Layout.cshtml:24-31`; both branches preserve the same icon/text, so there is no UI spoofing surface beyond the (intentional, non-sensitive) destination change. | Phase 31 plan 31-02 threat model (accept disposition), confirmed present in implementation |
| T-31-SC | No new external package dependencies introduced this phase | Confirmed zero `.csproj`/lockfile changes across all Phase 31 commits — supply-chain surface unchanged from pre-phase baseline. | Phase 31 plans 31-01/31-02/31-03/31-04 threat model (accept disposition), confirmed via git history |

## Verification Commands Used

```
grep -n "\[Authorize\]" QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
grep -n "\[Authorize\]" QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
grep -c "AllowAnonymous" QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
grep -n "Route(\"quests\")" QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
grep -n "UseMiddleware<GroupSessionMiddleware>" QuestBoard.Service/Program.cs
git show --stat <each Phase 31 task commit> | grep -iE "\.csproj|package\.json"
dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupSession|...CalendarController|...QuestLogController|...HomeController|...DungeonMasterController|...QuestController" --no-build
dotnet test QuestBoard.IntegrationTests --no-build   (full suite)
```

## Conclusion

All 10 STRIDE threats plus the recurring supply-chain acceptance (T-31-SC) declared across the four Phase 31 plans resolve to CLOSED. No implementation gaps found. No unregistered attack surface flagged. The two post-plan changes called out for explicit re-verification (returnUrl mechanism, reflection-derived exempt list) were independently confirmed safe against current code, not accepted on description. Phase 31 is cleared from a threat-mitigation-verification standpoint.
