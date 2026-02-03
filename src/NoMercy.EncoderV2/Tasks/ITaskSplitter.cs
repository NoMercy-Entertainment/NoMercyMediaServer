using NoMercy.Database.Models;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Task distribution strategy
/// </summary>
public enum TaskDistributionStrategy
{
    /// <summary>
    /// Single task for the entire job (no splitting)
    /// </summary>
    SingleTask,

    /// <summary>
    /// Split by resolution (each resolution is a separate task)
    /// </summary>
    ByResolution,

    /// <summary>
    /// Split by segment (for HLS, split into time segments)
    /// </summary>
    BySegment,

    /// <summary>
    /// Split by audio/video/subtitle processing
    /// </summary>
    ByStreamType
}

/// <summary>
/// Task definition for encoding
/// </summary>
public class EncodingTaskDefinition
{
    public string TaskType { get; set; } = string.Empty;
    public double Weight { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// <summary>
/// Service for splitting encoding jobs into distributable tasks
/// </summary>
public interface ITaskSplitter
{
    /// <summary>
    /// Split an encoding job into tasks based on the profile and analysis
    /// </summary>
    List<EncodingTaskDefinition> SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy = TaskDistributionStrategy.SingleTask);

    /// <summary>
    /// Calculate the weight of a task (for load balancing)
    /// </summary>
    double CalculateTaskWeight(EncodingTaskDefinition task, StreamAnalysis analysis);
}
