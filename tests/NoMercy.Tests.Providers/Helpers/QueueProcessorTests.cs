using ProviderQueue = NoMercy.Providers.Helpers.Queue;
using NoMercy.Providers.Helpers;

namespace NoMercy.Tests.Providers.Helpers;

public class QueueProcessorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Queue_ContinuesProcessing_AfterTransientError()
    {
        ProviderQueue queue = new(new() { Concurrent = 1, Interval = 10, Start = true });

        int callCount = 0;
        List<Exception> rejectedErrors = [];
        queue.Reject += (_, args) =>
        {
            if (args.Error is not null)
                rejectedErrors.Add(args.Error);
        };

        // First task throws
        try
        {
            await queue.Enqueue<string>(async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(1);
                throw new InvalidOperationException("Transient failure");
            }, "http://test/fail");
        }
        catch (InvalidOperationException)
        {
            // Expected — Enqueue propagates via TaskCompletionSource
        }

        // Second task should still work (queue continues processing)
        string result = await queue.Enqueue<string>(async () =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(1);
            return "success";
        }, "http://test/ok");

        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Queue_RejectsFailedTasks_WithErrorEvent()
    {
        ProviderQueue queue = new(new() { Concurrent = 1, Interval = 10, Start = true });

        List<Exception> rejectedErrors = [];
        queue.Reject += (_, args) =>
        {
            if (args.Error is not null)
                rejectedErrors.Add(args.Error);
        };

        InvalidOperationException thrownException = new("Test error for rejection");

        try
        {
            await queue.Enqueue<string>(async () =>
            {
                await Task.Delay(1);
                throw thrownException;
            }, "http://test/reject");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        rejectedErrors.Should().ContainSingle()
            .Which.Message.Should().Be("Test error for rejection");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Queue_ProcessesMultipleTasks_InOrder()
    {
        ProviderQueue queue = new(new() { Concurrent = 1, Interval = 10, Start = true });

        List<int> executionOrder = [];

        int result1 = await queue.Enqueue<int>(async () =>
        {
            await Task.Delay(1);
            lock (executionOrder) executionOrder.Add(1);
            return 1;
        }, "http://test/1");

        int result2 = await queue.Enqueue<int>(async () =>
        {
            await Task.Delay(1);
            lock (executionOrder) executionOrder.Add(2);
            return 2;
        }, "http://test/2");

        result1.Should().Be(1);
        result2.Should().Be(2);
        executionOrder.Should().ContainInOrder(1, 2);
    }
}
