
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[Index(nameof(Iso31661), IsUnique = true)]
[PrimaryKey(nameof(Id))]
public class Country
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }
    [Key] 
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string? EnglishName { get; set; }
    [JsonProperty("native_name")] public string? NativeName { get; set; }

    public Country()
    {
    }

    // public Country(Providers.TMDB.Models.Configuration.TmdbCountry tmdbCountry)
    // {
    //     Iso31661 = tmdbCountry.Iso31661;
    //     EnglishName = tmdbCountry.EnglishName;
    //     NativeName = tmdbCountry.NativeName;
    // }
}
