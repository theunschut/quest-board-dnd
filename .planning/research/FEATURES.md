# Feature Research

**Domain:** Incremental UX gap-fill on an existing ASP.NET Core MVC campaign-management app (quest signup character selection) + security-maintenance housekeeping
**Researched:** 2026-08-25
**Confidence:** HIGH — grounded directly in this codebase's existing code paths (`QuestController.UpdateSignupCharacter`, `PlayerSignupService.UpdateSignupCharacterAsync`, `Details.cshtml`) and this project's own established precedents from prior milestones (Phase 44, 54, 61, 63), not external competitor research. External "how do RSVP/signup apps handle this" research was deliberately skipped — the governing precedent is this app's own prior decisions, which are more authoritative than generic patterns for a 17-person trusted-group tool.

---

## Feature 1: Change character on an existing quest signup

### Current-state findings (grounds every recommendation below)

- `UpdateSignupCharacter` (`QuestController.cs:523`) has **no `IsFinalized` guard** today — unlike its sibling `UpdateSignup` (date votes, line 496-500), which explicitly `return NotFound()` when the quest is finalized. This asymmetry looks accidental (the action was written before finalization-editing was a designed concept) rather than intentional design.
- The action has **no `SignupRole` guard** — it only checks `playerSignup.Player.Id == user.Id` (ownership). Any role (Player, Spectator, AssistantDM) that owns a signup can already call it.
- The action has **no `IsSelected`/waitlist guard** — it operates on whatever `PlayerSignup` row belongs to the caller, selected or waitlisted alike.
- The existing "+" (add character) button in `Details.cshtml` already renders for **both** selected participants (line ~135-141) and waitlisted players (line ~251-257) — waitlisted players can already attach a character today, just not change one once set.
- The `JoinFinalizedQuest` flow (joining a quest after it's finalized) already lets **all three roles** — Player, AssistantDM, Spectator — pick a character at signup time (`Details.cshtml:344-386`). Character selection was never Player-exclusive.
- Character validation on write (`QuestController.cs:542-549`) rejects anything that isn't `Owner == user.Id && Status == CharacterStatus.Active`. The character dropdown itself (`ViewBag.UserCharacters`, `QuestController.cs:330`) is pre-filtered to `CharacterStatus.Active` only — an already-assigned-but-now-Retired/Dead character would **not appear in that dropdown list** at all.
- Direct precedent for "editing after finalization, no email, no audit trail": Phase 61 ("Edit finalized quest details") established that a DM can edit a finalized quest's metadata (title/description/rewards/CR/player count) without un-finalizing it, with **no email fired** (D-02) and **no time cutoff** (D-03) — because it doesn't touch the locked roster or date. Phase 63 (recap edit permission) similarly ships a permission broaden with **no notification, no audit attribution**.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Change character on an existing signup (desktop Details) | The mirror-image gap of the existing "+" add flow — a player who picked wrong, leveled up a different character, or changed their mind has no way to fix it without DM intervention today. This is the single reported gap driving the milestone item. | LOW | Reuses the existing `UpdateSignupCharacter` action/modal pattern; primarily a view change (render the modal for signups that already have a character, not just empty ones) plus small controller/action refinements below. |
| Change character on an existing signup (mobile Details) | Mobile Details currently has **neither** the add nor the edit affordance — it's a bigger gap than desktop's read-only-render problem. Table stakes because this project has a hard "mobile parity" precedent (Phase 43, 54: every desktop quest-signup capability gets a mobile equivalent, verified as its own checklist item every time). | LOW–MEDIUM | Needs the full add+change capability ported to `Details.Mobile.cshtml`, which today has no character-select UI for existing signups at all — more net-new markup than desktop's edit-in-place change. |
| Clear character back to "no character" | The explicit request scope. Today's "read-only once set" behavior traps a player who signed up with a character that's since been retired/killed, or who simply wants to switch to "no character." | LOW | `characterId` is already nullable end-to-end (`int? characterId` in both the controller action and `UpdateSignupCharacterAsync`) — passing `null` already clears it server-side. The only gap is the UI never offers that option once a character is set. |
| Currently-selected-but-now-inactive character stays visible and selectable in the edit UI | If the dropdown only lists `Active` characters (current behavior) and a signup holds a Retired/Dead character, opening the "change" modal would silently not pre-select anything — and submitting without touching the dropdown could silently overwrite the signup with the dropdown's default option instead of leaving it alone. This is a real correctness bug waiting to happen, not a hypothetical edge case, given `CharacterStatus.Dead` shipped in Phase 52 and a killed-off character mid-campaign is an entirely normal D&D scenario. | LOW | Add the signup's current character to the option list even if inactive (labeled, e.g., "Thorin (Retired)"), pre-selected. Read path is unaffected — this only touches the edit modal's option-building logic. |

### Differentiators

None. This is closing a UX gap in an existing flow, not adding new capability — there is no "competitive advantage" framing that applies to a single-tenant internal tool. The closest thing to a differentiator is **allowing the swap after finalization with zero friction**, which most generic RSVP/scheduling tools lock down — but that's better framed as a deliberate behavioral choice (see Q&A below) than a marketed feature.

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| DM email notification on character swap | Feels like the "safe" default — DM should know who's coming as what. | Email is rate-limited (100/day, 3000/month) and this project has an explicit "batch-first design" constraint at this scale. A cosmetic metadata edit close to session doesn't rise to the bar that earns an email today — quest finalization, session reminders, and waitlist promotion do because they're state changes that affect who shows up; a character swap doesn't change attendance. No prior "edit" feature in this codebase (Phase 61 finalized-quest edit, Phase 63 recap edit) fires email either — this would be the first exception, not a consistent pattern. | The DM already sees the live, current character on Details/Manage whenever they check before the session — that's the existing read surface and it's always live/authoritative, no push notification needed. |
| Audit trail / change history on character swaps | Feels responsible — "who changed what, when." | This project has zero precedent for field-level audit logging anywhere (quest edits, recap edits, character edits — none of them track history), and adding one bespoke audit mechanism for just this one field would be inconsistent with the rest of the app and a disproportionate amount of new schema/UI for a 17-person trusted group where a wrong character pick has zero real-world consequence. | If a `LastModified`/`ModifiedBy`-style audit ever becomes a real ask, it should be scoped as its own cross-cutting milestone applied consistently, not bolted onto this one field first. |
| A confirm ("Are you sure?") step before applying the swap | Character swaps feel like they should be guarded like a delete. | The action is trivially reversible (swap back, or clear and re-add) and costs nothing if done "wrong" — no data loss, no other player affected, no email sent. Reusing the existing modal-then-submit pattern (open modal → pick → Save) is already a soft confirm step; a second "are you sure" on top of that is friction with no corresponding risk reduction. | Keep the existing "open modal, pick, Save" flow as the only gate — same UX weight as the current add-character flow, which has never had a confirm step either. |
| Restricting the swap to Players only (excluding Spectator/AssistantDM) | Seems like "only real participants need a character." | Contradicts existing behavior: `JoinFinalizedQuest` already lets Spectators and AssistantDMs pick a character at signup time, and the current `UpdateSignupCharacter` action has no role check at all. Restricting *editing* more tightly than *creating* would be a net-new inconsistency this milestone would be introducing, not fixing. | Allow all three roles to change/clear their character, matching the existing signup-time behavior exactly. |

### Behavioral Q&A (every question from the brief, answered)

**Q: Should changing be allowed after the quest is finalized, or only before?**
**Recommendation: Allow it after finalization too, with no time cutoff — same posture as Phase 61's finalized-quest-edit precedent.**
Rationale: `UpdateSignup` (date votes) is correctly blocked post-finalization because votes only matter *before* the date is locked — voting after finalization is meaningless. Character selection is the opposite: it matters *most* during and right up to the session, since players level up, retire characters, or bring an alt right up until game night. A DM running a session would want the roster's character list to be accurate at kickoff, not frozen at whatever it was during voting. The existing `UpdateSignupCharacter` action already has no `IsFinalized` guard — this recommendation keeps that as intentional rather than "fixing" it into a restriction that would make the feature less useful than it already silently is today. This mirrors Phase 61's explicit precedent: DMs/players can edit non-roster-affecting quest metadata after finalization with no time cutoff, because the risk (someone picks a different character) is categorically different from the risk `UpdateSignup`'s guard protects against (relitigating who's coming and when).

