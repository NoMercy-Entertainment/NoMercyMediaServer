namespace NoMercy.Encoder.BuildingBlocks.Drm;

public record DrmArtifact(
    string KeyInfoFilePath,
    string KeyFilePath,
    string KeyUri,
    byte[] Key,
    byte[] Iv
);

public interface IDrmProcessor
{
    /// <summary>
    /// Which <see cref="DrmMethod"/> this processor handles. The planner
    /// resolves the matching processor for a profile's <see cref="DrmConfig"/>.
    /// </summary>
    DrmMethod Method { get; }

    /// <summary>
    /// Prepares DRM artifacts (key file, key info file, IV) inside
    /// <paramref name="outputDirectory"/> so ffmpeg can encrypt segments
    /// inline. Returns the artifact paths for the strategy to wire into
    /// the ffmpeg command.
    /// </summary>
    Task<DrmArtifact> PrepareAsync(string outputDirectory, DrmConfig config, CancellationToken ct);
}
