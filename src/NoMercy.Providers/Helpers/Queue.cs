using System.Collections.Concurrent;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Providers.Helpers;
public class Queue(QueueOptions options)
{
    private readonly Dictionary<string, Func<Task>> _tasks = [];

    private int _lastRan = Environment.TickCount;
    private int _currentlyHandled;

    private State _state = State.Idle;
    private QueueOptions Options { get; } = options;
    private SemaphoreSlim Semaphore { get; } = new(options.Concurrent, options.Concurrent);

    private readonly Random _r = new();

    public event EventHandler<QueueEventArgs>? Resolve;
    public event EventHandler<QueueEventArgs>? Reject;
    public event EventHandler? Start;
    public event EventHandler? Stop;
    public event EventHandler? End;

    private void StartQueue()
    {
        if (_state == State.Running || IsEmpty) return;

        _state = State.Running;
        Start?.Invoke(this, EventArgs.Empty);
        RunTasks();
    }

    private void StopQueue()
    {
        _state = State.Stopped;
        Stop?.Invoke(this, EventArgs.Empty);
    }

    private void Finish()
    {
        _currentlyHandled--;

        if (_currentlyHandled != 0 || !IsEmpty) return;

        // StopQueue();
        _state = State.Idle;
        End?.Invoke(this, EventArgs.Empty);
    }

    private async void RunTasks()
    {
        while (ShouldRun)
            try
            {
                await Dequeue();
            }
            catch (Exception)
            {
                //
            }
    }

    private Task Execute()
    {
        lock (_tasks)
        {
            List<KeyValuePair<string, Func<Task>>> tasks = new ConcurrentDictionary<string, Func<Task>?>(_tasks)
                .Where(_ => _currentlyHandled < Options.Concurrent).ToList();

            foreach ((string? key, Func<Task>? value) in tasks)
            {
                _currentlyHandled++;
                _tasks?.Remove(key);

                try
                {
                    Task result = value.Invoke();
                    Resolve?.Invoke(this, new() { Result = result });
                }
                catch (Exception ex)
                {
                    Reject?.Invoke(this, new() { Error = ex });
                }
                finally
                {
                    Finish();
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task Dequeue()
    {
        int interval = Math.Max(0, Options.Interval - (Environment.TickCount - _lastRan));
        return Task.Run(async () =>
        {
            await Task.Delay(interval);
            _lastRan = Environment.TickCount;
            await Execute();
        });
    }

    public async Task<T> Enqueue<T>(Func<Task<T>> task, string? url, bool? priority = false)
    {
        await Semaphore.WaitAsync();

        TaskCompletionSource<T> tcs = new();

        string? uniqueId = Ulid.NewUlid().ToString();

        if (priority is true) uniqueId = _r.Next(0, int.MaxValue).ToString();

        lock (_tasks)
        {
            while (_tasks.ContainsKey(uniqueId))
            {
                uniqueId = Ulid.NewUlid().ToString();
            }
            
            _tasks.Add(uniqueId, async () =>
            {
                try
                {
                    T result = await task();
                    Resolve?.Invoke(this, new() { Result = result });
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    Reject?.Invoke(this, new() { Error = ex });
                    tcs.SetException(ex);
                    if (ex.Message.Contains("404")) return;
                    Logger.App($"Url failed: {url} {ex.Message}", LogEventLevel.Error);
                }
                finally
                {
                    Semaphore.Release();
                    lock (_tasks)
                    {
                        _tasks.Remove(uniqueId);
                    }
                }
            });
        }

        if (Options.Start && _state != State.Stopped) StartQueue();

        return await tcs.Task;
    }

    private void Clear()
    {
        lock (_tasks)
        {
            _tasks.Clear();
        }
    }

    private int Size
    {
        get
        {
            lock (_tasks)
            {
                return _tasks.Count;
            }
        }
    }

    private bool IsEmpty => Size == 0;

    private bool ShouldRun => !IsEmpty && _state != State.Stopped;
}