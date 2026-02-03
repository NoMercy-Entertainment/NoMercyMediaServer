using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Repositories;

/// <summary>
/// Repository for encoding job CRUD operations
/// Uses QueueContext for unified queue management
/// </summary>
public class JobRepository(QueueContext context) : IJobRepository
{
    public async Task<EncodingJob?> GetJobAsync(string jobId)
    {
        return await context.EncodingJobs
            .Include(j => j.Profile)
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == jobId);
    }

    public async Task<List<EncodingJob>> GetJobsByStateAsync(string state)
    {
        return await context.EncodingJobs
            .Include(j => j.Profile)
            .Include(j => j.Tasks)
            .Where(j => j.State == state)
            .ToListAsync();
    }

    public async Task<List<EncodingJob>> GetActiveJobsAsync()
    {
        return await context.EncodingJobs
            .Include(j => j.Profile)
            .Include(j => j.Tasks)
            .Where(j => j.State == "queued" || j.State == "processing")
            .ToListAsync();
    }

    public async Task<List<EncodingJob>> ListJobsAsync(string? state = null, int limit = 50, int offset = 0)
    {
        IQueryable<EncodingJob> query = context.EncodingJobs
            .Include(j => j.Profile)
            .Include(j => j.Tasks);

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(j => j.State == state);
        }

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetJobCountAsync(string? state = null)
    {
        IQueryable<EncodingJob> query = context.EncodingJobs;

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(j => j.State == state);
        }

        return await query.CountAsync();
    }

    public async Task<EncodingJob> CreateJobAsync(EncodingJob job)
    {
        context.EncodingJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateJobAsync(EncodingJob job)
    {
        context.EncodingJobs.Update(job);
        await context.SaveChangesAsync();
    }

    public async Task DeleteJobAsync(string jobId)
    {
        EncodingJob? job = await context.EncodingJobs.FindAsync(jobId);
        if (job != null)
        {
            context.EncodingJobs.Remove(job);
            await context.SaveChangesAsync();
        }
    }

    public async Task<EncodingTask?> GetTaskAsync(string taskId)
    {
        return await context.EncodingTasks
            .Include(t => t.Job)
            .Include(t => t.AssignedNode)
            .FirstOrDefaultAsync(t => t.Id == taskId);
    }

    public async Task<List<EncodingTask>> GetTasksByJobAsync(string jobId)
    {
        return await context.EncodingTasks
            .Include(t => t.AssignedNode)
            .Where(t => t.JobId == jobId)
            .ToListAsync();
    }

    public async Task<List<EncodingTask>> GetPendingTasksAsync()
    {
        return await context.EncodingTasks
            .Where(t => t.State == "pending")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<EncodingTask> CreateTaskAsync(EncodingTask task)
    {
        context.EncodingTasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    public async Task UpdateTaskAsync(EncodingTask task)
    {
        context.EncodingTasks.Update(task);
        await context.SaveChangesAsync();
    }

    public async Task<EncodingProgress> AddProgressAsync(EncodingProgress progress)
    {
        context.EncodingProgress.Add(progress);
        await context.SaveChangesAsync();
        return progress;
    }

    public async Task<List<EncodingProgress>> GetTaskProgressAsync(string taskId, int limit = 100)
    {
        return await context.EncodingProgress
            .Where(p => p.TaskId == taskId)
            .OrderByDescending(p => p.RecordedAt)
            .Take(limit)
            .ToListAsync();
    }
}
