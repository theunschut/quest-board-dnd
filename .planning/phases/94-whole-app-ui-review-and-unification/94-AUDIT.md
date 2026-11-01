# Phase 94 Audit: Index and Summary

Index of the whole-app UI audit: one row per manifest view from the six area reports and the shell report, totals, the cascade measurement, the operator question batch for sign-off, the relayout list, merge proposals, dead dependencies and the proposed area-phase cut. Reports: [quests](94-AUDIT-quests.md), [events](94-AUDIT-events.md), [characters](94-AUDIT-characters.md), [shop](94-AUDIT-shop.md), [account](94-AUDIT-account.md), [admin](94-AUDIT-admin.md), [shell](94-AUDIT-shell.md), [vendor](94-AUDIT-vendor.md).

`QBR` is `~/qb-ui-review/phase-94`. Screenshots are never committed; paths below are scratch paths.

## Summary

The audit covers 60 manifest rows (57 view pairs and 3 state variants; `Account/SetPassword` needs an emailed token and was audited from source) on both layouts, the five layouts with both footers, the toasts and the backdrop. Seven reports, indexed below, record **397 findings: S1 37, S2 124, S3 190, S4 46**, of which 160 are accessibility, 83 are twin-content differences and 19 are cascade fights. The operator batch has **138 questions**; no answer removes anything.

### S1 findings (text unreadable or a control unreachable), all of them

Each blocks the "every page reads" rule (D-08) on its page until fixed. Most are contrast and disappear with surface inheritance, the button role table and the status variants; the ones that are not contrast are the two keyboard barriers (A-account-031, A-characters-006), the permanently hidden Return and Sell buttons (A-shop-002), the failing Create form (A-shop-010) and the Platform bar that is unreachable at 320px (A-admin-008).

