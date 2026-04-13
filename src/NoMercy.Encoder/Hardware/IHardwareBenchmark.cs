namespace NoMercy.Encoder.Hardware;

public interface IHardwareBenchmark
{
    SpeedIndex GetCachedIndex();
    Task<SpeedIndex> CalibrateAsync(CancellationToken ct);
    bool NeedsRecalibration();
}
