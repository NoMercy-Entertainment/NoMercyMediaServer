using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Strategy for selecting nodes when multiple candidates are available
/// </summary>
public enum NodeSelectionStrategy
{
    /// <summary>
    /// Select the node with the most available capacity (lowest current load)
    /// Best for balancing load across nodes
    /// </summary>
    LeastLoaded,

    /// <summary>
    /// Select the node with the best hardware match for the task
    /// Best for GPU-intensive tasks where hardware capabilities matter
    /// </summary>
    BestCapability,

    /// <summary>
    /// Select nodes in round-robin order
    /// Best for even distribution of tasks
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Select the fastest node based on historical performance
    /// Best for time-critical encoding jobs
    /// </summary>
    Fastest,

    /// <summary>
    /// Automatically choose the best strategy based on task characteristics
    /// </summary>
    Auto
}

/// <summary>
/// Options for node selection behavior
/// </summary>
public class NodeSelectionOptions
{
    /// <summary>
    /// Selection strategy to use
    /// </summary>
    public NodeSelectionStrategy Strategy { get; set; } = NodeSelectionStrategy.Auto;

    /// <summary>
    /// Whether to require GPU for tasks marked as RequiresGpu
    /// When false, GPU tasks can fall back to CPU nodes if no GPU nodes available
    /// </summary>
    public bool StrictGpuRequirement { get; set; } = false;

    /// <summary>
    /// Minimum memory (in GB) required on the node
    /// </summary>
    public int MinimumMemoryGb { get; set; } = 2;

    /// <summary>
    /// Whether to exclude nodes that are currently at capacity
    /// </summary>
    public bool ExcludeFullNodes { get; set; } = true;

    /// <summary>
    /// Maximum age of last heartbeat (in seconds) for a node to be considered healthy
    /// Nodes with older heartbeats are excluded from selection
    /// </summary>
    public int MaxHeartbeatAgeSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to prefer local node for small tasks
    /// </summary>
    public bool PreferLocalForSmallTasks { get; set; } = true;

    /// <summary>
    /// Weight threshold below which a task is considered "small"
    /// </summary>
    public double SmallTaskWeightThreshold { get; set; } = 10.0;

    /// <summary>
    /// Required hardware acceleration type (null for any)
    /// </summary>
    public string? RequiredAccelerationType { get; set; }
}

/// <summary>
/// Result of selecting a node for a task
/// </summary>
public class NodeSelectionResult
{
    /// <summary>
    /// Whether a suitable node was found
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The selected node (null if no suitable node found)
    /// </summary>
    public EncoderNode? SelectedNode { get; set; }

    /// <summary>
    /// Reason for selection or failure
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Score of the selected node (higher is better)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Alternative nodes that could handle the task, ordered by score
    /// </summary>
    public List<NodeCandidate> Alternatives { get; set; } = [];

    /// <summary>
    /// Strategy that was used for selection
    /// </summary>
    public NodeSelectionStrategy UsedStrategy { get; set; }
}

/// <summary>
/// A candidate node with its selection score
/// </summary>
public class NodeCandidate
{
    /// <summary>
    /// The encoder node
    /// </summary>
    public EncoderNode Node { get; set; } = null!;

    /// <summary>
    /// Selection score (higher is better)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Breakdown of how the score was calculated
    /// </summary>
    public Dictionary<string, double> ScoreBreakdown { get; set; } = [];

    /// <summary>
    /// Whether this node meets all requirements
    /// </summary>
    public bool MeetsRequirements { get; set; }

    /// <summary>
    /// Reasons why this node may not be ideal
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Result of batch node assignment for multiple tasks
/// </summary>
public class BatchAssignmentResult
{
    /// <summary>
    /// Tasks successfully assigned to nodes
    /// </summary>
    public List<TaskAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Tasks that could not be assigned to any node
    /// </summary>
    public List<UnassignedTask> UnassignedTasks { get; set; } = [];

