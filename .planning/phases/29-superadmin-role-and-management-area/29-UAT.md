---
status: complete
phase: 29-superadmin-role-and-management-area
source: [29-01-SUMMARY.md, 29-04-SUMMARY.md, 29-05-SUMMARY.md]
started: 2026-06-30T00:00:00Z
updated: 2026-06-30T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Restart the app (dotnet run or docker-compose up). It starts cleanly — no migration errors, no startup exceptions. The homepage or login page loads and responds. The SuperAdmin migration (role ID 4) applied automatically.
result: pass

### 2. /Admin/Users — role badges visible
expected: Navigate to /Admin/Users. Each user row shows a colored role badge (Admin / Dungeon Master / Player) matching their membership in the EuphoriaInn group. No rows with blank/missing badges.
result: pass

### 3. Promote user to DM
expected: On /Admin/Users, click "Promote to DM" for a Player. Redirect back to Users list — that user now shows a "Dungeon Master" badge.
result: blocked
blocked_by: prior-phase
reason: "Promote/demote write actions guard on null ActiveGroupId and correctly return early. Session key is set in Phase 30. Adding ?? 1 to writes is a workaround deferred to Phase 30."

### 4. Demote Admin → Player (WR-01 fix)
expected: On /Admin/Users, click "Demote from Admin" for an Admin user. Redirect back — that user now shows a "Player" badge (not DM — this was the bug that was just fixed).
result: blocked
blocked_by: prior-phase
reason: "Same as test 3 — prior-phase dependency."

### 5. /players — player list not empty
expected: Navigate to /players (quest sign-up page). The player roster shows users who have the Player role in the EuphoriaInn group (not an empty list).
result: pass

### 6. SuperAdmin bypasses AdminOnly auth
expected: Log in as the SuperAdmin account. Navigate to /Admin/Users. Access is granted — the page loads (200) even though SuperAdmin holds no group-scoped Admin role.
result: pass

### 7. /platform/Group/Index accessible to SuperAdmin
expected: Navigate to /platform/Group/Index as SuperAdmin. The Platform area loads: modern-card layout, "Group Management" heading, EuphoriaInn row in the groups table with member count.
result: pass

### 8. Non-SuperAdmin denied at /platform
expected: Log in as a regular Admin or Player. Navigate to /platform/Group/Index. Redirected to login page (or 403) — access denied.
result: pass

### 9. Create a new group
expected: On /platform/Group/Index, click "Create Group". Fill in a unique name, submit. Redirected to index — the new group appears in the table.
result: pass

### 10. Edit group name
expected: Click "Edit" on a group. Change the name, submit. Redirected to index — updated name visible in the table.
result: pass

### 11. Delete empty group
expected: Click "Delete" on a group with 0 members. Delete confirmation page loads. Confirm delete — redirected to index, group is gone.
result: pass

### 12. Group Members page
expected: Click "Members" on EuphoriaInn (or any group with members). Members list shows rows with Name, Email, role badge (Admin/DM/Player), and a Remove button per row.
result: pass

### 13. Add member to group
expected: On the Members page, the "Add Member" section shows a dropdown of users not yet in the group + a role picker. Select a user, pick a role, submit. The user appears in the member list with the chosen role badge.
result: issue
reported: "invalid form submission"
severity: major

### 14. Remove member from group
expected: Click "Remove" next to a member on the Members page. The member disappears from the list (page reloads or redirects back).
result: pass

### 15. Hangfire dashboard accessible to SuperAdmin (WR-02 fix)
expected: Navigate to /hangfire as SuperAdmin. Dashboard loads — job queues and history visible (no redirect to login). Previously SuperAdmin was blocked; this tests the authorization fix.
result: pass

## Summary

total: 15
passed: 12
issues: 2
pending: 0
skipped: 0
blocked: 2

## Gaps

- truth: "Submitting the Add Member form on the Members page creates a UserGroups membership row for the selected user"
  status: failed
  reason: "User reported: invalid form submission"
  severity: major
  test: 13
  root_cause: ""
  artifacts: []
  missing: []

- truth: "The Hangfire nav link in the admin dropdown is only visible to SuperAdmin users (not to regular Admins)"
  status: failed
  reason: "User requested: access to Hangfire should be locked to SuperAdmin only; the admin dropdown menu button must also be removed for non-SuperAdmins"
  severity: minor
  test: 15
  root_cause: ""
  artifacts: []
  missing: []
