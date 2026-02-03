using NoMercy.EncoderV2.FFmpeg;

namespace NoMercy.EncoderV2.Validation;

/// <summary>
/// Validates codec availability and compatibility with system FFmpeg
/// </summary>
public interface ICodecValidator
{
    Task<CodecValidationResult> ValidateVideoCodecAsync(string codec);
    Task<CodecValidationResult> ValidateAudioCodecAsync(string codec);
    Task<CodecValidationResult> ValidateSubtitleCodecAsync(string codec);
    Task<List<string>> GetAvailableVideoCodecsAsync();
    Task<List<string>> GetAvailableAudioCodecsAsync();
}

public class CodecValidationResult
{
    public bool IsAvailable { get; set; }
    public bool IsHardwareAccelerated { get; set; }
    public string CodecName { get; set; } = string.Empty;
    public List<string> SupportedPixelFormats { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class CodecValidator(IFFmpegService ffmpegService) : ICodecValidator
{
    private List<string>? _cachedVideoCodecs;
    private List<string>? _cachedAudioCodecs;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public async Task<CodecValidationResult> ValidateVideoCodecAsync(string codec)
    {
        List<string> availableCodecs = await GetAvailableVideoCodecsAsync();

        CodecValidationResult result = new()
        {
            CodecName = codec,
            IsAvailable = availableCodecs.Contains(codec)
        };

        if (!result.IsAvailable)
        {
            result.ErrorMessage = $"Video codec '{codec}' is not available in FFmpeg";
            return result;
        }

        result.IsHardwareAccelerated = IsHardwareCodec(codec);

        if (result.IsHardwareAccelerated)
        {
            result.Warnings.Add($"Hardware codec '{codec}' may not be available on all encoding nodes");
        }

        return result;
    }

    public async Task<CodecValidationResult> ValidateAudioCodecAsync(string codec)
    {
        List<string> availableCodecs = await GetAvailableAudioCodecsAsync();

        CodecValidationResult result = new()
        {
            CodecName = codec,
            IsAvailable = availableCodecs.Contains(codec)
        };

        if (!result.IsAvailable)
        {
            result.ErrorMessage = $"Audio codec '{codec}' is not available in FFmpeg";
        }

        return result;
    }

    public async Task<CodecValidationResult> ValidateSubtitleCodecAsync(string codec)
    {
        CodecValidationResult result = new()
        {
            CodecName = codec,
            IsAvailable = IsKnownSubtitleCodec(codec)
        };

        if (!result.IsAvailable)
        {
            result.ErrorMessage = $"Subtitle codec '{codec}' is not supported";
        }

        return result;
    }

    public async Task<List<string>> GetAvailableVideoCodecsAsync()
    {
        if (_cachedVideoCodecs != null)
        {
            return _cachedVideoCodecs;
        }

        await _cacheLock.WaitAsync();
        try
        {
            if (_cachedVideoCodecs != null)
            {
                return _cachedVideoCodecs;
            }

            FFmpegExecutionResult result = await ffmpegService.ExecuteAsync(
                "-encoders -v quiet",
                Environment.CurrentDirectory
            );

            if (!result.Success)
            {
                return [];
            }

            _cachedVideoCodecs = ParseVideoCodecs(result.StandardOutput);
            return _cachedVideoCodecs;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<List<string>> GetAvailableAudioCodecsAsync()
    {
        if (_cachedAudioCodecs != null)
        {
            return _cachedAudioCodecs;
        }

        await _cacheLock.WaitAsync();
        try
        {
            if (_cachedAudioCodecs != null)
            {
                return _cachedAudioCodecs;
            }

            FFmpegExecutionResult result = await ffmpegService.ExecuteAsync(
                "-encoders -v quiet",
                Environment.CurrentDirectory
            );

            if (!result.Success)
            {
                return [];
            }

            _cachedAudioCodecs = ParseAudioCodecs(result.StandardOutput);
            return _cachedAudioCodecs;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static List<string> ParseVideoCodecs(string output)
    {
        List<string> codecs = [];
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (line.Contains("V.....") && line.Length > 10)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    codecs.Add(parts[1]);
                }
            }
        }

        return codecs;
    }

    private static List<string> ParseAudioCodecs(string output)
    {
        List<string> codecs = [];
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (line.Contains("A.....") && line.Length > 10)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    codecs.Add(parts[1]);
                }
            }
        }

        return codecs;
    }

    private static bool IsHardwareCodec(string codec)
    {
        return codec.Contains("_nvenc") ||
               codec.Contains("_qsv") ||
               codec.Contains("_amf") ||
               codec.Contains("_videotoolbox") ||
               codec.Contains("_vaapi");
    }

    private static bool IsKnownSubtitleCodec(string codec)
    {
        string[] knownCodecs = ["webvtt", "srt", "ass", "ssa", "subrip", "mov_text", "dvdsub", "dvbsub"];
        return knownCodecs.Contains(codec.ToLowerInvariant());
    }
}
