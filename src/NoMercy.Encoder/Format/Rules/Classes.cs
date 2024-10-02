using NoMercy.Encoder.Format.Container;
using NoMercy.NmSystem;

namespace NoMercy.Encoder.Format.Rules;

[Serializable]
public class Classes
{
    public string BasePath { get; set; } = "";
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string InputFile { get; set; } = "";
    public string Extension { get; set; } = "";

    internal virtual bool IsVideo { get; set; }
    internal virtual bool IsAudio { get; set; }
    internal virtual bool IsImage { get; set; }
    internal virtual bool IsSubtitle { get; set; }
    internal int Index { get; set; }
    public bool ConvertSubtitle { get; set; }
    public BaseContainer Container { get; set; } = default!;

    internal string HlsFlags { get; set; } = "independent_segments";
    internal int HlsListSize { get; set; }
    internal string HlsPlaylistType { get; set; } = "event";
    protected int HlsTime { get; set; } = 4;

    public static bool HasGpu => CheckGpu();

    protected string Type
    {
        get
        {
            if (IsImage) return "image";
            if (IsAudio) return "audio";
            if (IsVideo) return "video";
            return IsSubtitle ? "subtitle" : "unknown";
        }
    }

    public class VideoQualityDto
    {
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class CodecDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string SimpleValue { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool RequiresGpu { get; set; }
        public bool RequiresStrict { get; set; }
    }

    public class ContainerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class CropArea
    {
        public double W { get; set; }
        public double H { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public CropArea()
        {
        }

        public CropArea(int w, int h, int x, int y)
        {
            W = w;
            H = h;
            X = x;
            Y = y;
        }

        // Method to convert back to tuple if needed
        public (double W, double H, double X, double Y) ToTuple()
        {
            return (W, H, X, Y);
        }
    }

    public class ScaleArea
    {
        public int W { get; set; }
        public int H { get; set; }
    }

    protected class ParamDto : Dictionary<string, dynamic>
    {
        public ParamDto(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public ParamDto(string key, int value)
        {
            Key = key;
            Value = value;
        }

        public ParamDto(string key, bool value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }
        public dynamic Value { get; set; }
    }

    public virtual Classes ApplyFlags()
    {
        return this;
    }

    private static bool CheckGpu()
    {
        try
        {
            string result = FfMpeg.Exec("-init_hw_device cuda=hw -filter_hw_device hw -hwaccels 2>&1").Result;
            // Logger.Encoder(result);
            return !result.Contains("Failed", StringComparison.InvariantCultureIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Encoder(e.Message);
            return false;
        }
    }
}