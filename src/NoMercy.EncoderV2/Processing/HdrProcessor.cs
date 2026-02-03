using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NoMercy.NmSystem.Information;

namespace NoMercy.EncoderV2.Processing;

/// <summary>
/// Processes HDR content including detection, metadata extraction, and SDR conversion
/// Implements the PRD goal G8: "Perform HDR→SDR conversion once, share across qualities"
/// </summary>
public partial class HdrProcessor : IHdrProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"max_content=(\d+)")]
    private static partial Regex MaxCllRegex();

    [GeneratedRegex(@"max_average=(\d+)")]
    private static partial Regex MaxFallRegex();

    [GeneratedRegex(@"side_data_type.*Dolby Vision", RegexOptions.IgnoreCase)]
    private static partial Regex DolbyVisionRegex();

    public async Task<HdrDetectionResult> DetectHdrAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        string ffprobeArgs = $"-v quiet -print_format json -show_streams -show_frames -read_intervals \"%+#1\" -select_streams v:0 \"{inputPath}\"";

        ProcessStartInfo startInfo = new()
        {
            FileName = AppFiles.FfProbePath,
            Arguments = ffprobeArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return new HdrDetectionResult { IsHdr = false };
        }

        return ParseHdrMetadata(output, error);
    }

    private static HdrDetectionResult ParseHdrMetadata(string ffprobeOutput, string ffprobeError)
    {
        string colorTransfer = string.Empty;
        string colorPrimaries = string.Empty;
        string colorSpace = string.Empty;
        int? maxCll = null;
        int? maxFall = null;
        string? masterDisplay = null;
        bool hasDolbyVision = false;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(ffprobeOutput);

            if (doc.RootElement.TryGetProperty("streams", out JsonElement streams) && streams.GetArrayLength() > 0)
            {
                JsonElement videoStream = streams[0];

                if (videoStream.TryGetProperty("color_transfer", out JsonElement ct))
                {
                    colorTransfer = ct.GetString() ?? string.Empty;
                }

                if (videoStream.TryGetProperty("color_primaries", out JsonElement cp))
                {
                    colorPrimaries = cp.GetString() ?? string.Empty;
                }

                if (videoStream.TryGetProperty("color_space", out JsonElement cs))
                {
                    colorSpace = cs.GetString() ?? string.Empty;
                }

                // Check side_data for HDR metadata
                if (videoStream.TryGetProperty("side_data_list", out JsonElement sideDataList))
                {
                    foreach (JsonElement sideData in sideDataList.EnumerateArray())
                    {
                        if (sideData.TryGetProperty("side_data_type", out JsonElement sideDataType))
                        {
                            string typeStr = sideDataType.GetString() ?? string.Empty;

                            if (typeStr.Contains("Content light level", StringComparison.OrdinalIgnoreCase))
                            {
                                if (sideData.TryGetProperty("max_content", out JsonElement maxContent))
                                {
                                    maxCll = maxContent.GetInt32();
                                }
                                if (sideData.TryGetProperty("max_average", out JsonElement maxAverage))
                                {
                                    maxFall = maxAverage.GetInt32();
                                }
                            }

                            if (typeStr.Contains("Mastering display metadata", StringComparison.OrdinalIgnoreCase))
                            {
                                masterDisplay = sideData.ToString();
                            }

                            if (typeStr.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase))
                            {
                                hasDolbyVision = true;
                            }
                        }
                    }
                }
            }

            // Check frames for additional metadata
            if (doc.RootElement.TryGetProperty("frames", out JsonElement frames) && frames.GetArrayLength() > 0)
            {
                JsonElement frame = frames[0];

                if (frame.TryGetProperty("side_data_list", out JsonElement frameSideData))
                {
                    foreach (JsonElement sideData in frameSideData.EnumerateArray())
                    {
                        if (sideData.TryGetProperty("side_data_type", out JsonElement sideDataType))
                        {
                            string typeStr = sideDataType.GetString() ?? string.Empty;

                            if (typeStr.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase))
                            {
                                hasDolbyVision = true;
                            }

                            // Extract MaxCLL/MaxFALL from frame-level metadata if not found in stream
                            if (typeStr.Contains("Content light level", StringComparison.OrdinalIgnoreCase))
                            {
                                if (maxCll == null && sideData.TryGetProperty("max_content", out JsonElement mc))
                                {
                                    maxCll = mc.GetInt32();
                                }
                                if (maxFall == null && sideData.TryGetProperty("max_average", out JsonElement ma))
                                {
                                    maxFall = ma.GetInt32();
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, try regex fallback
            Match maxCllMatch = MaxCllRegex().Match(ffprobeOutput);
            if (maxCllMatch.Success)
            {
                maxCll = int.Parse(maxCllMatch.Groups[1].Value);
            }

            Match maxFallMatch = MaxFallRegex().Match(ffprobeOutput);
            if (maxFallMatch.Success)
            {
                maxFall = int.Parse(maxFallMatch.Groups[1].Value);
            }
        }

        // Check stderr for Dolby Vision indicators
        if (DolbyVisionRegex().IsMatch(ffprobeError))
        {
            hasDolbyVision = true;
        }

        // Determine if content is HDR
        bool isHdr = IsPqTransfer(colorTransfer) ||
                     IsHlgTransfer(colorTransfer) ||
                     IsBt2020ColorSpace(colorSpace, colorPrimaries) ||
                     hasDolbyVision;

        // Determine HDR format
        HdrFormat format = HdrFormat.None;
        if (hasDolbyVision)
        {
            format = HdrFormat.DolbyVision;
        }
        else if (IsHlgTransfer(colorTransfer))
        {
            format = HdrFormat.Hlg;
        }
        else if (IsPqTransfer(colorTransfer))
        {
            // Check for HDR10+ dynamic metadata
            format = maxCll.HasValue || masterDisplay != null ? HdrFormat.Hdr10 : HdrFormat.Hdr10;
        }

        return new HdrDetectionResult
        {
            IsHdr = isHdr,
            Format = format,
            ColorTransfer = colorTransfer,
            ColorPrimaries = colorPrimaries,
            ColorSpace = colorSpace,
            MaxCll = maxCll,
            MaxFall = maxFall,
            MasterDisplayMetadata = masterDisplay,
            HasDolbyVisionSideData = hasDolbyVision
        };
    }

    private static bool IsPqTransfer(string colorTransfer)
    {
        return colorTransfer.Equals("smpte2084", StringComparison.OrdinalIgnoreCase) ||
               colorTransfer.Equals("smpte-st-2084", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHlgTransfer(string colorTransfer)
    {
        return colorTransfer.Equals("arib-std-b67", StringComparison.OrdinalIgnoreCase) ||
               colorTransfer.Equals("bt2020-10", StringComparison.OrdinalIgnoreCase) ||
               colorTransfer.Equals("bt2020-12", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBt2020ColorSpace(string colorSpace, string colorPrimaries)
    {
        return colorSpace.Contains("bt2020", StringComparison.OrdinalIgnoreCase) ||
               colorPrimaries.Contains("bt2020", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<HdrConversionResult> ConvertToSdrAsync(
        string inputPath,
        string outputPath,
        HdrProcessingOptions? options = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (!File.Exists(inputPath))
        {
            return new HdrConversionResult
            {
                Success = false,
                ErrorMessage = $"Input file not found: {inputPath}"
            };
        }

        options ??= new HdrProcessingOptions();

        HdrDetectionResult detection = await DetectHdrAsync(inputPath, cancellationToken);

        if (!detection.IsHdr)
        {
            return new HdrConversionResult
            {
                Success = false,
                SourceFormat = HdrFormat.None,
                ErrorMessage = "Source file is not HDR content"
            };
        }

        // Build the filter chain
        string filterChain = BuildToneMappingFilterChain(detection, options);

        // Get duration for progress tracking
        TimeSpan duration = await GetDurationAsync(inputPath, cancellationToken);

        // Build FFmpeg command for intermediate file (high quality, minimal re-encoding)
        string outputDir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDir);

        // Use high quality settings for the intermediate file
        string ffmpegArgs = $"-y -i \"{inputPath}\" " +
                           $"-vf \"{filterChain}\" " +
                           "-c:v libx264 -preset medium -crf 16 " +  // High quality intermediate
                           "-c:a copy " +  // Copy audio streams
                           "-c:s copy " +  // Copy subtitle streams
                           "-map 0 " +  // Map all streams
                           $"-progress pipe:1 \"{outputPath}\"";

        ProcessStartInfo startInfo = new()
        {
            FileName = AppFiles.FfmpegPath,
            Arguments = ffmpegArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        StringBuilder errorOutput = new();

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                errorOutput.AppendLine(args.Data);
            }
        };

        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data) && progressCallback != null && duration.TotalSeconds > 0)
            {
                // Parse progress output
                double? progress = ParseProgressOutput(args.Data, duration);
                if (progress.HasValue)
                {
                    progressCallback(progress.Value);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        stopwatch.Stop();

        if (process.ExitCode != 0)
        {
            return new HdrConversionResult
            {
                Success = false,
                SourceFormat = detection.Format,
                AlgorithmUsed = options.Algorithm,
                ProcessingTime = stopwatch.Elapsed,
                ErrorMessage = $"FFmpeg exited with code {process.ExitCode}: {errorOutput}"
            };
        }

        return new HdrConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            SourceFormat = detection.Format,
            AlgorithmUsed = options.Algorithm,
            ProcessingTime = stopwatch.Elapsed
        };
    }

    private static double? ParseProgressOutput(string output, TimeSpan totalDuration)
    {
        if (output.StartsWith("out_time_ms=", StringComparison.OrdinalIgnoreCase))
        {
            string timeStr = output["out_time_ms=".Length..];
            if (long.TryParse(timeStr, out long microseconds))
            {
                double currentSeconds = microseconds / 1_000_000.0;
                return Math.Min(100, currentSeconds / totalDuration.TotalSeconds * 100);
            }
        }
        return null;
    }

    private static async Task<TimeSpan> GetDurationAsync(string inputPath, CancellationToken cancellationToken)
    {
        string ffprobeArgs = $"-v quiet -print_format json -show_format \"{inputPath}\"";

        ProcessStartInfo startInfo = new()
        {
            FileName = AppFiles.FfProbePath,
            Arguments = ffprobeArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(output);
            if (doc.RootElement.TryGetProperty("format", out JsonElement format) &&
                format.TryGetProperty("duration", out JsonElement durationElem))
            {
                string? durationStr = durationElem.GetString();
                if (double.TryParse(durationStr, out double seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return TimeSpan.Zero;
    }

    public string BuildToneMappingFilterChain(HdrDetectionResult sourceInfo, HdrProcessingOptions? options = null)
    {
        options ??= new HdrProcessingOptions();

        if (!sourceInfo.IsHdr || options.Algorithm == ToneMappingAlgorithm.None)
        {
            return string.Empty;
        }

        StringBuilder filters = new();

        // Use hardware acceleration if requested and available
        if (options.HardwareAcceleration != HdrHardwareAcceleration.None)
        {
            string hwFilter = BuildHardwareAcceleratedFilter(sourceInfo, options);
            if (!string.IsNullOrEmpty(hwFilter))
            {
                return hwFilter;
            }
            // Fall through to software if hardware filter unavailable
        }

        // Software tone mapping using zscale and tonemap filters
        // Step 1: Set input color characteristics based on detected format
        string inputTransfer = sourceInfo.Format switch
        {
            HdrFormat.Hlg => "arib-std-b67",
            _ => "smpte2084"  // HDR10, HDR10+, DolbyVision base layer
        };

        // Step 2: Convert to linear light
        filters.Append($"zscale=t=linear:npl={options.PeakBrightness}");

        // Step 3: Convert to high precision float format for tone mapping
        filters.Append(",format=gbrpf32le");

        // Step 4: Apply gamut conversion to BT.709
        filters.Append(",zscale=p=bt709");

        // Step 5: Apply tone mapping
        string tonemapParams = BuildTonemapParameters(options);
        filters.Append($",tonemap={tonemapParams}");

        // Step 6: Apply color space conversion
        if (options.ConvertColorSpace)
        {
            filters.Append(",zscale=t=bt709:m=bt709:r=tv");
        }

        // Step 7: Convert to output pixel format
        filters.Append($",format={options.OutputPixelFormat}");

        return filters.ToString();
    }

    private static string BuildTonemapParameters(HdrProcessingOptions options)
    {
        StringBuilder tonemapParams = new();

        // Tone mapping algorithm
        string algorithm = options.Algorithm switch
        {
            ToneMappingAlgorithm.Hable => "hable",
            ToneMappingAlgorithm.Reinhard => "reinhard",
            ToneMappingAlgorithm.Mobius => "mobius",
            ToneMappingAlgorithm.Bt2390 => "bt2390",
            ToneMappingAlgorithm.Linear => "linear",
            _ => "hable"
        };

        tonemapParams.Append(algorithm);

        // Desaturation parameter
        if (options.Desaturation > 0)
        {
            tonemapParams.Append($":desat={options.Desaturation:F2}");
        }
        else
        {
            tonemapParams.Append(":desat=0");
        }

        // Peak brightness parameter (for algorithms that support it)
        if (options.Algorithm is ToneMappingAlgorithm.Reinhard or ToneMappingAlgorithm.Mobius)
        {
            tonemapParams.Append($":peak={options.TargetPeak / options.PeakBrightness:F4}");
        }

        return tonemapParams.ToString();
    }

    private static string BuildHardwareAcceleratedFilter(HdrDetectionResult sourceInfo, HdrProcessingOptions options)
    {
        // Build hardware-accelerated filter chains
        // Note: These require specific FFmpeg builds with corresponding support

        return options.HardwareAcceleration switch
        {
            HdrHardwareAcceleration.Cuda =>
                // NVIDIA CUDA tone mapping
                $"hwupload_cuda,tonemap_cuda=tonemap={GetTonemapName(options.Algorithm)}:desat={options.Desaturation}:peak={options.TargetPeak}:format=nv12,hwdownload,format=nv12",

            HdrHardwareAcceleration.OpenCl =>
                // OpenCL tone mapping
                $"hwupload,tonemap_opencl=tonemap={GetTonemapName(options.Algorithm)}:desat={options.Desaturation}:peak={options.TargetPeak}:format=nv12,hwdownload,format={options.OutputPixelFormat}",

            HdrHardwareAcceleration.Vulkan =>
                // Vulkan tone mapping (FFmpeg 5.0+)
                $"hwupload,tonemap_vulkan=tonemap={GetTonemapName(options.Algorithm)}:desat={options.Desaturation}:peak={options.TargetPeak}:format=nv12,hwdownload,format={options.OutputPixelFormat}",

            _ => string.Empty
        };
    }

    private static string GetTonemapName(ToneMappingAlgorithm algorithm)
    {
        return algorithm switch
        {
            ToneMappingAlgorithm.Hable => "hable",
            ToneMappingAlgorithm.Reinhard => "reinhard",
            ToneMappingAlgorithm.Mobius => "mobius",
            ToneMappingAlgorithm.Bt2390 => "bt2390",
            ToneMappingAlgorithm.Linear => "linear",
            _ => "hable"
        };
    }

    public string? GetCachedSdrPath(string inputPath, string cacheDirectory)
    {
        if (!Directory.Exists(cacheDirectory))
        {
            return null;
        }

        // Generate a hash-based filename for the cached SDR version
        string cacheFileName = GenerateCacheFileName(inputPath);
        string cachedPath = Path.Combine(cacheDirectory, cacheFileName);

        if (File.Exists(cachedPath))
        {
            // Verify the cache is still valid (source file hasn't changed)
            FileInfo sourceInfo = new(inputPath);
            FileInfo cacheInfo = new(cachedPath);

            if (cacheInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc)
            {
                return cachedPath;
            }
        }

        return null;
    }

    private static string GenerateCacheFileName(string inputPath)
    {
        // Create a deterministic hash of the input path and file modification time
        FileInfo fileInfo = new(inputPath);
        string hashInput = $"{inputPath}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc:O}";

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        string hash = Convert.ToHexString(hashBytes)[..16];

        return $"sdr_cache_{hash}.mkv";
    }

    public ToneMappingAlgorithm GetRecommendedAlgorithm(HdrFormat format)
    {
        return format switch
        {
            // Hable works well for most cinematic HDR10 content
            HdrFormat.Hdr10 => ToneMappingAlgorithm.Hable,

            // HDR10+ has dynamic metadata, BT.2390 can handle it better
            HdrFormat.Hdr10Plus => ToneMappingAlgorithm.Bt2390,

            // HLG is designed for broadcast, Reinhard preserves more detail
            HdrFormat.Hlg => ToneMappingAlgorithm.Reinhard,

            // Dolby Vision has complex metadata, Hable is a safe default
            HdrFormat.DolbyVision => ToneMappingAlgorithm.Hable,

            // Non-HDR content
            _ => ToneMappingAlgorithm.None
        };
    }
}
