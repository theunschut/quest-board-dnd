---
phase: 96-account-and-pickers-ui-conversion
plan: 11
subsystem: ui
tags: [set-password, mailed-link, smtp-sink, verification, tracer]
requires:
  - phase: 96-08
    provides: SetPassword merged into one view for both layouts
  - phase: 96-10
    provides: the full suite green and the area closure guard
provides:
  - the merged SetPassword view proven with a real reset token under the desktop and the phone User-Agent
  - a local SMTP catch-all (scratch tooling) so a mail check never leaves the machine
affects: [96-12]
tech-stack:
  added: []
  patterns: [local SMTP sink for any check that needs a real mailed token]
key-files:
  created: []
  modified: []
key-decisions:
  - "Ran the check against a local SMTP sink instead of the operator's mailbox; no mail left the machine (operator-approved)"
  - "Updated the qb-audit-player3 entry in credentials.json instead of adding a setpw role, and re-issued its storage state (coordinator instruction)"
requirements-completed: [D-09, X-3, DoC-7]
duration: ~30 min
completed: 2026-11-12
status: complete
actuals:
  tokens: 3300
  tasks: 3
  commits: 1
---

# Phase 96 Plan 11: Mailed SetPassword link check Summary

The merged SetPassword view works end to end with a freshly mailed token: it renders in both layouts, a new password set under the phone User-Agent is accepted, the page redirects to Log in with the success message, the new password signs in, and the same link is refused afterwards. No repository file was changed by this plan.

## Result (`after/96-11/report.json`)

| Flag | Result |
|------|--------|
| rendered.desktop | true (HTTP 200, the form with both password fields, one h1, no horizontal overflow) |
| rendered.mobile | true (HTTP 200, `mobile-layout` shell, one h1, no overflow) |
| submitted | true (phone UA: redirected to Log in showing `Your password has been set. Please log in.`) |
| signedIn | true (the new password leaves the Log in page) |
| reuseRefused | true (second submit of the same link refused; the second password does not sign in) |

The rendered-page probes on both layouts found 0 contrast failures, 0 interactive targets under 44x44 on the phone, 0 missing focus rings and no overflow. Screenshots: `after/96-11/{desktop,mobile}/setpassword-mailed.png`, `mobile/setpassword-submitted-login.png`, `desktop/setpassword-reused.png`.

Checkpoint rounds: 1 operator decision (the sink), 0 stale links replaced.

## Deviations from Plan

### Operator-approved

**1. [Operator decision] Local SMTP sink replaces the operator mailbox step (Task 2)**
- **Why:** the first email-enabled start (below) showed that a real relay with a real-member database can mail people through any Hangfire or notification path. The operator chose "Local SMTP sink".
- **What was done:** `tools/smtp-sink.py` (plain asyncio sockets, bound to 127.0.0.1:2525, writes each message to `secrets/mail/<n>.eml` mode 600, never relays). A local test message proved it before the app started. The app was restarted with `EmailSettings__SuppressSending=false`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=2525` and `EmailSettings__EnableSsl=false`, and the process environment confirmed all four before any request. `tools/setpassword-request.mjs` requested Forgot Password for the `qb-audit-player3` account; the link was extracted from the caught mail straight into `secrets/setpassword.json` (mode 600) and never printed. The Task 2 check printed `ok`.
- **Caught mail:** exactly one message other than the sink's own self-test, the reset mail (subject `Reset your D&D Quest Board password`). No leftover Hangfire job mail arrived, so there was no count by job type to record.
- **Cleanup:** the app was restarted with no overrides (process environment has no `EmailSettings__` variable; the log shows `EmailSettings:SuppressSending = True`), the sink was stopped (nothing listens on 2525), the caught `.eml` files and the link file were deleted, and `secrets/` is empty.

**2. [Operator instruction] `qb-audit-player3` credentials updated in place**
- The new password replaced that account's entry in `credentials.json` (mode 600, never printed) rather than a new `setpw` role, and its storage state `state/8000/player3.json` was re-issued with `login.mjs`.

### Auto-fixed Issues

**3. [Rule 1 - Bug] Plan's expectation for the reused link was inaccurate**
- **Found during:** Task 3.
- **Issue:** the plan expects the persistent toast `Password reset failed. The link may be expired or invalid. Please request a new one.` on reuse. That message only appears when the token cannot be Base64Url decoded (the controller's catch path). A well-formed but spent token makes `ResetPasswordAsync` return a failed result, and the action re-renders the SetPassword form with the validation summary `Invalid token.` (security stamp changed). The controller and its copy were not changed by this phase, so this is the existing behavior, not a regression.
- **Fix:** the script accepts either refusal form, records which one occurred (`reuseOutcome: invalid-token-summary-on-form`) and additionally proves the second password does not sign in. The backstop truth about the persistent error toast on a malformed token was not exercised by a mailed link; a malformed token is covered by the source path only.
- **Files modified:** none in the repository.

**4. [Rule 1 - Bug] Report redaction swallowed JSON syntax**
- **Found during:** Task 3 verify.
- **Issue:** the whole-text `token=` redaction consumed the closing quote and comma after a redacted URL, so `report.json` was not valid JSON.
- **Fix:** repaired the two affected lines in place; the script now stringifies records that were already redacted individually, and still asserts that no secret value reaches the report.

**5. [Plan variation] The final app restart and the cleanup ran as separate steps** rather than inside the script, so the player3 state could be re-issued and the sink checked before the restart.

### Environment substitutions (operator-approved, see ENVIRONMENT.md)

Port 8000 for 8096; `python3` for `python`; `/proc` and `ss` for process lookup; the relay probe used `bash -c` under `timeout`.

## Incident note

The first email-enabled start (Task 1, before the sink) used the committed SMTP relay (192.168.6.13:25, reachable) with `SuppressSending=false` for about one minute. No HTTP requests were made in that window, the app log holds 0 "sent" lines and 0 "skipping send" lines, and no reset was requested, so no mail is known to have been sent. Hangfire job processing is not logged at that level, and the Hangfire storage lives in the shared database, so a leftover scheduled job cannot be ruled out from the log alone. The coordinator stopped that process; every later email-enabled run used the sink and the sink caught nothing unexpected.

## Known Stubs

None. The repository is unchanged by this plan.

## Threat Flags

None.

## Self-Check: PASSED

- `after/96-11/report.json`, the four screenshots, `tools/setpassword-verify.mjs`, `tools/setpassword-request.mjs` and `tools/smtp-sink.py` exist under `~/qb-ui-review/phase-96/`.
- `secrets/setpassword.json` and `secrets/mail/` are gone; the `token=` grep over the scratch folder prints nothing.
- `git status --porcelain` shows no repository file changed by the plan's work.
