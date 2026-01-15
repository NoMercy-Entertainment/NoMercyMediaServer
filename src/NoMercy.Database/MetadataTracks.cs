using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using NoMercy.Database.Models;

namespace NoMercy.Database;

public class MetadataTracks: Timestamps
{
    [Column("Video")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _video { get; set; }

    [NotMapped]
    [JsonProperty("video")]
    public List<IVideo>? Video
    {
        get => _video != null
            ? JsonConvert.DeserializeObject<List<IVideo>>(_video)
            : null;
        init => _video = JsonConvert.SerializeObject(value);
    }

    [Column("Audio")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _audio { get; set; }

    [NotMapped]
    [JsonProperty("audio")]
    public List<IAudio>? Audio
    {
        get => _audio != null
            ? JsonConvert.DeserializeObject<List<IAudio>>(_audio)
            : null;
        init => _audio = JsonConvert.SerializeObject(value);
    }

    [Column("Subtitles")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitles { get; set; }

    [NotMapped]
    [JsonProperty("subtitles")]
    public List<ISubtitle>? Subtitles
    {
        get => _subtitles != null
            ? JsonConvert.DeserializeObject<List<ISubtitle>>(_subtitles)
            : null;
        init => _subtitles = JsonConvert.SerializeObject(value);
    }
}