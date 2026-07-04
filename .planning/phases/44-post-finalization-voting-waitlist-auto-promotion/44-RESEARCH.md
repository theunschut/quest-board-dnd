# Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion - Research

**Researched:** 2026-07-04
**Domain:** ASP.NET Core MVC — internal domain logic (vote state machine, EF Core migration, Hangfire email dispatch), no new external libraries
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Vote-Change UI (for players who already signed up before finalization)**
- **D-01:** A player who already has a signup record on a finalized quest (selected or waitlisted) gets three explicit buttons — **Vote Yes / Vote Maybe / Vote No** — matching the style of the existing pre-finalization vote UI already used elsewhere in this app. This satisfies VOTE-04 (selected player votes No → seat frees + promotion), VOTE-05 (selected player votes Maybe → keeps seat, no promotion), and VOTE-06 (waitlisted player votes No → stays on waitlist, record retained, sorts to bottom).
- **D-02:** These three vote buttons render in the **same row as the existing "Revoke Signup" button** — Revoke stays left-aligned; Vote Yes/Maybe/No are pushed to the right side of that row. Applies to both the selected-participants section and the waitlist section.
- **D-03 (locked, not new scope):** A **brand-new player** joining a finalized quest for the first time (never signed up before) still goes through the existing `JoinFinalizedQuest` action unchanged — always an automatic Yes signup. No Maybe/No option is offered at initial-join time; the three-button vote-change UI only applies to players who already have a `PlayerSignup` row.

**Revoke vs. Vote No**
- **D-04:** Keep **both** actions available post-finalization, side by side. "Revoke Signup" remains a hard delete of the `PlayerSignup` row (existing `RevokeSignup`/`RemoveAsync` behavior, unchanged). "Vote No" is a new, softer action that keeps the record (visible on the waitlist per VOTE-06, or moved to the waitlist per VOTE-04) rather than deleting it. **Both** must trigger the seat-freed/auto-promotion check when the voter was previously selected.

**Mobile Parity**
- **D-05:** Build full mobile parity in this same phase — port the waitlist table + vote-change buttons + promotion behavior to `Details.Mobile.cshtml`, matching whatever desktop `Details.cshtml` ships. Desktop currently has a waitlist section; mobile currently has none at all.

**Promotion Email**
- **D-06:** Reuse the existing `FinalizedEmail` template's visual styling/branding (same Razor + HtmlRenderer + Hangfire + Resend pipeline already used 6+ times in this codebase) — just new copy explaining the player was promoted off the waitlist into a freed seat. No new template family, no new infrastructure.

### Claude's Discretion
- Exact wording/copy for the promotion email subject and body (within the reused `FinalizedEmail` visual style) is Claude's call during planning/implementation.
- How "the timestamp used for waitlist ordering" is tracked at the data-model level (e.g., reusing `PlayerSignup.SignupTime` by updating it on every vote change, vs. adding a new field) is an implementation detail for research/planning to resolve — not a user-facing behavior choice. Whatever is chosen must satisfy VOTE-03 (any vote change resets the ordering timestamp). **This research resolves the question below — see "Waitlist Ordering Timestamp" pattern.**
- Fixing the latent `Vote = 0` bug in `PlayerSignupRepository.ChangeVoteToYesAndSelectAsync` as part of rewriting this method — not a separate decision, just correct-by-construction since Phase 44 rebuilds this code path anyway.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. (VOTE-08, the in-app "you were promoted" banner, is already a documented v2 deferred requirement in REQUIREMENTS.md, not a new idea from this discussion.)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| VOTE-01 | Player can vote Yes on a finalized One-Shot quest even when all seats are filled, landing on a waitlist instead of being rejected | Confirmed exact removal point: `QuestController.ChangeVoteToYes` line 582 hard-`BadRequest` when full. New capacity check in `PlayerSignupRepository`/service must set `IsSelected` conditionally, never reject. |
| VOTE-02 | Waitlist is ordered by vote (Yes > Maybe > No), then by signup/vote-change timestamp ascending | New centralized ordering method recommended (see "Waitlist Ordering Query" section) — replaces inline Razor LINQ in `Details.cshtml` so desktop+mobile share one implementation. |
| VOTE-03 | Any vote change resets that signup's timestamp used for waitlist ordering | New nullable field `LastVoteChangeTime` on `PlayerSignup`/`PlayerSignupEntity`, set on every vote mutation; `SignupTime` stays untouched (see rationale below). |
| VOTE-04 | Selected player's seat frees up and top waitlisted candidate auto-promotes when that player votes No or fully revokes | Shared `PromoteNextWaitlistedPlayerAsync` method in Domain, called from both new "Vote No" path and existing `RevokeSignup`/`RemoveAsync` path. |
| VOTE-05 | Selected player who changes vote to Maybe keeps seat — no promotion triggered | Vote-change method only triggers promotion when the OLD state was `IsSelected == true` AND new vote is `No` (or record is removed) — Maybe is a no-op for promotion. |
| VOTE-06 | Waitlisted player who votes No stays on waitlist (record retained), sorting to bottom | Vote-change method for a non-selected signup only updates the vote + `LastVoteChangeTime`; never deletes, never changes `IsSelected`. |
| VOTE-07 | Waitlisted player auto-promoted receives a notification email — never the player who freed the seat, never a player whose own vote change selected them | New `EnqueueWaitlistPromotedEmail` dispatcher method + new Hangfire job + new Razor email component, called ONLY from the promotion path with the single promoted player's info — never from the freeing player's own action response. |
</phase_requirements>

