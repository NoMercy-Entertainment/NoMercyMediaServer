using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoMercy.Database;
public class VideoTracks: Timestamps
{
    [Column("Track")]
    [StringLength(1024)]
    [JsonProperty("tracks")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _tracks { get; set; } = string.Empty;

    [NotMapped]
    public IVideoTrack[] Tracks
    {
        get => (_tracks != string.Empty
            ? JsonConvert.DeserializeObject<IVideoTrack[]>(_tracks)
            : []) ?? [];
        set => _tracks = JsonConvert.SerializeObject(value);
    }
}