using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Exception = System.Exception;

namespace NoMercyQueue.Workers;

public class QueueWorker(JobQueue queue, string name = "default", QueueRunner? runner = null, ILogger<QueueWorker>? logger = null)
{
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
                        classInstance.Handle().Wait();

                        queue.DeleteJob(job);
                        _currentJobId = null;
                        OnWorkCompleted(EventArgs.Empty);

                        logger?.LogTrace(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {ClassInstance} processed successfully",
                            name, CurrentIndex, job.Id, classInstance);
                    }
                    else
                    {
                        string typeName = jobWithArguments?.GetType().FullName ?? "null";
                        logger?.LogError(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} deserialized to {TypeName} which does not implement IShouldQueue — rejecting",
                            name, CurrentIndex, job.Id, typeName);

                        queue.FailJob(job, new InvalidOperationException(
                            $"Job payload deserialized to {typeName} which does not implement IShouldQueue"));
                        _currentJobId = null;
                    }
                }
                catch (Exception ex)
                {
                    queue.FailJob(job, ex);

                    _currentJobId = null;

                    logger?.LogError(
                        "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {Payload} failed with error: {Error}",
                        name, CurrentIndex, job.Id, job.Payload, ex);
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
        while (_currentJobId != null) Thread.Sleep(1000);

        Stop();
    }
}
