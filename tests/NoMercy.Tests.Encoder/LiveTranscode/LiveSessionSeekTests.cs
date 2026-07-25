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
/// Verifies that SeekAsync tears down the current runner and spawns a new one.
/// </summary>
public class LiveSessionSeekTests
{
    private static LiveQuality MakeQuality() =>
        new(
            "1080p",
            "1080p",
            1920,
            1080,
            VideoCodecType.H264,
            8000,
            "libx264",
            false,
            2.0,
            true
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Seek triggers new runner — Spawn called twice, first CTS cancelled once
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_WithFactory_SpawnsNewRunner()
    {
        LiveSession session = new("seek-001", MakeQuality());
        int spawnCount = 0;
        SemaphoreSlim spawned = new(0, int.MaxValue);

        session.AttachRunnerFactory(
            (_, _) =>
            {
                Interlocked.Increment(ref spawnCount);
                spawned.Release();
                return Task.CompletedTask;
            }
        );

        await session.SeekAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        // Spawn happens via Task.Run inside SeekAsync — wait for it.
        (await spawned.WaitAsync(TimeSpan.FromSeconds(2)))
            .Should()
            .BeTrue();
        spawnCount.Should().Be(1);
    }

    [Fact]
    public async Task SeekAsync_Twice_SpawnsRunnerTwice()
    {
        LiveSession session = new("seek-002", MakeQuality());
        int spawnCount = 0;
        SemaphoreSlim spawned = new(0, int.MaxValue);

        session.AttachRunnerFactory(
            (_, _) =>
            {
                Interlocked.Increment(ref spawnCount);
                spawned.Release();
                return Task.CompletedTask;
            }
        );

        await session.SeekAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        (await spawned.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        await session.SeekAsync(TimeSpan.FromSeconds(60), CancellationToken.None);
        (await spawned.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        spawnCount.Should().Be(2);
    }

    [Fact]
    public async Task SeekAsync_CancelsOldRunnerToken()
    {
        LiveSession session = new("seek-003", MakeQuality());

        // Capture the first token before seeking.
        CancellationToken firstToken = session.RunnerCancellation;

        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        await session.SeekAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        // After seek the old token must be cancelled.
        firstToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task SeekAsync_UpdatesTranscodedPosition()
    {
        LiveSession session = new("seek-004", MakeQuality());
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        await session.SeekAsync(TimeSpan.FromSeconds(120), CancellationToken.None);

        session.TranscodedPosition.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task SeekAsync_SetsTranscodingState_WhenFactoryAttached()
    {
        LiveSession session = new("seek-005", MakeQuality());
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        await session.SeekAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Transcoding);
    }

    [Fact]
    public async Task SeekAsync_SetsSeekingState_WhenNoFactoryAttached()
    {
        LiveSession session = new("seek-006", MakeQuality());
        // No factory attached — falls into "seeking only" path.

        await session.SeekAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        // Without a factory the state stays Seeking because no runner flips it back.
        session.State.Should().Be(LiveSessionState.Seeking);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Runner factory receives correct seek position
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_PassesCorrectPositionToFactory()
    {
        LiveSession session = new("seek-007", MakeQuality());
        TaskCompletionSource<TimeSpan> factoryCalled = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        session.AttachRunnerFactory(
            (pos, _) =>
            {
                factoryCalled.TrySetResult(pos);
                return Task.CompletedTask;
            }
        );

        TimeSpan targetPosition = TimeSpan.FromSeconds(75);
        await session.SeekAsync(targetPosition, CancellationToken.None);

        // The factory task is fire-and-forget — wait on the TCS deterministically
        // instead of guessing how long the runtime needs to schedule it.
        TimeSpan capturedPosition = await factoryCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        capturedPosition.Should().Be(targetPosition);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // New runner token is distinct from old one
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_IssuesNewRunnerToken()
    {
        LiveSession session = new("seek-008", MakeQuality());
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        CancellationToken tokenBefore = session.RunnerCancellation;

        await session.SeekAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        CancellationToken tokenAfter = session.RunnerCancellation;

        // The new token must not already be cancelled.
        tokenAfter.IsCancellationRequested.Should().BeFalse();
        // And it must be a different registration than the cancelled old one.
        tokenBefore.IsCancellationRequested.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Coverage regression wall: seek must not invalidate what is already
    // transcoded; a quality change genuinely must.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_DoesNotInvokeBufferResetCallback()
    {
        LiveSession session = new("seek-009", MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(() => resetCalled = true);
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        await session.SeekAsync(TimeSpan.FromSeconds(45), CancellationToken.None);

        resetCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeQualityAsync_InvokesBufferResetCallback()
    {
        LiveSession session = new("seek-010", MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(() => resetCalled = true);
        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        LiveQuality newQuality = MakeQuality() with { Id = "720p", Label = "720p" };

        await session.ChangeQualityAsync("720p", newQuality, CancellationToken.None);

        resetCalled.Should().BeTrue();
    }
}
