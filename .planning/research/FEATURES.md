# Feature Research

**Domain:** Crop-before-save avatar UX + waitlist auto-promotion notification UX (v7.0 Backlog Cleanup — issues #78 and #104)
**Researched:** 2026-07-04
**Confidence:** MEDIUM (cross-checked web search across independent queries; no official-vendor-doc tier source reached for either sub-topic — see Sources)

*Replaces the v6.1 admin/user-management research previously in this file — that milestone shipped (Phases 38–42, see PROJECT.md). This is fresh research for the v7.0 Backlog Cleanup milestone.*

## Context Recap

Two of the four v7.0 backlog items need external UX-pattern research (the other two are mobile CSS bug fixes needing no research, per milestone scope):

1. **Client-side crop-before-save (#78 / v1.0 Phase 8, deferred)** — applies to character photo uploads and DM profile photo uploads. User picks a crop frame in-browser before saving; both the original and the cropped image are persisted so the character/DM details page keeps showing the original while the guild-member list shows the cropped "avatar." Previously paused because the plan required a server-side image-processing library (SkiaSharp) whose native binary availability on the deployment host was unverified — this is a key constraint for how the crop is implemented this time.
2. **Post-finalization waitlist auto-promotion (#104)** — already internally specified (Yes > Maybe > No, then signup time, no hard capacity block). Only open question for research: standard UX for notifying a promoted player without spamming people whose own action already told them the outcome.

Existing infra confirmed from PROJECT.md: Hangfire background jobs + Razor/HtmlRenderer HTML email templates + Resend SMTP relay (all built in v4.0, reused by 6+ job/template pairs already); photo upload fields already exist for character and DM profile; email budget is constrained (100/day, 3000/month Resend limit, 17 members — batch-first design already a stated principle).

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist once "crop before save" or "waitlist with auto-promotion" is advertised. Missing these makes the feature feel half-built or actively confusing.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Draggable/resizable crop frame over the uploaded image | This is the entire feature — a crop step without a manipulable frame is just "upload and hope the framing is right" | LOW | Cropper.js (canvas-based, vanilla JS, no framework dependency, ~14k GitHub stars, MIT-licensed) is the established off-the-shelf choice for exactly this — drop-in via `<script>`/npm, works directly inside a Razor view with no SPA framework needed |
| Fixed aspect ratio locked to the destination avatar shape | The guild-member list and DM profile render photos at one specific slot shape; an unconstrained crop lets users produce off-ratio images that get squashed, stretched, or letterboxed downstream | LOW | Cropper.js takes `aspectRatio` as a constructor option — no custom geometry math required |
| Live preview of the resulting crop before confirming | Users need to see what the avatar will actually look like (correct framing of a face/character) before committing — present in every avatar-cropping tool and library surveyed | LOW | Cropper.js ships a built-in preview binding (mirror the crop box into a separate preview element via its `preview` option) — no extra library |
| Zoom / pan the source image inside the crop frame | Lets a user recenter a full-body portrait onto just the face/torso without needing perfect framing at the source — explicitly described as baseline (not a differentiator) by every avatar-crop-specific tool surveyed | LOW | Cropper.js supports wheel-zoom and drag-to-pan on the source image by default |
| Original image preserved; cropped image generated and stored separately | Directly required by the milestone spec: the character/DM details page must keep showing the original while the guild-list shows the cropped version. Also the general pattern in every avatar tool surveyed — cropping is treated as non-destructive to the source | MEDIUM | This is primarily a **backend/storage** requirement, not a UI one: whatever currently holds a single image path per character/DM profile needs a second path/column for the cropped variant, plus an EF Core migration. Both upload flows (character, DM profile) need identical treatment — plan them together, since the underlying schema change is the same shape twice |
| Crop happens entirely client-side, before the network request | The milestone wants a client-side "crop before save" step, not a server-side post-upload cropper — this also directly avoids reopening the SkiaSharp/native-library deployment risk that paused this feature in v1.0 | LOW–MEDIUM | Cropper.js's `getCroppedCanvas()` produces a canvas; convert to Blob and attach to the existing upload `FormData` (or a hidden base64 field) before submit. The server receives two already-finished image byte streams (original file + cropped blob) and only needs to *store* them — no image processing library, no native dependency, no repeat of the SkiaSharp verification problem |
| Touch-usable crop interaction (drag/pinch) on mobile | The app already ships purpose-built `.Mobile.cshtml` views; a crop widget that only works with a mouse would be a new mobile-parity gap the moment it ships — ironic given this milestone is partly about *fixing* mobile parity gaps | LOW–MEDIUM | Cropper.js is touch-friendly by default (drag + pinch-to-zoom); still needs on-device verification against this app's existing mobile viewport/CSS since it's new interactive surface, not just a display change |
| Promoted player receives a targeted notification | This is explicitly called out in the milestone spec (issue #104) and corroborated as standard waitlist UX — the person who benefits from a passive event needs to be told, since nothing else in the UI would surface it to them otherwise | LOW | Reuses existing Hangfire job + Razor/HtmlRenderer + Resend SMTP pattern already proven 6+ times in this codebase; this is a new trigger condition and template, not new plumbing |
| Waitlist position/status visible somewhere in the UI, not just via email | Every waitlist UX source surveyed treats "confirm the user's current status when they check" as baseline — a promoted player should see their new confirmed status when they view the quest, not rely solely on an email that could be missed or land in spam | LOW | Likely already satisfied by the existing signup/vote display surface once the underlying status data model updates — verify during planning that the quest signup view reflects "confirmed via promotion" distinctly from "originally selected," if that distinction matters to players |

### Differentiators (Nice-to-Have, Not Required)

Features that would be pleasant additions but are explicitly *not* expected for a baseline implementation, per every source surveyed.

| Feature | Value Proposition | Complexity | Notes |
|---------|--------------------|------------|-------|
| Rotate / flip the image in the crop widget | Occasionally useful for a sideways phone photo | LOW | Cropper.js exposes `rotate()`/`scaleX()`/`scaleY()` for free once the library is in use — cheap to bolt on, but not required for the milestone's stated scope |
| Multiple aspect-ratio presets (e.g. square vs. portrait) | This app only has one target avatar shape per context, so preset-switching solves a problem this project doesn't have | LOW (if added) | Skip — one locked aspect ratio per upload field is sufficient; a preset picker is speculative flexibility with no current consumer |
| Filters / brightness / contrast adjustment | Common as an "additional feature" in consumer avatar-cropper web tools, but never described as expected — always listed beyond the core crop | MEDIUM | Out of scope — no evidence this is wanted for an internal campaign tracker; pure scope creep |
| Circular crop guide overlay | Some avatar croppers show a circular mask because many apps render avatars as circles | LOW | Only relevant if the guild-list/DM-profile avatar is rendered as a circle in existing CSS — verify current avatar display shape before adding; a circular guide over a square/rounded-card destination would mislead users about the real crop |
| In-app "you were promoted" banner in addition to the email | Belt-and-suspenders confirmation beyond email alone | LOW | Nice reinforcement, but the milestone spec and the general waitlist pattern both treat email as sufficient; add only if verification shows players miss the email |

### Anti-Features (Commonly Considered, Wrong Fit Here)

| Feature | Why It Seems Appealing | Why Problematic Here | Alternative |
|---------|--------------------------|------------------------|-------------|
| Server-side re-crop / image-processing pipeline (e.g. SkiaSharp/ImageSharp resize-on-save) | Feels "more robust" or "more correct" than trusting a client-generated canvas blob | This is precisely the path already tried and paused in v1.0 Phase 8 (PROJECT.md: "SkiaSharp native lib availability on deployment host unverified") — reopening that dependency risk repeats a known failure mode instead of avoiding it | Do the crop entirely client-side (canvas to Blob) and upload the already-cropped bytes directly; the server only stores two images, never processes them, which sidesteps the native-library deployment risk this feature has been blocked on for years |
| Free-form/unconstrained crop (any aspect ratio, any shape) | Feels like giving the user more control | Produces avatars that don't fit the guild-list card layout consistently — some stretched, some cropped oddly small — defeating the point of a uniform list UI | Lock `aspectRatio` to match the destination avatar slot exactly; every avatar-specific crop tool surveyed treats a fixed base ratio as the norm, not a limitation |
| Notifying the entire waitlist whenever any position shifts | Feels maximally "transparent" | Directly the notification-spam anti-pattern every waitlist UX source warns against ("send always" doesn't scale, erodes trust in the channel) — especially costly here given this app's constrained email budget (100/day, 3000/month Resend limit) and its existing "batch-first design" principle | Only the specific promoted player receives an email; other waitlisted players' position shifts are visible passively in the UI next time they check, never pushed |
| Notifying a player who changed their own vote/dropped out that "your status changed" | Feels like thorough coverage ("tell everyone whose status changed") | Their own action already told them the outcome — a confirmation email for something they just did themselves is redundant noise, exactly the case web sources single out to avoid | Only the "you were promoted" email fires, and only for players passively bumped by someone *else's* drop-out — this already matches the milestone's own spec (#104: "targeted email only for players auto-promoted by someone else's action") |
| Waitlist notifications via SMS or push, in addition to email | Faster/more attention-grabbing for time-sensitive queues (a documented pattern for restaurant/event waitlists) | This app is email-only by architecture and by explicit prior decision (PROJECT.md: no per-user opt-out, small trusted group, batch-first email design) — adding a channel contradicts existing constraints for a marginal gain in a non-time-critical context (a scheduled quest is not a "your table is ready in 2 minutes" scenario) | Keep the existing email channel; the urgency that justifies SMS in restaurant-waitlist products doesn't apply to scheduling a future game session |

## Feature Dependencies

```
[Client-side crop UI] (Cropper.js widget on upload form)
    └──requires──> [Existing photo upload form/field] (character photo, DM profile photo — already exist)
    └──produces──> [Two stored images: original + cropped]
                       └──requires──> [Storage/schema change: second image path/column] (EF Core migration)
                       └──enhances──> [Character/DM details page] (keeps showing original — likely no view change if original path is unchanged)
                       └──enhances──> [Guild-member list page] (swaps to cropped-image path for avatar display)

[Waitlist auto-promotion] (internal logic already specified — Yes > Maybe > No, then signup time)
    └──requires──> [Existing quest-signup + vote data model] (already exists)
    └──requires──> [Existing email dispatch infrastructure] (Hangfire job + HTML Razor template + Resend SMTP — already exists)
    └──produces──> [Targeted "you were promoted" email]
                       └──must NOT trigger for──> [The player whose own vote/drop-out action caused the promotion] (anti-pattern: redundant self-notification)
                       └──must NOT trigger for──> [Other waitlisted players unaffected by this specific promotion] (anti-pattern: notification spam)
```

### Dependency Notes

- **Crop UI requires only the existing upload forms** — no new upload endpoint is needed. The crop step is inserted client-side, before form submission, on the two fields that already exist (character photo, DM profile photo). This is additive JS/CSS plus a small controller/model change to accept and persist a second image.
- **Two stored images is the one genuinely new backend piece.** Whatever currently holds a single image path (character entity, DM profile entity) needs a second column for the cropped variant, plus a migration. Do both upload flows (character, DM profile) in the same pass — the schema change is structurally identical for both, so splitting them across separate phases would duplicate effort for no isolation benefit.
- **Crop UI has zero dependency on SkiaSharp or any server-side image library** if done as a pure client-side canvas step — this is the key design choice that unblocks the feature after years of being paused; confirm during planning that no part of the implementation reintroduces a server-side processing requirement.
- **Waitlist promotion email requires only a new trigger condition and template, not new infrastructure** — Hangfire + Razor/HtmlRenderer + Resend SMTP are already fully built. The only design risk is getting the trigger condition precisely right: fire only for the passively-promoted player, never for the player whose own action caused the change, never broadcast to the rest of the waitlist.
- **No dependency between the two research areas** — crop UX and waitlist promotion touch entirely different tables (image storage vs. quest signup/vote) and can be planned and built as independent phases with no ordering constraint between them.

## MVP Definition

### Ship With This Milestone

- [ ] Cropper.js (or an equivalent vanilla-JS canvas cropper) wired into both photo upload forms — character photo, DM profile photo — required because the milestone explicitly names both fields
- [ ] Fixed aspect ratio matching the guild-list/DM-profile avatar display shape — without it, crops look inconsistent across members
- [ ] Drag-to-position + zoom inside the crop frame — baseline interaction per every source surveyed, not optional
- [ ] Live preview of the crop result before submit — standard expectation, low implementation cost via Cropper.js's built-in preview option
- [ ] Original + cropped image both persisted; details page keeps rendering the original, guild-list renders the cropped version — explicitly required by the milestone spec
- [ ] Entirely client-side crop pipeline (no server-side image processing library) — required to avoid repeating the SkiaSharp deployment-verification blocker that paused this feature previously
- [ ] Targeted "you were promoted" email — sent only to the passively-promoted player, never to the player whose own action triggered it, never broadcast to the rest of the waitlist — matches issue #104's own spec and the general waitlist-UX pattern

### Explicitly Not This Milestone

- [ ] Rotate/flip control in the crop widget — cheap to add later on top of Cropper.js if requested, but not currently asked for
- [ ] Multiple aspect-ratio presets — no second consumer shape exists in this app; speculative
- [ ] Image filters/brightness/contrast — scope creep for an internal campaign tracker
- [ ] SMS/push notification channels for waitlist promotion — contradicts this app's existing email-only, batch-first design constraints
- [ ] Notifying the whole waitlist on every position change — directly the anti-pattern this research flags; only the promoted player gets an email

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|----------------------|----------|
| Draggable crop frame + fixed aspect ratio (Cropper.js) | HIGH | LOW | P1 |
| Live crop preview | HIGH | LOW | P1 |
| Original + cropped dual storage (schema/migration) | HIGH | MEDIUM | P1 |
| Zoom/pan inside crop frame | HIGH | LOW | P1 |
| Client-side-only crop pipeline (no server image lib) | HIGH (unblocks the feature) | LOW | P1 |
| Mobile touch verification for crop widget | MEDIUM | LOW–MEDIUM | P1 |
| Targeted waitlist-promotion email (no spam to others) | HIGH | LOW (infra exists; mostly template + trigger-condition work) | P1 |
| Rotate/flip | LOW | LOW | P3 |
| Multiple aspect-ratio presets | LOW | LOW | P3 |
| Image filters | LOW | MEDIUM | P3 |
| SMS/push for waitlist promotion | LOW (contradicts constraints) | MEDIUM | Do not build |
| Broadcast notification to entire waitlist on any change | NEGATIVE | LOW | Do not build |

**Priority key:**
- P1: Must have for this milestone
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Comparable-System Analysis

| Concern | Dedicated avatar-cropper web tools (Pokecut, AvatarCropper.org, ToolPoint) | Component libraries (react-avatar-editor, vue-avatar-cropper) | This App's Recommended Approach |
|---------|-------------------------------------------------------------------------------|-------------------------------------------------------------------|--------------------------------------|
| Aspect ratio | Fixed 1:1 stage, non-configurable | Configurable via a prop, usually set once per integrating app | Fixed, matched to this app's existing avatar display shape — no need to expose a ratio picker |
| Zoom/pan | Standard, drag + scroll | Standard, drag + scroll | Use Cropper.js defaults; no custom interaction logic needed |
| Original preserved | Implicit — the tool always re-crops from the uploaded source | Depends on integration; crop output is separate from the raw file input | Explicit requirement here: persist both original and cropped as two distinct stored images/paths |
| Framework coupling | Standalone web tools, no framework | React/Vue-specific wrapper components | This app is server-rendered Razor with no SPA framework — Cropper.js (framework-agnostic vanilla JS) fits better than a React/Vue-specific wrapper, avoiding a new frontend framework dependency for one widget |
| Waitlist notification scope | N/A | N/A | (Waitlist pattern, separate feature) DICE's waitlist UX lets the *user* choose notification channel and confirms waitlist join immediately; this app keeps the existing single channel (email) and confirms promotion only to the affected person, consistent with this app's simpler, smaller-scale context |

## Sources

- [Image Upload Pattern — UX Patterns for Developers](https://uxpatterns.dev/patterns/media/image-upload) — MEDIUM confidence
- [Cropper.js official site](https://fengyuanchen.github.io/cropperjs/) and [GitHub — fengyuanchen/cropperjs](https://github.com/fengyuanchen/cropperjs) — MEDIUM confidence (community project docs; not reached via an official vendor-verified channel in this research pass, but corroborated by multiple independent secondary sources describing identical capabilities)
- [Best image cropping tools for developers — Uploadcare blog](https://uploadcare.com/blog/best-tools-for-image-cropping/) — MEDIUM confidence
- [react-avatar-editor — GitHub](https://github.com/mosch/react-avatar-editor) — MEDIUM confidence
- [Avatar Cropper (avatarcropper.org)](https://avatarcropper.org/) and related consumer avatar-cropper tool pages — LOW–MEDIUM confidence (marketing pages for consumer tools, used only to corroborate the "fixed 1:1 + zoom + live preview = baseline" pattern across multiple independent products)
- [From Disappointment To Hope: A UX Deep Dive into DICE's Waitlist Feature — Medium](https://medium.com/@jasmine.oulmi/from-disappointment-to-hope-a-ux-deep-dive-into-dices-waitlist-feature-ef1491fdfef6) — MEDIUM confidence
- [Customizable text messages for waitlists and reservations — Waitlist Me](https://www.waitlist.me/features/customize-notifications/) and [Sending Notifications — WaitlistCare](https://waitlistcare.com/help/sending-notifications/) — MEDIUM confidence (corroborate targeted/filtered notification as best practice over broadcast-to-all)
- Existing project context: `.planning/PROJECT.md` (v7.0 milestone scope, existing email/Hangfire/Resend infra, prior SkiaSharp pause rationale, email volume constraints)

**Confidence caveat:** External findings come from web search cross-checked across multiple independent queries converging on the same conclusions (classified MEDIUM by this project's confidence tooling once corroborated) rather than a single authoritative vendor source — no official Cropper.js documentation portal or waitlist-UX standards body was reached in this pass. Treat the *pattern-level* conclusions (fixed aspect ratio + preview + zoom as baseline; targeted-not-broadcast notification as baseline) as reliable, since they were independently corroborated by 3+ unrelated sources each; treat any specific library API detail as needing a quick confirmation pass against the library's own README during implementation planning.

---
*Feature research for: D&D Quest Board v7.0 Backlog Cleanup — crop-before-save image UX and waitlist auto-promotion notification UX*
*Researched: 2026-07-04*
