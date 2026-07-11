# Phase 67: Remaining Quest Fields & Email Templates - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 15 (13 views + 1 CSS file + 2 email components; some views carry two independent edits)
**Analogs found:** 15 / 15 (every file's analog is Phase 66's own already-shipped Description wiring — in most cases the analog lives in the *same file*, right next to the code being added)

This phase is a mechanical repeat of Phase 66. Consequently almost every "analog" is not a different file — it's the Description block sitting a few lines above/below the Rewards or Recap block you're about to change, in the same file. Two flagged discrepancies below (pre-wrap CSS class sharing, and a QuestLog mobile Rewards gap) were found by direct read and are not mentioned, or are mentioned inaccurately, in `67-CONTEXT.md` — read those before planning those specific files.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Views/Quest/Create.cshtml` | view (write form) | request-response | same file, Description block (lines 29-36) | exact — same file |
| `Views/Quest/Create.Mobile.cshtml` | view (write form) | request-response | same file, Description block (lines 36-43) | exact — same file |
| `Views/Quest/Edit.cshtml` | view (write form) | request-response | same file, Description block (lines 31-38) | exact — same file |
| `Views/Quest/Edit.Mobile.cshtml` | view (write form) | request-response | same file, Description block (lines 45-52) | exact — same file |
| `Views/Quest/CreateFollowUp.cshtml` | view (write form) | request-response | same file, Description block (lines 35-42) | exact — same file |
| `Views/Quest/CreateFollowUp.Mobile.cshtml` | view (write form) | request-response | same file, Description block (lines 40-47) **for markup pattern**; `Views/Quest/CreateFollowUp.cshtml` Rewards block (lines 44-48) **for field placement/copy** — field does not exist yet on this file (D-02) | role-match (cross-file, since there is nothing to copy in-file) |
| `Views/Quest/Details.cshtml` | view (read display) | request-response | same file, Description rendering (line 32: `@Html.Markdown(Model.Quest?.Description)`) | exact — same file |
| `Views/Quest/Details.Mobile.cshtml` | view (read display) | request-response | same file, Description rendering (lines 98-102) | exact — same file |
| `Views/Quest/Manage.cshtml` | view (read display, new surface) | request-response | same file, Description collapsible section (lines 37-51) | exact — same file |
| `Views/Quest/Manage.Mobile.cshtml` | view (read display, new surface) | request-response | same file, Description collapsible section (lines 40-54) | exact — same file |
| `Views/QuestLog/Details.cshtml` | view (read display) | request-response | `Views/Quest/Details.cshtml` line 32 (cross-file, since this file's own Description block is untouched/out-of-scope — see Flagged Discrepancy 1) | role-match (cross-file) |
| `Views/QuestLog/Details.Mobile.cshtml` | view (read display) | request-response | `Views/Quest/Details.Mobile.cshtml` lines 98-102 (cross-file) — **Rewards has no existing markup in this file at all**, see Flagged Discrepancy 2 | partial — no in-file Rewards markup exists to swap |
| `Views/QuestLog/EditRecap.cshtml` | view (write form) | request-response | `Views/Quest/Create.cshtml` Description block (lines 29-36) — cross-file, this is the first field in the `QuestLog` write-form family to get the editor | role-match (cross-file) |
| `Views/QuestLog/EditRecap.Mobile.cshtml` | view (write form) | request-response | `Views/Quest/Create.Mobile.cshtml` Description block (lines 36-43) — cross-file | role-match (cross-file) |
| `wwwroot/css/quests.css` | config (CSS) | n/a | same file, `.quest-description-box`/`.recap-display-box` rules (lines 813-835) | exact — same file, see Flagged Discrepancy 1 |
| `Components/Emails/SessionReminder.razor` | component (email template) | request-response (server-rendered email) | `Components/Emails/QuestFinalized.razor` (already-shipped, correct pattern) | exact — sibling file, same directory |
| `Components/Emails/WaitlistPromoted.razor` | component (email template) | request-response (server-rendered email) | `Components/Emails/QuestFinalized.razor` (already-shipped, correct pattern) | exact — sibling file, same directory |

## Pattern Assignments

### Rewards write forms: `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `CreateFollowUp.cshtml`

**Analog:** the Description block already wired in the *same file* by Phase 66. Do the exact same swap on the Rewards block a few lines below it.

