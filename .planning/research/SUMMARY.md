# Project Research Summary

**Project:** D&D Quest Board
**Domain:** Incremental UX gap-fill + security-maintenance housekeeping on a mature ASP.NET Core 10 MVC app
**Researched:** 2026-08-25
**Confidence:** HIGH

## Executive Summary

This milestone is deliberately unlike v1.0–v8.0: it is a rolling bucket for small, ad-hoc work rather than a themed feature set with a fixed end-state. Two items open it, and they are fully independent of each other.

**Item 1 — change the character on an existing quest signup** turned out to be almost entirely a view-layer problem. All four researchers independently verified that the full nullable-`characterId` path already works end to end: `QuestController.UpdateSignupCharacter` → `PlayerSignupService.UpdateSignupCharacterAsync` → `PlayerSignupRepository.UpdateAsync` (an explicit override, not the AutoMapper base) → `PlayerSignupEntity.CharacterId` (`int?`, already nullable in the DB). No controller, Domain, Repository, or migration change is required, and clearing to "no character" is already a supported server-side code path. The gap is that no UI element ever exposes it: `Details.cshtml` renders the character read-only once set, and `Details.Mobile.cshtml` has no character affordance at all.

**Item 2 — the 5 open HIGH GitHub alerts** is not a code task. All five are `System.Security.Cryptography.Xml` 8.0.0–8.0.3 against `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`, a manifest deleted in commit `a477ab9` on 2026-06-29. The package is absent from every tracked `.csproj` and from `dotnet list package --include-transitive`. But the decisive fact is a timing one: **the alerts were created 2026-08-10, six weeks after the manifest was deleted.** GitHub is actively minting new alerts off a stale cached dependency-graph snapshot. That makes this a graph-refresh problem, not a package problem — and it means the reflexive "the file is gone, dismiss it" reasoning is *not* sufficient evidence on its own.

**The main risk in this milestone is not difficulty — it is two silent-failure modes.** On Item 1, a naive implementation silently deletes a player's character selection (see Critical Pitfall 1 below). On Item 2, a rubber-stamp dismissal could permanently suppress a real vulnerability. Both are cheap to prevent and expensive to discover later.

## Key Findings

### Recommended Stack

**No new dependencies for either item.** Verified against source, not inferred: Bootstrap 5.3.0's full bundle (with Popper) is already loaded on both the desktop and mobile layouts, and the existing `#addCharacterModal` plus its plain form POST is directly reusable. Item 1 is markup-only.

**Core technologies (all existing, all confirmed sufficient):**
- Bootstrap 5.3.0 modal + `show.bs.modal` / `event.relatedTarget` — already an established idiom in this codebase (`Shop/Index.cshtml:455`, `ShopManagement/Index.cshtml:505`, and both `.Mobile` counterparts)
- ASP.NET Core default model binding — converts empty string to `null` for an `int?` parameter with no coercion risk anywhere in the chain
- `Html.PartialAsync` — the codebase's own established desktop/mobile sharing mechanism (`_Calendar.cshtml` is called from both `Details.cshtml` and `Details.Mobile.cshtml`)

**Tooling for Item 2:**
- `PATCH /repos/{owner}/{repo}/dependabot/alerts/{alert_number}` with `state=dismissed`, `dismissed_reason=not_used`. Valid `dismissed_reason` enum: `fix_started`, `inaccurate`, `no_bandwidth`, `not_used`, `tolerable_risk`. **No bulk endpoint exists** — each alert is dismissed individually, which happens to align with the audit-trail requirement below.
- `gh` has no native subcommand for this; use `gh api` passthrough.

**Explicitly do NOT add:** a `.github/dependabot.yml` (none exists; it governs version-update PRs, not alerts, and would not affect staleness), any new JS library, any new NuGet package, any migration.

### Expected Features

