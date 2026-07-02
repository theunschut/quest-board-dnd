---
phase: 31-unauthenticated-landing-redirect
verified: 2026-07-01T12:00:00Z
status: passed
score: 19/19 must-haves verified
overrides_applied: 0
---

# Phase 31: Unauthenticated Landing Redirect Verification Report

**Phase Goal:** Unauthenticated visitors are redirected to login (not shown empty/broken group-scoped pages), a public landing page lives at `/`, the quest board moves to `/quests`, and authenticated users with an expired group session are seamlessly recovered to the group picker.
**Verified:** 2026-07-01T12:00:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Unauthenticated GET /Calendar returns 302 | VERIFIED | `CalendarController` carries class-level `[Authorize]` (line 8); `CalendarControllerIntegrationTests.Index_WhenNotAuthenticated_ShouldRedirect` asserts `Redirect/Found/Unauthorized`; filtered test run green (16/16) |
| 2 | Unauthenticated GET /QuestLog returns 302 | VERIFIED | `QuestLogController` carries class-level `[Authorize]` (line 8); analogous integration test present and green |
| 3 | Unauthenticated GET /DungeonMaster/Profile/{id} returns 302 | VERIFIED | Both `[AllowAnonymous]` attributes removed from `Profile` and `GetDMProfilePicture` in `DungeonMasterController.cs` (0 occurrences); `Profile_WhenNotAuthenticated_ShouldRedirect` test asserts redirect/unauthorized |
| 4 | GET / returns 200 for unauthenticated user with a Log In button, no quest cards | VERIFIED | `HomeController.Index` returns `View()` for unauthenticated users (no `[Authorize]`, no service deps); `Views/Home/Index.cshtml`/`.Mobile.cshtml` contain "Log In" anchor to `Account/Login`, no `@model`, no quest markup; `Index_ShouldContainLoginButton` and `Index_WithQuests_ShouldNotDisplayQuestList` tests assert this even with quests seeded |
| 5 | GET /quests returns the quest board for an authenticated user | VERIFIED | `QuestController.Index` decorated `[HttpGet][Route("quests")][Authorize]`, migrated `GetQuestsWithSignupsForRoleAsync` logic verbatim; `Index_Quests_Authenticated_ReturnsOk` test (seeds a quest, asserts 200) is green |
| 6 | Authenticated visitor hitting / lands on the quest board, not the landing page | VERIFIED | `HomeController.Index`: `User.Identity?.IsAuthenticated == true ? RedirectToAction("Index","Quest") : View()` — this is the checkpoint-driven fix (commit `63ae9c4`), confirmed present in current code and re-verified by the human tester per SUMMARY 31-04 |
| 7 | Every in-app link that pointed to the old quest-board home now points to /quests | VERIFIED | Navbar brand (`_Layout.cshtml`/`.Mobile.cshtml`) conditional Quest/Index (auth) vs Home/Index (anon); `Quest/Create` Cancel buttons use `Url.Action("Index","Quest")`; `Account/Profile` back-to-board buttons use `asp-controller="Quest"`; `GroupPickerController.RedirectToLocal` fallback uses `RedirectToAction("Index","Quest")`; zero remaining `RedirectToAction("Index","Home")` in QuestController/GroupPickerController |
| 8 | Genuine public-landing links (logout, access-denied, platform-exit) still resolve to / | VERIFIED | `AccountController.Logout`/`RedirectToLocal`, `AccessDenied.cshtml`, and Platform layout exit link were confirmed unchanged per 31-02-SUMMARY and left out of the sweep |
| 9 | Authenticated non-SuperAdmin user with no active group is redirected to /groups/pick | VERIFIED | `GroupSessionMiddleware.InvokeAsync` — anonymous passthrough, then SuperAdmin passthrough, then exempt-path passthrough, then `groupContext.ActiveGroupId == null` → hardcoded `Response.Redirect("/groups/pick")`; `AuthenticatedUser_NoActiveGroup_RedirectsToGroupPick` test is green and asserts `Location` contains `/groups/pick` |
| 10 | SuperAdmin with no active group is NOT redirected (no loop) | VERIFIED | `IsInRole("SuperAdmin")` early-out precedes the group-null check; `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` test green |
| 11 | /groups/pick itself is never redirected by the middleware | VERIFIED | `ExemptPathPrefixes` includes `/groups/pick` and `/GroupPicker`; `GroupPickPath_NoActiveGroup_NotLooped` test green |
| 12 | Authenticated user with an active group reaches the page normally | VERIFIED | `AuthenticatedUser_WithActiveGroup_ReachesPage` test asserts 200 with `ActiveGroupId = 1` |
| 13 | GroupPickerController.Index is reachable at /groups/pick | VERIFIED | `[Route("groups/pick")]` present on `Index`; conventional `/GroupPicker/Index` route restored via `[Route("[controller]/[action]")]` (post-merge regression fix, commit `997d27f`) — both confirmed present in current file |
| 14 | Middleware registered after UseAuthentication, before UseAuthorization, outside Testing-only guard | VERIFIED | `Program.cs` line 175 `UseAuthentication()`, line 176 `UseMiddleware<GroupSessionMiddleware>()`, line 177 `UseAuthorization()`; Testing guard begins at line 179 (after registration) |
| 15 | Redirect target is hardcoded literal, not user-supplied (open-redirect mitigation) | VERIFIED | Literal `"/groups/pick"` string passed to `Response.Redirect`; no request-derived value used |
| 16 | Full integration suite is green after Phase 31 behavior changes | VERIFIED | Independently re-ran: `dotnet test` → 55 unit + 181 integration = 236 tests, 0 failures (matches SUMMARY claim exactly) |
| 17 | dotnet build succeeds with zero errors | VERIFIED | Independently re-ran: `dotnet build` → 0 errors, 4 pre-existing unrelated NU1510 warnings |
| 18 | No [AllowAnonymous] added or [Authorize] weakened to force tests green | VERIFIED | Grep for `AllowAnonymous` in `DungeonMasterController.cs` returns 0; no controller in this phase had `[Authorize]` removed |
| 19 | Human-verify checkpoint (landing page, /quests routing, session recovery) approved | VERIFIED | Checkpoint was executed live in this session (per task context) and approved after the authenticated-visitor-at-/ fix; fix confirmed present in `HomeController.cs` |

