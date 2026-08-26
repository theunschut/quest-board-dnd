# Pitfalls Research

**Domain:** Rolling-improvements milestone (v9.0) on a mature ASP.NET Core 10 MVC app (D&D Quest Board, ~60-70k LOC, 17 users, self-hosted LXC)
**Researched:** 2026-08-25
**Confidence:** HIGH (grounded directly in the current source tree and the project's own documented incident history in `.planning/PROJECT.md`)

This research covers two unrelated items in the same milestone:
- **Item 1** — add a "change character" affordance to an existing quest signup (desktop `Views/Quest/Details.cshtml` + mobile `Views/Quest/Details.Mobile.cshtml`), including clearing back to "no character," backed by the already-existing `QuestController.UpdateSignupCharacter(int questId, int? characterId)`.
- **Item 2** — resolve 5 stale HIGH Dependabot alerts for `System.Security.Cryptography.Xml` 8.0.0-8.0.3, all pointing at `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`, a manifest deleted in commit `a477ab9`.

## Critical Pitfalls

### Pitfall 1: Third near-duplicate character-cell block, not a second one

**What goes wrong:**
`Details.cshtml` already has the character-cell markup duplicated twice: the finalized-participants table (`~L108-145`, loop variable `participant`) and the waitlist table (`~L219-259`, loop variable `player`). Both blocks are structurally identical — same avatar `<img>`/placeholder/`onerror` fallback, same "No character" + conditional "Add character" plus-button that opens `#addCharacterModal`. Adding a "change character" control means editing **both** blocks in `Details.cshtml`, and a **third**, independent copy in `Details.Mobile.cshtml` — which today has **no character-cell UI at all** for participants (confirmed: `Details.Mobile.cshtml` has zero references to `addCharacterModal` or `UpdateSignupCharacter`). A developer who patches only the finalized-participants block (the one they're staring at when the ticket says "quest details") and misses the waitlist block reproduces this project's own most-cited failure mode: `Characters/Edit.cshtml` shipped for an entire phase (68) missing the `classIndex` guard its 3 siblings had, because nobody grepped for every copy before calling it done.

**Why it happens:**
The task is framed as "add change-character to the Details page" (singular), but the page contains two independent per-row renderings of the same concept, plus a third page for mobile. Nothing in the file signals that these blocks must stay in lockstep — no shared partial, no comment cross-referencing the sibling block.

**How to avoid:**
- Before writing any markup, `grep -n "participant.Character\|player.Character" Views/Quest/Details.cshtml Views/Quest/Details.Mobile.cshtml` to enumerate every character-cell instance (currently: 2 in desktop, 0 in mobile — 3 total sites needing the new control).
- Extract the character-cell markup (avatar + name + "no character" + action button) into one shared `_QuestSignupCharacterCell.cshtml` partial parameterized on `(PlayerSignup signup, bool isCurrentUser, bool canChange, List<Character> userCharacters)`. This turns "keep 3 copies in sync" into "call one partial 3 times" — the actual fix this codebase needed for `Characters/Edit.cshtml` but never got (that fix is still an open, unactioned item in Known Issues).
- If a shared partial is out of scope for this small phase, at minimum write the change as a single task touching all 3 sites together (desktop finalized table + desktop waitlist table + mobile), per the "Mobile parity enforced by pairing desktop+mobile edits into the SAME task" lesson this project already learned the hard way in Phase 43 and Phase 54 (see Key Decisions).

**Warning signs:**
- A diff that touches only one of `Details.cshtml`'s two character-cell blocks.
- A diff that touches `Details.cshtml` but not `Details.Mobile.cshtml`.
- Code review or manual test shows "change character" works in the finalized-participants table but the waitlist row (or mobile) still shows the old static "No character"/plus-button-only markup.

**Phase to address:**
The Item-1 implementation phase itself (view-layer work) — this is not a follow-up concern, it is the core risk of the phase.

---

### Pitfall 2: Reusing the existing "Add character" modal breaks "clear back to no character"

**What goes wrong:**
The only existing character-selection UI is `#addCharacterModal` (`Details.cshtml` ~L820-848), a Bootstrap modal with `<form asp-action="UpdateSignupCharacter" method="post">` and `<select name="characterId" id="characterSelect" class="form-select" required>`. It is only rendered when `participant.Character == null` (the "no character yet" branch) and its `<select>` is marked `required`, with no blank/"No Character" `<option>`. If the new "change character" work is implemented by naively copy-pasting this modal into the "character already assigned" branch, the `required` attribute silently makes "clear back to no character" impossible through that form — the browser blocks submission of an empty selection.

**Why it happens:**
The existing modal was built for the single case of "add a character where none exists," where `required` is correct. Reusing it verbatim for "change/clear an existing selection" carries that constraint along without anyone re-examining whether it still applies.

**How to avoid:**
- Explicitly decide, per requirement ("clear back to no character" is called out in the milestone target), that the change-character control needs a `<option value="">-- No character --</option>` and **no** `required` attribute, or a separate explicit "Clear" action.
- The controller/service already support this cleanly: `UpdateSignupCharacter(int questId, int? characterId)` takes a nullable `characterId` with no `[Required]`, and `PlayerSignupService.UpdateSignupCharacterAsync` does a bare `playerSignup.CharacterId = characterId;` — a null assignment already round-trips correctly. This is purely a view/markup gap, not a backend one — do not add backend work that already exists.

**Warning signs:**
- Attempting to submit the change form with no character selected does nothing (browser-native validation blocking submission), rather than clearing the character.

**Phase to address:**
Item-1 implementation phase, view layer only.

---

### Pitfall 3: Trusting "the controller already validates ownership" without re-verifying the layered defense is actually intact

**What goes wrong:**
This app has shipped **two real cross-tenant security leaks discovered mid-milestone in v7.0** (Phase 49's `GuildMembersController`/`DungeonMasterController`/`PlayerSignupEntity` leak, and Phase 55's `GroupSessionMiddleware` SuperAdmin fail-open leak affecting 7 entity query filters including `PlayerSignupEntity`). `UpdateSignupCharacter` today reads as safe: `characterService.GetCharacterWithDetailsAsync(characterId.Value)` is scoped by `CharacterEntity`'s fail-closed `HasQueryFilter` (added Phase 49, hardened Phase 55) to `ActiveGroupId`, `questService.GetQuestWithDetailsAsync(questId)` is scoped by `QuestEntity`'s equivalent filter, and `character.OwnerId != user.Id` blocks assigning someone else's character. **But this project's own recorded lesson (Key Decisions, Phase 49) is explicitly: "Authorization checks must validate the TARGET resource's group, not just the caller's role" and "'Reached only through an already-filtered navigation' code comments must be empirically verified, not trusted."** The safety of this action currently depends entirely on two EF Core query filters and `GroupSessionMiddleware`'s `ActiveGroupId` resolution being correct at the same time — exactly the multi-layer setup that has already silently regressed once (Phase 55: SuperAdmin's null-`ActiveGroupId` escape hatch stayed live across `Quest`/`ShopItem`/`ProposedDate`/`PlayerDateVote`/`PlayerSignup`/`ReminderLog`/`UserTransaction` for an entire prior milestone before being caught by a user bug report, not by tests).

**Why it happens:**
"It already exists and works" (per the milestone's own framing of `UpdateSignupCharacter`) is read as "already verified safe," when in fact no dedicated cross-tenant regression test exists for this specific action today — the general fail-closed filters were verified for other call sites (Phase 49/55), not this one.

**How to avoid:**
- Before adding UI on top of `UpdateSignupCharacter`, add (or confirm the existence of) an integration test asserting: a user in Group A cannot set `characterId` to a character owned by a same-named user in Group B, even when that character's numeric ID is guessable/sequential. This is the exact test shape Phase 49/55 used to close prior leaks.
- Do not add a redundant manual group-membership check inside the action if the query filters already guarantee scoping — that duplicates logic the codebase deliberately consolidated onto the EF filter layer (see Phase 55 Key Decision: "Fail-closed query filters treated as defense-in-depth, layered on top of \[not instead of\] the middleware gate fix"). The correct fix if a gap is found is to hold the filter/middleware layer accountable, not to bolt on a third redundant check.
- Confirm `GetQuestWithDetailsAsync`/`GetCharacterWithDetailsAsync` are not accidentally called with `IgnoreQueryFilters()` anywhere in this path (one documented `IgnoreQueryFilters` usage already exists elsewhere in `QuestRepository.cs:267` for a deliberate cross-group admin case — verify `UpdateSignupCharacter`'s call path does not share that code path).

**Warning signs:**
- No test in the repo exercises `UpdateSignupCharacter` with cross-group IDs.
- Any new code path that resolves `characterId` via a repository method not already covered by `CharacterEntity`'s `HasQueryFilter` (e.g., a raw SQL query, a `.IgnoreQueryFilters()` call, or a new lookup added "for convenience" in the dropdown-population code).

**Phase to address:**
Item-1 implementation phase — add the regression test as part of the same phase, not deferred. Optionally run `/gsd:secure-phase` on this phase given the project's established practice of independently re-verifying threat mitigations after auth-adjacent changes (used for Phases 52, 55, 56).

---

### Pitfall 4: Silent inconsistency between `UpdateSignupCharacter` (no finalized guard) and `UpdateSignup` (hard finalized guard)

**What goes wrong:**
`QuestController.UpdateSignup` (date votes, `~L496-518`) explicitly returns `NotFound()` when `quest.IsFinalized`. `UpdateSignupCharacter` (`~L523-555`) has no such check — a character can be changed on a finalized quest's signup today, silently. If Item 1 ships a UI that surfaces this control on the finalized-participants table (which it must, since that's one of the two places the character cell lives), users will be able to swap characters *after* the DM has already finalized the roster and (for One-Shot boards) already sent the "Quest Finalized" email listing the original character. Whatever the milestone decides — allow it, or lock it down to match `UpdateSignup` — the actual bug is if nobody makes the decision explicitly and the UI just inherits whatever the untouched controller already does.

**Why it happens:**
Two sibling actions on the same signup drifted independently over the app's history; nothing enforces they share a validation rule, and the milestone's own framing ("the controller action already exists and works") discourages touching it.

**How to avoid:**
- Force an explicit decision during discuss-phase: does changing character after finalization (a) work silently as today, (b) get blocked to match `UpdateSignup`'s `NotFound()`, or (c) work but re-trigger no email (since Finalized/SessionReminder/WaitlistPromoted emails already render the participant list with character at *send* time, not signup time — verify whether any email caches character name at finalization vs. reading live at send time before assuming "no consequence").
- Whatever is decided, gate the *UI control's visibility* to match the *controller's actual enforcement* — do not show a "Change" button that then silently fails (or silently succeeds with no visible effect) because the two layers disagree.
- If leaving current (unguarded) behavior, document it as a deliberate decision in CONTEXT.md, not an oversight, per this project's own established practice of recording every locked-vs-deferred call (see the "Key Decisions" table's density in PROJECT.md).

**Warning signs:**
- A "Change character" button appears on a finalized quest's Details page, and clicking it either does nothing (if wired against `UpdateSignup`'s guard by mistake) or works fine but no one verified an email wasn't supposed to be resent/regenerated.

