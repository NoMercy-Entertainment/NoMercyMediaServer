namespace NoMercy.Encoder.Output;

using System.Globalization;
using NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Parses an HLS variant playlist (.m3u8) and its segment files
/// to calculate actual bitrate metrics for the master playlist.
/// </summary>
public class HlsVariantAnalyzer : IHlsVariantAnalyzer
{
    public record VariantMetrics(int PeakBandwidth, int AverageBandwidth);

    /// <summary>
    /// Measures the actual bandwidth of an HLS variant by reading segment file sizes
    /// and durations from the playlist. Returns peak and average bitrate in bits/sec.
    /// </summary>
    public VariantMetrics Measure(string playlistPath)
    {
        if (!File.Exists(playlistPath))
            return new(0, 0);

        string playlistDir = Path.GetDirectoryName(playlistPath) ?? ".";
        string[] lines = File.ReadAllLines(playlistPath);

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

            string segmentPath = Path.Combine(playlistDir, segmentFile);
            if (!File.Exists(segmentPath))
                continue;

            long segmentBytes = new FileInfo(segmentPath).Length;
            totalBytes += segmentBytes;
            totalDuration += segDuration;

            // Peak = highest single-segment bitrate (bits/sec)
            double segBitrate = segmentBytes * 8.0 / segDuration;
            if (segBitrate > peakBitrate)
                peakBitrate = segBitrate;
        }

        int averageBandwidth = totalDuration > 0 ? (int)(totalBytes * 8.0 / totalDuration) : 0;

        return new(
            PeakBandwidth: (int)peakBitrate,
            AverageBandwidth: averageBandwidth
        );
    }
}
