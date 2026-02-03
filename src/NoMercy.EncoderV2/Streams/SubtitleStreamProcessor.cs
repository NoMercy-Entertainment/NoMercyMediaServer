using NoMercy.Database;
using NoMercy.Encoder;
using NoMercy.Encoder.Dto;

namespace NoMercy.EncoderV2.Streams;

/// <summary>
/// Processes subtitle streams during encoding
/// </summary>
public interface ISubtitleStreamProcessor
{
    Task<SubtitleStreamProcessingResult> ProcessSubtitleStreamAsync(
        string inputFile,
        ISubtitleProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default);

    bool RequiresConversion(string inputCodec, string outputCodec);
}

public class SubtitleStreamProcessingResult
{
    public bool Success { get; set; }
    public string OutputFile { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool IsForced { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SubtitleStreamProcessor : ISubtitleStreamProcessor
{
    public async Task<SubtitleStreamProcessingResult> ProcessSubtitleStreamAsync(
        string inputFile,
        ISubtitleProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default)
    {
        SubtitleStreamProcessingResult result = new()
        {
            Codec = profile.Codec
        };

        try
        {
            Ffprobe ffprobe = new(inputFile);
            await ffprobe.GetStreamData();

            if (streamIndex >= ffprobe.SubtitleStreams.Count)
            {
                result.Success = false;
                result.ErrorMessage = $"Subtitle stream index {streamIndex} not found in input file";
                return result;
            }

            SubtitleStream subtitleStream = ffprobe.SubtitleStreams[streamIndex];
            result.Language = subtitleStream.Language ?? "und";
            result.IsForced = subtitleStream.IsForced;

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to process subtitle stream: {ex.Message}";
        }

        return result;
    }

    public bool RequiresConversion(string inputCodec, string outputCodec)
    {
        string normalizedInput = NormalizeCodecName(inputCodec);
        string normalizedOutput = NormalizeCodecName(outputCodec);

        return normalizedInput != normalizedOutput;
    }

    private static string NormalizeCodecName(string codec)
    {
        return codec.ToLowerInvariant() switch
        {
            "subrip" => "srt",
            "ass" => "ass",
            "ssa" => "ssa",
            "webvtt" => "webvtt",
            "mov_text" => "mov_text",
            _ => codec.ToLowerInvariant()
        };
    }
}
