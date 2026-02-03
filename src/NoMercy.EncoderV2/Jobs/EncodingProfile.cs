using Newtonsoft.Json;

namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Encoding profile configuration
/// TODO: Migrate from NoMercy.EncoderV2.Profiles once that module is mature
/// </summary>
[JsonObject(ItemTypeNameHandling = TypeNameHandling.Auto)]
public class EncodingProfile
{
    [JsonProperty("profile_id")]
    public string ProfileId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("purpose")]
    public string Purpose { get; set; } = "general";

    [JsonProperty("video_profile")]
    public VideoProfileConfig? VideoProfile { get; set; }

    [JsonProperty("audio_profile")]
    public AudioProfileConfig? AudioProfile { get; set; }

    [JsonProperty("subtitle_profile")]
    public SubtitleProfileConfig? SubtitleProfile { get; set; }

    [JsonProperty("container")]
    public string Container { get; set; } = "mp4";

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Video encoding configuration
/// </summary>
public class VideoProfileConfig
{
    [JsonProperty("codec")]
    public string Codec { get; set; } = "h264";

    [JsonProperty("bitrate")]
    public int Bitrate { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("framerate")]
    public double Framerate { get; set; }

    [JsonProperty("preset")]
    public string? Preset { get; set; } = "medium";

    [JsonProperty("profile")]
    public string? Profile { get; set; }

    [JsonProperty("crf")]
    public int Crf { get; set; }

    [JsonProperty("tune")]
    public string? Tune { get; set; }

    [JsonProperty("pixel_format")]
    public string? PixelFormat { get; set; }

    [JsonProperty("colorspace")]
    public string? ColorSpace { get; set; }

    [JsonProperty("keyframe_interval")]
    public int KeyframeInterval { get; set; }

    [JsonProperty("convert_hdr_to_sdr")]
    public bool ConvertHdrToSdr { get; set; }

    [JsonProperty("custom_options")]
    public Dictionary<string, dynamic>? CustomOptions { get; set; }

    [JsonProperty("custom_arguments")]
    public string? CustomArguments { get; set; }
}

/// <summary>
/// Audio encoding configuration
/// </summary>
public class AudioProfileConfig
{
    [JsonProperty("codec")]
    public string Codec { get; set; } = "aac";

    [JsonProperty("bitrate")]
    public int Bitrate { get; set; }

    [JsonProperty("channels")]
    public int Channels { get; set; }

    [JsonProperty("sample_rate")]
    public int SampleRate { get; set; }

    [JsonProperty("allowed_languages")]
    public List<string> AllowedLanguages { get; set; } = [];

    [JsonProperty("custom_options")]
    public Dictionary<string, dynamic>? CustomOptions { get; set; }

    [JsonProperty("custom_arguments")]
    public string? CustomArguments { get; set; }
}

/// <summary>
/// Subtitle encoding configuration
/// </summary>
public class SubtitleProfileConfig
{
    [JsonProperty("codec")]
    public string Codec { get; set; } = "webvtt";

    [JsonProperty("languages")]
    public List<string> Languages { get; set; } = [];

    [JsonProperty("allowed_languages")]
    public List<string> AllowedLanguages { get; set; } = [];

    [JsonProperty("custom_options")]
    public Dictionary<string, dynamic>? CustomOptions { get; set; }

    [JsonProperty("custom_arguments")]
    public string? CustomArguments { get; set; }
}
