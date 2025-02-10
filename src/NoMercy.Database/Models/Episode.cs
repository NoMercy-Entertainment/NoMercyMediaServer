
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(TvId))]
[Index(nameof(SeasonId))]
public class Episode : ColorPalettes
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("air_date")] public DateTime? AirDate { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("production_code")] public string? ProductionCode { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }
    [JsonProperty("still")] public string? Still { get; set; }
    [JsonProperty("tvdb_id")] public int? TvdbId { get; set; }
    [JsonProperty("vote_average")] public float? VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int? VoteCount { get; set; }

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    [JsonProperty("season_id")] public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    [JsonProperty("casts")] public ICollection<Cast> Cast { get; set; } = [];
    [JsonProperty("crews")] public ICollection<Crew> Crew { get; set; } = [];
    [JsonProperty("special_items")] public ICollection<SpecialItem> SpecialItems { get; set; } = [];
    [JsonProperty("video_files")] public ICollection<VideoFile> VideoFiles { get; set; } = [];
    [JsonProperty("medias")] public ICollection<Media> Media { get; set; } = [];
    [JsonProperty("images")] public ICollection<Image> Images { get; set; } = [];
    [JsonProperty("guest_stars")] public ICollection<GuestStar> GuestStars { get; set; } = [];
    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];

    public Episode()
    {
    }

    public string CreateFolderName()
    {
        return "/" + string
            .Concat(
                Tv.Title.CleanFileName(),
                ".S", SeasonNumber.ToString("00"),
                "E", EpisodeNumber.ToString("00")
           ).CleanFileName();
    }

    public string CreateTitle()
    {
        return string. Concat(
            Tv.Title,
            " S", SeasonNumber.ToString("00"),
            "E", EpisodeNumber.ToString("00"),
            " ", Title,
            " NoMercy");
    }

    public string CreateFileName()
    {
        return string.Concat(
            Tv.Title.CleanFileName(),
            ".S", SeasonNumber.ToString("00"),
            "E", EpisodeNumber.ToString("00"),
            ".", Title.CleanFileName(),
            ".NoMercy"
        );
    }
}
