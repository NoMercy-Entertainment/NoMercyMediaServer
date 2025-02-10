
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), IsUnique = true)]
[Index(nameof(GuestStarId), IsUnique = true)]
public class Role
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("character")] public string? Character { get; set; }
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
    [JsonProperty("order")] public int? Order { get; set; } = 9999;

    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    public Cast? Cast { get; set; }

    [JsonProperty("guest_star_id")] public int? GuestStarId { get; set; }
    public GuestStar? GuestStar { get; set; }

    public Role()
    {
        //
    }

    // public Role(TmdbAggregatedCreditRole role)
    // {
    //     Character = role.Character;
    //     EpisodeCount = role.EpisodeCount;
    //     Order = role.Order;
    //     CreditId = role.CreditId;
    // }
    //
    // public Role(Providers.TMDB.Models.Shared.TmdbCast tmdbCast)
    // {
    //     Character = tmdbCast.Character;
    //     CreditId = tmdbCast.CreditId;
    //     Order = tmdbCast.Order;
    //     EpisodeCount = 0;
    // }
    //
    // public Role(Providers.TMDB.Models.Shared.TmdbGuestStar tmdbGuest)
    // {
    //     Character = tmdbGuest.CharacterName;
    //     CreditId = tmdbGuest.CreditId;
    //     Order = tmdbGuest.Order;
    //     EpisodeCount = 0;
    // }
}