**Current Rewards markup to replace** (`Create.cshtml` lines 38-42, same shape in all 5 files, only the `asp-for`/`Value` path differs):
```cshtml
<div class="mb-3">
    <label asp-for="Rewards" class="form-label">Rewards</label>
    <textarea asp-for="Rewards" class="form-control" rows="4" placeholder="Describe the loot, gold, or other rewards for this quest..."></textarea>
    <span asp-validation-for="Rewards" class="text-danger"></span>
</div>
```

**Replace with** (mirrors the Description partial call directly above it, `Create.cshtml` lines 29-36):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Rewards",
       Value = Model.Rewards,
       Label = "Rewards",
       Required = false,
       Placeholder = "Describe the loot, gold, or other rewards for this quest..."
   }); }
```

**Per-file FieldName/Value binding path** (matches each file's existing `asp-for="..."` path exactly):
| File | FieldName | Value |
|---|---|---|
| `Create.cshtml` / `Create.Mobile.cshtml` | `"Rewards"` | `Model.Rewards` |
| `Edit.cshtml` / `Edit.Mobile.cshtml` | `"Quest.Rewards"` | `Model.Quest.Rewards` |
| `CreateFollowUp.cshtml` | `"Rewards"` | `Model.Rewards` |

`Required = false` in every case — confirmed `public string? Rewards { get; set; }` with no `[Required]` on both `QuestViewModel.cs` and `FollowUpQuestViewModel.cs` (`ViewModels/QuestViewModels/`), unlike `Description` which is `[Required]` on both. No `<span class="text-danger">*</span>` should be added.

No `@section Scripts` change needed on these 5 files — `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` already include `@{ await Html.RenderPartialAsync("_QuestFormScripts"); }`, and `CreateFollowUp.cshtml` already does too (verified: all 5 already load the EasyMDE CDN assets + antiforgery token via `_QuestFormScripts.cshtml` because Description already needed it in Phase 66).

---

### `Views/Quest/CreateFollowUp.Mobile.cshtml` (D-02 — Rewards field missing entirely)

**Confirmed by direct read:** this file has Description wired via `_MarkdownEditor` (lines 40-47, identical partial call to the other 5 forms) but has **no Rewards field at all** — it jumps straight from Description to ChallengeRating (line 50). Desktop's `CreateFollowUp.cshtml` has Rewards (lines 44-48); this mobile file never got it (a Phase 59 asymmetry, confirmed pre-existing).

**Insert this block** between the Description partial call (ends line 47) and the ChallengeRating block (starts line 50), copying the desktop `CreateFollowUp.cshtml` Rewards block's field but using `_MarkdownEditor` (not a plain textarea, since D-02 says this backfill happens in the same task that wires the editor in):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Rewards",
       Value = Model.Rewards,
       Label = "Rewards",
       Required = false,
       Placeholder = "Describe the loot, gold, or other rewards for this quest..."
   }); }
```
New field starts blank per `67-CONTEXT.md` D-02 — `FollowUpQuestViewModel.Rewards` is never pre-populated from the original quest on either desktop or mobile (confirmed: `FollowUpQuestViewModel.cs` has no such assignment logic in view; this is a controller-level behavior already true today, out of this phase's scope to change).

`_QuestFormScripts` is already included in this file's `@section Scripts` (needed for Description's editor already) — no change needed there.

---

### Rewards read views: `Views/Quest/Details.cshtml`, `Views/Quest/Details.Mobile.cshtml`

**`Details.cshtml`** — Description already renders via `Html.Markdown()` with no wrapping div (line 32). Rewards currently (lines 34-40):
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Quest?.Rewards))
{
    <div class="mb-4">
        <h5><i class="fas fa-coins text-warning me-2"></i>Rewards</h5>
        <div class="quest-description-box" style="white-space: pre-wrap;">@Model.Quest.Rewards</div>
    </div>
}
```
Swap `@Model.Quest.Rewards` for `@Html.Markdown(Model.Quest.Rewards)` and remove the inline `style="white-space: pre-wrap;"` attribute (this exact attribute is what `67-CONTEXT.md` names as needing removal — the class itself is not being touched here, see Flagged Discrepancy 1 below for why that alone isn't the full picture).

**`Details.Mobile.cshtml`** — Rewards currently (lines 103-109), uses a *different* class than desktop (`.quest-description-mobile`, not `.quest-description-box`):
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Quest?.Rewards))
{
    <hr class="quest-card-divider mt-2 mb-2" />
    <div class="quest-description-mobile">
        <i class="fas fa-coins me-1"></i>@Model.Quest.Rewards
    </div>
}
```
Swap `@Model.Quest.Rewards` for `@Html.Markdown(Model.Quest.Rewards)`. No pre-wrap cleanup needed here — confirmed via `wwwroot/css/quests.mobile.css` that `.quest-description-mobile` (lines 103-110) has no `white-space: pre-wrap` rule to begin with, and `.quest-header-card-mobile .markdown-content` (line 116) already exists as a text-color override scoped to this same card, so the new `.markdown-content` div `Html.Markdown()` injects will inherit correct parchment-colored text automatically — no new CSS needed.