## Summary

This phase is a rewrite/extension of an existing, partial, desktop-only waitlist implementation — not greenfield work. All claims from CONTEXT.md's `<code_context>` section were verified directly against current source and are confirmed accurate (file paths and most line numbers match; a few line numbers drifted slightly but the described behavior is exact). The most important verified finding is the `VoteType` enum ordering: `{ No = 0, Maybe = 1, Yes = 2 }`, which confirms `ChangeVoteToYesAndSelectAsync`'s `Vote = 0` (commented as "VoteType.Yes = 0") is in fact storing `VoteType.No` — a real, live data-corruption bug that Phase 44 will fix as a side effect of rewriting this method.

The central open design question — how to track "the timestamp that resets on vote change" for VOTE-03 ordering — is resolved by this research: **add a new nullable `DateTime? LastVoteChangeTime` field**, do NOT repurpose `SignupTime`. Verification found `SignupTime` is read in at least 9 view locations (`Manage.cshtml`, `Manage.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`, `Index.cshtml`/`_QuestCard.cshtml`, `QuestLog/Details.cshtml`, `QuestLog/Details.Mobile.cshtml`) where it is displayed verbatim to users as "Signed up: {time}" — repurposing its semantics to mean "last vote change" would silently corrupt that unrelated, currently-correct display across the whole app. Adding a new nullable column follows the exact precedent already set by this project's own `AddQuestCloseFields` migration (added `ClosedDate` as `nullable: true` DateTime alongside a non-null bool flag) — no backfill needed since `null` naturally means "never changed vote since signup," which sorts identically to using `SignupTime` as the fallback.

The cleanest place for the shared "check-and-promote" logic is a new method on `PlayerSignupService` (Domain layer, implementing a new `IPlayerSignupService` method, e.g. `ChangeVoteAsync(int playerSignupId, VoteType vote, int proposedDateId)` that internally branches on old/new `IsSelected` state and conditionally calls into promotion logic), plus a `RevokeSignup` call site update. Both need access to `IQuestEmailDispatcher` — currently `PlayerSignupService` doesn't depend on it, so either the dispatcher is injected into `PlayerSignupService`, or (cleaner, avoids widening that service's responsibility) the promotion+email orchestration lives in `QuestService` (which already has `IQuestEmailDispatcher` wired) with `PlayerSignupService`/`IPlayerSignupRepository` exposing the primitive vote-change and promotion-candidate-lookup operations that `QuestService` composes. Given this project's one-way Repository→Domain→Service layering and that `QuestService` already owns quest-level orchestration + email dispatch (see `FinalizeQuestAsync`), **the recommended home for promotion+email orchestration is `QuestService`**, with `PlayerSignupService`/`IPlayerSignupRepository` providing the vote-change and "find top waitlisted candidate" primitives it calls.

**Primary recommendation:** Add `LastVoteChangeTime` as a new nullable column via EF Core migration; centralize waitlist ordering and promotion logic in `QuestService`/`PlayerSignupService` rather than duplicating LINQ in Razor twice (desktop+mobile); fix the `Vote = 0` bug as part of rewriting `ChangeVoteToYesAndSelectAsync`; reuse the exact `QuestFinalizedEmailJob`/`HangfireQuestEmailDispatcher`/`QuestFinalized.razor` pattern for a new `QuestWaitlistPromotedEmailJob`/`EnqueueWaitlistPromotedEmail`/`WaitlistPromoted.razor` triple.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Vote Yes/Maybe/No buttons (UI) | Browser / Client (Razor + vanilla JS fetch) | Frontend Server (SSR renders the buttons) | Matches existing `revokeSignup()`/`changeVoteToYes()` fetch pattern already in `Details.cshtml` — no SPA framework, no new JS dependency |
| Vote-change validation + capacity check | API / Backend (QuestController action) | — | Server must never trust client-computed "is there space" state; mirrors existing `ChangeVoteToYes`/`JoinFinalizedQuest` guard pattern |
| Waitlist ordering (vote priority + timestamp) | API / Backend (Domain service/repository) | — | Currently duplicated inline in Razor; must move server-side so desktop+mobile share one source of truth (D-05 requires mobile parity) |
| Auto-promotion trigger | API / Backend (Domain service, `QuestService`) | — | Business rule that spans both "vote No" and "revoke" call sites; must live above the controller layer to avoid duplicating the check |
| Promotion email dispatch | API / Backend (`IQuestEmailDispatcher` → Hangfire job) | — | Existing established pattern (`FinalizeQuestAsync` → `EnqueueFinalizedEmail`); new trigger condition, not new plumbing |
| `LastVoteChangeTime` persistence | Database / Storage (EF Core migration, SQL Server) | — | New column on existing `PlayerSignups` table; auto-applied via `context.Database.Migrate()` on startup per this project's deployment model |

## Standard Stack

No new packages required. This phase extends existing infrastructure only:

