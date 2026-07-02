---
phase: 33
slug: session-persistence-persist-activegroupid-across-app-restart
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-01
---

# Phase 33 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2), `Microsoft.AspNetCore.Mvc.Testing` 10.0.9, `FluentAssertions` 8.10.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Admin\|FullyQualifiedName~Session"` |
| **Full suite command** | `dotnet test` (runs `QuestBoard.UnitTests` + `QuestBoard.IntegrationTests`) |
| **Estimated runtime** | ~60 seconds (full suite, 191+ existing tests) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Admin"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 33-01-xx | 01 | 1 | SESSION-01 | — | DI resolves `SqlServerCache` as `IDistributedCache` outside Testing env | unit/integration | New test — DI-resolution check via non-Testing `WebApplicationFactory` variant | ❌ W0 | ⬜ pending |
| 33-01-xx | 01 | 1 | SESSION-02 | — | `AspNetSessionState` table created by migration with correct schema | manual/script | `dotnet ef migrations script --project QuestBoard.Repository` + inspect generated SQL | ❌ W0 | ⬜ pending |
| 33-02-xx | 02 | 2 | EMAIL-RATE-01 | T-33-01 | `SendConfirmationEmail` rejects 4th request/hour per target userId with 429 | integration | New test modeled on `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429` | ❌ W0 | ⬜ pending |
| 33-02-xx | 02 | 2 | EMAIL-RATE-02 | T-33-02 | Rate limit scoped per-target-userId (independent budgets) | integration | New test — interleave resends for userId=A and userId=B | ❌ W0 | ⬜ pending |
| 33-02-xx | 02 | 2 | EMAIL-RATE-03 | T-33-01 | `EditUser` POST email-change path rate-limited the same way | integration | New test modeled on EMAIL-RATE-01, via `EditUser` | ❌ W0 | ⬜ pending |
| 33-02-xx | 02 | 2 | EMAIL-RATE-04 | — | `CreateUser` POST (welcome email) NOT rate-limited (D-08 exemption) | integration | Existing tests pass unmodified; optional explicit 4x-succeeds assertion | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Manual/checkpoint verification against real local SQL Server for the `AspNetSessionState` migration DDL — the existing integration suite uses EF Core `InMemoryDatabase`, which silently no-ops `migrationBuilder.Sql(...)` and cannot exercise this migration.
- [ ] New lightweight test/verification for session-survives-restart behavior — recommend asserting DI resolves `SqlServerCache` as `IDistributedCache` in non-Testing config (via a `WebApplicationFactory` variant that does not override to Testing environment), rather than a literal process-restart test.
- [ ] New integration test file/methods for the four EMAIL-RATE-* behaviors — no existing test exercises admin email rate limiting today.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `AspNetSessionState` table schema (columns, PK, index, collation) is correctly created against real SQL Server | SESSION-02 | Integration suite uses EF Core InMemory, which does not execute raw-SQL migrations at all | Run app locally (migration auto-applies via `context.Database.Migrate()`), then inspect `dbo.AspNetSessionState` in SQL Server: verify `Id NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL` PK, `Value VARBINARY(MAX) NOT NULL`, `ExpiresAtTime DATETIMEOFFSET(7) NOT NULL` (indexed), `SlidingExpirationInSeconds BIGINT NULL`, `AbsoluteExpiration DATETIMEOFFSET(7) NULL` |
| `ActiveGroupId` survives an actual app restart (not just a DI-resolution check) | SESSION-01 | True restart-survival requires stopping/restarting the running app process mid-session, which automated tests cannot easily simulate | Log in, select a group, restart the app/container, reload the page — confirm the previously selected group is still active without re-picking |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
