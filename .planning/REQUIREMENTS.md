# Requirements: D&D Quest Board — v9.0 Rolling Improvements

**Defined:** 2026-08-25
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

> **Rolling milestone.** Unlike v1.0–v8.0, v9.0 has no fixed end-state. The requirements below are the *opening* scope. Additional requirements will be appended as ad-hoc work arrives, and the milestone closes when the operator decides to cut it. New categories get new REQ-ID prefixes; existing ones continue their numbering.

## v1 Requirements

Requirements for the v9.0 milestone. Each maps to a roadmap phase.

### Signup Character Selection

- [ ] **SIGNCHAR-01**: A player viewing a quest they are signed up for can change the character on their signup, even when a character is already selected — on the desktop quest Details page, in both the finalized-participants table and the waitlist table
- [ ] **SIGNCHAR-02**: A player can change the character on their signup from the mobile quest Details page, which today offers no way to set or change a character at all
- [ ] **SIGNCHAR-03**: A player can clear their signup back to "no character" from the change UI, on both desktop and mobile
- [ ] **SIGNCHAR-04**: When a player's signup holds a character that is no longer Active (Retired or Dead), the change UI shows that character as the current selection, clearly labelled with its status, so opening and saving the form cannot silently wipe the selection
- [ ] **SIGNCHAR-05**: Changing the character remains possible after a quest is finalized, with no time cutoff — a player can still swap right up to and during game night
- [ ] **SIGNCHAR-06**: Changing the character remains possible for waitlisted signups and for all three signup roles (Player, Spectator, AssistantDM), matching what signup-time character selection already allows
- [ ] **SIGNCHAR-07**: A player cannot set their signup to a character owned by another user or belonging to another group, and this is proven by an automated cross-group regression test rather than assumed from the query filters

### Security Alert Resolution

- [ ] **SECALERT-01**: The five open HIGH GitHub security alerts (#17–#21, `System.Security.Cryptography.Xml`) are investigated individually — branch scope, manifest attribution, and dependency-graph freshness confirmed per alert — before any of them is closed
- [ ] **SECALERT-02**: GitHub's dependency graph is force-refreshed and the alerts re-checked, so the staleness conclusion rests on GitHub's own re-scan rather than on local `dotnet list package` output alone
- [ ] **SECALERT-03**: Each of the five alerts is closed individually with a dismissal reason that cites the actual evidence gathered — never a bulk action with a generic reason
- [ ] **SECALERT-04**: The investigation and its outcome are recorded in `.planning/PROJECT.md`, so a future reviewer can distinguish a genuine triage from a rubber stamp without relying on GitHub's UI history
- [ ] **SECALERT-05**: The GitHub Security tab shows zero open HIGH alerts for this repository once the phase closes

## Future Requirements

Deferred — revisit if the need becomes real.

- [ ] **SIGNCHAR-08**: A "recently changed" indicator on the DM's Manage page, showing that a player swapped character since finalization — surfaced during research as the cheap alternative to an email notification; the operator chose no notification for v9.0, and this edges toward the audit-trail exclusion below
- [ ] Digest batching for session reminders — single combined email when a player has multiple same-day quests (EMAIL-04 / REMIND-02, deferred since v4.0; same-day quests have never occurred in over a year)
- [ ] Markdown toolbar extras — strikethrough, horizontal rule, cheatsheet link (EDITOR-07/08/09, deferred at v8.0 close; add only if users ask)

## Out of Scope

Explicit exclusions for v9.0, with reasoning.

- **DM-side character editing on the Quest Manage page** — the operator scoped this out. `UpdateSignupCharacter` only ever edits the caller's own signup, so this would need a new authorized action plus its own cross-tenant test surface. A separate feature, not a corner of this one.
- **Email notification to the DM on character swap** — operator decision, backed by research. The relay budget is 100/day and no existing edit feature (Phase 61 finalized-quest edit, Phase 63 recap edit) fires email. A character swap does not change attendance, and the DM's read surface on Details/Manage is already live and authoritative.
- **Audit trail / change history on signup fields** — no field-level audit logging exists anywhere in this app. Adding one bespoke audited field would be inconsistent with every other mutable field, and disproportionate for a 17-person trusted group. If this ever becomes a real ask it should be scoped as its own cross-cutting concern, applied uniformly.
- **A confirmation dialog before applying a swap** — the change is trivially reversible, affects no other player, and fires no email. The modal-open-then-Save flow is already the same friction as the existing add-character flow, which has never had a confirm step.
- **Restricting the change to Players only** — would be a net-new inconsistency. `JoinFinalizedQuest` already lets Spectators and AssistantDMs pick a character at signup, and the existing action has no role check.
- **Auto-clearing an inactive character server-side** — surprising and undiscoverable. SIGNCHAR-04 keeps the decision with the player instead.
- **Adding a `.github/dependabot.yml`** — none exists today. It governs Dependabot *version-update PRs*, not *alerts*, so it would have no effect on alert staleness.
- **Bumping or adding any NuGet package for the security alerts** — `System.Security.Cryptography.Xml` is absent from every tracked `.csproj` and from the transitive graph. There is nothing to bump.

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SIGNCHAR-01 | — | Pending roadmap |
| SIGNCHAR-02 | — | Pending roadmap |
| SIGNCHAR-03 | — | Pending roadmap |
| SIGNCHAR-04 | — | Pending roadmap |
| SIGNCHAR-05 | — | Pending roadmap |
| SIGNCHAR-06 | — | Pending roadmap |
| SIGNCHAR-07 | — | Pending roadmap |
| SECALERT-01 | — | Pending roadmap |
| SECALERT-02 | — | Pending roadmap |
| SECALERT-03 | — | Pending roadmap |
| SECALERT-04 | — | Pending roadmap |
| SECALERT-05 | — | Pending roadmap |

**Coverage:**

- v1 requirements: 12 total
- Mapped to phases: 0/12 (roadmap pending)
- Unmapped: 12

---
*Requirements defined: 2026-08-25*
