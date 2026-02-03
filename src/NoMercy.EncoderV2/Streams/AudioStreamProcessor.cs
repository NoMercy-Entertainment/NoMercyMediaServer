using NoMercy.Database;
using NoMercy.Encoder;
using NoMercy.Encoder.Dto;

namespace NoMercy.EncoderV2.Streams;

/// <summary>
/// Processes audio streams during encoding
/// </summary>
public interface IAudioStreamProcessor
{
    Task<AudioStreamProcessingResult> ProcessAudioStreamAsync(
        string inputFile,
        IAudioProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default);

    List<string> BuildAudioFilterChain(IAudioProfile profile, AudioStream audioStream);
}

public class AudioStreamProcessingResult
{
    public bool Success { get; set; }
    public string OutputFile { get; set; } = string.Empty;
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public string Codec { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class AudioStreamProcessor : IAudioStreamProcessor
{
    public async Task<AudioStreamProcessingResult> ProcessAudioStreamAsync(
        string inputFile,
        IAudioProfile profile,
        int streamIndex = 0,
        CancellationToken cancellationToken = default)
    {
        AudioStreamProcessingResult result = new()
        {
            Codec = profile.Codec,
            Channels = profile.Channels,
            SampleRate = profile.SampleRate
        };

        try
        {
            Ffprobe ffprobe = new(inputFile);
            await ffprobe.GetStreamData();

            if (streamIndex >= ffprobe.AudioStreams.Count)
            {
                result.Success = false;
                result.ErrorMessage = $"Audio stream index {streamIndex} not found in input file";
                return result;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to process audio stream: {ex.Message}";
        }

        return result;
    }

    public List<string> BuildAudioFilterChain(IAudioProfile profile, AudioStream audioStream)
    {
        List<string> filters = [];

        if (profile.SampleRate > 0 && audioStream.SampleRate != profile.SampleRate)
        {
            filters.Add($"aresample={profile.SampleRate}");
        }

        if (profile.Channels > 0 && audioStream.Channels != profile.Channels)
        {
            filters.Add($"pan=stereo|c0=c0|c1=c1");
        }

        return filters;
    }
}
