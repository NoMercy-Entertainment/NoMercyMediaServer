using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Result of a health check cycle
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Timestamp when the check was performed
    /// </summary>
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// Total number of registered nodes
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Number of healthy nodes after the check
    /// </summary>
    public int HealthyNodes { get; set; }

    /// <summary>
    /// Number of unhealthy nodes detected
    /// </summary>
    public int UnhealthyNodes { get; set; }

    /// <summary>
    /// Number of nodes that became unhealthy in this check
    /// </summary>
    public int NewlyUnhealthyNodes { get; set; }

    /// <summary>
    /// Number of nodes that recovered in this check
    /// </summary>
    public int RecoveredNodes { get; set; }

    /// <summary>
    /// IDs of currently unhealthy nodes
    /// </summary>
    public List<Ulid> UnhealthyNodeIds { get; set; } = [];

    /// <summary>
    /// Number of tasks that were reassigned from unhealthy nodes
    /// </summary>
    public int TasksReassigned { get; set; }

    /// <summary>
    /// Duration of the health check cycle
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Any warnings or errors encountered during the check
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Health status of a single encoder node
/// </summary>
public class NodeHealthStatus
{
    /// <summary>
    /// Unique identifier of the node
    /// </summary>
    public Ulid NodeId { get; set; }

    /// <summary>
    /// Display name of the node
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the node
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Port number of the node
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Whether the node is currently healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Whether the node is administratively enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat received
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// How long since the last heartbeat
    /// </summary>
    public TimeSpan? HeartbeatAge { get; set; }

    /// <summary>
    /// Current number of running tasks
    /// </summary>
    public int CurrentTaskCount { get; set; }

    /// <summary>
    /// Maximum concurrent tasks this node can handle
    /// </summary>
    public int MaxConcurrentTasks { get; set; }

    /// <summary>
    /// Whether the node has a GPU
    /// </summary>
    public bool HasGpu { get; set; }

    /// <summary>
    /// GPU model if available
    /// </summary>
    public string? GpuModel { get; set; }

    /// <summary>
    /// Reason if the node is unhealthy
    /// </summary>
    public string? UnhealthyReason { get; set; }
}

/// <summary>
/// Options for configuring health monitoring behavior
/// </summary>
public class HealthMonitorOptions
{
    /// <summary>
    /// Interval between health checks in seconds (default: 30)
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum age of last heartbeat before a node is considered unhealthy (default: 60 seconds)
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to automatically reassign tasks from unhealthy nodes (default: true)
    /// </summary>
    public bool AutoReassignTasks { get; set; } = true;

    /// <summary>
    /// Number of consecutive failed health checks before marking a node as unhealthy (default: 2)
    /// </summary>
    public int FailedChecksBeforeUnhealthy { get; set; } = 2;

    /// <summary>
    /// Whether to log detailed health check results (default: false)
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}

/// <summary>
/// Result of registering a node heartbeat
/// </summary>
public class HeartbeatResult
{
    /// <summary>
    /// Whether the heartbeat was successfully recorded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether the node was previously unhealthy and has now recovered
    /// </summary>
    public bool WasRecovered { get; set; }

    /// <summary>
    /// Error message if the heartbeat failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for monitoring health of distributed encoder nodes.
/// Performs periodic health checks, updates node status, and handles task reassignment.
/// </summary>
public interface INodeHealthMonitor
{
    /// <summary>
    /// Start the health monitoring background service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the health monitoring service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the health monitor is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Perform a single health check cycle on all nodes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result with status of all nodes</returns>
    Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a heartbeat from a node, marking it as healthy
    /// </summary>
    /// <param name="nodeId">Node ID sending the heartbeat</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Heartbeat result</returns>
    Task<HeartbeatResult> RecordHeartbeatAsync(Ulid nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a heartbeat with additional status information
    /// </summary>
    /// <param name="nodeId">Node ID sending the heartbeat</param>
    /// <param name="currentTaskCount">Current number of running tasks on the node</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Heartbeat result</returns>
    Task<HeartbeatResult> RecordHeartbeatAsync(
        Ulid nodeId,
        int currentTaskCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a node as unhealthy and optionally reassign its tasks
    /// </summary>
    /// <param name="nodeId">Node ID to mark as unhealthy</param>
    /// <param name="reason">Reason for marking as unhealthy</param>
    /// <param name="reassignTasks">Whether to reassign the node's tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks reassigned (0 if reassignTasks is false)</returns>
    Task<int> MarkNodeUnhealthyAsync(
        Ulid nodeId,
        string reason,
        bool reassignTasks = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a node as healthy
    /// </summary>
    /// <param name="nodeId">Node ID to mark as healthy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the node was successfully marked as healthy</returns>
    Task<bool> MarkNodeHealthyAsync(Ulid nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable a node for receiving tasks
    /// </summary>
    /// <param name="nodeId">Node ID to enable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the node was successfully enabled</returns>
    Task<bool> EnableNodeAsync(Ulid nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disable a node and optionally reassign its tasks
    /// </summary>
    /// <param name="nodeId">Node ID to disable</param>
    /// <param name="reason">Reason for disabling</param>
    /// <param name="reassignTasks">Whether to reassign the node's tasks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks reassigned</returns>
    Task<int> DisableNodeAsync(
        Ulid nodeId,
        string reason,
        bool reassignTasks = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current health status of all nodes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of node health statuses</returns>
    Task<List<NodeHealthStatus>> GetAllNodeStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the health status of a specific node
    /// </summary>
    /// <param name="nodeId">Node ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Node health status, or null if node not found</returns>
    Task<NodeHealthStatus?> GetNodeStatusAsync(Ulid nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get summary statistics for all nodes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (total nodes, healthy nodes, enabled nodes, total running tasks)</returns>
    Task<(int TotalNodes, int HealthyNodes, int EnabledNodes, int TotalRunningTasks)> GetNodeSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the most recent health check result
    /// </summary>
    HealthCheckResult? LastHealthCheckResult { get; }

    /// <summary>
    /// Timestamp of the last health check
    /// </summary>
    DateTime? LastHealthCheckTime { get; }
}
