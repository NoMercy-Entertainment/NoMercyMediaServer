namespace NoMercy.Encoder.Bundle;

public interface IBundleGarbageCollector
{
    /// <summary>
    /// Walks <paramref name="libraryRoot"/> for <c>encodes/*/manifest.json</c>
    /// files, reconciles each manifest against DB preset rows and on-disk files,
    /// and returns orphan bundles that should be reviewed for purge.
    /// </summary>
    Task<IReadOnlyList<BundleOrphan>> SweepAsync(string libraryRoot, CancellationToken ct);
}
