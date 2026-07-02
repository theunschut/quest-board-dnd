using Microsoft.AspNetCore.Http;
using QuestBoard.Service.Middleware;

namespace QuestBoard.IntegrationTests.Mobile;

public class MobileDetectionMiddlewareTests
{
    private static MobileDetectionMiddleware CreateMiddleware()
        => new(ctx => Task.CompletedTask);

    private static DefaultHttpContext CreateContext(string userAgent)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_IPhoneUserAgent_SetsMobileTrue()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_AndroidUserAgent_SetsMobileTrue()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_IPadUserAgent_SetsMobileTrue()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_DesktopWindowsChromeUserAgent_SetsMobileFalse()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(false);
    }

    [Fact]
    public async Task InvokeAsync_EmptyUserAgent_SetsMobileFalse()
    {
        // Arrange
        var context = CreateContext(string.Empty);
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(false);
    }

    [Fact]
    public async Task InvokeAsync_LowercaseMobiSubstring_SetsMobileTrue()
    {
        // Arrange
        var context = CreateContext("some-browser/1.0 mobi/2.0");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["IsMobile"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        var nextCallCount = 0;
        var middleware = new MobileDetectionMiddleware(ctx =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_MobileRequest_AlsoCallsNextDelegate()
    {
        // Arrange
        var context = CreateContext(
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Mobile/15E148");
        var nextCallCount = 0;
        var middleware = new MobileDetectionMiddleware(ctx =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCallCount.Should().Be(1);
    }
}
