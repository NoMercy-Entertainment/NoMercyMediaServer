using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Core.Models;
using Serilog.Events;
using Exception = System.Exception;

namespace NoMercy.Queue.Workers;

public class QueueWorker(JobQueue queue, string name = "default", QueueRunner? runner = null)
{
    private long? _currentJobId;
    private bool _isRunning = true;

    private int CurrentIndex => runner?.GetWorkerIndex(name, this) ?? -1;

    public event WorkCompletedEventHandler WorkCompleted = delegate { };

    public void Start()
    {
        Logger.Queue($"QueueWorker {name} - {CurrentIndex}: started");

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

                        Logger.Queue(
                            $"QueueWorker {name} - {CurrentIndex}: Job {job.Id} of Type {classInstance} processed successfully.",
                            LogEventLevel.Verbose);
                    }
                    else
                    {
                        string typeName = jobWithArguments?.GetType().FullName ?? "null";
                        Logger.Queue(
                            $"QueueWorker {name} - {CurrentIndex}: Job {job.Id} deserialized to {typeName} which does not implement IShouldQueue — rejecting.",
                            LogEventLevel.Error);

                        queue.FailJob(job, new InvalidOperationException(
                            $"Job payload deserialized to {typeName} which does not implement IShouldQueue"));
                        _currentJobId = null;
                    }
                }
                catch (Exception ex)
                {
                    queue.FailJob(job, ex);

                    _currentJobId = null;

                    Logger.Queue(
                        $"QueueWorker {name} - {CurrentIndex}: Job {job.Id} of Type {job.Payload} failed with error: {ex}",
                        LogEventLevel.Error);
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
        Logger.Queue($"QueueWorker {name} - {CurrentIndex}: stopped", LogEventLevel.Information);
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