- **A-quests-001** (Quest/Details (desktop)): Character name in the participants table is parchment on a pale striped cell: 1.33:1, effectively unreadable (same pale cell is used for the Waitlist and Not Selected...
- **A-quests-003** (Quest/Details (desktop)): Role labels in the Current Signups sidebar use text-primary, text-success, text-info on glass: strong.text-primary ("Players (n):") measures 1.22:1; the other two fall...
- **A-quests-019** (Quest/Details (mobile)): The Yes / No / Maybe vote buttons on the mobile voting state are outline buttons in Bootstrap green, red and gold on glass: labels measure 1.21:1 (Yes, No) and 3.36:1...
- **A-quests-031** (Quest/Create, Quest/Edit, Quest/CreateFollowUp (both)): Required asterisks and validation messages are span.text-danger: desktop #ff6b6b measures 1.97:1 on glass at B-max, mobile falls back to Bootstrap #dc3545 at 1.21:1. Two...
- **A-quests-032** (Quest/Create, Quest/Edit (mobile)): The "Dungeon Master Session Only" checkbox label on mobile is Bootstrap body dark grey: 1.77:1 at B-med, unreadable (desktop shows it in parchment).
- **A-quests-036** (Quest/CreateFollowUp (both)): The "Add Date" button on desktop is btn-outline-primary: Bootstrap blue text on glass, 1.22:1, an unreadable control (and outline against the filled-not-outline rule)....
- **A-quests-044** (Quest/Manage (desktop)): Character names, vote times and similar spans in the participants and waitlist tables are parchment on pale striped cells: span 1.45:1, time.local-time 1.31:1; the "No"...
- **A-quests-049** (QuestLog/Index, Home/Index (mobile)): Text on bare board in dark ink: the Quest Log title (h4.cinzel-heading 1.38:1 at B-med) and its italic tagline (#2c1810, 1.23:1); on the signed-out landing the title (h3...
- **A-events-001** (Events/Index, desktop (and shared partial...): the attendance headline ("0 Yes", 28px, 1.06:1 against a 3:1 bar), "(0 confirmed) 0 Maybe" (1.04:1) and the date line "Tue, Nov 3" (1.04:1) are cream text on the cream...
- **A-events-002** (Shared partial `_AvailabilityCell` as used on Events/Index...): the "Maybe" badge label is cream on amber (1.29:1), "Yes" 3.60, "No" 3.59, "Yes" unconfirmed 3.60; Agenda desktop badge bg-primary (One-Shot) 3.57, bg-secondary (Active...
- **A-events-003** (Agenda/Index and Events/Index, mobile (page header on bare...): the subtitle "Upcoming events across all your boards" is rgb(33,37,41) on the bare board, 1.11:1, and effectively invisible; the h4 title is #1a0f08 at 1.38:1 and...
- **A-characters-001** (Characters/Create (both layouts)): Image-crop dialog title and instruction text are unreadable (dark ink on the dark dialog)
- **A-characters-002** (shared partial: Shared/_CharacterSelectModal (both layouts)): Character add/change/clear dialog: title and "Select Character" label are unreadable; required marker and Remove button fail
- **A-characters-003** (Characters/Details (mobile)): "Edit Character" label is parchment on amber (1.29:1) on the mobile Character page
- **A-characters-004** (Contacts/Details (mobile)): "Edit Contact" label is parchment on amber (1.29:1) on the mobile Contact page for DMs
- **A-characters-005** (ContactCategoryManagement/Edit (mobile)): "Category Name" label is dark ink on the glass card (1.77:1) on the mobile Rename Category page
- **A-characters-006** (shared partial: Characters/Create (both layouts)): Markdown editor traps the keyboard: Tab and Escape never leave it, so the controls after it (Save, Cancel, Add Note) cannot be reached from the keyboard
- **A-shop-001** (`Shop/Index`, desktop (all three empty variants).): Desktop Shop empty states render dark ink on the navy panel
- **A-shop-002** (`Shop/Index`, desktop (purchase history panel).): Return and Sell buttons in the purchase history are permanently invisible
- **A-shop-003** (`Shop/Index`, desktop.): Purchase panel empty text is invisible
- **A-shop-004** (shared partial `_ShopItemDetailsContent`, rendered in the...): Item-details modal on desktop: labels, helper text and the Sell block read at 1.1 to 1.8:1
- **A-shop-005** (`Shop/Details.Mobile` content loaded into the mobile Shop...): Item-details modal on mobile: item title unreadable
- **A-shop-006** (`ShopManagement/Index`, desktop and mobile.): "View Shop" outline button text unreadable
- **A-shop-007** (`ShopManagement/Index`, desktop.): Description teasers and muted text on the parchment table rows
- **A-shop-008** (`ShopManagement/Index`, desktop.): "N left" stock badges unreadable in the item table
- **A-shop-009** (`ShopManagement/Edit.Mobile`.): Edit (mobile): "Item ID" unreadable on the status alert
- **A-shop-010** (`ShopManagement/Create`, both layouts.): Creating a shop item always fails (functional break, not a UI fix this phase makes)
- **A-account-019** (Profile): Profile desktop: calendar-subscription meta text is #6c757d on glass and on the parchment cell
- **A-account-031** (GroupPicker): Board cards are clickable divs: a keyboard or screen-reader user cannot choose a board
- **A-admin-001** (Platform Group/Index (desktop): span.badge.bg-info.text-dark is parchment #F4E4BC on cyan, 1.55:1; the bare .modern-card span { color: … !important } at wwwroot/css/modern-card.css:91-97 out-ranks the...
- **A-admin-002** (Admin/Users (desktop) and Platform Group/Members (desktop)): span.badge.bg-warning (Dungeon Master role badge) parchment on amber #ffc107, 1.29:1; same cause as A-admin-001 (modern-card.css:91-97 beats text-dark)...
- **A-admin-003** (Admin/Users (mobile)): span.badge.bg-warning white on amber, 1.63:1 (the mobile twin has no text-dark and no label rule); Views/Admin/Users.Mobile.cshtml:39
- **A-admin-004** (Admin/Quests (desktop)): the Description cell span.text-truncate.d-inline-block is parchment text on a parchment paper cell, 1.20 to 1.45:1 in every row (15 rows in the fixture); the bare-span...
- **A-admin-005** (Platform Users/Index (mobile)): every a.email-link is Bootstrap link blue rgb(13,110,253) on glass, 1.05:1 at B-99 (one per listed user); the .email-link rule exists only in modern-card.css:307-313...
- **A-admin-006** (Platform Group/Members (desktop, Create New User modal)): modal title and .form-labels are dark ink rgb(26,15,8) on the Bootstrap bg-dark content #212529, 1.22:1 (readable only through the white halo); cause...
- **A-admin-007** (Admin/EditUser (desktop and mobile)): a.btn.btn-outline-danger Reset Password is red rgb(220,53,69) on glass, 1.21:1 at B-max; it is also the only outline button in the area, against the filled-only rule...
- **A-admin-008** (Platform desktop layout (shared layout): ul.navbar-nav is 377px wide and ends at x=393, so the user name and the Logout button sit beyond the 320px viewport of a fixed navbar and cannot be scrolled to...

### Headline results

- **The foundation cannot be a Bootstrap layer alone.** Moving Bootstrap and Font Awesome into `@layer vendor` on a scratch copy changed all 118 captures: the footer turns into an opaque near-black bar with near-black text (1.22:1), the Dungeon Master and Admin menu toggles turn red (1.05 to 2.19:1), muted text on glass drops to 1.57 to 2.24:1, validation lines to 1.21 to 1.93:1, and the nav links widen and wrap. It also repairs the cream-on-cream attendance counts on Events/Index. Re-theme through Bootstrap's variables in the commit that retires each legacy rule (Cascade section).
- **The shell has defects of its own** (39 findings, none S1, 18 S2): no skip link or labelled landmarks on any layout, unnamed togglers and close buttons, a fixed bar 8px taller than its offset that wraps to 76 to 99px between 992 and 1280px and covers the first content row, toasts that sit on top of the Calendar link, the user menu and the phone menu button, error and warning toasts that disappear after five seconds, a 2.5 MB backdrop with a white page until it loads, and a footer whose text fails contrast on both layouts.
- **Functional breaks found beside the looks.** `ShopManagement/Create` fails for everyone (the controller never sets `GroupId`; recorded in `deferred-items.md`): it is to be fixed as its own pull request off `main`, outside this phase, and the Shop area phase's after-capture of Create needs it; the audit board's shop items were seeded by SQL as a workaround. Four more breaks need an owner (Q-137 proposes the Shop phase): the purchase history's Return and Sell buttons are permanently hidden (A-shop-002), the sell confirmation script never runs inside the modal (A-shop-017), the Create form's Preview button is dead (A-shop-027) and the Edit page's View Details link answers 404 (A-shop-026). Also: the role-select script on Quest Details hides the DM Controls card (A-quests-011), the markdown editor traps the keyboard on six pages (A-characters-006), and the board picker's cards are not keyboard-reachable (A-account-031, S1).
- **Twin differences.** 83 twin-content findings became questions; none was assumed deliberate except the two the contract seeds as deliberate (the desktop-only DM Controls card on Quest Manage and differences commented in the views). The six Account `.Mobile` views render an empty `<title>` (A-account-007, six questions).
- **Ownership gaps the reports flagged for the cut.** `Shared/_Calendar` and the vote and current-quest rules in `calendar.css` (Events or Quests); `dm-create.mobile.css`, `dm-manage.mobile.css` and `_CharacterSelectModal` (render only under Quest views); `_MarkdownEditor`'s keyboard trap (area 1's partial, six Characters-area pages); `Home/Index` (possibly Account and pickers); `Move/Index` serves quests and events. The cut below proposes an owner for each and asks the operator.
- **Evidence limits.** `Account/SetPassword` is source-only; no purchase row exists on the audit board so Return and Sell are static evidence; the focus probe is desktop-only; the cascade probe samples colour, spacing, size and position, not box-shadow or background images; real toasts were injected, not produced by the application. The report quotes no member name, email or user-entered text; screenshots are scratch paths only.
- **Dead dependencies** are in `94-AUDIT-vendor.md` (plan 94-13), linked below by name; it may land after this report.

## Index

One row per view, twins on one row, plus one row per layout. Each row is copied from the Index of the report named in its first cell, so the counts and the empty-state copy are the area report's own; the area phase column uses the draft cut's labels. The operator-question column cites the area report's own question IDs; the batch below renumbers them Q-01 onward.

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| [Shared/_Layout (desktop layout with `_Toasts`, backdrop and shell rules)](94-AUDIT-shell.md) | `Views/Shared/_Layout.cshtml` | `Views/Shared/_Layout.Mobile.cshtml` (next row) | Foundation (94) | a11y 13, visual-bug 4, cascade-fight 2, perf 2, inconsistency 1, dead-code 1 (23) | S2 | none (the shell has no list) | 1 (R-shell-1) |
| [Shared/_Layout.Mobile (mobile layout, twin of the row above)](94-AUDIT-shell.md) | `Views/Shared/_Layout.cshtml` | `Views/Shared/_Layout.Mobile.cshtml` | Foundation (94) | a11y 2, visual-bug 1, twin-content 4 (7) | S2 | none | 4 (Q-shell-02, 04, 05, 06) |
| [Shared/_Footer (both footers)](94-AUDIT-shell.md) | `Views/Shared/_Footer.cshtml` | `Views/Shared/_Footer.Mobile.cshtml` | Foundation (94) | twin-content 1, inconsistency 1, a11y 1 (3) | S2 | none | 1 (Q-shell-01) |
| [Shared/_Layout.GroupPicker](94-AUDIT-shell.md) | `Views/Shared/_Layout.GroupPicker.cshtml` | none (served under both) | Foundation (94) | twin-content 1, inconsistency 1 (2) | S3 | none | 1 (Q-shell-03) |
| [Platform/_Layout.Platform (both Platform layouts)](94-AUDIT-shell.md) | `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` | `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` | Foundation (94) | inconsistency 1, cascade-fight 1 (2) | S3 | none | 0 |
| [All five layouts (cross-layout items)](94-AUDIT-shell.md) | the five files above | the five files above | Foundation (94) | a11y 1, perf 1 (2) | S2 | none | 0 |
| [Shared/_Toasts (all layouts)](94-AUDIT-shell.md) | `Views/Shared/_Toasts.cshtml` | none (one partial, all layouts) | Foundation (94) | counted on the first row (A-shell-011 to 017) | S2 | none (copy comes from controllers) | 0 |
| [Quest/Details](94-AUDIT-quests.md) | `Views/Quest/Details.cshtml` | `Views/Quest/Details.Mobile.cshtml` | 1 Quests and Quest Log | visual-bug 2, inconsistency 4, cascade-fight 2, twin-drift 1, twin-content 14, a11y 12, perf 2 | S1 | none (no signup-list empty state; row fallbacks only: "No character" (desktop italic, mobile plain), "No Vote") | 14 (OQ-QD-1 to OQ-QD-14) |
| [Quest/Index](94-AUDIT-quests.md) | `Views/Quest/Index.cshtml` | `Views/Quest/Index.Mobile.cshtml` | 1 Quests and Quest Log | visual-bug 1, inconsistency 5, twin-content 3, a11y 5, perf 2 | S2 | desktop: none (an empty board renders an empty container). mobile: "No Quests Available" / "Check back soon — a Dungeon Master may post a quest at any time." | 3 (OQ-QI-1 to OQ-QI-3) |
| [Quest/Create](94-AUDIT-quests.md) | `Views/Quest/Create.cshtml` | `Views/Quest/Create.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 2, cascade-fight 1, twin-drift 1, twin-content 2, a11y 6 | S1 | none | 2 (OQ-QC-1 to OQ-QC-2) |
| [Quest/Edit](94-AUDIT-quests.md) | `Views/Quest/Edit.cshtml` | `Views/Quest/Edit.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 4, cascade-fight 1, twin-drift 1, twin-content 3, a11y 6 | S1 | none | 3 (OQ-QE-1 to OQ-QE-3) |
| [Quest/CreateFollowUp](94-AUDIT-quests.md) | `Views/Quest/CreateFollowUp.cshtml` | `Views/Quest/CreateFollowUp.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 3, cascade-fight 1, twin-drift 1, twin-content 1, a11y 7 | S1 | both: "No players were selected on the original quest. You can add players manually after saving." | 1 (OQ-QF-1) |
| [Quest/Manage](94-AUDIT-quests.md) | `Views/Quest/Manage.cshtml` | `Views/Quest/Manage.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 5, cascade-fight 1, twin-drift 1, twin-content 7, a11y 5, perf 2 | S1 | both: "No participants selected for this quest."; alert "No Proposed Dates" / "This quest doesn't have any proposed dates yet. Please edit the quest to add some dates before you can finalize it."; desktop only: "No proposed dates yet" (Quick Stats) | 7 (OQ-QM-1 to OQ-QM-7) |
| [QuestLog/Index](94-AUDIT-quests.md) | `Views/QuestLog/Index.cshtml` | `Views/QuestLog/Index.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 3, twin-content 3, a11y 4, perf 1 | S1 | both (same copy): "No Completed Quests Yet" / "Completed quests will appear here once they have been undertaken and finished." | 3 (OQ-LI-1 to OQ-LI-3) |
| [QuestLog/Details](94-AUDIT-quests.md) | `Views/QuestLog/Details.cshtml` | `Views/QuestLog/Details.Mobile.cshtml` | 1 Quests and Quest Log | visual-bug 1, inconsistency 1, cascade-fight 2, twin-drift 2, a11y 2 | S2 | both (same copy): "No adventurers participated in this quest."; "No recap has been written for this quest yet." | 0 |
| [QuestLog/EditRecap](94-AUDIT-quests.md) | `Views/QuestLog/EditRecap.cshtml` | `Views/QuestLog/EditRecap.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 1, cascade-fight 1, twin-drift 1, a11y 4 | S2 | none | 0 |
| [Home/Index](94-AUDIT-quests.md) | `Views/Home/Index.cshtml` | `Views/Home/Index.Mobile.cshtml` | 1 Quests and Quest Log | twin-drift 1, a11y 2 | S1 | none | 0 |
| [Move/Index](94-AUDIT-quests.md) | `Views/Move/Index.cshtml` | `Views/Move/Index.Mobile.cshtml` | 1 Quests and Quest Log | inconsistency 1, twin-drift 1, twin-content 1, a11y 1 | S2 | both: "Choose a board to see what the move will do there." (no target chosen); alert "That board is not part of this linked set. Choose one from the list." | 1 (OQ-MV-1) |
| [Area-wide (stylesheets, dead files, shared partials)](94-AUDIT-quests.md) | n/a | n/a | 1 Quests and Quest Log | inconsistency 1, cascade-fight 1, a11y 1, perf 1, dead-code 2 | S2 | n/a | 0 |
| [Calendar/Index](94-AUDIT-events.md) | `Views/Calendar/Index.cshtml` | `Views/Calendar/Index.Mobile.cshtml` | 2 Events, Series, Calendar, Agenda | a11y 9, visual-bug 2, twin-drift 1, twin-content 8, inconsistency 4, cascade-fight 1, perf 1, dead-code 2 | S2 | desktop none; mobile "Nothing This Month" (see Empty-state copy) | 8 (A-events-039 to 046) |
| [Agenda/Index](94-AUDIT-events.md) | `Views/Agenda/Index.cshtml` | `Views/Agenda/Index.Mobile.cshtml` | 2 Events, Series, Calendar, Agenda | a11y 6, cascade-fight 2, twin-drift 1, inconsistency 2, perf 1, dead-code 1 | S1 | "No Boards Yet", "No Upcoming Events", "All Boards Filtered Out" (identical on both layouts) | 0 |
| [Events/Index](94-AUDIT-events.md) | `Views/Events/Index.cshtml` | `Views/Events/Index.Mobile.cshtml` | 2 Events, Series, Calendar, Agenda | a11y 4, cascade-fight 3, twin-drift 1, twin-content 2, inconsistency 2, perf 1, dead-code 1 | S1 | "No Upcoming Events" (identical on both layouts) | 2 (A-events-047, 048) |
| [Events/Create](94-AUDIT-events.md) | `Views/Events/Create.cshtml` | none (served under both) | 2 Events, Series, Calendar, Agenda | a11y 4, twin-drift 1, inconsistency 2 | S2 | none (form); recurrence preview placeholder only | 0 |
| [Events/Details](94-AUDIT-events.md) | `Views/Events/Details.cshtml` | none (served under both) | 2 Events, Series, Calendar, Agenda | a11y 3, cascade-fight 1, visual-bug 1, twin-drift 1, inconsistency 2 | S1 | none as a page state; inline empties listed in Empty-state copy | 0 |
| [Events/Edit](94-AUDIT-events.md) | `Views/Events/Edit.cshtml` | none (served under both) | 2 Events, Series, Calendar, Agenda | a11y 2, twin-drift 1, inconsistency 1 | S2 | none (form) | 0 |
| [Series/Details](94-AUDIT-events.md) | `Views/Series/Details.cshtml` | none (served under both) | 2 Events, Series, Calendar, Agenda | a11y 3, inconsistency 2, dead-code 1 | S2 | "No description provided.", "No occurrences have been generated yet." | 0 |
| [Characters/Index](94-AUDIT-characters.md) | `Views/Characters/Index.cshtml` | `Views/Characters/Index.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | inconsistency 3, cascade-fight 1, twin-content 1, a11y 9, perf 2 | S2 | Desktop and mobile, same wording. My Characters: "No Characters Yet" / "Create your first character to get started!" / action "Create Character". Character Roster: "No Other Characters" / "No other adventurers have created characters yet!". Desktop headings are h4, mobile h5. | 1 |
| [Characters/Details](94-AUDIT-characters.md) | `Views/Characters/Details.cshtml` | `Views/Characters/Details.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 1, inconsistency 1, cascade-fight 1, twin-drift 1, a11y 7 | S1 | Both layouts: "Not provided" (no sheet link) and "No classes defined". No page-level empty state. | 1 |
| [Characters/Create](94-AUDIT-characters.md) | `Views/Characters/Create.cshtml` | `Views/Characters/Create.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, twin-content 1, a11y 7, dead-code 1 | S1 | none (form page) | 3 |
| [Characters/Edit](94-AUDIT-characters.md) | `Views/Characters/Edit.cshtml` | `Views/Characters/Edit.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, twin-content 1, a11y 7 | S1 | none (form page) | 3 |
| [Contacts/Index](94-AUDIT-characters.md) | `Views/Contacts/Index.cshtml` | `Views/Contacts/Index.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | inconsistency 5, cascade-fight 2, twin-drift 1, twin-content 2, a11y 8, perf 1 | S2 | Both layouts, same wording. No contacts: "No Contacts Yet" / "No one has added a contact yet. DMs can create the first one to start building out the world.". Filter matches nothing: "No contacts match your filters" / "Try selecting different tags." / action "Clear filters" (desktop) and "Clear Filters" (mobile). | 2 |
| [Contacts/Details](94-AUDIT-characters.md) | `Views/Contacts/Details.cshtml` | `Views/Contacts/Details.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 1, inconsistency 3, cascade-fight 1, twin-drift 2, a11y 9, dead-code 1 | S1 | Both layouts, same wording. "No description provided."; notes: "No Notes Yet" / "Be the first to jot something down about this contact." | 1 |
| [Contacts/Create](94-AUDIT-characters.md) | `Views/Contacts/Create.cshtml` | `Views/Contacts/Create.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, a11y 8 | S1 | none (form page). Form hint when the board has no categories, both layouts: "No categories yet. Manage Categories to create one." | 3 |
| [Contacts/Edit](94-AUDIT-characters.md) | `Views/Contacts/Edit.cshtml` | `Views/Contacts/Edit.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, a11y 8 | S1 | none (form page). Same no-categories hint as Contacts/Create, both layouts. | 3 |
| [ContactCategoryManagement/Manage](94-AUDIT-characters.md) | `Views/ContactCategoryManagement/Manage.cshtml` | `Views/ContactCategoryManagement/Manage.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | inconsistency 4, cascade-fight 1, twin-content 1, a11y 5 | S2 | Both layouts, same wording: "No Categories Yet" / "Add your first category above to start grouping your contacts." | 1 |
| [ContactCategoryManagement/Edit](94-AUDIT-characters.md) | `Views/ContactCategoryManagement/Edit.cshtml` | `Views/ContactCategoryManagement/Edit.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 1, inconsistency 2, cascade-fight 1, a11y 3 | S1 | none (form page) | 2 |
| [DungeonMaster/Profile](94-AUDIT-characters.md) | `Views/DungeonMaster/Profile.cshtml` | `Views/DungeonMaster/Profile.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | inconsistency 5, cascade-fight 1, twin-content 1, a11y 7, dead-code 1 | S2 | Both layouts, same wording. Bio: "No bio provided yet."; quests: "No Quests Yet" / "This Dungeon Master hasn't run any quests yet." (desktop heading h4, mobile h5). | 1 |
| [DungeonMaster/EditProfile](94-AUDIT-characters.md) | `Views/DungeonMaster/EditProfile.cshtml` | `Views/DungeonMaster/EditProfile.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | visual-bug 1, inconsistency 5, cascade-fight 2, twin-drift 1, a11y 6 | S1 | none (form page) | 3 |
| [Players/Index](94-AUDIT-characters.md) | `Views/Players/Index.cshtml` | `Views/Players/Index.Mobile.cshtml` | 3 Characters, Contacts, DM profile, Players | inconsistency 3, cascade-fight 1, a11y 5 | S2 | Both layouts, same wording. "No Dungeon Masters Found" / "No dungeon masters are registered yet. Brave souls may register as new quest leaders."; "No Players Found" / "No adventurers have registered yet. Players can register to join quests." | 0 |
| [Shared/_CharacterSelectModal](94-AUDIT-characters.md) | `Views/Shared/_CharacterSelectModal.cshtml` | none (one partial, both layouts) | 3 Characters, Contacts, DM profile, Players | visual-bug 1, inconsistency 3 | S1 | none | 0 |
| [Shared/_MarkdownEditor](94-AUDIT-characters.md) | `Views/Shared/_MarkdownEditor.cshtml` | none (one partial, both layouts) | 3 Characters, Contacts, DM profile, Players | a11y 1 | S1 | none | 0 |
| [Shop/Index](94-AUDIT-shop.md) | `Views/Shop/Index.cshtml` | `Views/Shop/Index.Mobile.cshtml` | 4 Shop and Shop management | 3 / 2 / 2 / 0 / 7 / 8 / 1 / 2 (25) | S1 | Desktop: "The shop is currently empty / Check back later for new items, or contact a Dungeon Master about stocking the shop."; "No items match your filters / Try removing some rarity filters or changing the sort order."; "No items match your search / Try a different name or adjust your filters."; panel "No purchases yet / Your purchase history will appear here". Mobile: "No items found / Try adjusting your filters or search query." (see "Empty-state copy") | 7 (Q-shop-01 to 07), plus theme decision Q-shop-22 and relayout sign-off R-1, R-2 |
| [Shop/Details](94-AUDIT-shop.md) | `Views/Shop/Details.cshtml` (+ partial `Views/Shared/_ShopItemDetailsContent.cshtml`) | `Views/Shop/Details.Mobile.cshtml` | 4 Shop and Shop management | 1 / 2 / 0 / 0 / 3 / 4 / 0 / 2 (12) | S1 | none (no list). Out-of-stock and draft notices: desktop "This item is currently out of stock." and "This item is currently under review and not available for purchase."; mobile "Out of stock." and "This item is not currently available for purchase." | 4 (Q-shop-08 to 11) |
| [ShopManagement/Index](94-AUDIT-shop.md) | `Views/ShopManagement/Index.cshtml` | `Views/ShopManagement/Index.Mobile.cshtml` | 4 Shop and Shop management | 0 / 0 / 0 / 1 / 5 / 9 / 0 / 2 (17) | S1 | Desktop: "No Items Yet / You haven't created any shop items yet. Start by adding your first item!" and "No Other Items / No other DMs have published items yet." Mobile: "No items yet / Add the first item to your shop." | 6 (Q-shop-12 to 17) |
| [ShopManagement/Create](94-AUDIT-shop.md) | `Views/ShopManagement/Create.cshtml` | `Views/ShopManagement/Create.Mobile.cshtml` | 4 Shop and Shop management | 1 / 0 / 0 / 0 / 0 / 4 / 0 / 3 (8; Create and Edit shared findings counted here) | S1 | none (form) | 1 (Q-shop-18) |
| [ShopManagement/Edit](94-AUDIT-shop.md) | `Views/ShopManagement/Edit.cshtml` | `Views/ShopManagement/Edit.Mobile.cshtml` | 4 Shop and Shop management | 0 / 0 / 0 / 0 / 1 / 1 / 0 / 1 (3) | S1 | none (form) | 2 (Q-shop-19, Q-shop-21) |
| [Account/Login](94-AUDIT-account.md) | Views/Account/Login.cshtml | Views/Account/Login.Mobile.cshtml | 5a Account and pickers | twin-content 1, a11y 10, perf 1 (total 12); also covered by shared 014, 015 | S2 | none (no empty state) | Q-account-1; M1 |
| [Account/ForgotPassword](94-AUDIT-account.md) | Views/Account/ForgotPassword.cshtml | Views/Account/ForgotPassword.Mobile.cshtml | 5a Account and pickers | none as primary view (total 0); also covered by shared 002, 003, 004, 005, 006, 007, 009, 010, 011, 012, 014 | S2 (shared) | none (no empty state) | Q-account-2; M2 |
| [Account/SetPassword](94-AUDIT-account.md) | Views/Account/SetPassword.cshtml | Views/Account/SetPassword.Mobile.cshtml | 5a Account and pickers | twin-drift 1 (total 1); also covered by shared 003, 004, 005, 006, 007, 009, 010, 011, 012, 013, 014 | S4 | none (no empty state) | Q-account-3; M3 |
| [Account/ChangePassword](94-AUDIT-account.md) | Views/Account/ChangePassword.cshtml | Views/Account/ChangePassword.Mobile.cshtml | 5a Account and pickers | a11y 1 (total 1); also covered by shared 003, 004, 005, 006, 007, 009, 010, 011, 012, 014 | S2 | none (no empty state) | Q-account-4; M4 |
| [Account/Profile](94-AUDIT-account.md) | Views/Account/Profile.cshtml | Views/Account/Profile.Mobile.cshtml | 5a Account and pickers | visual-bug 3, inconsistency 3, cascade-fight 1, twin-drift 1, twin-content 2, a11y 4, dead-code 2 (total 16); also covered by shared 002, 005, 007, 009, 010, 011, 012, 013, 015 | S1 | desktop and mobile identical: heading "No calendar subscriptions yet"; body in the Empty-state copy section | Q-account-6, 8, 9; R2 |
| [Account/Edit](94-AUDIT-account.md) | Views/Account/Edit.cshtml | Views/Account/Edit.Mobile.cshtml | 5a Account and pickers | twin-content 1, a11y 2 (total 3); also covered by shared 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 014, 015, 020 | S2 | none (no empty state) | Q-account-5, 7 |
| [GroupPicker/Index](94-AUDIT-account.md) | Views/GroupPicker/Index.cshtml | Views/GroupPicker/Index.Mobile.cshtml | 5a Account and pickers | inconsistency 1, a11y 4, perf 2 (total 7); also covered by shared 009, 010, 011, 012 | S1 | desktop and mobile identical: "Your account is not assigned to any group. Please contact your administrator." | Q-account-10; R1; M5 |
| [Shared/AccessDenied](94-AUDIT-account.md) | Views/Shared/AccessDenied.cshtml | none (served under both) | 5a Account and pickers | visual-bug 1, inconsistency 1, a11y 1 (total 3); also covered by shared 002, 009, 010, 011, 012, 014, 020 | S2 | none (the page is itself the denial state; copy in the Empty-state copy section) | none |
| [Admin/Users](94-AUDIT-admin.md) | `Views/Admin/Users.cshtml` | `Views/Admin/Users.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 4, cascade-fight 1, twin-drift 2, twin-content 3, a11y 6, perf 2, dead-code 1 (21) | S1 | desktop "No users found."; mobile "No users found. Users appear here once registered." (differs: A-admin-027) | 4 (Q1, Q2, Q9, R-3) |
| [Admin/CreateUser](94-AUDIT-admin.md) | `Views/Admin/CreateUser.cshtml` | `Views/Admin/CreateUser.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 2, a11y 4, perf 1 (10) | S2 | none (form) | 2 (Q6, R-1) |
| [Admin/EditUser](94-AUDIT-admin.md) | `Views/Admin/EditUser.cshtml` | `Views/Admin/EditUser.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 1, twin-content 1, a11y 6, perf 1 (12) | S1 | none (form) | 3 (Q6, Q7, R-1) |
| [Admin/ResetPassword](94-AUDIT-admin.md) | `Views/Admin/ResetPassword.cshtml` | `Views/Admin/ResetPassword.Mobile.cshtml` | 5b Admin and Platform | inconsistency 2, twin-content 1, a11y 5, perf 1 (9) | S2 | none (form) | 1 (Q8) |
| [Admin/Quests](94-AUDIT-admin.md) | `Views/Admin/Quests.cshtml` | `Views/Admin/Quests.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 2, cascade-fight 1, twin-drift 3, twin-content 2, a11y 6, perf 2, dead-code 1 (19) | S1 | desktop "No quests found."; mobile "No quests found. Quests appear here once created by a DM." (differs: A-admin-027) | 4 (Q3, Q5a, Q5b, Q9) |
| [Admin/EmailStats](94-AUDIT-admin.md) | `Views/Admin/EmailStats.cshtml` | none (served under both) | 5b Admin and Platform | inconsistency 1, twin-drift 1, a11y 3, perf 1 (6) | S2 | no list; state copy only, see Empty-state copy | 0 |
| [Group/Index (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Group/Index.cshtml` | `Areas/Platform/Views/Group/Index.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 2, cascade-fight 1, twin-drift 2, a11y 5, perf 2, dead-code 1 (15) | S1 | "No groups yet." / "Create the first group to get started." (same in both) | 0 |
| [Group/Create (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Group/Create.cshtml` | `Areas/Platform/Views/Group/Create.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, a11y 5, perf 1 (9) | S2 | none (form) | 1 (M-1) |
| [Group/Edit (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Group/Edit.cshtml` | `Areas/Platform/Views/Group/Edit.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 1, a11y 6, perf 1 (11) | S2 | none (form) | 1 (M-3) |
| [Group/Delete (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Group/Delete.cshtml` | `Areas/Platform/Views/Group/Delete.Mobile.cshtml` | 5b Admin and Platform | inconsistency 1, a11y 3, perf 1 (5) | S2 | none (confirmation page) | 1 (M-2) |
| [Group/Members (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Group/Members.cshtml` | `Areas/Platform/Views/Group/Members.Mobile.cshtml` | 5b Admin and Platform | visual-bug 1, inconsistency 3, cascade-fight 1, twin-drift 2, twin-content 1, a11y 7, perf 2, dead-code 1 (18) | S1 | members "No members in this group yet."; users "All users are already members of this group." plus search states (same in both) | 2 (Q4, R-2) |
| [Users/Index (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/Users/Index.cshtml` | `Areas/Platform/Views/Users/Index.Mobile.cshtml` | 5b Admin and Platform | visual-bug 1, inconsistency 2, twin-drift 1, a11y 7, perf 2, dead-code 1 (14) | S1 | "No users found." (same in both) | 0 |
| [LinkedBoards/Index (Platform)](94-AUDIT-admin.md) | `Areas/Platform/Views/LinkedBoards/Index.cshtml` | `Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, cascade-fight 1, twin-drift 2, a11y 5, perf 2, dead-code 1 (14) | S1 | "No linked sets yet." / "Create a set above, then add boards of the same type to share their calendars." (same in both) | 0 |

## Totals by class and severity

Findings counted from the finding entries of the seven reports (each finding once; shared-partial findings are counted in the report that owns them). The classes are the contract's eight; a few reports tagged a functional defect with the nearest class and say so in the finding.

| Class | S1 | S2 | S3 | S4 | Total |
|-------|----|----|----|----|-------|
| visual-bug | 7 | 14 | 5 | 4 | 30 |
| inconsistency | 0 | 1 | 36 | 11 | 48 |
| cascade-fight | 5 | 2 | 12 | 0 | 19 |
| twin-drift | 0 | 2 | 8 | 7 | 17 |
| twin-content | 0 | 5 | 71 | 7 | 83 |
| a11y | 25 | 95 | 35 | 5 | 160 |
| perf | 0 | 2 | 15 | 2 | 19 |
| dead-code | 0 | 3 | 8 | 10 | 21 |
| **Total** | **37** | **124** | **190** | **46** | **397** |

By report:

| Report | Findings | S1 | S2 | S3 | S4 | Twin-content | a11y |
|--------|----------|----|----|----|----|--------------|------|
| [94-AUDIT-shell.md](94-AUDIT-shell.md) (Foundation (94)) | 39 | 0 | 18 | 19 | 2 | 6 | 17 |
| [94-AUDIT-quests.md](94-AUDIT-quests.md) (1) | 100 | 8 | 22 | 65 | 5 | 34 | 33 |
| [94-AUDIT-events.md](94-AUDIT-events.md) (2) | 51 | 3 | 23 | 22 | 3 | 10 | 23 |
| [94-AUDIT-characters.md](94-AUDIT-characters.md) (3) | 55 | 6 | 13 | 25 | 11 | 6 | 21 |
| [94-AUDIT-shop.md](94-AUDIT-shop.md) (4) | 65 | 10 | 19 | 29 | 7 | 16 | 26 |
| [94-AUDIT-account.md](94-AUDIT-account.md) (5a) | 43 | 2 | 15 | 14 | 12 | 4 | 22 |
| [94-AUDIT-admin.md](94-AUDIT-admin.md) (5b) | 44 | 8 | 14 | 16 | 6 | 7 | 18 |
| **All** | **397** | **37** | **124** | **190** | **46** | **83** | **160** |

## Cascade

What moving Bootstrap and Font Awesome into `@layer vendor` costs, measured page by page, and which legacy rules the foundation has to retire first. Evidence is on scratch copies outside the repository; nothing in the repository was edited by the probe.

### Method (cascade-probe)

- **Scratch copies.** `git archive HEAD` of this branch (source identical to the phase base `b961a96f`, which already self-hosts Bootstrap, Font Awesome and Cinzel) was extracted twice under `$QBR/cascade-probe/`: `src/` (the probe) and `control-src/` (untouched control). In `src/` only, `wwwroot/css/app.css` got the layer statement `@layer vendor, tokens, base, components, pages, utilities;` followed by `@import url("../lib/bootstrap-5.3.0/css/bootstrap.min.css") layer(vendor);` and `@import url("../lib/fontawesome-6.4.0/css/all.min.css") layer(vendor);`, and the Bootstrap and Font Awesome `<link>` tags were removed from the five layouts (10 lines). Nothing else changed: no token file, no new rule, the legacy sheets stay unlayered.
- **Runs.** Each copy ran on port 8014 with `run-app.sh` against the audit board; `node tools/sweep.mjs cascade-probe --port 8014` (and `cascade-control` for the control, kept at `$QBR/cascade-probe/control-sweep`) captured every manifest row, 118 captures (59 pages by two User-Agents: desktop 1440, Pixel 7), plus the reflow checks. The probe and control apps were stopped afterwards (nothing listens on 8014).
- **Diffs.** `diff.py` at tolerance 24 with the masks of the `before` record (`$QBR/audit/shell/cascade-diff.py`). Three comparisons: probe against `before` (the plan's reference), control against `before`, and probe against control. Control against `before` differs on 59 captures, all mobile, at most 0.52 percent of pixels: the Cinzel glyph-edge difference plan 94-11 already accepted (desktop is exactly 0), so probe against control isolates the layering effect. A fourth pass masks the bar (top 110px desktop, 190px mobile) and footer (bottom 110px / 200px) to separate chrome changes from page-content changes.
- **Attribution.** A computed-style snapshot of every element on every page (`color`, `background-color`, `display`, padding, margin, `font-weight`, `font-size`, `border-top-color`, `opacity`, `white-space`, `position`, `text-shadow`) was taken on both copies and compared element by element (`style-snapshot.mjs`, `snap-analyze.py`); each changed value is grouped by selector. Properties not sampled (`box-shadow`, `background-image`, `border-radius`, `transition`) can hide further changes; the overlays below show none that the table lacks on the ones viewed.
- **Reading the overlays.** The vs-control overlay of a capture is `$QBR/cascade-probe/vs-control-overlay/{ua}/{id}.png` (red marks differing pixels); the control and probe captures are `$QBR/cascade-probe/control-sweep/{ua}/{id}.png` and `$QBR/cascade-probe/{ua}/{id}.png`; side-by-side crops of the worst pages are in `$QBR/audit/shell/shots/cascade/`.

### Result in one paragraph

Layering Bootstrap alone changes every page: all 118 captures differ from `before` (the bar and footer carry the change), 81 differ outside the bar and footer, 63 by more than 0.1 percent of pixels, 37 are identical in their content. The change is not neutral in either direction. It repairs the cream-on-cream figures on the Events overview rows (the legacy `.modern-card span` rule loses to Bootstrap's `!important` muted colour, so the dark grey is read on paper at 5.97:1 instead of 1.00:1) and it breaks everything that is muted, danger-coloured or `bg-dark` on glass: grey muted text on glass measures 1.57 to 2.24:1, `.text-danger` 1.21 to 1.93:1, and the footer turns into an opaque near-black bar with near-black text at 1.22:1. This confirms the contract's rule: re-theme Bootstrap through its variables in the same commit that retires the matching legacy rule, never by out-specifying.

### (a) Legacy rule families the foundation retires

File:line of each family in the current files (the replacement is the one in the cascade-architecture table of the UI-SPEC).

| Family | Where | Replacement |
|--------|-------|-------------|
| Element-enumerated cream colour: `h1`-`h6` (`color #F4E4BC !important`) and `p, li, span, small` (same) inside `.modern-card` | `modern-card.css:78-90`, `modern-card.css:91-100` | Surfaces set `color`; descendants inherit |
| `.modern-card .form-label`, `.form-text`, `.form-text a`; `.modern-card .text-muted` and `.text-danger` repairs | `modern-card.css:102-117`, `121-141`, `142-145` | token-driven classes (`--qb-fg-muted`, `--qb-danger-text`) |
| `.modern-card .table` double definition (cream cells then parchment cells) | `modern-card.css:252-272` and `274-295` | one `.table` in `components.css` |
| Global element colour rules in `site.css`: `h1`-`h6` (586-592), `.form-label` (594-600), `.form-text` (602-608), `.card-body` (626-630), `p, li, span, small` (632-637), `.text-muted` (639-643) | `site.css:586-643` | `--qb-fg-on-board` inherited from `body` |
| `div { background: transparent }` and `.container, .container-fluid { background: none !important }` | `site.css:473-476`, `site.css:399-401` | deleted |
| Bootstrap component re-themes: 54 rules in `site.css` (lines 40-1701), 32 in `modern-card.css` (32-340), 11 in `mobile.css` (2-135) by the component-class regex used here (the contract counts 213 with a broader rule; the full list is `$QBR/audit/shell/css-retheme.json`) | `site.css`, `modern-card.css`, `mobile.css` | one set in `components.css` through `--bs-btn-*` and the other component variables |
| Navbar and brand rules with `!important`, repeated in two files: `.navbar:not(footer)` (`site.css:501-512`), `footer.navbar` (`site.css:514-517`), `.navbar-brand` (`site.css:524-543`), `.navbar-nav .nav-link` (`site.css:545-570`), `.mobile-layout .navbar` (`mobile.css:80-87`), `.navbar-brand` (`mobile.css:67-77`), `.offcanvas*` (`mobile.css:89-120`) | `site.css`, `mobile.css` | one `shell.css` |
| Duplicated backdrop `body::before` | `site.css:381-395` and `mobile.css:22-35` | one in `base.css` |

### (b) Legacy `!important` colour or background rules that re-colour a Bootstrap utility

Regex used (`$QBR/audit/shell/css-fights.py`, run over every `QuestBoard.Service/wwwroot/css/*.css` with comments blanked and `@media` blocks descended): a style rule whose selector matches `\.(text|bg)-(muted|primary|secondary|success|danger|warning|info|light|dark|white|black|body|body-secondary|body-tertiary|black-50|white-50)\b` and whose declarations contain `color`, `background`, `background-color` or `border-color` followed by `!important`. It finds **45** rules, matching the research count of 45 with a loose regex: **28** name `.text-muted` (the contract's "28 of them `.text-muted`") and 17 name another utility. The contract's figure of 41 was not reproduced with its own regex; the 45 here include three `.bg-dark` background rules (`mobile.css:93`, `site.css:305`, `site.css:311`) and the `.item-name.text-light` rule present in two files, which are the likeliest source of the difference.

| # | File:line | Property | Selector |
|---|-----------|----------|----------|
| 1 | `account.mobile.css:48` | color | `.account-card-mobile .text-muted` |
| 2 | `agenda.css:45` | color | `.agenda-row .text-muted` |
| 3 | `calendar.mobile.css:14` | color | `.agenda-card-mobile .text-muted` |
| 4 | `character-detail.mobile.css:55` | color | `.character-detail-card .text-muted, .character-detail-card small, .character-info-value` |
| 5 | `character-form.mobile.css:37` | color | `.character-form-card .text-muted` |
| 6 | `characters.mobile.css:107` | color | `.character-section-card .text-muted` |
| 7 | `contact-detail.mobile.css:55` | color | `.contact-detail-card .text-muted, .contact-detail-card small, .contact-info-value` |
| 8 | `contact-detail.mobile.css:107` | color | `.contact-detail-card .note-item small, .contact-detail-card .note-item .text-muted` |
| 9 | `contact-form.mobile.css:46` | color | `.contact-form-card .text-muted` |
| 10 | `contacts.mobile.css:106` | color | `.contact-section-card .text-muted` |
| 11 | `dm-manage.mobile.css:44` | color | `.dm-manage-section-card small, .dm-manage-section-card .text-muted` |
| 12 | `home.mobile.css:46` | color | `.quest-card-mobile .text-muted` |
| 13 | `mobile.css:53` | color | `.text-muted` |
| 14 | `mobile.css:93` | background | `.offcanvas-header.bg-dark` |
| 15 | `modern-card.css:102` | color | `.modern-card .text-muted` |
| 16 | `modern-card.css:114` | color | `.modern-card .text-danger` |
| 17 | `platform-group.mobile.css:39` | color | `.platform-group-card-mobile small, .platform-group-card-mobile .form-text, .platform-gr...` |
| 18 | `platform-group.mobile.css:52` | color | `.platform-group-card-mobile .fa-3x.text-muted` |
| 19 | `platform-group.mobile.css:84` | color | `.platform-group-card-mobile [role="alert"] .text-danger` |
| 20 | `platform-users.mobile.css:32` | color | `.platform-users-card-mobile small, .platform-users-card-mobile .text-muted` |
| 21 | `platform-users.mobile.css:44` | color | `.platform-users-card-mobile .fa-3x.text-muted` |
| 22 | `players.mobile.css:73` | color | `.players-section-card .text-muted` |
| 23 | `quest-log-detail.mobile.css:52` | color | `.quest-log-detail-main-card .text-muted, .quest-log-detail-main-card small` |
| 24 | `quest-log.mobile.css:33` | color | `.quest-log-item small, .quest-log-item .text-muted` |
| 25 | `quests.css:805` | color | `.quest-log-details .text-muted` |
| 26 | `quests.css:809` | color | `.quest-log-details .text-white` |
| 27 | `quests.mobile.css:26` | color | `.participant-list-mobile small, .participant-list-mobile .text-muted` |
| 28 | `quests.mobile.css:67` | color | `.quest-header-card-mobile small, .quest-header-card-mobile .text-muted` |
| 29 | `shop.css:119` | color | `.item-name.text-light` |
| 30 | `shop.css:468` | color | `.purchase-details .text-muted` |
| 31 | `shop.css:473` | color | `.purchase-details .text-warning` |
| 32 | `shop.css:482` | color | `.purchase-details .text-success` |
| 33 | `shop.css:565` | color | `#denyModal .form-label .text-danger` |
| 34 | `shop.css:629` | color | `#addCharacterModal .form-label .text-danger` |
| 35 | `shop.mobile.css:101` | color | `.shop-item-card-mobile .text-muted, .shop-header-card .text-muted, .shop-empty-state .t...` |
| 36 | `site.css:160` | color | `.item-name.text-light` |
| 37 | `site.css:305` | background-color | `.form-control.bg-dark` |
| 38 | `site.css:311` | background-color | `.form-control.bg-dark:focus` |
| 39 | `site.css:639` | color | `.text-muted` |
| 40 | `site.css:1292` | color | `.purchase-details .text-muted` |
| 41 | `site.css:1297` | color | `.purchase-details .text-warning` |
| 42 | `site.css:1307` | color | `.purchase-item small.text-muted` |
| 43 | `site.css:1395` | color | `.purchase-details .text-success` |
| 44 | `site.css:1584` | color | `.purchase-item.sold-returned .purchase-details .text-warning` |
| 45 | `site.css:1589` | color | `.purchase-item.sold-returned .purchase-details .text-success` |

### (c) Rules that win only because they are unlayered

Found from the computed-style diff (a rule that previously lost on specificity or source order to a Bootstrap rule and now wins because every layered rule loses to every unlayered rule):

1. **`.navbar-nav .nav-link { padding: .4rem .8rem; margin: 0 .25rem }`** (`site.css:545-556`). Before, Bootstrap's `.navbar-expand-lg .navbar-nav .nav-link` (higher specificity) set the horizontal padding to 8px; unlayered, the legacy rule now sets 12.8px. 291 links on 51 captures widen by 9.6px each, and the bar wraps (icon over label, "Quest Log" on two lines) on the 1440px Dungeon Master capture (`$QBR/cascade-probe/desktop/Quest-Manage.png` against `$QBR/cascade-probe/control-sweep/desktop/Quest-Manage.png`; the bar grows from 64px to about 75px).
2. **`p, li, span, small { color: #1a0f08 }`** (`site.css:632-637`). Before, Bootstrap's `.navbar-dark .navbar-text` (0,2,0) beat the bare-element rule on the footer text; now the bare-element rule wins, so the footer text turns `#1a0f08` on the layered `bg-dark` fill: 1.22:1 (A-shell-002 becomes worse). 58 captures.
3. The same mechanism applies to every other bare-element colour rule in `site.css` (8 rules) and `modern-card.css` (7 rules): they now beat any non-important Bootstrap colour on their elements. The computed-style diff shows no further visible instance beyond items 1 and 2 and the `!important` inversions in (d).

### (d) Pages whose rendering changed

All 118 captures differ from `before`. By cause (captures affected; one capture can have several):

| Cause | Captures | Effect |
|-------|----------|--------|
| Bootstrap `.bg-dark` (`background-color: ... !important`, layer vendor) beats the legacy `footer.navbar { background: transparent !important }` and the offcanvas header colour | footer on 116, desktop and mobile bar fill on 118, offcanvas header on 58 | Footer becomes an opaque `#212529` band; bar darker under its 50 percent gradient; mobile offcanvas header loses its brown |
| (c) item 2: bare-element `span` colour beats `.navbar-text` | 58 | footer text `#1a0f08` on `#212529`: 1.22:1 |
| (c) item 1: legacy nav-link padding wins | 58 | nav links wider; the bar wraps on the 1440px Dungeon Master capture |
| Bootstrap `.text-danger` (`#dc3545`, `!important`) beats the legacy `.text-danger` cream and `#ff6b6b` rules | 32 captures plus the Dungeon Master and Admin menu toggles on 28 | validation and refusal lines go from 1.97:1 to 1.21 to 1.93:1 on glass (3.59:1 on paper); the red menu toggles are 1.05 to 2.19:1 on the bar (A-shell-021 is no longer latent) |
| Bootstrap `.text-muted` (`rgba(33,37,41,.75)`, `!important`) beats the 28 legacy `.text-muted` rules | cream to grey on 59 captures, dark to grey on 20 | on glass: 1.57 to 2.24:1 at the three boards (the old cream at 70 percent alpha was 2.99:1 at `B-max`, also failing); on paper 5.97:1 (the old cream-on-paper was 1.00:1) |
| Other utilities win over legacy `!important` rules: `.text-light`, `.text-info`, `.text-white`, `.text-success`, `.text-primary`, `.text-secondary`, `.badge .text-dark`, `.p-2`, `.fs-6` | 8, 4, 1, 1, 1, 1, 6, 4, 4 | colour shifts to Bootstrap values (for example `text-success` `#198754` on glass 1.21:1), card padding 12px to 8px, CR badge 17.6px to 16px |

Pages that need a human look first, by the share of pixels that changed outside the bar and footer (S1 candidates are the muted and danger lines on glass): Account/Profile on mobile (2.73 percent: the empty-state sentence and icon become grey on glass, `$QBR/audit/shell/shots/cascade/mobile-Account-Profile.png`), Calendar/Index on desktop (1.75 percent: the legend hint becomes grey on glass), Events/Index on desktop (1.65 percent: the attendance counts become readable on paper, the legend hint turns grey), Quest Index, Admin Quests, Quest Details and Events Index on mobile (1.3 to 1.5 percent), Quest Log details on desktop (1.05 percent). The foundation must land the variable re-theme for `.text-muted`, `.text-danger` and `.bg-dark` and retire the matching rules in one step per layout; no page may be merged with the layer in place until its before-and-after readability gate passes (D-08).

Per-capture figures follow, widest content change first. Columns: share of differing pixels against `before`; against the control (isolates layering); content only (bar and footer masked); chrome causes (B bar fill, F footer, N nav links wider, D Dungeon Master and Admin toggles red, O offcanvas header) and content causes with the number of elements.

| Page | UA | Differing, vs `before` | vs control | Content only | Chrome causes | Content causes (elements) |
|------|----|-----------------------|-----------|--------------|---------------|---------------------------|
| Account-Profile | mobile | 3.22% | 3.04% | 2.73% | BFO | muted cream>gray x14 |
| Calendar-Index | desktop | 9.13% | 9.13% | 1.75% | BFN | muted cream>gray x63, p-2 padding x8 |
| Calendar-Index--crowded | desktop | 9.13% | 9.13% | 1.75% | BFN | muted cream>gray x63, p-2 padding x8 |
| Events-Index | desktop | 9.02% | 9.02% | 1.65% | BFN | muted cream>gray x68, p-2 padding x8, badge text-dark x4 |
| Quest-Index | mobile | 3.01% | 2.90% | 1.54% | BDFO | muted cream>gray x60 |
| Admin-Quests | mobile | 1.62% | 1.55% | 1.53% | BFO | muted cream>gray x60 |
| Quest-Details | mobile | 1.77% | 1.56% | 1.40% | BDFO | muted cream>gray x61 |
| Events-Index | mobile | 2.96% | 2.85% | 1.29% | BFO | muted cream>gray x14, p-2 padding x8, badge text-dark x4 |
| QuestLog-Details | desktop | 8.53% | 8.53% | 1.05% | BFN | muted cream>gray x14, text-white x10, colour utility x10, fs-6 size x8 |
| ShopManagement-Index | desktop | 10.77% | 10.77% | 0.88% | BDFN | muted cream>gray x60, text-danger x29, muted dark>gray x2, text-primary x1, text-secondary x1 |
| Events-Details | mobile | 1.17% | 0.97% | 0.81% | BFO | muted cream>gray x10 |
| Home-Index | mobile | 1.17% | 0.96% | 0.80% | BFO | muted dark>gray x1 |
| QuestLog-Index | mobile | 1.50% | 0.96% | 0.80% | BDFO | muted cream>gray x60, muted dark>gray x1, colour utility x1 |
| Account-Profile | desktop | 8.92% | 8.92% | 0.79% | BFN | muted cream>gray x14 |
| GroupPicker-Index | mobile | 1.92% | 1.67% | 0.67% | B | muted cream>gray x14 |
| ShopManagement-Edit | mobile | 1.85% | 1.76% | 0.67% | BFO | muted cream>gray x60, text-info x5, colour utility x1 |
| Contacts-Details | mobile | 1.81% | 1.66% | 0.59% | BFO | muted cream>gray x9, text-danger x7 |
| Agenda-Index | mobile | 1.98% | 1.88% | 0.55% | BFO | muted dark>gray x15, muted cream>gray x2 |
| Quest-Create | desktop | 8.23% | 8.23% | 0.53% | BDFN | text-danger x62, muted cream>gray x6 |
| Calendar-Index | mobile | 0.65% | 0.53% | 0.52% | BFO | muted cream>gray x64 |
| Calendar-Index--crowded | mobile | 0.65% | 0.53% | 0.52% | BFO | muted cream>gray x64 |
| Contacts-Details | desktop | 7.95% | 7.95% | 0.50% | BFN | text-danger x33, muted cream>gray x23 |
| Shop-Index--empty-category | mobile | 0.85% | 0.64% | 0.48% | BFO | muted cream>gray x7 |
| Quest-Edit | desktop | 8.61% | 8.61% | 0.44% | BDFN | text-danger x62, muted cream>gray x6 |
| Platform-Group-Edit | mobile | 1.50% | 1.18% | 0.43% | BFO | muted cream>gray x1 |
| DungeonMaster-Profile | desktop | 7.35% | 7.35% | 0.36% | BDFN | muted cream>gray x15, muted dark>gray x2 |
| Platform-Group-Create | mobile | 0.94% | 0.51% | 0.35% | BFO | muted cream>gray x15 |
| Quest-Details--voting | mobile | 0.72% | 0.51% | 0.35% | BDFO | muted cream>gray x60, muted dark>gray x2 |
| Shared-AccessDenied | mobile | 0.69% | 0.49% | 0.34% | BFO | muted cream>gray x14 |
| QuestLog-Details | mobile | 1.81% | 1.68% | 0.32% | BFO | muted cream>gray x3 |
| GroupPicker-Index | desktop | 1.59% | 1.59% | 0.31% | B | muted cream>gray x14 |
| Characters-Details | desktop | 8.47% | 8.47% | 0.31% | BFN | muted cream>gray x15 |
| Quest-CreateFollowUp | desktop | 7.88% | 7.88% | 0.30% | BDFN | text-danger x62, muted cream>gray x20 |
| ShopManagement-Index | mobile | 1.09% | 0.92% | 0.29% | BFO | muted cream>gray x60, muted dark>gray x2 |
| Events-Details | desktop | 8.42% | 8.42% | 0.29% | BFN | muted cream>gray x10 |
| Shop-Index | desktop | 6.53% | 6.53% | 0.29% | BFN | text-light x3 |
| Quest-CreateFollowUp | mobile | 1.80% | 1.70% | 0.26% | BFO | not isolated by the sampled properties |
| Admin-Quests | desktop | 7.93% | 7.93% | 0.26% | BDFN | text-danger x29 |
| ShopManagement-Create | mobile | 1.70% | 1.60% | 0.25% | BFO | text-info x4 |
| Contacts-Create | mobile | 1.59% | 1.45% | 0.25% | BFO | muted cream>gray x15, muted dark>gray x10, text-danger x7 |
| Contacts-Edit | mobile | 1.59% | 1.45% | 0.25% | BFO | muted cream>gray x15, muted dark>gray x10, text-danger x7 |
| Characters-Create | desktop | 6.35% | 6.35% | 0.24% | BFN | text-danger x33, muted cream>gray x17, muted dark>gray x10 |
| Characters-Edit | desktop | 6.34% | 6.34% | 0.24% | BFN | text-danger x33, muted cream>gray x17, muted dark>gray x10 |
| DungeonMaster-EditProfile | desktop | 11.25% | 11.25% | 0.22% | BDFN | text-danger x29, muted cream>gray x17, muted dark>gray x10 |
| Platform-LinkedBoards-Index | desktop | 3.79% | 3.79% | 0.22% | BFN | muted cream>gray x14, text-light x7, badge text-dark x3 |
| Admin-EditUser | desktop | 11.23% | 11.23% | 0.21% | BDFN | text-danger x29, muted cream>gray x14 |
| Contacts-Edit | desktop | 10.17% | 10.17% | 0.20% | BDFN | text-danger x62, muted cream>gray x15, muted dark>gray x10 |
| Characters-Index | desktop | 7.01% | 7.01% | 0.20% | BFN | muted cream>gray x14 |
| Contacts-Create | desktop | 10.16% | 10.16% | 0.19% | BDFN | text-danger x62, muted cream>gray x15, muted dark>gray x10 |
| Platform-Users-Index | mobile | 0.23% | 0.19% | 0.18% | BFO | not isolated by the sampled properties |
| Quest-Manage | desktop | 11.41% | 11.41% | 0.18% | BDFN | text-danger x29, fs-6 size x8, colour utility x5, text-success x1 |
| DungeonMaster-EditProfile | mobile | 0.54% | 0.33% | 0.18% | BFO | muted dark>gray x10, muted cream>gray x2, colour utility x1 |
| Shop-Details | desktop | 6.58% | 6.58% | 0.17% | BFN | muted cream>gray x74, badge text-dark x4, colour utility x1 |
| Platform-Users-Index | desktop | 2.49% | 2.49% | 0.15% | BFN | text-light x7 |
| Platform-Group-Edit | desktop | 8.11% | 8.11% | 0.14% | BFN | text-light x7, muted cream>gray x3 |
| ShopManagement-Edit | desktop | 8.27% | 8.27% | 0.14% | BDFN | text-danger x62, muted cream>gray x60, text-info x4 |
| Quest-Details--voting | desktop | 5.02% | 5.02% | 0.14% | BFN | fs-6 size x8, muted cream>gray x1 |
| Admin-Users | mobile | 0.61% | 0.43% | 0.13% | BFO | not isolated by the sampled properties |
| Shared-AccessDenied | desktop | 8.25% | 8.25% | 0.12% | BFN | muted cream>gray x14 |
| ShopManagement-Create | desktop | 8.46% | 8.46% | 0.12% | BDFN | text-danger x62, text-info x4 |
| Quest-Manage | mobile | 0.49% | 0.28% | 0.12% | BFO | muted cream>gray x2 |
| Characters-Details | mobile | 1.26% | 1.11% | 0.12% | BFO | muted cream>gray x7 |
| QuestLog-EditRecap | desktop | 11.28% | 11.28% | 0.11% | BDFN | text-danger x29, muted cream>gray x15 |
| ContactCategoryManagement-Manage | mobile | 0.46% | 0.25% | 0.09% | BFO | muted cream>gray x60 |
| Platform-Group-Create | desktop | 7.96% | 7.96% | 0.09% | BFN | muted cream>gray x15, text-light x7 |
| Account-Edit | desktop | 8.22% | 8.22% | 0.09% | BFN | muted cream>gray x14 |
| Platform-Group-Index | desktop | 7.93% | 7.93% | 0.07% | BFN | text-light x7, badge text-dark x3 |
| Admin-Users | desktop | 11.32% | 11.32% | 0.06% | BDFN | text-danger x29 |
| Series-Details | mobile | 1.42% | 1.29% | 0.04% | BFO | muted dark>gray x2 |
| Shop-Index--empty-category | desktop | 6.28% | 6.28% | 0.03% | BFN | not isolated by the sampled properties |
| Quest-Details | desktop | 8.22% | 8.22% | 0.03% | BFN | fs-6 size x8, colour utility x2 |
| Platform-Group-Members | desktop | 1.99% | 1.99% | 0.02% | BDFN | text-light x7, badge text-dark x4 |
| Series-Details | desktop | 7.01% | 7.01% | 0.01% | BFN | muted dark>gray x2 |
| Events-Edit | mobile | 0.44% | 0.24% | 0.01% | BFO | text-danger x33 |
| Events-Create | mobile | 0.48% | 0.30% | 0.01% | BFO | text-danger x33, muted cream>gray x2 |
| Platform-Group-Delete | desktop | 7.87% | 7.87% | 0.01% | BFN | text-light x7 |
| Characters-Create | mobile | 1.04% | 0.95% | 0.01% | BFO | muted dark>gray x10, text-danger x7 |
| Characters-Edit | mobile | 0.98% | 0.89% | 0.01% | BFO | muted dark>gray x10, text-danger x7 |
| Admin-ResetPassword | desktop | 11.16% | 11.16% | 0.00% | BDFN | text-danger x29 |
| Events-Create | desktop | 11.24% | 11.24% | 0.00% | BDFN | text-danger x62, muted cream>gray x2 |
| Events-Edit | desktop | 11.24% | 11.24% | 0.00% | BDFN | text-danger x62 |
| Account-ChangePassword | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Account-ChangePassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-Edit | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-ForgotPassword | desktop | 7.51% | 7.51% | 0.00% | BFN | none |
| Account-ForgotPassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Account-Login | desktop | 7.51% | 7.51% | 0.00% | BFN | none |
| Account-Login | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-CreateUser | desktop | 11.01% | 11.01% | 0.00% | BDFN | text-danger x29 |
| Admin-CreateUser | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-EditUser | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-EmailStats | desktop | 11.25% | 11.25% | 0.00% | BDFN | text-danger x29 |
| Admin-EmailStats | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Admin-ResetPassword | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Agenda-Index | desktop | 6.37% | 6.37% | 0.00% | BFN | muted dark>gray x20, muted cream>gray x2 |
| Characters-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| ContactCategoryManagement-Edit | desktop | 11.13% | 11.13% | 0.00% | BDFN | text-danger x29 |
| ContactCategoryManagement-Edit | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| ContactCategoryManagement-Manage | desktop | 11.23% | 11.23% | 0.00% | BDFN | text-danger x29 |
| Contacts-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Contacts-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| DungeonMaster-Profile | mobile | 1.74% | 1.49% | 0.00% | BFO | none |
| Home-Index | desktop | 7.50% | 7.50% | 0.00% | BFN | none |
| Move-Index | desktop | 11.25% | 11.25% | 0.00% | BDFN | text-danger x29 |
| Move-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Platform-Group-Delete | mobile | 0.59% | 0.16% | 0.00% | BFO | none |
| Platform-Group-Index | mobile | 1.69% | 1.47% | 0.00% | BFO | none |
| Platform-Group-Members | mobile | 0.05% | 0.00% | 0.00% | BFO | none |
| Platform-LinkedBoards-Index | mobile | 0.28% | 0.12% | 0.00% | BFO | none |
| Players-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Players-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Quest-Create | mobile | 1.52% | 1.39% | 0.00% | BFO | none |
| Quest-Edit | mobile | 1.33% | 1.19% | 0.00% | BFO | none |
| Quest-Index | desktop | 4.06% | 4.06% | 0.00% | BFN | none |
| QuestLog-EditRecap | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| QuestLog-Index | desktop | 8.13% | 8.13% | 0.00% | BFN | none |
| Shop-Details | mobile | 0.37% | 0.16% | 0.00% | BFO | none |
| Shop-Index | mobile | 0.37% | 0.16% | 0.00% | BFO | none |

## Operator questions for sign-off

One consolidated list for sign-off: **138 questions** (94 twin-content, 16 merge, 17 relayout, 11 other decisions). Every question is numbered Q-01 onward, carries the ID of the area report it came from and its finding, and offers only the answers shown. Removal is never offered: no answer deletes a field, link, button or capability. Until a question is answered the foundation and the area phases keep today's text, structure and behaviour (D-14); nothing is added or removed without the answer. **All 138 were answered at the G1 sign-off on 2026-11-01** (the Answer line under each question); see the sign-off record at the end of this section.

Wording follows the copy contract. Twin-content questions: `{View}: the {desktop or mobile} view has {item} and the other does not. Is this deliberate?` with exactly `[Deliberate, record it]` and `[Add it to the other view]`. Merge questions: `[Approve merge]` and `[Keep the twin]`. Relayout questions: `[Approve relayout in principle]` and `[Keep today's layout]`, each ending with the line that an approval is in principle only. The final group is decisions that are neither twin, merge nor relayout; each has two answers and none removes anything.

Two duplicates in the area reports were asked once: the GroupPicker footer question (Q-shell-03 and Q-account-10) is Q-03, and OQ-QM-4 and OQ-QM-5 of the Quests report carry identical wording and are Q-33.

### Seeded and shell questions (Q-01 to Q-06)

Q-01 to Q-03 are the questions the contract seeds (footer text, brand text, picker footer); Q-04 to Q-06 were found by the layout comparison. Source: `94-AUDIT-shell.md`.

- **Q-01** (Q-shell-01, finding A-shell-024): Footer: the desktop view has the footer text "D&D Quest Board v{version}. Created by Theun Schut." and the mobile view has "D&D QB v{version}." Is the shortening deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (phone width; the shorter footer is on purpose (footer contrast is fixed separately)).
- **Q-02** (Q-shell-02, finding A-shell-025): Navbar brand: the desktop view has the brand text "D&D Quest Board" and the mobile view has "Quest Board". Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the brand has to fit the phone bar at the 20px Heading step).
- **Q-03** (Q-shell-03, finding A-shell-026) (also asked as Q-account-10 in the Account report; asked once): GroupPicker: the main and Platform layouts have a footer and the board picker layout does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the picker is the page before you are inside a board; a footer adds nothing).
- **Q-04** (Q-shell-04, finding A-shell-027): Main menu: the desktop view has the Background Jobs and Email Stats entries for a SuperAdmin and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a SuperAdmin on a phone cannot reach Background Jobs or Email Stats from the menu).
- **Q-05** (Q-shell-05, finding A-shell-028): Main menu: the desktop view has the "Admin" and "Dungeon Master" group labels over their menu items and the mobile view lists the same items flat with no labels. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (group labels give the flat phone menu structure for screen readers; two lines).
- **Q-06** (Q-shell-06, finding A-shell-029): Main menu: the desktop view has a "Profile" entry in the user menu and the mobile view shows the user's name as the link text with no "Profile" word. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the user name is the link text, so the link is named; nothing is missing).

### Twin content: Quests and Quest Log (Q-07 to Q-39)

- **Q-07** (OQ-QD-1, finding A-quests-067): Quest/Details: the desktop view has the Building Access badge in the Quest Summary sidebar and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (players need to know about key access on a phone too).
- **Q-08** (OQ-QD-2, finding A-quests-068): Quest/Details: the desktop view has the Created date line and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (low-value line; skip on a phone).
- **Q-09** (OQ-QD-3, finding A-quests-069): Quest/Details: the desktop view has the "Continues in" and "Continues from" links between a quest and its follow-up and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (follow-up links are the only way to walk between a quest and its follow-up).
- **Q-10** (OQ-QD-4, finding A-quests-070): Quest/Details: the desktop view has the Current Signups list (names and signup dates by role) on open and finalized quests and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (phone shows signups only once finalized; players want to see who has joined).
- **Q-11** (OQ-QD-5, finding A-quests-071): Quest/Details: the desktop view has the signup count lines (Players Signed Up, Selected Players, Total Signups) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (one count line per type; answers "is there room").
- **Q-12** (OQ-QD-6, finding A-quests-072): Quest/Details: the desktop view has the "Quest Finalized!" panel with the finalized date and time (mobile shows the date only inside the Join card) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the phone already shows the finalized date inside the Join card).
- **Q-13** (OQ-QD-7, finding A-quests-073): Quest/Details: the desktop view has the "You're signed up!" panel stating selected or on the waitlist and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (signup state (selected or waitlisted) should not depend on the device).
- **Q-14** (OQ-QD-8, finding A-quests-074): Quest/Details: the desktop view has the "Select Character (Optional)" field on the open-quest signup form and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a phone player cannot attach a character when signing up: a capability gap). Operator decision: add the character picker to the mobile signup view.
- **Q-15** (OQ-QD-9, finding A-quests-075): Quest/Details: the desktop view has the "Select Your Role" field (Player, Assistant DM, Spectator) on the open-quest signup form and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a phone player cannot sign up as Assistant DM or Spectator: a capability gap). Operator decision: add the role picker (Player, Assistant DM, Spectator) to the mobile signup view.
- **Q-16** (OQ-QD-10, finding A-quests-076): Quest/Details: the desktop view has the Vote column for selected participants on a finalized quest and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a table column; phone participant lists are names only by design (low confidence)).
- **Q-17** (OQ-QD-11, finding A-quests-077): Quest/Details: the desktop view has the character portrait beside each participant's character name and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a portrait beside the name is decoration that costs width).
- **Q-18** (OQ-QD-12, finding A-quests-078): Quest/Details: the desktop view has the "Sign In Required" notice with sign-in and register links for a signed-out visitor and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a signed-out phone visitor sees nothing: a dead end).
- **Q-19** (OQ-QD-13, finding A-quests-079): Quest/Details: the mobile view has the "You" marker and highlighted row for the viewer's own entry in the participant lists and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the "You" marker helps on desktop too and costs nothing).
- **Q-20** (OQ-QD-14, finding A-quests-080): Quest/Details: the mobile view has the Open / Closed status badge on Campaign boards (the desktop summary hides Status on Campaign boards) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (desktop hides Status on Campaign boards on purpose; leave it (low confidence)).
- **Q-21** (OQ-QI-1, finding A-quests-081): Quest/Index: the mobile view has an empty state ("No Quests Available") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (an empty board is blank on desktop; both mockup directions draw an empty state). Consistent with G1-R1 (quest-board poster card information layout): the empty state is drawn with the card relayout in the Quests phase.
- **Q-22** (OQ-QI-2, finding A-quests-082): Quest/Index: the desktop view has the adventurer count lines (Selected Adventurers, Adventurers signed up) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (spots left drive the sign-up choice; one line per card). Consistent with G1-R1: the spots-left line is part of the information layout redrawn on the poster in the Quests phase.
- **Q-23** (OQ-QI-3, finding A-quests-083): Quest/Index: the mobile view has the text status badge (Open, Finalized, Done, Closed) and the "Signed up" badge, which desktop shows only as wax-seal images and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (both directions show a word with the status, never a seal or colour alone). Consistent with G1-R1: the status word is part of the poster information layout redrawn in the Quests phase (the poster keeps its artwork).
- **Q-24** (OQ-QC-1, finding A-quests-084): Quest/Create: the desktop view has the "Quest Creation Tips" card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a tips card is clutter on a phone form).
- **Q-25** (OQ-QC-2, finding A-quests-085): Quest/Create: the desktop view has the helper text under Challenge Rating and under Proposed Dates (the Dungeon Master Session helper is shorter on mobile) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (helper text prevents bad input; Direction A draws it as visible hints).
- **Q-26** (OQ-QE-1, finding A-quests-086): Quest/Edit: the desktop view has the "Quest Editing Tips" card, including the sentence that tells a DM to use Open Quest to change the dates of a finalized quest and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (its one sentence on changing dates of a finalized quest is stated nowhere else).
- **Q-27** (OQ-QE-2, finding A-quests-087): Quest/Edit: the desktop view has the helper text under Challenge Rating and under Proposed Dates and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same reason as Q-25).
- **Q-28** (OQ-QE-3, finding A-quests-088): Quest/Edit: the desktop view has the per-date validation message under each proposed date and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (per-date validation is missing on the phone (WCAG 3.3.1)).
- **Q-29** (OQ-QF-1, finding A-quests-089): Quest/CreateFollowUp: the desktop view has the explanatory sentence in the Pre-Approved Players card ("Selected players from the original quest will be automatically added...") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (one sentence that stops a surprise auto-add).
- **Q-30** (OQ-QM-1, finding A-quests-090): Quest/Manage: the desktop view has the Quest Summary card (DM, Status, Building Access, follow-up links) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the DM knows their own quest; the card is desktop density).
- **Q-31** (OQ-QM-2, finding A-quests-091): Quest/Manage: the desktop view has the Quick Stats block (Best Date, players available) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (Best Date is what a DM needs to finalize, on a phone as much as at a desk).
- **Q-32** (OQ-QM-3, finding A-quests-092): Quest/Manage: the desktop view has the "Send again (bypasses duplicate check)" button that follows a sent reminder (it belongs to Send Reminder, which is desktop-only by decision; confirm it should follow that decision) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (belongs to Send Reminder, which is desktop-only by decision).
- **Q-33** (OQ-QM-4, finding A-quests-093) (OQ-QM-5 in the Quests report carries the identical wording; asked once): Quest/Manage: the desktop view has the voter names under Yes / Maybe / No for the finalized Selected Date (the open-state vote lists are commented as condensed on mobile) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the open-state lists are commented as condensed on mobile by design).
- **Q-34** (OQ-QM-6, finding A-quests-095): Quest/Manage: the desktop view has the Vote and Signup Time columns in the Selected Participants table and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (table columns; phone shows names only).
- **Q-35** (OQ-QM-7, finding A-quests-096): Quest/Manage: the desktop view has the admin "Remove player" button on the finalized Selected Participants rows (mobile has it only on the open-quest selection list) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (an admin can remove a player from the open list on a phone but not the finalized list).
- **Q-36** (OQ-LI-1, finding A-quests-097): QuestLog/Index: the desktop view has the Adventurers count in each log card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (log card density).
- **Q-37** (OQ-LI-2, finding A-quests-098): QuestLog/Index: the desktop view has the "Click to view details" cue in each log card footer and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] ("Click" means nothing on touch; the card is the tap target).
- **Q-38** (OQ-LI-3, finding A-quests-099): QuestLog/Index: the mobile view has the "Unknown DM" fallback when a quest has no DM and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (avoids a blank DM name on desktop).
- **Q-39** (OQ-MV-1, finding A-quests-100): Move/Index: the desktop view has the "This Quest" / "This Event" heading over the summary of the entry being moved and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a heading names the summary (heading outline)).

### Twin content: Events, Series, Calendar, Agenda (Q-40 to Q-49)

- **Q-40** (Q1, finding A-events-039): Calendar/Index: the desktop view has the Legend card (status colour key, linked-board list, "No building key" note and the "Click quests or events for details" hint) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the phone status code is colour-only with no key (finding A-events-039)). Calendar constraint G1-R2 applies: the legend must not push the desktop month below the fold at 1280 by 720 and up.
- **Q-41** (Q2, finding A-events-040): Calendar/Index: the desktop view has the page heading "Quest Calendar" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the phone Calendar has no heading at all; the contract gives phones a 20px page title). Calendar constraint G1-R2 applies on desktop: the existing heading stays within the fit-without-scroll target; the phone gains a 20px page title.
- **Q-42** (Q3, finding A-events-041): Calendar/Index: the mobile view has the "Also showing: ..." line naming the linked boards and the other does not (desktop names them only inside the legend, together with the sentence "Dashed entries belong to a linked board. You can open them only if you are a member of that board.", which mobile lacks). Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (desktop names the linked boards in its legend).
- **Q-43** (Q4, finding A-events-042): Calendar/Index: the desktop view has the quest's DM name on the chip and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a hover-only detail on desktop; phone rows have no hover). Consistent with G1-R2: on the compact entries the DM name belongs to the expanded entry (hover, focus, tap), not the one-line chip.
- **Q-44** (Q5, finding A-events-043): Calendar/Index: the desktop view has the viewer's own vote icon on a quest and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (your own vote at a glance is useful on a phone). Calendar constraint G1-R2: on desktop the compact entry carries the vote as a single marker with a non-colour cue; the phone agenda shows it per row.
- **Q-45** (Q6, finding A-events-044): Calendar/Index: the desktop view has the "No building key" warning and the finalized check on quests and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the key warning and the finalized check are status; the phone shows neither). Calendar constraint G1-R2: the key warning and the finalized check are single markers on the compact desktop entry, never a taller cell.
- **Q-46** (Q7, finding A-events-045): Calendar/Index: the desktop view has a today marker and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the phone list marks no today; both directions draw a today marker with a hidden word). Calendar constraint G1-R2: today stays the one marked cell in the compact month.
- **Q-47** (Q8, finding A-events-046): Calendar/Index: the mobile view has the empty state "Nothing This Month" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (an empty desktop month shows nothing; Direction A adds an empty-month note). Calendar constraint G1-R2: the desktop empty-month note must not push the grid below the fold.
- **Q-48** (Q9, finding A-events-047): Events/Index: the desktop view has the "Legend" heading with an always-visible legend and the other does not (mobile keeps the entries behind "What do these mean?" with no heading). Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the phone folds the legend behind "What do these mean?" today).
- **Q-49** (Q10, finding A-events-048): Events/Index: the desktop view has the "Attendance" column label and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the phone has no columns to label).

