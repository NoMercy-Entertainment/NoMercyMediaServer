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

using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NoMercy.Encoder.LiveTranscode;

public class LiveSession : ILiveSession
{
    private readonly Channel<Segment> _segmentChannel = Channel.CreateUnbounded<Segment>();

    // Protects concurrent seeks: only one seek can manipulate the runner CTS at a time.
    private readonly SemaphoreSlim _seekLock = new(1, 1);

    // Tracks the current runner's cancellation source. Replaced on each seek.
    private CancellationTokenSource _runnerCts = new();

    // Linked to _runnerCts; cancelled when the whole session is disposed.
    private readonly CancellationTokenSource _sessionCts = new();

    private int _state = (int)LiveSessionState.Starting;
    private long _playbackPositionTicks;
    private TimeSpan _transcodedPosition;
    private double _currentSpeed;
    private int _audioStreamIndex;

    // MinValue until the first runner is dispatched, so a session that never
    // spawns one (unit tests exercising Evaluate directly) is not treated as
    // "warming up". Stamped on every real (re)spawn via MarkTranscodeStart.
    private long _lastTranscodeStartTicks = DateTime.MinValue.Ticks;

    // Injected by LiveEncoder after construction via AttachRunnerFactory.
    private Func<TimeSpan, CancellationToken, Task>? _runnerFactory;

    // Injected by LiveStreamingService via AttachBufferResetCallback.
    private Action? _bufferResetCallback;

    /// <summary>
    /// Fires when the CURRENT runner should terminate. Replaced on each seek.
    /// The outer session lifetime is tracked via <see cref="_sessionCts"/>.
    /// </summary>
    public CancellationToken RunnerCancellation => _runnerCts.Token;

    public string SessionId { get; }
    public LiveSessionState State =>
        (LiveSessionState)Interlocked.CompareExchange(ref _state, 0, 0);
    public LiveQuality CurrentQuality { get; private set; }
    public int CurrentAudioStreamIndex => Volatile.Read(ref _audioStreamIndex);
    public double CurrentSpeed => _currentSpeed;
    public TimeSpan TranscodedPosition => _transcodedPosition;
    public TimeSpan BufferAhead =>
        _transcodedPosition - new TimeSpan(Interlocked.Read(ref _playbackPositionTicks));

    public IAsyncEnumerable<Segment> Segments => ReadSegmentsAsync();

    public LiveSession(string sessionId, LiveQuality quality)
    {
        SessionId = sessionId;
        CurrentQuality = quality;
    }

    public void AttachRunnerFactory(Func<TimeSpan, CancellationToken, Task> factory)
    {
        _runnerFactory = factory;
    }

    public void AttachBufferResetCallback(Action callback)
    {
        _bufferResetCallback = callback;
    }

    // Called by the encoder to push completed segments into the channel
    internal void PushSegment(Segment segment)
    {
        _transcodedPosition = segment.StartTime + segment.Duration;
        _segmentChannel.Writer.TryWrite(segment);
    }

    internal void SetState(LiveSessionState state) => Interlocked.Exchange(ref _state, (int)state);

    // Updates the reported quality in place, WITHOUT tearing down/restarting the
    // runner. Used by LiveFfmpegRunner's NVENC-session-cap fallback: the runner
    // has already substituted a software encoder for the CURRENT run, so the
    // session's public quality just needs to catch up for API/UI reporting and
    // so a later seek/resume respawn (which reads CurrentQuality) keeps using
    // the software encoder instead of retrying the exhausted GPU slot.
    internal void SetQuality(LiveQuality quality) => CurrentQuality = quality;

    // Sets the audio track a subsequent (re)spawn maps. Called at session start
    // with the track resolved from the library's language preference.
    internal void SetAudioStreamIndex(int index) => Volatile.Write(ref _audioStreamIndex, index);

    internal void SetSpeed(double speed) => _currentSpeed = speed;

    internal void Complete() => _segmentChannel.Writer.TryComplete();

    /// <summary>
    /// Completes the segment channel only when <paramref name="runnerToken"/>
    /// still belongs to the CURRENT runner generation. A seek / quality-change
    /// / resume replaces <see cref="_runnerCts"/> with a fresh source before
    /// the runner it is replacing has necessarily exited; that superseded
    /// runner's own completion must not end the session's segment stream out
    /// from under the runner that replaced it. Called by
    /// <see cref="LiveFfmpegRunner.RunAsync"/>'s finally block instead of
    /// <see cref="Complete"/> directly.
    /// </summary>
    internal void CompleteIfCurrentRunner(CancellationToken runnerToken)
    {
        try
        {
            if (_runnerCts.Token != runnerToken)
                return;
        }
        catch (ObjectDisposedException)
        {
            // Session is already tearing down — DisposeAsync owns completing
            // the channel in that case.
            return;
        }

        Complete();
    }

