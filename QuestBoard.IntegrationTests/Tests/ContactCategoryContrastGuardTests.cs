using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

// Guards styling contracts the rest of the suite provably cannot see. 668 integration tests
// passed while both defects these facts pin were live on the running app -- a server-side
// integration test cannot observe a computed style, but it can prove a scoped CSS rule exists
// with the right colour, that the rule's selector actually reaches the element it targets
// (asserted structurally against rendered HTML, not assumed), and that the mobile view file is
// genuinely the one the server selects under a real User-Agent. The final pixel that paints is
// confirmable only by eye and is tracked separately as a UAT human-verify item.
public class ContactCategoryContrastGuardTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // Attaches the mobile user agent header to a request and sends it through the supplied
    // authenticated client, so the client's default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // Resolves a stylesheet's path by walking up from AppContext.BaseDirectory until a
    // "QuestBoard.Service/wwwroot/css/{fileName}" descendant is found. Fails with a descriptive
    // message naming the attempted path when it cannot be resolved, so the facts below work
    // from any working directory.
    private static string ResolveCssPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot", "css", fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(attemptedBase, "QuestBoard.Service", "wwwroot", "css", fileName);
        throw new FileNotFoundException(
            $"{fileName} not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            $"Ensure QuestBoard.Service/wwwroot/css/{fileName} exists in the repo.",
            attemptedPath);
    }

    // Extracts the body of a single CSS rule given the exact "selector {" text that opens it, so
    // an assertion can be scoped to that one rule instead of matching a declaration that happens
    // to repeat elsewhere in the file. Returns an empty string when the selector is not found, so
    // a deleted rule fails the containing assertion instead of throwing.
    private static string ExtractCssRule(string css, string selectorWithOpenBrace)
    {
        var start = css.IndexOf(selectorWithOpenBrace, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = css.IndexOf('}', start);
        return end < 0 ? string.Empty : css[start..(end + 1)];
    }

    // The lightest pixel of the board backdrop; translucent card surfaces are measured over it because
    // that is where light text on glass is weakest.
    private const string LightestBoard = "#794317";

    // The declared value of a token in tokens.css (the first declaration, which is the board's own
    // palette rather than the Shop theme override further down). Every token used here is a plain
    // colour literal, which is what WcagContrast can parse.
    private static string TokenValue(string tokensCss, string name)
    {
        var match = Regex.Match(tokensCss, @"(?<![\w-])" + Regex.Escape(name) + @"\s*:\s*(?<value>[^;]+);");
        match.Success.Should().BeTrue(because: $"tokens.css must declare {name}");
        return match.Groups["value"].Value.Trim();
    }

    // A text token's contrast on the card surface: the glass tint over the scrim over the lightest board pixel.
    private static double RatioOnCardSurface(string tokensCss, string textToken) =>
        WcagContrast.TextOn(
            TokenValue(tokensCss, textToken),
            [TokenValue(tokensCss, "--qb-surface-scrim"), TokenValue(tokensCss, "--qb-surface-glass")],
            LightestBoard);

    [Fact]
    public async Task ContactCategoryContrastGuard_ManagementPageLabel_RendersInsideScopedFormOnMobileOnly()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contrast_guard_mgmt_dm", "contrast_guard_mgmt_dm@example.com", roles: ["DungeonMaster"]);

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, "/ContactCategoryManagement");
        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a DungeonMaster must be able to load the Manage Categories page under a real mobile User-Agent");

        var formStart = mobileHtml.IndexOf("category-mgmt-add-form", StringComparison.Ordinal);
        formStart.Should().BeGreaterThan(-1,
            because: "the add-category <form> in Manage.Mobile.cshtml must carry the category-mgmt-add-form scoping class, " +
                     "or the new label rule's selector matches nothing -- the precise failure mode of the original defect");

        var formEnd = mobileHtml.IndexOf("</form>", formStart, StringComparison.Ordinal);
        formEnd.Should().BeGreaterThan(-1,
            because: "the scoped add-category form must close with </form> after the class token appears");

        var formSlice = mobileHtml[formStart..(formEnd + "</form>".Length)];

        formSlice.Should().Contain("form-label",
            because: "the label styled by the new rule must actually sit inside the scoped form, not merely exist elsewhere on the page");
        formSlice.Should().Contain("New Category Name",
            because: "the specific label text UAT found unreadable must be the one enclosed by the scoped form");

        var desktopResponse = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("category-mgmt-add-form",
            because: "a request with no mobile User-Agent must select Manage.cshtml, proving the mobile file is the one the fix targets");
    }

    [Fact]
    public void ContactCategoryContrastGuard_MobileAddFormLabelRule_SetsParchmentColourAndShadow()
    {
        // The phone add-category label sits on the title card, so it takes the card's text colour by inheritance:
        // no rule names a colour or a shadow for it, the retired per-form rule is gone from the contacts phone sheet,
        // and the card's text colour holds the 4.5:1 bar on the card surface.
        var categoriesCss = File.ReadAllText(ResolveCssPath("contact-categories.mobile.css"));
        var contactsCss = File.ReadAllText(ResolveCssPath("contacts.mobile.css"));

        contactsCss.Should().NotContain("category-mgmt-add-form",
            because: "the per-form label colour rule moved out of the contacts phone sheet and must not come back");
        Regex.Matches(categoriesCss, @"[^{}]*\.form-label[^{}]*\{[^}]*\}")
            .Select(m => m.Value).Should().BeEmpty(
                because: "the add-category label must name no rule of its own, so it keeps the card's colour by inheritance");
        Regex.Matches(categoriesCss, @"(?<![\w-])color\s*:[^;]*;")
            .Select(m => m.Value).Should().OnlyContain(v => v.Contains("var(--qb-"),
                because: "any colour the phone category sheet names is a token");

        var components = File.ReadAllText(ResolveCssPath("components.css"));
        var cardRule = ExtractCssRule(components, ".modern-card {");
        cardRule.Should().Contain("color: var(--qb-fg)",
            because: "the card sets the text colour once and the label inherits it");
        var tokens = File.ReadAllText(ResolveCssPath("tokens.css"));
        RatioOnCardSurface(tokens, "--qb-fg").Should().BeGreaterThanOrEqualTo(4.5,
            because: "the label is body-size text on the card surface, so it needs the 4.5:1 text bar over the lightest board pixel");
    }

    [Fact]
    public void ContactCategoryContrastGuard_ModernCardFormTextLinkRule_SetsParchmentColourWithUnderlineIntact()
    {
        var cssPath = ResolveCssPath("components.css");
        var css = File.ReadAllText(cssPath);
        var rule = ExtractCssRule(css, ".form-text a {");

        rule.Should().NotBeEmpty(
            because: $"components.css at '{cssPath}' must contain a scoped .form-text a rule");

        var lowerRule = rule.ToLowerInvariant();
        lowerRule.Should().Contain("color: var(--qb-link)",
            because: "the desktop 'Manage Categories' helper link must resolve to the link token, not Bootstrap's default link blue rgb(13,110,253)");
        lowerRule.Should().NotContain("#0d6efd",
            because: "the rule body must not merely restate Bootstrap's default link colour under a more specific selector");
        lowerRule.Should().NotContain("text-decoration: none",
            because: "the underline is the link's non-colour affordance now that it shares a hue with its surrounding text; suppressing it would violate WCAG 1.4.1");

        var tokens = File.ReadAllText(ResolveCssPath("tokens.css"));
        RatioOnCardSurface(tokens, "--qb-link").Should().BeGreaterThanOrEqualTo(4.5,
            because: "the helper link is body-size text on the card surface, so it needs the 4.5:1 text bar over the lightest board pixel");
    }

    [Fact]
    public void ContactCategoryContrastGuard_MobileFormTextLinkRule_SetsParchmentColourWithUnderlineIntact()
    {
        // The phone form no longer has a sheet of its own: the one .form-text a rule in components.css serves
        // both layouts, so this fact reads it and shows the retired phone sheet is gone.
        var cssPath = ResolveCssPath("components.css");
        var css = File.ReadAllText(cssPath);
        var rule = ExtractCssRule(css, ".form-text a {");

        rule.Should().NotBeEmpty(
            because: $"components.css at '{cssPath}' must contain the .form-text a rule that serves the desktop and the phone layout -- " +
                     "without it the phone 'Manage Categories' helper link would render Bootstrap link blue");

        var lowerRule = rule.ToLowerInvariant();
        lowerRule.Should().Contain("color: var(--qb-link)",
            because: "the helper link on both layouts must resolve to the link token, not Bootstrap's default link blue rgb(13,110,253)");
        lowerRule.Should().NotContain("#0d6efd",
            because: "the rule body must not merely restate Bootstrap's default link colour under a more specific selector");
        lowerRule.Should().NotContain("text-decoration: none",
            because: "the underline is the link's non-colour affordance now that it shares a hue with its surrounding text; suppressing it would violate WCAG 1.4.1");

        var tokens = File.ReadAllText(ResolveCssPath("tokens.css"));
        RatioOnCardSurface(tokens, "--qb-link").Should().BeGreaterThanOrEqualTo(4.5,
            because: "the helper link is body-size text on the card surface, so it needs the 4.5:1 text bar over the lightest board pixel");

        File.Exists(Path.Combine(Path.GetDirectoryName(cssPath)!, "contact-form.mobile.css")).Should().BeFalse(
            because: "the phone form sheet was merged into the one shared form sheet and must not come back");
    }

    [Fact]
    public void ContactCategoryContrastGuard_PreExistingScopedOverrides_StillPinValidationRedAndHeaderSubtitle()
    {
        var cssPath = ResolveCssPath("components.css");
        var css = File.ReadAllText(cssPath);
        var tokens = File.ReadAllText(ResolveCssPath("tokens.css"));

        // Validation text inside a card means the light danger variant, which the card surface maps for Bootstrap's
        // danger utilities, so a .text-danger message reads as an error and is not repainted by anything else.
        var cardSurfaceRule = ExtractCssRule(css, ".modern-card {");
        cardSurfaceRule.Should().NotBeEmpty(
            because: $"components.css at '{cssPath}' must carry the .modern-card surface rule");
        cardSurfaceRule.Should().Contain("--bs-danger-rgb: var(--qb-danger-text-rgb)",
            because: "validation messages inside a .modern-card must still render the danger text colour, not the card's body colour");
        RatioOnCardSurface(tokens, "--qb-danger-text").Should().BeGreaterThanOrEqualTo(4.5,
            because: "validation text is body-size text on the card surface, so it needs the 4.5:1 text bar over the lightest board pixel");

        var headerSubtitleRule = ExtractCssRule(css, ".header-subtitle {");
        headerSubtitleRule.Should().NotBeEmpty(
            because: $"components.css at '{cssPath}' must still carry the .header-subtitle rule");
        var token = Regex.Match(headerSubtitleRule, @"color:\s*var\((?<name>--qb-[\w-]+)\)");
        token.Success.Should().BeTrue(
            because: "the card header's secondary line must take its colour from a --qb-* token, not a literal");
        RatioOnCardSurface(tokens, token.Groups["name"].Value).Should().BeGreaterThanOrEqualTo(4.5,
            because: "the card header's secondary line must stay readable on the header surface");
    }

    [Fact]
    public async Task ContactCategoryContrastGuard_ZeroCategoryHelperLink_RendersOnBothDesktopAndMobileCardSurfaces()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contrast_guard_zero_cat_dm", "contrast_guard_zero_cat_dm@example.com", roles: ["DungeonMaster"]);

        var desktopResponse = await dmClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a DungeonMaster on a zero-category board must be able to load Contacts Create on desktop");

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, "/Contacts/Create");
        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a DungeonMaster on a zero-category board must be able to load Contacts Create under a real mobile User-Agent");

        AssertHelperLinkPresentInsideFormText(desktopHtml, "desktop");
        AssertHelperLinkPresentInsideFormText(mobileHtml, "mobile");

        // One view serves both layouts, so both render the hint inside the same card surface the shared
        // .form-text a rule targets.
        desktopHtml.Should().Contain("modern-card",
            because: "the desktop Contacts Create view must wrap its form in .modern-card, the surface the shared card rules target");
        mobileHtml.Should().Contain("modern-card",
            because: "the phone Contacts Create view is the same view and must wrap its form in .modern-card too");
        mobileHtml.Should().NotContain("contact-form-card",
            because: "the phone-only card wrapper was retired with the phone view");
        mobileHtml.Should().Contain("mobile-layout");
        desktopHtml.Should().NotContain("mobile-layout");
    }

    // Locates the "Manage Categories" link and confirms it sits inside a form-text <small>
    // element -- the DOM relationship both new CSS rules depend on -- rather than assuming the
    // page's first "form-text" occurrence (the unrelated image-upload caption) is the right one.
    private static void AssertHelperLinkPresentInsideFormText(string html, string platform)
    {
        var linkIndex = html.IndexOf("ContactCategoryManagement", StringComparison.Ordinal);
        linkIndex.Should().BeGreaterThan(-1,
            because: $"on a board with zero categories, the {platform} Contacts Create page must render the helper link to Manage Categories");

        var formTextIndex = html.LastIndexOf("form-text", linkIndex, StringComparison.Ordinal);
        formTextIndex.Should().BeGreaterThan(-1,
            because: $"the {platform} helper link must sit inside a form-text element, the scope both new CSS rules target");

        var closingIndex = html.IndexOf("</small>", linkIndex, StringComparison.Ordinal);
        closingIndex.Should().BeGreaterThan(-1,
            because: $"the {platform} helper text's <small> element must close after the link appears");

        var slice = html[formTextIndex..(closingIndex + "</small>".Length)];
        slice.Should().Contain("ContactCategoryManagement",
            because: $"the {platform} form-text slice must actually contain the Manage Categories link, proving the DOM relationship both new rules depend on");
    }
}