### Core (existing, reused as-is)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| EF Core (SQL Server provider) | Already in `QuestBoard.Repository.csproj` | New migration for `LastVoteChangeTime` column | Project's only persistence layer; migrations auto-apply on startup |
| Hangfire | Already in `QuestBoard.Service` | New background job for promotion email | Every existing transactional email in this app goes through Hangfire fire-and-forget jobs |
| AutoMapper | Already wired at Entity↔Domain and Domain↔ViewModel boundaries | New field auto-maps by name convention (no `.ForMember` needed, same as `SignupTime` today) | Matching property names on `PlayerSignup`/`PlayerSignupEntity` map automatically per existing `EntityProfile.cs` pattern |
| Razor Components + `IEmailRenderService`/`HtmlRenderer` | Already in `QuestBoard.Service/Components/Emails` | New `WaitlistPromoted.razor` email template | Same pipeline as `QuestFinalized.razor`, `SessionReminder.razor`, etc. |
| xUnit v3 + NSubstitute + FluentAssertions | 3.2.2 / 5.3.0 / 8.10.0 (verified in `QuestBoard.UnitTests.csproj`) | Unit tests for the promotion state machine and the `VoteType` bug fix | Existing test project's only test stack; `QuestServiceTests.cs` and `EntityProfileEnumCastTests.cs` are the closest existing precedents to follow |

**Version verification:** All of the above are already installed and pinned in the solution's `.csproj` files — verified directly by reading `QuestBoard.UnitTests.csproj` (`FluentAssertions 8.10.0`, `NSubstitute 5.3.0`, `xunit.v3 3.2.2`). No new package installs are needed for this phase — `[VERIFIED: QuestBoard.UnitTests.csproj]`.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `LastVoteChangeTime` column | Repurpose `SignupTime`, update on every vote | Rejected — corrupts 9+ existing "Signed up: X" display sites across Manage/Details/QuestLog views, desktop and mobile, that rely on `SignupTime` meaning "when they first signed up" |
| Inline Razor LINQ ordering (status quo) | Centralized service/repository method | Recommended: centralize — D-05 mandates mobile parity, and duplicating vote-priority-then-time LINQ across `Details.cshtml` and `Details.Mobile.cshtml` is exactly the kind of drift that caused the mobile-parity backlog this project just cleaned up in Phase 43 |
| Consolidating vote-change into `PlayerSignupService` alone | Orchestrating in `QuestService` (which already owns email dispatch) | Recommended: `QuestService` — avoids adding an `IQuestEmailDispatcher` dependency to `PlayerSignupService`, which today has no email awareness at all; keeps the one-way layering clean |

## Package Legitimacy Audit

Not applicable — this phase introduces zero new third-party packages. All libraries used (EF Core, Hangfire, AutoMapper, xUnit/NSubstitute/FluentAssertions) are already installed, pinned, and in active use elsewhere in the codebase, verified directly via `.csproj` inspection rather than a fresh registry lookup.

## Architecture Patterns

### System Architecture Diagram

```
[Player clicks Vote Yes/Maybe/No button]
        │  (fetch POST, __RequestVerificationToken, matches existing revokeSignup() pattern)
        ▼
[QuestController.ChangeVote(id, vote)]  (NEW controller action — replaces narrow ChangeVoteToYes)
        │  - loads quest + signup, validates ownership
        │  - no capacity hard-reject (VOTE-01 removes the BadRequest-when-full block)
        ▼
[QuestService.ChangeVoteAsync / RevokeSignupAsync]  (Domain orchestration layer)
        │
        ├─▶ [PlayerSignupRepository]  — persist vote + IsSelected + LastVoteChangeTime
        │
        ├─▶ decision: was signup previously IsSelected, and did it just become
        │             unselected (voted No) or removed (Revoke)?
        │        │
        │        ├── NO  → done, no promotion (VOTE-05: Maybe keeps seat)
        │        │
        │        └── YES → [PlayerSignupRepository.GetTopWaitlistedCandidateAsync(questId)]
        │                       (ORDER BY vote priority DESC, then LastVoteChangeTime ?? SignupTime ASC)
        │                   │
        │                   ▼
        │             flip candidate.IsSelected = true
        │                   │
        │                   ▼
        │             [IQuestEmailDispatcher.EnqueueWaitlistPromotedEmail(candidate only)]
        │                   │
        │                   ▼
        │             [Hangfire: QuestWaitlistPromotedEmailJob]
        │                   │
        │                   ▼
        │             [WaitlistPromoted.razor → HtmlRenderer → Resend]
        │                   (sent ONLY to the promoted player — never to the voter,
        │                    never broadcast to the rest of the waitlist — VOTE-07)
        ▼
[Details.cshtml / Details.Mobile.cshtml re-render]
        │
        ▼
[Waitlist section: ordered via new centralized ordering method,
 shared between desktop and mobile views]
```

### Recommended Project Structure
No new folders — this phase adds files to existing locations:
```
QuestBoard.Repository/
├── Migrations/                          # new migration: AddLastVoteChangeTimeToPlayerSignup
├── Entities/PlayerSignupEntity.cs        # + LastVoteChangeTime property
└── PlayerSignupRepository.cs             # rewrite ChangeVoteToYesAndSelectAsync → generalized ChangeVoteAsync; add GetTopWaitlistedCandidateAsync

QuestBoard.Domain/
├── Models/QuestBoard/PlayerSignup.cs     # + LastVoteChangeTime property
├── Interfaces/IPlayerSignupRepository.cs # new method signatures
├── Interfaces/IPlayerSignupService.cs    # new method signatures
├── Interfaces/IQuestEmailDispatcher.cs   # + EnqueueWaitlistPromotedEmail
├── Services/PlayerSignupService.cs       # generalized vote-change method
└── Services/QuestService.cs              # + promotion orchestration + email dispatch call

QuestBoard.Service/
├── Controllers/QuestBoard/QuestController.cs   # new ChangeVote action(s); RevokeSignup wired to promotion
├── Services/HangfireQuestEmailDispatcher.cs    # + EnqueueWaitlistPromotedEmail implementation
├── Jobs/QuestWaitlistPromotedEmailJob.cs       # NEW — mirrors QuestFinalizedEmailJob.cs
├── Components/Emails/WaitlistPromoted.razor    # NEW — mirrors QuestFinalized.razor visual style
├── Views/Quest/Details.cshtml                  # waitlist section rewritten: 3-button UI, new ordering
└── Views/Quest/Details.Mobile.cshtml           # NEW waitlist section + vote buttons (parity, D-05)
```