### Twin content: Characters, Contacts, DM profile, Players (Q-50 to Q-55)

- **Q-50** (OQ-1, finding A-characters-020): Characters/Index: the desktop view has the page heading "Characters" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (every page gets a heading; the contract gives phones a 20px page title).
- **Q-51** (OQ-2, finding A-characters-024): Characters/Create and Characters/Edit: the desktop view has the required marker on the Classes label and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a required field must be marked on the phone too (WCAG 3.3.2)).
- **Q-52** (OQ-3, finding A-characters-021): Contacts/Index: the desktop view has the page heading "Contacts" and the other does not (when categories exist). Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (every page gets a heading).
- **Q-53** (OQ-4, finding A-characters-022): Contacts/Index: the mobile view has the Filter Tags button (disabled when the board has no tags) with an "Active" badge and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (desktop shows the same filter row inline when tags exist; the button is how a phone does it).
- **Q-54** (OQ-5, finding A-characters-023): ContactCategoryManagement/Manage: the desktop view has the page heading "Manage Categories" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (every page gets a heading).
- **Q-55** (OQ-6, finding A-characters-025): DungeonMaster/Profile: the mobile view has the text "Date TBD" for a quest without a date and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] ("Date TBD" avoids a blank date on desktop).

### Twin content: Shop and Shop management (Q-56 to Q-75)

