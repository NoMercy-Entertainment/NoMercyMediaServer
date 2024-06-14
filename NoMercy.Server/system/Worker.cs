using NoMercy.Helpers;
using Exception = System.Exception;
using LogLevel = NoMercy.Helpers.LogLevel;

namespace NoMercy.Server.system;

public delegate void WorkCompletedEventHandler(object sender, EventArgs e);

public class Worker(JobQueue queue, string name = "default")
{
    private long? _currentJobId;
    private bool _isRunning = true;

    private int CurrentIndex => QueueRunner.GetWorkerIndex(name, this);

    public event WorkCompletedEventHandler WorkCompleted = delegate { };

    public void Start()
    {
        Logger.Queue($"Worker {name} - {CurrentIndex}: started", LogLevel.Debug);

        Thread.CurrentThread.Priority = ThreadPriority.Lowest;

        while (_isRunning)
        {
            var job = queue.ReserveJob(name, _currentJobId);

            if (job != null)
            {
                _currentJobId = job.Id;

                try
                {
                    var jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);

                    if (jobWithArguments is IShouldQueue classInstance)
                    {
                        classInstance.Handle().Wait();

                        queue.DeleteJob(job);
                        _currentJobId = null;
                        OnWorkCompleted(EventArgs.Empty);

                        Logger.Queue(
                            $"Worker {name} - {CurrentIndex}: Job {job.Id} of Type {classInstance} processed successfully.");
                    }
                }
                catch (Exception ex)
                {
                    queue.FailJob(job, ex);

                    _currentJobId = null;

                    Logger.Queue(
                        $"Worker {name} - {CurrentIndex}: Job {job.Id} of Type {job.Payload} failed with error: {ex}",
                        LogLevel.Error);
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
        Logger.Queue($"Worker {name} - {CurrentIndex}: stopped", LogLevel.Info);
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