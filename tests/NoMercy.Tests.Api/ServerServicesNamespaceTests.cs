using System.Reflection;
using Microsoft.Extensions.Hosting;
using NoMercy.Api.Hubs;
using NoMercy.Api.Hubs.Shared;
using NoMercy.Api.Services.Music;
using NoMercy.Api.Services.Video;
using NoMercy.Service.Extensions;
using NoMercy.Service.Services;
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

        Assert.Equal("NoMercy.Service.Services", cloudflareType.Namespace);
        Assert.Equal("NoMercy.Service.Services", registrationType.Namespace);
        Assert.Equal("NoMercy.Service.Extensions", musicExtType.Namespace);
        Assert.Equal("NoMercy.Service.Extensions", videoExtType.Namespace);
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

    [Fact]
    public void MusicHub_UsesHubsNamespace()
    {
        Assert.Equal("NoMercy.Api.Hubs", typeof(MusicHub).Namespace);
    }

    [Fact]
    public void MusicServices_UsePascalCaseNamespace()
    {
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicDeviceManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlaybackService).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlayerStateManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlaybackCommandHandler).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlaylistManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlayerStateFactory).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicPlayerState).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicLikeEventDto).Namespace);
        Assert.Equal("NoMercy.Api.Services.Music", typeof(MusicEventType).Namespace);
    }

    [Fact]
    public void VideoHub_UsesHubsNamespace()
    {
        Assert.Equal("NoMercy.Api.Hubs", typeof(VideoHub).Namespace);
    }

    [Fact]
    public void VideoServices_UsePascalCaseNamespace()
    {
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoDeviceManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlaybackService).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlayerStateManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlaybackCommandHandler).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlaylistManager).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlayerStateFactory).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoPlayerState).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoProgressRequest).Namespace);
        Assert.Equal("NoMercy.Api.Services.Video", typeof(VideoEventType).Namespace);
    }

    [Fact]
    public void SharedActions_UsesHubsSharedNamespace()
    {
        Assert.Equal("NoMercy.Api.Hubs.Shared", typeof(Actions).Namespace);
        Assert.Equal("NoMercy.Api.Hubs.Shared", typeof(Disallows).Namespace);
    }
}
