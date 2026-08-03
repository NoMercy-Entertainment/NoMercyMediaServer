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

using FluentAssertions;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.Queue.MediaServer;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="ServiceRegistration.BuildQueueReadyStages"/> replaced the single
/// <c>BootStage.All</c> default every queue used to share — this pins the
/// per-queue combination so a future change can't silently widen a queue back
/// to depending on Auth/Network/Registered it doesn't use, or narrow the
/// encoder queues past what they genuinely need.
/// </summary>
[Trait("Category", "Unit")]
public class BuildQueueReadyStagesTests
{
    private static readonly RuntimeServerSettings Settings = new();

    private static BootStage StageFor(string queueName)
    {
        IReadOnlyDictionary<string, BootStage> stages = ServiceRegistration.BuildQueueReadyStages(
            Settings
        );
        return stages[queueName];
    }

    [Theory]
    [InlineData("library")]
    [InlineData("import")]
    [InlineData("extras")]
    [InlineData("file")]
    [InlineData("music")]
    [InlineData("image")]
    [InlineData("palette")]
    [InlineData("cron")]
    [InlineData("encoder")]
    [InlineData("encoder-gpu")]
    [InlineData("encoder-cpu")]
    public void EveryQueue_RequiresEssential(string queueName)
    {
        (StageFor(queueName) & BootStage.Essential).Should().Be(BootStage.Essential);
    }

    [Theory]
    [InlineData("library")]
    [InlineData("import")]
    [InlineData("extras")]
    [InlineData("file")]
    [InlineData("music")]
    [InlineData("image")]
    [InlineData("palette")]
    [InlineData("cron")]
    [InlineData("encoder")]
    [InlineData("encoder-gpu")]
    [InlineData("encoder-cpu")]
    public void NoQueue_RequiresRegistered(string queueName)
    {
        // Registered = SSL cert + cloud registration with nomercy-tv. Nothing
        // that scans a library, imports metadata, or spawns ffmpeg needs it.
        (StageFor(queueName) & BootStage.Registered)
            .Should()
            .Be(BootStage.None);
    }

    [Theory]
    [InlineData("palette")]
    [InlineData("cron")]
    public void SchemaOnlyQueues_RequireNothingBeyondEssential(string queueName)
    {
        StageFor(queueName).Should().Be(BootStage.Essential);
    }

    [Theory]
    [InlineData("library")]
    [InlineData("file")]
    [InlineData("music")]
    [InlineData("extras")]
    public void FfprobeConsumingQueues_RequireBinaries(string queueName)
    {
        (StageFor(queueName) & BootStage.Binaries).Should().Be(BootStage.Binaries);
    }

    [Theory]
    [InlineData("import")]
    [InlineData("image")]
    [InlineData("palette")]
    [InlineData("cron")]
    public void QueuesThatNeverTouchFfmpeg_DoNotRequireBinaries(string queueName)
    {
        (StageFor(queueName) & BootStage.Binaries).Should().Be(BootStage.None);
    }

    [Theory]
    [InlineData("library")]
    [InlineData("import")]
    [InlineData("music")]
    [InlineData("image")]
    public void RemoteMetadataQueues_RequireAuthAndNetwork(string queueName)
    {
        BootStage stage = StageFor(queueName);
        (stage & BootStage.Auth).Should().Be(BootStage.Auth);
        (stage & BootStage.Network).Should().Be(BootStage.Network);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("palette")]
    [InlineData("cron")]
    public void LocalOnlyQueues_DoNotRequireAuth(string queueName)
    {
        (StageFor(queueName) & BootStage.Auth).Should().Be(BootStage.None);
    }

    [Theory]
    [InlineData("encoder")]
    [InlineData("encoder-gpu")]
    [InlineData("encoder-cpu")]
    public void EncoderQueues_RequireBinariesAndHardware_ButNotAuthNetworkOrRegistered(
        string queueName
    )
    {
        BootStage stage = StageFor(queueName);
        (stage & BootStage.Binaries).Should().Be(BootStage.Binaries);
        (stage & BootStage.Hardware).Should().Be(BootStage.Hardware);
        (stage & BootStage.Auth).Should().Be(BootStage.None);
        (stage & BootStage.Network).Should().Be(BootStage.None);
        (stage & BootStage.Registered).Should().Be(BootStage.None);
    }
}
