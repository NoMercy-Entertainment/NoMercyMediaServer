using System.Reflection;
using System.Text.RegularExpressions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT-04: Tests verifying that .Wait() / .Result deadlock patterns
/// have been removed and replaced with proper synchronous or async alternatives.
/// </summary>
[Trait("Category", "Unit")]
public class BlockingPatternTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly JobQueue _jobQueue;

    public BlockingPatternTests()
    {
        _context = TestQueueContextFactory.CreateInMemoryContext();
        _jobQueue = new(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void JobQueue_ReserveJobQuery_IsSynchronous()
    {
        // CRIT-04: ReserveJobQuery must be a synchronous compiled query (not async)
        // so that .Result is not needed inside the lock-protected ReserveJob method.
        FieldInfo? field = typeof(JobQueue).GetField(
            "ReserveJobQuery",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(field);

        Type fieldType = field.FieldType;
        // Should be Func<QueueContext, byte, string, long?, QueueJob?> (synchronous)
        // NOT Func<QueueContext, byte, string, long?, Task<QueueJob?>> (async)
        Assert.True(fieldType.IsGenericType);

        Type[] typeArgs = fieldType.GetGenericArguments();
        Type returnType = typeArgs[^1];

        Assert.False(
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "ReserveJobQuery should return QueueJob? directly, not Task<QueueJob?>. " +
            "Using async compiled query requires .Result which causes deadlocks (CRIT-04).");
    }

    [Fact]
    public void JobQueue_ExistsQuery_IsSynchronous()
    {
        // CRIT-04: ExistsQuery must be a synchronous compiled query (not async)
        // so that .Result is not needed inside the Exists method.
        FieldInfo? field = typeof(JobQueue).GetField(
            "ExistsQuery",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        Type fieldType = field.FieldType;
        Assert.True(fieldType.IsGenericType);

        Type[] typeArgs = fieldType.GetGenericArguments();
        Type returnType = typeArgs[^1];

        Assert.False(
            returnType == typeof(Task<bool>) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)),
            "ExistsQuery should return bool directly, not Task<bool>. " +
            "Using async compiled query requires .Result which causes deadlocks (CRIT-04).");
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
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJob? reserved = _jobQueue.ReserveJob("sync-test", null);

        Assert.NotNull(reserved);
        Assert.Equal("sync-test-payload", reserved.Payload);
        Assert.NotNull(reserved.ReservedAt);
        Assert.Equal(1, reserved.Attempts);
    }

    [Fact]
    public void JobQueue_Enqueue_DuplicateCheckWorksSynchronously()
    {
        // Verify that the synchronous ExistsQuery correctly prevents duplicate enqueue.
        QueueJob job1 = new()
        {
            Queue = "dup-test",
            Payload = "dup-payload",
            AvailableAt = DateTime.UtcNow
        };
        QueueJob job2 = new()
        {
            Queue = "dup-test",
            Payload = "dup-payload",
            AvailableAt = DateTime.UtcNow
        };

        _jobQueue.Enqueue(job1);
        _jobQueue.Enqueue(job2);

        int count = _context.QueueJobs.Count();
        Assert.Equal(1, count);
    }

    [Fact]
    public void JobQueue_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify JobQueue.cs contains no .Wait() or .Result calls.
        string sourceFile = FindSourceFile("src/NoMercy.Queue/JobQueue.cs");
        string source = File.ReadAllText(sourceFile);

        // Check for .Result pattern (but exclude comments and string literals)
        string[] lines = source.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"\.\s*Result\b", trimmed);
        }

        // Check for .Wait() pattern
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"\.\s*Wait\s*\(", trimmed);
        }
    }

    [Fact]
    public void HomeController_SourceCode_NoBlockingWait()
    {
        // Static analysis: Verify HomeController.cs no longer uses Task.Delay().Wait().
        string sourceFile = FindSourceFile(
            "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"Task\.Delay\([^)]*\)\s*\.Wait\s*\(", trimmed);
        }
    }

    [Fact]
    public void MusicPlaybackService_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify MusicPlaybackService.cs has no .Wait() calls.
        string sourceFile = FindSourceFile(
            "src/NoMercy.Api/Services/Music/MusicPlaybackService.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"\)\s*\.Wait\s*\(", trimmed);
        }
    }

    [Fact]
    public void VideoPlaybackService_SourceCode_NoBlockingPatterns()
    {
        // Static analysis: Verify VideoPlaybackService.cs has no .Wait() calls.
        string sourceFile = FindSourceFile(
            "src/NoMercy.Api/Controllers/Socket/video/VideoPlaybackService.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            Assert.DoesNotMatch(@"\)\s*\.Wait\s*\(", trimmed);
        }
    }

    [Fact]
    public void HomeController_UsesAsyncDelay()
    {
        // Verify the HomeController now uses await Task.Delay instead of .Wait().
        string sourceFile = FindSourceFile(
            "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("await Task.Delay", source);
    }

    [Fact]
    public void HomeController_HasTimeout()
    {
        // Verify the HomeController polling loop has a timeout to prevent infinite waits.
        string sourceFile = FindSourceFile(
            "src/NoMercy.Api/Controllers/V1/Media/HomeController.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("CancelAfter", source);
    }

    private static string FindSourceFile(string relativePath)
    {
        // Walk up from the test assembly location to find the repo root
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;

            // Also check if we're in a well-known build output structure
            string repoCandidate = Path.Combine(dir, "..", "..", "..", "..", "..", relativePath);
            string resolved = Path.GetFullPath(repoCandidate);
            if (File.Exists(resolved)) return resolved;

            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: try from /workspaces/NoMercyMediaServer
        string fallback = Path.Combine("/workspaces/NoMercyMediaServer", relativePath);
        if (File.Exists(fallback)) return fallback;

        throw new FileNotFoundException($"Could not find source file: {relativePath}");
    }
}
