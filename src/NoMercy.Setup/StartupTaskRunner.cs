using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup;

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
            _completedTasks.Add(name);
        ValidateDependencies();
    }

    private void ValidateDependencies()
    {
        HashSet<string> taskNames = _tasks.Select(t => t.Name).ToHashSet();
        taskNames.UnionWith(_completedTasks);

        foreach (StartupTask task in _tasks)
        {
            if (task.DependsOn is null) continue;

            foreach (string dep in task.DependsOn)
            {
                if (!taskNames.Contains(dep))
                {
                    throw new InvalidOperationException(
                        $"Startup task '{task.Name}' depends on '{dep}' which does not exist");
                }
            }
        }

        // Check for circular dependencies
        HashSet<string> visited = [];
        HashSet<string> inStack = [];

        foreach (StartupTask task in _tasks)
        {
            if (HasCycle(task.Name, visited, inStack))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving task '{task.Name}'");
            }
        }
    }

    private bool HasCycle(string taskName, HashSet<string> visited, HashSet<string> inStack)
    {
        if (inStack.Contains(taskName)) return true;
        if (visited.Contains(taskName)) return false;

        visited.Add(taskName);
        inStack.Add(taskName);

        StartupTask? task = _tasks.FirstOrDefault(t => t.Name == taskName);
        if (task?.DependsOn is not null)
        {
            foreach (string dep in task.DependsOn)
            {
                if (HasCycle(dep, visited, inStack)) return true;
            }
        }

        inStack.Remove(taskName);
        return false;
    }

    public async Task RunAll()
    {
        IEnumerable<IGrouping<int, StartupTask>> phases = _tasks
            .GroupBy(t => t.Phase)
            .OrderBy(g => g.Key);

        foreach (IGrouping<int, StartupTask> phase in phases)
        {
            List<StartupTask> phaseTasks = phase.ToList();

            foreach (StartupTask task in phaseTasks)
            {
                if (!AreDependenciesMet(task))
                {
                    if (task.CanDefer)
                    {
                        Logger.Setup(
                            $"Startup task '{task.Name}' deferred — dependencies not met",
                            LogEventLevel.Warning);
                        _deferredTasks.Add(task);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Required startup task '{task.Name}' cannot run — " +
                        $"dependencies not met: {string.Join(", ", GetUnmetDependencies(task))}");
                }

                try
                {
                    await task.Action.Invoke();
                    _completedTasks.Add(task.Name);
                }
                catch (Exception ex) when (task.CanDefer)
                {
                    Logger.Setup(
                        $"Startup task '{task.Name}' failed: {ex.Message}. Deferring to background.",
                        LogEventLevel.Warning);
                    _deferredTasks.Add(task);
                }
                catch (Exception ex) when (!task.CanDefer)
                {
                    Logger.Setup(
                        $"Required startup task '{task.Name}' failed: {ex.Message}",
                        LogEventLevel.Fatal);
                    throw;
                }
            }
        }
    }

    internal bool AreDependenciesMet(StartupTask task)
    {
        if (task.DependsOn is null) return true;
        return task.DependsOn.All(dep => _completedTasks.Contains(dep));
    }

    private IEnumerable<string> GetUnmetDependencies(StartupTask task)
    {
        if (task.DependsOn is null) return [];
        return task.DependsOn.Where(dep => !_completedTasks.Contains(dep));
    }
}
