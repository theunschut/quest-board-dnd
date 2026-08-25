---
phase: 72
slug: change-character-on-an-existing-signup
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-08-25
updated: 2026-08-25
---

# Phase 72 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 (integration) · xunit.v3 3.2.2 + NSubstitute 5.3.0 (unit) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` · `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (no separate `xunit.runner.json`) |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UpdateSignupCharacter"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick ~15–30 s · full suite ~2–4 min |

**Build note:** if `dotnet build`/`dotnet test` fails on locked output files, Visual Studio is running the app under the debugger — stop it (Shift+F5) before retrying (CLAUDE.md).

---

## Sampling Rate

- **After every task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~UpdateSignupCharacter"`
- **After every plan wave:** `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds (quick filter)

Sampling continuity: **12 tasks across 4 plans, every one carrying an automated verify command**, so the "3 consecutive tasks without automated verify" rule cannot be breached.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 72-01-T1 | 01 | 1 | SIGNCHAR-01/03/05/06/07 | T-72-01, T-72-05 | Another user's character in the same board is rejected; the signup is always resolved from the authenticated user, never from a posted id | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestUpdateSignupCharacterTests"` | W0 — creates `QuestUpdateSignupCharacterTests.cs` | pending |
| 72-01-T2 | 01 | 1 | SIGNCHAR-03/04/05/06/07 | T-72-02, T-72-06 | Explicit board-scope comparison backstops the entity query filter; the widened list stays owner- and board-scoped | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController\|FullyQualifiedName~UpdateSignupCharacter"` | exists via 72-01-T1 | pending |
| 72-01-T3 | 01 | 1 | SIGNCHAR-04/07 | T-72-01, T-72-02 | Cross-board POST rejected end to end; cross-user POST rejected and isolatable | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestUpdateSignupCharacterTests"` | exists via 72-01-T1 | pending |
| 72-02-T1 | 02 | 1 | SIGNCHAR-04 | T-72-08 | The label is a plain string builder with no markup, so option text cannot carry HTML | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterDisplayExtensionsTests"` | W0 — creates `CharacterDisplayExtensionsTests.cs` | pending |
| 72-02-T2 | 02 | 1 | SIGNCHAR-01/02/03/04 | T-72-08, T-72-09 | Razor encoding on option text; antiforgery token emitted by the form tag helper; exactly one `characterId` field | build + source | `dotnet build` (Razor compilation) plus the task's source assertions; behaviour pinned one wave later by 72-03-T3 / 72-04-T3 | n/a — new markup file | pending |
| 72-02-T3 | 02 | 1 | SIGNCHAR-03/04 | T-72-07, T-72-10 | Inject-if-missing kills the silent wipe; `textContent` blocks label-driven markup injection; a disabled select posts an absent field | build + source | `dotnet build` plus the task's source assertions; behaviour pinned one wave later by 72-03-T3 / 72-04-T3 | n/a — script inside the partial | pending |
| 72-03-T1 | 03 | 2 | SIGNCHAR-01/03/04 | T-72-11 | Filled-state trigger guarded by `isCurrentUser`; no control on another player's row | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController"` | existing suite; pinned by 72-03-T3 | pending |
| 72-03-T2 | 03 | 2 | SIGNCHAR-04 | T-72-08, T-72-12 | Widened pickers stay owner- and board-scoped; option text HTML-encoded | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController\|FullyQualifiedName~Mobile"` | existing suite | pending |
| 72-03-T3 | 03 | 2 | SIGNCHAR-01/03/04 | T-72-11 | Another player's character id absent from every current-character attribute; exactly one modal instance per page | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestDetailsCharacterControlTests"` | W0 — creates `QuestDetailsCharacterControlTests.cs` | pending |
| 72-04-T1 | 04 | 2 | SIGNCHAR-02/03 | T-72-13 | Filled-state trigger guarded by `isCurrentUser`; no control on another player's mobile row | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Mobile"` | existing suite; pinned by 72-04-T3 | pending |
| 72-04-T2 | 04 | 2 | SIGNCHAR-04 | T-72-08, T-72-12 | Sixth and final list reader routed through the shared label; stays owner- and board-scoped | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Mobile\|FullyQualifiedName~QuestController"` | existing suite | pending |
| 72-04-T3 | 04 | 2 | SIGNCHAR-02/04 | T-72-13, T-72-14 | Mobile view proven to be selected for a real mobile User-Agent; UA spoofing changes the template only, never the data | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestDetailsMobileCharacterControlTests"` | W0 — creates `QuestDetailsMobileCharacterControlTests.cs` | pending |

