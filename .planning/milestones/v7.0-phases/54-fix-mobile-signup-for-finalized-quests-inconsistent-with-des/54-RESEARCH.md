# Phase 54: Fix mobile signup for finalized quests (inconsistent with desktop) - Research

**Researched:** 2026-07-06
**Domain:** ASP.NET Core 10 MVC Razor views (mobile/desktop parity) + EF Core-backed waitlist domain logic
**Confidence:** HIGH

## Summary

This phase is a well-bounded, already-diagnosed bug fix with all decisions locked in `54-CONTEXT.md` (D-01 through D-06). Research confirms every claim in CONTEXT.md against the live source: the desktop "Join This Quest" card (`Details.cshtml` lines 293-382), the `JoinFinalizedQuest` controller action (`QuestController.cs` lines 414-491), `WaitlistOrdering.OrderWaitlist`, and `QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync` were all read directly and match the CONTEXT.md description exactly — no surprises, no code drift since context-gathering.

Two independent changes ship in this phase: (1) a pure Razor-view port of desktop's 3-button join card into `Details.Mobile.cshtml`, requiring zero controller/ViewModel changes because the shared `Details` GET action already populates every ViewBag value the card needs (`IsPlayerSignedUp`, `UserCharacters`, `CanManage`, `BoardType`); and (2) a one-line-of-intent behavior change in `JoinFinalizedQuest` — replace the capacity hard-reject with `IsSelected = false`, routing a new joiner into the pre-existing Phase 44 waitlist machinery unchanged. Verified: `PromoteNextWaitlistedPlayerIfSeatFreedAsync` is a `private` method only ever called from `ChangeVoteAsync` and `RevokeSignupAsync` (not `JoinFinalizedQuest`), so a newly-waitlisted joiner from this phase will NOT trigger an immediate promotion attempt — it simply enters the waitlist pool and gets promoted later exactly like any other waitlisted signup, when someone else votes No or revokes. This is the correct, minimal-blast-radius behavior and requires no change to either of those methods.

No existing automated test (unit or integration) currently exercises `JoinFinalizedQuest`'s capacity-check branch at all — this is a genuinely new-coverage area, not a "tests will break" area. The codebase has an established, reusable integration-test pattern (`MobileViewsTests.cs`) for asserting mobile-vs-desktop view differences via `User-Agent` header switching, which the new join-card test(s) should follow.

**Primary recommendation:** Port the markup verbatim (D-01/D-02), change `JoinFinalizedQuest`'s capacity branch from reject to `IsSelected = false` (D-03), and add net-new integration/unit test coverage for both — there are no existing tests to update, only tests to add. Real-device verification (physical phone, not devtools emulation) is required for the mobile card per this project's own established precedent (Phase 43).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Mobile "Join This Quest" card rendering | Frontend Server (SSR / Razor view) | — | Pure `.Mobile.cshtml` markup addition; no client-side JS beyond the existing inline character-sync script pattern already used on desktop |
| Character-select-to-hidden-input sync | Browser / Client (inline `<script>`) | — | Vanilla DOM `addEventListener` pattern, no framework; mirrors desktop exactly, ported verbatim |
| Capacity check / waitlist routing decision | API / Backend (`QuestController.JoinFinalizedQuest`) | — | Server must never trust client-computed "is there space" state (established pattern, reused unchanged) |
| Waitlist ordering | Domain (`WaitlistOrdering.OrderWaitlist`) | — | Pure sort extension, already handles arbitrary waitlisted signups; no changes needed |
| Auto-promotion on seat-free | Domain (`QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync`) | — | Only triggered by `ChangeVoteAsync`/`RevokeSignupAsync`, not by new joins — confirmed via `FindReferences`-equivalent grep; no changes needed |
| Persisted signup state | Database / Storage (`PlayerSignups` table via EF Core) | — | `IsSelected` flag flip is the only schema-level change in behavior; no migration needed (column already exists) |

## Standard Stack

No new packages are introduced by this phase. This is a code-only bug fix within the existing ASP.NET Core 10 MVC + EF Core + SQL Server stack (see `.planning/codebase/STACK.md` for the full dependency list). No installation, no `npm`/`pip`/`cargo` verification applicable.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages. Skipping the Package Legitimacy Gate protocol per its own scope (audit is required only "whenever this phase installs external packages").

