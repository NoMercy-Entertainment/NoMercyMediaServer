using System.Reflection;
using NoMercyQueue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// HIGH-16: Tests verifying that worker counter updates in QueueRunner
/// use proper synchronization to prevent race conditions.
/// </summary>
[Trait("Category", "Unit")]
public class WorkerCountRaceConditionTests
{
    [Fact]
    public void QueueRunner_HasWorkersLock()
    {
        // HIGH-16: Verify a dedicated lock object exists for synchronizing Workers access
        FieldInfo? lockField = typeof(QueueRunner).GetField(
            "_workersLock",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(lockField);
        Assert.Equal(typeof(object), lockField.FieldType);
    }

    [Fact]
    public void QueueRunner_SourceCode_SpawnWorkerUsesLock()
    {
        // HIGH-16: SpawnWorker must lock before adding to worker instances list
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        // Extract SpawnWorker method body (not SpawnWorkerThread)
        string spawnWorkerBody = ExtractMethodBody(source, "void SpawnWorker(");

        Assert.Contains("lock (_workersLock)", spawnWorkerBody);
        Assert.Contains("workerInstances.Add(", spawnWorkerBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_QueueWorkerCompletedUsesLock()
    {
        // HIGH-16: QueueWorkerCompleted must lock before removing from worker instances list
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string methodBody = ExtractMethodBody(
            source, "WorkCompletedEventHandler QueueWorkerCompleted(");

        Assert.Contains("lock (_workersLock)", methodBody);
        Assert.Contains("workerInstances.Remove(", methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_UpdateRunningWorkerCountsUsesLock()
    {
        // HIGH-16: UpdateRunningWorkerCounts must lock before reading worker counts
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string methodBody = ExtractMethodBody(source, "UpdateRunningWorkerCounts");

        Assert.Contains("lock (_workersLock)", methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_NoNonAtomicCounterIncrement()
    {
        // HIGH-16: Verify the old pattern of local `i += 1` counter is gone.
        // The worker count should be read atomically from the actual list each iteration.
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string methodBody = ExtractMethodBody(source, "UpdateRunningWorkerCounts");

        // The old non-atomic pattern was: int i = ...; while(i < count) { i += 1; }
        Assert.DoesNotContain("i += 1", methodBody);
        Assert.DoesNotContain("i++", methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_GetWorkerIndexUsesLock()
    {
        // HIGH-16: GetWorkerIndex accesses the worker list and must be synchronized
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string methodBody = ExtractMethodBody(source, "GetWorkerIndex");

        Assert.Contains("lock (_workersLock)", methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_SetWorkerCountUsesLock()
    {
        // HIGH-16: SetWorkerCount modifies the Workers dictionary and must be synchronized
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        string methodBody = ExtractMethodBody(source, "SetWorkerCount");

        Assert.Contains("lock (_workersLock)", methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_StartStopUseLockForSnapshot()
    {
        // HIGH-16: Start/Stop/Restart take snapshots under lock to avoid
        // iterating a list that another thread may modify
        string sourceFile = FindSourceFile("src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(sourceFile);

        // Start method should snapshot under lock
        string startBody = ExtractMethodBody(source, "public Task Start(");
        Assert.Contains("lock (_workersLock)", startBody);

        // Stop method should snapshot under lock
        string stopBody = ExtractMethodBody(source, "public Task Stop(");
        Assert.Contains("lock (_workersLock)", stopBody);
    }

    private static string ExtractMethodBody(string source, string methodSignature)
    {
        int methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
        if (methodStart < 0)
            throw new InvalidOperationException(
                $"Method containing '{methodSignature}' not found in source");

        // Find the opening brace of the method
        int braceStart = source.IndexOf('{', methodStart);
        if (braceStart < 0)
            throw new InvalidOperationException("Opening brace not found");

        // Count braces to find the matching closing brace
        int depth = 0;
        int pos = braceStart;
        while (pos < source.Length)
        {
            if (source[pos] == '{') depth++;
            else if (source[pos] == '}') depth--;

            if (depth == 0) break;
            pos++;
        }

        return source.Substring(braceStart, pos - braceStart + 1);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;

            string repoCandidate = Path.Combine(
                dir, "..", "..", "..", "..", "..", relativePath);
            string resolved = Path.GetFullPath(repoCandidate);
            if (File.Exists(resolved)) return resolved;

            dir = Directory.GetParent(dir)?.FullName;
        }

        string fallback = Path.Combine("/workspaces/NoMercyMediaServer", relativePath);
        if (File.Exists(fallback)) return fallback;

        throw new FileNotFoundException(
            $"Could not find source file: {relativePath}");
    }
}