    /// <summary>
    /// Cancels the current FFmpeg runner, resets position state, then spawns
    /// a new runner from <paramref name="position"/> via the attached factory.
    /// </summary>
    public async Task SeekAsync(TimeSpan position, CancellationToken ct)
    {
        await _seekLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            SetState(LiveSessionState.Seeking);

            // Tear down existing runner
            CancellationTokenSource oldCts = _runnerCts;
            try
            {
                await oldCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            finally
            {
                oldCts.Dispose();
            }

            // Reset position bookkeeping
            Interlocked.Exchange(ref _playbackPositionTicks, position.Ticks);
            _transcodedPosition = position;

            // Create a new CTS for the replacement runner, linked to session lifetime
            _runnerCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

            _bufferResetCallback?.Invoke();

            // Spawn new runner if a factory is wired up
            if (_runnerFactory is not null)
            {
                SetState(LiveSessionState.Transcoding);
                MarkTranscodeStart();

                _ = Task.Run(
                    () => _runnerFactory(position, _runnerCts.Token),
                    CancellationToken.None
                );
            }
        }
        finally
        {
            _seekLock.Release();
        }
    }

    public async Task ChangeQualityAsync(
        string qualityId,
        LiveQuality newQuality,
        CancellationToken ct
    )
    {
        await _seekLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _state, (int)LiveSessionState.ChangingQuality);

            // Tear down existing runner
            CancellationTokenSource oldCts = _runnerCts;
            try
            {
                await oldCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            finally
            {
                oldCts.Dispose();
            }

            // Update quality before spawning so the factory closure picks it up
            CurrentQuality = newQuality;

            // Create a new CTS for the replacement runner, linked to session lifetime
            _runnerCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

            _bufferResetCallback?.Invoke();

            // Spawn new runner if a factory is wired up
            if (_runnerFactory is not null)
            {
                SetState(LiveSessionState.Transcoding);

                // Keep same playback position — quality change doesn't rewind
                TimeSpan resumePosition = new(Interlocked.Read(ref _playbackPositionTicks));
                MarkTranscodeStart();
                _ = Task.Run(
                    () => _runnerFactory(resumePosition, _runnerCts.Token),
                    CancellationToken.None
                );
            }
        }
        finally
        {
            _seekLock.Release();
        }
    }

    /// <summary>
    /// Blocking acquire on the same <see cref="_seekLock"/> SeekAsync /
    /// ChangeQualityAsync await into asynchronously. Suspend/Resume are
    /// synchronous by contract (SignalR hub methods and the buffer-adaptive
    /// sweep call them without awaiting), so they take the lock via the
    /// synchronous companion instead of changing that contract — without it,
    /// a Resume racing a Seek could each read/replace _runnerCts out from
    /// under the other and leak a zombie ffmpeg the session no longer tracks.
    /// </summary>
    public void Suspend()
    {
        _seekLock.Wait();
        try
        {
            int previous = Interlocked.CompareExchange(
                ref _state,
                (int)LiveSessionState.Buffered,
                (int)LiveSessionState.Transcoding
            );

            if (previous != (int)LiveSessionState.Transcoding)
                return;

            try
            {
                _runnerCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed; nothing to cancel
            }
        }
        finally
        {
            _seekLock.Release();
        }
    }

    public void Resume()
    {
        _seekLock.Wait();
        try
        {
            int previous = Interlocked.CompareExchange(
                ref _state,
                (int)LiveSessionState.Transcoding,
                (int)LiveSessionState.Buffered
            );

            if (previous != (int)LiveSessionState.Buffered)
                return;

            // The CTS being replaced here was cancelled by the matching
            // Suspend() call but never disposed — Dispose() is idempotent so
            // this is safe even if something else already disposed it.
            _runnerCts.Dispose();

            _runnerCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

            if (_runnerFactory is not null)
            {
                MarkTranscodeStart();
                TimeSpan resumePosition = new(Interlocked.Read(ref _playbackPositionTicks));
                _ = Task.Run(
                    () => _runnerFactory(resumePosition, _runnerCts.Token),
                    CancellationToken.None
                );
            }
        }
        finally
        {
            _seekLock.Release();
        }
    }

    public void ReportPlaybackPosition(TimeSpan position) =>
        Interlocked.Exchange(ref _playbackPositionTicks, position.Ticks);

    public DateTime LastTranscodeStart =>
        new(Interlocked.Read(ref _lastTranscodeStartTicks), DateTimeKind.Utc);

    public void MarkTranscodeStart() =>
        Interlocked.Exchange(ref _lastTranscodeStartTicks, DateTime.UtcNow.Ticks);

    public ValueTask DisposeAsync()
    {
        try
        {
            _sessionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        try
        {
            _runnerCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        SetState(LiveSessionState.Ended);
        _segmentChannel.Writer.TryComplete();

        _seekLock.Dispose();
        _runnerCts.Dispose();
        _sessionCts.Dispose();
        return ValueTask.CompletedTask;
    }

    private async IAsyncEnumerable<Segment> ReadSegmentsAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (Segment segment in _segmentChannel.Reader.ReadAllAsync(ct))
        {
            yield return segment;
        }
    }
}
