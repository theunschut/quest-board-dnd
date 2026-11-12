# Phase 102: Board deletion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-11
**Phase:** 102-board-deletion
**Areas discussed:** Player-owned rows, Cross-board links, Confirmation page, Users board column

---

## Player-owned rows

| Option | Description | Selected |
|--------|-------------|----------|
| Characters: Delete with board | Character, classes and images go; owners are ex-members already | ✓ |
| Characters: Block until moved/deleted | Refuse while characters remain | |
| Characters: Keep, make board nullable | Schema change plus new UI | |
| Purchases: Delete with board | Transactions on the board's shop items go | ✓ |
| Purchases: Keep as orphan history | Nullable ShopItemId plus snapshot | |
| Exceptions: None | All board content goes whoever created it | ✓ |
| Exceptions: Let me name some | | |
| Images: You decide | Remove whatever would be orphaned | ✓ |
| Images: Rows only | | |

**User's choice:** Delete everything board-scoped, including characters and purchase history; no exceptions.

---

## Cross-board links

| Option | Description | Selected |
|--------|-------------|----------|
| Linked set: Leave as is | SetNull clears the link; the set stays for the admin to tidy | ✓ |
| Linked set: Delete if ≤1 board left | Automatic cleanup | |
| Reach-ins: Unlink them | Clear OriginalQuestId / CharacterId on other boards' rows | ✓ |
| Reach-ins: Refuse the delete | Admin fixes them first | |
| Session: You decide | Verify the existing stale-board recovery | ✓ |
| Session: Must test explicitly | | |

---

## Confirmation page

| Option | Description | Selected |
|--------|-------------|----------|
| Preview: Counts per type | | |
| Preview: Counts + names | | ✓ |
| Confirm: Type board name | Button disabled until the name matches | ✓ |
| Confirm: Plain button | | |
| Recheck: Refuse if members, else go | New content is deleted too | ✓ |
| Recheck: Refuse on any change | Phase 92 style | |
| Name list: Collapsed per type | `<details>` per type; counts only for nameless rows | ✓ |
| Name list: All expanded | | |
| Name list: Cap per type | | |
| Mobile: Follow Platform convention | Keep the `.Mobile` twin | ✓ |
| Mobile: Desktop only | | |

---

## Users board column

| Option | Description | Selected |
|--------|-------------|----------|
| Count, names on hover/expand | | ✓ |
| Count only | | |
| Board names inline | | |
| Sort/filter: No, just display | | ✓ |
| Sort/filter: Sortable | | |

---

## Claude's Discretion

- Image file cleanup (depends on where images are stored)
- Stale active-board session recovery (verify, fix only if broken)
- Delete order, service placement, and the landing page and toast after deleting

## Deferred Ideas

- Sorting or filtering Platform Users by board count
- Auto-cleanup of linked board sets left with one board or none
