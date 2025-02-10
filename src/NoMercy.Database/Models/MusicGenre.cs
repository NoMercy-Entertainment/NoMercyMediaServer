
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class MusicGenre
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    public MusicGenre()
    {
        //
    }

    // public MusicGenre(Providers.MusicBrainz.Models.MusicBrainzGenre musicBrainzGenre)
    // {
    //     Id = musicBrainzGenre.Id;
    //     Name = musicBrainzGenre.Name;
    // }
}
