using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Exception = System.Exception;

namespace NoMercyQueue.Workers;

public class QueueWorker(
    JobQueue queue,
    string name = "default",
    QueueRunner? runner = null,
    ILogger<QueueWorker>? logger = null
)
{
    private const int MaxTransientRetries = 5;
    private const int TransientRetryBaseMs = 3000;
    private const int TransientRetryJitterMs = 2000;

    private long? _currentJobId;
    private bool _isRunning = true;

    private int CurrentIndex => runner?.GetWorkerIndex(name, this) ?? -1;

    public event WorkCompletedEventHandler WorkCompleted = delegate { };

    public void Start()
    {
        // Per-worker start not logged — summary in QueueRunner.Initialize()

        Thread.CurrentThread.Priority = ThreadPriority.Lowest;

        while (_isRunning)
        {
            QueueJobModel? job = queue.ReserveJob(name, _currentJobId);

            if (job != null)
            {
                _currentJobId = job.Id;

                try
                {
                    object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);

                    if (jobWithArguments is IShouldQueue classInstance)
                    {
                        ExecuteWithTransientRetry(classInstance, job);

                        queue.DeleteJob(job);
                        _currentJobId = null;
                        OnWorkCompleted(EventArgs.Empty);

                        logger?.LogTrace(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {ClassInstance} processed successfully",
                            name,
                            CurrentIndex,
                            job.Id,
                            classInstance
                        );
                    }
                    else
                    {
                        string typeName = jobWithArguments?.GetType().FullName ?? "null";
                        logger?.LogError(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} deserialized to {TypeName} which does not implement IShouldQueue — rejecting",
                            name,
                            CurrentIndex,
                            job.Id,
                            typeName
                        );

                        queue.FailJob(
                            job,
                            new InvalidOperationException(
                                $"Job payload deserialized to {typeName} which does not implement IShouldQueue"
                            )
                        );
                        _currentJobId = null;
                    }
                }
                catch (Exception ex)
                {
                    queue.FailJob(job, ex);

                    _currentJobId = null;

                    logger?.LogError(
                        "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {Payload} failed with error: {Error}",
                        name,
                        CurrentIndex,
                        job.Id,
                        job.Payload,
                        ex
                    );
                }

                Thread.Sleep(1000);
            }
            else
            {
                OnWorkCompleted(EventArgs.Empty);

                Thread.Sleep(1000);
            }
        }
    }

    protected virtual void OnWorkCompleted(EventArgs e)
    {
        WorkCompleted.Invoke(this, e);
    }

    /// <summary>
    /// Executes a job with transparent retry for transient SQLite errors (SQLITE_BUSY /
    /// "database is locked").  These retries do NOT consume the job's attempt count —
    /// they exist to absorb short-lived write-lock contention that is normal under
    /// concurrent queue workers sharing a single SQLite database.
    /// </summary>
    private void ExecuteWithTransientRetry(IShouldQueue job, QueueJobModel queueJob)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                // GetAwaiter().GetResult() rather than .Wait() so that the
                // original exception propagates unwrapped (not wrapped in
                // AggregateException) — this keeps catch-block handling and
                // retry classification correct.
                job.Handle().GetAwaiter().GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientSqliteError(ex) && attempt < MaxTransientRetries)
            {
                int delay = TransientRetryBaseMs + Random.Shared.Next(TransientRetryJitterMs);

                logger?.LogWarning(
                    "QueueWorker {Name} - {CurrentIndex}: Job {JobId} hit transient SQLite error (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    name,
                    CurrentIndex,
                    queueJob.Id,
                    attempt + 1,
                    MaxTransientRetries,
                    delay
                );

                Thread.Sleep(delay);
            }
        }
    }

    private static bool IsTransientSqliteError(Exception ex)
    {
        // Walk the exception chain looking for SQLite lock contention:
        //   Error 5  (SQLITE_BUSY)   → "database is locked"
        //   Error 6  (SQLITE_LOCKED) → "database table is locked" (shared-cache contention)
        // We check the type name instead of casting because this assembly
        // does not reference Microsoft.Data.Sqlite directly.
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            string typeName = current.GetType().Name;

            if (typeName is "SqliteException" &&
                current.Message.Contains("is locked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Stop()
    {
        logger?.LogInformation("QueueWorker {Name} - {CurrentIndex}: stopped", name, CurrentIndex);
        _isRunning = false;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void StopWhenReady()
    {
        while (_currentJobId != null)
            Thread.Sleep(1000);

        Stop();
    }
}