- **Q-56** (Q-shop-01, finding A-shop-028): Shop/Index: the desktop view has the My Transactions panel (purchase history with Return and Sell) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a phone player cannot reach purchase history, Return or Sell: a capability gap).
- **Q-57** (Q-shop-02, finding A-shop-049): Shop/Index: the desktop view has a description snippet and a stock label on each item card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (description snippet and stock label are card density (low confidence)).
- **Q-58** (Q-shop-03, finding A-shop-050): Shop/Index: the desktop view has Previous and Next links and a "Showing x-y of n items" line and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (without paging links items past the first page may be unreachable on a phone).
- **Q-59** (Q-shop-04, finding A-shop-051): Shop/Index: the desktop view has three separate empty states (search, filters, empty shop) with Clear buttons and the mobile view has one generic empty state. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (Clear buttons turn a dead end into a way out; one component).
- **Q-60** (Q-shop-05, finding A-shop-065): Shop/Index: the desktop view has the floating Mystical Merchant and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (decorative and not keyboard reachable (finding A-shop-034)). Not an override: the floating merchant button stays desktop-only. Operator finding G1-O2: it hovers over the footer, so the Shop phase gives it a new place (required fix).
- **Q-61** (Q-shop-06, finding A-shop-052): Shop/Index: the mobile view shows Clear Filters (and an "Active" badge) when a search is active and the desktop view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (seeing that a search is active, with a way to clear it, helps desktop too).
- **Q-62** (Q-shop-07, finding A-shop-060): Shop/Index: the desktop view says "Purchase" and "Login to Purchase" and the mobile view says "Buy" and "Login to Buy". Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] ("Buy" is shorter on a phone button; low stakes).
- **Q-63** (Q-shop-08, finding A-shop-029): Shop/Details: the desktop view has Sell to Shop (quantity and "You will receive") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (selling is a capability; a phone player cannot reach it today).
- **Q-64** (Q-shop-09, finding A-shop-053): Shop/Details: the desktop view has the Availability card (stock and availability dates), Item Properties and the View on D&D Beyond link and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (availability dates matter before buying; the D&D Beyond link is useful).
- **Q-65** (Q-shop-10, finding A-shop-054): Shop/Details: the desktop view has a draft-review notice, an out-of-stock alert and a sign-in prompt and the mobile view has plain short text and no sign-in branch; the mobile view also repeats Back to Shop inside the modal. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a signed-out phone visitor gets a purchase form with no sign-in branch).
- **Q-66** (Q-shop-11, finding A-shop-053): Shop/Details: the desktop view limits the Purchase quantity to the stock (`max`) and shows "Max: N available" and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (stops over-ordering at the form (the server validates either way)).
- **Q-67** (Q-shop-12, finding A-shop-055): ShopManagement/Index: the desktop view has three sections (Awaiting Review, My Items, All Other Published) and the mobile view has one merged list. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the phone merges three lists into one de-duplicated list on purpose).
- **Q-68** (Q-shop-13, finding A-shop-055): ShopManagement/Index: the desktop view has Type, Stock and Created By, a description teaser and the denial reason with date and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (list columns and teasers are desktop density; the denial reason is the one worth a second look).
- **Q-69** (Q-shop-14, finding A-shop-057): ShopManagement/Index: the desktop view has a Bulk Actions button and the mobile view has the modal but no button. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (Bulk Actions is a recorded placeholder; do not add a trigger to a stub).
- **Q-70** (Q-shop-15, finding A-shop-056): ShopManagement/Index: the desktop view has "Edit and Resubmit" for Denied items, Publish and Deny on the review list for every DM, and Delete on another DM's Draft; the mobile view limits Publish to Admin on Draft and has none of the others. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (it changes who sees Publish, Deny and Delete: leave until policies are confirmed).
- **Q-71** (Q-shop-16, finding A-shop-062): ShopManagement/Index: the desktop view labels items "Draft" and "Available" and the mobile view labels them "Pending Review" and "Active". Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (one status vocabulary on both views (decide which wording at the area phase)).
- **Q-72** (Q-shop-17, finding A-shop-058): ShopManagement/Index: the desktop view has the empty states "No Items Yet" (with a Create First Item button) and "No Other Items" and the mobile view has "No items yet" without a button. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (the phone empty state has no Create First Item button: a dead end).
- **Q-73** (Q-shop-18, finding A-shop-027): ShopManagement/Create: the desktop view has a Preview button (currently non-functional) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the Preview button is dead; do not copy a dead control to the phone).
- **Q-74** (Q-shop-19, finding A-shop-026): ShopManagement/Edit: the desktop view has a View Details button (currently a dead link) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (the View Details link answers 404; do not copy it to the phone).
- **Q-75** (Q-shop-21, finding A-shop-064): ShopManagement/Edit: the desktop view starts the price hint with "Pricing suggestion will appear here" and the mobile view with "Select a rarity to see pricing suggestions." Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (align the wording; the phone line is the clearer one).

