using NoMercy.Database.Models;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Task distribution strategy for splitting encoding jobs
/// </summary>
public enum TaskDistributionStrategy
{
    /// <summary>
    /// Single task for the entire job (no splitting)
    /// </summary>
    SingleTask,

    /// <summary>
    /// Split by resolution (each resolution is a separate task)
    /// Best for multi-quality encoding where each resolution can run on different nodes
    /// </summary>
    ByResolution,

    /// <summary>
    /// Split by segment (for HLS, split into time segments)
    /// Best for very long videos where segment-level parallelism is beneficial
    /// </summary>
    BySegment,

    /// <summary>
    /// Split by stream type (video/audio/subtitle as separate tasks)
    /// Best for balanced workload when tasks can run in parallel
    /// </summary>
    ByStreamType,

    /// <summary>
    /// Optimal splitting based on source analysis
    /// Automatically chooses the best strategy based on source characteristics
    /// </summary>
    Optimal
}

/// <summary>
/// Options for task splitting behavior
/// </summary>
public class TaskSplittingOptions
{
    /// <summary>
    /// Whether to include HDR conversion as a separate shared task
    /// When true, HDR→SDR conversion runs once and is shared across quality levels
    /// </summary>
    public bool ShareHdrConversion { get; set; } = true;

    /// <summary>
    /// Whether to include post-processing tasks (fonts, chapters, sprites)
    /// </summary>
    public bool IncludePostProcessing { get; set; } = true;

    /// <summary>
    /// Whether to include validation as a final task
    /// </summary>
    public bool IncludeValidation { get; set; } = true;

    /// <summary>
    /// Minimum segment duration in seconds for BySegment strategy
    /// </summary>
    public int MinSegmentDuration { get; set; } = 30;

    /// <summary>
    /// Maximum number of segments to create
    /// </summary>
    public int MaxSegments { get; set; } = 100;

    /// <summary>
    /// Whether to generate separate audio tasks per language
    /// </summary>
    public bool SplitAudioByLanguage { get; set; } = true;
}

/// <summary>
/// Task definition for encoding with comprehensive parameters
/// </summary>
public class EncodingTaskDefinition
{
    /// <summary>
    /// Unique identifier for this task definition
    /// </summary>
    public string Id { get; set; } = Ulid.NewUlid().ToString();

    /// <summary>
    /// Task type from EncodingTaskType constants
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the task
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Computed weight for load balancing (higher = more resource-intensive)
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// List of task IDs that must complete before this task can run
    /// </summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Task-specific parameters (codec, resolution, language, etc.)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Expected output file path (relative to job output folder)
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Whether this task requires GPU acceleration
    /// </summary>
    public bool RequiresGpu { get; set; }

    /// <summary>
    /// Estimated memory requirement in MB
    /// </summary>
    public int EstimatedMemoryMb { get; set; }
}

/// <summary>
/// Result of splitting a job into tasks
/// </summary>
public class TaskSplitResult
{
    /// <summary>
    /// List of task definitions in dependency order
    /// </summary>
    public List<EncodingTaskDefinition> Tasks { get; set; } = [];

    /// <summary>
    /// Total estimated weight of all tasks
    /// </summary>
    public double TotalWeight { get; set; }

    /// <summary>
    /// Strategy that was used for splitting
    /// </summary>
    public TaskDistributionStrategy UsedStrategy { get; set; }

    /// <summary>
    /// Number of tasks that can run in parallel (no dependencies on each other)
    /// </summary>
    public int MaxParallelism { get; set; }

    /// <summary>
    /// Whether HDR conversion is shared across video tasks
    /// </summary>
    public bool HasSharedHdrConversion { get; set; }

    /// <summary>
    /// Critical path length (longest chain of dependent tasks)
    /// </summary>
    public int CriticalPathLength { get; set; }
}

/// <summary>
/// Service for splitting encoding jobs into distributable tasks
/// </summary>
public interface ITaskSplitter
{
    /// <summary>
    /// Split an encoding job into tasks based on the profile and analysis
    /// </summary>
    /// <param name="analysis">Source media analysis</param>
    /// <param name="profile">Encoding profile to apply</param>
    /// <param name="strategy">Task distribution strategy</param>
    /// <param name="options">Optional splitting configuration</param>
    /// <returns>Result containing task definitions and metadata</returns>
    TaskSplitResult SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy = TaskDistributionStrategy.Optimal,
        TaskSplittingOptions? options = null);

    /// <summary>
    /// Split an encoding job into tasks (simple overload returning task list)
    /// </summary>
    List<EncodingTaskDefinition> SplitJob(
        StreamAnalysis analysis,
        EncoderProfile profile,
        TaskDistributionStrategy strategy);

    /// <summary>
    /// Calculate the weight of a task (for load balancing)
    /// </summary>
    /// <param name="task">Task definition to evaluate</param>
    /// <param name="analysis">Source media analysis for context</param>
    /// <returns>Weight value (1.0 = reference 1080p h264 medium encode)</returns>
    double CalculateTaskWeight(EncodingTaskDefinition task, StreamAnalysis analysis);

    /// <summary>
    /// Determine the optimal distribution strategy based on source characteristics
    /// </summary>
    /// <param name="analysis">Source media analysis</param>
    /// <param name="profile">Encoding profile to apply</param>
    /// <returns>Recommended distribution strategy</returns>
    TaskDistributionStrategy DetermineOptimalStrategy(StreamAnalysis analysis, EncoderProfile profile);

    /// <summary>
    /// Convert task definitions to database EncodingTask entities
    /// </summary>
    /// <param name="jobId">Parent job ID</param>
    /// <param name="tasks">Task definitions to convert</param>
    /// <returns>Database-ready EncodingTask entities</returns>
    List<EncodingTask> ToEncodingTasks(Ulid jobId, List<EncodingTaskDefinition> tasks);
}
