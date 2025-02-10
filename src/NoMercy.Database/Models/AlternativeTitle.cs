

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Title), nameof(TvId), IsUnique = true)]
[Index(nameof(Title), nameof(MovieId), IsUnique = true)]
public class AlternativeTitle
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }
    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }

    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    [JsonProperty("tv_id")] public int? TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public AlternativeTitle()
    {
        //
    }
}
