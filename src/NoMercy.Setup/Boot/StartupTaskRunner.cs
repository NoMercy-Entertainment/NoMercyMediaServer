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

using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup.Boot;

public class StartupTaskRunner
{
    private readonly List<StartupTask> _tasks;
    private readonly HashSet<string> _completedTasks = [];
    private readonly List<StartupTask> _deferredTasks = [];

    public IReadOnlyList<StartupTask> DeferredTasks => _deferredTasks;
    public IReadOnlySet<string> CompletedTasks => _completedTasks;

    public StartupTaskRunner(List<StartupTask> tasks)
    {
        _tasks = tasks;
        ValidateDependencies();
    }

    public StartupTaskRunner(List<StartupTask> tasks, IEnumerable<string> alreadyCompleted)
    {
        _tasks = tasks;
        foreach (string name in alreadyCompleted)
            _completedTasks.Add(item: name);
        ValidateDependencies();
    }

    private void ValidateDependencies()
    {
        HashSet<string> taskNames = _tasks.Select(selector: t => t.Name).ToHashSet();
        taskNames.UnionWith(other: _completedTasks);

        foreach (StartupTask task in _tasks)
        {
            if (task.DependsOn is null)
                continue;

            foreach (string dep in task.DependsOn)
            {
                if (!taskNames.Contains(item: dep))
                {
                    throw new InvalidOperationException(
                        message: $"Startup task '{task.Name}' depends on '{dep}' which does not exist"
                    );
                }
            }
        }

        // Check for circular dependencies
        HashSet<string> visited = [];
        HashSet<string> inStack = [];

        foreach (StartupTask task in _tasks)
        {
            if (HasCycle(taskName: task.Name, visited: visited, inStack: inStack))
            {
                throw new InvalidOperationException(
                    message: $"Circular dependency detected involving task '{task.Name}'"
                );
            }
        }
    }

    private bool HasCycle(string taskName, HashSet<string> visited, HashSet<string> inStack)
    {
        if (inStack.Contains(item: taskName))
            return true;
        if (visited.Contains(item: taskName))
            return false;

        visited.Add(item: taskName);
        inStack.Add(item: taskName);

        StartupTask? task = _tasks.FirstOrDefault(predicate: t => t.Name == taskName);
        if (task?.DependsOn is not null)
        {
            foreach (string dep in task.DependsOn)
            {
                if (HasCycle(taskName: dep, visited: visited, inStack: inStack))
                    return true;
            }
        }

        inStack.Remove(item: taskName);
        return false;
    }

    public async Task RunAll()
    {
        IEnumerable<IGrouping<int, StartupTask>> phases = _tasks
            .GroupBy(keySelector: t => t.Phase)
            .OrderBy(keySelector: g => g.Key);

        foreach (IGrouping<int, StartupTask> phase in phases)
        {
            List<StartupTask> phaseTasks = phase.ToList();

            foreach (StartupTask task in phaseTasks)
            {
                if (!AreDependenciesMet(task: task))
                {
                    if (task.CanDefer)
                    {
                        Logger.Setup(
                            message: $"Startup task '{task.Name}' deferred — will retry in background"
                        );
                        _deferredTasks.Add(item: task);
                        continue;
                    }

                    throw new InvalidOperationException(
                        message: $"Required startup task '{task.Name}' cannot run — "
                                 + $"dependencies not met: {string.Join(separator: ", ", values: GetUnmetDependencies(task: task))}"
                    );
                }

                try
                {
                    await task.Action.Invoke();
                    _completedTasks.Add(item: task.Name);
                }
                catch (Exception ex) when (task.CanDefer)
                {
                    Logger.Setup(
                        message: $"Startup task '{task.Name}' not ready: {ex.Message} — will retry in background"
                    );
                    _deferredTasks.Add(item: task);
                }
                catch (Exception ex) when (!task.CanDefer)
                {
                    Logger.Setup(
                        message: $"Required startup task '{task.Name}' failed: {ex.Message}",
                        level: LogEventLevel.Fatal
                    );
                    throw;
                }
            }
        }
    }

    internal bool AreDependenciesMet(StartupTask task)
    {
        if (task.DependsOn is null)
            return true;
        return task.DependsOn.All(predicate: dep => _completedTasks.Contains(item: dep));
    }

    // Internal (not private): the null-DependsOn guard below is unreachable via RunAll()
    // (AreDependenciesMet already returns true for a null DependsOn, so this method is
    // only ever invoked once dependencies are known non-null) — exposed so
    // NoMercy.Tests.Setup can still lock the guard's behavior directly.
    internal IEnumerable<string> GetUnmetDependencies(StartupTask task)
    {
        if (task.DependsOn is null)
            return [];
        return task.DependsOn.Where(predicate: dep => !_completedTasks.Contains(item: dep));
    }
}
