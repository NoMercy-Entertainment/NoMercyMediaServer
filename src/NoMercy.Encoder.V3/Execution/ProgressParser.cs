namespace NoMercy.Encoder.V3.Execution;

using System.Globalization;

public record FfmpegProgressSnapshot(
    int Frame,
    double Fps,
    double? BitrateKbps,
    long TotalSizeBytes,
    TimeSpan OutTime,
    double Speed,
    bool IsEnd
);

public class ProgressParser
{
    private int _frame;
    private double _fps;
    private double? _bitrateKbps;
    private long _totalSizeBytes;
    private TimeSpan _outTime;
    private double _speed;

    /// Feeds a single line from FFmpeg stdout.
    /// Returns a snapshot when a progress=continue or progress=end line is received.
    /// Returns null for intermediate key=value lines.
    public FfmpegProgressSnapshot? FeedLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        int eqIdx = line.IndexOf('=');
        if (eqIdx < 0)
            return null;

        string key = line[..eqIdx].Trim();
        string value = line[(eqIdx + 1)..].Trim();

        switch (key)
        {
            case "frame":
                int.TryParse(value, out _frame);
                break;
            case "fps":
                double.TryParse(value, CultureInfo.InvariantCulture, out _fps);
                break;
            case "bitrate":
                // Format: "8234.5kbits/s" or "N/A"
                _bitrateKbps = ParseBitrate(value);
                break;
            case "total_size":
                long.TryParse(value, out _totalSizeBytes);
                break;
            case "out_time_us":
                if (long.TryParse(value, out long us))
                    _outTime = TimeSpan.FromMicroseconds(us);
                break;
            case "speed":
                // Format: "2.50x" or "N/A"
                _speed = ParseSpeed(value);
                break;
            case "progress":
                bool isEnd = value == "end";
                FfmpegProgressSnapshot snapshot = new(
                    Frame: _frame,
                    Fps: _fps,
                    BitrateKbps: _bitrateKbps,
                    TotalSizeBytes: _totalSizeBytes,
                    OutTime: _outTime,
                    Speed: _speed,
                    IsEnd: isEnd
                );
                return snapshot;
        }

        return null;
    }

    private static double? ParseBitrate(string value)
    {
        if (value is "N/A")
            return null;

        // "8234.5kbits/s"
        string numPart = value.Replace("kbits/s", "").Trim();
        return double.TryParse(numPart, CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }

    private static double ParseSpeed(string value)
    {
        if (value is "N/A")
            return 0;

        // "2.50x"
        string numPart = value.Replace("x", "").Trim();
        return double.TryParse(numPart, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0;
    }
}