---

### New Manage-page Rewards collapsible section (D-01): `Views/Quest/Manage.cshtml`, `Views/Quest/Manage.Mobile.cshtml`

**Analog:** the Description collapsible section Phase 66 already added to both these files. Copy the structure verbatim, changing only title/icon/collapse-target-id/model property.

**Desktop analog** (`Manage.cshtml` lines 37-51):
```cshtml
<div class="card modern-card mb-3">
    <div class="card-header modern-card-header">
        <h5 class="mb-0">
            <button class="btn btn-link text-decoration-none text-warning fw-bold p-0" type="button" data-bs-toggle="collapse" data-bs-target="#questDescriptionCollapse" aria-expanded="false" aria-controls="questDescriptionCollapse">
                <i class="fas fa-scroll me-2"></i>Description
                <i class="fas fa-chevron-down ms-2"></i>
            </button>
        </h5>
    </div>
    <div id="questDescriptionCollapse" class="collapse">
        <div class="card-body modern-card-body">
            @Html.Markdown(Model.Description)
        </div>
    </div>
</div>
```
New Rewards section (per `67-UI-SPEC.md`'s Copywriting Contract row): title `"Rewards"`, icon `fas fa-coins` (not `fas fa-scroll`), collapse target id `questRewardsCollapse` (desktop) / `questRewardsCollapseMobile` (mobile), collapsed by default (`class="collapse"`, no `show`), wrapper `<div class="card modern-card mb-3">` **omitted entirely when `Model.Rewards` is blank** (guard with `@if (!string.IsNullOrWhiteSpace(Model.Rewards)) { ... }` around the whole card — this is a new conditional Description's own section does not have, since Description is `[Required]` and Rewards is not).

**Mobile analog** (`Manage.Mobile.cshtml` lines 40-54) is structurally identical, just uses `questDescriptionCollapseMobile` as the id and sits inside `.dm-manage-section-card` rather than a `.card modern-card`. Apply the same id-suffix convention (`questRewardsCollapseMobile`) and the same blank-guard.

Place the new Rewards section immediately after the Description section in both files (mirrors Details/QuestLog's Description-then-Rewards ordering).

---

### `Views/QuestLog/Details.cshtml` — Rewards (lines 52-61) and Recap (lines 116-128)

**Rewards** — analog is `Quest/Details.cshtml`'s `Html.Markdown()` call (cross-file, since this file's own Description block at lines 44-50 is untouched/out-of-scope — see Flagged Discrepancy 1). Current:
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Quest.Rewards))
{
    <hr />
    <div class="mb-4">
        <h5 class="text-white"><i class="fas fa-coins text-warning me-2"></i>Rewards</h5>
        <div class="quest-description-box">@Model.Quest.Rewards</div>
    </div>
}
```
Swap `@Model.Quest.Rewards` for `@Html.Markdown(Model.Quest.Rewards)`.

**Recap** — current (lines 116-128):
```cshtml
<div class="mb-4">
    <h5 class="text-white"><i class="fas fa-book me-2"></i>Session Recap</h5>
    @if (!string.IsNullOrWhiteSpace(Model.Quest.Recap))
    {
        <div class="recap-display-box">
            @Model.Quest.Recap
        </div>
    }
    else
    {
        <p class="text-muted">No recap has been written for this quest yet.</p>
    }
    ...
</div>
```
Swap `@Model.Quest.Recap` for `@Html.Markdown(Model.Quest.Recap)`. The empty-state `<p class="text-muted">No recap has been written for this quest yet.</p>` branch is unchanged — stays outside the Markdown pipeline verbatim, per `67-UI-SPEC.md`'s Copywriting Contract.

---

### `Views/QuestLog/Details.Mobile.cshtml` — Recap only (lines 91-104); Rewards has no existing markup (Flagged Discrepancy 2)

**Recap** — current:
```cshtml
<div class="mb-0">
    <h6><i class="fas fa-book me-2"></i>Session Recap</h6>
    @if (!string.IsNullOrWhiteSpace(Model.Quest.Recap))
    {
        <div class="recap-display-box">
            @Model.Quest.Recap
        </div>
    }
    else
    {
        <p class="mb-0 text-muted">No recap has been written for this quest yet.</p>
    }
    ...
