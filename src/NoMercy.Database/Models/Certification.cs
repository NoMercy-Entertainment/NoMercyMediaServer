
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Iso31661), nameof(Rating), IsUnique = true)]
public class Certification
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; } = string.Empty;
    [JsonProperty("rating")] public string? Rating { get; set; } = string.Empty;
    [JsonProperty("meaning")] public string Meaning { get; set; } = string.Empty;
    [JsonProperty("order")] public int Order { get; set; }

    public Certification()
    {
        //
    }

    // public Certification(string? country, TmdbTvShowCertification certification)
    // {
    //     Iso31661 = country;
    //     Rating = certification.Rating;
    //     Meaning = certification.Meaning;
    //     Order = certification.Order;
    // }
    //
    // public Certification(string? country, TmdbMovieCertification certification)
    // {
    //     Iso31661 = country;
    //     Rating = certification.Rating;
    //     Meaning = certification.Meaning;
    //     Order = certification.Order;
    // }
}