**Phase to address:**
Item-1 discuss-phase (decision), Item-1 implementation phase (enforcement + view gating).

---

### Pitfall 5: The "change character" dropdown silently hides — or silently discards — a deactivated character already on the signup

**What goes wrong:**
Confirmed in `QuestController`'s `Details` GET action: `userCharacters = allCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();` — `ViewBag.UserCharacters` is filtered to `Active` only (Retired and the newer `Dead` status, added Phase 52, are excluded). A signup's `Character` navigation property has no such filter — it shows whatever character was assigned at signup time, active or not, by design (Character validation via `CharacterStatus.Active` only runs on *write*, per the milestone brief). If the new "change character" `<select>` is populated straight from `ViewBag.UserCharacters` and pre-selected to `participant.Character.Id`, the currently-assigned character (now Retired/Dead) **will not appear in the option list at all**. Depending on how the `<select>` is built:
- If pre-selection is attempted via `asp-for`/`Selected` and the value isn't in the option list, browsers silently default to the first `<option>` in the list.
- If the user doesn't notice, hits "Save" without touching the dropdown, they unintentionally change the signup's character to whatever the first Active character happens to be — a data-loss bug masquerading as "no-op."

**Why it happens:**
The dropdown's data source (`Active`-only characters) and the field being edited (`Character`, any status) were built for different purposes (create/join uses Active-only by design; the historical signup can hold any status) and were never reconciled because no UI previously needed to display both together.

