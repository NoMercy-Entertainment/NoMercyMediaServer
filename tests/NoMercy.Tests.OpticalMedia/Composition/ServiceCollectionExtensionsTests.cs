// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.OpticalMedia.Capabilities;
using NoMercy.OpticalMedia.Composition;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Storage;
using OpticalAudio = NoMercy.OpticalMedia.Audio;
using OpticalLive = NoMercy.OpticalMedia.Live;

namespace NoMercy.Tests.OpticalMedia.Composition;

/// <summary>
/// REQUIREMENT: <see cref="ServiceCollectionExtensions.AddNoMercyOpticalMedia"/>
/// must wire every contract used by the disc-ripping pipeline — drive
/// backend/monitor, ripper, audio tag writer, one <see cref="IDiscSource"/>
/// per disc type, the identification chain, and the live-session /
/// Blu-ray-capability singletons — so a container built from a bare
/// <see cref="ServiceCollection"/> resolves every one of them without
/// throwing, and repeated registration (<c>TryAdd*</c>) never duplicates a
/// singleton.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();

        // Dependencies AddNoMercyOpticalMedia itself does not register — at
        // real startup these come from ServiceConfiguration (Encoder /
        // Hosting modules). Faked here purely so every OpticalMedia service
        // can actually be constructed when resolved.
        services.AddSingleton(implementationInstance: Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton(implementationInstance: Mock.Of<IServerPhaseTracker>());
        services.AddSingleton(
            implementationInstance: new EncoderOptions { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" }
        );
        services.AddSingleton(implementationInstance: Mock.Of<IProcessRunner>());
        services.AddSingleton(implementationInstance: Mock.Of<IStorage>());
        services.AddSingleton(implementationInstance: Mock.Of<IStorageDriver>());
        services.AddSingleton(implementationInstance: Mock.Of<IMediaAnalyzer>());
        services.AddSingleton(implementationInstance: Mock.Of<ILiveEncoder>());

        services.AddNoMercyOpticalMedia();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddNoMercyOpticalMedia_ReturnsSameServiceCollectionInstance()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddNoMercyOpticalMedia();

        result.Should().BeSameAs(expected: services);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersDriveBackend()
    {
        await using ServiceProvider provider = BuildProvider();

        IDriveBackend backend = provider.GetRequiredService<IDriveBackend>();

        backend.Should().NotBeNull();
        // On Windows this may resolve to WindowsDriveBackend (WMI) or fall
        // back to PollingDriveBackend when WMI construction fails; on every
        // other platform it is always PollingDriveBackend.
        backend.Should().Match(predicate: b => b is WindowsDriveBackend || b is PollingDriveBackend);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_DriveBackend_IsSingleton()
    {
        await using ServiceProvider provider = BuildProvider();

        IDriveBackend first = provider.GetRequiredService<IDriveBackend>();
        IDriveBackend second = provider.GetRequiredService<IDriveBackend>();

        first.Should().BeSameAs(expected: second);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersDriveMonitorAndLockRegistry()
    {
        await using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IDriveMonitor>().Should().NotBeNull();
        provider.GetRequiredService<DriveLockRegistry>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersDiscRipperAndAudioMetadataWriter()
    {
        await using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IDiscRipper>().Should().BeOfType<DiscRipper>();
        provider
            .GetRequiredService<IAudioMetadataWriter>()
            .Should()
            .BeOfType<OpticalAudio.TagLibAudioMetadataWriter>();
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersOneDiscSourcePerDiscType()
    {
        await using ServiceProvider provider = BuildProvider();

        IEnumerable<IDiscSource> sources = provider.GetRequiredService<IEnumerable<IDiscSource>>();
        List<NoMercy.NmSystem.Dto.OpticalDiscType> types = sources.Select(selector: s => s.Type).ToList();

        types
            .Should()
            .BeEquivalentTo(expectation:
            [
                NoMercy.NmSystem.Dto.OpticalDiscType.BluRay,
                NoMercy.NmSystem.Dto.OpticalDiscType.Dvd,
                NoMercy.NmSystem.Dto.OpticalDiscType.Cd,
            ]);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersDiscSourceFactory()
    {
        await using ServiceProvider provider = BuildProvider();

        DiscSourceFactory factory = provider.GetRequiredService<DiscSourceFactory>();

        factory.CreateFor(type: NoMercy.NmSystem.Dto.OpticalDiscType.BluRay).Should().NotBeNull();
        factory.CreateFor(type: NoMercy.NmSystem.Dto.OpticalDiscType.None).Should().BeNull();
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersTocReaderChain()
    {
        await using ServiceProvider provider = BuildProvider();

        IEnumerable<ITocReader> readers = provider.GetRequiredService<IEnumerable<ITocReader>>();

        readers.Should().NotBeEmpty();
        if (OperatingSystem.IsWindows())
            readers.Should().Contain(predicate: r => r is WindowsTocReader);
        else
            readers.Should().Contain(predicate: r => r is NullTocReader);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersIdentificationChain()
    {
        await using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<MusicBrainzDiscClient>().Should().NotBeNull();
        IEnumerable<IDiscIdentifier> identifiers = provider.GetRequiredService<
            IEnumerable<IDiscIdentifier>
        >();
        identifiers.Should().Contain(predicate: i => i is VideoDiscIdentifier);
        identifiers.Should().Contain(predicate: i => i is AudioCdIdentifier);
        provider.GetRequiredService<DiscIdentificationService>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersLiveDiscSessionServices()
    {
        await using ServiceProvider provider = BuildProvider();

        provider
            .GetRequiredService<OpticalLive.ILiveDiscSession>()
            .Should()
            .BeOfType<OpticalLive.LiveDiscSession>();
        provider
            .GetRequiredService<OpticalLive.IDiscSessionRegistry>()
            .Should()
            .BeOfType<OpticalLive.DiscSessionRegistry>();
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersFfmpegBluRayCapability_AsSingleton()
    {
        await using ServiceProvider provider = BuildProvider();

        FfmpegBluRayCapability first = provider.GetRequiredService<FfmpegBluRayCapability>();
        FfmpegBluRayCapability second = provider.GetRequiredService<FfmpegBluRayCapability>();

        first.Should().BeSameAs(expected: second);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_RegistersBluRayCapabilityStartupService_AsHostedService()
    {
        await using ServiceProvider provider = BuildProvider();

        IEnumerable<IHostedService> hosted = provider.GetRequiredService<
            IEnumerable<IHostedService>
        >();

        hosted.Should().Contain(predicate: h => h is BluRayCapabilityStartupService);
    }

    [Fact]
    public async Task AddNoMercyOpticalMedia_CalledTwice_DoesNotDuplicateSingletons()
    {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddNoMercyOpticalMedia();
        services.AddNoMercyOpticalMedia();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<DriveLockRegistry> registries = provider.GetServices<DriveLockRegistry>();

        registries.Should().HaveCount(expected: 1, because: "TryAddSingleton must not duplicate on repeated calls");
    }
}
