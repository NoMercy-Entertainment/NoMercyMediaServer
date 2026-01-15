using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using SixLabors.ImageSharp.Formats;

namespace NoMercy.Helpers;

public class ImageConvertArguments
{
    [JsonProperty("width")] public int? Width { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("quality")] public int Quality { get; set; } = 100;

    public IImageFormat Format
    {
        get
        {
            IImageFormat result;
            try
            {
                result = Images.Parse(Type ?? "png");
            }
            catch (Exception e)
            {
                result = Images.Parse("png");
                Logger.App(e.Message, LogEventLevel.Error);
            }

            return result;
        }
    }

    [FromQuery(Name = "aspect_ratio")]
    [JsonProperty("aspect_ratio")]
    public double? AspectRatio { get; set; }
}