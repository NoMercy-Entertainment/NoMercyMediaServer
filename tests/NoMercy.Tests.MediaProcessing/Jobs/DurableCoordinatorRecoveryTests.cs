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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// B2 regression guard: verifies that <see cref="VideoEncodeJob"/> carries durable
/// <see cref="CoordinatorState"/> through JSON round-trips so a server restart
/// between child completions does not orphan the encode run.
///
/// These tests do NOT spin up a real database or QueueRunner. They verify:
///   1. <c>CoordinatorState</c> survives a full JSON round-trip without data loss.
///   2. A job deserialized with a non-null <c>Coordinator</c> property reports the
///      correct phase from the deserialized state.
///   3. A null <c>Coordinator</c> on the payload signals the initial-run path
///      (not a coordinator wake-up).
/// </summary>
[Trait("Category", "Unit")]
public class DurableCoordinatorRecoveryTests
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };

    [Fact]
    public void CoordinatorState_RoundTrip_AllFieldsPreserved()
    {
        string groupTag = Ulid.NewUlid().ToString();
        string[] taskIds =
        [
            "run-pass1-0",
            "run-pass1-1",
            "run-pass2-0",
            "run-pass2-1",
            "run-audio-0",
        ];
        Ulid presetId = Ulid.NewUlid();
        DateTime now = DateTime.UtcNow;

        CoordinatorState original = new(
            groupTag,
            taskIds,
            CoordinatorPhase.WaitPass1,
            now,
            null,
            null,
            presetId,
            3
        );

        string json = JsonConvert.SerializeObject(original, SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            json,
            SerializerSettings
        );

        restored.Should().NotBeNull();
        restored!.GroupTag.Should().Be(groupTag);
        restored.TaskIds.Should().BeEquivalentTo(taskIds);
        restored.Phase.Should().Be(CoordinatorPhase.WaitPass1);
        restored.Pass1DispatchedAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
        restored.Pass2DispatchedAt.Should().BeNull();
        restored.Pass1StatsPath.Should().BeNull();
        restored.PresetId.Should().Be(presetId);
        restored.ExpectedFinalCount.Should().Be(3);
    }

    [Fact]
    public void CoordinatorState_WaitChildrenPhase_RoundTrip_PhasePreserved()
    {
        CoordinatorState state = new(
            "grp-abc",
            ["task-pass2-0", "task-audio-0"],
            CoordinatorPhase.WaitChildren,
            DateTime.UtcNow.AddSeconds(-30),
            DateTime.UtcNow.AddSeconds(-5),
            "/tmp/stats/x264",
            Ulid.NewUlid(),
            2
        );

        string json = JsonConvert.SerializeObject(state, SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            json,
            SerializerSettings
        );

        restored!.Phase.Should().Be(CoordinatorPhase.WaitChildren);
        restored.Pass1StatsPath.Should().Be("/tmp/stats/x264");
    }

    [Fact]
    public void CoordinatorState_FinalizePhase_RoundTrip_PhasePreserved()
    {
        CoordinatorState state = new(
            "grp-xyz",
            ["task-video-0"],
            CoordinatorPhase.Finalize,
            null,
            null,
            null,
            Ulid.NewUlid(),
            1
        );

        string json = JsonConvert.SerializeObject(state, SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            json,
            SerializerSettings
        );

        restored!.Phase.Should().Be(CoordinatorPhase.Finalize);
    }

    [Fact]
    public void VideoEncodeJob_WithNullCoordinator_IsInitialRun()
    {
        VideoEncodeJob job = new()
        {
            LibraryId = Ulid.NewUlid(),
            FolderId = Ulid.NewUlid(),
            Id = "01HNXYZ",
            InputFile = "/media/video.mkv",
            Coordinator = null,
        };

        job.Coordinator.Should().BeNull("a null Coordinator property signals the initial-run path");
    }

    [Fact]
    public void VideoEncodeJob_WithCoordinatorState_IsWakeUp()
    {
        CoordinatorState state = new(
            "grp-123",
            ["task-0"],
            CoordinatorPhase.WaitChildren,
            null,
            null,
            null,
            Ulid.NewUlid(),
            1
        );

        VideoEncodeJob job = new()
        {
            LibraryId = Ulid.NewUlid(),
            FolderId = Ulid.NewUlid(),
            Id = "01HNXYZ",
            InputFile = "/media/video.mkv",
            Coordinator = state,
        };

        job.Coordinator.Should()
            .NotBeNull("a non-null Coordinator property signals the coordinator wake-up path");
        job.Coordinator!.Phase.Should().Be(CoordinatorPhase.WaitChildren);
        job.Coordinator.GroupTag.Should().Be("grp-123");
    }

    [Fact]
    public void VideoEncodeJob_CoordinatorState_SerializesInsideJobPayload()
    {
        CoordinatorState state = new(
            "grp-roundtrip",
            ["t1", "t2"],
            CoordinatorPhase.WaitPass1,
            DateTime.UtcNow,
            null,
            null,
            Ulid.NewUlid(),
            2
        );

        VideoEncodeJob original = new()
        {
            LibraryId = Ulid.NewUlid(),
            FolderId = Ulid.NewUlid(),
            Id = "01HNXYZ",
            InputFile = "/media/movie.mkv",
            Coordinator = state,
        };

        string json = JsonConvert.SerializeObject(original, SerializerSettings);

        VideoEncodeJob? restored = JsonConvert.DeserializeObject<VideoEncodeJob>(
            json,
            SerializerSettings
        );

        restored.Should().NotBeNull();
        restored!
            .Coordinator.Should()
            .NotBeNull("coordinator state must survive a full job payload round-trip");
        restored.Coordinator!.Phase.Should().Be(CoordinatorPhase.WaitPass1);
        restored.Coordinator.GroupTag.Should().Be("grp-roundtrip");
        restored.Coordinator.TaskIds.Should().BeEquivalentTo(["t1", "t2"]);
    }
}
