using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(nameof(NetworkId), nameof(TvId))]
[Index(nameof(NetworkId), nameof(TvId), IsUnique = true)]
public class NetworkTv : Timestamps
{
    [JsonProperty("network_id")] public int NetworkId { get; set; }
    [JsonProperty("network")] public Network Network { get; set; } = null!;
    [JsonProperty("tv_id")] public int TvId { get; set; }
    [JsonProperty("tv")] public Tv Tv { get; set; } = null!;
}