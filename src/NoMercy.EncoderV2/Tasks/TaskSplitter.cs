using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder.Dto;
using NoMercy.EncoderV2.Core;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Splits encoding jobs into distributable tasks for parallel/distributed processing.
/// Implements various splitting strategies and handles task dependencies.
/// </summary>
/// <remarks>
/// Key responsibilities:
/// - Decompose jobs into individual tasks (video, audio, subtitle, post-processing)
/// - Calculate task weights for load balancing
/// - Manage task dependencies (e.g., HDR conversion before video encoding)
/// - Convert task definitions to database entities
/// </remarks>
public class TaskSplitter : ITaskSplitter
{
    private const double HdrConversionWeight = 2.0;
    private const double FontExtractionWeight = 0.05;
    private const double ChapterExtractionWeight = 0.02;
    private const double SpriteGenerationWeight = 0.3;
    private const double ValidationWeight = 0.1;
    private const double PlaylistGenerationWeight = 0.05;
    private const double SubtitleExtractionWeight = 0.1;

    /// <inheritdoc />
    public TaskSplitResult SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy = TaskDistributionStrategy.Optimal,
        TaskSplittingOptions? options = null)
    {
        options ??= new TaskSplittingOptions();

        // Determine actual strategy if Optimal is selected
        TaskDistributionStrategy actualStrategy = strategy == TaskDistributionStrategy.Optimal
            ? DetermineOptimalStrategy(analysis, profile)
            : strategy;

        List<EncodingTaskDefinition> tasks = actualStrategy switch
        {
            TaskDistributionStrategy.SingleTask => CreateSingleTaskSplit(analysis, profile, options),
            TaskDistributionStrategy.ByResolution => CreateResolutionSplit(analysis, profile, options),
            TaskDistributionStrategy.ByStreamType => CreateStreamTypeSplit(analysis, profile, options),
            TaskDistributionStrategy.BySegment => CreateSegmentSplit(analysis, profile, options),
            _ => CreateSingleTaskSplit(analysis, profile, options)
        };

        // Calculate result metadata
        double totalWeight = tasks.Sum(t => t.Weight);
        int maxParallelism = CalculateMaxParallelism(tasks);
        int criticalPath = CalculateCriticalPathLength(tasks);
        bool hasSharedHdr = tasks.Any(t => t.TaskType == EncodingTaskType.HdrConversion);

        return new TaskSplitResult
        {
            Tasks = tasks,
            TotalWeight = totalWeight,
            UsedStrategy = actualStrategy,
            MaxParallelism = maxParallelism,
            HasSharedHdrConversion = hasSharedHdr,
            CriticalPathLength = criticalPath
        };
    }

    /// <inheritdoc />
    public List<EncodingTaskDefinition> SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy)
    {
        return SplitJob(analysis, profile, strategy, null).Tasks;
    }

    /// <inheritdoc />
    public double CalculateTaskWeight(EncodingTaskDefinition task, StreamAnalysis analysis)
    {
        return task.TaskType switch
        {
            EncodingTaskType.HdrConversion => CalculateHdrConversionWeight(analysis),
            EncodingTaskType.VideoEncoding => CalculateVideoEncodingWeight(task, analysis),
            EncodingTaskType.AudioEncoding => CalculateAudioEncodingWeight(task, analysis),
            EncodingTaskType.SubtitleExtraction => SubtitleExtractionWeight * (analysis.Duration.TotalMinutes / 60.0),
            EncodingTaskType.FontExtraction => FontExtractionWeight,
            EncodingTaskType.ChapterExtraction => ChapterExtractionWeight,
            EncodingTaskType.SpriteGeneration => SpriteGenerationWeight * (analysis.Duration.TotalMinutes / 60.0),
            EncodingTaskType.ThumbnailGeneration => SpriteGenerationWeight * (analysis.Duration.TotalMinutes / 60.0),
            EncodingTaskType.PlaylistGeneration => PlaylistGenerationWeight,
            EncodingTaskType.Validation => ValidationWeight,
            _ => 1.0
        };
    }

    /// <inheritdoc />
    public TaskDistributionStrategy DetermineOptimalStrategy(StreamAnalysis analysis, EncoderProfile profile)
    {
        int videoProfileCount = profile.VideoProfiles?.Length ?? 0;
        int audioProfileCount = profile.AudioProfiles?.Length ?? 0;
        double durationMinutes = analysis.Duration.TotalMinutes;
        bool isHls = profile.Container?.Equals("hls", StringComparison.OrdinalIgnoreCase) == true ||
                     profile.Container?.Equals("m3u8", StringComparison.OrdinalIgnoreCase) == true;

        // For very long content (> 2 hours) with HLS, segment splitting can be beneficial
        if (isHls && durationMinutes > 120 && videoProfileCount <= 2)
        {
            return TaskDistributionStrategy.BySegment;
        }

        // For multi-quality encoding (3+ resolutions), split by resolution
        if (videoProfileCount >= 3)
        {
            return TaskDistributionStrategy.ByResolution;
        }

        // For content with multiple audio tracks or when video/audio can process independently
        if (audioProfileCount >= 2 || (videoProfileCount >= 2 && audioProfileCount >= 1))
        {
            return TaskDistributionStrategy.ByStreamType;
        }

        // For simple jobs, use single task
        if (videoProfileCount <= 1 && audioProfileCount <= 1)
        {
            return TaskDistributionStrategy.SingleTask;
        }

        // Default to ByResolution for multi-output scenarios
        return TaskDistributionStrategy.ByResolution;
    }

    /// <inheritdoc />
    public List<EncodingTask> ToEncodingTasks(Ulid jobId, List<EncodingTaskDefinition> tasks)
    {
        // Build ID mapping for dependency resolution
        Dictionary<string, Ulid> idMapping = tasks.ToDictionary(
            t => t.Id,
            t => Ulid.NewUlid()
        );

        List<EncodingTask> result = [];

        foreach (EncodingTaskDefinition taskDef in tasks)
        {
            Ulid taskId = idMapping[taskDef.Id];

            // Resolve dependencies to actual Ulid values
            string[] dependencies = taskDef.Dependencies
                .Where(d => idMapping.ContainsKey(d))
                .Select(d => idMapping[d].ToString())
                .ToArray();

            EncodingTask encodingTask = new()
            {
                Id = taskId,
                JobId = jobId,
                TaskType = taskDef.TaskType,
                Weight = (int)Math.Ceiling(taskDef.Weight * 100), // Scale to integer
                State = EncodingTaskState.Pending,
                Dependencies = dependencies,
                CommandArgsJson = JsonConvert.SerializeObject(taskDef.Parameters),
                OutputFile = taskDef.OutputPath
            };

            result.Add(encodingTask);
        }

        return result;
    }

    #region Strategy Implementations

    private List<EncodingTaskDefinition> CreateSingleTaskSplit(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskSplittingOptions options)
    {
        List<EncodingTaskDefinition> tasks = [];

        // Single combined encoding task
        EncodingTaskDefinition mainTask = new()
        {
            TaskType = EncodingTaskType.VideoEncoding,
            Description = "Full encode (video + audio + subtitles)",
            Weight = TaskWeighting.ComputeJobWeight(analysis, profile),
            Parameters = new Dictionary<string, object>
            {
                ["ProfileId"] = profile.Id.ToString(),
                ["Duration"] = analysis.Duration.TotalSeconds,
                ["IsHDR"] = analysis.IsHDR,
                ["HasSubtitles"] = analysis.HasSubtitles,
                ["VideoProfileCount"] = profile.VideoProfiles?.Length ?? 0,
                ["AudioProfileCount"] = profile.AudioProfiles?.Length ?? 0
            }
        };

        tasks.Add(mainTask);

        // Add post-processing tasks
        if (options.IncludePostProcessing)
        {
            AddPostProcessingTasks(tasks, analysis, profile, [mainTask.Id]);
        }

        return tasks;
    }

    private List<EncodingTaskDefinition> CreateResolutionSplit(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskSplittingOptions options)
    {
        List<EncodingTaskDefinition> tasks = [];
        List<string> videoTaskIds = [];
        string? hdrTaskId = null;

        // Create HDR conversion task if needed (shared across all video tasks)
        if (options.ShareHdrConversion && analysis.IsHDR && HasSdrOutputs(profile))
        {
            EncodingTaskDefinition hdrTask = CreateHdrConversionTask(analysis);
            tasks.Add(hdrTask);
            hdrTaskId = hdrTask.Id;
        }

        // Create video encoding tasks per resolution
        if (profile.VideoProfiles != null)
        {
            foreach (IVideoProfile videoProfile in profile.VideoProfiles)
            {
                List<string> dependencies = hdrTaskId != null && videoProfile.ConvertHdrToSdr
                    ? [hdrTaskId]
                    : [];

                EncodingTaskDefinition videoTask = new()
                {
                    TaskType = EncodingTaskType.VideoEncoding,
                    Description = $"Video encode {videoProfile.Width}x{videoProfile.Height} ({videoProfile.Codec})",
                    Weight = TaskWeighting.ComputeVideoProfileWeight(videoProfile, analysis),
                    Dependencies = dependencies,
                    RequiresGpu = IsHardwareCodec(videoProfile.Codec),
                    EstimatedMemoryMb = EstimateVideoMemory(videoProfile),
                    OutputPath = $"video_{videoProfile.Width}x{videoProfile.Height}/",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Codec"] = videoProfile.Codec,
                        ["Width"] = videoProfile.Width,
                        ["Height"] = videoProfile.Height,
                        ["Bitrate"] = videoProfile.Bitrate,
                        ["Crf"] = videoProfile.Crf,
                        ["Preset"] = videoProfile.Preset,
                        ["Profile"] = videoProfile.Profile,
                        ["ConvertHdrToSdr"] = videoProfile.ConvertHdrToSdr,
                        ["Duration"] = analysis.Duration.TotalSeconds
                    }
                };

                tasks.Add(videoTask);
                videoTaskIds.Add(videoTask.Id);
            }
        }

        // Create single audio encoding task (runs in parallel with video)
        if (profile.AudioProfiles != null && profile.AudioProfiles.Length > 0)
        {
            AddAudioTasks(tasks, analysis, profile, options, []);
        }

        // Create subtitle extraction task
        if (profile.SubtitleProfiles != null && profile.SubtitleProfiles.Length > 0 && analysis.HasSubtitles)
        {
            AddSubtitleTask(tasks, analysis, profile, []);
        }

        // Add post-processing tasks (depend on all encoding tasks)
        if (options.IncludePostProcessing)
        {
            List<string> allEncodingTaskIds = tasks.Select(t => t.Id).ToList();
            AddPostProcessingTasks(tasks, analysis, profile, allEncodingTaskIds);
        }

        return tasks;
    }

    private List<EncodingTaskDefinition> CreateStreamTypeSplit(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskSplittingOptions options)
    {
        List<EncodingTaskDefinition> tasks = [];
        string? hdrTaskId = null;

        // Create HDR conversion task if needed
        if (options.ShareHdrConversion && analysis.IsHDR && HasSdrOutputs(profile))
        {
            EncodingTaskDefinition hdrTask = CreateHdrConversionTask(analysis);
            tasks.Add(hdrTask);
            hdrTaskId = hdrTask.Id;
        }

        // All video profiles in one task
        if (profile.VideoProfiles != null && profile.VideoProfiles.Length > 0)
        {
            List<string> dependencies = hdrTaskId != null ? [hdrTaskId] : [];

            double totalVideoWeight = profile.VideoProfiles
                .Sum(vp => TaskWeighting.ComputeVideoProfileWeight(vp, analysis));

            EncodingTaskDefinition videoTask = new()
            {
                TaskType = EncodingTaskType.VideoEncoding,
                Description = $"Video encode ({profile.VideoProfiles.Length} qualities)",
                Weight = totalVideoWeight,
                Dependencies = dependencies,
                RequiresGpu = profile.VideoProfiles.Any(vp => IsHardwareCodec(vp.Codec)),
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileCount"] = profile.VideoProfiles.Length,
                    ["Profiles"] = profile.VideoProfiles.Select(vp => new
                    {
                        vp.Codec,
                        vp.Width,
                        vp.Height,
                        vp.Bitrate,
                        vp.Crf
                    }).ToList(),
                    ["Duration"] = analysis.Duration.TotalSeconds
                }
            };

            tasks.Add(videoTask);
        }

        // Audio encoding (can run in parallel with video)
        if (profile.AudioProfiles != null && profile.AudioProfiles.Length > 0)
        {
            AddAudioTasks(tasks, analysis, profile, options, []);
        }

        // Subtitle extraction (can run in parallel)
        if (profile.SubtitleProfiles != null && profile.SubtitleProfiles.Length > 0 && analysis.HasSubtitles)
        {
            AddSubtitleTask(tasks, analysis, profile, []);
        }

        // Post-processing
        if (options.IncludePostProcessing)
        {
            List<string> allEncodingTaskIds = tasks.Select(t => t.Id).ToList();
            AddPostProcessingTasks(tasks, analysis, profile, allEncodingTaskIds);
        }

        return tasks;
    }

    private List<EncodingTaskDefinition> CreateSegmentSplit(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskSplittingOptions options)
    {
        List<EncodingTaskDefinition> tasks = [];

        // Segment splitting only makes sense for HLS
        string? container = profile.Container?.ToLowerInvariant();
        if (container != "hls" && container != "m3u8")
        {
            return CreateResolutionSplit(analysis, profile, options);
        }

        // Create HDR conversion task if needed
        string? hdrTaskId = null;
        if (options.ShareHdrConversion && analysis.IsHDR && HasSdrOutputs(profile))
        {
            EncodingTaskDefinition hdrTask = CreateHdrConversionTask(analysis);
            tasks.Add(hdrTask);
            hdrTaskId = hdrTask.Id;
        }

        // Calculate segment count
        int totalSeconds = (int)analysis.Duration.TotalSeconds;
        int segmentDuration = Math.Max(options.MinSegmentDuration, 30);
        int segmentCount = Math.Min(
            (int)Math.Ceiling((double)totalSeconds / segmentDuration),
            options.MaxSegments
        );

        // Adjust segment duration if we hit max segments
        if (segmentCount == options.MaxSegments)
        {
            segmentDuration = (int)Math.Ceiling((double)totalSeconds / segmentCount);
        }

        List<string> segmentTaskIds = [];

        // Create segment encoding tasks
        for (int i = 0; i < segmentCount; i++)
        {
            int startTime = i * segmentDuration;
            int duration = Math.Min(segmentDuration, totalSeconds - startTime);

            if (duration <= 0) break;

            List<string> dependencies = hdrTaskId != null ? [hdrTaskId] : [];

            // Weight proportional to segment duration
            double segmentWeight = (duration / 60.0) * (profile.VideoProfiles?.Length ?? 1);

            EncodingTaskDefinition segmentTask = new()
            {
                TaskType = EncodingTaskType.VideoEncoding,
                Description = $"Segment {i + 1}/{segmentCount} ({TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss})",
                Weight = segmentWeight,
                Dependencies = dependencies,
                Parameters = new Dictionary<string, object>
                {
                    ["SegmentIndex"] = i,
                    ["StartTime"] = startTime,
                    ["Duration"] = duration,
                    ["TotalSegments"] = segmentCount,
                    ["VideoProfiles"] = profile.VideoProfiles?.Length ?? 0
                }
            };

            tasks.Add(segmentTask);
            segmentTaskIds.Add(segmentTask.Id);
        }

        // Audio encoding (single task, doesn't benefit from segmentation as much)
        if (profile.AudioProfiles != null && profile.AudioProfiles.Length > 0)
        {
            AddAudioTasks(tasks, analysis, profile, options, []);
        }

        // Subtitle extraction
        if (profile.SubtitleProfiles != null && profile.SubtitleProfiles.Length > 0 && analysis.HasSubtitles)
        {
            AddSubtitleTask(tasks, analysis, profile, []);
        }

        // Post-processing depends on all encoding tasks
        if (options.IncludePostProcessing)
        {
            List<string> allEncodingTaskIds = tasks.Select(t => t.Id).ToList();
            AddPostProcessingTasks(tasks, analysis, profile, allEncodingTaskIds);
        }

        return tasks;
    }

    #endregion

    #region Helper Methods

    private EncodingTaskDefinition CreateHdrConversionTask(StreamAnalysis analysis)
    {
        return new EncodingTaskDefinition
        {
            TaskType = EncodingTaskType.HdrConversion,
            Description = "HDR to SDR conversion (shared)",
            Weight = CalculateHdrConversionWeight(analysis),
            RequiresGpu = true, // GPU tone mapping is much faster
            EstimatedMemoryMb = 4096, // HDR processing is memory-intensive
            OutputPath = "_hdr_cache/",
            Parameters = new Dictionary<string, object>
            {
                ["SourceIsHDR"] = true,
                ["ToneMapAlgorithm"] = "hable",
                ["Duration"] = analysis.Duration.TotalSeconds,
                ["Width"] = analysis.PrimaryVideoStream?.Width ?? 1920,
                ["Height"] = analysis.PrimaryVideoStream?.Height ?? 1080
            }
        };
    }

    private void AddAudioTasks(
        List<EncodingTaskDefinition> tasks,
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskSplittingOptions options,
        List<string> dependencies)
    {
        if (profile.AudioProfiles == null || profile.AudioProfiles.Length == 0)
            return;

        if (options.SplitAudioByLanguage && analysis.AudioStreams.Count > 1)
        {
            // Create separate tasks per language
            HashSet<string> processedLanguages = [];

            foreach (AudioStream audioStream in analysis.AudioStreams)
            {
                string language = audioStream.Language ?? "und";
                if (processedLanguages.Contains(language))
                    continue;

                processedLanguages.Add(language);

                double weight = TaskWeighting.ComputeAudioProfileWeight(
                    profile.AudioProfiles[0],
                    analysis.Duration
                );

                EncodingTaskDefinition audioTask = new()
                {
                    TaskType = EncodingTaskType.AudioEncoding,
                    Description = $"Audio encode ({language})",
                    Weight = weight,
                    Dependencies = [..dependencies],
                    OutputPath = $"audio_{language}/",
                    Parameters = new Dictionary<string, object>
                    {
                        ["Language"] = language,
                        ["Codec"] = profile.AudioProfiles[0].Codec,
                        ["Channels"] = profile.AudioProfiles[0].Channels,
                        ["SampleRate"] = profile.AudioProfiles[0].SampleRate,
                        ["Duration"] = analysis.Duration.TotalSeconds
                    }
                };

                tasks.Add(audioTask);
            }
        }
        else
        {
            // Single audio task for all tracks
            double totalWeight = profile.AudioProfiles
                .Sum(ap => TaskWeighting.ComputeAudioProfileWeight(ap, analysis.Duration));

            EncodingTaskDefinition audioTask = new()
            {
                TaskType = EncodingTaskType.AudioEncoding,
                Description = $"Audio encode ({profile.AudioProfiles.Length} profiles)",
                Weight = totalWeight,
                Dependencies = [..dependencies],
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileCount"] = profile.AudioProfiles.Length,
                    ["AudioStreamCount"] = analysis.AudioStreams.Count,
                    ["Duration"] = analysis.Duration.TotalSeconds
                }
            };

            tasks.Add(audioTask);
        }
    }

    private void AddSubtitleTask(
        List<EncodingTaskDefinition> tasks,
        StreamAnalysis analysis,
        EncoderProfile profile,
        List<string> dependencies)
    {
        EncodingTaskDefinition subtitleTask = new()
        {
            TaskType = EncodingTaskType.SubtitleExtraction,
            Description = $"Subtitle extraction ({analysis.SubtitleStreams.Count} tracks)",
            Weight = SubtitleExtractionWeight * Math.Max(1, analysis.SubtitleStreams.Count),
            Dependencies = [..dependencies],
            OutputPath = "subtitles/",
            Parameters = new Dictionary<string, object>
            {
                ["SubtitleCount"] = analysis.SubtitleStreams.Count,
                ["Languages"] = analysis.SubtitleStreams.Select(s => s.Language ?? "und").Distinct().ToList(),
                ["PreserveAss"] = true // Never auto-convert ASS per PRD
            }
        };

        tasks.Add(subtitleTask);
    }

    private void AddPostProcessingTasks(
        List<EncodingTaskDefinition> tasks,
        StreamAnalysis analysis,
        EncoderProfile profile,
        List<string> encodingTaskDependencies)
    {
        // Font extraction (from attachments or ASS subtitles)
        if (analysis.HasAttachments || analysis.HasSubtitles)
        {
            EncodingTaskDefinition fontTask = new()
            {
                TaskType = EncodingTaskType.FontExtraction,
                Description = "Font extraction",
                Weight = FontExtractionWeight,
                Dependencies = [], // Can run in parallel with encoding
                OutputPath = "fonts/",
                Parameters = new Dictionary<string, object>
                {
                    ["AttachmentCount"] = analysis.Attachments.Count,
                    ["HasSubtitles"] = analysis.HasSubtitles
                }
            };
            tasks.Add(fontTask);
        }

        // Chapter extraction
        if (analysis.HasChapters)
        {
            EncodingTaskDefinition chapterTask = new()
            {
                TaskType = EncodingTaskType.ChapterExtraction,
                Description = $"Chapter extraction ({analysis.Chapters.Count} chapters)",
                Weight = ChapterExtractionWeight,
                Dependencies = [], // Can run in parallel with encoding
                OutputPath = "chapters.vtt",
                Parameters = new Dictionary<string, object>
                {
                    ["ChapterCount"] = analysis.Chapters.Count
                }
            };
            tasks.Add(chapterTask);
        }

        // Sprite generation (depends on video being available)
        EncodingTaskDefinition spriteTask = new()
        {
            TaskType = EncodingTaskType.SpriteGeneration,
            Description = "Thumbnail sprite generation",
            Weight = SpriteGenerationWeight * (analysis.Duration.TotalMinutes / 60.0),
            Dependencies = [], // Can start early, reads from source
            OutputPath = "thumbs/",
            Parameters = new Dictionary<string, object>
            {
                ["Duration"] = analysis.Duration.TotalSeconds,
                ["Interval"] = 10, // One thumbnail every 10 seconds
                ["Width"] = 160,
                ["Height"] = 90
            }
        };
        tasks.Add(spriteTask);

        // Playlist generation (for HLS, depends on all video/audio encoding)
        string? container = profile.Container?.ToLowerInvariant();
        if (container == "hls" || container == "m3u8")
        {
            EncodingTaskDefinition playlistTask = new()
            {
                TaskType = EncodingTaskType.PlaylistGeneration,
                Description = "Master playlist generation",
                Weight = PlaylistGenerationWeight,
                Dependencies = [..encodingTaskDependencies],
                Parameters = new Dictionary<string, object>
                {
                    ["VideoProfiles"] = profile.VideoProfiles?.Length ?? 0,
                    ["AudioProfiles"] = profile.AudioProfiles?.Length ?? 0
                }
            };
            tasks.Add(playlistTask);
        }

        // Validation (depends on all other tasks)
        List<string> allPriorTaskIds = tasks.Select(t => t.Id).ToList();
        EncodingTaskDefinition validationTask = new()
        {
            TaskType = EncodingTaskType.Validation,
            Description = "Output validation",
            Weight = ValidationWeight,
            Dependencies = [..allPriorTaskIds],
            Parameters = new Dictionary<string, object>
            {
                ["Container"] = profile.Container ?? "unknown",
                ["ValidatePlaylist"] = container == "hls" || container == "m3u8"
            }
        };
        tasks.Add(validationTask);
    }

    private static bool HasSdrOutputs(EncoderProfile profile)
    {
        return profile.VideoProfiles?.Any(vp => vp.ConvertHdrToSdr) == true;
    }

    private static bool IsHardwareCodec(string codec)
    {
        string lower = codec.ToLowerInvariant();
        return lower.Contains("nvenc") ||
               lower.Contains("qsv") ||
               lower.Contains("amf") ||
               lower.Contains("videotoolbox") ||
               lower.Contains("vaapi");
    }

    private static int EstimateVideoMemory(IVideoProfile profile)
    {
        // Rough memory estimate based on resolution
        int pixels = profile.Width * profile.Height;
        return pixels switch
        {
            >= 33177600 => 16384, // 8K
            >= 8294400 => 8192,   // 4K
            >= 3686400 => 4096,   // 1440p
            >= 2073600 => 2048,   // 1080p
            _ => 1024             // 720p and below
        };
    }

    private double CalculateHdrConversionWeight(StreamAnalysis analysis)
    {
        double baseWeight = HdrConversionWeight;

        // Scale by resolution
        if (analysis.PrimaryVideoStream != null)
        {
            int pixels = analysis.PrimaryVideoStream.Width * analysis.PrimaryVideoStream.Height;
            baseWeight *= pixels / 2073600.0; // Relative to 1080p
        }

        // Scale by duration
        baseWeight *= analysis.Duration.TotalMinutes / 60.0;

        return baseWeight;
    }

    private double CalculateVideoEncodingWeight(EncodingTaskDefinition task, StreamAnalysis analysis)
    {
        double weight = 1.0;

        // Resolution factor
        if (task.Parameters.TryGetValue("Width", out object? widthObj) &&
            task.Parameters.TryGetValue("Height", out object? heightObj))
        {
            int width = Convert.ToInt32(widthObj);
            int height = Convert.ToInt32(heightObj);
            int pixels = width * height;
            weight = pixels / 2073600.0; // Relative to 1080p
        }

        // Codec factor
        if (task.Parameters.TryGetValue("Codec", out object? codecObj))
        {
            string codec = codecObj?.ToString() ?? "h264";
            weight *= GetCodecMultiplier(codec);
        }

        // Preset factor
        if (task.Parameters.TryGetValue("Preset", out object? presetObj))
        {
            string preset = presetObj?.ToString() ?? "medium";
            weight *= GetPresetMultiplier(preset);
        }

        // Duration factor
        weight *= analysis.Duration.TotalMinutes / 60.0;

        // HDR conversion adds complexity
        if (task.Parameters.TryGetValue("ConvertHdrToSdr", out object? hdrObj) &&
            Convert.ToBoolean(hdrObj))
        {
            weight *= 1.5;
        }

        return weight;
    }

    private double CalculateAudioEncodingWeight(EncodingTaskDefinition task, StreamAnalysis analysis)
    {
        double weight = 0.1;

        // Scale by duration
        weight *= analysis.Duration.TotalMinutes / 60.0;

        // Scale by profile count or stream count
        if (task.Parameters.TryGetValue("ProfileCount", out object? countObj))
        {
            weight *= Convert.ToInt32(countObj);
        }

        return weight;
    }

    private static double GetCodecMultiplier(string codec)
    {
        return codec.ToLowerInvariant() switch
        {
            "av1" or "libaom-av1" => 3.0,
            "libsvtav1" => 2.0,
            "hevc" or "libx265" or "h265" => 1.5,
            "vp9" or "libvpx-vp9" => 1.8,
            "h264" or "libx264" => 1.0,
            "h264_nvenc" => 0.3,
            "hevc_nvenc" => 0.4,
            "av1_nvenc" => 0.5,
            "h264_qsv" => 0.35,
            "hevc_qsv" => 0.45,
            _ => 1.0
        };
    }

    private static double GetPresetMultiplier(string preset)
    {
        return preset.ToLowerInvariant() switch
        {
            "ultrafast" => 0.2,
            "superfast" => 0.3,
            "veryfast" => 0.4,
            "faster" => 0.6,
            "fast" => 0.8,
            "medium" => 1.0,
            "slow" => 1.5,
            "slower" => 2.5,
            "veryslow" => 4.0,
            "placebo" => 8.0,
            _ => 1.0
        };
    }

    private static int CalculateMaxParallelism(List<EncodingTaskDefinition> tasks)
    {
        // Find tasks with no dependencies (can all run in parallel)
        HashSet<string> taskIds = tasks.Select(t => t.Id).ToHashSet();
        int noDependencies = tasks.Count(t =>
            t.Dependencies.Count == 0 ||
            !t.Dependencies.Any(d => taskIds.Contains(d)));

        return Math.Max(1, noDependencies);
    }

    private static int CalculateCriticalPathLength(List<EncodingTaskDefinition> tasks)
    {
        if (tasks.Count == 0) return 0;

        Dictionary<string, int> depths = [];

        int CalculateDepth(EncodingTaskDefinition task)
        {
            if (depths.TryGetValue(task.Id, out int cached))
                return cached;

            if (task.Dependencies.Count == 0)
            {
                depths[task.Id] = 1;
                return 1;
            }

            int maxDependencyDepth = task.Dependencies
                .Select(depId => tasks.FirstOrDefault(t => t.Id == depId))
                .Where(dep => dep != null)
                .Select(dep => CalculateDepth(dep!))
                .DefaultIfEmpty(0)
                .Max();

            int depth = maxDependencyDepth + 1;
            depths[task.Id] = depth;
            return depth;
        }

        return tasks.Max(t => CalculateDepth(t));
    }

    #endregion
}
