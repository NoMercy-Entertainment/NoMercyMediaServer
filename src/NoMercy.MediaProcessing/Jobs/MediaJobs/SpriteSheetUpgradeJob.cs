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
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Files;
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
    private IFileRepository? _fileRepository;

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
        _fileRepository = serviceProvider.GetService<IFileRepository>();
    }

    public async Task Handle()
    {
        // Every branch below that gives up says why. A previous version returned
        // silently on each of them, so a hundred-odd jobs failing on a missing
        // dependency read exactly like a hundred-odd jobs finding nothing to do —
        // the only trace was a row in FailedJobs nobody was looking at.
        if (_refresher is null || _storageFactory is null)
        {
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: sprite services unavailable in this scope",
                LogEventLevel.Warning
            );
            return;
        }

        if (
            !Ulid.TryParse(FolderId, out Ulid folderId)
            || !Ulid.TryParse(DriverId, out Ulid driverId)
        )
        {
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: unreadable folder/driver id ({FolderId}/{DriverId})",
                LogEventLevel.Warning
            );
            return;
        }

        IStorage storage = _storageFactory.For(folderId, driverId, string.Empty);
        if (!storage.Exists(HostFolder))
        {
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: {HostFolder} is gone",
                LogEventLevel.Warning
            );
            return;
        }

        IReadOnlyList<string> undersized = SpriteSheet.SelectUndersized(
            storage
                .List(HostFolder, null, recursive: false)
                .Where(entry => !entry.IsDirectory)
                .Select(entry => storage.GetName(entry.Path))
        );

        // The only quiet exit worth keeping: another pass already widened this
        // sheet, which is the expected outcome of re-queuing across scans.
        if (undersized.Count == 0)
            return;

        HlsDerivatives derivatives = new();

        // Sampling a full-length title is minutes of ffmpeg. Without these the
        // dashboard had two observable states for that whole stretch, "queued"
        // and "gone", and a card that sat still for four minutes is
        // indistinguishable from a queue that has stopped moving.
        await PublishProgressAsync(0, "running");

        string? sheet = await _refresher.RefreshAsync(
            storage,
            HostFolder,
            derivatives.SpriteVttThumbnailWidth,
            derivatives.SpriteVttIntervalSeconds,
            progress => _ = PublishProgressAsync(progress.PercentComplete, "running"),
            CancellationToken.None
        );

        if (sheet is null)
        {
            await PublishProgressAsync(0, "failed");
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: nothing playable to sample in {HostFolder}",
                LogEventLevel.Warning
            );
            return;
        }

        await PublishProgressAsync(100, "completed");

        int repointed = await RepointRegistrationAsync(sheet);

        Logger.App(
            $"[SpriteSheetUpgrade] {Title}: {string.Join(", ", undersized)} -> {sheet}"
                + $" ({repointed} registration(s) repointed)",
            LogEventLevel.Information
        );
    }

    /// <summary>
    /// Names the new pair everywhere the old one was registered.
    ///
    /// <para>The sheet this job replaces is deleted, and the registration a scan
    /// wrote still named it — so an upgraded title answered every scrub request
    /// with a 404 until an unrelated scan happened to run. The rebuild is only
    /// half the work; the record clients read is the other half.</para>
    /// </summary>
    private async Task<int> RepointRegistrationAsync(string sheet)
    {
        if (_fileRepository is null)
        {
            Logger.App(
                $"[SpriteSheetUpgrade] {Title}: no file repository in this scope, "
                    + $"{HostFolder} still names its old sheet",
                LogEventLevel.Warning
            );
            return 0;
        }

        return await _fileRepository.RepointPreviewTracksAsync(
            HostFolder,
            sheet,
            Path.ChangeExtension(sheet, ".vtt")
        );
    }

    /// <summary>
    /// One progress message on the same channel an encode reports on.
    ///
    /// <para>Carries <c>target</c> because this job has no media id to be keyed
    /// by — the queue row it belongs to is identified by its host folder, and
    /// <c>id</c> stays 0 so a client reading the numeric key sees nothing rather
    /// than something belonging to media 0. Lower-case keys because that is the
    /// encoder-progress contract: the serializer has no naming strategy, so a
    /// PascalCase member reaches the dashboard as an unreadable field.</para>
    /// </summary>
    private async Task PublishProgressAsync(double percent, string status)
    {
        await EventBusProvider.Current.PublishAsync(
            new EncodingProgressBroadcastedEvent
            {
                ProgressData = new
                {
                    id = 0,
                    target = HostFolder,
                    status,
                    title = Title,
                    progress = Math.Clamp(percent, 0, 100),
                },
            }
        );
    }
}
