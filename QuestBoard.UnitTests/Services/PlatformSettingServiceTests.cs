using NSubstitute;
using QuestBoard.Domain.Constants;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

public class PlatformSettingServiceTests
{
    private readonly IPlatformSettingRepository _repository;
    private readonly PlatformSettingService _sut;

    public PlatformSettingServiceTests()
    {
        _repository = Substitute.For<IPlatformSettingRepository>();

        _sut = new PlatformSettingService(_repository);
    }

    // ---------------------------------------------------------------------------
    // SaveAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_WhenNewSecretIsNull_UpsertsUrlAndEnabledButNotSecret()
    {
        // Act
        await _sut.SaveAsync(1, "https://omphalos.example", null, true, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosUrl, "https://omphalos.example", 1, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosEnabled, "true", 1, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WhenNewSecretIsWhitespace_UpsertsUrlAndEnabledButNotSecret()
    {
        // Act
        await _sut.SaveAsync(1, "https://omphalos.example", "   ", false, TestContext.Current.CancellationToken);

        // Assert
        await _repository.DidNotReceive().UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WhenNewSecretIsProvided_UpsertsAllThreeKeysIncludingSecret()
    {
        // Act
        await _sut.SaveAsync(1, "https://omphalos.example", "s3cr3t", true, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosUrl, "https://omphalos.example", 1, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosEnabled, "true", 1, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, "s3cr3t", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WhenIsEnabledTrue_PersistsLowercaseTrueToEnabledKey()
    {
        // Act
        await _sut.SaveAsync(1, "https://omphalos.example", null, true, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosEnabled, "true", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WhenIsEnabledFalse_PersistsLowercaseFalseToEnabledKey()
    {
        // Act
        await _sut.SaveAsync(1, "https://omphalos.example", null, false, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosEnabled, "false", 1, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // GenerateAndSaveSecretAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAndSaveSecretAsync_ReturnsNonEmptyValueAndPersistsMatchingSecret()
    {
        // Arrange
        string? persistedValue = null;
        _repository.When(x => x.UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, Arg.Any<string>(), 1, Arg.Any<CancellationToken>()))
            .Do(callInfo => persistedValue = callInfo.ArgAt<string>(1));

        // Act
        var result = await _sut.GenerateAndSaveSecretAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        await _repository.Received(1).UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, Arg.Any<string>(), 1, Arg.Any<CancellationToken>());
        persistedValue.Should().Be(result);
    }

    // ---------------------------------------------------------------------------
    // GetResolvedAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetResolvedAsync_ComposesCascadeValuesFromRepositoryIntoOmphalosSettings()
    {
        // Arrange
        _repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosUrl, 1, Arg.Any<CancellationToken>())
            .Returns("https://omphalos.example");
        _repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosSharedSecret, 1, Arg.Any<CancellationToken>())
            .Returns("s3cr3t");
        _repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosEnabled, 1, Arg.Any<CancellationToken>())
            .Returns("true");

        // Act
        var result = await _sut.GetResolvedAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.Url.Should().Be("https://omphalos.example");
        result.SharedSecret.Should().Be("s3cr3t");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetResolvedAsync_WhenNoValuesExist_ReturnsEmptyUrlNullSecretAndDisabled()
    {
        // Arrange
        _repository.GetCascadeValueAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _sut.GetResolvedAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.Url.Should().Be(string.Empty);
        result.SharedSecret.Should().BeNull();
        result.IsEnabled.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // ClearScopeAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClearScopeAsync_CallsRepositoryClearScopeWithGroupIdAndThreeKeys()
    {
        // Act
        await _sut.ClearScopeAsync(1, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).ClearScopeAsync(
            1,
            Arg.Is<IEnumerable<string>>(keys =>
                keys.Contains(PlatformSettingKeys.OmphalosUrl) &&
                keys.Contains(PlatformSettingKeys.OmphalosSharedSecret) &&
                keys.Contains(PlatformSettingKeys.OmphalosEnabled)),
            Arg.Any<CancellationToken>());
    }
}