*Status: pending · green · red · flaky*

**On the two build-and-source tasks (72-02-T2, 72-02-T3):** they create a Razor partial that no view renders yet, so no runtime assertion can reach it at commit time. They are verified by `dotnet build` (Razor compilation catches a bad using directive or a mistyped extension call) plus explicit source assertions, and their behaviour is pinned one wave later by `72-03-T3` and `72-04-T3`, which assert the rendered markup, the trigger attributes and the single-instance property on both platforms. They are not unverified — they are verified across a wave boundary, inside this same phase.

### Requirement → Task Map

| Req ID | Behavior | Owning tasks | Automated command |
|--------|----------|--------------|-------------------|
| SIGNCHAR-01 | Change the character on an existing signup, desktop, both tables | 72-01-T1, 72-02-T2, 72-02-T3, 72-03-T1, 72-03-T3 | `--filter "FullyQualifiedName~QuestUpdateSignupCharacterTests\|FullyQualifiedName~QuestDetailsCharacterControlTests"` |
| SIGNCHAR-02 | Same from the mobile Details page, which has no control at all today | 72-02-T2, 72-02-T3, 72-04-T1, 72-04-T3 | `--filter "FullyQualifiedName~QuestDetailsMobileCharacterControlTests"` |
| SIGNCHAR-03 | Clear back to no character, both platforms | 72-01-T1, 72-02-T3, 72-03-T1, 72-04-T1 | `--filter "FullyQualifiedName~QuestUpdateSignupCharacterTests"` — asserts a null `CharacterId` read from a fresh scoped DbContext |
| SIGNCHAR-04 | Inactive character shown as the current selection, status-labelled, no silent wipe | 72-01-T3, 72-02-T1, 72-02-T3, 72-03-T3, 72-04-T3 | `--filter "FullyQualifiedName~CharacterDisplayExtensionsTests"` plus the Retired-character cases in the three integration files |
| SIGNCHAR-05 | Works post-finalization, no time cutoff | 72-01-T1 | `--filter "FullyQualifiedName~OnFinalizedQuest"` |
| SIGNCHAR-06 | Works for waitlisted signups and all three signup roles | 72-01-T1 | `--filter "FullyQualifiedName~ForWaitlistedSignup\|FullyQualifiedName~ForEachSignupRole"` |
| SIGNCHAR-07 | Cross-user rejected (isolatable) and cross-board rejected (boundary regression) | 72-01-T1, 72-01-T3 | `--filter "FullyQualifiedName~AnotherUsersCharacter\|FullyQualifiedName~AnotherBoard"` |

**Note on SIGNCHAR-07.** The same-board / different-owner case is the load-bearing, isolatable test: it exercises the ownership check directly and that check is currently the only thing standing between a player and another player's character within one board. The cross-board case is a boundary **regression** test — `CharacterEntity` already carries a global board query filter that resolves a foreign character to null before the action's own comparison is reached, so it passes whether or not the explicit comparison exists. It documents that the boundary holds end to end; it does not prove the explicit check in isolation. Plan 01 states this in its research-correction block, and the cross-board test itself carries a doc comment saying so, so nobody later mistakes it for coverage of the explicit check alone.

---

## Wave 0 Requirements

Three new test files and one new production file, each created inside the first task that depends on it — no separate Wave 0 plan is needed because every consuming task lives in the same plan as its fixture.