### Pattern 1: Generalized Vote-Change Method (replaces narrow ChangeVoteToYesAndSelectAsync)
**What:** A single repository/service method that accepts the target `VoteType` (Yes/Maybe/No) instead of three near-duplicate methods, matching the "consolidating into a single 'vote + auto-promote' code path" integration point CONTEXT.md already anticipated.
**When to use:** Any post-finalization vote change, whether the player is currently selected or waitlisted.
**Example (illustrative, not literal code to paste):**
```csharp
// PlayerSignupRepository.cs — corrected enum usage (fixes the Vote=0 bug)
public async Task<bool> ChangeVoteAsync(
    int playerSignupId, int proposedDateId, VoteType vote, CancellationToken cancellationToken = default)
{
    var entity = await DbSet.Include(ps => ps.DateVotes)
        .FirstOrDefaultAsync(ps => ps.Id == playerSignupId, cancellationToken)
        ?? throw new ArgumentException("Player signup not found", nameof(playerSignupId));

    var wasSelected = entity.IsSelected;

    var existingVote = entity.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == proposedDateId);
    if (existingVote != null) existingVote.Vote = (int)vote;   // correct cast — was hardcoded 0
    else entity.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = proposedDateId, PlayerSignupId = playerSignupId, Vote = (int)vote });

    entity.LastVoteChangeTime = DateTime.UtcNow;   // VOTE-03: reset ordering timestamp on every vote change

    if (vote == VoteType.Yes && !wasSelected)
    {
        // VOTE-01: never reject — select only if there is room, else the row stays
        // unselected and simply becomes the top-priority waitlist entry via the
        // Yes-vote + earliest-timestamp ordering. Do NOT hard-reject here.
        entity.IsSelected = HasAvailableSeat(entity.QuestId); // pseudocode — actual seat check happens in caller with fresh count
    }
    else if (vote == VoteType.No && wasSelected)
    {
        entity.IsSelected = false;   // VOTE-04: seat frees; caller must trigger promotion check
    }
    // VoteType.Maybe: IsSelected is left unchanged in all cases (VOTE-05)

    await DbContext.SaveChangesAsync(cancellationToken);
    return wasSelected && !entity.IsSelected;   // true = "a seat just freed, go find someone to promote"
}
```
*Source: derived from reading the existing `ChangeVoteToYesAndSelectAsync` at `QuestBoard.Repository/PlayerSignupRepository.cs:21-50` — not copied verbatim, illustrates the corrected pattern.*

### Pattern 2: Centralized Waitlist Ordering
**What:** A repository or service method returning the waitlist already sorted by vote priority (Yes > Maybe > No) then by `LastVoteChangeTime ?? SignupTime` ascending — instead of the current inline Razor LINQ (`Details.cshtml:67`, `.OrderBy(ps => ps.SignupTime)` only, no vote-priority component at all today).
**When to use:** Both `Details.cshtml` and `Details.Mobile.cshtml` waitlist sections; call once from the controller/ViewModel construction, not duplicated in each view.
**Example:**
```csharp
// Recommended: expose as a method on PlayerSignup domain model or a small
// static helper (e.g. WaitlistOrdering.Sort(IEnumerable<PlayerSignup>)) that both
// Details.cshtml and Details.Mobile.cshtml call — same object graph, same sort, zero duplication.
public static IEnumerable<PlayerSignup> OrderWaitlist(this IEnumerable<PlayerSignup> waitlist, int finalizedProposedDateId) =>
    waitlist
        .OrderByDescending(ps => VotePriority(ps, finalizedProposedDateId))   // Yes=2, Maybe=1, No/none=0
        .ThenBy(ps => ps.LastVoteChangeTime ?? ps.SignupTime);
```
*This is a NEW pattern — no direct precedent exists in the codebase today; the closest analog is the inline vote-grouping LINQ already used in `Manage.cshtml` (`yesVotes`/`maybeVotes`/`noVotes` split, then `OrderBy(SignupTime)` within each group) which this pattern generalizes into one combined sort.*

### Pattern 3: Reused Fetch/AJAX Pattern for New Vote Buttons
**What:** The existing `changeVoteToYes()` / `revokeSignup()` JS functions in `Details.cshtml` (POST/DELETE with `FormData` carrying `__RequestVerificationToken`, `location.reload()` on success, `alert()` on failure).
**When to use:** All three new Vote Yes/Maybe/No buttons, both desktop and mobile.
**Example:**
```javascript
// Source: QuestBoard.Service/Views/Quest/Details.cshtml:871-891 (existing changeVoteToYes pattern)
function changeVote(questId, vote) {
    const formData = new FormData();
    formData.append('__RequestVerificationToken', '@tokens.RequestToken');
    formData.append('vote', vote);   // NEW: 0=No, 1=Maybe, 2=Yes
    fetch(`/Quest/ChangeVote/${questId}`, { method: "POST", body: formData })
        .then(res => res.ok ? location.reload() : res.text().then(t => alert(`Failed to change vote: ${t}`)))
        .catch(() => alert("An error occurred while changing vote."));
}
```

