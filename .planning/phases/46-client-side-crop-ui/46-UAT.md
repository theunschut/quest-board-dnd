---
status: testing
phase: 46-client-side-crop-ui
source: [46-VERIFICATION.md]
started: 2026-07-07T22:35:00Z
updated: 2026-07-07T22:35:00Z
---

## Current Test

number: 1
name: Confirm the DM-profile-shows-cropped (not original) deviation from ROADMAP.md Success Criterion #3's literal text is an acceptable permanent product decision, not something that should be corrected before this phase is considered closed.
expected: |
  Human either (a) confirms D-03 as the final intended behavior and the roadmap/requirements wording should be updated to match, or (b) determines DM Profile should in fact show the original and requests a follow-up fix.
awaiting: user response

## Tests

### 1. D-03 DM-Profile-shows-cropped deviation from literal roadmap/requirement wording
expected: Human either (a) confirms D-03 as the final intended behavior and the roadmap/requirements wording should be updated to match, or (b) determines DM Profile should in fact show the original and requests a follow-up fix.
result: [pending]

### 2. Real touchscreen device: confirm the crop frame responds correctly to drag and pinch gestures
expected: Smooth, precise response to touch gestures with no missed input.
result: [pending]

### 3. Real phone-camera EXIF orientation: upload a real phone-camera portrait photo through any of the 6 crop-enabled forms on a real iOS Safari device
expected: Photo displays with correct orientation everywhere the cropped/original image is shown.
result: [pending]

### 4. Real iOS Safari canvas-memory ceiling: upload a full-resolution (12MP+) camera photo through the crop flow on real iOS Safari
expected: Canvas renders and remains interactive; no crash, no blank canvas.
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
