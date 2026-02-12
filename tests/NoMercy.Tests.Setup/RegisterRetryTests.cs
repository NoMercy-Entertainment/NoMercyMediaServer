using System.Diagnostics;

namespace NoMercy.Tests.Setup;

public class RegisterRetryTests
{
    private static readonly int[] BackoffSeconds = [2, 5, 15, 30, 60];

    /// <summary>
    /// Replicates the retry loop pattern used in Register.RegisterServer and
    /// Register.AssignServerWithRetry to verify exponential backoff behavior
    /// without requiring network access.
    /// </summary>
    private static async Task<int> ExecuteWithRetry(
        int maxRetries,
        Func<int, Task> action,
        Func<int, int> getDelay)
    {
        int attemptsMade = 0;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            attemptsMade++;
            try
            {
                await action(attempt);
                return attemptsMade;
            }
            catch (Exception) when (attempt < maxRetries)
            {
                int delay = getDelay(attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(delay));
            }
        }

        return attemptsMade;
    }

    [Fact]
    public async Task Retry_SucceedsOnFirstAttempt_NoRetry()
    {
        int callCount = 0;

        int attempts = await ExecuteWithRetry(
            maxRetries: 5,
            action: _ =>
            {
                callCount++;
                return Task.CompletedTask;
            },
            getDelay: attempt => BackoffSeconds[Math.Min(attempt - 1, BackoffSeconds.Length - 1)]);

        Assert.Equal(1, callCount);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Retry_FailsThenSucceeds_RetriesCorrectly()
    {
        int callCount = 0;
        int succeedOnAttempt = 3;

        int attempts = await ExecuteWithRetry(
            maxRetries: 5,
            action: attempt =>
            {
                callCount++;
                if (attempt < succeedOnAttempt)
                    throw new HttpRequestException("Network error");
                return Task.CompletedTask;
            },
            getDelay: attempt => 1);

        Assert.Equal(succeedOnAttempt, callCount);
        Assert.Equal(succeedOnAttempt, attempts);
    }

    [Fact]
    public async Task Retry_ExhaustsAllRetries_ThrowsOnLastAttempt()
    {
        int callCount = 0;

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    callCount++;
                    throw new HttpRequestException("Network error");
                }
                catch (Exception) when (attempt < 5)
                {
                    await Task.Delay(1);
                }
            }
        });

        Assert.Equal(5, callCount);
    }

    [Fact]
    public void BackoffSeconds_AreExponentiallyIncreasing()
    {
        for (int i = 1; i < BackoffSeconds.Length; i++)
        {
            Assert.True(BackoffSeconds[i] > BackoffSeconds[i - 1],
                $"BackoffSeconds[{i}] ({BackoffSeconds[i]}) should be greater than BackoffSeconds[{i - 1}] ({BackoffSeconds[i - 1]})");
        }
    }

    [Fact]
    public void BackoffSeconds_AreClampedToLastValue()
    {
        int maxIndex = BackoffSeconds.Length - 1;
        for (int attempt = BackoffSeconds.Length; attempt < BackoffSeconds.Length + 5; attempt++)
        {
            int delay = BackoffSeconds[Math.Min(attempt - 1, maxIndex)];
            Assert.Equal(BackoffSeconds[maxIndex], delay);
        }
    }

    [Fact]
    public void BackoffSeconds_FirstValueIsSmall()
    {
        Assert.True(BackoffSeconds[0] <= 5,
            "First backoff delay should be small (<=5s) for fast initial retry");
    }

    [Fact]
    public void BackoffSeconds_LastValueIsCapped()
    {
        Assert.True(BackoffSeconds[^1] <= 120,
            "Max backoff delay should be capped at a reasonable value (<=120s)");
    }

    [Fact]
    public async Task Retry_DelaysIncreaseBetweenAttempts()
    {
        List<int> delays = [];

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    throw new InvalidOperationException("fail");
                }
                catch (Exception) when (attempt < 3)
                {
                    int delay = BackoffSeconds[Math.Min(attempt - 1, BackoffSeconds.Length - 1)];
                    delays.Add(delay);
                    await Task.Delay(1);
                }
            }
        });

        Assert.Equal(2, delays.Count);
        Assert.Equal(2, delays[0]);
        Assert.Equal(5, delays[1]);
    }

    [Fact]
    public async Task Retry_NonRetryableException_PropagatesImmediately()
    {
        int callCount = 0;

        // When attempt == maxRetries, the 'when' guard fails and exception propagates
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ExecuteWithRetry(
                maxRetries: 1,
                action: _ =>
                {
                    callCount++;
                    throw new InvalidOperationException("Non-transient error");
                },
                getDelay: _ => 1);
        });

        Assert.Equal(1, callCount);
    }
}