### Twin content: Account and pickers (Q-76 to Q-84)

- **Q-76** (Q-account-1, finding A-account-007): Account/Login: the desktop view has a document title ("Log in") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (an empty document title on the phone fails WCAG 2.4.2; it also unblocks the Login merge).
- **Q-77** (Q-account-2, finding A-account-007): Account/ForgotPassword: the desktop view has a document title ("Forgot Password") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same: empty document title; unblocks the ForgotPassword merge).
- **Q-78** (Q-account-3, finding A-account-007): Account/SetPassword: the desktop view has a document title ("Set Your Password") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same: empty document title; unblocks the SetPassword merge).
- **Q-79** (Q-account-4, finding A-account-007): Account/ChangePassword: the desktop view has a document title ("Change Password") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same: empty document title; unblocks the ChangePassword merge).
- **Q-80** (Q-account-5, finding A-account-007): Account/Edit: the desktop view has a document title ("Edit Profile") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same: empty document title).
- **Q-81** (Q-account-6, finding A-account-007): Account/Profile: the desktop view has a document title ("Profile") and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (same: empty document title).
- **Q-82** (Q-account-7, finding A-account-018): Account/Edit: the desktop view has the sentence "Want to change your password?" above the Change Password button and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a lead-in sentence; the button label already says it).
- **Q-83** (Q-account-8, finding A-account-027) (the item exists on both sides in different words: "Add it to the other view" means aligning the other twin's wording to the answered one): Account/Profile: the desktop view has the Account Type word "Administrator" and the mobile view has "Admin". Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] ("Admin" is shorter on a phone; low stakes).
- **Q-84** (Q-account-9, finding A-account-028) (the item exists on both sides in different words: "Add it to the other view" means aligning the other twin's wording to the answered one): Account/Profile: the desktop view has "Has Building Key" and "No Building Key" and the mobile view has "Has building key" and "No building key". Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (align the casing; this is an inconsistency, not a decision).

### Twin content: Admin and Platform (Q-85 to Q-94)

- **Q-85** (Q1, finding A-admin-017): Admin/Users: the desktop view has the Resend Welcome Email button (unconfirmed users with an address) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (an admin on a phone cannot resend a welcome email today).
- **Q-86** (Q2, finding A-admin-018): Admin/Users: the desktop view has each user's email (mailto link, or "No email provided") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (email is phone-list density (low confidence)).
- **Q-87** (Q3, finding A-admin-016): Admin/Quests: the desktop view has the Campaign board status logic (Open or Closed from the quest's closed flag) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (Campaign status logic diverges between views; it is logic, not layout).
- **Q-88** (Q4, finding A-admin-024): Platform Group/Members: the desktop view has the "Current Members" section heading and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (heading outline).
- **Q-89** (Q5a, finding A-admin-023): Admin/Quests: the desktop view has the Description excerpt column and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a column the phone does not have room for).
- **Q-90** (Q5b, finding A-admin-023): Admin/Quests: the desktop view has the Dungeon Master email link and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a column the phone does not have room for).
- **Q-91** (Q6, finding A-admin-015): Admin/CreateUser and Admin/EditUser: the desktop view has a second page title (an `h1`) above the card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] (asked together with relayout R-1) [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (desktop shows a duplicate title that relayout Q-124 removes; do not copy the duplicate).
- **Q-92** (Q7, finding A-admin-025): Admin/EditUser: the desktop view has the "Password Management" heading and its explanatory sentence and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (a heading for the password section; one sentence).
- **Q-93** (Q8, finding A-admin-026): Admin/ResetPassword: the desktop view names the user in the card heading ("Reset Password for {user}") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Add it to the other view] (names whose password you are resetting, on a destructive action).
- **Q-94** (Q9, finding A-admin-027): Admin/Users and Admin/Quests: the mobile view has a second explanatory sentence in the empty state and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
  **Answer (G1, 2026-11-01):** [Deliberate, record it] (a second explanatory sentence in an empty state is not needed).

### Merge proposals (each costly to reverse)

Details (precedent, CSS-loading change, tests that change) are in the Merge proposals section.

- **Q-95** (M-quests-01, costly to reverse): QuestLog/EditRecap: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical inventory; the differences are wrapper classes; recap edit tests re-pinned in the Quests phase).
- **Q-96** (M-quests-02, costly to reverse): QuestLog/Details: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (layout-only: same sections and links in the same order; one mobile-view test re-pinned).
- **Q-97** (M-characters-1, costly to reverse): Characters/Details: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-98** (M-characters-2, costly to reverse): Contacts/Details: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-99** (M-characters-3, costly to reverse): Contacts/Create: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-100** (M-characters-4, costly to reverse): Contacts/Edit: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-101** (M-characters-5, costly to reverse): ContactCategoryManagement/Edit: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-102** (M-characters-6, costly to reverse): DungeonMaster/EditProfile: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (identical or layout-only pair; precedent Events/Create, Events/Details, Events/Edit and Series/Details; removes a twin that can drift).
- **Q-103** (M-account-1, costly to reverse): Account/Login: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (wrapper and heading only; needs Q-76 not answered Deliberate (the recommendation is A)).
- **Q-104** (M-account-2, costly to reverse): Account/ForgotPassword: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (wrapper and heading only; needs Q-77 not answered Deliberate).
- **Q-105** (M-account-3, costly to reverse): Account/SetPassword: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (wrapper and heading only; needs Q-78 not answered Deliberate; verify with a fresh mailed token).
- **Q-106** (M-account-4, costly to reverse): Account/ChangePassword: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (wrapper and heading only; needs Q-79 not answered Deliberate).
- **Q-107** (M-account-5, costly to reverse): GroupPicker/Index: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Approve merge], carried into its area phase (wrapper and heading only; lands together with Q-122 and the keyboard fix (A-account-031)).
- **Q-108** (M-admin-1, costly to reverse): Platform Group/Create: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Keep the twin], the twin stays (SuperAdmin-only page; the merge needs the Platform phone layout to gain card and form styles, small payoff for real risk (low confidence)).
- **Q-109** (M-admin-2, costly to reverse): Platform Group/Delete: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Keep the twin], the twin stays (SuperAdmin-only page; the merge needs the Platform phone layout to gain card and form styles, small payoff for real risk (low confidence)).
- **Q-110** (M-admin-3, costly to reverse): Platform Group/Edit: merge the desktop and mobile views into one view served under both layouts, with the CSS-loading change and the test changes listed under Merge proposals. [Approve merge] [Keep the twin]
  **Answer (G1, 2026-11-01):** [Keep the twin], the twin stays (SuperAdmin-only page; the merge needs the Platform phone layout to gain card and form styles, small payoff for real risk (low confidence)).

