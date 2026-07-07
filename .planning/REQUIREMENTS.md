# Requirements: D&D Quest Board

**Defined:** 2026-07-04
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## v1 Requirements

Requirements for milestone v7.0 (Backlog Cleanup). Each maps to roadmap phases.

### Mobile Bugs

- [ ] **MOBILE-01**: Background image stays visually fixed while the page scrolls on mobile browsers, including iOS Safari (#116)
- [ ] **MOBILE-02**: Mobile Quest Log list view shows a "Session Recap Available" badge for quests with a recap, matching desktop (#115)

### Post-Finalization Voting

- [ ] **VOTE-01**: Player can vote Yes on a finalized One-Shot quest even when all seats are filled, landing on a waitlist instead of being rejected
- [ ] **VOTE-02**: Waitlist is ordered by vote (Yes > Maybe > No), then by signup/vote-change timestamp ascending
- [ ] **VOTE-03**: Any vote change resets that signup's timestamp used for waitlist ordering
- [ ] **VOTE-04**: A selected player's seat frees up and the top waitlisted candidate auto-promotes when that player votes No or fully revokes their signup
- [ ] **VOTE-05**: A selected player who changes their vote to Maybe keeps their seat — no promotion triggered
- [ ] **VOTE-06**: A waitlisted player who votes No stays on the waitlist (record retained), sorting to the bottom
- [ ] **VOTE-07**: A waitlisted player auto-promoted into a freed seat as a result of another player's action receives a notification email — never the player who freed the seat, never a player whose own vote change is what selected them

### Character & Profile Image Cropping

- [ ] **IMAGE-01**: User can interactively drag/resize/zoom a crop frame (Cropper.js v2.1.1) over an uploaded photo before saving
- [ ] **IMAGE-02**: The crop happens entirely client-side — no server-side image-processing library
- [x] **IMAGE-03**: Both the original uploaded image and the cropped result are saved
- [ ] **IMAGE-04**: Guild-member list page displays the cropped image; character/DM details pages display the original
- [ ] **IMAGE-05**: Crop UI applies to every image-upload field in the app (character photo, DM profile photo)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Character & Profile Image Cropping

- **IMAGE-06**: Rotate/flip control in the crop widget
- **IMAGE-07**: Multiple aspect-ratio presets

### Post-Finalization Voting

- **VOTE-08**: In-app "you were promoted" banner in addition to the email

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Server-side re-crop/image-processing pipeline (SkiaSharp, ImageSharp, etc.) | This exact path caused the original multi-year pause on issue #78; avoided by design — crop is entirely client-side |
| Broadcasting promotion/waitlist-shift notifications to everyone affected | Notification-spam anti-pattern; costly against the constrained Resend email budget (100/day, 3000/month) |
| SMS/push notification channels for waitlist promotion | Contradicts this app's existing email-only, batch-first design |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| MOBILE-01 | Phase 43 | Pending |
| MOBILE-02 | Phase 43 | Pending |
| VOTE-01 | Phase 44 | Pending |
| VOTE-02 | Phase 44 | Pending |
| VOTE-03 | Phase 44 | Pending |
| VOTE-04 | Phase 44 | Pending |
| VOTE-05 | Phase 44 | Pending |
| VOTE-06 | Phase 44 | Pending |
| VOTE-07 | Phase 44 | Pending |
| IMAGE-01 | Phase 46 | Pending |
| IMAGE-02 | Phase 45 | Pending |
| IMAGE-03 | Phase 45 | Complete |
| IMAGE-04 | Phase 46 | Pending |
| IMAGE-05 | Phase 46 | Pending |

**Coverage:**
- v1 requirements: 14 total
- Mapped to phases: 14
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-04*
*Last updated: 2026-07-04 after roadmap creation (v7.0 Phases 43–46)*
