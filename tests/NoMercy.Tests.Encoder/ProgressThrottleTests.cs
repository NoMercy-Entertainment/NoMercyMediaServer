using NoMercy.Encoder.Core;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class ProgressThrottleTests
{
    /// <summary>
    /// The first call to ShouldSend should always return true
    /// since no previous update has been recorded.
    /// </summary>
    [Fact]
    public void ShouldSend_FirstCall_ReturnsTrue()
    {
        ProgressThrottle throttle = new(500);
        Assert.True(throttle.ShouldSend());
    }

    /// <summary>
    /// A second call immediately after the first should be throttled
    /// (return false) since the interval hasn't elapsed.
    /// </summary>
    [Fact]
    public void ShouldSend_ImmediateSecondCall_ReturnsFalse()
    {
        ProgressThrottle throttle = new(500);
        throttle.ShouldSend(); // first call — allowed
        Assert.False(throttle.ShouldSend()); // second call — throttled
    }

    /// <summary>
    /// After waiting longer than the interval, ShouldSend should
    /// return true again.
    /// </summary>
    [Fact]
    public async Task ShouldSend_AfterInterval_ReturnsTrue()
    {
        int intervalMs = 100; // short interval for test speed
        ProgressThrottle throttle = new(intervalMs);
        throttle.ShouldSend(); // first call
        await Task.Delay(intervalMs + 50); // wait past the interval
        Assert.True(throttle.ShouldSend());
    }

    /// <summary>
    /// Rapid-fire calls should only allow ~2 per second with 500ms interval.
    /// With 20 rapid calls, only the first should be allowed.
    /// </summary>
    [Fact]
    public void ShouldSend_RapidCalls_ThrottlesCorrectly()
    {
        ProgressThrottle throttle = new(500);
        int allowedCount = 0;
        for (int i = 0; i < 20; i++)
        {
            if (throttle.ShouldSend())
                allowedCount++;
        }

        Assert.Equal(1, allowedCount);
    }

    /// <summary>
    /// Reset should allow the next ShouldSend call to pass immediately.
    /// </summary>
    [Fact]
    public void Reset_AllowsNextSend()
    {
        ProgressThrottle throttle = new(500);
        throttle.ShouldSend(); // first call
        Assert.False(throttle.ShouldSend()); // throttled

        throttle.Reset();
        Assert.True(throttle.ShouldSend()); // allowed after reset
    }

    /// <summary>
    /// Multiple intervals should each allow exactly one send.
    /// Simulates the real-world pattern of ~100 FFmpeg updates per second
    /// being reduced to ~2 per second.
    /// </summary>
    [Fact]
    public async Task ShouldSend_OverMultipleIntervals_AllowsExpectedCount()
    {
        int intervalMs = 100;
        ProgressThrottle throttle = new(intervalMs);

        int allowedCount = 0;

        // Simulate 3 intervals worth of rapid calls
        for (int interval = 0; interval < 3; interval++)
        {
            // Rapid-fire 50 calls within this interval
            for (int i = 0; i < 50; i++)
            {
                if (throttle.ShouldSend())
                    allowedCount++;
            }
            await Task.Delay(intervalMs + 20);
        }

        // Should have allowed approximately 3 sends (one per interval)
        Assert.InRange(allowedCount, 3, 4);
    }

    /// <summary>
    /// Default interval should be 500ms.
    /// </summary>
    [Fact]
    public void DefaultInterval_Is500ms()
    {
        ProgressThrottle throttle = new();
        throttle.ShouldSend(); // first call at time T

        // A second call within <500ms should be throttled
        Assert.False(throttle.ShouldSend());
    }
}
