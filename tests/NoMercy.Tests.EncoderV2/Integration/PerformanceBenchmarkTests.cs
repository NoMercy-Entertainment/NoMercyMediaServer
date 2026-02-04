using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Tasks;
using NoMercy.EncoderV2.Validation;
using NoMercy.EncoderV2.Specifications.HLS;

namespace NoMercy.Tests.EncoderV2.Integration;

/// <summary>
/// Performance benchmark tests for EncoderV2 components.
/// Validates performance requirements from PRD:
/// - TR2: Progress update latency < 1 second
/// - TR3: API response time < 200ms (simulated via service calls)
/// - TR4: Database query time < 100ms
/// - TR5: Output validation time < 30 seconds
/// - TR6: Task assignment latency < 5 seconds
///
/// Note: TR1 (encoding time < 30 min) requires actual FFmpeg encoding and is
/// tested separately with real media files.
/// </summary>
public class PerformanceBenchmarkTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private QueueContext? _queueContext;
    private string _dbName = string.Empty;
    private string _testOutputFolder = string.Empty;

    // PRD Performance Thresholds
    private const int ProgressUpdateLatencyMs = 1000;    // TR2: < 1 second
    private const int ApiResponseTimeMs = 200;           // TR3: < 200ms
    private const int DatabaseQueryTimeMs = 100;         // TR4: < 100ms
    private const int OutputValidationTimeMs = 30_000;   // TR5: < 30 seconds
    private const int TaskAssignmentLatencyMs = 5_000;   // TR6: < 5 seconds

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        ServiceCollection services = new();

        _dbName = $"PerfBenchmark_{Guid.NewGuid():N}";
        _testOutputFolder = Path.Combine(Path.GetTempPath(), $"EncoderV2Perf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputFolder);

        DbContextOptions<QueueContext> queueOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase($"Queue_{_dbName}")
            .Options;

        _queueContext = new TestQueueContext(queueOptions);

        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<QueueContext>(_queueContext);
        services.AddScoped<INodeSelector, NodeSelector>();
        services.AddScoped<IHLSPlaylistGenerator, HLSPlaylistGenerator>();
        services.AddScoped<IPlaylistValidator, PlaylistValidator>();
        services.AddScoped<IOutputValidator, OutputValidator>();

        _serviceProvider = services.BuildServiceProvider();

        _output.WriteLine($"Performance test initialized: {_dbName}");
        _output.WriteLine($"Test output folder: {_testOutputFolder}");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        if (Directory.Exists(_testOutputFolder))
        {
            try { Directory.Delete(_testOutputFolder, true); }
            catch { /* Ignore cleanup errors */ }
        }

        return Task.CompletedTask;
    }

    #region Database Query Performance Tests (TR4: < 100ms)

    [Fact]
    public async Task DatabasePerformance_JobQueryById_CompletesWithinThreshold()
    {
        // Arrange - Create job in database
        EncodingJob job = CreateTestJob();
        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.SaveChangesAsync();

        // Act - Time the query
        Stopwatch sw = Stopwatch.StartNew();
        EncodingJob? result = await _queueContext.EncodingJobs.FindAsync(job.Id);
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(sw.ElapsedMilliseconds < DatabaseQueryTimeMs,
            $"Job query took {sw.ElapsedMilliseconds}ms, expected < {DatabaseQueryTimeMs}ms");

        _output.WriteLine($"Job query by ID: {sw.ElapsedMilliseconds}ms (threshold: {DatabaseQueryTimeMs}ms)");
    }

    [Fact]
    public async Task DatabasePerformance_JobWithTasksQuery_CompletesWithinThreshold()
    {
        // Arrange - Create job with multiple tasks
        EncodingJob job = CreateTestJob();
        List<EncodingTask> tasks = Enumerable.Range(0, 10)
            .Select(_ => CreateTestTask(job.Id))
            .ToList();

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync(tasks);
        await _queueContext.SaveChangesAsync();

        // Act - Time the query with include
        Stopwatch sw = Stopwatch.StartNew();
        EncodingJob? result = await _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == job.Id);
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Tasks.Count);
        Assert.True(sw.ElapsedMilliseconds < DatabaseQueryTimeMs,
            $"Job+Tasks query took {sw.ElapsedMilliseconds}ms, expected < {DatabaseQueryTimeMs}ms");

        _output.WriteLine($"Job with 10 tasks query: {sw.ElapsedMilliseconds}ms (threshold: {DatabaseQueryTimeMs}ms)");
    }

    [Fact]
    public async Task DatabasePerformance_BulkTaskQuery_CompletesWithinThreshold()
    {
        // Arrange - Create multiple jobs with many tasks
        List<EncodingJob> jobs = Enumerable.Range(0, 10)
            .Select(_ => CreateTestJob())
            .ToList();

        List<EncodingTask> tasks = jobs
            .SelectMany(j => Enumerable.Range(0, 10).Select(_ => CreateTestTask(j.Id)))
            .ToList();

        await _queueContext!.EncodingJobs.AddRangeAsync(jobs);
        await _queueContext.EncodingTasks.AddRangeAsync(tasks);
        await _queueContext.SaveChangesAsync();

        // Act - Time query for pending tasks across all jobs
        Stopwatch sw = Stopwatch.StartNew();
        List<EncodingTask> pendingTasks = await _queueContext.EncodingTasks
            .Where(t => t.State == EncodingTaskState.Pending)
            .OrderBy(t => t.CreatedAt)
            .Take(50)
            .ToListAsync();
        sw.Stop();

        // Assert
        Assert.NotEmpty(pendingTasks);
        Assert.True(sw.ElapsedMilliseconds < DatabaseQueryTimeMs,
            $"Bulk task query took {sw.ElapsedMilliseconds}ms, expected < {DatabaseQueryTimeMs}ms");

        _output.WriteLine($"Query 50 pending tasks from 100: {sw.ElapsedMilliseconds}ms (threshold: {DatabaseQueryTimeMs}ms)");
    }

    [Fact]
    public async Task DatabasePerformance_ProgressRecording_CompletesWithinThreshold()
    {
        // Arrange
        EncodingJob job = CreateTestJob();
        EncodingTask task = CreateTestTask(job.Id);
        task.State = EncodingTaskState.Running;

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act - Time progress insertion
        Stopwatch sw = Stopwatch.StartNew();
        EncodingProgress progress = new()
        {
            Id = Ulid.NewUlid(),
            TaskId = task.Id,
            ProgressPercentage = 50.0,
            Fps = 60.0,
            Speed = 2.5,
            Bitrate = "5000kbps",
            RecordedAt = DateTime.UtcNow
        };
        await _queueContext.EncodingProgress.AddAsync(progress);
        await _queueContext.SaveChangesAsync();
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < DatabaseQueryTimeMs,
            $"Progress recording took {sw.ElapsedMilliseconds}ms, expected < {DatabaseQueryTimeMs}ms");

        _output.WriteLine($"Progress record insertion: {sw.ElapsedMilliseconds}ms (threshold: {DatabaseQueryTimeMs}ms)");
    }

    [Fact]
    public async Task DatabasePerformance_NodeHealthQuery_CompletesWithinThreshold()
    {
        // Arrange - Create multiple nodes
        List<EncoderNode> nodes = Enumerable.Range(0, 20)
            .Select(i => CreateTestNode($"Node-{i}"))
            .ToList();

        // Set half as unhealthy
        for (int i = 0; i < 10; i++)
        {
            nodes[i].IsHealthy = false;
            nodes[i].LastHeartbeat = DateTime.UtcNow.AddMinutes(-5);
        }

        await _queueContext!.EncoderNodes.AddRangeAsync(nodes);
        await _queueContext.SaveChangesAsync();

        // Act - Time healthy node query
        Stopwatch sw = Stopwatch.StartNew();
        List<EncoderNode> healthyNodes = await _queueContext.EncoderNodes
            .Where(n => n.IsEnabled && n.IsHealthy && n.LastHeartbeat > DateTime.UtcNow.AddSeconds(-60))
            .OrderByDescending(n => n.HasGpu)
            .ThenByDescending(n => n.CpuCores)
            .ToListAsync();
        sw.Stop();

        // Assert
        Assert.Equal(10, healthyNodes.Count);
        Assert.True(sw.ElapsedMilliseconds < DatabaseQueryTimeMs,
            $"Node health query took {sw.ElapsedMilliseconds}ms, expected < {DatabaseQueryTimeMs}ms");

        _output.WriteLine($"Healthy nodes query (20 total): {sw.ElapsedMilliseconds}ms (threshold: {DatabaseQueryTimeMs}ms)");
    }

    #endregion

    #region Task Assignment Performance Tests (TR6: < 5 seconds)

    [Fact]
    public void TaskAssignment_SingleTask_CompletesWithinThreshold()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateNodeCluster(10);
        EncodingTaskDefinition task = new()
        {
            Id = Ulid.NewUlid().ToString(),
            TaskType = EncodingTaskType.VideoEncoding,
            Weight = 100,
            RequiresGpu = true
        };

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        NodeSelectionResult result = nodeSelector.SelectNode(task, nodes);
        sw.Stop();

        // Assert
        Assert.True(result.Success);
        Assert.True(sw.ElapsedMilliseconds < TaskAssignmentLatencyMs,
            $"Single task assignment took {sw.ElapsedMilliseconds}ms, expected < {TaskAssignmentLatencyMs}ms");

        _output.WriteLine($"Single task assignment: {sw.ElapsedMilliseconds}ms (threshold: {TaskAssignmentLatencyMs}ms)");
    }

    [Fact]
    public void TaskAssignment_BatchAssignment_CompletesWithinThreshold()
    {
        // Arrange
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateNodeCluster(10);
        List<EncodingTaskDefinition> tasks = Enumerable.Range(0, 100)
            .Select(i => new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = i % 3 == 0 ? EncodingTaskType.VideoEncoding : EncodingTaskType.AudioEncoding,
                Weight = (i % 3 == 0) ? 100 : 50,
                RequiresGpu = i % 5 == 0
            })
            .ToList();

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < TaskAssignmentLatencyMs,
            $"Batch assignment of 100 tasks took {sw.ElapsedMilliseconds}ms, expected < {TaskAssignmentLatencyMs}ms");

        _output.WriteLine($"Batch assignment (100 tasks, 10 nodes): {sw.ElapsedMilliseconds}ms (threshold: {TaskAssignmentLatencyMs}ms)");
        _output.WriteLine($"  Assigned: {result.AssignedCount}, Unassigned: {result.UnassignedTasks.Count}");
    }

    [Fact]
    public void TaskAssignment_LargeClusterScenario_CompletesWithinThreshold()
    {
        // Arrange - Simulate production scale: 50 nodes, 500 tasks
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateNodeCluster(50);
        List<EncodingTaskDefinition> tasks = CreateRealisticTaskSet(500);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes, new NodeSelectionOptions
        {
            Strategy = NodeSelectionStrategy.Auto
        });
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < TaskAssignmentLatencyMs,
            $"Large cluster assignment took {sw.ElapsedMilliseconds}ms, expected < {TaskAssignmentLatencyMs}ms");

        _output.WriteLine($"Large cluster assignment (500 tasks, 50 nodes): {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Assigned: {result.AssignedCount}");
        _output.WriteLine($"  Assignment rate: {result.AssignedCount / (sw.ElapsedMilliseconds / 1000.0):F0} tasks/sec");
    }

    [Fact]
    public void TaskAssignment_CapabilityMatching_CompletesWithinThreshold()
    {
        // Arrange - Mixed GPU/CPU requirements with strict matching
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateHeterogeneousCluster();
        List<EncodingTaskDefinition> tasks = Enumerable.Range(0, 50)
            .Select(i => new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = EncodingTaskType.VideoEncoding,
                Weight = 100,
                RequiresGpu = i % 2 == 0,
                EstimatedMemoryMb = (i % 3 + 1) * 8000 // 8GB, 16GB, 24GB
            })
            .ToList();

        NodeSelectionOptions options = new()
        {
            Strategy = NodeSelectionStrategy.BestCapability,
            StrictGpuRequirement = true,
            MinimumMemoryGb = 8
        };

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes, options);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < TaskAssignmentLatencyMs,
            $"Capability matching took {sw.ElapsedMilliseconds}ms, expected < {TaskAssignmentLatencyMs}ms");

        _output.WriteLine($"Capability-based assignment: {sw.ElapsedMilliseconds}ms (threshold: {TaskAssignmentLatencyMs}ms)");
        _output.WriteLine($"  Assigned: {result.AssignedCount}, Unassigned: {result.UnassignedTasks.Count}");
    }

    #endregion

    #region Progress Update Latency Tests (TR2: < 1 second)

    [Fact]
    public async Task ProgressUpdate_SingleUpdate_CompletesWithinThreshold()
    {
        // Arrange
        EncodingJob job = CreateTestJob();
        EncodingTask task = CreateTestTask(job.Id);
        task.State = EncodingTaskState.Running;

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act - Simulate full progress update cycle
        Stopwatch sw = Stopwatch.StartNew();

        // 1. Record progress
        EncodingProgress progress = new()
        {
            Id = Ulid.NewUlid(),
            TaskId = task.Id,
            ProgressPercentage = 50.0,
            Fps = 60.0,
            Speed = 2.5,
            Bitrate = "5000kbps",
            RecordedAt = DateTime.UtcNow
        };
        await _queueContext.EncodingProgress.AddAsync(progress);
        await _queueContext.SaveChangesAsync();

        // 2. Query latest progress for broadcasting
        EncodingProgress? latestProgress = await _queueContext.EncodingProgress
            .Where(p => p.TaskId == task.Id)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();

        sw.Stop();

        // Assert
        Assert.NotNull(latestProgress);
        Assert.True(sw.ElapsedMilliseconds < ProgressUpdateLatencyMs,
            $"Progress update cycle took {sw.ElapsedMilliseconds}ms, expected < {ProgressUpdateLatencyMs}ms");

        _output.WriteLine($"Progress update cycle: {sw.ElapsedMilliseconds}ms (threshold: {ProgressUpdateLatencyMs}ms)");
    }

    [Fact]
    public async Task ProgressUpdate_RapidUpdates_MaintainsLatency()
    {
        // Arrange
        EncodingJob job = CreateTestJob();
        EncodingTask task = CreateTestTask(job.Id);
        task.State = EncodingTaskState.Running;

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        List<long> latencies = [];

        // Act - Simulate 100 rapid progress updates
        for (int i = 0; i < 100; i++)
        {
            Stopwatch sw = Stopwatch.StartNew();

            EncodingProgress progress = new()
            {
                Id = Ulid.NewUlid(),
                TaskId = task.Id,
                ProgressPercentage = i,
                Fps = 60.0 + (i % 10),
                Speed = 2.5,
                Bitrate = "5000kbps",
                RecordedAt = DateTime.UtcNow
            };
            await _queueContext.EncodingProgress.AddAsync(progress);
            await _queueContext.SaveChangesAsync();

            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        double avgLatency = latencies.Average();
        long maxLatency = latencies.Max();
        double p95Latency = latencies.OrderBy(l => l).ElementAt((int)(latencies.Count * 0.95));

        Assert.True(avgLatency < ProgressUpdateLatencyMs / 2,
            $"Average latency {avgLatency:F1}ms exceeds half of threshold");
        Assert.True(p95Latency < ProgressUpdateLatencyMs,
            $"P95 latency {p95Latency}ms exceeds threshold {ProgressUpdateLatencyMs}ms");

        _output.WriteLine($"Rapid progress updates (100 iterations):");
        _output.WriteLine($"  Average: {avgLatency:F1}ms");
        _output.WriteLine($"  Max: {maxLatency}ms");
        _output.WriteLine($"  P95: {p95Latency}ms");
        _output.WriteLine($"  Threshold: {ProgressUpdateLatencyMs}ms");
    }

    #endregion

    #region Output Validation Performance Tests (TR5: < 30 seconds)

    [Fact]
    public async Task OutputValidation_SmallPlaylist_CompletesWithinThreshold()
    {
        // Arrange
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        string playlistPath = Path.Combine(_testOutputFolder, "small_playlist.m3u8");

        string content = GenerateMediaPlaylist(10);
        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        PlaylistValidationResult result = await validator.ValidateMediaPlaylistAsync(playlistPath);
        sw.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.True(sw.ElapsedMilliseconds < OutputValidationTimeMs,
            $"Small playlist validation took {sw.ElapsedMilliseconds}ms, expected < {OutputValidationTimeMs}ms");

        _output.WriteLine($"Small playlist (10 segments) validation: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task OutputValidation_LargePlaylist_CompletesWithinThreshold()
    {
        // Arrange - 24-minute episode at 6-second segments = ~240 segments
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        string playlistPath = Path.Combine(_testOutputFolder, "large_playlist.m3u8");

        string content = GenerateMediaPlaylist(240);
        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        PlaylistValidationResult result = await validator.ValidateMediaPlaylistAsync(playlistPath);
        sw.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(240, result.SegmentCount);
        Assert.True(sw.ElapsedMilliseconds < OutputValidationTimeMs,
            $"Large playlist validation took {sw.ElapsedMilliseconds}ms, expected < {OutputValidationTimeMs}ms");

        _output.WriteLine($"Large playlist (240 segments) validation: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task OutputValidation_MasterPlaylistWithVariants_CompletesWithinThreshold()
    {
        // Arrange
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        string playlistPath = Path.Combine(_testOutputFolder, "master.m3u8");

        string content = GenerateMasterPlaylist(5, 3); // 5 video variants, 3 audio groups
        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        PlaylistValidationResult result = await validator.ValidateMasterPlaylistAsync(playlistPath);
        sw.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(5, result.VariantCount);
        Assert.True(sw.ElapsedMilliseconds < OutputValidationTimeMs,
            $"Master playlist validation took {sw.ElapsedMilliseconds}ms, expected < {OutputValidationTimeMs}ms");

        _output.WriteLine($"Master playlist (5 variants, 3 audio) validation: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task OutputValidation_CompleteOutputStructure_CompletesWithinThreshold()
    {
        // Arrange - Create complete production output structure
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        IOutputValidator outputValidator = _serviceProvider.GetRequiredService<IOutputValidator>();

        string episodeDir = Path.Combine(_testOutputFolder, "S01E01");
        Directory.CreateDirectory(episodeDir);

        // Create 3 video quality folders with segments
        string[] resolutions = ["1920x1080", "1280x720", "854x480"];
        foreach (string res in resolutions)
        {
            string videoDir = Path.Combine(episodeDir, $"video_{res}_SDR");
            Directory.CreateDirectory(videoDir);

            // Create segment files
            for (int i = 0; i < 240; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(videoDir, $"video_{res}_SDR_{i:D5}.ts"), $"seg{i}");
            }

            // Create media playlist
            await File.WriteAllTextAsync(
                Path.Combine(videoDir, $"video_{res}_SDR.m3u8"),
                GenerateMediaPlaylist(240));
        }

        // Create 2 audio folders
        string[] languages = ["eng", "jpn"];
        foreach (string lang in languages)
        {
            string audioDir = Path.Combine(episodeDir, $"audio_{lang}_aac");
            Directory.CreateDirectory(audioDir);

            for (int i = 0; i < 240; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(audioDir, $"audio_{lang}_aac_{i:D5}.ts"), $"seg{i}");
            }

            await File.WriteAllTextAsync(
                Path.Combine(audioDir, $"audio_{lang}_aac.m3u8"),
                GenerateMediaPlaylist(240));
        }

        // Create master playlist
        string masterPath = Path.Combine(episodeDir, "S01E01.m3u8");
        await File.WriteAllTextAsync(masterPath, GenerateMasterPlaylist(3, 2));

        // Act - Validate entire structure
        Stopwatch sw = Stopwatch.StartNew();

        // Validate master
        PlaylistValidationResult masterResult = await validator.ValidateMasterPlaylistAsync(masterPath);

        // Validate all media playlists
        List<PlaylistValidationResult> mediaResults = [];
        foreach (string res in resolutions)
        {
            string playlistPath = Path.Combine(episodeDir, $"video_{res}_SDR", $"video_{res}_SDR.m3u8");
            mediaResults.Add(await validator.ValidateMediaPlaylistAsync(playlistPath));
        }
        foreach (string lang in languages)
        {
            string playlistPath = Path.Combine(episodeDir, $"audio_{lang}_aac", $"audio_{lang}_aac.m3u8");
            mediaResults.Add(await validator.ValidateMediaPlaylistAsync(playlistPath));
        }

        sw.Stop();

        // Assert
        Assert.True(masterResult.IsValid);
        Assert.All(mediaResults, r => Assert.True(r.IsValid));
        Assert.True(sw.ElapsedMilliseconds < OutputValidationTimeMs,
            $"Complete structure validation took {sw.ElapsedMilliseconds}ms, expected < {OutputValidationTimeMs}ms");

        _output.WriteLine($"Complete output validation (3 video + 2 audio, 240 segments each):");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms (threshold: {OutputValidationTimeMs}ms)");
        _output.WriteLine($"  Master playlist: {masterResult.VariantCount} variants");
        _output.WriteLine($"  Media playlists validated: {mediaResults.Count}");
    }

    #endregion

    #region API Response Time Simulation Tests (TR3: < 200ms)

    [Fact]
    public async Task ApiSimulation_GetJobStatus_CompletesWithinThreshold()
    {
        // Arrange - Create job with tasks and progress
        EncodingJob job = CreateTestJob();
        List<EncodingTask> tasks = Enumerable.Range(0, 5)
            .Select(_ => CreateTestTask(job.Id))
            .ToList();
        tasks[0].State = EncodingTaskState.Completed;
        tasks[1].State = EncodingTaskState.Running;

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync(tasks);
        await _queueContext.SaveChangesAsync();

        // Add progress for running task
        EncodingProgress progress = new()
        {
            Id = Ulid.NewUlid(),
            TaskId = tasks[1].Id,
            ProgressPercentage = 45.0,
            Fps = 60.0,
            Speed = 2.5,
            RecordedAt = DateTime.UtcNow
        };
        await _queueContext.EncodingProgress.AddAsync(progress);
        await _queueContext.SaveChangesAsync();

        // Act - Simulate API GetJobStatus endpoint
        Stopwatch sw = Stopwatch.StartNew();

        // Query job with tasks and latest progress
        EncodingJob? queriedJob = await _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        Dictionary<Ulid, EncodingProgress?> latestProgressMap = [];
        foreach (EncodingTask task in queriedJob!.Tasks)
        {
            EncodingProgress? latest = await _queueContext.EncodingProgress
                .Where(p => p.TaskId == task.Id)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();
            latestProgressMap[task.Id] = latest;
        }

        // Calculate aggregated status
        int completedCount = queriedJob.Tasks.Count(t => t.State == EncodingTaskState.Completed);
        int totalWeight = queriedJob.Tasks.Sum(t => t.Weight);
        int completedWeight = queriedJob.Tasks
            .Where(t => t.State == EncodingTaskState.Completed)
            .Sum(t => t.Weight);
        double overallProgress = totalWeight > 0 ? (completedWeight * 100.0 / totalWeight) : 0;

        sw.Stop();

        // Assert
        Assert.NotNull(queriedJob);
        Assert.True(sw.ElapsedMilliseconds < ApiResponseTimeMs,
            $"GetJobStatus simulation took {sw.ElapsedMilliseconds}ms, expected < {ApiResponseTimeMs}ms");

        _output.WriteLine($"GetJobStatus API simulation: {sw.ElapsedMilliseconds}ms (threshold: {ApiResponseTimeMs}ms)");
        _output.WriteLine($"  Tasks: {queriedJob.Tasks.Count}, Completed: {completedCount}");
        _output.WriteLine($"  Overall progress: {overallProgress:F1}%");
    }

    [Fact]
    public async Task ApiSimulation_ListJobs_CompletesWithinThreshold()
    {
        // Arrange - Create multiple jobs
        List<EncodingJob> jobs = Enumerable.Range(0, 50)
            .Select(_ => CreateTestJob())
            .ToList();

        // Set various states
        string[] states = [EncodingJobState.Queued, EncodingJobState.Encoding, EncodingJobState.Completed, EncodingJobState.Failed, EncodingJobState.Cancelled];
        for (int i = 0; i < jobs.Count; i++)
        {
            jobs[i].State = states[i % 5];
        }

        await _queueContext!.EncodingJobs.AddRangeAsync(jobs);
        await _queueContext.SaveChangesAsync();

        // Act - Simulate API list endpoint with filtering
        Stopwatch sw = Stopwatch.StartNew();

        List<EncodingJob> activeJobs = await _queueContext.EncodingJobs
            .Where(j => j.State == EncodingJobState.Queued || j.State == EncodingJobState.Encoding)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Take(20)
            .ToListAsync();

        sw.Stop();

        // Assert
        Assert.NotEmpty(activeJobs);
        Assert.True(sw.ElapsedMilliseconds < ApiResponseTimeMs,
            $"ListJobs simulation took {sw.ElapsedMilliseconds}ms, expected < {ApiResponseTimeMs}ms");

        _output.WriteLine($"ListJobs API simulation: {sw.ElapsedMilliseconds}ms (threshold: {ApiResponseTimeMs}ms)");
        _output.WriteLine($"  Returned: {activeJobs.Count} active jobs from {jobs.Count} total");
    }

    [Fact]
    public async Task ApiSimulation_GetNodeStatuses_CompletesWithinThreshold()
    {
        // Arrange - Create node cluster
        List<EncoderNode> nodes = Enumerable.Range(0, 20)
            .Select(i => CreateTestNode($"Node-{i}"))
            .ToList();

        await _queueContext!.EncoderNodes.AddRangeAsync(nodes);
        await _queueContext.SaveChangesAsync();

        // Act - Simulate API node status endpoint
        Stopwatch sw = Stopwatch.StartNew();

        List<EncoderNode> allNodes = await _queueContext.EncoderNodes
            .OrderBy(n => n.Name)
            .ToListAsync();

        // Aggregate statistics
        var summary = new
        {
            Total = allNodes.Count,
            Healthy = allNodes.Count(n => n.IsHealthy),
            WithGpu = allNodes.Count(n => n.HasGpu),
            TotalCapacity = allNodes.Sum(n => n.MaxConcurrentTasks),
            CurrentLoad = allNodes.Sum(n => n.CurrentTaskCount)
        };

        sw.Stop();

        // Assert
        Assert.Equal(20, allNodes.Count);
        Assert.True(sw.ElapsedMilliseconds < ApiResponseTimeMs,
            $"GetNodeStatuses simulation took {sw.ElapsedMilliseconds}ms, expected < {ApiResponseTimeMs}ms");

        _output.WriteLine($"GetNodeStatuses API simulation: {sw.ElapsedMilliseconds}ms (threshold: {ApiResponseTimeMs}ms)");
        _output.WriteLine($"  Nodes: {summary.Total}, Healthy: {summary.Healthy}, GPU: {summary.WithGpu}");
        _output.WriteLine($"  Capacity: {summary.CurrentLoad}/{summary.TotalCapacity}");
    }

    #endregion

    #region Scalability Tests (TR7-TR10)

    [Fact]
    public async Task Scalability_100ConcurrentJobs_MaintainsPerformance()
    {
        // Arrange - TR9: 100+ queued jobs without degradation
        List<EncodingJob> jobs = Enumerable.Range(0, 100)
            .Select(_ => CreateTestJob())
            .ToList();

        // Add 5 tasks per job = 500 total tasks
        List<EncodingTask> allTasks = jobs
            .SelectMany(j => Enumerable.Range(0, 5).Select(_ => CreateTestTask(j.Id)))
            .ToList();

        await _queueContext!.EncodingJobs.AddRangeAsync(jobs);
        await _queueContext.EncodingTasks.AddRangeAsync(allTasks);
        await _queueContext.SaveChangesAsync();

        // Act - Perform multiple query operations
        Stopwatch sw = Stopwatch.StartNew();

        // 1. Get all pending tasks
        List<EncodingTask> pendingTasks = await _queueContext.EncodingTasks
            .Where(t => t.State == EncodingTaskState.Pending)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // 2. Get job count by state
        var jobsByState = await _queueContext.EncodingJobs
            .GroupBy(j => j.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync();

        // 3. Get next executable task
        EncodingTask? nextTask = await _queueContext.EncodingTasks
            .Where(t => t.State == EncodingTaskState.Pending && t.DependenciesJson == "[]")
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        sw.Stop();

        // Assert
        Assert.Equal(500, pendingTasks.Count);
        Assert.NotNull(nextTask);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"100-job queries took {sw.ElapsedMilliseconds}ms, expected < 500ms");

        _output.WriteLine($"Scalability test (100 jobs, 500 tasks):");
        _output.WriteLine($"  Total query time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Pending tasks found: {pendingTasks.Count}");
    }

    [Fact]
    public void Scalability_BatchTaskAssignment_HandlesLargeWorkloads()
    {
        // Arrange - Simulate stress scenario: 500 tasks across 50 nodes
        INodeSelector nodeSelector = _serviceProvider!.GetRequiredService<INodeSelector>();
        List<EncoderNode> nodes = CreateNodeCluster(50);
        List<EncodingTaskDefinition> tasks = CreateRealisticTaskSet(500);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        BatchAssignmentResult result = nodeSelector.SelectNodesForTasks(tasks, nodes);
        sw.Stop();

        // Calculate throughput
        double tasksPerSecond = result.AssignedCount / (sw.ElapsedMilliseconds / 1000.0);

        // Assert - Should handle 500 tasks in under 5 seconds
        Assert.True(sw.ElapsedMilliseconds < TaskAssignmentLatencyMs,
            $"Large batch took {sw.ElapsedMilliseconds}ms, expected < {TaskAssignmentLatencyMs}ms");
        Assert.True(tasksPerSecond > 100, $"Assignment rate {tasksPerSecond:F0}/sec too slow");

        _output.WriteLine($"Large batch assignment (500 tasks, 50 nodes):");
        _output.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Assigned: {result.AssignedCount}");
        _output.WriteLine($"  Throughput: {tasksPerSecond:F0} tasks/second");
    }

    #endregion

    #region Helper Methods

    private static EncodingJob CreateTestJob()
    {
        return new EncodingJob
        {
            Id = Ulid.NewUlid(),
            Title = $"Test Job {Guid.NewGuid():N}",
            State = EncodingJobState.Queued,
            Priority = 0,
            InputFilePath = "/test/input.mkv",
            OutputFolder = "/test/output",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static EncodingTask CreateTestTask(Ulid jobId)
    {
        return new EncodingTask
        {
            Id = Ulid.NewUlid(),
            JobId = jobId,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Pending,
            Weight = 100,
            DependenciesJson = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static EncoderNode CreateTestNode(string name)
    {
        return new EncoderNode
        {
            Id = Ulid.NewUlid(),
            Name = name,
            IpAddress = $"192.168.1.{Random.Shared.Next(1, 255)}",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            HasGpu = Random.Shared.Next(2) == 0,
            GpuModel = Random.Shared.Next(2) == 0 ? "NVIDIA RTX 4090" : null,
            CpuCores = Random.Shared.Next(4, 32),
            MemoryGb = Random.Shared.Next(16, 128),
            MaxConcurrentTasks = Random.Shared.Next(2, 8),
            CurrentTaskCount = 0,
            LastHeartbeat = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static List<EncoderNode> CreateNodeCluster(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = $"Node-{i}",
                IpAddress = $"192.168.1.{i + 10}",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = i % 3 == 0,
                GpuModel = i % 3 == 0 ? "NVIDIA RTX 4090" : null,
                GpuVendor = i % 3 == 0 ? "nvidia" : null,
                CpuCores = 8 + (i % 4) * 4,
                MemoryGb = 16 + (i % 4) * 16,
                MaxConcurrentTasks = 2 + (i % 3),
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();
    }

    private static List<EncoderNode> CreateHeterogeneousCluster()
    {
        return
        [
            new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = "GPU-High",
                IpAddress = "192.168.1.10",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = true,
                GpuModel = "NVIDIA RTX 4090",
                GpuVendor = "nvidia",
                CpuCores = 16,
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
                Name = "GPU-Mid",
                IpAddress = "192.168.1.11",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = true,
                GpuModel = "NVIDIA RTX 3080",
                GpuVendor = "nvidia",
                CpuCores = 8,
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
                Name = "CPU-High",
                IpAddress = "192.168.1.12",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = false,
                CpuCores = 32,
                MemoryGb = 128,
                MaxConcurrentTasks = 4,
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new EncoderNode
            {
                Id = Ulid.NewUlid(),
                Name = "CPU-Low",
                IpAddress = "192.168.1.13",
                Port = 8080,
                IsEnabled = true,
                IsHealthy = true,
                HasGpu = false,
                CpuCores = 4,
                MemoryGb = 16,
                MaxConcurrentTasks = 1,
                CurrentTaskCount = 0,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ];
    }

    private static List<EncodingTaskDefinition> CreateRealisticTaskSet(int count)
    {
        List<EncodingTaskDefinition> tasks = [];
        string[] taskTypes =
        [
            EncodingTaskType.HdrConversion,
            EncodingTaskType.VideoEncoding,
            EncodingTaskType.VideoEncoding,
            EncodingTaskType.VideoEncoding,
            EncodingTaskType.AudioEncoding,
            EncodingTaskType.AudioEncoding,
            EncodingTaskType.SubtitleExtraction,
            EncodingTaskType.FontExtraction,
            EncodingTaskType.SpriteGeneration,
            EncodingTaskType.PlaylistGeneration
        ];

        for (int i = 0; i < count; i++)
        {
            string taskType = taskTypes[i % taskTypes.Length];
            tasks.Add(new EncodingTaskDefinition
            {
                Id = Ulid.NewUlid().ToString(),
                TaskType = taskType,
                Weight = taskType switch
                {
                    EncodingTaskType.HdrConversion => 200,
                    EncodingTaskType.VideoEncoding => 100,
                    EncodingTaskType.AudioEncoding => 50,
                    EncodingTaskType.SpriteGeneration => 30,
                    _ => 10
                },
                RequiresGpu = taskType is EncodingTaskType.HdrConversion or EncodingTaskType.VideoEncoding,
                EstimatedMemoryMb = taskType switch
                {
                    EncodingTaskType.HdrConversion => 16000,
                    EncodingTaskType.VideoEncoding => 8000,
                    _ => 2000
                }
            });
        }

        return tasks;
    }

    private static string GenerateMediaPlaylist(int segmentCount)
    {
        List<string> lines =
        [
            "#EXTM3U",
            "#EXT-X-VERSION:3",
            "#EXT-X-TARGETDURATION:10",
            "#EXT-X-MEDIA-SEQUENCE:0",
            "#EXT-X-PLAYLIST-TYPE:VOD",
            "#EXT-X-INDEPENDENT-SEGMENTS"
        ];

        for (int i = 0; i < segmentCount; i++)
        {
            lines.Add("#EXTINF:6.000000,");
            lines.Add($"segment_{i:D5}.ts");
        }

        lines.Add("#EXT-X-ENDLIST");
        return string.Join("\n", lines);
    }

    private static string GenerateMasterPlaylist(int videoVariants, int audioGroups)
    {
        List<string> lines =
        [
            "#EXTM3U",
            "#EXT-X-VERSION:3"
        ];

        // Audio groups
        string[] languages = ["eng", "jpn", "spa", "fre", "ger"];
        for (int i = 0; i < audioGroups && i < languages.Length; i++)
        {
            string lang = languages[i];
            string isDefault = i == 0 ? "YES" : "NO";
            lines.Add($"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"{lang.ToUpper()} - AAC\",LANGUAGE=\"{lang}\",DEFAULT={isDefault},AUTOSELECT=YES,URI=\"audio_{lang}_aac/audio_{lang}_aac.m3u8\"");
        }

        // Video variants
        int[] bandwidths = [15_000_000, 8_000_000, 5_000_000, 3_000_000, 1_500_000];
        string[] resolutions = ["3840x2160", "1920x1080", "1280x720", "854x480", "640x360"];

        for (int i = 0; i < videoVariants && i < bandwidths.Length; i++)
        {
            lines.Add($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidths[i]},RESOLUTION={resolutions[i]},CODECS=\"avc1.640028,mp4a.40.2\",AUDIO=\"audio\"");
            lines.Add($"video_{resolutions[i].Replace("x", "x")}_SDR/video_{resolutions[i].Replace("x", "x")}_SDR.m3u8");
        }

        return string.Join("\n", lines);
    }

    #endregion
}
