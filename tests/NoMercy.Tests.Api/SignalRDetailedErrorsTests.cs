using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Integration")]
public class SignalRDetailedErrorsTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public SignalRDetailedErrorsTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ProductionSignalR_DoesNotEnableDetailedErrors()
    {
        // The test factory runs without --dev flag, so Config.IsDev is false (production mode).
        // Verify that EnableDetailedErrors is disabled in production.
        IOptions<HubOptions> hubOptions = _factory.Services.GetRequiredService<IOptions<HubOptions>>();

        Assert.False(
            hubOptions.Value.EnableDetailedErrors,
            "SignalR EnableDetailedErrors must be false in production to prevent stack trace leakage to clients");
    }

    [Fact]
    public void SignalR_MaximumReceiveMessageSize_IsReasonablyLimited()
    {
        IOptions<HubOptions> hubOptions = _factory.Services.GetRequiredService<IOptions<HubOptions>>();
        long? maxSize = hubOptions.Value.MaximumReceiveMessageSize;

        Assert.NotNull(maxSize);

        long tenMb = 10L * 1024 * 1024;
        Assert.True(
            maxSize <= tenMb,
            $"SignalR MaximumReceiveMessageSize should be at most 10MB but was {maxSize / (1024 * 1024.0):F1}MB");

        Assert.True(
            maxSize >= 1024 * 1024,
            $"SignalR MaximumReceiveMessageSize should be at least 1MB but was {maxSize / 1024.0:F0}KB");
    }
}
