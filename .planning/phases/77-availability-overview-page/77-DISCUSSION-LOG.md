# Phase 77: Availability Overview Page - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-29
**Phase:** 77-availability-overview-page
**Areas discussed:** Untouched-default rendering, What the count counts, How far "upcoming" reaches, Member axis on One-Shot, Who can see it, Grid shape on mobile, Entry point and navigation, Read-only vs clickable cells

All eight offered gray areas were selected.

---

## Untouched-default rendering (EVTVIEW-02)

**Q1 — On a Campaign board an untouched row *stores* Yes. What should its cell actually say?**

| Option | Description | Selected |
|--------|-------------|----------|
| Neutral — no vote claimed | A dash / hollow circle that states nothing. Kills the confusion outright; loses the fact that they will still be counted as available. | |
| Yes chip, muted | Same Yes as a real answer but hollow/faded, solid for a confirmed one. Keeps "will be counted" visible while marking it unconfirmed; a difference of degree, easy to miss on a dense grid. | ✓ |
| Yes chip + "?" marker | Yes chip with a badge or dotted border layered on. Says both things explicitly; busiest cell of the three. | |

**Q2 — Should the muted state carry a second, non-colour signal as well?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — add a shape/text cue | Fill weight plus something surviving greyscale: dotted border, italic label, small icon. | ✓ |
| Fill weight alone is enough | Cleanest cell; accepts a colour-blind viewer reading muted Yes as confirmed. | |
| Plus a legend on the page | Fill weight alone in the cell, with a page key. Only helps the reader who looks at it. | |

**Q3 — Campaign untouched (stores Yes) vs One-Shot no-row (stores nothing): same or different?**

| Option | Description | Selected |
|--------|-------------|----------|
| Different | Muted Yes for the Campaign default; genuinely empty for a One-Shot member with no row. Honest to what each means. | ✓ |
| Same neutral treatment | One "hasn't answered" look for both. Simpler; flattens a real difference. | |
| You decide | Leave to the planner once the member axis is settled. | |

**Notes:** Q1's choice was made on the grounds that the stored Yes is not a lie — it is what will happen unless someone acts — which is what makes Q2's second signal mandatory rather than cosmetic. Q3 raises the cell vocabulary to four states.

---

## What the count counts (EVTVIEW-03)

**Q1 — Should an untouched Campaign default count toward the event's availability number?**

| Option | Description | Selected |
|--------|-------------|----------|
| No — confirmed answers only | Counts only `HasAnswered` rows. A fresh Campaign event reads 0, which is true. A settled board that never clicks reads 0 forever. | |
| Yes — count the stored value | Reflects what happens if nobody acts. Reintroduces the confusion EVTVIEW-02 exists to kill. | |
| Both, shown together | Stored total plus how much of it a person vouched for. Two numbers on the densest part of the page. | ✓ |

**Q2 — Where does Maybe sit in that count?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes only | Cleanest reading of "who is available"; 2 Yes + 4 Maybe looks dead when it may be the best date on the board. | |
| Yes and Maybe shown separately | Nothing hidden, nothing inflated; pushes the row to three or four figures. | ✓ |
| Yes + Maybe as one number | One figure; collapses the distinction the three-value vote exists to record. | |

**Q3 — Which figure is the headline?**

| Option | Description | Selected |
|--------|-------------|----------|
| Confirmed yes | Most conservative; a healthy Campaign board that never clicks looks bad. | |
| Total yes (incl. defaults) | Reads as "who's expected", which is what a DM planning a session wants. The big number is the one that can lie. | ✓ |
| Confirmed / total pair | Neither figure stands alone; a fraction takes a beat longer to read. | |

**Notes:** Q3's cost is explicitly mitigated by Q1 — the headline never appears without the confirmed portion beside it.

---

## How far "upcoming" reaches

**Q1 — What bounds the set of events on the page?**

| Option | Description | Selected |
|--------|-------------|----------|
| A fixed count — next N events | Predictable width regardless of how many series a board runs; N means different amounts of real time per board. | ✓ |
| A rolling date window | Consistent stretch of calendar everywhere; unbounded width — three series is three times the columns. | |
| Everything in the future | Nothing hidden; 40+ columns on a two-series Campaign board, worst case set by data you don't control. | |