### Anti-Patterns to Avoid
- **Repurposing `SignupTime` for vote-change ordering:** Confirmed to break the literal "Signed up: {time}" display in `Manage.cshtml` (lines 246, 289, 325, 478), `Details.cshtml` (line 747, 768, 789), and mirrored mobile/QuestLog views. Use a new field instead.
- **Duplicating waitlist-ordering LINQ in both `Details.cshtml` and `Details.Mobile.cshtml`:** This is exactly the pattern that produced the MOBILE-01/MOBILE-02 backlog fixed in Phase 43 (mobile view silently drifting from desktop). Centralize the ordering logic once.
- **Sending the promotion email from the same job/method that handles the voter's/revoker's own request:** VOTE-07 requires the email goes ONLY to the promoted player. Do not accidentally include the freeing player in any recipient list — the existing `FinalizeQuestAsync` pattern computes `recipientEmails` as an array; the new promotion path must construct a single-recipient array (or singular parameters) to make broadcast-by-accident structurally difficult.
- **Trusting client-side "seat available" state:** Mirror the existing pattern in `ChangeVoteToYes`/`JoinFinalizedQuest` — always recompute `selectedPlayersCount` server-side immediately before deciding `IsSelected`, never trust anything from the request body.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Sending the promotion email | A new ad-hoc `SmtpClient`/direct-Resend call | Existing `IQuestEmailDispatcher` → Hangfire job → `IEmailRenderService`/`HtmlRenderer` → `IEmailService` pipeline | This exact pipeline is used 6+ times already (`FinalizeQuestAsync`, `SessionReminderJob`, `WelcomeEmailJob`, etc.) with retry policy, dedup guards, and scoped DI already solved |
| Vote-priority + timestamp sort | Hand-rolled comparer class or repeated inline Razor LINQ | A single ordering method (extension method or repository query) shared by desktop+mobile | Avoids the exact mobile/desktop drift pattern this project just spent Phase 43 fixing |
| Enum-to-int mapping validation | Manual eyeballing of enum values when writing repository code | The existing `EntityProfileEnumCastTests.cs` round-trip test pattern — add a regression test asserting `(int)VoteType.Yes == 2` explicitly, or better, a test that exercises the actual `ChangeVoteAsync` repository method end-to-end | The `Vote = 0` bug happened because nobody wrote a test verifying the actual persisted int value; this project already has the exact test pattern (`AssertRoundTrips<TEnum>`) needed to catch this class of bug |

**Key insight:** Every piece of infrastructure this phase needs (email pipeline, migration pattern, AJAX fetch convention, test harness) already exists in this codebase with 1-6+ working examples. The risk in this phase is not "which library to pick" — it's correctly wiring together existing pieces without duplicating logic across desktop/mobile views or introducing a second silent enum-casting bug.

## Common Pitfalls

