using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Background service for monitoring encoder node health.
/// Performs periodic health checks every 30 seconds, updates node status,
/// and reassigns tasks from unhealthy nodes.
/// </summary>
public class NodeHealthMonitor : BackgroundService, INodeHealthMonitor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NodeHealthMonitor> _logger;
    private readonly HealthMonitorOptions _options;
    private readonly object _lockObject = new();

    private bool _isRunning;
    private HealthCheckResult? _lastHealthCheckResult;
    private DateTime? _lastHealthCheckTime;
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;

    // Track consecutive failed checks per node for hysteresis
    private readonly Dictionary<Ulid, int> _consecutiveFailedChecks = new();

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <inheritdoc/>
    public HealthCheckResult? LastHealthCheckResult => _lastHealthCheckResult;

    /// <inheritdoc/>
    public DateTime? LastHealthCheckTime => _lastHealthCheckTime;

    public NodeHealthMonitor(
        IServiceProvider serviceProvider,
        ILogger<NodeHealthMonitor> logger)
        : this(serviceProvider, logger, new HealthMonitorOptions())
    {
    }

    public NodeHealthMonitor(
        IServiceProvider serviceProvider,
        ILogger<NodeHealthMonitor> logger,
        HealthMonitorOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NodeHealthMonitor starting with {Interval}s check interval and {Timeout}s heartbeat timeout",
            _options.CheckIntervalSeconds, _options.HeartbeatTimeoutSeconds);

        _isRunning = true;

        try
        {
            // Initial delay to let nodes register
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HealthCheckResult result = await PerformHealthCheckAsync(stoppingToken);

                    if (_options.VerboseLogging || result.NewlyUnhealthyNodes > 0 || result.RecoveredNodes > 0)
                    {
                        _logger.LogInformation(
                            "Health check completed: {Healthy}/{Total} nodes healthy, {Reassigned} tasks reassigned, {NewUnhealthy} newly unhealthy, {Recovered} recovered",
                            result.HealthyNodes, result.TotalNodes, result.TasksReassigned,
                            result.NewlyUnhealthyNodes, result.RecoveredNodes);
                    }

                    foreach (string warning in result.Warnings)
                    {
                        _logger.LogWarning("Health check warning: {Warning}", warning);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during health check cycle");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NodeHealthMonitor stopping due to cancellation");
        }
        finally
        {
            _isRunning = false;
        }

        _logger.LogInformation("NodeHealthMonitor stopped");
    }

    /// <inheritdoc/>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("NodeHealthMonitor is already running");
            return;
        }

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = ExecuteAsync(_monitoringCts.Token);

        _logger.LogInformation("NodeHealthMonitor started manually");
    }

    /// <inheritdoc/>
    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning || _monitoringCts is null)
        {
            return;
        }

        _logger.LogInformation("Stopping NodeHealthMonitor...");

        await _monitoringCts.CancelAsync();

        if (_monitoringTask is not null)
        {
            try
            {
                await _monitoringTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("NodeHealthMonitor did not stop within timeout");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _monitoringCts.Dispose();
        _monitoringCts = null;
        _monitoringTask = null;

        _logger.LogInformation("NodeHealthMonitor stopped");
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.UtcNow;
        HealthCheckResult result = new()
        {
            CheckedAt = startTime
        };

        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            // Get all enabled nodes
            List<EncoderNode> nodes = await queueContext.EncoderNodes
                .Where(n => n.IsEnabled)
                .Include(n => n.AssignedTasks.Where(t => t.State == EncodingTaskState.Running))
                .ToListAsync(cancellationToken);

            result.TotalNodes = nodes.Count;

            DateTime now = DateTime.UtcNow;
            List<Ulid> newlyUnhealthyNodeIds = [];
            List<Ulid> recoveredNodeIds = [];
            int totalTasksReassigned = 0;

            foreach (EncoderNode node in nodes)
            {
                bool wasHealthy = node.IsHealthy;
                bool isNowHealthy = IsNodeHealthy(node, now);

                // Track consecutive failed checks for hysteresis
                if (!isNowHealthy)
                {
                    lock (_lockObject)
                    {
                        if (!_consecutiveFailedChecks.TryGetValue(node.Id, out int count))
                        {
                            count = 0;
                        }
                        _consecutiveFailedChecks[node.Id] = count + 1;

                        // Only mark unhealthy after consecutive failures
                        if (_consecutiveFailedChecks[node.Id] < _options.FailedChecksBeforeUnhealthy)
                        {
                            isNowHealthy = wasHealthy; // Keep current state
                        }
                    }
                }
                else
                {
                    lock (_lockObject)
                    {
                        _consecutiveFailedChecks.Remove(node.Id);
                    }
                }

                // Update node health status
                if (node.IsHealthy != isNowHealthy)
                {
                    node.IsHealthy = isNowHealthy;

                    if (!isNowHealthy)
                    {
                        newlyUnhealthyNodeIds.Add(node.Id);
                        _logger.LogWarning(
                            "Node {NodeName} ({NodeId}) marked as unhealthy. Last heartbeat: {LastHeartbeat}",
                            node.Name, node.Id, node.LastHeartbeat);

                        // Reassign tasks if enabled
                        if (_options.AutoReassignTasks)
                        {
                            int reassigned = await ReassignTasksFromNodeInternalAsync(
                                queueContext, node.Id, cancellationToken);
                            totalTasksReassigned += reassigned;

                            if (reassigned > 0)
                            {
                                _logger.LogInformation(
                                    "Reassigned {Count} tasks from unhealthy node {NodeName}",
                                    reassigned, node.Name);
                            }
                        }
                    }
                    else if (wasHealthy == false)
                    {
                        recoveredNodeIds.Add(node.Id);
                        _logger.LogInformation(
                            "Node {NodeName} ({NodeId}) recovered and is now healthy",
                            node.Name, node.Id);
                    }
                }

                // Count current status
                if (isNowHealthy)
                {
                    result.HealthyNodes++;
                }
                else
                {
                    result.UnhealthyNodes++;
                    result.UnhealthyNodeIds.Add(node.Id);
                }
            }

            // Save changes
            await queueContext.SaveChangesAsync(cancellationToken);

            result.NewlyUnhealthyNodes = newlyUnhealthyNodeIds.Count;
            result.RecoveredNodes = recoveredNodeIds.Count;
            result.TasksReassigned = totalTasksReassigned;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Health check error: {ex.Message}");
            _logger.LogError(ex, "Error during health check");
        }

        result.Duration = DateTime.UtcNow - startTime;
        _lastHealthCheckResult = result;
        _lastHealthCheckTime = startTime;

        return result;
    }

    private bool IsNodeHealthy(EncoderNode node, DateTime now)
    {
        if (!node.IsEnabled)
        {
            return false;
        }

        if (node.LastHeartbeat is null)
        {
            // Node has never sent a heartbeat - consider unhealthy if it has been registered for a while
            TimeSpan timeSinceCreation = now - node.CreatedAt;
            return timeSinceCreation < TimeSpan.FromSeconds(_options.HeartbeatTimeoutSeconds * 2);
        }

        TimeSpan heartbeatAge = now - node.LastHeartbeat.Value;
        return heartbeatAge <= TimeSpan.FromSeconds(_options.HeartbeatTimeoutSeconds);
    }

    /// <inheritdoc/>
    public async Task<HeartbeatResult> RecordHeartbeatAsync(Ulid nodeId, CancellationToken cancellationToken = default)
    {
        return await RecordHeartbeatAsync(nodeId, -1, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<HeartbeatResult> RecordHeartbeatAsync(
        Ulid nodeId,
        int currentTaskCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                return new HeartbeatResult
                {
                    Success = false,
                    ErrorMessage = $"Node {nodeId} not found"
                };
            }

            bool wasUnhealthy = !node.IsHealthy;

            node.LastHeartbeat = DateTime.UtcNow;
            node.IsHealthy = true;

            if (currentTaskCount >= 0)
            {
                node.CurrentTaskCount = currentTaskCount;
            }

            // Clear consecutive failed checks
            lock (_lockObject)
            {
                _consecutiveFailedChecks.Remove(nodeId);
            }

            await queueContext.SaveChangesAsync(cancellationToken);

            if (wasUnhealthy)
            {
                _logger.LogInformation("Node {NodeName} ({NodeId}) recovered via heartbeat", node.Name, nodeId);
            }

            return new HeartbeatResult
            {
                Success = true,
                WasRecovered = wasUnhealthy
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording heartbeat for node {NodeId}", nodeId);
            return new HeartbeatResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<int> MarkNodeUnhealthyAsync(
        Ulid nodeId,
        string reason,
        bool reassignTasks = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                _logger.LogWarning("Cannot mark node {NodeId} as unhealthy: node not found", nodeId);
                return 0;
            }

            node.IsHealthy = false;
            _logger.LogWarning("Node {NodeName} ({NodeId}) manually marked as unhealthy: {Reason}",
                node.Name, nodeId, reason);

            int reassigned = 0;
            if (reassignTasks)
            {
                reassigned = await ReassignTasksFromNodeInternalAsync(queueContext, nodeId, cancellationToken);
            }

            await queueContext.SaveChangesAsync(cancellationToken);
            return reassigned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking node {NodeId} as unhealthy", nodeId);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkNodeHealthyAsync(Ulid nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                _logger.LogWarning("Cannot mark node {NodeId} as healthy: node not found", nodeId);
                return false;
            }

            node.IsHealthy = true;
            node.LastHeartbeat = DateTime.UtcNow;

            // Clear consecutive failed checks
            lock (_lockObject)
            {
                _consecutiveFailedChecks.Remove(nodeId);
            }

            await queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Node {NodeName} ({NodeId}) manually marked as healthy", node.Name, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking node {NodeId} as healthy", nodeId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> EnableNodeAsync(Ulid nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                _logger.LogWarning("Cannot enable node {NodeId}: node not found", nodeId);
                return false;
            }

            node.IsEnabled = true;
            node.IsHealthy = true;
            node.LastHeartbeat = DateTime.UtcNow;

            await queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Node {NodeName} ({NodeId}) enabled", node.Name, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling node {NodeId}", nodeId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> DisableNodeAsync(
        Ulid nodeId,
        string reason,
        bool reassignTasks = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                _logger.LogWarning("Cannot disable node {NodeId}: node not found", nodeId);
                return 0;
            }

            node.IsEnabled = false;
            node.IsHealthy = false;

            _logger.LogInformation("Node {NodeName} ({NodeId}) disabled: {Reason}", node.Name, nodeId, reason);

            int reassigned = 0;
            if (reassignTasks)
            {
                reassigned = await ReassignTasksFromNodeInternalAsync(queueContext, nodeId, cancellationToken);
            }

            await queueContext.SaveChangesAsync(cancellationToken);
            return reassigned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling node {NodeId}", nodeId);
            return 0;
        }
    }

    private async Task<int> ReassignTasksFromNodeInternalAsync(
        QueueContext queueContext,
        Ulid nodeId,
        CancellationToken cancellationToken)
    {
        // Get running tasks assigned to this node
        List<EncodingTask> tasksToReassign = await queueContext.EncodingTasks
            .Where(t => t.AssignedNodeId == nodeId &&
                       (t.State == EncodingTaskState.Running || t.State == EncodingTaskState.Pending))
            .ToListAsync(cancellationToken);

        if (tasksToReassign.Count == 0)
        {
            return 0;
        }

        foreach (EncodingTask task in tasksToReassign)
        {
            task.AssignedNodeId = null;
            task.State = EncodingTaskState.Pending;
            task.StartedAt = null;
        }

        return tasksToReassign.Count;
    }

    /// <inheritdoc/>
    public async Task<List<NodeHealthStatus>> GetAllNodeStatusesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            List<EncoderNode> nodes = await queueContext.EncoderNodes
                .ToListAsync(cancellationToken);

            DateTime now = DateTime.UtcNow;

            return nodes.Select(n => CreateNodeHealthStatus(n, now)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all node statuses");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<NodeHealthStatus?> GetNodeStatusAsync(Ulid nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            EncoderNode? node = await queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

            if (node is null)
            {
                return null;
            }

            return CreateNodeHealthStatus(node, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting node status for {NodeId}", nodeId);
            return null;
        }
    }

    private NodeHealthStatus CreateNodeHealthStatus(EncoderNode node, DateTime now)
    {
        TimeSpan? heartbeatAge = node.LastHeartbeat.HasValue
            ? now - node.LastHeartbeat.Value
            : null;

        string? unhealthyReason = null;
        if (!node.IsEnabled)
        {
            unhealthyReason = "Node is disabled";
        }
        else if (!node.IsHealthy)
        {
            if (node.LastHeartbeat is null)
            {
                unhealthyReason = "No heartbeat received";
            }
            else if (heartbeatAge > TimeSpan.FromSeconds(_options.HeartbeatTimeoutSeconds))
            {
                unhealthyReason = $"Heartbeat timeout ({heartbeatAge?.TotalSeconds:F0}s > {_options.HeartbeatTimeoutSeconds}s)";
            }
            else
            {
                unhealthyReason = "Marked as unhealthy";
            }
        }

        return new NodeHealthStatus
        {
            NodeId = node.Id,
            NodeName = node.Name,
            IpAddress = node.IpAddress,
            Port = node.Port,
            IsHealthy = node.IsHealthy,
            IsEnabled = node.IsEnabled,
            LastHeartbeat = node.LastHeartbeat,
            HeartbeatAge = heartbeatAge,
            CurrentTaskCount = node.CurrentTaskCount,
            MaxConcurrentTasks = node.MaxConcurrentTasks,
            HasGpu = node.HasGpu,
            GpuModel = node.GpuModel,
            UnhealthyReason = unhealthyReason
        };
    }

    /// <inheritdoc/>
    public async Task<(int TotalNodes, int HealthyNodes, int EnabledNodes, int TotalRunningTasks)> GetNodeSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            List<EncoderNode> nodes = await queueContext.EncoderNodes
                .ToListAsync(cancellationToken);

            int totalNodes = nodes.Count;
            int healthyNodes = nodes.Count(n => n.IsHealthy && n.IsEnabled);
            int enabledNodes = nodes.Count(n => n.IsEnabled);
            int totalRunningTasks = nodes.Sum(n => n.CurrentTaskCount);

            return (totalNodes, healthyNodes, enabledNodes, totalRunningTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting node summary");
            return (0, 0, 0, 0);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NodeHealthMonitor StopAsync called");
        await base.StopAsync(cancellationToken);
    }
}
