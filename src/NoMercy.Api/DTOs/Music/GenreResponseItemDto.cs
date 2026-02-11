using Newtonsoft.Json;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public record GenreResponseItemDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("tracks")] public IEnumerable<GenreTrackDto> Tracks { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    public GenreResponseItemDto(MusicGenre genre, string? country = "US")
    {
        Id = genre.Id;
        Name = genre.Name.ToTitleCase();
        Link = new($"/music/genres/{Id}", UriKind.Relative);
        Type = "genre";
        Tracks = genre.MusicGenreTracks
            .Select(genreTrack => new GenreTrackDto(genreTrack, country!))
            .OrderBy(genreTrack => genreTrack.Disc)
            .ThenBy(genreTrack => genreTrack.Track);
    }
}