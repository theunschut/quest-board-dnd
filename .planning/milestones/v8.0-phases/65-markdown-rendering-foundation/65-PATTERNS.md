# Phase 65: Markdown Rendering Foundation - Pattern Map

**Mapped:** 2026-07-09
**Files analyzed:** 5 (3 new, 2 modified)
**Analogs found:** 5 / 5

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `QuestBoard.Domain/Interfaces/IMarkdownService.cs` | service interface | transform (string? → string) | `QuestBoard.Domain/Interfaces/IImageValidationService.cs` | exact |
| `QuestBoard.Domain/Services/MarkdownService.cs` | service | transform (string? → string) | `QuestBoard.Domain/Services/ImageValidationService.cs` | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` | config (DI registration) | n/a | self (existing `AddDomainServices` method) | exact — self-modification |
| `QuestBoard.Domain/QuestBoard.Domain.csproj` | config (package manifest) | n/a | self (existing `PackageReference` block) | exact — self-modification |
| `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` | test | transform (pure-function unit test) | `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` | exact |

No files with zero analog — this phase's shape (stateless Domain transform service, its interface, its DI registration, its unit test) is a well-established pattern in this codebase via the v7.0 `ImageValidationService` precedent. The only genuinely new element is the **third-party libraries themselves** (Markdig, Ganss.Xss `HtmlSanitizer`) — there is no prior codebase usage of either (confirmed via `Grep` — zero hits outside `.planning/`). For that library-specific code (pipeline composition, dual-sanitizer construction), RESEARCH.md's "Code Examples" section (source-verified against the libraries' own GitHub repos) is the authoritative reference, not a codebase analog — cited inline below where relevant.

## Pattern Assignments

### `QuestBoard.Domain/Interfaces/IMarkdownService.cs` (service interface, transform)

**Analog:** `QuestBoard.Domain/Interfaces/IImageValidationService.cs` (full file, 22 lines)

**Full pattern to copy** (`QuestBoard.Domain/Interfaces/IImageValidationService.cs:1-21`):
```csharp
namespace QuestBoard.Domain.Interfaces;

public interface IImageValidationService
{
    /// <summary>
    /// Validates an original (required-if-present) plus an optional cropped image pair against the
    /// shared MIME allowlist, extension allowlist, and size limit. A null or zero-length file is
    /// treated as absent and never produces an error. Returns errors keyed by field name (empty
    /// list means both files are valid).
    /// </summary>
    IList<ImageValidationError> ValidateImagePair(ImageFileInput? original, ImageFileInput? cropped);
}

/// <summary>
/// The subset of an uploaded file's metadata needed for validation, kept as primitive values
/// (not IFormFile) so the validator can be unit-tested without constructing upload fakes and so
/// the Domain layer stays free of a hard dependency on ASP.NET Core upload types.
/// </summary>
public record ImageFileInput(long Length, string ContentType, string FileName, string FieldName);

public record ImageValidationError(string FieldName, string Message);
```

**What to mirror:**
- Bare `namespace QuestBoard.Domain.Interfaces;` — no other usings needed for a pure-primitive-in/primitive-out contract.
- XML doc comment on the single interface method explaining input/output semantics in plain language (per this project's CLAUDE.md — no phase/requirement IDs in the comment).
- Same file hosts small supporting types (`ImageFileInput`, `ImageValidationError` records) alongside the interface — apply the same shape for RESEARCH.md's `MarkdownRenderTarget` enum (Pattern 1 in RESEARCH.md): put `public enum MarkdownRenderTarget { Web, Email }` in this same file, next to `IMarkdownService`, exactly as `ImageValidationError`/`ImageFileInput` sit next to `IImageValidationService`.
- Public interface method returns a concrete value (`IList<...>` here; `string` for the Markdown case) — never `void`, never throws for expected "empty input" cases (see zero-length-file handling below, mirrored by null/empty-markdown handling).

---

### `QuestBoard.Domain/Services/MarkdownService.cs` (service, transform)

**Analog:** `QuestBoard.Domain/Services/ImageValidationService.cs` (full file, 51 lines)

**Full pattern to copy** (`QuestBoard.Domain/Services/ImageValidationService.cs:1-51`):
```csharp
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Domain.Services;

internal class ImageValidationService : IImageValidationService
{
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif"];
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <inheritdoc/>
    public IList<ImageValidationError> ValidateImagePair(ImageFileInput? original, ImageFileInput? cropped)
    {
        var errors = new List<ImageValidationError>();

        ValidateSingle(original, errors);
        ValidateSingle(cropped, errors);

        return errors;
    }

