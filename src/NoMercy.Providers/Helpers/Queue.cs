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
    private SemaphoreSlim Semaphore { get; } = new(initialCount: options.Concurrent, maxCount: options.Concurrent);

    private readonly Random _r = new();

    public event EventHandler? Start;
    public event EventHandler? Stop;
    public event EventHandler? End;

    private void StartQueue()
    {
        if (_state == State.Running || IsEmpty)
            return;

        _state = State.Running;
        Start?.Invoke(sender: this, e: EventArgs.Empty);
        _ = Task.Run(function: RunTasksAsync, cancellationToken: CancellationToken.None);
    }

    private void StopQueue()
    {
        _state = State.Stopped;
        Stop?.Invoke(sender: this, e: EventArgs.Empty);
    }

    private void Finish()
    {
        _currentlyHandled--;

        if (_currentlyHandled != 0 || !IsEmpty)
            return;

        // StopQueue();
        _state = State.Idle;
        End?.Invoke(sender: this, e: EventArgs.Empty);
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
                Logger.App(message: $"Queue processor error: {ex.Message}", level: LogEventLevel.Error);
                await Task.Delay(millisecondsDelay: 1000);
            }
    }

    private Task Execute()
    {
        lock (_tasks)
        {
            // Priority queue drains first, then the normal queue. Inside each,
            // insertion order = FIFO.
            DrainQueue(queue: _priorityTasks);
            DrainQueue(queue: _tasks);
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

            if (!queue.TryGetValue(key: key, value: out Func<Task>? value))
                continue;

            _currentlyHandled++;
            queue.Remove(key: key);

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
        int interval = Math.Max(val1: 0, val2: Options.Interval - (Environment.TickCount - _lastRan));
        return Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: interval);
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
            while (bucket.ContainsKey(key: uniqueId))
                uniqueId = Ulid.NewUlid().ToString();

            bucket.Add(
                key: uniqueId,
                value: async () =>
                {
                    try
                    {
                        int maxRetries = 3;
                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                T result = await task();
                                tcs.SetResult(result: result);
                                return;
                            }
                            catch (HttpRequestException ex)
                                when (attempt < maxRetries
                                    && ex.StatusCode
                                        is HttpStatusCode.BadGateway
                                            or HttpStatusCode.ServiceUnavailable
                                            or HttpStatusCode.GatewayTimeout
                                            or HttpStatusCode.TooManyRequests
                                            or HttpStatusCode.Forbidden
                                )
                            {
                                int delay = (int)Math.Pow(x: 2, y: attempt + 1) * 1000;
                                Logger.App(
                                    message: $"Rate limited {ex.StatusCode} ({url}), retrying in {delay / 1000}s (attempt {attempt + 1}/{maxRetries})",
                                    level: LogEventLevel.Debug
                                );
                                await Task.Delay(millisecondsDelay: delay);
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(exception: ex);
                                if (IsExpectedTransport(ex: ex))
                                    return;
                                Logger.App(message: $"Url failed: {url} {ex.Message}", level: LogEventLevel.Debug);
                                return;
                            }
                        }
                    }
                    finally
                    {
                        Semaphore.Release();
                        lock (_tasks)
                        {
                            _priorityTasks.Remove(key: uniqueId);
                            _tasks.Remove(key: uniqueId);
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