- [ ] `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` — **new**, created by `72-01-T1`. Nothing covers `UpdateSignupCharacter` today. Fixture modelled on `QuestJoinFinalizedQuestTests.cs` (same controller, adjacent action). The cross-board case is modelled on `TenantIsolationTests.cs`'s `factory.TestGroupContext.ActiveGroupId` mutable-singleton pattern: seed the foreign character through `factory.Database.CreateContext()` (which bypasses the query filter for writes), then issue the authenticated request scoped to `ActiveGroupId = 1`. The class implements `IAsyncLifetime` and resets `ActiveGroupId = 1` in `DisposeAsync`.
- [ ] `QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs` — **new**, created by `72-02-T1`. Modelled on `QuestBoard.UnitTests/Extensions/WaitlistOrderingTests.cs`. Pins the exact option-label format, including byte-for-byte preservation of today's text for Active characters, so widening the list cannot silently change what three pre-existing pickers render.
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs` — **new**, created by `72-03-T3`. Desktop Details GET markup assertions; request shape modelled on `QuestControllerIntegrationTests_Comprehensive.cs`'s Details GET test. Sends no User-Agent header, which is what selects the desktop view.
- [ ] `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs` — **new**, created by `72-04-T3`. Real mobile User-Agent requests built as `HttpRequestMessage` with `TryAddWithoutValidation`, modelled on `MobileViewsTests.cs` lines 168–215, including a desktop-UA control assertion that fails if the platform split ever stops selecting the mobile file.

Scaffolding this phase introduces that the tests depend on:

- [ ] `TestDataHelper.CreatePlayerSignupAsync` gains a **trailing** optional `int? characterId = null` parameter (`72-01-T1`), so a fixture can seed a signup that already holds a character — required by the swap, clear and Retired-character cases. Trailing position keeps every existing call site source-compatible.
- [ ] `QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs` — **new production file** (`72-02-T1`), exposing `public static string ToSelectLabel(this Character character)`. It must be `public`, not `internal`: `QuestBoard.UnitTests` references `QuestBoard.Service` and there is no `InternalsVisibleTo` grant, so an internal type would not be testable.
- [ ] `TestDataHelper.CreateTestCharacterAsync` needs **no** change — it already accepts `status` and `groupId`, which covers both the Retired/Dead and the cross-board fixtures.
- [ ] Framework install: **none** — every package needed is already referenced in both test projects.

---

## Manual-Only Verifications

| Behavior | Requirement | Owning plan | Why Manual | Test Instructions |
|----------|-------------|-------------|------------|-------------------|
| The change control renders in the desktop finalized-participants **and** waitlist character cells, in the same cell immediately after name + avatar, at the same size as the green add button | SIGNCHAR-01 | 03 | Visual placement and sizing are not assertable from markup alone | Open a quest Details page as a player with an existing signup; confirm the control sits in the same cell, immediately after name + avatar, in both tables |
| The mobile control renders inline on the second line and does **not** increase participant row height | SIGNCHAR-02 | 04 | Row-height regression is visual | Load Details with a **real mobile User-Agent** — not devtools emulation, because the split matches on the UA string — and compare row height against a before screenshot at the same width |
| A Retired or Dead character appears as the pre-selected, status-labelled option in the dropdown | SIGNCHAR-04 | 03 (desktop), 04 (mobile) | The integration tests prove the trigger carries the right id and that persistence holds; the rendered selected option is a browser-runtime concern | Open the change control on a signup holding a Retired character; confirm the dropdown shows it selected with its status suffix, on both platforms |
| The confirm dialog fires before Remove, and cancelling performs no POST | SIGNCHAR-03 | 02 (mechanism), 03 and 04 (both surfaces) | Native browser dialog | Click Remove character, confirm the dialog appears, cancel it, and confirm the signup is unchanged |
| A success toast appears after both a swap and a clear, on desktop and mobile | SIGNCHAR-01 / SIGNCHAR-03 | 01 (TempData), 03 and 04 (surfaces) | Toast rendering is layout-level | Perform a swap and a clear on each platform and confirm the toast shows. Matters most on mobile, where the changed row can be scrolled out of view after the reload |

Each of the five has a named home in the owning plan's `<verification>` block, so all of them reach UAT. None is faked with a brittle markup assertion.

---

## Validation Sign-Off

- [x] All 12 tasks carry an `<automated>` verify command, or an explicit cross-wave dependency documented above (72-02-T2, 72-02-T3)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references — 3 new test files, 1 helper signature change, 1 new production extension
- [x] No watch-mode flags
- [x] Feedback latency under 30 s on the quick filter
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planned — pending execution
