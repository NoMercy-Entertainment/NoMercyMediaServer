using Newtonsoft.Json;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Validation;

namespace NoMercy.EncoderV2.PostProcessing;

#region Result DTOs

/// <summary>
/// Individual post-processing step result
/// </summary>
public class PostProcessingStepResult
{
    [JsonProperty("step")] public string Step { get; set; } = string.Empty;
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("durationMs")] public long DurationMs { get; set; }
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonProperty("outputPath")] public string? OutputPath { get; set; }
    [JsonProperty("metadata")] public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Complete post-processing result with all step outcomes
/// </summary>
public class PostProcessingResult
{
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("totalDurationMs")] public long TotalDurationMs { get; set; }
    [JsonProperty("outputDirectory")] public string OutputDirectory { get; set; } = string.Empty;
    [JsonProperty("steps")] public List<PostProcessingStepResult> Steps { get; set; } = [];
    [JsonProperty("errors")] public List<string> Errors { get; set; } = [];
    [JsonProperty("warnings")] public List<string> Warnings { get; set; } = [];

    // Aggregated results from individual processors
    [JsonProperty("fontExtraction")] public FontExtractionResult? FontExtraction { get; set; }
    [JsonProperty("chapterExtraction")] public ChapterExtractionResult? ChapterExtraction { get; set; }
    [JsonProperty("spriteGeneration")] public SpriteGenerationResult? SpriteGeneration { get; set; }
    [JsonProperty("validation")] public OutputValidationResult? Validation { get; set; }
    [JsonProperty("masterPlaylistPath")] public string? MasterPlaylistPath { get; set; }

    public int TotalSteps => Steps.Count;
    public int SuccessfulSteps => Steps.Count(s => s.Success);
    public int FailedSteps => Steps.Count(s => !s.Success);
}

/// <summary>
/// Configuration options for post-processing
/// </summary>
public class PostProcessingOptions
{
    /// <summary>
    /// Enable font extraction from media file
    /// </summary>
    public bool ExtractFonts { get; set; } = true;

    /// <summary>
    /// Enable chapter extraction and WebVTT generation
    /// </summary>
    public bool ExtractChapters { get; set; } = true;

    /// <summary>
    /// Enable thumbnail sprite sheet generation
    /// </summary>
    public bool GenerateSprites { get; set; } = true;

    /// <summary>
    /// Enable output validation
    /// </summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>
    /// Enable master playlist generation for HLS
    /// </summary>
    public bool GenerateMasterPlaylist { get; set; } = true;

    /// <summary>
    /// Continue processing even if individual steps fail
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Sprite thumbnail width in pixels
    /// </summary>
    public int SpriteWidth { get; set; } = 320;

    /// <summary>
    /// Sprite thumbnail height (null to preserve aspect ratio)
    /// </summary>
    public int? SpriteHeight { get; set; }

    /// <summary>
    /// Interval between sprite thumbnails in seconds
    /// </summary>
    public double SpriteIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Expected duration for validation (null to skip duration check)
    /// </summary>
    public TimeSpan? ExpectedDuration { get; set; }

    /// <summary>
    /// Container format for playlist generation
    /// </summary>
    public string ContainerFormat { get; set; } = "hls";

    /// <summary>
    /// Base filename for output files (without extension)
    /// </summary>
    public string? BaseFilename { get; set; }

    /// <summary>
    /// Callback for progress updates (stepName, currentStep, totalSteps)
    /// </summary>
    public Action<string, int, int>? ProgressCallback { get; set; }
}

#endregion

