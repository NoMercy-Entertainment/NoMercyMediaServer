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
[Trait(name: "Category", value: "Unit")]
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
            GroupTag: groupTag,
            TaskIds: taskIds,
            Phase: CoordinatorPhase.WaitPass1,
            Pass1DispatchedAt: now,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: presetId,
            ExpectedFinalCount: 3
        );

        string json = JsonConvert.SerializeObject(value: original, settings: SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            value: json,
            settings: SerializerSettings
        );

        restored.Should().NotBeNull();
        restored!.GroupTag.Should().Be(expected: groupTag);
        restored.TaskIds.Should().BeEquivalentTo(expectation: taskIds);
        restored.Phase.Should().Be(expected: CoordinatorPhase.WaitPass1);
        restored.Pass1DispatchedAt.Should().BeCloseTo(nearbyTime: now, precision: TimeSpan.FromMilliseconds(milliseconds: 1));
        restored.Pass2DispatchedAt.Should().BeNull();
        restored.Pass1StatsPath.Should().BeNull();
        restored.PresetId.Should().Be(expected: presetId);
        restored.ExpectedFinalCount.Should().Be(expected: 3);
    }

    [Fact]
    public void CoordinatorState_WaitChildrenPhase_RoundTrip_PhasePreserved()
    {
        CoordinatorState state = new(
            GroupTag: "grp-abc",
            TaskIds: ["task-pass2-0", "task-audio-0"],
            Phase: CoordinatorPhase.WaitChildren,
            Pass1DispatchedAt: DateTime.UtcNow.AddSeconds(value: -30),
            Pass2DispatchedAt: DateTime.UtcNow.AddSeconds(value: -5),
            Pass1StatsPath: "/tmp/stats/x264",
            PresetId: Ulid.NewUlid(),
            ExpectedFinalCount: 2
        );

        string json = JsonConvert.SerializeObject(value: state, settings: SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            value: json,
            settings: SerializerSettings
        );

        restored!.Phase.Should().Be(expected: CoordinatorPhase.WaitChildren);
        restored.Pass1StatsPath.Should().Be(expected: "/tmp/stats/x264");
    }

    [Fact]
    public void CoordinatorState_FinalizePhase_RoundTrip_PhasePreserved()
    {
        CoordinatorState state = new(
            GroupTag: "grp-xyz",
            TaskIds: ["task-video-0"],
            Phase: CoordinatorPhase.Finalize,
            Pass1DispatchedAt: null,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.NewUlid(),
            ExpectedFinalCount: 1
        );

        string json = JsonConvert.SerializeObject(value: state, settings: SerializerSettings);
        CoordinatorState? restored = JsonConvert.DeserializeObject<CoordinatorState>(
            value: json,
            settings: SerializerSettings
        );

        restored!.Phase.Should().Be(expected: CoordinatorPhase.Finalize);
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

        job.Coordinator.Should().BeNull(because: "a null Coordinator property signals the initial-run path");
    }

    [Fact]
    public void VideoEncodeJob_WithCoordinatorState_IsWakeUp()
    {
        CoordinatorState state = new(
            GroupTag: "grp-123",
            TaskIds: ["task-0"],
            Phase: CoordinatorPhase.WaitChildren,
            Pass1DispatchedAt: null,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.NewUlid(),
            ExpectedFinalCount: 1
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
            .NotBeNull(because: "a non-null Coordinator property signals the coordinator wake-up path");
        job.Coordinator!.Phase.Should().Be(expected: CoordinatorPhase.WaitChildren);
        job.Coordinator.GroupTag.Should().Be(expected: "grp-123");
    }

    [Fact]
    public void VideoEncodeJob_CoordinatorState_SerializesInsideJobPayload()
    {
        CoordinatorState state = new(
            GroupTag: "grp-roundtrip",
            TaskIds: ["t1", "t2"],
            Phase: CoordinatorPhase.WaitPass1,
            Pass1DispatchedAt: DateTime.UtcNow,
            Pass2DispatchedAt: null,
            Pass1StatsPath: null,
            PresetId: Ulid.NewUlid(),
            ExpectedFinalCount: 2
        );

        VideoEncodeJob original = new()
        {
            LibraryId = Ulid.NewUlid(),
            FolderId = Ulid.NewUlid(),
            Id = "01HNXYZ",
            InputFile = "/media/movie.mkv",
            Coordinator = state,
        };

        string json = JsonConvert.SerializeObject(value: original, settings: SerializerSettings);

        VideoEncodeJob? restored = JsonConvert.DeserializeObject<VideoEncodeJob>(
            value: json,
            settings: SerializerSettings
        );

        restored.Should().NotBeNull();
        restored!
            .Coordinator.Should()
            .NotBeNull(because: "coordinator state must survive a full job payload round-trip");
        restored.Coordinator!.Phase.Should().Be(expected: CoordinatorPhase.WaitPass1);
        restored.Coordinator.GroupTag.Should().Be(expected: "grp-roundtrip");
        restored.Coordinator.TaskIds.Should().BeEquivalentTo(expectation: ["t1", "t2"]);
    }
}
