using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Hardware;

public interface IBenchmarkJobTracker
{
    /// <summary>
    /// Starts a new benchmark job and returns its initial status immediately.
    /// The benchmark runs asynchronously in the background.
    /// </summary>
    BenchmarkJobStatus Start(IReadOnlyList<VideoCodecType> codecs, IReadOnlyList<int> resolutions);

    /// <summary>Returns the current status of a job, or null if the id is unknown.</summary>
    BenchmarkJobStatus? Get(string jobId);

    /// <summary>Returns a snapshot of all known jobs.</summary>
    IReadOnlyList<BenchmarkJobStatus> List();
}
