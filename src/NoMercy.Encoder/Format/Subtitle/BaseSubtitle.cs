using FFMpegCore;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Subtitle;

public class BaseSubtitle : Classes
{
    #region Properties

    public CodecDto SubtitleCodec { get; set; } = SubtitleCodecs.Webvtt;

    protected internal SubtitleStream? SubtitleStream;
    internal List<SubtitleStream> SubtitleStreams { get; set; } = [];

    public string Language => SubtitleStream?.Language ?? "und";
    private string[] AllowedLanguages { get; set; } = ["eng"];
    public int StreamIndex => SubtitleStream?.Index ?? -1;

    private readonly Dictionary<string, dynamic> _extraParameters = [];
    private readonly Dictionary<string, dynamic> _filters = [];
    private readonly Dictionary<string, dynamic> _ops = [];
    protected virtual CodecDto[] AvailableCodecs => [];
    protected virtual string[] AvailableContainers => [];

    protected string Variant { get; set; } = "full";
    private string _hlsSegmentFilename = "";

    internal string HlsSegmentFilename
    {
        get => _hlsSegmentFilename
            .Replace(":language:", Language)
            .Replace(":codec:", SubtitleCodec.SimpleValue)
            .Replace(":type:", Type)
            .Replace(":filename:", FileName)
            .Replace("\\", Str.DirectorySeparator)
            .Replace("/", Str.DirectorySeparator);
        set => _hlsSegmentFilename = value;
    }

    private string _hlsPlaylistFilename = "";

    internal string HlsPlaylistFilename
    {
        get => _hlsPlaylistFilename
            .Replace(":language:", Language)
            .Replace(":codec:", SubtitleCodec.SimpleValue)
            .Replace(":type:", Type)
            .Replace(":variant:", Variant)
            .Replace(":filename:", FileName)
            .Replace("\\", Str.DirectorySeparator)
            .Replace("/", Str.DirectorySeparator);
        set => _hlsPlaylistFilename = value;
    }

    #endregion

    #region Setters

    protected BaseSubtitle SetSubtitleCodec(string subtitleCodec)
    {
        CodecDto[] availableCodecs = AvailableCodecs;
        if (availableCodecs.All(codec => codec.Value != subtitleCodec))
            throw new Exception(
                $"Wrong subtitle codec value for {subtitleCodec}, available formats are {string.Join(", ", AvailableCodecs.Select(codec => codec.Value))}");

        SubtitleCodec = availableCodecs.First(codec => codec.Value == subtitleCodec);

        return this;
    }

    public BaseSubtitle AddCustomArgument(string key, dynamic? i)
    {
        _extraParameters.Add(key, i);
        return this;
    }

    public BaseSubtitle AddCustomArguments((string key, string Val)[] profileCustomArguments)
    {
        foreach ((string key, string Val) in profileCustomArguments)
            AddCustomArgument(key, Val);
        return this;
    }

    public BaseSubtitle AddOpts(string key, dynamic value)
    {
        _ops.Add(key, value);
        return this;
    }
    public BaseSubtitle AddOpt(string value)
    {
        AddCustomArgument(value, null);
        return this;
    }
    public BaseSubtitle AddOpts(string[] value)
    {
        foreach (string opt in value)
            AddOpt(opt);
        return this;
    }

    public BaseSubtitle SetHlsSegmentFilename(string value)
    {
        HlsSegmentFilename = value;
        return this;
    }

    public BaseSubtitle SetHlsPlaylistFilename(string value)
    {
        HlsPlaylistFilename = value;
        return this;
    }

    public BaseSubtitle SetAllowedLanguages(string[] languages)
    {
        AllowedLanguages = languages;
        return this;
    }

    public override BaseSubtitle ApplyFlags()
    {
        // AddCustomArgument("-map_metadata", -1);
        // AddCustomArgument("-fflags", "+bitexact");
        // AddCustomArgument("-flags:v", "+bitexact");
        // AddCustomArgument("-flags:a", "+bitexact");
        // AddCustomArgument("-flags:s", "+bitexact");
        return this;
    }