**How to avoid:**
- Build the option list as: the user's Active characters **plus** the currently-assigned character if it is not already Active (labeled distinctly, e.g. "Grimshaw the Bold (Retired) — current"), so the true current state is always representable and never silently defaults elsewhere.
- Never rely on implicit browser first-option fallback for a field this consequential — explicitly render a disabled/labeled entry for the actual current value so "I didn't touch the dropdown" and "I explicitly selected this" are visually distinguishable states.
- Add a regression test: a signup whose `Character.Status` is `Retired`/`Dead` renders the Details page without throwing and without silently pre-selecting a different character.

**Warning signs:**
- QA/manual test: retire a character that's currently signed up to a quest, reload Details, observe the change dropdown's initial selection versus the character actually shown in the row.

**Phase to address:**
Item-1 implementation phase — this is a correctness bug in the new feature itself, not pre-existing debt.

---

### Pitfall 6: Adding a 4th interaction pattern into an already-inconsistent CSRF/redirect mix

**What goes wrong:**
`Details.cshtml`/`Details.Mobile.cshtml` currently mix at least two patterns for mutating a signup: (a) plain `<form asp-action="...">` full-page POST-and-redirect (`UpdateSignupCharacter`'s existing add-character modal, `UpdateSignup`'s date-vote form), and (b) `fetch()` calls that manually append `__RequestVerificationToken` and hit `RevokeSignup`/`ChangeVote` directly (confirmed at `Details.cshtml:869-893` and mirrored in `Details.Mobile.cshtml:395-410`), returning `Ok()`/JSON rather than a redirect. If "change character" is implemented as a third variant — say, an inline dropdown with `fetch()` posting to `UpdateSignupCharacter` — note that `UpdateSignupCharacter` currently `return RedirectToAction("Details", ...)` on success, not `Ok()`. A `fetch()` call against an action that returns a `RedirectResult` gets a 302 response with `fetch`'s default `redirect: 'follow'` silently re-GETting the Details page and returning that HTML as the fetch response body — not a clean success signal the calling JS can branch on. Wiring `fetch()` UI against this action without changing its return type (or explicitly setting `redirect: 'manual'` and handling the resulting opaque-redirect response) produces a "works in testing, silently misbehaves for edge cases" bug (e.g., JS assumes success and updates the DOM optimistically even if the server actually rejected the character with `BadRequest`, since a `BadRequest` *does* reach `fetch` correctly but a redirect does not signal failure vs. success the way `ChangeVote`'s `Ok()`/error-status pattern does).
Conversely, if the plain-form pattern is reused (like the existing add-character modal), it inherits a full-page reload for what should feel like a small, local action — inconsistent with the "chip"-style ChangeVote/RevokeSignup UX already on the page.

**Why it happens:**
Two established but divergent patterns already coexist on this exact page; the milestone doesn't specify which one to extend, and the existing action's response type (`RedirectResult`) was written for the plain-form pattern, not the `fetch()` one.

