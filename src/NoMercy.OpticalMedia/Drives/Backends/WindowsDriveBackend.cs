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

using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.OpticalMedia.Drives.Backends;

/// <summary>
/// Windows event-driven backend using WMI <c>__InstanceModificationEvent</c>
/// against <c>Win32_LogicalDisk</c> filtered to <c>DriveType=5</c> (CD-ROM).
/// Falls back to <see cref="PollingDriveBackend"/> at construction failure.
/// </summary>
[SupportedOSPlatform(platformName: "windows")]
public sealed class WindowsDriveBackend : IDriveBackend
{
    private readonly Channel<DriveEvent> _events = Channel.CreateUnbounded<DriveEvent>(
        options: new() { SingleReader = true, SingleWriter = true }
    );
    private readonly ILogger<WindowsDriveBackend> _logger;
    private ManagementEventWatcher? _watcher;

    public WindowsDriveBackend(ILogger<WindowsDriveBackend> logger)
    {
        _logger = logger;

        WqlEventQuery query = new(
            queryOrEventClassName: "SELECT * FROM __InstanceModificationEvent WITHIN 2 "
                                   + "WHERE TargetInstance ISA 'Win32_LogicalDisk' "
                                   + "AND TargetInstance.DriveType = 5"
        );
        _watcher = new(query: query);
        _watcher.EventArrived += OnDriveChanged;
        _watcher.Start();
    }

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

    private void OnDriveChanged(object? sender, EventArrivedEventArgs e)
    {
        try
        {
            ManagementBaseObject target = (ManagementBaseObject)e.NewEvent[propertyName: "TargetInstance"];
            string deviceId = (string)target[propertyName: "DeviceID"];
            string mount = deviceId + Path.DirectorySeparatorChar;
            string? volumeName = target[propertyName: "VolumeName"] as string;
            bool hasDisc = !string.IsNullOrEmpty(value: volumeName);

            OpticalDiscType type = hasDisc ? Optical.GetDiscType(drivePath: mount) : OpticalDiscType.None;
            DiscDrive drive = new(Path: mount, Label: volumeName, HasDisc: hasDisc, DiscType: type);

            DriveEventType eventType = hasDisc
                ? DriveEventType.DiscInserted
                : DriveEventType.DiscEjected;

            _events.Writer.TryWrite(item: new(Type: eventType, Drive: drive));
        }
        catch (Exception ex)
        {
            _logger.LogInformation(message: "[WindowsDriveBackend] OnDriveChanged: {Error}", args: ex.Message);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_watcher is not null)
        {
            _watcher.EventArrived -= OnDriveChanged;
            _watcher.Stop();
            _watcher.Dispose();
            _watcher = null;
        }
        _events.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
