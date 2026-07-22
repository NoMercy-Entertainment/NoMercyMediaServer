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

using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// HlsVariantAnalyzer reads a variant's .m3u8 and segment files to
/// compute actual peak/average bandwidth for the master playlist.
/// Wrong numbers here mean the master playlist advertises BANDWIDTH
/// values that don't match reality — players pick the wrong variant
/// for their link, causing rebuffer loops or picking a low tier on a
/// gigabit connection.
///
/// Edge cases pinned:
///   - Missing playlist file returns (0, 0), not throw.
///   - Missing segment file mid-playlist is skipped, not fatal.
///   - Zero-duration EXTINF is skipped (division-by-zero guard).
///   - Peak is the slowest segment (highest bitrate single-segment).
///   - Average is total_bytes / total_duration.
/// </summary>
public class HlsVariantAnalyzerTests
{
    private readonly HlsVariantAnalyzer _analyzer = new(storage: TestStorageFactory.CreateLocal());

    [Fact]
    public void MissingPlaylist_ReturnsZeros()
    {
        VariantMetrics metrics = _analyzer.Measure(playlistPath: "/nonexistent/path/playlist.m3u8");

        metrics.PeakBandwidth.Should().Be(expected: 0);
        metrics.AverageBandwidth.Should().Be(expected: 0);
    }

    [Fact]
    public void EmptyPlaylist_ReturnsZeros()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"hls-empty-{Guid.NewGuid()}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string playlist = Path.Combine(path1: dir, path2: "empty.m3u8");
            File.WriteAllText(path: playlist, contents: "#EXTM3U\n#EXT-X-VERSION:3\n");

            VariantMetrics metrics = _analyzer.Measure(playlistPath: playlist);

            metrics.PeakBandwidth.Should().Be(expected: 0);
            metrics.AverageBandwidth.Should().Be(expected: 0);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void FullPlaylist_ComputesPeakAndAverage()
    {
        // 3 segments, 6s each, 1 MB / 2 MB / 1 MB.
        // Average: 4 MB * 8 bits / 18s = 1,777,777 bps
        // Peak: 2 MB * 8 / 6s = 2,666,666 bps
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"hls-full-{Guid.NewGuid()}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string playlist = Path.Combine(path1: dir, path2: "index.m3u8");
            WriteSegment(dir: dir, name: "seg0.ts", sizeBytes: 1_000_000);
            WriteSegment(dir: dir, name: "seg1.ts", sizeBytes: 2_000_000);
            WriteSegment(dir: dir, name: "seg2.ts", sizeBytes: 1_000_000);

            File.WriteAllText(
                path: playlist,
                contents: "#EXTM3U\n"
                          + "#EXT-X-VERSION:3\n"
                          + "#EXTINF:6.000,\n"
                          + "seg0.ts\n"
                          + "#EXTINF:6.000,\n"
                          + "seg1.ts\n"
                          + "#EXTINF:6.000,\n"
                          + "seg2.ts\n"
                          + "#EXT-X-ENDLIST\n"
            );

            VariantMetrics metrics = _analyzer.Measure(playlistPath: playlist);

            // Peak = slowest seg / duration = 2,666,666 bps approx.
            metrics.PeakBandwidth.Should().BeInRange(minimumValue: 2_600_000, maximumValue: 2_700_000);
            // Average = 4 MB * 8 bits / 18s = 1,777,777 bps.
            metrics.AverageBandwidth.Should().BeInRange(minimumValue: 1_750_000, maximumValue: 1_800_000);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void MissingSegmentFile_IsSkipped()
    {
        // Playlist references seg1 which doesn't exist on disk. Analyzer
        // must not throw — it drops the missing segment and computes
        // metrics on what's actually there.
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"hls-missing-{Guid.NewGuid()}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string playlist = Path.Combine(path1: dir, path2: "index.m3u8");
            WriteSegment(dir: dir, name: "seg0.ts", sizeBytes: 1_000_000);
            // seg1.ts intentionally missing.

            File.WriteAllText(
                path: playlist,
                contents: "#EXTM3U\n"
                          + "#EXTINF:6.000,\n"
                          + "seg0.ts\n"
                          + "#EXTINF:6.000,\n"
                          + "seg1.ts\n"
                          + "#EXT-X-ENDLIST\n"
            );

            VariantMetrics metrics = _analyzer.Measure(playlistPath: playlist);

            // Only seg0 contributes.
            metrics.AverageBandwidth.Should().BeInRange(minimumValue: 1_200_000, maximumValue: 1_500_000);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void ZeroDurationSegment_IsSkipped()
    {
        // EXTINF:0, is invalid HLS — analyzer must skip instead of dividing
        // by zero and producing Infinity / NaN.
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"hls-zero-{Guid.NewGuid()}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string playlist = Path.Combine(path1: dir, path2: "index.m3u8");
            WriteSegment(dir: dir, name: "seg0.ts", sizeBytes: 1_000_000);

            File.WriteAllText(
                path: playlist,
                contents: "#EXTM3U\n" + "#EXTINF:0,\n" + "seg0.ts\n" + "#EXT-X-ENDLIST\n"
            );

            VariantMetrics metrics = _analyzer.Measure(playlistPath: playlist);

            metrics.PeakBandwidth.Should().Be(expected: 0);
            metrics.AverageBandwidth.Should().Be(expected: 0);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void PeakIsAboveAverageWhenSegmentsVary()
    {
        // Small, medium, huge segment — peak must exceed average.
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"hls-varied-{Guid.NewGuid()}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string playlist = Path.Combine(path1: dir, path2: "index.m3u8");
            WriteSegment(dir: dir, name: "seg0.ts", sizeBytes: 500_000);
            WriteSegment(dir: dir, name: "seg1.ts", sizeBytes: 1_000_000);
            WriteSegment(dir: dir, name: "seg2.ts", sizeBytes: 3_000_000);

            File.WriteAllText(
                path: playlist,
                contents: "#EXTM3U\n"
                          + "#EXTINF:6,\n"
                          + "seg0.ts\n"
                          + "#EXTINF:6,\n"
                          + "seg1.ts\n"
                          + "#EXTINF:6,\n"
                          + "seg2.ts\n"
                          + "#EXT-X-ENDLIST\n"
            );

            VariantMetrics metrics = _analyzer.Measure(playlistPath: playlist);

            metrics.PeakBandwidth.Should().BeGreaterThan(expected: metrics.AverageBandwidth);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    private static void WriteSegment(string dir, string name, int sizeBytes)
    {
        File.WriteAllBytes(path: Path.Combine(path1: dir, path2: name), bytes: new byte[sizeBytes]);
    }
}
