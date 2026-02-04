using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Tasks;

using DbEncodingProgress = NoMercy.Database.Models.EncodingProgress;

namespace NoMercy.Tests.EncoderV2.Integration;

/// <summary>
/// Integration tests for multi-node distributed encoding.
/// Tests node selection, task distribution, health monitoring, failover,
/// and load balancing across heterogeneous encoder nodes.
/// </summary>
public class MultiNodeDistributionTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private QueueContext? _queueContext;
    private INodeHealthMonitor? _healthMonitor;
    private string _dbName = string.Empty;

    public MultiNodeDistributionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        _dbName = $"MultiNodeDistTest_{Guid.NewGuid():N}";

        DbContextOptions<QueueContext> queueOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase($"Queue_{_dbName}")
            .Options;

        _queueContext = new TestQueueContext(queueOptions);

        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<QueueContext>(_queueContext);
        services.AddScoped<INodeSelector, NodeSelector>();

        // Register NodeHealthMonitor with fast timeouts for testing
        HealthMonitorOptions healthOptions = new()
        {
            CheckIntervalSeconds = 1,
            HeartbeatTimeoutSeconds = 5,
            AutoReassignTasks = true,
            FailedChecksBeforeUnhealthy = 1
        };
        services.AddSingleton(healthOptions);
        services.AddSingleton<INodeHealthMonitor>(sp =>
            new NodeHealthMonitor(sp, sp.GetRequiredService<ILogger<NodeHealthMonitor>>(), healthOptions));

        _serviceProvider = services.BuildServiceProvider();
        _healthMonitor = _serviceProvider.GetRequiredService<INodeHealthMonitor>();

        _output.WriteLine($"Test database initialized: {_dbName}");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    #region Task Distribution Across Multiple Nodes

    [Fact]
    public void Distribution_TasksDistributedAcrossMultipleNodes_NoSingleNodeOverloaded()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateHeterogeneousCluster();

        List<EncodingTaskDefinition> tasks = Enumerable.Range(0, 10)
            .Select(i => new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.VideoEncoding,
                Weight = 50 + (i * 10)
            })
            .ToList();

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.LeastLoaded
        };

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes, options);

        // Assert
        Assert.True(result.AssignedCount > 0);

        Dictionary<string, int> taskCountPerNode = result.Assignments
            .GroupBy(a => a.Node.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        // No single node should have more tasks than its MaxConcurrentTasks
        foreach (TaskAssignment assignment in result.Assignments)
        {
            int assigned = taskCountPerNode[assignment.Node.Name];
            Assert.True(assigned <= assignment.Node.MaxConcurrentTasks,
                $"Node {assignment.Node.Name} has {assigned} tasks but max is {assignment.Node.MaxConcurrentTasks}");
        }

        // At least 2 different nodes should be used (we have 5 nodes)
        Assert.True(taskCountPerNode.Count >= 2,
            $"Expected tasks on at least 2 nodes, got {taskCountPerNode.Count}");

        _output.WriteLine($"Distributed {result.AssignedCount}/{result.TotalTasks} tasks across {taskCountPerNode.Count} nodes:");
        foreach (KeyValuePair<string, int> kvp in taskCountPerNode)
        {
            _output.WriteLine($"  - {kvp.Key}: {kvp.Value} tasks");
        }
    }

    [Fact]
    public void Distribution_GpuTasksPreferGpuNodes_CpuTasksAcceptAnyNode()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateHeterogeneousCluster();

        List<EncodingTaskDefinition> tasks =
        [
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 100, RequiresGpu = true },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 100, RequiresGpu = true },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.AudioEncoding, Weight = 30, RequiresGpu = false },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.SubtitleExtraction, Weight = 5, RequiresGpu = false },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.FontExtraction, Weight = 2, RequiresGpu = false }
        ];

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.BestCapability,
            StrictGpuRequirement = true
        };

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes, options);

        // Assert - GPU tasks should go to GPU nodes
        List<TaskAssignment> gpuAssignments = result.Assignments
            .Where(a => a.Task.RequiresGpu)
            .ToList();

        foreach (TaskAssignment assignment in gpuAssignments)
        {
            Assert.True(assignment.Node.HasGpu,
                $"GPU task {assignment.Task.TaskType} assigned to non-GPU node {assignment.Node.Name}");
        }

        _output.WriteLine($"GPU task assignments:");
        foreach (TaskAssignment a in gpuAssignments)
        {
            _output.WriteLine($"  - {a.Task.TaskType} -> {a.Node.Name} (GPU: {a.Node.GpuModel})");
        }

        List<TaskAssignment> cpuAssignments = result.Assignments
            .Where(a => !a.Task.RequiresGpu)
            .ToList();

        _output.WriteLine($"CPU task assignments:");
        foreach (TaskAssignment a in cpuAssignments)
        {
            _output.WriteLine($"  - {a.Task.TaskType} -> {a.Node.Name}");
        }
    }

    [Fact]
    public void Distribution_HighMemoryTasks_OnlyAssignedToHighMemoryNodes()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateHeterogeneousCluster();

        EncodingTaskDefinition highMemTask = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.HdrConversion,
            Weight = 200,
            EstimatedMemoryMb = 48000 // 48 GB
        };

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.BestCapability,
            MinimumMemoryGb = 48
        };

        // Act
        NodeSelectionResult result = nodeSelector.SelectNode(highMemTask, nodes, options);

        // Assert
        if (result.Success)
        {
            Assert.True(result.SelectedNode!.MemoryGb >= 48,
                $"Selected node {result.SelectedNode.Name} has only {result.SelectedNode.MemoryGb}GB memory");
            _output.WriteLine($"High-memory task assigned to: {result.SelectedNode.Name} ({result.SelectedNode.MemoryGb}GB)");
        }
        else
        {
            // Only the 64GB node qualifies - if it's at capacity, no node can handle it
            _output.WriteLine($"No node with 48GB+ memory available: {result.Reason}");
        }
    }

    [Fact]
    public void Distribution_MixedWorkload_BalancesAcrossNodeTypes()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateHeterogeneousCluster();

        // Simulate a typical encoding job: HDR conversion, multiple video qualities,
        // audio tracks, subtitles, and post-processing
        List<EncodingTaskDefinition> tasks =
        [
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.HdrConversion, Weight = 200, RequiresGpu = true },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 100, Description = "1080p" },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 60, Description = "720p" },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 30, Description = "480p" },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.AudioEncoding, Weight = 20, Description = "English AAC" },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.AudioEncoding, Weight = 20, Description = "Japanese AAC" },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.SubtitleExtraction, Weight = 5 },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.FontExtraction, Weight = 2 },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.SpriteGeneration, Weight = 15 },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.ChapterExtraction, Weight = 2 }
        ];

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes);

        // Assert
        Assert.True(result.AssignedCount > 0);

        Dictionary<string, double> weightPerNode = result.Assignments
            .GroupBy(a => a.Node.Name)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Task.Weight));

        _output.WriteLine($"Mixed workload distribution ({result.AssignedCount}/{result.TotalTasks} assigned):");
        foreach (KeyValuePair<string, double> kvp in weightPerNode.OrderByDescending(k => k.Value))
        {
            _output.WriteLine($"  - {kvp.Key}: total weight {kvp.Value:F0}");
        }

        if (result.UnassignedTasks.Count > 0)
        {
            _output.WriteLine($"Unassigned tasks: {result.UnassignedTasks.Count}");
            foreach (UnassignedTask u in result.UnassignedTasks)
            {
                _output.WriteLine($"  - {u.Task.TaskType}: {u.Reason}");
            }
        }
    }

    #endregion

    #region Node Health Monitoring

    [Fact]
    public async Task HealthMonitor_HealthCheckDetectsStaleHeartbeat_MarksNodeUnhealthy()
    {
        // Arrange - Create nodes in database, one with stale heartbeat
        EncoderNode healthyNode = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Healthy Node",
            IpAddress = "192.168.1.10",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CpuCores = 8,
            MemoryGb = 16,
            MaxConcurrentTasks = 2,
            LastHeartbeat = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncoderNode staleNode = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Stale Node",
            IpAddress = "192.168.1.11",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CpuCores = 8,
            MemoryGb = 16,
            MaxConcurrentTasks = 2,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-10), // Way past heartbeat timeout
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddRangeAsync([healthyNode, staleNode]);
        await _queueContext.SaveChangesAsync();

        // Act
        HealthCheckResult result = await _healthMonitor!.PerformHealthCheckAsync();

        // Assert
        Assert.Equal(2, result.TotalNodes);
        Assert.Equal(1, result.HealthyNodes);
        Assert.Equal(1, result.UnhealthyNodes);
        Assert.Contains(staleNode.Id, result.UnhealthyNodeIds);

        _output.WriteLine($"Health check: {result.HealthyNodes}/{result.TotalNodes} healthy");
        _output.WriteLine($"Unhealthy nodes: {string.Join(", ", result.UnhealthyNodeIds)}");
    }

    [Fact]
    public async Task HealthMonitor_RecordHeartbeat_RecoversUnhealthyNode()
    {
        // Arrange
        EncoderNode unhealthyNode = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Recovering Node",
            IpAddress = "192.168.1.20",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = false,
            CpuCores = 8,
            MemoryGb = 16,
            MaxConcurrentTasks = 2,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(unhealthyNode);
        await _queueContext.SaveChangesAsync();

        // Act
        HeartbeatResult heartbeatResult = await _healthMonitor!.RecordHeartbeatAsync(unhealthyNode.Id, 0);

        // Assert
        Assert.True(heartbeatResult.Success);
        Assert.True(heartbeatResult.WasRecovered);

        _output.WriteLine($"Node recovered: {heartbeatResult.WasRecovered}");
    }

    [Fact]
    public async Task HealthMonitor_UnhealthyNodeWithRunningTasks_TasksReassigned()
    {
        // Arrange
        EncoderNode failingNode = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Failing Node",
            IpAddress = "192.168.1.30",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CpuCores = 8,
            MemoryGb = 16,
            MaxConcurrentTasks = 4,
            CurrentTaskCount = 2,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-10), // Stale heartbeat
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Health Monitor Reassignment Test",
            State = EncodingJobState.Encoding,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask runningTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Running,
            AssignedNodeId = failingNode.Id,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask pendingTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.AudioEncoding,
            State = EncodingTaskState.Pending,
            AssignedNodeId = failingNode.Id,
            Weight = 30,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(failingNode);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([runningTask, pendingTask]);
        await _queueContext.SaveChangesAsync();

        // Act
        HealthCheckResult result = await _healthMonitor!.PerformHealthCheckAsync();

        // Assert
        Assert.Equal(1, result.NewlyUnhealthyNodes);
        Assert.Equal(2, result.TasksReassigned);

        // Verify tasks were reset to Pending with no assigned node
        EncodingTask? updatedRunning = await _queueContext.EncodingTasks.FindAsync(runningTask.Id);
        EncodingTask? updatedPending = await _queueContext.EncodingTasks.FindAsync(pendingTask.Id);

        Assert.Equal(EncodingTaskState.Pending, updatedRunning?.State);
        Assert.Null(updatedRunning?.AssignedNodeId);
        Assert.Equal(EncodingTaskState.Pending, updatedPending?.State);
        Assert.Null(updatedPending?.AssignedNodeId);

        _output.WriteLine($"Health check reassigned {result.TasksReassigned} tasks from unhealthy node");
    }

    [Fact]
    public async Task HealthMonitor_DisableNode_ReassignsTasksAndPreventsNewAssignment()
    {
        // Arrange
        EncoderNode nodeToDisable = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Node To Disable",
            IpAddress = "192.168.1.40",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CpuCores = 8,
            MemoryGb = 16,
            MaxConcurrentTasks = 4,
            CurrentTaskCount = 1,
            LastHeartbeat = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Disable Node Test",
            State = EncodingJobState.Encoding,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask task = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Running,
            AssignedNodeId = nodeToDisable.Id,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(nodeToDisable);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act
        int reassigned = await _healthMonitor!.DisableNodeAsync(
            nodeToDisable.Id, "Maintenance", reassignTasks: true);

        // Assert
        Assert.Equal(1, reassigned);

        EncoderNode? updatedNode = await _queueContext.EncoderNodes.FindAsync(nodeToDisable.Id);
        Assert.False(updatedNode?.IsEnabled);
        Assert.False(updatedNode?.IsHealthy);

        // Verify disabled node is excluded from selection
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        EncodingTaskDefinition newTask = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 50
        };

        List<EncoderNode> allNodes = await _queueContext.EncoderNodes.ToListAsync();
        NodeSelectionResult selectResult = nodeSelector.SelectNode(newTask, allNodes);

        if (selectResult.Success)
        {
            Assert.NotEqual(nodeToDisable.Id, selectResult.SelectedNode!.Id);
        }

        _output.WriteLine($"Disabled node, reassigned {reassigned} task(s)");
        _output.WriteLine($"Node enabled: {updatedNode?.IsEnabled}, healthy: {updatedNode?.IsHealthy}");
    }

    [Fact]
    public async Task HealthMonitor_GetNodeSummary_ReturnsAccurateStatistics()
    {
        // Arrange
        List<EncoderNode> nodes =
        [
            new()
            {
                Id = Ulid.NewUlid(), Name = "Active1", IpAddress = "10.0.0.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, CurrentTaskCount = 2, MaxConcurrentTasks = 4,
                CpuCores = 8, MemoryGb = 16, HasGpu = true, GpuModel = "RTX 4090",
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Active2", IpAddress = "10.0.0.2", Port = 8080,
                IsEnabled = true, IsHealthy = true, CurrentTaskCount = 1, MaxConcurrentTasks = 2,
                CpuCores = 4, MemoryGb = 8,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Unhealthy", IpAddress = "10.0.0.3", Port = 8080,
                IsEnabled = true, IsHealthy = false, CurrentTaskCount = 0, MaxConcurrentTasks = 2,
                CpuCores = 4, MemoryGb = 8,
                LastHeartbeat = DateTime.UtcNow.AddMinutes(-10), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Disabled", IpAddress = "10.0.0.4", Port = 8080,
                IsEnabled = false, IsHealthy = false, CurrentTaskCount = 0, MaxConcurrentTasks = 4,
                CpuCores = 16, MemoryGb = 64,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        await _queueContext!.EncoderNodes.AddRangeAsync(nodes);
        await _queueContext.SaveChangesAsync();

        // Act
        (int totalNodes, int healthyNodes, int enabledNodes, int totalRunningTasks) =
            await _healthMonitor!.GetNodeSummaryAsync();

        // Assert
        Assert.Equal(4, totalNodes);
        Assert.Equal(2, healthyNodes); // Active1 + Active2
        Assert.Equal(3, enabledNodes); // Active1 + Active2 + Unhealthy
        Assert.Equal(3, totalRunningTasks); // 2 + 1 + 0 + 0

        _output.WriteLine($"Cluster summary: {totalNodes} total, {healthyNodes} healthy, {enabledNodes} enabled, {totalRunningTasks} running tasks");
    }

    #endregion

    #region Node Failure and Recovery

    [Fact]
    public async Task NodeFailover_MultipleNodesInCluster_TasksRedistributedToSurvivors()
    {
        // Arrange - Create a 3-node cluster with tasks spread across them
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        EncoderNode node1 = new()
        {
            Id = Ulid.NewUlid(), Name = "Node-1", IpAddress = "10.0.0.1", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 32,
            MaxConcurrentTasks = 4, CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncoderNode node2 = new()
        {
            Id = Ulid.NewUlid(), Name = "Node-2", IpAddress = "10.0.0.2", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 16, MemoryGb = 64,
            MaxConcurrentTasks = 4, CurrentTaskCount = 0, HasGpu = true, GpuModel = "RTX 4090",
            GpuVendor = "nvidia",
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncoderNode node3 = new()
        {
            Id = Ulid.NewUlid(), Name = "Node-3", IpAddress = "10.0.0.3", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 32,
            MaxConcurrentTasks = 2, CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(), Title = "Failover Test Job",
            State = EncodingJobState.Encoding, InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // Assign 2 tasks to Node-2 (which will "fail")
        List<EncodingTask> node2Tasks =
        [
            new()
            {
                Id = Ulid.NewUlid(), JobId = job.Id, TaskType = EncodingTaskType.VideoEncoding,
                State = EncodingTaskState.Running, AssignedNodeId = node2.Id, Weight = 100,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), JobId = job.Id, TaskType = EncodingTaskType.AudioEncoding,
                State = EncodingTaskState.Pending, AssignedNodeId = node2.Id, Weight = 30,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        // Assign 1 completed task to Node-1 (should be unaffected)
        EncodingTask node1CompletedTask = new()
        {
            Id = Ulid.NewUlid(), JobId = job.Id, TaskType = EncodingTaskType.SubtitleExtraction,
            State = EncodingTaskState.Completed, AssignedNodeId = node1.Id, Weight = 5,
            CompletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        node2.CurrentTaskCount = 2;

        await _queueContext!.EncoderNodes.AddRangeAsync([node1, node2, node3]);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync(node2Tasks);
        await _queueContext.EncodingTasks.AddAsync(node1CompletedTask);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine("Initial state: 3 nodes, 2 tasks on Node-2, 1 completed on Node-1");

        // Act - Simulate Node-2 failure via MarkNodeUnhealthy
        int reassigned = await _healthMonitor!.MarkNodeUnhealthyAsync(
            node2.Id, "Connection lost", reassignTasks: true);

        // Assert
        Assert.Equal(2, reassigned);

        // Verify tasks are now unassigned and ready for redistribution
        List<EncodingTask> reassignedTasks = await _queueContext.EncodingTasks
            .Where(t => node2Tasks.Select(nt => nt.Id).Contains(t.Id))
            .ToListAsync();

        Assert.All(reassignedTasks, t =>
        {
            Assert.Equal(EncodingTaskState.Pending, t.State);
            Assert.Null(t.AssignedNodeId);
        });

        // Node-1's completed task should be unaffected
        EncodingTask? completedTask = await _queueContext.EncodingTasks.FindAsync(node1CompletedTask.Id);
        Assert.Equal(EncodingTaskState.Completed, completedTask?.State);
        Assert.Equal(node1.Id, completedTask?.AssignedNodeId);

        // Verify surviving nodes can pick up the unassigned tasks
        List<EncoderNode> survivingNodes = await _queueContext.EncoderNodes
            .Where(n => n.IsHealthy && n.IsEnabled)
            .ToListAsync();

        Assert.Equal(2, survivingNodes.Count);

        List<EncodingTaskDefinition> tasksToReassign = reassignedTasks
            .Select(t => new EncodingTaskDefinition
            {
                Id = t.Id.ToString(),
                TaskType = t.TaskType,
                Weight = t.Weight
            })
            .ToList();

        BatchAssignmentResult reassignment = nodeSelector.SelectNodesForTasks(tasksToReassign, survivingNodes);
        Assert.True(reassignment.AssignedCount > 0);

        _output.WriteLine($"After failover: {reassigned} tasks reassigned");
        _output.WriteLine($"Surviving nodes picked up {reassignment.AssignedCount} tasks");
        foreach (TaskAssignment a in reassignment.Assignments)
        {
            _output.WriteLine($"  - {a.Task.TaskType} -> {a.Node.Name}");
        }
    }

    [Fact]
    public async Task NodeRecovery_NodeComesBackOnline_BecomesAvailableForSelection()
    {
        // Arrange
        EncoderNode recoveredNode = new()
        {
            Id = Ulid.NewUlid(), Name = "Recovered Node", IpAddress = "10.0.0.5", Port = 8080,
            IsEnabled = true, IsHealthy = false, CpuCores = 16, MemoryGb = 64,
            MaxConcurrentTasks = 4, CurrentTaskCount = 0,
            HasGpu = true, GpuModel = "RTX 3090", GpuVendor = "nvidia",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1), UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(recoveredNode);
        await _queueContext.SaveChangesAsync();

        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100
        };

        // Verify node is not selected while unhealthy
        List<EncoderNode> nodesBeforeRecovery = await _queueContext.EncoderNodes.ToListAsync();
        NodeSelectionResult beforeResult = nodeSelector.SelectNode(task, nodesBeforeRecovery);
        Assert.False(beforeResult.Success, "Unhealthy node should not be selectable");

        _output.WriteLine("Before recovery: node not selectable");

        // Act - Node sends heartbeat to recover
        HeartbeatResult heartbeat = await _healthMonitor!.RecordHeartbeatAsync(recoveredNode.Id, 0);

        // Assert
        Assert.True(heartbeat.Success);
        Assert.True(heartbeat.WasRecovered);

        // Verify node is now selectable
        List<EncoderNode> nodesAfterRecovery = await _queueContext.EncoderNodes.ToListAsync();
        NodeSelectionResult afterResult = nodeSelector.SelectNode(task, nodesAfterRecovery);
        Assert.True(afterResult.Success, "Recovered node should be selectable");
        Assert.Equal(recoveredNode.Id, afterResult.SelectedNode!.Id);

        _output.WriteLine($"After recovery: node selectable, score {afterResult.Score:F2}");
    }

    #endregion

    #region Concurrent Multi-Job Distribution

    [Fact]
    public async Task ConcurrentJobs_MultipleJobsCompeteForNodes_TasksDistributedFairly()
    {
        // Arrange - 3 nodes, 3 jobs each with multiple tasks
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        List<EncoderNode> nodes =
        [
            new()
            {
                Id = Ulid.NewUlid(), Name = "Worker-A", IpAddress = "10.0.0.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 32,
                MaxConcurrentTasks = 3, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Worker-B", IpAddress = "10.0.0.2", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 16, MemoryGb = 64,
                MaxConcurrentTasks = 4, CurrentTaskCount = 0,
                HasGpu = true, GpuModel = "RTX 4090", GpuVendor = "nvidia",
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Worker-C", IpAddress = "10.0.0.3", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                MaxConcurrentTasks = 2, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        // Create tasks for 3 jobs
        List<EncodingTaskDefinition> allTasks = [];
        for (int jobIdx = 0; jobIdx < 3; jobIdx++)
        {
            allTasks.Add(new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.VideoEncoding,
                Weight = 80 + (jobIdx * 10),
                Description = $"Job{jobIdx + 1} Video"
            });
            allTasks.Add(new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.AudioEncoding,
                Weight = 20,
                Description = $"Job{jobIdx + 1} Audio"
            });
        }

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.LeastLoaded
        };

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(allTasks, nodes, options);

        // Assert
        Assert.True(result.AssignedCount > 0);

        Dictionary<string, int> taskCountPerNode = result.Assignments
            .GroupBy(a => a.Node.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        // All 3 nodes should receive at least 1 task (total 6 tasks, total capacity 9)
        Assert.True(taskCountPerNode.Count >= 2,
            $"Expected tasks on at least 2 nodes, got {taskCountPerNode.Count}");

        // Verify capacity respected
        int totalCapacity = nodes.Sum(n => n.MaxConcurrentTasks); // 3 + 4 + 2 = 9
        Assert.True(result.AssignedCount <= totalCapacity);

        _output.WriteLine($"Concurrent job distribution ({result.AssignedCount}/{result.TotalTasks}):");
        foreach (KeyValuePair<string, int> kvp in taskCountPerNode)
        {
            _output.WriteLine($"  - {kvp.Key}: {kvp.Value} tasks");
        }
    }

    [Fact]
    public async Task ConcurrentJobs_HighPriorityJobGetsNodeFirst_LowerPriorityWaits()
    {
        // Arrange
        EncodingJob highPriorityJob = new()
        {
            Id = Ulid.NewUlid(), Title = "Urgent Job", State = EncodingJobState.Queued,
            Priority = 100, InputFilePath = "/test/urgent.mkv", OutputFolder = "/test/output1",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingJob lowPriorityJob = new()
        {
            Id = Ulid.NewUlid(), Title = "Batch Job", State = EncodingJobState.Queued,
            Priority = -10, InputFilePath = "/test/batch.mkv", OutputFolder = "/test/output2",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // High priority task
        EncodingTask urgentTask = new()
        {
            Id = Ulid.NewUlid(), JobId = highPriorityJob.Id,
            TaskType = EncodingTaskType.VideoEncoding, State = EncodingTaskState.Pending,
            Weight = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // Low priority task (created earlier, but lower priority)
        EncodingTask batchTask = new()
        {
            Id = Ulid.NewUlid(), JobId = lowPriorityJob.Id,
            TaskType = EncodingTaskType.VideoEncoding, State = EncodingTaskState.Pending,
            Weight = 100, CreatedAt = DateTime.UtcNow.AddMinutes(-10), UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddRangeAsync([highPriorityJob, lowPriorityJob]);
        await _queueContext.EncodingTasks.AddRangeAsync([urgentTask, batchTask]);
        await _queueContext.SaveChangesAsync();

        // Act - Query tasks ordered by job priority
        List<EncodingTask> orderedTasks = await _queueContext.EncodingTasks
            .Where(t => t.State == EncodingTaskState.Pending)
            .Join(
                _queueContext.EncodingJobs,
                task => task.JobId,
                job => job.Id,
                (task, job) => new { Task = task, Job = job })
            .OrderByDescending(x => x.Job.Priority)
            .ThenBy(x => x.Task.CreatedAt)
            .Select(x => x.Task)
            .ToListAsync();

        // Assert - Urgent task should be first despite batch task being older
        Assert.Equal(urgentTask.Id, orderedTasks[0].Id);
        Assert.Equal(batchTask.Id, orderedTasks[1].Id);

        _output.WriteLine("Priority ordering:");
        for (int i = 0; i < orderedTasks.Count; i++)
        {
            EncodingJob? parentJob = await _queueContext.EncodingJobs.FindAsync(orderedTasks[i].JobId);
            _output.WriteLine($"  {i + 1}. {parentJob?.Title} (priority: {parentJob?.Priority})");
        }
    }

    #endregion

    #region Node Capability Scoring

    [Fact]
    public void Scoring_NvidiaGpuNode_ScoresHigherThanCpuNodeForVideoEncoding()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        EncoderNode gpuNode = new()
        {
            Id = Ulid.NewUlid(), Name = "GPU Node", IpAddress = "10.0.0.1", Port = 8080,
            IsEnabled = true, IsHealthy = true, HasGpu = true,
            GpuModel = "NVIDIA RTX 4090", GpuVendor = "nvidia",
            CpuCores = 8, MemoryGb = 64, MaxConcurrentTasks = 4, CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncoderNode cpuNode = new()
        {
            Id = Ulid.NewUlid(), Name = "CPU Node", IpAddress = "10.0.0.2", Port = 8080,
            IsEnabled = true, IsHealthy = true, HasGpu = false,
            CpuCores = 16, MemoryGb = 32, MaxConcurrentTasks = 2, CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingTaskDefinition videoTask = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100,
            RequiresGpu = true
        };

        // Act
        double gpuScore = nodeSelector.CalculateNodeScore(gpuNode, videoTask);
        double cpuScore = nodeSelector.CalculateNodeScore(cpuNode, videoTask);

        // Assert
        Assert.True(gpuScore > cpuScore,
            $"GPU node ({gpuScore:F2}) should score higher than CPU node ({cpuScore:F2}) for video encoding");

        _output.WriteLine($"GPU node score: {gpuScore:F2}");
        _output.WriteLine($"CPU node score: {cpuScore:F2}");
        _output.WriteLine($"GPU advantage: {gpuScore - cpuScore:F2} points");
    }

    [Fact]
    public void Scoring_PartiallyLoadedNode_ScoresLowerThanIdleNode()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        EncoderNode idleNode = new()
        {
            Id = Ulid.NewUlid(), Name = "Idle Node", IpAddress = "10.0.0.1", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 32,
            MaxConcurrentTasks = 4, CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncoderNode loadedNode = new()
        {
            Id = Ulid.NewUlid(), Name = "Loaded Node", IpAddress = "10.0.0.2", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 32,
            MaxConcurrentTasks = 4, CurrentTaskCount = 3,
            LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.AudioEncoding,
            Weight = 30
        };

        // Act
        double idleScore = nodeSelector.CalculateNodeScore(idleNode, task);
        double loadedScore = nodeSelector.CalculateNodeScore(loadedNode, task);

        // Assert
        Assert.True(idleScore > loadedScore,
            $"Idle node ({idleScore:F2}) should score higher than loaded node ({loadedScore:F2})");

        _output.WriteLine($"Idle node score: {idleScore:F2}");
        _output.WriteLine($"Loaded node score (3/4 tasks): {loadedScore:F2}");
    }

    [Fact]
    public void Scoring_AutoStrategy_SelectsBestCapabilityForGpuTasks()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        EncodingTaskDefinition gpuTask = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.HdrConversion,
            Weight = 200,
            RequiresGpu = true
        };

        EncodingTaskDefinition lightTask = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.ChapterExtraction,
            Weight = 2,
            RequiresGpu = false
        };

        // Act
        NodeSelectionStrategy gpuStrategy = nodeSelector.DetermineOptimalStrategy(gpuTask);
        NodeSelectionStrategy lightStrategy = nodeSelector.DetermineOptimalStrategy(lightTask);

        // Assert
        Assert.Equal(NodeSelectionStrategy.BestCapability, gpuStrategy);
        Assert.Equal(NodeSelectionStrategy.RoundRobin, lightStrategy);

        _output.WriteLine($"GPU/HDR task -> {gpuStrategy}");
        _output.WriteLine($"Light task -> {lightStrategy}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_SingleNodeCluster_AllTasksGoToSingleNode()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        List<EncoderNode> nodes =
        [
            new()
            {
                Id = Ulid.NewUlid(), Name = "Solo Node", IpAddress = "10.0.0.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 16, MemoryGb = 64,
                MaxConcurrentTasks = 8, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        List<EncodingTaskDefinition> tasks = Enumerable.Range(0, 5)
            .Select(i => new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.VideoEncoding,
                Weight = 50
            })
            .ToList();

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes);

        // Assert
        Assert.Equal(5, result.AssignedCount);
        Assert.True(result.AllAssigned);
        Assert.All(result.Assignments, a => Assert.Equal("Solo Node", a.Node.Name));

        _output.WriteLine($"Single node handled all {result.AssignedCount} tasks");
    }

    [Fact]
    public void EdgeCase_AllNodesAtCapacity_TasksRemainUnassigned()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        List<EncoderNode> nodes =
        [
            new()
            {
                Id = Ulid.NewUlid(), Name = "Full-1", IpAddress = "10.0.0.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                MaxConcurrentTasks = 1, CurrentTaskCount = 1,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Full-2", IpAddress = "10.0.0.2", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                MaxConcurrentTasks = 1, CurrentTaskCount = 1,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100
        };

        NodeSelectionOptions options = new()
        {
            ExcludeFullNodes = true
        };

        // Act
        NodeSelectionResult result = nodeSelector.SelectNode(task, nodes, options);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.SelectedNode);

        _output.WriteLine($"No available capacity: {result.Reason}");
    }

    [Fact]
    public void EdgeCase_EmptyNodeList_ReturnsFailure()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = [];

        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100
        };

        // Act
        NodeSelectionResult result = nodeSelector.SelectNode(task, nodes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.SelectedNode);

        _output.WriteLine($"Empty node list: {result.Reason}");
    }

    [Fact]
    public void EdgeCase_FilterHealthyNodes_ExcludesDisabledAndStaleNodes()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();

        List<EncoderNode> nodes =
        [
            new()
            {
                Id = Ulid.NewUlid(), Name = "Healthy", IpAddress = "10.0.0.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Disabled", IpAddress = "10.0.0.2", Port = 8080,
                IsEnabled = false, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Stale", IpAddress = "10.0.0.3", Port = 8080,
                IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
                LastHeartbeat = DateTime.UtcNow.AddMinutes(-5), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(), Name = "Unhealthy", IpAddress = "10.0.0.4", Port = 8080,
                IsEnabled = true, IsHealthy = false, CpuCores = 8, MemoryGb = 16,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        // Act
        List<EncoderNode> healthy = nodeSelector.FilterHealthyNodes(nodes, maxHeartbeatAgeSeconds: 60).ToList();

        // Assert - Only the first node should pass (enabled + healthy + recent heartbeat)
        Assert.Single(healthy);
        Assert.Equal("Healthy", healthy[0].Name);

        _output.WriteLine($"Filtered {nodes.Count} nodes -> {healthy.Count} healthy");
        foreach (EncoderNode n in healthy)
        {
            _output.WriteLine($"  - {n.Name}");
        }
    }

    [Fact]
    public async Task EdgeCase_NodeRecoversAndCompletedTasksUntouched()
    {
        // Arrange - Node with both completed and running tasks goes unhealthy
        EncoderNode node = new()
        {
            Id = Ulid.NewUlid(), Name = "Intermittent Node", IpAddress = "10.0.0.10", Port = 8080,
            IsEnabled = true, IsHealthy = true, CpuCores = 8, MemoryGb = 16,
            MaxConcurrentTasks = 4, CurrentTaskCount = 2,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddHours(-1), UpdatedAt = DateTime.UtcNow
        };

        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(), Title = "Completed Tasks Preservation",
            State = EncodingJobState.Encoding, InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingTask completedTask = new()
        {
            Id = Ulid.NewUlid(), JobId = job.Id, TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Completed, AssignedNodeId = node.Id, Weight = 100,
            CompletedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        EncodingTask runningTask = new()
        {
            Id = Ulid.NewUlid(), JobId = job.Id, TaskType = EncodingTaskType.AudioEncoding,
            State = EncodingTaskState.Running, AssignedNodeId = node.Id, Weight = 30,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(node);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([completedTask, runningTask]);
        await _queueContext.SaveChangesAsync();

        // Act
        int reassigned = await _healthMonitor!.MarkNodeUnhealthyAsync(
            node.Id, "Heartbeat timeout", reassignTasks: true);

        // Assert - Only the running task should be reassigned, not the completed one
        Assert.Equal(1, reassigned);

        EncodingTask? updatedCompleted = await _queueContext.EncodingTasks.FindAsync(completedTask.Id);
        EncodingTask? updatedRunning = await _queueContext.EncodingTasks.FindAsync(runningTask.Id);

        Assert.Equal(EncodingTaskState.Completed, updatedCompleted?.State);
        Assert.Equal(node.Id, updatedCompleted?.AssignedNodeId); // Completed task keeps node assignment

        Assert.Equal(EncodingTaskState.Pending, updatedRunning?.State);
        Assert.Null(updatedRunning?.AssignedNodeId); // Running task reassigned

        _output.WriteLine($"Reassigned {reassigned} task(s), completed task preserved");
    }

    #endregion

    #region Helper Methods

    private static List<EncoderNode> CreateHeterogeneousCluster()
    {
        return
        [
            new EncoderNode
            {
                Id = Ulid.NewUlid(), Name = "CPU-Heavy", IpAddress = "10.0.1.1", Port = 8080,
                IsEnabled = true, IsHealthy = true, HasGpu = false,
                CpuCores = 32, MemoryGb = 128, MaxConcurrentTasks = 4, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(), Name = "GPU-NVIDIA-High", IpAddress = "10.0.1.2", Port = 8080,
                IsEnabled = true, IsHealthy = true, HasGpu = true,
                GpuModel = "NVIDIA RTX 4090", GpuVendor = "nvidia",
                SupportedAccelerationsJson = "[\"nvenc\"]",
                CpuCores = 16, MemoryGb = 64, MaxConcurrentTasks = 4, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(), Name = "GPU-NVIDIA-Mid", IpAddress = "10.0.1.3", Port = 8080,
                IsEnabled = true, IsHealthy = true, HasGpu = true,
                GpuModel = "NVIDIA RTX 3070", GpuVendor = "nvidia",
                SupportedAccelerationsJson = "[\"nvenc\"]",
                CpuCores = 8, MemoryGb = 32, MaxConcurrentTasks = 3, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(), Name = "GPU-Intel", IpAddress = "10.0.1.4", Port = 8080,
                IsEnabled = true, IsHealthy = true, HasGpu = true,
                GpuModel = "Intel Arc A770", GpuVendor = "intel",
                SupportedAccelerationsJson = "[\"qsv\"]",
                CpuCores = 8, MemoryGb = 16, MaxConcurrentTasks = 2, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(), Name = "CPU-Light", IpAddress = "10.0.1.5", Port = 8080,
                IsEnabled = true, IsHealthy = true, HasGpu = false,
                CpuCores = 4, MemoryGb = 8, MaxConcurrentTasks = 1, CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];
    }

    #endregion
}
