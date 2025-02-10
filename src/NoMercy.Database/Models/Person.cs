
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Person : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("also_known_as")] public string? AlsoKnownAs { get; set; }
    [JsonProperty("biography")] public string? Biography { get; set; }
    [JsonProperty("birthday")] public DateTime? BirthDay { get; set; }
    [JsonProperty("deathday")] public DateTime? DeathDay { get; set; }
    [JsonProperty("homepage")] public string? Homepage { get; set; }
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("known_for_department")] public string? KnownForDepartment { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("place_of_birth")] public string? PlaceOfBirth { get; set; }
    [JsonProperty("popularity")] public double Popularity { get; set; }
    [JsonProperty("profile")] public string? Profile { get; set; }
    [JsonProperty("title_sort")] public string TitleSort { get; set; } = string.Empty;

    [JsonProperty("casts")] public ICollection<Cast> Casts { get; set; } = [];
    [JsonProperty("crews")] public ICollection<Crew> Crews { get; set; } = [];
    [JsonProperty("images")] public ICollection<Image> Images { get; set; } = [];
    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];

    [Column("Gender")]
    [JsonProperty("gender")]
    [System.Text.Json.Serialization.JsonIgnore]
    public TmdbGender TmdbGender { get; set; }

    [NotMapped]
    [JsonProperty("Gender")]
    public string Gender
    {
        get => TmdbGender.ToString();
        set => TmdbGender = Enum.Parse<TmdbGender>(value);
    }

    [Column("ExternalIds")]
    [JsonProperty("external_ids")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _externalIds { get; set; }

    [NotMapped]
    public TmdbPersonExternalIds? ExternalIds
    {
        get => _externalIds is null ? null : JsonConvert.DeserializeObject<TmdbPersonExternalIds>(_externalIds);
        set => _externalIds = JsonConvert.SerializeObject(value);
    }

    public Person()
    {
        //
    }
}