</div>
```
Swap `@Model.Quest.Recap` for `@Html.Markdown(Model.Quest.Recap)`, same as desktop.

**Rewards: see Flagged Discrepancy 2 below** — this file has no Rewards display block to swap.

---

### Recap write forms: `Views/QuestLog/EditRecap.cshtml`, `Views/QuestLog/EditRecap.Mobile.cshtml`

**Analog:** cross-file — `Views/Quest/Create.cshtml`'s `_MarkdownEditor` partial call, since Recap's current form is a hand-rolled `<textarea name="recap" ...>` (not `asp-for`-bound — `EditRecapViewModel.Recap` is bound by convention-matched parameter name, confirmed in `EditRecapViewModel.cs`) with no editor.

**Current** (`EditRecap.cshtml` lines 21-33):
```cshtml
<div class="mb-3">
    <label for="recap" class="form-label">
        Write or update the session recap:
    </label>
    <textarea class="form-control"
              id="recap"
              name="recap"
              rows="10"
              placeholder="Describe what happened during this quest...">@Model.Recap</textarea>
    <small class="form-text text-muted">
        Share the story of this adventure with your players!
    </small>
</div>
```
**Replace with**, per `67-UI-SPEC.md`'s Copywriting Contract row for the Recap editor field:
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "recap",
       Value = Model.Recap,
       Label = "Recap",
       Required = false,
       Placeholder = "Describe what happened during this quest..."
   }); }
<small class="form-text text-muted">
    Share the story of this adventure with your players!
</small>
```
Note: `FieldName = "recap"` (lowercase) must match the existing `name="recap"` binding — this form does not use `asp-for`, it binds `EditRecapViewModel.Recap` via the raw `name="recap"` convention already in place; changing the field name would break model binding. The encouragement line has no slot in `_MarkdownEditor.cshtml` (label/textarea/validation only per `ViewModels/Shared/MarkdownEditorViewModel.cs`) so it must stay as a sibling `<small>` element immediately after the partial include, exactly as it sits today relative to the textarea.

`EditRecap.Mobile.cshtml` (lines 26-34) — identical swap, `rows="6"` on the current textarea has no equivalent on `_MarkdownEditor` (which is hardcoded `rows="4"` in the partial itself, `Views/Shared/_MarkdownEditor.cshtml` line 18) — this is consistent with how Description's forms behave today (no per-instance row-count control), not a regression to flag.

**Both `EditRecap.cshtml` and `EditRecap.Mobile.cshtml` currently do NOT include `_QuestFormScripts`** (confirmed by direct read — neither has a `@section Scripts` block at all). Since `_QuestFormScripts.cshtml` is what loads the EasyMDE CDN CSS/JS, the antiforgery token (`window.markdownAntiforgeryToken`), and `markdown-editor.js` (which self-initializes every `.markdown-editor-textarea` on `DOMContentLoaded`), **both files need a new `@section Scripts { @{ await Html.RenderPartialAsync("_QuestFormScripts"); } }` block added** — without it the Recap editor will render as a plain unstyled textarea with no toolbar/preview, since nothing will call `initMarkdownEditor()`. `_QuestFormScripts.cshtml`'s own internal date-validation JS (`document.querySelector('form[action*="/Quest/"]')`) is scoped to quest forms by URL substring and will simply no-op harmlessly on `/QuestLog/EditRecap` (no `#proposed-dates` container, and the form action doesn't match `/Quest/` anyway since `EditRecap` posts to `QuestLog`), so including the shared partial is safe — it doesn't need generalizing further.

---

### Email templates: `Components/Emails/SessionReminder.razor`, `Components/Emails/WaitlistPromoted.razor`

**Analog:** `Components/Emails/QuestFinalized.razor` — already shipped in Phase 66 (commits `719fad3` feat(66-03), `ed1c696` fix(66) CR-01). Confirmed current shipped state by direct read:

```csharp
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Service.Components.Emails
@inject IMarkdownService MarkdownService
```
and, in the Description row:
```cshtml
<div style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>
```
Note the tag is `<div>`, not `<p>` — this was CR-01's fix (`<p>` cannot legally contain block-level HTML like Markdig's own `<p>`/`<ul>`/`<blockquote>` output, silently dropping structure). Copy the `<div>` form directly; do not use `<p>`.

