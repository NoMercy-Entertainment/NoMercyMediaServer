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

using NoMercy.Setup.Boot;

namespace NoMercy.Tests.Setup;

public class StartupTaskRunnerTests
{
    [Fact]
    public async Task RunAll_ExecutesTasksInPhaseOrder()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new(
                Name: "Phase2Task",
                Action: () =>
                {
                    executionOrder.Add(item: "Phase2Task");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 2
            ),
            new(
                Name: "Phase1Task",
                Action: () =>
                {
                    executionOrder.Add(item: "Phase1Task");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                Name: "Phase3Task",
                Action: () =>
                {
                    executionOrder.Add(item: "Phase3Task");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 3
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Equal(expected: 3, actual: executionOrder.Count);
        Assert.Equal(expected: "Phase1Task", actual: executionOrder[index: 0]);
        Assert.Equal(expected: "Phase2Task", actual: executionOrder[index: 1]);
        Assert.Equal(expected: "Phase3Task", actual: executionOrder[index: 2]);
    }

    [Fact]
    public async Task RunAll_DefersFailedDeferrableTask()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new(
                Name: "Required",
                Action: () =>
                {
                    executionOrder.Add(item: "Required");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                Name: "Deferrable",
                Action: () => throw new InvalidOperationException(message: "Network error"),
                CanDefer: true,
                Phase: 2
            ),
            new(
                Name: "AfterDeferred",
                Action: () =>
                {
                    executionOrder.Add(item: "AfterDeferred");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 3
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Contains(expected: "Required", collection: executionOrder);
        Assert.Contains(expected: "AfterDeferred", collection: executionOrder);
        Assert.Single(collection: runner.DeferredTasks);
        Assert.Equal(expected: "Deferrable", actual: runner.DeferredTasks[index: 0].Name);
    }

    [Fact]
    public async Task RunAll_ThrowsOnRequiredTaskFailure()
    {
        List<StartupTask> tasks =
        [
            new(
                Name: "Required",
                Action: () => throw new InvalidOperationException(message: "Fatal error"),
                CanDefer: false,
                Phase: 1
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () => runner.RunAll());
    }

    [Fact]
    public async Task RunAll_DefersTaskWithUnmetDependencies()
    {
        List<StartupTask> tasks =
        [
            new(
                Name: "Auth",
                Action: () => throw new InvalidOperationException(message: "No network"),
                CanDefer: true,
                Phase: 1
            ),
            new(
                Name: "Register",
                Action: () => Task.CompletedTask,
                CanDefer: true,
                Phase: 2,
                DependsOn: ["Auth"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Equal(expected: 2, actual: runner.DeferredTasks.Count);
        Assert.Contains(collection: runner.DeferredTasks, filter: t => t.Name == "Auth");
        Assert.Contains(collection: runner.DeferredTasks, filter: t => t.Name == "Register");
    }

    [Fact]
    public async Task RunAll_ExecutesDependentTaskAfterDependency()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new(
                Name: "First",
                Action: () =>
                {
                    executionOrder.Add(item: "First");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                Name: "Second",
                Action: () =>
                {
                    executionOrder.Add(item: "Second");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["First"]
            ),
            new(
                Name: "Third",
                Action: () =>
                {
                    executionOrder.Add(item: "Third");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["Second"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Equal(expected: 3, actual: executionOrder.Count);
        Assert.True(condition: executionOrder.IndexOf(item: "First") < executionOrder.IndexOf(item: "Second"));
        Assert.True(condition: executionOrder.IndexOf(item: "Second") < executionOrder.IndexOf(item: "Third"));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidDependency()
    {
        List<StartupTask> tasks =
        [
            new(
                Name: "Task1",
                Action: () => Task.CompletedTask,
                CanDefer: false,
                Phase: 1,
                DependsOn: ["NonExistent"]
            ),
        ];

        Assert.Throws<InvalidOperationException>(testCode: () => new StartupTaskRunner(tasks: tasks));
    }

    [Fact]
    public void Constructor_ThrowsOnCircularDependency()
    {
        List<StartupTask> tasks =
        [
            new(Name: "A", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1, DependsOn: ["B"]),
            new(Name: "B", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1, DependsOn: ["A"]),
        ];

        Assert.Throws<InvalidOperationException>(testCode: () => new StartupTaskRunner(tasks: tasks));
    }

    [Fact]
    public async Task RunAll_TracksCompletedTasks()
    {
        List<StartupTask> tasks =
        [
            new(Name: "Task1", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1),
            new(Name: "Task2", Action: () => Task.CompletedTask, CanDefer: false, Phase: 2),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Contains(expected: "Task1", set: runner.CompletedTasks);
        Assert.Contains(expected: "Task2", set: runner.CompletedTasks);
        Assert.Equal(expected: 2, actual: runner.CompletedTasks.Count);
    }

    [Fact]
    public async Task RunAll_RequiredTaskWithUnmetDeps_Throws()
    {
        List<StartupTask> tasks =
        [
            new(
                Name: "Auth",
                Action: () => throw new InvalidOperationException(message: "Fail"),
                CanDefer: true,
                Phase: 1
            ),
            new(
                Name: "Critical",
                Action: () => Task.CompletedTask,
                CanDefer: false,
                Phase: 2,
                DependsOn: ["Auth"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () => runner.RunAll());
    }

    [Fact]
    public async Task RunAll_EmptyTaskList_Succeeds()
    {
        List<StartupTask> tasks = [];
        StartupTaskRunner runner = new(tasks: tasks);

        await runner.RunAll();

        Assert.Empty(collection: runner.CompletedTasks);
        Assert.Empty(collection: runner.DeferredTasks);
    }

    [Fact]
    public async Task RunAll_MultiplePhasesWithDependencies_CorrectOrder()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new(
                Name: "AppFolders",
                Action: () =>
                {
                    executionOrder.Add(item: "AppFolders");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                Name: "ApiInfo",
                Action: () =>
                {
                    executionOrder.Add(item: "ApiInfo");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["AppFolders"]
            ),
            new(
                Name: "NetworkProbe",
                Action: () =>
                {
                    executionOrder.Add(item: "NetworkProbe");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 2,
                DependsOn: ["ApiInfo"]
            ),
            new(
                Name: "Auth",
                Action: () =>
                {
                    executionOrder.Add(item: "Auth");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 2,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                Name: "Networking",
                Action: () =>
                {
                    executionOrder.Add(item: "Networking");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                Name: "Register",
                Action: () =>
                {
                    executionOrder.Add(item: "Register");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 4,
                DependsOn: ["Auth", "Networking"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        Assert.Equal(expected: 6, actual: executionOrder.Count);
        Assert.True(condition: executionOrder.IndexOf(item: "AppFolders") < executionOrder.IndexOf(item: "ApiInfo"));
        Assert.True(condition: executionOrder.IndexOf(item: "ApiInfo") < executionOrder.IndexOf(item: "NetworkProbe"));
        Assert.True(condition: executionOrder.IndexOf(item: "NetworkProbe") < executionOrder.IndexOf(item: "Auth"));
        Assert.True(condition: executionOrder.IndexOf(item: "Auth") < executionOrder.IndexOf(item: "Register"));
        Assert.True(condition: executionOrder.IndexOf(item: "Networking") < executionOrder.IndexOf(item: "Register"));
    }

    [Fact]
    public async Task RunAll_DegradedMode_DefersAuthAndDownstreamTasks()
    {
        List<string> executionOrder = [];

        List<StartupTask> tasks =
        [
            new(
                Name: "AppFolders",
                Action: () =>
                {
                    executionOrder.Add(item: "AppFolders");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                Name: "ApiInfo",
                Action: () =>
                {
                    executionOrder.Add(item: "ApiInfo");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["AppFolders"]
            ),
            new(
                Name: "NetworkProbe",
                Action: () =>
                {
                    executionOrder.Add(item: "NetworkProbe");
                    return Task.CompletedTask;
                },
                CanDefer: false,
                Phase: 2,
                DependsOn: ["ApiInfo"]
            ),
            new(
                Name: "Auth",
                Action: () => throw new InvalidOperationException(message: "No network"),
                CanDefer: true,
                Phase: 2,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                Name: "Seeds",
                Action: () =>
                {
                    executionOrder.Add(item: "Seeds");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["Auth"]
            ),
            new(
                Name: "Register",
                Action: () =>
                {
                    executionOrder.Add(item: "Register");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 4,
                DependsOn: ["Auth", "Networking"]
            ),
            new(
                Name: "Networking",
                Action: () =>
                {
                    executionOrder.Add(item: "Networking");
                    return Task.CompletedTask;
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        await runner.RunAll();

        // Required tasks completed
        Assert.Contains(expected: "AppFolders", set: runner.CompletedTasks);
        Assert.Contains(expected: "ApiInfo", set: runner.CompletedTasks);
        Assert.Contains(expected: "NetworkProbe", set: runner.CompletedTasks);
        Assert.Contains(expected: "Networking", set: runner.CompletedTasks);

        // Auth failed → deferred, along with downstream
        Assert.Contains(collection: runner.DeferredTasks, filter: t => t.Name == "Auth");
        Assert.Contains(collection: runner.DeferredTasks, filter: t => t.Name == "Seeds");
        Assert.Contains(collection: runner.DeferredTasks, filter: t => t.Name == "Register");
    }

    [Fact]
    public void AreDependenciesMet_ReturnsTrueForNoDependencies()
    {
        List<StartupTask> tasks =
        [
            new(Name: "NoDeps", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        Assert.True(condition: runner.AreDependenciesMet(task: tasks[index: 0]));
    }

    [Fact]
    public void AreDependenciesMet_ReturnsFalseWhenDepsNotCompleted()
    {
        List<StartupTask> tasks =
        [
            new(Name: "Dep", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1),
            new(
                Name: "Dependent",
                Action: () => Task.CompletedTask,
                CanDefer: false,
                Phase: 1,
                DependsOn: ["Dep"]
            ),
        ];

        StartupTaskRunner runner = new(tasks: tasks);
        Assert.False(condition: runner.AreDependenciesMet(task: tasks[index: 1]));
    }
}

public class StartupTaskRecordTests
{
    [Fact]
    public void StartupTask_DefaultDependsOnIsNull()
    {
        StartupTask task = new(Name: "Test", Action: () => Task.CompletedTask, CanDefer: false, Phase: 1);

        Assert.Null(@object: task.DependsOn);
    }

    [Fact]
    public void StartupTask_StoresAllProperties()
    {
        Func<Task> action = () => Task.CompletedTask;
        string[] deps = ["Dep1", "Dep2"];

        StartupTask task = new(Name: "MyTask", Action: action, CanDefer: true, Phase: 3, DependsOn: deps);

        Assert.Equal(expected: "MyTask", actual: task.Name);
        Assert.Same(expected: action, actual: task.Action);
        Assert.True(condition: task.CanDefer);
        Assert.Equal(expected: 3, actual: task.Phase);
        Assert.Equal(expected: deps, actual: task.DependsOn);
    }
}

public class BuildStartupTasksTests
{
    [Fact]
    public void BuildStartupTasks_ContainsAllExpectedTasks()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();

        // Auth and API-key loading are now handled by BootOrchestrator
        // (IApiKeyLoader.LoadKeys in Phase 1) — neither is a startup task anymore.
        string[] expectedNames =
        [
            "UserSettings",
            "CreateAppFolders",
            "NetworkProbe",
            "Binaries",
            "Networking",
            "ChromeCast",
            "DesktopIcon",
        ];

        foreach (string name in expectedNames)
        {
            Assert.Contains(collection: tasks, filter: t => t.Name == name);
        }

        // Auth and ApiInfo moved to BootOrchestrator — neither should be a startup task.
        Assert.DoesNotContain(collection: tasks, filter: t => t.Name == "Auth");
        Assert.DoesNotContain(collection: tasks, filter: t => t.Name == "ApiInfo");

        // UpdateChecker moved to PeriodicUpdateCheckService (IHostedService) so it can
        // inject IUpdateStatus — it is no longer a static startup task.
        Assert.DoesNotContain(collection: tasks, filter: t => t.Name == "UpdateChecker");
    }

    [Fact]
    public void BuildStartupTasks_Phase1TasksAreNotDeferrable()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();

        List<StartupTask> phase1 = tasks.Where(predicate: t => t.Phase == 1).ToList();

        Assert.All(
            collection: phase1,
            action: t => Assert.False(condition: t.CanDefer, userMessage: $"Phase 1 task '{t.Name}' should not be deferrable")
        );
    }

    [Fact]
    public void BuildStartupTasks_NoDuplicateNames()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();

        List<string> names = tasks.Select(selector: t => t.Name).ToList();
        Assert.Equal(expected: names.Count, actual: names.Distinct().Count());
    }

    [Fact]
    public void BuildStartupTasks_AllDependenciesExist()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();
        HashSet<string> taskNames = tasks.Select(selector: t => t.Name).ToHashSet();

        foreach (StartupTask task in tasks)
        {
            if (task.DependsOn is null)
                continue;
            foreach (string dep in task.DependsOn)
            {
                Assert.Contains(expected: dep, set: taskNames);
            }
        }
    }

    [Fact]
    public void BuildStartupTasks_PhasesAreOrdered()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();

        // Phase 1 tasks should exist
        Assert.Contains(collection: tasks, filter: t => t.Phase == 1);
        // Phase 2 tasks should exist
        Assert.Contains(collection: tasks, filter: t => t.Phase == 2);
        // Phase 3 tasks should exist
        Assert.Contains(collection: tasks, filter: t => t.Phase == 3);
    }

    [Fact]
    public void BuildStartupTasks_PassesValidation()
    {
        List<StartupTask> tasks = Start.BuildStartupTasks();

        // Should not throw — validates dependencies and circular references
        StartupTaskRunner runner = new(tasks: tasks);

        Assert.NotNull(@object: runner);
    }
}
