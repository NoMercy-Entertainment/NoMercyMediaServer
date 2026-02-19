using System.Collections.Concurrent;
using NoMercy.Setup;
using Xunit;

namespace NoMercy.Tests.Setup;

public class StartupTaskRunnerTests
{
    [Fact]
    public async Task RunAll_ExecutesTasksInPhaseOrder()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new("Phase2Task", () => { executionOrder.Add("Phase2Task"); return Task.CompletedTask; },
                CanDefer: false, Phase: 2),
            new("Phase1Task", () => { executionOrder.Add("Phase1Task"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1),
            new("Phase3Task", () => { executionOrder.Add("Phase3Task"); return Task.CompletedTask; },
                CanDefer: false, Phase: 3),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Phase1Task", executionOrder[0]);
        Assert.Equal("Phase2Task", executionOrder[1]);
        Assert.Equal("Phase3Task", executionOrder[2]);
    }

    [Fact]
    public async Task RunAll_DefersFailedDeferrableTask()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new("Required", () => { executionOrder.Add("Required"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1),
            new("Deferrable", () => throw new InvalidOperationException("Network error"),
                CanDefer: true, Phase: 2),
            new("AfterDeferred", () => { executionOrder.Add("AfterDeferred"); return Task.CompletedTask; },
                CanDefer: false, Phase: 3),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Contains("Required", executionOrder);
        Assert.Contains("AfterDeferred", executionOrder);
        Assert.Single(runner.DeferredTasks);
        Assert.Equal("Deferrable", runner.DeferredTasks[0].Name);
    }

    [Fact]
    public async Task RunAll_ThrowsOnRequiredTaskFailure()
    {
        List<StartupTask> tasks =
        [
            new("Required", () => throw new InvalidOperationException("Fatal error"),
                CanDefer: false, Phase: 1),
        ];

        StartupTaskRunner runner = new(tasks);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAll());
    }

    [Fact]
    public async Task RunAll_DefersTaskWithUnmetDependencies()
    {
        List<StartupTask> tasks =
        [
            new("Auth", () => throw new InvalidOperationException("No network"),
                CanDefer: true, Phase: 1),
            new("Register", () => Task.CompletedTask,
                CanDefer: true, Phase: 2, DependsOn: ["Auth"]),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Equal(2, runner.DeferredTasks.Count);
        Assert.Contains(runner.DeferredTasks, t => t.Name == "Auth");
        Assert.Contains(runner.DeferredTasks, t => t.Name == "Register");
    }

    [Fact]
    public async Task RunAll_ExecutesDependentTaskAfterDependency()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new("First", () => { executionOrder.Add("First"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1),
            new("Second", () => { executionOrder.Add("Second"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1, DependsOn: ["First"]),
            new("Third", () => { executionOrder.Add("Third"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1, DependsOn: ["Second"]),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Equal(3, executionOrder.Count);
        Assert.True(executionOrder.IndexOf("First") < executionOrder.IndexOf("Second"));
        Assert.True(executionOrder.IndexOf("Second") < executionOrder.IndexOf("Third"));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidDependency()
    {
        List<StartupTask> tasks =
        [
            new("Task1", () => Task.CompletedTask, CanDefer: false, Phase: 1,
                DependsOn: ["NonExistent"]),
        ];

        Assert.Throws<InvalidOperationException>(() => new StartupTaskRunner(tasks));
    }

    [Fact]
    public void Constructor_ThrowsOnCircularDependency()
    {
        List<StartupTask> tasks =
        [
            new("A", () => Task.CompletedTask, CanDefer: false, Phase: 1, DependsOn: ["B"]),
            new("B", () => Task.CompletedTask, CanDefer: false, Phase: 1, DependsOn: ["A"]),
        ];

        Assert.Throws<InvalidOperationException>(() => new StartupTaskRunner(tasks));
    }

    [Fact]
    public async Task RunAll_TracksCompletedTasks()
    {
        List<StartupTask> tasks =
        [
            new("Task1", () => Task.CompletedTask, CanDefer: false, Phase: 1),
            new("Task2", () => Task.CompletedTask, CanDefer: false, Phase: 2),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Contains("Task1", runner.CompletedTasks);
        Assert.Contains("Task2", runner.CompletedTasks);
        Assert.Equal(2, runner.CompletedTasks.Count);
    }

    [Fact]
    public async Task RunAll_RequiredTaskWithUnmetDeps_Throws()
    {
        List<StartupTask> tasks =
        [
            new("Auth", () => throw new InvalidOperationException("Fail"),
                CanDefer: true, Phase: 1),
            new("Critical", () => Task.CompletedTask,
                CanDefer: false, Phase: 2, DependsOn: ["Auth"]),
        ];

        StartupTaskRunner runner = new(tasks);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAll());
    }

    [Fact]
    public async Task RunAll_EmptyTaskList_Succeeds()
    {
        List<StartupTask> tasks = [];
        StartupTaskRunner runner = new(tasks);

        await runner.RunAll();

        Assert.Empty(runner.CompletedTasks);
        Assert.Empty(runner.DeferredTasks);
    }

    [Fact]
    public async Task RunAll_MultiplePhasesWithDependencies_CorrectOrder()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new("AppFolders", () => { executionOrder.Add("AppFolders"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1),
            new("ApiInfo", () => { executionOrder.Add("ApiInfo"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1, DependsOn: ["AppFolders"]),
            new("NetworkProbe", () => { executionOrder.Add("NetworkProbe"); return Task.CompletedTask; },
                CanDefer: false, Phase: 2, DependsOn: ["ApiInfo"]),
            new("Auth", () => { executionOrder.Add("Auth"); return Task.CompletedTask; },
                CanDefer: true, Phase: 2, DependsOn: ["NetworkProbe"]),
            new("Networking", () => { executionOrder.Add("Networking"); return Task.CompletedTask; },
                CanDefer: true, Phase: 3, DependsOn: ["NetworkProbe"]),
            new("Register", () => { executionOrder.Add("Register"); return Task.CompletedTask; },
                CanDefer: true, Phase: 4, DependsOn: ["Auth", "Networking"]),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        Assert.Equal(6, executionOrder.Count);
        Assert.True(executionOrder.IndexOf("AppFolders") < executionOrder.IndexOf("ApiInfo"));
        Assert.True(executionOrder.IndexOf("ApiInfo") < executionOrder.IndexOf("NetworkProbe"));
        Assert.True(executionOrder.IndexOf("NetworkProbe") < executionOrder.IndexOf("Auth"));
        Assert.True(executionOrder.IndexOf("Auth") < executionOrder.IndexOf("Register"));
        Assert.True(executionOrder.IndexOf("Networking") < executionOrder.IndexOf("Register"));
    }

    [Fact]
    public async Task RunAll_DegradedMode_DefersAuthAndDownstreamTasks()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new("AppFolders", () => { executionOrder.Add("AppFolders"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1),
            new("ApiInfo", () => { executionOrder.Add("ApiInfo"); return Task.CompletedTask; },
                CanDefer: false, Phase: 1, DependsOn: ["AppFolders"]),
            new("NetworkProbe", () => { executionOrder.Add("NetworkProbe"); return Task.CompletedTask; },
                CanDefer: false, Phase: 2, DependsOn: ["ApiInfo"]),
            new("Auth", () => throw new InvalidOperationException("No network"),
                CanDefer: true, Phase: 2, DependsOn: ["NetworkProbe"]),
            new("CallerTask_0", () => { executionOrder.Add("CallerTask_0"); return Task.CompletedTask; },
                CanDefer: true, Phase: 3, DependsOn: ["Auth"]),
            new("Register", () => { executionOrder.Add("Register"); return Task.CompletedTask; },
                CanDefer: true, Phase: 4, DependsOn: ["Auth", "Networking"]),
            new("Networking", () => { executionOrder.Add("Networking"); return Task.CompletedTask; },
                CanDefer: true, Phase: 3, DependsOn: ["NetworkProbe"]),
        ];

        StartupTaskRunner runner = new(tasks);
        await runner.RunAll();

        // Required tasks completed
        Assert.Contains("AppFolders", runner.CompletedTasks);
        Assert.Contains("ApiInfo", runner.CompletedTasks);
        Assert.Contains("NetworkProbe", runner.CompletedTasks);
        Assert.Contains("Networking", runner.CompletedTasks);

        // Auth failed → deferred, along with downstream
        Assert.Contains(runner.DeferredTasks, t => t.Name == "Auth");
        Assert.Contains(runner.DeferredTasks, t => t.Name == "CallerTask_0");
        Assert.Contains(runner.DeferredTasks, t => t.Name == "Register");
    }

    [Fact]
    public void AreDependenciesMet_ReturnsTrueForNoDependencies()
    {
        List<StartupTask> tasks =
        [
            new("NoDeps", () => Task.CompletedTask, CanDefer: false, Phase: 1),
        ];

        StartupTaskRunner runner = new(tasks);
        Assert.True(runner.AreDependenciesMet(tasks[0]));
    }

    [Fact]
    public void AreDependenciesMet_ReturnsFalseWhenDepsNotCompleted()
    {
        List<StartupTask> tasks =
        [
            new("Dep", () => Task.CompletedTask, CanDefer: false, Phase: 1),
            new("Dependent", () => Task.CompletedTask, CanDefer: false, Phase: 1, DependsOn: ["Dep"]),
        ];

        StartupTaskRunner runner = new(tasks);
        Assert.False(runner.AreDependenciesMet(tasks[1]));
    }
}

public class StartupTaskRecordTests
{
    [Fact]
    public void StartupTask_DefaultDependsOnIsNull()
    {
        StartupTask task = new("Test", () => Task.CompletedTask, CanDefer: false, Phase: 1);

        Assert.Null(task.DependsOn);
    }

    [Fact]
    public void StartupTask_StoresAllProperties()
    {
        Func<Task> action = () => Task.CompletedTask;
        string[] deps = ["Dep1", "Dep2"];

        StartupTask task = new("MyTask", action, CanDefer: true, Phase: 3, DependsOn: deps);

        Assert.Equal("MyTask", task.Name);
        Assert.Same(action, task.Action);
        Assert.True(task.CanDefer);
        Assert.Equal(3, task.Phase);
        Assert.Equal(deps, task.DependsOn);
    }
}

public class BuildStartupTasksTests
{
    [Fact]
    public void BuildStartupTasks_ContainsAllExpectedTasks()
    {
        List<TaskDelegate> callerTasks = [() => Task.CompletedTask];
        List<StartupTask> tasks = Start.BuildStartupTasks(callerTasks);

        string[] expectedNames =
        [
            "UserSettings", "CreateAppFolders", "ApiInfo",
            "NetworkProbe", "Auth", "Binaries",
            "Networking", "ChromeCast", "UpdateChecker",
            "DesktopIcon", "CallerTask_0",
            "Register"
        ];

        foreach (string name in expectedNames)
        {
            Assert.Contains(tasks, t => t.Name == name);
        }
    }

    [Fact]
    public void BuildStartupTasks_Phase1TasksAreNotDeferrable()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([]);

        List<StartupTask> phase1 = tasks.Where(t => t.Phase == 1).ToList();

        Assert.All(phase1, t => Assert.False(t.CanDefer,
            $"Phase 1 task '{t.Name}' should not be deferrable"));
    }

    [Fact]
    public void BuildStartupTasks_RegisterDependsOnAuthAndNetworking()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([]);

        StartupTask register = tasks.Single(t => t.Name == "Register");

        Assert.NotNull(register.DependsOn);
        Assert.Contains("Auth", register.DependsOn);
        Assert.Contains("Networking", register.DependsOn);
    }

    [Fact]
    public void BuildStartupTasks_CallerTasksDependOnAuth()
    {
        List<TaskDelegate> callerTasks =
        [
            () => Task.CompletedTask,
            () => Task.CompletedTask,
        ];

        List<StartupTask> tasks = Start.BuildStartupTasks(callerTasks);

        List<StartupTask> callerStartupTasks = tasks
            .Where(t => t.Name.StartsWith("CallerTask_")).ToList();

        Assert.Equal(2, callerStartupTasks.Count);
        Assert.All(callerStartupTasks, t =>
        {
            Assert.NotNull(t.DependsOn);
            Assert.Contains("Auth", t.DependsOn);
        });
    }

    [Fact]
    public void BuildStartupTasks_NoDuplicateNames()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([]);

        List<string> names = tasks.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void BuildStartupTasks_AllDependenciesExist()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([() => Task.CompletedTask]);
        HashSet<string> taskNames = tasks.Select(t => t.Name).ToHashSet();

        foreach (StartupTask task in tasks)
        {
            if (task.DependsOn is null) continue;
            foreach (string dep in task.DependsOn)
            {
                Assert.Contains(dep, taskNames);
            }
        }
    }

    [Fact]
    public void BuildStartupTasks_PhasesAreOrdered()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([]);

        // Phase 1 tasks should exist
        Assert.Contains(tasks, t => t.Phase == 1);
        // Phase 2 tasks should exist
        Assert.Contains(tasks, t => t.Phase == 2);
        // Phase 3 tasks should exist
        Assert.Contains(tasks, t => t.Phase == 3);
        // Phase 4 tasks should exist
        Assert.Contains(tasks, t => t.Phase == 4);
    }

    [Fact]
    public void BuildStartupTasks_PassesValidation()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks([() => Task.CompletedTask]);

        // Should not throw — validates dependencies and circular references
        StartupTaskRunner runner = new(tasks);

        Assert.NotNull(runner);
    }
}
