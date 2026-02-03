using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Selects encoder nodes for tasks based on capabilities, load, and hardware requirements.
/// Implements capability matching to ensure tasks are assigned to nodes that can handle them.
/// </summary>
public class NodeSelector : INodeSelector
{
    // Score weights for different factors
    private const double GpuMatchWeight = 30.0;
    private const double CapacityWeight = 25.0;
    private const double MemoryWeight = 15.0;
    private const double CpuWeight = 15.0;
    private const double AccelerationMatchWeight = 15.0;

    // Round-robin state (simple implementation, not thread-safe for simplicity)
    private int _roundRobinIndex = 0;

    /// <inheritdoc />
    public NodeSelectionResult SelectNode(
        EncodingTaskDefinition task,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null)
    {
        options ??= new NodeSelectionOptions();
        List<EncoderNode> nodeList = availableNodes.ToList();

        if (nodeList.Count == 0)
        {
            return new NodeSelectionResult
            {
                Success = false,
                Reason = "No encoder nodes available"
            };
        }

        // Filter healthy nodes
        List<EncoderNode> healthyNodes = FilterHealthyNodes(nodeList, options.MaxHeartbeatAgeSeconds).ToList();
        if (healthyNodes.Count == 0)
        {
            return new NodeSelectionResult
            {
                Success = false,
                Reason = "No healthy encoder nodes available (all nodes have stale heartbeats)"
            };
        }

        // Get capable nodes with scores
        List<NodeCandidate> candidates = GetCapableNodes(task, healthyNodes, options);

        if (candidates.Count == 0)
        {
            return new NodeSelectionResult
            {
                Success = false,
                Reason = BuildNoCapableNodesReason(task, healthyNodes, options)
            };
        }

        // Determine strategy
        NodeSelectionStrategy strategy = options.Strategy == NodeSelectionStrategy.Auto
            ? DetermineOptimalStrategy(task)
            : options.Strategy;

        // Select based on strategy
        NodeCandidate selected = SelectByStrategy(candidates, task, strategy, options);

        return new NodeSelectionResult
        {
            Success = true,
            SelectedNode = selected.Node,
            Score = selected.Score,
            Reason = $"Selected via {strategy} strategy",
            UsedStrategy = strategy,
            Alternatives = candidates
                .Where(c => c.Node.Id != selected.Node.Id)
                .OrderByDescending(c => c.Score)
                .Take(3)
                .ToList()
        };
    }

