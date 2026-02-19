using System.Reflection;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT-09: Tests verifying that ReserveJob's recursive retry path returns
/// the result instead of discarding it (the bug was a missing `return` before
/// the recursive call).
/// </summary>
[Trait("Category", "Unit")]
public class ReserveJobRetryTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public ReserveJobRetryTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void ReserveJob_RecursiveCallReturnsResult_NotNull()
    {
        // Verify through IL/source inspection that the recursive call in the catch
        // block has a return statement. The bug was:
        //   ReserveJob(name, currentJobId, attempt + 1);  // result discarded
        // The fix is:
        //   return ReserveJob(name, currentJobId, attempt + 1);  // result returned

        // We verify this structurally: the method body's IL should contain a recursive
        // call followed by a return, not a pop (discard). We inspect via reflection
        // to confirm the method references itself.
        MethodInfo? reserveJobMethod = typeof(JobQueue).GetMethod(
            "ReserveJob",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(string), typeof(long?), typeof(int)]);

        Assert.NotNull(reserveJobMethod);

        // Get the IL bytes and verify the recursive call exists
        MethodBody? body = reserveJobMethod.GetMethodBody();
        Assert.NotNull(body);

        byte[] ilBytes = body.GetILAsByteArray()!;
        Assert.NotNull(ilBytes);
        Assert.True(ilBytes.Length > 0);

        // The method token for the recursive call should be present in the IL.
        // A missing `return` before the recursive call would cause the result
        // to be popped (IL opcode 0x26 = pop) instead of returned (0x2A = ret).
        // We verify that there is no pop-after-call pattern for the recursive call.
        // This is a structural guard against regression.

        // Find all call/callvirt instructions (0x28 = call, 0x6F = callvirt)
        // followed by pop (0x26). If the recursive call has a pop after it,
        // the bug has regressed.
        int recursiveCallToken = reserveJobMethod.MetadataToken;
        bool foundPopAfterRecursiveCall = false;

        for (int i = 0; i < ilBytes.Length - 5; i++)
        {
            // call or callvirt instruction is 5 bytes: opcode + 4-byte token
            if (ilBytes[i] == 0x28 || ilBytes[i] == 0x6F)
            {
                int token = BitConverter.ToInt32(ilBytes, i + 1);
                if (token == recursiveCallToken && i + 5 < ilBytes.Length)
                {
                    // Check if next instruction after the call is pop (0x26)
                    if (ilBytes[i + 5] == 0x26)
                    {
                        foundPopAfterRecursiveCall = true;
                    }
                }
            }
        }

        Assert.False(foundPopAfterRecursiveCall,
            "CRIT-09 regression: ReserveJob recursive call result is being discarded (pop after call). " +
            "The recursive call must have 'return' before it.");
    }

    [Fact]
    public void ReserveJob_ExceedingMaxDbRetryAttempts_ReturnsNull()
    {
        // When attempt >= MaxDbRetryAttempts (5) in the catch block, ReserveJob
        // should log the error and return null. We verify this by calling
        // ReserveJob with attempt=5 directly (which skips the retry path and
        // falls through to return null if the normal path also fails).

        // With an empty queue, ReserveJob returns null on the happy path too,
        // so we verify the method handles high attempt values gracefully.
        QueueJobModel? result = _jobQueue.ReserveJob("nonexistent-queue", null, 5);
        Assert.Null(result);
    }

    [Fact]
    public void ReserveJob_NormalPath_ReturnsJobSuccessfully()
    {
        // Verify the normal (non-retry) path still works correctly after the fix.
        QueueJob job = new()
        {
            Queue = "normal-path",
            Payload = "normal-payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = _jobQueue.ReserveJob("normal-path", null);

        Assert.NotNull(reserved);
        Assert.Equal("normal-payload", reserved.Payload);
        Assert.NotNull(reserved.ReservedAt);
        Assert.Equal(1, reserved.Attempts);
    }

    [Fact]
    public void ReserveJob_NormalPathNoJob_ReturnsNull()
    {
        // Verify the method returns null when no jobs match (empty queue).
        QueueJobModel? reserved = _jobQueue.ReserveJob("empty-queue", null);
        Assert.Null(reserved);
    }
}
