using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class AppVersionTests
{
    [Fact]
    public void ExtractVersion_NullInformationalVersion_ReturnsDev()
    {
        AppVersion.ExtractVersion(null).Should().Be("dev");
    }

    [Fact]
    public void ExtractVersion_PlainSemver_ReturnsUnchanged()
    {
        AppVersion.ExtractVersion("1.4.2").Should().Be("1.4.2");
    }

    [Fact]
    public void ExtractVersion_WithSourceRevisionSuffix_StripsSuffix()
    {
        AppVersion.ExtractVersion("1.4.2+a1b2c3d4e5f6").Should().Be("1.4.2");
    }
}
