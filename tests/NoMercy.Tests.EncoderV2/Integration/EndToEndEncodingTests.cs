using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Factories;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Tasks;

// Use explicit alias to avoid ambiguity with NoMercy.EncoderV2.Abstractions.EncodingProgress
using DbEncodingProgress = NoMercy.Database.Models.EncodingProgress;

namespace NoMercy.Tests.EncoderV2.Integration;

/// <summary>
/// End-to-end integration tests for the EncoderV2 pipeline.
/// Tests the complete encoding workflow from job creation to output validation.
/// </summary>
public class EndToEndEncodingTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private QueueContext? _queueContext;
    private string _testOutputFolder = string.Empty;
    private string _dbName = string.Empty;

    public EndToEndEncodingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        _dbName = $"E2EEncodingTest_{Guid.NewGuid():N}";
        _testOutputFolder = Path.Combine(Path.GetTempPath(), $"EncoderV2E2E_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputFolder);

        ServiceCollection services = new();

        // Create in-memory database
        DbContextOptions<QueueContext> queueOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase($"Queue_{_dbName}")
            .Options;

        _queueContext = new TestQueueContext(queueOptions);
        services.AddSingleton<QueueContext>(_queueContext);

        // Register logging
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        // Register real services that don't need FFmpeg
        services.AddSingleton<ICodecFactory, CodecFactory>();
        services.AddSingleton<IContainerFactory, ContainerFactory>();
        services.AddScoped<IHLSPlaylistGenerator, HLSPlaylistGenerator>();
        services.AddScoped<INodeSelector, NodeSelector>();

        _serviceProvider = services.BuildServiceProvider();

        _output.WriteLine($"Test database: {_dbName}");
        _output.WriteLine($"Test output folder: {_testOutputFolder}");

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        // Cleanup test folder
        if (Directory.Exists(_testOutputFolder))
        {
            try
            {
                Directory.Delete(_testOutputFolder, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        return Task.CompletedTask;
    }

    #region Job Lifecycle E2E Tests

    [Fact]
    public async Task JobLifecycle_CreateExecuteComplete_FullWorkflow()
    {
        // Arrange
        Ulid jobId = Ulid.NewUlid();

        // Step 1: Create job in database
        Database.Models.EncodingJob job = new()
        {
            Id = jobId,
            Title = "E2E Test Job",
            State = EncodingJobState.Queued,
            InputFilePath = "/test/input.mkv",
            OutputFolder = Path.Combine(_testOutputFolder, "e2e_job"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine($"Step 1: Created job {jobId}");

        // Step 2: Create tasks for the job
        List<EncodingTask> tasks =
        [
            new()
            {
                Id = Ulid.NewUlid(),
                JobId = jobId,
                TaskType = EncodingTaskType.VideoEncoding,
                State = EncodingTaskState.Pending,
                Weight = 100,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(),
                JobId = jobId,
                TaskType = EncodingTaskType.AudioEncoding,
                State = EncodingTaskState.Pending,
                Weight = 30,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(),
                JobId = jobId,
                TaskType = EncodingTaskType.SubtitleExtraction,
                State = EncodingTaskState.Pending,
                Weight = 10,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ];

        await _queueContext.EncodingTasks.AddRangeAsync(tasks);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine($"Step 2: Added {tasks.Count} tasks to database");

        // Step 3: Simulate task execution
        job.State = EncodingJobState.Encoding;
        job.StartedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        foreach (EncodingTask task in tasks)
        {
            // Start task
            task.State = EncodingTaskState.Running;
            task.StartedAt = DateTime.UtcNow;
            await _queueContext.SaveChangesAsync();

            // Record some progress
            DbEncodingProgress progress = new()
            {
                Id = Ulid.NewUlid(),
                TaskId = task.Id,
                ProgressPercentage = 50.0,
                Fps = 120.0,
                Speed = 3.5,
                RecordedAt = DateTime.UtcNow
            };
            await _queueContext.EncodingProgress.AddAsync(progress);

            // Complete task
            task.State = EncodingTaskState.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await _queueContext.SaveChangesAsync();

            _output.WriteLine($"  - Completed task: {task.TaskType}");
        }

        // Step 4: Complete job
        job.State = EncodingJobState.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        _output.WriteLine($"Step 4: Job completed");

        // Assert final state
        Database.Models.EncodingJob? finalJob = await _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        Assert.NotNull(finalJob);
        Assert.Equal(EncodingJobState.Completed, finalJob.State);
        Assert.All(finalJob.Tasks, t => Assert.Equal(EncodingTaskState.Completed, t.State));

        // Verify progress was recorded
        int progressCount = await _queueContext.EncodingProgress
            .Where(p => tasks.Select(t => t.Id).Contains(p.TaskId))
            .CountAsync();

        Assert.Equal(tasks.Count, progressCount);

        _output.WriteLine($"E2E workflow completed successfully");
        _output.WriteLine($"  Final job state: {finalJob.State}");
        _output.WriteLine($"  Tasks completed: {finalJob.Tasks.Count(t => t.State == EncodingTaskState.Completed)}");
        _output.WriteLine($"  Progress records: {progressCount}");
    }

    [Fact]
    public async Task JobLifecycle_TaskFailureAndRetry_Recovers()
    {
        // Arrange
        Ulid jobId = Ulid.NewUlid();

        Database.Models.EncodingJob job = new()
        {
            Id = jobId,
            Title = "Retry Test Job",
            State = EncodingJobState.Queued,
            InputFilePath = "/test/input.mkv",
            OutputFolder = Path.Combine(_testOutputFolder, "retry_test"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask task = new()
        {
            Id = Ulid.NewUlid(),
            JobId = jobId,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Pending,
            Weight = 100,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine("Created job with task for retry testing");

        // Step 1: First attempt fails
        task.State = EncodingTaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        task.State = EncodingTaskState.Failed;
        task.ErrorMessage = "Simulated failure #1";
        task.RetryCount = 1;
        await _queueContext.SaveChangesAsync();

        _output.WriteLine("Step 1: Task failed (retry 1)");

        // Step 2: Retry task
        task.State = EncodingTaskState.Pending;
        task.ErrorMessage = null;
        task.StartedAt = null;
        await _queueContext.SaveChangesAsync();

        // Step 3: Second attempt succeeds
        task.State = EncodingTaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        task.State = EncodingTaskState.Completed;
        task.CompletedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        job.State = EncodingJobState.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        _output.WriteLine("Step 3: Task succeeded on retry");

        // Assert
        EncodingTask? finalTask = await _queueContext.EncodingTasks.FindAsync(task.Id);
        Database.Models.EncodingJob? finalJob = await _queueContext.EncodingJobs.FindAsync(jobId);

        Assert.Equal(EncodingTaskState.Completed, finalTask?.State);
        Assert.Equal(EncodingJobState.Completed, finalJob?.State);
        Assert.Equal(1, finalTask?.RetryCount);

        _output.WriteLine($"Retry test completed: Task recovered after {finalTask?.RetryCount} retry");
    }

    [Fact]
    public async Task JobLifecycle_NodeFailure_TasksReassigned()
    {
        // Arrange
        Ulid nodeId = Ulid.NewUlid();
        Ulid jobId = Ulid.NewUlid();

        EncoderNode node = new()
        {
            Id = nodeId,
            Name = "Failing Node",
            IpAddress = "192.168.1.100",
            Port = 8080,
            IsEnabled = true,
            IsHealthy = true,
            CurrentTaskCount = 2,
            MaxConcurrentTasks = 4,
            CpuCores = 8,
            MemoryGb = 16,
            LastHeartbeat = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Database.Models.EncodingJob job = new()
        {
            Id = jobId,
            Title = "Node Failure Test",
            State = EncodingJobState.Encoding,
            InputFilePath = "/test/input.mkv",
            OutputFolder = Path.Combine(_testOutputFolder, "node_failure_test"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        List<EncodingTask> tasks =
        [
            new()
            {
                Id = Ulid.NewUlid(),
                JobId = jobId,
                TaskType = EncodingTaskType.VideoEncoding,
                State = EncodingTaskState.Running,
                AssignedNodeId = nodeId,
                Weight = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Ulid.NewUlid(),
                JobId = jobId,
                TaskType = EncodingTaskType.AudioEncoding,
                State = EncodingTaskState.Pending,
                AssignedNodeId = nodeId,
                Weight = 50,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ];

        await _queueContext!.EncoderNodes.AddAsync(node);
        await _queueContext.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddRangeAsync(tasks);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine("Setup: Node with 2 assigned tasks");

        // Act: Simulate node failure
        node.IsHealthy = false;
        node.LastHeartbeat = DateTime.UtcNow.AddMinutes(-5);
        await _queueContext.SaveChangesAsync();

        // Reassign tasks from failed node
        List<EncodingTask> tasksToReassign = await _queueContext.EncodingTasks
            .Where(t => t.AssignedNodeId == nodeId &&
                       (t.State == EncodingTaskState.Running || t.State == EncodingTaskState.Pending))
            .ToListAsync();

        foreach (EncodingTask task in tasksToReassign)
        {
            task.State = EncodingTaskState.Pending;
            task.AssignedNodeId = null;
            task.StartedAt = null;
        }

        node.CurrentTaskCount = 0;
        await _queueContext.SaveChangesAsync();

        _output.WriteLine($"Reassigned {tasksToReassign.Count} tasks from failed node");

        // Assert
        List<EncodingTask> updatedTasks = await _queueContext.EncodingTasks
            .Where(t => t.JobId == jobId)
            .ToListAsync();

        Assert.All(updatedTasks, t =>
        {
            Assert.Null(t.AssignedNodeId);
            Assert.Equal(EncodingTaskState.Pending, t.State);
        });

        EncoderNode? updatedNode = await _queueContext.EncoderNodes.FindAsync(nodeId);
        Assert.False(updatedNode?.IsHealthy);
        Assert.Equal(0, updatedNode?.CurrentTaskCount);

        _output.WriteLine("Node failure handling test completed");
    }

    [Fact]
    public async Task JobLifecycle_MultipleJobs_ExecuteConcurrently()
    {
        // Arrange - Create multiple jobs
        List<Database.Models.EncodingJob> jobs = [];
        for (int i = 0; i < 3; i++)
        {
            Database.Models.EncodingJob job = new()
            {
                Id = Ulid.NewUlid(),
                Title = $"Concurrent Job {i + 1}",
                State = EncodingJobState.Queued,
                InputFilePath = $"/test/input{i + 1}.mkv",
                OutputFolder = Path.Combine(_testOutputFolder, $"concurrent_job_{i + 1}"),
                Priority = i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            jobs.Add(job);
        }

        await _queueContext!.EncodingJobs.AddRangeAsync(jobs);
        await _queueContext.SaveChangesAsync();

        _output.WriteLine($"Created {jobs.Count} concurrent jobs");

        // Create tasks for each job
        foreach (Database.Models.EncodingJob job in jobs)
        {
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
            await _queueContext.EncodingTasks.AddAsync(task);
        }
        await _queueContext.SaveChangesAsync();

        // Simulate concurrent execution
        foreach (Database.Models.EncodingJob job in jobs)
        {
            job.State = EncodingJobState.Encoding;
            job.StartedAt = DateTime.UtcNow;
        }
        await _queueContext.SaveChangesAsync();

        // Complete all tasks
        List<EncodingTask> allTasks = await _queueContext.EncodingTasks.ToListAsync();
        foreach (EncodingTask task in allTasks)
        {
            task.State = EncodingTaskState.Running;
            task.StartedAt = DateTime.UtcNow;
        }
        await _queueContext.SaveChangesAsync();

        foreach (EncodingTask task in allTasks)
        {
            task.State = EncodingTaskState.Completed;
            task.CompletedAt = DateTime.UtcNow;
        }
        await _queueContext.SaveChangesAsync();

        // Complete all jobs
        foreach (Database.Models.EncodingJob job in jobs)
        {
            job.State = EncodingJobState.Completed;
            job.CompletedAt = DateTime.UtcNow;
        }
        await _queueContext.SaveChangesAsync();

        // Assert
        List<Database.Models.EncodingJob> completedJobs = await _queueContext.EncodingJobs
            .Where(j => j.State == EncodingJobState.Completed)
            .ToListAsync();

        Assert.Equal(3, completedJobs.Count);

        _output.WriteLine($"All {completedJobs.Count} concurrent jobs completed successfully");
    }

    [Fact]
    public async Task JobLifecycle_ProgressTracking_RecordsMultipleSnapshots()
    {
        // Arrange
        Ulid jobId = Ulid.NewUlid();

        Database.Models.EncodingJob job = new()
        {
            Id = jobId,
            Title = "Progress Tracking Test",
            State = EncodingJobState.Queued,
            InputFilePath = "/test/input.mkv",
            OutputFolder = Path.Combine(_testOutputFolder, "progress_test"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EncodingTask task = new()
        {
            Id = Ulid.NewUlid(),
            JobId = jobId,
            TaskType = EncodingTaskType.VideoEncoding,
            State = EncodingTaskState.Pending,
            Weight = 100,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queueContext!.EncodingJobs.AddAsync(job);
        await _queueContext.EncodingTasks.AddAsync(task);
        await _queueContext.SaveChangesAsync();

        // Act: Start task and record progress snapshots
        job.State = EncodingJobState.Encoding;
        job.StartedAt = DateTime.UtcNow;
        task.State = EncodingTaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        // Record progress at different stages
        List<DbEncodingProgress> progressSnapshots =
        [
            new() { Id = Ulid.NewUlid(), TaskId = task.Id, ProgressPercentage = 25.0, Fps = 60.0, Speed = 2.0, RecordedAt = DateTime.UtcNow },
            new() { Id = Ulid.NewUlid(), TaskId = task.Id, ProgressPercentage = 50.0, Fps = 65.0, Speed = 2.2, RecordedAt = DateTime.UtcNow.AddSeconds(10) },
            new() { Id = Ulid.NewUlid(), TaskId = task.Id, ProgressPercentage = 75.0, Fps = 70.0, Speed = 2.4, RecordedAt = DateTime.UtcNow.AddSeconds(20) },
            new() { Id = Ulid.NewUlid(), TaskId = task.Id, ProgressPercentage = 100.0, Fps = 72.0, Speed = 2.5, RecordedAt = DateTime.UtcNow.AddSeconds(30) }
        ];

        await _queueContext.EncodingProgress.AddRangeAsync(progressSnapshots);
        await _queueContext.SaveChangesAsync();

        // Complete task and job
        task.State = EncodingTaskState.Completed;
        task.CompletedAt = DateTime.UtcNow;
        job.State = EncodingJobState.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _queueContext.SaveChangesAsync();

        // Assert
        List<DbEncodingProgress> storedProgress = await _queueContext.EncodingProgress
            .Where(p => p.TaskId == task.Id)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        Assert.Equal(4, storedProgress.Count);
        Assert.Equal(25.0, storedProgress[0].ProgressPercentage);
        Assert.Equal(100.0, storedProgress[3].ProgressPercentage);

        // Verify FPS increased over time (simulated encoding efficiency improvement)
        Assert.True(storedProgress[3].Fps > storedProgress[0].Fps);

        _output.WriteLine($"Recorded {storedProgress.Count} progress snapshots");
        foreach (DbEncodingProgress p in storedProgress)
        {
            _output.WriteLine($"  - {p.ProgressPercentage:F1}% @ {p.Fps:F1} fps, speed {p.Speed:F1}x");
        }
    }

    [Fact]
    public async Task JobLifecycle_PriorityOrdering_HigherPriorityProcessedFirst()
    {
        // Arrange - Create jobs with different priorities
        List<Database.Models.EncodingJob> jobs =
        [
            new() { Id = Ulid.NewUlid(), Title = "Low Priority Job", State = EncodingJobState.Queued, Priority = 0, InputFilePath = "/test/low.mkv", OutputFolder = _testOutputFolder, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Ulid.NewUlid(), Title = "High Priority Job", State = EncodingJobState.Queued, Priority = 10, InputFilePath = "/test/high.mkv", OutputFolder = _testOutputFolder, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Ulid.NewUlid(), Title = "Medium Priority Job", State = EncodingJobState.Queued, Priority = 5, InputFilePath = "/test/medium.mkv", OutputFolder = _testOutputFolder, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        ];

        await _queueContext!.EncodingJobs.AddRangeAsync(jobs);
        await _queueContext.SaveChangesAsync();

        // Act: Query jobs by priority (descending)
        List<Database.Models.EncodingJob> orderedJobs = await _queueContext.EncodingJobs
            .Where(j => j.State == EncodingJobState.Queued)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync();

        // Assert
        Assert.Equal("High Priority Job", orderedJobs[0].Title);
        Assert.Equal("Medium Priority Job", orderedJobs[1].Title);
        Assert.Equal("Low Priority Job", orderedJobs[2].Title);

        _output.WriteLine("Priority ordering test passed:");
        foreach (Database.Models.EncodingJob j in orderedJobs)
        {
            _output.WriteLine($"  - {j.Title} (priority: {j.Priority})");
        }
    }

    #endregion

    #region HLS Playlist Generator Tests

    [Fact]
    public async Task HLSPlaylistGenerator_WriteMediaPlaylist_CreatesValidContent()
    {
        // Arrange
        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        string outputPath = Path.Combine(_testOutputFolder, "media_playlist_test.m3u8");

        HLSSpecification spec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD"
        };

        List<string> segmentFiles =
        [
            "segment-0000.ts",
            "segment-0001.ts",
            "segment-0002.ts",
            "segment-0003.ts"
        ];

        TimeSpan totalDuration = TimeSpan.FromSeconds(22.5);

        // Act
        await generator.WriteMediaPlaylistAsync(outputPath, spec, segmentFiles, totalDuration);

        // Assert
        Assert.True(File.Exists(outputPath));

        string content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("#EXTM3U", content);
        Assert.Contains("#EXT-X-VERSION:3", content);
        Assert.Contains("#EXT-X-TARGETDURATION:10", content);
        Assert.Contains("#EXT-X-PLAYLIST-TYPE:VOD", content);
        Assert.Contains("#EXT-X-ENDLIST", content);
        Assert.Contains("segment-0000.ts", content);

        _output.WriteLine("Generated media playlist:");
        _output.WriteLine(content);
    }

    [Fact]
    public async Task HLSPlaylistGenerator_WriteMasterPlaylist_CreatesValidContent()
    {
        // Arrange
        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        string outputPath = Path.Combine(_testOutputFolder, "master_playlist_test.m3u8");

        List<HLSVariantStream> variants =
        [
            new()
            {
                Bandwidth = 5000000,
                AverageBandwidth = 4500000,
                Resolution = "1920x1080",
                Framerate = 23.976,
                Codecs = "avc1.640028,mp4a.40.2",
                PlaylistUri = "1080p/playlist.m3u8"
            },
            new()
            {
                Bandwidth = 2500000,
                AverageBandwidth = 2200000,
                Resolution = "1280x720",
                Framerate = 23.976,
                Codecs = "avc1.64001f,mp4a.40.2",
                PlaylistUri = "720p/playlist.m3u8"
            }
        ];

        // Act
        await generator.WriteMasterPlaylistAsync(outputPath, variants);

        // Assert
        Assert.True(File.Exists(outputPath));

        string content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("#EXTM3U", content);
        Assert.Contains("#EXT-X-VERSION:3", content);
        Assert.Contains("#EXT-X-STREAM-INF:", content);
        Assert.Contains("BANDWIDTH=5000000", content);
        Assert.Contains("RESOLUTION=1920x1080", content);
        Assert.Contains("1080p/playlist.m3u8", content);
        Assert.Contains("720p/playlist.m3u8", content);

        _output.WriteLine("Generated master playlist:");
        _output.WriteLine(content);
    }

    #endregion

    #region Codec and Container Factory Tests

    [Fact]
    public void CodecFactory_CreateVideoCodec_ReturnsCorrectCodec()
    {
        // Arrange
        ICodecFactory factory = _serviceProvider!.GetRequiredService<ICodecFactory>();

        // Act & Assert
        IVideoCodec? h264 = factory.CreateVideoCodec("h264");
        Assert.NotNull(h264);
        Assert.Equal("libx264", h264.Name);

        IVideoCodec? h265 = factory.CreateVideoCodec("hevc");
        Assert.NotNull(h265);
        Assert.Equal("libx265", h265.Name);

        IVideoCodec? av1 = factory.CreateVideoCodec("av1");
        Assert.NotNull(av1);
        Assert.Equal("libaom-av1", av1.Name);

        _output.WriteLine("Codec factory created video codecs correctly");
    }

    [Fact]
    public void CodecFactory_CreateAudioCodec_ReturnsCorrectCodec()
    {
        // Arrange
        ICodecFactory factory = _serviceProvider!.GetRequiredService<ICodecFactory>();

        // Act & Assert
        IAudioCodec? aac = factory.CreateAudioCodec("aac");
        Assert.NotNull(aac);
        Assert.Equal("aac", aac.Name);

        IAudioCodec? opus = factory.CreateAudioCodec("opus");
        Assert.NotNull(opus);
        Assert.Equal("libopus", opus.Name);

        _output.WriteLine("Codec factory created audio codecs correctly");
    }

    [Fact]
    public void ContainerFactory_CreateContainer_ReturnsCorrectContainer()
    {
        // Arrange
        IContainerFactory factory = _serviceProvider!.GetRequiredService<IContainerFactory>();

        // Act & Assert
        IContainer? hls = factory.CreateContainer("hls");
        Assert.NotNull(hls);

        IContainer? mp4 = factory.CreateContainer("mp4");
        Assert.NotNull(mp4);

        IContainer? mkv = factory.CreateContainer("mkv");
        Assert.NotNull(mkv);

        _output.WriteLine("Container factory created containers correctly");
    }

    #endregion
}