    // Kept as its own helper (rather than inlined) so a future magic-byte/file-signature check
    // could be added to this one spot later without restructuring the caller.
    private static void ValidateSingle(ImageFileInput? file, List<ImageValidationError> errors)
    {
        // A null or zero-length file is treated as absent -- this phase has no crop UI, so an
        // absent cropped file must never be an error.
        if (file == null || file.Length == 0)
        {
            return;
        }
        // ... allowlist checks ...
    }
}
```

**What to mirror:**
- `internal class MarkdownService : IMarkdownService` — same `internal` visibility (not `public`), relying on `InternalsVisibleTo("QuestBoard.UnitTests")` (see AssemblyInfo section below) for testability. Do not make the class `public`.
- Constants/config as `private static readonly` fields or `private const` at the top of the class (mirrors `AllowedMimeTypes`/`AllowedExtensions`/`MaxFileSizeBytes`) — apply this to the two `HtmlSanitizerOptions`-derived `HtmlSanitizer` instances and the one `MarkdownPipeline`, all three built once and held as `private static readonly` fields (RESEARCH.md Pattern 2/3 confirms these must never be mutated after construction — this project's existing "build once, hold as static readonly" idiom is the same shape already used for `AllowedMimeTypes` etc., just applied to more complex objects).
- `/// <inheritdoc/>` on the public method, matching the interface's XML doc rather than duplicating prose.
- No constructor-injected dependencies (no `IServiceScopeFactory`, no repository, nothing) — this is what makes `AddSingleton` safe, and what RESEARCH.md's ARCHITECTURE.md confirms doesn't need `HangfireJobHelper`-style scope bridging. `ImageValidationService` has zero constructor parameters; `MarkdownService` should also have zero constructor parameters (the pipeline/sanitizers are built as static/instance readonly fields initialized inline or in a parameterless constructor, not injected).
- Defensive short-circuit for the "absent" case at the top of the method (`file == null || file.Length == 0` → `return`) — mirror this shape for `string.IsNullOrWhiteSpace(markdown)` → return `string.Empty` (or similar) before invoking Markdig, rather than letting Markdig process an empty/null string unnecessarily.
- Small private static helper methods for distinct sub-steps, each with a comment explaining *why* it's separated (not *what* it does) — apply this to a private helper that builds each `HtmlSanitizerOptions` variant, if the constructor logic gets long enough to warrant it.

**Library-specific composition** (no codebase analog exists — copy from RESEARCH.md's source-verified snippets, which read Markdig's and Ganss.Xss's actual GitHub source, not just docs):

Pipeline construction (RESEARCH.md lines 308-317, verified against `xoofx/markdig` master):
```csharp
using Markdig;
using Markdig.Extensions.EmphasisExtras;

private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .DisableHtml()
    .UseAutoLinks()
    .UsePipeTables()
    .UseTaskLists()
    .UseDefinitionLists()
    .UseFootnotes()
    .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
    .Build();
```
**Critical:** never `.UseAdvancedExtensions()` (D-04) — it bundles 19 extensions including `UseGenericAttributes()`, an attribute-injection XSS vector. Compose exactly the 6 calls above, individually.

Dual-sanitizer construction (RESEARCH.md lines 193-236, verified against `mganss/HtmlSanitizer` master) — two `HtmlSanitizer` instances built once from a shared `baseTags` set, one with `"img"` added (web, D-06) and one without (email, D-07). See RESEARCH.md's full listing for the exact `AllowedTags`/`AllowedAttributes`/`AllowedSchemes`/`UriAttributes` sets — every tag in that list is load-bearing per Pitfall 1 (`KeepChildNodes = false` deletes entire subtrees, not just the wrapping tag, for any tag missing from the allowlist).

---

### `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (config, DI registration)

**Analog:** self — `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (full file, 28 lines)

**Current pattern** (`QuestBoard.Domain/Extensions/ServiceExtensions.cs:9-27`):
```csharp
public static class ServiceExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPlayerSignupService, PlayerSignupService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDungeonMasterProfileService, DungeonMasterProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IImageValidationService, ImageValidationService>();

        return services;
    }
}
```

**What to mirror:**
- Same alphabetically-unordered append-to-the-list convention — add the new registration as one more line inside `AddDomainServices`, following the existing `services.Add***<IInterface, Implementation>();` shape.
- **Deliberate deviation, confirmed by CONTEXT.md/RESEARCH.md:** every existing line uses `AddScoped`. The new line must be `services.AddSingleton<IMarkdownService, MarkdownService>();` — the one intentional exception in this file. Add a short inline comment on this line explaining *why* it deviates (stateless, holds only immutable pre-built Markdig/sanitizer state, safe for concurrent singleton use), so a future reader doesn't "fix" it back to `AddScoped` by pattern-matching the rest of the file.
- No new `using` needed beyond what's already imported (`QuestBoard.Domain.Interfaces`, `QuestBoard.Domain.Services` are already imported at the top of the file for the other registrations).

**Cross-codebase confirmation this `AddSingleton` deviation is a known, safe pattern elsewhere in the app** (not unprecedented, just not yet used *in this specific file*): `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:71-73` and `QuestBoard.Service/Program.cs:158` both use `AddSingleton` for stateless/shared-state services (`IBackgroundJobClient`, `IActiveGroupContext`, a rate limiter) — confirming `AddSingleton` is an established idiom in this codebase generally, just applied here for the first time inside `ServiceExtensions.AddDomainServices` specifically.

---

### `QuestBoard.Domain/QuestBoard.Domain.csproj` (config, package manifest)

**Analog:** self — `QuestBoard.Domain/QuestBoard.Domain.csproj` (full file, 17 lines)

**Current pattern** (`QuestBoard.Domain/QuestBoard.Domain.csproj:9-15`):
```xml
<ItemGroup>
    <PackageReference Include="AutoMapper" Version="16.2.0" />
