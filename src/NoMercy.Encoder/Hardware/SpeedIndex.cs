namespace NoMercy.Encoder.Hardware;

using NoMercy.Encoder.Codecs;

public record SpeedKey(VideoCodecType Codec, string Encoder, int Width, string? DeviceName);

public record SpeedMeasurement(double Fps, double SpeedMultiplier, DateTime MeasuredAt);

public record SpeedIndex(Dictionary<SpeedKey, SpeedMeasurement> Measurements)
{
    public SpeedMeasurement? GetSpeed(
        VideoCodecType codec,
        string encoder,
        int width,
        string? deviceName
    )
    {
        SpeedKey key = new(codec, encoder, width, deviceName);
        return Measurements.GetValueOrDefault(key);
    }

    public double GetSpeedMultiplier(
        VideoCodecType codec,
        string encoder,
        int width,
        string? deviceName
    )
    {
        SpeedMeasurement? measurement = GetSpeed(codec, encoder, width, deviceName);
        return measurement?.SpeedMultiplier ?? 0;
    }
}
