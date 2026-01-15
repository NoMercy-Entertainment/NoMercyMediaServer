namespace NoMercy.Encoder.Format.Rules;

public static class AudioCodecs
{
    public static readonly Classes.CodecDto Aac = new()
    {
        Name = "Advanced Audio Coding",
        Value = "aac",
        SimpleValue = "aac",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Ac3 = new()
    {
        Name = "Dolby Digital",
        Value = "ac3",
        SimpleValue = "ac3",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Eac3 = new()
    {
        Name = "Dolby Digital Plus",
        Value = "eac3",
        SimpleValue = "eac3",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Flac = new()
    {
        Name = "Free Lossless Audio Codec",
        Value = "flac",
        SimpleValue = "flac",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Mp3 = new()
    {
        Name = "MP3",
        Value = "libmp3lame",
        SimpleValue = "mp3",
        IsDefault = true
    };

    public static readonly Classes.CodecDto LibOpus = new()
    {
        Name = "Opus Audio Codec",
        Value = "libopus",
        SimpleValue = "opus",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Opus = new()
    {
        Name = "Opus Audio Codec (Experimental)",
        Value = "opus",
        SimpleValue = "opus",
        RequiresStrict = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto TrueHd = new()
    {
        Name = "TrueHD",
        Value = "truehd",
        SimpleValue = "truehd",
        IsDefault = true
    };

    public static readonly Classes.CodecDto LibVorbis = new()
    {
        Name = "Vorbis Audio Codec",
        Value = "libvorbis",
        SimpleValue = "vorbis",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Vorbis = new()
    {
        Name = "Vorbis Audio Codec (experimental)",
        Value = "vorbis",
        SimpleValue = "vorbis",
        RequiresStrict = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto PcmS16Le = new()
    {
        Name = "PCM signed 16-bit little-endian",
        Value = "pcm_s16le",
        SimpleValue = "pcm",
        IsDefault = true
    };
}