using Newtonsoft.Json;

namespace NoMercy.Data.Repositories;

public class LibraryUpdateRequest
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("image")] public string? Image { get; set; }
    [JsonProperty("autoRefreshInterval")] public bool PerfectSubtitleMatch { get; set; } = true;
    [JsonProperty("realtime")] public bool Realtime { get; set; }
    [JsonProperty("specialSeasonName")] public string SpecialSeasonName { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("folder_library")] public FolderLibraryDto[] FolderLibrary { get; set; } = [];
    [JsonProperty("subtitles")] public string[] Subtitles { get; set; } = [];
}