using NoMercy.Database;
using NoMercy.Encoder;
using NoMercy.Encoder.Dto;

namespace NoMercy.EncoderV2.Streams;

/// <summary>
/// Processes video streams during encoding
/// </summary>
public interface IVideoStreamProcessor
{
    Task<VideoStreamProcessingResult> ProcessVideoStreamAsync(
        string inputFile,
        IVideoProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default);

    List<string> BuildVideoFilterChain(IVideoProfile profile, VideoStream videoStream);
}

public class VideoStreamProcessingResult
{
    public bool Success { get; set; }
    public string OutputFile { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double Framerate { get; set; }
    public string Codec { get; set; } = string.Empty;
    public bool IsHDR { get; set; }
    public string? ErrorMessage { get; set; }
}

public class VideoStreamProcessor : IVideoStreamProcessor
{
    public async Task<VideoStreamProcessingResult> ProcessVideoStreamAsync(
        string inputFile,
        IVideoProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default)
    {
        VideoStreamProcessingResult result = new()
        {
            Codec = profile.Codec,
            Width = profile.Width,
            Height = profile.Height
        };

        try
        {
            Ffprobe ffprobe = new(inputFile);
            await ffprobe.GetStreamData();

            if (streamIndex >= ffprobe.VideoStreams.Count)
            {
                result.Success = false;
                result.ErrorMessage = $"Video stream index {streamIndex} not found in input file";
                return result;
            }

            VideoStream videoStream = ffprobe.VideoStreams[streamIndex];
            result.IsHDR = videoStream.IsHdr;
            result.Framerate = videoStream.FrameRate;

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to process video stream: {ex.Message}";
        }

        return result;
    }

    public List<string> BuildVideoFilterChain(IVideoProfile profile, VideoStream videoStream)
    {
        List<string> filters = [];

        bool needsScaling = profile.Width != videoStream.Width || profile.Height != videoStream.Height;

        if (needsScaling)
        {
            filters.Add($"scale={profile.Width}:{profile.Height}");
        }

        if (videoStream.IsHdr && profile.ConvertHdrToSdr)
        {
            filters.Add("zscale=t=linear:npl=100");
            filters.Add("format=gbrpf32le");
            filters.Add("zscale=p=bt709");
            filters.Add("tonemap=tonemap=hable:desat=0");
            filters.Add("zscale=t=bt709:m=bt709:r=tv");
            filters.Add("format=yuv420p");
        }

        return filters;
    }
}
