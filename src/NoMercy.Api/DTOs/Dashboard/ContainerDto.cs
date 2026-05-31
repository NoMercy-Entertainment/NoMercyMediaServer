using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ContainerDto
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("default")]
    public bool IsDefault { get; set; }

    [JsonProperty("available_video_codecs")]
    public VideoCodecDto[] AvailableVideoCodecs { get; set; } = [];

    [JsonProperty("available_audio_codecs")]
    public AudioCodecDto[] AvailableAudioCodecs { get; set; } = [];

    [JsonProperty("available_subtitle_codecs")]
    public SubtitleCodecDto[] AvailableSubtitleCodecs { get; set; } = [];

    [JsonProperty("available_resolutions")]
    public VideoQualityDto[] AvailableVideoSizes { get; set; } = [];
}

public class CodecDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("simple_value")]
    public string SimpleValue { get; set; } = string.Empty;

    [JsonProperty("requires_gpu")]
    public bool RequiresGpu { get; set; }

    [JsonProperty("is_default")]
    public bool IsDefault { get; set; }
}

public class VideoQualityDto
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;
}

public class VideoCodecDto : CodecDto
{
    [JsonProperty("color_spaces")]
    public LabelValueDto[] AvailableVideoColorSpaces { get; set; } = [];

    [JsonProperty("tunes")]
    public LabelValueDto[] AvailableVideoTunes { get; set; } = [];

    [JsonProperty("profiles")]
    public LabelValueDto[] AvailableVideoProfiles { get; set; } = [];

    [JsonProperty("presets")]
    public LabelValueDto[] AvailablePresets { get; set; } = [];
}

public class AudioCodecDto : CodecDto
{
    [JsonProperty("available_languages")]
    public LabelValueDto[] AvailableLanguages { get; set; } = [];

    [JsonProperty("audio_quality_level")]
    public int AudioQualityLevel { get; set; }

    [JsonProperty("audio_channels")]
    public int AudioChannels { get; set; }

    [JsonProperty("hls_segment_filename")]
    public string HlsSegmentFilename { get; set; } = string.Empty;

    [JsonProperty("hls_playlist_filename")]
    public string HlsPlaylistFilename { get; set; } = string.Empty;

    [JsonProperty("bit_rate")]
    public long BitRate { get; set; }
}

public class SubtitleCodecDto : CodecDto
{
    [JsonProperty("available_languages")]
    public LabelValueDto[] AvailableLanguages { get; set; } = [];

    [JsonProperty("hls_segment_filename")]
    public string HlsSegmentFilename { get; set; } = string.Empty;

    [JsonProperty("hls_playlist_filename")]
    public string HlsPlaylistFilename { get; set; } = string.Empty;
}

public class LabelValueDto
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    public LabelValueDto(string s)
    {
        Label = s;
        Value = s;
    }

    public LabelValueDto()
    {
        //
    }
}
