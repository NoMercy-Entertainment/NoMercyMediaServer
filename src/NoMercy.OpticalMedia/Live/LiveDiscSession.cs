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
        string[] extraArgs = BuildExtraInputArgs(drive, titleIndex);
        logger.LogInformation(
            "Live disc session for {Drive} title {Title} → {InputPath} {ExtraArgs}",
            drive.Path,
            titleIndex,
            inputPath,
            string.Join(' ', extraArgs)
        );

        // Probe the disc input the same way the encoder probes any other
        // source. The analyzer wraps ffprobe + parses streams + chapters
        // into a MediaInfo the live runner consumes verbatim. Disc protocol
        // URLs (bluray:/dvd:) are not filesystem paths, so this must take the
        // extraInputArgs overload — the plain overload resolves inputPath
        // through IStorage.AcquireLocalPath, which is meaningless here, and
        // has no way to carry -playlist N ahead of -i.
        MediaInfo info = await mediaAnalyzer.AnalyzeAsync(inputPath, extraArgs, ct);

        // ClientCapabilities defaults — the runner only needs format + codec
        // hints, the streaming service refines per-client when it stamps
        // the runtime context. Caller can override later via the existing
        // /streaming/live/sessions/* endpoints.
        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264, VideoCodecType.H265],
            SupportedAudioCodecs: [AudioCodecType.Aac, AudioCodecType.Eac3],
            SupportedContainers: ["hls", "mp4"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: false,
            Supports10Bit: false,
            MaxBitrateKbps: 8000,
            MaxAudioChannels: 2
        );

        LiveEncodeRequest request = new(
            InputPath: inputPath,
            CachedInfo: info,
            Client: client,
            StartPosition: startPosition,
            PreferredQuality: preferredQuality,
            ExtraInputArgs: extraArgs.Length > 0 ? extraArgs : null
        );

        return await liveEncoder.StartAsync(request, ct);
    }

    /// <summary>
    /// Builds the disc-type-specific input URL. The title/playlist index is
    /// never encoded into the URL itself — ffmpeg's bluray: protocol has no
    /// query-string selector, and DVD/CD have none either. Every disc type
    /// selects its title via CLI flags instead, from
    /// <see cref="BuildExtraInputArgs"/>, mirroring the exact pattern
    /// <c>DiscRipper.cs</c> already uses for the same disc types.
    /// </summary>
    private static string BuildInputPath(DiscDrive drive, int titleIndex)
    {
        string trimmed = drive.Path.TrimEnd('\\', '/');
        return drive.DiscType switch
        {
            OpticalDiscType.BluRay => $"bluray:{trimmed}/",
            OpticalDiscType.Dvd => $"{trimmed}/",
            OpticalDiscType.Cd => drive.Path,
            _ => trimmed,
        };
    }

    /// <summary>
    /// ffmpeg input flags that must precede "-i" to select the right title —
    /// same per-type mapping as <c>DiscRipper.BuildRipArgs</c>.
    /// </summary>
    private static string[] BuildExtraInputArgs(DiscDrive drive, int titleIndex)
    {
        string index = titleIndex.ToString(CultureInfo.InvariantCulture);
        return drive.DiscType switch
        {
            OpticalDiscType.BluRay => ["-playlist", index],
            OpticalDiscType.Dvd => ["-f", "dvdvideo", "-title", index],
            OpticalDiscType.Cd => ["-f", "libcdio"],
            _ => [],
        };
    }
}
