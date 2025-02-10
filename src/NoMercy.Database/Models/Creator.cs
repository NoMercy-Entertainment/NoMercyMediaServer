
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(PersonId), nameof(TvId))]
public class Creator
{
    [JsonProperty("person_id")] public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public Creator()
    {
        //
    }
}
