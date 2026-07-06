---
phase: 52-add-dead-status-to-characterstatus-enum
plan: 01
audited: 2026-07-06
asvs_level: 1
block_on: high
threats_total: 3
threats_closed: 3
threats_open: 0
---

# Phase 52 Plan 01: Security Audit

## Threat Register Verification

### T-52-01 — Tampering — `Status` form field on Create/Edit POST
**Disposition:** accept
**Verification method:** grep for existing model-binding/ownership controls cited in mitigation plan (accept rationale rests on "no auth/ownership logic changes")
**Result:** CLOSED

Evidence:
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:182-186` (Edit POST) — ownership check `if (currentUser == null || existingCharacter.OwnerId != currentUser.Id) return Forbid();` is unchanged and still gates the `Status` field write at line 205 (`existingCharacter.Status = viewModel.Status;`).
- `QuestBoard.Domain/Enums/CharacterStatus.cs:3-8` — `Dead = 2` is a legitimate, explicitly-valued enum member (not a sentinel/reserved value), consistent with the plan's claim that this introduces no new tampering surface beyond what already existed for out-of-range ints.
- No new `[Authorize]`, ownership, or validation logic was added or removed around the `Status` field — accept disposition rationale holds as stated.

### T-52-02 — Elevation of Privilege — Toggle-button hide (D-03)
**Disposition at plan time:** accept
**Disposition at audit time:** RECLASSIFIED to `mitigate` — CLOSED

This disposition was flagged as STALE by the orchestrator. A post-plan code-review finding (`52-REVIEW.md` WR-01) identified that the view-only button hide left `ToggleRetirement` performing an unconditional Active/Retired flip server-side, meaning a crafted POST to a Dead character's id would silently revive it — a real, exploitable gap undermining the feature's "permanently deceased" intent. Commit `c5f8dba` ("fix(guild-members): add server-side guard against reviving Dead characters") added the missing guard.

**Verification method:** grep for the guard clause in `GuildMembersController.ToggleRetirement`, confirm placement (after ownership check, before the Active/Retired flip).

Evidence — `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:276-302`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ToggleRetirement(int id, CancellationToken token = default)
{
    var character = await characterService.GetCharacterWithDetailsAsync(id, token);
    if (character == null)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null || character.OwnerId != currentUser.Id)   // ownership check — line 285
    {
        return Forbid();
    }

    if (character.Status == CharacterStatus.Dead)                     // guard — lines 290-293
    {
        return BadRequest("Dead characters cannot be retired or reactivated.");
    }

    character.Status = character.Status == CharacterStatus.Active     // flip — lines 295-297
        ? CharacterStatus.Retired
        : CharacterStatus.Active;

    await characterService.UpdateAsync(character, token);

    return RedirectToAction(nameof(Details), new { id });
}
```

Placement verified correct: ownership check (line 285-288) precedes the Dead guard (line 290-293), which precedes the Active/Retired flip (line 295-297). Guard covers the exact case described (Dead → BadRequest, no flip occurs). Confirmed against `git show c5f8dba` — the diff adds exactly this guard, 7 insertions / 2 deletions, single-purpose commit. No other `ToggleRetirement`-equivalent entry point exists (`FindReferences`-equivalent grep for `ToggleRetirement` across the controller shows a single action method; the two Details views are the only callers and both already gate the button's visibility as a defense-in-depth UI convenience, now backed by the server-side invariant).

**Reclassification justification:** The server now enforces the same invariant the view merely hides. This is no longer "accepted risk with no security impact" — it is an actively enforced guard. CLOSED as `mitigate`.

### T-52-SC — Tampering — npm/pip/cargo installs
**Disposition:** n/a (not applicable)
**Verification method:** confirm no package manifest changes in phase file list
**Result:** CLOSED

Evidence: Phase 52-01 `files_modified` (PLAN.md frontmatter) lists only `CharacterStatus.cs`, two CSS files, and four Razor views — no `package.json`, `packages.config`, `*.csproj` `<PackageReference>` additions, or lockfile changes. `52-01-SUMMARY.md` confirms 7 files modified, matching. No package installs occurred; disposition holds.

## Unregistered Flags

None. `52-01-SUMMARY.md` contains no `## Threat Flags` section — the executor did not flag any new attack surface during implementation. No reconciliation needed.

## Accepted Risks Log

| Threat ID | Risk | Rationale | Accepted By |
|-----------|------|-----------|-------------|
| T-52-01 | An out-of-range/tampered `Status` int on Create/Edit POST | Pre-existing risk unrelated to this phase; model binding and ownership checks are unchanged; `Dead` is a legitimate selectable value, not a privilege escalation path | Phase 52 plan (PLAN.md threat_model) |

## Summary

All 3 declared threats resolved to CLOSED. T-52-02's plan-time `accept` disposition was stale relative to the shipped code — a post-plan review (WR-01) surfaced a genuine server-side gap, which was subsequently fixed in commit `c5f8dba` and independently verified present, correctly placed, and functionally complete in the current `GuildMembersController.cs`. No open threats. No unregistered attack surface. No implementation files were modified as part of this audit.