**Must have (table stakes):**
- Change character on an existing signup — desktop `Details` (the reported gap)
- Change character on an existing signup — mobile `Details` (currently has *neither* add nor change; a bigger gap than desktop's)
- Clear back to "no character" — blocked only by the modal `<select>`'s client-side `required` attribute
- **Keep a currently-assigned-but-inactive character visible and pre-selected in the dropdown** — a correctness requirement, not a nice-to-have (see Critical Pitfalls)
- All 5 HIGH alerts closed with individually recorded, evidence-referencing reasons

**Deliberate non-restrictions (do not add guards that do not already exist):**
- Post-finalization changes stay allowed — precedent is Phase 61 (edit finalized quest metadata: no email, no time cutoff). Character choice matters *most* right up to game night, unlike date votes, which are meaningless once locked.
- Waitlisted signups stay editable — the "+" add button already renders for waitlist rows today
- All three roles (Player, Spectator, AssistantDM) stay eligible — `JoinFinalizedQuest` already lets all three pick a character at signup

**Anti-features (researched and argued against, not merely skipped):**
- DM email on swap — rate limit is 100/day, and no existing edit feature (Phase 61, Phase 63) fires email; a character swap does not change attendance
- Audit trail on the swap — zero precedent anywhere in this app; one bespoke audited field would be inconsistent
- A confirm dialog — trivially reversible; the modal-then-Save flow is already the right friction

**Defer:**
- DM-side editing on the Manage page — explicitly out of scope for this milestone (would need a new authorized action; `UpdateSignupCharacter` only ever edits the caller's own signup)

### Architecture Approach

Item 1 is **Service-layer only** — views plus one new partial. Every hop from controller to DB column was read directly and confirmed to handle `null` cleanly with no coercion. Item 2 touches no application architecture at all.

**Major components:**
1. `Views/Quest/_CharacterSelectModal.cshtml` (NEW) — `@model PlayerSignup` partial holding the modal markup and a self-contained inline `<script>`. Placed in `Views/Quest/` not `Views/Shared/`, matching the codebase's own "single-feature partials live with their feature" convention (`_QuestCard.cshtml`, `_QuestSection.cshtml`). The script goes *inside* the partial to sidestep a real inconsistency: `Details.cshtml` puts scripts in the page body while `Details.Mobile.cshtml` uses `@section Scripts`.
2. `Views/Quest/Details.cshtml` (MODIFIED) — add a change trigger to the `Character != null` branches in **both** the finalized-participants table (~L116) and the waitlist table (~L232), gated on the existing `isCurrentUser` check; replace the inline modal with the partial call.
3. `Views/Quest/Details.Mobile.cshtml` (MODIFIED) — add the trigger to both participant-row blocks (~L215, ~L243), which today are bare `<small>` text, plus one partial call.

`ViewBag.UserCharacters` is populated in a single shared `Details` GET action, so it is identically available on both render paths — there is no separate mobile action.

### Critical Pitfalls

1. **The inactive-character silent-wipe — the highest-value finding in this research, and it emerged from a conflict between the research documents.** ARCHITECTURE.md judged the `Active`-only dropdown filter harmless because "the dropdown never offers a character the POST would reject" — true for the *add* case it was reasoning about. FEATURES.md and PITFALLS.md independently flagged it as a live bug for the *change* case. Combining them: ARCHITECTURE.md's own proposed `select.value = characterId` line has no matching `<option>` when the signup holds a Retired or Dead character, so the value falls back to `""`, and a user who opens the modal and saves **silently clears their character**. `CharacterStatus.Dead` shipped in Phase 52; a killed-off PC mid-campaign is routine. *Avoid:* build the option list to always include the signup's current character even when inactive, labeled (e.g. "Thorin (Retired)") and pre-selected. Test by retiring a signed-up character and reloading Details.
2. **Third near-duplicate view block.** `Details.cshtml` already contains two structurally identical character cells; mobile makes a third. This is the exact drift class PROJECT.md already blames for the `Characters/Edit.cshtml` `classIndex` bug, `Characters/Create.cshtml`'s dead `isEdit` branch, the triple `BoardType` lookup, and `.quest-description-mobile`. *Avoid:* extract the shared partial rather than hand-copying a fourth instance.
3. **The `required` attribute silently defeats the clear requirement.** Reusing the existing modal verbatim blocks the explicit "clear to no character" scope item at the browser level. *Avoid:* drop `required`, add a blank first option, and regression-test that `CharacterId` actually round-trips to null.
4. **Reflexive alert dismissal.** A clean `dotnet list package` is necessary but not sufficient — it is the developer-facing view, while alerts are driven by GitHub's separately-cached dependency graph. The 2026-08-10 creation date proves that graph is stale *and still active*. *Avoid:* force a "Refresh Dependabot alerts" (rate-limited to once per hour, so trigger it early in the phase, not last), confirm branch scope, and only then dismiss — individually, each with a reason citing the actual evidence.
5. **Cross-tenant re-verification.** `UpdateSignupCharacter` looks safe (`HasQueryFilter` on `CharacterEntity`/`QuestEntity` is fail-closed post-Phase 55) but has no dedicated cross-group regression test, and this project shipped two real cross-tenant leaks in v7.0 on these exact entities. *Avoid:* add the test rather than assume.

## Implications for Roadmap

### Phase 72: Change Character on an Existing Signup
**Rationale:** The item that drove the milestone, and the one with actual user-visible value. Kept as a **single phase covering desktop and mobile together** — splitting them risks the two platforms diverging on behavior, which is the explicit lesson recorded from Phases 43/54 and the pattern followed through the Phase 66–71 Markdown rollout.
**Delivers:** Change and clear a signup's character from both desktop and mobile Details, with inactive characters correctly represented.
**Addresses:** All Item 1 table stakes above, including the inactive-character correctness fix.
**Avoids:** Pitfalls 1, 2, 3, 5.
**Internal order:** new partial first (both hosts depend on it existing) → desktop wiring → mobile wiring.

### Phase 73: Resolve Stale HIGH Security Alerts
**Rationale:** Fully independent of Phase 72 — no technical ordering constraint exists between them. Framed as investigate-then-resolve per the operator's explicit choice, not a straight dismissal.
**Delivers:** Zero open HIGH alerts, each closed with an individually recorded, evidence-referencing reason, plus the investigation logged in PROJECT.md.
**Avoids:** Pitfall 4, and the audit-trail gap.
**Note:** the research recommends running this *first* as a zero-risk quick win, and the rate-limited graph refresh does benefit from an early start. Ordering is an operator preference call, not a dependency.

### Phase Ordering Rationale

- The two phases share no code, no files, and no data. Either order works.
- Phase 72 is sequenced first because it is the operator's driving request and carries the user-visible value; Phase 73 is maintenance.
- Within Phase 72, the partial must exist before either host view can call it.

### Research Flags

Phases likely needing deeper research during planning: **none.** Both items were researched to implementation-ready depth, with exact file paths and line numbers verified against source.

Phases with standard patterns (skip research-phase):
- **Phase 72** — reuses three established codebase idioms (`Html.PartialAsync` desktop/mobile sharing, `show.bs.modal` + `event.relatedTarget`, the existing modal-and-form-POST flow)
- **Phase 73** — the exact GitHub API surface is documented in STACK.md

**Requires a discuss-phase decision:** whether to leave `UpdateSignupCharacter` without a finalized-quest guard (research recommends yes, leave it — and document that as deliberate) or add one to match its sibling `UpdateSignup`. Today the asymmetry is undocumented and looks accidental. Either answer is defensible; leaving it undecided is not.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified against source files and live `gh api` calls, not inferred |
| Features | HIGH | Grounded in this project's own Phase 44/52/54/61/63 precedents rather than generic patterns |
| Architecture | HIGH | Every hop from controller to DB column opened and read at cited line numbers |
| Pitfalls | HIGH | Each pitfall tied to a concrete file, line, or documented prior incident in this repo |

**Overall confidence:** HIGH

### Gaps to Address

- **The finalized-quest guard decision** — flagged above; belongs in Phase 72's discuss-phase, not left to the executor.
- **DM notification on swap** — researched recommendation is no, and this summary treats it as settled unless the operator says otherwise.
- **PITFALLS.md's open question about non-default-branch or fork scanning is now closed:** the repo's default branch is `main` (verified via `gh repo view`), the manifest is absent there, and Dependabot alerts only scan the default branch (verified against GitHub docs in STACK.md). Phase 73 still owns forcing the graph refresh, which is the decisive test.
- **PITFALLS.md's suggested staleness heuristic needs correcting before use:** it proposed that a detection date at or before commit `a477ab9` would be consistent with staleness. The actual dates run the other way — detected 2026-08-10, manifest deleted 2026-06-29. That does not weaken the staleness conclusion; it strengthens it, and it makes the graph refresh the decisive evidence rather than the timestamps.
- **Leftover `EuphoriaInn.*` directories:** PITFALLS.md rates these a live re-commit risk. Verified: they contain only `bin/` and `obj/`, zero `.csproj`, zero `.cs` outside build output, and both are matched by `.gitignore:25`/`:26`. The re-commit risk is therefore theoretical, and deleting them is optional cleanup rather than a phase deliverable — though it is free and closes the question.

## Sources

### Primary (HIGH confidence)
- Direct source reads across `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Domain/Services/PlayerSignupService.cs`, `QuestBoard.Repository/PlayerSignupRepository.cs`, `QuestBoard.Repository/Entities/PlayerSignupEntity.cs`, `Views/Quest/Details.cshtml`, `Views/Quest/Details.Mobile.cshtml`
- Live `gh api repos/theunschut/quest-board/dependabot/alerts` — alert numbers, severities, manifest paths, creation timestamps
- `git show --stat a477ab9`, `git ls-files`, `git check-ignore -v`, `dotnet list package --include-transitive`
- [Troubleshooting the dependency graph — GitHub Docs](https://docs.github.com/en/code-security/supply-chain-security/understanding-your-software-supply-chain/troubleshooting-the-dependency-graph)
- [Viewing and updating Dependabot alerts — GitHub Docs](https://docs.github.com/code-security/dependabot/dependabot-alerts/viewing-and-updating-dependabot-alerts)
- `.planning/PROJECT.md` — Known issues / tech debt, Constraints, Key Decisions

### Secondary (MEDIUM confidence)
- Community `dependabot-core` issue reports corroborating the deleted-manifest auto-close gap — the behavior is not documented officially, only the manual-refresh remedy is

---
*Research completed: 2026-08-25*
*Ready for roadmap: yes*
