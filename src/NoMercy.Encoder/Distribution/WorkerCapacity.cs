namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Snapshot of a worker's current throughput potential used by the
/// <see cref="IWorkerAssigner"/> to weight task distribution. Higher
/// <see cref="SpeedMultiplier"/> = faster box at this specific tier;
/// higher <see cref="AvailableSlots"/> = more room for parallel work
/// before the worker tips over.
/// </summary>
public record WorkerCapacity(string WorkerId, double SpeedMultiplier, int AvailableSlots);
