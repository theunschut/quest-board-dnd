# Deferred Items — Phase 36

Items discovered during execution that are out of scope for the current task/plan and were
not fixed, per the executor's scope boundary rule.

## From Plan 36-03

- **`AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`
  flakes with 429 TooManyRequests instead of the expected 302.** Reproducible even when running
  only `AdminControllerIntegrationTests` in isolation (order-dependent within the class — the
  3/hour rate-limit bucket for admin email resends is shared across tests in the class and gets
  exhausted before this test runs). Pre-existing, unrelated to `QuestController`/
  `QuestLogController`/`QuestViewModel` — out of scope for 36-03 (campaign quest posting/closing).
  Not fixed per the executor's scope-boundary rule (only auto-fix issues directly caused by the
  current task's changes).
