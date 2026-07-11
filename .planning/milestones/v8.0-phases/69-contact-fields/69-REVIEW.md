---
phase: 69-contact-fields
reviewed: 2026-07-10T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs
  - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Create.cshtml
  - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Details.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
  - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/markdown-content.css
  - QuestBoard.Service/wwwroot/js/markdown-editor.js
findings:
  critical: 0
  warning: 5
  info: 1
  total: 6
status: issues_found
---

# Phase 69: Code Review Report

**Reviewed:** 2026-07-10
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

Reviewed the Contact Fields markdown-editor wiring (Create/Edit/Details, desktop + mobile) plus the shared infrastructure this phase modified (`MarkdownEditorViewModel.ElementId`, `_MarkdownEditor.cshtml`, `markdown-editor.js`, `markdown-content.css`). The `ElementId`/`FieldName` split for per-note editors is implemented correctly and does not disturb existing single-instance call sites (Quest, Character): every pre-existing site implicitly passes `ElementId = null` and remains visible on load, so the new `offsetParent` guard never skips them, and no consumer form currently has fields hidden at initial page load, so no regression from the visibility guard was found in Quest/Character/QuestLog forms.

However, the desktop Contact Details view has an incomplete fix: the team explicitly diagnosed and fixed "pale-gold Markdown heading on near-white note background" for **mobile** (`contact-detail.mobile.css`, commit `5f7b64cf`) but the identical bug exists on **desktop** and was never patched, because the desktop override rule only matches `.markdown-content` when it is a *direct* child of `.modern-card-body` — Notes render one level deeper. A parallel legibility gap exists on mobile for note links. Two additional findings concern robustness of the shared `markdown-editor.js` infra (used by Quest/Character/Contact alike): a missing `response.ok` check in the preview fetch, and a submit-button selector that silently no-ops for buttons that rely on HTML's implicit `type="submit"` default. A CodeMirror re-show-without-refresh gap in the per-note lazy-init/collapse cycle rounds out the findings.

## Warnings

### WR-01: Contact Note Markdown headings are illegible on the desktop Details view (mobile-only fix, desktop untouched)

**File:** `QuestBoard.Service/wwwroot/css/markdown-content.css:79-92`
**Issue:** `.markdown-content h1..h6` sets `color: #F4E4BC` (pale gold) with a dark drop-shadow, intended for dark-overlay boxes. The only override back to dark, readable text is scoped to a *direct* child: `.modern-card-body > .markdown-content h1..h6 { color: #1a1a1a; }`.

On `Contacts/Details.cshtml` (desktop), the Description field's `.markdown-content` **is** a direct child of `.modern-card-body`, so it correctly renders dark. But each Note's markdown is nested one level deeper — `.modern-card-body > .note-item > .note-view-text > .markdown-content` (see `Details.cshtml:140`) — so the direct-child selector never matches, and any heading a user adds via the editor's "heading" toolbar button (`markdown-editor.js:28`) renders in pale gold with a dark 1px text-shadow against `.note-item`'s near-white background (`background: rgba(0, 0, 0, 0.03)` in `contacts.css:214-216`), which is nearly invisible — exactly the failure mode the team already diagnosed and fixed for mobile in commit `5f7b64cf` ("Markdown headings inside a Contact Note rendered pale-gold-on-white ... nearly illegible against `.note-item`'s near-white card"). `contact-detail.mobile.css:92-104` added a `.contact-detail-card .note-item h1..h6 { color: inherit !important; }` override for the mobile view only; no equivalent exists for desktop.
**Fix:**
```css
/* In a desktop contact-detail stylesheet, or scoped in markdown-content.css: */
.modern-card-body .note-item h1,
.modern-card-body .note-item h2,
.modern-card-body .note-item h3,
.modern-card-body .note-item h4,
.modern-card-body .note-item h5,
.modern-card-body .note-item h6 {
    color: #1a1a1a;
    text-shadow: none;
}
```

### WR-02: Mobile Contact Note Markdown links keep dark-background styling against the light note background

**File:** `QuestBoard.Service/wwwroot/css/contact-detail.mobile.css:76-104`
**Issue:** The catch-all parchment rule at lines 76-83 applies `color: #F4E4BC !important` plus a heavy dark text-shadow to every `p`, `a`, `li`, and `span:not(.badge)` inside `.contact-detail-card`, including inside `.note-item`. The subsequent "scope-out" rule (lines 92-104) reverses this for `.note-item`'s `p`, `li`, `span`, and `h1`-`h6` — but omits `a`. Since the Markdown editor's toolbar includes a "link" button (`markdown-editor.js:28`), any link a user adds to a Note renders with pale-gold text and a dark drop-shadow against `.note-item`'s near-white background (`rgba(255, 255, 255, 0.85)`, line 113) — the same illegibility problem this file's own comment (lines 85-91) describes and fixes for headings/paragraphs/lists, just left unfixed for anchors.
**Fix:**
```css
.contact-detail-card .note-item,
.contact-detail-card .note-item p,
.contact-detail-card .note-item a,
.contact-detail-card .note-item li,
.contact-detail-card .note-item span:not(.badge),
.contact-detail-card .note-item h1,
...
```

