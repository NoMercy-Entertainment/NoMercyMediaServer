using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Bundle;

public record Reconstruction(
    [property: JsonProperty("version")] int Version,
    [property: JsonProperty("reconstruction_target_container")] string TargetContainer,
    [property: JsonProperty("source")] ReconstructionSource Source,
    [property: JsonProperty("tracks")] IReadOnlyList<ReconstructionTrack> Tracks,
    [property: JsonProperty("external_assets")] ReconstructionAssets ExternalAssets,
    [property: JsonProperty("reconstruction_command_template")] string CommandTemplate,
    [property: JsonProperty("lossy_warnings")] IReadOnlyList<string> LossyWarnings
);

public record ReconstructionSource(
    [property: JsonProperty("original_path")] string OriginalPath,
    [property: JsonProperty("original_filename")] string OriginalFilename,
    [property: JsonProperty("size_bytes")] long SizeBytes,
    // sha256 is left null at encode time — computing it would require a full
    // re-read of the source file. The reconstruction manifest documents the
    // shape so downstream consumers can populate it later.
    [property: JsonProperty("sha256")] string? Sha256,
    [property: JsonProperty("duration_seconds")] double DurationSeconds,
    [property: JsonProperty("container")] string Container,
    // Raw ffprobe JSON preserved as-is. Null when the encoder runs without an
    // attached analyzer result (e.g. unit tests using minimal stubs).
    [property: JsonProperty("ffprobe")] JObject? Ffprobe
);

public record ReconstructionTrack(
    [property: JsonProperty("kind")] string Kind,
    [property: JsonProperty("source_stream_index")] int SourceStreamIndex,
    [property: JsonProperty("source_codec")] string SourceCodec,
    [property: JsonProperty("policy")] string Policy,
    [property: JsonProperty("output_codec")] string? OutputCodec,
    [property: JsonProperty("bundle_files")] IReadOnlyList<string> BundleFiles,
    [property: JsonProperty("concat_method")] string ConcatMethod,
    [property: JsonProperty("metadata")] JObject? Metadata,
    [property: JsonProperty("fidelity")] string Fidelity,
    [property: JsonProperty("lossy_reason", NullValueHandling = NullValueHandling.Ignore)]
        string? LossyReason
);

public record ReconstructionAssets(
    [property: JsonProperty("fonts")] IReadOnlyList<string> Fonts,
    [property: JsonProperty("cover_art")] IReadOnlyList<string> CoverArt,
    [property: JsonProperty("chapters")] IReadOnlyList<ReconstructionChapter> Chapters,
    [property: JsonProperty("attachments")] IReadOnlyList<string> Attachments
);

public record ReconstructionChapter(
    [property: JsonProperty("start")] double Start,
    [property: JsonProperty("end")] double End,
    [property: JsonProperty("title")] string? Title
);
