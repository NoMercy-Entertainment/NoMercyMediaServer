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
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue.MediaServer;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT-04: Tests verifying that .Wait() / .Result deadlock patterns
/// have been removed and replaced with proper synchronous or async alternatives.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class BlockingPatternTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public BlockingPatternTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(context: _adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void JobQueue_ReserveJobQuery_IsSynchronous()
    {
        // CRIT-04: ReserveJobQuery must be a synchronous compiled query (not async)
        // so that .Result is not needed inside the lock-protected ReserveJob method.
        // ReserveJobQuery has been moved to EfQueueContextAdapter.
        FieldInfo? field = typeof(EfQueueContextAdapter).GetField(
            name: "ReserveJobQuery",
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(@object: field);

        Type fieldType = field.FieldType;
        // Should be Func<QueueContext, byte, string, long?, QueueJob?> (synchronous)
        // NOT Func<QueueContext, byte, string, long?, Task<QueueJob?>> (async)
        Assert.True(condition: fieldType.IsGenericType);

        Type[] typeArgs = fieldType.GetGenericArguments();
        Type returnType = typeArgs[^1];

        Assert.False(
            condition: returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>),
            userMessage: "ReserveJobQuery should return QueueJob? directly, not Task<QueueJob?>. "
                         + "Using async compiled query requires .Result which causes deadlocks (CRIT-04)."
        );
    }

    [Fact]
    public void JobQueue_ExistsQuery_IsSynchronous()
    {
        // CRIT-04: ExistsQuery must be a synchronous compiled query (not async)
        // so that .Result is not needed inside the Exists method.
        // ExistsQuery has been moved to EfQueueContextAdapter.
        FieldInfo? field = typeof(EfQueueContextAdapter).GetField(
            name: "ExistsQuery",
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(@object: field);

        Type fieldType = field.FieldType;
        Assert.True(condition: fieldType.IsGenericType);

        Type[] typeArgs = fieldType.GetGenericArguments();
        Type returnType = typeArgs[^1];

        Assert.False(
            condition: returnType == typeof(Task<bool>)
                       || (
                           returnType.IsGenericType
                           && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                       ),
            userMessage: "ExistsQuery should return bool directly, not Task<bool>. "
                         + "Using async compiled query requires .Result which causes deadlocks (CRIT-04)."
        );
    }

    [Fact]
    public void JobQueue_ReserveJob_WorksWithSynchronousQuery()
    {
        // Verify ReserveJob still works correctly after switching from async to sync query.
        QueueJob job = new()
        {
            Queue = "sync-test",
            Payload = "sync-test-payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? reserved = _jobQueue.ReserveJob(name: "sync-test", currentJobId: null);

        Assert.NotNull(@object: reserved);
        Assert.Equal(expected: "sync-test-payload", actual: reserved.Payload);
        Assert.NotNull(value: reserved.ReservedAt);
        Assert.Equal(expected: 1, actual: reserved.Attempts);
    }

    [Fact]
    public void JobQueue_Enqueue_DuplicateCheckWorksSynchronously()
    {
        // Verify that the synchronous ExistsQuery correctly prevents duplicate enqueue.
        QueueJobModel job1 = new()
        {
            Queue = "dup-test",
            Payload = "dup-payload",
            AvailableAt = DateTime.UtcNow,
        };
        QueueJobModel job2 = new()
        {
            Queue = "dup-test",
            Payload = "dup-payload",
            AvailableAt = DateTime.UtcNow,
        };

        _jobQueue.Enqueue(queueJob: job1);
        _jobQueue.Enqueue(queueJob: job2);

        int count = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public void JobQueue_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify JobQueue.cs contains no .Wait() or .Result calls.
        string sourceFile = FindSourceFile(relativePath: "src/NoMercyQueue/JobQueue.cs");
        string source = File.ReadAllText(path: sourceFile);

        // Check for .Result pattern (but exclude comments and string literals)
        string[] lines = source.Split(separator: '\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"\.\s*Result\b", actualString: trimmed);
        }

        // Check for .Wait() pattern
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"\.\s*Wait\s*\(", actualString: trimmed);
        }
    }

    [Fact]
    public void HomeController_SourceCode_NoBlockingWait()
    {
        // Static analysis: Verify HomeController.cs no longer uses Task.Delay().Wait().
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"Task\.Delay\([^)]*\)\s*\.Wait\s*\(", actualString: trimmed);
        }
    }

    [Fact]
    public void MusicPlaybackService_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify MusicPlaybackService.cs has no .Wait() calls.
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Api/Services/Music/MusicPlaybackService.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"\)\s*\.Wait\s*\(", actualString: trimmed);
        }
    }

    [Fact]
    public void VideoPlaybackService_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify VideoPlaybackService.cs has no .Wait() calls.
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Api/Services/Video/VideoPlaybackService.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                continue;

            Assert.DoesNotMatch(expectedRegexPattern: @"\)\s*\.Wait\s*\(", actualString: trimmed);
        }
    }

    [Fact]
    public void HomeController_UsesAsyncDelay()
    {
        // Verify the HomeController now uses await Task.Delay instead of .Wait().
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "await Task.Delay", actualString: source);
    }

    [Fact]
    public void HomeController_HasTimeout()
    {
        // Verify the HomeController polling loop has a timeout to prevent infinite waits.
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "CancelAfter", actualString: source);
    }

    private static string FindSourceFile(string relativePath)
    {
        // Walk up from the test assembly location to find the repo root
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            // Also check if we're in a well-known build output structure
            string repoCandidate = Path.Combine(paths: [dir, "..", "..", "..", "..", "..", relativePath]);
            string resolved = Path.GetFullPath(path: repoCandidate);
            if (File.Exists(path: resolved))
                return resolved;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        // Fallback: try from /workspaces/NoMercyMediaServer
        string fallback = Path.Combine(path1: "/workspaces/NoMercyMediaServer", path2: relativePath);
        if (File.Exists(path: fallback))
            return fallback;

        throw new FileNotFoundException(message: $"Could not find source file: {relativePath}");
    }
}