</ItemGroup>
```

**What to mirror:** Add two more `<PackageReference>` lines inside this same `<ItemGroup>` (do not create a new `ItemGroup`):
```xml
<PackageReference Include="Markdig" Version="1.3.2" />
<PackageReference Include="HtmlSanitizer" Version="9.0.892" />
```
Matches RESEARCH.md's re-verified-today version pins (Standard Stack table) and the existing single-`ItemGroup`-for-`PackageReference` convention already used for `AutoMapper`.

---

### `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` (test)

**Analog:** `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` (full file, 98 lines)

**Imports pattern** (`QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs:1-4`):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;
```

**Service instantiation pattern** (`QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs:11`):
```csharp
private static readonly IImageValidationService Service = new ImageValidationService();
```
Mirror exactly: `private static readonly IMarkdownService Service = new MarkdownService();` — direct `new`, no mocking framework, no DI container — confirms the `internal` class + `InternalsVisibleTo` pattern (see AssemblyInfo below) is sufficient for direct construction from the test project.

**Core test structure — `[Theory]`/`[InlineData]` for input-variant coverage** (`QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs:13-25`):
```csharp
[Theory]
[InlineData("image/jpeg", "photo.jpg")]
[InlineData("image/jpeg", "photo.jpeg")]
[InlineData("image/png", "photo.png")]
[InlineData("image/gif", "photo.gif")]
public void ValidateImagePair_ValidOriginalWithinSizeLimit_ReturnsNoErrors(string contentType, string fileName)
{
    var original = new ImageFileInput(OneMegabyte, contentType, fileName, "ProfilePictureFile");

    var errors = Service.ValidateImagePair(original, null);

    errors.Should().BeEmpty();
}
```
Apply this exact `[Theory]`/`[InlineData]` shape for the XSS-payload test in RESEARCH.md's Code Examples section (5 `[InlineData]` payloads: raw `<script>`, raw `<img onerror>`, `javascript:` in `[text](url)`, `javascript:` in native autolink, generic-attribute-injection `{onmouseover=...}`) and for edge-case-per-extension tests (task list checkbox shape, footnote id/href shape, definition list, pipe table).

**Single-scenario `[Fact]` pattern** (`QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs:59-65`):
```csharp
[Fact]
public void ValidateImagePair_NullOriginalAndNullCropped_ReturnsNoErrors()
{
    var errors = Service.ValidateImagePair(null, null);

    errors.Should().BeEmpty();
}
```
Mirror this exact `[Fact]` + arrange/act/assert-on-one-line shape for the two RENDER-03 paragraph tests in RESEARCH.md (`RenderToHtml_SingleNewlineNoBlankLine_StaysOneParagraph`, `RenderToHtml_BlankLineBetweenLines_ProducesTwoParagraphs`) — both already given in RESEARCH.md's Code Examples in this exact style, confirming RESEARCH.md's own test payloads already match this codebase's established test-naming convention (`MethodName_Scenario_ExpectedResult`).

**FluentAssertions usage** — every assertion in the analog uses `.Should()` fluent syntax (`errors.Should().BeEmpty()`, `errors.Should().ContainSingle(...)`) — confirms this project's `QuestBoard.UnitTests.csproj` already references FluentAssertions (RESEARCH.md's Environment Availability table confirms `FluentAssertions 8.10.0` is already present); use `html.Should().NotContain(...)` / `.Should().Contain(...)` for the Markdown/sanitizer assertions, not raw `Assert.*` xUnit calls.

