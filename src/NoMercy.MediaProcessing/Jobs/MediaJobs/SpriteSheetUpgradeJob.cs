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

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Re-renders one title's scrub-preview sheet at the current tile width.
///
/// <para>A library scan queues this when it finds a sheet narrower than the
/// server now renders. Titles encoded when the tile was 160 wide show a preview
/// blown up three and a half times on a television, and there is no reason to
/// re-encode a single frame of video to fix that — the sheet is a sidecar, and
/// this rebuilds only the sidecar.</para>
///
/// <para>Idempotent: it re-reads the folder and no-ops when a wide-enough sheet
/// is already there, so re-queuing across scans until it lands is harmless.</para>
/// </summary>
[Serializable]
public class SpriteSheetUpgradeJob : IShouldQueue, IJobStorageInjector
{
    // The encoder queue, because this is an ffmpeg run and must not compete with
    // encodes for the same hardware. Below a real encode's priority (4) so live
    // work always drains first — nobody is waiting on a preview.
    public string QueueName => "encoder";
    public int Priority => 1;

    public string FolderId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string HostFolder { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    private ISpriteSheetRefresher? _refresher;
    private IStorageFactory? _storageFactory;

    public SpriteSheetUpgradeJob() { }

    public SpriteSheetUpgradeJob(string folderId, string driverId, string hostFolder, string title)
    {
        FolderId = folderId;
        DriverId = driverId;
        HostFolder = hostFolder;
        Title = title;
    }

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        _refresher = serviceProvider.GetService<ISpriteSheetRefresher>();
        _storageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
    }

    public async Task Handle()
    {
        if (_refresher is null || _storageFactory is null)
            return;
        if (!Ulid.TryParse(FolderId, out Ulid folderId))
            return;
        if (!Ulid.TryParse(DriverId, out Ulid driverId))
            return;

        IStorage storage = _storageFactory.For(folderId, driverId, string.Empty);
        if (!storage.Exists(HostFolder))
            return;

        IReadOnlyList<string> undersized = SpriteSheet.SelectUndersized(
            storage
                .List(HostFolder, null, recursive: false)
                .Where(entry => !entry.IsDirectory)
                .Select(entry => storage.GetName(entry.Path))
        );

        if (undersized.Count == 0)
            return;

        HlsDerivatives derivatives = new();

        string? sheet = await _refresher.RefreshAsync(
            storage,
            HostFolder,
            derivatives.SpriteVttThumbnailWidth,
            derivatives.SpriteVttIntervalSeconds,
            CancellationToken.None
        );

        if (sheet is null)
        {
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: nothing playable to sample in {HostFolder}",
                LogEventLevel.Warning
            );
            return;
        }

        Logger.App(
            $"[SpriteSheetUpgrade] {Title}: {string.Join(", ", undersized)} -> {sheet}",
            LogEventLevel.Information
        );
    }
}
