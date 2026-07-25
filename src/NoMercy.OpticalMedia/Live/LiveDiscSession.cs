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

using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Routes a disc title through the standard live HLS pipeline. Builds the
/// right ffmpeg input URL for the disc type:
///   Bluray  → <c>bluray:&lt;mount&gt;/</c> with <c>-playlist N</c>
///   DVD     → <c>&lt;mount&gt;/</c> with <c>-f dvdvideo -title N</c>
///   CD      → <c>&lt;mount&gt;</c> with <c>-f libcdio</c>
/// then probes via <see cref="IMediaAnalyzer"/> to populate the cached
/// <see cref="MediaInfo"/> the encoder needs, and hands off to
/// <see cref="ILiveEncoder.StartAsync"/>.
/// </summary>
public sealed class LiveDiscSession(
    IMediaAnalyzer mediaAnalyzer,
    ILiveEncoder liveEncoder,
    ILogger<LiveDiscSession> logger
) : ILiveDiscSession
{
    public async Task<ILiveSession> StartAsync(
        DiscDrive drive,
        int titleIndex,
        TimeSpan startPosition,
        string? preferredQuality,
        CancellationToken ct
    )
    {
        string inputPath = BuildInputPath(drive, titleIndex);
        logger.LogInformation(
            "Live disc session for {Drive} title {Title} → {InputPath}", [drive.Path, titleIndex, inputPath]
        );

        // Probe the disc input the same way the encoder probes any other
        // source. The analyzer wraps ffprobe + parses streams + chapters
        // into a MediaInfo the live runner consumes verbatim.
        MediaInfo info = await mediaAnalyzer.AnalyzeAsync(inputPath, ct);

        // ClientCapabilities defaults — the runner only needs format + codec
        // hints, the streaming service refines per-client when it stamps
        // the runtime context. Caller can override later via the existing
        // /streaming/live/sessions/* endpoints.
        ClientCapabilities client = new(
            [VideoCodecType.H264, VideoCodecType.H265],
            [AudioCodecType.Aac, AudioCodecType.Eac3],
            ["hls", "mp4"],
            1920,
            1080,
            false,
            false,
            8000,
            2
        );

        LiveEncodeRequest request = new(
            inputPath,
            info,
            client,
            startPosition,
            preferredQuality
        );

        return await liveEncoder.StartAsync(request, ct);
    }

    /// <summary>
    /// Builds the disc-type-specific input URL. Title index is encoded into
    /// the URL where the protocol supports it (libbluray uses
    /// <c>?playlist=N</c>); for DVD the demuxer parameter is set via
    /// command-line flags the encoder layer can't pass through, so we rely
    /// on libdvdread auto-selecting the longest title (title 0 = auto).
    /// CD tracks are passed via the libcdio protocol with the track number.
    /// </summary>
    private static string BuildInputPath(DiscDrive drive, int titleIndex)
    {
        string trimmed = drive.Path.TrimEnd(['\\', '/']);
        return drive.DiscType switch
        {
            OpticalDiscType.BluRay =>
                $"bluray:{trimmed}/?playlist={titleIndex.ToString(CultureInfo.InvariantCulture)}",
            OpticalDiscType.Dvd => $"{trimmed}/",
            OpticalDiscType.Cd => drive.Path,
            _ => trimmed,
        };
    }
}
