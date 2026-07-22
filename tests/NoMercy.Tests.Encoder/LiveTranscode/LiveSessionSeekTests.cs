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
    // Seek triggers new runner — Spawn called twice, first CTS cancelled once
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_WithFactory_SpawnsNewRunner()
    {
        LiveSession session = new(sessionId: "seek-001", quality: MakeQuality());
        int spawnCount = 0;
        SemaphoreSlim spawned = new(initialCount: 0, maxCount: int.MaxValue);

        session.AttachRunnerFactory(
            factory: (_, _) =>
            {
                Interlocked.Increment(location: ref spawnCount);
                spawned.Release();
                return Task.CompletedTask;
            }
        );

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 30), ct: CancellationToken.None);

        // Spawn happens via Task.Run inside SeekAsync — wait for it.
        (await spawned.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2)))
            .Should()
            .BeTrue();
        spawnCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task SeekAsync_Twice_SpawnsRunnerTwice()
    {
        LiveSession session = new(sessionId: "seek-002", quality: MakeQuality());
        int spawnCount = 0;
        SemaphoreSlim spawned = new(initialCount: 0, maxCount: int.MaxValue);

        session.AttachRunnerFactory(
            factory: (_, _) =>
            {
                Interlocked.Increment(location: ref spawnCount);
                spawned.Release();
                return Task.CompletedTask;
            }
        );

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 10), ct: CancellationToken.None);
        (await spawned.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2))).Should().BeTrue();

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 60), ct: CancellationToken.None);
        (await spawned.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2))).Should().BeTrue();

        spawnCount.Should().Be(expected: 2);
    }

    [Fact]
    public async Task SeekAsync_CancelsOldRunnerToken()
    {
        LiveSession session = new(sessionId: "seek-003", quality: MakeQuality());

        // Capture the first token before seeking.
        CancellationToken firstToken = session.RunnerCancellation;

        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 10), ct: CancellationToken.None);

        // After seek the old token must be cancelled.
        firstToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task SeekAsync_UpdatesTranscodedPosition()
    {
        LiveSession session = new(sessionId: "seek-004", quality: MakeQuality());
        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 120), ct: CancellationToken.None);

        session.TranscodedPosition.Should().Be(expected: TimeSpan.FromSeconds(seconds: 120));
    }

    [Fact]
    public async Task SeekAsync_SetsTranscodingState_WhenFactoryAttached()
    {
        LiveSession session = new(sessionId: "seek-005", quality: MakeQuality());
        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 30), ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
    }

    [Fact]
    public async Task SeekAsync_SetsSeekingState_WhenNoFactoryAttached()
    {
        LiveSession session = new(sessionId: "seek-006", quality: MakeQuality());
        // No factory attached — falls into "seeking only" path.

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 30), ct: CancellationToken.None);

        // Without a factory the state stays Seeking because no runner flips it back.
        session.State.Should().Be(expected: LiveSessionState.Seeking);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Runner factory receives correct seek position
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_PassesCorrectPositionToFactory()
    {
        LiveSession session = new(sessionId: "seek-007", quality: MakeQuality());
        TaskCompletionSource<TimeSpan> factoryCalled = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );

        session.AttachRunnerFactory(
            factory: (pos, _) =>
            {
                factoryCalled.TrySetResult(result: pos);
                return Task.CompletedTask;
            }
        );

        TimeSpan targetPosition = TimeSpan.FromSeconds(seconds: 75);
        await session.SeekAsync(position: targetPosition, ct: CancellationToken.None);

        // The factory task is fire-and-forget — wait on the TCS deterministically
        // instead of guessing how long the runtime needs to schedule it.
        TimeSpan capturedPosition = await factoryCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));

        capturedPosition.Should().Be(expected: targetPosition);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // New runner token is distinct from old one
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_IssuesNewRunnerToken()
    {
        LiveSession session = new(sessionId: "seek-008", quality: MakeQuality());
        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        CancellationToken tokenBefore = session.RunnerCancellation;

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 10), ct: CancellationToken.None);

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
        LiveSession session = new(sessionId: "seek-009", quality: MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(callback: () => resetCalled = true);
        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 45), ct: CancellationToken.None);

        resetCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeQualityAsync_InvokesBufferResetCallback()
    {
        LiveSession session = new(sessionId: "seek-010", quality: MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(callback: () => resetCalled = true);
        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        LiveQuality newQuality = MakeQuality() with { Id = "720p", Label = "720p" };

        await session.ChangeQualityAsync(qualityId: "720p", newQuality: newQuality, ct: CancellationToken.None);

        resetCalled.Should().BeTrue();
    }
}
