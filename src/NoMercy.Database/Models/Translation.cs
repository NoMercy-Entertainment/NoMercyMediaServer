
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(TvId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(SeasonId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(EpisodeId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(MovieId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(CollectionId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(PersonId), nameof(Iso6391), nameof(Iso31661), IsUnique = true)]
[Index(nameof(ReleaseGroupId), nameof(Iso31661), IsUnique = true)]
[Index(nameof(ArtistId), nameof(Iso31661), IsUnique = true)]
[Index(nameof(AlbumId), nameof(Iso31661), IsUnique = true)]
[Index(nameof(GenreId), nameof(Iso6391), IsUnique = true)]
[Index(nameof(TvId))]
[Index(nameof(SeasonId))]
[Index(nameof(EpisodeId))]
[Index(nameof(MovieId))]
[Index(nameof(CollectionId))]
[Index(nameof(PersonId))]
[Index(nameof(ReleaseGroupId))]
[Index(nameof(ArtistId))]
[Index(nameof(AlbumId))]
[Index(nameof(GenreId))]
public class Translation : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; }
    [JsonProperty("iso_639_1")] public string? Iso6391 { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("english_name")] public string? EnglishName { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("homepage")] public string? Homepage { get; set; }
    [JsonProperty("biography")] public string? Biography { get; set; }

    [JsonProperty("tv_id")] public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("season_id")] public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("collection_id")] public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty("person_id")] public int? PersonId { get; set; }
    public Person? People { get; set; }

    [JsonProperty("release_group_id")] public Guid? ReleaseGroupId { get; set; }
    public ReleaseGroup? ReleaseGroup { get; set; }

    [JsonProperty("artist_id")] public Guid? ArtistId { get; set; }
    public Artist? Artist { get; set; }

    [JsonProperty("release_id")] public Guid? AlbumId { get; set; }
    public Album? Album { get; set; }

    [JsonProperty("genre_id")] public int? GenreId { get; set; }
    public Genre? Genre { get; set; }

    public Translation()
    {
    }
}
