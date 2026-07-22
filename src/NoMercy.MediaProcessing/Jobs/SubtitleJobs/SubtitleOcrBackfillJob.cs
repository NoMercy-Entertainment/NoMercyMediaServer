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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Jobs.SubtitleJobs;

/// <summary>
/// OCRs one preserved bitmap-subtitle sidecar (<c>.sup</c>) into its
/// <c>.vtt</c> sibling. A library scan enqueues this whenever it finds a bitmap
/// track with no usable text sibling — titles encoded before the OCR pipeline
/// worked never got their sidecar, and OCR is far too slow to run inline on the
/// scan thread. The job reads the <c>.sup</c> directly, so the original release
/// source is not required (it is usually long gone by the time a title is being
/// rescanned). Idempotent: it no-ops when the sidecar already exists or the
/// <c>.sup</c> is gone, so re-enqueuing across scans is harmless.
/// </summary>
[Serializable]
public class SubtitleOcrBackfillJob : IShouldQueue, IJobStorageInjector
{
    // Shares the encoder queue (the OCR ffmpeg run is encoder work), but at a
    // lower priority than a real encode (4) so live encodes always drain first.
    public string QueueName => "encoder";
    public int Priority => 2;

    public string FolderId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string HostFolder { get; set; } = string.Empty;
    public string SupFileName { get; set; } = string.Empty;
    public string MediaTitle { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;

    private ISubtitleOcrEngine? _ocrEngine;
    private IStorageFactory? _storageFactory;

    public SubtitleOcrBackfillJob() { }

    public SubtitleOcrBackfillJob(
        string folderId,
        string driverId,
        string hostFolder,
        string supFileName,
        string mediaTitle,
        string language,
        string variant
    )
    {
        FolderId = folderId;
        DriverId = driverId;
        HostFolder = hostFolder;
        SupFileName = supFileName;
        MediaTitle = mediaTitle;
        Language = language;
        Variant = variant;
    }

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        _ocrEngine = serviceProvider.GetService<ISubtitleOcrEngine>();
        _storageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
    }

    public async Task Handle()
    {
        if (_ocrEngine is null || _storageFactory is null)
            return;
        if (!Ulid.TryParse(base32: FolderId, ulid: out Ulid folderId))
            return;
        if (!Ulid.TryParse(base32: DriverId, ulid: out Ulid driverId))
            return;

        IStorage storage = _storageFactory.For(folderId: folderId, driverId: driverId, subPath: string.Empty);

        string subtitleFolder = storage.CombinePath(parent: HostFolder, child: "subtitles");
        string vttPath = storage.CombinePath(
            parent: subtitleFolder,
            child: $"{MediaTitle}.{Language}.{Variant}.vtt"
        );

        // A prior pass already produced the sidecar — nothing to do.
        if (storage.Exists(path: vttPath))
            return;

        string supPath = storage.CombinePath(parent: subtitleFolder, child: SupFileName);
        if (!storage.Exists(path: supPath))
            return;

        // A standalone .sup carries exactly one PGS stream, so the subtitle-stream
        // index is always 0. The sidecar target reproduces the .sup's own
        // {MediaTitle}.{lang}.{variant} stem so the scan pairs the new .vtt with
        // the bitmap track and the orphan clears on the next pass.
        SubtitleTrack track = await _ocrEngine.OcrAsync(
            inputPath: supPath,
            streamIndex: 0,
            language: Language,
            outputFormat: SubtitleCodecType.WebVtt,
            ct: CancellationToken.None,
            sourceStorage: storage,
            sidecar: new OcrSidecarTarget(Storage: storage, OutputDirectory: HostFolder, MediaTitle: MediaTitle, Variant: Variant)
        );

        Logger.App(
            message: $"[SubtitleOcrBackfill] {MediaTitle}.{Language}.{Variant}: OCR'd {track.CueCount} cues from {SupFileName}",
            level: LogEventLevel.Information
        );
    }
}
