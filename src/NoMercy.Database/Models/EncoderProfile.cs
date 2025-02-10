using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class EncoderProfile : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Key] [JsonProperty("name")] public required string Name { get; set; }
    [JsonProperty("container")] public string? Container { get; set; }
    [JsonProperty("type")] public string? Param { get; set; }

    [Column("VideoProfile")]
    [JsonProperty("video_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _videoProfiles { get; set; } = string.Empty;

    [NotMapped]
    public IVideoProfile[] VideoProfiles
    {
        get => _videoProfiles != string.Empty
            ? JsonConvert.DeserializeObject<IVideoProfile[]>(_videoProfiles)!
            : [];
        set => _videoProfiles = JsonConvert.SerializeObject(value);
    }

    [Column("AudioProfile")]
    [JsonProperty("audio_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _audioProfiles { get; set; } = string.Empty;

    [NotMapped]
    public IAudioProfile[] AudioProfiles
    {
        get => _audioProfiles != string.Empty
            ? JsonConvert.DeserializeObject<IAudioProfile[]>(_audioProfiles)!
            : [];
        set => _audioProfiles = JsonConvert.SerializeObject(value);
    }

    [Column("SubtitleProfile")]
    [JsonProperty("subtitle_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _subtitleProfiles { get; set; } = string.Empty;

    [NotMapped]
    public ISubtitleProfile[] SubtitleProfiles
    {
        get => _subtitleProfiles != string.Empty
            ? JsonConvert.DeserializeObject<ISubtitleProfile[]>(_subtitleProfiles)!
            : [];
        set => _subtitleProfiles = JsonConvert.SerializeObject(value);
    }

    [JsonProperty("encoder_profile_folder")]
    public ICollection<EncoderProfileFolder> EncoderProfileFolder { get; set; } = [];
}
