using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Image;

public class BaseImage : Classes
{
    #region Properties

    private CodecDto ImageCodec { get; set; } = ImageCodecs.Png;

    protected internal FfProbeImageStream? ImageStream;

    internal List<FfProbeImageStream> ImageStreams { get; set; } = [];

    private protected virtual string[] InitialParameters => [];
    private protected virtual string[] AvailableContainers => [];
    private protected virtual string[] AvailablePresets => [];
    private protected virtual string[] AvailableProfiles => [];
    private protected virtual string[] AvailableFormats => [];
    private protected virtual CodecDto[] AvailableCodecs => [];

    private readonly Dictionary<string, dynamic> _extraParameters = [];
    private readonly Dictionary<string, dynamic> _filters = [];
    private readonly Dictionary<string, dynamic> _ops = [];

    internal int OutputWidth { get; set; }
    internal int? OutputHeight { get; set; }
    internal int FrameRate { get; set; }

    public bool IsHdr => VideoIsHdr();

    // ReSharper disable once InconsistentNaming
    private string _filename = string.Empty;

    internal string Filename
    {
        get => _filename.Replace(":framesize:", $"{Scale.W}x{Scale.H}").Replace(":type:", Type);
        set => _filename = value;
    }

    public dynamic Data =>
        new
        {
            Container = ImageCodec.Name,
            ExtraParameters = _extraParameters,
            Filters = _filters,
            Ops = _ops,
            Type,
        };

    #endregion

    #region Getters

    //

    #endregion

    #region Setters

    protected BaseImage SetImageCodec(string imageCodec)
    {
        CodecDto[] availableCodecs = AvailableCodecs;
        if (availableCodecs.All(codec => codec.Value != imageCodec))
            throw new(
                $"Wrong image codec value for {imageCodec}, available formats are {string.Join(", ", AvailableCodecs.Select(codec => codec.Value))}"
            );

        ImageCodec = availableCodecs.First(codec => codec.Value == imageCodec);

        return this;
    }

    public bool VideoIsHdr()
    {
        return false;
    }

    public BaseImage SetScale(string scale)
    {
        OutputWidth = scale.Split(":")[0].ToInt();
        ScaleValue = scale;
        return this;
    }

    public BaseImage SetScale(int value)
    {
        OutputWidth = value;
        ScaleValue = $"{value}:-2";
        return this;
    }

    public BaseImage SetScale(int width, int height)
    {
        OutputWidth = width;
        OutputHeight = height;
        ScaleValue = $"{width}:{height}";

        return this;
    }

    public BaseImage SetFilename(string fileName)
    {
        Filename = fileName;

        return this;
    }

    protected BaseImage AddCustomArgument(string key, dynamic i)
    {
        _extraParameters.Add(key, i);
        return this;
    }

    public BaseImage AddOpts(string key, dynamic value)
    {
        _ops.Add(key, value);
        return this;
    }

    public override BaseImage ApplyFlags()
    {
        AddCustomArgument("-ss", 1);
        // AddCustomArgument("-vf", $"\"fps=1/{FrameRate}\"");

        return this;
    }

    public void AddToDictionary(Dictionary<string, dynamic> commandDictionary, int index)
    {
        commandDictionary["-map"] = $"[i{index}_hls_0]";

        foreach (KeyValuePair<string, dynamic> extraParameter in _extraParameters)
            commandDictionary[extraParameter.Key] = extraParameter.Value;

        commandDictionary["-c:v"] = ImageCodec.Value;
    }

    public void CreateFolder()
    {
        string path = Path.Combine(BasePath, Filename.Split("/").First());

        if (!Directory.Exists(path))
        {
            Logger.Encoder($"Creating folder {path}", LogEventLevel.Verbose);
            Directory.CreateDirectory(path);
        }
    }

    public (int width, int height) GetImageDimensions(string imagePath)
    {
        SixLabors.ImageSharp.ImageInfo info = SixLabors.ImageSharp.Image.Identify(imagePath);
        return (info.Width, info.Height);
    }

    public async Task BuildSprite()
    {
        string baseName = Filename.Split("/").First();
        string spriteFile = Path.Combine(BasePath, baseName + ".webp");
        string thumbnailsFolder = Path.Combine(BasePath, baseName);

        if (File.Exists(spriteFile) || !Directory.Exists(thumbnailsFolder))
            return;

        string[] imageFiles = Directory.GetFiles(thumbnailsFolder).OrderBy(f => f).ToArray();
        if (imageFiles.Length == 0)
            return;

        // The spritevtt muxer in nomercy-ffmpeg tiles the input frames into a single sprite sheet
        // and writes a sibling .vtt with #xywh= cues in one invocation. Input framerate is set to
        // 1/FrameRate so the VTT cues land at the original sampling interval rather than playback
        // speed of the JPG sequence.
        string inputPattern = Path.Combine(thumbnailsFolder, baseName + "-%04d.jpg");
        string spritevttCommand =
            $"-framerate 1/{FrameRate} -i \"{inputPattern}\" -f spritevtt -y \"{spriteFile}\"";

        await FfMpeg.ExecStdErrOut(spritevttCommand, BasePath);

        if (Directory.Exists(thumbnailsFolder))
        {
            Logger.Encoder($"Deleting folder {thumbnailsFolder}");
            Directory.Delete(thumbnailsFolder, true);
        }
    }

    public BaseImage Build()
    {
        BaseImage newStream = (BaseImage)MemberwiseClone();

        newStream.IsImage = true;

        newStream.ImageStream = ImageStreams.FirstOrDefault();

        return newStream;
    }

    #endregion
}
