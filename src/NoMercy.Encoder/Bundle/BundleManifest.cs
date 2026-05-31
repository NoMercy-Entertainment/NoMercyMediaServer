using Newtonsoft.Json;

namespace NoMercy.Encoder.Bundle;

public record BundleManifest(
    [property: JsonProperty("version")] int Version,
    [property: JsonProperty("encoder_version")] string EncoderVersion,
    [property: JsonProperty("preset_id")] string PresetId,
    [property: JsonProperty("preset_name")] string PresetName,
    [property: JsonProperty("preset_slug")] string PresetSlug,
    [property: JsonProperty("media_type")] string MediaType, // "movie" | "episode" | "track"
    [property: JsonProperty("media_id")] long MediaId,
    [property: JsonProperty("media_external_id")] string? MediaExternalId,
    [property: JsonProperty("media_folder")] string MediaFolder,
    [property: JsonProperty("container")] string Container,
    [property: JsonProperty("created_at")] DateTime CreatedAt,
    [property: JsonProperty("completed_at")] DateTime? CompletedAt,
    [property: JsonProperty("media_key")] string MediaKey,
    [property: JsonProperty("files")] IReadOnlyList<string> Files
);
