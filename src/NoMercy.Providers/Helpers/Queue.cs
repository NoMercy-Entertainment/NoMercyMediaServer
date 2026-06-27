// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Net;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Providers.Helpers;

public class Queue(QueueOptions options)
{
    // Two separate queues: priority drains first. Inside each, FIFO via
    // insertion-ordered Dictionary. Without this split, priority=true was
    // a no-op — Execute() walked keys in insertion order regardless of
    // the randomized uniqueId, so user-facing TMDB calls sat behind
    // background metadata fills.
    private readonly Dictionary<string, Func<Task>> _priorityTasks = [];
    private readonly Dictionary<string, Func<Task>> _tasks = [];

    private int _lastRan = Environment.TickCount;
    private int _currentlyHandled;

    private State _state = State.Idle;
    private QueueOptions Options { get; } = options;
    private SemaphoreSlim Semaphore { get; } = new(options.Concurrent, options.Concurrent);

    private readonly Random _r = new();

    public event EventHandler? Start;
    public event EventHandler? Stop;
    public event EventHandler? End;

    private void StartQueue()
    {
        if (_state == State.Running || IsEmpty)
            return;

        _state = State.Running;
        Start?.Invoke(this, EventArgs.Empty);
        _ = Task.Run(RunTasksAsync, CancellationToken.None);
    }

    private void StopQueue()
    {
        _state = State.Stopped;
        Stop?.Invoke(this, EventArgs.Empty);
    }

    private void Finish()
    {
        _currentlyHandled--;

        if (_currentlyHandled != 0 || !IsEmpty)
            return;

        // StopQueue();
        _state = State.Idle;
        End?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunTasksAsync()
    {
        while (ShouldRun)
            try
            {
                await Dequeue();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.App($"Queue processor error: {ex.Message}", LogEventLevel.Error);
                await Task.Delay(1000);
            }
    }

    private Task Execute()
    {
        lock (_tasks)
        {
            // Priority queue drains first, then the normal queue. Inside each,
            // insertion order = FIFO.
            DrainQueue(_priorityTasks);
            DrainQueue(_tasks);
        }

        return Task.CompletedTask;
    }

    private void DrainQueue(Dictionary<string, Func<Task>> queue)
    {
        List<string> keys = queue.Keys.ToList();
        foreach (string key in keys)
        {
            if (_currentlyHandled >= Options.Concurrent)
                return;

            if (!queue.TryGetValue(key, out Func<Task>? value))
                continue;

            _currentlyHandled++;
            queue.Remove(key);

            try
            {
                value.Invoke();
            }
            catch (Exception)
            {
                // Failures surface to callers via the per-task TaskCompletionSource.
            }
            finally
            {
                Finish();
            }
        }
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

        bool isPriority = priority is true;
        string uniqueId = Ulid.NewUlid().ToString();

        lock (_tasks)
        {
            Dictionary<string, Func<Task>> bucket = isPriority ? _priorityTasks : _tasks;
            while (bucket.ContainsKey(uniqueId))
                uniqueId = Ulid.NewUlid().ToString();

            bucket.Add(
                uniqueId,
                async () =>
                {
                    try
                    {
                        int maxRetries = 3;
                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                T result = await task();
                                tcs.SetResult(result);
                                return;
                            }
                            catch (HttpRequestException ex)
                                when (attempt < maxRetries
                                    && ex.StatusCode
                                        is HttpStatusCode.BadGateway
                                            or HttpStatusCode.ServiceUnavailable
                                            or HttpStatusCode.GatewayTimeout
                                            or HttpStatusCode.TooManyRequests
                                )
                            {
                                int delay = (int)Math.Pow(2, attempt + 1) * 1000;
                                Logger.App(
                                    $"Rate limited {ex.StatusCode} ({url}), retrying in {delay / 1000}s (attempt {attempt + 1}/{maxRetries})",
                                    LogEventLevel.Debug
                                );
                                await Task.Delay(delay);
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                                if (IsExpectedTransport(ex))
                                    return;
                                Logger.App($"Url failed: {url} {ex.Message}", LogEventLevel.Debug);
                                return;
                            }
                        }
                    }
                    finally
                    {
                        Semaphore.Release();
                        lock (_tasks)
                        {
                            _priorityTasks.Remove(uniqueId);
                            _tasks.Remove(uniqueId);
                        }
                    }
                }
            );
        }

        if (Options.Start && _state != State.Stopped)
            StartQueue();

        return await tcs.Task;
    }

    private void Clear()
    {
        lock (_tasks)
        {
            _priorityTasks.Clear();
            _tasks.Clear();
        }
    }

    private int Size
    {
        get
        {
            lock (_tasks)
            {
                return _priorityTasks.Count + _tasks.Count;
            }
        }
    }

    private bool IsEmpty => Size == 0;

    private bool ShouldRun => !IsEmpty && _state != State.Stopped;

    private static bool IsExpectedTransport(Exception ex)
    {
        return ex
            is HttpRequestException
            {
                StatusCode: HttpStatusCode.NotFound
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout
                    or HttpStatusCode.TooManyRequests,
            };
    }
}
