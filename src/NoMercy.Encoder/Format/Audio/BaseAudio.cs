using FFMpegCore;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Audio;

public class BaseAudio : Classes
{
    #region Properties

    public virtual CodecDto AudioCodec { get; set; } = AudioCodecs.Aac;

    protected internal AudioStream? AudioStream;
    internal List<AudioStream> AudioStreams { get; set; } = [];

    private string? _language;

    public string Language
    {
        get => _language ?? AudioStream?.Language ?? "und";
        set => _language = value != null ? value : _language ?? "und";
    }

    public int StreamIndex => AudioStream?.Index ?? -1;

    // ReSharper disable once InconsistentNaming
    public long _bitRate = -1;

    private long BitRate => _bitRate == -1
        ? AudioStream?.BitRate ?? -1
        : _bitRate;

    public int AudioChannels { get; set; }

    public int AudioQualityLevel { get; set; } = -1;
    public int AudioSampleRate { get; set; } = -1;
    private string[] AllowedLanguages { get; set; } = ["eng"];

    protected virtual int Passes => 1;

    private readonly Dictionary<string, dynamic> _extraParameters = [];
    private readonly Dictionary<string, dynamic> _filters = [];
    private readonly Dictionary<string, dynamic> _ops = [];

    internal readonly List<string> _id3Tags = [];

    protected virtual string[] AvailableContainers { get; set; } =
    [
        AudioContainers.Mp3, AudioContainers.Flac, AudioContainers.M4A,
        AudioContainers.Aac, AudioContainers.Ogg, AudioContainers.Wav
    ];

    public virtual CodecDto[] AvailableCodecs { get; set; } =
    [
        AudioCodecs.Aac, AudioCodecs.Opus, AudioCodecs.Vorbis,
        AudioCodecs.Mp3, AudioCodecs.Flac, AudioCodecs.Ac3,
        AudioCodecs.Eac3, AudioCodecs.TrueHd
    ];

    private string _hlsSegmentFilename = "";

    public string HlsSegmentFilename
    {
        get => _hlsSegmentFilename
            .Replace(":language:", Language)
            .Replace(":codec:", AudioCodec.SimpleValue)
            .Replace(":filename:", FileName)
            .Replace(":type:", Type);
        set => _hlsSegmentFilename = value;
    }

    private string _hlsPlaylistFilename = "";

    public BaseAudio()
    {
        AudioChannels = AudioStream?.Channels ?? -1;
    }

    public string HlsPlaylistFilename
    {
        get => _hlsPlaylistFilename
            .Replace(":language:", Language)
            .Replace(":codec:", AudioCodec.SimpleValue)
            .Replace(":filename:", FileName)
            .Replace(":type:", Type);
        set => _hlsPlaylistFilename = value;
    }

    #endregion

    #region Setters

    public BaseAudio SetAudioKiloBitrate(int kiloBitrate)
    {
        if (kiloBitrate < 1)
            throw new("Wrong kilo bitrate value");

        _bitRate = kiloBitrate;

        return this;
    }

    protected virtual BaseAudio SetAudioQuality(int qualityLevel)
    {
        if (qualityLevel is < 0 or > 9)
            throw new("Wrong quality level value");

        AudioQualityLevel = qualityLevel;

        return this;
    }

    protected BaseAudio SetAudioCodec(string audioCodec)
    {
        if (AvailableCodecs.All(codec => codec.Value != audioCodec))
            throw new(
                $"Wrong audio codec value for {audioCodec}, available formats are {string.Join(", ", AvailableCodecs.Select(codec => codec.Value))}");

        AudioCodec = AvailableCodecs.First(codec => codec.Value == audioCodec);

        lock (AudioCodec.SimpleValue)
        {
        }

        return this;
    }

    public BaseAudio SetAudioChannels(int channels)
    {
        if (channels is 0)
            return this;

        if (channels < 1)
            throw new("Wrong audio channels value");

        AudioChannels = channels;

        return this;
    }

    public BaseAudio AddCustomArgument(string key, dynamic? value)
    {
        _extraParameters[key] = value ?? string.Empty;
        return this;
    }

