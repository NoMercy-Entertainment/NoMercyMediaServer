using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Music;

public record ReleaseGroupDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("origin")] public Guid Origin { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    public ReleaseGroupDto(AlbumReleaseGroup artistReleaseGroup, string country)
    {
        string? description = artistReleaseGroup.ReleaseGroup.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Description;

        Description = !string.IsNullOrEmpty(description)
            ? description
            : artistReleaseGroup.ReleaseGroup.Description;

        Id = artistReleaseGroup.ReleaseGroupId;
        Title = artistReleaseGroup.ReleaseGroup.Title;
        Cover = artistReleaseGroup.ReleaseGroup.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        ColorPalette = artistReleaseGroup.ReleaseGroup.ColorPalette;
        LibraryId = artistReleaseGroup.ReleaseGroup.LibraryId;
        Origin = Info.DeviceId;
        Type = "release_groups";
        Year = artistReleaseGroup.ReleaseGroup.Year;
        Link = new($"/music/release_groups/{Id}", UriKind.Relative);
    }
}