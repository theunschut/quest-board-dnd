# Phase 37 / Plan 37-03 — Human Verification Checklist

Tasks 1-2 are merged into `milestone/v6-board-types` and all automated `LayoutNavigationTests` pass (16/16). This checklist is the one remaining item before Plan 37-03 (and Phase 37) closes out.

**How to run:** `dotnet run --project QuestBoard.Service` (or F5 in Visual Studio), then open the app in a browser.

## Desktop

- [ ] **OneShot group, DM/player login:** Calendar, Shop, Manage Shop (DM dropdown), Edit My Profile (DM dropdown), Players, Guild Members, Quest Log all visible — exactly as before this phase.
- [ ] **Campaign group:** Calendar, Shop, Manage Shop, Edit My Profile, Players are all GONE. Guild Members and Quest Log remain visible.
- [ ] **Logged out (anonymous):** Calendar link is NOT visible.
- [ ] **Admin (not SuperAdmin) login:** "Email Stats" link is absent from the Admin dropdown.
- [ ] **SuperAdmin login:** "Email Stats" link IS present.

## Mobile (narrow browser width / device emulation, offcanvas nav)

- [ ] **OneShot group:** same items visible as desktop.
- [ ] **Campaign group:** same items hidden as desktop (Guild Members/Quest Log remain).
- [ ] **Logged out:** Calendar hidden.
- [ ] *(Email Stats link doesn't exist in the mobile Admin section today — nothing to check here; trivially passes.)*

## Result

- [ ] **All checks pass** → reply "approved" and I'll close out the plan.
- [ ] **Something's wrong** → note which item, which layout (desktop/mobile), and which role/group state below.

**Notes:**
_(fill in here if anything looks off)_
