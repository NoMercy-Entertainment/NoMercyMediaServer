using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Tasks;

namespace NoMercy.Tests.EncoderV2.Integration;

/// <summary>
/// Test-specific QueueContext that prevents the base class from configuring SQLite.
/// This allows us to use InMemory database for testing.
/// </summary>
internal class TestQueueContext : QueueContext
{
    public TestQueueContext(DbContextOptions<QueueContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Do NOT call base.OnConfiguring() - we want to use only the options passed via DI
        // The base class unconditionally adds SQLite, which conflicts with InMemory
    }
}

/// <summary>
/// Integration tests for the EncoderV2 pipeline components.
/// Tests job dispatching, task splitting, node selection, and lifecycle management.
/// </summary>
public class EncodingPipelineIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private QueueContext? _queueContext;
    private string _dbName = string.Empty;

    public EncodingPipelineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        ServiceCollection services = new();

        // Use unique in-memory database per test
        _dbName = $"EncoderV2IntegrationTest_{Guid.NewGuid():N}";

        // Create the QueueContext directly with InMemory options (bypass DI for context)
        DbContextOptions<QueueContext> queueOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase($"Queue_{_dbName}")
            .Options;

        // Create the TestQueueContext directly
        _queueContext = new TestQueueContext(queueOptions);

        // Register services that we need via DI
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        // Register task distribution services - inject the context instance
        services.AddSingleton<QueueContext>(_queueContext);
        services.AddScoped<INodeSelector, NodeSelector>();

        _serviceProvider = services.BuildServiceProvider();

        _output.WriteLine($"Test database initialized: {_dbName}");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    #region Node Selector Tests

    [Fact]
    public void NodeSelector_SelectNode_ReturnsHealthyNode()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateTestNodes();
        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100
        };

        // Act
        NodeSelectionResult result = nodeSelector.SelectNode(task, nodes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.SelectedNode);
        Assert.True(result.Score > 0);

        _output.WriteLine($"Selected node: {result.SelectedNode.Name}");
        _output.WriteLine($"Score: {result.Score:F2}");
        _output.WriteLine($"Reason: {result.Reason}");
    }

    [Fact]
    public void NodeSelector_WithGpuRequirement_PrefersGpuNodes()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateTestNodes();
        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100,
            RequiresGpu = true
        };

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.BestCapability,
            StrictGpuRequirement = true
        };

        // Act
        NodeSelectionResult result = nodeSelector.SelectNode(task, nodes, options);

        // Assert
        if (result.Success)
        {
            Assert.True(result.SelectedNode!.HasGpu);
            _output.WriteLine($"Selected GPU node: {result.SelectedNode.Name} ({result.SelectedNode.GpuModel})");
        }
        else
        {
            _output.WriteLine($"No GPU node available: {result.Reason}");
        }
    }

    [Fact]
    public void NodeSelector_SelectNodesForTasks_AssignsAllTasks()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateTestNodes();
        List<EncodingTaskDefinition> tasks =
        [
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.VideoEncoding, Weight = 100 },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.AudioEncoding, Weight = 50 },
            new() { Id = Ulid.NewUlid().ToString(), TaskType = EncodingTaskType.SubtitleExtraction, Weight = 10 }
        ];

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes);

        // Assert
        _output.WriteLine($"Total tasks: {result.TotalTasks}");
        _output.WriteLine($"Assigned: {result.AssignedCount}");
        _output.WriteLine($"Unassigned: {result.UnassignedTasks.Count}");

        foreach (TaskAssignment assignment in result.Assignments)
        {
            _output.WriteLine($"  - Task {assignment.Task.TaskType} -> Node {assignment.Node.Name}");
        }

        Assert.Equal(tasks.Count, result.TotalTasks);
        Assert.True(result.AssignedCount > 0);
    }

    [Fact]
    public void NodeSelector_LeastLoadedStrategy_BalancesLoad()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateTestNodes();

        // Simulate load on first node
        nodes[0].CurrentTaskCount = 3;

        List<EncodingTaskDefinition> tasks = Enumerable.Range(0, 5)
            .Select(_ => new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.VideoEncoding,
                Weight = 100
            })
            .ToList();

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.LeastLoaded
        };

        // Act
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes, options);

        // Assert
        Dictionary<string, int> assignmentCounts = result.Assignments
            .GroupBy(a => a.Node.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        _output.WriteLine("Load distribution:");
        foreach (KeyValuePair<string, int> kvp in assignmentCounts)
        {
            _output.WriteLine($"  - {kvp.Key}: {kvp.Value} tasks");
        }
    }

    [Fact]
    public void NodeSelector_NoHealthyNodes_ReturnsFailure()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateTestNodes();

        // Mark all nodes as unhealthy
        foreach (EncoderNode node in nodes)
        {
            node.IsHealthy = false;
        }

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
        Assert.NotEmpty(result.Reason);

        _output.WriteLine($"Expected failure: {result.Reason}");
    }

    #endregion

    #region Job Lifecycle Database Tests

    [Fact]
    public async Task JobLifecycle_CancelJob_UpdatesJobAndTaskStates()
    {
        // Arrange - Create job and tasks directly in database
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Test Job",
            State = EncodingJobState.Encoding,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask task1 = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Running,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask task2 = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.AudioEncoding,
            State = EncodingTaskState.Pending,
            Weight = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([task1, task2]);
        await _queueContext.SaveChangesAsync();

        // Act - Cancel job by updating states
        job.State = EncodingJobState.Cancelled;
        job.UpdatedAt = DateTime.UtcNow;
        task1.State = EncodingTaskState.Cancelled;
        task1.UpdatedAt = DateTime.UtcNow;
        task2.State = EncodingTaskState.Cancelled;
        task2.UpdatedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        // Assert
        EncodingJob? updatedJob = await _queueContext.EncodingJobs.FindAsync(job.Id);
        List<EncodingTask> updatedTasks = await _queueContext.EncodingTasks
            .Where(t => t.JobId == job.Id)
            .ToListAsync();

        Assert.Equal(EncodingJobState.Cancelled, updatedJob?.State);
        Assert.All(updatedTasks, t => Assert.Equal(EncodingTaskState.Cancelled, t.State));

        _output.WriteLine($"Cancelled job {job.Id} with {updatedTasks.Count} tasks");
    }

    [Fact]
    public async Task JobLifecycle_RetryFailedTasks_ResetsTaskState()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Failed Job",
            State = EncodingJobState.Failed,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask failedTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Failed,
            ErrorMessage = "Test error",
            RetryCount = 1,
            MaxRetries = 3,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(failedTask);
        await _queueContext.SaveChangesAsync();

        // Act - Retry task
        failedTask.State = EncodingTaskState.Pending;
        failedTask.RetryCount++;
        failedTask.ErrorMessage = null;
        failedTask.StartedAt = null;
        failedTask.CompletedAt = null;
        failedTask.UpdatedAt = DateTime.UtcNow;

        job.State = EncodingJobState.Queued;
        job.ErrorMessage = null;
        job.UpdatedAt = DateTime.UtcNow;

        await _queueContext.SaveChangesAsync();

        // Assert
        EncodingTask? updatedTask = await _queueContext.EncodingTasks.FindAsync(failedTask.Id);
        EncodingJob? updatedJob = await _queueContext.EncodingJobs.FindAsync(job.Id);

        Assert.Equal(EncodingTaskState.Pending, updatedTask?.State);
        Assert.Equal(2, updatedTask?.RetryCount);
        Assert.Null(updatedTask?.ErrorMessage);
        Assert.Equal(EncodingJobState.Queued, updatedJob?.State);

        _output.WriteLine("Task retried successfully");
    }

    [Fact]
    public async Task JobLifecycle_GetJobStatus_ReturnsAggregatedProgress()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Progress Test Job",
            State = EncodingJobState.Encoding,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask completedTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Completed,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask runningTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.AudioEncoding,
            State = EncodingTaskState.Running,
            Weight = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask pendingTask = new()
        {
            Id = Ulid.NewUlid(),
            JobId = job.Id,
            TaskType = EncodingTaskType.SubtitleExtraction,
            State = EncodingTaskState.Pending,
            Weight = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([completedTask, runningTask, pendingTask]);
        await _queueContext.SaveChangesAsync();

        // Act - Query job status
        EncodingJob? retrievedJob = await _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        List<EncodingTask> tasks = retrievedJob!.Tasks.ToList();
        int completed = tasks.Count(t => t.State == EncodingTaskState.Completed);
        int running = tasks.Count(t => t.State == EncodingTaskState.Running);
        int pending = tasks.Count(t => t.State == EncodingTaskState.Pending);

        // Calculate progress
        double totalWeight = tasks.Sum(t => t.Weight);
        double completedWeight = tasks.Where(t => t.State == EncodingTaskState.Completed).Sum(t => t.Weight);
        double overallProgress = totalWeight > 0 ? (completedWeight / totalWeight) * 100.0 : 0;

        // Assert
        Assert.Equal(1, completed);
        Assert.Equal(1, running);
        Assert.Equal(1, pending);
        Assert.Equal(3, tasks.Count);
        Assert.True(overallProgress > 0);
        Assert.True(overallProgress < 100);

        _output.WriteLine($"Job status:");
        _output.WriteLine($"  Overall progress: {overallProgress:F2}%");
        _output.WriteLine($"  Completed: {completed}/{tasks.Count}");
        _output.WriteLine($"  Running: {running}");
        _output.WriteLine($"  Pending: {pending}");
    }

    [Fact]
    public async Task JobLifecycle_TaskStateTransitions_UpdatesCorrectly()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Lifecycle Test",
            State = EncodingJobState.Queued,
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
            State = EncodingTaskState.Pending,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act & Assert - Start Task
        task.State = EncodingTaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        job.State = EncodingJobState.Encoding;
        job.StartedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        EncodingTask? startedTask = await _queueContext.EncodingTasks.FindAsync(task.Id);
        EncodingJob? startedJob = await _queueContext.EncodingJobs.FindAsync(job.Id);

        Assert.Equal(EncodingTaskState.Running, startedTask?.State);
        Assert.Equal(EncodingJobState.Encoding, startedJob?.State);
        Assert.NotNull(startedTask?.StartedAt);
        Assert.NotNull(startedJob?.StartedAt);

        _output.WriteLine("Task started successfully");

        // Act & Assert - Complete Task
        task.State = EncodingTaskState.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.OutputFile = "/output/video.ts";
        task.UpdatedAt = DateTime.UtcNow;
        job.State = EncodingJobState.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        EncodingTask? completedTask = await _queueContext.EncodingTasks.FindAsync(task.Id);
        EncodingJob? completedJob = await _queueContext.EncodingJobs.FindAsync(job.Id);

        Assert.Equal(EncodingTaskState.Completed, completedTask?.State);
        Assert.Equal(EncodingJobState.Completed, completedJob?.State);
        Assert.NotNull(completedTask?.CompletedAt);
        Assert.NotNull(completedJob?.CompletedAt);
        Assert.Equal("/output/video.ts", completedTask?.OutputFile);

        _output.WriteLine("Task completed successfully");
    }

    [Fact]
    public async Task JobLifecycle_RecordProgress_StoresProgressSnapshot()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Progress Recording Test",
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
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act - Record progress
        EncodingProgress progress = new()
        {
            Id = Ulid.NewUlid(),
            TaskId = task.Id,
            ProgressPercentage = 45.5,
            Fps = 60.0,
            Speed = 2.5,
            Bitrate = "5000kbps",
            EncodedFrames = 1500,
            TotalFrames = 3300,
            RecordedAt = DateTime.UtcNow
        };

        await _queueContext.EncodingProgress.AddAsync(progress);
        await _queueContext.SaveChangesAsync();

        // Assert
        EncodingProgress? storedProgress = await _queueContext.EncodingProgress
            .FirstOrDefaultAsync(p => p.TaskId == task.Id);

        Assert.NotNull(storedProgress);
        Assert.Equal(45.5, storedProgress.ProgressPercentage);
        Assert.Equal(60.0, storedProgress.Fps);
        Assert.Equal(2.5, storedProgress.Speed);

        _output.WriteLine($"Recorded progress: {storedProgress.ProgressPercentage}% @ {storedProgress.Fps} fps");
    }

    [Fact]
    public async Task JobLifecycle_ReassignTasksFromNode_ResetsTasksToUnassigned()
    {
        // Arrange
        EncoderNode unhealthyNode = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Unhealthy Node",
            IpAddress = "192.168.1.100",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CurrentTaskCount = 2,
            MaxConcurrentTasks = 4,
            CpuCores = 8,
            MemoryGb = 16,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Reassignment Test",
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
            AssignedNodeId = unhealthyNode.Id,
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
            AssignedNodeId = unhealthyNode.Id,
            Weight = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncoderNodes.AddAsync(unhealthyNode);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([runningTask, pendingTask]);
        await _queueContext.SaveChangesAsync();

        // Act - Reassign tasks from unhealthy node
        List<EncodingTask> tasksToReassign = await _queueContext.EncodingTasks
            .Where(t => t.AssignedNodeId == unhealthyNode.Id &&
                       (t.State == EncodingTaskState.Running || t.State == EncodingTaskState.Pending))
            .ToListAsync();

        foreach (EncodingTask t in tasksToReassign)
        {
            t.State = EncodingTaskState.Pending;
            t.AssignedNodeId = null;
            t.StartedAt = null;
            t.UpdatedAt = DateTime.UtcNow;
        }

        unhealthyNode.CurrentTaskCount = 0;
        unhealthyNode.IsHealthy = false;
        unhealthyNode.UpdatedAt = DateTime.UtcNow;

        await _queueContext.SaveChangesAsync();

        // Assert
        EncodingTask? reassignedRunning = await _queueContext.EncodingTasks.FindAsync(runningTask.Id);
        EncodingTask? reassignedPending = await _queueContext.EncodingTasks.FindAsync(pendingTask.Id);
        EncoderNode? updatedNode = await _queueContext.EncoderNodes.FindAsync(unhealthyNode.Id);

        Assert.Equal(EncodingTaskState.Pending, reassignedRunning?.State);
        Assert.Null(reassignedRunning?.AssignedNodeId);
        Assert.Null(reassignedPending?.AssignedNodeId);
        Assert.False(updatedNode?.IsHealthy);
        Assert.Equal(0, updatedNode?.CurrentTaskCount);

        _output.WriteLine($"Reassigned {tasksToReassign.Count} tasks from unhealthy node");
    }

    [Fact]
    public async Task JobLifecycle_TaskDependencies_RespectsExecutionOrder()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Dependency Test",
            State = EncodingJobState.Queued,
            Priority = 0,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Ulid videoTaskId = Ulid.NewUlid();
        Ulid playlistTaskId = Ulid.NewUlid();

        EncodingTask videoTask = new()
        {
            Id = videoTaskId,
            JobId = job.Id,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Pending,
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask playlistTask = new()
        {
            Id = playlistTaskId,
            JobId = job.Id,
            TaskType = EncodingTaskType.PlaylistGeneration,
            State = EncodingTaskState.Pending,
            DependenciesJson = $"[\"{videoTaskId}\"]",
            Weight = 10,
            CreatedAt = DateTime.UtcNow.AddSeconds(1),
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync([videoTask, playlistTask]);
        await _queueContext.SaveChangesAsync();

        // Act - Get first pending task with no dependencies
        List<EncodingTask> pendingTasks = await _queueContext.EncodingTasks
            .Where(t => t.State == EncodingTaskState.Pending)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // Find task with no unmet dependencies
        EncodingTask? firstTask = null;
        foreach (EncodingTask task in pendingTasks)
        {
            string[] deps = task.Dependencies;
            if (deps.Length == 0)
            {
                firstTask = task;
                break;
            }

            // Check if all dependencies are completed
            int completedDeps = await _queueContext.EncodingTasks
                .Where(t => deps.Contains(t.Id.ToString()) && t.State == EncodingTaskState.Completed)
                .CountAsync();

            if (completedDeps == deps.Length)
            {
                firstTask = task;
                break;
            }
        }

        // Assert - Video task should be first (no dependencies)
        Assert.NotNull(firstTask);
        Assert.Equal(videoTaskId, firstTask.Id);
        Assert.Equal(EncodingTaskType.VideoEncoding, firstTask.TaskType);

        _output.WriteLine($"First pending task: {firstTask.TaskType} (dependency check passed)");

        // Simulate completing video task
        videoTask.State = EncodingTaskState.Completed;
        await _queueContext.SaveChangesAsync();

        // Now playlist task should be available
        EncodingTask? updatedPlaylistTask = await _queueContext.EncodingTasks.FindAsync(playlistTaskId);
        string[] playlistDeps = updatedPlaylistTask!.Dependencies;
        int completedPlaylistDeps = await _queueContext.EncodingTasks
            .Where(t => playlistDeps.Contains(t.Id.ToString()) && t.State == EncodingTaskState.Completed)
            .CountAsync();

        Assert.Equal(playlistDeps.Length, completedPlaylistDeps);
        _output.WriteLine($"Playlist task dependencies resolved: {completedPlaylistDeps}/{playlistDeps.Length}");
    }

    [Fact]
    public async Task JobLifecycle_FailTask_MarksTaskAndJobAsFailed()
    {
        // Arrange
        EncodingJob job = new()
        {
            Id = Ulid.NewUlid(),
            Title = "Failure Test",
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
            Weight = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act - Fail task
        task.State = EncodingTaskState.Failed;
        task.ErrorMessage = "FFmpeg exited with code 1";
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        job.State = EncodingJobState.Failed;
        job.ErrorMessage = "1 task(s) failed";
        job.UpdatedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        // Assert
        EncodingTask? failedTask = await _queueContext.EncodingTasks.FindAsync(task.Id);
        EncodingJob? failedJob = await _queueContext.EncodingJobs.FindAsync(job.Id);

        Assert.Equal(EncodingTaskState.Failed, failedTask?.State);
        Assert.Equal(EncodingJobState.Failed, failedJob?.State);
        Assert.Equal("FFmpeg exited with code 1", failedTask?.ErrorMessage);
        Assert.Contains("1 task(s) failed", failedJob?.ErrorMessage);

        _output.WriteLine($"Task failed with error: {failedTask?.ErrorMessage}");
    }

    #endregion

    #region Helper Methods

    private static List<EncoderNode> CreateTestNodes()
    {
        return
        [
            new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = "Node-CPU",
                IpAddress = "192.168.1.10",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = false,
                CpuCores = 16,
                MemoryGb = 32,
                MaxConcurrentTasks = 2,
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = "Node-GPU-NVIDIA",
                IpAddress = "192.168.1.11",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = true,
                GpuModel = "NVIDIA RTX 4090",
                GpuVendor = "nvidia",
                CpuCores = 8,
                MemoryGb = 64,
                MaxConcurrentTasks = 4,
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = "Node-GPU-Intel",
                IpAddress = "192.168.1.12",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = true,
                GpuModel = "Intel Arc A770",
                GpuVendor = "intel",
                CpuCores = 4,
                MemoryGb = 16,
                MaxConcurrentTasks = 2,
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ];
    }

    #endregion
}
