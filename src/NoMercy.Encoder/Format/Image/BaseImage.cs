using System.Text;
using FFMpegCore;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using Serilog.Events;

namespace NoMercy.Encoder.Format.Image;

public class BaseImage : Classes
{
    #region Properties

    private CodecDto ImageCodec { get; set; } = ImageCodecs.Png;

    protected internal VideoStream? ImageStream;

    internal List<VideoStream> ImageStreams { get; set; }

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

    internal string _Filename = "";

    internal string Filename
    {
        get => _Filename
            .Replace(":framesize:", $"{Scale.W}x{Scale.H}")
            .Replace(":type:", Type);
        set => _Filename = value;
    }

    public dynamic Data => new
    {
        Container = ImageCodec.Name,
        ExtraParameters = _extraParameters,
        Filters = _filters,
        Ops = _ops,
        Type
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
                $"Wrong image codec value for {imageCodec}, available formats are {string.Join(", ", AvailableCodecs.Select(codec => codec.Value))}");

        ImageCodec = availableCodecs.First(codec => codec.Value == imageCodec);

        return this;
    }

    public bool VideoIsHdr()
    {
        return ImageStream?.PixelFormat.Contains("hdr") ?? false;
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
        commandDictionary["-c:v"] = ImageCodec.Value;

        foreach (KeyValuePair<string, dynamic> extraParameter in _extraParameters)
            commandDictionary[extraParameter.Key] = extraParameter.Value;
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
        using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(imagePath))
        {
            return (image.Width, image.Height);
        }
    }

    public async Task BuildSprite(ProgressMeta progressMeta)
    {
        string spriteFilename = Filename.Split("/").First() + ".webp";
        string spriteFile = Path.Combine(BasePath, spriteFilename);
        string timeFile = Path.Combine(BasePath, Filename.Split("/").First() + ".vtt");

        string thumbnailsFolder = Path.Combine(BasePath, Filename.Split("/").First());

        if (File.Exists(spriteFile) || !Directory.Exists(thumbnailsFolder)) return;

        string[] imageFiles = Directory.GetFiles(thumbnailsFolder)
            .OrderBy(f => f)
            .ToArray();

        if (imageFiles.Length == 0) return;

        (int thumbWidth, int thumbHeight) = GetImageDimensions(imageFiles.First());

        int gridWidth = (int)Math.Ceiling(Math.Sqrt(imageFiles.Length));
        int gridHeight = (int)Math.Ceiling((double)imageFiles.Length / gridWidth);

        string montageCommand =
            $"-i \"{Path.Combine(thumbnailsFolder, Filename.Split("/").First() + "-%04d.jpg")}\" -filter_complex tile=\"{gridWidth}x{gridHeight}\" -y \"{spriteFile}\"";

        await FfMpeg.Run(montageCommand, BasePath, progressMeta);

        List<string> times = CreateTimeInterval(imageFiles.Length * FrameRate + 1, FrameRate);

        int dstX = 0;
        int dstY = 0;

        int jpg = 1;
        int line = 1;

        StringBuilder thumbContent = new();
        thumbContent.AppendLine("WEBVTT");
        thumbContent.AppendLine("");

        foreach (string time in times.Take(times.Count - 1))
        {
            int index = times.IndexOf(time);
            thumbContent.AppendLine(jpg.ToString());
            thumbContent.AppendLine($"{time} --> {times[index + 1]}");
            thumbContent.AppendLine($"{spriteFilename}#xywh={dstX},{dstY},{thumbWidth},{thumbHeight}");
            thumbContent.AppendLine("");

            if (line > gridHeight) continue;

            if (jpg % gridWidth == 0)
            {
                dstX = 0;
                dstY += thumbHeight;
            }
            else
            {
                dstX += thumbWidth;
            }

            jpg++;
        }

        await File.WriteAllTextAsync(timeFile, thumbContent.ToString());

        if (Directory.Exists(thumbnailsFolder))
        {
            Logger.Encoder($"Deleting folder {thumbnailsFolder}");
            Directory.Delete(thumbnailsFolder, true);
        }
    }

    private List<string> CreateTimeInterval(double duration, int interval)
    {
        DateTime d = new DateTime().Date;
        List<string> timeArr = new();

        for (int i = 0; i <= duration / interval; i++)
        {
            string hours = d.Hour.ToString("D2");
            string minute = d.Minute.ToString("D2");
            string second = d.Second.ToString("D2");
            string miliSecond = d.Millisecond.ToString("D3");

            timeArr.Add($"{hours}:{minute}:{second}.{miliSecond}");

            d = d.AddSeconds(interval);
        }

        return timeArr;
    }

    public BaseImage Build()
    {
        BaseImage newStream = (BaseImage)MemberwiseClone();

        newStream.IsImage = true;

        newStream.ImageStream = ImageStreams.First();

        return newStream;
    }

    #endregion
}