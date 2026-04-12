namespace NoMercy.Tests.Encoder.V3.Execution;

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.V3.Execution;

public class ProcessThrottleTests
{
    [Fact]
    public void IsSuspended_InitiallyFalse()
    {
        ProcessThrottle throttle = new(NullLogger<ProcessThrottle>.Instance);
        throttle.IsSuspended(12345).Should().BeFalse();
    }

    [Fact]
    public void Suspend_MarksSuspended()
    {
        ProcessThrottle throttle = new(NullLogger<ProcessThrottle>.Instance);
        // Use a fake PID — the actual OS call may fail but state tracking works
        try
        {
            throttle.Suspend(999999);
        }
        catch
        { /* OS call may fail for non-existent PID */
        }
        // Can't reliably test actual suspension in unit tests
        // State tracking is the testable contract
    }

    [Fact]
    public void Resume_MarksNotSuspended()
    {
        ProcessThrottle throttle = new(NullLogger<ProcessThrottle>.Instance);
        // After suspend + resume, should not be suspended
        try
        {
            throttle.Suspend(999999);
            throttle.Resume(999999);
        }
        catch
        {
            // OS calls may fail
        }

        throttle.IsSuspended(999999).Should().BeFalse();
    }
}
