namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

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
    private readonly HlsVariantAnalyzer _analyzer = new(TestStorageFactory.CreateLocal());

    [Fact]
    public void MissingPlaylist_ReturnsZeros()
    {
        HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(
            "/nonexistent/path/playlist.m3u8"
        );

        metrics.PeakBandwidth.Should().Be(0);
        metrics.AverageBandwidth.Should().Be(0);
    }

    [Fact]
    public void EmptyPlaylist_ReturnsZeros()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hls-empty-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            string playlist = Path.Combine(dir, "empty.m3u8");
            File.WriteAllText(playlist, "#EXTM3U\n#EXT-X-VERSION:3\n");

            HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(playlist);

            metrics.PeakBandwidth.Should().Be(0);
            metrics.AverageBandwidth.Should().Be(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FullPlaylist_ComputesPeakAndAverage()
    {
        // 3 segments, 6s each, 1 MB / 2 MB / 1 MB.
        // Average: 4 MB * 8 bits / 18s = 1,777,777 bps
        // Peak: 2 MB * 8 / 6s = 2,666,666 bps
        string dir = Path.Combine(Path.GetTempPath(), $"hls-full-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            string playlist = Path.Combine(dir, "index.m3u8");
            WriteSegment(dir, "seg0.ts", sizeBytes: 1_000_000);
            WriteSegment(dir, "seg1.ts", sizeBytes: 2_000_000);
            WriteSegment(dir, "seg2.ts", sizeBytes: 1_000_000);

            File.WriteAllText(
                playlist,
                "#EXTM3U\n"
                    + "#EXT-X-VERSION:3\n"
                    + "#EXTINF:6.000,\n"
                    + "seg0.ts\n"
                    + "#EXTINF:6.000,\n"
                    + "seg1.ts\n"
                    + "#EXTINF:6.000,\n"
                    + "seg2.ts\n"
                    + "#EXT-X-ENDLIST\n"
            );

            HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(playlist);

            // Peak = slowest seg / duration = 2,666,666 bps approx.
            metrics.PeakBandwidth.Should().BeInRange(2_600_000, 2_700_000);
            // Average = 4 MB * 8 bits / 18s = 1,777,777 bps.
            metrics.AverageBandwidth.Should().BeInRange(1_750_000, 1_800_000);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MissingSegmentFile_IsSkipped()
    {
        // Playlist references seg1 which doesn't exist on disk. Analyzer
        // must not throw — it drops the missing segment and computes
        // metrics on what's actually there.
        string dir = Path.Combine(Path.GetTempPath(), $"hls-missing-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            string playlist = Path.Combine(dir, "index.m3u8");
            WriteSegment(dir, "seg0.ts", sizeBytes: 1_000_000);
            // seg1.ts intentionally missing.

            File.WriteAllText(
                playlist,
                "#EXTM3U\n"
                    + "#EXTINF:6.000,\n"
                    + "seg0.ts\n"
                    + "#EXTINF:6.000,\n"
                    + "seg1.ts\n"
                    + "#EXT-X-ENDLIST\n"
            );

            HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(playlist);

            // Only seg0 contributes.
            metrics.AverageBandwidth.Should().BeInRange(1_200_000, 1_500_000);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ZeroDurationSegment_IsSkipped()
    {
        // EXTINF:0, is invalid HLS — analyzer must skip instead of dividing
        // by zero and producing Infinity / NaN.
        string dir = Path.Combine(Path.GetTempPath(), $"hls-zero-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            string playlist = Path.Combine(dir, "index.m3u8");
            WriteSegment(dir, "seg0.ts", sizeBytes: 1_000_000);

            File.WriteAllText(
                playlist,
                "#EXTM3U\n" + "#EXTINF:0,\n" + "seg0.ts\n" + "#EXT-X-ENDLIST\n"
            );

            HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(playlist);

            metrics.PeakBandwidth.Should().Be(0);
            metrics.AverageBandwidth.Should().Be(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PeakIsAboveAverageWhenSegmentsVary()
    {
        // Small, medium, huge segment — peak must exceed average.
        string dir = Path.Combine(Path.GetTempPath(), $"hls-varied-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            string playlist = Path.Combine(dir, "index.m3u8");
            WriteSegment(dir, "seg0.ts", sizeBytes: 500_000);
            WriteSegment(dir, "seg1.ts", sizeBytes: 1_000_000);
            WriteSegment(dir, "seg2.ts", sizeBytes: 3_000_000);

            File.WriteAllText(
                playlist,
                "#EXTM3U\n"
                    + "#EXTINF:6,\n"
                    + "seg0.ts\n"
                    + "#EXTINF:6,\n"
                    + "seg1.ts\n"
                    + "#EXTINF:6,\n"
                    + "seg2.ts\n"
                    + "#EXT-X-ENDLIST\n"
            );

            HlsVariantAnalyzer.VariantMetrics metrics = _analyzer.Measure(playlist);

            metrics.PeakBandwidth.Should().BeGreaterThan(metrics.AverageBandwidth);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void WriteSegment(string dir, string name, int sizeBytes)
    {
        File.WriteAllBytes(Path.Combine(dir, name), new byte[sizeBytes]);
    }
}
