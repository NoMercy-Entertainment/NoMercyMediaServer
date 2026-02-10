using NoMercy.Queue;

namespace NoMercy.Tests.Queue.TestHelpers;

public class TestJob : IShouldQueue
{
    public string Message { get; set; } = string.Empty;
    public bool HasExecuted { get; set; } = false;
    public bool ShouldFail { get; set; } = false;
    public int ExecutionDelay { get; set; } = 0;

    public async Task Handle()
    {
        if (ExecutionDelay > 0)
        {
            await Task.Delay(ExecutionDelay);
        }

        if (ShouldFail)
        {
            throw new InvalidOperationException($"TestJob failed with message: {Message}");
        }

        HasExecuted = true;
    }
}

public class AnotherTestJob : IShouldQueue
{
    public int Value { get; set; }
    public bool HasExecuted { get; set; } = false;

    public async Task Handle()
    {
        await Task.Delay(1); // Minimal delay to simulate work
        HasExecuted = true;
        Value *= 2;
    }
}

/// <summary>
/// A type that does NOT implement IShouldQueue — used to test the safety gate
/// that prevents non-job types from executing in the queue worker.
/// </summary>
public class NotAJob
{
    public string Data { get; set; } = string.Empty;
}
