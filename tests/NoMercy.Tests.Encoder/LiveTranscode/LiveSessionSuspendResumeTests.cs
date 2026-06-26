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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Tests for the wired Suspend/Resume behavior added in the live-transcode
/// adaptive-buffer pass: Suspend cancels the runner CTS; Resume spawns a new
/// runner from the current playback position.
/// </summary>
public class LiveSessionSuspendResumeTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend cancels the runner CTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_WhenTranscoding_CancelsRunnerCancellationToken()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);

        CancellationToken runnerToken = session.RunnerCancellation;

        session.Suspend();

        // The original runner token must be cancelled so FFmpeg exits.
        runnerToken.IsCancellationRequested.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend idempotence: a second Suspend while already Buffered is a no-op
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_WhenAlreadyBuffered_DoesNotChangeState()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Buffered);

        session.Suspend();

        session.State.Should().Be(LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume spawns a new runner from the playback position
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_AfterSuspend_SpawnsNewRunner()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);

        TimeSpan reportedPosition = TimeSpan.FromSeconds(42);
        session.ReportPlaybackPosition(reportedPosition);

        TimeSpan? spawnedPosition = null;
        session.AttachRunnerFactory(
            (pos, _) =>
            {
                spawnedPosition = pos;
                return Task.CompletedTask;
            }
        );

        session.Suspend();
        session.Resume();

        // Give the fire-and-forget Task a moment to execute
        await Task.Delay(50);

        spawnedPosition.Should().NotBeNull();
        spawnedPosition!.Value.Should().Be(reportedPosition);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume without a factory wired (no-runner path) still flips state
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Resume_NoFactory_FlipsStateWithoutThrowing()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Buffered);

        Action act = () => session.Resume();

        act.Should().NotThrow();
        session.State.Should().Be(LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume idempotence: a second Resume while already Transcoding is a no-op
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_WhenAlreadyTranscoding_DoesNotSpawnSecondRunner()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);

        int spawnCount = 0;
        session.AttachRunnerFactory(
            (_, _) =>
            {
                spawnCount++;
                return Task.CompletedTask;
            }
        );

        session.Resume();

        await Task.Delay(50);

        spawnCount.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume issues a fresh CTS after Suspend
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_AfterSuspend_RunnerTokenIsNotCancelled()
    {
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        session.Suspend();

        CancellationToken oldToken = session.RunnerCancellation;
        oldToken.IsCancellationRequested.Should().BeTrue();

        session.Resume();

        await Task.Delay(50);

        // After resume, a fresh CTS is installed — new token must not be cancelled
        session.RunnerCancellation.IsCancellationRequested.Should().BeFalse();
    }
}
