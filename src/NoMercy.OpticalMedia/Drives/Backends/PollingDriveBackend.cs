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
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.OpticalMedia.Drives.Backends;

/// <summary>
/// Cross-platform polling fallback. Diffs drive state every <see cref="PollInterval"/>
/// against the previous snapshot and emits insert/eject events.
/// </summary>
public sealed class PollingDriveBackend(ILogger<PollingDriveBackend> logger) : IDriveBackend
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly Channel<DriveEvent> _events = Channel.CreateUnbounded<DriveEvent>(
        new() { SingleReader = true, SingleWriter = false }
    );
    private readonly HashSet<string> _knownDrives = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource? _loopCts;

    public IReadOnlyList<DiscDrive> GetDrives() =>
        [
            .. Optical
                .GetOpticalDrives()
                .Select(kvp =>
                {
                    bool hasDisc = !string.IsNullOrEmpty(kvp.Value);
                    OpticalDiscType type = hasDisc
                        ? Optical.GetDiscType(kvp.Key)
                        : OpticalDiscType.None;
                    return new DiscDrive(kvp.Key, kvp.Value, hasDisc, type);
                }),
        ];

    public async IAsyncEnumerable<DriveEvent> ListenAsync(
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => PollLoopAsync(_loopCts.Token), _loopCts.Token);

        while (!ct.IsCancellationRequested)
        {
            DriveEvent ev;
            try
            {
                ev = await _events.Reader.ReadAsync(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            yield return ev;
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Dictionary<string, string?> detected = Optical
                    .GetOpticalDrives()
                    .Where(d => d.Value != null)
                    .ToDictionary(d => d.Key, d => d.Value);

                lock (_lock)
                {
                    foreach ((string drive, string? label) in detected)
                        if (_knownDrives.Add(drive))
                        {
                            OpticalDiscType type = Optical.GetDiscType(drive);
                            _events.Writer.TryWrite(
                                new(DriveEventType.DiscInserted, new(drive, label, true, type))
                            );
                        }

                    foreach (string ejected in _knownDrives.Except(detected.Keys).ToList())
                    {
                        _events.Writer.TryWrite(
                            new(
                                DriveEventType.DiscEjected,
                                new(ejected, null, false, OpticalDiscType.None)
                            )
                        );
                        _knownDrives.Remove(ejected);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("[PollingDriveBackend] {Error}", ex.Message);
            }

            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
            _loopCts = null;
        }
        _events.Writer.TryComplete();
    }
}