    /// <summary>
    /// Total number of tasks processed
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Number of successfully assigned tasks
    /// </summary>
    public int AssignedCount => Assignments.Count;

    /// <summary>
    /// Whether all tasks were successfully assigned
    /// </summary>
    public bool AllAssigned => UnassignedTasks.Count == 0;
}

/// <summary>
/// Assignment of a task to a node
/// </summary>
public class TaskAssignment
{
    /// <summary>
    /// The task definition
    /// </summary>
    public EncodingTaskDefinition Task { get; set; } = null!;

    /// <summary>
    /// The assigned node
    /// </summary>
    public EncoderNode Node { get; set; } = null!;

    /// <summary>
    /// Selection score for this assignment
    /// </summary>
    public double Score { get; set; }
}

/// <summary>
/// A task that could not be assigned
/// </summary>
public class UnassignedTask
{
    /// <summary>
    /// The task that could not be assigned
    /// </summary>
    public EncodingTaskDefinition Task { get; set; } = null!;

    /// <summary>
    /// Reason why the task could not be assigned
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Service for selecting encoder nodes for tasks based on capabilities and load
/// </summary>
public interface INodeSelector
{
    /// <summary>
    /// Select the best node for a single task
    /// </summary>
    /// <param name="task">The task to assign</param>
    /// <param name="availableNodes">List of available encoder nodes</param>
    /// <param name="options">Selection options (null for defaults)</param>
    /// <returns>Selection result with the chosen node</returns>
    NodeSelectionResult SelectNode(
        EncodingTaskDefinition task,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null);

    /// <summary>
    /// Select nodes for multiple tasks (batch assignment)
    /// </summary>
    /// <param name="tasks">Tasks to assign</param>
    /// <param name="availableNodes">List of available encoder nodes</param>
    /// <param name="options">Selection options (null for defaults)</param>
    /// <returns>Batch assignment result</returns>
    BatchAssignmentResult SelectNodesForTasks(
        IEnumerable<EncodingTaskDefinition> tasks,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null);

    /// <summary>
    /// Check if a node can handle a specific task
    /// </summary>
    /// <param name="node">The node to check</param>
    /// <param name="task">The task to evaluate</param>
    /// <param name="options">Selection options for capability requirements</param>
    /// <returns>True if the node can handle the task</returns>
    bool CanNodeHandleTask(
        EncoderNode node,
        EncodingTaskDefinition task,
        NodeSelectionOptions? options = null);

    /// <summary>
    /// Calculate a capability score for a node relative to a task
    /// </summary>
    /// <param name="node">The node to score</param>
    /// <param name="task">The task for context</param>
    /// <returns>Score (0-100, higher is better)</returns>
    double CalculateNodeScore(EncoderNode node, EncodingTaskDefinition task);

    /// <summary>
    /// Get all nodes that can handle a specific task
    /// </summary>
    /// <param name="task">The task to match</param>
    /// <param name="availableNodes">List of available encoder nodes</param>
    /// <param name="options">Selection options</param>
    /// <returns>List of capable nodes with their scores</returns>
    List<NodeCandidate> GetCapableNodes(
        EncodingTaskDefinition task,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null);

    /// <summary>
    /// Filter nodes by health status
    /// </summary>
    /// <param name="nodes">Nodes to filter</param>
    /// <param name="maxHeartbeatAgeSeconds">Maximum acceptable heartbeat age</param>
    /// <returns>Healthy nodes only</returns>
    IEnumerable<EncoderNode> FilterHealthyNodes(
        IEnumerable<EncoderNode> nodes,
        int maxHeartbeatAgeSeconds = 60);

    /// <summary>
    /// Determine the best selection strategy for a task
    /// </summary>
    /// <param name="task">The task to evaluate</param>
    /// <returns>Recommended selection strategy</returns>
    NodeSelectionStrategy DetermineOptimalStrategy(EncodingTaskDefinition task);
}
