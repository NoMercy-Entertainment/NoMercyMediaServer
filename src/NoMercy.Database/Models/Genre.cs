
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Genre
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    public ICollection<GenreMovie> GenreMovies { get; set; } = [];
    public ICollection<GenreTv> GenreTvShows { get; set; } = [];

    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];

    public Genre()
    {
        //
    }
}
