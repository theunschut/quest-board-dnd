# Phase 30: Group UX & Admin User Creation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-30
**Phase:** 30-group-ux-admin-user-creation
**Areas discussed:** Login routing flow, Group-picker page, Self-registration removal + admin user creation, Navigation group display

---

## Login Routing Flow

| Option | Description | Selected |
|--------|-------------|----------|
| In AccountController.Login POST | Add group-detection logic inline after SignInAsync succeeds | |
| Dedicated GroupPickerController | Login always redirects to /groups/pick; GroupPickerController handles all routing | ✓ |
| Action filter / middleware | Intercepts all post-login requests to check group context | |

**User's choice:** Dedicated GroupPickerController at /groups/pick

---

| Option | Description | Selected |
|--------|-------------|----------|
| Always /groups/pick | Login always redirects to picker, threading returnUrl | ✓ |
| Honour returnUrl first | Skip picker if returnUrl present, otherwise go to picker | |
| You decide | Claude picks | |

**User's choice:** Always /groups/pick

---

| Option | Description | Selected |
|--------|-------------|----------|
| Drop returnUrl — always Home/Index | Simpler, small trusted group | |
| Thread returnUrl through picker | Login passes ?returnUrl=... to GroupPickerController; picker forwards it on final redirect | ✓ |

**User's choice:** Thread returnUrl — user confirmed when asked about current returnUrl implementation

**Notes:** User asked "is returnUrl currently implemented?" — confirmed it IS currently implemented and working in AccountController.Login via RedirectToLocal(). User then chose to preserve this behavior by threading it through the new picker.

---

| Option | Description | Selected |
|--------|-------------|----------|
| GroupPickerController writes directly to ISession | No interface changes | ✓ (Claude's discretion) |
| Expose SetGroupId on IActiveGroupContext | Clean separation | |
| You decide | Claude picks | ✓ |

**User's choice:** You decide

---

| Option | Description | Selected |
|--------|-------------|----------|
| AdminHandler returns Fail → 403 | Existing Phase 29 D-03 behavior | ✓ (Claude's discretion) |
| Redirect to group picker | More user-friendly | |
| You decide | Claude picks | ✓ |

**User's choice:** You decide

---

## Group-Picker Page

| Option | Description | Selected |
|--------|-------------|----------|
| /groups/pick (new GroupPickerController) | Dedicated controller for clean separation | ✓ |
| /account/pick-group (AccountController) | Keep all auth flows together | |
| /home/pick-group (HomeController) | Less semantically clear | |

**User's choice:** /groups/pick — new GroupPickerController

---

| Option | Description | Selected |
|--------|-------------|----------|
| Cards grid | modern-card pattern, one card per group, click to select | ✓ |
| Simple list with buttons | Lighter-weight table format | |
| Dropdown + confirm | Compact, for large group counts | |

**User's choice:** Cards grid

---

| Option | Description | Selected |
|--------|-------------|----------|
| All groups + "Go to Platform →" button | SuperAdmin enters any group OR navigates to /platform | ✓ |
| All groups only | SuperAdmin navigates to platform from quest board nav | |
| You decide | Claude picks | |

**User's choice:** All groups + "Go to Platform →" button

---

| Option | Description | Selected |
|--------|-------------|----------|
| Stripped-down layout | User has no active group yet; full nav misleading | ✓ |
| Main quest board _Layout.cshtml | Reuse existing layout | |
| You decide | Claude picks | |

**User's choice:** Stripped-down layout (like login page)

---

## Self-Registration Removal + Admin User Creation

| Option | Description | Selected |
|--------|-------------|----------|
| Remove entirely + CreateUser in AdminController | Delete public /register; new action in admin area | ✓ |
| Keep /register with AdminOnly | Restrict existing form to logged-in admins | |
| Redirect /register to login | Soft removal | |

**User's choice:** Remove entirely + CreateUser in AdminController

**Notes:** User asked "if I apply the [Authorize] attribute, the new page can reuse the controller right? Is removal + new page a better choice?" — explained that both approaches reuse the same service calls (CreateAsync, SetGroupRoleAsync, ConfirmationEmailJob), but AdminController placement is better UX because the admin is already on the Users management page. User agreed with removal + AdminController approach.

---

| Option | Description | Selected |
|--------|-------------|----------|
| All three: Player, DungeonMaster, Admin | Admin can assign any role | ✓ |
| Player and DungeonMaster only | Prevents accidental admin proliferation | |
| You decide | Claude picks | |

**User's choice:** All three

---

| Option | Description | Selected |
|--------|-------------|----------|
| Back to Admin/Users list | Consistent with existing Edit/Reset redirect pattern | ✓ |
| Stay on CreateUser with success banner | Better for batch creation | |
| You decide | Claude picks | |

**User's choice:** Back to Admin/Users list

---

## Navigation Group Display

| Option | Description | Selected |
|--------|-------------|----------|
| @inject IActiveGroupContext in _Layout.cshtml | Direct DI in layout | ✓ |
| Base controller sets ViewBag.ActiveGroupName | New base controller pattern | |
| Razor ViewComponent | Full component for small nav text | |

**User's choice:** @inject in _Layout.cshtml

---

**User's choice (freeform):** "As a menu option from the Name Dropdown. Currently there's the option Profile and Logout, but there should be an additional button with the group name and some sort of 'switch' font awesome icon like arrows-rotate. clicking it will act as the switch group button"

---

| Option | Description | Selected |
|--------|-------------|----------|
| Icon + group name as the link text | e.g. ⟳ EuphoriaInn | ✓ |
| Icon + "Switch group" (no name) | No group name in link | |
| Two separate items | Name (non-clickable) + switch link | |

**User's choice:** Icon + group name as the link text

---

| Option | Description | Selected |
|--------|-------------|----------|
| At the top before Profile | Group context is primary | |
| After Profile, before Logout | Keeps profile editing near user name toggle | ✓ |

**User's choice:** After Profile, before Logout

---

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown only — no permanent badge | Sufficient for 17 members | ✓ |
| Badge in nav bar always visible | Always shows group name outside dropdown | |

**User's choice:** Dropdown only

---

**Mobile views (follow-up, user-initiated):** User asked about mobile views from prior milestones. Confirmed Phase 30 is within scope — mobile view parity for all new views (GroupPicker/Index.Mobile.cshtml, Admin/CreateUser.Mobile.cshtml, _Layout.Mobile.cshtml update) and deletion of Register.Mobile.cshtml. Not a new phase.

---

## Claude's Discretion

- Session writing location in GroupPickerController (write directly to ISession vs. interface method)
- Expired session mid-request behavior (stay with existing 403 from Phase 29 D-03)
- Group name storage strategy (session vs. per-request DB lookup — see CONTEXT.md D-16)
- Layout file name for group picker page
- Whether GroupPickerController lives under Controllers/ or a subdirectory
- Exact column count for cards grid
- CSS class for "Go to Platform →" button
- MGMT-08 verification (promote/demote already works via Phase 29 D-09; planner should verify end-to-end)

## Deferred Ideas

- Redirect to group picker on expired session mid-request (instead of 403) — adds middleware complexity, deferred
- Permanent group name badge in nav bar (outside dropdown) — user decided dropdown-only sufficient
- Per-group email configuration — future milestone
- Group invitation flow — future milestone
