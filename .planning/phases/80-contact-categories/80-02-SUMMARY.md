---
phase: 80-contact-categories
plan: 02
subsystem: database
tags: [efcore, automapper, sqlserver, migrations]

requires: []
provides:
  - "ContactCategoryEntity/ContactCategory schema and domain model"
  - "Nullable Contact.CategoryId/Contact.Category reference with SetNull orphan behavior"
  - "Fail-closed board-scoped query filter on ContactCategoryEntity"
  - "AddContactCategories EF Core migration"
  - "TestDataHelper.CreateTestContactCategoryAsync seeding helper"
affects: [80-contact-categories, contact-tags-and-filtering]

tech-stack:
  added: []
  patterns:
    - "Group-scoped entity: HasQueryFilter dereferencing IActiveGroupContext.ActiveGroupId inline, NoAction FK to GroupEntity"
    - "Orphan-not-cascade nullable reference: SetNull FK + IsRequired(false) on the optional side"

key-files:
  created:
    - QuestBoard.Repository/Entities/ContactCategoryEntity.cs
    - QuestBoard.Domain/Models/ContactCategory.cs
    - QuestBoard.Repository/Migrations/20260830094351_AddContactCategories.cs
  modified:
    - QuestBoard.Repository/Entities/ContactEntity.cs
    - QuestBoard.Domain/Models/Contact.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "ContactCategoryEntity.Name capped at 60 chars (not ContactEntity.Name's 100) because it renders as a heading on narrow phones"
  - "ContactCategoryEntity -> GroupEntity FK uses NoAction, matching every other board-scoped entity, to avoid SQL Server's multiple-cascade-paths error"
  - "Contact -> ContactCategory FK uses SetNull with IsRequired(false) so deleting a category orphans its contacts rather than deleting them"
  - "Case-insensitive per-board name uniqueness comes from a plain unique index on (GroupId, Name); no collation override needed since the database's ambient collation is already case-insensitive"

patterns-established:
  - "Reorder-ready SortOrder convention: dense positions, ordered by SortOrder then Id, new rows append at max+1, reordering swaps two positions"

requirements-completed: [CONTACTCAT-01, CONTACTCAT-02, CONTACTCAT-03, CONTACTCAT-05, CONTACTCAT-07, CONTACTCAT-13, CONTACTCAT-14]

coverage:
  - id: D1
    description: "ContactCategoryEntity, ContactCategory domain model, nullable Contact category reference, and both AutoMapper entries exist and compile"
    requirement: "CONTACTCAT-01"
    verification:
      - kind: unit
        ref: "dotnet build (whole solution) — 0 errors, no new warnings"
        status: pass
    human_judgment: false
  - id: D2
    description: "QuestBoardContext declares the ContactCategories DbSet, a fail-closed board-scoped query filter dereferencing the active-group service inline, the NoAction board FK, the SetNull contact-to-category FK, and the case-insensitive unique (GroupId, Name) index"
    requirement: "CONTACTCAT-02"
    verification:
      - kind: unit
        ref: "grep verification of QuestBoardContext.cs filter/FK/index blocks (see plan 80-02 Task 2 acceptance criteria) — all pass"
        status: pass
    human_judgment: false
  - id: D3
    description: "AddContactCategories migration creates the ContactCategories table, the unique index, SetNull on the contacts-to-categories FK, and no Cascade on either FK"
    requirement: "CONTACTCAT-03"
    verification:
      - kind: unit
        ref: "grep of QuestBoard.Repository/Migrations/20260830094351_AddContactCategories.cs — table/index/SetNull confirmed, Cascade count 0"
        status: pass
    human_judgment: true
    rationale: "The categories-to-groups FK's NoAction behavior is verified indirectly (absence of Cascade, matching the established Contacts-to-Groups migration precedent) rather than by a literal 'ReferentialAction.NoAction' string match — see Deviations. A human should confirm this reasoning is acceptable rather than have it silently auto-pass."
  - id: D4
    description: "TestDataHelper.CreateTestContactCategoryAsync exists and the existing Contacts integration suite plus the full unit suite stay green against the new schema"
    requirement: "CONTACTCAT-13"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactsControllerIntegrationTests"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-08-30
status: complete
---

# Phase 80 Plan 02: Contact Category Schema Summary

**ContactCategory entity, nullable Contact-to-category reference, and the AddContactCategories EF Core migration, with a fail-closed board-scoped query filter and an orphan-not-cascade delete behavior.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-08-30T09:12:00Z
- **Completed:** 2026-08-30T09:47:17Z
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments
- `ContactCategoryEntity`/`ContactCategory` added across the Repository and Domain layers, with `ContactEntity.CategoryId`/`.Category` and `Contact.CategoryId`/`.CategoryName`/`.CategorySortOrder` wired through AutoMapper
- `QuestBoardContext` configured with the `ContactCategories` DbSet, a fail-closed board-scoped query filter, a `NoAction` board foreign key, a `SetNull` contact-to-category foreign key, and a case-insensitive unique `(GroupId, Name)` index
- `AddContactCategories` migration generated and its referential actions confirmed against the intended configuration
- `TestDataHelper.CreateTestContactCategoryAsync` added; the existing Contacts integration suite (32 tests) and the full unit suite (422 tests) stay green against the new schema

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the ContactCategory entity, the contact category reference, and the entity-to-domain mapping** - `a1fdd697` (feat)
2. **Task 2: Configure the schema in QuestBoardContext and generate the AddContactCategories migration** - `1e0e7fec` (feat)
3. **Task 3: Add the CreateTestContactCategoryAsync seeding helper** - `0703d047` (test)

