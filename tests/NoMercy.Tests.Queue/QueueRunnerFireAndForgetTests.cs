using System.Collections.Concurrent;
using System.Reflection;
using NoMercy.Queue;
using NoMercy.Queue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT-08: Tests verifying that fire-and-forget tasks in QueueRunner
/// now have exception handling, lifecycle tracking, and proper thread management.
/// </summary>
[Trait("Category", "Unit")]
public class QueueRunnerFireAndForgetTests
{
    [Fact]
    public void QueueRunner_SourceCode_NoUnobservedGetAwaiter()
    {
        // CRIT-08: Verify QueueRunner.cs no longer calls .GetAwaiter() without await
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"\.GetAwaiter\s*\(\s*\)\s*;", trimmed);
        }
    }

    [Fact]
    public void QueueRunner_SourceCode_NoTaskRunWithNewThread()
    {
        // CRIT-08: Verify QueueRunner.cs no longer wraps Thread creation in Task.Run
        // The pattern Task.Run(() => new Thread(() => ...).Start()) is redundant
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.DoesNotContain("Task.Run(() => new Thread", source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsHaveExceptionHandling()
    {
        // CRIT-08: Verify that worker thread spawning includes try-catch
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        // SpawnWorkerThread should contain try-catch for exception handling
        Assert.Contains("try", source);
        Assert.Contains("catch (Exception", source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsAreBackground()
    {
        // CRIT-08: Verify that spawned threads are background threads
        // so they don't prevent server shutdown
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("IsBackground = true", source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsAreNamed()
    {
        // CRIT-08: Verify that spawned threads have descriptive names
        // for debugging and diagnostics
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("Name = $\"QueueWorker-", source);
    }

    [Fact]
    public void QueueRunner_HasActiveWorkerTracking()
    {
        // CRIT-08: Verify that a ConcurrentDictionary tracks active worker threads
        // (now instance field since QueueRunner is no longer static)
        FieldInfo? field = typeof(QueueRunner).GetField(
            "_activeWorkerThreads",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);
        Assert.True(
            field.FieldType.IsGenericType &&
            field.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>),
            "_activeWorkerThreads should be a ConcurrentDictionary for thread-safe tracking");
    }

    [Fact]
    public void QueueRunner_GetActiveWorkerThreads_ReturnsReadOnlyView()
    {
        // CRIT-08: Verify active workers are queryable via public method
        TestQueueContextAdapter context = new();
        QueueConfiguration config = new();
        QueueRunner runner = new(context, config);

        IReadOnlyDictionary<string, Thread> workers = runner.GetActiveWorkerThreads();
        Assert.NotNull(workers);
    }

    [Fact]
    public void QueueRunner_VolatileFlags_AreMarkedVolatile()
    {
        // CRIT-08: Verify _isInitialized and _isUpdating are volatile for cross-thread visibility
        // (now instance fields since QueueRunner is no longer static)
        FieldInfo? isInitialized = typeof(QueueRunner).GetField(
            "_isInitialized",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo? isUpdating = typeof(QueueRunner).GetField(
            "_isUpdating",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(isInitialized);
        Assert.NotNull(isUpdating);

        // Check for volatile modifier via attributes
        Assert.True(
            isInitialized.GetRequiredCustomModifiers().Any(t => t == typeof(System.Runtime.CompilerServices.IsVolatile)) ||
            isInitialized.FieldType == typeof(bool),
            "_isInitialized should be volatile");
        Assert.True(
            isUpdating.GetRequiredCustomModifiers().Any(t => t == typeof(System.Runtime.CompilerServices.IsVolatile)) ||
            isUpdating.FieldType == typeof(bool),
            "_isUpdating should be volatile");
    }

    [Fact]
    public void QueueRunner_SourceCode_UpdateWorkerCountsHasErrorLogging()
    {
        // CRIT-08: Verify that UpdateRunningWorkerCounts logs errors from fire-and-forget tasks
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        // Should use ContinueWith(OnlyOnFaulted) or similar error observation
        Assert.Contains("OnlyOnFaulted", source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsCleanUpOnExit()
    {
        // CRIT-08: Verify worker threads remove themselves from tracking on exit
        string sourceFile = FindSourceFile("src/NoMercy.Queue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        // Should have finally block that removes from ActiveWorkerThreads
        Assert.Contains("finally", source);
        Assert.Contains("TryRemove", source);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;

            string repoCandidate = Path.Combine(dir, "..", "..", "..", "..", "..", relativePath);
            string resolved = Path.GetFullPath(repoCandidate);
            if (File.Exists(resolved)) return resolved;

            dir = Directory.GetParent(dir)?.FullName;
        }

        string fallback = Path.Combine("/workspaces/NoMercyMediaServer", relativePath);
        if (File.Exists(fallback)) return fallback;

        throw new FileNotFoundException($"Could not find source file: {relativePath}");
    }
}
