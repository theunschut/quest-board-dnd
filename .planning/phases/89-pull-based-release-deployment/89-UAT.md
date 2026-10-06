---
status: complete
phase: 89-pull-based-release-deployment
source: [89-VERIFICATION.md]
started: 2026-10-05T19:30:00Z
updated: 2026-10-06T07:31:00Z
---

## Current Test

[testing complete]

## Tests

### 1. WR-03: main-ancestry check with no usable answer is a quiet retry
expected: You accept that a rate-limited or flaky compare call never parks a good release, and that an unreadable 200 body is still a refusal
result: pass — signed off by the operator in chat (2026-10-06) before merging PR #158

### 2. WR-05: backup skipped only on an explicit databaseExists false
expected: You prefer refusing over backing up blind when the status does not say whether the database exists, and accept the reused "database could not be reached" reason
result: pass — signed off by the operator in chat (2026-10-06)

### 3. WR-06: a failed apply is re-read before it is called rolled back
expected: You accept that an unchanged pending count restarts the previous release, fewer pending continues as an applied install (installed or halted), and an unreadable database after three tries keeps the old "failed, rolled back" behaviour
result: pass — signed off by the operator in chat (2026-10-06), including the double-fault deviation

### 4. T-89-31: failed manual rollback recorded; manual runs journal
expected: You accept "abandoned" for the release left and "failed" for the target, no new mail reason, and journal lines for manual runs (not duplicated under the poll unit)
result: pass — signed off by the operator in chat (2026-10-06)

### 5. Ship the hardening to production
expected: Next release cut from PR #158 installs itself on CT 102; after `setup` from that release, /usr/local/sbin/questboard-deploy and /usr/local/lib/questboard-deploy/*.sh match the new zip; the next poll reports "nothing newer" and mails nothing
result: pass — v5.4.2 published and workstation-verified (sha256 2d4895b1…); CT 102 installed it by itself (current -> releases/5.4.2, /health X-QuestBoard-Version 5.4.2); after `setup` from 5.4.2, all 11 checked files (installer, 5 libs, poll units, drop-in, app DLL, manifest) are byte-identical to the verified zip; timer active, last poll Result=success

### 6. Remove leftover cutover logs on CT 102
expected: /root/setup-adopt.log and /root/setup-final.log deleted
result: pass — operator ran `setup && rm -f /root/setup-adopt.log /root/setup-final.log` on CT 102 and reported done

### 7. Optional: exercise a rollback or halted path on the real CT
expected: A deliberately unhealthy release switches back, restarts the previous release, sends one "rolled back" mail, and later polls only journal
result: pass — run on CT 102 on 2026-10-06 with no migrations between 5.4.1 and 5.4.2: `questboard-deploy rollback 5.4.1` exit 0 (attempts: `v5.4.2 abandoned`, `v5.4.1 rolled_back_manual`; current -> releases/5.4.1, /health X-QuestBoard-Version 5.4.1; manual rollback sends no mail by design); a poll then logged `skipping v5.4.2: abandoned earlier` and left current on 5.4.1; `questboard-deploy install v5.4.2` ran the full verified install, exit 0, attempts `v5.4.2 installed`, current -> releases/5.4.2, /health 5.4.2. The unhealthy-release auto switch-back and halted rows remain covered by offline flow tests and real-SQL CI tests

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
