namespace NoMercy.Encoder.V3.Hardware;

public interface IHardwareBenchmark
{
    SpeedIndex GetCachedIndex();
    Task<SpeedIndex> CalibrateAsync(CancellationToken ct);
    bool NeedsRecalibration();
}
