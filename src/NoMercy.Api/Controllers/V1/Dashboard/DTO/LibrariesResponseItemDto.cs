using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record LibrariesResponseItemDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("autoRefreshInterval")] public long AutoRefreshInterval { get; set; }
    [JsonProperty("chapterImages")] public long ChapterImages { get; set; }
    [JsonProperty("image")] public string? Image { get; set; }
    [JsonProperty("perfectSubtitleMatch")] public bool PerfectSubtitleMatch { get; set; }
    [JsonProperty("realtime")] public bool Realtime { get; set; }
    [JsonProperty("specialSeasonName")] public string? SpecialSeasonName { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }

    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("pagination")] public string Pagination { get; set; } = "auto";
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    [JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
    [JsonProperty("folder_library")] public FolderLibraryDto[] FolderLibrary { get; set; }
    [JsonProperty("subtitles")] public string[] Subtitles { get; set; }

    public LibrariesResponseItemDto(Library library)
    {
        Id = library.Id;
        AutoRefreshInterval = library.AutoRefreshInterval;
        Image = library.Image;
        PerfectSubtitleMatch = library.PerfectSubtitleMatch;
        Realtime = library.Realtime;
        SpecialSeasonName = library.SpecialSeasonName;
        Title = library.Title;
        Type = library.Type;
        Order = library.Order;
        CreatedAt = library.CreatedAt;
        Pagination = library.LibraryMovies.Count + library.LibraryTvs.Count > 300 ? "letter" : "auto";
        Link = library.LibraryMovies.Count + library.LibraryTvs.Count > 300
            ? new($"/{Id}/letter/A", UriKind.Relative)
            : new($"/{Id}", UriKind.Relative);

        Subtitles = library.LanguageLibraries
            .Select(languageLibrary => languageLibrary.Language.Iso6391)
            .ToArray();

        FolderLibrary = library.FolderLibraries
            .Select(folderLibrary => new FolderLibraryDto
            {
                FolderId = folderLibrary.FolderId,
                LibraryId = folderLibrary.LibraryId,
                Folder = new()
                {
                    Id = folderLibrary.Folder.Id,
                    Path = folderLibrary.Folder.Path,
                    EncoderProfiles = folderLibrary.Folder.EncoderProfileFolder
                        .Select(encoderProfileFolder => encoderProfileFolder.EncoderProfile)
                        .ToArray()
                }
            })
            .ToArray();
    }
}