# Phase 34: Codebase Cleanup & Security Hardening - Context

**Gathered:** 2026-07-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Full review of the entire codebase (including code that predates GSD tracking) covering three deliverables:

1. **Dead code removal** — remove all confirmed-unused code, no exceptions for previously-deferred "harmless" leftovers.
2. **Comment cleanup** — strip low-value inline comments referencing GSD requirement IDs or phase numbers everywhere in the codebase, replaced with XML doc comments (`/// <summary>`) on interfaces.
3. **Security & known-issues audit** — manual review plus automated dependency scanning, fixing everything catalogued in `.planning/codebase/CONCERNS.md` except items that overlap with already-deferred requirements.

This is the closing phase of the v5.0 Multi-Tenancy milestone — the user considers v5.0 done once this phase completes.

This phase does NOT include: digest batching for session reminders (EMAIL-04/REMIND-02) or email unsubscribe/preference management — both remain explicitly deferred (see `<deferred>`).

</domain>

<decisions>
## Implementation Decisions

### Fix Scope (Known Issues Audit)

- **D-01:** Fix scope = everything in `CONCERNS.md`'s **Tech Debt**, **Known Bugs**, **Security Considerations**, **Performance Bottlenecks**, **Fragile Areas**, **Scaling Limits**, **Dependencies at Risk**, and **Test Coverage Gaps** sections.
- **D-02:** EXCEPT `CONCERNS.md`'s **Missing Critical Features** section (digest batching for session reminders, email unsubscribe/preferences) — these overlap with EMAIL-04/REMIND-02, already explicitly deferred in `PROJECT.md`/`STATE.md` at the v4.0 milestone close. Phase 34 does NOT re-open that decision.
- **D-03:** The planner may split Phase 34 into sub-phases (e.g., `34a`, `34b`) grouped by concern area (e.g., security/bugs first, then performance/architecture) if the full `CONCERNS.md` scope exceeds one phase's context budget for full-fidelity planning. Use `/gsd-phase --insert` if a split is recommended — user has pre-approved this outcome.

### Dead Code Removal

- **D-04:** Remove ALL confirmed-unused code, including previously-deferred items explicitly called out in `STATE.md` (e.g., `RegisterViewModel` — noted "unused but harmless" and kept out-of-scope in Phase 30-02). Nothing is "left for later" — this is the dedicated cleanup phase closing v5.0.
- **D-05:** "Confirmed unused" means verified via reference search (no callers found) — not just suspected. Verify via RIP/grep before removing.

### Comment Cleanup

- **D-06:** Strip low-value inline comments referencing GSD requirement IDs or phase numbers (e.g. `// EMAIL-04:`, `// Phase 30-04:`, `// D-06`) across the ENTIRE codebase, including code that predates GSD tracking — not just v1.x–v5.0 GSD-era comments.
- **D-07:** Replace with XML doc comments (`/// <summary>`) on **interfaces** (e.g. `IQuestService`, `IUserService`, `IActiveGroupContext`, `IQuestRepository`, etc.). `CONVENTIONS.md` already shows one interface following this pattern (`IQuestRepository.GetQuestsForTomorrowAllGroupsAsync`) — extend the pattern to interfaces that currently lack XML docs.
- **D-08:** Preserve genuinely useful inline comments (business-logic explanations, landmine/gotcha warnings, "why" context) — e.g. the manual-cleanup-order comment in `QuestService.RemoveAsync()`. Only ID/phase-number-referencing comments are targeted for removal.

### Security Audit Method

- **D-09:** Security audit = manual code review against `CONCERNS.md`'s "Security Considerations" section PLUS an automated dependency/vulnerability scan (`dotnet list package --vulnerable`) across all three projects (Service, Domain, Repository).
- **D-10:** Any vulnerable package found by the scan should be upgraded if a non-breaking fix version exists; if a breaking upgrade is required, document it rather than forcing a breaking change mid-cleanup-phase.

### Claude's Discretion