/// <summary>
/// Interface for post-processing orchestration after encoding completes
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Executes all enabled post-processing steps
    /// </summary>
    /// <param name="inputFilePath">Path to the source media file</param>
    /// <param name="outputDirectory">Directory containing encoded output</param>
    /// <param name="options">Post-processing configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated result from all post-processing steps</returns>
    Task<PostProcessingResult> ProcessAsync(
        string inputFilePath,
        string outputDirectory,
        PostProcessingOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts fonts only
    /// </summary>
    Task<FontExtractionResult> ExtractFontsAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts chapters only
    /// </summary>
    Task<ChapterExtractionResult> ExtractChaptersAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates sprites only
    /// </summary>
    Task<SpriteGenerationResult> GenerateSpritesAsync(
        string inputFilePath,
        string outputDirectory,
        int width = 320,
        int? height = null,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates output only
    /// </summary>
    Task<OutputValidationResult> ValidateOutputAsync(
        string outputDirectory,
        TimeSpan expectedDuration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates post-processing tasks after encoding completes.
/// Coordinates font extraction, chapter extraction, sprite generation,
/// output validation, and master playlist generation.
/// </summary>
public class PostProcessor : IPostProcessor
{
    private readonly IFontExtractor _fontExtractor;
    private readonly IChapterProcessor _chapterProcessor;
    private readonly ISpriteGenerator _spriteGenerator;
    private readonly IOutputValidator _outputValidator;
    private readonly IHLSPlaylistGenerator _playlistGenerator;

    public PostProcessor(
        IFontExtractor fontExtractor,
        IChapterProcessor chapterProcessor,
        ISpriteGenerator spriteGenerator,
        IOutputValidator outputValidator,
        IHLSPlaylistGenerator playlistGenerator)
    {
        _fontExtractor = fontExtractor;
        _chapterProcessor = chapterProcessor;
        _spriteGenerator = spriteGenerator;
        _outputValidator = outputValidator;
        _playlistGenerator = playlistGenerator;
    }

    public async Task<PostProcessingResult> ProcessAsync(
        string inputFilePath,
        string outputDirectory,
        PostProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PostProcessingOptions();
        System.Diagnostics.Stopwatch totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        PostProcessingResult result = new()
        {
            OutputDirectory = outputDirectory
        };

        // Validate input file exists
        if (!File.Exists(inputFilePath))
        {
            result.Success = false;
            result.Errors.Add($"Input file not found: {inputFilePath}");
            return result;
        }

        // Ensure output directory exists
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        int currentStep = 0;
        int totalSteps = CountEnabledSteps(options);

        try
        {
            // Step 1: Font Extraction
            if (options.ExtractFonts)
            {
                currentStep++;
                options.ProgressCallback?.Invoke("Extracting fonts", currentStep, totalSteps);

                PostProcessingStepResult fontStep = await ExecuteStepAsync(
                    "FontExtraction",
                    async () =>
                    {
                        FontExtractionResult fontResult = await _fontExtractor.ExtractFontsAsync(
                            inputFilePath, outputDirectory, cancellationToken);
                        result.FontExtraction = fontResult;
                        return (fontResult.Success, fontResult.ErrorMessage, fontResult.OutputDirectory,
                            new Dictionary<string, object> { ["fontsExtracted"] = fontResult.TotalFontsExtracted });
                    },
                    cancellationToken);

                result.Steps.Add(fontStep);

                if (!fontStep.Success && !options.ContinueOnError)
                {
                    result.Success = false;
                    result.Errors.Add($"Font extraction failed: {fontStep.Message}");
                    return FinalizeResult(result, totalStopwatch);
                }
            }

            // Step 2: Chapter Extraction
            if (options.ExtractChapters)
            {
                currentStep++;
                options.ProgressCallback?.Invoke("Extracting chapters", currentStep, totalSteps);

                PostProcessingStepResult chapterStep = await ExecuteStepAsync(
                    "ChapterExtraction",
                    async () =>
                    {
                        ChapterExtractionResult chapterResult = await _chapterProcessor.ExtractChaptersAsync(
                            inputFilePath, outputDirectory, cancellationToken);
                        result.ChapterExtraction = chapterResult;
                        return (chapterResult.Success, chapterResult.ErrorMessage, chapterResult.OutputPath,
                            new Dictionary<string, object> { ["chaptersExtracted"] = chapterResult.TotalChapters });
                    },
                    cancellationToken);

                result.Steps.Add(chapterStep);

                if (!chapterStep.Success && !options.ContinueOnError)
                {
                    result.Success = false;
                    result.Errors.Add($"Chapter extraction failed: {chapterStep.Message}");
                    return FinalizeResult(result, totalStopwatch);
                }
            }

            // Step 3: Sprite Generation
            if (options.GenerateSprites)
            {
                currentStep++;
                options.ProgressCallback?.Invoke("Generating sprites", currentStep, totalSteps);

                PostProcessingStepResult spriteStep = await ExecuteStepAsync(
                    "SpriteGeneration",
                    async () =>
                    {
                        SpriteGenerationResult spriteResult = await _spriteGenerator.GenerateSpriteAsync(
                            inputFilePath,
                            outputDirectory,
                            options.SpriteWidth,
                            options.SpriteHeight,
                            options.SpriteIntervalSeconds,
                            cancellationToken);
                        result.SpriteGeneration = spriteResult;
                        return (spriteResult.Success, spriteResult.ErrorMessage, spriteResult.SpriteFilePath,
                            new Dictionary<string, object>
                            {
                                ["framesGenerated"] = spriteResult.TotalFrames,
                                ["vttPath"] = spriteResult.VttFilePath
                            });
                    },
                    cancellationToken);

                result.Steps.Add(spriteStep);

                if (!spriteStep.Success && !options.ContinueOnError)
                {
                    result.Success = false;
                    result.Errors.Add($"Sprite generation failed: {spriteStep.Message}");
                    return FinalizeResult(result, totalStopwatch);
                }
            }

            // Step 4: Output Validation
            if (options.ValidateOutput && options.ExpectedDuration.HasValue)
            {
                currentStep++;
                options.ProgressCallback?.Invoke("Validating output", currentStep, totalSteps);

                PostProcessingStepResult validationStep = await ExecuteStepAsync(
                    "OutputValidation",
                    async () =>
                    {
                        OutputValidationResult validationResult = await ValidateOutputDirectoryAsync(
                            outputDirectory, options.ExpectedDuration.Value, cancellationToken);
                        result.Validation = validationResult;

                        // Add warnings from validation to result
                        if (validationResult.Warnings.Count > 0)
                        {
                            result.Warnings.AddRange(validationResult.Warnings);
                        }

                        return (validationResult.IsValid,
                            validationResult.Errors.Count > 0 ? string.Join("; ", validationResult.Errors) : null,
                            outputDirectory,
                            new Dictionary<string, object>
                            {
                                ["filesValidated"] = Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories).Length,
                                ["durationDifferenceSeconds"] = validationResult.DurationDifferenceSeconds
                            });
                    },
                    cancellationToken);

                result.Steps.Add(validationStep);

                if (!validationStep.Success && !options.ContinueOnError)
                {
                    result.Success = false;
                    result.Errors.Add($"Output validation failed: {validationStep.Message}");
                    return FinalizeResult(result, totalStopwatch);
                }
            }

            // Step 5: Master Playlist Generation (HLS only)
            if (options.GenerateMasterPlaylist &&
                options.ContainerFormat.Equals("hls", StringComparison.OrdinalIgnoreCase))
            {
                currentStep++;
                options.ProgressCallback?.Invoke("Generating master playlist", currentStep, totalSteps);

                PostProcessingStepResult playlistStep = await ExecuteStepAsync(
                    "MasterPlaylistGeneration",
                    async () =>
                    {
                        string baseFilename = options.BaseFilename ?? Path.GetFileNameWithoutExtension(inputFilePath);
                        string masterPlaylistPath = Path.Combine(outputDirectory, $"{baseFilename}.m3u8");

                        // Generate master playlist from existing media playlists
                        (bool success, string? error) = await GenerateMasterPlaylistFromDirectoryAsync(
                            outputDirectory, masterPlaylistPath, cancellationToken);

                        if (success)
                        {
                            result.MasterPlaylistPath = masterPlaylistPath;
                        }

                        return (success, error, masterPlaylistPath,
                            new Dictionary<string, object> { ["playlistPath"] = masterPlaylistPath });
                    },
                    cancellationToken);

                result.Steps.Add(playlistStep);

                if (!playlistStep.Success && !options.ContinueOnError)
                {
                    result.Success = false;
                    result.Errors.Add($"Master playlist generation failed: {playlistStep.Message}");
                    return FinalizeResult(result, totalStopwatch);
                }
            }

            // Determine overall success
            result.Success = result.Steps.All(s => s.Success) || result.FailedSteps == 0;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Errors.Add("Post-processing was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Unexpected error during post-processing: {ex.Message}");
        }

        return FinalizeResult(result, totalStopwatch);
    }

    public async Task<FontExtractionResult> ExtractFontsAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        return await _fontExtractor.ExtractFontsAsync(inputFilePath, outputDirectory, cancellationToken);
    }

    public async Task<ChapterExtractionResult> ExtractChaptersAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        return await _chapterProcessor.ExtractChaptersAsync(inputFilePath, outputDirectory, cancellationToken);
    }

    public async Task<SpriteGenerationResult> GenerateSpritesAsync(
        string inputFilePath,
        string outputDirectory,
        int width = 320,
        int? height = null,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        return await _spriteGenerator.GenerateSpriteAsync(
            inputFilePath, outputDirectory, width, height, intervalSeconds, cancellationToken);
    }

    public async Task<OutputValidationResult> ValidateOutputAsync(
        string outputDirectory,
        TimeSpan expectedDuration,
        CancellationToken cancellationToken = default)
    {
        return await ValidateOutputDirectoryAsync(outputDirectory, expectedDuration, cancellationToken);
    }

    #region Private Helper Methods

    private static int CountEnabledSteps(PostProcessingOptions options)
    {
        int count = 0;
        if (options.ExtractFonts) count++;
        if (options.ExtractChapters) count++;
        if (options.GenerateSprites) count++;
        if (options.ValidateOutput && options.ExpectedDuration.HasValue) count++;
        if (options.GenerateMasterPlaylist && options.ContainerFormat.Equals("hls", StringComparison.OrdinalIgnoreCase)) count++;
        return count;
    }

    private static async Task<PostProcessingStepResult> ExecuteStepAsync(
        string stepName,
        Func<Task<(bool success, string? error, string? outputPath, Dictionary<string, object> metadata)>> action,
        CancellationToken cancellationToken)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        PostProcessingStepResult stepResult = new()
        {
            Step = stepName
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            (bool success, string? error, string? outputPath, Dictionary<string, object> metadata) = await action();

            stepResult.Success = success;
            stepResult.Message = error;
            stepResult.OutputPath = outputPath;
            stepResult.Metadata = metadata;
        }
        catch (OperationCanceledException)
        {
            stepResult.Success = false;
            stepResult.Message = "Step was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            stepResult.Success = false;
            stepResult.Message = ex.Message;
        }

        stopwatch.Stop();
        stepResult.DurationMs = stopwatch.ElapsedMilliseconds;
        return stepResult;
    }

    private async Task<OutputValidationResult> ValidateOutputDirectoryAsync(
        string outputDirectory,
        TimeSpan expectedDuration,
        CancellationToken cancellationToken)
    {
        OutputValidationResult aggregatedResult = new()
        {
            IsValid = true,
            FileExists = true,
            ExpectedDuration = expectedDuration
        };

        // Find master playlist or first media playlist
        string[] playlists = Directory.GetFiles(outputDirectory, "*.m3u8", SearchOption.AllDirectories);

        if (playlists.Length == 0)
        {
            // No playlists found, try to validate video files directly
            string[] videoFiles = Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(outputDirectory, "*.mkv", SearchOption.AllDirectories))
                .ToArray();

            if (videoFiles.Length == 0)
            {
                aggregatedResult.IsValid = false;
                aggregatedResult.Errors.Add("No playlist or video files found in output directory");
                return aggregatedResult;
            }

            // Validate first video file
            OutputValidationResult videoResult = await _outputValidator.ValidateOutputAsync(
                videoFiles[0], expectedDuration);

            return videoResult;
        }

        // Find master playlist (usually at root level)
        string? masterPlaylist = playlists.FirstOrDefault(p =>
            Path.GetDirectoryName(p) == outputDirectory);

        if (masterPlaylist != null)
        {
            OutputValidationResult playlistResult = await _outputValidator.ValidatePlaylistAsync(masterPlaylist);
            if (!playlistResult.IsValid)
            {
                aggregatedResult.IsValid = false;
                aggregatedResult.Errors.AddRange(playlistResult.Errors);
            }
            aggregatedResult.Warnings.AddRange(playlistResult.Warnings);
        }

        // Validate individual media playlists
        foreach (string playlist in playlists.Where(p => p != masterPlaylist))
        {
            cancellationToken.ThrowIfCancellationRequested();

            OutputValidationResult playlistResult = await _outputValidator.ValidatePlaylistAsync(playlist);
            if (!playlistResult.IsValid)
            {
                aggregatedResult.Warnings.Add($"Playlist validation warning for {Path.GetFileName(playlist)}: {string.Join("; ", playlistResult.Errors)}");
            }
        }

        return aggregatedResult;
    }

    private async Task<(bool success, string? error)> GenerateMasterPlaylistFromDirectoryAsync(
        string outputDirectory,
        string masterPlaylistPath,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find all variant playlists (exclude the master playlist itself)
            string[] variantPlaylists = Directory.GetFiles(outputDirectory, "*.m3u8", SearchOption.AllDirectories)
                .Where(p => p != masterPlaylistPath && !Path.GetFileName(p).Equals(Path.GetFileName(masterPlaylistPath)))
                .ToArray();

            if (variantPlaylists.Length == 0)
            {
                return (false, "No variant playlists found in output directory");
            }

            List<HLSVariantStream> variants = [];
            List<HLSMediaGroup> mediaGroups = [];

            foreach (string playlistPath in variantPlaylists)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(outputDirectory, playlistPath);
                string folderName = Path.GetDirectoryName(relativePath) ?? "";

                // Determine if this is a video or audio stream based on folder name
                if (folderName.StartsWith("video_", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse resolution from folder name (e.g., video_1920x1080_SDR)
                    (int width, int height, bool isHdr) = ParseVideoFolderName(folderName);
                    int bandwidth = EstimateBandwidth(width, height, isHdr);

                    HLSVariantStream variant = new()
                    {
                        Bandwidth = bandwidth,
                        Resolution = $"{width}x{height}",
                        Codecs = isHdr ? "hvc1.1.6.L120.90,mp4a.40.2" : "avc1.640028,mp4a.40.2",
                        PlaylistUri = relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                        AudioGroup = "audio"
                    };
                    variants.Add(variant);
                }
                else if (folderName.StartsWith("audio_", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse language and codec from folder name (e.g., audio_eng_aac)
                    (string language, string codec) = ParseAudioFolderName(folderName);

                    HLSMediaGroup mediaGroup = new()
                    {
                        Type = "AUDIO",
                        GroupId = "audio",
                        Name = $"{language.ToUpper()} - {codec.ToUpper()}",
                        Language = language,
                        IsDefault = mediaGroups.Count == 0, // First audio is default
                        Autoselect = true,
                        Uri = relativePath.Replace(Path.DirectorySeparatorChar, '/')
                    };
                    mediaGroups.Add(mediaGroup);
                }
            }

            // Sort variants by bandwidth (ascending)
            variants = variants.OrderBy(v => v.Bandwidth).ToList();

            // Generate master playlist
            await _playlistGenerator.WriteMasterPlaylistAsync(masterPlaylistPath, variants, mediaGroups);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to generate master playlist: {ex.Message}");
        }
    }

    private static (int width, int height, bool isHdr) ParseVideoFolderName(string folderName)
    {
        // Expected format: video_1920x1080_SDR or video_1920x1080_HDR
        int width = 1920;
        int height = 1080;
        bool isHdr = false;

        string[] parts = folderName.Split('_');
        if (parts.Length >= 2)
        {
            string resolution = parts[1];
            string[] dimensions = resolution.Split('x');
            if (dimensions.Length == 2)
            {
                int.TryParse(dimensions[0], out width);
                int.TryParse(dimensions[1], out height);
            }
        }

        if (parts.Length >= 3)
        {
            isHdr = parts[2].Equals("HDR", StringComparison.OrdinalIgnoreCase);
        }

        return (width, height, isHdr);
    }

    private static (string language, string codec) ParseAudioFolderName(string folderName)
    {
        // Expected format: audio_eng_aac
        string language = "und";
        string codec = "aac";

        string[] parts = folderName.Split('_');
        if (parts.Length >= 2)
        {
            language = parts[1];
        }
        if (parts.Length >= 3)
        {
            codec = parts[2];
        }

        return (language, codec);
    }

    private static int EstimateBandwidth(int width, int height, bool isHdr)
    {
        // Rough bandwidth estimation based on resolution
        int baseBandwidth = (width, height) switch
        {
            ( >= 3840, >= 2160) => 25_000_000, // 4K
            ( >= 2560, >= 1440) => 12_000_000, // 1440p
            ( >= 1920, >= 1080) => 8_000_000,  // 1080p
            ( >= 1280, >= 720) => 5_000_000,   // 720p
            _ => 3_000_000                      // SD
        };

        // HDR content typically has higher bitrate
        if (isHdr)
        {
            baseBandwidth = (int)(baseBandwidth * 1.3);
        }

        return baseBandwidth;
    }

    private static PostProcessingResult FinalizeResult(
        PostProcessingResult result,
        System.Diagnostics.Stopwatch stopwatch)
    {
        stopwatch.Stop();
        result.TotalDurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    #endregion
}
