namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Describes a bundle directory that is no longer backed by a live preset
/// row (or has a structural anomaly) and should be reviewed for purge.
/// </summary>
public record BundleOrphan(
    /// <summary>Bundle directory path relative to the library root.</summary>
    string Path,
    string PresetSlug,
    string PresetId,
    /// <summary>
    /// Why the bundle is considered orphaned:
    /// "preset deleted" | "extra-files" | "missing-files" | "duplicate-manifest"
    /// </summary>
    string Reason
);
