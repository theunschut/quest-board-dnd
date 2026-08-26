---
phase: 72
slug: change-character-on-an-existing-signup
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
register_authored_at_plan_time: true
created: 2026-08-26
---

# Phase 72 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

Register was authored at plan time across all four PLAN.md `<threat_model>` blocks (15 unique
threats after dedup) and verified against the implementation at ASVS L1 depth. Two further
threats were added retroactively to cover code changed *after* planning, by the two code-review
blocker fixes — those changes touched authorization-adjacent paths the plan-time register never
modelled, so they are recorded here rather than assumed safe.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser form POST → `QuestController.UpdateSignupCharacter` | `questId` and `characterId` arrive fully attacker-controlled; the modal's pre-selected value is a client-side hint with no authority | Character id (integer reference to a per-group owned entity) |
| ASP.NET Core Session → `IActiveGroupContext.ActiveGroupId` | The active board identity that scopes both the `CharacterEntity` global query filter and the action's explicit comparison | Group/tenant identity |
| Caller identity → `PlayerSignup` lookup | The signup is always resolved from the authenticated user, never from a posted signup id | Signup row ownership |
| Trigger `data-*` attributes → modal priming script | Client-side DOM values with no authority; they decide what the picker shows, never what the server accepts | Character id + display label |
| Character-supplied text (name) → option text and `data-current-character-label` | User-authored strings rendered into HTML | Free-text character names |
| Request User-Agent → `MobileDetectionMiddleware` → view selection | An attacker-controlled request header decides which view file renders; it must never decide what data is returned | Template selection only |
| Other players' rows → the acting player's browser | Both Details pages already render every participant's character name; neither may render a *control* on a row the viewer does not own | Participant roster |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-72-01 | Elevation of Privilege | `UpdateSignupCharacter` `characterId` parameter | high | mitigate | `character.OwnerId != user.Id` → `BadRequest` (`QuestController.cs:557`). Test: `UpdateSignupCharacter_Post_WithAnotherUsersCharacterInSameBoard_ReturnsBadRequestAndLeavesCharacterUnchanged` asserts both the 400 and the unchanged row. | closed |
| T-72-02 | Information Disclosure | Cross-board `characterId` reaching `GetCharacterWithDetailsAsync` | medium | mitigate | Primary control is the `CharacterEntity` global query filter (`QuestBoardContext.cs:328-331`) — scopes to `ActiveGroupId`, no SuperAdmin escape hatch, returns nothing when no group is active. Explicit `character.GroupId != groupId` → `BadRequest` added at `QuestController.cs:567` as insurance. Test: `..._WithCharacterFromAnotherBoard_ReturnsBadRequestAndLeavesCharacterUnchanged`. | closed |
| T-72-03 | Tampering (CSRF) | Character-change POST | medium | accept | `[ValidateAntiForgeryToken]` present on the action (`QuestController.cs:527`), untouched by this phase. | closed |
| T-72-04 | Spoofing | Unauthenticated access to the change endpoint | medium | accept | `[Authorize]` present (`QuestController.cs:528`); `GetUserAsync(User)` returning null still yields `Challenge()`. | closed |
| T-72-05 | Tampering | Targeting another player's signup row | high | mitigate | Signup resolved as `quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)`. The action signature takes `(int questId, int? characterId)` only — no client-supplied signup id exists, so there is no IDOR surface. | closed |
| T-72-06 | Information Disclosure | Widened `ViewBag.UserCharacters` | low | accept | Widening adds only the caller's own inactive characters. Owner scoping (`GetCharactersByOwnerIdAsync(currentUser.Id)`, `QuestController.cs:332`) and board scoping (query filter) both unchanged. | closed |
| T-72-07 | Tampering | Option list and injected current-character option | medium | mitigate | Option values carry no authority; every posted value is re-resolved and re-checked server-side (T-72-01/T-72-02). A tampered value yields a 400, not a write. | closed |
| T-72-08 | Tampering (stored XSS) | Character name in option text and `data-current-character-label` | medium | mitigate | Razor `@` encoding throughout; `Html.Raw` count is 0 and `innerHTML` count is 0 across `_CharacterSelectModal.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`; the priming script writes labels with `textContent` (2 uses). | closed |
| T-72-09 | Tampering (CSRF) | `#characterSelectForm` | medium | accept | `<form asp-action="UpdateSignupCharacter" method="post">` emits the antiforgery token automatically; no hand-built `fetch` exists in the partial. The Remove path submits this same form, so the token travels with it. | closed |
| T-72-10 | Denial of Service (self-inflicted data loss) | Silent character wipe on a no-op save | high | mitigate | Inject-if-missing branch (`_CharacterSelectModal.cshtml:95-98`) creates a real option rather than assigning `select.value` blindly, so an untouched Save re-posts the same id. Server-side backstop: `..._ResubmittingTheCurrentRetiredCharacter_LeavesItAssigned`. | closed |
| T-72-11 | Elevation of Privilege | Change trigger on a desktop row the viewer does not own | high | mitigate | Both filled-state triggers guarded by `isCurrentUser` (`Details.cshtml:128`, `:259`), computed per row from `ViewBag.CurrentUserId`. Server-side backstop is T-72-05. | closed |
| T-72-12 | Information Disclosure | Widened pickers exposing other players'/boards' characters | low | accept | Owner-filtered in the controller, board-filtered by the query filter. No new disclosure surface on either platform. | closed |
| T-72-13 | Elevation of Privilege | Change trigger on a mobile row the viewer does not own | high | mitigate | Both filled-state triggers guarded by `isCurrentUser` (`Details.Mobile.cshtml:210`, `:255`). Test: `MobileDetails_MobileUserAgent_ForAnotherPlayersRow_RendersNoTriggerForThatRow`. | closed |
| T-72-14 | Spoofing | Forged User-Agent selecting a different view | low | accept | The platform split chooses a template only. Both templates render the same model from the same authorized action with identical board and ownership scoping. A spoofed User-Agent yields a differently-styled page, never additional data. | closed |
| T-72-SC | Tampering (supply chain) | npm/pip/cargo/NuGet installs | high | accept | Phase installs zero packages. Verified: `git diff 1d7cca5..HEAD` touches no `*.csproj`, `package.json`, `package-lock.json`, or `Directory.Packages.props`. | closed |
| T-72-15 | Elevation of Privilege | Ownership gate on the two widened signup-time save paths | high | mitigate | **Added post-plan.** The CR-02 fix removed the `CharacterStatus.Active` clause from `Details` POST (`:414`) and `JoinFinalizedQuest` (`:461`). Status was never an authorization control; ownership is, and `character.OwnerId != user.Id` survives at both sites, as do `[HttpPost] [ValidateAntiForgeryToken] [Authorize]`. New test `JoinFinalizedQuest_Post_WithAnotherPlayersCharacter_CreatesNoSignup` pins this explicitly. | closed |
| T-72-16 | Elevation of Privilege (IDOR) | New `IPlayerSignupRepository.UpdateCharacterAsync(int playerSignupId, …)` write path | high | mitigate | **Added post-plan.** The CR-01 fix introduced a repository method keyed on an arbitrary signup id. Single caller chain verified: `QuestController.cs:574` passes `playerSignup.Id`, derived from the user-scoped lookup in T-72-05 — never from request input. No other caller exists in Domain, Service or Repository. | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `workflow.security_block_on` count toward `threats_open`*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-72-01 | T-72-02, T-72-15 | The explicit board-scope comparison exists **only** on `UpdateSignupCharacter`. The other two character-accepting save paths — `Details` POST and `JoinFinalizedQuest` — rely solely on the `CharacterEntity` global query filter. This asymmetry is pre-existing (confirmed against `1d7cca5`: the explicit check never existed on either path) and matches T-72-02's stated design, where the filter is the primary control and the explicit comparison is insurance. Recorded so a future reader does not assume all three paths are equally belt-and-braces. | Operator | 2026-08-26 |
| R-72-02 | T-72-14 | The desktop/mobile view split is driven by an attacker-controlled request header. Accepted as a presentation concern, not an authorization boundary, because both templates render the same model from the same authorized action. | Operator (plan-time, 72-04-PLAN.md) | 2026-08-26 |

---

## Post-Plan Findings

The code review that ran after execution found two blocker-class defects. Neither was a
security vulnerability in the confidentiality/integrity-of-others sense, but one is recorded
here because it destroyed user data:

- **CR-01 — date-vote deletion on every character change.** A scalar edit routed through the
  collection-replacing signup update, orphaning the player's `PlayerDateVote` rows. Effect:
  the player silently lost reminder eligibility and waitlist-promotion candidacy while being
  told the change succeeded. This is the same *class* as T-72-10 (self-inflicted data loss on
  the character-change path) but a different mechanism, and the plan-time register did not
  anticipate it. Fixed via T-72-16's targeted write path; regression test verified to fail
  against the pre-fix code.
- **CR-02 — dead-end picker.** Not a security defect; recorded because its fix changed two
  authorization code paths, which is why T-72-15 exists.

Six Warning and four Info findings from that review remain open in `72-REVIEW.md`. None were
classified as security-blocking at ASVS L1.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-26 | 17 | 17 | 0 | Claude (orchestrator, L1 verification) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-26
