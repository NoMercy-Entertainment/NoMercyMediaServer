using System.Reflection;
using Microsoft.Extensions.Hosting;
using NoMercy.Server.Services;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class ServerServicesNamespaceTests
{
    [Fact]
    public void ServerServices_UsesPascalCaseNamespace()
    {
        // Verify the namespace follows PascalCase convention (Services, not services)
        Type cloudflareType = typeof(CloudflareTunnelService);
        Type registrationType = typeof(ServerRegistrationService);
        Type musicExtType = typeof(MusicHubServiceExtensions);
        Type videoExtType = typeof(VideoHubServiceExtensions);

        Assert.Equal("NoMercy.Server.Services", cloudflareType.Namespace);
        Assert.Equal("NoMercy.Server.Services", registrationType.Namespace);
        Assert.Equal("NoMercy.Server.Services", musicExtType.Namespace);
        Assert.Equal("NoMercy.Server.Services", videoExtType.Namespace);
    }

    [Fact]
    public void CloudflareTunnelService_ImplementsIHostedService()
    {
        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(CloudflareTunnelService)));
    }

    [Fact]
    public void ServerRegistrationService_ImplementsIHostedServiceAndIDisposable()
    {
        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(ServerRegistrationService)));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ServerRegistrationService)));
    }

    [Fact]
    public void MusicHubServiceExtensions_HasAddMusicHubServicesMethod()
    {
        MethodInfo? method = typeof(MusicHubServiceExtensions)
            .GetMethod("AddMusicHubServices", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
    }

    [Fact]
    public void VideoHubServiceExtensions_HasAddVideoHubServicesMethod()
    {
        MethodInfo? method = typeof(VideoHubServiceExtensions)
            .GetMethod("AddVideoHubServices", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
    }
}
