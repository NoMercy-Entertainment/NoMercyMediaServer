
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(TvId))]
public class Season : ColorPalettes
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")] public string? Title { get; set; }
    [JsonProperty("air_date")] public DateTime? AirDate { get; set; }
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster_path")] public string? Poster { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    [JsonProperty("episodes")] public ICollection<Episode> Episodes { get; set; } = [];
    [JsonProperty("casts")] public ICollection<Cast> Cast { get; set; } = [];
    [JsonProperty("crews")] public ICollection<Crew> Crew { get; set; } = []; 
    [JsonProperty("medias")] public ICollection<Media> Medias { get; set; } = [];
    [JsonProperty("images")] public ICollection<Image> Images { get; set; } = [];
    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];

    public Season()
    {
        //
    }
}