**Q: Should changing be allowed for waitlisted players as well as finalized/selected participants?**
**Recommendation: Yes — no special-case restriction.**
Rationale: this is already the de facto behavior — the "+" add-character button already renders for waitlisted rows today, and the underlying action has no `IsSelected` check. Restricting *edit* to selected-only would again be introducing a new inconsistency (can add before promotion, can't change after) rather than fixing one. A waitlisted player is exactly as likely to want to swap characters as anyone else, arguably more so since waitlist tenure can span weeks.

**Q: Should the DM be notified when a player swaps their character close to session date?**
**Operator decision flagged, with a clear recommendation: No.** See Anti-Features above for the full rationale (rate limit, no precedent, cosmetic-not-attendance-affecting). If the operator wants a lighter-weight signal than email, the *cheap* alternative — worth surfacing as an option rather than silently dropping — is a visual "recently changed" indicator on the DM's Manage page (no schema needed beyond what a `LastModified` timestamp would already require for other reasons), but that itself edges toward the audit-trail anti-feature above and should not be added speculatively.

**Q: Does the character swap need to be recorded/audited anywhere, or is a silent overwrite fine?**
**Recommendation: Silent overwrite is fine.** See Anti-Features above. No other mutable field in this app (quest description, recap, character stats) is audited; singling out this one field would be inconsistent and disproportionate for a trusted 17-person group where the worst case is "wrong character showed up in a list," self-correctable by the same swap mechanism.

**Q: Should Spectators and AssistantDMs be able to set/change a character, or only Players?**
**Recommendation: Yes, all three roles.** See Anti-Features above — this matches existing `JoinFinalizedQuest` behavior and the current unrestricted `UpdateSignupCharacter` action; restricting it now would be a regression relative to what already works.

**Q: What should happen if the currently-selected character is later archived/deactivated (`CharacterStatus` not Active)?**
**Recommendation: Read paths are unaffected (an inactive character still displays wherever it's shown today — Details, Manage, emails — since none of those paths filter by status). The edit UI must include the current signup's character in its option list even if inactive, clearly labeled (e.g., "(Retired)"/"(Dead)"), pre-selected, so the player can see what's set and explicitly choose to either keep it or replace it with an Active character.** This is a correctness requirement, not a nice-to-have — without it, the dropdown (currently filtered to `Active` only) would either show nothing selected or silently default to the first Active character, and a naive "submit without touching anything" could quietly overwrite a signup that was never meant to change. Do **not** auto-clear an inactive character server-side on some background trigger — that would be surprising, undiscoverable, and unrequested; leave it fully player-controlled.

**Q: Confirmation UX — is a swap destructive enough to warrant a confirm step, or is it trivially reversible?**
**Recommendation: Trivially reversible — no extra confirm step.** See Anti-Features above. The existing modal-open-then-Save interaction is already the right amount of friction (matches the current add-character flow's UX weight exactly); adding a second confirmation layer on top would be inconsistent with every other low-stakes edit in this app (recap edits, quest metadata edits) that ship with zero confirm dialogs.

### Feature Dependencies

```
Change character (desktop Details)
    └──requires──> Existing UpdateSignupCharacter action/modal (already shipped)
    └──requires──> Inactive-character-inclusion fix in the option list (new, small)

Change character (mobile Details)
    └──requires──> Change character (desktop) behavioral decisions locked first
                       (same controller action, same validation rules — mobile is a view-layer port,
                        not an independent design, per this project's established Phase 66-71 "mechanically
                        repeat the pattern" precedent for desktop→mobile rollouts)

Clear character back to "no character"
    └──requires──> nothing new — characterId is already nullable end-to-end; purely a UI gap
```

### Dependency Notes

- **Mobile requires desktop's behavioral decisions to be locked first:** this project's own delivery pattern (Phases 66-71, Markdown rollout) is "design once against the shared controller/service, then mechanically port the view to mobile" — desktop and mobile should not diverge on *whether* finalized/waitlisted/role-restricted swaps are allowed, only on markup. Planning this as two separate phases risks two different behavior sets; planning it as one phase with a desktop+mobile view pairing per task (the lesson explicitly called out from Phase 43/54's own retro) avoids that.
- **Clear-to-none has no dependency on the inactive-character fix** — it's already fully supported server-side; only the view needs to expose a "-- Sign up without character --" option in the edit modal, identical wording to the existing add-modal's empty option.

---

## Feature 2: Resolving stale security alerts

Pure maintenance — not a user-facing feature. Kept short per scope.

**What "resolved" means to the operator:** the alert count on the repository's GitHub Security tab (Dependabot/code-scanning alerts currently at 5 open HIGH) reaches zero for those 5, whether by upgrading the flagged dependency/fixing the flagged code pattern, or by an explicit, justified dismissal (false positive / not applicable to this app's actual usage) recorded in GitHub's own dismissal-reason UI. There is no separate in-repo "done" artifact needed beyond that — GitHub's Security tab *is* the source of truth here, consistent with how this project already treats dependency scanning today (PROJECT.md's Phase 34 already records "clean dependency vulnerability scan captured as evidence" as its closure bar).

**User-visible behavior change:** none expected. These are almost always dependency-version bumps or internal code hardening; assume zero UI/API-contract change unless a specific alert's fix turns out to require one (e.g., a breaking major-version bump), in which case that becomes its own regression-testing concern at fix time, not a planning-time feature.

### Table Stakes (maintenance framing)

| Item | Why Expected | Complexity | Notes |
|------|--------------|------------|-------|
| All 5 HIGH alerts closed (fixed or justified-dismissed) | Standing security hygiene; HIGH severity is the bar this project already treats as blocking (see Phase 34/34.1's dependency-scan and security-hardening precedent). | LOW–MEDIUM per alert, unknown until each is inspected (patch-version bump vs. requiring a code change) | Each alert should be triaged individually — no blanket assumption that all 5 are the same shape or same fix cost. |
| Full regression suite green after each dependency bump | This project's own standing bar for any dependency change (see every `dotnet build`/`dotnet test` gate referenced across all prior phases). | LOW | Existing test suite (609+ tests as of v7.0 close) already covers this; no new tests needed unless an alert's fix touches behavior. |

No differentiators or anti-features apply to this item — it's pure hygiene with no design-tradeoff surface.

---

## MVP Definition

### Launch With (v9.0, this milestone)

- [ ] Change character on an existing signup — desktop Details (edit-in-place, replacing the current read-only render) — the core reported gap
- [ ] Change character on an existing signup — mobile Details (net-new capability, ported from the same decisions as desktop) — required by this project's mobile-parity precedent
- [ ] Clear character back to "no character" — desktop + mobile — explicit scope item, near-zero cost given `characterId` is already nullable
- [ ] Inactive-character-in-dropdown fix — desktop + mobile — correctness requirement, not optional, once "change" becomes possible (currently masked because there's no edit UI to expose the bug)
- [ ] Allow the change for finalized quests, waitlisted signups, and all three signup roles — no restriction beyond current unrestricted server-side behavior
- [ ] 5 open HIGH GitHub Security alerts triaged and closed (fixed or justified-dismissed)

### Add After Validation (v9.x)

- [ ] None identified — this is a small, complete, closed-scope item. If the operator later wants a "recently changed" DM-facing indicator (flagged above as a lighter alternative to email notification), that would be the natural next increment.

### Future Consideration (v2+ / explicitly out of scope per this milestone)

- [ ] DM-side character editing on the Manage page — explicitly out of scope per the brief
- [ ] Any form of audit/change-history logging for signup fields — no precedent, disproportionate for this feature alone
- [ ] Email notification on character swap — rate-limit and precedent argue against it; revisit only if the operator explicitly wants it despite the tradeoffs above

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|----------------------|----------|
| Change character — desktop Details | HIGH (closes the explicitly reported gap) | LOW | P1 |
| Change character — mobile Details | HIGH (mobile currently has zero affordance) | LOW–MEDIUM | P1 |
| Clear to "no character" | MEDIUM | LOW | P1 |
| Inactive-character dropdown fix | MEDIUM (silent-data-loss prevention) | LOW | P1 — bundle with the above, not deferrable once edit exists |
| Allow post-finalization / waitlisted / all-roles | MEDIUM (removes artificial restriction) | LOW (no new guard code — this is "don't add restrictions that don't already exist") | P1 |
| DM notification on swap | LOW (see anti-feature rationale) | LOW–MEDIUM (new job + template + rate-limit budget check) | P3 / operator decision |
| Audit trail | LOW | MEDIUM–HIGH (new schema, cross-cutting) | Not planned |
| 5 HIGH security alerts | HIGH (standing hygiene bar) | Unknown per-alert, likely LOW–MEDIUM each | P1 |

## Sources

- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `UpdateSignup` (line 496), `UpdateSignupCharacter` (line 523), `JoinFinalizedQuest` character-selection forms (`Details.cshtml:344-386`) — current guard/validation behavior read directly from source
- `QuestBoard.Domain/Services/PlayerSignupService.cs` — `UpdateSignupCharacterAsync` (line 36) — confirms nullable `characterId` already clears the field server-side
- `QuestBoard.Domain/Enums/CharacterStatus.cs`, `SignupRole.cs` — confirms `Dead`/`Retired`/`Active` and `Player`/`Spectator`/`AssistantDM` value sets
- `QuestBoard.Service/Views/Quest/Details.cshtml` — confirms the "+" add-character button already renders for both selected and waitlisted rows (lines ~135-141, ~251-257), and the current modal/form pattern (line 819+)
- `.planning/PROJECT.md` — Phase 61 ("Edit finalized quest details," D-01–D-04) as direct precedent for post-finalization editing with no email/no time cutoff; Phase 63 ("Recap edit permission broadened," D-01–D-04) as precedent for no-audit/no-notification permission changes; Phase 44 (waitlist auto-promotion) and Phase 54 (mobile join-finalized-quest) as precedent for role/waitlist behavior; Phase 43/54 mobile-parity lesson (pair desktop+mobile view edits into single tasks); Email constraint (100/day, 3000/month, "batch-first design")

---
*Feature research for: D&D Quest Board — v9.0 Rolling Improvements*
*Researched: 2026-08-25*