**How to avoid:**
- Decide explicitly which pattern "change character" follows, and if choosing the `fetch()`/inline pattern (to match `ChangeVote`'s already-established chip-like UX), change `UpdateSignupCharacter`'s success path to return `Ok()` (or a small JSON payload) consistently with `ChangeVote`, rather than leaving it returning `RedirectToAction` and papering over the mismatch client-side.
- Any new `fetch()` call must copy the existing `__RequestVerificationToken` append pattern exactly (`Details.cshtml:869`/`890`) — do not assume the antiforgery cookie alone is sufficient; this codebase's own convention requires the token in the form body for these `fetch()`-based POSTs.
- If keeping the plain-form pattern (simpler, lower risk given the milestone is "small"), that's an acceptable and consistent choice too — just don't mix `fetch()` targeting an action that still returns `RedirectToAction`.

**Warning signs:**
- Browser network tab shows a `fetch()` call to `UpdateSignupCharacter` returning a 200 response whose body is a full HTML page (the redirected-to Details page), not the JSON/empty body the calling JS expects.
- Character-change UI appears to succeed (no console error) but the visible character doesn't actually update without a manual page refresh.

**Phase to address:**
Item-1 implementation phase.

---

### Pitfall 7: Assuming mobile markup renders just because it's in a `.Mobile.cshtml` file

**What goes wrong:**
PROJECT.md documents a real, still-open case where mobile view files are dead code: `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` is never selected because the Platform area's own `_ViewStart.cshtml` doesn't branch on `IsMobile` the way the root `_ViewStart.cshtml` does — two CSS file header comments even incorrectly claim otherwise. `Views/Quest/Details.Mobile.cshtml` is **not** in the Platform area (the root `_ViewStart.cshtml`'s `IsMobile` branching does apply here, per the working Phase 12-19 mobile-view-location-expander pattern), so this specific trap does not directly apply to Item 1 — but the underlying discipline it teaches does: **never assume a `.Mobile.cshtml` edit is live without confirming it actually renders for a real mobile user-agent.** This app's mobile-view selection is User-Agent-based, not viewport-based (explicitly noted in Phase 48's shipped-item text) — browser devtools "mobile emulation" alone does not always reproduce the real UA-sniffing path; Phase 54 explicitly logged its real-device verification checkpoint as a user-approved deviation (browser emulation instead of a physical device) rather than silently treating it as equivalent.

**Why it happens:**
The codebase already has one proven case of "the mobile file exists, is edited, and never renders" — treating "I edited the `.Mobile.cshtml` file" as equivalent to "I verified the change is visible on mobile" is an easy, previously-realized mistake here.

**How to avoid:**
- After implementing, load the quest Details page with a real mobile User-Agent (a real device over LAN, per this project's own standing requirement reaffirmed in Phase 43 — "verified on a real iPhone... not devtools emulation, per this project's own standing PITFALLS.md requirement") or, at minimum, confirm via `curl -A "<mobile UA string>"` or the root `_ViewStart.cshtml`'s actual `IsMobile` detection logic that `Details.Mobile.cshtml` is the file being selected for this specific route, not silently falling back to desktop.
- Do not extrapolate "Platform area's mobile layout is broken" to "therefore Quest's mobile view is also broken" or vice versa — verify this specific route/area independently; they use different `_ViewStart.cshtml` files with different (and, per PROJECT.md, inconsistent) behavior.

**Warning signs:**
- The "change character" control appears correctly in desktop devtools mobile emulation but a real phone shows unstyled/stale markup, or shows no control at all.

**Phase to address:**
Item-1 implementation phase — verification step, ideally with a real device per the project's own established (if inconsistently followed) bar.

---

### Pitfall 8: Green tests don't prove the real DI graph is safe for this change

**What goes wrong:**
PROJECT.md records, as a still-open Known Issue: "Integration tests always override `IActiveGroupContext`/`IBoardTypeResolver` with a test double (`MutableGroupContext`), so no automated test exercises `Program.cs`'s real production DI graph end-to-end — a regression of the circular DI cycle fixed in Phase 37 wouldn't be caught by the current suite." Pitfall 3 above depends on `IActiveGroupContext`'s real resolution being correct in production; the test suite that will be used to sign off on Item 1 structurally cannot detect a regression in that resolution, because it never runs through it. A fully green `dotnet test` run after adding the change-character feature is evidence the *feature logic* works against the test double's group context — it is not evidence the *authorization boundary* (Pitfall 3) holds in the real app.

**Why it happens:**
Test doubles for cross-cutting infrastructure (group context, board-type resolution) are the correct default for unit/integration test isolation and speed — but they mean this specific class of regression (session/DI-graph-level authorization bypass) has a structural blind spot the suite itself can't close, a fact this project has already explicitly named as a known gap rather than accidentally overlooked.

**How to avoid:**
- Don't treat "544/609+ tests green" as sufficient sign-off for the character-change feature's authorization safety. Pair it with a manual, live-app smoke test (a real `dotnet run` against the real DI graph, logging in as two different-group users, confirming cross-group character IDs are rejected) — the same mitigation this project already uses elsewhere for this exact gap ("mitigated once by a live `dotnet run` smoke test during verification, no permanent guard" — Phase 37).
- If this phase adds meaningful new authorization surface (Pitfall 3's regression test), consider whether it's worth removing the `IActiveGroupContext` override for that one specific test, exercising the real service — a heavier but more conclusive test, consistent with the project's own unresolved wish to eventually close this gap.

**Warning signs:**
- Sign-off reasoning that cites "tests pass" as the sole evidence for the cross-tenant safety of the new dropdown, with no mention of manual verification against the real DI graph.

**Phase to address:**
Item-1 implementation phase, verification step.

---

### Pitfall 9: Dismissing the 5 Dependabot alerts on "the file is gone" alone, without ruling out a stale dependency graph

**What goes wrong:**
`git status`/`dotnet list package --include-transitive` correctly show the package is absent from every tracked project today — but that is necessary, not sufficient, evidence for dismissal. GitHub's Dependabot alerts are driven by its **dependency graph**, a separately-cached index of manifests that does not automatically re-scan on every push; per GitHub's own troubleshooting documentation, if the dependency graph doesn't accurately reflect the current repository, it must be **manually refreshed** ("Refresh Dependabot alerts," rate-limited to once per hour) — simply deleting the manifest file does not retroactively clear alerts already raised against it. Dismissing without first forcing and confirming a graph refresh risks two failure modes: (a) closing a real, still-valid alert while the underlying stale-graph entry silently persists and later resurfaces as "new" (confusing future audits), or (b) — the more dangerous case — assuming the alert is purely a graph artifact when it is in fact tracking a genuinely different location than assumed (a non-default branch, a fork, or an old cached commit), and a real vulnerable reference still exists somewhere reachable.

**Why it happens:**
"The package isn't in `dotnet list package`" feels like conclusive proof because it's the developer-facing view of dependencies — but Dependabot alerts are scoped to *GitHub's* dependency graph, which is a distinct, independently-refreshed system that can lag or point at a different ref entirely. Treating a local CLI check as equivalent to GitHub's remote index is the exact reflexive-dismissal trap this research question flags.

**How to avoid — evidence that must actually be gathered before dismissal is defensible:**
1. **Confirm scope per alert, not just per package.** Open each of the 5 alerts in GitHub's Security tab and record: which branch/ref it's attributed to, the manifest path shown (`EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`), and the "detected" vs. "last updated" timestamps. If all 5 show a detection date at or before commit `a477ab9` (2026-06-29) and no update since, that's consistent with — but does not yet confirm — staleness.
2. **Confirm the default branch is what's being scanned.** Dependabot's dependency graph tracks the repository's default branch (`main`) by default; verify in repo Settings that no non-default branch, and no fork, is separately configured as a dependency-graph source. This directly answers "can Dependabot be scanning a stale or non-default branch, a fork, or a cached dependency graph" — check, don't assume.
3. **Force a graph refresh and re-check.** Trigger "Refresh Dependabot alerts" (Security → Dependabot alerts → the refresh action in the list header) and wait for the background task to complete (rate-limited to once/hour, so schedule this early in the phase, not as the last step). If the alerts auto-close after refresh, that is real, actionable evidence of staleness — not an assumption.
4. **Only after 1-3 confirm staleness**, dismiss each alert individually with a reason (see Pitfall 11) rather than bulk-dismissing.

**What a wrong dismissal would hide:**
If any of the 5 alerts is actually tracking a currently-reachable reference — e.g., a forked/mirrored copy of the repo, a long-lived feature branch that still has the old `.csproj`, or (see Pitfall 10) a resurrected file from an accidental re-commit — dismissing on the "local check is clean" assumption would permanently suppress a real, exploitable `System.Security.Cryptography.Xml` XML-signature-wrapping vulnerability (the actual CVE class behind these advisories) with no further warning.

**Warning signs:**
- Dismissal reasoning that cites only `dotnet list package --include-transitive` output, with no mention of the GitHub alert UI, branch scoping, or a graph refresh.
- All 5 alerts dismissed in a single bulk action with a generic reason rather than individually confirmed.

**Phase to address:**
Item-2 implementation phase — this IS the phase; do not treat evidence-gathering as optional overhead on top of the "real" fix.

---

### Pitfall 10: Leftover `EuphoriaInn.*` directories on disk are a live re-commit risk, confirmed present today

**What goes wrong:**
Verified directly in this repository's working tree: `git status --ignored` shows `EuphoriaInn.Domain/bin/`, `EuphoriaInn.Domain/obj/`, and the equivalent `bin/`/`obj/` subfolders for `EuphoriaInn.IntegrationTests`, `EuphoriaInn.Repository`, `EuphoriaInn.Service`, `EuphoriaInn.UnitTests` — meaning **all five `EuphoriaInn.*` top-level directories still physically exist on disk right now**, over a milestone after the rename commit. They're invisible to a plain `git status` only because `.gitignore`'s `bin/`/`obj/` patterns hide their contents — the directories themselves are not gitignored, only what's currently inside them. No `.csproj` currently exists in any of them (confirmed: only `bin/`/`obj/` subfolders present), so there is no *live* resurrection today — but this is a fragile absence, not a structural guarantee: a `git stash pop` from an old stash, a restored backup, an IDE "restore from local history," or a careless `git checkout a477ab9~1 -- EuphoriaInn.Domain/` could repopulate a real `.csproj` referencing the vulnerable package version into a directory `git add -A` would then happily stage (since only `bin/`/`obj/` are ignored, not the directory or a hypothetical `.csproj` placed directly in it).

**Why it happens:**
`.gitignore` patterns for build output (`bin/`, `obj/`) don't clean up the parent directory once its tracked contents are removed; a rename via `git mv` (or hand-editing paths, as this rename appears to have done non-mechanically per the commit's stated scope) leaves stale build artifacts behind since `dotnet clean`/`git clean -xdf` was evidently never run against the old directory names post-rename.

**How to avoid:**
- Delete the five leftover `EuphoriaInn.*` directories outright as part of this phase (`rm -rf EuphoriaInn.Domain EuphoriaInn.IntegrationTests EuphoriaInn.Repository EuphoriaInn.Service EuphoriaInn.UnitTests`, or the Windows equivalent) — there is nothing tracked or needed inside them; they are pure stale build cache.
- After deleting, confirm the solution still builds clean (`dotnet build`) to prove nothing was silently referencing the leftover `obj/project.assets.json`/`.deps.json` files (unlikely, but cheap to verify given a `.slnx`-based solution).
- This closes both the literal re-commit risk this pitfall names AND removes any chance that a future CI step or local tool that walks the full working directory (not just tracked files) could pick up the stale `obj/project.assets.json` inside `EuphoriaInn.Domain/obj/` and misattribute a dependency-graph entry to it.

**Warning signs:**
- `git status --ignored` (not plain `git status`) still lists `EuphoriaInn.*` paths after this phase closes.
- A future `dotnet build`/`dotnet restore` from repo root silently touches an `EuphoriaInn.*` directory.

**Phase to address:**
Item-2 implementation phase — cheap, concrete, and directly closes one of the plausible root causes for why the alerts still reference the old manifest path.

---

### Pitfall 11: Dismissing without recording why — the audit-trail gap

**What goes wrong:**
GitHub allows dismissing a Dependabot alert with a reason (e.g., "No bandwidth to fix," "Risk is tolerated," "Vulnerable code is not actually used," or a free-text note depending on the alert type) — but nothing forces a *specific, evidence-linked* reason. If these 5 alerts are dismissed with a generic or default reason (or worse, bulk-dismissed with no individual reasoning), a future reviewer — including a future audit, a future security-focused contributor, or the project owner themself six months later — cannot distinguish "this was genuinely investigated and confirmed dead" from "someone rubber-stamped 5 alerts to make the Security tab green." This project has an explicit, repeatedly-demonstrated norm of recording *why*, not just *what* (see the density of the "Key Decisions" table's rationale column throughout PROJECT.md, and the standing CLAUDE.md rule against untracked/unexplained changes) — a silent bulk dismissal breaks that norm specifically in the one place (security alerts) where it matters most.

**Why it happens:**
GitHub's dismiss UI makes bulk-dismiss with a dropdown reason fast and low-friction; the evidence-gathering (Pitfall 9) happens in a terminal/browser session that leaves no trace inside GitHub's own alert history unless someone deliberately writes it into the dismissal comment.

**How to avoid:**
- Dismiss each of the 5 alerts individually (not via a bulk action) with a reason that references the actual evidence gathered: e.g., "Manifest `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` removed in commit a477ab9 (EuphoriaInn→QuestBoard rename); confirmed absent from `dotnet list package --include-transitive` across all 5 current `.csproj` files as of \<date\>; dependency graph refreshed \<date\> and alert did not reappear; stale build artifacts at repo root deleted (Pitfall 10)."
- Additionally record the same summary once in `.planning/PROJECT.md`'s Known Issues or Key Decisions table (consistent with how e.g. the Phase 34 "clean dependency vulnerability scan captured as evidence" was logged) — GitHub's own audit trail is sufficient for GitHub's UI, but this project's own convention is to also make security posture visible in its own planning docs, not solely in a third-party tool's history.

**Warning signs:**
- All 5 alerts show the same generic dismissal reason with an identical timestamp (bulk action).
- No corresponding entry in PROJECT.md documenting the investigation.

**Phase to address:**
Item-2 implementation phase — the dismissal step itself.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Reuse the existing `#addCharacterModal` markup verbatim for "change," keeping its `required` `<select>` | Fastest path to a working "change" UI | Silently blocks the "clear to no character" requirement the milestone explicitly asks for | Never for this milestone — the requirement explicitly includes clearing |
| Skip extracting a shared character-cell partial, hand-edit all 3 sites identically | Smaller diff, faster to plan | Becomes the 4th documented instance of this exact drift class (`Characters/Edit.cshtml`, `Characters/Create.cshtml`'s dead branch, triple `BoardType` lookup, `.quest-description-mobile`) | Acceptable only if the 3 sites are edited in one atomic task with an explicit post-diff grep verifying all 3 changed identically |
| Leave `UpdateSignupCharacter` without a finalized-quest guard, matching neither `UpdateSignup`'s block nor an explicit "allowed" decision | Zero controller changes needed | Ships an undecided, undocumented behavior difference between two sibling actions on the same signup | Never — must be an explicit, documented decision either way |
| Bulk-dismiss all 5 Dependabot alerts with the same one-line reason | Fast, clears the Security tab in one click | No individual audit trail; can't distinguish real triage from rubber-stamping later (Pitfall 11) | Never |
| Skip forcing a Dependabot dependency-graph refresh before dismissing | Saves ~1 hour of rate-limit wait | Dismissal isn't grounded in confirmed-current GitHub state, only local CLI output (Pitfall 9) | Never — the refresh is cheap and the single most conclusive piece of evidence available |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|------------------|-------------------|
| `fetch()` vs. `RedirectToAction` mismatch on `UpdateSignupCharacter` | Wiring a `fetch()`-based inline control against an action that still returns `RedirectToAction`, silently swallowing the 302-followed HTML response as if it were a success signal | Change the action's success return to `Ok()`/JSON if adopting the `fetch()` pattern (matching `ChangeVote`), or keep the plain-form pattern consistently — don't mix without adjusting the response type |
| GitHub Dependabot dependency graph | Treating a local `dotnet list package` check as equivalent to GitHub's own graph state | Force "Refresh Dependabot alerts" (Security tab, once/hour) and re-check before dismissing (Pitfall 9) |
| `ViewBag.UserCharacters` (Active-only) vs. `PlayerSignup.Character` (any status) | Populating the change-character dropdown solely from the Active-only list, silently excluding the signup's actual current (possibly Retired/Dead) character | Explicitly include the currently-assigned character in the option list regardless of its status, labeled distinctly (Pitfall 5) |

## Performance Traps

Not applicable at meaningful scale for either item — 17 users, a single-signup dropdown edit, and a 5-alert manual security review carry no performance-scale dimension worth tracking here.

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Assuming `UpdateSignupCharacter`'s existing ownership/query-filter checks are sufficient without a dedicated cross-tenant regression test | Silent reopening of the exact class of leak already shipped twice in v7.0 (Phase 49, Phase 55), specifically on `PlayerSignupEntity`/`CharacterEntity` — the very entities this action touches | Add an explicit cross-group integration test for this action as part of Item 1 (Pitfall 3) |
| No finalized-quest guard on `UpdateSignupCharacter`, inconsistent with sibling `UpdateSignup` | Not itself a cross-tenant leak, but an undecided authorization-adjacent inconsistency that could let a player retroactively alter the finalized roster's character record after DM sign-off | Explicit decision + matching guard, gated in phase scope (Pitfall 4) |
| Dismissing Dependabot alerts on incomplete evidence | A real, exploitable `System.Security.Cryptography.Xml` vulnerability (XML signature wrapping class) could remain reachable via a branch/fork/stale-graph the local check never examined | Full evidence chain per Pitfall 9 before any dismissal |
| Leftover `EuphoriaInn.*` directories creating a re-commit surface for the exact vulnerable manifest | A future accidental `git add -A`/restore could resurrect the flagged `.csproj`, reopening the alerts for real this time | Delete the leftover directories now (Pitfall 10) |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|--------------|-------------------|
| Change-character dropdown pre-selects a different character than the one actually shown in the row, because the current (Retired/Dead) character isn't in the Active-only option list | Player thinks they're keeping their assigned character but silently reassigns to whatever the browser defaulted the `<select>` to | Always render the true current value as a distinct, present option (Pitfall 5) |
| A "Change" control appears on a finalized quest's row but silently does nothing (or nothing visible) because of an unresolved guard mismatch | Confusing dead-end interaction, erodes trust in the control | Resolve Pitfall 4 explicitly before shipping the UI |
| Mobile users see no character-change control at all because the new markup landed only in `Details.cshtml` | Feature parity gap directly contradicting the milestone's stated scope ("desktop + mobile") | Verify all 3 sites (Pitfall 1) and real mobile-UA rendering (Pitfall 7) |

## "Looks Done But Isn't" Checklist

- [ ] **Change character on finalized-participants table:** Often the only place tested — verify the waitlist table's identical block was also updated (Pitfall 1).
- [ ] **Change character on mobile:** Often skipped because desktop "already works" — verify `Details.Mobile.cshtml` was touched at all, since it currently has zero character-cell UI, and verify with a real mobile User-Agent, not just devtools emulation (Pitfall 1, Pitfall 7).
- [ ] **Clear back to "no character":** Often silently blocked by a copy-pasted `required` `<select>` — verify submitting with no selection actually clears `CharacterId` to null server-side (Pitfall 2).
- [ ] **Cross-group character assignment:** Often assumed safe because "the query filters already handle it" — verify with an actual integration test using two different groups' character IDs (Pitfall 3).
- [ ] **Retired/Dead character on the signup:** Often invisible until manually tested — verify the dropdown correctly represents (not silently swaps away from) a currently-assigned inactive character (Pitfall 5).
- [ ] **Dependabot alert dismissal:** Often done from local evidence alone — verify each alert individually against GitHub's own dependency-graph refresh, branch scope, and a written per-alert reason (Pitfall 9, Pitfall 11).
- [ ] **Leftover `EuphoriaInn.*` directories:** Often left alone as "harmless clutter" — verify they're actually deleted, not just ignored by git (Pitfall 10).

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|------------------|
| Waitlist/mobile character-cell block missed | LOW | Grep for the sibling pattern, apply the same edit; if it recurs a second time, extract the shared partial instead of patching a 4th site by hand |
| `required` attribute silently blocks clearing | LOW | Remove `required`, add a blank/"No character" option, add a regression test asserting null round-trips |
| Cross-tenant gap found after ship | MEDIUM | Follow the Phase 49/55 precedent exactly: patch the specific gap, add the fail-closed regression test, run `/gsd:secure-phase` to independently re-verify, and log it in PROJECT.md's Key Decisions the same way those two incidents were |
| Dependabot alert wrongly dismissed, later found still valid | MEDIUM | Re-open via GitHub's UI (dismissed alerts can be reopened), re-run the full evidence chain (Pitfall 9), document the correction in PROJECT.md |
| Deleted `EuphoriaInn.*` directories broke something unexpected | LOW | `git status`/`dotnet build` immediately after deletion catches this before commit; nothing tracked lives in those directories so recovery is trivial (they were never referenced by the current solution) |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| 1. Third near-duplicate character-cell block | Item-1 implementation | Grep confirms all 3 sites (desktop finalized table, desktop waitlist table, mobile) changed identically; manual test on both tables + mobile |
| 2. `required` modal blocks clearing | Item-1 implementation | Manual test: submit change form with blank selection, confirm `CharacterId` becomes null in DB |
| 3. Cross-tenant re-verification | Item-1 implementation | New integration test with cross-group character IDs passes; optionally `/gsd:secure-phase` |
| 4. Finalized-quest guard inconsistency | Item-1 discuss-phase + implementation | CONTEXT.md records the explicit decision; controller behavior matches the decision; UI visibility matches controller behavior |
| 5. Stale/inactive character in dropdown | Item-1 implementation | Manual test: retire a signed-up character, reload Details, confirm dropdown shows true current state |
| 6. CSRF/fetch pattern mismatch | Item-1 implementation | Browser network-tab check: response type from the new control matches what the calling JS expects |
| 7. Mobile view-selection trap | Item-1 implementation, verification step | Real mobile User-Agent (ideally real device) confirms `Details.Mobile.cshtml` renders the new control |
| 8. Green tests ≠ real DI graph safety | Item-1 verification step | Manual live `dotnet run` smoke test with two real different-group users, alongside the automated suite |
| 9. Reflexive Dependabot dismissal | Item-2 implementation | Per-alert branch/graph-refresh evidence gathered and recorded before any dismissal |
| 10. Leftover `EuphoriaInn.*` re-commit risk | Item-2 implementation | `git status --ignored` shows zero `EuphoriaInn.*` paths after the phase closes |
| 11. Missing audit trail on dismissal | Item-2 implementation | Each of the 5 alerts has an individual, evidence-referencing dismissal reason; PROJECT.md records the investigation once |

## Sources

- `.planning/PROJECT.md` — "Known issues / tech debt," "Constraints," and "Key Decisions" sections (primary source; this project's own documented incident history, read in full for this research)
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `UpdateSignup` (L496-518), `UpdateSignupCharacter` (L523-555), `ChangeVote` (L557-596), and the `Details` GET action's `ViewBag.UserCharacters` population (Active-only filter)
- `QuestBoard.Service/Views/Quest/Details.cshtml` — finalized-participants character cell (~L108-145), waitlist character cell (~L219-259), `fetch()`-based `RevokeSignup`/`ChangeVote` calls (~L869-893), `#addCharacterModal` (~L820-848)
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` — confirmed absence of any character-cell/`addCharacterModal`/`UpdateSignupCharacter` reference; `fetch()`-based `RevokeSignup`/`ChangeVote` calls (~L395-410)
- `QuestBoard.Domain/Services/PlayerSignupService.cs` — `UpdateSignupCharacterAsync` (L36-46), confirming null `characterId` already round-trips cleanly at the service layer
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — exempt-path list, membership revalidation interval, and its own extensive header-comment history of prior regressions
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — confirmed `HasQueryFilter` present for `CharacterEntity`, `QuestEntity`, `PlayerSignupEntity`, and 4 others
- Live repository inspection (2026-08-25): `git show --stat a477ab98363e54afc6e21f131aac0aef6c1d3f3d` (the EuphoriaInn→QuestBoard rename commit); `dotnet list package --include-transitive` across all 5 current `.csproj` files (no `System.Security.Cryptography.Xml` hits); `git status --ignored=matching` and direct `ls` confirming all 5 `EuphoriaInn.*` directories still exist on disk with only stale `bin/`/`obj/` contents; no `.github/dependabot.yml` or dependency-submission workflow present in `.github/workflows/`
- [Troubleshooting the dependency graph — GitHub Docs](https://docs.github.com/en/code-security/supply-chain-security/understanding-your-software-supply-chain/troubleshooting-the-dependency-graph) — confirms Dependabot alerts require a manual "Refresh Dependabot alerts" action (rate-limited to once/hour) to reflect a repository's current manifest state; deleting a manifest does not retroactively clear existing alerts
- [Viewing and updating Dependabot alerts — GitHub Docs](https://docs.github.com/code-security/dependabot/dependabot-alerts/viewing-and-updating-dependabot-alerts) — per-alert dismissal reasons and dismissal-history visibility

---
*Pitfalls research for: v9.0 Rolling Improvements milestone (D&D Quest Board)*
*Researched: 2026-08-25*
