using FFMpegCore;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
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
        set => _language = value;
    }

    public int StreamIndex => AudioStream?.Index ?? -1;

    public long _bitRate = -1;

    private long BitRate => _bitRate == -1
        ? AudioStream?.BitRate ?? -1
        : _bitRate;

    public int AudioChannels { get; set; }

    public int AudioQualityLevel { get; set; } = -1;
    private string[] AllowedLanguages { get; set; } = ["eng"];

    protected virtual int Passes => 1;

    private readonly Dictionary<string, dynamic> _extraParameters = [];
    private readonly Dictionary<string, dynamic> _filters = [];
    private readonly Dictionary<string, dynamic> _ops = [];

    protected virtual string[] AvailableContainers { get; set; } = [
        AudioContainers.Mp3, AudioContainers.Flac, AudioContainers.M4a,
        AudioContainers.Aac, AudioContainers.Ogg, AudioContainers.Wav
    ];

    public virtual CodecDto[] AvailableCodecs { get; set; } = [
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
        _extraParameters[key] = value;
        return this;
    }

    public BaseAudio AddCustomArguments((string key, string Val)[] profileCustomArguments)
    {
        foreach ((string key, string Val) in profileCustomArguments)
            AddCustomArgument(key, Val);
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
        AddCustomArgument("-map_metadata", -1);
        AddCustomArgument("-fflags", "+bitexact");
        AddCustomArgument("-flags:v", "+bitexact");
        AddCustomArgument("-flags:a", "+bitexact");
        AddCustomArgument("-flags:s", "+bitexact");
        return this;
    }

    public List<BaseAudio> Build()
    {
        List<BaseAudio> streams = [];

        foreach (string allowedLanguage in AllowedLanguages.Append("und"))
        {
            foreach (AudioStream stream in AudioStreams.Where(audioStream => audioStream.Language == allowedLanguage))
            {
                BaseAudio newStream = (BaseAudio)MemberwiseClone();

                newStream.Language = stream.Language == "und" ? "eng" : stream.Language;

                newStream.IsAudio = true;

                newStream.AudioStream = stream;

                newStream.Index = AudioStreams.IndexOf(newStream.AudioStream);

                if(streams.Any(s => s.HlsPlaylistFilename == newStream.HlsPlaylistFilename)) continue;

                streams.Add(newStream);
            }
        }

        return streams;
    }

    public void AddToDictionary(Dictionary<string, dynamic> commandDictionary, int index)
    {
        commandDictionary["-map"] = $"[a{index}_hls_0]";
        commandDictionary["-c:a"] = AudioCodec.Value;

        if (AudioChannels != -1)
            commandDictionary["-ac"] = AudioChannels;

        if (!IsoLanguageMapper.IsoToLanguage.TryGetValue(Language, out string? language))
        {
            throw new($"Language {Language} is not supported");
        }
        commandDictionary[$"-metadata:s:a:{index}"] = $"title=\"{language} {AudioChannels}-{AudioCodec.SimpleValue}\"";
        commandDictionary[$"-metadata:s:a:{index}"] = $"language=\"{Language}\"";

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

    public static BaseAudio Create(string profileCodec)
    {
        return profileCodec switch
        {
            "aac" => new Aac(),
            "eac3" => new Eac3(),
            "ac3" => new Ac3(),
            "truehd" => new TrueHd(),

            "opus" => new Opus(),
            "mp3" => new Mp3(),
            "flac" => new Flac(),
            "vorbis" => new Vorbis(),
            _ => throw new($"Audio codec {profileCodec} is not supported")
        };
    }
}