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
using System.Text;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Storage;

namespace NoMercy.Encoder.Output;

/// <summary>
/// Parses an HLS variant playlist (.m3u8) and its segment files
/// to calculate actual bitrate metrics for the master playlist.
/// </summary>
public class HlsVariantAnalyzer(IStorage storage) : IHlsVariantAnalyzer
{
    /// <summary>
    /// Measures the actual bandwidth of an HLS variant by reading segment file sizes
    /// and durations from the playlist. Returns peak and average bitrate in bits/sec.
    /// </summary>
    public VariantMetrics Measure(string playlistPath)
    {
        if (!storage.Exists(playlistPath))
        {
            // Failures here often mean BuildStage didn't write the playlist yet
            // or the path template resolved differently between plan and build.
            return new(0, 0);
        }

        string normalized = playlistPath.Replace('\\', '/');
        int lastSlash = normalized.LastIndexOf('/');
        string playlistDir = lastSlash > 0 ? playlistPath[..lastSlash] : string.Empty;
        string[] lines = Encoding.UTF8.GetString(storage.Read(playlistPath)).Split('\n');

        long totalBytes = 0;
        double totalDuration = 0;
        double peakBitrate = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("#EXTINF:"))
                continue;

            // Parse segment duration from #EXTINF:6.006000,
            string durationStr = lines[i][8..].TrimEnd(',');
            if (
                !double.TryParse(durationStr, CultureInfo.InvariantCulture, out double segDuration)
                || segDuration <= 0
            )
                continue;

            // Next non-comment line is the segment filename
            if (i + 1 >= lines.Length)
                break;

            string segmentFile = lines[i + 1].Trim();
            if (segmentFile.StartsWith('#'))
                continue;

            string segmentPath = string.IsNullOrEmpty(playlistDir)
                ? segmentFile
                : storage.CombinePath(playlistDir, segmentFile);
            if (!storage.Exists(segmentPath))
                continue;

            long segmentBytes = storage.Size(segmentPath);
            totalBytes += segmentBytes;
            totalDuration += segDuration;

            // Peak = highest single-segment bitrate (bits/sec)
            double segBitrate = segmentBytes * 8.0 / segDuration;
            if (segBitrate > peakBitrate)
                peakBitrate = segBitrate;
        }

        int averageBandwidth = totalDuration > 0 ? (int)(totalBytes * 8.0 / totalDuration) : 0;

        return new(PeakBandwidth: (int)peakBitrate, AverageBandwidth: averageBandwidth);
    }
}
