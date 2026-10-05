---
status: testing
phase: 89-pull-based-release-deployment
source: [89-VERIFICATION.md]
started: 2026-10-05T19:30:00Z
updated: 2026-10-05T19:30:00Z
---

## Current Test

number: 1
name: Sign off the WR-03 decision (main-ancestry check with no usable answer is a quiet retry)
expected: |
  You accept that a transport error or HTTP 403/429/5xx on the compare call is a quiet retry (no mail,
  not remembered), while 404, other 4xx, ahead/diverged and an unreadable 200 stay refusals.
awaiting: user response

## Tests

### 1. WR-03: main-ancestry check with no usable answer is a quiet retry
expected: You accept that a rate-limited or flaky compare call never parks a good release, and that an unreadable 200 body is still a refusal
result: [pending]

### 2. WR-05: backup skipped only on an explicit databaseExists false
expected: You prefer refusing over backing up blind when the status does not say whether the database exists, and accept the reused "database could not be reached" reason
result: [pending]

### 3. WR-06: a failed apply is re-read before it is called rolled back
expected: You accept that an unchanged pending count restarts the previous release, fewer pending continues as an applied install (installed or halted), and an unreadable database after three tries keeps the old "failed, rolled back" behaviour
result: [pending]

### 4. T-89-31: failed manual rollback recorded; manual runs journal
expected: You accept "abandoned" for the release left and "failed" for the target, no new mail reason, and journal lines for manual runs (not duplicated under the poll unit)
result: [pending]

### 5. Ship the hardening to production
expected: Next release cut from PR #158 installs itself on CT 102; after `setup` from that release, /usr/local/sbin/questboard-deploy and /usr/local/lib/questboard-deploy/*.sh match the new zip; the next poll reports "nothing newer" and mails nothing
result: [pending]

### 6. Remove leftover cutover logs on CT 102
expected: /root/setup-adopt.log and /root/setup-final.log deleted
result: [pending]

### 7. Optional: exercise a rollback or halted path on the real CT
expected: A deliberately unhealthy release switches back, restarts the previous release, sends one "rolled back" mail, and later polls only journal
result: [pending]

## Summary

total: 7
passed: 0
issues: 0
pending: 7
skipped: 0
blocked: 0

## Gaps
