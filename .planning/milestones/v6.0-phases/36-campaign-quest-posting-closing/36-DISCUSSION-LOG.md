# Phase 36: Campaign Quest Posting & Closing - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-03
**Phase:** 36-campaign-quest-posting-closing
**Areas discussed:** Close/Reopen control, Campaign quest board display, Quest Log integration, Campaign create-form fields

---

## Close/Reopen control

| Option | Description | Selected |
|--------|-------------|----------|
| Stay on Manage | Manage re-renders with a Reopen button in place of Close, consistent with how Open behaves | |
| Redirect to Details | DM sent to read-only Details after closing, consistent with how Finalize behaves | |
| Let Claude decide | — | ✓ |

**User's choice:** Let Claude decide.
**Notes:** Claude leans toward "stay on Manage" — Close/Reopen is a reversible toggle like Open, not a one-time commit like Finalize.

| Option | Description | Selected |
|--------|-------------|----------|
| Single-click, no confirmation | Matches existing Finalize/Open pattern — no confirm dialogs exist anywhere in the app today | |
| Confirmation modal first | Guards against a misclick since closing hides the quest from the active board immediately | |
| Let Claude decide | — | ✓ |

**User's choice:** Let Claude decide.
**Notes:** Claude leans toward "single-click, no confirmation" to match existing precedent exactly.

**Additional context surfaced during discussion (not asked as a question):** Location (Manage.cshtml only) and authorization (quest owner or group Admin) were found to already be established, unambiguous conventions from the existing Finalize/Open actions — not treated as open questions.

---

## Campaign quest board display

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse wax seal, relabel Closed/Open | Same visual mechanic, swap wording | |
| Different visual entirely | Plain status badge/pill instead of wax seal | |
| Let Claude decide | — | |

**User's choice:** Free-text — user explained the actual mechanic (bottom-left seal = finalized/picked-date state, hidden when open via `.open-seal{display:none}`, brightened via `.finalized-seal` when finalized; top-right seal = current viewer's own signup, hue-rotated blue, independent of finalized state) and said this needs research rather than a decision now, since neither concept maps cleanly to campaign quests.
**Notes:** Flagged as D-06 "needs research" in CONTEXT.md — not locked.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, keep CR badge | CR still conveys difficulty/danger even without signups | |
| Drop CR for campaign quests | CR was a signup-decision aid; no signup decision exists in campaign mode | ✓ |
| Let Claude decide | — | |

**User's choice:** Drop CR for campaign quests.

| Option | Description | Selected |
|--------|-------------|----------|
| Nothing — just DM name | Simplest; no signup-shaped line at all | |
| Posted date instead | Show CreatedAt in that slot | |
| Let Claude decide | — | |

**User's choice:** Free-text — remove the signup-count line entirely and let the description div expand into the freed space.

---

## Quest Log integration

| Option | Description | Selected |
|--------|-------------|----------|
| Mixed, one chronological grid | Both quest types share one grid ordered by completion/close date | |
| Separate sections | Campaign closed quests get their own section/heading | |
| Let Claude decide | — | |

**User's choice:** Question withdrawn as moot mid-discussion. `BoardType` is per-group and immutable (Phase 35) — a single group's Quest Log can only ever contain one kind of entry, never a mix. User's initial confusion ("Completed quest log should be group bound?") correctly flagged that the question's premise didn't hold.

| Option | Description | Selected |
|--------|-------------|----------|
| Same simplification as board card | Drop CR badge and Adventurers count, consistent with board card decision | ✓ |
| Handle differently here | Something else specific to Quest Log card | |
| Let Claude decide | — | |

**User's choice:** Same simplification as board card.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, same as one-shot | DM can add/edit recap notes on closed campaign quests | ✓ |
| No recap for campaign quests | Recap stays one-shot-only | |

**User's choice:** Yes, same as one-shot.

---

## Campaign create-form fields

| Option | Description | Selected |
|--------|-------------|----------|
| Drop CR field from campaign form | No CR input at all for campaign quests; value defaults under the hood | ✓ |
| Keep CR field, just don't display it later | DM still picks CR on create for reference | |
| Let Claude decide | — | |

**User's choice:** Drop CR field from campaign form.

| Option | Description | Selected |
|--------|-------------|----------|
| Drop TotalPlayerCount from campaign form | No signup cap needed | ✓ |
| Keep it for campaign quests | Still useful as a reference number | |
| Let Claude decide | — | |

**User's choice:** Drop TotalPlayerCount from campaign form.

| Option | Description | Selected |
|--------|-------------|----------|
| Drop it for campaign quests | Doesn't map to campaign mode | ✓ |
| Keep it — same meaning applies | DM might still want a hidden-from-log post | |
| Let Claude decide | — | |

**User's choice:** Drop it for campaign quests.

---

## Claude's Discretion

- Post-close redirect target (lean: stay on Manage)
- Confirmation-step presence for Close/Reopen (lean: single-click, no confirmation)
- Exact mechanism for relaxing `ProposedDates` validation on `QuestViewModel` for campaign quests
- Exact entity/schema design for Close/Reopen state (reuse `IsFinalized`/`FinalizedDate` vs new fields) — service-method separation is locked (PROJECT.md), but the underlying data shape is not

## Deferred Ideas

None — discussion stayed within phase scope.