_Note: Task 1 was tagged `tdd="true"` in the plan, but its `<behavior>` block describes AutoMapper projection behavior with no independent test file specified anywhere in this plan's task list — the verification step for Task 1 is `dotnet build`, matching every other task's structural-only acceptance criteria. No RED/GREEN/REFACTOR cycle was applicable; treated as a single `feat` commit._

## Files Created/Modified
- `QuestBoard.Repository/Entities/ContactCategoryEntity.cs` - new entity: Id, Name (60-char cap), SortOrder, GroupId/Group
- `QuestBoard.Domain/Models/ContactCategory.cs` - new domain model mirroring the entity
- `QuestBoard.Repository/Entities/ContactEntity.cs` - added nullable `CategoryId`/`Category` navigation
- `QuestBoard.Domain/Models/Contact.cs` - added `CategoryId`, display-only `CategoryName`/`CategorySortOrder`
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - `ContactCategory<->ContactCategoryEntity` map; `CategoryName`/`CategorySortOrder` projection on `ContactEntity -> Contact`
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - `ContactCategories` DbSet, board FK, category FK, unique index, query filter
- `QuestBoard.Repository/Migrations/20260830094351_AddContactCategories.cs` (+ `.Designer.cs`, snapshot) - the generated migration
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` - `CreateTestContactCategoryAsync` seeding helper

## Decisions Made
- Followed the plan's copy-from-`ContactEntity`/`Event → Series` shapes exactly; no independent design decisions were made beyond what RESEARCH.md/PATTERNS.md already specified.
- `ClearDatabaseAsync` was left unmodified: it drops and recreates the whole database (`EnsureDeletedAsync`/`EnsureCreatedAsync`) rather than enumerating individual DbSets, so the plan's conditional "if `ClearDatabaseAsync` enumerates sets explicitly" branch did not apply.

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing functionality, or blocking issues were encountered; every file matched its analog closely enough that no Rule 1/2/3 fixes were needed.

### Acceptance-Criteria Note (not a code deviation)

**1. [Verification method substitution] Task 2's literal `ReferentialAction.NoAction` grep does not match the generated migration text**
- **Found during:** Task 2 verification
- **Issue:** The plan's acceptance criteria and `<verify>` command both grep for the literal string `ReferentialAction.NoAction` in the generated migration file, expecting it to appear for the categories-to-groups foreign key. EF Core's migration generator omits the `onDelete:` parameter entirely for a `NoAction`/`Restrict` foreign key created inline within a `CreateTable` call (as opposed to a standalone `AddForeignKey` call against an already-existing table, which does render it explicitly) — because SQL Server's own default when `ON DELETE` is unspecified is already `NO ACTION`. This is not a configuration bug: the codebase's own pre-existing `Contacts -> Groups` foreign key (`FK_Contacts_Groups_GroupId`, in `20260706193921_AddContactsFeature.cs`) is the exact same shape and also omits the literal text, and that migration has shipped and worked correctly for months.
- **Verification performed instead:** Confirmed (a) `grep -c 'ReferentialAction.Cascade'` on the migration outputs `0` (the one thing the plan's own `<action>` text says would actually indicate a wrong configuration — "If the generated file carries `Cascade` on either foreign key, the configuration is wrong"), (b) the categories-to-groups `table.ForeignKey(...)` block has no `onDelete` parameter at all, matching the established `Contacts -> Groups` precedent byte-for-byte in shape, and (c) `ReferentialAction.SetNull` appears explicitly for the contacts-to-categories FK (a standalone `AddForeignKey` call against the pre-existing `Contacts` table, which does render the literal text) — all as intended.
- **Files affected:** None modified; this only affects how Task 2's acceptance criteria should be read. No hand-edit was made to the generated migration, per the plan's own explicit prohibition.
- **Committed in:** `1e0e7fec` (Task 2 commit) — migration generated as-is via `dotnet ef migrations add`, unedited.

---

**Total deviations:** 0 auto-fixed; 1 acceptance-criteria reading note (verification-method substitution, not a functional gap)
**Impact on plan:** None on functionality — the schema's delete-behavior guarantees (T-80-02-02, T-80-02-03) are fully satisfied and verified by the substitute checks above. Flagged `coverage.D3.human_judgment: true` above so a human confirms this reasoning is acceptable.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `ContactCategoryEntity`, `ContactCategory`, the nullable `Contact` reference, and `CreateTestContactCategoryAsync` are all available for the repository/service/controller/view work in later plans of this phase.
- The schema change has been proven not to regress the existing Contacts integration suite or the unit suite.
- No blockers.

---
*Phase: 80-contact-categories*
*Completed: 2026-08-30*

## Self-Check: PASSED

All claimed created files verified present on disk (`ContactCategoryEntity.cs`, `ContactCategory.cs`, `20260830094351_AddContactCategories.cs`, this SUMMARY). All four commit hashes (`a1fdd697`, `1e0e7fec`, `0703d047`, `7260b572`) verified present in `git log --oneline`.