### Pitfall 1: Reintroducing the VoteType int-casting bug in the rewritten method
**What goes wrong:** A future edit or copy-paste from the old `ChangeVoteToYesAndSelectAsync` reintroduces a hardcoded `Vote = 0` (or `Vote = 2` typed as a "magic number" without the enum cast) instead of `(int)VoteType.Yes`.
**Why it happens:** The vote enum is stored as `int` on `PlayerDateVoteEntity.Vote`, and the original bug came from a stale comment (`// VoteType.Yes = 0`) that was never re-verified against the actual enum declaration order.
**How to avoid:** Always write `(int)VoteType.X`, never a bare literal. Add a regression test (see Don't Hand-Roll table) asserting the actual persisted value for a Yes vote change.
**Warning signs:** Any repository/service code that assigns `.Vote = <int literal>` without a `(int)VoteType.___` cast should be flagged in code review.

### Pitfall 2: Promotion check omitted from one of the two call sites
**What goes wrong:** The "Vote No" action gets promotion wired correctly, but `RevokeSignup` (or vice versa) is missed, silently leaving freed seats un-promoted in one of the two paths.
**Why it happens:** These are two separate controller actions with separate code paths (`RevokeSignup` at `QuestController.cs:605` is `[HttpDelete]`; the new Vote No action will be `[HttpPost]`) — easy to update one and forget the other since they don't share a base method today.
**How to avoid:** Both call into the exact same `QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync(...)`-style method rather than each independently reimplementing the "was this person selected, is there now a candidate" logic. Write a unit test for each call site independently (mirroring `QuestServiceTests.cs`'s existing per-scenario test style).
**Warning signs:** If the promotion logic exists in two places with even slightly different LINQ, that's the bug already happening.

### Pitfall 3: Ordering by `LastVoteChangeTime` without the null-coalescing fallback
**What goes wrong:** A player who has never changed their vote since original signup (i.e., they signed up, got auto-added to the waitlist, and never touched a vote button) has `LastVoteChangeTime == null`. If the sort uses `ThenBy(ps => ps.LastVoteChangeTime)` without `?? ps.SignupTime`, all such players sort as if their timestamp were the earliest possible (nulls typically sort first in SQL Server ascending order) or get grouped incorrectly relative to players who have changed votes.
**Why it happens:** New nullable field, easy to forget the fallback when writing the `OrderBy`/`ThenBy` clause.
**How to avoid:** Always sort by `ps.LastVoteChangeTime ?? ps.SignupTime`, both in the new centralized ordering method and if there's ever a raw SQL/EF query equivalent (`ISNULL(LastVoteChangeTime, SignupTime)` in SQL Server terms).
**Warning signs:** A player who has been sitting on the waitlist since original signup (never touched a vote button) unexpectedly jumping to the top or bottom of the waitlist relative to their actual signup time.

### Pitfall 4: Assuming `RevokeSignup`'s hard-delete needs no vote-check before promoting
**What goes wrong:** Promoting the next candidate after a Revoke without checking whether the revoked signup was actually `IsSelected` — e.g., a waitlisted player revoking their own signup should NOT trigger a promotion (there's no freed seat, they weren't occupying one).
**Why it happens:** It's tempting to wire "always check-and-promote after any Revoke" without the `wasSelected` guard.
**How to avoid:** Exactly mirror VOTE-04's wording: promotion only fires "when that player votes No **or fully revokes their signup**" — but only if they were previously selected. A waitlisted player revoking changes nothing about seat occupancy.
**Warning signs:** Promotion firing (and an email going out) when a waitlisted (non-selected) player simply removes themselves from a quest they were never confirmed for.

### Pitfall 5: Mobile view drifting from desktop again
**What goes wrong:** `Details.Mobile.cshtml` waitlist section is built with slightly different filtering/ordering logic than `Details.cshtml`, recreating exactly the MOBILE-01/MOBILE-02 pattern this project just spent Phase 43 fixing.
**Why it happens:** Mobile views in this codebase are hand-maintained separate `.cshtml` files (not a shared partial for this section today), so there's no compiler-enforced parity.
**How to avoid:** Per D-05 and the centralization recommendation above, both views should call the exact same ordering method/ViewModel property rather than each writing their own inline `Where`/`OrderBy`. Consider extracting a shared `_WaitlistTable.cshtml` partial (or two partials, one per layout) that both `Details.cshtml` and `Details.Mobile.cshtml` render, parameterized by the already-ordered list.
**Warning signs:** Any PR that touches the waitlist section of one Details view but not the other.

## Code Examples

### Existing capacity-check pattern to mirror (and the exact line VOTE-01 removes)
```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:577-585 (ChangeVoteToYes, current)
var selectedPlayersCount = quest.PlayerSignups
    .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
    .Count();

if (selectedPlayersCount >= quest.TotalPlayerCount)
{
    return BadRequest("No available spots in this quest.");   // <-- VOTE-01 removes this hard rejection
}
```
The replacement logic keeps the `selectedPlayersCount`/`TotalPlayerCount` comparison (VOTE-01 does not remove capacity tracking, only the rejection) but uses it to decide `IsSelected = true` vs. `IsSelected = false` (waitlisted) rather than returning `BadRequest`.

### Existing email dispatch pattern to mirror exactly
```csharp
// Source: QuestBoard.Domain/Services/QuestService.cs:36-45 (FinalizeQuestAsync)
dispatcher.EnqueueFinalizedEmail(
    quest.Id,
    quest.GroupId,
    finalizedDate,
    recipientEmails,   // array — but for promotion, this becomes a SINGLE recipient only (VOTE-07)
    playerNames,
    quest.Title,
    quest.DungeonMaster?.Name ?? "Unknown DM",
    quest.Description,
    quest.ChallengeRating);
```
New method signature recommendation: `EnqueueWaitlistPromotedEmail(int questId, int groupId, DateTime finalizedDate, string recipientEmail, string playerName, string questTitle, string dmName, string questDescription, int challengeRating)` — singular `recipientEmail`/`playerName` (not arrays) makes it structurally impossible to accidentally broadcast to more than one player.

### Enum declaration that confirms the bug
```csharp
// Source: QuestBoard.Domain/Enums/VoteType.cs — verified current content
public enum VoteType
{
    No,      // = 0
    Maybe,   // = 1
    Yes      // = 2
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `ChangeVoteToYesAndSelectAsync` — hard-rejects when full, only supports Yes | Generalized `ChangeVoteAsync(vote)` — never rejects, supports Yes/Maybe/No | This phase (44) | VOTE-01 requirement; removes the `BadRequest("No available spots...")` block |
| Waitlist ordered only by `SignupTime` ascending | Ordered by vote priority (Yes>Maybe>No) then `LastVoteChangeTime ?? SignupTime` | This phase (44) | VOTE-02, VOTE-03 |
| `RevokeSignup` with no promotion wiring | `RevokeSignup` triggers promotion check when the revoked signup was selected | This phase (44) | VOTE-04 |
| No waitlist UI on mobile at all | Full waitlist + vote-change parity on `Details.Mobile.cshtml` | This phase (44), per D-05 | Closes the same mobile-parity gap pattern fixed in Phase 43 for MOBILE-01/02 |

**Deprecated/outdated:**
- `ChangeVoteToYes` controller action and `ChangeVoteToYesAndSelectAsync` repository method: superseded by the generalized vote-change path in this phase. Decide during planning whether to delete these outright or fold their logic into the new unified method (recommend: delete, since the new method is a strict superset).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core nullable-column-with-no-backfill is the right general migration pattern for this case | Standard Stack / Architecture Patterns | Low — this claim is backed primarily by the project's own existing `AddQuestCloseFields` migration precedent (`[VERIFIED: codebase]`), with the general EF Core guidance itself only `[CITED: web search, not cross-checked against docs.microsoft.com directly]` as a secondary confirmation. If wrong, worst case is an unnecessary `defaultValueSql` clause — not a correctness risk. |
| A2 | Recommended home for promotion orchestration is `QuestService` rather than `PlayerSignupService` | Summary / Don't Hand-Roll | Medium — this is an architectural judgment call based on which service already owns `IQuestEmailDispatcher`, not a hard technical constraint. If the planner disagrees, `PlayerSignupService` could instead take a new `IQuestEmailDispatcher` dependency; either is technically viable, but mixing the two half-and-half across both services would violate this project's own layering conventions. |
| A3 | Deleting `ChangeVoteToYes`/`ChangeVoteToYesAndSelectAsync` outright (rather than keeping alongside the new generalized method) | State of the Art | Low — functionally the new method is a superset; if any external caller/test depends on the exact old method signature, a grep should confirm before deletion (none found in this research beyond `QuestController.cs` itself). |

## Open Questions

1. **Exact new controller action shape: one `ChangeVote(id, vote)` action, or three separate actions (`VoteYes`/`VoteMaybe`/`VoteNo`)?**
   - What we know: D-01 specifies three distinct buttons; the existing `ChangeVoteToYes` is a single-purpose action.
   - What's unclear: Whether the planner should implement one parameterized action (cleaner, matches the "single vote+auto-promote path" integration point CONTEXT.md flags) or three thin actions that all delegate to one shared service method (slightly more RESTful/discoverable, matches the one-action-per-button convention already used for `RevokeSignup`).
   - Recommendation: One action taking a `VoteType` parameter (mirrors `UpdateSignup`'s existing pattern of accepting vote data in the request body) — three buttons POST to the same endpoint with different `vote` values, all funneling into the same `ChangeVoteAsync` service method. Keeps the "single path" property CONTEXT.md already identified as desirable.

2. **Should `ChangeVoteToYes`/`ChangeVoteToYesAndSelectAsync` be deleted or deprecated-in-place?**
   - What we know: The new generalized method is a strict superset of the old one's Yes-only, always-select behavior would need adjusting anyway (since VOTE-01 requires it to no longer force-reject when full).
   - What's unclear: Whether any other part of the codebase calls these directly (verified: only `QuestController.ChangeVoteToYes` calls the repository method; no other call sites found).
   - Recommendation: Delete both and replace with the new generalized action/method — verified no other callers exist.

3. **Partial view extraction for the waitlist table (desktop vs. mobile)**
   - What we know: D-05 requires mobile parity; centralizing the *ordering logic* server-side is recommended above regardless.
   - What's unclear: Whether the *rendering* (HTML/Razor markup) should also be extracted into a shared partial, given the two views use different CSS classes/layout patterns (`Manage.cshtml` vs `Manage.Mobile.cshtml` today keep entirely separate markup despite sharing data).
   - Recommendation: Keep markup separate (consistent with the existing Desktop/Mobile split convention throughout this codebase, e.g. `Manage.cshtml`/`Manage.Mobile.cshtml`, `Details.cshtml`/`Details.Mobile.cshtml` today), but centralize the *data* (ordered list, vote-eligibility flags) so both views consume identical, already-sorted data rather than independently computing it.

## Environment Availability

Not applicable — this phase has no new external tool/service dependencies. SQL Server, Hangfire, and Resend (email) are already configured and running per this project's existing deployment (per user memory: app runs directly on Linux at `/opt/questboard/`, not Docker for production; `docker-compose` is the dev-environment path per CLAUDE.md). No new environment setup is required for this phase.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + NSubstitute 5.3.0 + FluentAssertions 8.10.0 (verified via `QuestBoard.UnitTests.csproj`) |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (no separate test-runner config file) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignup\|FullyQualifiedName~QuestService"` |
| Full suite command | `dotnet test` (run from repo root; builds all projects then runs `QuestBoard.UnitTests`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VOTE-01 | Voting Yes on a full quest sets `IsSelected=false` (waitlisted), never throws/rejects | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ❌ Wave 0 — no `PlayerSignupRepositoryTests.cs` exists today |
| VOTE-02 | Waitlist ordering method sorts Yes > Maybe > No, then by timestamp | unit | `dotnet test --filter FullyQualifiedName~WaitlistOrdering` | ❌ Wave 0 |
| VOTE-03 | `LastVoteChangeTime` updates on every vote mutation | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync` | ❌ Wave 0 |
| VOTE-04 | Selected player votes No or is revoked → next waitlisted candidate promoted | unit | `dotnet test --filter FullyQualifiedName~QuestServiceTests.PromoteNextWaitlisted` | ❌ Wave 0 — extend existing `QuestServiceTests.cs` |
| VOTE-05 | Selected player votes Maybe → `IsSelected` stays true, no promotion call | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync_Maybe` | ❌ Wave 0 |
| VOTE-06 | Waitlisted player votes No → record retained, `IsSelected` stays false | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync_WaitlistedVotesNo` | ❌ Wave 0 |
| VOTE-07 | Promotion email dispatched to exactly one recipient (the promoted player), never the freeing player | unit | `dotnet test --filter FullyQualifiedName~EnqueueWaitlistPromotedEmail` | ❌ Wave 0 — mirror `QuestServiceTests.FinalizeQuestAsync_*` dispatcher-call-assertion style |
| Regression | `(int)VoteType.Yes` cast persists correctly (bug fix regression guard) | unit | `dotnet test --filter FullyQualifiedName~EntityProfileEnumCastTests` | ✅ exists — extend or add a repository-level test that exercises the actual persisted `Vote` int value after `ChangeVoteAsync(..., VoteType.Yes)` |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~PlayerSignup|FullyQualifiedName~QuestService"` (targeted, <30s)
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` (or extend `QuestServiceTests.cs`) — covers VOTE-01, VOTE-03, VOTE-05, VOTE-06, and the enum-cast bug-fix regression
- [ ] Extend `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — covers VOTE-04, VOTE-07 (promotion orchestration + single-recipient email dispatch assertion)
- [ ] New test class for the centralized waitlist-ordering method (VOTE-02) — no existing precedent, needs a fresh `WaitlistOrderingTests.cs` (or wherever the ordering logic ends up living)
- [ ] Framework install: none — `xunit.v3`/`NSubstitute`/`FluentAssertions` already installed

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Vote-change actions reuse existing `[Authorize]` + cookie-auth pattern already on `RevokeSignup`/`ChangeVoteToYes` — no new auth surface |
| V3 Session Management | No | No session changes in this phase |
| V4 Access Control | Yes | New `ChangeVote` action must verify the acting user owns the `PlayerSignup` row being modified — exactly mirrors the existing `playerSignup.Player.Id == user.Id`-equivalent check pattern already in `RevokeSignup`/`ChangeVoteToYes`/`UpdateSignup` (all find the signup via `quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)`, never trust a posted signup ID directly) |
| V5 Input Validation | Yes | `vote` parameter must be validated as a defined `VoteType` enum value server-side (reject undefined ints) — mirrors the `Enum.IsDefined` check pattern already exercised in `EntityProfileEnumCastTests.cs` |
| V6 Cryptography | No | No new cryptographic operations |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on the new vote-change POST endpoint | Tampering | `[ValidateAntiForgeryToken]` — already the convention on every mutating action in `QuestController.cs`; new action must carry the same attribute |
| IDOR — voting on behalf of another player's signup by guessing/passing a different `PlayerSignup` id | Elevation of Privilege | Resolve the signup server-side via the authenticated user's id (`quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)`), never accept a raw `playerSignupId` from the client as the target to mutate — exactly the pattern already used by every existing signup-mutation action in this controller |
| Undefined/out-of-range `vote` int smuggled in the POST body | Tampering | Validate with `Enum.IsDefined(typeof(VoteType), postedValue)` before casting, reject with `BadRequest` otherwise |
| Promotion email leaking to the wrong recipient due to a shared/looped recipient array | Information Disclosure | Use a singular-recipient dispatcher signature (not `string[]`) for `EnqueueWaitlistPromotedEmail`, making it structurally harder to accidentally pass multiple recipients (see Code Examples section) |

## Sources

### Primary (HIGH confidence — verified directly against current source in this repository)
- `QuestBoard.Domain/Enums/VoteType.cs` — confirmed enum ordering `No=0, Maybe=1, Yes=2`
- `QuestBoard.Repository/PlayerSignupRepository.cs` — confirmed `Vote = 0` bug in both branches of `ChangeVoteToYesAndSelectAsync`
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — confirmed `ChangeVoteToYes` (line ~553), the hard-reject block (line ~582), `RevokeSignup` (line ~605), `UpdateSignup`'s finalized-quest block (line ~492), `JoinFinalizedQuest` (line ~410)
- `QuestBoard.Service/Views/Quest/Details.cshtml` — confirmed existing waitlist table (lines ~189-303), `SignupTime`-only ordering (line 67), existing `revokeSignup()`/`changeVoteToYes()` JS fetch pattern (lines ~848-891)
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` — confirmed no waitlist section, no vote-change UI exists today
- `QuestBoard.Service/Views/Quest/Manage.cshtml`, `Manage.Mobile.cshtml` — confirmed multiple literal "Signed up: {SignupTime}" display sites, ruling out repurposing `SignupTime`
- `QuestBoard.Service/Views/QuestLog/Details.cshtml`, `Details.Mobile.cshtml`, `Index.cshtml`, `Views/Quest/_QuestCard.cshtml`, `Views/Quest/Index.cshtml` — additional `SignupTime`/`IsSelected` read sites found beyond CONTEXT.md's list, all read-only/display, unaffected by the ordering change beyond confirming no hidden write dependency on `SignupTime`
- `QuestBoard.Domain/Services/QuestService.cs` — confirmed `FinalizeQuestAsync`'s `EnqueueFinalizedEmail` call pattern and `CreateFollowUpQuestAsync`'s use of `IsSelected` (not `SignupTime`) for follow-up-quest player import
- `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs`, `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs`, `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs`, `QuestBoard.Service/Components/Emails/QuestFinalized.razor` — confirmed the exact dispatcher → Hangfire job → Razor/HtmlRenderer → email-service pipeline to replicate
- `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs` — confirmed the project's own precedent for adding a new nullable `DateTime` column via migration
- `QuestBoard.UnitTests/Services/QuestServiceTests.cs`, `EntityProfileEnumCastTests.cs`, `QuestBoard.UnitTests.csproj` — confirmed test framework/versions and existing test patterns to extend

### Secondary (MEDIUM confidence)
- None beyond the codebase-verified findings above — this phase required no external library research.

### Tertiary (LOW confidence)
- WebSearch: "EF Core migration add nullable DateTime column with default value existing rows" — general EF Core migration guidance, used only as secondary confirmation of a pattern already established by this project's own `AddQuestCloseFields` migration (the primary, `[VERIFIED: codebase]` source for that recommendation).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all versions verified directly from `.csproj` files in this repo
- Architecture: HIGH — every pattern recommended has a direct, verified precedent already in this codebase (email dispatch, migration style, AJAX fetch, AutoMapper field mapping)
- Pitfalls: HIGH — each pitfall is grounded in an actual verified line of existing code (the enum bug, the `SignupTime` display sites, the missing mobile waitlist section)

**Research date:** 2026-07-04
**Valid until:** 30 days (stable internal codebase, no fast-moving external dependencies)