**`SessionReminder.razor` current state** (confirmed unchanged from pre-Phase-66, lines 1-2 and 40-43):
```cshtml
@using QuestBoard.Service.Components.Emails

<_EmailLayout ...>
```
```cshtml
<div style="height:100%;overflow-y:auto;padding-right:6px;">
    <p style="font-size:14px;font-weight:700;font-family:Georgia,serif;color:#1a0f08;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0 0 4px 0;">The Adventure:</p>
    <p style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@QuestDescription</p>
</div>
```
Add `@using QuestBoard.Domain.Interfaces` and `@inject IMarkdownService MarkdownService` at the top (matching `QuestFinalized.razor`'s first 3 lines exactly), then swap only the second `<p>` (the one holding `@QuestDescription`, not the "The Adventure:" label `<p>`) for a `<div>` wrapping `@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))`, keeping the same inline `style` attribute contents. The "The Adventure:" label `<p>` stays a `<p>` — it's static text, not rendered Markdown, and is unaffected by CR-01's fix.

**`WaitlistPromoted.razor` current state** (confirmed unchanged, lines 1-2 and 40-43):
```cshtml
@using QuestBoard.Service.Components.Emails

<_EmailLayout ...>
```
```cshtml
<div style="height:100%;overflow-y:auto;padding-right:6px;">
    <p style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@QuestDescription</p>
</div>
```
Same fix as `SessionReminder.razor`: add the `@using`/`@inject` lines, swap the single `<p>@QuestDescription</p>` for `<div>@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>`.

`QuestFinalized.razor` itself needs **no changes** — reference-only.

---

## Shared Patterns

### `_MarkdownEditor.cshtml` partial + `MarkdownEditorViewModel`
**Source:** `Views/Shared/_MarkdownEditor.cshtml`, `ViewModels/Shared/MarkdownEditorViewModel.cs`
**Apply to:** every write-form field this phase touches (Rewards x6, Recap x2)
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "...",      // must match the model-binding name (asp-for path, or raw name="..." for EditRecap)
       Value = Model...,
       Label = "...",
       Required = false,       // true only for Description; Rewards and Recap are both optional
       Placeholder = "..."
   }); }
