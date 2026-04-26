namespace NoMercy.Encoder.Hardware;

public interface ISpeedIndexStore
{
    SpeedIndex? Load();
    void Save(SpeedIndex index);
    DateTime? LastCalibratedAt { get; }

    /// <summary>
    /// Schema version of the most recently loaded cache file. Null when no
    /// cache has been loaded yet. Compared against the current
    /// <see cref="HardwareBenchmark.BenchmarkSchemaVersion"/> in
    /// <see cref="HardwareBenchmark.NeedsRecalibration"/> so a code change
    /// to probe length, tier list, or candidate enumeration auto-invalidates
    /// stale cached numbers without waiting for the 30-day grace window.
    /// </summary>
    int? LoadedSchemaVersion { get; }
}