### Relayout proposals

BEFORE screenshots for every page are in the Relayout list section.

- **Q-111** (R-quests-01): Quest/Details (desktop): split the columns col-lg-9 / col-lg-3 instead of col-lg-10 / col-lg-2 so the Quest Summary reads without wrapping (width change only, order unchanged). [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (the Quest Summary wraps today; width change only, order unchanged).
- **Q-112** (R-quests-02): Quest/Details (desktop, voting and update states): draw the calendar months directly inside the one signup card instead of one nested card per month (removes two levels of blur). [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (removes two nested blur levels; note the mockups do not draw the voting calendar).
- **Q-113** (R-quests-03): Quest/Create and Quest/Edit (desktop): action row Cancel left and the commit action right after an hr, instead of commit left with no hr (reordering inside the action row only). [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (one commit action closing the form is what both directions draw).
- **Q-114** (R-quests-04): Quest/Manage (mobile): remove the card-in-card nesting; Description and Rewards become headed sections inside the single card, collapsible exactly as today. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (removes card-in-card nesting on the phone; collapsible exactly as today).
- **Q-115** (R1 (events)): Events/Details (both layouts): move the "Your Availability" card above "Description" so the main player action is the first card after the title block (order only). [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (puts the main player action first; order only).
- **Q-116** (R2 (events)): Calendar/Index (desktop, below the large breakpoint): month navigation on its own row under the title, "Board Availability" and "My Agenda" in a wrapping button row; same four controls and labels. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (same four controls; stops the title row crowding below the large breakpoint). Calendar constraint G1-R2 applies: the relayout must keep the whole desktop month visible without scrolling at 1280 by 720 and up (compact one-line entries, "+N more").
- **Q-117** (R-1 (characters)): ContactCategoryManagement/Manage (desktop): wrap the category table in a responsive container so it scrolls inside its card at narrow widths; columns, buttons and order unchanged. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (the table scrolls inside its card instead of overflowing the page).
- **Q-118** (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (one button row across the Characters forms; buttons move within the row only).
- **Q-119** (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (removes an empty block; fields keep their order).
- **Q-120** (R-1 (shop)): Shop/Index (desktop): move the purchase history from a fixed 280px side panel to an in-flow section under the item grid (a reorder of two blocks; same rows, same Return and Sell forms), so it is reachable at every width. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (the fixed 280px side panel hides purchase history at narrow widths; same rows and forms). Shop variant (a) navy is the look the AFTER is drawn in (Direction A shop-a); the floating merchant button gets a new place (G1-O2).
- **Q-121** (R-2 (shop)): Shop/Index (desktop): one filter card containing the rarity checkboxes, sort select, search input and the Apply Filters and Search buttons, instead of two stacked bars tied by a form attribute. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (one filter card instead of two bars tied by a form attribute). Shop variant (a) navy is the look the AFTER is drawn in (Direction A shop-a).
- **Q-122** (R1 (account)): GroupPicker/Index (desktop and mobile): the per-board cards become flat rows on the one panel, each row being the submit control; every board entry, hidden input and the Go to Platform link stay. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (fixes the severity-S1 keyboard barrier (A-account-031); every board entry stays).
- **Q-123** (R2 (account)): Account/Profile (desktop, populated, narrow widths): the five row actions drop beneath the subscription name and dates instead of sitting to their right (no BEFORE capture of the populated state exists; two scratch frames were taken for the audit). [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (row actions drop under the name at narrow widths; no populated BEFORE capture exists, so the area phase takes one).
- **Q-124** (R-admin-1): Admin/CreateUser and Admin/EditUser (desktop): merge the bare-board duplicate h1 and the card h2 into one page title in the card header; no field or action moves. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (one page title instead of a duplicate; pairs with Q-91 Deliberate).
- **Q-125** (R-admin-2): Platform Group/Members (desktop): stack "Current Members" and "Available Users" full width instead of two half-width columns; all columns, forms and buttons stay. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (stacks two half-width tables full width; every column and form stays).
- **Q-126** (R-admin-3): Admin/Users (desktop): stack the Email under the Name in one cell so the Actions cell gets the freed width; every column's content and every button stays. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (stacks Email under Name to free the Actions cell; nothing removed).
- **Q-127** (R-shell-1): Desktop layout navbar (992 to 1280 px): keep every link on one line by a second measured row or by collapsing at the xl breakpoint; order, labels, icons and conditions unchanged. [Approve relayout in principle] [Keep today's layout]
  An approval here is in principle: the page is re-laid out only after its before/after sign-off in its area phase
  **Answer (G1, 2026-11-01):** [Approve relayout in principle] — approved in principle — re-confirmed with its AFTER in the area phase (the bar wraps to two or three rows between 992 and 1280px today and covers the first content row).

### Other decisions (not twin, merge or relayout)

- **Q-128** (Q-shop-22, A-shop-037): Shop theme (decided from the mockups with the direction, not a twin question): variant (a) keep the navy theme of the desktop Shop page as a `[data-theme="shop"]` block of semantic-token overrides, or variant (b) retire it and fold the Shop into the main parchment and gold look. The audit measured the navy theme as the more compliant of the two looks. [Shop variant (a)] [Shop variant (b)]
  **Answer (G1, 2026-11-01):** [Shop variant (a)] (operator choice at G1; the sheet suggested (b)). OVERRIDE of the sheet suggestion (Shop variant (b)): the operator chose variant (a), the navy theme of Direction A. The floating merchant button needs a new place (G1-O2). Colour-count note: G1-O4.
- **Q-129** (Quests report, `_QuestFormScripts` finding): Markdown editor library: EasyMDE is loaded from a CDN by `Quest/_QuestFormScripts` and is the one CDN host left in rendered area pages. [Self-host it] [Accept the exception]
  **Answer (G1, 2026-11-01):** [Self-host it] (the Docker path must work offline with no setup, and it is the only CDN host left after the dependency work).
- **Q-130** (Quests report, dead-code findings): Unreferenced files: `Quest/_QuestCard`, `Quest/_QuestSection` and `QuestSectionViewModel` are rendered nowhere. Clean them up in the Quests phase (with the closed ambient-clock test list rewritten to the surviving files)? No page changes either way. [Approve the cleanup] [Leave them in place]
  **Answer (G1, 2026-11-01):** [Approve the cleanup] (three files rendered nowhere; no page changes either way).
- **Q-131** (Quests report): Quest board poster artwork: keep the 1000x1400 PNG posters or add WebP derivatives for the phone shell? [Keep the PNG posters] [Add WebP derivatives]
  **Answer (G1, 2026-11-01):** [Keep the PNG posters] (Direction B has no poster art; if posters survive in a mix, revisit in the Quests phase). Consistent with the decision to keep the poster artwork (G1-O1): the poster PNGs stay; WebP derivatives are revisited in the Quests phase with the poster relayout.
- **Q-132** (OQ-7, A-characters-007): Image crop dialog (five pages): the selection can only be moved or resized by dragging, with no keyboard or single-pointer path (WCAG 2.5.7 fails). Adding nudge and resize buttons adds controls to the dialog. [Approve the addition] [Keep drag-only; record the exception]
  **Answer (G1, 2026-11-01):** [Approve the addition] (WCAG 2.5.7 needs a non-drag path; the buttons are additive).
- **Q-133** (A-events-017): Calendar today highlight: `_Calendar.cshtml` reads `DateTime.Today`, outside the closed ambient-clock list, so the highlight can show the wrong day for a few hours a day on the board clock. Replacing it with the board clock changes what the highlight shows for those hours and needs its own test and guarded-list entry. [Approve the behaviour change with its own test] [Keep today's behaviour]
  **Answer (G1, 2026-11-01):** [Approve the behaviour change with its own test] (server-side "what day is it" must go through the board clock, never DateTime.Today).
- **Q-134** (Events and Quests reports, scope notes): Ownership of the `_Calendar` vote UI and of the `.current-quest` and vote rules in `calendar.css` (lines 385-440 and 673-765), which exist for Quest Details: [Quests phase carries a scoped patch and Events verifies] [Events phase takes it and runs before Quests]
  **Answer (G1, 2026-11-01):** [Quests phase carries a scoped patch and Events verifies] (the audit order already puts Quests first and the vote UI exists for Quest Details). The month-fit relayout (G1-R2) is carried by the Events phase; the scoped vote-UI patch stays with Quests as answered.
- **Q-135** (Quests and Characters reports, scope notes): Ownership of `Shared/_CharacterSelectModal`, which only Quest Details renders: [Quests phase] [Characters phase]
  **Answer (G1, 2026-11-01):** [Quests phase] (only Quest Details renders the modal and it carries a severity-S1 finding).
- **Q-136** (Quests report, adjustments): Ownership of `Home/Index` (the signed-out landing page with the Log In action, no quest content, no stylesheet of its own): [Move it to Account and pickers] [Keep it with Quests]
  **Answer (G1, 2026-11-01):** [Move it to Account and pickers] (the signed-out landing page with a Log In action; no quest content).
- **Q-137** (A-shop-002, 017, 026, 027): Functional shop breaks the audit found beside the looks: the hidden Return and Sell buttons (A-shop-002), the sell confirmation script that never runs (A-shop-017), the dead Preview button (A-shop-027) and the dead View Details link (A-shop-026). Who fixes them? ("Shop creation fails" is separate: see the Summary.) [The Shop phase fixes them with the conversion] [A separate fix first]
  **Answer (G1, 2026-11-01):** [The Shop phase fixes them with the conversion] (Shop creation failure is already a separate fix; these four sit in the files the Shop phase rewrites).
- **Q-138** (Proposed area-phase cut): The proposed area-phase cut and order at the end of this report. [Approve the cut and order] [Adjust it before the phases are appended]
  **Answer (G1, 2026-11-01):** [Approve the cut and order] (seven phases; order Foundation, 1, 5a, 2, 3, 4, 5b, 6, each step with a stated reason).

### Sign-off record (G1, 2026-11-01)

**Resume signal (the operator's decisions, as the orchestrator transcribed them):** Direction A ("refined glass") overall with stated exceptions: the desktop quest board keeps today's poster card art (relayout of the information on it approved in principle, drawn first in the Quests phase); the Shop is variant (a), navy, and its floating merchant button needs a new place; the calendar takes Direction A's look with the desktop month fitting without scrolling ("+N more" behaviour to be confirmed against the calendar-fit mockup). S values: texture strong, wood action, scrim default, phone navbar static, Shop navy, every other value as proposed by Direction A. Q-nn: "use the answer sheet's suggestions" for all 138, except where those decisions override them.

**Answers recorded:** 57 twin questions answered [Add it to the other view] and 37 [Deliberate, record it] (Q-01 to Q-94); 13 merges approved and carried into their area phases (Q-95 to Q-107) and 3 kept (Q-108 to Q-110); all 17 relayouts `approved in principle — re-confirmed with its AFTER in the area phase` (Q-111 to Q-127); Q-128 to Q-138 answered as the sheet suggested except Q-128. Q-14 and Q-15 (character and role pickers) are added to the mobile signup view.

**Overrides of the sheet's suggestions (each also recorded next to its Q-nn):**

| Q-nn | Sheet suggestion | Operator answer | Why |
|------|------------------|-----------------|-----|
| Q-128 | Shop variant (b) | Shop variant (a), navy | operator chose the navy theme at G1 (S9) |
| S2 to S11 | the sheet's prefilled values were Direction B's (paper, no blur, solid plank, veil 50%) | Direction A's values; explicit: texture strong, wood, scrim default, phone navbar static, Shop navy | the operator signed off Direction A |

No other Q-nn needed an override. Consistency notes (not overrides) are recorded on Q-14, Q-15, Q-21 to Q-23, Q-40 to Q-47, Q-60, Q-116, Q-120, Q-121, Q-131 and Q-134.

**Open items carried forward (none blocks the foundation):**

- **G1-O1 / G1-R1, quest board poster cards (Quests phase).** Keep the poster artwork; relayout the information inside it, drawn and shown to the operator before it is built. Ink on the bare poster measures 3.8 to 4.2:1 at the 5th percentile (fails 4.5:1 for body text); titles are large text (3:1). Approach: very gently lighten the text zone inside the poster so body text reaches 4.5:1 at B-max and it still reads as the same parchment, not a separate slip. The foundation must not convert quest cards to the slip treatment (`--qb-surface-slip` is not in `tokens-final.css`); the six poster-slip pairs of the mockup were left out of the re-score and are re-scored when the redraw exists.
- **G1-O2, Shop floating merchant button (Shop phase).** It hovers over the footer in the mockups; it needs a new place. Required fix.
- **G1-O3 / G1-R2, calendar (Events phase).** Direction A's look; the desktop month must fit without scrolling at 1280 by 720 and up. "+N more" (click, tap or Enter opening a day panel, against hover-to-open) is not final: the operator confirms it against the calendar-fit mockup (`$QBR/mockups/calendar-fit/`) before the Events phase.
- **G1-O4, colour cap.** `tokens-final.css` has 46 distinct colour literals in `:root` and 61 with the Shop theme block (hex and `rgba()`, Bootstrap channel triples excluded), against the contract's cap of 40; as distinct opaque hex values it is 30 and 38. Proposed smallest adjustment, for the operator to confirm before the token layer ports the file: count the cap as distinct opaque hex values (38 with Shop (a)) and list the alpha steps separately. Direction B with Shop (a) was 45 by the literal count; Direction A is over the cap on that count even without the Shop theme.

## Relayout list

17 relayout proposals from the seven reports, one row per page affected, plus two operator-initiated rows added at the G1 sign-off (2026-11-01). Every proposal keeps every field, link, button and action and each control's type and collapses nothing behind a toggle (D-04); the one exception is the calendar month fit (G1-R2), where "+N more" is an operator-requested exception limited to day cells. BEFORE cells are the captures of the BEFORE record (`$QBR` is `~/qb-ui-review/phase-94`). The AFTER cell for the mocked pages (Quest Details, Quest Create/Edit, Calendar, Shop) now lists the Direction A renders under `$QBR/mockups/direction-a/png/` (Direction A is the signed-off direction; the Shop AFTER is variant (a), the navy theme); every other page says it is not yet drawn and is drawn in its area phase before it is re-laid out. The mockups were drawn from today's real views, not from these proposals, so a drawn page is not an approval of the proposal beside it. Every question was answered at G1 and every answer is `approved in principle — re-confirmed with its AFTER in the area phase`: no page is re-laid out until its before/after sign-off in its area phase.

| Page | BEFORE desktop | BEFORE mobile | AFTER | Proposed change (within D-04) | Answer (G1, 2026-11-01) |
|------|----------------|---------------|-------|--------------------------------|---|
| Quest/Details | `$QBR/before/desktop/Quest-Details.png` | `$QBR/before/mobile/Quest-Details.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png` | Q-111 (R-quests-01): Quest/Details (desktop): split the columns col-lg-9 / col-lg-3 instead of col-lg-10 / col-lg-2 so the Quest Summary reads without wrapping (width change only, order unchanged) | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Details (voting) | `$QBR/before/desktop/Quest-Details--voting.png` | `$QBR/before/mobile/Quest-Details--voting.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png` (the mockups draw the finalized Quest Details page; the voting calendar is not drawn) | Q-111 (R-quests-01): Quest/Details (desktop): split the columns col-lg-9 / col-lg-3 instead of col-lg-10 / col-lg-2 so the Quest Summary reads without wrapping (width change only, order unchanged) | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Details (voting) | `$QBR/before/desktop/Quest-Details--voting.png` | `$QBR/before/mobile/Quest-Details--voting.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-details-1440.png`, `$QBR/mockups/direction-a/png/quest-details-390.png` (the mockups draw the finalized Quest Details page; the voting calendar is not drawn) | Q-112 (R-quests-02): Quest/Details (desktop, voting and update states): draw the calendar months directly inside the one signup card instead of one nested card per month (removes two levels of blur) | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Create | `$QBR/before/desktop/Quest-Create.png` | `$QBR/before/mobile/Quest-Create.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-form-1440.png`, `$QBR/mockups/direction-a/png/quest-form-390.png` | Q-113 (R-quests-03): Quest/Create and Quest/Edit (desktop): action row Cancel left and the commit action right after an hr, instead of commit left with no hr (reordering inside the action row only) | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Edit | `$QBR/before/desktop/Quest-Edit.png` | `$QBR/before/mobile/Quest-Edit.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/quest-form--edit-1440.png`, `$QBR/mockups/direction-a/png/quest-form--edit-390.png` (Direction B draws the form once, as Create; Edit uses the same layout) | Q-113 (R-quests-03): Quest/Create and Quest/Edit (desktop): action row Cancel left and the commit action right after an hr, instead of commit left with no hr (reordering inside the action row only) | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Manage | `$QBR/before/desktop/Quest-Manage.png` | `$QBR/before/mobile/Quest-Manage.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-114 (R-quests-04): Quest/Manage (mobile): remove the card-in-card nesting; Description and Rewards become headed sections inside the single card, collapsible exactly as today | approved in principle — re-confirmed with its AFTER in the area phase |
| Events/Details | `$QBR/before/desktop/Events-Details.png` | `$QBR/before/mobile/Events-Details.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-115 (R1 (events)): Events/Details (both layouts): move the "Your Availability" card above "Description" so the main player action is the first card after the title block (order only) | approved in principle — re-confirmed with its AFTER in the area phase |
| Calendar/Index | `$QBR/before/desktop/Calendar-Index.png` | `$QBR/before/mobile/Calendar-Index.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/calendar-1440.png`, `$QBR/mockups/direction-a/png/calendar-390.png` | Q-116 (R2 (events)): Calendar/Index (desktop, below the large breakpoint): month navigation on its own row under the title, "Board Availability" and "My Agenda" in a wrapping button row; same four controls and labels | approved in principle — re-confirmed with its AFTER in the area phase; constraint G1-R2: the whole desktop month stays visible without scrolling at 1280 by 720 and up |
| ContactCategoryManagement/Manage | `$QBR/before/desktop/ContactCategoryManagement-Manage.png` | `$QBR/before/mobile/ContactCategoryManagement-Manage.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-117 (R-1 (characters)): ContactCategoryManagement/Manage (desktop): wrap the category table in a responsive container so it scrolls inside its card at narrow widths; columns, buttons and order unchanged | approved in principle — re-confirmed with its AFTER in the area phase |
| Characters/Create | `$QBR/before/desktop/Characters-Create.png` | `$QBR/before/mobile/Characters-Create.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| Characters/Edit | `$QBR/before/desktop/Characters-Edit.png` | `$QBR/before/mobile/Characters-Edit.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| Contacts/Create | `$QBR/before/desktop/Contacts-Create.png` | `$QBR/before/mobile/Contacts-Create.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| Contacts/Edit | `$QBR/before/desktop/Contacts-Edit.png` | `$QBR/before/mobile/Contacts-Edit.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| ContactCategoryManagement/Edit | `$QBR/before/desktop/ContactCategoryManagement-Edit.png` | `$QBR/before/mobile/ContactCategoryManagement-Edit.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| DungeonMaster/EditProfile | `$QBR/before/desktop/DungeonMaster-EditProfile.png` | `$QBR/before/mobile/DungeonMaster-EditProfile.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-118 (R-2 (characters, OQ-8)): Form pages of the Characters area: one button row (hr, Cancel or Back left, primary right on desktop; one stacked order on mobile); buttons move within their row only | approved in principle — re-confirmed with its AFTER in the area phase |
| Contacts/Create | `$QBR/before/desktop/Contacts-Create.png` | `$QBR/before/mobile/Contacts-Create.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order | approved in principle — re-confirmed with its AFTER in the area phase |
| Contacts/Edit | `$QBR/before/desktop/Contacts-Edit.png` | `$QBR/before/mobile/Contacts-Edit.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order | approved in principle — re-confirmed with its AFTER in the area phase |
| Characters/Create | `$QBR/before/desktop/Characters-Create.png` | `$QBR/before/mobile/Characters-Create.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order | approved in principle — re-confirmed with its AFTER in the area phase |
| Characters/Edit | `$QBR/before/desktop/Characters-Edit.png` | `$QBR/before/mobile/Characters-Edit.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-119 (R-3 (characters)): Contacts and Characters Create/Edit (desktop): align the image block with the field stack so the empty block under the file input goes away; fields keep their order | approved in principle — re-confirmed with its AFTER in the area phase |
| Shop/Index | `$QBR/before/desktop/Shop-Index.png` | `$QBR/before/mobile/Shop-Index.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a-1440.png`, `$QBR/mockups/direction-a/png/shop-a-390.png` | Q-120 (R-1 (shop)): Shop/Index (desktop): move the purchase history from a fixed 280px side panel to an in-flow section under the item grid (a reorder of two blocks; same rows, same Return and Sell forms), so it is reachable at every width | approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2) |
| Shop/Index (empty-category) | `$QBR/before/desktop/Shop-Index--empty-category.png` | `$QBR/before/mobile/Shop-Index--empty-category.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a--empty-1440.png`, `$QBR/mockups/direction-a/png/shop-a--empty-390.png` | Q-120 (R-1 (shop)): Shop/Index (desktop): move the purchase history from a fixed 280px side panel to an in-flow section under the item grid (a reorder of two blocks; same rows, same Return and Sell forms), so it is reachable at every width | approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2) |
| Shop/Index | `$QBR/before/desktop/Shop-Index.png` | `$QBR/before/mobile/Shop-Index.png` | Direction A (signed off) renders: `$QBR/mockups/direction-a/png/shop-a-1440.png`, `$QBR/mockups/direction-a/png/shop-a-390.png` | Q-121 (R-2 (shop)): Shop/Index (desktop): one filter card containing the rarity checkboxes, sort select, search input and the Apply Filters and Search buttons, instead of two stacked bars tied by a form attribute | approved in principle — re-confirmed with its AFTER in the area phase; drawn in the navy Shop theme (a); the floating merchant button gets a new place (G1-O2) |
| GroupPicker/Index | `$QBR/before/desktop/GroupPicker-Index.png` | `$QBR/before/mobile/GroupPicker-Index.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-122 (R1 (account)): GroupPicker/Index (desktop and mobile): the per-board cards become flat rows on the one panel, each row being the submit control; every board entry, hidden input and the Go to Platform link stay | approved in principle — re-confirmed with its AFTER in the area phase |
| Account/Profile | `$QBR/before/desktop/Account-Profile.png` | `$QBR/before/mobile/Account-Profile.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-123 (R2 (account)): Account/Profile (desktop, populated, narrow widths): the five row actions drop beneath the subscription name and dates instead of sitting to their right (no BEFORE capture of the populated state exists; two scratch frames were taken for the audit) | approved in principle — re-confirmed with its AFTER in the area phase |
| Admin/CreateUser | `$QBR/before/desktop/Admin-CreateUser.png` | `$QBR/before/mobile/Admin-CreateUser.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-124 (R-admin-1): Admin/CreateUser and Admin/EditUser (desktop): merge the bare-board duplicate h1 and the card h2 into one page title in the card header; no field or action moves | approved in principle — re-confirmed with its AFTER in the area phase |
| Admin/EditUser | `$QBR/before/desktop/Admin-EditUser.png` | `$QBR/before/mobile/Admin-EditUser.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-124 (R-admin-1): Admin/CreateUser and Admin/EditUser (desktop): merge the bare-board duplicate h1 and the card h2 into one page title in the card header; no field or action moves | approved in principle — re-confirmed with its AFTER in the area phase |
| Group/Members | `$QBR/before/desktop/Platform-Group-Members.png` | `$QBR/before/mobile/Platform-Group-Members.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-125 (R-admin-2): Platform Group/Members (desktop): stack "Current Members" and "Available Users" full width instead of two half-width columns; all columns, forms and buttons stay | approved in principle — re-confirmed with its AFTER in the area phase |
| Admin/Users | `$QBR/before/desktop/Admin-Users.png` | `$QBR/before/mobile/Admin-Users.png` | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-126 (R-admin-3): Admin/Users (desktop): stack the Email under the Name in one cell so the Actions cell gets the freed width; every column's content and every button stays | approved in principle — re-confirmed with its AFTER in the area phase |
| Desktop layout navbar (shell) | `$QBR/audit/shell/shots/dm-quests-1200/top.png`, `$QBR/audit/shell/shots/quests-992/quests.png` | not applicable (the bar wraps only on the desktop layout) | after: not yet drawn — drawn in its area phase before that page is re-laid out | Q-127 (R-shell-1): Desktop layout navbar (992 to 1280 px): keep every link on one line by a second measured row or by collapsing at the xl breakpoint; order, labels, icons and conditions unchanged | approved in principle — re-confirmed with its AFTER in the area phase |
| Quest/Index (desktop poster cards) | `$QBR/before/desktop/Quest-Index.png` | not applicable (the poster cards are desktop only; the phone keeps its glass cards) | after: not yet drawn — drawn in its area phase before that page is re-laid out (Direction A's pinned-slip render of this page is not the AFTER: the operator kept the poster artwork) | G1-R1 (operator-initiated at G1): keep today's parchment poster images; re-lay out only the information inside the poster, with a very gently lightened text zone so body text reaches 4.5:1 at B-max (ink on the bare poster measured 3.8 to 4.2:1 at the 5th percentile); titles stay large text at 3:1 | approved in principle — re-confirmed with its AFTER in the area phase; Quests phase |
| Calendar/Index (desktop month fit) | `$QBR/before/desktop/Calendar-Index.png` | `$QBR/before/mobile/Calendar-Index.png` | Direction A calendar at 1440 and 390: `$QBR/mockups/direction-a/png/calendar-1440.png`, `$QBR/mockups/direction-a/png/calendar-390.png`; the "+N more" interaction is shown in the calendar-fit mockup the operator tries before the Events phase (`$QBR/mockups/calendar-fit/`, being built; open item G1-O3) | G1-R2 (operator requirement at G1): the desktop month fits without scrolling at 1280 by 720 and up: compact one-line entries (marker with a non-colour cue, title with an ellipsis, 4.5:1), "+N more" in busy cells so cells never grow, an entry expands on hover, keyboard focus and tap (dismissible with Esc, hoverable, persistent); phones keep the agenda list. Entries collapse behind "+N more": an operator-requested exception to the no-collapse rule, limited to day cells, every entry stays reachable | approved in principle — re-confirmed with its AFTER in the area phase; "+N more" behaviour confirmed against the calendar-fit mockup before the Events phase |

## Merge proposals

16 merge proposals: a pair qualifies only if it is identical or layout-only, has no deliberate difference, and its mobile differences are CSS-only. Each is **costly to reverse**: the mobile file goes, the view is served under both layouts like the precedent views `Events/Create`, `Events/Details`, `Events/Edit` and `Series/Details`, and every mobile-view test that selects the `.Mobile` file changes. Each waits for the operator's explicit approval (a question in the batch). Pairs checked and not proposed, with the reason, are in each area report (Events and Shop: none qualifies; most Quests, Characters, Account and Admin pairs differ in content or markup).

| # | Q | Pair | Qualifies because | CSS-loading change in both layouts | Tests whose facts would change | Status |
|---|---|------|-------------------|------------------------------------|--------------------------------|--------|
| M-quests-01 | Q-95 | QuestLog/EditRecap | identical inventory; differences are the card wrapper class (`modern-card` against `quest-edit-card-mobile`), `<hr>` and `flex-fill` | one `@section Styles` link to a single page sheet (or the `pages` layer file) rendered by the desktop layout (needs the `Styles` section the foundation adds) and by the mobile layout; `quest-edit.mobile.css` stays for Quest/Edit.Mobile until that pair converts | `MobileViewsTests` selects `EditRecap.Mobile` by user agent: no fact in `MobileViewsTests` names EditRecap, but `QuestLogControllerIntegrationTests` (recap edit at lines 259-289) and the shared `quest-edit.mobile.css` link fact at `MobileViewsTests.cs:888` (Quest/Edit) must be re-pinned; the mobile-view-location expander test that expects an `EditRecap.Mobile` file would be removed | approved at G1 (2026-11-01), carried into the Quests and Quest Log phase; costly to reverse |
| M-quests-02 | Q-96 | QuestLog/Details | layout-only: same sections in the same order, same links and gates; grouping differs (8/4 grid against three stacked cards) | a single page sheet through `@section Styles` in both layouts; `quest-log-detail.mobile.css` retires | `MobileViewsTests.cs:953-987` (`GetMobilePage_QuestLogDetails_ReturnsSuccessAndMobileLayout` asserts the `quest-log-detail-main-card` class and the `quest-log-detail.mobile.css` link) | approved at G1 (2026-11-01), carried into the Quests and Quest Log phase; costly to reverse |
| M-characters-1 | Q-97 | Characters/Details | Identical inventory (twin comparison in the Characters report); the mobile markup differs only by class names and by `div` against `h4` headings. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | `MobileViewsTests` "Mobile UA on /Characters/Details/{id} renders character-detail-card glass card and links character-detail.mobile.css" (`MobileViewsTests.cs` near line 767): the class and stylesheet link it asserts change to the merged view's. | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-characters-2 | Q-98 | Contacts/Details | Identical inventory; same notes script twice; the note surface differs by CSS only once unified. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | `ContactDetailsCategoryTests` (the category line on Details) and `ContactsTagsDesktopMarkupTests` (`Details_*`: tag chips and Player visibility). `MobileViewsTests` has no Contacts/Details case, so a mobile-UA test for the merged view would be added. | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-characters-3 | Q-99 | Contacts/Create | Layout-only pair (tracer): same fields, buttons and dialog; the only non-CSS difference is the mobile hint class. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | `ContactsTagsFormMarkupTests` (`CreateForm_DesktopUserAgent_...` and `CreateForm_MobileUserAgent_...`), `ContactCategoryContrastGuardTests` (the mobile Create surface `.contact-form-card` and the `contact-form.mobile.css` rules it pins; "must wrap its form in .modern-card" for desktop). | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-characters-4 | Q-100 | Contacts/Edit | Layout-only pair; same as Create plus Re-crop and Id. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | Same tests as M-3; merge Create and Edit together so the shared stylesheet is moved once. | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-characters-5 | Q-101 | ContactCategoryManagement/Edit | Identical inventory; the markup differs only in wrapper and class names. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | `ContactCategoryManagementControllerIntegrationTests`; `ContactCategoryContrastGuardTests` pins the Manage.Mobile add form rather than Edit, and `MobileViewsTests` has no case for this view. | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-characters-6 | Q-102 | DungeonMaster/EditProfile | Identical inventory; desktop two columns against stacked mobile is CSS-only. | one page stylesheet loaded with `@section Styles` from the merged view (needs the desktop and Platform layouts to render the `Styles` section); the mobile sheet leaves the mobile pass; the `.Mobile.cshtml` file goes and the inline `style` and `<script>` blocks move to files first | `MobileViewsTests` "DM EditProfile renders glass card form on mobile UA" (`GetMobilePage_DmEditProfile_ReturnsSuccessAndMobileLayout`, asserts `dm-editprofile-card-mobile` and `dm-editprofile.mobile.css`). | approved at G1 (2026-11-01), carried into the Characters, Contacts, DM profile and Players phase; costly to reverse |
| M-account-1 | Q-103 | Account/Login | differs only in wrapper, heading element and widths (see the report); precedent: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` | The merged view links one area file through `@section Styles`. The mobile layout already renders `Styles`; the desktop layout gains `@await RenderSectionAsync("Styles", required: false)` in the foundation; `account.mobile.css` drops this view from its consumers. | `MobileAccountLogin_MobileUserAgent_RendersGlassCardForm` and `MobileAccountLogin_DesktopUserAgent_DoesNotRenderGlassCard` (MobileViewsTests.cs:552-575) assert `account-card-mobile` and `account.mobile.css` on mobile and their absence on desktop; both would be rewritten to pin the same intent against the merged markup | approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-1 must not be answered "Deliberate" (an untitled mobile page cannot merge with a titled desktop one); costly to reverse |
| M-account-2 | Q-104 | Account/ForgotPassword | differs only in wrapper, heading element and widths (see the report); precedent: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` | same as M1 | no mobile-view fact pins this view (the only mobile Account facts are Login, Edit, Profile and GroupPicker); `AccountControllerIntegrationTests` run under the default User-Agent | approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-2; costly to reverse |
| M-account-3 | Q-105 | Account/SetPassword | differs only in wrapper, heading element and widths (see the report); precedent: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` | same as M1 | none (no mobile-view fact pins this view) | approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-3; source-only audit, so the merge must be verified with a freshly mailed token; costly to reverse |
| M-account-4 | Q-106 | Account/ChangePassword | differs only in wrapper, heading element and widths (see the report); precedent: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` | same as M1 | none (no mobile-view fact pins this view) | approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: Q-account-4; costly to reverse |
| M-account-5 | Q-107 | GroupPicker/Index | differs only in wrapper, heading element and widths (see the report); precedent: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` | `Index.Mobile.cshtml` is the only consumer of `account.mobile.css` on this page; the layout already renders `Styles`, so only the link moves; desktop has no CSS-loading change | `MobileGroupPicker_MobileUserAgent_RendersGroupCardsAndStylesSection` (MobileViewsTests.cs:619-663) asserts `account-card-mobile` and `account.mobile.css` plus the Styles-section regression; it would be rewritten to pin the same regression against the merged view; `GroupPickerControllerIntegrationTests` do not branch on User-Agent | approved at G1 (2026-11-01), carried into the Account and pickers phase; condition: A-account-031 and the R1 relayout land in the same change; costly to reverse |
| M-admin-1 | Q-108 | Platform Group/Create | identical inventory; the mobile differences are the wrapper (`row`/`col-md-6`/`card` against one glass div) and the heading level, all expressible in CSS | `_Layout.Platform` keeps the shared card styles; `_Layout.Platform.Mobile` gains the same shared card and form styles (today it loads `mobile.css` only), and the mobile-only form rules move into a shared sheet scoped under `.mobile-layout` loaded by both; `platform-group.mobile.css` stops being linked from this view | `MobileViewsTests` (selects the `.Mobile` view under a real mobile User-Agent: the Create view would no longer have a `.Mobile` file); `BoardPageStaticGuardTests` (reads `platform-group.mobile.css`, line 137 of that test); `PlatformAreaLayoutTests` (the Platform layout loads the card styles) | kept at G1 (2026-11-01): the twin stays; costly to reverse |
| M-admin-2 | Q-109 | Platform Group/Delete | identical inventory; wrapper and heading level only | same as M-1 | same as M-1 | kept at G1 (2026-11-01): the twin stays; costly to reverse |
| M-admin-3 | Q-110 | Platform Group/Edit | identical inventory; the mobile-only `platform-group-pages-mobile` wrapper and the 44px switch rows (`platform-group.mobile.css:192-201`) are CSS rules | same as M-1; the 44px row and label-as-tap-target rule becomes a `.mobile-layout` rule in the shared sheet (A-5) | same as M-1, plus the Phase 93 board-pages guards that read `platform-group.mobile.css` | kept at G1 (2026-11-01): the twin stays; costly to reverse |

Common to every proposal: the desktop layouts (`_Layout.cshtml` and `_Layout.Platform.cshtml`) gain `@await RenderSectionAsync("Styles", required: false)` in the foundation; the mobile layouts already render it; the inline `style=` and `<script>` blocks of the pair move to files first so one view can serve both layouts; `MobileViewLocationExpander` stays. Merging is sequenced inside the owning area phase, never earlier. G1 outcome (2026-11-01): 13 merges approved (Q-95 to Q-107) and carried into their area phases, 3 kept (Q-108 to Q-110, the Platform Group pages).

## Dead dependencies

The dead-dependency proofs (jQuery, the Font Awesome v4 shim and the other vendor lines) are in [94-AUDIT-vendor.md](94-AUDIT-vendor.md), written by plan 94-13, which may land after this report; this index links to it by name and does not copy it. The shell report's A-shell-039 records where each layout loads jQuery and the v4 shim today.

## Proposed area-phase cut

The cut starts from the roadmap's draft grouping (Quests and Quest Log; Events, Series, Calendar, Agenda; Characters, Contacts, DM profile, Players; Shop and Shop management; Account and pickers; Admin and Platform; Email templates) and keeps its seven phases. The audit adjusts ownership of eleven items (listed after the phases) and proposes an order. The Foundation (this phase) owns the five layouts, both footers, the toasts, the backdrop and the shell rules, so shell findings (39) belong to no area phase; area phases only verify them on their pages. Phase numbers below are the draft cut's labels; the operator assigns real phase numbers when `/gsd-phase` appends them.

**Definition of "converted" (every area phase; feeds each phase's must-haves).**

1. Zero inline `style=""`, `<style>` and `<script>` and no inline event handler in the area's views; styles in stylesheets, scripts in files or ES modules (D-12).
2. Zero hex or `rgb()` literals and zero `!important` in the area's stylesheets; they live in the `pages` layer or move into `components`.
3. The area's stylesheet is loaded through `@section Styles`, not the global list.
4. Every twin aligned with its partner; every twin content difference answered (D-14); approved merges done.
5. The accessibility bar (A-1 to A-11) verified on each page, desktop and mobile.
6. Before and after screenshots stored outside the repository; relaid-out pages signed off.
7. Mobile checks listed in the phase UAT and run under a mobile User-Agent (D-15).
8. Static guard tests that read the area's files are rewritten to pin the same intent; none is deleted.
9. `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests`.
10. At merge, every page still works and reads (D-08).

### Phase 1: Quests and Quest Log

- **Goal:** Convert the core loop (quest board, quest details, create and edit forms, DM manage, quest log, move) to the foundation, carrying the shared pieces the loop depends on, so a DM can post a quest and a player can sign up on converted pages.
- **Views:** 11 view pairs, 22 files: `Quest/{Index,Details,Create,Edit,CreateFollowUp,Manage}`, `QuestLog/{Index,Details,EditRecap}`, `Home/Index` (or Phase 5a, Q-136), `Move/Index` (serves quests and events). Partials: `Quest/_QuestFormScripts`, `Shared/_MarkdownEditor` (also fixes the keyboard trap for six Characters-area pages), and, by the proposed adjustments, `Shared/_CharacterSelectModal` and the vote UI of `Shared/_Calendar`. `Quest/_QuestCard` and `_QuestSection` are unreferenced (Q-130).
- **Stylesheets:** `quests.css`, `quests.mobile.css`, `quest-edit.mobile.css`, `quest-followup.mobile.css`, `quest-log.mobile.css`, `quest-log-detail.mobile.css`, `home.mobile.css`, `move-entry.css`, `move-entry.mobile.css`, `markdown-editor.css`, `markdown-content.css`, and by adjustment `dm-create.mobile.css` and `dm-manage.mobile.css`
- **Guard tests to rewrite (to the same intent, none deleted):** `Mobile/MobileViewsTests` facts for the Quest, QuestLog, Home and Move pages (including the `quest-edit.mobile.css` link fact near line 888 and the Quest Log detail fact near lines 953-987); `Tests/BoardMoveViewsTests` (Move desktop sheet only, mobile sheet only, `move-card-mobile`), `BoardMoveEventTests`, `BoardMovePageTests`, `BoardMoveEndToEndTests` and the presence lists in `BoardMoveStaticGuardTests` and `BoardPageStaticGuardTests`; `QuestLogControllerIntegrationTests` (recap edit); `LinkedBoardCalendarStyleTests` and `LinkedBoardStaticGuardTests` for `quests.mobile.css` and the calendar partials; the closed `AmbientClockSeamTests` list if the dead partials go
- **Findings owned:** S1 8 and S2 22 of 100 findings (S3 65, S4 5); the S1 items are contrast (table cells, role labels, vote buttons, required marks, outline controls, dark ink on bare board)
- **Open questions inherited:** twin questions Q-07 to Q-39, merges Q-95 to Q-96 (QuestLog/EditRecap and QuestLog/Details), relayouts Q-111 to Q-114, and the other decisions on the Markdown editor host, dead files, posters and ownership
- **Definition of converted:** all ten points above, plus: the Quest board mockup page is built from this phase (it is first because it carries it); the role-select script no longer hides the DM Controls card; the vote buttons on Quest Details pass the bar in both layouts; Quest Details is re-verified with `_Calendar` in both layouts

### Phase 5a: Account and pickers

- **Goal:** Convert the sign-in, profile and board-picker pages; the picker first because it is the one S1 that stops a keyboard user entering the app at all.
- **Views:** 6 Account pairs, the picker pair and the twin-less `Shared/AccessDenied`: `Account/{Login,ForgotPassword,SetPassword,ChangePassword,Edit,Profile}`, `GroupPicker/Index`, `Shared/AccessDenied`; plus `Home/Index` if Q-136 moves it here. 15 files in all (the draft count).
- **Stylesheets:** `account.mobile.css` and the Profile block of `site.css` (lines 1675-1738)
- **Guard tests to rewrite (to the same intent, none deleted):** `MobileViewsTests` Login (lines 552-575), GroupPicker (619-663), Edit and Profile facts; `ProfileCalendarSubscriptionTests` (both subscription shapes are pinned deliberately); `CalendarSubscriptionStaticGuardTests` (the QR sizing rule on `site.css` and `account.mobile.css`); `GroupPickerControllerIntegrationTests` and `CrossBoardPickerSkipTests` (do not branch on User-Agent; keep green)
- **Findings owned:** S1 2 and S2 15 of 43 findings (S3 14, S4 12); S1: picker cards not keyboard-reachable (A-account-031), profile meta text (A-account-019)
- **Open questions inherited:** twin questions Q-76 to Q-84, merges Q-103 to Q-107 (Login, ForgotPassword, SetPassword, ChangePassword, GroupPicker), relayouts Q-122 to Q-123
- **Definition of converted:** all ten points above, plus: the foundation delivers `.modern-card--interactive`, the opaque modal surface, `--qb-fg-muted` and `--qb-danger-text` first; the AFTER sweep adds Profile with one subscription, AccessDenied signed out and the Login validation state; SetPassword is verified with a freshly mailed token; the AccessDenied overflow fix is reused on `Events/Details`

### Phase 2: Events, Series, Calendar, Agenda

- **Goal:** Convert the calendar, agenda and events overview with their shared partials, keeping the date helpers and the linked-board entries intact.
- **Views:** 7 views and 10 files: `Calendar/Index`, `Agenda/Index`, `Events/Index` (each with a twin), the four twin-less `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`; partials `Shared/_Calendar`, `_Calendar.Mobile`, `_LinkedDayEntries`, `_LinkedAlsoThatDay`, `Events/_AvailabilityCell`, `_AvailabilityCounts`, `_SeriesFormScripts`
- **Stylesheets:** `calendar.css`, `calendar.mobile.css`, `events-overview.css`, `events-overview.mobile.css`, `agenda.css`, `agenda.mobile.css` (the vote and current-quest block of `calendar.css`, lines 385-440 and 673-765, per Q-134)
- **Guard tests to rewrite (to the same intent, none deleted):** `EventsOverviewMobileStyleTests` (its literal colours and `!important` conflict with the foundation: re-pin readable counts, a glass-or-paper surface, 44px targets), `LinkedBoardStaticGuardTests`, `LinkedBoardCalendarStyleTests`, and the Events parts of `MobileViewsTests` and `BoardPageStaticGuardTests`; the `AmbientClockSeamTests` list gains the corrected today read if Q-133 is approved
- **Findings owned:** S1 3 and S2 23 of 51 findings (S3 22, S4 3); S1: attendance counts cream on cream, availability badge labels, dark subtitle on bare board
- **Open questions inherited:** twin questions Q-40 to Q-49, no merges (none qualifies), relayouts Q-115 to Q-116
- **Definition of converted:** all ten points above, plus: linked-entry dashed border, the inert `--static` rule and the visually hidden status text stay; no date conversion; every date line keeps its format; calendar-feed files are untouched; the twin-less views get their narrow-width rules in the `pages` layer

### Phase 3: Characters, Contacts, DM profile, Players

- **Goal:** Convert characters, contacts, categories, the DM profile and the players list, and the image crop dialog.
- **Views:** 13 rows, 26 files: `Characters/*` (8), `Contacts/*` (8), `ContactCategoryManagement/*` (4), `DungeonMaster/{Profile,EditProfile}` (4), `Players/Index` (2); `wwwroot/js/image-crop.js` and its dialog markup. `_CharacterSelectModal` per Q-135 (proposed for Phase 1); `_MarkdownEditor` is fixed in Phase 1 and verified here on six pages
- **Stylesheets:** `characters.css`, `characters.mobile.css`, `character-detail.mobile.css`, `character-form.mobile.css`, `contacts.css`, `contacts.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-profile.css`, `dm-profile.mobile.css`, `dm-editprofile.mobile.css`, `players.mobile.css`, `image-crop.css` (13; `dm-create.mobile.css` and `dm-manage.mobile.css` go to Phase 1)
- **Guard tests to rewrite (to the same intent, none deleted):** `MobileViewsTests` facts for Characters (near line 767), DM EditProfile (`GetMobilePage_DmEditProfile_...`), Contacts; `ContactCategoryContrastGuardTests` (the `contacts.mobile.css` and `contact-form.mobile.css` facts and the two rendered-HTML facts, plus the three `modern-card.css` facts the foundation re-pins), `ContactsTagsFormMarkupTests`, `ContactsTagsDesktopMarkupTests`, `ContactDetailsCategoryTests`, `ContactCategoryManagementControllerIntegrationTests`
- **Findings owned:** S1 6 and S2 13 of 55 findings (S3 25, S4 11); S1: crop dialog and character dialog text, two amber mobile buttons, a dark category label, the editor keyboard trap
- **Open questions inherited:** twin questions Q-50 to Q-55, merges Q-97 to Q-102 (six pairs), relayouts Q-117 to Q-119, and the crop dialog decision
- **Definition of converted:** all ten points above, plus: the crop dialog meets 2.5.7 per the answer to Q-132; the one remaining infinite animation (`characters.css:203`) is capped; the editor keyboard trap fix is verified on all six pages

### Phase 4: Shop and Shop management

- **Goal:** Convert the shop and its management pages after the Shop theme is decided, fixing the functional breaks the conversion touches.
- **Views:** 10 view files plus the partial `Shared/_ShopItemDetailsContent`: `Shop/{Index,Details}` and `ShopManagement/{Index,Create,Edit}`, each with a twin (11 with the partial)
- **Stylesheets:** `shop.css`, `shop.mobile.css`, `shop-details.mobile.css`, `shop-management-index.mobile.css`, `shop-management-create.mobile.css`, `shop-management-edit.mobile.css`, plus the Shop rule families in `site.css` (lines 46-310, 1202-1393, 1460, 1562) that the phase deletes as duplicates and the `shop.css` line in `_Layout.cshtml`
- **Guard tests to rewrite (to the same intent, none deleted):** `MobileViewsTests` Shop and ShopManagement entries (the purchase-panel omission at lines 716-733 and the management mobile layout at 1123), `BoardPageStaticGuardTests` (controller file list), `BoardPageGateMatrixTests`, the Shop controller integration tests
- **Findings owned:** S1 10 and S2 19 of 65 findings (S3 29, S4 7); S1 includes the creation failure A-shop-010, which is handed off, not fixed here
- **Open questions inherited:** twin questions Q-56 to Q-75, no merges (none qualifies), relayouts Q-120 to Q-121, the theme decision and the functional-breaks owner
- **Definition of converted:** all ten points above, plus: ten infinite animation declarations (not six; four are dead copies in the Create and Edit `<style>` blocks) are removed or capped; the four views with `<style>` lose it; Return and Sell are verified with a purchase made through the Purchase form on the audit board; Create works first (separate fix off `main`)

### Phase 5b: Admin and Platform

- **Goal:** Convert the admin pages and the Platform area on top of the foundation's Platform layouts.
- **Views:** 25 view files: `Admin/*` (11: five twin pairs plus `EmailStats`), `Platform/Group/*` (10), `Platform/Users/Index` (2), `Platform/LinkedBoards/Index` (2). The two Platform layouts are Foundation work that this phase verifies (A-admin-008, A-admin-014, A-shell-037, A-shell-038)
- **Stylesheets:** `admin-form.mobile.css`, `admin-quests.mobile.css`, `admin-users.mobile.css`, `platform-group.mobile.css`, `platform-users.mobile.css`, `platform-linked-boards.mobile.css`
- **Guard tests to rewrite (to the same intent, none deleted):** `PlatformAreaLayoutTests` (the Platform layout loads the card styles), `LinkedBoardsPlatformIntegrationTests` (`platform-linked-boards.mobile.css`, `platform-group-card-mobile`), `BoardPageStaticGuardTests` (reads `platform-group.mobile.css`), the Admin and Platform entries of `MobileViewsTests`
- **Findings owned:** S1 8 and S2 14 of 44 findings (S3 16, S4 6); S1: badge and table-cell readability, mailto links on Platform phones, the dark modal, the outline Reset Password button, the Platform bar at 320px
- **Open questions inherited:** twin questions Q-85 to Q-94, merges Q-108 to Q-110 (Platform Group Create, Delete, Edit), relayouts Q-124 to Q-126
- **Definition of converted:** all ten points above, plus: a configured provider key (or a seeded stub) so `Admin/EmailStats` tile contrast is measured, not read; the Campaign status mapping (A-admin-016) is answered with the Quests owner's knowledge before it is fixed; the stale note about `_Layout.Platform.Mobile` being dead code is corrected in the milestone documentation

### Phase 6: Email templates (D-18, D-19)

- **Goal:** Give the ten email components the same brand look as the converted web pages, within what mail clients support, with every message's content and trigger unchanged.
- **Views:** all 10 `.razor` components under `QuestBoard.Service/Components/Emails/`: `AddedToGroup`, `ChangeEmailConfirm`, `ForgotPassword`, `QuestDateChanged`, `QuestFinalized`, `QuestMoved`, `SessionReminder`, `WaitlistPromoted`, `Welcome` and `_EmailLayout.razor`, which carries the shared shell and today's remote Font Awesome and Google Fonts links and a `<style>` block.
- **Stylesheets:** none (email styling is inline); the shell's tokens are translated to literal values once, in `_EmailLayout.razor`.
- **Constraints (fixed by the contract):** inline styles only; no CSS custom properties (mail clients do not resolve `var()`); limited mail-client CSS (no layers, no `:where()`, no `backdrop-filter`, table or div layouts that survive the common clients); the markdown sections keep their `<div>` wrapper (`@Html.Markdown(...)` output untouched, the Phase 66 finding); copy, subjects and send triggers unchanged; `Html.LocalTime` and `Html.WallClock` markup untouched where used. The remote fonts in `_EmailLayout.razor` are the one place the "no CDN link" rule cannot apply to mail: the phase decides, with the operator, whether to keep web fonts as progressive enhancement behind safe fallbacks or drop them.
- **Verification (fixed by the contract):** a rendered preview of every template **plus a real test send of each template to the operator's inbox** (the development setting that suppresses sending is switched off for the test send only); the preview and the received message are compared per template.
- **Guard tests to rewrite (to the same intent, none deleted):** `Emails/QuestFinalizedMarkdownRenderTests`, `Emails/WaitlistPromotedMarkdownRenderTests`, `Emails/SessionReminderMarkdownRenderTests` (the markdown `<div>` wrapper), `UnitTests/Services/SessionReminderJobTests`, and the email file references in `Tests/BoardMoveStaticGuardTests`.
- **Findings owned:** none from this audit (the email templates are not web views and were not part of the manifest); the phase starts with its own inventory of the ten components.
- **Open questions inherited:** none; the operator confirms the font decision and the test-send inbox inside the phase.
- **Definition of converted:** the ten points above read for email: points 1 to 3 become "inline styles only, no custom properties, no scripts" and point 5 is checked on the rendered preview and the received message; points 6 and 7 become the preview plus the real test send of each template.

### Adjustments the audit makes to the draft cut

1. **Home/Index (2 files)** from Phase 1 to Phase 5a: the signed-out landing beside the Log In action, no quest content, no stylesheet of its own (Q-136).
2. **`dm-create.mobile.css` and `dm-manage.mobile.css`** into Phase 1: only `Quest/Create.Mobile` and `Quest/Manage.Mobile` load them (the draft cut listed neither).
3. **`Shared/_Calendar` vote UI and the `.current-quest` and vote rules of `calendar.css`** exist for Quest Details and were unowned: proposed to Phase 1 as a scoped patch, with Phase 2 re-verifying Quest Details in both layouts (Q-134).
4. **`Shared/_CharacterSelectModal`** (draft: Phase 3) renders only under Quest Details and carries an S1: proposed to Phase 1 (Q-135).
5. **`Shared/_MarkdownEditor`** stays in Phase 1 and its S1 keyboard trap is fixed there first; Phase 3 verifies it on its six pages.
6. **`Move/Index`** serves quests and events: it stays in Phase 1 and Phase 2 must not edit it in parallel.
7. **Platform layouts** are Foundation work, not Phase 5b's: 5b verifies A-admin-008, A-admin-014, A-shell-037 and A-shell-038.
8. **Shop functional breaks** (hidden Return and Sell, the sell script, the dead Preview and View Details): proposed to Phase 4 (Q-137); the **Shop creation failure** is a separate fix off `main` before Phase 4, outside this phase.
9. **A-admin-016** (Campaign status on the mobile Admin Quests list) stays in 5b but is answered with Q-87 before the Quests phase fixes the shared status mapping.
10. **AccessDenied's 4px overflow** is a twin-less-view pattern shared with `Events/Details`: one fix, in the earlier of the two phases.
11. **Phase 6 Email templates** is unchanged from the draft except that this audit does not feed it findings.

### Proposed order, with a reason for each step

1. **Foundation (this phase)**: layouts, tokens, components and the shell findings come first; every area phase depends on them.
2. **Phase 1, Quests and Quest Log**: the core loop, the first mockup page, and the shared pieces (`_MarkdownEditor`, the signup modal, the vote UI) the later phases depend on.
3. **Phase 5a, Account and pickers**: the only S1 that stops a keyboard user entering the app (A-account-031) and a small phase (15 files).
4. **Phase 2, Events, Series, Calendar, Agenda**: inherits `_Calendar` after Phase 1 has patched its vote UI, and re-verifies Quest Details.
5. **Phase 3, Characters, Contacts, DM profile, Players**: depends on the editor fix from Phase 1 and settles the crop dialog decision.
6. **Phase 4, Shop and Shop management**: after the Shop theme is decided at direction sign-off and the Create fix has landed.
7. **Phase 5b, Admin and Platform**: after the foundation's Platform layouts are proven.
8. **Phase 6, Email templates**: independent of the web phases; it needs the operator's inbox for the real test send and can run any time after the tokens are final.

Each phase's S1 findings are its first tasks (they block the "every page reads" rule), then the conversion in the order the area report suggests.
