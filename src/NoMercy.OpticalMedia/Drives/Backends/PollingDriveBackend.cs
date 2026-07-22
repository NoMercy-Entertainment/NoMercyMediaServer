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
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(seconds: 5);

    private readonly Channel<DriveEvent> _events = Channel.CreateUnbounded<DriveEvent>(
        options: new() { SingleReader = true, SingleWriter = false }
    );
    private readonly HashSet<string> _knownDrives = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource? _loopCts;

    public IReadOnlyList<DiscDrive> GetDrives() =>
        Optical
            .GetOpticalDrives()
            .Select(selector: kvp =>
            {
                bool hasDisc = !string.IsNullOrEmpty(value: kvp.Value);
                OpticalDiscType type = hasDisc
                    ? Optical.GetDiscType(drivePath: kvp.Key)
                    : OpticalDiscType.None;
                return new DiscDrive(Path: kvp.Key, Label: kvp.Value, HasDisc: hasDisc, DiscType: type);
            })
            .ToList();

    public async IAsyncEnumerable<DriveEvent> ListenAsync(
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
        _ = Task.Run(function: () => PollLoopAsync(ct: _loopCts.Token), cancellationToken: _loopCts.Token);

        while (!ct.IsCancellationRequested)
        {
            DriveEvent ev;
            try
            {
                ev = await _events.Reader.ReadAsync(cancellationToken: ct);
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
                    .Where(predicate: d => d.Value != null)
                    .ToDictionary(keySelector: d => d.Key, elementSelector: d => d.Value);

                lock (_lock)
                {
                    foreach ((string drive, string? label) in detected)
                        if (_knownDrives.Add(item: drive))
                        {
                            OpticalDiscType type = Optical.GetDiscType(drivePath: drive);
                            _events.Writer.TryWrite(
                                item: new(
                                    Type: DriveEventType.DiscInserted,
                                    Drive: new(Path: drive, Label: label, HasDisc: true, DiscType: type)
                                )
                            );
                        }

                    foreach (string ejected in _knownDrives.Except(second: detected.Keys).ToList())
                    {
                        _events.Writer.TryWrite(
                            item: new(
                                Type: DriveEventType.DiscEjected,
                                Drive: new(Path: ejected, Label: null, HasDisc: false, DiscType: OpticalDiscType.None)
                            )
                        );
                        _knownDrives.Remove(item: ejected);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation(message: "[PollingDriveBackend] {Error}", args: ex.Message);
            }

            try
            {
                await Task.Delay(delay: PollInterval, cancellationToken: ct);
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