**Score:** 19/19 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` | Class-level `[Authorize]` | VERIFIED | Line 8, bare `[Authorize]` |
| `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` | Class-level `[Authorize]` | VERIFIED | Line 8, bare `[Authorize]` |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | No `[AllowAnonymous]` | VERIFIED | 0 occurrences; class-level `[Authorize]` intact; `EditProfile` policy attrs unchanged |
| `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` | Public landing + auth redirect | VERIFIED | No DI deps, no `[Authorize]`; conditional redirect for authenticated users |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` | `/quests` route with migrated logic | VERIFIED | `[Route("quests")]` + `[Authorize]` action-level; `GetQuestsWithSignupsForRoleAsync` call present |
| `QuestBoard.Service/Views/Home/Index.cshtml` + `.Mobile.cshtml` | Landing page, Log In button, no model | VERIFIED | No `@model`, "Log In" anchor to Account/Login present in both |
| `QuestBoard.Service/Views/Quest/Index.cshtml` + `.Mobile.cshtml` | Migrated quest board views | VERIFIED | `@model IEnumerable<Quest>` present, poster-card markup intact |
| `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` | Session-recovery middleware | VERIFIED | `InvokeAsync` present; correct guard order; resolves `IActiveGroupContext` via `RequestServices` |
| `QuestBoard.Service/Program.cs` | Middleware registered correctly | VERIFIED | Between `UseAuthentication`/`UseAuthorization`, outside Testing guard |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` | `/groups/pick` route + fallback to /quests | VERIFIED | `[Route("groups/pick")]` + `[Route("[controller]/[action]")]`; `RedirectToLocal` targets `Index, "Quest"` |
| `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` | 4 middleware behavior tests | VERIFIED | All 4 tests present, substantive assertions, green |
| `QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs` | Landing-page tests replacing quest-display tests | VERIFIED | `Index_ShouldContainLoginButton`, `Index_WithQuests_ShouldNotDisplayQuestList` present and green |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| CalendarController | ASP.NET Identity auth challenge | `[Authorize]` class attribute | WIRED | 302/401 confirmed by test + attribute inspection |
| QuestController.Index | Views/Quest/Index.cshtml | `return View(quests)` | WIRED | Confirmed in source (line 56) |
| QuestController.Index | IQuestService.GetQuestsWithSignupsForRoleAsync | migrated service call | WIRED | Confirmed in source (line 52) |
| Views/Shared/_Layout navbar brand | /quests (auth) or / (anon) | conditional `asp-controller` | WIRED | Both branches confirmed present in `_Layout.cshtml` and `_Layout.Mobile.cshtml` |
| GroupSessionMiddleware | IActiveGroupContext.ActiveGroupId | `RequestServices.GetRequiredService` | WIRED | Confirmed in source; no direct `context.Session` read |
| Program.cs pipeline | GroupSessionMiddleware | `UseMiddleware<GroupSessionMiddleware>()` | WIRED | Confirmed positioned correctly, outside Testing guard |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds | `dotnet build` | 0 errors, 4 pre-existing unrelated warnings | PASS |
| Full test suite green | `dotnet test` | 55 unit + 181 integration, 0 failures | PASS |
| Calendar/QuestLog filtered tests green | `dotnet test --filter "FullyQualifiedName~CalendarController\|FullyQualifiedName~QuestLogController"` | 16/16 passed | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description (per REQUIREMENTS.md) | Status | Evidence |
|-------------|------------|-------------------------------------|--------|----------|
| UX-01 | 31-02 (declared) | "User belonging to exactly one group is automatically redirected to that group's content after login (no picker shown)" | ALREADY SATISFIED — Phase 30 | REQUIREMENTS.md marks UX-01 `[x] Complete` under **Phase 30**, not Phase 31. This requirement's actual text describes Phase 30's single-group auto-pick behavior, unrelated to Phase 31's landing/quests split. The plan's `requirements:` frontmatter field reused this ID; it does not describe new Phase 31 work. |
| UX-04 | 31-01, 31-03, 31-04 (declared) | "Active group is stored in ASP.NET Core Session per request; selected group persists across requests until session expires or user exits" | ALREADY SATISFIED — Phase 30 | Same discrepancy: REQUIREMENTS.md attributes UX-04 to Phase 30 with different subject matter (session persistence of group selection), not Phase 31's auth-lockdown/session-recovery work. |

**Note on requirement ID mismatch:** ROADMAP.md's Phase 31 header line (`**Requirements**: UX-01, UX-04`) and all four PLAN.md frontmatter blocks declare `UX-01`/`UX-04`, but REQUIREMENTS.md's traceability table maps both IDs to Phase 30 and describes different functionality than what Phase 31 built. No REQUIREMENTS.md entry exists that specifically names Phase 31's actual deliverables (auth lockdown, landing/quests split, session-recovery middleware) — these are captured only as CONTEXT.md design decisions D-01 through D-11, which are not requirement IDs. This is a requirements-documentation labeling defect carried from planning, not evidence the phase's actual work is incomplete — the phase's ROADMAP-defined goal (verified above, truths 1-19) is fully and independently achieved regardless of which requirement ID is attached to it. Flagged as WARNING for documentation hygiene; does not block phase completion since the goal itself is the primary verification contract and it passes.

### Anti-Patterns Found

None. No `TBD`/`FIXME`/`XXX`/`TODO`/`PLACEHOLDER` markers found in any Phase 31–modified controller, middleware, or view file. No stub returns (`return null`, empty handlers) found in the reviewed artifacts.

### Advisory Findings (Non-Blocking, Already Reviewed)

A code review (`31-REVIEW.md`) already ran against this phase and found 2 critical + 5 warning + 3 info findings:
- **CR-01:** Non-GET requests mid-submission can be redirected by `GroupSessionMiddleware`, silently discarding POST body data on session expiry.
- **CR-02:** The middleware's exempt-path list is a hand-maintained string array with no compile-time tie to `[AllowAnonymous]` attributes — a future edit could silently under- or over-exempt routes.
- 5 warnings (no returnUrl preservation through the picker redirect, missing zero-group-membership test case, duplicated path literals, HomeController mixing responsibilities, tests bypassing the real middleware flow for full round-trip coverage).

Per the task's explicit instruction, these are advisory/non-blocking per project convention and do not affect this PASS/FAIL determination — none represent a must-have the phase explicitly promised (the phase's stated goal is about redirect behavior and route relocation, not about session-expiry POST-safety or self-enforcing route-attribute consistency, which are reasonable follow-up hardening items). They are preserved here for visibility ahead of any future security-hardening phase.

### Human Verification Required

None outstanding. The single planned human-verify checkpoint (31-04 Task 5: landing page, /quests routing, navbar brand, single-group session recovery, Calendar/QuestLog auth boundary) was executed live in this session per the task context, found one UX gap (authenticated visitor seeing the logged-out landing page at `/`), which was fixed by the orchestrator (`HomeController.Index` redirect, commit `63ae9c4`) and re-verified by the human as working. This verifier independently confirmed the fix is present in the current codebase (see Truth #6).

### Gaps Summary

No gaps. All 19 derived truths (covering all 4 plans' must_haves, merged with the ROADMAP.md phase goal) are independently verified against the actual codebase — not merely asserted by SUMMARY.md. Build and full test suite were independently re-executed and confirmed green (236/236 tests passing), matching the SUMMARY's claims exactly. The one requirements-traceability issue (UX-01/UX-04 already attributed to Phase 30 with different subject matter) is a documentation-labeling defect, not a functional gap, and is surfaced as a WARNING for future requirements-doc cleanup rather than a blocker.

---

_Verified: 2026-07-01T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
