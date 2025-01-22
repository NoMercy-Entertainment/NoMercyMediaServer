using NoMercy.Networking;
using NoMercy.NmSystem.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace NoMercy.Server;

public class Dev
{
    public static Task Run()
    {
        // OpenSubtitlesClient client = new();
        // OpenSubtitlesClient subtitlesClient = await client.Login();
        // SubtitleSearchResponse? x = await subtitlesClient.SearchSubtitles("Black Panther Wakanda Forever (2022)", "dut");
        // Logger.OpenSubs(x);


        return Task.CompletedTask;
    }
    
    public static string GetDominantColor(string path)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        image.Mutate(x => x
            .Resize(new ResizeOptions()
            {
                Sampler = KnownResamplers.NearestNeighbor,
                Size = new(100, 0)
            })
            .Quantize(new OctreeQuantizer
            {
                Options =
                {
                    MaxColors = 1,
                    Dither = new OrderedDither(1),
                    DitherScale = 1
                }
            }));

        Rgb24 dominant = image[0, 0];

        return dominant.ToHexString();

    }
}