## Architecture Patterns

### System Architecture Diagram

```
Player (mobile browser)
    │
    ▼
GET /Quest/Details/{id}          [QuestController.Details — shared GET action, already returns
    │                              ViewBag.IsPlayerSignedUp / UserCharacters / CanManage / BoardType]
    ▼
Mobile UA? ──yes──▶ Details.Mobile.cshtml
    │                    │
    │                    ├─ Participant list (existing, unchanged)
    │                    ├─ Waitlist list (existing, unchanged)
    │                    ├─ [NEW] "Join This Quest" card ── D-01/D-02
    │                    │     3 forms → POST JoinFinalizedQuest (Player / AssistantDM / Spectator)
    │                    └─ DM manage link / revoke+vote section (existing, unchanged)
    │
    no
    ▼
Details.cshtml (desktop, unchanged — already has the card)

POST /Quest/JoinFinalizedQuest        [shared controller action — both views submit here]
    │
    ▼
Re-fetch quest server-side (never trust client "has space" state)
    │
    ▼
role == Player? ──yes──▶ selectedPlayersCount >= TotalPlayerCount?
    │                         │
    │                        yes ──▶ [CHANGED, D-03] IsSelected = false (waitlisted)
    │                         │        instead of ModelState reject
    │                        no  ──▶ IsSelected = true (seated, unchanged)
    │
    no (AssistantDM/Spectator) ──▶ IsSelected = true always (unchanged, D-05)
    │
    ▼
PlayerSignupService.AddAsync → PlayerSignupRepository (EF Core) → PlayerSignups table
    │
    ▼
Redirect to Details
    │
    ▼
(Later, independently) another player votes No / revokes
    │
    ▼
QuestService.ChangeVoteAsync / RevokeSignupAsync
    │
    ▼
PromoteNextWaitlistedPlayerIfSeatFreedAsync
    │
    ▼
PlayerSignupRepository.GetTopWaitlistedCandidateAsync
    (ORDER BY vote priority DESC, THEN BY LastVoteChangeTime ?? SignupTime ASC)
    │
    ▼
Top candidate (may be a Phase-54-created waitlisted joiner) → IsSelected = true → email dispatched
```

### Recommended Project Structure

No new files/folders. Changes land in existing files only:

```
QuestBoard.Service/
├── Views/Quest/
│   ├── Details.cshtml              # source of truth for D-06 copy update (line ~314)
│   └── Details.Mobile.cshtml       # D-01/D-02: new card inserted between waitlist (line 254) and DM link (line 257); D-06 copy update
├── Controllers/QuestBoard/
│   └── QuestController.cs          # D-03: JoinFinalizedQuest capacity branch (lines 438-448)
QuestBoard.UnitTests/
│   └── ... (extend, see Validation Architecture)
QuestBoard.IntegrationTests/
│   └── Controllers/ or Mobile/ (new test file or extend existing, see Validation Architecture)
```

### Pattern 1: Shared controller action, two view variants

**What:** `QuestController.Details` (GET) populates `ViewBag` once; the view-selection middleware (`MobileViewLocationExpander`, referenced by `MobileViewLocationExpanderTests.cs`) resolves to `Details.Mobile.cshtml` or `Details.cshtml` based on User-Agent. Both views read from the identical `ViewBag`/`Model` surface.

**When to use:** Any future desktop/mobile parity fix in this codebase — confirmed by direct inspection that `IsPlayerSignedUp`, `UserCharacters`, `CanManage`, `CalendarMonths`, `BoardType` are all already set at `QuestController.cs` lines 328-356 before either view renders. No controller change is needed purely to add UI to one view variant.

**Example (verified from source, `Details.cshtml` lines 336-346):**
```csharp
<form asp-action="JoinFinalizedQuest" method="post" class="d-inline" id="joinPlayerForm">
    <input type="hidden" name="questId" value="@Model.Quest?.Id" />
    <input type="hidden" name="selectedRole" value="0" />
    <input type="hidden" name="characterId" id="playerCharacterId" value="" />
    <button type="submit" class="btn btn-primary w-100">
        <i class="fas fa-users me-2"></i>Join as Player
        <small class="d-block mt-1" style="font-size: 0.8em;">Participate in the quest (counts toward player limit)</small>
    </button>
</form>
```
Note: `asp-action` tag helpers auto-inject the antiforgery token; the mobile port does not need an explicit `@Html.AntiForgeryToken()` call, matching desktop's existing forms (which also omit it explicitly and rely on the tag helper).

