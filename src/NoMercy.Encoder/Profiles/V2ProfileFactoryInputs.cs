namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Adapter records that mirror the V1 DB profile shape (IVideoProfile,
/// IAudioProfile, ISubtitleProfile, IThumbnailProfile) without depending on
/// NoMercy.Database. <see cref="V2ProfileFactory"/> consumes these to build a
/// V2 <see cref="EncodingProfile"/>. The job (e.g. VideoEncodeJob) maps from
/// the DB row to these records before calling the factory.
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
