---
phase: 75
slug: event-availability-signups
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-28
---

# Phase 75 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

Register origin: authored at plan time. All five PLAN.md files carried a `<threat_model>`
block, so this is a verification run, not a retroactive STRIDE reconstruction.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser to the two write endpoints | Untrusted event id and availability value; the actor identity must never come from the request | Event id, availability enum, antiforgery token |
| Browser to rendered control visibility | Whether a control was rendered says nothing about whether the request is permitted | Presentation state only |
| `EventsController` to `EventSignups` insert | The global read filter offers no protection on an insert, so board membership is re-verified in the controller | Signup row (event id, user id, availability) |
| Domain/Service caller to `IEventSignupRepository` | `eventId` and `userId` arrive as plain integers; the repository cannot see the HTTP principal | Row identifiers |
| `QuestBoardContext` global query filter to `DbSet.Add` | The filter constrains reads only; an insert crosses out of its protection entirely | Signup row |
| Platform admin request to `GroupService` membership writes | `groupId` is an arbitrary route value unrelated to the admin own selected board | Group id, user id |
| `GroupRepository` to `DbContext` with `IgnoreQueryFilters()` | The one place in this phase that deliberately steps outside tenant scoping and must re-impose it by hand | Event ids, signup rows |
| Test harness shared group context to test classes | A singleton mutated by one class can turn a genuine failure into a false pass | Active board id, board-type flag |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-75-01 | Elevation of Privilege | `SetAvailability` / `Withdraw`, `EventSignupRepository` | high | mitigate | Actor resolved via `userService.GetUserAsync(User)`; zero user/member/signup id parameters on either action; repository locates rows by the `(eventId, userId)` pair, never by row id | closed |
| T-75-02 | Information Disclosure | Cross-board availability read and write | high | mitigate | `EventIsOnActiveBoard` (3 call sites) compares the loaded event `GroupId` to the active board and fails closed when none is selected | closed |
| T-75-03 | Tampering | Hand-crafted withdraw on a campaign board | medium | mitigate | Board type re-resolved server-side via `IBoardTypeResolver`; anything not one-shot, including null, returns bad request | closed |
| T-75-04 | Information Disclosure | Cross-board insert; `IgnoreQueryFilters` helpers | high | mitigate | `AnyAsync` existence probe against the filtered `Events` set before insert, throwing on miss; both `IgnoreQueryFilters()` calls immediately followed by a `Where` re-imposing scope from the explicit `groupId` | closed |
| T-75-05 | Tampering | Out-of-range availability value | low | mitigate | `ModelState.IsValid` plus `Enum.IsDefined(typeof(VoteType), availability)`; entity `[Range(0, 2)]` as backstop | closed |
| T-75-06 | Spoofing | CSRF on the two new write endpoints | medium | mitigate | Both actions carry `[ValidateAntiForgeryToken]`; view scripts send `__RequestVerificationToken` | closed |
| T-75-07 | Repudiation | Who set an availability answer | low | accept | See Accepted Risks Log | closed |
| T-75-08 | Tampering | Ambient board-type confusion on membership writes | high | mitigate | Board type read from `Groups` by the explicit `groupId`; `IBoardTypeResolver` verified absent from `GroupService` | closed |
| T-75-09 | Denial of Service | Failed backfill leaving a campaign member unable to answer | high | mitigate | Membership row and signup rows staged on one context; exactly one `SaveChangesAsync` per membership method, so join and backfill commit or fail together | closed |
| T-75-10 | Tampering | Widened save misreading a write failure as a duplicate-membership race | medium | accept | See Accepted Risks Log | closed |
| T-75-11 | Denial of Service | Leaving a board destroys availability history irreversibly | medium | accept | See Accepted Risks Log | closed |
| T-75-12 | Information Disclosure | Fan-out reading the member list for the wrong board | medium | mitigate | Member list loaded with the same validated `activeGroupId` that is stamped onto the event, so members and event cannot diverge | closed |
| T-75-13 | Information Disclosure | Leaking the answered-versus-default distinction early | medium | mitigate | Zero occurrences of `HasAnswered` or `UpdatedAt` in either event view model; render test asserts neither name appears in the body | closed |
| T-75-14 | Information Disclosure | Roster exposing member names to a non-member | low | mitigate | Details action returns `NotFound` for an off-board event before the view renders; page requires authentication | closed |
| T-75-15 | Repudiation | False pass from leaked singleton harness state | medium | mitigate | Both new test classes implement the async lifecycle and reset active board and board-type flag in `DisposeAsync` | closed |
| T-75-16 | Information Disclosure | A refused write that was actually accepted against the wrong board | high | mitigate | Refusal facts assert resulting table state through the unfiltered seeding context, so an accepted-but-misrouted write fails the fact | closed |
| T-75-SC | Tampering | Package installs | low | accept | See Accepted Risks Log | closed |

*Status: open / closed / open below threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `security.block_on` (high) count toward threats_open*
*Disposition: mitigate (implementation required) / accept (documented risk) / transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-75-01 | T-75-07 | `UpdatedAt` records that a person answered, not which person changed what or when it previously differed. No audit trail is in scope for this phase and none of the five requirements asks for one. | Plan author (75-01) | 2026-08-28 |
| R-75-02 | T-75-10 | An event would have to be deleted between the read and the insert for the duplicate-membership message to mislead. The narrowed assumption is documented at the catch; provider-specific error inspection is not warranted at this scale. | Plan author (75-02) | 2026-08-28 |
| R-75-03 | T-75-11 | Destroying availability history on leave is a deliberate operator decision, restated in the phase objective. The mitigation actually requested was a visible warning, which both Platform Members views now carry. | Plan author (75-02) | 2026-08-28 |
| R-75-SC | T-75-SC | The phase installs no packages. Research confirmed no external dependency is added, so the package-legitimacy gate has nothing to audit. | Plan author (all plans) | 2026-08-28 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-28 | 17 | 17 | 0 | /gsd-secure-phase (L1 verification; short-circuit: register authored at plan time, asvs_level 1) |

### Verification notes

Every `mitigate` threat was confirmed against the shipped implementation rather than accepted
from the plan text. Two security-relevant controls in this phase were found broken *during* the
phase and fixed before this audit, which is why they are recorded closed rather than never-broken:

- The member-removal confirmation (the mitigation credited to T-75-11) never compiled. An
  HTML-entity apostrophe terminated the inline handler JS string early, so the handler was
  treated as absent and the form submitted with no confirmation at all. Fixed in `62cfa06`
  and verified live in-browser: the handler compiles, fires with the correct text, and returns
  false on cancel. The pre-fix markup was replayed in the same engine and produced no handler,
  confirming both the bug and the fix.
- `SetAvailability` was missing its `ModelState.IsValid` check (T-75-05), so an out-of-range
  value silently defaulted to No instead of being refused. Found and fixed during plan 75-05.

**Robustness note, not a threat:** the T-75-04 mitigation throws `ArgumentException` on a
cross-board or deleted-event insert. The write is correctly refused, so the security property
holds, but that exception is unhandled at the controller and surfaces as a 500 rather than a
graceful 404 on a narrow delete race. Tracked as a Warning in `75-REVIEW.md`; it does not open
the threat.

**Scope note:** this run verified the threat register authored across the five plans. It was not
a fresh scan for threats outside that register — correct for a plan-time register at ASVS L1, but
it means novel threats introduced outside the modelled boundaries would not be surfaced here.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
