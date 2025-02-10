
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(MediaId), nameof(TvFromId), IsUnique = true)]
[Index(nameof(MediaId), nameof(MovieFromId), IsUnique = true)]
public class Recommendation : ColorPalettes
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("mediaId")] public int MediaId { get; set; }

    [ForeignKey("TvFromId")] public int? TvFromId { get; set; }
    public Tv? TvFrom { get; set; }

    [ForeignKey("TvToId")] public int? TvToId { get; set; }
    public Tv? TvTo { get; set; }

    [ForeignKey("RecommendationFrom")] public int? MovieFromId { get; set; }
    public Movie? MovieFrom { get; set; }

    [ForeignKey("RecommendationTo")] public int? MovieToId { get; set; }
    public Movie? MovieTo { get; set; }

    public Recommendation()
    {
        //
    }
}
