namespace NoMercy.Encoder.Naming;

/// <summary>
/// Resolved output paths for one (media, preset) combination. All paths are
/// relative to the media item's library folder unless flagged absolute.
/// </summary>
public record BundleLayout(
    string MediaKey,
    string PresetSlug,
    bool IsSingleFile,
    string BundleDirectory, // "encodes/{presetSlug}" or "" for single-file
    string MasterPlaylistName, // "{mediaKey}_master.m3u8" or "" for single-file
    string ManifestPath, // "{bundleDir}/manifest.json" or "{singleFile}.manifest.json"
    string ReconstructionPath, // "{bundleDir}/reconstruction.json" or "{singleFile}.reconstruction.json"
    string SingleFileName // "{Title}.NoMercy.{ext}" or "" for HLS
);