### Pattern 2: Server-side recompute-then-decide (never trust client state)

**What:** `JoinFinalizedQuest` recomputes `selectedPlayersCount` from the freshly re-fetched `quest.PlayerSignups` immediately before branching on it (`QuestController.cs` lines 440-444). D-03's fix only changes the outcome of the `if` branch (waitlist instead of reject); the recompute-then-decide shape itself is untouched and must remain untouched.

**When to use:** Preserve this pattern exactly — do not introduce any client-supplied "is there space" flag.

### Pattern 3: Waitlist ordering is timestamp-tiebreak-safe for brand-new signups

**What:** `WaitlistOrdering.OrderWaitlist` sorts `.ThenBy(ps => ps.LastVoteChangeTime ?? ps.SignupTime)`. A signup created via `JoinFinalizedQuest` will have `LastVoteChangeTime == null` (it's never set anywhere except `PlayerSignupRepository.ChangeVoteAsync`, confirmed via grep) and `SignupTime = DateTime.UtcNow` at creation (entity default, `PlayerSignupEntity.cs` line 22). This is structurally identical to any other waitlisted signup that has never had its vote changed — confirmed by the existing test `GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime` (`PlayerSignupRepositoryTests.cs` lines 271-298), which already covers exactly this `LastVoteChangeTime == null` + `SignupTime` fallback shape for a pre-existing (non-JoinFinalizedQuest-created) signup.

**Conclusion:** No ordering bug exists for a same-timestamp brand-new joiner interacting with existing waitlisted signups — the mechanism was already designed to handle "signup with a null LastVoteChangeTime" as its baseline case, not an edge case.

### Anti-Patterns to Avoid

- **Reinventing waitlist mechanics:** Do not add a new "join as waitlisted" code path distinct from the existing `IsSelected = false` + `OrderWaitlist` + `PromoteNextWaitlistedPlayerIfSeatFreedAsync` trio. D-03 explicitly reuses these unchanged.
- **Calling promotion logic from `JoinFinalizedQuest`:** `PromoteNextWaitlistedPlayerIfSeatFreedAsync` is `private` and only wired to `ChangeVoteAsync`/`RevokeSignupAsync` (seat-freeing events). A new join never frees a seat, so it must never call this method — verified this is not currently wired and should stay that way.
- **Mobile-specific UI simplification:** D-01 explicitly forbids collapsing the 3-button role selection into a single dropdown for mobile — port the desktop interaction model exactly.
- **Divergent messaging wording between platforms:** D-06 requires updating the "quest full" copy on both `Details.cshtml` and `Details.Mobile.cshtml` consistently — don't fix only the platform that happens to get touched first.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Waitlist ordering for a new joiner | A separate "insert into position N" algorithm | `WaitlistOrdering.OrderWaitlist` (unchanged) | Already handles arbitrary `IsSelected = false` rows uniformly by vote-priority + timestamp; a new joiner is not a special case |
| Seat-freed promotion trigger | A promotion check inside `JoinFinalizedQuest` itself | Leave promotion solely triggered by `ChangeVoteAsync`/`RevokeSignupAsync` | A new join never frees a seat — there is nothing to promote into at join time; promotion already fires correctly later when a seat *does* free |
| Antiforgery token wiring for the new mobile forms | Manual `@Html.AntiForgeryToken()` insertion reasoning from scratch | Copy desktop's `asp-action` tag-helper-only forms verbatim | Desktop's forms already work correctly without an explicit token call (tag helper injects it); mirroring this avoids introducing an inconsistency `AntiForgeryTokenCoverageTests.cs`-style reflection tests could flag |

**Key insight:** Every piece of "hard" logic this phase might otherwise re-derive (ordering, promotion, capacity recompute) already exists, is already tested, and CONTEXT.md's own research already confirmed it needs zero modification. The actual work is narrow: one Razor markup port + one `if`-branch outcome change.

## Common Pitfalls

### Pitfall 1: Assuming `JoinFinalizedQuest`'s capacity fix needs to touch `PromoteNextWaitlistedPlayerIfSeatFreedAsync`
**What goes wrong:** A plan might add a "try to promote immediately" call after creating a waitlisted signup, on the theory that "maybe there's already room."
**Why it happens:** The capacity check that led to waitlisting, by definition, already confirmed `selectedPlayersCount >= quest.TotalPlayerCount` — there is no freed seat to promote into. Calling promotion here is a no-op at best, or a subtle bug at worst if the method's `freeingPlayerSignupId` guard semantics are misapplied to a joiner who never had a prior selected seat.
**How to avoid:** Do not call `PromoteNextWaitlistedPlayerIfSeatFreedAsync` from `JoinFinalizedQuest`. Simply set `IsSelected = false` and let the existing seat-freeing triggers handle promotion later, as designed.
**Warning signs:** Any new call site added to `PromoteNextWaitlistedPlayerIfSeatFreedAsync` inside `JoinFinalizedQuest`, or a new public overload made to accommodate it.

### Pitfall 2: Antiforgery token coverage test could flag a new action if one were mistakenly added
**What goes wrong:** `AntiForgeryTokenCoverageTests.cs` uses reflection to assert every `[HttpPost]` action carries `[ValidateAntiForgeryToken]`. If a plan mistakenly introduces a *new* controller action (e.g., a separate mobile-specific join endpoint) instead of reusing `JoinFinalizedQuest`, this test will still pass as long as the attribute is present — but doing so would violate D-04 (shared action for both platforms) and duplicate logic that must stay in sync.
**Why it happens:** Tempting to think "mobile needs its own lightweight endpoint" when porting UI.
**How to avoid:** Reuse `JoinFinalizedQuest` exactly as-is (already carries `[HttpPost][ValidateAntiForgeryToken][Authorize]`, `QuestController.cs` lines 414-416) — the mobile forms simply POST to the same action via `asp-action="JoinFinalizedQuest"`.
**Warning signs:** Any new action method appearing in `QuestController.cs` for this phase.

### Pitfall 3: Mobile CSS class drift from `Details.Mobile.cshtml`'s established conventions
**What goes wrong:** Inventing new class names instead of reusing `quest-section-card-mobile` / `quest-section-heading`, causing the new join card to look visually inconsistent with the vote/date-selection cards already in the same file.
**Why it happens:** The desktop card uses `card modern-card` / `card-header modern-card-header` (Bootstrap card pattern per this project's UI/UX guidelines in CLAUDE.md), which is a different visual system than mobile's glassmorphism `quest-section-card-mobile` (defined in `quests.mobile.css` lines 84-91, `backdrop-filter: blur(15px)`, parchment-colored text with heavy text-shadow).
**How to avoid:** Follow CONTEXT.md's explicit discretion note — use `quest-section-card-mobile` + `quest-section-heading` (icon + heading pattern), not desktop's `modern-card` classes, for the new mobile card's container. The *interaction structure* (3 buttons, shared character select) ports 1:1; the *visual container* must match mobile's existing sibling cards, not desktop's.
**Warning signs:** `class="card modern-card"` appearing anywhere inside `Details.Mobile.cshtml`.

### Pitfall 4: Verifying mobile UI changes only via devtools/responsive-mode emulation
**What goes wrong:** A touch-target, glassmorphism-rendering, or button-stacking bug ships because it was only checked via a resized desktop browser window.
**Why it happens:** Faster iteration loop than real-device testing.
**How to avoid:** This project has an explicit, established precedent (Phase 43, `43-VERIFICATION.md`) requiring real-device confirmation for mobile-specific UI/CSS changes, with the device model and OS version recorded in the phase's SUMMARY.md (e.g., "iPhone 17 Pro, iOS 26, physical device, real Wi-Fi LAN session — not devtools emulation"). The project's own `PITFALLS.md` (from the v7.0 image-cropping research) generalizes this into a standing rule: "any new CSS/JS work motivated by mobile-specific behavior ships without a named real-device (or real-device-cloud) verification step" is itself listed as a warning sign to avoid repeating.
**Warning signs:** A verification checklist for this phase that only lists desktop browsers or "Chrome devtools mobile view" as the tested environment for the new join card.
**Phase to address:** This phase directly — the plan's verification section should include an explicit real-device (or real-device-cloud, e.g. BrowserStack) checkpoint naming the device/OS/method used, mirroring `43-01-SUMMARY.md`'s recorded evidence format.

### Pitfall 5: Forgetting D-06's copy update is needed on *both* platforms
**What goes wrong:** The "quest full" messaging fix (currently `Details.cshtml` line 314: "Player slots full, but you can join as Assistant DM or Spectator!") gets updated only on the platform being actively worked on (likely mobile, since that's the new card), leaving desktop's copy stale and now factually wrong (since a Player join no longer gets blocked — it waitlists).
**Why it happens:** The phase's headline framing is "fix mobile," which can bias attention away from a desktop-only line that also needs to change.
**How to avoid:** D-06 explicitly requires updating this copy "on both platforms." Since the new mobile card is a fresh port, its copy should be written correctly from the start (no "full" messaging that implies rejection); the existing desktop line at `Details.cshtml` line 314 must be separately edited in the same phase.
**Warning signs:** A diff that touches `Details.Mobile.cshtml` copy but leaves `Details.cshtml` line 314 unchanged.

## Code Examples

### Current capacity-reject block (to be changed, `QuestController.cs` lines 438-449)
```csharp
// Check if quest has space - only count Player roles
if (role == SignupRole.Player)
{
    var selectedPlayersCount = quest.PlayerSignups
        .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
        .Count();

    if (selectedPlayersCount >= quest.TotalPlayerCount)
    {
        ModelState.AddModelError("", $"This quest is full ({selectedPlayersCount}/{quest.TotalPlayerCount} players).");
        return RedirectToAction("Details", new { id = questId });
    }
}
```
Per D-03, this becomes a decision that flows into the existing `signup.IsSelected` assignment below it (`QuestController.cs` line 479, currently hardcoded `true`) rather than an early return. The `IsSelected` value must become conditional: `true` when there's space, `false` when full — for the `Player` role only (D-05: `AssistantDM`/`Spectator` stay always-`true`, no capacity branch touches them).

### Existing waitlist ordering (unchanged, reused, `WaitlistOrdering.cs`)
```csharp
public static IEnumerable<PlayerSignup> OrderWaitlist(this IEnumerable<PlayerSignup> waitlist, int finalizedProposedDateId)
{
    return waitlist
        .OrderByDescending(ps => VotePriority(ps, finalizedProposedDateId))
        .ThenBy(ps => ps.LastVoteChangeTime ?? ps.SignupTime);
}
```

### Existing promotion trigger call sites (unchanged, confirmed NOT called from JoinFinalizedQuest, `QuestService.cs`)
```csharp
// ChangeVoteAsync (line ~285) and RevokeSignupAsync (line ~310) both call:
await PromoteNextWaitlistedPlayerIfSeatFreedAsync(questId, finalizedProposedDateId, freeingPlayerSignupId: playerSignupId, token);
```
No third call site should be added for this phase.

### Established mobile integration-test pattern to follow (`MobileViewsTests.cs` lines 14-56)
```csharp
public class MobileViewsTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null) request.Headers.Authorization = authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }
}
```
A new test for D-01/D-02 should follow this exact shape: seed a finalized One-Shot quest at capacity via `TestDataHelper.CreateTestQuestAsync(..., isFinalized: true, finalizedDate: ...)` + `CreateProposedDateAsync` + `CreatePlayerSignupAsync(..., isSelected: true)` × `TotalPlayerCount`, then assert the mobile response HTML contains the new join card's markup/text for an authenticated, not-yet-signed-up user.

### Established repository-level test pattern for waitlist assertions (`PlayerSignupRepositoryTests.cs` lines 270-298)
```csharp
[Fact]
public async Task GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime()
{
    // earlySignup.LastVoteChangeTime = null; lateChanger.LastVoteChangeTime = DateTime.UtcNow;
    // earlySignup wins the tiebreak via SignupTime fallback
}
```
A new test verifying "JoinFinalizedQuest creates a waitlisted signup that participates correctly in existing ordering" should extend this file or `QuestServiceTests.cs`, using `MakeSignupEntity`/`SeedQuestAndUserAsync` helpers already defined there.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `JoinFinalizedQuest` hard-rejects a new Player join when full | Creates `PlayerSignup` with `IsSelected = false` (waitlisted) | This phase (D-03), reopening Phase 44's locked D-03 boundary | A previously-blocked action now succeeds; "quest full" messaging must be rewritten (D-06) |
| Mobile has no path to join a finalized quest at all | Mobile gets the same 3-button join card as desktop | This phase (D-01/D-02) | Closes a genuine functional gap — mobile users could not join finalized quests before this phase |

**Deprecated/outdated:**
- Phase 44's `44-CONTEXT.md` D-03 ("brand-new player joining a finalized quest... still goes through the existing `JoinFinalizedQuest` action unchanged — always an automatic Yes signup... Not in scope") is explicitly superseded by this phase's D-03 per direct user instruction. Downstream readers of Phase 44's docs should treat that specific boundary as historical, not current — Phase 54's CONTEXT.md and this RESEARCH.md are the current source of truth for `JoinFinalizedQuest`'s capacity behavior.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The exact wording for the D-06 "you'll join the waitlist" copy is Claude's discretion per CONTEXT.md — no specific string is locked | Common Pitfalls / Code Examples | Low — CONTEXT.md explicitly delegates this to Claude's discretion; any reasonably clear, tonally-consistent wording satisfies the decision |

No other claims in this research are unverified — every technical claim (file/line locations, method signatures, call graphs, test file existence/absence, CSS class definitions, ViewBag population) was confirmed by directly reading the relevant source file or running a targeted grep against the actual repository in this session.

**If this table is empty:** N/A — one low-risk discretionary item listed above; everything else is `[VERIFIED: codebase]`.

## Open Questions

1. **Exact D-06 copy wording**
   - What we know: Must convey that clicking "Join as Player" still succeeds but results in a waitlist position, not immediate seating; should read consistently with the existing "You are on the waitlist for this quest" message (`Details.cshtml` line 56).
   - What's unclear: The exact sentence — this is explicitly Claude's discretion per CONTEXT.md, not a gap in research.
   - Recommendation: Planner/implementer picks wording at plan/execution time; suggested direction: "Player slots full — joining now will place you on the waitlist." (illustrative only, not locked).

2. **Real-device access confirmation**
   - What we know: `.planning/STATE.md` Pending Todos lists "Confirm real-device or real-device-cloud (e.g. BrowserStack) access is available" as an open item carried from v7.0 planning, and Phase 43 already successfully used a physical iPhone 17 Pro (iOS 26) for its mobile verification.
   - What's unclear: Whether the same physical device/access is still available for this phase's verification checkpoint, or whether it needs re-confirming with the user before the plan's verification task can be executed.
   - Recommendation: Plan should include an explicit `checkpoint:human-verify` task for real-device confirmation of the new mobile join card, following the exact evidence-recording format Phase 43 used (device model + OS version + "not devtools emulation" statement in the resulting SUMMARY.md).

## Environment Availability

Skipped — this phase has no new external tool/service/runtime dependencies beyond the existing ASP.NET Core 10 / SQL Server / Docker stack already running in this environment. The only environment-dependent verification need (a physical mobile device or device-cloud access for real-device testing) is captured above as Open Question 2 rather than a build/runtime environment probe, since it's a manual verification resource rather than a CLI/service dependency.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 (xunit.runner.visualstudio 3.1.5) — confirmed via `QuestBoard.UnitTests.csproj` / `QuestBoard.IntegrationTests.csproj` `[VERIFIED: codebase]` |
| Config file | none — standard SDK-style `.csproj` test projects, no separate xunit config file found |
| Quick run command | `dotnet test --filter "FullyQualifiedName~QuestController\|FullyQualifiedName~PlayerSignupRepository\|FullyQualifiedName~QuestServiceTests"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

This is an ad-hoc bug-fix phase with no REQUIREMENTS.md IDs (same pattern as Phases 47-52, 55). The table below maps CONTEXT.md's locked decisions (D-01 through D-06) to concrete test targets instead.

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01/D-02 | Mobile renders "Join This Quest" card (3 buttons + character select) for an authenticated, not-yet-signed-up user on a finalized One-Shot quest, positioned after the waitlist section | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` | ❌ Wave 0 — extend `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` with a new `[Fact]` |
| D-01/D-02 (negative) | Mobile does NOT render the card for a user who is already signed up, or for an unauthenticated user | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` | ❌ Wave 0 — same file |
| D-03 | `JoinFinalizedQuest` creates a `PlayerSignup` with `IsSelected = false` when `selectedPlayersCount >= TotalPlayerCount` for a Player-role join, instead of returning a `ModelState` error | integration | new test file/class, e.g. `dotnet test --filter FullyQualifiedName~QuestJoinFinalizedQuestTests` | ❌ Wave 0 — no existing test touches `JoinFinalizedQuest` at all (confirmed via grep across `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |
| D-03 (regression guard) | A Player join with available space still sets `IsSelected = true` (unchanged behavior) | integration | same new test file | ❌ Wave 0 |
| D-05 | Assistant DM / Spectator joins remain always-`IsSelected = true` regardless of Player-slot fullness | integration | same new test file | ❌ Wave 0 |
| D-03 + waitlist integration | A `JoinFinalizedQuest`-created waitlisted signup is correctly included and ordered by `WaitlistOrdering.OrderWaitlist` / `GetTopWaitlistedCandidateAsync` alongside pre-existing waitlisted signups | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ✅ exists — extend, following `GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime` pattern (lines 271-298) |
| D-06 | Updated "quest full" copy appears identically (in substance) on both `Details.cshtml` and `Details.Mobile.cshtml`, and no longer implies a hard block for Player role | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` (mobile) + manual/visual check (desktop, or extend an existing desktop `Details` integration test if one exists) | ❌ Wave 0 |
| Antiforgery coverage (regression guard) | `JoinFinalizedQuest` still carries `[ValidateAntiForgeryToken]` after the edit (no new action introduced) | unit | `dotnet test --filter FullyQualifiedName~AntiForgeryTokenCoverageTests` | ✅ exists — no changes needed, existing reflection test auto-covers this as long as no new controller action is added |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~QuestController\|FullyQualifiedName~PlayerSignupRepository\|FullyQualifiedName~MobileViewsTests"` (targeted, should run well under 30s given existing suite sizes)
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`, plus the real-device manual verification checkpoint (see Common Pitfalls #4 and Open Question 2)

### Wave 0 Gaps
- [ ] New integration test file/class for `JoinFinalizedQuest` capacity-branch behavior (D-03/D-05) — no existing coverage at all; likely `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` or an extension of `QuestControllerIntegrationTests_Comprehensive.cs`
- [ ] New `[Fact]`(s) in `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` for the mobile join card's presence/absence/content (D-01/D-02/D-06)
- [ ] Extend `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` with a test seeding a `JoinFinalizedQuest`-shaped signup (`LastVoteChangeTime = null`, fresh `SignupTime`, `IsSelected = false`) into an existing waitlist and asserting correct ordering
- [ ] Manual real-device verification checkpoint (checklist item, not automatable) — record device model + OS version + "not devtools emulation" in the resulting SUMMARY.md, per Phase 43 precedent

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | yes (unchanged) | `[Authorize]` already present on `JoinFinalizedQuest` (`QuestController.cs` line 416) — no change needed |
| V3 Session Management | no | Not touched by this phase |
| V4 Access Control | yes (unchanged) | `[Authorize]` + explicit "already signed up" duplicate-join guard (`QuestController.cs` lines 429-433) — unchanged by D-03; still runs before the capacity branch |
| V5 Input Validation | yes (unchanged) | `selectedRole` cast to `SignupRole` enum, `characterId` ownership/status validated against `character.OwnerId != user.Id` (lines 454-459) — unchanged; D-03 does not touch input validation, only the capacity-branch outcome |
| V6 Cryptography | no | Not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| CSRF on the new mobile join forms | Tampering | `[ValidateAntiForgeryToken]` already on `JoinFinalizedQuest`; mobile forms reuse `asp-action` tag helper (auto-injects token) exactly like desktop — no new mitigation needed, just don't regress the existing one |
| Client-trusted capacity/seat-count bypass | Tampering / Elevation of Privilege | Server-side recompute of `selectedPlayersCount` immediately before the capacity decision (existing pattern, preserved unchanged by D-03) — never accept a client-supplied "has space" flag |
| IDOR via `characterId` on the join form | Tampering / Information Disclosure | Existing check `character.OwnerId != user.Id \|\| character.Status != CharacterStatus.Active` (lines 454-459) already guards this; unchanged by this phase, applies identically to the new mobile forms since they submit to the same action |

## Sources

### Primary (HIGH confidence — direct source inspection this session)
- `C:\Repos\quest-board\QuestBoard.Service\Views\Quest\Details.cshtml` (lines 1-400) — desktop finalized-quest block, join card structure, capacity messaging
- `C:\Repos\quest-board\QuestBoard.Service\Views\Quest\Details.Mobile.cshtml` (full file) — current mobile structure, insertion point, existing CSS class usage
- `C:\Repos\quest-board\QuestBoard.Service\Controllers\QuestBoard\QuestController.cs` (lines 400-500, 328-356) — `JoinFinalizedQuest` action, `Details` GET ViewBag population
- `C:\Repos\quest-board\QuestBoard.Domain\Extensions\WaitlistOrdering.cs` (full file) — ordering logic
- `C:\Repos\quest-board\QuestBoard.Domain\Services\QuestService.cs` (lines 280-345) — `PromoteNextWaitlistedPlayerIfSeatFreedAsync` and its two call sites
- `C:\Repos\quest-board\QuestBoard.Repository\PlayerSignupRepository.cs` (full file) — `ChangeVoteAsync`, `GetTopWaitlistedCandidateAsync`
- `C:\Repos\quest-board\QuestBoard.Repository\Entities\PlayerSignupEntity.cs` (full file) — `LastVoteChangeTime`/`SignupTime` defaults
- `C:\Repos\quest-board\QuestBoard.UnitTests\Repository\PlayerSignupRepositoryTests.cs` (lines 240-310) — existing waitlist-ordering test patterns
- `C:\Repos\quest-board\QuestBoard.UnitTests\Services\QuestServiceTests.cs` (lines 1-80) — existing service-test conventions (`MakeQuest`/`MakeSignup` helpers)
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Mobile\MobileViewsTests.cs` (lines 1-80) — mobile/desktop User-Agent-switching test pattern
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Mobile\MobileCssTests.cs` (lines 1-60) — CSS file integrity test pattern
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Security\AntiForgeryTokenCoverageTests.cs` — reflection-based antiforgery coverage test
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Helpers\TestDataHelper.cs` (full file) — test data seeding helpers (`CreateTestQuestAsync`, `CreatePlayerSignupAsync`, `CreateProposedDateAsync`)
- `C:\Repos\quest-board\QuestBoard.Service\wwwroot\css\quests.mobile.css` (full file) — `quest-section-card-mobile`/`quest-section-heading` class definitions, 44px touch-target convention
- `C:\Repos\quest-board\QuestBoard.UnitTests\QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests\QuestBoard.IntegrationTests.csproj` — xunit.v3 3.1.5 confirmed
- `C:\Repos\quest-board\.planning\config.json` — `nyquist_validation: true`, `test_command: "dotnet test"`
- `C:\Repos\quest-board\.planning\phases\54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des\54-CONTEXT.md` — locked decisions D-01 through D-06
- `C:\Repos\quest-board\.planning\phases\44-post-finalization-voting-waitlist-auto-promotion\44-CONTEXT.md` — the D-03 boundary being reopened
- `C:\Repos\quest-board\.planning\phases\43-mobile-parity-fixes\43-VERIFICATION.md` — real-device verification precedent (iPhone 17 Pro, iOS 26)
- `C:\Repos\quest-board\.planning\research\PITFALLS.md` (lines 88-224) — project-wide real-device verification standing rule
- `C:\Repos\quest-board\.planning\REQUIREMENTS.md`, `C:\Repos\quest-board\.planning\STATE.md` — project-wide context, confirms Phase 54 has no REQUIREMENTS.md mapping

No WebSearch/Context7/external documentation lookups were needed for this phase — it is a pure internal bug-fix within an already-well-documented, already-built codebase feature (Phase 44's waitlist machinery). All research questions were answerable via direct source inspection.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — no new packages introduced
- Architecture: HIGH — every claim verified against live source in this session, matches CONTEXT.md exactly with no drift
- Pitfalls: HIGH — derived directly from confirmed call-graph analysis (promotion trigger call sites) and this project's own documented verification-protocol history (Phase 43, PITFALLS.md)

**Research date:** 2026-07-06
**Valid until:** Effectively indefinite for the architectural facts (call graphs, entity defaults, test file existence) since these are static code facts, not fast-moving external dependencies. Re-verify only if Phase 44's waitlist machinery or `JoinFinalizedQuest` itself is touched by an intervening phase before Phase 54 executes.
