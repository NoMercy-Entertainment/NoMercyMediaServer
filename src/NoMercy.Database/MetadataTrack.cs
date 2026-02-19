using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Database;

public class MetadataTrack: Timestamps
{
    [Column("Video")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _video { get; set; }

    [NotMapped]
    [JsonProperty("video", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IVideo? Video
    {
        get => _video != null
            ? JsonConvert.DeserializeObject<IVideo>(_video)
            : null;
        init => _video = value != null 
            ? JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            })
            : null;
    }

    [Column("Audio")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _audio { get; set; }

    [NotMapped]
    [JsonProperty("audio", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IAudio? Audio
    {
        get => _audio != null
            ? JsonConvert.DeserializeObject<IAudio>(_audio)
            : null;
        init => _audio = value != null 
            ? JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            })
            : null;
    }

    [Column("Subtitles")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitle { get; set; }

    [NotMapped]
    [JsonProperty("subtitle", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ISubtitle? Subtitle
    {
        get => _subtitle != null
            ? JsonConvert.DeserializeObject<ISubtitle>(_subtitle)
            : null;
        init => _subtitle = value != null 
            ? JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            })
            : null;
    }
}