---

## Shared Patterns

### Domain-layer stateless service shape (interface + internal impl + zero-arg constructor)
**Source:** `QuestBoard.Domain/Interfaces/IImageValidationService.cs` + `QuestBoard.Domain/Services/ImageValidationService.cs`
**Apply to:** `IMarkdownService.cs`, `MarkdownService.cs`
This is the single most important shared pattern for this phase — it is the direct, deliberate precedent CONTEXT.md and RESEARCH.md both name explicitly. `internal class`, no base class (`ImageValidationService` does NOT inherit `BaseService` — confirmed by reading the file; this is not a repository-backed CRUD service, so `BaseService`'s pattern does not apply here either), no constructor dependencies, pure `input → output` method shape, unit-testable via direct `new`.

### `InternalsVisibleTo` test access
**Source:** `QuestBoard.Domain/Properties/AssemblyInfo.cs` (full file, 3 lines)
```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("QuestBoard.UnitTests")]
```
**Apply to:** No change needed — this attribute already grants `QuestBoard.UnitTests` access to `internal class MarkdownService`, exactly as it already does for `internal class ImageValidationService`. Confirmed present; do not add a duplicate `InternalsVisibleTo` entry.

### `AddSingleton` deviation from the file's dominant `AddScoped` convention
**Source:** `QuestBoard.Domain/Extensions/ServiceExtensions.cs:15-24` (existing `AddScoped` lines) contrasted with `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:71-73` and `QuestBoard.Service/Program.cs:158` (existing `AddSingleton` precedent elsewhere in the app)
**Apply to:** The one new DI registration line in `ServiceExtensions.AddDomainServices`
This is a locked decision (CONTEXT.md, code_context section) — the new service is the only `AddSingleton` line in an otherwise all-`AddScoped` file, justified by statelessness. Not a pattern to generalize to other services without the same statelessness justification.

### Comment style — no phase/requirement IDs in source
**Source:** Project `CLAUDE.md` ("Code Comments" section) + observed in `ImageValidationService.cs`'s inline comments (e.g. `// A null or zero-length file is treated as absent -- this phase has no crop UI, so an absent cropped file must never be an error.` — explains the *why* in plain language, no ID reference)
**Apply to:** All new comments in `MarkdownService.cs`, `IMarkdownService.cs`, `MarkdownServiceTests.cs` — never write `// D-04: ...` or `// RENDER-01: ...` in source; explain reasoning in plain language instead (e.g. "never call `.UseAdvancedExtensions()` — it bundles an attribute-injection XSS vector via `UseGenericAttributes()`", not "D-04: don't use UseAdvancedExtensions").

## No Analog Found

None at the file-classification level — all 5 files have a strong codebase analog (see table above). The one caveat: the **internal composition details** of `MarkdownService` (exact Markdig pipeline calls, exact `HtmlSanitizerOptions` allowlists) have no codebase precedent since no prior code in this repository uses either library — for that sub-level of detail, RESEARCH.md's "Code Examples" section (source-verified against both libraries' actual GitHub repositories, not training-data recall) is the authoritative reference, cited inline above.

## Metadata

**Analog search scope:** `QuestBoard.Domain/Interfaces/`, `QuestBoard.Domain/Services/`, `QuestBoard.Domain/Extensions/`, `QuestBoard.Domain/Properties/`, `QuestBoard.Domain/QuestBoard.Domain.csproj`, `QuestBoard.UnitTests/Services/`, `QuestBoard.Service/Extensions/`, `QuestBoard.Service/Components/Emails/`, plus a codebase-wide `Grep` for `AddSingleton` and for `Markdig|HtmlSanitizer|Ganss` (confirmed zero pre-existing usage of either library, and confirmed `AddSingleton` precedent exists elsewhere in the app for the DI-deviation justification)
**Files scanned:** 12 read directly (`IImageValidationService.cs`, `ImageValidationService.cs`, `ServiceExtensions.cs`, `AssemblyInfo.cs`, `QuestBoard.Domain.csproj`, `ImageValidationServiceTests.cs`, `ControllerExtensions.cs`, `IEmailRenderService.cs`, plus Glob listings of `Interfaces/`, `Services/`, `UnitTests/Services/`, `Emails/` directories) + 2 upstream docs (65-CONTEXT.md, 65-RESEARCH.md)
**Pattern extraction date:** 2026-07-09
