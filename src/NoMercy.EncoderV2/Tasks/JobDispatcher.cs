using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Repositories;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Dispatches encoding jobs to the queue system.
/// Integrates with TaskSplitter for job decomposition and NodeSelector for node assignment.
/// </summary>
public class JobDispatcher : IJobDispatcher
{
    private readonly QueueContext _queueContext;
    private readonly IProfileRepository _profileRepository;
    private readonly ITaskSplitter _taskSplitter;
    private readonly INodeSelector _nodeSelector;
    private readonly IStreamAnalyzer _streamAnalyzer;
    private readonly ILogger<JobDispatcher> _logger;

    public JobDispatcher(
        QueueContext queueContext,
        IProfileRepository profileRepository,
        ITaskSplitter taskSplitter,
        INodeSelector nodeSelector,
        IStreamAnalyzer streamAnalyzer,
        ILogger<JobDispatcher> logger)
    {
        _queueContext = queueContext;
        _profileRepository = profileRepository;
        _taskSplitter = taskSplitter;
        _nodeSelector = nodeSelector;
        _streamAnalyzer = streamAnalyzer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JobDispatchResult> DispatchAsync(
        string inputFilePath,
        string outputFolder,
        Ulid? profileId = null,
        JobDispatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new JobDispatchOptions();

        // Get the profile
        EncoderProfile? profile = profileId.HasValue
            ? await _profileRepository.GetProfileAsync(profileId.Value)
            : await _profileRepository.GetDefaultProfileAsync();

        if (profile == null)
        {
            return new JobDispatchResult
            {
                Success = false,
                ErrorMessage = profileId.HasValue
                    ? $"Profile with ID {profileId} not found"
                    : "No default profile found"
            };
        }

        return await DispatchAsync(inputFilePath, outputFolder, profile, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JobDispatchResult> DispatchAsync(
        string inputFilePath,
        string outputFolder,
        EncoderProfile profile,
        JobDispatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new JobDispatchOptions();

        try
        {
            // Validate input file exists
            if (!File.Exists(inputFilePath))
            {
                return new JobDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"Input file not found: {inputFilePath}"
                };
            }

            // Analyze the source media
            _logger.LogInformation("Analyzing source file: {InputFile}", inputFilePath);
            StreamAnalysis analysis = await _streamAnalyzer.AnalyzeAsync(inputFilePath, cancellationToken);

            // Create the encoding job
            EncodingJob job = new()
            {
                Id = Ulid.NewUlid(),
                ProfileId = profile.Id,
                ProfileSnapshotJson = JsonConvert.SerializeObject(profile),
                InputFilePath = inputFilePath,
                OutputFolder = outputFolder,
                Title = options.Title ?? Path.GetFileNameWithoutExtension(inputFilePath),
                State = EncodingJobState.Queued,
                Priority = (int)options.Priority,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Split the job into tasks
            TaskSplittingOptions splittingOptions = new()
            {
                IncludePostProcessing = options.IncludePostProcessing,
                IncludeValidation = options.IncludeValidation
            };

            TaskSplitResult splitResult = _taskSplitter.SplitJob(
                analysis,
                profile,
                options.SplitStrategy,
                splittingOptions);

            _logger.LogInformation(
                "Job split into {TaskCount} tasks with {MaxParallelism} max parallelism using {Strategy} strategy",
                splitResult.Tasks.Count,
                splitResult.MaxParallelism,
                splitResult.UsedStrategy);

            // Convert task definitions to database entities
            List<EncodingTask> tasks = _taskSplitter.ToEncodingTasks(job.Id, splitResult.Tasks);

            // Set max retries from options
            foreach (EncodingTask task in tasks)
            {
                task.MaxRetries = options.MaxTaskRetries;
            }

            // Optionally assign tasks to nodes immediately
            List<UnassignedTask> unassignedTasks = [];
            if (options.AssignNodesImmediately)
            {
                List<EncoderNode> availableNodes = await _queueContext.EncoderNodes
                    .Where(n => n.IsEnabled && n.IsHealthy)
                    .ToListAsync(cancellationToken);

                if (availableNodes.Count > 0)
                {
                    NodeSelectionOptions nodeOptions = new()
                    {
                        Strategy = options.NodeStrategy,
                        ExcludeFullNodes = true
                    };

                    BatchAssignmentResult assignmentResult = _nodeSelector.SelectNodesForTasks(
                        splitResult.Tasks,
                        availableNodes,
                        nodeOptions);

                    // Apply node assignments to tasks
                    foreach (TaskAssignment assignment in assignmentResult.Assignments)
                    {
                        EncodingTask? task = tasks.FirstOrDefault(t => t.Id.ToString() == assignment.Task.Id);
                        if (task != null)
                        {
                            task.AssignedNodeId = assignment.Node.Id;
                        }
                    }

                    unassignedTasks = assignmentResult.UnassignedTasks;

                    _logger.LogInformation(
                        "Assigned {AssignedCount}/{TotalCount} tasks to nodes",
                        assignmentResult.AssignedCount,
                        assignmentResult.TotalTasks);
                }
            }

            // Save job and tasks to database
            await _queueContext.EncodingJobs.AddAsync(job, cancellationToken);
            await _queueContext.EncodingTasks.AddRangeAsync(tasks, cancellationToken);
            await _queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Dispatched encoding job {JobId} with {TaskCount} tasks",
                job.Id,
                tasks.Count);

            return new JobDispatchResult
            {
                Success = true,
                Job = job,
                Tasks = tasks,
                TotalWeight = splitResult.TotalWeight,
                MaxParallelism = splitResult.MaxParallelism,
                UnassignedTasks = unassignedTasks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch encoding job for {InputFile}", inputFilePath);
            return new JobDispatchResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<JobCancelResult> CancelJobAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EncodingJob? job = await _queueContext.EncodingJobs
                .Include(j => j.Tasks)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                return new JobCancelResult
                {
                    Success = false,
                    ErrorMessage = $"Job with ID {jobId} not found"
                };
            }

            int cancelledCount = 0;
            int alreadyCompletedCount = 0;

            foreach (EncodingTask task in job.Tasks)
            {
                if (task.State == EncodingTaskState.Completed)
                {
                    alreadyCompletedCount++;
                }
                else if (task.State != EncodingTaskState.Cancelled)
                {
                    task.State = EncodingTaskState.Cancelled;
                    task.UpdatedAt = DateTime.UtcNow;
                    cancelledCount++;
                }
            }

            job.State = EncodingJobState.Cancelled;
            job.UpdatedAt = DateTime.UtcNow;

            await _queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cancelled job {JobId}: {CancelledCount} tasks cancelled, {CompletedCount} already completed",
                jobId,
                cancelledCount,
                alreadyCompletedCount);

            return new JobCancelResult
            {
                Success = true,
                CancelledTaskCount = cancelledCount,
                AlreadyCompletedCount = alreadyCompletedCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return new JobCancelResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<TaskRetryResult> RetryFailedTasksAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<EncodingTask> failedTasks = await _queueContext.EncodingTasks
                .Where(t => t.JobId == jobId && t.State == EncodingTaskState.Failed)
                .ToListAsync(cancellationToken);

            if (failedTasks.Count == 0)
            {
                return new TaskRetryResult
                {
                    Success = true,
                    RetriedCount = 0
                };
            }

            int retriedCount = 0;
            List<EncodingTask> exceededMaxRetries = [];

            foreach (EncodingTask task in failedTasks)
            {
                if (task.RetryCount >= task.MaxRetries)
                {
                    exceededMaxRetries.Add(task);
                }
                else
                {
                    task.State = EncodingTaskState.Pending;
                    task.RetryCount++;
                    task.ErrorMessage = null;
                    task.StartedAt = null;
                    task.CompletedAt = null;
                    task.AssignedNodeId = null;
                    task.UpdatedAt = DateTime.UtcNow;
                    retriedCount++;
                }
            }

            // Update job state if we're retrying
            if (retriedCount > 0)
            {
                EncodingJob? job = await _queueContext.EncodingJobs
                    .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

                if (job != null && job.State == EncodingJobState.Failed)
                {
                    job.State = EncodingJobState.Queued;
                    job.ErrorMessage = null;
                    job.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Retried {RetriedCount} failed tasks for job {JobId}, {ExceededCount} exceeded max retries",
                retriedCount,
                jobId,
                exceededMaxRetries.Count);

            return new TaskRetryResult
            {
                Success = true,
                RetriedCount = retriedCount,
                ExceededMaxRetries = exceededMaxRetries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry tasks for job {JobId}", jobId);
            return new TaskRetryResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<JobStatus?> GetJobStatusAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default)
    {
        EncodingJob? job = await _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .ThenInclude(t => t.ProgressHistory.OrderByDescending(p => p.RecordedAt).Take(1))
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            return null;
        }

        List<EncodingTask> tasks = job.Tasks.ToList();

        int pendingTasks = tasks.Count(t => t.State == EncodingTaskState.Pending);
        int runningTasks = tasks.Count(t => t.State == EncodingTaskState.Running);
        int completedTasks = tasks.Count(t => t.State == EncodingTaskState.Completed);
        int failedTasks = tasks.Count(t => t.State == EncodingTaskState.Failed);
        int totalTasks = tasks.Count;

        // Calculate overall progress based on task weights
        double totalWeight = tasks.Sum(t => t.Weight);
        double completedWeight = tasks
            .Where(t => t.State == EncodingTaskState.Completed)
            .Sum(t => t.Weight);

        // Add partial progress from running tasks
        EncodingTask? currentTask = tasks.FirstOrDefault(t => t.State == EncodingTaskState.Running);
        EncodingProgress? latestProgress = null;

        if (currentTask != null)
        {
            latestProgress = currentTask.ProgressHistory
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefault();

            if (latestProgress != null)
            {
                completedWeight += currentTask.Weight * (latestProgress.ProgressPercentage / 100.0);
            }
        }

        double overallProgress = totalWeight > 0 ? (completedWeight / totalWeight) * 100.0 : 0;

        // Estimate remaining time based on current progress and elapsed time
        TimeSpan? estimatedRemaining = null;
        if (job.StartedAt.HasValue && overallProgress > 0 && overallProgress < 100)
        {
            TimeSpan elapsed = DateTime.UtcNow - job.StartedAt.Value;
            double remainingProgress = 100 - overallProgress;
            estimatedRemaining = TimeSpan.FromTicks((long)(elapsed.Ticks * (remainingProgress / overallProgress)));
        }

        return new JobStatus
        {
            Job = job,
            OverallProgress = Math.Round(overallProgress, 2),
            PendingTasks = pendingTasks,
            RunningTasks = runningTasks,
            CompletedTasks = completedTasks,
            FailedTasks = failedTasks,
            TotalTasks = totalTasks,
            EstimatedRemaining = estimatedRemaining,
            CurrentTask = currentTask,
            LatestProgress = latestProgress
        };
    }

    /// <inheritdoc />
    public async Task<List<EncodingJob>> GetJobsAsync(
        string? state = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        IQueryable<EncodingJob> query = _queueContext.EncodingJobs
            .Include(j => j.Tasks)
            .OrderByDescending(j => j.CreatedAt);

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(j => j.State == state);
        }

        return await query
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EncodingTask?> GetNextPendingTaskAsync(
        Ulid? nodeId = null,
        CancellationToken cancellationToken = default)
    {
        // Get all pending tasks with their jobs ordered by priority and creation time
        IQueryable<EncodingTask> query = _queueContext.EncodingTasks
            .Include(t => t.Job)
            .Where(t => t.State == EncodingTaskState.Pending)
            .OrderByDescending(t => t.Job.Priority)
            .ThenBy(t => t.Job.CreatedAt)
            .ThenBy(t => t.CreatedAt);

        // If a specific node is requesting, prefer tasks assigned to that node
        if (nodeId.HasValue)
        {
            EncodingTask? assignedTask = await query
                .Where(t => t.AssignedNodeId == nodeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (assignedTask != null)
            {
                // Check dependencies are met
                if (await AreDependenciesMetAsync(assignedTask, cancellationToken))
                {
                    return assignedTask;
                }
            }

            // Fall back to unassigned tasks
            query = query.Where(t => t.AssignedNodeId == null || t.AssignedNodeId == nodeId);
        }

        // Find the first task whose dependencies are all completed
        List<EncodingTask> candidates = await query
            .Take(50) // Check up to 50 candidates for efficiency
            .ToListAsync(cancellationToken);

        foreach (EncodingTask candidate in candidates)
        {
            if (await AreDependenciesMetAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> AssignTaskToNodeAsync(
        Ulid taskId,
        Ulid nodeId,
        CancellationToken cancellationToken = default)
    {
        EncodingTask? task = await _queueContext.EncodingTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for assignment", taskId);
            return false;
        }

        EncoderNode? node = await _queueContext.EncoderNodes
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

        if (node == null)
        {
            _logger.LogWarning("Node {NodeId} not found for task assignment", nodeId);
            return false;
        }

        task.AssignedNodeId = nodeId;
        task.UpdatedAt = DateTime.UtcNow;

        await _queueContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Assigned task {TaskId} to node {NodeId}", taskId, nodeId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> StartTaskAsync(
        Ulid taskId,
        CancellationToken cancellationToken = default)
    {
        EncodingTask? task = await _queueContext.EncodingTasks
            .Include(t => t.Job)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for start", taskId);
            return false;
        }

        if (task.State != EncodingTaskState.Pending)
        {
            _logger.LogWarning("Task {TaskId} is not in pending state (current: {State})", taskId, task.State);
            return false;
        }

        task.State = EncodingTaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        // Update job state to encoding if this is the first running task
        if (task.Job.State == EncodingJobState.Queued)
        {
            task.Job.State = EncodingJobState.Encoding;
            task.Job.StartedAt = DateTime.UtcNow;
            task.Job.UpdatedAt = DateTime.UtcNow;
        }

        // Increment node task count
        if (task.AssignedNodeId.HasValue)
        {
            EncoderNode? node = await _queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == task.AssignedNodeId, cancellationToken);

            if (node != null)
            {
                node.CurrentTaskCount++;
                node.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _queueContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started task {TaskId} ({TaskType})", taskId, task.TaskType);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> CompleteTaskAsync(
        Ulid taskId,
        string? outputFile = null,
        CancellationToken cancellationToken = default)
    {
        EncodingTask? task = await _queueContext.EncodingTasks
            .Include(t => t.Job)
            .ThenInclude(j => j.Tasks)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for completion", taskId);
            return false;
        }

        task.State = EncodingTaskState.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.OutputFile = outputFile;
        task.UpdatedAt = DateTime.UtcNow;

        // Decrement node task count
        if (task.AssignedNodeId.HasValue)
        {
            EncoderNode? node = await _queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == task.AssignedNodeId, cancellationToken);

            if (node != null && node.CurrentTaskCount > 0)
            {
                node.CurrentTaskCount--;
                node.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Check if all tasks are completed
        List<EncodingTask> allTasks = task.Job.Tasks.ToList();
        bool allCompleted = allTasks.All(t => t.State == EncodingTaskState.Completed);
        bool anyFailed = allTasks.Any(t => t.State == EncodingTaskState.Failed);

        if (allCompleted)
        {
            task.Job.State = EncodingJobState.Completed;
            task.Job.CompletedAt = DateTime.UtcNow;
            task.Job.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Job {JobId} completed successfully", task.Job.Id);
        }
        else if (anyFailed)
        {
            // Check if remaining tasks can still complete
            bool remainingCanComplete = allTasks
                .Where(t => t.State == EncodingTaskState.Pending || t.State == EncodingTaskState.Running)
                .Any();

            if (!remainingCanComplete)
            {
                task.Job.State = EncodingJobState.Failed;
                task.Job.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _queueContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed task {TaskId} ({TaskType})", taskId, task.TaskType);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> FailTaskAsync(
        Ulid taskId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        EncodingTask? task = await _queueContext.EncodingTasks
            .Include(t => t.Job)
            .ThenInclude(j => j.Tasks)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for failure", taskId);
            return false;
        }

        task.State = EncodingTaskState.Failed;
        task.ErrorMessage = errorMessage;
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        // Decrement node task count
        if (task.AssignedNodeId.HasValue)
        {
            EncoderNode? node = await _queueContext.EncoderNodes
                .FirstOrDefaultAsync(n => n.Id == task.AssignedNodeId, cancellationToken);

            if (node != null && node.CurrentTaskCount > 0)
            {
                node.CurrentTaskCount--;
                node.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Update job state to failed if task exceeded retries or is critical
        List<EncodingTask> allTasks = task.Job.Tasks.ToList();
        int failedCount = allTasks.Count(t => t.State == EncodingTaskState.Failed);
        int pendingCount = allTasks.Count(t => t.State == EncodingTaskState.Pending);
        int runningCount = allTasks.Count(t => t.State == EncodingTaskState.Running);

        // Job fails if no tasks are pending/running and some have failed
        if (pendingCount == 0 && runningCount == 0 && failedCount > 0)
        {
            task.Job.State = EncodingJobState.Failed;
            task.Job.ErrorMessage = $"{failedCount} task(s) failed";
            task.Job.UpdatedAt = DateTime.UtcNow;
        }

        await _queueContext.SaveChangesAsync(cancellationToken);

        _logger.LogError("Task {TaskId} ({TaskType}) failed: {Error}", taskId, task.TaskType, errorMessage);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RecordProgressAsync(
        Ulid taskId,
        EncodingProgress progress,
        CancellationToken cancellationToken = default)
    {
        EncodingTask? task = await _queueContext.EncodingTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return false;
        }

        progress.Id = Ulid.NewUlid();
        progress.TaskId = taskId;
        progress.RecordedAt = DateTime.UtcNow;

        await _queueContext.EncodingProgress.AddAsync(progress, cancellationToken);
        await _queueContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> ReassignTasksFromNodeAsync(
        Ulid nodeId,
        CancellationToken cancellationToken = default)
    {
        // Find all running or pending tasks assigned to the failing node
        List<EncodingTask> tasksToReassign = await _queueContext.EncodingTasks
            .Where(t => t.AssignedNodeId == nodeId &&
                        (t.State == EncodingTaskState.Running || t.State == EncodingTaskState.Pending))
            .ToListAsync(cancellationToken);

        if (tasksToReassign.Count == 0)
        {
            return 0;
        }

        foreach (EncodingTask task in tasksToReassign)
        {
            // Reset task to pending state
            task.State = EncodingTaskState.Pending;
            task.AssignedNodeId = null;
            task.StartedAt = null;
            task.UpdatedAt = DateTime.UtcNow;

            // Increment retry count for tasks that were running
            if (task.State == EncodingTaskState.Running)
            {
                task.RetryCount++;
            }
        }

        // Reset the node's task count
        EncoderNode? node = await _queueContext.EncoderNodes
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

        if (node != null)
        {
            node.CurrentTaskCount = 0;
            node.IsHealthy = false;
            node.UpdatedAt = DateTime.UtcNow;
        }

        await _queueContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Reassigned {Count} tasks from unhealthy node {NodeId}",
            tasksToReassign.Count,
            nodeId);

        return tasksToReassign.Count;
    }

    /// <summary>
    /// Check if all dependencies for a task have been completed
    /// </summary>
    private async Task<bool> AreDependenciesMetAsync(
        EncodingTask task,
        CancellationToken cancellationToken)
    {
        string[] dependencies = task.Dependencies;

        if (dependencies.Length == 0)
        {
            return true;
        }

        // Check all dependencies are completed
        int completedCount = await _queueContext.EncodingTasks
            .Where(t => t.JobId == task.JobId &&
                        dependencies.Contains(t.Id.ToString()) &&
                        t.State == EncodingTaskState.Completed)
            .CountAsync(cancellationToken);

        return completedCount == dependencies.Length;
    }
}
