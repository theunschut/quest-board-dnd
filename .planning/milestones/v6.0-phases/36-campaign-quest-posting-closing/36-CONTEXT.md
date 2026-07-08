# Phase 36: Campaign Quest Posting & Closing - Context

**Gathered:** 2026-07-03
**Status:** Ready for planning

<domain>
## Phase Boundary

A DM running a campaign-type group can post a quest with no date picker and no per-quest signup (the party is just the group's fixed roster), and can close/reopen that quest as a simple status toggle. No quest-related email (posted/reminder/finalized) is ever sent for a campaign-group quest. This reuses the existing `QuestController`/`QuestService`/views, gated by `BoardType` — no new controller or Area. Navigation visibility and Email Stats access control are out of scope here (Phase 37).

</domain>

<decisions>
## Implementation Decisions

### Close/Reopen control
- **D-01:** Location and authorization follow the existing `Finalize`/`Open` precedent exactly — DungeonMasterOnly-gated actions on `Manage.cshtml`, restricted to the quest's owning DM or a group Admin (`IsQuestOwner(currentUser, quest.DungeonMaster) || role == GroupRole.Admin`, mirroring `QuestController.Finalize`/`Open`). This was not treated as an open question — the user confirmed there's no reason to deviate from precedent here.
- **D-02 (Claude's discretion):** After closing, the page should stay on `Manage` (re-rendering with a Reopen button in place of Close) rather than redirecting to `Details` — reasoning: Close/Reopen is a reversible toggle like the existing `Open` action (which stays on Manage), not a one-time commit like `Finalize` (which redirects to Details).
- **D-03 (Claude's discretion):** Close/Reopen should be a single-click POST with no confirmation dialog — matches the existing Finalize/Open pattern exactly; no confirmation dialogs exist anywhere else in the app today.

### Campaign quest board display
- **D-04:** Drop the Challenge Rating badge entirely for campaign quests — on the quest board card, Details, and Manage pages. CR was a signup-decision aid ("should I sign up for this difficulty?"); campaign quests have no signup decision.
- **D-05:** Remove the signup-count line entirely from the campaign quest board card (replaces "Adventurers signed up: N" / "Selected Adventurers: N", both driven by `PlayerSignups` which campaign quests won't have). Let the `.quest-description` div expand to fill the freed vertical space rather than substituting different content into that slot.
- **D-06 (needs research, not decided):** The wax-seal treatment on the board card is genuinely unresolved — see `<specifics>` below for the mechanic and the open question. Flagged for the researcher, not locked here.

### Quest Log integration
- **D-07:** Apply the same simplification to the Quest Log card as the board card — drop the CR badge and the "Adventurers: N" (`selectedPlayers.Count`) line for campaign-closed quest entries.
- **D-08:** The Session Recap field (DM's free-text post-session notes, `Quest.Recap`) applies to campaign quests exactly as it does to one-shot — DM can add/edit it on a closed campaign quest via the same `UpdateRecap` action/UI.
- **Note:** Mixed-vs-separate Quest Log sections was raised as a question and withdrawn as moot — `BoardType` is set per-group and immutable (Phase 35), so a single group's Quest Log can only ever contain one kind of entry (all one-shot-finalized, or all campaign-closed). There is no scenario where the two interleave within one group's view.

### Campaign create-form fields
- **D-09:** Drop the Challenge Rating field from the campaign Create form entirely (not just hidden from display) — value defaults under the hood, not DM-selectable for campaign quests.
- **D-10:** Drop `TotalPlayerCount` from the campaign Create form entirely — no signup cap needed since there's no per-quest signup to cap.
- **D-11:** Drop the `DungeonMasterSession` checkbox/concept from campaign quests entirely — it doesn't map to campaign mode (campaign quests are all real party quests; the "DM-only planning session excluded from Quest Log" concept was one-shot-specific).

### Claude's Discretion
- Post-close redirect target (D-02) and confirmation-step presence (D-03) — see above, both explicitly deferred to Claude by the user.
- Exact mechanism for how `ProposedDates`' required-field validation on `QuestViewModel` relaxes for campaign quests (conditional validation, separate ViewModel, etc.) — not discussed; this is implementation-level and left to planner/researcher.
- Exact mechanism for how Close/Reopen state is modeled at the entity level (reusing `IsFinalized`/`FinalizedDate`, or new `IsClosed`/`ClosedDate` fields) — not discussed; PROJECT.md already locks that `CloseQuestAsync`/`ReopenQuestAsync` are additive and separate from `FinalizeQuestAsync`/`OpenQuestAsync` at the service-method level, but the underlying entity/schema shape is an implementation decision for research/planning, informed by the `<code_context>` notes below (e.g. `GetCompletedQuestsAsync`'s next-day filter and `DailyReminderJob`'s tomorrow-date query both currently key off `FinalizedDate`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/ROADMAP.md` — Phase 36 section (goal, success criteria, CQUEST-01..06)
- `.planning/REQUIREMENTS.md` — CQUEST-01 through CQUEST-06 definitions and Out of Scope table (no board-type changing, no per-quest party tagging, no rewards/gold flow tied to closing, no campaign emails, no separate controller/Area)
- `.planning/PROJECT.md` — Key Decisions table: `CloseQuestAsync`/`ReopenQuestAsync` kept additive and separate from `FinalizeQuestAsync`/`OpenQuestAsync`; `BoardType` dispatch uses C# switch expressions (matching `ShopService.CalculateItemPriceAsync`'s `ItemRarity` convention); reuse existing `QuestController`/`QuestService`/Areas, no new controller/Area

### Prior phase context
- `.planning/phases/35-board-type-configuration/35-CONTEXT.md` — `BoardType` enum (`OneShot`/`Campaign`) foundation: how it's stored on `GroupEntity`, how it's read, and that it's immutable after group creation. This phase reads `BoardType` but never writes it.

No external specs/ADRs beyond the project's own planning docs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestController` (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`) — `Finalize` (line ~609) and `Open` (line ~637) are the direct precedent for `Close`/`Reopen`: both `[HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "DungeonMasterOnly")]`, both check `IsQuestOwner(currentUser, quest.DungeonMaster) || role == GroupRole.Admin` and `Forbid()` otherwise, both call a `QuestService` method then redirect (`Finalize` → `Details`, `Open` → `Manage`).
- `QuestService.FinalizeQuestAsync`/`OpenQuestAsync` (`QuestBoard.Domain/Services/QuestService.cs:17,91`) and their `QuestRepository` counterparts (`QuestBoard.Repository/QuestRepository.cs:114,141`) — the pattern to mirror for `CloseQuestAsync`/`ReopenQuestAsync`: load entity, flip the relevant flag(s)/date, `SaveChangesAsync`.
- `QuestViewModel` (`QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs`) — `ProposedDates` is `[Required, MinLength(1)]`; `ChallengeRating`, `TotalPlayerCount`, `DungeonMasterSession` are all currently bound here too. All four fields need board-type-aware handling on Create per the decisions above.
- `QuestEntity` (`QuestBoard.Repository/Entities/QuestEntity.cs`) — `IsFinalized` (bool), `FinalizedDate` (nullable DateTime) are the fields the one-shot flow uses for both "picked a date" and driving the Quest Log's next-day-wait filter. No existing "closed" concept exists yet — net new for this phase.
- Wax-seal CSS (`QuestBoard.Service/wwwroot/css/quests.css:230-267`, mirrored in `site.css`): `.open-seal { display: none; }` (hidden entirely when not finalized), `.finalized-seal` (brightened/saturated filter, shown when finalized), `.user-signup-seal`/`.signup-seal-image` (hue-rotated blue, shown top-right only when the current viewer has a `PlayerSignup` on that quest — independent of finalized state).

### Established Patterns
- `GetCompletedQuestsAsync` (`QuestBoard.Domain/Services/QuestService.cs:164-175`) — Quest Log's "next-day wait" filter: `q.IsFinalized && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date && !q.DungeonMasterSession`. CQUEST-05 requires campaign-closed quests to skip this next-day wait entirely — whatever entity design is chosen must make that filter (or an equivalent OR-branch) not apply to campaign quests.
- `DailyReminderJob.ExecuteAsync` (`QuestBoard.Service/Jobs/DailyReminderJob.cs:13-44`) — queries `GetQuestsForTomorrowAllGroupsAsync(tomorrow)`, keyed off `FinalizedDate`. If campaign quests never populate `FinalizedDate` (i.e. Close uses different field(s) than Finalize), this job naturally already excludes them with zero changes — worth confirming during research rather than adding explicit `BoardType` branching to the job.
- No "posted" email exists today for one-shot quests either (only `Finalized` and session-reminder emails exist) — CQUEST-06's "no posted" requirement may already be satisfied by simply not adding one, rather than needing to suppress an existing mechanism.
- Index.cshtml quest board card (`QuestBoard.Service/Views/Quest/Index.cshtml:52-135`) — CR badge, wax seal, and signup-count line are all rendered inline per-card (no shared partial yet). Details.cshtml (923 lines) and Manage.cshtml (764 lines) are both heavily intertwined with signup/date-voting Razor logic and are the two views the "extract into board-type-conditional partials" work (per PROJECT.md target features) applies to.

### Integration Points
- `QuestController.Create` (GET ~line 64, POST likely ~78-111) — where `ProposedDates` requirement needs to become conditional on the group's `BoardType`.
- `Areas`/views: `Views/Quest/Index.cshtml`, `Details.cshtml` (+`.Mobile`), `Manage.cshtml` (+`.Mobile`) — all need board-type-conditional rendering for the card/badge/signup-section changes captured above.
- `Views/QuestLog/Index.cshtml` (lines 16-59) and `Details.cshtml` — card simplification (drop CR, drop Adventurers count) and Recap (`UpdateRecap`) continuing to work unchanged for campaign entries.
- New `CloseQuestAsync`/`ReopenQuestAsync` on `IQuestService`/`QuestService`/`IQuestRepository`/`QuestRepository`, plus new `Close`/`Reopen` actions on `QuestController` mirroring `Finalize`/`Open`.

</code_context>

<specifics>
## Specific Ideas

- **Wax-seal treatment for campaign quests (unresolved, flagged for research):** The user clarified the actual mechanic — the bottom-left seal on the board card represents "finalized" state (hidden via `.open-seal{display:none}` when not finalized, brightened via `.finalized-seal` when finalized); the top-right seal represents the *current viewer's own signup* (hue-rotated blue via `.user-signup-seal`/`.signup-seal-image`), shown independent of finalized state. For campaign quests: the top-right signup seal will naturally never render (no `PlayerSignups` ever exist), so that half needs no work. The bottom-left "finalized state" seal has no clean campaign equivalent — the user was unsure whether to relabel it Closed/Open using the same seal mechanic, replace it with a different visual entirely, or something else, and explicitly asked for this to be researched rather than decided now. Whatever the researcher/planner proposes should stay consistent with the existing wax-seal aesthetic unless there's a strong reason to diverge.
- User confirmed a consistent simplification philosophy across board card and Quest Log card: when a data point (CR, signup counts) doesn't apply to campaign mode, remove it and let remaining content (mainly the description) expand into the freed space, rather than substituting placeholder content.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 36-campaign-quest-posting-closing*
*Context gathered: 2026-07-03*
