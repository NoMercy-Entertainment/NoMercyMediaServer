
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), nameof(EpisodeId), IsUnique = true)]
[Index(nameof(CreditId))]
[Index(nameof(EpisodeId))]
[Index(nameof(PersonId))]
public class GuestStar
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("credit_id")] public string? CreditId { get; set; }

    [JsonProperty("episode_id")] public int EpisodeId { get; set; }
    public Episode Episode { get; set; } = null!;

    [JsonProperty("person_id")] public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public GuestStar()
    {
        //
    }
}
