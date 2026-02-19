using Newtonsoft.Json;

namespace NoMercy.Service.Seeds.Dto;

public class LibrarySeedDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("image")] public string Image { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("order")] public int Order { get; set; } = 99;
    [JsonProperty("specialSeasonName")] public string SpecialSeasonName { get; set; } = string.Empty;
    [JsonProperty("realtime")] public bool Realtime { get; set; }
    [JsonProperty("autoRefreshInterval")] public int AutoRefreshInterval { get; set; }
    [JsonProperty("chapterImages")] public bool ChapterImages { get; set; }

    [JsonProperty("extractChaptersDuring")]
    public bool ExtractChaptersDuring { get; set; }

    [JsonProperty("extractChapters")] public bool ExtractChapters { get; set; }
    [JsonProperty("perfectSubtitleMatch")] public bool PerfectSubtitleMatch { get; set; }
    [JsonProperty("folders")] public FolderSeedDto[] Folders { get; set; } = [];
}