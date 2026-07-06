### Phase 54: Fix mobile signup for finalized quests (inconsistent with desktop)

**Goal:** Mobile users can join a finalized One-Shot quest via the same 3-button "Join This Quest" card desktop already has (Join as Player / Assistant DM / Spectator with a shared character select), and — for both platforms — a new Player joining a full finalized quest is placed on the existing waitlist (`IsSelected = false`) instead of being hard-rejected, with the "quest full" copy rewritten to match.
**Requirements**: None (ad-hoc bug-fix phase — no REQ-IDs; source of truth is 54-CONTEXT.md decisions D-01 through D-06)
**Depends on:** Phase 53
**Plans:** 2 plans

Plans:
**Wave 1** *(both parallel — disjoint files)*

- [ ] 54-01-PLAN.md — JoinFinalizedQuest waitlists a full Player join instead of rejecting it (D-03/D-04/D-05) + new QuestJoinFinalizedQuestTests integration coverage + PlayerSignupRepositoryTests new-joiner ordering test
- [ ] 54-02-PLAN.md — Mobile "Join This Quest" card in Details.Mobile.cshtml (D-01/D-02) + locked D-06 quest-full copy on both Details views + MobileViewsTests coverage + real-device human verification
