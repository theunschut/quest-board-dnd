# Phase 33: Session Persistence + Admin Email Rate Limiting - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-01
**Phase:** 33-session-persistence-persist-activegroupid-across-app-restart
**Areas discussed:** SQL cache table setup, Rate-limit partition key, Rate-limit window + quota, EditUser email-change coverage

---

## SQL Cache Table Setup

### Question 1 — Table provisioning approach

| Option | Description | Selected |
|--------|-------------|----------|
| EF migration with raw SQL | `migrationBuilder.Sql(CREATE TABLE IF NOT EXISTS ...)`. Auto-applied on startup, stays in migration audit trail. | ✓ |
| Startup code | App checks at launch and creates table if missing. Bypasses migration audit trail. | |
| Let Claude decide | Claude picks EF migration approach. | |

**User's choice:** EF migration with raw SQL
**Notes:** Consistent with "no manual deploy steps" constraint and the project's existing pattern of auto-applied migrations.

### Question 2 — Expired entry cleanup

| Option | Description | Selected |
|--------|-------------|----------|
| Built-in cleanup interval | `SqlServerCacheOptions.ExpiredItemsDeletionInterval` (default: 30 min). Cache provider handles cleanup automatically. | ✓ |
| Hangfire CRON | Periodic job deleting rows WHERE ExpiresAtTime < GETUTCDATE(). More setup for something .NET already handles. | |

**User's choice:** Built-in cleanup interval
**Notes:** No Hangfire job needed for session cleanup.

---

## Rate-limit Partition Key

| Option | Description | Selected |
|--------|-------------|----------|
| Per target user ID | Limits resends per recipient (e.g. max 3 to same person per hour). Partition key: `userId`. | ✓ |
| Per admin IP | Consistent with ForgotPassword pattern (`RemoteIpAddress`). Doesn't distinguish which recipient is spammed. | |
| Per admin user ID | Requires resolving logged-in admin's user ID from auth context. | |

**User's choice:** Per target user ID
**Notes:** ROADMAP.md explicitly flagged this as an open question. User chose inbox protection over admin-session bucketing.

---

## Rate-limit Window + Quota

| Option | Description | Selected |
|--------|-------------|----------|
| 3 per hour per target user | Generous for legitimate use (spam folder, retry). Consistent numerically with ForgotPassword (3 requests), different window (1hr vs 15min). | ✓ |
| 1 per hour per target user | Stricter. May frustrate retries within same session. | |
| 5 per day per target user | Daily window. Harder to reason about from UX ("when does slot reset?"). | |

**User's choice:** 3 per hour per target user
**Notes:** ROADMAP.md explicitly flagged "exact relay send-limit numbers" as open. User chose the middle-ground option.

---

## EditUser Email-Change Coverage

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — same policy, per target user ID | EditUser POST triggers ChangeEmailConfirmationJob on email change. Apply 3/hr-per-target limit. | ✓ |
| No — exempt it | Email-change is tied to a real data change (not a pure resend). Low repeat-click risk. | |

**User's choice:** Yes — same policy, per target user ID
**Notes:** Even though it's a form save rather than a dedicated resend button, the user decided consistency (same policy) outweighs the marginal friction on legitimate edits.

---

## Claude's Discretion

- Whether to apply rate limit to `EditUser` via `[EnableRateLimiting]` attribute (applies to full action) or programmatically via `IPartitionedRateLimiter` injection (applies only to email-dispatch sub-path). Planner decides based on complexity vs. accuracy trade-off.
- `SqlServerCacheOptions.ExpiredItemsDeletionInterval` exact value — framework default (30 min) is fine.
- Migration name and ordering.

## Deferred Ideas

None introduced during discussion — all areas stayed within phase scope. Existing deferred items from PROJECT.md (password-changed notification email, digest batching, per-group email config) are not part of Phase 33.
