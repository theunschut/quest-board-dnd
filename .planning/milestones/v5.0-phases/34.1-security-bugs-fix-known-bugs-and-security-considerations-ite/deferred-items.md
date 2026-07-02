# Deferred Items — Phase 34.1

Items discovered during execution that are out of scope for the current plan(s) and were not fixed.

## Flaky integration test: SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess

- **Discovered during:** 34.1-01, Task 2 verification (full `dotnet test QuestBoard.IntegrationTests` run)
- **Symptom:** Fails with `HttpStatusCode.TooManyRequests (429)` instead of the expected redirect when run as part of the full integration test suite. Passes when run in isolation (`--filter FullyQualifiedName~SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`).
- **Root cause (not fixed):** `PartitionedRateLimiter<int> emailResendLimiter` (Phase 33) is registered as a process-wide singleton via `AddSingleton`. Its rate-limit state (3 requests / 1 hour per target userId) persists across test cases within the same test-run process, so an earlier test in the suite that exercises the same code path against an overlapping target user id can exhaust the budget before this test runs.
- **Scope note:** This plan's changes (`ResendStatsClient` extraction, Program.cs email-config startup guard, EmailService/ResendStatsClient comments) do not touch `emailResendLimiter`, `SendConfirmationEmail`, or any Phase 33 rate-limiting code. Confirmed pre-existing by running the failing test in isolation (passes) vs. full suite (fails) — behavior is unrelated to this plan's diff.
- **Suggested fix (future plan):** Either reset/scope the rate limiter per test run in the integration test factory (e.g. re-register a fresh `PartitionedRateLimiter<int>` per `WebApplicationFactory` instance instead of a global singleton), or have tests use distinct target user ids per rate-limit-sensitive test to avoid budget collisions.