**Q2 — Can a DM reach past the first N?**

| Option | Description | Selected |
|--------|-------------|----------|
| N is the page | Simplest; no way to plan a session past the window from this page. | |
| Paging / "show more" | Nothing unreachable; a second interaction and a second query shape on the page flagged for N+1 risk. | ✓ |
| Adjustable window | Adapts per board; asks the reader an implementation question. | |

**Q3 — Does an event dated today stay on the page all day, even after it has started?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — date-only, all day | Matches Phase 75 D-17; `DateOnly` was chosen in Phase 74 D-01 to make this boundary bug impossible. | ✓ |
| Drop it once StartTime passes | Reads as "what's still ahead"; reintroduces the time comparison, and `StartTime` is nullable so all-day events need a second rule. | |
| You decide | Leave the boundary to the planner. | |

**Notes:** The value of N was left open deliberately.

---

## Member axis on One-Shot

**Q1 — On a One-Shot board, who gets a line in the grid?**

| Option | Description | Selected |
|--------|-------------|----------|
| Every board member | Makes "four people never looked" visible; needs a membership query and an empty-state design. | |
| Only members with a row | Follows Phase 75 D-03 exactly; no membership query; inherits D-03's accepted blind spot across the whole page. | ✓ |
| Every member, both board types | One rule, no board-type branch; runs the membership query on Campaign where it can only confirm the rows. | |

**Q2 — What does an empty cell read as (a member on the axis with no row for one event)?**

| Option | Description | Selected |
|--------|-------------|----------|
| Genuinely blank | Honest; a sparse grid can look broken. | |
| Explicit "not answered" mark | More legible in a sparse grid; one more mark to tell apart from the muted Yes. | |
| You decide | Same visual family as the muted-Yes decision; best judged with all states side by side. | ✓ |

---

## Who can see it

**Q1 — Who can reach the availability overview?**

| Option | Description | Selected |
|--------|-------------|----------|
| All board members | Follows Phase 75 D-02; no new authorization surface. Aggregating makes patterns legible the per-event views didn't. | ✓ |
| DM and Admin only | Matches the goal's framing and the DM-gated `Quest/Manage` precedent; makes the same fact public per-event and gated in aggregate. | |
| All members, DM sees more | `IsDmTierAsync` already exists; two renderings of one page to build and test. | |

**Notes:** This settles the open question the ROADMAP flags for Phase 77.

---

## Grid shape on mobile

**Q1 — What does the page become on a phone?**

| Option | Description | Selected |
|--------|-------------|----------|
| Per-event cards | Native to the phone, reuses the card idiom; a different information shape from desktop, weakening cross-event comparison. | ✓ |
| Same grid, horizontal scroll | One shape everywhere, no `.Mobile.cshtml`; awkward on touch, pinned first column is real CSS work. | |
| Transpose on mobile | Keeps a true grid on both; axes swap between devices, and fails on a large board. | |

**Q2 — How much does a mobile card show by default?**

| Option | Description | Selected |
|--------|-------------|----------|
| Counts prominent, names collapsed | Scrolling is a scan of counts, keeping EVTVIEW-03 alive on mobile; the grid is a tap away. | ✓ |
| Counts and full member list | Nothing hidden; card as tall as the board is large, counts stop being glanceable. | |
| Counts only, names on the event page | No duplicated roster markup; mobile shows availability per player nowhere on this page. | |

---

## Entry point and navigation

**Q1 — How does someone reach the overview?**

| Option | Description | Selected |
|--------|-------------|----------|
| Own nav entry beside Calendar | Discoverable; two layouts to change, and Phase 76 plan `76-14` just fought a regression in this block. | |
| Link from the Calendar page | No nav change; nobody finds it unless already on the calendar. | |
| Both | Maximum discoverability; two entry points to keep working. | |
| *(free text)* | **"do both, but make the current calendar button a dropdown with both calendar and this new page in it"** | ✓ |

**Q2 — How should the mobile offcanvas nav handle it?**