### WR-03: Reopening a previously canceled note editor never calls `codemirror.refresh()`

**File:** `QuestBoard.Service/Views/Contacts/Details.cshtml:203-223`, `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml:196-223`
**Issue:** The lazy-init comment (`Details.cshtml:216-218`) correctly explains why EasyMDE must be constructed only after its container becomes visible: "never construct EasyMDE while the container is display:none (avoids the CodeMirror hidden-container sizing bug)." That guard is applied to *construction*, but the same class of bug applies to *re-showing* an already-constructed CodeMirror instance: `collapseNote()` sets `.note-edit-form` back to `display: none` (line 191), and the second time a user clicks Edit on that same note, `noteEditors.has(noteId)` is already `true` (line 219), so `initMarkdownEditor` is skipped entirely — `editForm.style.display = 'block'` is set (line 214) but `entry.instance.codemirror.refresh()` is never called. CodeMirror is documented to require a `refresh()` call after being unhidden, or it can render with stale/zero measurements (collapsed height, broken line wrapping) until the user interacts with the window. First-open works fine; second-and-later opens of the same note (open → cancel → open again) are at risk.
**Fix:**
```javascript
if (!noteEditors.has(noteId)) {
    const instance = initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    noteEditors.set(noteId, { instance: instance, originalText: textarea.value });
} else {
    noteEditors.get(noteId).instance.codemirror.refresh();
}
```

### WR-04: Preview fetch does not check `response.ok` before injecting the response body as HTML

**File:** `QuestBoard.Service/wwwroot/js/markdown-editor.js:32-53`
**Issue:** `fetch('/markdown/preview', ...)` only rejects on a network-level failure; a non-2xx HTTP response (e.g., an expired/invalid antiforgery token returning 400, or a 500) still resolves normally. The code chains straight to `response.text()` and injects the result into `previewElement.innerHTML` (lines 43-46) without checking `response.ok`, so the friendly `.catch()` fallback (`'Preview failed to load.'`) never fires for HTTP-level errors — instead, whatever the server's error page/body is (raw HTML, potentially a full error page) gets rendered directly inside the Preview pane.
**Fix:**
```javascript
.then(function (response) {
    if (!response.ok) {
        throw new Error('Preview request failed: ' + response.status);
    }
    return response.text();
})
```

### WR-05: Submit-sync fix depends on an explicit `type="submit"` attribute and silently no-ops for implicit-submit buttons

**File:** `QuestBoard.Service/wwwroot/js/markdown-editor.js:69-76`
**Issue:** This phase's fix for "click Save/Add Note, nothing happens" (commit `0589221f`) selects submit controls via `form.querySelectorAll('button[type="submit"], input[type="submit"]')`. Per the HTML spec, a `<button>` inside a `<form>` with **no** `type` attribute at all defaults to `type="submit"` — but the attribute selector `button[type="submit"]` only matches buttons that *explicitly* declare the attribute. Every current consumer (Quest, Character, Contacts) happens to declare `type="submit"` explicitly, so there is no live regression today, but this is the exact shared-infra fragility the task called out to check: any future field wired into this shared editor with a bare `<button>Save</button>` submit control will silently reintroduce the original bug this commit fixes, with no error and no test signal.
**Fix:**
```javascript
form.querySelectorAll('button, input[type="submit"]').forEach(function (submitControl) {
    if (submitControl.tagName === 'BUTTON' && submitControl.type !== 'submit') return; // type defaults to 'submit'
    submitControl.addEventListener('click', function () {
        easyMDE.codemirror.save();
    });
});
```

## Info

### IN-01: EasyMDE's live Preview pane doesn't mirror heading styling from the saved page

**File:** `QuestBoard.Service/wwwroot/css/markdown-content.css:67-77`
**Issue:** The comment at lines 67-71 explains that `.editor-preview` (EasyMDE's live Preview pane) renders the same Markdig-generated HTML as the saved page, and duplicates the `blockquote` rule onto `.editor-preview` to fix a preview/saved-page mismatch. The same mismatch exists for headings: `.markdown-content h1..h6` (font-size scale, color, `line-height`) and `white-space: normal` are never duplicated onto `.editor-preview`, so a user previewing a Description or Note with a heading sees EasyMDE's default preview typography, not the size/color it will actually render at once saved (Description on a dark box, or dark-card text per WR-01). Lower severity than WR-01/WR-02 since it only affects the live-editing Preview tab, not the persisted page.
**Fix:** Extend the existing duplication pattern to headings, e.g. `.editor-preview h1, .editor-preview h2, ... { font-weight: 700; line-height: 1.2; }` plus matching font-size rules, or add a shared class both contexts include.

---

_Reviewed: 2026-07-10_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
