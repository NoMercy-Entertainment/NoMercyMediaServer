using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Splits encoding jobs into distributable tasks
/// Implements various splitting strategies for parallel processing
/// </summary>
public class TaskSplitter : ITaskSplitter
{
    public List<EncodingTaskDefinition> SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy = TaskDistributionStrategy.SingleTask)
    {
        return strategy switch
        {
            TaskDistributionStrategy.SingleTask => CreateSingleTask(analysis, profile),
            TaskDistributionStrategy.ByResolution => SplitByResolution(analysis, profile),
            TaskDistributionStrategy.ByStreamType => SplitByStreamType(analysis, profile),
            TaskDistributionStrategy.BySegment => SplitBySegment(analysis, profile),
            _ => CreateSingleTask(analysis, profile)
        };
    }

    public double CalculateTaskWeight(EncodingTaskDefinition task, StreamAnalysis analysis)
    {
        double baseWeight = 1.0;

        // Increase weight based on resolution
        if (task.Parameters.TryGetValue("Width", out object? widthObj) &&
            task.Parameters.TryGetValue("Height", out object? heightObj))
        {
            int width = Convert.ToInt32(widthObj);
            int height = Convert.ToInt32(heightObj);
            int pixels = width * height;

            // 1080p = 1.0, 4K = 4.0, 720p = 0.5, etc.
            baseWeight = pixels / 2073600.0; // 1920x1080
        }

        // Increase weight for HDR conversion
        if (task.Parameters.TryGetValue("ConvertHdrToSdr", out object? hdrObj) &&
            Convert.ToBoolean(hdrObj))
        {
            baseWeight *= 1.5;
        }

        // Increase weight based on duration
        double durationFactor = analysis.Duration.TotalMinutes / 60.0;
        baseWeight *= Math.Max(0.1, Math.Min(10.0, durationFactor));

        return baseWeight;
    }

    private List<EncodingTaskDefinition> CreateSingleTask(StreamAnalysis analysis, EncoderProfile profile)
    {
        return
        [
            new EncodingTaskDefinition
            {
                TaskType = "FullEncode",
                Weight = 1.0,
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileId"] = profile.Id.ToString(),
                    ["Duration"] = analysis.Duration.TotalSeconds
                }
            }
        ];
    }

    private List<EncodingTaskDefinition> SplitByResolution(StreamAnalysis analysis, EncoderProfile profile)
    {
        List<EncodingTaskDefinition> tasks = [];

        if (profile.VideoProfiles == null)
        {
            return CreateSingleTask(analysis, profile);
        }

        foreach (IVideoProfile videoProfile in profile.VideoProfiles)
        {
            EncodingTaskDefinition task = new()
            {
                TaskType = "VideoEncode",
                Parameters = new Dictionary<string, object>
                {
                    ["Codec"] = videoProfile.Codec,
                    ["Width"] = videoProfile.Width,
                    ["Height"] = videoProfile.Height,
                    ["Bitrate"] = videoProfile.Bitrate,
                    ["Crf"] = videoProfile.Crf,
                    ["ConvertHdrToSdr"] = videoProfile.ConvertHdrToSdr
                }
            };

            task.Weight = CalculateTaskWeight(task, analysis);
            tasks.Add(task);
        }

        if (profile.AudioProfiles != null && profile.AudioProfiles.Length > 0)
        {
            EncodingTaskDefinition audioTask = new()
            {
                TaskType = "AudioEncode",
                Weight = 0.2,
                Dependencies = tasks.Select(t => t.TaskType).ToList(),
                Parameters = new Dictionary<string, object>
                {
                    ["AudioProfileCount"] = profile.AudioProfiles.Length
                }
            };

            tasks.Add(audioTask);
        }

        return tasks;
    }

    private List<EncodingTaskDefinition> SplitByStreamType(StreamAnalysis analysis, EncoderProfile profile)
    {
        List<EncodingTaskDefinition> tasks = [];

        if (profile.VideoProfiles != null && profile.VideoProfiles.Length > 0)
        {
            tasks.Add(new EncodingTaskDefinition
            {
                TaskType = "VideoEncode",
                Weight = 0.7,
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileCount"] = profile.VideoProfiles.Length
                }
            });
        }

        if (profile.AudioProfiles != null && profile.AudioProfiles.Length > 0)
        {
            tasks.Add(new EncodingTaskDefinition
            {
                TaskType = "AudioEncode",
                Weight = 0.2,
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileCount"] = profile.AudioProfiles.Length
                }
            });
        }

        if (profile.SubtitleProfiles != null && profile.SubtitleProfiles.Length > 0 && analysis.HasSubtitles)
        {
            tasks.Add(new EncodingTaskDefinition
            {
                TaskType = "SubtitleEncode",
                Weight = 0.1,
                Parameters = new Dictionary<string, object>
                {
                    ["ProfileCount"] = profile.SubtitleProfiles.Length
                }
            });
        }

        return tasks;
    }

    private List<EncodingTaskDefinition> SplitBySegment(StreamAnalysis analysis, EncoderProfile profile)
    {
        List<EncodingTaskDefinition> tasks = [];

        string? container = profile.Container?.ToLower();
        if (container != "hls" && container != "m3u8")
        {
            return CreateSingleTask(analysis, profile);
        }

        int segmentDuration = 10;
        int totalSegments = (int)Math.Ceiling(analysis.Duration.TotalSeconds / segmentDuration);
        totalSegments = Math.Max(1, Math.Min(totalSegments, 100));

        for (int i = 0; i < totalSegments; i++)
        {
            int startTime = i * segmentDuration;
            int duration = Math.Min(segmentDuration, (int)analysis.Duration.TotalSeconds - startTime);

            EncodingTaskDefinition task = new()
            {
                TaskType = "SegmentEncode",
                Weight = duration / 60.0,
                Parameters = new Dictionary<string, object>
                {
                    ["SegmentIndex"] = i,
                    ["StartTime"] = startTime,
                    ["Duration"] = duration
                }
            };

            tasks.Add(task);
        }

        return tasks;
    }
}
