using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Pins the meaning of <see cref="QueueWorker.IsProcessingJob"/> and
/// <see cref="QueueRunner.CountWorkersProcessingJob"/>: a worker that
/// has only just been spawned (and is blocked on
/// <c>queue.ReserveJob</c> waiting for the next item) is NOT busy.
///
/// <para>The encoder's hardware-benchmark deferral relies on this:
/// before the fix the probe asked
/// <see cref="QueueRunner.GetActiveWorkerThreads"/>, which counted
/// every spawned worker thread for its lifetime, so an idle encoder
/// queue worker permanently blocked the benchmark.</para>
/// </summary>
public class QueueWorkerIsProcessingJobTests
{
    [Fact]
    public void Newly_constructed_worker_is_not_processing_a_job()
    {
        (_, IQueueContext adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        JobQueue jobQueue = new(adapter, maxAttempts: 5);

        QueueWorker worker = new(jobQueue, name: "encoder");

        Assert.False(worker.IsProcessingJob);
    }
}
