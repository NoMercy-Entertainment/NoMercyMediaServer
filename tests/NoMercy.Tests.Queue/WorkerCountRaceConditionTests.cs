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

using System.Reflection;
using NoMercyQueue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// HIGH-16: Tests verifying that worker counter updates in QueueRunner
/// use proper synchronization to prevent race conditions.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class WorkerCountRaceConditionTests
{
    [Fact]
    public void QueueRunner_HasWorkersLock()
    {
        // HIGH-16: Verify a dedicated lock object exists for synchronizing Workers access
        FieldInfo? lockField = typeof(QueueRunner).GetField(
            name: "_workersLock",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: lockField);
        Assert.Equal(expected: typeof(object), actual: lockField.FieldType);
    }

    [Fact]
    public void QueueRunner_SourceCode_SpawnWorkerUsesLock()
    {
        // HIGH-16: SpawnWorker must lock before adding to worker instances list
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        // Extract SpawnWorker method body (not SpawnWorkerThread)
        string spawnWorkerBody = ExtractMethodBody(source: source, methodSignature: "void SpawnWorker(");

        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: spawnWorkerBody);
        Assert.Contains(expectedSubstring: "WorkerInstances.Add(", actualString: spawnWorkerBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_QueueWorkerCompletedUsesLock()
    {
        // HIGH-16: QueueWorkerCompleted must lock before removing from worker instances list
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string methodBody = ExtractMethodBody(
            source: source,
            methodSignature: "WorkCompletedEventHandler QueueWorkerCompleted("
        );

        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: methodBody);
        Assert.Contains(expectedSubstring: "WorkerInstances.Remove(", actualString: methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_UpdateRunningWorkerCountsUsesLock()
    {
        // HIGH-16: UpdateRunningWorkerCounts must lock before reading worker counts
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string methodBody = ExtractMethodBody(source: source, methodSignature: "UpdateRunningWorkerCounts");

        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_NoNonAtomicCounterIncrement()
    {
        // HIGH-16: Verify the old pattern of local `i += 1` counter is gone.
        // The worker count should be read atomically from the actual list each iteration.
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string methodBody = ExtractMethodBody(source: source, methodSignature: "UpdateRunningWorkerCounts");

        // The old non-atomic pattern was: int i = ...; while(i < count) { i += 1; }
        Assert.DoesNotContain(expectedSubstring: "i += 1", actualString: methodBody);
        Assert.DoesNotContain(expectedSubstring: "i++", actualString: methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_GetWorkerIndexUsesLock()
    {
        // HIGH-16: GetWorkerIndex accesses the worker list and must be synchronized
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string methodBody = ExtractMethodBody(source: source, methodSignature: "GetWorkerIndex");

        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_SetWorkerCountUsesLock()
    {
        // HIGH-16: SetWorkerCount modifies the Workers dictionary and must be synchronized
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string methodBody = ExtractMethodBody(source: source, methodSignature: "SetWorkerCount");

        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: methodBody);
    }

    [Fact]
    public void QueueRunner_SourceCode_StartStopUseLockForSnapshot()
    {
        // HIGH-16: Start/Stop/Restart take snapshots under lock to avoid
        // iterating a list that another thread may modify
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        // Start method should snapshot under lock
        string startBody = ExtractMethodBody(source: source, methodSignature: "public Task Start(");
        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: startBody);

        // Stop method should snapshot under lock
        string stopBody = ExtractMethodBody(source: source, methodSignature: "public Task Stop(");
        Assert.Contains(expectedSubstring: "lock (_workersLock)", actualString: stopBody);
    }

    private static string ExtractMethodBody(string source, string methodSignature)
    {
        int methodStart = source.IndexOf(value: methodSignature, comparisonType: StringComparison.Ordinal);
        if (methodStart < 0)
            throw new InvalidOperationException(
                message: $"Method containing '{methodSignature}' not found in source"
            );

        // Find the opening brace of the method
        int braceStart = source.IndexOf(value: '{', startIndex: methodStart);
        if (braceStart < 0)
            throw new InvalidOperationException(message: "Opening brace not found");

        // Count braces to find the matching closing brace
        int depth = 0;
        int pos = braceStart;
        while (pos < source.Length)
        {
            if (source[index: pos] == '{')
                depth++;
            else if (source[index: pos] == '}')
                depth--;

            if (depth == 0)
                break;
            pos++;
        }

        return source.Substring(startIndex: braceStart, length: pos - braceStart + 1);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            string repoCandidate = Path.Combine(paths: [dir, "..", "..", "..", "..", "..", relativePath]);
            string resolved = Path.GetFullPath(path: repoCandidate);
            if (File.Exists(path: resolved))
                return resolved;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        string fallback = Path.Combine(path1: "/workspaces/NoMercyMediaServer", path2: relativePath);
        if (File.Exists(path: fallback))
            return fallback;

        throw new FileNotFoundException(message: $"Could not find source file: {relativePath}");
    }
}
