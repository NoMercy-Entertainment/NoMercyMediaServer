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
}