    public BaseAudio AddCustomArguments((string key, string val)[] profileCustomArguments)
    {
        foreach ((string key, string val) in profileCustomArguments)
            AddCustomArgument(key, val);
        return this;
    }

    public BaseAudio AddId3Tag(string value)
    {
        _id3Tags.Add(value);
        return this;
    }

    public BaseAudio AddOpts(string key, dynamic value)
    {
        _ops[key] = value;
        return this;
    }

    public BaseAudio SetRate(int value)
    {
        AddCustomArgument("-b:a", value);
        return this;
    }

    public BaseAudio SetSampleRate(int value)
    {
        if (value < 1)
            throw new("Wrong sample rate value");
        AudioSampleRate = value;
        return this;
    }

    public BaseAudio AddOpt(string value)
    {
        AddCustomArgument(value, null);
        return this;
    }

    public BaseAudio AddOpts(string[] value)
    {
        foreach (string opt in value)
            AddOpt(opt);
        return this;
    }

    public BaseAudio SetHlsSegmentFilename(string value)
    {
        HlsSegmentFilename = value;
        return this;
    }

    public BaseAudio SetHlsPlaylistFilename(string value)
    {
        HlsPlaylistFilename = value;
        return this;
    }

    #endregion

    public BaseAudio SetAllowedLanguages(string[] languages)
    {
        AllowedLanguages = languages;
        return this;
    }

    public override BaseAudio ApplyFlags()
    {
        return this;
    }

    public List<BaseAudio> Build()
    {
        List<BaseAudio> streams = [];

        foreach (string allowedLanguage in AllowedLanguages.Append("und"))
        foreach (AudioStream stream in AudioStreams.Where(audioStream =>
                     audioStream.Language is null || audioStream.Language == allowedLanguage))
        {
            BaseAudio newStream = (BaseAudio)MemberwiseClone();

            newStream.Language = stream.Language == "und"
                ? "eng"
                : stream.Language ?? "eng";

            newStream.IsAudio = true;

            newStream.AudioStream = stream;

            newStream.Index = AudioStreams.IndexOf(newStream.AudioStream);

            if (streams.Any(s => s.HlsPlaylistFilename == newStream.HlsPlaylistFilename)) continue;

            streams.Add(newStream);
        }

        return streams.Distinct().ToList();
    }

    public void AddToDictionary(Dictionary<string, dynamic> commandDictionary, int index)
    {
        commandDictionary["-map"] = $"[a{index}_hls_0]";
        commandDictionary["-c:a"] = AudioCodec.Value;

        if (AudioChannels != -1)
            commandDictionary["-ac"] = AudioChannels;

        if (AudioSampleRate != -1)
            commandDictionary["-ar"] = AudioSampleRate;

        if (_bitRate != -1)
            commandDictionary["-b:a"] = $"{_bitRate}k";

        foreach (KeyValuePair<string, dynamic> extraParameter in _extraParameters)
            commandDictionary[extraParameter.Key] = extraParameter.Value;
    }

    public void CreateFolder()
    {
        string path = Path.Combine(BasePath, HlsSegmentFilename.Split("/").First());

        if (!Directory.Exists(path))
        {
            Logger.Encoder($"Creating folder {path}", LogEventLevel.Verbose);
            Directory.CreateDirectory(path);
        }
    }

    public static BaseAudio Create(string codec)
    {
        return codec switch
        {
            "aac" => new Aac(),
            "eac3" => new Eac3(),
            "ac3" => new Ac3(),
            "truehd" => new TrueHd(),

            "opus" => new Opus(),
            "libmp3lame" => new Mp3(),
            "flac" => new Flac(),
            "vorbis" => new Vorbis(),
            _ => throw new($"Audio codec {codec} is not supported")
        };
    }

    public BaseAudio SetLanguage(string language)
    {
        Language = language;

        return this;
    }

    public BaseAudio AddId3Tags(Dictionary<string, object?> id3Tags)
    {
        foreach (KeyValuePair<string, object?> item in id3Tags)
        {
            if (item.Value is null or "")
                continue;
            AddId3Tag($"{item.Key.Replace(" ", "_")}=\"{item.Value}\"");
        }

        return this;
    }
}