---
status: complete
phase: 33-session-persistence-persist-activegroupid-across-app-restart
source: [33-03-PLAN.md]
started: 2026-07-01
updated: 2026-07-01
---

## Current Test

Completed — both Manual-Only Verifications approved during the 33-03 Task 3 blocking human-verify checkpoint.

## Tests

### 1. SESSION-02 — AspNetSessionState table schema (real SQL Server)
expected: `dbo.AspNetSessionState` exists in local SQL Server with the exact `SqlServerCache` schema (`Id NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL` clustered PK, `Value VARBINARY(MAX) NOT NULL`, `ExpiresAtTime DATETIMEOFFSET(7) NOT NULL` with nonclustered index `Index_ExpiresAtTime`, `SlidingExpirationInSeconds BIGINT NULL`, `AbsoluteExpiration DATETIMEOFFSET(7) NULL`), with no conflicting pre-existing table, and a row appears after logging in and picking a group.
result: approved 2026-07-01 — user confirmed "table is fine and filled with my session" after logging in and selecting a group.

### 2. SESSION-01 — ActiveGroupId survives a real app restart
expected: Log in, select a group, then perform a genuine process restart (not a browser reload) — kill the running process and start a fresh one — then reload in the same browser. The same group should still be active; the user should land on `/quest` rather than being bounced to the group picker.
result: approved 2026-07-01 — user performed a real process kill/restart via the Visual Studio debugger ("I did a process kill with VS and restarted the debugger") and confirmed they were redirected to `/quest` for the same active group instead of the group picker.

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