- Exact grouping/ordering of `CONCERNS.md` items into plans/waves (e.g., security-first vs. dependency order).
- Whether XML doc comments are added to ALL public interfaces or only those touched during this phase's other work — planner should aim for full coverage per D-07 but may scope-limit if it would balloon the phase.
- Naming/grouping of any sub-phases if a split is recommended (e.g., "34a: Security & Bugs", "34b: Performance & Architecture").

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope & Known Issues
- `.planning/ROADMAP.md` §Phase 34 — goal statement (dead code, comments, security audit, closes v5.0)
- `.planning/codebase/CONCERNS.md` — the enumerated list of ~20 known issues (Tech Debt, Known Bugs, Security Considerations, Performance Bottlenecks, Fragile Areas, Scaling Limits, Dependencies at Risk, Missing Critical Features, Test Coverage Gaps) this phase's audit scope is built from. Every section EXCEPT "Missing Critical Features" is in scope (see D-01/D-02).
- `.planning/codebase/ARCHITECTURE.md` §Anti-Patterns — documented anti-patterns (AutoMapper circular recursion, missing group context in jobs, repository mutable state without re-query, service-layer repository direct references) — verify these are not reintroduced during cleanup.
- `.planning/codebase/CONVENTIONS.md` §Comments — existing comment conventions and the one interface (`IQuestRepository`) already using the target XML-doc-comment pattern.
- `.planning/STATE.md` §Roadmap Evolution — the "Phase 34 added" entry explaining the phase's origin and full-codebase (not just GSD-tracked) scope.
- `.planning/PROJECT.md` §Requirements / Out of Scope — confirms EMAIL-04/REMIND-02 (digest batching) and per-user email opt-out are already-deferred decisions Phase 34 must not re-open (D-02).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IQuestRepository`'s existing XML doc comment (`GetQuestsForTomorrowAllGroupsAsync`) — the pattern to replicate on other interfaces (D-07).

### Established Patterns
- Three-layer clean architecture (Service → Domain → Repository) — dead code removal and fixes must preserve this dependency direction (`ARCHITECTURE.md`).
- `internal` visibility on Domain/Repository service implementations, `public` interfaces — XML doc comments target the public interface surface.

### Integration Points
- `CONCERNS.md`'s specific file/line references (e.g. `QuestController.cs`, `AdminController.cs`, `ShopSeedService.cs`, `QuestService.cs`, `EmailService.cs`, `SessionReminderJob.cs`, `QuestBoardContext.cs`, `Program.cs`) are the concrete entry points for the security/bug/tech-debt fixes.

### Known Landmines
- Global Query Filter is applied to `QuestEntity`/`ShopItemEntity` only, NOT `UserEntity` — this is intentional (Identity needs global user visibility), not an inconsistency to "fix" (per `ARCHITECTURE.md`).
- `RegisterViewModel` removal (D-04) — verify zero references before deleting; it was deliberately left in place during Phase 30-02, so double-check no view or controller still binds to it.

</code_context>

<specifics>
## Specific Ideas

- User explicitly wants ALL confirmed-unused code removed, including previously-punted items like `RegisterViewModel` — no more "leave for later."
- Comment cleanup explicitly targets ID/phase-number-referencing comments (e.g. `// EMAIL-04:`) — codebase-wide, not just GSD-era.
- Dependency vulnerability scan added as a net-new tooling step (`dotnet list package --vulnerable`) — cheap addition to the manual audit.

</specifics>

<deferred>
## Deferred Ideas

- **Digest batching for session reminders** (EMAIL-04/REMIND-02) — stays deferred per D-02; do not implement in Phase 34.
- **Email unsubscribe/preference management** — stays deferred per D-02; do not implement in Phase 34.
- **"Password changed" notification email** — already deferred from Phase 32; not resurfaced in this discussion.

None else — discussion stayed within phase scope.

</deferred>

---

*Phase: 34-codebase-cleanup-and-security-hardening-remove-unused-code-s*
*Context gathered: 2026-07-01*