    public List<BaseSubtitle> Build()
    {
        List<BaseSubtitle> streams = [];

        foreach (string allowedLanguage in AllowedLanguages)
        {
            if (SubtitleStreams.All(stream => stream.Language != allowedLanguage)) continue;

            foreach (var stream in SubtitleStreams.Where(stream => stream.Language == allowedLanguage))
            {
                BaseSubtitle newStream = (BaseSubtitle)MemberwiseClone();

                newStream.IsSubtitle = true;

                newStream.SubtitleStream = stream;

                newStream.Index = SubtitleStreams.IndexOf(newStream.SubtitleStream);

                newStream.Variant = GetVariant(newStream);

                if(newStream.SubtitleStream!.CodecName == newStream.SubtitleCodec.SimpleValue) continue;

                if(streams.Any(s => s.Extension == newStream.Extension && s.Variant == newStream.Variant && s.Language == newStream.Language)) continue;

                streams.Add(newStream);
            }
        }

        Logger.Encoder($"Added {streams.Count} subtitle streams", LogEventLevel.Verbose);

        return streams;
    }

    private string GetVariant(BaseSubtitle stream)
    {
        var variant = "full";

        string? description = "";
        if(stream.SubtitleStream!.Tags?.TryGetValue("title", out description) is false) return variant;

        if (
            description.Contains("sign", StringComparison.CurrentCultureIgnoreCase)
            || description.Contains("song", StringComparison.CurrentCultureIgnoreCase)
            || description.Contains("s&s", StringComparison.CurrentCultureIgnoreCase)
            )
        {
            variant = "sign";
        }
        else  if (description.Contains("sdh", StringComparison.CurrentCultureIgnoreCase))
        {
            variant = "sdh";
        }

        return variant;
    }

    internal static string GetExtension(BaseSubtitle stream)
    {
        stream.SubtitleCodec = SubtitleCodecs.Webvtt;
        var extension = "vtt";
            
        if (stream.SubtitleStream!.CodecName == "hdmv_pgs_subtitle" || stream.SubtitleStream.CodecName == "dvd_subtitle")
        {
            stream.SubtitleCodec = SubtitleCodecs.Copy;
            stream.ConvertSubtitle = true;
            extension = "sup";
        }
        else if (stream.SubtitleStream.CodecName == "ass")
        {
            stream.SubtitleCodec = SubtitleCodecs.Ass;
            extension = "ass";
        }
        else if (stream.SubtitleStream.CodecName == "srt")
        {
            stream.SubtitleCodec = SubtitleCodecs.Srt;
            extension = "srt";
        }

        return extension;
    }

    public void AddToDictionary(Dictionary<string, dynamic> commandDictionary, int index)
    {
        // commandDictionary["-map"] = $"[s{index}_hls_0]";
        commandDictionary["-map"] = $"s:{index}";
        commandDictionary["-c:s"] = SubtitleCodec.Value;
        
        if (!IsoLanguageMapper.IsoToLanguage.TryGetValue(Language, out string? language))
        {
            throw new Exception($"Language {Language} is not supported");
        }
        commandDictionary[$"-metadata:s:s:{index}"] = $"title=\"{language}\"";
        commandDictionary[$"-metadata:s:s:{index}"] = $"language=\"{Language}\"";

        foreach (KeyValuePair<string, dynamic> extraParameter in _extraParameters)
        {
            commandDictionary[extraParameter.Key] = extraParameter.Value;
        }
    }

    public void CreateFolder()
    {
        string path = Path.Combine(BasePath, HlsPlaylistFilename.Split(Str.DirectorySeparator).First());
        Logger.Encoder($"Creating folder {path}", LogEventLevel.Verbose);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    #endregion

    public static BaseSubtitle Create(string profileCodec)
    {
        return profileCodec switch
        {
            "webvtt" => new Vtt(),
            "srt" => new Srt(),
            "ass" => new Ass(),
            "_" => throw new Exception($"Subtitle {profileCodec} is not supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(profileCodec), profileCodec, null)
        };
    }
}