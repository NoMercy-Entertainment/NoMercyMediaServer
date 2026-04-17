namespace NoMercy.Encoder.Profiles;

/// <summary>
/// V1 profile type aliases — these match the database IVideoProfile/IAudioProfile/ISubtitleProfile
/// but live in the Encoder namespace so the mapper doesn't depend on Database project.
/// The job maps from Database types to these before calling the mapper.
/// </summary>
public record V1VideoProfile(
    string Codec,
    int Bitrate,
    int Width,
    int Height,
    string Preset,
    string Profile,
    string Tune,
    string Level,
    string SegmentName,
    string PlaylistName,
    string ColorSpace,
    int Crf,
    int KeyInt,
    bool ConvertHdrToSdr,
    (string key, string Val)[] CustomArguments
);

public record V1AudioProfile(
    string Codec,
    int Channels,
    int SampleRate,
    string SegmentName,
    string PlaylistName,
    string[] AllowedLanguages,
    (string key, string Val)[] CustomArguments,
    string? Loudness = null,
    string? Downmix = null,
    string? CustomPanMatrix = null
);

public record V1SubtitleProfile(
    string Codec,
    string PlaylistName,
    string[] AllowedLanguages,
    (string key, string Val)[] CustomArguments
);

public record V1ThumbnailProfile(int Width, int IntervalSeconds);