| Option | Description | Selected |
|--------|-------------|----------|
| Two flat entries on mobile | Offcanvas is already a vertical list; no new pattern in a layout that has none. The two layouts structure the choice differently. | ✓ |
| Dropdown on both | Consistent mental model; first-of-its-kind interaction in that layout, inside an offcanvas with its own dismiss behaviour. | |
| You decide | Leave the mobile treatment to the planner. | |

**Notes:** Two facts were verified during this area and fed back before Q2 — `LayoutNavigationTests` asserts on the string `"Calendar"` rather than markup structure, so restructuring the entry keeps all four existing cases green; and `_Layout.Mobile.cshtml` contains zero `dropdown` occurrences, which is what made the flat-list answer the cheap one.

---

## Read-only vs clickable cells

**Q1 — Can a member change their own answer from the overview?**

| Option | Description | Selected |
|--------|-------------|----------|
| Read-only | Keeps Phase 75 D-01's single availability surface true; a grid of your own answers you cannot act on. | |
| Your own row is clickable | Reuses the shipped, ownership-checked `SetAvailability`; makes D-01 false and puts every cell in a write-reflecting state. | |
| You decide | Read-only is the literal reading; the write can be added later. | |
| *(free text)* | **"read-only ish? make the rows clickable which redirects to the details page of an event. Then the user can edit their vote (which already exists)"** | ✓ |

**Q2 — On desktop, which way round is the grid?**

| Option | Description | Selected |
|--------|-------------|----------|
| Events as rows, members as columns | Row is a natural click target; matches the mobile cards. Width scales with board size. | ✓ |
| Members as rows, events as columns | Literal reading of "events against players"; weaker click target, inverts relative to mobile. | |
| You decide | Leave to the planner. | |

**Notes:** Q1's answer resolves the read-only-vs-dead-end tension without opening a write path — the page stays a read surface and delegates the action to the surface that already owns it.

---

## Cross-board overview (raised at wrap-up)

The operator asked whether the page could also show all events linked to the logged-in user across every board, and asked for advice on whether it belonged in the Calendar dropdown.

Advice given: the idea is sound and the app already assumes multi-board membership, but it conflicts with EVTVIEW-04 and success criterion 4 — a cross-board mode would make one two-group test both prove and disprove the same property depending on a toggle. It also cannot sit behind the Calendar nav gate, which is keyed on a resolved `activeBoardType` that a cross-board page does not have; it belongs beside **Switch Group**. Two safe cross-group mechanisms already exist and both follow the same rule (bypass the ambient filter only while supplying the group explicitly): the Phase 76 job's per-group `SetGroupId()` iteration, and `GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`'s group-pinned predicate.

| Option | Description | Selected |
|--------|-------------|----------|
| Defer to its own phase | Capture in CONTEXT.md deferred ideas with the analysis. | |
| Defer, and add it to the roadmap now | Same, plus a real numbered phase in ROADMAP.md. | ✓ |
| Something else | Include in 77 anyway with EVTVIEW-04 reworded. | |

**Outcome:** added as **Phase 82: Personal Cross-Board Event Agenda**, with scope notes, two named risks, and its own open discuss questions.

---

## Claude's Discretion

- The empty-cell treatment for a member with no row on one event (Member axis Q2 — "You decide").
- The value of N, and whether it is a code constant or configurable.
- The exact non-colour cue, the exact count format, and the page legend copy.
- Page name, icon, route, and controller placement.
- Empty-state copy, column ordering, viewer-column highlighting.
- Paging mechanism (page index vs grow-the-set) and whether hidden-event counts are stated.
- Whether excluded cancelled occurrences are acknowledged on the page.
- Naming, file placement, AutoMapper entries, and test structure beyond the mandated isolation test.

## Deferred Ideas

- **Personal cross-board event agenda** — promoted to Phase 82 in ROADMAP.md (2026-08-29).
- **Writable cells** — declined; the click-through to Details gives the action path without a second write surface.
- **An on-page adjustable window control** — declined; asks the reader an implementation question.
- **DM-only extras on a shared page** — declined; two renderings for no requirement.
- **A per-board denominator on the count** — undecided, folded into format discretion; note that One-Shot has no meaningful denominator under the chosen member axis.
