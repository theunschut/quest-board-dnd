# Phase 28: Tenant Isolation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-30
**Phase:** 28-tenant-isolation
**Areas discussed:** Hangfire bypass strategy, Test stub design, Filter null semantics

---

## Hangfire bypass strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Null-bypass — service returns null outside HTTP context | ActiveGroupContextService gracefully returns null when no HTTP context; filter predicate `null \|\| groupId == x` handles Hangfire naturally; zero job changes | |
| Explicit groupId on job enqueue | Add groupId parameter to QuestFinalizedEmailJob and SessionReminderJob; caller passes it at enqueue time; concrete service exposes SetGroupId method | ✓ |

**User's choice:** Explicit groupId — "I don't really like null bypasses."
**Notes:** Also discussed secondary method (no param) but user noted that adds code duplication. Explicit parameter preferred for clarity.

---

## CRON sweep cross-group access (follow-up to Hangfire)

| Option | Description | Selected |
|--------|-------------|----------|
| IgnoreQueryFilters() on dedicated repository method | GetQuestsForTomorrowAllGroupsAsync() uses IgnoreQueryFilters(); method name makes cross-group intent explicit | ✓ |
| System service with null context | Register a SystemGroupContext that always returns null; CRON sweep resolves it directly | |

**User's choice:** IgnoreQueryFilters on a dedicated method (accepted recommended option).

---

## Test stub design

| Option | Description | Selected |
|--------|-------------|----------|
| Mutable singleton on the factory | WebApplicationFactoryBase gets public TestGroupContext property; default GroupId=1; tests set factory.GroupContext.ActiveGroupId = N as needed | ✓ |
| Constructor parameter on factory | WebApplicationFactoryBase(int activeGroupId = 1); new factory per group | |
| Hardcoded singleton GroupId=1 always | services.AddSingleton(new FixedGroupContext(1)); simplest but not overridable | |

**User's choice:** Mutable singleton (accepted recommended option).

---

## Filter null semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Null = see all groups | Predicate: `context.ActiveGroupId == null \|\| e.GroupId == context.ActiveGroupId`; null short-circuits to true | ✓ (Phase 28 only) |
| Null = see nothing (strict) | Predicate: `e.GroupId == context.ActiveGroupId`; NULL == int always false in SQL | |
| Null = see nothing except SuperAdmin (user suggestion) | Filter checks SuperAdmin role in addition to group | Deferred to Phase 29 |

**User's choice:** Null = see all for Phase 28 — user initially asked about SuperAdmin exception; Claude advised this is not implementable in Phase 28 (SuperAdmin role doesn't exist until Phase 29, and no group picker exists until Phase 30 so all users would get null = no data). User accepted the Phase 28→29→30 progression.

**Notes:** User asked "will Phase 29 know about this?" — confirmed: explicit forward note in CONTEXT.md deferred section specifying exactly what Phase 29 must do.

---

## Claude's Discretion

- Session key naming convention
- DI registration lifetime for ActiveGroupContextService (Scoped)
- MutableGroupContext placement (test project)

## Deferred Ideas

- Null = see nothing except SuperAdmin — deferred to Phase 29 (requires SuperAdmin role + Phase 30 group picker to be safe)
