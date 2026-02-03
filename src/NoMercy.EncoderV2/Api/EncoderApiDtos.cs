using Newtonsoft.Json;

namespace NoMercy.EncoderV2.Api;

public record CreateEncodingProfileRequest
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("container")] public string Container { get; set; } = "mp4";
    [JsonProperty("purpose")] public string? Purpose { get; set; }
    [JsonProperty("video_profile")] public VideoProfileConfigDto? VideoProfile { get; set; }
    [JsonProperty("audio_profile")] public AudioProfileConfigDto? AudioProfile { get; set; }
    [JsonProperty("subtitle_profile")] public SubtitleProfileConfigDto? SubtitleProfile { get; set; }
}

public record VideoProfileConfigDto
{
    [JsonProperty("codec")] public string Codec { get; set; } = "h264";
    [JsonProperty("width")] public int Width { get; set; } = 1920;
    [JsonProperty("height")] public int Height { get; set; } = 1080;
    [JsonProperty("bitrate")] public int Bitrate { get; set; } = 5000;
    [JsonProperty("framerate")] public int Framerate { get; set; } = 30;
    [JsonProperty("crf")] public int Crf { get; set; } = 23;
    [JsonProperty("preset")] public string Preset { get; set; } = "medium";
    [JsonProperty("profile")] public string Profile { get; set; } = "high";
    [JsonProperty("tune")] public string Tune { get; set; } = string.Empty;
    [JsonProperty("pixel_format")] public string PixelFormat { get; set; } = "yuv420p";
    [JsonProperty("color_space")] public string ColorSpace { get; set; } = string.Empty;
    [JsonProperty("keyframe_interval")] public int KeyframeInterval { get; set; } = 2;
    [JsonProperty("convert_hdr_to_sdr")] public bool ConvertHdrToSdr { get; set; }
    [JsonProperty("custom_options")] public Dictionary<string, dynamic> CustomOptions { get; set; } = [];
    [JsonProperty("custom_arguments")] public string? CustomArguments { get; set; }
}

public record AudioProfileConfigDto
{
    [JsonProperty("codec")] public string Codec { get; set; } = "aac";
    [JsonProperty("bitrate")] public int Bitrate { get; set; } = 192;
    [JsonProperty("channels")] public int Channels { get; set; } = 2;
    [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 48000;
    [JsonProperty("allowed_languages")] public List<string> AllowedLanguages { get; set; } = [];
    [JsonProperty("custom_options")] public Dictionary<string, dynamic> CustomOptions { get; set; } = [];
    [JsonProperty("custom_arguments")] public string? CustomArguments { get; set; }
}

public record SubtitleProfileConfigDto
{
    [JsonProperty("codec")] public string Codec { get; set; } = "webvtt";
    [JsonProperty("allowed_languages")] public List<string> AllowedLanguages { get; set; } = [];
    [JsonProperty("custom_options")] public Dictionary<string, dynamic> CustomOptions { get; set; } = [];
    [JsonProperty("custom_arguments")] public string? CustomArguments { get; set; }
}

public record EncodingProfileResponse
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("container")] public string Container { get; set; } = string.Empty;
    [JsonProperty("purpose")] public string Purpose { get; set; } = string.Empty;
    [JsonProperty("video_profile")] public VideoProfileConfigDto? VideoProfile { get; set; }
    [JsonProperty("audio_profile")] public AudioProfileConfigDto? AudioProfile { get; set; }
    [JsonProperty("subtitle_profile")] public SubtitleProfileConfigDto? SubtitleProfile { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
}

public record CapabilitiesResponse
{
    [JsonProperty("video_encoders")] public Dictionary<string, EncoderCapabilityDto> VideoEncoders { get; set; } = [];
    [JsonProperty("audio_encoders")] public Dictionary<string, EncoderCapabilityDto> AudioEncoders { get; set; } = [];
    [JsonProperty("subtitle_encoders")] public Dictionary<string, EncoderCapabilityDto> SubtitleEncoders { get; set; } = [];
    [JsonProperty("containers")] public Dictionary<string, ContainerCapabilityDto> Containers { get; set; } = [];
    [JsonProperty("generated_at")] public DateTime GeneratedAt { get; set; }
}

public record EncoderCapabilityDto
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("long_name")] public string LongName { get; set; } = string.Empty;
    [JsonProperty("is_hardware")] public bool IsHardware { get; set; }
    [JsonProperty("options")] public Dictionary<string, EncoderOptionDto> Options { get; set; } = [];
}

public record EncoderOptionDto
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("default")] public string? Default { get; set; }
    [JsonProperty("min")] public object? Min { get; set; }
    [JsonProperty("max")] public object? Max { get; set; }
    [JsonProperty("choices")] public List<string> Choices { get; set; } = [];
    [JsonProperty("help")] public string Help { get; set; } = string.Empty;
}

public record ContainerCapabilityDto
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("long_name")] public string LongName { get; set; } = string.Empty;
    [JsonProperty("can_mux")] public bool CanMux { get; set; }
    [JsonProperty("can_demux")] public bool CanDemux { get; set; }
    [JsonProperty("supported_video_codecs")] public List<string> SupportedVideoCodecs { get; set; } = [];
    [JsonProperty("supported_audio_codecs")] public List<string> SupportedAudioCodecs { get; set; } = [];
    [JsonProperty("supported_subtitle_codecs")] public List<string> SupportedSubtitleCodecs { get; set; } = [];
}

public record ValidationErrorResponse
{
    [JsonProperty("field")] public string Field { get; set; } = string.Empty;
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonProperty("severity")] public string Severity { get; set; } = "error";
}

public record ValidationResponse
{
    [JsonProperty("is_valid")] public bool IsValid { get; set; } = true;
    [JsonProperty("errors")] public List<ValidationErrorResponse> Errors { get; set; } = [];
}
