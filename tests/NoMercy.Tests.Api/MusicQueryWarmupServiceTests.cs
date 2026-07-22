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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Service.Workers;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Covers the boot-time music-query warmup added alongside the GetAlbumTracksAsync fix: the
/// FIRST call of any of StartPlaybackCommand's GetPlaylist query shapes in a fresh server
/// process measured low seconds even once the query itself was fast (EF model building +
/// query-plan compilation), which a real user's first tap paid for directly. This service
/// exercises each shape once against a Guid.Empty probe before any real client connects.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MusicQueryWarmupServiceTests
{
    private static (
        MusicQueryWarmupService Service,
        Mock<IMusicRepository> Repository
    ) MakeService()
    {
        Mock<IMusicRepository> repository = new();
        repository
            .Setup(expression: r =>
                r.GetPlaylistTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        repository
            .Setup(expression: r =>
                r.GetAlbumTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        repository
            .Setup(expression: r =>
                r.GetArtistTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        repository
            .Setup(expression: r =>
                r.GetGenreTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        repository
            .Setup(expression: r => r.GetTrackAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (Track?)null);

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: repository.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        MusicQueryWarmupService service = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<MusicQueryWarmupService>.Instance
        );

        return (service, repository);
    }

    [Fact]
    public async Task WarmupAsync_CallsEveryGetPlaylistShape_WithAnEmptyProbe()
    {
        (MusicQueryWarmupService service, Mock<IMusicRepository> repository) = MakeService();

        await service.WarmupAsync(cancellationToken: CancellationToken.None);

        repository.Verify(
            expression: r => r.GetPlaylistTracksAsync(Guid.Empty, Guid.Empty, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        repository.Verify(
            expression: r => r.GetAlbumTracksAsync(Guid.Empty, Guid.Empty, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        repository.Verify(
            expression: r => r.GetArtistTracksAsync(Guid.Empty, Guid.Empty, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        repository.Verify(
            expression: r => r.GetGenreTracksAsync(Guid.Empty, Guid.Empty, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        repository.Verify(
            expression: r => r.GetTrackAsync(Guid.Empty, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task WarmupAsync_SwallowsRepositoryFailure_NeverThrows()
    {
        Mock<IMusicRepository> repository = new();
        repository
            .Setup(expression: r =>
                r.GetPlaylistTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "database unavailable"));

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: repository.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        MusicQueryWarmupService service = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<MusicQueryWarmupService>.Instance
        );

        Func<Task> act = () => service.WarmupAsync(cancellationToken: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_ReturnsImmediately_WithoutWaitingForWarmupToComplete()
    {
        // Fire-and-forget by design — it must never delay "Server started" the way the
        // synchronous parts of Phase 4 do. A slow/blocked repository call must not hold up
        // StartAsync's own returned Task.
        Mock<IMusicRepository> repository = new();
        TaskCompletionSource neverCompletes = new();
        repository
            .Setup(expression: r =>
                r.GetPlaylistTracksAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: async () =>
            {
                await neverCompletes.Task;
                return [];
            });

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: repository.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        MusicQueryWarmupService service = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<MusicQueryWarmupService>.Instance
        );

        Task startTask = service.StartAsync(cancellationToken: CancellationToken.None);

        Task completedFirst = await Task.WhenAny(task1: startTask, task2: Task.Delay(millisecondsDelay: 500));
        completedFirst.Should().BeSameAs(expected: startTask);

        neverCompletes.TrySetResult();
    }
}