    /// <inheritdoc />
    public BatchAssignmentResult SelectNodesForTasks(
        IEnumerable<EncodingTaskDefinition> tasks,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null)
    {
        options ??= new NodeSelectionOptions();
        List<EncodingTaskDefinition> taskList = tasks.ToList();
        List<EncoderNode> nodeList = availableNodes.ToList();

        BatchAssignmentResult result = new()
        {
            TotalTasks = taskList.Count
        };

        // Create a mutable copy of node states for tracking capacity during assignment
        Dictionary<Ulid, int> nodeTaskCounts = nodeList.ToDictionary(
            n => n.Id,
            n => n.CurrentTaskCount);

        // Sort tasks by weight descending (assign heavy tasks first)
        List<EncodingTaskDefinition> sortedTasks = taskList
            .OrderByDescending(t => t.Weight)
            .ToList();

        foreach (EncodingTaskDefinition task in sortedTasks)
        {
            // Create virtual nodes with updated task counts
            List<EncoderNode> virtualNodes = nodeList.Select(n =>
            {
                // Create a shallow copy with updated task count
                return new EncoderNode
                {
                    Id = n.Id,
                    Name = n.Name,
                    IpAddress = n.IpAddress,
                    Port = n.Port,
                    HasGpu = n.HasGpu,
                    GpuModel = n.GpuModel,
                    GpuVendor = n.GpuVendor,
                    CpuCores = n.CpuCores,
                    MemoryGb = n.MemoryGb,
                    IsHealthy = n.IsHealthy,
                    IsEnabled = n.IsEnabled,
                    LastHeartbeat = n.LastHeartbeat,
                    MaxConcurrentTasks = n.MaxConcurrentTasks,
                    CurrentTaskCount = nodeTaskCounts[n.Id],
                    SupportedAccelerationsJson = n.SupportedAccelerationsJson,
                    OperatingSystem = n.OperatingSystem,
                    FfmpegVersion = n.FfmpegVersion
                };
            }).ToList();

            NodeSelectionResult selectionResult = SelectNode(task, virtualNodes, options);

            if (selectionResult.Success && selectionResult.SelectedNode is not null)
            {
                result.Assignments.Add(new TaskAssignment
                {
                    Task = task,
                    Node = nodeList.First(n => n.Id == selectionResult.SelectedNode.Id),
                    Score = selectionResult.Score
                });

                // Update virtual task count
                nodeTaskCounts[selectionResult.SelectedNode.Id]++;
            }
            else
            {
                result.UnassignedTasks.Add(new UnassignedTask
                {
                    Task = task,
                    Reason = selectionResult.Reason
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool CanNodeHandleTask(
        EncoderNode node,
        EncodingTaskDefinition task,
        NodeSelectionOptions? options = null)
    {
        options ??= new NodeSelectionOptions();

        // Check if node is enabled and healthy
        if (!node.IsEnabled || !node.IsHealthy)
        {
            return false;
        }

        // Check heartbeat age
        if (node.LastHeartbeat.HasValue)
        {
            TimeSpan heartbeatAge = DateTime.UtcNow - node.LastHeartbeat.Value;
            if (heartbeatAge.TotalSeconds > options.MaxHeartbeatAgeSeconds)
            {
                return false;
            }
        }

        // Check capacity
        if (options.ExcludeFullNodes && node.CurrentTaskCount >= node.MaxConcurrentTasks)
        {
            return false;
        }

        // Check memory requirement
        if (node.MemoryGb < options.MinimumMemoryGb)
        {
            return false;
        }

        // Check task-specific memory requirement
        if (task.EstimatedMemoryMb > 0)
        {
            int requiredMemoryGb = (int)Math.Ceiling(task.EstimatedMemoryMb / 1024.0);
            if (node.MemoryGb < requiredMemoryGb)
            {
                return false;
            }
        }

        // Check GPU requirement
        if (task.RequiresGpu && options.StrictGpuRequirement && !node.HasGpu)
        {
            return false;
        }

        // Check required acceleration type
        if (!string.IsNullOrEmpty(options.RequiredAccelerationType))
        {
            if (!node.SupportedAccelerations.Contains(options.RequiredAccelerationType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public double CalculateNodeScore(EncoderNode node, EncodingTaskDefinition task)
    {
        double score = 0.0;
        Dictionary<string, double> breakdown = [];

        // GPU match score
        double gpuScore = CalculateGpuScore(node, task);
        score += gpuScore * (GpuMatchWeight / 100.0);
        breakdown["gpu"] = gpuScore;

        // Capacity score (prefer nodes with more available slots)
        double capacityScore = CalculateCapacityScore(node);
        score += capacityScore * (CapacityWeight / 100.0);
        breakdown["capacity"] = capacityScore;

        // Memory score (prefer nodes with more memory)
        double memoryScore = CalculateMemoryScore(node, task);
        score += memoryScore * (MemoryWeight / 100.0);
        breakdown["memory"] = memoryScore;

        // CPU score (prefer nodes with more cores)
        double cpuScore = CalculateCpuScore(node);
        score += cpuScore * (CpuWeight / 100.0);
        breakdown["cpu"] = cpuScore;

        // Acceleration match score
        double accelScore = CalculateAccelerationScore(node, task);
        score += accelScore * (AccelerationMatchWeight / 100.0);
        breakdown["acceleration"] = accelScore;

        return score * 100.0; // Scale to 0-100
    }

    /// <inheritdoc />
    public List<NodeCandidate> GetCapableNodes(
        EncodingTaskDefinition task,
        IEnumerable<EncoderNode> availableNodes,
        NodeSelectionOptions? options = null)
    {
        options ??= new NodeSelectionOptions();
        List<NodeCandidate> candidates = [];

        foreach (EncoderNode node in availableNodes)
        {
            bool meetsRequirements = CanNodeHandleTask(node, task, options);
            double score = CalculateNodeScore(node, task);

            List<string> warnings = [];
            Dictionary<string, double> breakdown = BuildScoreBreakdown(node, task);

            // Add warnings for potential issues
            if (task.RequiresGpu && !node.HasGpu)
            {
                warnings.Add("Task prefers GPU but node has no GPU (will use CPU fallback)");
            }

            if (node.CurrentTaskCount > 0)
            {
                warnings.Add($"Node is currently running {node.CurrentTaskCount} task(s)");
            }

            double availableCapacityPercent = (node.MaxConcurrentTasks - node.CurrentTaskCount) / (double)node.MaxConcurrentTasks * 100;
            if (availableCapacityPercent < 50)
            {
                warnings.Add($"Node is at {100 - availableCapacityPercent:F0}% capacity");
            }

            // Only include nodes that meet requirements (or all if we want to show why they don't qualify)
            if (meetsRequirements)
            {
                candidates.Add(new NodeCandidate
                {
                    Node = node,
                    Score = score,
                    ScoreBreakdown = breakdown,
                    MeetsRequirements = true,
                    Warnings = warnings
                });
            }
        }

        return candidates.OrderByDescending(c => c.Score).ToList();
    }

    /// <inheritdoc />
    public IEnumerable<EncoderNode> FilterHealthyNodes(
        IEnumerable<EncoderNode> nodes,
        int maxHeartbeatAgeSeconds = 60)
    {
        DateTime cutoff = DateTime.UtcNow.AddSeconds(-maxHeartbeatAgeSeconds);

        return nodes.Where(n =>
            n.IsEnabled &&
            n.IsHealthy &&
            (!n.LastHeartbeat.HasValue || n.LastHeartbeat.Value >= cutoff));
    }

    /// <inheritdoc />
    public NodeSelectionStrategy DetermineOptimalStrategy(EncodingTaskDefinition task)
    {
        // GPU-intensive tasks: prioritize hardware capability
        if (task.RequiresGpu)
        {
            return NodeSelectionStrategy.BestCapability;
        }

        // Heavy tasks: prioritize least loaded nodes
        if (task.Weight > 50.0)
        {
            return NodeSelectionStrategy.LeastLoaded;
        }

        // Video encoding tasks: prioritize capability
        if (task.TaskType == EncodingTaskType.VideoEncoding ||
            task.TaskType == EncodingTaskType.HdrConversion)
        {
            return NodeSelectionStrategy.BestCapability;
        }

        // Light tasks (audio, subtitles, etc.): round-robin for even distribution
        if (task.Weight < 10.0)
        {
            return NodeSelectionStrategy.RoundRobin;
        }

        // Default: least loaded for general balance
        return NodeSelectionStrategy.LeastLoaded;
    }

    /// <summary>
    /// Select a node using the specified strategy
    /// </summary>
    private NodeCandidate SelectByStrategy(
        List<NodeCandidate> candidates,
        EncodingTaskDefinition task,
        NodeSelectionStrategy strategy,
        NodeSelectionOptions options)
    {
        return strategy switch
        {
            NodeSelectionStrategy.LeastLoaded => SelectLeastLoaded(candidates),
            NodeSelectionStrategy.BestCapability => SelectBestCapability(candidates),
            NodeSelectionStrategy.RoundRobin => SelectRoundRobin(candidates),
            NodeSelectionStrategy.Fastest => SelectFastest(candidates),
            _ => SelectBestCapability(candidates) // Default fallback
        };
    }

    /// <summary>
    /// Select the node with the lowest current load
    /// </summary>
    private NodeCandidate SelectLeastLoaded(List<NodeCandidate> candidates)
    {
        return candidates
            .OrderBy(c => (double)c.Node.CurrentTaskCount / c.Node.MaxConcurrentTasks)
            .ThenByDescending(c => c.Score)
            .First();
    }

    /// <summary>
    /// Select the node with the best hardware capability score
    /// </summary>
    private NodeCandidate SelectBestCapability(List<NodeCandidate> candidates)
    {
        return candidates
            .OrderByDescending(c => c.Score)
            .First();
    }

    /// <summary>
    /// Select nodes in round-robin order
    /// </summary>
    private NodeCandidate SelectRoundRobin(List<NodeCandidate> candidates)
    {
        _roundRobinIndex = (_roundRobinIndex + 1) % candidates.Count;
        return candidates[_roundRobinIndex];
    }

    /// <summary>
    /// Select the fastest node based on hardware capability
    /// Note: In a full implementation, this would use historical performance data
    /// </summary>
    private NodeCandidate SelectFastest(List<NodeCandidate> candidates)
    {
        // For now, use a heuristic based on hardware
        // GPU nodes are generally faster for video encoding
        NodeCandidate? gpuNode = candidates
            .Where(c => c.Node.HasGpu)
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();

        if (gpuNode is not null)
        {
            return gpuNode;
        }

        // Fall back to highest CPU core count
        return candidates
            .OrderByDescending(c => c.Node.CpuCores)
            .ThenByDescending(c => c.Score)
            .First();
    }

    /// <summary>
    /// Calculate GPU match score
    /// </summary>
    private double CalculateGpuScore(EncoderNode node, EncodingTaskDefinition task)
    {
        if (!task.RequiresGpu)
        {
            // GPU not required, give neutral score
            return 50.0;
        }

        if (!node.HasGpu)
        {
            // Task wants GPU but node doesn't have one
            return 10.0;
        }

        // Node has GPU and task wants it
        double score = 80.0;

        // Bonus for NVIDIA GPUs (NVENC is widely supported and fast)
        if (string.Equals(node.GpuVendor, GpuVendor.Nvidia, StringComparison.OrdinalIgnoreCase))
        {
            score += 20.0;
        }
        else if (string.Equals(node.GpuVendor, GpuVendor.Intel, StringComparison.OrdinalIgnoreCase))
        {
            score += 10.0; // QSV is also good
        }

        return Math.Min(score, 100.0);
    }

    /// <summary>
    /// Calculate capacity score based on available slots
    /// </summary>
    private double CalculateCapacityScore(EncoderNode node)
    {
        if (node.MaxConcurrentTasks <= 0)
        {
            return 0.0;
        }

        double availableRatio = (double)(node.MaxConcurrentTasks - node.CurrentTaskCount) / node.MaxConcurrentTasks;
        return availableRatio * 100.0;
    }

    /// <summary>
    /// Calculate memory score
    /// </summary>
    private double CalculateMemoryScore(EncoderNode node, EncodingTaskDefinition task)
    {
        int requiredMemoryGb = task.EstimatedMemoryMb > 0
            ? (int)Math.Ceiling(task.EstimatedMemoryMb / 1024.0)
            : 4; // Default assumption

        if (node.MemoryGb < requiredMemoryGb)
        {
            return 0.0;
        }

        // Score based on how much memory is available above requirement
        double excessRatio = (double)(node.MemoryGb - requiredMemoryGb) / requiredMemoryGb;
        return Math.Min(50.0 + excessRatio * 50.0, 100.0);
    }

    /// <summary>
    /// Calculate CPU score based on core count
    /// </summary>
    private double CalculateCpuScore(EncoderNode node)
    {
        // Normalize to a common range (assuming 32 cores is high-end)
        double normalizedCores = Math.Min(node.CpuCores / 32.0, 1.0);
        return normalizedCores * 100.0;
    }

    /// <summary>
    /// Calculate hardware acceleration match score
    /// </summary>
    private double CalculateAccelerationScore(EncoderNode node, EncodingTaskDefinition task)
    {
        string[] accelerations = node.SupportedAccelerations;

        if (accelerations.Length == 0)
        {
            return 30.0; // CPU-only is baseline
        }

        double score = 50.0;

        // Bonus for each supported acceleration type
        if (accelerations.Contains(HardwareAccelerationType.Nvenc, StringComparer.OrdinalIgnoreCase))
        {
            score += 20.0; // NVENC is excellent
        }

        if (accelerations.Contains(HardwareAccelerationType.Qsv, StringComparer.OrdinalIgnoreCase))
        {
            score += 15.0; // QSV is very good
        }

        if (accelerations.Contains(HardwareAccelerationType.Vaapi, StringComparer.OrdinalIgnoreCase))
        {
            score += 10.0; // VAAPI is good on Linux
        }

        if (accelerations.Contains(HardwareAccelerationType.VideoToolbox, StringComparer.OrdinalIgnoreCase))
        {
            score += 15.0; // VideoToolbox is good on macOS
        }

        return Math.Min(score, 100.0);
    }

    /// <summary>
    /// Build score breakdown dictionary
    /// </summary>
    private Dictionary<string, double> BuildScoreBreakdown(EncoderNode node, EncodingTaskDefinition task)
    {
        return new Dictionary<string, double>
        {
            ["gpu"] = CalculateGpuScore(node, task),
            ["capacity"] = CalculateCapacityScore(node),
            ["memory"] = CalculateMemoryScore(node, task),
            ["cpu"] = CalculateCpuScore(node),
            ["acceleration"] = CalculateAccelerationScore(node, task)
        };
    }

    /// <summary>
    /// Build a detailed reason string for when no capable nodes are found
    /// </summary>
    private string BuildNoCapableNodesReason(
        EncodingTaskDefinition task,
        List<EncoderNode> nodes,
        NodeSelectionOptions options)
    {
        List<string> reasons = [];

        int disabledCount = nodes.Count(n => !n.IsEnabled);
        if (disabledCount > 0)
        {
            reasons.Add($"{disabledCount} node(s) disabled");
        }

        int unhealthyCount = nodes.Count(n => !n.IsHealthy);
        if (unhealthyCount > 0)
        {
            reasons.Add($"{unhealthyCount} node(s) unhealthy");
        }

        int fullCount = nodes.Count(n => n.CurrentTaskCount >= n.MaxConcurrentTasks);
        if (fullCount > 0 && options.ExcludeFullNodes)
        {
            reasons.Add($"{fullCount} node(s) at capacity");
        }

        if (task.RequiresGpu && options.StrictGpuRequirement)
        {
            int noGpuCount = nodes.Count(n => !n.HasGpu);
            if (noGpuCount > 0)
            {
                reasons.Add($"{noGpuCount} node(s) lack required GPU");
            }
        }

        int lowMemoryCount = nodes.Count(n => n.MemoryGb < options.MinimumMemoryGb);
        if (lowMemoryCount > 0)
        {
            reasons.Add($"{lowMemoryCount} node(s) have insufficient memory");
        }

        if (reasons.Count == 0)
        {
            return "No nodes meet the task requirements";
        }

        return $"No capable nodes: {string.Join(", ", reasons)}";
    }
}
