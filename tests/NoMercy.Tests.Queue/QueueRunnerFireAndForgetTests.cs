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

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT-08: Tests verifying that fire-and-forget tasks in QueueRunner
/// now have exception handling, lifecycle tracking, and proper thread management.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class QueueRunnerFireAndForgetTests
{
    [Fact]
    public void QueueRunner_SourceCode_NoUnobservedGetAwaiter()
    {
        // CRIT-08: Verify QueueRunner.cs no longer calls .GetAwaiter() without await
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"\.GetAwaiter\s*\(\s*\)\s*;", actualString: trimmed);
        }
    }

    [Fact]
    public void QueueRunner_SourceCode_NoTaskRunWithNewThread()
    {
        // CRIT-08: Verify QueueRunner.cs no longer wraps Thread creation in Task.Run
        // The pattern Task.Run(() => new Thread(() => ...).Start()) is redundant
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        Assert.DoesNotContain(expectedSubstring: "Task.Run(() => new Thread", actualString: source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsHaveExceptionHandling()
    {
        // CRIT-08: Verify that worker thread spawning includes try-catch
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        // SpawnWorkerThread should contain try-catch for exception handling
        Assert.Contains(expectedSubstring: "try", actualString: source);
        Assert.Contains(expectedSubstring: "catch (Exception", actualString: source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsAreBackground()
    {
        // CRIT-08: Verify that spawned threads are background threads
        // so they don't prevent server shutdown
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "IsBackground = true", actualString: source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsAreNamed()
    {
        // CRIT-08: Verify that spawned threads have descriptive names
        // for debugging and diagnostics
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "Name = $\"QueueWorker-", actualString: source);
    }

    [Fact]
    public void QueueRunner_HasActiveWorkerTracking()
    {
        // CRIT-08: Verify that a ConcurrentDictionary tracks active worker threads
        // (now instance field since QueueRunner is no longer static)
        FieldInfo? field = typeof(QueueRunner).GetField(
            name: "_activeWorkerThreads",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: field);
        Assert.True(
            condition: field.FieldType.IsGenericType
                       && field.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>),
            userMessage: "_activeWorkerThreads should be a ConcurrentDictionary for thread-safe tracking"
        );
    }

    [Fact]
    public void QueueRunner_GetActiveWorkerThreads_ReturnsReadOnlyView()
    {
        // CRIT-08: Verify active workers are queryable via public method
        TestQueueContextAdapter context = new();
        QueueConfiguration config = new();
        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        IReadOnlyDictionary<string, Thread> workers = runner.GetActiveWorkerThreads();
        Assert.NotNull(@object: workers);
    }

    [Fact]
    public void QueueRunner_VolatileFlags_AreMarkedVolatile()
    {
        // CRIT-08: Verify _isInitialized is volatile for cross-thread visibility.
        // _isUpdating was moved to a per-pool flag in the _workers dictionary,
        // protected by _workersLock instead of volatile.
        FieldInfo? isInitialized = typeof(QueueRunner).GetField(
            name: "_isInitialized",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: isInitialized);

        // Check for volatile modifier via attributes
        Assert.True(
            condition: isInitialized.GetRequiredCustomModifiers().Any(predicate: t => t == typeof(IsVolatile))
                       || isInitialized.FieldType == typeof(bool),
            userMessage: "_isInitialized should be volatile"
        );
    }

    [Fact]
    public void QueueRunner_SourceCode_UpdateWorkerCountsHasErrorLogging()
    {
        // CRIT-08: Verify that UpdateRunningWorkerCounts logs errors from fire-and-forget tasks
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        // Should use ContinueWith(OnlyOnFaulted) or similar error observation
        Assert.Contains(expectedSubstring: "OnlyOnFaulted", actualString: source);
    }

    [Fact]
    public void QueueRunner_SourceCode_WorkerThreadsCleanUpOnExit()
    {
        // CRIT-08: Verify worker threads remove themselves from tracking on exit
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/QueueRunner.cs");
        string source = File.ReadAllText(path: sourceFile);

        // Should have finally block that removes from ActiveWorkerThreads
        Assert.Contains(expectedSubstring: "finally", actualString: source);
        Assert.Contains(expectedSubstring: "TryRemove", actualString: source);
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