```
The partial itself renders the label + info-icon tooltip + `<textarea class="markdown-editor-textarea">` + validation span — do not hand-roll any of that.

### `_QuestFormScripts.cshtml` — EasyMDE CDN + antiforgery + init script loader
**Source:** `Views/Quest/_QuestFormScripts.cshtml`
**Apply to:** every write-form file this phase touches. Already included by all `Views/Quest/*` forms in scope (no action needed there). **Must be newly added** to `Views/QuestLog/EditRecap.cshtml` and `Views/QuestLog/EditRecap.Mobile.cshtml` (see that section above) since those files never needed it before this phase.
```cshtml
@section Scripts {
    @{ await Html.RenderPartialAsync("_QuestFormScripts"); }
}
```

### `Html.Markdown()` read-side helper
**Source:** `Extensions/HtmlHelperExtensions.cs`
```csharp
internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
{
    var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
    var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
    return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
}
```
**Apply to:** every read-side swap in this phase (`@Model.Quest.Rewards` → `@Html.Markdown(Model.Quest.Rewards)`, same for `.Recap`). Always wraps output in `.markdown-content` automatically — do not add another wrapping div for styling, the helper already does it.

### Email `@inject IMarkdownService` + `<div>` wrapper
**Source:** `Components/Emails/QuestFinalized.razor` (already shipped)
**Apply to:** `SessionReminder.razor`, `WaitlistPromoted.razor`
```cshtml
@using QuestBoard.Domain.Interfaces
@inject IMarkdownService MarkdownService
...
<div style="...">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>
```
Never `<p>` — Markdig's block-level output (`<p>`, `<ul>`, `<blockquote>`) is illegal inside a `<p>` and silently drops in most mail clients (CR-01 finding, Phase 66).

### Manage-page collapsible section (D-01/D-02 origin)
**Source:** `Views/Quest/Manage.cshtml` lines 37-51 / `Manage.Mobile.cshtml` lines 40-54 (Description, already shipped)
**Apply to:** new Rewards section on both files. Structure: `card modern-card mb-3` > `card-header` with `btn-link` + `data-bs-toggle="collapse"` + chevron icon > `id="..."` target div with class `collapse` (collapsed by default) > `card-body modern-card-body` containing `@Html.Markdown(...)`. Guard the whole card with a blank-check `@if` since Rewards (unlike Description) can be empty.

## Flagged Discrepancies (found by direct read, not fully covered by 67-CONTEXT.md/67-UI-SPEC.md)

### 1. `.quest-description-box` / `.recap-display-box` CSS classes are shared between in-scope and out-of-scope content

Both CSS classes carry a class-level `white-space: pre-wrap` rule in `wwwroot/css/quests.css` (lines 813-835):
```css
.quest-description-box { ... white-space: pre-wrap; }
.recap-display-box { ... white-space: pre-wrap; }
```
`67-CONTEXT.md`'s pre-wrap cleanup decision only asks to remove the **inline** `style="white-space: pre-wrap;"` on `Quest/Details.cshtml`'s Rewards div, and to remove `.recap-display-box`'s **CSS rule** entirely. Direct read shows this is incomplete/risky:

- **`.quest-description-box`** is used by THREE distinct blocks across this phase's files: `Quest/Details.cshtml` Rewards (in-scope, inline pre-wrap being removed per context — but the *class* still carries `pre-wrap` after that edit, so the inline removal alone has no visible effect unless the class rule is also addressed for this specific usage), `QuestLog/Details.cshtml` Description (line 47, **untouched/out-of-scope this phase**, must keep rendering raw pre-wrap plain text), and `QuestLog/Details.cshtml` Rewards (line 59, in-scope, needs pre-wrap gone once it renders `Html.Markdown()` output).
- **`.recap-display-box`** is used by FOUR blocks: `QuestLog/Details.cshtml` Recap (in-scope), `QuestLog/Details.Mobile.cshtml` Recap (in-scope), `QuestLog/Details.Mobile.cshtml` **Description** (line 45, untouched/out-of-scope, must keep pre-wrap), and (desktop) `QuestLog/Details.cshtml` does NOT use this class for Description (desktop uses `.quest-description-box` for Description instead — the two platforms are inconsistent about which shared class Description uses).

Removing `white-space: pre-wrap` from either class at the CSS-rule level (as `67-CONTEXT.md` directs for `.recap-display-box`) will also strip line-break preservation from the untouched, still-plain-text Description blocks that share the class, until a later phase (68/69/70?) migrates Description on `QuestLog/Details(.Mobile).cshtml` too. This may be an acceptable, understood trade-off (cosmetic only — Description text without blank-line-separated paragraphs would visually run together) or may need a scoped selector (e.g. a `.markdown-content`-adjacent modifier, or per-block inline override on just the Recap/Rewards divs) — this is a planning decision, not resolved here.

### 2. `Views/QuestLog/Details.Mobile.cshtml` has no Rewards display block at all

`67-CONTEXT.md`'s Integration Points section lists `QuestLog/Details.Mobile.cshtml` alongside `QuestLog/Details.cshtml` as a "Rewards read view" needing `@Model.Quest.Rewards` swapped for `@Html.Markdown(Model.Quest.Rewards)`. Direct read of the full file (170 lines) confirms **no Rewards markup exists there at all** — the file goes straight from "Original Quest Description" to "Adventurers" to "Session Recap," with no Rewards section, unlike the desktop `QuestLog/Details.cshtml` which has one (lines 52-61). This mirrors the same kind of desktop/mobile asymmetry `67-CONTEXT.md`'s own D-02 (`CreateFollowUp.Mobile.cshtml`) calls out as a "Phase 59 asymmetry" worth fixing in-phase per this project's Phase 43/54/61 mobile-parity lesson — but unlike D-02, this gap was not raised as an explicit decision in `67-CONTEXT.md`. Planner should decide whether to (a) add a new Rewards block to this file for mobile parity (mirroring `QuestLog/Details.cshtml` desktop's structure, adapted to the mobile card conventions already used elsewhere in this file), or (b) treat this as a pre-existing gap outside this phase's stated scope and defer it.

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/Quest/`, `QuestBoard.Service/Views/QuestLog/`, `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml`, `QuestBoard.Service/Components/Emails/`, `QuestBoard.Service/wwwroot/css/quests.css` + `quests.mobile.css`, `QuestBoard.Service/wwwroot/js/markdown-editor.js`, `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs`, `QuestBoard.Service/ViewModels/{QuestViewModels,QuestLogViewModels,Shared}/`
**Files scanned:** 24 (all files read directly; no file over 2,000 lines encountered, no offset/limit reads needed)
**Pattern extraction date:** 2026-07-10
